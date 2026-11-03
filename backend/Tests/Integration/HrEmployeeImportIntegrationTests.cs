using Core.Identity;
using Core.Transactions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Modules.Hr.DTOs;
using Modules.Hr.Entities;
using Modules.Hr.Repositories;
using Modules.Hr.Services;
using Npgsql;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class HrEmployeeImportIntegrationTests : DatabaseIntegrationTestBase
{
    [HrPostgresFact]
    public async Task EmployeeImportReplaysIdenticalRowAndRejectsChangedPayload()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for HR PostgreSQL integration tests.");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for HR PostgreSQL integration tests.");
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"hr-employee-import-{suffix}@atlas.com";
        var password = $"HrEmployeeImport-{suffix[..8]}1!";
        Guid tenantId;
        Guid legalEntityId;
        string departmentCode;
        Guid positionId;

        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync();
            await using var transaction = await admin.BeginTransactionAsync();
            await using (var scope = new NpgsqlCommand("select t.id,e.id from platform.tenant t join platform.legal_entity e on e.tenant_id=t.id and e.status='Active' where t.slug='atlas-trading' order by e.id limit 1", admin, transaction))
            await using (var reader = await scope.ExecuteReaderAsync())
            {
                if (!await reader.ReadAsync()) throw new InvalidOperationException("The HR integration database must contain the canonical Atlas development seed.");
                tenantId = reader.GetGuid(0);
                legalEntityId = reader.GetGuid(1);
            }
            await using (var department = new NpgsqlCommand("select code from business_hr.department where tenant_id=$1 and legal_entity_id=$2 and status='Active' order by code limit 1", admin, transaction))
            {
                department.Parameters.AddWithValue(tenantId); department.Parameters.AddWithValue(legalEntityId);
                departmentCode = (string?)await department.ExecuteScalarAsync() ?? throw new InvalidOperationException("The canonical HR seed must contain an active department for the assignment import scenario.");
            }
            await using (var position = new NpgsqlCommand("select id from business_hr.position where tenant_id=$1 and legal_entity_id=$2 and status='Active' order by code limit 1", admin, transaction))
            {
                position.Parameters.AddWithValue(tenantId); position.Parameters.AddWithValue(legalEntityId);
                positionId = (Guid?)await position.ExecuteScalarAsync() ?? throw new InvalidOperationException("The canonical HR seed must contain an active position for the assignment history scenario.");
            }
            await CreateAccountAsync(admin, transaction, tenantId, email, password);
            await using (var goLivePermission = new NpgsqlCommand("insert into platform.role_permission(tenant_id,role_id,permission_code) select $1,id,permission_code from platform.role cross join (values ('hr.golive.manage'),('hr.reminder.read')) required(permission_code) where tenant_id=$1 and code='hr_privacy' and is_active on conflict do nothing", admin, transaction))
            {
                goLivePermission.Parameters.AddWithValue(tenantId);
                await goLivePermission.ExecuteNonQueryAsync();
            }
            await using (var identifierPermissions = new NpgsqlCommand("insert into platform.role_permission(tenant_id,role_id,permission_code) select $1,id,permission_code from platform.role cross join (values ('hr.employee.identifier.manage'),('hr.employee.identifier.read')) required(permission_code) where tenant_id=$1 and code='hr_privacy' and is_active on conflict do nothing", admin, transaction))
            {
                identifierPermissions.Parameters.AddWithValue(tenantId);
                await identifierPermissions.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnectionString)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await LoginAsync(client, email, password, tenantId);

        var goLiveDate = DateOnly.FromDateTime(DateTime.UtcNow);
        using var existingGoLiveResponse = await client.GetAsync($"/api/v1/hr/legal-entities/{legalEntityId:D}/go-live");
        var initialGoLiveVersion = existingGoLiveResponse.StatusCode == HttpStatusCode.NotFound
            ? 0
            : (await existingGoLiveResponse.Content.ReadFromJsonAsync<HrGoLiveConfigurationView>())!.Version;
        var goLiveConfiguration = new HrGoLiveConfigurationRequest(goLiveDate, "ActiveAndHistorical", goLiveDate.AddYears(-1), goLiveDate.AddDays(-1), "AllAvailable", initialGoLiveVersion, "Approved HR migration boundary for this legal entity");
        using var saveGoLive = await SendWithCsrfAsync(client, HttpMethod.Put, $"/api/v1/hr/legal-entities/{legalEntityId:D}/go-live", goLiveConfiguration);
        Assert.Equal(HttpStatusCode.OK, saveGoLive.StatusCode);
        var savedGoLive = await saveGoLive.Content.ReadFromJsonAsync<HrGoLiveConfigurationView>();
        Assert.NotNull(savedGoLive);
        Assert.Equal(initialGoLiveVersion + 1, savedGoLive.Version);
        using var readGoLive = await client.GetAsync($"/api/v1/hr/legal-entities/{legalEntityId:D}/go-live");
        Assert.Equal(HttpStatusCode.OK, readGoLive.StatusCode);
        Assert.Equal(goLiveDate, (await readGoLive.Content.ReadFromJsonAsync<HrGoLiveConfigurationView>())!.GoLiveDate);
        using var staleGoLive = await SendWithCsrfAsync(client, HttpMethod.Put, $"/api/v1/hr/legal-entities/{legalEntityId:D}/go-live", goLiveConfiguration with { ExpectedVersion = initialGoLiveVersion });
        Assert.Equal(HttpStatusCode.Conflict, staleGoLive.StatusCode);
        using var invalidGoLive = await SendWithCsrfAsync(client, HttpMethod.Put, $"/api/v1/hr/legal-entities/{legalEntityId:D}/go-live",
            goLiveConfiguration with { ExpectedVersion = savedGoLive.Version, AttendanceHistoryBoundary = goLiveDate.AddDays(1) });
        Assert.Equal(HttpStatusCode.BadRequest, invalidGoLive.StatusCode);

        var fileHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(suffix))).ToLowerInvariant();
        var request = new HrEmployeeImportRequest($"Legacy-{suffix[..8]}", $"batch-{suffix[..12]}", fileHash,
        [new HrEmployeeImportRow("employee-row-1", $"legacy-{suffix[..12]}", "مها عبدالعزيز", "Maha Abdelaziz", null,
            legalEntityId, EmploymentType.FullTime, new DateOnly(2010, 1, 1), null, EmploymentStatus.Active, "Complete", DepartmentCode: departmentCode)]);

        using var preview = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/employees/preview", request);
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var previewStatus = Assert.Single((await preview.Content.ReadFromJsonAsync<HrEmployeeImportSummary>())!.Rows).Status;
        Assert.True(previewStatus is "Ready" or "ReadyWithWarnings");

        using var commit = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/employees/commit", request);
        Assert.Equal(HttpStatusCode.OK, commit.StatusCode);
        var committed = Assert.Single((await commit.Content.ReadFromJsonAsync<HrEmployeeImportSummary>())!.Rows);
        Assert.Equal("Imported", committed.Status);
        Assert.NotNull(committed.EmployeeId);
        Assert.NotNull(committed.EmploymentId);

        var sensitiveIdentifier = HrIdentifierCrypto.Normalize($"2980101123{suffix[..4]}");
        using var identifierResponse = await SendWithCsrfAsync(client, HttpMethod.Post,
            $"/api/v1/hr/employees/{committed.EmployeeId:D}/identifiers",
            new CreateEmployeeIdentifierRequest(legalEntityId, "NationalId", "EG", sensitiveIdentifier));
        Assert.True(identifierResponse.StatusCode == HttpStatusCode.Created, await identifierResponse.Content.ReadAsStringAsync());
        var identifier = await identifierResponse.Content.ReadFromJsonAsync<EmployeeIdentifierView>();
        Assert.NotNull(identifier);
        Assert.Equal(HrIdentifierCrypto.Mask(sensitiveIdentifier), identifier.MaskedDisplay);
        Assert.DoesNotContain(sensitiveIdentifier, await identifierResponse.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        await using (var verifyIdentifierAudit = new NpgsqlConnection(adminConnectionString))
        {
            await verifyIdentifierAudit.OpenAsync();
            await using var audit = new NpgsqlCommand("select coalesce(before_json::text,'') || coalesce(after_json::text,'') || coalesce(reason,'') || coalesce(metadata_json::text,'') from platform.audit_entry where tenant_id=$1 and module='hr' and action='employee.identifier_added' and entity_id=$2", verifyIdentifierAudit);
            audit.Parameters.AddWithValue(tenantId); audit.Parameters.AddWithValue(identifier.Id);
            var auditPayload = (string?)await audit.ExecuteScalarAsync();
            Assert.False(string.IsNullOrEmpty(auditPayload));
            Assert.DoesNotContain(sensitiveIdentifier, auditPayload, StringComparison.Ordinal);
        }

        using var updateGoLive = await SendWithCsrfAsync(client, HttpMethod.Put, $"/api/v1/hr/legal-entities/{legalEntityId:D}/go-live",
            goLiveConfiguration with { ExpectedVersion = savedGoLive.Version, EmployeeMigrationStrategy = "ActiveOnly", Reason = "The company confirmed active employees only" });
        Assert.Equal(HttpStatusCode.OK, updateGoLive.StatusCode);
        var activeOnlyGoLive = await updateGoLive.Content.ReadFromJsonAsync<HrGoLiveConfigurationView>();
        var historicalEmployee = request with
        {
            BatchReference = $"historical-employee-{suffix[..10]}",
            Rows = [request.Rows[0] with
            {
                RowReference = "historical-employee-row",
                LegacyEmployeeId = $"legacy-history-{suffix[..10]}",
                EndDate = new DateOnly(2014, 12, 31),
                Status = EmploymentStatus.Ended,
                DepartmentCode = null
            }]
        };
        using var disallowedHistory = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/employees/preview", historicalEmployee);
        Assert.Equal(HttpStatusCode.OK, disallowedHistory.StatusCode);
        Assert.Contains("hr.employee_migration_history_not_allowed", Assert.Single((await disallowedHistory.Content.ReadFromJsonAsync<HrEmployeeImportSummary>())!.Rows).Issues);

        var hireDate = request.Rows[0].HireDate;
        var importedAssignmentStart = hireDate.AddDays(30);
        var assignmentEffectiveDate = hireDate.AddDays(119);
        var assignmentImport = new HrAssignmentImportRequest(request.SourceSystem, $"assignment-batch-{suffix[..10]}", fileHash,
            [new HrAssignmentImportRow("assignment-row-1", $"legacy-assignment-{suffix[..12]}", request.Rows[0].LegacyEmployeeId, legalEntityId,
                importedAssignmentStart, assignmentEffectiveDate, null, departmentCode, null, null, "InitialHire")]);
        using var assignmentPreview = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/assignments/preview", assignmentImport);
        Assert.Equal(HttpStatusCode.OK, assignmentPreview.StatusCode);
        Assert.Equal("Ready", Assert.Single((await assignmentPreview.Content.ReadFromJsonAsync<HrAssignmentImportSummary>())!.Rows).Status);
        using var assignmentCommit = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/assignments/commit", assignmentImport);
        Assert.Equal(HttpStatusCode.OK, assignmentCommit.StatusCode);
        var importedAssignment = Assert.Single((await assignmentCommit.Content.ReadFromJsonAsync<HrAssignmentImportSummary>())!.Rows);
        Assert.Equal("Imported", importedAssignment.Status);
        Assert.NotNull(importedAssignment.AssignmentId);
        using var assignmentReplay = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/assignments/commit", assignmentImport);
        Assert.Equal(HttpStatusCode.OK, assignmentReplay.StatusCode);
        Assert.Equal("AlreadyImported", Assert.Single((await assignmentReplay.Content.ReadFromJsonAsync<HrAssignmentImportSummary>())!.Rows).Status);
        var changedAssignment = assignmentImport with { Rows = [assignmentImport.Rows[0] with { ReasonCode = "PROMOTION" }] };
        using var assignmentConflict = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/assignments/preview", changedAssignment);
        Assert.Equal(HttpStatusCode.OK, assignmentConflict.StatusCode);
        Assert.Contains("hr.import_row_conflict", Assert.Single((await assignmentConflict.Content.ReadFromJsonAsync<HrAssignmentImportSummary>())!.Rows).Issues);

        var contractImport = new HrContractImportRequest(request.SourceSystem, $"contract-batch-{suffix[..10]}", fileHash,
            [new HrContractImportRow("contract-row-1", $"legacy-contract-{suffix[..12]}", request.Rows[0].LegacyEmployeeId, legalEntityId,
                "EmploymentAgreement", null, new DateOnly(2014, 12, 31), null, null, "Expired", "UnknownHistory")]);
        using var contractPreview = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/contracts/preview", contractImport);
        Assert.Equal(HttpStatusCode.OK, contractPreview.StatusCode);
        Assert.Equal("Ready", Assert.Single((await contractPreview.Content.ReadFromJsonAsync<HrContractImportSummary>())!.Rows).Status);
        using var contractCommit = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/contracts/commit", contractImport);
        Assert.Equal(HttpStatusCode.OK, contractCommit.StatusCode);
        var importedContract = Assert.Single((await contractCommit.Content.ReadFromJsonAsync<HrContractImportSummary>())!.Rows);
        Assert.Equal("Imported", importedContract.Status);
        using var contractReplay = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/contracts/commit", contractImport);
        Assert.Equal(HttpStatusCode.OK, contractReplay.StatusCode);
        Assert.Equal("AlreadyImported", Assert.Single((await contractReplay.Content.ReadFromJsonAsync<HrContractImportSummary>())!.Rows).Status);
        using var contractConflict = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/contracts/preview",
            contractImport with { Rows = [contractImport.Rows[0] with { ContractType = "UnknownType" }] });
        Assert.Equal(HttpStatusCode.OK, contractConflict.StatusCode);
        Assert.Contains("hr.import_row_conflict", Assert.Single((await contractConflict.Content.ReadFromJsonAsync<HrContractImportSummary>())!.Rows).Issues);

        var compensationImport = new HrCompensationImportRequest(request.SourceSystem, $"compensation-batch-{suffix[..10]}", fileHash,
            [new HrCompensationImportRow("compensation-row-1", $"legacy-agreement-{suffix[..12]}", request.Rows[0].LegacyEmployeeId, legalEntityId,
                "EGP", 8500m, "Monthly", hireDate, new DateOnly(2014, 12, 31), "Ended", "Historical salary from archived employee file", "UnknownHistory", contractImport.Rows[0].LegacyContractId)]);
        using var compensationPreview = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/compensation/preview", compensationImport);
        Assert.Equal(HttpStatusCode.OK, compensationPreview.StatusCode);
        Assert.Equal("Ready", Assert.Single((await compensationPreview.Content.ReadFromJsonAsync<HrCompensationImportSummary>())!.Rows).Status);
        using var compensationCommit = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/compensation/commit", compensationImport);
        Assert.Equal(HttpStatusCode.OK, compensationCommit.StatusCode);
        var importedCompensation = Assert.Single((await compensationCommit.Content.ReadFromJsonAsync<HrCompensationImportSummary>())!.Rows);
        Assert.Equal("Imported", importedCompensation.Status);
        using var compensationReplay = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/compensation/commit", compensationImport);
        Assert.Equal(HttpStatusCode.OK, compensationReplay.StatusCode);
        Assert.Equal("AlreadyImported", Assert.Single((await compensationReplay.Content.ReadFromJsonAsync<HrCompensationImportSummary>())!.Rows).Status);
        using var compensationConflict = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/compensation/preview",
            compensationImport with { Rows = [compensationImport.Rows[0] with { Amount = 8600m }] });
        Assert.Equal(HttpStatusCode.OK, compensationConflict.StatusCode);
        Assert.Contains("hr.import_row_conflict", Assert.Single((await compensationConflict.Content.ReadFromJsonAsync<HrCompensationImportSummary>())!.Rows).Issues);

        const string historicalInsuranceMemberNumber = "EG-INS-55670214";
        var insuranceImport = new HrInsuranceImportRequest(request.SourceSystem, $"insurance-batch-{suffix[..10]}", fileHash,
            [new HrInsuranceImportRow("insurance-row-1", $"legacy-enrollment-{suffix[..12]}", request.Rows[0].LegacyEmployeeId, legalEntityId,
                "SOCIAL_INSURANCE", "NASS", historicalInsuranceMemberNumber, hireDate, new DateOnly(2014, 12, 31), "Ended", "UnknownHistory")]);
        using var insurancePreview = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/insurance/preview", insuranceImport);
        Assert.Equal(HttpStatusCode.OK, insurancePreview.StatusCode);
        Assert.Equal("Ready", Assert.Single((await insurancePreview.Content.ReadFromJsonAsync<HrInsuranceImportSummary>())!.Rows).Status);
        using var insuranceImportCommit = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/insurance/commit", insuranceImport);
        Assert.Equal(HttpStatusCode.OK, insuranceImportCommit.StatusCode);
        var importedInsurance = Assert.Single((await insuranceImportCommit.Content.ReadFromJsonAsync<HrInsuranceImportSummary>())!.Rows);
        Assert.Equal("Imported", importedInsurance.Status);
        using var insuranceImportReplay = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/insurance/commit", insuranceImport);
        Assert.Equal(HttpStatusCode.OK, insuranceImportReplay.StatusCode);
        Assert.Equal("AlreadyImported", Assert.Single((await insuranceImportReplay.Content.ReadFromJsonAsync<HrInsuranceImportSummary>())!.Rows).Status);
        using var insuranceMemberConflict = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/insurance/preview",
            insuranceImport with { Rows = [insuranceImport.Rows[0] with { MemberNumber = "EG-INS-55670215" }] });
        Assert.Equal(HttpStatusCode.OK, insuranceMemberConflict.StatusCode);
        Assert.Contains("hr.import_row_conflict", Assert.Single((await insuranceMemberConflict.Content.ReadFromJsonAsync<HrInsuranceImportSummary>())!.Rows).Issues);

        var legacyFileId = Guid.CreateVersion7();
        var legacyFileSha = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"legacy-hr-file-{suffix}"))).ToLowerInvariant();
        Guid anotherEmployeeId;
        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync();
            await using (var employee = new NpgsqlCommand("select employee_id from business_hr.employment where tenant_id=$1 and legal_entity_id=$2 and employee_id<>$3 and status='Active' order by employment_number limit 1", admin))
            {
                employee.Parameters.AddWithValue(tenantId); employee.Parameters.AddWithValue(legalEntityId); employee.Parameters.AddWithValue(committed.EmployeeId.Value);
                anotherEmployeeId = (Guid?)await employee.ExecuteScalarAsync() ?? throw new InvalidOperationException("The canonical seed must contain another active employee for the document owner-boundary scenario.");
            }
            await using var insertFile = new NpgsqlCommand("insert into platform.file_record(id,tenant_id,owner_module,owner_entity_type,owner_entity_id,object_key,original_name,content_type,size,sha256,status,classification,created_by,created_at) values($1,$2,'hr','employee',$3,$4,'employment-contract.pdf','application/pdf',2048,$5,'Ready','confidential',$6,now())", admin);
            insertFile.Parameters.AddWithValue(legacyFileId); insertFile.Parameters.AddWithValue(tenantId); insertFile.Parameters.AddWithValue(committed.EmployeeId!.Value);
            insertFile.Parameters.AddWithValue($"hr-integration/{suffix}/employment-contract.pdf"); insertFile.Parameters.AddWithValue(legacyFileSha); insertFile.Parameters.AddWithValue(Guid.NewGuid());
            await insertFile.ExecuteNonQueryAsync();
        }
        var documentImport = new HrDocumentImportRequest(request.SourceSystem, $"document-batch-{suffix[..10]}", fileHash,
            [new HrDocumentImportRow("document-row-1", $"legacy-document-{suffix[..12]}", request.Rows[0].LegacyEmployeeId, legalEntityId,
                "EmploymentContract", "Confidential", legacyFileId, null, "UnknownHistory")]);
        using var documentPreview = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/documents/preview", documentImport);
        Assert.Equal(HttpStatusCode.OK, documentPreview.StatusCode);
        Assert.Equal("Ready", Assert.Single((await documentPreview.Content.ReadFromJsonAsync<HrDocumentImportSummary>())!.Rows).Status);
        using var documentCommit = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/documents/commit", documentImport);
        Assert.Equal(HttpStatusCode.OK, documentCommit.StatusCode);
        var importedDocument = Assert.Single((await documentCommit.Content.ReadFromJsonAsync<HrDocumentImportSummary>())!.Rows);
        Assert.Equal("Imported", importedDocument.Status);
        using var documentReplay = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/documents/commit", documentImport);
        Assert.Equal(HttpStatusCode.OK, documentReplay.StatusCode);
        Assert.Equal("AlreadyImported", Assert.Single((await documentReplay.Content.ReadFromJsonAsync<HrDocumentImportSummary>())!.Rows).Status);
        using var documentFileConflict = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/documents/preview",
            documentImport with { Rows = [documentImport.Rows[0] with { FileRef = Guid.NewGuid() }] });
        Assert.Equal(HttpStatusCode.OK, documentFileConflict.StatusCode);
        Assert.Contains("hr.document_file_reference_invalid", Assert.Single((await documentFileConflict.Content.ReadFromJsonAsync<HrDocumentImportSummary>())!.Rows).Issues);
        var otherEmployeeFileId = Guid.CreateVersion7();
        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync();
            await using var insertFile = new NpgsqlCommand("insert into platform.file_record(id,tenant_id,owner_module,owner_entity_type,owner_entity_id,object_key,original_name,content_type,size,sha256,status,classification,created_by,created_at) values($1,$2,'hr','employee',$3,$4,'another-employee.pdf','application/pdf',2048,$5,'Ready','confidential',$6,now())", admin);
            insertFile.Parameters.AddWithValue(otherEmployeeFileId); insertFile.Parameters.AddWithValue(tenantId); insertFile.Parameters.AddWithValue(anotherEmployeeId);
            insertFile.Parameters.AddWithValue($"hr-integration/{suffix}/another-employee.pdf"); insertFile.Parameters.AddWithValue(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"another-employee-file-{suffix}"))).ToLowerInvariant()); insertFile.Parameters.AddWithValue(Guid.NewGuid());
            await insertFile.ExecuteNonQueryAsync();
        }
        using var crossEmployeeFile = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/documents/preview",
            documentImport with { Rows = [documentImport.Rows[0] with { LegacyDocumentId = $"foreign-file-{suffix[..12]}", RowReference = "foreign-file-row", FileRef = otherEmployeeFileId }] });
        Assert.Equal(HttpStatusCode.OK, crossEmployeeFile.StatusCode);
        Assert.Contains("hr.document_file_reference_invalid", Assert.Single((await crossEmployeeFile.Content.ReadFromJsonAsync<HrDocumentImportSummary>())!.Rows).Issues);
        using var disableDocumentMigration = await SendWithCsrfAsync(client, HttpMethod.Put, $"/api/v1/hr/legal-entities/{legalEntityId:D}/go-live",
            goLiveConfiguration with { ExpectedVersion = activeOnlyGoLive!.Version, DocumentMigrationStrategy = "None", Reason = "Legacy document archive migration is deferred" });
        Assert.Equal(HttpStatusCode.OK, disableDocumentMigration.StatusCode);
        using var disabledDocumentPreview = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/documents/preview",
            documentImport with { Rows = [documentImport.Rows[0] with { LegacyDocumentId = $"deferred-document-{suffix[..12]}", RowReference = "deferred-document-row" }] });
        Assert.Equal(HttpStatusCode.OK, disabledDocumentPreview.StatusCode);
        Assert.Contains("hr.document_migration_disabled", Assert.Single((await disabledDocumentPreview.Content.ReadFromJsonAsync<HrDocumentImportSummary>())!.Rows).Issues);

        var firstContractRequest = new CreateContractRequest(legalEntityId, "FixedTerm", new DateOnly(2015, 1, 1), new DateOnly(2025, 12, 31));
        using var firstContractResponse = await SendWithCsrfAsync(client, HttpMethod.Post, $"/api/v1/hr/employments/{committed.EmploymentId:D}/contracts", firstContractRequest);
        Assert.Equal(HttpStatusCode.Created, firstContractResponse.StatusCode);
        var firstContract = await firstContractResponse.Content.ReadFromJsonAsync<JsonElement>();
        var firstContractId = firstContract.GetProperty("id").GetGuid();
        using var activateFirst = await SendWithCsrfAsync(client, HttpMethod.Post, $"/api/v1/hr/contracts/{firstContractId:D}/activate?legalEntityId={legalEntityId:D}", new ActivateContractRequest(null));
        Assert.Equal(HttpStatusCode.OK, activateFirst.StatusCode);

        var renewalStart = new DateOnly(2026, 1, 1);
        var renewalRequest = new CreateContractRequest(legalEntityId, "FixedTerm", renewalStart, new DateOnly(2026, 12, 31), RenewalOfContractId: firstContractId);
        using var renewalResponse = await SendWithCsrfAsync(client, HttpMethod.Post, $"/api/v1/hr/employments/{committed.EmploymentId:D}/contracts", renewalRequest);
        Assert.Equal(HttpStatusCode.Created, renewalResponse.StatusCode);
        var renewal = await renewalResponse.Content.ReadFromJsonAsync<JsonElement>();
        var renewalId = renewal.GetProperty("id").GetGuid();
        Assert.Equal(firstContractId, renewal.GetProperty("renewalOfContractId").GetGuid());
        using var activateRenewal = await SendWithCsrfAsync(client, HttpMethod.Post, $"/api/v1/hr/contracts/{renewalId:D}/activate?legalEntityId={legalEntityId:D}", new ActivateContractRequest(null));
        Assert.Equal(HttpStatusCode.OK, activateRenewal.StatusCode);
        using var contractsResponse = await client.GetAsync($"/api/v1/hr/employments/{committed.EmploymentId:D}/contracts?legalEntityId={legalEntityId:D}");
        Assert.Equal(HttpStatusCode.OK, contractsResponse.StatusCode);
        var contracts = await contractsResponse.Content.ReadFromJsonAsync<JsonElement[]>();
        var historicalImportedContract = Assert.Single(contracts!, item => item.GetProperty("id").GetGuid() == importedContract.ContractId);
        Assert.Equal(JsonValueKind.Null, historicalImportedContract.GetProperty("startDate").ValueKind);
        Assert.Contains(contracts!, item => item.GetProperty("id").GetGuid() == firstContractId);
        Assert.Contains(contracts!, item => item.GetProperty("id").GetGuid() == renewalId);

        var assignmentChange = new CreateAssignmentRequest(legalEntityId, null, null, positionId, null, assignmentEffectiveDate, null, "PROMOTION");
        using var assignmentResponse = await SendWithCsrfAsync(client, HttpMethod.Post,
            $"/api/v1/hr/employments/{committed.EmploymentId:D}/assignments", assignmentChange);
        Assert.Equal(HttpStatusCode.Created, assignmentResponse.StatusCode);
        var forgedManagerAssignment = assignmentChange with { ManagerEmploymentId = Guid.CreateVersion7(), EffectiveFrom = assignmentEffectiveDate.AddDays(1), ReasonCode = "FORGED_MANAGER" };
        using var forgedManagerResponse = await SendWithCsrfAsync(client, HttpMethod.Post,
            $"/api/v1/hr/employments/{committed.EmploymentId:D}/assignments", forgedManagerAssignment);
        Assert.Equal(HttpStatusCode.BadRequest, forgedManagerResponse.StatusCode);
        using var priorAssignmentResponse = await client.GetAsync($"/api/v1/hr/employments/{committed.EmploymentId:D}/assignments?legalEntityId={legalEntityId:D}&asOf={assignmentEffectiveDate.AddDays(-1):yyyy-MM-dd}");
        using var currentAssignmentResponse = await client.GetAsync($"/api/v1/hr/employments/{committed.EmploymentId:D}/assignments?legalEntityId={legalEntityId:D}&asOf={assignmentEffectiveDate:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, priorAssignmentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, currentAssignmentResponse.StatusCode);
        var priorAssignment = Assert.Single(await priorAssignmentResponse.Content.ReadFromJsonAsync<AssignmentView[]>() ?? []);
        var currentAssignment = Assert.Single(await currentAssignmentResponse.Content.ReadFromJsonAsync<AssignmentView[]>() ?? []);
        Assert.Null(priorAssignment.PositionId);
        Assert.Equal(positionId, currentAssignment.PositionId);

        var firstCompensation = new CreateCompensationAgreementRequest(legalEntityId, "EGP", 12000m, "Monthly", hireDate,
            new DateOnly(2025, 12, 31), Reason: "Imported contractual salary before renewal");
        using var firstCompensationResponse = await SendWithCsrfAsync(client, HttpMethod.Post,
            $"/api/v1/hr/employees/{committed.EmployeeId:D}/employments/{committed.EmploymentId:D}/compensation", firstCompensation);
        Assert.Equal(HttpStatusCode.Created, firstCompensationResponse.StatusCode);
        var secondCompensation = new CreateCompensationAgreementRequest(legalEntityId, "EGP", 14500m, "Monthly", renewalStart,
            Reason: "Contractual salary after renewal");
        using var secondCompensationResponse = await SendWithCsrfAsync(client, HttpMethod.Post,
            $"/api/v1/hr/employees/{committed.EmployeeId:D}/employments/{committed.EmploymentId:D}/compensation", secondCompensation);
        Assert.Equal(HttpStatusCode.Created, secondCompensationResponse.StatusCode);
        using var compensationResponse = await client.GetAsync($"/api/v1/hr/employments/{committed.EmploymentId:D}/compensation?employeeId={committed.EmployeeId:D}&legalEntityId={legalEntityId:D}");
        Assert.Equal(HttpStatusCode.OK, compensationResponse.StatusCode);
        var compensationHistory = await compensationResponse.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.Equal(3, compensationHistory!.Length);
        Assert.Contains(compensationHistory, item => item.GetProperty("amount").GetDecimal() == 8500m);
        Assert.Contains(compensationHistory, item => item.GetProperty("amount").GetDecimal() == 12000m);
        Assert.Contains(compensationHistory, item => item.GetProperty("amount").GetDecimal() == 14500m);

        const string insuranceMemberNumber = "EG-INS-88421957";
        var insuranceRequest = new CreateInsuranceEnrollmentRequest(legalEntityId, "SOCIAL_INSURANCE", "NASS", insuranceMemberNumber, hireDate);
        using var insuranceDraftResponse = await SendWithCsrfAsync(client, HttpMethod.Post,
            $"/api/v1/hr/employees/{committed.EmployeeId:D}/employments/{committed.EmploymentId:D}/insurance", insuranceRequest);
        Assert.Equal(HttpStatusCode.Created, insuranceDraftResponse.StatusCode);
        var insuranceDraft = await insuranceDraftResponse.Content.ReadFromJsonAsync<InsuranceEnrollmentView>();
        Assert.NotNull(insuranceDraft);
        Assert.Equal("Draft", insuranceDraft.Status);
        Assert.NotEqual(insuranceMemberNumber, insuranceDraft.MaskedMemberNumber);
        using var activateInsurance = await SendWithCsrfAsync(client, HttpMethod.Post,
            $"/api/v1/hr/employees/{committed.EmployeeId:D}/employments/{committed.EmploymentId:D}/insurance/{insuranceDraft.Id:D}/activate?legalEntityId={legalEntityId:D}",
            new InsuranceEnrollmentDecisionRequest(insuranceDraft.Version));
        Assert.True(activateInsurance.StatusCode == HttpStatusCode.OK,
            $"Insurance activation failed: {(int)activateInsurance.StatusCode} {await activateInsurance.Content.ReadAsStringAsync()}");
        var activeInsurance = await activateInsurance.Content.ReadFromJsonAsync<InsuranceEnrollmentView>();
        Assert.Equal("Active", activeInsurance!.Status);
        using var activeInsuranceHistory = await client.GetAsync($"/api/v1/hr/employees/{committed.EmployeeId:D}/employments/{committed.EmploymentId:D}/insurance?legalEntityId={legalEntityId:D}");
        Assert.Equal(HttpStatusCode.OK, activeInsuranceHistory.StatusCode);
        var insuranceHistory = await activeInsuranceHistory.Content.ReadFromJsonAsync<InsuranceEnrollmentView[]>();
        Assert.Equal(2, insuranceHistory!.Length);
        Assert.Contains(insuranceHistory, item => item.Status == "Active");
        Assert.Contains(insuranceHistory, item => item.Status == "Ended");
        using var rejectedInsuranceDraftResponse = await SendWithCsrfAsync(client, HttpMethod.Post,
            $"/api/v1/hr/employees/{committed.EmployeeId:D}/employments/{committed.EmploymentId:D}/insurance",
            new CreateInsuranceEnrollmentRequest(legalEntityId, "PRIVATE_MEDICAL", null, null, hireDate));
        Assert.Equal(HttpStatusCode.Created, rejectedInsuranceDraftResponse.StatusCode);
        var rejectedInsuranceDraft = await rejectedInsuranceDraftResponse.Content.ReadFromJsonAsync<InsuranceEnrollmentView>();
        using var rejectInsurance = await SendWithCsrfAsync(client, HttpMethod.Post,
            $"/api/v1/hr/employees/{committed.EmployeeId:D}/employments/{committed.EmploymentId:D}/insurance/{rejectedInsuranceDraft!.Id:D}/reject?legalEntityId={legalEntityId:D}",
            new InsuranceEnrollmentDecisionRequest(rejectedInsuranceDraft.Version, "Registration evidence does not match the employee record"));
        Assert.Equal(HttpStatusCode.OK, rejectInsurance.StatusCode);
        Assert.Equal("Rejected", (await rejectInsurance.Content.ReadFromJsonAsync<InsuranceEnrollmentView>())!.Status);

        using var replay = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/employees/commit", request);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var replayed = Assert.Single((await replay.Content.ReadFromJsonAsync<HrEmployeeImportSummary>())!.Rows);
        Assert.Equal("AlreadyImported", replayed.Status);
        Assert.Equal(committed.EmployeeId, replayed.EmployeeId);

        await using (var verify = new NpgsqlConnection(adminConnectionString))
        {
            await verify.OpenAsync();
            await using var assignment = new NpgsqlCommand("select exists(select 1 from business_hr.employment_assignment assignment join business_hr.department department on department.tenant_id=assignment.tenant_id and department.id=assignment.department_id join business_hr.employment_assignment_import_reference reference on reference.tenant_id=assignment.tenant_id and reference.assignment_id=assignment.id where assignment.tenant_id=$1 and assignment.employment_id=$2 and assignment.id=$3 and department.code=$4 and assignment.effective_from=$5 and assignment.effective_to=$6)", verify);
            assignment.Parameters.AddWithValue(tenantId); assignment.Parameters.AddWithValue(committed.EmploymentId!.Value); assignment.Parameters.AddWithValue(importedAssignment.AssignmentId!.Value); assignment.Parameters.AddWithValue(departmentCode); assignment.Parameters.AddWithValue(importedAssignmentStart); assignment.Parameters.AddWithValue(assignmentEffectiveDate);
            Assert.True((bool)(await assignment.ExecuteScalarAsync() ?? false));
            await using var importedContractSource = new NpgsqlCommand("select exists(select 1 from business_hr.employment_contract_import_reference where tenant_id=$1 and contract_id=$2 and data_quality='UnknownHistory' and source_system=$3 and file_sha256=$4)", verify);
            importedContractSource.Parameters.AddWithValue(tenantId); importedContractSource.Parameters.AddWithValue(importedContract.ContractId!.Value); importedContractSource.Parameters.AddWithValue(contractImport.SourceSystem); importedContractSource.Parameters.AddWithValue(fileHash);
            Assert.True((bool)(await importedContractSource.ExecuteScalarAsync() ?? false));
            await using var importedCompensationSource = new NpgsqlCommand("select exists(select 1 from business_hr.employment_compensation_import_reference where tenant_id=$1 and agreement_id=$2 and data_quality='UnknownHistory' and source_system=$3 and file_sha256=$4)", verify);
            importedCompensationSource.Parameters.AddWithValue(tenantId); importedCompensationSource.Parameters.AddWithValue(importedCompensation.AgreementId!.Value); importedCompensationSource.Parameters.AddWithValue(compensationImport.SourceSystem); importedCompensationSource.Parameters.AddWithValue(fileHash);
            Assert.True((bool)(await importedCompensationSource.ExecuteScalarAsync() ?? false));
            await using var importedInsuranceSource = new NpgsqlCommand("select exists(select 1 from business_hr.employee_insurance_import_reference r join business_hr.employee_insurance_enrollment i on i.tenant_id=r.tenant_id and i.id=r.enrollment_id where r.tenant_id=$1 and r.enrollment_id=$2 and r.data_quality='UnknownHistory' and i.encrypted_member_number is not null and i.masked_member_number<>$3 and i.encrypted_member_number<>$3)", verify);
            importedInsuranceSource.Parameters.AddWithValue(tenantId); importedInsuranceSource.Parameters.AddWithValue(importedInsurance.EnrollmentId!.Value); importedInsuranceSource.Parameters.AddWithValue(historicalInsuranceMemberNumber);
            Assert.True((bool)(await importedInsuranceSource.ExecuteScalarAsync() ?? false));
        }

        var changedRow = request.Rows[0] with { ArabicName = "مها إبراهيم" };
        var changedRequest = request with { Rows = [changedRow] };
        using var conflict = await SendWithCsrfAsync(client, HttpMethod.Post, "/api/v1/hr/imports/employees/preview", changedRequest);
        Assert.Equal(HttpStatusCode.OK, conflict.StatusCode);
        var rejected = Assert.Single((await conflict.Content.ReadFromJsonAsync<HrEmployeeImportSummary>())!.Rows);
        Assert.Equal("Rejected", rejected.Status);
        Assert.Contains("hr.import_row_conflict", rejected.Issues);

        using var employmentHistoryResponse = await client.GetAsync($"/api/v1/hr/employees/{committed.EmployeeId:D}/employments?legalEntityId={legalEntityId:D}");
        Assert.Equal(HttpStatusCode.OK, employmentHistoryResponse.StatusCode);
        var enumJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
        var employmentHistory = await employmentHistoryResponse.Content.ReadFromJsonAsync<EmploymentView[]>(enumJsonOptions);
        var priorEmployment = Assert.Single(employmentHistory!);
        var terminationDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var offboardingPlan = new OffboardingPlanRequest("Resignation", terminationDate.AddDays(-30), terminationDate, true, "LastWorkingDay", Tasks: [new("ASSET_RETURN", "استلام العهدة", terminationDate), new("EXIT_INTERVIEW", "مقابلة المغادرة", terminationDate)]);
        var termination = new ChangeEmploymentStatusRequest(EmploymentStatus.Terminated, terminationDate, "VOLUNTARY_RESIGNATION", priorEmployment.Version, offboardingPlan);
        using var terminationResponse = await SendWithCsrfAsync(client, HttpMethod.Post,
            $"/api/v1/hr/employees/{committed.EmployeeId:D}/employments/{priorEmployment.Id:D}/status?legalEntityId={legalEntityId:D}", termination);
        Assert.Equal(HttpStatusCode.OK, terminationResponse.StatusCode);
        using var offboardingResponse = await client.GetAsync($"/api/v1/hr/employees/{committed.EmployeeId:D}/employments/{priorEmployment.Id:D}/offboarding?legalEntityId={legalEntityId:D}");
        Assert.Equal(HttpStatusCode.OK, offboardingResponse.StatusCode);
        var offboardingCase = await offboardingResponse.Content.ReadFromJsonAsync<OffboardingCaseView>();
        Assert.NotNull(offboardingCase);
        Assert.Equal("Open", offboardingCase.Status);
        Assert.True(offboardingCase.FinalPayrollRequired);
        Assert.Equal(2, offboardingCase.Tasks.Count);
        await using (var reminderDataSource = NpgsqlDataSource.Create(runtimeConnectionString))
        {
            var reminders = new HrReminderRepository(reminderDataSource, TimeProvider.System);
            Assert.True(await reminders.ProcessDueAsync(new(tenantId), legalEntityId, CancellationToken.None) >= 2);
            Assert.Equal(0, await reminders.ProcessDueAsync(new(tenantId), legalEntityId, CancellationToken.None));
        }
        using var remindersResponse = await client.GetAsync($"/api/v1/hr/reminders?legalEntityId={legalEntityId:D}&asOfDate={terminationDate:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, remindersResponse.StatusCode);
        var reminderPage = await remindersResponse.Content.ReadFromJsonAsync<HrReminderPage>();
        Assert.NotNull(reminderPage);
        Assert.NotEmpty(reminderPage.Items);
        var offboardingTask = offboardingCase.Tasks[0];
        using var completeTaskResponse = await SendWithCsrfAsync(client, HttpMethod.Post,
            $"/api/v1/hr/offboarding/tasks/{offboardingTask.Id:D}/complete?legalEntityId={legalEntityId:D}", new CompleteOffboardingTaskRequest(offboardingTask.Version, "تم تسليم العهدة"));
        Assert.Equal(HttpStatusCode.OK, completeTaskResponse.StatusCode);
        var completedOffboarding = await completeTaskResponse.Content.ReadFromJsonAsync<OffboardingCaseView>();
        Assert.NotNull(completedOffboarding);
        Assert.Equal("Open", completedOffboarding.Status);
        var finalTask = completedOffboarding.Tasks.Single(task => task.Status == "Pending");
        using var completeFinalTaskResponse = await SendWithCsrfAsync(client, HttpMethod.Post,
            $"/api/v1/hr/offboarding/tasks/{finalTask.Id:D}/complete?legalEntityId={legalEntityId:D}", new CompleteOffboardingTaskRequest(finalTask.Version, "اكتملت المقابلة"));
        Assert.Equal(HttpStatusCode.OK, completeFinalTaskResponse.StatusCode);
        completedOffboarding = await completeFinalTaskResponse.Content.ReadFromJsonAsync<OffboardingCaseView>();
        Assert.NotNull(completedOffboarding);
        Assert.Equal("Completed", completedOffboarding.Status);
        Assert.All(completedOffboarding.Tasks, task => Assert.Equal("Completed", task.Status));
        await using (var payrollDataSource = NpgsqlDataSource.Create(runtimeConnectionString))
        {
            var payroll = new PayrollIntegrationRepository(new NpgsqlTransactionCoordinator(payrollDataSource), TimeProvider.System);
            var payrollSnapshot = await payroll.CreateSnapshotAsync(new(tenantId), new(legalEntityId, terminationDate, terminationDate, DateTimeOffset.UtcNow), CancellationToken.None);
            var finalExit = Assert.Single(payrollSnapshot.Employments, item => item.EmploymentId == priorEmployment.Id).FinalExit;
            Assert.NotNull(finalExit);
            Assert.Equal("Resignation", finalExit.ReasonType);
            Assert.Equal("VOLUNTARY_RESIGNATION", finalExit.ReasonCode);
            Assert.Equal(terminationDate, finalExit.LastWorkingDate);
            Assert.True(finalExit.FinalPayrollRequired);
        }
        var rehireDate = terminationDate.AddDays(1);
        using var overlappingRehire = await SendWithCsrfAsync(client, HttpMethod.Post,
            $"/api/v1/hr/employees/{committed.EmployeeId:D}/employments",
            new CreateEmploymentRequest(legalEntityId, EmploymentType.FullTime, terminationDate));
        Assert.Equal(HttpStatusCode.BadRequest, overlappingRehire.StatusCode);
        var rehire = new CreateEmploymentRequest(legalEntityId, EmploymentType.FullTime, rehireDate);
        using var rehireResponse = await SendWithCsrfAsync(client, HttpMethod.Post,
            $"/api/v1/hr/employees/{committed.EmployeeId:D}/employments", rehire);
        Assert.Equal(HttpStatusCode.Created, rehireResponse.StatusCode);
        var newEmployment = await rehireResponse.Content.ReadFromJsonAsync<EmploymentView>(enumJsonOptions);
        Assert.NotNull(newEmployment);
        Assert.Equal(EmploymentStatus.PreHire, newEmployment.Status);
        Assert.NotEqual(priorEmployment.Id, newEmployment.Id);

        var scheduledTerminationDate = rehireDate.AddDays(2);
        var scheduledOffboarding = new OffboardingPlanRequest("Resignation", rehireDate, scheduledTerminationDate.AddDays(-1), false, "LastWorkingDay");
        using var scheduleTerminationResponse = await SendWithCsrfAsync(client, HttpMethod.Post,
            $"/api/v1/hr/employees/{committed.EmployeeId:D}/employments/{newEmployment.Id:D}/status?legalEntityId={legalEntityId:D}",
            new ChangeEmploymentStatusRequest(EmploymentStatus.Terminated, scheduledTerminationDate, "VOLUNTARY_RESIGNATION", newEmployment.Version, scheduledOffboarding));
        Assert.Equal(HttpStatusCode.OK, scheduleTerminationResponse.StatusCode);
        var scheduledEmployment = await scheduleTerminationResponse.Content.ReadFromJsonAsync<EmploymentView>(enumJsonOptions);
        Assert.NotNull(scheduledEmployment);
        Assert.Equal(EmploymentStatus.PreHire, scheduledEmployment.Status);
        Assert.Equal(scheduledTerminationDate, scheduledEmployment.EndDate);
        using var scheduledCaseResponse = await client.GetAsync($"/api/v1/hr/employees/{committed.EmployeeId:D}/employments/{newEmployment.Id:D}/offboarding?legalEntityId={legalEntityId:D}");
        Assert.Equal(HttpStatusCode.OK, scheduledCaseResponse.StatusCode);
        var scheduledCase = await scheduledCaseResponse.Content.ReadFromJsonAsync<OffboardingCaseView>();
        Assert.NotNull(scheduledCase);
        Assert.Equal("Scheduled", scheduledCase.TerminationStatus);
        await using (var scheduledPayrollDataSource = NpgsqlDataSource.Create(runtimeConnectionString))
        {
            var payroll = new PayrollIntegrationRepository(new NpgsqlTransactionCoordinator(scheduledPayrollDataSource), TimeProvider.System);
            var beforeEffectiveSnapshot = await payroll.CreateSnapshotAsync(new(tenantId), new(legalEntityId, scheduledTerminationDate, scheduledTerminationDate, DateTimeOffset.UtcNow), CancellationToken.None);
            Assert.DoesNotContain(beforeEffectiveSnapshot.Employments, item => item.EmploymentId == newEmployment.Id);
        }
        await using (var lifecycleDataSource = NpgsqlDataSource.Create(runtimeConnectionString))
        {
            var lifecycle = new HrOffboardingLifecycleRepository(new NpgsqlTransactionCoordinator(lifecycleDataSource), TimeProvider.System);
            Assert.True(await lifecycle.ApplyDueTerminationsAsync(new(tenantId), legalEntityId, scheduledTerminationDate, CancellationToken.None) >= 1);
            Assert.Equal(0, await lifecycle.ApplyDueTerminationsAsync(new(tenantId), legalEntityId, scheduledTerminationDate, CancellationToken.None));
        }
        using var effectiveCaseResponse = await client.GetAsync($"/api/v1/hr/employees/{committed.EmployeeId:D}/employments/{newEmployment.Id:D}/offboarding?legalEntityId={legalEntityId:D}");
        Assert.Equal(HttpStatusCode.OK, effectiveCaseResponse.StatusCode);
        Assert.Equal("Effective", (await effectiveCaseResponse.Content.ReadFromJsonAsync<OffboardingCaseView>())!.TerminationStatus);
        await using (var effectivePayrollDataSource = NpgsqlDataSource.Create(runtimeConnectionString))
        {
            var payroll = new PayrollIntegrationRepository(new NpgsqlTransactionCoordinator(effectivePayrollDataSource), TimeProvider.System);
            var afterEffectiveSnapshot = await payroll.CreateSnapshotAsync(new(tenantId), new(legalEntityId, scheduledTerminationDate, scheduledTerminationDate, DateTimeOffset.UtcNow.AddSeconds(1)), CancellationToken.None);
            Assert.NotNull(Assert.Single(afterEffectiveSnapshot.Employments, item => item.EmploymentId == newEmployment.Id).FinalExit);
        }

        using var rehireHistoryResponse = await client.GetAsync($"/api/v1/hr/employees/{committed.EmployeeId:D}/employments?legalEntityId={legalEntityId:D}");
        Assert.Equal(HttpStatusCode.OK, rehireHistoryResponse.StatusCode);
        var rehireHistory = await rehireHistoryResponse.Content.ReadFromJsonAsync<EmploymentView[]>(enumJsonOptions);
        Assert.Equal(2, rehireHistory!.Length);
        Assert.Contains(rehireHistory, employment => employment.Id == priorEmployment.Id && employment.Status == EmploymentStatus.Terminated);
        Assert.Contains(rehireHistory, employment => employment.Id == newEmployment.Id && employment.Status == EmploymentStatus.Terminated && employment.EndDate == scheduledTerminationDate);

        await using var verifyRehire = new NpgsqlConnection(adminConnectionString);
        await verifyRehire.OpenAsync();
        await using var rehireEvent = new NpgsqlCommand("select exists(select 1 from business_hr.employment_event where tenant_id=$1 and employee_id=$2 and employment_id=$3 and event_type='Rehired' and payload->>'previousEmploymentId'=$4)", verifyRehire);
        rehireEvent.Parameters.AddWithValue(tenantId); rehireEvent.Parameters.AddWithValue(committed.EmployeeId!.Value); rehireEvent.Parameters.AddWithValue(newEmployment.Id);
        rehireEvent.Parameters.AddWithValue(priorEmployment.Id.ToString("D"));
        Assert.True((bool)(await rehireEvent.ExecuteScalarAsync() ?? false));

        var accrualAsOf = new DateOnly(DateTime.UtcNow.Year - 1, 12, 31);
        long accrualsBefore;
        await using (var accrualEvidence = new NpgsqlConnection(adminConnectionString))
        {
            await accrualEvidence.OpenAsync();
            await using var count = new NpgsqlCommand("select count(*) from business_hr.leave_balance_movement where tenant_id=$1 and legal_entity_id=$2 and movement_type='Accrual' and reason='Scheduled policy accrual' and effective_date<=$3", accrualEvidence);
            count.Parameters.AddWithValue(tenantId); count.Parameters.AddWithValue(legalEntityId); count.Parameters.AddWithValue(accrualAsOf);
            accrualsBefore = (long)(await count.ExecuteScalarAsync() ?? 0L);
        }
        await using (var accrualDataSource = NpgsqlDataSource.Create(runtimeConnectionString))
        {
            var leaveRequests = new LeaveRequestRepository(new NpgsqlTransactionCoordinator(accrualDataSource), accrualDataSource, TimeProvider.System);
            var inserted = await leaveRequests.AccrueScheduledPeriodsAsync(new(tenantId), legalEntityId, accrualAsOf, CancellationToken.None);
            var retry = await leaveRequests.AccrueScheduledPeriodsAsync(new(tenantId), legalEntityId, accrualAsOf, CancellationToken.None);
            Assert.Equal(0, retry);
            await using var verifyAccruals = new NpgsqlConnection(adminConnectionString);
            await verifyAccruals.OpenAsync();
            await using var count = new NpgsqlCommand("select count(*) from business_hr.leave_balance_movement where tenant_id=$1 and legal_entity_id=$2 and movement_type='Accrual' and reason='Scheduled policy accrual' and effective_date<=$3", verifyAccruals);
            count.Parameters.AddWithValue(tenantId); count.Parameters.AddWithValue(legalEntityId); count.Parameters.AddWithValue(accrualAsOf);
            var accrualsAfter = (long)(await count.ExecuteScalarAsync() ?? 0L);
            Assert.Equal(accrualsBefore + inserted, accrualsAfter);
            Assert.True(accrualsAfter > 0);
        }
    }

    [HrPostgresFact]
    public async Task AnnualCarryoverUsesFifoExpiryAndKeepsRemindersIdempotent()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION") ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for HR PostgreSQL integration tests.");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION") ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for HR PostgreSQL integration tests.");
        Guid tenantId; Guid legalEntityId; Guid employmentId; Guid leaveTypeId; Guid actorId; Guid membershipId;
        var carryoverBoundary = new DateOnly(DateTime.UtcNow.Year + 1, 1, 1);
        var policyEffectiveFrom = carryoverBoundary.AddYears(-1);
        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync();
            await using var transaction = await admin.BeginTransactionAsync();
            await using var scope = new NpgsqlCommand("select tenant.id,entity.id,employment.id,identity.id,membership.id from platform.tenant tenant join platform.legal_entity entity on entity.tenant_id=tenant.id and entity.status='Active' join business_hr.employment employment on employment.tenant_id=tenant.id and employment.legal_entity_id=entity.id and employment.status='Active' join platform.membership membership on membership.tenant_id=tenant.id and membership.status='Active' join platform.identity identity on identity.tenant_id=membership.tenant_id and identity.id=membership.identity_id and identity.status='Active' where tenant.slug='atlas-trading' and employment.employment_number='EMP-000001' order by entity.id,membership.id limit 1", admin, transaction);
            await using var reader = await scope.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) throw new InvalidOperationException("The canonical HR development seed must include its first employee and active user.");
            tenantId = reader.GetGuid(0); legalEntityId = reader.GetGuid(1); employmentId = reader.GetGuid(2); actorId = reader.GetGuid(3); membershipId = reader.GetGuid(4);
            await reader.CloseAsync();

            leaveTypeId = Guid.CreateVersion7();
            var policyId = Guid.CreateVersion7();
            var sourceReference = $"carryover-test-opening-{Guid.CreateVersion7():N}";
            await using (var insertType = new NpgsqlCommand("insert into business_hr.leave_type(id,tenant_id,legal_entity_id,code,name_ar,name_en,category,unit,status,created_by,created_at) values($1,$2,$3,$4,'إجازة ترحيل اختبار','Carryover integration test','Annual','Days','Active',$5,now())", admin, transaction))
            {
                insertType.Parameters.AddWithValue(leaveTypeId); insertType.Parameters.AddWithValue(tenantId); insertType.Parameters.AddWithValue(legalEntityId); insertType.Parameters.AddWithValue($"CARRY-{Guid.CreateVersion7():N}"[..22]); insertType.Parameters.AddWithValue(actorId);
                await insertType.ExecuteNonQueryAsync();
            }
            await using (var insertPolicy = new NpgsqlCommand("insert into business_hr.leave_policy(id,tenant_id,legal_entity_id,leave_type_id,version,effective_from,counting_method,approval_required,partial_day_allowed,negative_balance_policy,negative_balance_limit,payroll_treatment,accrual_rule,carryover_rule,eligibility_rules,created_by,created_at) values($1,$2,$3,$4,1,$5,'CalendarDays',true,false,'Disallow',0,'Paid','{\"mode\":\"None\"}'::jsonb,'{\"quantity\":5,\"expiryMonths\":6}'::jsonb,'{}'::jsonb,$6,now())", admin, transaction))
            {
                insertPolicy.Parameters.AddWithValue(policyId); insertPolicy.Parameters.AddWithValue(tenantId); insertPolicy.Parameters.AddWithValue(legalEntityId); insertPolicy.Parameters.AddWithValue(leaveTypeId); insertPolicy.Parameters.AddWithValue(policyEffectiveFrom); insertPolicy.Parameters.AddWithValue(actorId);
                await insertPolicy.ExecuteNonQueryAsync();
            }
            await using (var insertOpening = new NpgsqlCommand("insert into business_hr.leave_balance_movement(id,tenant_id,legal_entity_id,employment_id,leave_type_id,effective_date,movement_type,quantity,source_type,source_reference,idempotency_key,policy_version,expires_on,reason,created_by,created_at) values($1,$2,$3,$4,$5,$6,'Opening',5,'IntegrationTest',$7,$8,1,null,'Isolated entitlement for the carryover concurrency proof',$9,now())", admin, transaction))
            {
                insertOpening.Parameters.AddWithValue(Guid.CreateVersion7()); insertOpening.Parameters.AddWithValue(tenantId); insertOpening.Parameters.AddWithValue(legalEntityId); insertOpening.Parameters.AddWithValue(employmentId); insertOpening.Parameters.AddWithValue(leaveTypeId); insertOpening.Parameters.AddWithValue(policyEffectiveFrom); insertOpening.Parameters.AddWithValue(sourceReference); insertOpening.Parameters.AddWithValue($"carryover-test-opening:{leaveTypeId:D}"); insertOpening.Parameters.AddWithValue(actorId);
                await insertOpening.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }

        await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
        var repository = new LeaveRequestRepository(new NpgsqlTransactionCoordinator(dataSource), dataSource, TimeProvider.System);
        var tenant = new Shared.Primitives.Identifiers.TenantId(tenantId);
        await repository.ProcessScheduledCarryoversAsync(tenant, legalEntityId, carryoverBoundary, CancellationToken.None);
        await using var verify = new NpgsqlConnection(adminConnectionString); await verify.OpenAsync();
        Guid carryoverId; decimal carriedQuantity; DateOnly expiryDate;
        await using (var carryover = new NpgsqlCommand("select id,quantity,expires_on from business_hr.leave_balance_movement where tenant_id=$1 and employment_id=$2 and leave_type_id=$3 and movement_type='CarryOver' and effective_date=$4", verify))
        {
            carryover.Parameters.AddWithValue(tenantId); carryover.Parameters.AddWithValue(employmentId); carryover.Parameters.AddWithValue(leaveTypeId); carryover.Parameters.AddWithValue(carryoverBoundary);
            await using var reader = await carryover.ExecuteReaderAsync(System.Data.CommandBehavior.SingleRow);
            Assert.True(await reader.ReadAsync(), "The Jan 1 carryover job must create an independent expiring entitlement bucket.");
            carryoverId = reader.GetGuid(0); carriedQuantity = reader.GetDecimal(1); expiryDate = reader.GetFieldValue<DateOnly>(2);
        }
        Assert.Equal(carryoverBoundary.AddMonths(6), expiryDate);
        Assert.InRange(carriedQuantity, 0.0001m, 5m);
        Assert.Equal(0, await repository.ProcessScheduledCarryoversAsync(tenant, legalEntityId, carryoverBoundary, CancellationToken.None));

        var request = await repository.CreateAsync(tenant, actorId, membershipId, employmentId,
            new CreateLeaveRequest(legalEntityId, leaveTypeId, carryoverBoundary.AddDays(4), carryoverBoundary.AddDays(4), $"hr-carryover-fifo-proof-{Guid.CreateVersion7():N}", RequestedQuantity: 1m, Reason: "FIFO entitlement allocation proof"), Guid.CreateVersion7(), CancellationToken.None);
        Assert.Equal("Pending", request.Status);

        async Task<(LeaveRequestView? Result, Exception? Error)> TryApproveAsync()
        {
            try
            {
                return (await repository.DecideAsync(tenant, actorId, membershipId, request.Id, legalEntityId,
                    new LeaveRequestDecision(request.Version, "Concurrent approval race proof"), true, Guid.CreateVersion7(), CancellationToken.None), null);
            }
            catch (Exception exception)
            {
                return (null, exception);
            }
        }

        var concurrentDecisions = await Task.WhenAll(TryApproveAsync(), TryApproveAsync());
        var approvedDecision = Assert.Single(concurrentDecisions, result => result.Result is not null);
        Assert.Null(approvedDecision.Error);
        Assert.Single(concurrentDecisions, result => result.Error is not null);
        Assert.Equal("Approved", approvedDecision.Result!.Status);
        await using (var consumption = new NpgsqlCommand("select count(*) from business_hr.leave_balance_movement where tenant_id=$1 and source_type='LeaveRequest' and source_reference=$2 and movement_type='LeaveTaken'", verify))
        {
            consumption.Parameters.AddWithValue(tenantId); consumption.Parameters.AddWithValue(request.Id.ToString("D"));
            Assert.Equal(1L, Convert.ToInt64(await consumption.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
        }

        await using (var allocation = new NpgsqlCommand("select allocation.entitlement_movement_id from business_hr.leave_balance_movement reservation join business_hr.leave_balance_allocation allocation on allocation.tenant_id=reservation.tenant_id and allocation.movement_id=reservation.id where reservation.tenant_id=$1 and reservation.source_reference=$2 and reservation.movement_type='RequestReservation'", verify))
        {
            allocation.Parameters.AddWithValue(tenantId); allocation.Parameters.AddWithValue(request.Id.ToString("D"));
            Assert.Equal(carryoverId, (Guid?)await allocation.ExecuteScalarAsync());
        }

        var reminders = new HrReminderRepository(dataSource, TimeProvider.System);
        var reminderDate = expiryDate.AddDays(-15);
        _ = await reminders.ProcessDueAsync(tenant, legalEntityId, reminderDate, CancellationToken.None);
        Assert.Equal(0, await reminders.ProcessDueAsync(tenant, legalEntityId, reminderDate, CancellationToken.None));
        await using (var reminder = new NpgsqlCommand("select count(*) from business_hr.hr_reminder where tenant_id=$1 and legal_entity_id=$2 and reminder_type='LeaveBalanceExpiry' and source_id=$3 and due_date=$4", verify))
        {
            reminder.Parameters.AddWithValue(tenantId); reminder.Parameters.AddWithValue(legalEntityId); reminder.Parameters.AddWithValue(carryoverId); reminder.Parameters.AddWithValue(expiryDate);
            Assert.Equal(1L, Convert.ToInt64(await reminder.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
        }

        Assert.True(await repository.ProcessScheduledCarryoversAsync(tenant, legalEntityId, expiryDate, CancellationToken.None) > 0);
        await using (var expired = new NpgsqlCommand("select sum(movement.quantity) from business_hr.leave_balance_movement movement join business_hr.leave_balance_allocation allocation on allocation.tenant_id=movement.tenant_id and allocation.movement_id=movement.id where movement.tenant_id=$1 and allocation.entitlement_movement_id=$2 and movement.movement_type='Expiry'", verify))
        {
            expired.Parameters.AddWithValue(tenantId); expired.Parameters.AddWithValue(carryoverId);
            Assert.Equal(-carriedQuantity + 1m, Convert.ToDecimal(await expired.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
        }
        Assert.Equal(0, await repository.ProcessScheduledCarryoversAsync(tenant, legalEntityId, expiryDate, CancellationToken.None));
    }

    private static async Task CreateAccountAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid tenantId, string email, string password)
    {
        var identityId = Guid.CreateVersion7();
        var membershipId = Guid.CreateVersion7();
        await using (var identity = new NpgsqlCommand("insert into platform.identity(id,tenant_id,email,normalized_email,login_prefix,name,email_verified,password_hash,must_change_password,status,created_at,security_stamp,version) values($1,$2,$3,$4,$5,'HR employee import integration',true,$6,false,'Active',now(),$7,1)", connection, transaction))
        {
            identity.Parameters.AddWithValue(identityId); identity.Parameters.AddWithValue(tenantId); identity.Parameters.AddWithValue(email); identity.Parameters.AddWithValue(IdentityConventions.NormalizeEmail(email));
            identity.Parameters.AddWithValue(email[..email.IndexOf('@')]); identity.Parameters.AddWithValue(IdentityConventions.HashPassword(password)); identity.Parameters.AddWithValue(Guid.CreateVersion7());
            await identity.ExecuteNonQueryAsync();
        }
        await using (var membership = new NpgsqlCommand("insert into platform.membership(id,tenant_id,identity_id,display_name,status,created_at,version) values($1,$2,$3,'HR employee import integration','Active',now(),1)", connection, transaction))
        {
            membership.Parameters.AddWithValue(membershipId); membership.Parameters.AddWithValue(tenantId); membership.Parameters.AddWithValue(identityId); await membership.ExecuteNonQueryAsync();
        }
        await using var assignment = new NpgsqlCommand("insert into platform.membership_role_assignment(tenant_id,membership_id,role_id,scope_type) select $1,$2,id,'Tenant' from platform.role where tenant_id=$1 and code='hr_privacy' and is_active", connection, transaction);
        assignment.Parameters.AddWithValue(tenantId); assignment.Parameters.AddWithValue(membershipId);
        if (await assignment.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("The canonical HR privacy role is missing from the HR integration seed.");
    }

    private static async Task LoginAsync(HttpClient client, string email, string password, Guid tenantId)
    {
        var token = await GetCsrfTokenAsync(client);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = JsonContent.Create(new { email, password }) };
        request.Headers.Add("X-CSRF-TOKEN", token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(tenantId, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tenantId").GetGuid());
    }

    private static async Task<HttpResponseMessage> SendWithCsrfAsync<T>(HttpClient client, HttpMethod method, string path, T content)
    {
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(content) };
        request.Headers.Add("X-CSRF-TOKEN", await GetCsrfTokenAsync(client));
        return await client.SendAsync(request);
    }

    private static async Task<string> GetCsrfTokenAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/v1/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString() ?? throw new InvalidOperationException("The API did not issue a CSRF token.");
    }

    private sealed class HrPostgresFactAttribute : FactAttribute
    {
        public HrPostgresFactAttribute()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable("TODIR_HR_TEST_DATABASE"), "1", StringComparison.Ordinal))
                Skip = "Set TODIR_HR_TEST_DATABASE=1 only for a disposable PostgreSQL database with the canonical development seed.";
        }
    }
}
