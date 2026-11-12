using System.Text.Json;
using Core.Services;
using Core.Transactions;
using Modules.Hr.DTOs;
using Modules.Hr.Entities;
using Modules.Hr.Services;
using Npgsql;
using Shared.Primitives.Identifiers;

namespace Modules.Hr.Repositories;

public sealed class HrRepository(ITransactionCoordinator transactions, NpgsqlDataSource dataSource, TimeProvider clock, ITransactionalSequenceAllocator sequences, HrIdentifierCrypto identifierCrypto, Shared.Contracts.Platform.IPlatformFileReferenceCapability files, Shared.Contracts.Platform.IPlatformFileUploadCapability fileUploads) : Shared.Contracts.Hr.IHumanResourcesLifecycleCapability
{
    public Task<bool> ShouldDeactivateMembershipAsync(TenantId tenantId, Guid employeeId, Guid membershipId, DateOnly asOfDate, CancellationToken cancellationToken = default) =>
        transactions.ExecuteAsync(tenantId, async (context, token) =>
        {
            if (employeeId == Guid.Empty || membershipId == Guid.Empty || asOfDate == default) return false;
            await using var command = new NpgsqlCommand("select exists(select 1 from business_hr.employee e where e.tenant_id=$1 and e.id=$2 and e.membership_id=$3 and e.status<>'Archived') and not exists(select 1 from business_hr.employee e join business_hr.employment active on active.tenant_id=e.tenant_id and active.employee_id=e.id where e.tenant_id=$1 and e.membership_id=$3 and e.status<>'Archived' and active.status in ('Active','Suspended','NoticePeriod') and active.start_date<=$4 and (active.end_date is null or active.end_date>=$4) and not exists(select 1 from business_hr.offboarding_case offboarding where offboarding.tenant_id=active.tenant_id and offboarding.employment_id=active.id and offboarding.access_deactivation_on<=$4))", context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenantId.Value); command.Parameters.AddWithValue(employeeId); command.Parameters.AddWithValue(membershipId); command.Parameters.AddWithValue(asOfDate);
            return (bool)(await command.ExecuteScalarAsync(token) ?? false);
        }, cancellationToken);

