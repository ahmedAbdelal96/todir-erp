using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Modules.Tax.Entities;
using Modules.Tax.Repositories;
using Npgsql;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class TaxProfileImportIntegrationTests : DatabaseIntegrationTestBase {
    [Fact]
    public async Task TaxProfileImportExportRunsAgainstDisposablePostgresAndPreservesAtomicity()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for the Tax PostgreSQL behavior gate.");
        var databaseName = $"tax_p3a_{Guid.CreateVersion7():N}";
        var seedPassword = $"Tax-Pass3A-{Convert.ToHexString(RandomNumberGenerator.GetBytes(12))}a1";
        var runtimePassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var migratorConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = databaseName }.ConnectionString;
        var runtimeConnectionString = new NpgsqlConnectionStringBuilder(migratorConnectionString) { Username = "erp_app", Password = runtimePassword }.ConnectionString;
        var repositoryRoot = FindRepositoryRoot();
        var databaseCreated = false;

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        try
        {
            await using (var create = new NpgsqlCommand($"create database \"{databaseName}\"", admin)) await create.ExecuteNonQueryAsync();
            databaseCreated = true;
            var migrator = await RunMigratorAsync(repositoryRoot, migratorConnectionString, seedPassword, runtimePassword);
            Assert.Equal(0, migrator.ExitCode);
            Assert.Contains("Development Seed Ready", migrator.Output, StringComparison.Ordinal);

            var fixture = await ReadFixtureAsync(adminConnectionString, databaseName);
            await using var databaseAdmin = new NpgsqlConnection(migratorConnectionString);
            await databaseAdmin.OpenAsync();
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
                .UseEnvironment("Development")
                .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnectionString)
                .UseSetting("TODIR_RABBITMQ_HOST", string.Empty)
                .UseSetting("TODIR_COOKIE_SECURE_POLICY", "Always"));
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
            await LoginAsync(client, fixture.TenantId, seedPassword);

            var initialCounts = await ReadProfileCountsAsync(databaseAdmin, fixture.TenantId);
            var productPreview = await PostJsonAsync(client, "/api/v1/tax/imports/product-profiles/preview", new
            {
                kind = "Product", legalEntityId = fixture.LegalEntityId, sourceReference = "tax-pass-3a-product-round-trip",
                rows = new[]
                {
                    ProductRow(1, fixture.TemplateCode, "Template", "2027-01-01"),
                    ProductRow(2, fixture.VariantSku, "Variant", "2027-01-01"),
                    ProductRow(3, "UNKNOWN-TAX-PRODUCT", "Template", "2027-01-01")
                }
            }, HttpStatusCode.OK);
            Assert.Equal("Previewed", productPreview.GetProperty("status").GetString());
            Assert.Equal("Create", productPreview.GetProperty("rows")[0].GetProperty("outcome").GetString());
            Assert.Equal("Create", productPreview.GetProperty("rows")[1].GetProperty("outcome").GetString());
            Assert.Equal("UnknownProduct", productPreview.GetProperty("rows")[2].GetProperty("outcome").GetString());
            Assert.Equal(initialCounts, await ReadProfileCountsAsync(databaseAdmin, fixture.TenantId));

            var productBatchId = productPreview.GetProperty("batchId").GetGuid();
            var productCommit = await PostJsonAsync(client, $"/api/v1/tax/imports/{productBatchId:D}/commit?legalEntityId={fixture.LegalEntityId:D}", new { batchId = productBatchId }, HttpStatusCode.OK);
            Assert.Equal(2, productCommit.GetProperty("createdCount").GetInt32());
            Assert.Equal(2, productCommit.GetProperty("acceptedCount").GetInt32());
            Assert.Equal(2, (await ReadProfileCountsAsync(databaseAdmin, fixture.TenantId)).ProductCount - initialCounts.ProductCount);

            var productVersionPreview = await PostJsonAsync(client, "/api/v1/tax/imports/product-profiles/preview", new
            {
                kind = "Product", legalEntityId = fixture.LegalEntityId, sourceReference = "tax-pass-3a-product-version",
                rows = new[]
                {
                    ProductRow(1, fixture.TemplateCode, "Template", "2028-01-01"),
                    ProductRow(2, fixture.VariantSku, "Variant", "2028-01-01")
                }
            }, HttpStatusCode.OK);
            Assert.All(productVersionPreview.GetProperty("rows").EnumerateArray(), row => Assert.Equal("CreateNewVersion", row.GetProperty("outcome").GetString()));
            var productVersionCommit = await PostJsonAsync(client, $"/api/v1/tax/imports/{productVersionPreview.GetProperty("batchId").GetGuid():D}/commit?legalEntityId={fixture.LegalEntityId:D}", new { batchId = productVersionPreview.GetProperty("batchId").GetGuid() }, HttpStatusCode.OK);
            Assert.Equal(2, productVersionCommit.GetProperty("versionedCount").GetInt32());

            var duplicatePreview = await PostJsonAsync(client, "/api/v1/tax/imports/product-profiles/preview", new
            {
                kind = "Product", legalEntityId = fixture.LegalEntityId,
                rows = new[] { ProductRow(1, fixture.TemplateCode, "Template", "2029-01-01"), ProductRow(2, fixture.TemplateCode, "Template", "2029-01-01") }
            }, HttpStatusCode.OK);
            Assert.Equal("DuplicateInput", duplicatePreview.GetProperty("rows")[1].GetProperty("outcome").GetString());
            var overlapPreview = await PostJsonAsync(client, "/api/v1/tax/imports/product-profiles/preview", new
            {
                kind = "Product", legalEntityId = fixture.LegalEntityId,
                rows = new[] { ProductRow(1, fixture.TemplateCode, "Template", "2027-01-01") }
            }, HttpStatusCode.OK);
            Assert.Equal("EffectiveDateOverlap", overlapPreview.GetProperty("rows")[0].GetProperty("outcome").GetString());
            Assert.Equal(initialCounts.ProductCount + 4, (await ReadProfileCountsAsync(databaseAdmin, fixture.TenantId)).ProductCount);

            var partyPreview = await PostJsonAsync(client, "/api/v1/tax/imports/party-profiles/preview", new
            {
                kind = "Party", legalEntityId = fixture.LegalEntityId, sourceReference = "tax-pass-3a-party-round-trip",
                rows = new[] { PartyRow(1, fixture.PartyReference, "2027-01-01", "TEST-REG-001") }
            }, HttpStatusCode.OK);
            Assert.Equal("Create", partyPreview.GetProperty("rows")[0].GetProperty("outcome").GetString());
            Assert.Equal(initialCounts.PartyCount, (await ReadProfileCountsAsync(databaseAdmin, fixture.TenantId)).PartyCount);
            var partyBatchId = partyPreview.GetProperty("batchId").GetGuid();
            var partyCommit = await PostJsonAsync(client, $"/api/v1/tax/imports/{partyBatchId:D}/commit?legalEntityId={fixture.LegalEntityId:D}", new { batchId = partyBatchId }, HttpStatusCode.OK);
            Assert.Equal(1, partyCommit.GetProperty("createdCount").GetInt32());

            var partyVersionPreview = await PostJsonAsync(client, "/api/v1/tax/imports/party-profiles/preview", new
            {
                kind = "Party", legalEntityId = fixture.LegalEntityId,
                rows = new[] { PartyRow(1, fixture.PartyReference, "2028-01-01", "TEST-REG-002") }
            }, HttpStatusCode.OK);
            Assert.Equal("CreateNewVersion", partyVersionPreview.GetProperty("rows")[0].GetProperty("outcome").GetString());
            var partyVersionBatchId = partyVersionPreview.GetProperty("batchId").GetGuid();
            var partyVersionCommit = await PostJsonAsync(client, $"/api/v1/tax/imports/{partyVersionBatchId:D}/commit?legalEntityId={fixture.LegalEntityId:D}", new { batchId = partyVersionBatchId }, HttpStatusCode.OK);
            Assert.Equal(1, partyVersionCommit.GetProperty("versionedCount").GetInt32());

            var invalidRegistration = await PostJsonAsync(client, "/api/v1/tax/imports/party-profiles/preview", new
            {
                kind = "Party", legalEntityId = fixture.LegalEntityId,
                rows = new[] { PartyRow(1, fixture.PartyReference, "2030-01-01", "TEST-REG-003") with { registrationCountryCode = "Eg" } }
            }, HttpStatusCode.OK);
            Assert.Equal("InvalidTaxNumberFormat", invalidRegistration.GetProperty("rows")[0].GetProperty("outcome").GetString());
            var unknownParty = await PostJsonAsync(client, "/api/v1/tax/imports/party-profiles/preview", new
            {
                kind = "Party", legalEntityId = fixture.LegalEntityId,
                rows = new[] { PartyRow(1, "directory-import:UNKNOWN-PARTY", "2030-01-01", "TEST-REG-004") }
            }, HttpStatusCode.OK);
            Assert.Equal("UnknownParty", unknownParty.GetProperty("rows")[0].GetProperty("outcome").GetString());
            var partyOverlap = await PostJsonAsync(client, "/api/v1/tax/imports/party-profiles/preview", new
            {
                kind = "Party", legalEntityId = fixture.LegalEntityId,
                rows = new[] { PartyRow(1, fixture.PartyReference, "2027-01-01", "TEST-REG-005") }
            }, HttpStatusCode.OK);
            Assert.Equal("EffectiveDateOverlap", partyOverlap.GetProperty("rows")[0].GetProperty("outcome").GetString());

            var productExport = await GetTextAsync(client, $"/api/v1/tax/exports/Product?legalEntityId={fixture.LegalEntityId:D}", HttpStatusCode.OK);
            Assert.Contains("ProductScope,ProductReference,SKU,TaxCategoryCode,CountryCode,Scheme,LegalEntityCode,EffectiveFrom,EffectiveTo,Version", productExport, StringComparison.Ordinal);
            Assert.Contains(fixture.TemplateCode, productExport, StringComparison.Ordinal);
            Assert.Contains("2027-01-01", productExport, StringComparison.Ordinal);
            Assert.Contains("2028-01-01", productExport, StringComparison.Ordinal);

            var partyExport = await GetTextAsync(client, $"/api/v1/tax/exports/Party?legalEntityId={fixture.LegalEntityId:D}", HttpStatusCode.OK);
            Assert.Contains("PartyReference,RegistrationCountry,RegistrationNumber,TaxTreatment,TaxCategoryCode,Scheme,LegalEntity,EffectiveFrom,EffectiveTo,Version", partyExport, StringComparison.Ordinal);
            Assert.Contains("TEST-REG-001", partyExport, StringComparison.Ordinal);
            Assert.Contains("TEST-REG-002", partyExport, StringComparison.Ordinal);

            await RemovePermissionAsync(databaseAdmin, fixture.TenantId, fixture.AdministratorRoleId, "tax.registration.read");
            using var restrictedFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
                .UseEnvironment("Development")
                .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnectionString)
                .UseSetting("TODIR_RABBITMQ_HOST", string.Empty)
                .UseSetting("TODIR_COOKIE_SECURE_POLICY", "Always"));
            using var restrictedClient = restrictedFactory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
            await LoginAsync(restrictedClient, fixture.TenantId, seedPassword);
            var restrictedPartyExport = await GetTextAsync(restrictedClient, $"/api/v1/tax/exports/Party?legalEntityId={fixture.LegalEntityId:D}", HttpStatusCode.OK);
            Assert.DoesNotContain("TEST-REG-001", restrictedPartyExport, StringComparison.Ordinal);
            Assert.Contains(fixture.PartyReference, restrictedPartyExport, StringComparison.Ordinal);
            await RemovePermissionAsync(databaseAdmin, fixture.TenantId, fixture.AdministratorRoleId, "tax.export");
            using var forbiddenFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
                .UseEnvironment("Development")
                .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnectionString)
                .UseSetting("TODIR_RABBITMQ_HOST", string.Empty)
                .UseSetting("TODIR_COOKIE_SECURE_POLICY", "Always"));
            using var forbiddenClient = forbiddenFactory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
            await LoginAsync(forbiddenClient, fixture.TenantId, seedPassword);
            using var forbiddenExport = await forbiddenClient.GetAsync($"/api/v1/tax/exports/Product?legalEntityId={fixture.LegalEntityId:D}");
            Assert.Equal(HttpStatusCode.Forbidden, forbiddenExport.StatusCode);
            using var anonymousClient = forbiddenFactory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
            using var unauthorizedExport = await anonymousClient.GetAsync($"/api/v1/tax/exports/Product?legalEntityId={fixture.LegalEntityId:D}");
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedExport.StatusCode);

            await RestorePermissionAsync(databaseAdmin, fixture.TenantId, fixture.AdministratorRoleId, "tax.export");
            await RestorePermissionAsync(databaseAdmin, fixture.TenantId, fixture.AdministratorRoleId, "tax.registration.read");

            var rollbackPreview = await PostJsonAsync(client, "/api/v1/tax/imports/product-profiles/preview", new
            {
                kind = "Product", legalEntityId = fixture.LegalEntityId,
                rows = new[] { ProductRow(1, fixture.TemplateCodeForRollback, "Template", "2031-01-01"), ProductRow(2, fixture.SecondTemplateCodeForRollback, "Template", "2031-01-01") }
            }, HttpStatusCode.OK);
            var rollbackBatchId = rollbackPreview.GetProperty("batchId").GetGuid();
            Assert.Equal(2, rollbackPreview.GetProperty("acceptedCount").GetInt32());
            await CorruptAcceptedRowCategoryAsync(databaseAdmin, fixture.TenantId, rollbackBatchId, 2);
            var rollbackResponse = await PostJsonAsync(client, $"/api/v1/tax/imports/{rollbackBatchId:D}/commit?legalEntityId={fixture.LegalEntityId:D}", new { batchId = rollbackBatchId }, HttpStatusCode.Conflict);
            Assert.Contains("Tax Category", rollbackResponse.GetProperty("title").GetString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(initialCounts.ProductCount + 4, (await ReadProfileCountsAsync(databaseAdmin, fixture.TenantId)).ProductCount);
            Assert.Equal("Previewed", await ReadBatchStatusAsync(databaseAdmin, fixture.TenantId, rollbackBatchId));
            Assert.Equal(0, await CountOutboxAsync(databaseAdmin, fixture.TenantId, rollbackBatchId));
        }
        finally
        {
            if (databaseCreated)
            {
                await using var terminate = new NpgsqlCommand("select pg_terminate_backend(pid) from pg_stat_activity where datname = $1 and pid <> pg_backend_pid()", admin);
                terminate.Parameters.AddWithValue(databaseName);
                await terminate.ExecuteNonQueryAsync();
                await using var drop = new NpgsqlCommand($"drop database if exists \"{databaseName}\"", admin);
                await drop.ExecuteNonQueryAsync();
            }
        }
    }

    [Fact]
    public async Task PersistedCountryPackLifecycleProtectsHistoryAndWritesAuditOutboxAtomically()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for the Tax PostgreSQL behavior gate.");
        var databaseName = $"tax_p3b_{Guid.CreateVersion7():N}";
        var seedPassword = $"Tax-Pass3B-{Convert.ToHexString(RandomNumberGenerator.GetBytes(12))}a1";
        var runtimePassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var migratorConnectionString = new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = databaseName }.ConnectionString;
        var runtimeConnectionString = new NpgsqlConnectionStringBuilder(migratorConnectionString) { Username = "erp_app", Password = runtimePassword }.ConnectionString;
        var databaseCreated = false;

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        try
        {
            await using (var create = new NpgsqlCommand($"create database \"{databaseName}\"", admin)) await create.ExecuteNonQueryAsync();
            databaseCreated = true;
            var migrator = await RunMigratorAsync(FindRepositoryRoot(), migratorConnectionString, seedPassword, runtimePassword);
            Assert.Equal(0, migrator.ExitCode);
            var fixture = await ReadFixtureAsync(adminConnectionString, databaseName);
            await using var databaseAdmin = new NpgsqlConnection(migratorConnectionString);
            await databaseAdmin.OpenAsync();
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
                .UseEnvironment("Development")
                .UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnectionString)
                .UseSetting("TODIR_RABBITMQ_HOST", string.Empty)
                .UseSetting("TODIR_COOKIE_SECURE_POLICY", "Always"));
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
            await LoginAsync(client, fixture.TenantId, seedPassword);

            var packCode = $"TEST-PACK-{Guid.CreateVersion7():N}";
            var v1 = await PostJsonAsync(client, "/api/v1/tax/country-packs", PackRequest(packCode, "1.0", "2026-01-01", "TestOnly", "fixture:p3b:v1", 1, "Passed"), HttpStatusCode.OK);
            var v1Id = v1.GetProperty("id").GetGuid();
            var duplicateV1 = await PostJsonAsync(client, "/api/v1/tax/country-packs", PackRequest(packCode, "1.0", "2026-01-01", "TestOnly", "fixture:p3b:v1", 1, "Passed"), HttpStatusCode.OK);
            Assert.Equal(v1Id, duplicateV1.GetProperty("id").GetGuid());
            var v1Validation = await PostJsonAsync(client, $"/api/v1/tax/country-packs/{v1Id:D}/validate", new { }, HttpStatusCode.OK);
            Assert.True(v1Validation.GetProperty("valid").GetBoolean());
            var v1Scheduled = await PostJsonAsync(client, $"/api/v1/tax/country-packs/{v1Id:D}/schedule", new { effectiveFrom = "2026-01-01" }, HttpStatusCode.OK);
            Assert.Equal("Scheduled", v1Scheduled.GetProperty("status").GetString());
            var v1Active = await PostJsonAsync(client, $"/api/v1/tax/country-packs/{v1Id:D}/activate", new { effectiveFrom = "2026-01-01" }, HttpStatusCode.OK);
            Assert.Equal("Active", v1Active.GetProperty("status").GetString());

            var v2 = await PostJsonAsync(client, "/api/v1/tax/country-packs", PackRequest(packCode, "2.0", "2027-01-01", "TestOnly", "fixture:p3b:v2", 1, "Passed"), HttpStatusCode.OK);
            var v2Id = v2.GetProperty("id").GetGuid();
            await PostJsonAsync(client, $"/api/v1/tax/country-packs/{v2Id:D}/validate", new { }, HttpStatusCode.OK);
            await PostJsonAsync(client, $"/api/v1/tax/country-packs/{v2Id:D}/schedule", new { effectiveFrom = "2027-01-01" }, HttpStatusCode.OK);
            var v2Active = await PostJsonAsync(client, $"/api/v1/tax/country-packs/{v2Id:D}/activate", new { effectiveFrom = "2027-01-01" }, HttpStatusCode.OK);
            Assert.Equal("Active", v2Active.GetProperty("status").GetString());
            Assert.Equal("Superseded", await ReadPackStatusAsync(databaseAdmin, fixture.TenantId, v1Id));
            Assert.Equal("1.0", await ResolvePackVersionAsync(databaseAdmin, fixture.TenantId, "EG", new DateOnly(2026, 12, 31)));
            Assert.Equal("2.0", await ResolvePackVersionAsync(databaseAdmin, fixture.TenantId, "EG", new DateOnly(2027, 1, 1)));
            Assert.Equal("2.0", await ResolvePackVersionAsync(databaseAdmin, fixture.TenantId, "EG", new DateOnly(2028, 1, 1)));

            var impact = await PostJsonAsync(client, "/api/v1/tax/draft-impact", new
            {
                legalEntityId = fixture.LegalEntityId, taxDate = "2027-01-01", countryCode = "EG", schemeCode = (string?)null,
                previousPackVersion = "1.0", previousRuleSetVersion = (int?)null, previousTaxFingerprint = "1.0:missing"
            }, HttpStatusCode.OK);
            Assert.True(impact.GetProperty("taxRecalculationRequired").GetBoolean());
            Assert.Equal("CountryPackVersionChanged", impact.GetProperty("reason").GetString());

            var snapshotId = Guid.CreateVersion7();
            var sourceDocumentId = Guid.CreateVersion7();
            await using (var snapshot = new NpgsqlCommand("insert into foundation_tax.tax_snapshot(tenant_id,id,legal_entity_id,source_document_kind,source_document_id,source_line_id,tax_date,calculation_version,rule_set_version,evaluation_state,result,created_at) values($1,$2,$3,'P3B-Finalized',$4,null,'2027-01-01','tax_calculation_v1','2.0','Evaluated','{\"taxTotal\":\"10.00\"}'::jsonb,now())", databaseAdmin))
            {
                snapshot.Parameters.AddWithValue(fixture.TenantId); snapshot.Parameters.AddWithValue(snapshotId); snapshot.Parameters.AddWithValue(fixture.LegalEntityId); snapshot.Parameters.AddWithValue(sourceDocumentId); await snapshot.ExecuteNonQueryAsync();
            }
            var beforeSnapshot = await ReadSnapshotAsync(databaseAdmin, fixture.TenantId, snapshotId);

            var rollback = await PostJsonAsync(client, $"/api/v1/tax/country-packs/{v1Id:D}/rollback-for-future", new { effectiveFrom = "2028-01-01", reason = "Future corrective rollback" }, HttpStatusCode.OK);
            Assert.Equal("Active", rollback.GetProperty("status").GetString());
            Assert.Equal("1.0", await ResolvePackVersionAsync(databaseAdmin, fixture.TenantId, "EG", new DateOnly(2028, 1, 1)));
            Assert.Equal(beforeSnapshot, await ReadSnapshotAsync(databaseAdmin, fixture.TenantId, snapshotId));
            Assert.True(await CountAuditAsync(databaseAdmin, fixture.TenantId, "CountryPackRollbackForFuture") >= 1);
            Assert.True(await CountOutboxTypeAsync(databaseAdmin, fixture.TenantId, "TaxCountryPackActivatedV1") >= 3);

            var v3 = await PostJsonAsync(client, "/api/v1/tax/country-packs", PackRequest(packCode, "3.0", "2030-01-01", "TestOnly", "fixture:p3b:v3", 1, "Passed"), HttpStatusCode.OK);
            var v3Id = v3.GetProperty("id").GetGuid();
            await PostJsonAsync(client, $"/api/v1/tax/country-packs/{v3Id:D}/validate", new { }, HttpStatusCode.OK);
            await PostJsonAsync(client, $"/api/v1/tax/country-packs/{v3Id:D}/schedule", new { effectiveFrom = "2030-01-01" }, HttpStatusCode.OK);
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<TaxCountryPackRepository>();
                await Assert.ThrowsAsync<TaxException>(() => repository.ActivateAsync(fixture.TenantId, fixture.AdministratorId, Guid.CreateVersion7(), v3Id, new DateOnly(2030, 1, 1), true, CancellationToken.None));
            }
            Assert.Equal("Scheduled", await ReadPackStatusAsync(databaseAdmin, fixture.TenantId, v3Id));
            Assert.Equal(0, await CountAuditForPackAsync(databaseAdmin, fixture.TenantId, v3Id, "CountryPackActivated"));
            Assert.Equal(0, await CountOutboxForPackAsync(databaseAdmin, fixture.TenantId, v3Id, "TaxCountryPackActivatedV1"));

            var statutory = await PostJsonAsync(client, "/api/v1/tax/country-packs", PackRequest($"{packCode}-STAT", "1.0", "2031-01-01", "Statutory", "", 0, "Missing"), HttpStatusCode.OK);
            var statutoryId = statutory.GetProperty("id").GetGuid();
            var statutoryValidation = await PostJsonAsync(client, $"/api/v1/tax/country-packs/{statutoryId:D}/validate", new { }, HttpStatusCode.OK);
            Assert.False(statutoryValidation.GetProperty("valid").GetBoolean());
            Assert.Contains(statutoryValidation.GetProperty("issues").EnumerateArray(), issue => issue.GetProperty("code").GetString() == "tax.country_pack_evidence_missing");
            Assert.Contains(statutoryValidation.GetProperty("issues").EnumerateArray(), issue => issue.GetProperty("code").GetString() == "tax.country_pack_review_required");

            using var anonymous = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
            using var anonymousResponse = await anonymous.PostAsJsonAsync("/api/v1/tax/country-packs", PackRequest($"{packCode}-ANON", "1.0", "2032-01-01", "TestOnly", "fixture:p3b:anon", 1, "Passed"));
            Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

            await RemovePermissionAsync(databaseAdmin, fixture.TenantId, fixture.AdministratorRoleId, "tax.country_pack.manage");
            using var manageForbiddenFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Development").UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnectionString).UseSetting("TODIR_RABBITMQ_HOST", string.Empty).UseSetting("TODIR_COOKIE_SECURE_POLICY", "Always"));
            using var manageForbiddenClient = manageForbiddenFactory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
            await LoginAsync(manageForbiddenClient, fixture.TenantId, seedPassword);
            using var manageForbidden = await PostRawAsync(manageForbiddenClient, "/api/v1/tax/country-packs", PackRequest($"{packCode}-FORBIDDEN", "1.0", "2033-01-01", "TestOnly", "fixture:p3b:forbidden", 1, "Passed"));
            Assert.Equal(HttpStatusCode.Forbidden, manageForbidden.StatusCode);
            await RestorePermissionAsync(databaseAdmin, fixture.TenantId, fixture.AdministratorRoleId, "tax.country_pack.manage");

            await RemovePermissionAsync(databaseAdmin, fixture.TenantId, fixture.AdministratorRoleId, "tax.country_pack.activate");
            using var activateForbiddenFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Development").UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnectionString).UseSetting("TODIR_RABBITMQ_HOST", string.Empty).UseSetting("TODIR_COOKIE_SECURE_POLICY", "Always"));
            using var activateForbiddenClient = activateForbiddenFactory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
            await LoginAsync(activateForbiddenClient, fixture.TenantId, seedPassword);
            using var activateForbidden = await PostRawAsync(activateForbiddenClient, $"/api/v1/tax/country-packs/{v2Id:D}/activate", new { effectiveFrom = "2027-01-01" });
            Assert.Equal(HttpStatusCode.Forbidden, activateForbidden.StatusCode);
            await RestorePermissionAsync(databaseAdmin, fixture.TenantId, fixture.AdministratorRoleId, "tax.country_pack.activate");
        }
        finally
        {
            if (databaseCreated)
            {
                await using var terminate = new NpgsqlCommand("select pg_terminate_backend(pid) from pg_stat_activity where datname = $1 and pid <> pg_backend_pid()", admin); terminate.Parameters.AddWithValue(databaseName); await terminate.ExecuteNonQueryAsync();
                await using var drop = new NpgsqlCommand($"drop database if exists \"{databaseName}\"", admin); await drop.ExecuteNonQueryAsync();
            }
        }
    }

    private static object ProductRow(int rowNumber, string reference, string scope, string from) => new
    {
        rowNumber, reference, taxCategoryCode = "GENERAL", schemeCode = "DEVELOPMENT", legalEntityId = (Guid?)null,
        countryCode = "EG", effectiveFrom = DateOnly.Parse(from, CultureInfo.InvariantCulture), effectiveTo = (DateOnly?)null, productScope = scope
    };

    private static PartyRowModel PartyRow(int rowNumber, string reference, string from, string registrationNumber) => new(rowNumber, reference, "PARTY-GENERAL", "DEVELOPMENT", null, "EG", DateOnly.Parse(from, CultureInfo.InvariantCulture), null, "EG", registrationNumber, null, false, "Tax pass 3A synthetic fixture");

    private sealed record PartyRowModel(int rowNumber, string reference, string taxCategoryCode, string schemeCode, Guid? legalEntityId, string countryCode, DateOnly effectiveFrom, DateOnly? effectiveTo, string registrationCountryCode, string registrationNumber, string? treatment, bool reverseChargeEligible, string evidenceReference);

    private sealed record Fixture(Guid TenantId, Guid AdministratorId, Guid AdministratorRoleId, Guid LegalEntityId, string TemplateCode, string VariantSku, string PartyReference, string TemplateCodeForRollback, string SecondTemplateCodeForRollback);
    private sealed record ProfileCounts(long ProductCount, long PartyCount);

    private static object PackRequest(string code, string version, string effectiveFrom, string sourceKind, string sourceReference, int vectorCount, string vectorStatus) => new
    {
        countryCode = "EG", packCode = code, packVersion = version, engineContractVersion = "tax_calculation_v1", effectiveFrom,
        effectiveTo = (string?)null, sourceKind, sourceAuthority = sourceKind == "TestOnly" ? "Internal Test Authority" : "", sourceTitle = sourceKind == "TestOnly" ? "Technical Fixture" : "", sourceReference,
        ruleSetReferences = new[] { "DEVELOPMENT" }, capabilities = new { vat = true, excise = false, withholding = false, taxGrouping = false, eInvoice = false }, goldenVectorCount = vectorCount, goldenVectorStatus = vectorStatus
    };

    private static async Task<Fixture> ReadFixtureAsync(string adminConnectionString, string databaseName)
    {
        await using var switchDatabase = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = databaseName }.ConnectionString);
        await switchDatabase.OpenAsync();
        const string sql = "select t.id,i.id,r.id,e.id,(select p.product_template_code from foundation_catalog.product_template p where p.tenant_id=t.id and p.product_template_code='SEED-PRD-000001'),(select v.sku from foundation_catalog.product_variant v join foundation_catalog.product_template p on p.tenant_id=v.tenant_id and p.id=v.product_template_id where v.tenant_id=t.id and p.product_template_code='SEED-PRD-000002' and v.is_default_variant),(select p.product_template_code from foundation_catalog.product_template p where p.tenant_id=t.id and p.product_template_code='SEED-PRD-000004'),(select p.product_template_code from foundation_catalog.product_template p where p.tenant_id=t.id and p.product_template_code='SEED-PRD-000005'),(select x.system_code || ':' || x.external_key from foundation_directory.external_reference x where x.tenant_id=t.id and x.external_key=upper(t.slug) || '-001' order by x.id limit 1) from platform.tenant t join platform.identity i on i.tenant_id=t.id and i.normalized_email='ADMIN@ATLAS.COM' join platform.membership m on m.tenant_id=t.id and m.identity_id=i.id join platform.role r on r.tenant_id=t.id and r.code='administrator' join platform.legal_entity e on e.tenant_id=t.id and e.code='SECONDARY' where t.slug='atlas-trading' limit 1";
        await using var command = new NpgsqlCommand(sql, switchDatabase);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "The seeded Atlas administrator, administrator role, Secondary legal entity, and products must exist.");
        return new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3), reader.GetString(4), reader.GetString(5), reader.GetString(8), reader.GetString(6), reader.GetString(7));
    }

    private static async Task<ProfileCounts> ReadProfileCountsAsync(NpgsqlConnection admin, Guid tenantId)
    {
        await using var command = new NpgsqlCommand("select (select count(*) from foundation_tax.product_tax_profile where tenant_id=$1 and legal_entity_id is not null),(select count(*) from foundation_tax.party_tax_profile where tenant_id=$1 and legal_entity_id is not null)", admin);
        command.Parameters.AddWithValue(tenantId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return new(reader.GetInt64(0), reader.GetInt64(1));
    }

    private static async Task RemovePermissionAsync(NpgsqlConnection admin, Guid tenant, Guid role, string permission)
    {
        await using var command = new NpgsqlCommand("delete from platform.role_permission where tenant_id=$1 and role_id=$2 and permission_code=$3", admin);
        command.Parameters.AddWithValue(tenant); command.Parameters.AddWithValue(role); command.Parameters.AddWithValue(permission); Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    private static async Task RestorePermissionAsync(NpgsqlConnection admin, Guid tenant, Guid role, string permission)
    {
        await using var command = new NpgsqlCommand("insert into platform.role_permission(tenant_id,role_id,permission_code) values($1,$2,$3) on conflict do nothing", admin);
        command.Parameters.AddWithValue(tenant); command.Parameters.AddWithValue(role); command.Parameters.AddWithValue(permission); await command.ExecuteNonQueryAsync();
    }

    private static async Task CorruptAcceptedRowCategoryAsync(NpgsqlConnection admin, Guid tenant, Guid batch, int rowNumber)
    {
        await using var command = new NpgsqlCommand("update foundation_tax.tax_import_row set row_data=jsonb_set(row_data,'{categoryId}',to_jsonb($4::text),true) where tenant_id=$1 and batch_id=$2 and row_number=$3", admin);
        command.Parameters.AddWithValue(tenant); command.Parameters.AddWithValue(batch); command.Parameters.AddWithValue(rowNumber); command.Parameters.AddWithValue(Guid.CreateVersion7().ToString("D"));
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    private static async Task<string> ReadBatchStatusAsync(NpgsqlConnection admin, Guid tenant, Guid batch)
    {
        await using var command = new NpgsqlCommand("select status from foundation_tax.tax_import_batch where tenant_id=$1 and id=$2", admin);
        command.Parameters.AddWithValue(tenant); command.Parameters.AddWithValue(batch); return (string)(await command.ExecuteScalarAsync() ?? string.Empty);
    }

    private static async Task<long> CountOutboxAsync(NpgsqlConnection admin, Guid tenant, Guid batch)
    {
        await using var command = new NpgsqlCommand("select count(*) from platform.outbox_message where tenant_id=$1 and causation_id=$2 and message_type='TaxProfilesImportedV1'", admin);
        command.Parameters.AddWithValue(tenant); command.Parameters.AddWithValue(batch); return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private static async Task<HttpResponseMessage> PostRawAsync(HttpClient client, string path, object body)
    {
        using var csrfResponse = await client.GetAsync("/api/v1/auth/csrf");
        var csrf = await csrfResponse.Content.ReadFromJsonAsync<JsonElement>();
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }

    private static async Task<string> ReadPackStatusAsync(NpgsqlConnection admin, Guid tenant, Guid id)
    {
        await using var command = new NpgsqlCommand("select status from foundation_tax.tax_pack_activation where tenant_id=$1 and id=$2", admin); command.Parameters.AddWithValue(tenant); command.Parameters.AddWithValue(id); return (string)(await command.ExecuteScalarAsync() ?? string.Empty);
    }

    private static async Task<string?> ResolvePackVersionAsync(NpgsqlConnection admin, Guid tenant, string country, DateOnly at)
    {
        await using var command = new NpgsqlCommand("select pack_version from foundation_tax.tax_pack_activation where tenant_id=$1 and country_code=$2 and status in ('Active','Superseded') and effective_from<=$3 and (effective_to is null or effective_to>$3) order by effective_from desc limit 1", admin); command.Parameters.AddWithValue(tenant); command.Parameters.AddWithValue(country); command.Parameters.AddWithValue(at); return await command.ExecuteScalarAsync() as string;
    }

    private static async Task<string> ReadSnapshotAsync(NpgsqlConnection admin, Guid tenant, Guid id)
    {
        await using var command = new NpgsqlCommand("select result::text from foundation_tax.tax_snapshot where tenant_id=$1 and id=$2", admin); command.Parameters.AddWithValue(tenant); command.Parameters.AddWithValue(id); return (string)(await command.ExecuteScalarAsync() ?? string.Empty);
    }

    private static async Task<long> CountAuditAsync(NpgsqlConnection admin, Guid tenant, string operation)
    {
        await using var command = new NpgsqlCommand("select count(*) from foundation_tax.tax_audit where tenant_id=$1 and entity_type='TaxCountryPack' and operation=$2", admin); command.Parameters.AddWithValue(tenant); command.Parameters.AddWithValue(operation); return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private static async Task<long> CountAuditForPackAsync(NpgsqlConnection admin, Guid tenant, Guid packId, string operation)
    {
        await using var command = new NpgsqlCommand("select count(*) from foundation_tax.tax_audit where tenant_id=$1 and entity_type='TaxCountryPack' and entity_id=$2 and operation=$3", admin); command.Parameters.AddWithValue(tenant); command.Parameters.AddWithValue(packId); command.Parameters.AddWithValue(operation); return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private static async Task<long> CountOutboxTypeAsync(NpgsqlConnection admin, Guid tenant, string messageType)
    {
        await using var command = new NpgsqlCommand("select count(*) from platform.outbox_message where tenant_id=$1 and message_type=$2", admin); command.Parameters.AddWithValue(tenant); command.Parameters.AddWithValue(messageType); return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private static async Task<long> CountOutboxForPackAsync(NpgsqlConnection admin, Guid tenant, Guid packId, string messageType)
    {
        await using var command = new NpgsqlCommand("select count(*) from platform.outbox_message where tenant_id=$1 and causation_id=$2 and message_type=$3", admin); command.Parameters.AddWithValue(tenant); command.Parameters.AddWithValue(packId); command.Parameters.AddWithValue(messageType); return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private static async Task LoginAsync(HttpClient client, Guid tenant, string password)
    {
        var response = await PostJsonAsync(client, "/api/v1/auth/login", new { email = "admin@atlas.com", password }, HttpStatusCode.OK);
        Assert.Equal("Authenticated", response.GetProperty("state").GetString());
        Assert.Equal(tenant, response.GetProperty("tenantId").GetGuid());
    }

    private static async Task<JsonElement> PostJsonAsync(HttpClient client, string path, object body, HttpStatusCode expected)
    {
        using var csrfResponse = await client.GetAsync("/api/v1/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
        var csrf = await csrfResponse.Content.ReadFromJsonAsync<JsonElement>();
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Equal(expected, response.StatusCode);
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    private static async Task<string> GetTextAsync(HttpClient client, string path, HttpStatusCode expected)
    {
        using var response = await client.GetAsync(path);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Equal(expected, response.StatusCode);
        return text;
    }

    private static async Task<(int ExitCode, string Output)> RunMigratorAsync(string repositoryRoot, string connectionString, string seedPassword, string runtimePassword)
    {
        var start = new ProcessStartInfo("dotnet") { WorkingDirectory = repositoryRoot, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(Path.Combine(repositoryRoot, "backend", "DbMigrator", "bin", "Debug", "net10.0", "DbMigrator.dll"));
        start.ArgumentList.Add("--seed"); start.ArgumentList.Add("development");
        start.Environment["TODIR_MIGRATOR_CONNECTION"] = connectionString;
        start.Environment["TODIR_REPOSITORY_ROOT"] = repositoryRoot;
        start.Environment["TODIR_RUNTIME_PASSWORD"] = runtimePassword;
        start.Environment["SEED_DEFAULT_PASSWORD"] = seedPassword;
        start.Environment["DOTNET_ENVIRONMENT"] = "Testing";
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Unable to start DbMigrator for the Tax PostgreSQL behavior gate.");
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync();
        return (process.ExitCode, $"{await output}{await error}");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "backend", "DbMigrator", "DbMigrator.csproj"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not find the repository root from the Tax integration test output.");
    }
}
