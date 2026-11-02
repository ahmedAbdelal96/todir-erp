using Core.Transactions;
using Core.Services;
using Modules.Hr.DTOs;
using Modules.Hr.Entities;
using Npgsql;
using NpgsqlTypes;
using Shared.Primitives.Identifiers;

namespace Modules.Hr.Repositories;

public sealed class HrImportRepository(ITransactionCoordinator transactions, ITransactionalSequenceAllocator sequences, TimeProvider clock)
{
    private static readonly IReadOnlyList<string> PossibleDuplicateWarning = Array.AsReadOnly(["hr.import_possible_duplicate"]);

    public async Task<HrEmployeeImportSummary> PreviewEmployeesAsync(TenantId tenant, HrEmployeeImportRequest request, CancellationToken ct)
    {
        var validated = Validate(request);
        if (validated.Values.All(issues => issues.Count > 0)) return RejectedSummary(request, validated);
        return await PreviewCoreAsync(tenant, request, validated, ct);
    }

    private Task<HrEmployeeImportSummary> PreviewCoreAsync(TenantId tenant, HrEmployeeImportRequest request,
        IReadOnlyDictionary<string, IReadOnlyList<string>> validation, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var results = new List<HrEmployeeImportRowResult>(request.Rows.Count);
            var eligibleRows = request.Rows.Where(row => validation.GetValueOrDefault(row.RowReference ?? string.Empty, []).Count == 0).ToArray();
            var matchByRow = new Dictionary<string, (Guid? EmployeeId, string? PayloadHash, string? FileHash, string? ExistingRowReference, string? BatchReference, bool PossibleDuplicate, bool MissingBranch, bool MissingDepartment, bool MissingPosition, bool MissingManager)>(StringComparer.Ordinal);
            if (eligibleRows.Length > 0)
            {
                var input = System.Text.Json.JsonSerializer.Serialize(eligibleRows.Select(row => new
                {
                    row_reference = row.RowReference,
                    legacy_employee_id = row.LegacyEmployeeId.Trim(),
                    payload_sha256 = EmployeeImportRowHash(row),
                    arabic_normalized = HrSearchNormalization.Normalize(row.ArabicName),
                    english_normalized = string.IsNullOrWhiteSpace(row.EnglishName) ? null : HrSearchNormalization.Normalize(row.EnglishName),
                    legal_entity_id = row.LegalEntityId,
                    branch_code = string.IsNullOrWhiteSpace(row.BranchCode) ? null : row.BranchCode.Trim(),
                    department_code = string.IsNullOrWhiteSpace(row.DepartmentCode) ? null : row.DepartmentCode.Trim(),
                    position_code = string.IsNullOrWhiteSpace(row.PositionCode) ? null : row.PositionCode.Trim(),
                    manager_legacy_employee_id = string.IsNullOrWhiteSpace(row.ManagerLegacyEmployeeId) ? null : row.ManagerLegacyEmployeeId.Trim(),
                    hire_date = row.HireDate,
                    manager_in_batch = !string.IsNullOrWhiteSpace(row.ManagerLegacyEmployeeId) && request.Rows.Any(candidate => string.Equals(candidate.LegacyEmployeeId?.Trim(), row.ManagerLegacyEmployeeId.Trim(), StringComparison.Ordinal))
                }));
                await using var matches = new NpgsqlCommand("""
                    with incoming as materialized (
                        select * from jsonb_to_recordset($2::jsonb) as x(row_reference text,legacy_employee_id text,payload_sha256 text,arabic_normalized text,english_normalized text,legal_entity_id uuid,branch_code text,department_code text,position_code text,manager_legacy_employee_id text,hire_date date,manager_in_batch boolean)
                    )
                    select incoming.row_reference, reference.employee_id, reference.row_payload_sha256, reference.file_sha256, reference.row_reference, reference.batch_reference,
                        exists(select 1 from business_hr.employee employee where employee.tenant_id=$1 and employee.status<>'Archived'
                            and (employee.arabic_name_normalized=incoming.arabic_normalized or (incoming.english_normalized is not null and employee.english_name_normalized=incoming.english_normalized))),
                        incoming.branch_code is not null and not exists(select 1 from platform.branch branch where branch.tenant_id=$1 and branch.legal_entity_id=incoming.legal_entity_id and branch.code=incoming.branch_code and branch.status='Active'),
                        incoming.department_code is not null and not exists(select 1 from business_hr.department department where department.tenant_id=$1 and department.legal_entity_id=incoming.legal_entity_id and department.code=incoming.department_code and department.status='Active'),
                        incoming.position_code is not null and not exists(select 1 from business_hr.position position where position.tenant_id=$1 and position.legal_entity_id=incoming.legal_entity_id and position.code=incoming.position_code and position.status='Active'),
                        incoming.manager_legacy_employee_id is not null and not incoming.manager_in_batch and not exists(
                            select 1 from business_hr.employee_import_reference manager_reference
                            join business_hr.employment manager_employment on manager_employment.tenant_id=manager_reference.tenant_id and manager_employment.id=manager_reference.employment_id
                            where manager_reference.tenant_id=$1 and manager_reference.source_system=$3 and manager_reference.legacy_employee_id=incoming.manager_legacy_employee_id
                                and manager_employment.legal_entity_id=incoming.legal_entity_id and manager_employment.start_date<=incoming.hire_date
                                and (manager_employment.end_date is null or manager_employment.end_date>=incoming.hire_date))
                    from incoming
                    left join business_hr.employee_import_reference reference on reference.tenant_id=$1 and reference.source_system=$3 and reference.legacy_employee_id=incoming.legacy_employee_id
                    """, context.Connection, context.Transaction);
                matches.Parameters.AddWithValue(tenant.Value);
                matches.Parameters.Add(new NpgsqlParameter { NpgsqlDbType = NpgsqlDbType.Jsonb, Value = input });
                matches.Parameters.AddWithValue(request.SourceSystem.Trim());
                await using var reader = await matches.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token))
                    matchByRow[reader.GetString(0)] = (reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetBoolean(6), reader.GetBoolean(7), reader.GetBoolean(8), reader.GetBoolean(9), reader.GetBoolean(10));
            }
            var repeatedArabicNames = request.Rows.Where(row => !string.IsNullOrWhiteSpace(row.ArabicName))
                .GroupBy(row => HrSearchNormalization.Normalize(row.ArabicName), StringComparer.Ordinal)
                .Where(group => group.Count() > 1).SelectMany(group => group.Select(row => row.RowReference)).ToHashSet(StringComparer.Ordinal);
            foreach (var row in request.Rows)
            {
                var rowReference = row.RowReference ?? string.Empty;
                var issues = validation.GetValueOrDefault(rowReference, []).ToList();
                if (issues.Count == 0)
                {
                    var match = matchByRow[rowReference];
                    if (match.EmployeeId is { } employeeId)
                    {
                        var samePayload = match.PayloadHash is not null
                            ? string.Equals(match.PayloadHash, EmployeeImportRowHash(row), StringComparison.Ordinal)
                            : string.Equals(match.FileHash, request.FileSha256, StringComparison.OrdinalIgnoreCase)
                                && string.Equals(match.ExistingRowReference, rowReference, StringComparison.Ordinal)
                                && string.Equals(match.BatchReference, request.BatchReference.Trim(), StringComparison.Ordinal);
                        results.Add(samePayload
                            ? new(rowReference, "AlreadyImported", employeeId, null, [])
                            : new(rowReference, "Rejected", null, null, ["hr.import_row_conflict"]));
                        continue;
                    }
                    var migrationStrategy = await GetMigrationStrategyAsync(context.Connection, context.Transaction, tenant, row.LegalEntityId, false, token);
                    if (migrationStrategy is null) issues.Add("hr.go_live_configuration_required");
                    else if (migrationStrategy == "ActiveOnly" && row.Status is EmploymentStatus.Terminated or EmploymentStatus.Ended) issues.Add("hr.employee_migration_history_not_allowed");
                    if (match.MissingBranch) issues.Add("hr.import_branch_not_found");
                    if (match.MissingDepartment) issues.Add("hr.import_department_not_found");
                    if (match.MissingPosition) issues.Add("hr.import_position_not_found");
                    if (match.MissingManager) issues.Add("hr.import_manager_not_found");
                    if (issues.Count > 0)
                    {
                        results.Add(new(rowReference, "Rejected", null, null, issues));
                        continue;
                    }
                    var warnings = new List<string>();
                    if (match.PossibleDuplicate) warnings.AddRange(PossibleDuplicateWarning);
                    if (repeatedArabicNames.Contains(rowReference)) warnings.Add("hr.import_duplicate_name_in_batch");
                    results.Add(new(rowReference, warnings.Count == 0 ? "Ready" : "ReadyWithWarnings", null, null, [], warnings));
                }
                else results.Add(new(rowReference, "Rejected", null, null, issues));
            }
            var imported = results.Count(item => item.Status == "AlreadyImported");
            return new HrEmployeeImportSummary(results.Count, 0, imported, results.Count(item => item.Status == "Rejected"), results);
        }, ct);

    public async Task<HrEmployeeImportSummary> CommitEmployeesAsync(TenantId tenant, Guid actor, Guid membershipId,
        Guid correlationId, HrEmployeeImportRequest request, CancellationToken ct)
    {
        var validation = Validate(request);
        if (validation.Values.All(issues => issues.Count > 0)) return RejectedSummary(request, validation);
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var results = new List<HrEmployeeImportRowResult>(request.Rows.Count);
            foreach (var row in OrderByManager(request.Rows))
            {
                var rowReference = row.RowReference ?? string.Empty;
                var issues = validation.GetValueOrDefault(rowReference, []).ToList();
                if (issues.Count > 0)
                {
                    results.Add(new(rowReference, "Rejected", null, null, issues));
                    continue;
                }

                await using (var find = new NpgsqlCommand("select employee_id,employment_id,row_payload_sha256,file_sha256,row_reference,batch_reference from business_hr.employee_import_reference where tenant_id=$1 and source_system=$2 and legacy_employee_id=$3", context.Connection, context.Transaction))
                {
                    find.Parameters.AddWithValue(tenant.Value); find.Parameters.AddWithValue(request.SourceSystem.Trim()); find.Parameters.AddWithValue(row.LegacyEmployeeId.Trim());
                    await using var reader = await find.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow, token);
                    if (await reader.ReadAsync(token))
                    {
                        var samePayload = reader.IsDBNull(2)
                            ? string.Equals(reader.GetString(3), request.FileSha256, StringComparison.OrdinalIgnoreCase)
                                && string.Equals(reader.GetString(4), rowReference, StringComparison.Ordinal)
                                && string.Equals(reader.GetString(5), request.BatchReference.Trim(), StringComparison.Ordinal)
                            : string.Equals(reader.GetString(2), EmployeeImportRowHash(row), StringComparison.Ordinal);
                        results.Add(samePayload
                            ? new(rowReference, "AlreadyImported", reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), [])
                            : new(rowReference, "Rejected", null, null, ["hr.import_row_conflict"]));
                        continue;
                    }
                }

                var migrationStrategy = await GetMigrationStrategyAsync(context.Connection, context.Transaction, tenant, row.LegalEntityId, true, token);
                if (migrationStrategy is null)
                {
                    results.Add(new(rowReference, "Rejected", null, null, ["hr.go_live_configuration_required"]));
                    continue;
                }
                if (migrationStrategy == "ActiveOnly" && row.Status is EmploymentStatus.Terminated or EmploymentStatus.Ended)
                {
                    results.Add(new(rowReference, "Rejected", null, null, ["hr.employee_migration_history_not_allowed"]));
                    continue;
                }

                await context.Transaction.SaveAsync("hr_import_row", token);
                try
                {
                    var employee = Employee.Create(Guid.CreateVersion7(), tenant.Value, row.ArabicName, row.EnglishName, row.PreferredName, null);
                    var now = clock.GetUtcNow();
                    await using (var insert = new NpgsqlCommand("insert into business_hr.employee(tenant_id,id,arabic_name,english_name,preferred_name,display_name,display_name_normalized,arabic_name_normalized,english_name_normalized,status,membership_id,created_by,created_at,version) values($1,$2,$3,$4,$5,$6,$7,$8,$9,'Active',null,$10,$11,1)", context.Connection, context.Transaction))
                    {
                        insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(employee.Id); insert.Parameters.AddWithValue(employee.ArabicName);
                        insert.Parameters.AddWithValue((object?)employee.EnglishName ?? DBNull.Value); insert.Parameters.AddWithValue((object?)employee.PreferredName ?? DBNull.Value);
                        insert.Parameters.AddWithValue(employee.DisplayName); insert.Parameters.AddWithValue(HrSearchNormalization.Normalize(employee.DisplayName)); insert.Parameters.AddWithValue(HrSearchNormalization.Normalize(employee.ArabicName));
                        insert.Parameters.AddWithValue(employee.EnglishName is null ? DBNull.Value : HrSearchNormalization.Normalize(employee.EnglishName)); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(now);
                        await insert.ExecuteNonQueryAsync(token);
                    }
                    var employmentId = Guid.CreateVersion7();
                    var employmentNumber = await sequences.AllocateAsync(context, new SequenceDefinition("hr.employee-number", tenant, row.LegalEntityId, null, null, null, "EMP-", 6, 1), token);
                    var employment = Employment.Create(employmentId, tenant.Value, employee.Id, row.LegalEntityId, employmentNumber, row.EmploymentType, row.HireDate, row.EndDate, DateOnly.FromDateTime(now.UtcDateTime));
                    if (!Enum.IsDefined(row.Status)) throw new HrException("hr.import_employment_status_invalid");
                    await using (var insert = new NpgsqlCommand("insert into business_hr.employment(tenant_id,id,employee_id,legal_entity_id,employment_number,employment_type,status,start_date,end_date,created_by,created_at,version) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,1)", context.Connection, context.Transaction))
                    {
                        insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(employmentId); insert.Parameters.AddWithValue(employee.Id); insert.Parameters.AddWithValue(row.LegalEntityId);
                        insert.Parameters.AddWithValue(employmentNumber); insert.Parameters.AddWithValue(row.EmploymentType.ToString()); insert.Parameters.AddWithValue(row.Status.ToString()); insert.Parameters.AddWithValue(row.HireDate);
                        insert.Parameters.AddWithValue((object?)row.EndDate ?? DBNull.Value); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(now); await insert.ExecuteNonQueryAsync(token);
                    }
                    if (HasAssignmentReference(row))
                    {
                        var refs = await ResolveAssignmentReferencesAsync(context.Connection, context.Transaction, tenant, request.SourceSystem.Trim(), row, token);
                        if (row.BranchCode is not null && refs.BranchId is null) throw new HrException("hr.import_branch_not_found");
                        if (row.DepartmentCode is not null && refs.DepartmentId is null) throw new HrException("hr.import_department_not_found");
                        if (row.PositionCode is not null && refs.PositionId is null) throw new HrException("hr.import_position_not_found");
                        if (row.ManagerLegacyEmployeeId is not null && refs.ManagerEmploymentId is null) throw new HrException("hr.import_manager_not_found");
                        await using var assignment = new NpgsqlCommand("insert into business_hr.employment_assignment(id,tenant_id,legal_entity_id,employment_id,branch_id,department_id,position_id,manager_employment_id,effective_from,effective_to,reason_code,created_by,created_at,version) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,'legacy_import',$11,$12,1)", context.Connection, context.Transaction);
                        assignment.Parameters.AddWithValue(Guid.CreateVersion7()); assignment.Parameters.AddWithValue(tenant.Value); assignment.Parameters.AddWithValue(row.LegalEntityId); assignment.Parameters.AddWithValue(employmentId);
                        assignment.Parameters.AddWithValue((object?)refs.BranchId ?? DBNull.Value); assignment.Parameters.AddWithValue((object?)refs.DepartmentId ?? DBNull.Value); assignment.Parameters.AddWithValue((object?)refs.PositionId ?? DBNull.Value);
                        assignment.Parameters.AddWithValue((object?)refs.ManagerEmploymentId ?? DBNull.Value); assignment.Parameters.AddWithValue(row.HireDate);
                        assignment.Parameters.AddWithValue((object?)(row.EndDate?.AddDays(1)) ?? DBNull.Value); assignment.Parameters.AddWithValue(actor); assignment.Parameters.AddWithValue(now);
                        await assignment.ExecuteNonQueryAsync(token);
                    }
                    await using (var reference = new NpgsqlCommand("insert into business_hr.employee_import_reference(tenant_id,id,source_system,legacy_employee_id,batch_reference,file_sha256,row_reference,row_payload_sha256,employee_id,employment_id,data_quality,imported_at,imported_by) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13)", context.Connection, context.Transaction))
                    {
                        reference.Parameters.AddWithValue(tenant.Value); reference.Parameters.AddWithValue(Guid.CreateVersion7()); reference.Parameters.AddWithValue(request.SourceSystem.Trim()); reference.Parameters.AddWithValue(row.LegacyEmployeeId.Trim());
                        reference.Parameters.AddWithValue(request.BatchReference.Trim()); reference.Parameters.AddWithValue(request.FileSha256!.ToLowerInvariant()); reference.Parameters.AddWithValue(rowReference.Trim());
                        reference.Parameters.AddWithValue(EmployeeImportRowHash(row)); reference.Parameters.AddWithValue(employee.Id); reference.Parameters.AddWithValue(employmentId); reference.Parameters.AddWithValue(row.DataQuality); reference.Parameters.AddWithValue(now); reference.Parameters.AddWithValue(actor);
                        await reference.ExecuteNonQueryAsync(token);
                    }
                    await using (var projection = new NpgsqlCommand("insert into business_hr.employee_list_projection(tenant_id,employee_id,display_name,display_name_normalized,arabic_name_normalized,english_name,english_name_normalized,status,projected_at) values($1,$2,$3,$4,$5,$6,$7,'Active',$8)", context.Connection, context.Transaction))
                    {
                        projection.Parameters.AddWithValue(tenant.Value); projection.Parameters.AddWithValue(employee.Id); projection.Parameters.AddWithValue(employee.DisplayName);
                        projection.Parameters.AddWithValue(HrSearchNormalization.Normalize(employee.DisplayName)); projection.Parameters.AddWithValue(HrSearchNormalization.Normalize(employee.ArabicName));
                        projection.Parameters.AddWithValue((object?)employee.EnglishName ?? DBNull.Value); projection.Parameters.AddWithValue(employee.EnglishName is null ? DBNull.Value : HrSearchNormalization.Normalize(employee.EnglishName)); projection.Parameters.AddWithValue(now);
                        await projection.ExecuteNonQueryAsync(token);
                    }
                    var importEventId = Guid.CreateVersion7();
                    var importPayload = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        legacySystem = request.SourceSystem.Trim(), row.DataQuality,
                        branchCode = row.BranchCode?.Trim(), departmentCode = row.DepartmentCode?.Trim(),
                        positionCode = row.PositionCode?.Trim()
                    });
                    await using (var eventInsert = new NpgsqlCommand("insert into business_hr.employment_event(tenant_id,id,employee_id,employment_id,event_type,effective_on,actor_id,payload,correlation_id,occurred_at) values($1,$2,$3,$4,'EmployeeImported',$5,$6,$7::jsonb,$8,$9)", context.Connection, context.Transaction))
                    {
                        eventInsert.Parameters.AddWithValue(tenant.Value); eventInsert.Parameters.AddWithValue(importEventId); eventInsert.Parameters.AddWithValue(employee.Id); eventInsert.Parameters.AddWithValue(employmentId);
                        eventInsert.Parameters.AddWithValue(row.HireDate); eventInsert.Parameters.AddWithValue(actor); eventInsert.Parameters.AddWithValue(importPayload); eventInsert.Parameters.AddWithValue(correlationId); eventInsert.Parameters.AddWithValue(now);
                        await eventInsert.ExecuteNonQueryAsync(token);
                    }
                    await using (var timeline = new NpgsqlCommand("insert into business_hr.employee_timeline_projection(tenant_id,id,employee_id,employment_id,event_type,effective_on,occurred_at,payload) values($1,$2,$3,$4,'EmployeeImported',$5,$6,$7::jsonb)", context.Connection, context.Transaction))
                    {
                        timeline.Parameters.AddWithValue(tenant.Value); timeline.Parameters.AddWithValue(importEventId); timeline.Parameters.AddWithValue(employee.Id); timeline.Parameters.AddWithValue(employmentId);
                        timeline.Parameters.AddWithValue(row.HireDate); timeline.Parameters.AddWithValue(now); timeline.Parameters.AddWithValue(importPayload); await timeline.ExecuteNonQueryAsync(token);
                    }
                    await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction,
                        new AuditEntry(Guid.CreateVersion7(), tenant, row.LegalEntityId, null, "Human", actor, membershipId, "hr", "employee.imported", "employee", employee.Id,
                            null, System.Text.Json.JsonSerializer.Serialize(new { request.SourceSystem, request.BatchReference, row.RowReference, employmentNumber, row.DataQuality }),
                            null, "hr-api", correlationId, System.Diagnostics.Activity.Current?.TraceId.ToString(), now, null), token);
                    await context.Transaction.ReleaseAsync("hr_import_row", token);
                    results.Add(new(rowReference, "Imported", employee.Id, employmentId, []));
                }
                catch (Exception exception) when (exception is HrException or PostgresException { SqlState: "23503" or "23505" or "23514" or "23P01" })
                {
                    await context.Transaction.RollbackAsync("hr_import_row", token);
                    await context.Transaction.ReleaseAsync("hr_import_row", token);
                    var issue = exception is HrException ? "hr.import_row_invalid" : exception is PostgresException { SqlState: "23503" } ? "hr.import_reference_invalid" : "hr.import_row_conflict";
                    results.Add(new(rowReference, "Rejected", null, null, [issue]));
                }
            }
            return new HrEmployeeImportSummary(results.Count, results.Count(item => item.Status == "Imported"), results.Count(item => item.Status == "AlreadyImported"), results.Count(item => item.Status == "Rejected"), results);
        }, ct);
    }

    private Dictionary<string, IReadOnlyList<string>> Validate(HrEmployeeImportRequest request)
    {
        if (request.Rows is null || request.Rows.Count is 0 or > 500) throw new HrException("hr.import_row_count_invalid");
        if (request.Rows.Any(row => row is null)) throw new HrException("hr.import_rows_invalid");
        if (string.IsNullOrWhiteSpace(request.SourceSystem) || request.SourceSystem.Trim().Length > 80 || string.IsNullOrWhiteSpace(request.BatchReference) || request.BatchReference.Trim().Length > 160)
            throw new HrException("hr.import_source_invalid");
        if (request.FileSha256 is null || request.FileSha256.Length != 64 || !request.FileSha256.All(Uri.IsHexDigit)) throw new HrException("hr.import_file_hash_invalid");
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var rowRefs = new HashSet<string>(StringComparer.Ordinal);
        var legacyIds = new HashSet<string>(StringComparer.Ordinal);
        var asOfDate = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        foreach (var row in request.Rows)
        {
            var issues = new List<string>();
            if (string.IsNullOrWhiteSpace(row.RowReference) || row.RowReference.Trim().Length > 160 || !rowRefs.Add(row.RowReference.Trim())) issues.Add("hr.import_row_reference_invalid");
            if (string.IsNullOrWhiteSpace(row.LegacyEmployeeId) || row.LegacyEmployeeId.Trim().Length > 160 || !legacyIds.Add(row.LegacyEmployeeId.Trim())) issues.Add("hr.import_legacy_employee_id_duplicate");
            if (string.IsNullOrWhiteSpace(row.ArabicName) || row.ArabicName.Trim().Length > 200) issues.Add("hr.employee_arabic_name_invalid");
            if (row.EnglishName?.Trim().Length > 200 || row.PreferredName?.Trim().Length > 200) issues.Add("hr.employee_name_too_long");
            if (row.LegalEntityId == Guid.Empty || !Enum.IsDefined(row.EmploymentType) || !Enum.IsDefined(row.Status) || row.HireDate == default || row.EndDate < row.HireDate) issues.Add("hr.import_employment_invalid");
            if (row.Status == EmploymentStatus.PreHire && row.HireDate <= asOfDate) issues.Add("hr.import_status_date_mismatch");
            if ((row.Status is EmploymentStatus.Active or EmploymentStatus.Suspended or EmploymentStatus.NoticePeriod)
                && (row.HireDate > asOfDate || row.EndDate < asOfDate)) issues.Add("hr.import_status_date_mismatch");
            if ((row.Status is EmploymentStatus.Terminated or EmploymentStatus.Ended)
                && (row.EndDate is null && row.DataQuality != "UnknownHistory" || row.EndDate > asOfDate)) issues.Add("hr.import_status_date_mismatch");
            if (HasAssignmentReference(row) && (row.Status is EmploymentStatus.Terminated or EmploymentStatus.Ended) && row.EndDate is null)
                issues.Add("hr.import_assignment_end_date_required");
            if (row.EndDate == DateOnly.MaxValue) issues.Add("hr.employment_date_range_invalid");
            if (row.DataQuality is not ("Complete" or "Partial" or "UnknownHistory")) issues.Add("hr.import_data_quality_invalid");
            if (row.BranchCode is not null && (string.IsNullOrWhiteSpace(row.BranchCode) || row.BranchCode.Trim().Length > 40)
                || row.DepartmentCode is not null && (string.IsNullOrWhiteSpace(row.DepartmentCode) || row.DepartmentCode.Trim().Length > 40)
                || row.PositionCode is not null && (string.IsNullOrWhiteSpace(row.PositionCode) || row.PositionCode.Trim().Length > 40)
                || row.ManagerLegacyEmployeeId is not null && (string.IsNullOrWhiteSpace(row.ManagerLegacyEmployeeId) || row.ManagerLegacyEmployeeId.Trim().Length > 160))
                issues.Add("hr.import_assignment_reference_invalid");
            errors[row.RowReference ?? string.Empty] = issues;
        }
        var rowsByLegacyId = request.Rows.Where(row => !string.IsNullOrWhiteSpace(row.LegacyEmployeeId))
            .GroupBy(row => row.LegacyEmployeeId.Trim(), StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var cyclicLegacyIds = FindManagerCycles(rowsByLegacyId);
        foreach (var row in request.Rows)
        {
            var rowIssues = errors[row.RowReference ?? string.Empty];
            if (!string.IsNullOrWhiteSpace(row.LegacyEmployeeId) && cyclicLegacyIds.Contains(row.LegacyEmployeeId.Trim())) rowIssues.Add("hr.import_manager_cycle");
            if (!string.IsNullOrWhiteSpace(row.ManagerLegacyEmployeeId) && rowsByLegacyId.TryGetValue(row.ManagerLegacyEmployeeId.Trim(), out var managerRow))
            {
                if (managerRow.LegalEntityId != row.LegalEntityId) rowIssues.Add("hr.import_manager_entity_mismatch");
                if (managerRow.HireDate > row.HireDate || managerRow.EndDate < row.HireDate) rowIssues.Add("hr.import_manager_not_active_on_hire_date");
                if (errors[managerRow.RowReference ?? string.Empty].Count > 0) rowIssues.Add("hr.import_manager_row_invalid");
            }
        }
        return errors.ToDictionary(item => item.Key, item => (IReadOnlyList<string>)item.Value, StringComparer.Ordinal);
    }

    private static HrEmployeeImportSummary RejectedSummary(HrEmployeeImportRequest request, IReadOnlyDictionary<string, IReadOnlyList<string>> validation)
    {
        var rows = request.Rows.Select(row => new HrEmployeeImportRowResult(row.RowReference ?? string.Empty, "Rejected", null, null,
            validation.GetValueOrDefault(row.RowReference ?? string.Empty, []))).ToArray();
        return new(rows.Length, 0, 0, rows.Length, rows);
    }

    private static async Task<string?> GetMigrationStrategyAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid legalEntityId, bool useLock, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand($"select employee_migration_strategy from business_hr.legal_entity_go_live_configuration where tenant_id=$1 and legal_entity_id=$2{(useLock ? " for share" : string.Empty)}", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId);
        return (string?)await command.ExecuteScalarAsync(ct);
    }

    private static string EmployeeImportRowHash(HrEmployeeImportRow row)
    {
        var canonical = System.Text.Json.JsonSerializer.Serialize(new
        {
            rowReference = row.RowReference.Trim(),
            legacyEmployeeId = row.LegacyEmployeeId.Trim(),
            arabicName = row.ArabicName.Trim(),
            englishName = row.EnglishName?.Trim(),
            preferredName = row.PreferredName?.Trim(),
            row.LegalEntityId,
            row.EmploymentType,
            row.HireDate,
            row.EndDate,
            row.Status,
            dataQuality = row.DataQuality,
            branchCode = row.BranchCode?.Trim(),
            departmentCode = row.DepartmentCode?.Trim(),
            positionCode = row.PositionCode?.Trim(),
            managerLegacyEmployeeId = row.ManagerLegacyEmployeeId?.Trim()
        });
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static bool HasAssignmentReference(HrEmployeeImportRow row) => row.BranchCode is not null || row.DepartmentCode is not null || row.PositionCode is not null || row.ManagerLegacyEmployeeId is not null;

    private static List<HrEmployeeImportRow> OrderByManager(IReadOnlyList<HrEmployeeImportRow> rows)
    {
        var remaining = rows.ToList();
        var ordered = new List<HrEmployeeImportRow>(rows.Count);
        while (remaining.Count > 0)
        {
            var pendingIds = remaining.Select(row => row.LegacyEmployeeId?.Trim()).ToHashSet(StringComparer.Ordinal);
            var ready = remaining.Where(row => string.IsNullOrWhiteSpace(row.ManagerLegacyEmployeeId)
                || !pendingIds.Contains(row.ManagerLegacyEmployeeId.Trim())
                || string.Equals(row.LegacyEmployeeId?.Trim(), row.ManagerLegacyEmployeeId.Trim(), StringComparison.Ordinal)).ToArray();
            if (ready.Length == 0)
            {
                ordered.AddRange(remaining);
                break;
            }
            foreach (var row in ready)
            {
                ordered.Add(row);
                remaining.Remove(row);
            }
        }
        return ordered;
    }

    private static HashSet<string> FindManagerCycles(IReadOnlyDictionary<string, HrEmployeeImportRow> rows)
    {
        var states = new Dictionary<string, byte>(StringComparer.Ordinal);
        var stack = new List<string>();
        var cycles = new HashSet<string>(StringComparer.Ordinal);
        void Visit(string id)
        {
            if (states.TryGetValue(id, out var state))
            {
                if (state == 1)
                {
                    var cycleStart = stack.LastIndexOf(id);
                    for (var index = Math.Max(0, cycleStart); index < stack.Count; index++) cycles.Add(stack[index]);
                }
                return;
            }
            states[id] = 1;
            stack.Add(id);
            var managerId = rows[id].ManagerLegacyEmployeeId?.Trim();
            if (!string.IsNullOrWhiteSpace(managerId) && rows.ContainsKey(managerId)) Visit(managerId);
            stack.RemoveAt(stack.Count - 1);
            states[id] = 2;
        }
        foreach (var id in rows.Keys) Visit(id);
        return cycles;
    }

    private static async Task<AssignmentReferences> ResolveAssignmentReferencesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction,
        TenantId tenant, string sourceSystem, HrEmployeeImportRow row, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            select
                (select id from platform.branch where tenant_id=$1 and legal_entity_id=$2 and code=$3 and status='Active' limit 1),
                (select id from business_hr.department where tenant_id=$1 and legal_entity_id=$2 and code=$4 and status='Active' limit 1),
                (select id from business_hr.position where tenant_id=$1 and legal_entity_id=$2 and code=$5 and status='Active' limit 1),
                (select employment.id from business_hr.employee_import_reference reference
                 join business_hr.employment employment on employment.tenant_id=reference.tenant_id and employment.id=reference.employment_id
                 where reference.tenant_id=$1 and reference.source_system=$6 and reference.legacy_employee_id=$7 and employment.legal_entity_id=$2
                   and employment.start_date<=$8 and (employment.end_date is null or employment.end_date>=$8) limit 1)
            """, connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(row.LegalEntityId);
        command.Parameters.AddWithValue((object?)row.BranchCode?.Trim() ?? DBNull.Value); command.Parameters.AddWithValue((object?)row.DepartmentCode?.Trim() ?? DBNull.Value);
        command.Parameters.AddWithValue((object?)row.PositionCode?.Trim() ?? DBNull.Value); command.Parameters.AddWithValue(sourceSystem);
        command.Parameters.AddWithValue((object?)row.ManagerLegacyEmployeeId?.Trim() ?? DBNull.Value); command.Parameters.AddWithValue(row.HireDate);
        await using var reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow, ct);
        if (!await reader.ReadAsync(ct)) return new(null, null, null, null);
        return new(reader.IsDBNull(0) ? null : reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2), reader.IsDBNull(3) ? null : reader.GetGuid(3));
    }

    private sealed record AssignmentReferences(Guid? BranchId, Guid? DepartmentId, Guid? PositionId, Guid? ManagerEmploymentId);
}