    public async Task<EmployeeProfile> CreateEmployeeAsync(TenantId tenant, Guid actor, Guid membershipId, CreateEmployeeRequest request, Guid correlationId, CancellationToken ct)
    {
        var hasInitialEmployment = request.LegalEntityId is not null || request.EmploymentType is not null || request.StartDate is not null || request.EndDate is not null;
        if (hasInitialEmployment && (request.LegalEntityId is null || request.EmploymentType is null || request.StartDate is null))
            throw new HrException("hr.initial_employment_incomplete");
        if (request.EndDate is not null && request.StartDate is not null && request.EndDate < request.StartDate)
            throw new HrException("hr.employment_date_range_invalid");
        var employeeId = Guid.CreateVersion7();
        var employee = Employee.Create(employeeId, tenant.Value, request.ArabicName, request.EnglishName, request.PreferredName, request.MembershipId);
        return await transactions.ExecuteAsync<EmployeeProfile>(tenant, async (context, token) =>
        {
            if (request.MembershipId is { } linkedMembership)
            {
                await using var validateMembership = new NpgsqlCommand("select 1 from platform.membership where tenant_id=$1 and id=$2 and status='Active'", context.Connection, context.Transaction);
                validateMembership.Parameters.AddWithValue(tenant.Value); validateMembership.Parameters.AddWithValue(linkedMembership);
                if (await validateMembership.ExecuteScalarAsync(token) is null) throw new HrException("hr.membership_link_invalid");
            }
            await using (var insert = new NpgsqlCommand("insert into business_hr.employee(tenant_id,id,arabic_name,english_name,preferred_name,display_name,display_name_normalized,arabic_name_normalized,english_name_normalized,status,membership_id,created_by,created_at,version) values($1,$2,$3,$4,$5,$6,$7,$8,$9,'Active',$10,$11,$12,1)", context.Connection, context.Transaction))
            {
                insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(employee.Id); insert.Parameters.AddWithValue(employee.ArabicName);
                insert.Parameters.AddWithValue((object?)employee.EnglishName ?? DBNull.Value); insert.Parameters.AddWithValue((object?)employee.PreferredName ?? DBNull.Value);
                insert.Parameters.AddWithValue(employee.DisplayName); insert.Parameters.AddWithValue(HrSearchNormalization.Normalize(employee.DisplayName)); insert.Parameters.AddWithValue(HrSearchNormalization.Normalize(employee.ArabicName));
                insert.Parameters.AddWithValue((object?)employee.EnglishName is null ? DBNull.Value : HrSearchNormalization.Normalize(employee.EnglishName)); insert.Parameters.AddWithValue((object?)employee.MembershipId ?? DBNull.Value);
                insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(clock.GetUtcNow());
                await insert.ExecuteNonQueryAsync(token);
            }
            await RefreshEmployeeProjectionAsync(context.Connection, context.Transaction, employee, token);
            await AppendEventAsync(context.Connection, context.Transaction, tenant, employee.Id, null, "EmployeeCreated", DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), actor,
                new { employee.ArabicName, employee.EnglishName, employee.DisplayName }, correlationId, token);
            EmploymentView? initialEmployment = null;
            if (request.LegalEntityId is { } legalEntityId && request.EmploymentType is { } type && request.StartDate is { } startDate)
            {
                var employmentId = Guid.CreateVersion7();
                var employmentNumber = await sequences.AllocateAsync(context, new SequenceDefinition("hr.employee-number", tenant, legalEntityId, null, null, null, "EMP-", 6, 1), token);
                var employment = Employment.Create(employmentId, tenant.Value, employeeId, legalEntityId, employmentNumber, type, startDate, request.EndDate, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));
                await using var insertEmployment = new NpgsqlCommand("insert into business_hr.employment(tenant_id,id,employee_id,legal_entity_id,employment_number,employment_type,status,start_date,end_date,created_by,created_at,version) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,1)", context.Connection, context.Transaction);
                insertEmployment.Parameters.AddWithValue(tenant.Value); insertEmployment.Parameters.AddWithValue(employmentId); insertEmployment.Parameters.AddWithValue(employeeId); insertEmployment.Parameters.AddWithValue(legalEntityId);
                insertEmployment.Parameters.AddWithValue(employmentNumber); insertEmployment.Parameters.AddWithValue(type.ToString()); insertEmployment.Parameters.AddWithValue(employment.Status.ToString()); insertEmployment.Parameters.AddWithValue(startDate); insertEmployment.Parameters.AddWithValue((object?)request.EndDate ?? DBNull.Value);
                insertEmployment.Parameters.AddWithValue(actor); insertEmployment.Parameters.AddWithValue(clock.GetUtcNow());
                await insertEmployment.ExecuteNonQueryAsync(token);
                await AppendEventAsync(context.Connection, context.Transaction, tenant, employeeId, employmentId, "EmploymentCreated", startDate, actor,
                    new { employment.EmploymentNumber, employment.Type, employment.StartDate }, correlationId, token);
                initialEmployment = new(employmentId, employeeId, legalEntityId, employmentNumber, type, employment.Status, startDate, request.EndDate, employment.Version);
            }
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction,
                new AuditEntry(Guid.CreateVersion7(), tenant, initialEmployment?.LegalEntityId, null, "Human", actor, membershipId, "hr", "employee.created", "employee", employee.Id,
                    null, JsonSerializer.Serialize(new { employee.ArabicName, employee.EnglishName, employee.DisplayName, employee.Status, initialEmployment?.EmploymentNumber }), null, "hr-api", correlationId,
                    System.Diagnostics.Activity.Current?.TraceId.ToString(), clock.GetUtcNow(), null), token);
            return new EmployeeProfile(employee.Id, employee.ArabicName, employee.EnglishName, employee.PreferredName, employee.Status, employee.Version, initialEmployment);
        }, ct);
    }

    public Task<EmployeeMembershipLinkView> LinkMembershipAsync(TenantId tenant, Guid actor, Guid actingMembershipId, Guid employeeId,
        EmployeeMembershipLinkRequest request, Guid correlationId, CancellationToken ct) =>
        transactions.ExecuteAsync<EmployeeMembershipLinkView>(tenant, async (context, token) =>
        {
            var reason = request.Reason?.Trim();
            if (employeeId == Guid.Empty || request.ExpectedVersion < 1 || string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
                throw new HrException("hr.employee_membership_link_invalid");

            Guid? previousMembershipId;
            long version;
            await using (var read = new NpgsqlCommand("select membership_id,version from business_hr.employee where tenant_id=$1 and id=$2 and status<>'Archived' for update", context.Connection, context.Transaction))
            {
                read.Parameters.AddWithValue(tenant.Value); read.Parameters.AddWithValue(employeeId);
                await using var reader = await read.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow, token);
                if (!await reader.ReadAsync(token)) throw new HrException("hr.employee_not_found");
                previousMembershipId = reader.IsDBNull(0) ? null : reader.GetGuid(0);
                version = reader.GetInt64(1);
            }
            if (version != request.ExpectedVersion) throw new HrException("hr.employee_concurrency_conflict");
            if (previousMembershipId == request.MembershipId)
                return new(employeeId, request.MembershipId is not null, version);

            if (request.MembershipId is { } nextMembershipId)
            {
                await using var active = new NpgsqlCommand("select 1 from platform.membership where tenant_id=$1 and id=$2 and status='Active'", context.Connection, context.Transaction);
                active.Parameters.AddWithValue(tenant.Value); active.Parameters.AddWithValue(nextMembershipId);
                if (await active.ExecuteScalarAsync(token) is null) throw new HrException("hr.membership_link_invalid");
                await using var alreadyLinked = new NpgsqlCommand("select 1 from business_hr.employee where tenant_id=$1 and membership_id=$2 and id<>$3", context.Connection, context.Transaction);
                alreadyLinked.Parameters.AddWithValue(tenant.Value); alreadyLinked.Parameters.AddWithValue(nextMembershipId); alreadyLinked.Parameters.AddWithValue(employeeId);
                if (await alreadyLinked.ExecuteScalarAsync(token) is not null) throw new HrException("hr.membership_already_linked");
            }

            var now = clock.GetUtcNow();
            await using (var update = new NpgsqlCommand("update business_hr.employee set membership_id=$1,version=version+1 where tenant_id=$2 and id=$3 and version=$4 returning version", context.Connection, context.Transaction))
            {
                update.Parameters.AddWithValue((object?)request.MembershipId ?? DBNull.Value); update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(employeeId); update.Parameters.AddWithValue(request.ExpectedVersion);
                version = (long?)await update.ExecuteScalarAsync(token) ?? throw new HrException("hr.employee_concurrency_conflict");
            }
            var effectiveOn = DateOnly.FromDateTime(now.UtcDateTime);
            await AppendEventAsync(context.Connection, context.Transaction, tenant, employeeId, null, "EmployeeMembershipLinked", effectiveOn, actor,
                new { previousMembershipId, membershipId = request.MembershipId, reason }, correlationId, token);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction,
                new AuditEntry(Guid.CreateVersion7(), tenant, null, null, "Human", actor, actingMembershipId, "hr", "employee.membership_link_changed", "employee", employeeId,
                    JsonSerializer.Serialize(new { membershipId = previousMembershipId }), JsonSerializer.Serialize(new { membershipId = request.MembershipId, reason, version }), null,
                    "hr-api", correlationId, System.Diagnostics.Activity.Current?.TraceId.ToString(), now, null), token);
            return new(employeeId, request.MembershipId is not null, version);
        }, ct);

    public async Task<EmployeePage> ListEmployeesAsync(TenantId tenant, EmployeePageRequest request, CancellationToken ct)
    {
        var page = Math.Clamp(request.Page, 1, 100_000);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var search = string.IsNullOrWhiteSpace(request.Search) ? null : HrSearchNormalization.Normalize(request.Search.Trim());
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, tx, tenant, ct);
        var entityPredicate = request.LegalEntityId is null
            ? string.Empty
            : " and exists(select 1 from business_hr.employment scoped where scoped.tenant_id=e.tenant_id and scoped.employee_id=e.employee_id and scoped.legal_entity_id=$2 and scoped.status in ('PreHire','Active','Suspended','NoticePeriod'))";
        var entityParameterCount = request.LegalEntityId is null ? 0 : 1;
        var searchNameParameter = 2 + entityParameterCount;
        var searchNumberParameter = searchNameParameter + 1;
        var employmentSearchEntityPredicate = request.LegalEntityId is null ? string.Empty : " and sx.legal_entity_id=$2";
        var candidates = search is null
            ? "select e.employee_id from business_hr.employee_list_projection e where e.tenant_id=$1 and e.status<>'Archived'"
            : $"select e.employee_id from business_hr.employee_list_projection e where e.tenant_id=$1 and e.status<>'Archived' and (e.arabic_name_normalized like ${searchNameParameter} or e.english_name_normalized like ${searchNameParameter}) union select sx.employee_id from business_hr.employment sx where sx.tenant_id=$1{employmentSearchEntityPredicate} and lower(sx.employment_number) like ${searchNumberParameter}";
        var pageSizeParameter = 2 + entityParameterCount + (search is null ? 0 : 2);
        var countSql = $"with candidate_ids as ({candidates}) select count(*) from business_hr.employee_list_projection e where e.tenant_id=$1 and e.status<>'Archived' and e.employee_id in (select employee_id from candidate_ids){entityPredicate}";
        long count;
        await using (var countCommand = new NpgsqlCommand(countSql, connection, tx))
        {
            countCommand.Parameters.AddWithValue(tenant.Value);
            if (request.LegalEntityId is { } entity) countCommand.Parameters.AddWithValue(entity);
            if (search is not null)
            {
                countCommand.Parameters.AddWithValue($"%{search}%");
                countCommand.Parameters.AddWithValue($"%{search.ToLowerInvariant()}%");
            }
            count = (long)(await countCommand.ExecuteScalarAsync(ct) ?? 0L);
        }
        var employmentEntityPredicate = request.LegalEntityId is null ? string.Empty : " and em.legal_entity_id=$2";
        var candidateSql = $"with candidate_ids as ({candidates}) ";
        var sql = candidateSql + $"select e.employee_id,em.employment_number,e.display_name,e.english_name,e.status,em.start_date,em.legal_entity_id from business_hr.employee_list_projection e left join lateral (select employment_number,start_date,legal_entity_id from business_hr.employment em where em.tenant_id=e.tenant_id and em.employee_id=e.employee_id and em.status in ('PreHire','Active','Suspended','NoticePeriod'){employmentEntityPredicate} order by start_date desc,id limit 1) em on true where e.tenant_id=$1 and e.status<>'Archived' and e.employee_id in (select employee_id from candidate_ids){entityPredicate} order by e.display_name_normalized,e.employee_id limit ${pageSizeParameter} offset ${pageSizeParameter + 1}";
        var items = new List<EmployeeListItem>(pageSize);
        await using (var command = new NpgsqlCommand(sql, connection, tx))
        {
            command.Parameters.AddWithValue(tenant.Value);
            if (request.LegalEntityId is { } entity) command.Parameters.AddWithValue(entity);
            if (search is not null)
            {
                command.Parameters.AddWithValue($"%{search}%");
                command.Parameters.AddWithValue($"%{search.ToLowerInvariant()}%");
            }
            command.Parameters.AddWithValue(pageSize); command.Parameters.AddWithValue(checked((page - 1) * pageSize));
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) items.Add(new(reader.GetGuid(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), Enum.Parse<EmployeeStatus>(reader.GetString(4)), reader.IsDBNull(5) ? null : reader.GetFieldValue<DateOnly>(5), reader.IsDBNull(6) ? null : reader.GetGuid(6)));
        }
        await tx.CommitAsync(ct);
        return new(items, page, pageSize, count);
    }

    public async Task<IReadOnlyList<EmployeeDuplicateSuggestion>> FindPossibleDuplicatesAsync(TenantId tenant, string arabicName, string? englishName, CancellationToken ct)
    {
        var arabic = HrSearchNormalization.Normalize(arabicName);
        var english = string.IsNullOrWhiteSpace(englishName) ? null : HrSearchNormalization.Normalize(englishName);
        if (arabic.Length is < 2 or > 200 || english?.Length > 200) throw new HrException("hr.employee_name_invalid");
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tx, tenant, ct);
        const string sql = "select e.id,e.arabic_name,e.english_name,e.status,case when e.arabic_name_normalized=$2 then true else false end,case when $3::text is not null and e.english_name_normalized=$3 then true else false end,exists(select 1 from business_hr.employment em where em.tenant_id=e.tenant_id and em.employee_id=e.id and em.status in ('Terminated','Ended')) from business_hr.employee e where e.tenant_id=$1 and e.status<>'Archived' and (e.arabic_name_normalized=$2 or ($3::text is not null and e.english_name_normalized=$3)) order by e.arabic_name_normalized,e.id limit 10";
        await using var command = new NpgsqlCommand(sql, connection, tx); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(arabic); command.Parameters.AddWithValue((object?)english ?? DBNull.Value);
        var result = new List<EmployeeDuplicateSuggestion>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var reasons = new List<string>(); if (reader.GetBoolean(4)) reasons.Add("matching_arabic_name"); if (reader.GetBoolean(5)) reasons.Add("matching_english_name"); if (reader.GetBoolean(6)) reasons.Add("previous_employment");
            result.Add(new(reader.GetGuid(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), Enum.Parse<EmployeeStatus>(reader.GetString(3)), reasons));
        }
        await reader.CloseAsync(); await tx.CommitAsync(ct); return result;
    }

    public async Task<EmployeeIdentifierView> CreateIdentifierAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, CreateEmployeeIdentifierRequest request, Guid correlationId, CancellationToken ct)
    {
        var type = (request.IdentifierType ?? string.Empty).Trim(); var value = HrIdentifierCrypto.Normalize(request.Value); var country = request.CountryCode?.Trim().ToUpperInvariant(); var issuer = request.Issuer?.Trim();
        if (type.Length is < 1 or > 60 || value.Length is < 1 or > 200 || country is { Length: not 2 } || country is not null && !country.All(char.IsAsciiLetter)
            || request.ExpiryDate is { } expiry && request.IssueDate is { } issue && expiry < issue || issuer?.Length > 200)
            throw new HrException("hr.identifier_invalid");
        var id = Guid.CreateVersion7();
        if (request.FileRef is { } identifierFile)
        {
            var identifierEvidence = await files.VerifyReadyReferenceAsync(tenant, identifierFile, "hr", "employee", employeeId, ct);
            if (identifierEvidence is null || identifierEvidence.Classification.ToLowerInvariant() is not ("restricted" or "medical")) throw new HrException("hr.identifier_file_reference_invalid");
        }
        return await transactions.ExecuteAsync<EmployeeIdentifierView>(tenant, async (context, token) =>
        {
            await using (var keyInsert = new NpgsqlCommand("insert into business_hr.identifier_lookup_key(tenant_id,protected_key,created_at) values($1,$2,$3) on conflict(tenant_id) do nothing", context.Connection, context.Transaction))
            {
                keyInsert.Parameters.AddWithValue(tenant.Value); keyInsert.Parameters.AddWithValue(identifierCrypto.ProtectLookupKey(HrIdentifierCrypto.CreateLookupKey())); keyInsert.Parameters.AddWithValue(clock.GetUtcNow()); await keyInsert.ExecuteNonQueryAsync(token);
            }
            string protectedKey;
            await using (var keyRead = new NpgsqlCommand("select protected_key from business_hr.identifier_lookup_key where tenant_id=$1", context.Connection, context.Transaction))
            {
                keyRead.Parameters.AddWithValue(tenant.Value); protectedKey = (string?)await keyRead.ExecuteScalarAsync(token) ?? throw new HrException("hr.identifier_crypto_unavailable");
            }
            var lookupKey = identifierCrypto.UnprotectLookupKey(protectedKey);
            var lookupHash = HrIdentifierCrypto.Hash(lookupKey, value);
            var cipherText = identifierCrypto.Protect(value);
            var masked = HrIdentifierCrypto.Mask(value);
            await using var insert = new NpgsqlCommand("insert into business_hr.employee_identifier(id,tenant_id,employee_id,identifier_type,country_code,encrypted_value,lookup_hash,masked_display,issue_date,expiry_date,issuer,status,file_ref,created_by,created_at,version) select $1,$2,e.id,$4,$5,$6,$7,$8,$9,$10,$11,'Active',$12,$13,$14,1 from business_hr.employee e where e.tenant_id=$2 and e.id=$3 and e.status='Active' and exists(select 1 from business_hr.employment em where em.tenant_id=e.tenant_id and em.employee_id=e.id and em.legal_entity_id=$15)", context.Connection, context.Transaction);
            insert.Parameters.AddWithValue(id); insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(employeeId); insert.Parameters.AddWithValue(type); insert.Parameters.AddWithValue((object?)country ?? DBNull.Value);
            insert.Parameters.AddWithValue(cipherText); insert.Parameters.AddWithValue(lookupHash); insert.Parameters.AddWithValue(masked); insert.Parameters.AddWithValue((object?)request.IssueDate ?? DBNull.Value); insert.Parameters.AddWithValue((object?)request.ExpiryDate ?? DBNull.Value);
            insert.Parameters.AddWithValue((object?)issuer ?? DBNull.Value); insert.Parameters.AddWithValue((object?)request.FileRef ?? DBNull.Value); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(clock.GetUtcNow()); insert.Parameters.AddWithValue(request.LegalEntityId);
            if (await insert.ExecuteNonQueryAsync(token) != 1) throw new HrException("hr.employee_not_found_or_outside_entity");
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction,
                new AuditEntry(Guid.CreateVersion7(), tenant, request.LegalEntityId, null, "Human", actor, membershipId, "hr", "employee.identifier_added", "employee_identifier", id,
                    null, JsonSerializer.Serialize(new { employeeId, type, countryCode = country, masked, request.ExpiryDate }), null, "hr-api", correlationId,
                    System.Diagnostics.Activity.Current?.TraceId.ToString(), clock.GetUtcNow(), null), token);
            return new(id, employeeId, type, country, masked, request.IssueDate, request.ExpiryDate, ExpiryStatus(request.ExpiryDate), "Active", request.FileRef, 1);
        }, ct);
    }

    public async Task<IReadOnlyList<EmployeeIdentifierView>> ListIdentifiersAsync(TenantId tenant, Guid employeeId, Guid legalEntityId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tx, tenant, ct);
        const string sql = "select i.id,i.employee_id,i.identifier_type,i.country_code,i.masked_display,i.issue_date,i.expiry_date,i.status,i.file_ref,i.version from business_hr.employee_identifier i where i.tenant_id=$1 and i.employee_id=$2 and exists(select 1 from business_hr.employment e where e.tenant_id=i.tenant_id and e.employee_id=i.employee_id and e.legal_entity_id=$3) order by i.identifier_type,i.id limit 100";
        await using var command = new NpgsqlCommand(sql, connection, tx); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(employeeId); command.Parameters.AddWithValue(legalEntityId);
        var rows = new List<EmployeeIdentifierView>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var expiry = reader.IsDBNull(6) ? null : reader.GetFieldValue<DateOnly?>(6);
            rows.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetFieldValue<DateOnly?>(5), expiry, ExpiryStatus(expiry), reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetGuid(8), reader.GetInt64(9)));
        }
        await reader.CloseAsync(); await tx.CommitAsync(ct); return rows;
    }

    public async Task<EmployeeDocumentView> CreateDocumentAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, CreateEmployeeDocumentRequest request, Guid correlationId, CancellationToken ct)
    {
        var category = (request.Category ?? string.Empty).Trim(); var sensitivity = (request.Sensitivity ?? string.Empty).Trim();
        if (category.Length is < 1 or > 80 || sensitivity is not ("Normal" or "Confidential" or "Medical" or "Restricted")) throw new HrException("hr.document_metadata_invalid");
        await EnsureEmployeeInEntityAsync(tenant, employeeId, request.LegalEntityId, request.EmploymentId, ct);
        var file = await files.VerifyReadyReferenceAsync(tenant, request.FileRef, "hr", "employee", employeeId, ct);
        if (file is null || !FileClassificationAllows(file.Classification, sensitivity)) throw new HrException("hr.document_file_reference_invalid");
        var id = Guid.CreateVersion7();
        return await transactions.ExecuteAsync<EmployeeDocumentView>(tenant, async (context, token) =>
        {
            if (request.SupersedesDocumentId is { } priorId)
            {
                await using var supersede = new NpgsqlCommand("update business_hr.employee_document set status='Superseded',version=version+1 where tenant_id=$1 and id=$2 and employee_id=$3 and employment_id is not distinct from $4 and legal_entity_id is not distinct from $5 and status='Active'", context.Connection, context.Transaction);
                supersede.Parameters.AddWithValue(tenant.Value); supersede.Parameters.AddWithValue(priorId); supersede.Parameters.AddWithValue(employeeId); supersede.Parameters.AddWithValue((object?)request.EmploymentId ?? DBNull.Value); supersede.Parameters.AddWithValue((object?)request.EmploymentId is null ? DBNull.Value : request.LegalEntityId);
                if (await supersede.ExecuteNonQueryAsync(token) != 1) throw new HrException("hr.document_supersede_target_invalid");
            }
            await using var insert = new NpgsqlCommand("insert into business_hr.employee_document(id,tenant_id,employee_id,employment_id,legal_entity_id,scope_status,category,sensitivity,file_ref,sha256,content_type,size_bytes,expiry_date,verification_status,status,supersedes_document_id,created_by,created_at,version) select $1,$2,e.id,$4,$15,$16,$5,$6,$7,$8,$9,$10,$11,'Pending','Active',$12,$13,$14,1 from business_hr.employee e where e.tenant_id=$2 and e.id=$3 and e.status='Active' and exists(select 1 from business_hr.employment em where em.tenant_id=e.tenant_id and em.employee_id=e.id and em.legal_entity_id=$15 and ($4::uuid is null or em.id=$4))", context.Connection, context.Transaction);
            insert.Parameters.AddWithValue(id); insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(employeeId); insert.Parameters.AddWithValue((object?)request.EmploymentId ?? DBNull.Value); insert.Parameters.AddWithValue(category); insert.Parameters.AddWithValue(sensitivity); insert.Parameters.AddWithValue(file.FileRef); insert.Parameters.AddWithValue(file.Sha256.ToLowerInvariant()); insert.Parameters.AddWithValue(file.ContentType); insert.Parameters.AddWithValue(file.SizeBytes); insert.Parameters.AddWithValue((object?)request.ExpiryDate ?? DBNull.Value); insert.Parameters.AddWithValue((object?)request.SupersedesDocumentId ?? DBNull.Value); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(clock.GetUtcNow()); insert.Parameters.AddWithValue((object?)request.EmploymentId is null ? DBNull.Value : request.LegalEntityId); insert.Parameters.AddWithValue("Resolved");
            if (await insert.ExecuteNonQueryAsync(token) != 1) throw new HrException("hr.employee_not_found_or_outside_entity");
            await AppendEventAsync(context.Connection, context.Transaction, tenant, employeeId, null, "EmployeeDocumentAdded", DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), actor,
                new { id, category, sensitivity, request.ExpiryDate }, correlationId, token);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction,
                new AuditEntry(Guid.CreateVersion7(), tenant, request.LegalEntityId, null, "Human", actor, membershipId, "hr", "employee.document_added", "employee_document", id,
                    null, JsonSerializer.Serialize(new { employeeId, request.EmploymentId, category, sensitivity, file.FileRef, file.Sha256, request.ExpiryDate, request.SupersedesDocumentId }), null, "hr-api", correlationId,
                    System.Diagnostics.Activity.Current?.TraceId.ToString(), clock.GetUtcNow(), null), token);
            return new(id, employeeId, category, sensitivity, file.FileRef, file.Sha256, file.ContentType, file.SizeBytes, request.ExpiryDate, "Pending", "Active", request.SupersedesDocumentId, 1, request.EmploymentId);
        }, ct);
    }

    public async Task<EmployeeDocumentUploadIntentView> CreateDocumentUploadIntentAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, CreateEmployeeDocumentUploadRequest request, CancellationToken ct)
    {
        var sensitivity = (request.Sensitivity ?? string.Empty).Trim();
        if (sensitivity is not ("Normal" or "Confidential" or "Medical" or "Restricted")) throw new HrException("hr.document_metadata_invalid");
        await EnsureEmployeeInEntityAsync(tenant, employeeId, request.LegalEntityId, request.EmploymentId, ct);
        var classification = sensitivity switch { "Normal" => "internal", "Confidential" => "confidential", "Medical" => "medical", _ => "restricted" };
        Shared.Contracts.Platform.SignedPlatformFileUpload? intent;
        try { intent = await fileUploads.CreateUploadIntentAsync(tenant, actor, membershipId, "hr", "employee", employeeId, request.FileName, request.ContentType, request.SizeBytes, classification, ct); }
        catch (Shared.Contracts.Platform.PlatformFileStorageUnavailableException) { throw new HrException("hr.file_access_unavailable"); }
        catch (Core.Services.PlatformFileContentRejectedException) { throw new HrException("hr.document_upload_invalid"); }
        if (intent is null) throw new HrException("hr.document_upload_unavailable");
        return new(intent.FileRef, intent.Url, intent.Fields, intent.ContentType, intent.SizeBytes, intent.ExpiresAt);
    }

    public async Task<Shared.Contracts.Platform.PlatformFileUploadCompletion> CompleteDocumentUploadAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, Guid legalEntityId, Guid? employmentId, Guid fileRef, CancellationToken ct)
    {
        await EnsureEmployeeInEntityAsync(tenant, employeeId, legalEntityId, employmentId, ct);
        try
        {
            var completion = await fileUploads.CompleteUploadAsync(tenant, actor, membershipId, "hr", "employee", employeeId, fileRef, ct);
            return completion ?? throw new HrException("hr.document_upload_not_found");
        }
        catch (Shared.Contracts.Platform.PlatformFileStorageUnavailableException) { throw new HrException("hr.file_access_unavailable"); }
    }

    private async Task EnsureEmployeeInEntityAsync(TenantId tenant, Guid employeeId, Guid legalEntityId, Guid? employmentId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        await using var command = new NpgsqlCommand("select exists(select 1 from business_hr.employee e where e.tenant_id=$1 and e.id=$2 and e.status='Active' and exists(select 1 from business_hr.employment em where em.tenant_id=e.tenant_id and em.employee_id=e.id and em.legal_entity_id=$3 and ($4::uuid is null or em.id=$4)))", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(employeeId); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue((object?)employmentId ?? DBNull.Value);
        var exists = (bool)(await command.ExecuteScalarAsync(ct) ?? false);
        await transaction.CommitAsync(ct);
        if (!exists) throw new HrException("hr.employee_not_found_or_outside_entity");
    }

    public async Task<IReadOnlyList<EmployeeDocumentView>> ListDocumentsAsync(TenantId tenant, Guid employeeId, Guid legalEntityId, bool includeSensitive, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tx, tenant, ct);
        const string sql = "select d.id,d.employee_id,d.category,d.sensitivity,d.file_ref,d.sha256,d.content_type,d.size_bytes,d.expiry_date,d.verification_status,d.status,d.supersedes_document_id,d.version,d.employment_id from business_hr.employee_document d where d.tenant_id=$1 and d.employee_id=$2 and d.status<>'Archived' and d.scope_status='Resolved' and ($4 or d.sensitivity='Normal') and exists(select 1 from business_hr.employment e where e.tenant_id=d.tenant_id and e.employee_id=d.employee_id and e.legal_entity_id=$3) and (d.legal_entity_id is null or d.legal_entity_id=$3) and (d.employment_id is null or exists(select 1 from business_hr.employment scoped where scoped.tenant_id=d.tenant_id and scoped.id=d.employment_id and scoped.legal_entity_id=$3)) order by d.created_at desc,d.id desc limit 100";
        await using var command = new NpgsqlCommand(sql, connection, tx); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(employeeId); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue(includeSensitive);
        var result = new List<EmployeeDocumentView>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetGuid(4), reader.GetString(5), reader.GetString(6), reader.GetInt64(7), reader.IsDBNull(8) ? null : reader.GetFieldValue<DateOnly?>(8), reader.GetString(9), reader.GetString(10), reader.IsDBNull(11) ? null : reader.GetGuid(11), reader.GetInt64(12), reader.IsDBNull(13) ? null : reader.GetGuid(13)));
        await reader.CloseAsync(); await tx.CommitAsync(ct); return result;
    }

    public async Task<EmployeeDocumentDownloadView> CreateDocumentDownloadAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, Guid documentId, Guid legalEntityId, bool includeSensitive, Guid correlationId, CancellationToken ct)
    {
        Guid fileRef; string sensitivity; string verificationStatus; string documentStatus;
        await using (var connection = await dataSource.OpenConnectionAsync(ct))
        await using (var transaction = await connection.BeginTransactionAsync(ct))
        {
            await SetTenantAsync(connection, transaction, tenant, ct);
            await using var command = new NpgsqlCommand("select d.file_ref,d.sensitivity,d.verification_status,d.status from business_hr.employee_document d where d.tenant_id=$1 and d.id=$2 and d.employee_id=$3 and d.scope_status='Resolved' and exists(select 1 from business_hr.employment e where e.tenant_id=d.tenant_id and e.employee_id=d.employee_id and e.legal_entity_id=$4) and (d.legal_entity_id is null or d.legal_entity_id=$4) and (d.employment_id is null or exists(select 1 from business_hr.employment scoped where scoped.tenant_id=d.tenant_id and scoped.id=d.employment_id and scoped.legal_entity_id=$4))", connection, transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(documentId); command.Parameters.AddWithValue(employeeId); command.Parameters.AddWithValue(legalEntityId);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) throw new HrException("hr.document_not_found");
            fileRef = reader.GetGuid(0); sensitivity = reader.GetString(1); verificationStatus = reader.GetString(2); documentStatus = reader.GetString(3);
            await reader.CloseAsync(); await transaction.CommitAsync(ct);
        }

        if (sensitivity != "Normal" && !includeSensitive) throw new HrException("hr.document_not_found");
        if (documentStatus != "Active" || verificationStatus != "Verified") throw new HrException("hr.document_not_downloadable");
        Shared.Contracts.Platform.SignedPlatformFileDownload? signed;
        try { signed = await files.CreateReadyDownloadAsync(tenant, fileRef, "hr", "employee", employeeId, TimeSpan.FromMinutes(5), ct); }
        catch (Shared.Contracts.Platform.PlatformFileStorageUnavailableException) { throw new HrException("hr.file_access_unavailable"); }
        if (signed is null) throw new HrException("hr.document_file_unavailable");

        await using (var connection = await dataSource.OpenConnectionAsync(ct))
        await using (var transaction = await connection.BeginTransactionAsync(ct))
        {
            await SetTenantAsync(connection, transaction, tenant, ct);
            await NpgsqlAuditWriter.WriteAsync(connection, transaction,
                new AuditEntry(Guid.CreateVersion7(), tenant, legalEntityId, null, "Human", actor, membershipId, "hr", "hr.employee_document.download_url_issued", "employee_document", documentId,
                    null, JsonSerializer.Serialize(new { employeeId, documentId, fileRef, expiresAt = signed.ExpiresAt }), null, "hr-api", correlationId,
                    System.Diagnostics.Activity.Current?.TraceId.ToString(), clock.GetUtcNow(), null), ct);
            await transaction.CommitAsync(ct);
        }
        return new(signed.Url, signed.ExpiresAt, signed.FileName);
    }

    public async Task<EmployeeDocumentView> VerifyDocumentAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, Guid documentId, Guid legalEntityId, VerifyEmployeeDocumentRequest request, Guid correlationId, CancellationToken ct)
    {
        var reason = (request.Reason ?? string.Empty).Trim(); if (request.ExpectedVersion < 1 || reason.Length is < 1 or > 500) throw new HrException("hr.document_verification_invalid");
        return await transactions.ExecuteAsync<EmployeeDocumentView>(tenant, async (context, token) =>
        {
            var status = request.Verified ? "Verified" : "Rejected";
            await using var command = new NpgsqlCommand("update business_hr.employee_document d set verification_status=$1,version=version+1 where d.tenant_id=$2 and d.id=$3 and d.employee_id=$4 and d.scope_status='Resolved' and d.verification_status='Pending' and d.version=$5 and exists(select 1 from business_hr.employment e where e.tenant_id=d.tenant_id and e.employee_id=d.employee_id and e.legal_entity_id=$6) and (d.legal_entity_id is null or d.legal_entity_id=$6) and (d.employment_id is null or exists(select 1 from business_hr.employment scoped where scoped.tenant_id=d.tenant_id and scoped.id=d.employment_id and scoped.legal_entity_id=$6)) returning d.category,d.sensitivity,d.file_ref,d.sha256,d.content_type,d.size_bytes,d.expiry_date,d.status,d.supersedes_document_id,d.version,d.employment_id", context.Connection, context.Transaction);
            command.Parameters.AddWithValue(status); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(documentId); command.Parameters.AddWithValue(employeeId); command.Parameters.AddWithValue(request.ExpectedVersion); command.Parameters.AddWithValue(legalEntityId);
            await using var reader = await command.ExecuteReaderAsync(token); if (!await reader.ReadAsync(token)) throw new HrException("hr.document_concurrency_or_scope_conflict");
            var result = new EmployeeDocumentView(documentId, employeeId, reader.GetString(0), reader.GetString(1), reader.GetGuid(2), reader.GetString(3), reader.GetString(4), reader.GetInt64(5), reader.IsDBNull(6) ? null : reader.GetFieldValue<DateOnly?>(6), status, reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetGuid(8), reader.GetInt64(9), reader.IsDBNull(10) ? null : reader.GetGuid(10));
            await reader.CloseAsync();
            await AppendEventAsync(context.Connection, context.Transaction, tenant, employeeId, null, "EmployeeDocumentVerified", DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), actor, new { documentId, status }, correlationId, token);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction,
                new AuditEntry(Guid.CreateVersion7(), tenant, legalEntityId, null, "Human", actor, membershipId, "hr", "employee.document_verification_changed", "employee_document", documentId,
                    null, JsonSerializer.Serialize(new { employeeId, status, reason }), null, "hr-api", correlationId,
                    System.Diagnostics.Activity.Current?.TraceId.ToString(), clock.GetUtcNow(), null), token);
            return result;
        }, ct);
    }

    public async Task<QualificationView> CreateQualificationAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, CreateQualificationRequest request, Guid correlationId, CancellationToken ct)
    {
        var type = (request.QualificationType ?? string.Empty).Trim(); var name = (request.Name ?? string.Empty).Trim(); var institution = (request.Institution ?? string.Empty).Trim(); var field = request.FieldOfStudy?.Trim(); var level = request.Level?.Trim();
        if (type.Length is < 1 or > 60 || name.Length is < 1 or > 200 || institution.Length is < 1 or > 200 || field?.Length > 200 || level?.Length > 80) throw new HrException("hr.qualification_invalid");
        if (request.FileRef is { } fileRef)
        {
            var file = await files.VerifyReadyReferenceAsync(tenant, fileRef, "hr", "employee", employeeId, ct);
            if (file is null || file.Classification.ToLowerInvariant() is not ("confidential" or "restricted")) throw new HrException("hr.qualification_file_reference_invalid");
        }
        var id = Guid.CreateVersion7(); var createdAt = clock.GetUtcNow();
        return await transactions.ExecuteAsync<QualificationView>(tenant, async (context, token) =>
        {
            await using var insert = new NpgsqlCommand("insert into business_hr.employee_qualification(id,tenant_id,employee_id,qualification_type,name,institution,field_of_study,level,completed_on,file_ref,status,created_by,created_at) select $1,$2,e.id,$4,$5,$6,$7,$8,$9,$10,'Active',$11,$12 from business_hr.employee e where e.tenant_id=$2 and e.id=$3 and e.status='Active' and exists(select 1 from business_hr.employment em where em.tenant_id=e.tenant_id and em.employee_id=e.id and em.legal_entity_id=$13)", context.Connection, context.Transaction);
            insert.Parameters.AddWithValue(id); insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(employeeId); insert.Parameters.AddWithValue(type); insert.Parameters.AddWithValue(name); insert.Parameters.AddWithValue(institution); insert.Parameters.AddWithValue((object?)field ?? DBNull.Value); insert.Parameters.AddWithValue((object?)level ?? DBNull.Value); insert.Parameters.AddWithValue((object?)request.CompletedOn ?? DBNull.Value); insert.Parameters.AddWithValue((object?)request.FileRef ?? DBNull.Value); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(createdAt); insert.Parameters.AddWithValue(request.LegalEntityId);
            if (await insert.ExecuteNonQueryAsync(token) != 1) throw new HrException("hr.employee_not_found_or_outside_entity");
            await AppendEventAsync(context.Connection, context.Transaction, tenant, employeeId, null, "EmployeeQualificationAdded", DateOnly.FromDateTime(createdAt.UtcDateTime), actor, new { id, type, name }, correlationId, token);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction,
                new AuditEntry(Guid.CreateVersion7(), tenant, request.LegalEntityId, null, "Human", actor, membershipId, "hr", "employee.qualification_added", "employee_qualification", id,
                    null, JsonSerializer.Serialize(new { employeeId, type, name, institution, field, level, request.CompletedOn, request.FileRef }), null, "hr-api", correlationId,
                    System.Diagnostics.Activity.Current?.TraceId.ToString(), createdAt, null), token);
            return new(id, employeeId, type, name, institution, field, level, request.CompletedOn, request.FileRef, "Active", createdAt);
        }, ct);
    }

    public async Task<IReadOnlyList<QualificationView>> ListQualificationsAsync(TenantId tenant, Guid employeeId, Guid legalEntityId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tx, tenant, ct);
        const string sql = "select q.id,q.employee_id,q.qualification_type,q.name,q.institution,q.field_of_study,q.level,q.completed_on,q.file_ref,q.status,q.created_at from business_hr.employee_qualification q where q.tenant_id=$1 and q.employee_id=$2 and exists(select 1 from business_hr.employment e where e.tenant_id=q.tenant_id and e.employee_id=q.employee_id and e.legal_entity_id=$3) order by q.completed_on desc nulls last,q.created_at desc,q.id desc limit 100";
        await using var command = new NpgsqlCommand(sql, connection, tx); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(employeeId); command.Parameters.AddWithValue(legalEntityId);
        var result = new List<QualificationView>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetFieldValue<DateOnly?>(7), reader.IsDBNull(8) ? null : reader.GetGuid(8), reader.GetString(9), reader.GetFieldValue<DateTimeOffset>(10)));
        await reader.CloseAsync(); await tx.CommitAsync(ct); return result;
    }

    public async Task<CredentialView> CreateCredentialAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, CreateCredentialRequest request, Guid correlationId, CancellationToken ct)
    {
        var type = (request.CredentialType ?? string.Empty).Trim(); var name = (request.Name ?? string.Empty).Trim(); var issuer = (request.Issuer ?? string.Empty).Trim(); var country = request.CountryCode?.Trim().ToUpperInvariant();
        if (type.Length is < 1 or > 60 || name.Length is < 1 or > 200 || issuer.Length is < 1 or > 200 || country is { Length: not 2 } || country is not null && !country.All(char.IsAsciiLetter) || request.IssuedOn is { } issued && request.ExpiryDate is { } expiry && expiry < issued)
            throw new HrException("hr.credential_invalid");
        if (request.FileRef is { } fileRef)
        {
            var file = await files.VerifyReadyReferenceAsync(tenant, fileRef, "hr", "employee", employeeId, ct);
            if (file is null || file.Classification.ToLowerInvariant() is not ("confidential" or "restricted")) throw new HrException("hr.credential_file_reference_invalid");
        }
        var id = Guid.CreateVersion7();
        return await transactions.ExecuteAsync<CredentialView>(tenant, async (context, token) =>
        {
            await using var insert = new NpgsqlCommand("insert into business_hr.employee_credential(id,tenant_id,employee_id,credential_type,name,issuer,country_code,issued_on,expiry_date,file_ref,verification_status,status,created_by,created_at,version) select $1,$2,e.id,$4,$5,$6,$7,$8,$9,$10,'Pending','Active',$11,$12,1 from business_hr.employee e where e.tenant_id=$2 and e.id=$3 and e.status='Active' and exists(select 1 from business_hr.employment em where em.tenant_id=e.tenant_id and em.employee_id=e.id and em.legal_entity_id=$13)", context.Connection, context.Transaction);
            insert.Parameters.AddWithValue(id); insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(employeeId); insert.Parameters.AddWithValue(type); insert.Parameters.AddWithValue(name); insert.Parameters.AddWithValue(issuer); insert.Parameters.AddWithValue((object?)country ?? DBNull.Value); insert.Parameters.AddWithValue((object?)request.IssuedOn ?? DBNull.Value); insert.Parameters.AddWithValue((object?)request.ExpiryDate ?? DBNull.Value); insert.Parameters.AddWithValue((object?)request.FileRef ?? DBNull.Value); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(clock.GetUtcNow()); insert.Parameters.AddWithValue(request.LegalEntityId);
            if (await insert.ExecuteNonQueryAsync(token) != 1) throw new HrException("hr.employee_not_found_or_outside_entity");
            await AppendEventAsync(context.Connection, context.Transaction, tenant, employeeId, null, "EmployeeCredentialAdded", DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), actor, new { id, type, name, request.ExpiryDate }, correlationId, token);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction,
                new AuditEntry(Guid.CreateVersion7(), tenant, request.LegalEntityId, null, "Human", actor, membershipId, "hr", "employee.credential_added", "employee_credential", id,
                    null, JsonSerializer.Serialize(new { employeeId, type, name, issuer, countryCode = country, request.IssuedOn, request.ExpiryDate, request.FileRef }), null, "hr-api", correlationId,
                    System.Diagnostics.Activity.Current?.TraceId.ToString(), clock.GetUtcNow(), null), token);
            return new(id, employeeId, type, name, issuer, country, request.IssuedOn, request.ExpiryDate, ExpiryStatus(request.ExpiryDate), "Pending", request.FileRef, "Active", 1);
        }, ct);
    }

    public async Task<IReadOnlyList<CredentialView>> ListCredentialsAsync(TenantId tenant, Guid employeeId, Guid legalEntityId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tx, tenant, ct);
        const string sql = "select c.id,c.employee_id,c.credential_type,c.name,c.issuer,c.country_code,c.issued_on,c.expiry_date,c.verification_status,c.file_ref,c.status,c.version from business_hr.employee_credential c where c.tenant_id=$1 and c.employee_id=$2 and exists(select 1 from business_hr.employment e where e.tenant_id=c.tenant_id and e.employee_id=c.employee_id and e.legal_entity_id=$3) order by c.expiry_date nulls last,c.name,c.id limit 100";
        await using var command = new NpgsqlCommand(sql, connection, tx); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(employeeId); command.Parameters.AddWithValue(legalEntityId);
        var result = new List<CredentialView>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var expiry = reader.IsDBNull(7) ? null : reader.GetFieldValue<DateOnly?>(7);
            result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetFieldValue<DateOnly?>(6), expiry, ExpiryStatus(expiry), reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetGuid(9), reader.GetString(10), reader.GetInt64(11)));
        }
        await reader.CloseAsync(); await tx.CommitAsync(ct); return result;
    }

    public async Task<CredentialView> VerifyCredentialAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, Guid credentialId, Guid legalEntityId, VerifyCredentialRequest request, Guid correlationId, CancellationToken ct)
    {
        if (request.ExpectedVersion <= 0 || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 500) throw new HrException("hr.credential_verification_invalid");
        Guid? fileRef;
        await using (var connection = await dataSource.OpenConnectionAsync(ct))
        await using (var tx = await connection.BeginTransactionAsync(ct))
        {
            await SetTenantAsync(connection, tx, tenant, ct);
            await using var command = new NpgsqlCommand("select c.file_ref from business_hr.employee_credential c where c.tenant_id=$1 and c.id=$2 and c.employee_id=$3 and exists(select 1 from business_hr.employment e where e.tenant_id=c.tenant_id and e.employee_id=c.employee_id and e.legal_entity_id=$4) and c.version=$5 and c.verification_status='Pending'", connection, tx);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(credentialId); command.Parameters.AddWithValue(employeeId); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue(request.ExpectedVersion);
            var result = await command.ExecuteScalarAsync(ct);
            if (result is null) throw new HrException("hr.credential_concurrency_conflict");
            fileRef = result is DBNull ? null : (Guid)result;
            await tx.CommitAsync(ct);
        }
        if (request.Verified)
        {
            if (fileRef is not { } evidence) throw new HrException("hr.credential_evidence_required");
            var file = await files.VerifyReadyReferenceAsync(tenant, evidence, "hr", "employee", employeeId, ct);
            if (file is null || file.Classification.ToLowerInvariant() is not ("confidential" or "restricted")) throw new HrException("hr.credential_file_reference_invalid");
        }
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var status = request.Verified ? "Verified" : "Rejected";
            CredentialView view;
            await using (var command = new NpgsqlCommand("update business_hr.employee_credential c set verification_status=$6,version=c.version+1 from business_hr.employee e where c.tenant_id=$1 and c.id=$2 and c.employee_id=$3 and c.version=$4 and c.verification_status='Pending' and e.tenant_id=c.tenant_id and e.id=c.employee_id and exists(select 1 from business_hr.employment em where em.tenant_id=e.tenant_id and em.employee_id=e.id and em.legal_entity_id=$5) returning c.credential_type,c.name,c.issuer,c.country_code,c.issued_on,c.expiry_date,c.file_ref,c.status,c.version", context.Connection, context.Transaction))
            {
                command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(credentialId); command.Parameters.AddWithValue(employeeId); command.Parameters.AddWithValue(request.ExpectedVersion); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue(status);
                await using var reader = await command.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new HrException("hr.credential_concurrency_conflict");
                var expiry = reader.IsDBNull(5) ? null : reader.GetFieldValue<DateOnly?>(5);
                view = new(credentialId, employeeId, reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetFieldValue<DateOnly?>(4), expiry, ExpiryStatus(expiry), status, reader.IsDBNull(6) ? null : reader.GetGuid(6), reader.GetString(7), reader.GetInt64(8));
            }
            await AppendEventAsync(context.Connection, context.Transaction, tenant, employeeId, null, "EmployeeCredentialVerified", DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), actor, new { credentialId, status, request.Reason }, correlationId, token);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, legalEntityId, null, "Human", actor, membershipId, "hr", "employee.credential_verification_changed", "employee_credential", credentialId, null, JsonSerializer.Serialize(new { status, reason = request.Reason }), null, "hr-api", correlationId, System.Diagnostics.Activity.Current?.TraceId.ToString(), clock.GetUtcNow(), null), token);
            return view;
        }, ct);
    }

    public async Task<InsuranceEnrollmentView> CreateInsuranceEnrollmentAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, Guid employmentId, CreateInsuranceEnrollmentRequest request, Guid correlationId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SchemeCode) || request.SchemeCode.Trim().Length > 80 || request.EffectiveTo < request.EffectiveFrom || request.EffectiveFrom == default)
            throw new HrException("hr.insurance_enrollment_invalid");
        if (request.EvidenceFileRef is { } evidence)
        {
            var file = await files.VerifyReadyReferenceAsync(tenant, evidence, "hr", "employee", employeeId, ct);
            if (file is null || file.Classification.ToLowerInvariant() is not ("confidential" or "restricted")) throw new HrException("hr.insurance_evidence_invalid");
        }
        var id = Guid.CreateVersion7();
        var encryptedMember = string.IsNullOrWhiteSpace(request.MemberNumber) ? null : identifierCrypto.ProtectInsuranceNumber(HrIdentifierCrypto.Normalize(request.MemberNumber));
        var maskedMember = string.IsNullOrWhiteSpace(request.MemberNumber) ? null : HrIdentifierCrypto.Mask(request.MemberNumber.Trim());
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var insert = new NpgsqlCommand("insert into business_hr.employee_insurance_enrollment(id,tenant_id,employment_id,legal_entity_id,scheme_code,provider_code,encrypted_member_number,masked_member_number,effective_from,effective_to,status,country_provider_version,evidence_file_ref,created_by,created_at,version) select $1,$2,e.id,e.legal_entity_id,$5,$6,$7,$8,$9,$10,'Draft',$11,$12,$13,$14,1 from business_hr.employment e where e.tenant_id=$2 and e.id=$3 and e.employee_id=$4 and e.legal_entity_id=$15 returning id,effective_from,effective_to,status,version", context.Connection, context.Transaction);
            insert.Parameters.AddWithValue(id); insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(employmentId); insert.Parameters.AddWithValue(employeeId);
            insert.Parameters.AddWithValue(request.SchemeCode.Trim()); insert.Parameters.AddWithValue((object?)request.ProviderCode?.Trim() ?? DBNull.Value);
            insert.Parameters.AddWithValue((object?)encryptedMember ?? DBNull.Value); insert.Parameters.AddWithValue((object?)maskedMember ?? DBNull.Value);
            insert.Parameters.AddWithValue(request.EffectiveFrom); insert.Parameters.AddWithValue((object?)request.EffectiveTo ?? DBNull.Value);
            insert.Parameters.AddWithValue(DBNull.Value); insert.Parameters.AddWithValue((object?)request.EvidenceFileRef ?? DBNull.Value);
            insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(clock.GetUtcNow()); insert.Parameters.AddWithValue(request.LegalEntityId);
            await using var reader = await insert.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new HrException("hr.employment_not_found");
            var view = new InsuranceEnrollmentView(id, employmentId, request.SchemeCode.Trim(), request.ProviderCode?.Trim(), maskedMember, reader.GetFieldValue<DateOnly>(1), reader.IsDBNull(2) ? null : reader.GetFieldValue<DateOnly>(2), reader.GetString(3), null, request.EvidenceFileRef, reader.GetInt64(4));
            await reader.DisposeAsync();
            await AppendEventAsync(context.Connection, context.Transaction, tenant, employeeId, employmentId, "InsuranceEnrollmentDrafted", request.EffectiveFrom, actor, new { id, request.SchemeCode, request.ProviderCode, status = view.Status, request.EffectiveFrom, request.EffectiveTo }, correlationId, token);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, request.LegalEntityId, null, "Human", actor, membershipId, "hr", "hr.insurance.enrollment.created", "employee_insurance_enrollment", id, null, JsonSerializer.Serialize(new { employeeId, employmentId, request.SchemeCode, request.EffectiveFrom, request.EffectiveTo }), null, "hr-api", correlationId, System.Diagnostics.Activity.Current?.TraceId.ToString(), clock.GetUtcNow(), null), token);
            return view;
        }, ct);
    }

    public Task<InsuranceEnrollmentView> ActivateInsuranceEnrollmentAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, Guid employmentId, Guid enrollmentId, Guid legalEntityId, InsuranceEnrollmentDecisionRequest request, Guid correlationId, CancellationToken ct) =>
        TransitionInsuranceEnrollmentAsync(tenant, actor, membershipId, employeeId, employmentId, enrollmentId, legalEntityId, request, "Active", correlationId, ct);

    public Task<InsuranceEnrollmentView> RejectInsuranceEnrollmentAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, Guid employmentId, Guid enrollmentId, Guid legalEntityId, InsuranceEnrollmentDecisionRequest request, Guid correlationId, CancellationToken ct) =>
        TransitionInsuranceEnrollmentAsync(tenant, actor, membershipId, employeeId, employmentId, enrollmentId, legalEntityId, request, "Rejected", correlationId, ct);

    private async Task<InsuranceEnrollmentView> TransitionInsuranceEnrollmentAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, Guid employmentId, Guid enrollmentId, Guid legalEntityId, InsuranceEnrollmentDecisionRequest request, string status, Guid correlationId, CancellationToken ct)
    {
        var reason = request.Reason?.Trim();
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        if (request.ExpectedVersion < 1 || status == "Rejected" && string.IsNullOrWhiteSpace(reason) || reason?.Length > 500)
            throw new HrException(status == "Rejected" ? "hr.insurance_rejection_reason_required" : "hr.insurance_enrollment_invalid");
        return await transactions.ExecuteAsync<InsuranceEnrollmentView>(tenant, async (context, token) =>
        {
            const string sql = "update business_hr.employee_insurance_enrollment set status=$1,version=version+1 where tenant_id=$2 and id=$3 and employment_id=$4 and legal_entity_id=$5 and version=$6 and status='Draft' and ($1<>'Active' or (effective_from<=$7 and (effective_to is null or effective_to>=$7))) returning id,employment_id,scheme_code,provider_code,masked_member_number,effective_from,effective_to,status,country_provider_version,evidence_file_ref,version";
            await using var command = new NpgsqlCommand(sql, context.Connection, context.Transaction);
            command.Parameters.AddWithValue(status); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(enrollmentId);
            command.Parameters.AddWithValue(employmentId); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue(request.ExpectedVersion); command.Parameters.AddWithValue(today);
            InsuranceEnrollmentView view;
            await using (var reader = await command.ExecuteReaderAsync(token))
            {
                if (!await reader.ReadAsync(token)) throw new HrException("hr.insurance_enrollment_concurrency_conflict");
                view = new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetFieldValue<DateOnly>(5), reader.IsDBNull(6) ? null : reader.GetFieldValue<DateOnly>(6),
                    reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetGuid(9), reader.GetInt64(10));
            }
            await AppendEventAsync(context.Connection, context.Transaction, tenant, employeeId, employmentId, status == "Active" ? "InsuranceEnrollmentActivated" : "InsuranceEnrollmentRejected", today, actor,
                new { enrollmentId, status, reason, view.EffectiveFrom, view.EffectiveTo }, correlationId, token);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction,
                new AuditEntry(Guid.CreateVersion7(), tenant, legalEntityId, null, "Human", actor, membershipId, "hr", "hr.insurance.enrollment." + status.ToLowerInvariant(), "employee_insurance_enrollment", enrollmentId,
                    null, JsonSerializer.Serialize(new { employeeId, employmentId, reason, expectedVersion = request.ExpectedVersion }), null, "hr-api", correlationId,
                    System.Diagnostics.Activity.Current?.TraceId.ToString(), clock.GetUtcNow(), null), token);
            return view;
        }, ct);
    }

    public async Task<IReadOnlyList<InsuranceEnrollmentView>> ListInsuranceEnrollmentsAsync(TenantId tenant, Guid employeeId, Guid employmentId, Guid legalEntityId, CancellationToken ct) =>
        await transactions.ExecuteAsync<IReadOnlyList<InsuranceEnrollmentView>>(tenant, async (context, token) =>
        {
            var result = new List<InsuranceEnrollmentView>();
            await using var command = new NpgsqlCommand("select i.id,i.employment_id,i.scheme_code,i.provider_code,i.masked_member_number,i.effective_from,i.effective_to,i.status,i.country_provider_version,i.evidence_file_ref,i.version from business_hr.employee_insurance_enrollment i join business_hr.employment e on e.tenant_id=i.tenant_id and e.id=i.employment_id where i.tenant_id=$1 and e.employee_id=$2 and i.employment_id=$3 and i.legal_entity_id=$4 order by i.effective_from desc,i.id desc limit 200", context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(employeeId); command.Parameters.AddWithValue(employmentId); command.Parameters.AddWithValue(legalEntityId);
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetFieldValue<DateOnly>(5), reader.IsDBNull(6) ? null : reader.GetFieldValue<DateOnly>(6), reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetGuid(9), reader.GetInt64(10)));
            return result;
        }, ct);

    public async Task<RevealedInsuranceMemberNumber> RevealInsuranceNumberAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, Guid employmentId, Guid enrollmentId, Guid legalEntityId, Guid correlationId, CancellationToken ct) =>
        await transactions.ExecuteAsync<RevealedInsuranceMemberNumber>(tenant, async (context, token) =>
        {
            string encrypted;
            await using (var command = new NpgsqlCommand("select i.encrypted_member_number from business_hr.employee_insurance_enrollment i join business_hr.employment e on e.tenant_id=i.tenant_id and e.id=i.employment_id where i.tenant_id=$1 and i.id=$2 and i.employment_id=$3 and e.employee_id=$4 and i.legal_entity_id=$5 and i.encrypted_member_number is not null", context.Connection, context.Transaction))
            {
                command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(enrollmentId); command.Parameters.AddWithValue(employmentId); command.Parameters.AddWithValue(employeeId); command.Parameters.AddWithValue(legalEntityId);
                encrypted = (string?)await command.ExecuteScalarAsync(token) ?? throw new HrException("hr.insurance_number_unavailable");
            }
            var revealed = new RevealedInsuranceMemberNumber(enrollmentId, identifierCrypto.UnprotectInsuranceNumber(encrypted), clock.GetUtcNow());
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, legalEntityId, null, "Human", actor, membershipId, "hr", "hr.insurance.member_number.revealed", "employee_insurance_enrollment", enrollmentId, null, JsonSerializer.Serialize(new { employeeId, employmentId }), null, "hr-api", correlationId, System.Diagnostics.Activity.Current?.TraceId.ToString(), revealed.RevealedAt, null), token);
            return revealed;
        }, ct);

    public async Task<CompensationAgreementView> CreateCompensationAgreementAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, Guid employmentId, CreateCompensationAgreementRequest request, Guid correlationId, CancellationToken ct)
    {
        if (request.Amount <= 0 || request.Amount > 999999999999999m || request.CurrencyCode?.Length != 3 || request.CurrencyCode.Any(static c => c is < 'A' or > 'Z') || request.EffectiveFrom == default || request.EffectiveTo < request.EffectiveFrom || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length > 1000)
            throw new HrException("hr.compensation_agreement_invalid");
        var allowedFrequencies = new[] { "Hourly", "Daily", "Weekly", "Biweekly", "Monthly", "Quarterly", "Annual", "Other" };
        if (!allowedFrequencies.Contains(request.PayFrequency, StringComparer.Ordinal)) throw new HrException("hr.compensation_frequency_invalid");
        if (request.EvidenceFileRef is { } evidence)
        {
            var file = await files.VerifyReadyReferenceAsync(tenant, evidence, "hr", "employee", employeeId, ct);
            if (file is null || file.Classification.ToLowerInvariant() is not ("confidential" or "restricted")) throw new HrException("hr.compensation_evidence_invalid");
        }
        var id = Guid.CreateVersion7();
        return await transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await using (var closePrior = new NpgsqlCommand("update business_hr.employment_compensation_agreement set effective_to=$5,status='Superseded',version=version+1 where tenant_id=$1 and employment_id=$2 and legal_entity_id=$3 and status='Active' and effective_from<$6 and (effective_to is null or effective_to >= $6) and exists(select 1 from business_hr.employment e where e.tenant_id=$1 and e.id=$2 and e.employee_id=$4 and e.legal_entity_id=$3)", context.Connection, context.Transaction))
            {
                closePrior.Parameters.AddWithValue(tenant.Value); closePrior.Parameters.AddWithValue(employmentId); closePrior.Parameters.AddWithValue(request.LegalEntityId); closePrior.Parameters.AddWithValue(employeeId); closePrior.Parameters.AddWithValue(request.EffectiveFrom.AddDays(-1)); closePrior.Parameters.AddWithValue(request.EffectiveFrom);
                await closePrior.ExecuteNonQueryAsync(token);
            }
            await using var insert = new NpgsqlCommand("insert into business_hr.employment_compensation_agreement(id,tenant_id,employment_id,legal_entity_id,currency_code,amount,pay_frequency,effective_from,effective_to,status,contract_id,reason,evidence_file_ref,created_by,created_at,version) select $1,$2,e.id,e.legal_entity_id,$5,$6,$7,$8,$9,'Active',$10,$11,$12,$13,$14,1 from business_hr.employment e where e.tenant_id=$2 and e.id=$3 and e.employee_id=$4 and e.legal_entity_id=$15 returning id,employment_id,currency_code,amount,pay_frequency,effective_from,effective_to,status,contract_id,reason,evidence_file_ref,version", context.Connection, context.Transaction);
            insert.Parameters.AddWithValue(id); insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(employmentId); insert.Parameters.AddWithValue(employeeId); insert.Parameters.AddWithValue(request.CurrencyCode);
            insert.Parameters.AddWithValue(request.Amount); insert.Parameters.AddWithValue(request.PayFrequency); insert.Parameters.AddWithValue(request.EffectiveFrom); insert.Parameters.AddWithValue((object?)request.EffectiveTo ?? DBNull.Value);
            insert.Parameters.AddWithValue((object?)request.ContractId ?? DBNull.Value); insert.Parameters.AddWithValue(request.Reason.Trim()); insert.Parameters.AddWithValue((object?)request.EvidenceFileRef ?? DBNull.Value); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(clock.GetUtcNow()); insert.Parameters.AddWithValue(request.LegalEntityId);
            await using var reader = await insert.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new HrException("hr.employment_not_found");
            var view = new CompensationAgreementView(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2).Trim(), reader.GetDecimal(3), reader.GetString(4), reader.GetFieldValue<DateOnly>(5), reader.IsDBNull(6) ? null : reader.GetFieldValue<DateOnly>(6), reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetGuid(8), reader.GetString(9), reader.IsDBNull(10) ? null : reader.GetGuid(10), reader.GetInt64(11));
            await reader.DisposeAsync();
            await AppendEventAsync(context.Connection, context.Transaction, tenant, employeeId, employmentId, "CompensationAgreementCreated", request.EffectiveFrom, actor, new { view.Id, view.CurrencyCode, view.Amount, view.PayFrequency, view.EffectiveFrom, view.EffectiveTo, reason = request.Reason }, correlationId, token);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, request.LegalEntityId, null, "Human", actor, membershipId, "hr", "hr.compensation.agreement.created", "employment_compensation_agreement", id, null, JsonSerializer.Serialize(new { employeeId, employmentId, view.CurrencyCode, view.Amount, view.PayFrequency, view.EffectiveFrom, view.EffectiveTo }), null, "hr-api", correlationId, System.Diagnostics.Activity.Current?.TraceId.ToString(), clock.GetUtcNow(), null), token);
            return view;
        }, ct);
    }

    public async Task<IReadOnlyList<CompensationAgreementView>> ListCompensationAgreementsAsync(TenantId tenant, Guid employeeId, Guid employmentId, Guid legalEntityId, CancellationToken ct) =>
        await transactions.ExecuteAsync<IReadOnlyList<CompensationAgreementView>>(tenant, async (context, token) =>
        {
            var result = new List<CompensationAgreementView>();
            await using var command = new NpgsqlCommand("select c.id,c.employment_id,c.currency_code,c.amount,c.pay_frequency,c.effective_from,c.effective_to,c.status,c.contract_id,c.reason,c.evidence_file_ref,c.version from business_hr.employment_compensation_agreement c join business_hr.employment e on e.tenant_id=c.tenant_id and e.id=c.employment_id where c.tenant_id=$1 and e.employee_id=$2 and c.employment_id=$3 and c.legal_entity_id=$4 order by c.effective_from desc,c.id desc limit 200", context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(employeeId); command.Parameters.AddWithValue(employmentId); command.Parameters.AddWithValue(legalEntityId);
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2).Trim(), reader.GetDecimal(3), reader.GetString(4), reader.GetFieldValue<DateOnly>(5), reader.IsDBNull(6) ? null : reader.GetFieldValue<DateOnly>(6), reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetGuid(8), reader.GetString(9), reader.IsDBNull(10) ? null : reader.GetGuid(10), reader.GetInt64(11)));
            return result;
        }, ct);

    public async Task<RevealedEmployeeIdentifier> RevealIdentifierAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, Guid identifierId, Guid legalEntityId, Guid correlationId, CancellationToken ct) =>
        await transactions.ExecuteAsync<RevealedEmployeeIdentifier>(tenant, async (context, token) =>
        {
            string encrypted; string type;
            await using (var command = new NpgsqlCommand("select i.encrypted_value,i.identifier_type from business_hr.employee_identifier i where i.tenant_id=$1 and i.id=$2 and i.employee_id=$3 and i.status='Active' and exists(select 1 from business_hr.employment e where e.tenant_id=i.tenant_id and e.employee_id=i.employee_id and e.legal_entity_id=$4)", context.Connection, context.Transaction))
            {
                command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(identifierId); command.Parameters.AddWithValue(employeeId); command.Parameters.AddWithValue(legalEntityId);
                await using var reader = await command.ExecuteReaderAsync(token); if (!await reader.ReadAsync(token)) throw new HrException("hr.identifier_not_found"); encrypted = reader.GetString(0); type = reader.GetString(1);
            }
            var value = identifierCrypto.Unprotect(encrypted); var revealedAt = clock.GetUtcNow();
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction,
                new AuditEntry(Guid.CreateVersion7(), tenant, legalEntityId, null, "Human", actor, membershipId, "hr", "employee.identifier_revealed", "employee_identifier", identifierId,
                    null, JsonSerializer.Serialize(new { employeeId, type }), null, "hr-api", correlationId,
                    System.Diagnostics.Activity.Current?.TraceId.ToString(), revealedAt, null), token);
            return new(identifierId, value, revealedAt);
        }, ct);

    public async Task<EmployeeIdentifierView> RevokeIdentifierAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, Guid identifierId, Guid legalEntityId, RevokeEmployeeIdentifierRequest request, Guid correlationId, CancellationToken ct)
    {
        var reason = (request.Reason ?? string.Empty).Trim();
        if (request.ExpectedVersion < 1 || reason.Length is < 1 or > 500) throw new HrException("hr.identifier_revoke_invalid");
        return await transactions.ExecuteAsync<EmployeeIdentifierView>(tenant, async (context, token) =>
        {
            await using var command = new NpgsqlCommand("update business_hr.employee_identifier i set status='Revoked',version=version+1 where i.tenant_id=$1 and i.id=$2 and i.employee_id=$3 and i.status='Active' and i.version=$4 and exists(select 1 from business_hr.employment e where e.tenant_id=i.tenant_id and e.employee_id=i.employee_id and e.legal_entity_id=$5) returning i.identifier_type,i.country_code,i.masked_display,i.issue_date,i.expiry_date,i.file_ref,i.version", context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(identifierId); command.Parameters.AddWithValue(employeeId); command.Parameters.AddWithValue(request.ExpectedVersion); command.Parameters.AddWithValue(legalEntityId);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new HrException("hr.identifier_concurrency_or_scope_conflict");
            var type = reader.GetString(0); var country = reader.IsDBNull(1) ? null : reader.GetString(1); var masked = reader.GetString(2); var issueDate = reader.IsDBNull(3) ? null : reader.GetFieldValue<DateOnly?>(3); var expiryDate = reader.IsDBNull(4) ? null : reader.GetFieldValue<DateOnly?>(4); Guid? fileRef = reader.IsDBNull(5) ? null : reader.GetGuid(5); var version = reader.GetInt64(6);
            await reader.CloseAsync();
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction,
                new AuditEntry(Guid.CreateVersion7(), tenant, legalEntityId, null, "Human", actor, membershipId, "hr", "employee.identifier_revoked", "employee_identifier", identifierId,
                    null, JsonSerializer.Serialize(new { employeeId, type, masked, reason }), null, "hr-api", correlationId,
                    System.Diagnostics.Activity.Current?.TraceId.ToString(), clock.GetUtcNow(), null), token);
            return new(identifierId, employeeId, type, country, masked, issueDate, expiryDate, ExpiryStatus(expiryDate), "Revoked", fileRef, version);
        }, ct);
    }

    private string ExpiryStatus(DateOnly? expiry) => expiry is null ? "NoExpiry" : expiry < DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime) ? "Expired" : expiry <= DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime).AddDays(30) ? "ExpiringSoon" : "Valid";
    internal static bool FileClassificationAllows(string actual, string requested)
    {
        var classification = actual.Trim().ToLowerInvariant();
        return requested.Trim().ToLowerInvariant() switch
        {
            "normal" => classification is "normal" or "internal" or "confidential" or "restricted" or "medical",
            "confidential" => classification is "confidential" or "restricted" or "medical",
            "medical" => classification is "medical" or "restricted",
            "restricted" => classification == "restricted",
            _ => false
        };
    }

    public async Task<EmployeeProfile?> GetEmployeeAsync(TenantId tenant, Guid employeeId, Guid? legalEntityId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, tx, tenant, ct);
        await using var command = new NpgsqlCommand("select e.id,e.arabic_name,e.english_name,e.preferred_name,e.status,e.version from business_hr.employee e where e.tenant_id=$1 and e.id=$2 and e.status<>'Archived' and ($3::uuid is null or exists(select 1 from business_hr.employment em where em.tenant_id=e.tenant_id and em.employee_id=e.id and em.legal_entity_id=$3))", connection, tx);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(employeeId); command.Parameters.AddWithValue((object?)legalEntityId ?? DBNull.Value);
        try
        {
            await using var reader = await command.ExecuteReaderAsync(ct);
            EmployeeProfile? result = null;
            if (await reader.ReadAsync(ct)) result = new(reader.GetGuid(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), Enum.Parse<EmployeeStatus>(reader.GetString(4)), reader.GetInt64(5));
            await reader.CloseAsync(); await tx.CommitAsync(ct); return result;
        }
        catch (PostgresException exception) when (exception.SqlState == "42501")
        {
            await tx.RollbackAsync(ct);
            return null;
        }
    }

    public async Task<EmployeeProfile?> GetMyEmployeeAsync(TenantId tenant, Guid membershipId, Guid legalEntityId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct); await using var tx = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tx, tenant, ct);
        await using var command = new NpgsqlCommand("select e.id,e.arabic_name,e.english_name,e.preferred_name,e.status,e.version,em.id,em.employee_id,em.legal_entity_id,em.employment_number,em.employment_type,em.status,em.start_date,em.end_date,em.version from business_hr.employee e join business_hr.employment em on em.tenant_id=e.tenant_id and em.employee_id=e.id where e.tenant_id=$1 and e.membership_id=$2 and e.status<>'Archived' and em.legal_entity_id=$3 order by case when em.status='Active' then 0 else 1 end,em.start_date desc,em.id limit 1", connection, tx);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(membershipId); command.Parameters.AddWithValue(legalEntityId);
        try
        {
            await using var reader = await command.ExecuteReaderAsync(ct); EmployeeProfile? result = null;
            if (await reader.ReadAsync(ct)) result = new(reader.GetGuid(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3), Enum.Parse<EmployeeStatus>(reader.GetString(4)), reader.GetInt64(5), new(reader.GetGuid(6), reader.GetGuid(7), reader.GetGuid(8), reader.GetString(9), Enum.Parse<EmploymentType>(reader.GetString(10)), Enum.Parse<EmploymentStatus>(reader.GetString(11)), reader.GetFieldValue<DateOnly>(12), reader.IsDBNull(13) ? null : reader.GetFieldValue<DateOnly>(13), reader.GetInt64(14)));
            await reader.CloseAsync(); await tx.CommitAsync(ct); return result;
        }
        catch (PostgresException exception) when (exception.SqlState == "42501")
        {
            await tx.RollbackAsync(ct);
            return null;
        }
    }

    public async Task<EmploymentView> CreateEmploymentAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, CreateEmploymentRequest request, Guid correlationId, CancellationToken ct)
    {
        var id = Guid.CreateVersion7();
        var employment = Employment.Create(id, tenant.Value, employeeId, request.LegalEntityId, "pending", request.Type, request.StartDate, request.EndDate, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));
        return await transactions.ExecuteAsync<EmploymentView>(tenant, async (context, token) =>
        {
            Guid? previousEmploymentId = null;
            DateOnly? previousEndDate = null;
            await using (var previous = new NpgsqlCommand("select id,end_date from business_hr.employment where tenant_id=$1 and employee_id=$2 and legal_entity_id=$3 and status in ('Terminated','Ended') order by start_date desc,id desc limit 1 for update", context.Connection, context.Transaction))
            {
                previous.Parameters.AddWithValue(tenant.Value); previous.Parameters.AddWithValue(employeeId); previous.Parameters.AddWithValue(request.LegalEntityId);
                await using var reader = await previous.ExecuteReaderAsync(token);
                if (await reader.ReadAsync(token))
                {
                    previousEmploymentId = reader.GetGuid(0);
                    previousEndDate = reader.IsDBNull(1) ? null : reader.GetFieldValue<DateOnly>(1);
                }
            }
            if (previousEmploymentId is not null && previousEndDate is { } previousEnd && request.StartDate <= previousEnd)
                throw new HrException("hr.rehire_start_must_follow_prior_employment");

            var number = await sequences.AllocateAsync(context, new SequenceDefinition("hr.employee-number", tenant, request.LegalEntityId, null, null, null, "EMP-", 6, 1), token);
            employment = employment with { EmploymentNumber = number };
            await using (var insert = new NpgsqlCommand("insert into business_hr.employment(tenant_id,id,employee_id,legal_entity_id,employment_number,employment_type,status,start_date,end_date,created_by,created_at,version) select $1,$2,e.id,$3,$4,$5,$6,$7,$8,$9,$10,1 from business_hr.employee e where e.tenant_id=$1 and e.id=$11 and e.status='Active'", context.Connection, context.Transaction))
            {
                insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(id); insert.Parameters.AddWithValue(request.LegalEntityId); insert.Parameters.AddWithValue(number);
                insert.Parameters.AddWithValue(employment.Type.ToString()); insert.Parameters.AddWithValue(employment.Status.ToString()); insert.Parameters.AddWithValue(employment.StartDate); insert.Parameters.AddWithValue((object?)employment.EndDate ?? DBNull.Value);
                insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(clock.GetUtcNow()); insert.Parameters.AddWithValue(employeeId);
                if (await insert.ExecuteNonQueryAsync(token) != 1) throw new HrException("hr.employee_not_found_or_inactive");
            }
            var isRehire = previousEmploymentId is not null;
            var eventType = isRehire ? "Rehired" : "EmploymentCreated";
            await AppendEventAsync(context.Connection, context.Transaction, tenant, employeeId, id, eventType, employment.StartDate, actor,
                new { employment.EmploymentNumber, employment.Type, previousEmploymentId }, correlationId, token);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction,
                new AuditEntry(Guid.CreateVersion7(), tenant, request.LegalEntityId, null, "Human", actor, membershipId, "hr", isRehire ? "employment.rehired" : "employment.created", "employment", id,
                    null, JsonSerializer.Serialize(new { employment.EmploymentNumber, employment.Type, employment.StartDate, previousEmploymentId }), null, "hr-api", correlationId,
                    System.Diagnostics.Activity.Current?.TraceId.ToString(), clock.GetUtcNow(), null), token);
            return new(id, employeeId, request.LegalEntityId, number, employment.Type, employment.Status, employment.StartDate, employment.EndDate, employment.Version);
        }, ct);
    }

    public async Task<IReadOnlyList<EmploymentView>> ListEmploymentsAsync(TenantId tenant, Guid employeeId, Guid legalEntityId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tx, tenant, ct);
        const string sql = "select id,employee_id,legal_entity_id,employment_number,employment_type,status,start_date,end_date,version from business_hr.employment where tenant_id=$1 and employee_id=$2 and legal_entity_id=$3 order by start_date desc,id limit 100";
        await using var command = new NpgsqlCommand(sql, connection, tx); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(employeeId); command.Parameters.AddWithValue(legalEntityId);
        var rows = new List<EmploymentView>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) rows.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), Enum.Parse<EmploymentType>(reader.GetString(4)), Enum.Parse<EmploymentStatus>(reader.GetString(5)), reader.GetFieldValue<DateOnly>(6), reader.IsDBNull(7) ? null : reader.GetFieldValue<DateOnly>(7), reader.GetInt64(8)));
        await reader.CloseAsync(); await tx.CommitAsync(ct); return rows;
    }

    public async Task<EmploymentView> ChangeEmploymentStatusAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employeeId, Guid employmentId, Guid legalEntityId, ChangeEmploymentStatusRequest request, Guid correlationId, CancellationToken ct)
    {
        if (request.ExpectedVersion < 1) throw new HrException("hr.employment_concurrency_conflict");
        return await transactions.ExecuteAsync<EmploymentView>(tenant, async (context, token) =>
        {
            Employment current;
            await using (var read = new NpgsqlCommand("select employment_number,employment_type,status,start_date,end_date,version from business_hr.employment where tenant_id=$1 and id=$2 and employee_id=$3 and legal_entity_id=$4 for update", context.Connection, context.Transaction))
            {
                read.Parameters.AddWithValue(tenant.Value); read.Parameters.AddWithValue(employmentId); read.Parameters.AddWithValue(employeeId); read.Parameters.AddWithValue(legalEntityId);
                await using var reader = await read.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new HrException("hr.employment_not_found");
                current = new(employmentId, tenant.Value, employeeId, legalEntityId, reader.GetString(0), Enum.Parse<EmploymentType>(reader.GetString(1)), Enum.Parse<EmploymentStatus>(reader.GetString(2)), reader.GetFieldValue<DateOnly>(3), reader.IsDBNull(4) ? null : reader.GetFieldValue<DateOnly>(4), reader.GetInt64(5));
            }
            if (current.Version != request.ExpectedVersion) throw new HrException("hr.employment_concurrency_conflict");
            OffboardingPlanRequest? offboarding = request.NewStatus is EmploymentStatus.Terminated or EmploymentStatus.Ended ? request.Offboarding : null;
            List<OffboardingTaskRequest> offboardingTasks = [];
            DateOnly? accessDeactivationOn = null;
            if (request.NewStatus is not (EmploymentStatus.Terminated or EmploymentStatus.Ended) && request.Offboarding is not null)
                throw new HrException("hr.offboarding_details_not_allowed");
            if (request.NewStatus is EmploymentStatus.Terminated or EmploymentStatus.Ended)
            {
                if (offboarding is null) throw new HrException("hr.offboarding_details_required");
                if (offboarding.ReasonType is not ("Resignation" or "Termination" or "ContractExpiry" or "Retirement" or "TemporaryAssignmentEnd" or "Other")
                    || offboarding.LastWorkingDate < current.StartDate || offboarding.LastWorkingDate > request.EffectiveOn
                    || offboarding.NoticeDate is { } noticeDate && (noticeDate < current.StartDate || noticeDate > offboarding.LastWorkingDate))
                    throw new HrException("hr.offboarding_dates_or_reason_invalid");
                offboardingTasks = (offboarding.Tasks ?? []).ToList();
                if (offboardingTasks.All(task => !string.Equals(task.Code, "FIXED_ASSET_RETURN", StringComparison.Ordinal)))
                    offboardingTasks.Add(new OffboardingTaskRequest("FIXED_ASSET_RETURN", "تسليم الأصول الثابتة", offboarding.LastWorkingDate, $"fixed-assets:employee:{employeeId:D}"));
                if (offboardingTasks.Count > 32 || offboardingTasks.Any(task => string.IsNullOrWhiteSpace(task.Code) || task.Code.Length > 60 || !char.IsAsciiLetterUpper(task.Code[0]) || task.Code.Any(ch => !char.IsAsciiLetterUpper(ch) && !char.IsAsciiDigit(ch) && ch != '_')
                    || string.IsNullOrWhiteSpace(task.TitleAr) || task.TitleAr.Trim().Length > 160 || task.ExternalReference?.Length > 160)
                    || offboardingTasks.Select(task => task.Code).Distinct(StringComparer.Ordinal).Count() != offboardingTasks.Count)
                    throw new HrException("hr.offboarding_task_invalid");
                accessDeactivationOn = offboarding.AccessDeactivationTiming switch
                {
                    "Immediately" when offboarding.CustomAccessDeactivationDate is null => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime),
                    "LastWorkingDay" when offboarding.CustomAccessDeactivationDate is null => offboarding.LastWorkingDate,
                    "Custom" when offboarding.CustomAccessDeactivationDate is { } customDate && customDate >= DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime) => customDate,
                    _ => throw new HrException("hr.offboarding_access_timing_invalid")
                };
            }
            if (current.Status == EmploymentStatus.PreHire && request.NewStatus == EmploymentStatus.Active)
            {
                await using var onboarding = new NpgsqlCommand("select exists(select 1 from business_hr.onboarding_case c join business_hr.onboarding_task t on t.tenant_id=c.tenant_id and t.case_id=c.id where c.tenant_id=$1 and c.employment_id=$2 and c.status='Open' and t.is_required and t.status<>'Completed')", context.Connection, context.Transaction);
                onboarding.Parameters.AddWithValue(tenant.Value); onboarding.Parameters.AddWithValue(employmentId);
                if ((bool)(await onboarding.ExecuteScalarAsync(token) ?? false)) throw new HrException("hr.onboarding_required_tasks_pending");
            }
            var asOfDate = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            var isTermination = request.NewStatus is EmploymentStatus.Terminated or EmploymentStatus.Ended;
            var scheduledTermination = isTermination && request.EffectiveOn > asOfDate;
            var transitioned = scheduledTermination
                ? current.ScheduleTermination(request.NewStatus, request.EffectiveOn, asOfDate, request.ReasonCode)
                : current.TransitionTo(request.NewStatus, request.EffectiveOn, asOfDate, request.ReasonCode);
            await using (var update = new NpgsqlCommand("update business_hr.employment set status=$1,end_date=$2,status_reason_code=$3,version=version+1 where tenant_id=$4 and id=$5 and version=$6", context.Connection, context.Transaction))
            {
                update.Parameters.AddWithValue(transitioned.Status.ToString()); update.Parameters.AddWithValue((object?)transitioned.EndDate ?? DBNull.Value); update.Parameters.AddWithValue(request.ReasonCode.Trim()); update.Parameters.AddWithValue(tenant.Value); update.Parameters.AddWithValue(employmentId); update.Parameters.AddWithValue(request.ExpectedVersion);
                if (await update.ExecuteNonQueryAsync(token) != 1) throw new HrException("hr.employment_concurrency_conflict");
            }
            IReadOnlyList<Guid> affectedContracts = [];
            if (isTermination && !scheduledTermination)
            {
                var contractIds = new List<Guid>();
                await using var endContracts = new NpgsqlCommand("update business_hr.employment_contract c set status=case when c.start_date>$1 then 'CancelledBeforeStart' else 'Ended' end,end_date=case when c.start_date>$1 then c.end_date when c.end_date is null or c.end_date>$1 then $1 else c.end_date end,version=version+1 where c.tenant_id=$2 and c.employment_id=$3 and c.status in ('Draft','Active') and (c.end_date is null or c.end_date>=$1) returning c.id", context.Connection, context.Transaction);
                endContracts.Parameters.AddWithValue(request.EffectiveOn); endContracts.Parameters.AddWithValue(tenant.Value); endContracts.Parameters.AddWithValue(employmentId);
                await using (var reader = await endContracts.ExecuteReaderAsync(token)) while (await reader.ReadAsync(token)) contractIds.Add(reader.GetGuid(0));
                affectedContracts = contractIds;
            }
            Guid? offboardingCaseId = null;
            if (offboarding is not null)
            {
                offboardingCaseId = Guid.CreateVersion7();
                var tasks = offboardingTasks;
                var caseStatus = tasks.Count == 0 ? "Completed" : "Open";
                var caseCreatedAt = clock.GetUtcNow();
                await using (var insertCase = new NpgsqlCommand("insert into business_hr.offboarding_case(id,tenant_id,legal_entity_id,employee_id,employment_id,reason_type,reason_code,notice_date,last_working_date,effective_end_date,access_deactivation_on,final_payroll_required,status,created_by,created_at,completed_at,version,target_status,termination_status,termination_applied_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,case when $13='Completed' then $15 else null end,1,$16,$17,$18)", context.Connection, context.Transaction))
                {
                    insertCase.Parameters.AddWithValue(offboardingCaseId.Value); insertCase.Parameters.AddWithValue(tenant.Value); insertCase.Parameters.AddWithValue(legalEntityId); insertCase.Parameters.AddWithValue(employeeId); insertCase.Parameters.AddWithValue(employmentId); insertCase.Parameters.AddWithValue(offboarding.ReasonType); insertCase.Parameters.AddWithValue(request.ReasonCode.Trim()); insertCase.Parameters.AddWithValue((object?)offboarding.NoticeDate ?? DBNull.Value); insertCase.Parameters.AddWithValue(offboarding.LastWorkingDate); insertCase.Parameters.AddWithValue(request.EffectiveOn); insertCase.Parameters.AddWithValue(accessDeactivationOn!.Value); insertCase.Parameters.AddWithValue(offboarding.FinalPayrollRequired); insertCase.Parameters.AddWithValue(caseStatus); insertCase.Parameters.AddWithValue(actor); insertCase.Parameters.AddWithValue(caseCreatedAt); insertCase.Parameters.AddWithValue(request.NewStatus.ToString()); insertCase.Parameters.AddWithValue(scheduledTermination ? "Scheduled" : "Effective"); insertCase.Parameters.AddWithValue(scheduledTermination ? DBNull.Value : caseCreatedAt);
                    await insertCase.ExecuteNonQueryAsync(token);
                }
                foreach (var task in tasks)
                {
                    await using var insertTask = new NpgsqlCommand("insert into business_hr.offboarding_task(id,tenant_id,legal_entity_id,case_id,code,title_ar,due_date,external_reference,status,version) values($1,$2,$3,$4,$5,$6,$7,$8,'Pending',1)", context.Connection, context.Transaction);
                    insertTask.Parameters.AddWithValue(Guid.CreateVersion7()); insertTask.Parameters.AddWithValue(tenant.Value); insertTask.Parameters.AddWithValue(legalEntityId); insertTask.Parameters.AddWithValue(offboardingCaseId.Value); insertTask.Parameters.AddWithValue(task.Code); insertTask.Parameters.AddWithValue(task.TitleAr.Trim()); insertTask.Parameters.AddWithValue((object?)task.DueDate ?? DBNull.Value); insertTask.Parameters.AddWithValue((object?)task.ExternalReference?.Trim() ?? DBNull.Value);
                    await insertTask.ExecuteNonQueryAsync(token);
                }
            }
            await AppendEventAsync(context.Connection, context.Transaction, tenant, employeeId, employmentId, scheduledTermination ? "EmploymentTerminationScheduled" : isTermination ? "EmploymentTerminated" : "EmploymentStatusChanged", request.EffectiveOn, actor,
                new { previousStatus = current.Status, status = transitioned.Status, targetStatus = request.NewStatus, scheduledTermination, request.ReasonCode, affectedContracts, offboardingCaseId }, correlationId, token);
            var occurredAt = clock.GetUtcNow();
            if (isTermination)
            {
                await using var membership = new NpgsqlCommand("select e.membership_id from business_hr.employee e where e.tenant_id=$1 and e.id=$2 and not exists(select 1 from business_hr.employment active where active.tenant_id=e.tenant_id and active.employee_id=e.id and active.id<>$4 and active.status in ('Active','Suspended','NoticePeriod') and active.start_date<=$3 and (active.end_date is null or active.end_date>=$3))", context.Connection, context.Transaction);
                membership.Parameters.AddWithValue(tenant.Value); membership.Parameters.AddWithValue(employeeId); membership.Parameters.AddWithValue(accessDeactivationOn!.Value); membership.Parameters.AddWithValue(employmentId);
                var linkedMembershipId = await membership.ExecuteScalarAsync(token) as Guid?;
                if (linkedMembershipId is { } linkedMembership)
                {
                    var message = new Shared.Contracts.Hr.HrMembershipDeactivationRequestedV1(employeeId, employmentId, linkedMembership, legalEntityId, accessDeactivationOn!.Value, request.ReasonCode.Trim(), occurredAt);
                    context.AddOutbox(new(Guid.CreateVersion7(), "HrMembershipDeactivationRequestedV1", JsonSerializer.Serialize(message), tenant, occurredAt, correlationId, employmentId, "hr", System.Diagnostics.Activity.Current?.Id));
                }
            }
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction,
                new AuditEntry(Guid.CreateVersion7(), tenant, legalEntityId, null, "Human", actor, membershipId, "hr", "employment.status_changed", "employment", employmentId,
                    JsonSerializer.Serialize(new { previousStatus = current.Status, status = transitioned.Status, current.Version }), JsonSerializer.Serialize(new { status = transitioned.Status, transitioned.EndDate, targetStatus = request.NewStatus, scheduledTermination, request.ReasonCode, affectedContracts, offboardingCaseId, accessDeactivationOn }), null, "hr-api", correlationId,
                    System.Diagnostics.Activity.Current?.TraceId.ToString(), occurredAt, null), token);
            return new(employmentId, employeeId, legalEntityId, current.EmploymentNumber, current.Type, transitioned.Status, current.StartDate, transitioned.EndDate, transitioned.Version);
        }, ct);
    }

    public async Task<OffboardingCaseView?> GetOffboardingCaseAsync(TenantId tenant, Guid employeeId, Guid employmentId, Guid legalEntityId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, transaction, tenant, ct);
        OffboardingCaseView? result = null;
        await using (var command = new NpgsqlCommand("select id,employment_id,legal_entity_id,reason_type,reason_code,notice_date,last_working_date,effective_end_date,access_deactivation_on,final_payroll_required,status,termination_status,version from business_hr.offboarding_case where tenant_id=$1 and employee_id=$2 and employment_id=$3 and legal_entity_id=$4", connection, transaction))
        {
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(employeeId); command.Parameters.AddWithValue(employmentId); command.Parameters.AddWithValue(legalEntityId);
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct)) result = new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetFieldValue<DateOnly>(5), reader.GetFieldValue<DateOnly>(6), reader.GetFieldValue<DateOnly>(7), reader.GetFieldValue<DateOnly>(8), reader.GetBoolean(9), reader.GetString(10), reader.GetString(11), reader.GetInt64(12), []);
        }
        if (result is null) { await transaction.CommitAsync(ct); return null; }
        var tasks = new List<OffboardingTaskView>();
        await using (var command = new NpgsqlCommand("select id,code,title_ar,due_date,external_reference,status,version from business_hr.offboarding_task where tenant_id=$1 and case_id=$2 order by due_date nulls last,code,id", connection, transaction))
        {
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(result.Id);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) tasks.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetFieldValue<DateOnly>(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(5), reader.GetInt64(6)));
        }
        await transaction.CommitAsync(ct);
        return result with { Tasks = tasks };
    }

    public async Task<OffboardingCaseView> CompleteOffboardingTaskAsync(TenantId tenant, Guid actor, Guid membershipId, Guid taskId, Guid legalEntityId, CompleteOffboardingTaskRequest request, Guid correlationId, CancellationToken ct)
    {
        var note = request.CompletionNote?.Trim();
        if (request.ExpectedVersion < 1 || note?.Length > 500) throw new HrException("hr.offboarding_task_invalid");
        Guid completedEmploymentId = Guid.Empty;
        Guid completedEmployeeId = Guid.Empty;
        var caseId = await transactions.ExecuteAsync<Guid>(tenant, async (context, token) =>
        {
            Guid id;
            Guid employeeId;
            await using (var task = new NpgsqlCommand("select t.case_id,c.employment_id,c.employee_id from business_hr.offboarding_task t join business_hr.offboarding_case c on c.tenant_id=t.tenant_id and c.id=t.case_id where t.tenant_id=$1 and t.id=$2 and t.legal_entity_id=$3 and t.status='Pending' and t.version=$4 and c.status='Open' for update of t,c", context.Connection, context.Transaction))
            {
                task.Parameters.AddWithValue(tenant.Value); task.Parameters.AddWithValue(taskId); task.Parameters.AddWithValue(legalEntityId); task.Parameters.AddWithValue(request.ExpectedVersion);
                await using var reader = await task.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new HrException("hr.offboarding_task_not_found_or_changed");
                id = reader.GetGuid(0); completedEmploymentId = reader.GetGuid(1); employeeId = reader.GetGuid(2); completedEmployeeId = employeeId;
            }
            var now = clock.GetUtcNow();
            await using (var updateTask = new NpgsqlCommand("update business_hr.offboarding_task set status='Completed',completion_note=$1,completed_by=$2,completed_at=$3,version=version+1 where tenant_id=$4 and id=$5 and version=$6 and status='Pending'", context.Connection, context.Transaction))
            {
                updateTask.Parameters.AddWithValue((object?)note ?? DBNull.Value); updateTask.Parameters.AddWithValue(actor); updateTask.Parameters.AddWithValue(now); updateTask.Parameters.AddWithValue(tenant.Value); updateTask.Parameters.AddWithValue(taskId); updateTask.Parameters.AddWithValue(request.ExpectedVersion);
                if (await updateTask.ExecuteNonQueryAsync(token) != 1) throw new HrException("hr.offboarding_task_not_found_or_changed");
            }
            await using (var finishCase = new NpgsqlCommand("update business_hr.offboarding_case set status='Completed',completed_at=$1,version=version+1 where tenant_id=$2 and id=$3 and status='Open' and not exists(select 1 from business_hr.offboarding_task pending where pending.tenant_id=$2 and pending.case_id=$3 and pending.status='Pending')", context.Connection, context.Transaction))
            {
                finishCase.Parameters.AddWithValue(now); finishCase.Parameters.AddWithValue(tenant.Value); finishCase.Parameters.AddWithValue(id);
                await finishCase.ExecuteNonQueryAsync(token);
            }
            await AppendEventAsync(context.Connection, context.Transaction, tenant, employeeId, completedEmploymentId, "OffboardingTaskCompleted", DateOnly.FromDateTime(now.UtcDateTime), actor, new { offboardingCaseId = id, taskId }, correlationId, token);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new AuditEntry(Guid.CreateVersion7(), tenant, legalEntityId, null, "Human", actor, membershipId, "hr", "offboarding.task_completed", "offboarding_task", taskId, JsonSerializer.Serialize(new { request.ExpectedVersion }), JsonSerializer.Serialize(new { status = "Completed", completionNote = note, offboardingCaseId = id }), note, "hr-api", correlationId, System.Diagnostics.Activity.Current?.TraceId.ToString(), now, null), token);
            return id;
        }, ct);
        return await GetOffboardingCaseAsync(tenant, completedEmployeeId, completedEmploymentId, legalEntityId, ct)
            ?? throw new HrException("hr.offboarding_case_not_found");
    }

    public Task<OrganizationDefinition> CreateDepartmentAsync(TenantId tenant, Guid actor, Guid legalEntityId, CreateDepartmentRequest request, CancellationToken ct) =>
        CreateOrganizationDefinitionAsync(tenant, actor, legalEntityId, request.Code, request.NameAr, request.NameEn, request.ParentId, "department", ct);

    public Task<OrganizationDefinition> CreatePositionAsync(TenantId tenant, Guid actor, Guid legalEntityId, CreatePositionRequest request, CancellationToken ct) =>
        CreateOrganizationDefinitionAsync(tenant, actor, legalEntityId, request.Code, request.TitleAr, request.TitleEn, null, "position", ct);

    public async Task<IReadOnlyList<OrganizationDefinition>> ListDepartmentsAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct) =>
        await ListOrganizationDefinitionsAsync(tenant, legalEntityId, "department", ct);

    public async Task<IReadOnlyList<OrganizationDefinition>> ListPositionsAsync(TenantId tenant, Guid legalEntityId, CancellationToken ct) =>
        await ListOrganizationDefinitionsAsync(tenant, legalEntityId, "position", ct);

    public Task<OrganizationDefinition> ArchiveDepartmentAsync(TenantId tenant, Guid actor, Guid membershipId, Guid id, Guid legalEntityId, Guid correlationId, CancellationToken ct) =>
        ArchiveOrganizationDefinitionAsync(tenant, actor, membershipId, id, legalEntityId, correlationId, "department", ct);

    public Task<OrganizationDefinition> ArchivePositionAsync(TenantId tenant, Guid actor, Guid membershipId, Guid id, Guid legalEntityId, Guid correlationId, CancellationToken ct) =>
        ArchiveOrganizationDefinitionAsync(tenant, actor, membershipId, id, legalEntityId, correlationId, "position", ct);

    private async Task<OrganizationDefinition> ArchiveOrganizationDefinitionAsync(TenantId tenant, Guid actor, Guid membershipId, Guid id, Guid legalEntityId, Guid correlationId, string kind, CancellationToken ct)
    {
        var isDepartment = kind == "department";
        var table = isDepartment ? "department" : "position";
        var nameColumn = isDepartment ? "name_ar" : "title_ar";
        var englishColumn = isDepartment ? "name_en" : "title_en";
        return await transactions.ExecuteAsync<OrganizationDefinition>(tenant, async (context, token) =>
        {
            var dependencies = isDepartment
                ? "exists(select 1 from business_hr.employment_assignment a join business_hr.employment e on e.tenant_id=a.tenant_id and e.id=a.employment_id where a.tenant_id=d.tenant_id and a.department_id=d.id and a.effective_from<=current_date and (a.effective_to is null or a.effective_to>current_date) and e.status in ('Active','Suspended','NoticePeriod')) or exists(select 1 from business_hr.department child where child.tenant_id=d.tenant_id and child.legal_entity_id=d.legal_entity_id and child.parent_id=d.id and child.status='Active')"
                : "exists(select 1 from business_hr.employment_assignment a join business_hr.employment e on e.tenant_id=a.tenant_id and e.id=a.employment_id where a.tenant_id=p.tenant_id and a.position_id=p.id and a.effective_from<=current_date and (a.effective_to is null or a.effective_to>current_date) and e.status in ('Active','Suspended','NoticePeriod'))";
            var alias = isDepartment ? "d" : "p";
            await using var command = new NpgsqlCommand($"update business_hr.{table} {alias} set status='Archived',version=version+1 where {alias}.tenant_id=$1 and {alias}.id=$2 and {alias}.legal_entity_id=$3 and {alias}.status='Active' and not ({dependencies}) returning {alias}.id,{alias}.legal_entity_id,{alias}.code,{alias}.{nameColumn},{alias}.{englishColumn},{(isDepartment ? "d.parent_id" : "null::uuid")},{alias}.version", context.Connection, context.Transaction);
            command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(legalEntityId);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new HrException("hr.organization_definition_in_use_or_not_found");
            var result = new OrganizationDefinition(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetGuid(5), "Archived", reader.GetInt64(6));
            await reader.CloseAsync();
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction,
                new AuditEntry(Guid.CreateVersion7(), tenant, legalEntityId, null, "Human", actor, membershipId, "hr", $"{kind}.archived", kind, id,
                    null, JsonSerializer.Serialize(result), null, "hr-api", correlationId, System.Diagnostics.Activity.Current?.TraceId.ToString(), clock.GetUtcNow(), null), token);
            return result;
        }, ct);
    }

    private async Task<OrganizationDefinition> CreateOrganizationDefinitionAsync(TenantId tenant, Guid actor, Guid legalEntityId, string code, string nameAr, string? nameEn, Guid? parentId, string kind, CancellationToken ct)
    {
        var normalizedCode = (code ?? string.Empty).Trim().ToUpperInvariant();
        var normalizedName = (nameAr ?? string.Empty).Trim().Normalize(System.Text.NormalizationForm.FormKC);
        if (normalizedCode.Length is < 1 or > 40 || normalizedName.Length is < 1 or > 160 || (nameEn?.Trim().Length ?? 0) > 160)
            throw new HrException("hr.organization_definition_invalid");
        if (kind is not ("department" or "position")) throw new ArgumentOutOfRangeException(nameof(kind));
        var id = Guid.CreateVersion7();
        return await transactions.ExecuteAsync<OrganizationDefinition>(tenant, async (context, token) =>
        {
            var table = kind == "department" ? "department" : "position";
            var nameColumn = kind == "department" ? "name_ar" : "title_ar";
            var englishColumn = kind == "department" ? "name_en" : "title_en";
            var sql = $"insert into business_hr.{table}(tenant_id,id,legal_entity_id,code,{nameColumn},{englishColumn}," + (kind == "department" ? "parent_id," : string.Empty) + "status,version,created_at) values($1,$2,$3,$4,$5,$6," + (kind == "department" ? "$7," : string.Empty) + "'Active',1,$" + (kind == "department" ? "8" : "7") + ")";
            await using var insert = new NpgsqlCommand(sql, context.Connection, context.Transaction);
            insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(id); insert.Parameters.AddWithValue(legalEntityId); insert.Parameters.AddWithValue(normalizedCode);
            insert.Parameters.AddWithValue(normalizedName); insert.Parameters.AddWithValue((object?)nameEn?.Trim() ?? DBNull.Value);
            if (kind == "department") insert.Parameters.AddWithValue((object?)parentId ?? DBNull.Value);
            insert.Parameters.AddWithValue(clock.GetUtcNow());
            await insert.ExecuteNonQueryAsync(token);
            return new(id, legalEntityId, normalizedCode, normalizedName, nameEn?.Trim(), parentId, "Active", 1);
        }, ct);
    }

    private async Task<IReadOnlyList<OrganizationDefinition>> ListOrganizationDefinitionsAsync(TenantId tenant, Guid legalEntityId, string kind, CancellationToken ct)
    {
        if (kind is not ("department" or "position")) throw new ArgumentOutOfRangeException(nameof(kind));
        var table = kind == "department" ? "department" : "position";
        var nameColumn = kind == "department" ? "name_ar" : "title_ar";
        var englishColumn = kind == "department" ? "name_en" : "title_en";
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tx, tenant, ct);
        var parentColumn = kind == "department" ? "parent_id" : "null::uuid";
        await using var command = new NpgsqlCommand($"select id,legal_entity_id,code,{nameColumn},{englishColumn},{parentColumn},status,version from business_hr.{table} where tenant_id=$1 and legal_entity_id=$2 order by code,id limit 500", connection, tx);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(legalEntityId);
        var result = new List<OrganizationDefinition>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetGuid(5), reader.GetString(6), reader.GetInt64(7)));
        await reader.CloseAsync(); await tx.CommitAsync(ct); return result;
    }

    public async Task<AssignmentView> CreateAssignmentAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employmentId, CreateAssignmentRequest request, Guid correlationId, CancellationToken ct)
    {
        if (request.EffectiveFrom == default || request.EffectiveTo <= request.EffectiveFrom || string.IsNullOrWhiteSpace(request.ReasonCode) || request.ReasonCode.Trim().Length > 80)
            throw new HrException("hr.assignment_invalid");
        var id = Guid.CreateVersion7();
        return await transactions.ExecuteAsync<AssignmentView>(tenant, async (context, token) =>
        {
            Guid employeeIdForEvent;
            await using (var validate = new NpgsqlCommand("select e.employee_id from business_hr.employment e where e.tenant_id=$1 and e.id=$2 and e.legal_entity_id=$3 and e.status in ('PreHire','Active','Suspended','NoticePeriod') and ($4::uuid is null or exists(select 1 from business_hr.department d where d.tenant_id=$1 and d.id=$4 and d.legal_entity_id=$3 and d.status='Active')) and ($5::uuid is null or exists(select 1 from business_hr.position p where p.tenant_id=$1 and p.id=$5 and p.legal_entity_id=$3 and p.status='Active')) and ($6::uuid is null or exists(select 1 from platform.branch b where b.tenant_id=$1 and b.id=$6 and b.legal_entity_id=$3 and b.status='Active')) and ($7::uuid is null or exists(select 1 from business_hr.work_calendar wc where wc.tenant_id=$1 and wc.id=$7 and wc.legal_entity_id=$3 and wc.status='Active')) for update of e", context.Connection, context.Transaction))
            {
                validate.Parameters.AddWithValue(tenant.Value); validate.Parameters.AddWithValue(employmentId); validate.Parameters.AddWithValue(request.LegalEntityId);
                validate.Parameters.AddWithValue((object?)request.DepartmentId ?? DBNull.Value); validate.Parameters.AddWithValue((object?)request.PositionId ?? DBNull.Value); validate.Parameters.AddWithValue((object?)request.BranchId ?? DBNull.Value); validate.Parameters.AddWithValue((object?)request.WorkCalendarId ?? DBNull.Value);
                employeeIdForEvent = await validate.ExecuteScalarAsync(token) as Guid? ?? throw new HrException("hr.assignment_reference_invalid");
            }
            DateOnly? nextAssignmentStart = null;
            await using (var successor = new NpgsqlCommand("select effective_from from business_hr.employment_assignment where tenant_id=$1 and employment_id=$2 and effective_from>$3 order by effective_from,id limit 1", context.Connection, context.Transaction))
            {
                successor.Parameters.AddWithValue(tenant.Value); successor.Parameters.AddWithValue(employmentId); successor.Parameters.AddWithValue(request.EffectiveFrom);
                var value = await successor.ExecuteScalarAsync(token);
                if (value is DateOnly date) nextAssignmentStart = date;
            }
            var effectiveTo = request.EffectiveTo;
            if (nextAssignmentStart is { } next)
            {
                if (effectiveTo is { } requestedEnd && requestedEnd > next) throw new HrException("hr.assignment_effective_dates_overlap");
                if (effectiveTo is null) effectiveTo = next;
            }
            if (effectiveTo <= request.EffectiveFrom) throw new HrException("hr.assignment_invalid");
            await using (var predecessor = new NpgsqlCommand("select id,effective_to,version from business_hr.employment_assignment where tenant_id=$1 and employment_id=$2 and effective_from<$3 and (effective_to is null or effective_to>$3) order by effective_from desc,id desc limit 1 for update", context.Connection, context.Transaction))
            {
                predecessor.Parameters.AddWithValue(tenant.Value); predecessor.Parameters.AddWithValue(employmentId); predecessor.Parameters.AddWithValue(request.EffectiveFrom);
                await using var reader = await predecessor.ExecuteReaderAsync(token);
                if (await reader.ReadAsync(token))
                {
                    var priorId = reader.GetGuid(0);
                    var priorVersion = reader.GetInt64(2);
                    await reader.CloseAsync();
                    await using var closePrior = new NpgsqlCommand("update business_hr.employment_assignment set effective_to=$1,version=version+1 where tenant_id=$2 and id=$3 and version=$4", context.Connection, context.Transaction);
                    closePrior.Parameters.AddWithValue(request.EffectiveFrom); closePrior.Parameters.AddWithValue(tenant.Value); closePrior.Parameters.AddWithValue(priorId); closePrior.Parameters.AddWithValue(priorVersion);
                    if (await closePrior.ExecuteNonQueryAsync(token) != 1) throw new HrException("hr.assignment_concurrency_conflict");
                }
            }
            await using (var insert = new NpgsqlCommand("insert into business_hr.employment_assignment(tenant_id,id,legal_entity_id,employment_id,branch_id,department_id,position_id,manager_employment_id,work_calendar_id,effective_from,effective_to,reason_code,created_by,created_at,version) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,1)", context.Connection, context.Transaction))
            {
                insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(id); insert.Parameters.AddWithValue(request.LegalEntityId); insert.Parameters.AddWithValue(employmentId);
                insert.Parameters.AddWithValue((object?)request.BranchId ?? DBNull.Value); insert.Parameters.AddWithValue((object?)request.DepartmentId ?? DBNull.Value); insert.Parameters.AddWithValue((object?)request.PositionId ?? DBNull.Value);
                insert.Parameters.AddWithValue((object?)request.ManagerEmploymentId ?? DBNull.Value); insert.Parameters.AddWithValue((object?)request.WorkCalendarId ?? DBNull.Value); insert.Parameters.AddWithValue(request.EffectiveFrom); insert.Parameters.AddWithValue((object?)effectiveTo ?? DBNull.Value);
                insert.Parameters.AddWithValue(request.ReasonCode.Trim()); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(clock.GetUtcNow()); await insert.ExecuteNonQueryAsync(token);
            }
            await AppendEventAsync(context.Connection, context.Transaction, tenant, employeeIdForEvent, employmentId, "AssignmentChanged", request.EffectiveFrom, actor,
                new { request.LegalEntityId, request.BranchId, request.DepartmentId, request.PositionId, request.ManagerEmploymentId, request.WorkCalendarId, request.ReasonCode }, correlationId, token);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction,
                new AuditEntry(Guid.CreateVersion7(), tenant, request.LegalEntityId, request.BranchId, "Human", actor, membershipId, "hr", "assignment.created", "employment_assignment", id,
                    null, JsonSerializer.Serialize(new { employmentId, request.BranchId, request.DepartmentId, request.PositionId, request.ManagerEmploymentId, request.WorkCalendarId, request.EffectiveFrom, effectiveTo }),
                    request.ReasonCode, "hr-api", correlationId, System.Diagnostics.Activity.Current?.TraceId.ToString(), clock.GetUtcNow(), null), token);
            return new(id, employmentId, request.LegalEntityId, request.BranchId, request.DepartmentId, request.PositionId, request.ManagerEmploymentId, request.EffectiveFrom, effectiveTo, request.ReasonCode.Trim(), 1, request.WorkCalendarId);
        }, ct);
    }

    public async Task<IReadOnlyList<AssignmentView>> ListAssignmentsAsync(TenantId tenant, Guid employmentId, Guid legalEntityId, DateOnly? asOf, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        await SetTenantAsync(connection, tx, tenant, ct);
        const string sql = "select a.id,a.employment_id,a.legal_entity_id,a.branch_id,a.department_id,a.position_id,a.manager_employment_id,a.effective_from,a.effective_to,a.reason_code,a.version,a.work_calendar_id from business_hr.employment_assignment a join business_hr.employment e on e.tenant_id=a.tenant_id and e.id=a.employment_id where a.tenant_id=$1 and a.employment_id=$2 and a.legal_entity_id=$3 and ($4::date is null or (a.effective_from<=$4 and (a.effective_to is null or a.effective_to>$4))) order by a.effective_from desc,a.id desc limit 100";
        await using var command = new NpgsqlCommand(sql, connection, tx);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(employmentId); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue((object?)asOf ?? DBNull.Value);
        var rows = new List<AssignmentView>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) rows.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.IsDBNull(3) ? null : reader.GetGuid(3), reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.IsDBNull(5) ? null : reader.GetGuid(5), reader.IsDBNull(6) ? null : reader.GetGuid(6), reader.GetFieldValue<DateOnly>(7), reader.IsDBNull(8) ? null : reader.GetFieldValue<DateOnly>(8), reader.GetString(9), reader.GetInt64(10), reader.IsDBNull(11) ? null : reader.GetGuid(11)));
        await reader.CloseAsync(); await tx.CommitAsync(ct); return rows;
    }

    public async Task<ContractView> CreateContractAsync(TenantId tenant, Guid actor, Guid membershipId, Guid employmentId, CreateContractRequest request, Guid correlationId, CancellationToken ct)
    {
        var type = (request.ContractType ?? string.Empty).Trim();
        if (type.Length is < 1 or > 60 || request.StartDate == default || request.EndDate < request.StartDate
            || request.ProbationStart is not null != (request.ProbationEnd is not null)
            || request.ProbationStart is { } probationStart && (probationStart < request.StartDate || request.ProbationEnd > request.EndDate && request.EndDate is not null))
            throw new HrException("hr.contract_terms_invalid");
        foreach (var value in new[] { request.WorkingHoursBasis, request.NoticePeriodTerms, request.CountryProviderVersion, request.AutoRenewalTerms })
            if (value?.Length > 1000) throw new HrException("hr.contract_terms_invalid");
        var id = Guid.CreateVersion7();
        return await transactions.ExecuteAsync<ContractView>(tenant, async (context, token) =>
        {
            Guid employeeId;
            await using (var employment = new NpgsqlCommand("select employee_id from business_hr.employment where tenant_id=$1 and id=$2 and legal_entity_id=$3 and status in ('PreHire','Active','Suspended','NoticePeriod')", context.Connection, context.Transaction))
            {
                employment.Parameters.AddWithValue(tenant.Value); employment.Parameters.AddWithValue(employmentId); employment.Parameters.AddWithValue(request.LegalEntityId);
                employeeId = await employment.ExecuteScalarAsync(token) as Guid? ?? throw new HrException("hr.contract_employment_invalid");
            }
            await using var insert = new NpgsqlCommand("insert into business_hr.employment_contract(id,tenant_id,legal_entity_id,employment_id,contract_type,start_date,end_date,probation_start,probation_end,work_calendar_id,working_hours_basis,notice_period_terms,compensation_agreement_ref,country_provider_version,auto_renewal_terms,status,renewal_of_contract_id,created_by,created_at,version) select $1,$2,$3,e.id,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,'Draft',$15,$16,$17,1 from business_hr.employment e where e.tenant_id=$2 and e.id=$18 and e.legal_entity_id=$3 and e.status in ('PreHire','Active','Suspended','NoticePeriod')", context.Connection, context.Transaction);
            insert.Parameters.AddWithValue(id); insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(request.LegalEntityId); insert.Parameters.AddWithValue(type); insert.Parameters.AddWithValue(request.StartDate);
            insert.Parameters.AddWithValue((object?)request.EndDate ?? DBNull.Value); insert.Parameters.AddWithValue((object?)request.ProbationStart ?? DBNull.Value); insert.Parameters.AddWithValue((object?)request.ProbationEnd ?? DBNull.Value);
            insert.Parameters.AddWithValue((object?)request.WorkCalendarId ?? DBNull.Value); insert.Parameters.AddWithValue((object?)request.WorkingHoursBasis?.Trim() ?? DBNull.Value); insert.Parameters.AddWithValue((object?)request.NoticePeriodTerms?.Trim() ?? DBNull.Value);
            insert.Parameters.AddWithValue((object?)request.CompensationAgreementRef ?? DBNull.Value); insert.Parameters.AddWithValue((object?)request.CountryProviderVersion?.Trim() ?? DBNull.Value); insert.Parameters.AddWithValue((object?)request.AutoRenewalTerms?.Trim() ?? DBNull.Value);
            insert.Parameters.AddWithValue((object?)request.RenewalOfContractId ?? DBNull.Value); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(clock.GetUtcNow()); insert.Parameters.AddWithValue(employmentId);
            if (await insert.ExecuteNonQueryAsync(token) != 1) throw new HrException("hr.contract_employment_invalid");
            await AppendEventAsync(context.Connection, context.Transaction, tenant, employeeId, employmentId, "ContractDraftCreated", request.StartDate, actor, new { id, type, request.StartDate, request.EndDate }, correlationId, token);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction,
                new AuditEntry(Guid.CreateVersion7(), tenant, request.LegalEntityId, null, "Human", actor, membershipId, "hr", "contract.draft_created", "employment_contract", id,
                    null, JsonSerializer.Serialize(new { employmentId, type, request.StartDate, request.EndDate, request.RenewalOfContractId }), null, "hr-api", correlationId,
                    System.Diagnostics.Activity.Current?.TraceId.ToString(), clock.GetUtcNow(), null), token);
            return new(id, employmentId, request.LegalEntityId, null, type, request.StartDate, request.EndDate, request.ProbationStart, request.ProbationEnd, "Draft", null, request.RenewalOfContractId, 1);
        }, ct);
    }

    public async Task<IReadOnlyList<ContractView>> ListContractsAsync(TenantId tenant, Guid employmentId, Guid legalEntityId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tx, tenant, ct);
        const string sql = "select id,employment_id,legal_entity_id,contract_number,contract_type,start_date,end_date,probation_start,probation_end,status,signed_file_ref,renewal_of_contract_id,version from business_hr.employment_contract where tenant_id=$1 and employment_id=$2 and legal_entity_id=$3 order by start_date desc,id desc limit 100";
        await using var command = new NpgsqlCommand(sql, connection, tx); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(employmentId); command.Parameters.AddWithValue(legalEntityId);
        var rows = new List<ContractView>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) rows.Add(ReadContract(reader));
        await reader.CloseAsync(); await tx.CommitAsync(ct); return rows;
    }

    public async Task<ContractView> ActivateContractAsync(TenantId tenant, Guid actor, Guid membershipId, Guid contractId, Guid legalEntityId, ActivateContractRequest request, Guid correlationId, CancellationToken ct)
    {
        if (request.SignedFileRef is { } signedFile)
        {
            var signedEvidence = await files.VerifyReadyReferenceAsync(tenant, signedFile, "hr", "employment_contract", contractId, ct);
            if (signedEvidence is null || signedEvidence.Classification.ToLowerInvariant() is not ("confidential" or "restricted")) throw new HrException("hr.contract_file_reference_invalid");
        }
        return await transactions.ExecuteAsync<ContractView>(tenant, async (context, token) =>
        {
            await using var command = new NpgsqlCommand("update business_hr.employment_contract set status='Active',signed_file_ref=$1,version=version+1 where tenant_id=$2 and id=$3 and legal_entity_id=$4 and status='Draft' returning id,employment_id,legal_entity_id,contract_number,contract_type,start_date,end_date,probation_start,probation_end,status,signed_file_ref,renewal_of_contract_id,version", context.Connection, context.Transaction);
            command.Parameters.AddWithValue((object?)request.SignedFileRef ?? DBNull.Value); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(contractId); command.Parameters.AddWithValue(legalEntityId);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new HrException("hr.contract_not_draft");
            var result = ReadContract(reader); await reader.CloseAsync();
            var employeeId = await GetEmploymentEmployeeIdAsync(context.Connection, context.Transaction, tenant, result.EmploymentId, token);
            await AppendEventAsync(context.Connection, context.Transaction, tenant, employeeId, result.EmploymentId, "ContractActivated", result.StartDate ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), actor, new { result.Id, result.ContractType, result.StartDate, result.EndDate }, correlationId, token);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction,
                new AuditEntry(Guid.CreateVersion7(), tenant, legalEntityId, null, "Human", actor, membershipId, "hr", "contract.activated", "employment_contract", contractId,
                    null, JsonSerializer.Serialize(new { result.EmploymentId, result.StartDate, result.EndDate, request.SignedFileRef }), null, "hr-api", correlationId,
                    System.Diagnostics.Activity.Current?.TraceId.ToString(), clock.GetUtcNow(), null), token);
            return result;
        }, ct);
    }

    public async Task<ContractView> EndContractAsync(TenantId tenant, Guid actor, Guid membershipId, Guid contractId, Guid legalEntityId, EndContractRequest request, Guid correlationId, CancellationToken ct)
    {
        var reason = (request.Reason ?? string.Empty).Trim();
        if (request.EffectiveOn == default || reason.Length is < 1 or > 500) throw new HrException("hr.contract_end_invalid");
        return await transactions.ExecuteAsync<ContractView>(tenant, async (context, token) =>
        {
            await using var command = new NpgsqlCommand("update business_hr.employment_contract set end_date=$1,status=case when $1<=current_date then 'Ended' else 'Active' end,version=version+1 where tenant_id=$2 and id=$3 and legal_entity_id=$4 and status='Active' and (start_date is null or start_date<=$1) and (end_date is null or end_date>=$1) returning id,employment_id,legal_entity_id,contract_number,contract_type,start_date,end_date,probation_start,probation_end,status,signed_file_ref,renewal_of_contract_id,version", context.Connection, context.Transaction);
            command.Parameters.AddWithValue(request.EffectiveOn); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(contractId); command.Parameters.AddWithValue(legalEntityId);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new HrException("hr.contract_not_active_or_date_invalid");
            var result = ReadContract(reader); await reader.CloseAsync();
            var employeeId = await GetEmploymentEmployeeIdAsync(context.Connection, context.Transaction, tenant, result.EmploymentId, token);
            await AppendEventAsync(context.Connection, context.Transaction, tenant, employeeId, result.EmploymentId, "ContractEnded", request.EffectiveOn, actor, new { result.Id }, correlationId, token);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction,
                new AuditEntry(Guid.CreateVersion7(), tenant, legalEntityId, null, "Human", actor, membershipId, "hr", "contract.ended", "employment_contract", contractId,
                    null, JsonSerializer.Serialize(new { result.EmploymentId, request.EffectiveOn, reason }), null, "hr-api", correlationId,
                    System.Diagnostics.Activity.Current?.TraceId.ToString(), clock.GetUtcNow(), null), token);
            return result;
        }, ct);
    }

    public async Task<ContractAmendmentView> CreateContractAmendmentAsync(TenantId tenant, Guid actor, Guid membershipId, Guid contractId, Guid legalEntityId, CreateContractAmendmentRequest request, Guid correlationId, CancellationToken ct)
    {
        var changeType = (request.ChangeType ?? string.Empty).Trim(); var reason = (request.Reason ?? string.Empty).Trim();
        if (request.EffectiveDate == default || changeType.Length is < 1 or > 80 || reason.Length is < 1 or > 1000 || request.NewTerms.ValueKind != JsonValueKind.Object)
            throw new HrException("hr.contract_amendment_invalid");
        if (request.FileRef is { } amendmentFile)
        {
            var amendmentEvidence = await files.VerifyReadyReferenceAsync(tenant, amendmentFile, "hr", "employment_contract", contractId, ct);
            if (amendmentEvidence is null || amendmentEvidence.Classification.ToLowerInvariant() is not ("confidential" or "restricted")) throw new HrException("hr.contract_file_reference_invalid");
        }
        var id = Guid.CreateVersion7();
        return await transactions.ExecuteAsync<ContractAmendmentView>(tenant, async (context, token) =>
        {
            Guid employmentId; Guid employeeId; DateOnly? startDate; DateOnly? endDate; string status; string type;
            await using (var contract = new NpgsqlCommand("select c.employment_id,e.employee_id,c.start_date,c.end_date,c.status,c.contract_type from business_hr.employment_contract c join business_hr.employment e on e.tenant_id=c.tenant_id and e.id=c.employment_id where c.tenant_id=$1 and c.id=$2 and c.legal_entity_id=$3 for update of c", context.Connection, context.Transaction))
            {
                contract.Parameters.AddWithValue(tenant.Value); contract.Parameters.AddWithValue(contractId); contract.Parameters.AddWithValue(legalEntityId);
                await using var reader = await contract.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new HrException("hr.contract_not_found");
                employmentId = reader.GetGuid(0); employeeId = reader.GetGuid(1); startDate = reader.IsDBNull(2) ? null : reader.GetFieldValue<DateOnly>(2); endDate = reader.IsDBNull(3) ? null : reader.GetFieldValue<DateOnly>(3); status = reader.GetString(4); type = reader.GetString(5);
            }
            if (status != "Active" || startDate is { } knownStart && request.EffectiveDate < knownStart || endDate is { } end && request.EffectiveDate > end) throw new HrException("hr.contract_amendment_outside_active_term");
            await using var priorTermsCommand = new NpgsqlCommand("select coalesce(jsonb_agg(jsonb_build_object('effectiveDate',effective_date,'changeType',change_type,'terms',new_terms) order by effective_date,created_at,id),'[]'::jsonb)::text from business_hr.employment_contract_amendment where tenant_id=$1 and contract_id=$2 and effective_date<=$3", context.Connection, context.Transaction);
            priorTermsCommand.Parameters.AddWithValue(tenant.Value); priorTermsCommand.Parameters.AddWithValue(contractId); priorTermsCommand.Parameters.AddWithValue(request.EffectiveDate);
            using var priorTerms = JsonDocument.Parse((string?)await priorTermsCommand.ExecuteScalarAsync(token) ?? "[]");
            var previous = JsonSerializer.SerializeToElement(new { contractId, type, startDate, endDate, priorAmendments = priorTerms.RootElement.Clone() });
            await using var insert = new NpgsqlCommand("insert into business_hr.employment_contract_amendment(id,tenant_id,contract_id,employment_id,effective_date,change_type,previous_snapshot,new_terms,reason,file_ref,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7::jsonb,$8::jsonb,$9,$10,$11,$12)", context.Connection, context.Transaction);
            insert.Parameters.AddWithValue(id); insert.Parameters.AddWithValue(tenant.Value); insert.Parameters.AddWithValue(contractId); insert.Parameters.AddWithValue(employmentId); insert.Parameters.AddWithValue(request.EffectiveDate); insert.Parameters.AddWithValue(changeType);
            insert.Parameters.AddWithValue(JsonSerializer.Serialize(previous)); insert.Parameters.AddWithValue(JsonSerializer.Serialize(request.NewTerms)); insert.Parameters.AddWithValue(reason); insert.Parameters.AddWithValue((object?)request.FileRef ?? DBNull.Value); insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(clock.GetUtcNow());
            await insert.ExecuteNonQueryAsync(token);
            await AppendEventAsync(context.Connection, context.Transaction, tenant, employeeId, employmentId, "ContractAmended", request.EffectiveDate, actor, new { contractId, changeType }, correlationId, token);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction,
                new AuditEntry(Guid.CreateVersion7(), tenant, legalEntityId, null, "Human", actor, membershipId, "hr", "contract.amended", "employment_contract_amendment", id,
                    null, JsonSerializer.Serialize(new { contractId, effectiveDate = request.EffectiveDate, changeType, previous, newTerms = request.NewTerms, reason }), null, "hr-api", correlationId,
                    System.Diagnostics.Activity.Current?.TraceId.ToString(), clock.GetUtcNow(), null), token);
            return new(id, contractId, request.EffectiveDate, changeType, previous, request.NewTerms.Clone(), reason, request.FileRef, clock.GetUtcNow());
        }, ct);
    }

    public async Task<IReadOnlyList<ContractAmendmentView>> ListContractAmendmentsAsync(TenantId tenant, Guid contractId, Guid legalEntityId, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tx, tenant, ct);
        const string sql = "select a.id,a.contract_id,a.effective_date,a.change_type,a.previous_snapshot,a.new_terms,a.reason,a.file_ref,a.created_at from business_hr.employment_contract_amendment a join business_hr.employment_contract c on c.tenant_id=a.tenant_id and c.id=a.contract_id where a.tenant_id=$1 and a.contract_id=$2 and c.legal_entity_id=$3 order by a.effective_date desc,a.created_at desc,a.id desc limit 100";
        await using var command = new NpgsqlCommand(sql, connection, tx); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(contractId); command.Parameters.AddWithValue(legalEntityId);
        var result = new List<ContractAmendmentView>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetFieldValue<DateOnly>(2), reader.GetString(3), reader.GetFieldValue<System.Text.Json.JsonDocument>(4).RootElement.Clone(), reader.GetFieldValue<System.Text.Json.JsonDocument>(5).RootElement.Clone(), reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetGuid(7), reader.GetFieldValue<DateTimeOffset>(8)));
        await reader.CloseAsync(); await tx.CommitAsync(ct); return result;
    }

    private static ContractView ReadContract(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.IsDBNull(3) ? null : reader.GetString(3), reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetFieldValue<DateOnly>(5), reader.IsDBNull(6) ? null : reader.GetFieldValue<DateOnly>(6), reader.IsDBNull(7) ? null : reader.GetFieldValue<DateOnly>(7), reader.IsDBNull(8) ? null : reader.GetFieldValue<DateOnly>(8), reader.GetString(9), reader.IsDBNull(10) ? null : reader.GetGuid(10), reader.IsDBNull(11) ? null : reader.GetGuid(11), reader.GetInt64(12));

    private static async Task<Guid> GetEmploymentEmployeeIdAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid employmentId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select employee_id from business_hr.employment where tenant_id=$1 and id=$2", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(employmentId);
        return await command.ExecuteScalarAsync(ct) as Guid? ?? throw new HrException("hr.employment_not_found");
    }

    public async Task<IReadOnlyList<EmployeeTimelineEvent>> GetTimelineAsync(TenantId tenant, Guid employeeId, Guid? legalEntityId, int limit, CancellationToken ct)
    {
        var bounded = Math.Clamp(limit, 1, 200);
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct); await SetTenantAsync(connection, tx, tenant, ct);
        const string sql = "select ev.id,ev.event_type,ev.effective_on,ev.occurred_at,ev.employment_id,ev.payload from business_hr.employee_timeline_projection ev where ev.tenant_id=$1 and ev.employee_id=$2 and ($3::uuid is null or exists(select 1 from business_hr.employment em where em.tenant_id=ev.tenant_id and em.id=ev.employment_id and em.legal_entity_id=$3)) order by ev.effective_on desc,ev.occurred_at desc,ev.id desc limit $4";
        await using var command = new NpgsqlCommand(sql, connection, tx); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(employeeId); command.Parameters.AddWithValue((object?)legalEntityId ?? DBNull.Value); command.Parameters.AddWithValue(bounded);
        var rows = new List<EmployeeTimelineEvent>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) rows.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetFieldValue<DateOnly>(2), reader.GetFieldValue<DateTimeOffset>(3), reader.IsDBNull(4) ? null : reader.GetGuid(4), reader.GetFieldValue<System.Text.Json.JsonDocument>(5).RootElement.Clone()));
        await reader.CloseAsync(); await tx.CommitAsync(ct); return rows;
    }

    private async Task AppendEventAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid employeeId, Guid? employmentId, string eventType, DateOnly effectiveOn, Guid actor, object payload, Guid correlationId, CancellationToken ct)
    {
        var id = Guid.CreateVersion7();
        var occurredAt = clock.GetUtcNow();
        var payloadJson = JsonSerializer.Serialize(payload);
        await using var command = new NpgsqlCommand("insert into business_hr.employment_event(tenant_id,id,employee_id,employment_id,event_type,effective_on,actor_id,payload,correlation_id,occurred_at) values($1,$2,$3,$4,$5,$6,$7,$8::jsonb,$9,$10)", connection, transaction);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(id); command.Parameters.AddWithValue(employeeId); command.Parameters.AddWithValue((object?)employmentId ?? DBNull.Value);
        command.Parameters.AddWithValue(eventType); command.Parameters.AddWithValue(effectiveOn); command.Parameters.AddWithValue(actor); command.Parameters.AddWithValue(payloadJson);
        command.Parameters.AddWithValue(correlationId); command.Parameters.AddWithValue(occurredAt); await command.ExecuteNonQueryAsync(ct);
        await using var projection = new NpgsqlCommand("insert into business_hr.employee_timeline_projection(tenant_id,id,employee_id,employment_id,event_type,effective_on,occurred_at,payload) values($1,$2,$3,$4,$5,$6,$7,$8::jsonb)", connection, transaction);
        projection.Parameters.AddWithValue(tenant.Value); projection.Parameters.AddWithValue(id); projection.Parameters.AddWithValue(employeeId); projection.Parameters.AddWithValue((object?)employmentId ?? DBNull.Value);
        projection.Parameters.AddWithValue(eventType); projection.Parameters.AddWithValue(effectiveOn); projection.Parameters.AddWithValue(occurredAt); projection.Parameters.AddWithValue(payloadJson); await projection.ExecuteNonQueryAsync(ct);
    }

    private async Task RefreshEmployeeProjectionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Employee employee, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("insert into business_hr.employee_list_projection(tenant_id,employee_id,display_name,display_name_normalized,arabic_name_normalized,english_name,english_name_normalized,status,projected_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9) on conflict(tenant_id,employee_id) do update set display_name=excluded.display_name,display_name_normalized=excluded.display_name_normalized,arabic_name_normalized=excluded.arabic_name_normalized,english_name=excluded.english_name,english_name_normalized=excluded.english_name_normalized,status=excluded.status,projected_at=excluded.projected_at", connection, transaction);
        command.Parameters.AddWithValue(employee.TenantId); command.Parameters.AddWithValue(employee.Id); command.Parameters.AddWithValue(employee.DisplayName); command.Parameters.AddWithValue(HrSearchNormalization.Normalize(employee.DisplayName)); command.Parameters.AddWithValue(HrSearchNormalization.Normalize(employee.ArabicName)); command.Parameters.AddWithValue((object?)employee.EnglishName ?? DBNull.Value); command.Parameters.AddWithValue((object?)employee.EnglishName is null ? DBNull.Value : HrSearchNormalization.Normalize(employee.EnglishName)); command.Parameters.AddWithValue(employee.Status.ToString()); command.Parameters.AddWithValue(clock.GetUtcNow()); await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, CancellationToken ct)
    { await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction); command.Parameters.AddWithValue(tenant.Value.ToString("D")); await command.ExecuteNonQueryAsync(ct); }
}
