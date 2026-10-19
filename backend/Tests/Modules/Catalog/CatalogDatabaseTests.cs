using System.IO.Compression;
using System.Xml.Linq;
using Npgsql;
using Modules.Catalog.Services;
using Modules.Catalog.DTOs;
using Modules.Catalog.Entities;
using Core.Transactions;
using Shared.Primitives.Identifiers;
using Xunit;

namespace CatalogTests;

[Collection(CatalogDatabaseGroup.Name)]
public sealed class CatalogDatabaseTests
{
    private static readonly string[] ExpectedUomUsageFlags = ["Sales", "Purchase"];
    [Fact]
    public async Task RuntimeRoleCannotReadAnotherTenantCatalog()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_CATALOG_ADMIN_CONNECTION"); var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_CATALOG_RUNTIME_CONNECTION"); if (string.IsNullOrWhiteSpace(adminConnectionString) || string.IsNullOrWhiteSpace(runtimeConnectionString)) return;
        await ResetRuntimePasswordAsync(adminConnectionString); var tenantA = Guid.CreateVersion7(); var tenantB = Guid.CreateVersion7();
        await using (var admin = NpgsqlDataSource.Create(adminConnectionString)) await using (var connection = await admin.OpenConnectionAsync()) { await using var command = new NpgsqlCommand("insert into platform.tenant (id,slug,display_name,status,login_domain,normalized_login_domain,created_at,version) values ($1,$2,$3,'Active',$4,$4,now(),1),($5,$6,$7,'Active',$8,$8,now(),1)", connection); command.Parameters.AddWithValue(tenantA); command.Parameters.AddWithValue($"catalog-a-{tenantA:N}"); command.Parameters.AddWithValue("Catalog A"); command.Parameters.AddWithValue($"catalog-a-{tenantA:N}.local"); command.Parameters.AddWithValue(tenantB); command.Parameters.AddWithValue($"catalog-b-{tenantB:N}"); command.Parameters.AddWithValue("Catalog B"); command.Parameters.AddWithValue($"catalog-b-{tenantB:N}.local"); await command.ExecuteNonQueryAsync(); }
        try { await using var runtime = NpgsqlDataSource.Create(runtimeConnectionString); await using var connection = await runtime.OpenConnectionAsync(); await using var transaction = await connection.BeginTransactionAsync(); await using (var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction)) { context.Parameters.AddWithValue(tenantA.ToString("D")); await context.ExecuteNonQueryAsync(); } var id = Guid.CreateVersion7(); await using (var insert = new NpgsqlCommand("insert into foundation_catalog.brand (tenant_id,id,name,name_normalized,status,created_at,updated_at,version) values ($1,$2,'A','a','Active',now(),now(),1)", connection, transaction)) { insert.Parameters.AddWithValue(tenantA); insert.Parameters.AddWithValue(id); await insert.ExecuteNonQueryAsync(); } await using (var switchTenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,true)", connection, transaction)) { switchTenant.Parameters.AddWithValue(tenantB.ToString("D")); await switchTenant.ExecuteNonQueryAsync(); } await using var select = new NpgsqlCommand("select count(*) from foundation_catalog.brand", connection, transaction); Assert.Equal(0L, (long)(await select.ExecuteScalarAsync() ?? -1L)); await transaction.RollbackAsync(); }
        finally { await using var admin = NpgsqlDataSource.Create(adminConnectionString); await using var connection = await admin.OpenConnectionAsync(); foreach (var sql in new[] { "delete from foundation_catalog.brand where tenant_id in ($1,$2)", "delete from platform.tenant where id in ($1,$2)" }) { await using var cleanup = new NpgsqlCommand(sql, connection); cleanup.Parameters.AddWithValue(tenantA); cleanup.Parameters.AddWithValue(tenantB); await cleanup.ExecuteNonQueryAsync(); } }
    }

    [Fact]
    public async Task ProductServiceCreatesServiceWithDefaultVariantAndBaseUom()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_CATALOG_ADMIN_CONNECTION"); var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_CATALOG_RUNTIME_CONNECTION"); if (string.IsNullOrWhiteSpace(adminConnectionString) || string.IsNullOrWhiteSpace(runtimeConnectionString)) return;
        await ResetRuntimePasswordAsync(adminConnectionString); var tenant = Guid.CreateVersion7(); var actor = Guid.CreateVersion7(); var membership = Guid.CreateVersion7(); var uom = Guid.CreateVersion7(); var alternateUom = Guid.CreateVersion7();
        await using (var admin = NpgsqlDataSource.Create(adminConnectionString)) await using (var connection = await admin.OpenConnectionAsync()) { await using var command = new NpgsqlCommand("insert into platform.tenant (id,slug,display_name,status,login_domain,normalized_login_domain,created_at,version) values ($1,$2,$3,'Active',$4,$4,now(),1)", connection); command.Parameters.AddWithValue(tenant); command.Parameters.AddWithValue($"catalog-service-{tenant:N}"); command.Parameters.AddWithValue("Catalog Service"); command.Parameters.AddWithValue($"catalog-service-{tenant:N}.local"); await command.ExecuteNonQueryAsync(); }
        try
        {
            await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString); await using (var connection = await dataSource.OpenConnectionAsync()) { await using (var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection)) { context.Parameters.AddWithValue(tenant.ToString("D")); await context.ExecuteNonQueryAsync(); } await using var insert = new NpgsqlCommand("insert into foundation_catalog.uom (tenant_id,id,code,name_ar,name_en,dimension,allow_fraction,precision,status,created_at,updated_at,version) values ($1,$2,'HOUR','ساعة','Hour','Time',true,2,'Active',now(),now(),1),($1,$3,'DAY','يوم','Day','Time',true,2,'Active',now(),now(),1)", connection); insert.Parameters.AddWithValue(tenant); insert.Parameters.AddWithValue(uom); insert.Parameters.AddWithValue(alternateUom); await insert.ExecuteNonQueryAsync(); }
            var service = new ProductService(new Modules.Catalog.Repositories.ProductRepository(new NpgsqlTransactionCoordinator(dataSource), dataSource)); var created = await service.CreateAsync(new TenantId(tenant), actor, membership, new CreateProductRequest("استشارة", ProductNature.Service, "CONSULT-01", "استشارة", "Consulting", null, null, uom), Guid.CreateVersion7(), null, CancellationToken.None); Assert.Equal(ProductNature.Service, created.Nature); Assert.Equal("CONSULT-01", created.Sku); Assert.Equal("HOUR", created.BaseUomCode); Assert.NotEqual(Guid.Empty, created.DefaultVariantId); var details = new CatalogDetailService(new Modules.Catalog.Repositories.CatalogDetailRepository(new NpgsqlTransactionCoordinator(dataSource), dataSource)); var conversion = await details.AddUomAsync(new TenantId(tenant), created.Id, created.DefaultVariantId, new AddProductUomRequest(alternateUom, 8m, DateTimeOffset.UtcNow), CancellationToken.None); Assert.Equal(8m, conversion.FactorToBase); var catalogContract = new CatalogCapabilities(new Modules.Catalog.Repositories.CatalogCapabilityRepository(dataSource)); var canonicalVariant = await catalogContract.FindVariantAsync(new TenantId(tenant), created.DefaultVariantId, null, CancellationToken.None); Assert.NotNull(canonicalVariant); Assert.Equal(created.DefaultVariantId, canonicalVariant.ProductVariantId); Assert.Equal(created.Id, canonicalVariant.ProductTemplateId); var contractConversion = await catalogContract.ConvertAsync(new TenantId(tenant), created.DefaultVariantId, alternateUom, 3m, DateTimeOffset.UtcNow, null, CancellationToken.None); Assert.NotNull(contractConversion); Assert.Equal(24m, contractConversion.BaseQuantity); Assert.Equal("DAY", contractConversion.UnitCode); Assert.Null(await catalogContract.FindVariantAsync(new TenantId(Guid.CreateVersion7()), created.DefaultVariantId, null, CancellationToken.None)); var barcode = await service.AddBarcodeAsync(new TenantId(tenant), actor, membership, created.Id, null, null, new AddBarcodeRequest(null, BarcodeType.Ean13, "1234567890123", true), Guid.CreateVersion7(), CancellationToken.None); Assert.Equal(created.DefaultVariantId, barcode.VariantId); await service.AddBarcodeAsync(new TenantId(tenant), actor, membership, created.Id, null, null, new AddBarcodeRequest(alternateUom, BarcodeType.Ean13, "1234567890124", false), Guid.CreateVersion7(), CancellationToken.None); var unitBarcodeSnapshot = await details.ResolveAsync(new TenantId(tenant), "1234567890124", CancellationToken.None); Assert.Equal(alternateUom, unitBarcodeSnapshot.ResolvedUomId); Assert.Equal(8m, unitBarcodeSnapshot.ConversionFactor); await details.AddAliasAsync(new TenantId(tenant), created.Id, new AddAliasRequest(created.DefaultVariantId, "Consulting Alias", "Internal", "en"), CancellationToken.None); await details.AddTranslationAsync(new TenantId(tenant), created.Id, new AddTranslationRequest("fr", "Consultation Produit", null), CancellationToken.None); var snapshot = await details.ResolveAsync(new TenantId(tenant), "CONSULT-01", CancellationToken.None); Assert.Equal(created.DefaultVariantId, snapshot.VariantId); var barcodeSnapshot = await details.ResolveAsync(new TenantId(tenant), "1234567890123", CancellationToken.None); Assert.Equal(created.DefaultVariantId, barcodeSnapshot.VariantId); var arabicSearch = await service.ListAsync(new TenantId(tenant), "استشارة", null, null, null, null, null, 1, 25, CancellationToken.None); Assert.Contains(arabicSearch.Items, item => item.Id == created.Id); var englishSearch = await service.ListAsync(new TenantId(tenant), "Consulting", null, null, null, null, null, 1, 25, CancellationToken.None); Assert.Contains(englishSearch.Items, item => item.Id == created.Id); var search = await service.ListAsync(new TenantId(tenant), "1234567890123", null, null, null, null, null, 1, 25, CancellationToken.None); Assert.Contains(search.Items, item => item.Id == created.Id); var translatedSearch = await service.ListAsync(new TenantId(tenant), "Consultation Produit", null, null, null, null, null, 1, 25, CancellationToken.None); Assert.Contains(translatedSearch.Items, item => item.Id == created.Id); var readiness = await details.ReadinessAsync(new TenantId(tenant), created.Id, CancellationToken.None); Assert.True(readiness.Ready);
            var updated = await service.UpdateAsync(new TenantId(tenant), actor, membership, created.Id, null, new UpdateProductRequest("استشارة محدثة", "CONSULT-01", "استشارة محدثة", "Consulting Updated", null, null, created.Version), Guid.CreateVersion7(), null, CancellationToken.None);
            Assert.Equal("استشارة محدثة", updated.NameAr);
            Assert.Contains((await service.ListAsync(new TenantId(tenant), "Consulting Updated", null, null, null, null, null, 1, 25, CancellationToken.None)).Items, item => item.Id == created.Id);
        }
        finally { await using var admin = NpgsqlDataSource.Create(adminConnectionString); await using var connection = await admin.OpenConnectionAsync(); foreach (var sql in new[] { "delete from foundation_catalog.product_alias where tenant_id=$1", "delete from foundation_catalog.product_translation where tenant_id=$1", "delete from foundation_catalog.product_barcode where tenant_id=$1", "delete from foundation_catalog.catalog_search_projection where tenant_id=$1", "delete from foundation_catalog.product_uom where tenant_id=$1", "delete from foundation_catalog.product_visibility where tenant_id=$1", "delete from foundation_catalog.product_variant where tenant_id=$1", "delete from foundation_catalog.product_template where tenant_id=$1", "delete from foundation_catalog.uom where tenant_id=$1", "delete from platform.outbox_message where tenant_id=$1", "delete from platform.audit_entry where tenant_id=$1", "delete from platform.tenant where id=$1" }) { await using var cleanup = new NpgsqlCommand(sql, connection); cleanup.Parameters.AddWithValue(tenant); await cleanup.ExecuteNonQueryAsync(); } }
    }

    [Fact]
    public async Task VariantAttributeCombinationsAreValidatedAndUniqueInPostgres()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_CATALOG_ADMIN_CONNECTION");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_CATALOG_RUNTIME_CONNECTION");
        if (string.IsNullOrWhiteSpace(adminConnectionString) || string.IsNullOrWhiteSpace(runtimeConnectionString)) return;

        await ResetRuntimePasswordAsync(adminConnectionString);
        var tenant = Guid.CreateVersion7();
        var uom = Guid.CreateVersion7();
        await using (var admin = NpgsqlDataSource.Create(adminConnectionString))
        await using (var connection = await admin.OpenConnectionAsync())
        {
            await using var command = new NpgsqlCommand("insert into platform.tenant (id,slug,display_name,status,login_domain,normalized_login_domain,created_at,version) values ($1,$2,$3,'Active',$4,$4,now(),1)", connection);
            command.Parameters.AddWithValue(tenant);
            command.Parameters.AddWithValue($"catalog-attributes-{tenant:N}");
            command.Parameters.AddWithValue("Catalog Attribute Test");
            command.Parameters.AddWithValue($"catalog-attributes-{tenant:N}.local");
            await command.ExecuteNonQueryAsync();
        }

        try
        {
            await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
            await using (var connection = await dataSource.OpenConnectionAsync())
            {
                await using var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection);
                context.Parameters.AddWithValue(tenant.ToString("D"));
                await context.ExecuteNonQueryAsync();
                await using var insertUom = new NpgsqlCommand("insert into foundation_catalog.uom (tenant_id,id,code,name_ar,name_en,dimension,allow_fraction,precision,status,created_at,updated_at,version) values ($1,$2,'EA','قطعة','Each','Count',false,0,'Active',now(),now(),1)", connection);
                insertUom.Parameters.AddWithValue(tenant);
                insertUom.Parameters.AddWithValue(uom);
                await insertUom.ExecuteNonQueryAsync();
            }

            var tenantId = new TenantId(tenant);
            var productService = new ProductService(new Modules.Catalog.Repositories.ProductRepository(new NpgsqlTransactionCoordinator(dataSource), dataSource));
            var product = await productService.CreateAsync(tenantId, Guid.CreateVersion7(), Guid.CreateVersion7(), new CreateProductRequest("قميص تجريبي", ProductNature.Goods, "SHIRT-BASE", "قميص تجريبي", "Test shirt", null, null, uom), Guid.CreateVersion7(), null, CancellationToken.None);
            var duplicateSku = await Assert.ThrowsAsync<CatalogException>(() => productService.CreateAsync(tenantId, Guid.CreateVersion7(), Guid.CreateVersion7(), new CreateProductRequest("منتج مكرر", ProductNature.Goods, "shirt-base", "منتج مكرر", "Duplicate product", null, null, uom), Guid.CreateVersion7(), null, CancellationToken.None));
            Assert.Equal("catalog.sku_duplicate", duplicateSku.Code);
            async Task<ProductDetails?> CreateRaceCandidateAsync()
            {
                try { return await productService.CreateAsync(tenantId, Guid.CreateVersion7(), Guid.CreateVersion7(), new CreateProductRequest("نتيجة سباق", ProductNature.Goods, "RACE-SKU", "نتيجة سباق", "Race winner", null, null, uom), Guid.CreateVersion7(), null, CancellationToken.None); }
                catch (CatalogException exception) when (exception.Code == "catalog.sku_duplicate") { return null; }
            }
            var raceResults = await Task.WhenAll(CreateRaceCandidateAsync(), CreateRaceCandidateAsync());
            var raceWinner = Assert.Single(raceResults, result => result is not null)!;
            await using (var connection = await dataSource.OpenConnectionAsync())
            {
                await using var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); context.Parameters.AddWithValue(tenant.ToString("D")); await context.ExecuteNonQueryAsync();
                await using var count = new NpgsqlCommand("select count(*) from foundation_catalog.product_template p join foundation_catalog.product_variant v on v.tenant_id=p.tenant_id and v.product_template_id=p.id where p.tenant_id=$1 and v.normalized_sku='RACE-SKU'", connection); count.Parameters.AddWithValue(tenant); Assert.Equal(1L, (long)(await count.ExecuteScalarAsync() ?? -1L));
                await using var events = new NpgsqlCommand("select count(*) from platform.outbox_message where tenant_id=$1 and message_type='CatalogProductCreatedV1' and payload->>'templateId'=$2", connection); events.Parameters.AddWithValue(tenant); events.Parameters.AddWithValue(raceWinner.Id.ToString("D")); Assert.Equal(1L, (long)(await events.ExecuteScalarAsync() ?? -1L));
            }
            var entityA = Guid.CreateVersion7();
            var entityB = Guid.CreateVersion7();
            await productService.SetVisibilityAsync(tenantId, Guid.CreateVersion7(), Guid.CreateVersion7(), product.Id, new ProductVisibilityRequest("LegalEntity", entityA), Guid.CreateVersion7(), null, CancellationToken.None);
            Assert.NotNull(await productService.GetAsync(tenantId, product.Id, entityA, CancellationToken.None));
            Assert.Null(await productService.GetAsync(tenantId, product.Id, entityB, CancellationToken.None));
            var visibleList = await productService.ListAsync(tenantId, null, null, null, null, null, entityA, 1, 25, CancellationToken.None);
            var hiddenList = await productService.ListAsync(tenantId, null, null, null, null, null, entityB, 1, 25, CancellationToken.None);
            Assert.Contains(visibleList.Items, item => item.Id == product.Id);
            Assert.DoesNotContain(hiddenList.Items, item => item.Id == product.Id);
            var details = new CatalogDetailService(new Modules.Catalog.Repositories.CatalogDetailRepository(new NpgsqlTransactionCoordinator(dataSource), dataSource));
            var hiddenReadiness = await Assert.ThrowsAsync<CatalogException>(() => details.ReadinessAsync(tenantId, product.Id, CancellationToken.None, entityB));
            Assert.Equal("catalog.product_not_found", hiddenReadiness.Code);
            var hiddenResolution = await Assert.ThrowsAsync<CatalogException>(() => details.ResolveAsync(tenantId, "SHIRT-BASE", CancellationToken.None, entityB));
            Assert.Equal("catalog.identifier_not_found", hiddenResolution.Code);
            await Assert.ThrowsAsync<CatalogException>(() => details.AddUomAsync(tenantId, product.Id, product.DefaultVariantId, new AddProductUomRequest(uom, 2m, DateTimeOffset.UtcNow), CancellationToken.None, entityB));
            var attribute = await details.CreateAttributeAsync(tenantId, new CreateAttributeRequest("COLOR", "اللون", "Color", true), CancellationToken.None);
            var navy = await details.CreateAttributeValueAsync(tenantId, attribute.Id, new CreateAttributeValueRequest("NAVY", "كحلي", "Navy"), CancellationToken.None);
            var white = await details.CreateAttributeValueAsync(tenantId, attribute.Id, new CreateAttributeValueRequest("WHITE", "أبيض", "White"), CancellationToken.None);
            await details.AssignTemplateAttributeAsync(tenantId, product.Id, attribute.Id, true, 0, CancellationToken.None, entityA);

            var navyVariant = await details.CreateVariantAsync(tenantId, product.Id, new CreateVariantRequest("SHIRT-NAVY", "كحلي", new Dictionary<Guid, Guid> { [attribute.Id] = navy.Id }), CancellationToken.None, entityA);
            var whiteVariant = await details.CreateVariantAsync(tenantId, product.Id, new CreateVariantRequest("SHIRT-WHITE", "أبيض", new Dictionary<Guid, Guid> { [attribute.Id] = white.Id }), CancellationToken.None, entityA);
            Assert.Equal("كحلي", navyVariant.DisplaySuffix);
            Assert.NotEqual(navyVariant.Id, whiteVariant.Id);
            await Assert.ThrowsAsync<CatalogException>(() => details.CreateVariantAsync(tenantId, product.Id, new CreateVariantRequest("SHIRT-NAVY-2", "كحلي", new Dictionary<Guid, Guid> { [attribute.Id] = navy.Id }), CancellationToken.None, entityA));
            await Assert.ThrowsAsync<CatalogException>(() => details.CreateVariantAsync(tenantId, product.Id, new CreateVariantRequest("SHIRT-MISSING", null), CancellationToken.None, entityA));
            Assert.Single(await details.ListTemplateAttributesAsync(tenantId, product.Id, CancellationToken.None, entityA));
        }
        finally
        {
            await using var admin = NpgsqlDataSource.Create(adminConnectionString);
            await using var connection = await admin.OpenConnectionAsync();
            foreach (var sql in new[]
            {
                "delete from foundation_catalog.product_variant_attribute_value where tenant_id=$1",
                "delete from foundation_catalog.product_template_attribute where tenant_id=$1",
                "delete from foundation_catalog.product_attribute_value where tenant_id=$1",
                "delete from foundation_catalog.product_attribute where tenant_id=$1",
                "delete from foundation_catalog.product_barcode where tenant_id=$1",
                "delete from foundation_catalog.catalog_search_projection where tenant_id=$1",
                "delete from foundation_catalog.product_uom where tenant_id=$1",
                "delete from foundation_catalog.product_visibility where tenant_id=$1",
                "delete from foundation_catalog.product_variant where tenant_id=$1",
                "delete from foundation_catalog.product_template where tenant_id=$1",
                "delete from foundation_catalog.uom where tenant_id=$1",
                "delete from platform.outbox_message where tenant_id=$1",
                "delete from platform.audit_entry where tenant_id=$1",
                "delete from platform.tenant where id=$1"
            })
            {
                await using var cleanup = new NpgsqlCommand(sql, connection);
                cleanup.Parameters.AddWithValue(tenant);
                await cleanup.ExecuteNonQueryAsync();
            }
        }
    }

    [Fact]
    public async Task CatalogImportPreviewIsNonMutatingAndCommitReconcilesGroupedVariants()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_CATALOG_ADMIN_CONNECTION");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_CATALOG_RUNTIME_CONNECTION");
        if (string.IsNullOrWhiteSpace(adminConnectionString) || string.IsNullOrWhiteSpace(runtimeConnectionString)) return;
        await ResetRuntimePasswordAsync(adminConnectionString);
        var tenant = Guid.CreateVersion7(); var actor = Guid.CreateVersion7(); var membership = Guid.CreateVersion7(); var uom = Guid.CreateVersion7();
        await using (var admin = NpgsqlDataSource.Create(adminConnectionString))
        await using (var connection = await admin.OpenConnectionAsync())
        {
            await using var command = new NpgsqlCommand("insert into platform.tenant (id,slug,display_name,status,login_domain,normalized_login_domain,created_at,version) values ($1,$2,$3,'Active',$4,$4,now(),1)", connection);
            command.Parameters.AddWithValue(tenant); command.Parameters.AddWithValue($"catalog-import-{tenant:N}"); command.Parameters.AddWithValue("Catalog Import"); command.Parameters.AddWithValue($"catalog-import-{tenant:N}.local"); await command.ExecuteNonQueryAsync();
        }
        try
        {
            await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
            var boxUom = Guid.CreateVersion7();
            await using (var connection = await dataSource.OpenConnectionAsync())
            {
                await using (var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection)) { context.Parameters.AddWithValue(tenant.ToString("D")); await context.ExecuteNonQueryAsync(); }
                await using var insert = new NpgsqlCommand("insert into foundation_catalog.uom (tenant_id,id,code,name_ar,name_en,dimension,allow_fraction,precision,status,created_at,updated_at,version) values ($1,$2,'EA','قطعة','Each','Count',false,0,'Active',now(),now(),1)", connection); insert.Parameters.AddWithValue(tenant); insert.Parameters.AddWithValue(uom); await insert.ExecuteNonQueryAsync();
                await using var insertBox = new NpgsqlCommand("insert into foundation_catalog.uom (tenant_id,id,code,name_ar,name_en,dimension,allow_fraction,precision,status,created_at,updated_at,version) values ($1,$2,'BOX','علبة','Box','Count',false,0,'Active',now(),now(),1)", connection); insertBox.Parameters.AddWithValue(tenant); insertBox.Parameters.AddWithValue(boxUom); await insertBox.ExecuteNonQueryAsync();
            }
            var import = new CatalogImportService(new Modules.Catalog.Repositories.CatalogImportRepository(new NpgsqlTransactionCoordinator(dataSource), dataSource));
            var details = new CatalogDetailService(new Modules.Catalog.Repositories.CatalogDetailRepository(new NpgsqlTransactionCoordinator(dataSource), dataSource));
            var color = await details.CreateAttributeAsync(new TenantId(tenant), new CreateAttributeRequest("COLOR", "اللون", "Color", true), CancellationToken.None);
            var red = await details.CreateAttributeValueAsync(new TenantId(tenant), color.Id, new CreateAttributeValueRequest("RED", "أحمر", "Red"), CancellationToken.None);
            var blue = await details.CreateAttributeValueAsync(new TenantId(tenant), color.Id, new CreateAttributeValueRequest("BLUE", "أزرق", "Blue"), CancellationToken.None);
            var boxBarcode = "CAT-BOX-001";
            var request = new CatalogImportPreviewRequest("catalog-demo.json", [
                new CatalogImportRowRequest("shirt", "قميص تجريبي", ProductNature.Goods, "SHIRT-BLUE", "قميص تجريبي", "Demo shirt", null, null, "EA", "CAT-BLUE-001", "legacy-blue", [new CatalogImportUomRequest("BOX", 6m, DateTimeOffset.UtcNow.AddMinutes(-1), null, boxBarcode, ["Sales", "Purchase"], 3)], new Dictionary<string, string> { ["COLOR"] = "BLUE" }),
                new CatalogImportRowRequest("shirt", "قميص تجريبي", ProductNature.Goods, "SHIRT-RED", "قميص تجريبي", "Demo shirt", null, null, "EA", "CAT-RED-001", "legacy-red", null, new Dictionary<string, string> { ["COLOR"] = "RED" }),
                new CatalogImportRowRequest(null, "وحدة مجهولة", ProductNature.Goods, "BAD-UOM", null, null, null, null, "MISSING", null, null)
            ]);
            var preview = await import.PreviewAsync(new TenantId(tenant), actor, membership, request, CancellationToken.None);
            Assert.Equal(2, preview.CreateCount); Assert.Equal(0, preview.UpdateCount); Assert.Equal(0, preview.ReviewCount); Assert.Equal(1, preview.ErrorCount);
            await using (var connection = await dataSource.OpenConnectionAsync())
            {
                await using (var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection)) { context.Parameters.AddWithValue(tenant.ToString("D")); await context.ExecuteNonQueryAsync(); }
                await using var count = new NpgsqlCommand("select count(*) from foundation_catalog.product_template where tenant_id=$1", connection); count.Parameters.AddWithValue(tenant); Assert.Equal(0L, (long)(await count.ExecuteScalarAsync() ?? -1L));
            }
            var queued = await import.RequestCommitAsync(new TenantId(tenant), preview.BatchId, CancellationToken.None);
            Assert.Equal("Queued", queued.Status);
            await using (var outbox = NpgsqlDataSource.Create(runtimeConnectionString))
            await using (var connection = await outbox.OpenConnectionAsync())
            {
                await using (var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection)) { context.Parameters.AddWithValue(tenant.ToString("D")); await context.ExecuteNonQueryAsync(); }
                await using var eventQuery = new NpgsqlCommand("select count(*) from platform.outbox_message where tenant_id=$1 and message_type='CatalogImportRequestedV1' and causation_id=$2", connection); eventQuery.Parameters.AddWithValue(tenant); eventQuery.Parameters.AddWithValue(preview.BatchId); Assert.Equal(1L, (long)(await eventQuery.ExecuteScalarAsync() ?? -1L));
                await using var productEvents = new NpgsqlCommand("select count(*) from platform.outbox_message where tenant_id=$1 and message_type='CatalogProductCreatedV1' and causation_id=$2", connection); productEvents.Parameters.AddWithValue(tenant); productEvents.Parameters.AddWithValue(preview.BatchId); Assert.Equal(0L, (long)(await productEvents.ExecuteScalarAsync() ?? -1L));
            }
            var committed = await import.CommitAsync(new TenantId(tenant), preview.BatchId, CancellationToken.None);
            Assert.Equal("Committed", committed.Status); Assert.Equal(2, committed.Created); Assert.Equal(0, committed.Updated);
            await using (var outbox = NpgsqlDataSource.Create(runtimeConnectionString))
            await using (var connection = await outbox.OpenConnectionAsync())
            {
                await using (var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection)) { context.Parameters.AddWithValue(tenant.ToString("D")); await context.ExecuteNonQueryAsync(); }
                await using var productEvents = new NpgsqlCommand("select count(*) from platform.outbox_message where tenant_id=$1 and message_type='CatalogProductCreatedV1' and causation_id=$2", connection); productEvents.Parameters.AddWithValue(tenant); productEvents.Parameters.AddWithValue(preview.BatchId); Assert.Equal(2L, (long)(await productEvents.ExecuteScalarAsync() ?? -1L));
            }
            var replayedCommit = await import.CommitAsync(new TenantId(tenant), preview.BatchId, CancellationToken.None);
            Assert.Equal(2, replayedCommit.Created); Assert.Equal(0, replayedCommit.Updated);
            var updatePreview = await import.PreviewAsync(new TenantId(tenant), actor, membership, new CatalogImportPreviewRequest("update.json", [new CatalogImportRowRequest(null, "قميص معدل", ProductNature.Goods, "SHIRT-BLUE", "قميص معدل", "Updated demo shirt", null, null, "EA", "CAT-BLUE-001", "legacy-blue")]), CancellationToken.None);
            Assert.Equal(1, updatePreview.UpdateCount); Assert.Equal(0, updatePreview.ErrorCount); Assert.Equal(0, updatePreview.ReviewCount);
            var updated = await import.CommitAsync(new TenantId(tenant), updatePreview.BatchId, CancellationToken.None);
            Assert.Equal(0, updated.Created); Assert.Equal(1, updated.Updated);
            await using (var connection = await dataSource.OpenConnectionAsync())
            {
                await using (var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection)) { context.Parameters.AddWithValue(tenant.ToString("D")); await context.ExecuteNonQueryAsync(); }
                await using var count = new NpgsqlCommand("select count(*) from foundation_catalog.product_template where tenant_id=$1 and display_name='قميص معدل'", connection); count.Parameters.AddWithValue(tenant); Assert.Equal(1L, (long)(await count.ExecuteScalarAsync() ?? -1L));
                await using var variantCount = new NpgsqlCommand("select count(*) from foundation_catalog.product_variant v join foundation_catalog.product_template p on p.tenant_id=v.tenant_id and p.id=v.product_template_id where p.tenant_id=$1 and p.display_name='قميص معدل'", connection); variantCount.Parameters.AddWithValue(tenant); Assert.Equal(2L, (long)(await variantCount.ExecuteScalarAsync() ?? -1L));
                await using var attributeValues = new NpgsqlCommand("select count(*) from foundation_catalog.product_variant_attribute_value x join foundation_catalog.product_variant v on v.tenant_id=x.tenant_id and v.id=x.variant_id join foundation_catalog.product_template p on p.tenant_id=v.tenant_id and p.id=v.product_template_id where x.tenant_id=$1 and p.display_name='قميص معدل' and x.attribute_id=$2 and x.attribute_value_id=any($3)", connection); attributeValues.Parameters.AddWithValue(tenant); attributeValues.Parameters.AddWithValue(color.Id); attributeValues.Parameters.AddWithValue(new[] { red.Id, blue.Id }); Assert.Equal(2L, (long)(await attributeValues.ExecuteScalarAsync() ?? -1L));
                await using var importedUom = new NpgsqlCommand("select pu.factor_to_base,b.value,b.uom_id,pu.usage_flags,pu.rounding_precision from foundation_catalog.product_uom pu join foundation_catalog.product_variant v on v.tenant_id=pu.tenant_id and v.id=pu.variant_id join foundation_catalog.product_template p on p.tenant_id=v.tenant_id and p.id=v.product_template_id join foundation_catalog.product_barcode b on b.tenant_id=pu.tenant_id and b.variant_id=pu.variant_id and b.uom_id=pu.uom_id where pu.tenant_id=$1 and p.display_name='قميص معدل' and pu.uom_id=$2 and not pu.is_base", connection); importedUom.Parameters.AddWithValue(tenant); importedUom.Parameters.AddWithValue(boxUom); await using (var uomReader = await importedUom.ExecuteReaderAsync()) { Assert.True(await uomReader.ReadAsync()); Assert.Equal(6m, uomReader.GetDecimal(0)); Assert.Equal(boxBarcode, uomReader.GetString(1)); Assert.Equal(boxUom, uomReader.GetGuid(2)); Assert.Equal(ExpectedUomUsageFlags, uomReader.GetFieldValue<string[]>(3)); Assert.Equal((short)3, uomReader.GetInt16(4)); }
                await using var audit = new NpgsqlCommand("select before_json->>'displayName',after_json->>'displayName',actor_id,membership_id from platform.audit_entry where tenant_id=$1 and action='catalog_products_import_updated' order by occurred_at desc limit 1", connection); audit.Parameters.AddWithValue(tenant); await using var auditReader = await audit.ExecuteReaderAsync(); Assert.True(await auditReader.ReadAsync()); Assert.Equal("قميص تجريبي", auditReader.GetString(0)); Assert.Equal("قميص معدل", auditReader.GetString(1)); Assert.Equal(actor, auditReader.GetGuid(2)); Assert.Equal(membership, auditReader.GetGuid(3));
            }
            using var csv = new MemoryStream(); await import.WriteExportCsvAsync(new TenantId(tenant), null, csv, CancellationToken.None); var export = System.Text.Encoding.UTF8.GetString(csv.ToArray()); Assert.Contains("قميص معدل", export); Assert.Contains("AdditionalUomsJson", export); Assert.Contains("AttributesJson", export); Assert.Contains("AliasesJson", export); Assert.Contains("ReferencesJson", export); Assert.Contains("TranslationsJson", export); Assert.Contains("uomCode", export); Assert.Contains("BOX", export); Assert.Contains("usageFlags", export); Assert.Contains("Sales", export); Assert.Contains("COLOR", export); Assert.Contains("BLUE", export);
            var csvPreview = await import.PreviewAsync(new TenantId(tenant), actor, membership, new CatalogImportPreviewRequest("mapping.csv", null, "SKU,Product Name,Type,Base Unit\r\nCSV-NEW,\"منتج, مستورد\",Goods,EA", null, new Dictionary<string, string> { ["sku"] = "SKU", ["name"] = "Product Name", ["type"] = "Type", ["baseUom"] = "Base Unit" }), CancellationToken.None);
            Assert.Equal(1, csvPreview.CreateCount); Assert.Equal(0, csvPreview.ErrorCount);
            var csvCommit = await import.CommitAsync(new TenantId(tenant), csvPreview.BatchId, CancellationToken.None); Assert.Equal(1, csvCommit.Created);
            var jsonCsv = "SKU,Product Name,Type,Base Unit,Additional UOMs,Attributes\r\nJSON-UOM-NEW,منتج تحويلات,Goods,EA,\"[{\"\"uomCode\"\":\"\"BOX\"\",\"\"factorToBase\"\":6,\"\"effectiveFrom\"\":\"\"2026-10-14T00:00:00Z\"\",\"\"usageFlags\"\":[\"\"Sales\"\",\"\"Purchase\"\"],\"\"roundingPrecision\"\":3}]\",\"{\"\"COLOR\"\":\"\"BLUE\"\"}\"";
            var jsonCsvPreview = await import.PreviewAsync(new TenantId(tenant), actor, membership, new CatalogImportPreviewRequest("catalog-roundtrip.csv", null, jsonCsv, null, new Dictionary<string, string> { ["sku"] = "SKU", ["name"] = "Product Name", ["type"] = "Type", ["baseUom"] = "Base Unit", ["additionalUomsJson"] = "Additional UOMs", ["attributesJson"] = "Attributes" }), CancellationToken.None);
            Assert.Equal(1, jsonCsvPreview.CreateCount); Assert.Equal(0, jsonCsvPreview.ErrorCount);
            var jsonCsvCommit = await import.CommitAsync(new TenantId(tenant), jsonCsvPreview.BatchId, CancellationToken.None); Assert.Equal(1, jsonCsvCommit.Created);
            var excelPreview = await import.PreviewAsync(new TenantId(tenant), actor, membership, new CatalogImportPreviewRequest("mapping.xlsx", null, null, CreateXlsx(["SKU", "Product Name", "Type", "Base Unit"], ["XLSX-NEW", "منتج إكسل", "Goods", "EA"]), new Dictionary<string, string> { ["sku"] = "SKU", ["name"] = "Product Name", ["type"] = "Type", ["baseUom"] = "Base Unit" }), CancellationToken.None);
            Assert.Equal(1, excelPreview.CreateCount); Assert.Equal(0, excelPreview.ErrorCount);
            var excelCommit = await import.CommitAsync(new TenantId(tenant), excelPreview.BatchId, CancellationToken.None); Assert.Equal(1, excelCommit.Created);
            var conflicts = await import.PreviewAsync(new TenantId(tenant), actor, membership, new CatalogImportPreviewRequest("conflicts.json", [
                new CatalogImportRowRequest(null, "تصنيف غير معروف", ProductNature.Goods, "CAT-UNKNOWN-CATEGORY", null, null, "تصنيف مجهول", null, "EA", null, null),
                new CatalogImportRowRequest(null, "تحويل غير صالح", ProductNature.Goods, "CAT-INVALID-UOM", null, null, null, null, "EA", null, null, [new CatalogImportUomRequest("BOX", 0m, DateTimeOffset.UtcNow, null)]),
                new CatalogImportRowRequest(null, "تكرار SKU أول", ProductNature.Goods, "DUP-SKU", null, null, null, null, "EA", "DUP-BARCODE", null),
                new CatalogImportRowRequest(null, "تكرار SKU ثاني", ProductNature.Goods, "DUP-SKU", null, null, null, null, "EA", "DUP-BARCODE", null),
                new CatalogImportRowRequest(null, "تعارض معرفات", ProductNature.Goods, "SHIRT-BLUE", null, null, null, null, "EA", "CAT-RED-001", null)
            ]), CancellationToken.None);
            Assert.Equal(2, conflicts.ErrorCount); Assert.Equal(3, conflicts.ReviewCount);
            Assert.Contains(conflicts.Rows, row => row.Issues.Contains("unknown_category"));
            Assert.Contains(conflicts.Rows, row => row.Issues.Contains("invalid_conversion"));
            Assert.Contains(conflicts.Rows, row => row.Issues.Contains("identifier_match_conflict"));
            var conflictSummary = await import.GetSummaryAsync(new TenantId(tenant), conflicts.BatchId, CancellationToken.None);
            Assert.Equal(3, conflictSummary.SkippedCount); Assert.Equal(2, conflictSummary.DuplicateSkuCount); Assert.Equal(2, conflictSummary.DuplicateBarcodeCount); Assert.Equal(1, conflictSummary.UomIssueCount); Assert.Equal(1, conflictSummary.UnknownCategoryCount);
            var catalog = new Modules.Catalog.Services.CatalogCapabilities(new Modules.Catalog.Repositories.CatalogCapabilityRepository(dataSource));
            var search = await catalog.SearchAsync(new TenantId(tenant), "قميص معدل", null, null, 1, 10, CancellationToken.None); Assert.Equal(2, search.TotalCount);
            var variant = search.Items[0];
            Assert.NotNull(await catalog.GetProductSummaryAsync(new TenantId(tenant), variant.ProductTemplateId, null, CancellationToken.None));
            Assert.NotNull(await catalog.FindVariantAsync(new TenantId(tenant), variant.ProductVariantId, null, CancellationToken.None));
            Assert.NotNull(await catalog.ResolveVariantAsync(new TenantId(tenant), variant.Sku!, DateTimeOffset.UtcNow, null, CancellationToken.None));
            var allowedUoms = await catalog.GetAllowedUomsAsync(new TenantId(tenant), variant.ProductVariantId, DateTimeOffset.UtcNow, null, CancellationToken.None); Assert.NotEmpty(allowedUoms);
            Assert.NotNull(await catalog.ConvertAsync(new TenantId(tenant), variant.ProductVariantId, allowedUoms[0].UnitId, 2m, DateTimeOffset.UtcNow, null, CancellationToken.None));
            var fractional = await catalog.ValidateAsync(new TenantId(tenant), variant.ProductVariantId, allowedUoms[0].UnitId, 1.5m, DateTimeOffset.UtcNow, null, CancellationToken.None); Assert.False(fractional.QuantityValid); Assert.Equal("catalog.fraction_not_allowed", fractional.FailureCode);
            Assert.Null(await catalog.ConvertAsync(new TenantId(tenant), variant.ProductVariantId, allowedUoms[0].UnitId, 1.5m, DateTimeOffset.UtcNow, null, CancellationToken.None));
            Assert.Empty((await catalog.SearchAsync(new TenantId(Guid.CreateVersion7()), "قميص معدل", null, null, 1, 10, CancellationToken.None)).Items);
            var anotherTenant = new TenantId(Guid.CreateVersion7());
            await Assert.ThrowsAsync<KeyNotFoundException>(() => import.GetSummaryAsync(anotherTenant, preview.BatchId, CancellationToken.None));
        }
        finally
        {
            await using var admin = NpgsqlDataSource.Create(adminConnectionString); await using var connection = await admin.OpenConnectionAsync();
            foreach (var sql in new[] { "delete from foundation_catalog.catalog_import_row where tenant_id=$1", "delete from foundation_catalog.catalog_import_batch where tenant_id=$1", "delete from foundation_catalog.product_reference where tenant_id=$1", "delete from foundation_catalog.product_variant_attribute_value where tenant_id=$1", "delete from foundation_catalog.product_template_attribute where tenant_id=$1", "delete from foundation_catalog.product_attribute_value where tenant_id=$1", "delete from foundation_catalog.product_attribute where tenant_id=$1", "delete from foundation_catalog.product_barcode where tenant_id=$1", "delete from foundation_catalog.catalog_search_projection where tenant_id=$1", "delete from foundation_catalog.product_visibility where tenant_id=$1", "delete from foundation_catalog.product_uom where tenant_id=$1", "delete from foundation_catalog.product_variant where tenant_id=$1", "delete from foundation_catalog.product_template where tenant_id=$1", "delete from foundation_catalog.uom where tenant_id=$1", "delete from platform.outbox_message where tenant_id=$1", "delete from platform.audit_entry where tenant_id=$1", "delete from platform.tenant where id=$1" })
            { await using var cleanup = new NpgsqlCommand(sql, connection); cleanup.Parameters.AddWithValue(tenant); await cleanup.ExecuteNonQueryAsync(); }
        }
    }

    [Fact]
    public async Task CatalogImportRehearsesMaximumBatchWithBoundedReconciliationAndExport()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_CATALOG_ADMIN_CONNECTION");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_CATALOG_RUNTIME_CONNECTION");
        if (string.IsNullOrWhiteSpace(adminConnectionString) || string.IsNullOrWhiteSpace(runtimeConnectionString)) return;
        await ResetRuntimePasswordAsync(adminConnectionString);
        var tenant = Guid.CreateVersion7(); var actor = Guid.CreateVersion7(); var membership = Guid.CreateVersion7(); var uom = Guid.CreateVersion7();
        await using (var admin = NpgsqlDataSource.Create(adminConnectionString))
        await using (var connection = await admin.OpenConnectionAsync())
        {
            await using var tenantCommand = new NpgsqlCommand("insert into platform.tenant (id,slug,display_name,status,login_domain,normalized_login_domain,created_at,version) values ($1,$2,$3,'Active',$4,$4,now(),1)", connection);
            tenantCommand.Parameters.AddWithValue(tenant); tenantCommand.Parameters.AddWithValue($"catalog-import-load-{tenant:N}"); tenantCommand.Parameters.AddWithValue("Catalog Import Load"); tenantCommand.Parameters.AddWithValue($"catalog-import-load-{tenant:N}.local"); await tenantCommand.ExecuteNonQueryAsync();
        }
        try
        {
            await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
            await using (var connection = await dataSource.OpenConnectionAsync())
            {
                await using var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); context.Parameters.AddWithValue(tenant.ToString("D")); await context.ExecuteNonQueryAsync();
                await using var unit = new NpgsqlCommand("insert into foundation_catalog.uom (tenant_id,id,code,name_ar,name_en,dimension,allow_fraction,precision,status,created_at,updated_at,version) values ($1,$2,'EA','قطعة','Each','Count',false,0,'Active',now(),now(),1)", connection); unit.Parameters.AddWithValue(tenant); unit.Parameters.AddWithValue(uom); await unit.ExecuteNonQueryAsync();
            }
            var csv = new System.Text.StringBuilder("SKU,Product Name,Type,Base Unit\r\n");
            for (var index = 1; index <= 5000; index++) csv.Append("LOAD-").Append(index.ToString("D5", System.Globalization.CultureInfo.InvariantCulture)).Append(",منتج استيراد ").Append(index.ToString("D5", System.Globalization.CultureInfo.InvariantCulture)).AppendLine(",Goods,EA");
            var import = new CatalogImportService(new Modules.Catalog.Repositories.CatalogImportRepository(new NpgsqlTransactionCoordinator(dataSource), dataSource));
            var preview = await import.PreviewAsync(new TenantId(tenant), actor, membership, new CatalogImportPreviewRequest("catalog-load.csv", null, csv.ToString(), null, new Dictionary<string, string> { ["sku"] = "SKU", ["name"] = "Product Name", ["type"] = "Type", ["baseUom"] = "Base Unit" }), CancellationToken.None);
            Assert.Equal(5000, preview.TotalRows); Assert.Equal(5000, preview.CreateCount); Assert.Equal(0, preview.ErrorCount); Assert.Equal("Previewed", preview.Status);
            await using (var connection = await dataSource.OpenConnectionAsync())
            {
                await using var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); context.Parameters.AddWithValue(tenant.ToString("D")); await context.ExecuteNonQueryAsync();
                await using var count = new NpgsqlCommand("select count(*) from foundation_catalog.product_template where tenant_id=$1", connection); count.Parameters.AddWithValue(tenant); Assert.Equal(0L, (long)(await count.ExecuteScalarAsync() ?? -1L));
            }
            var committed = await import.CommitAsync(new TenantId(tenant), preview.BatchId, CancellationToken.None);
            Assert.Equal("Committed", committed.Status); Assert.Equal(5000, committed.Created); Assert.Equal(0, committed.Updated); Assert.Equal(0, committed.ErrorCount); Assert.Equal(0, committed.ReviewCount);
            var reconciled = await import.GetSummaryAsync(new TenantId(tenant), preview.BatchId, CancellationToken.None);
            Assert.Equal(5000, reconciled.Created); Assert.Equal(5000, reconciled.TotalRows); Assert.Equal(200, reconciled.Rows.Count);
            using var export = new MemoryStream(); await import.WriteExportCsvAsync(new TenantId(tenant), null, export, CancellationToken.None);
            Assert.True(export.Length > 100_000);
            var exportedCsv = System.Text.Encoding.UTF8.GetString(export.ToArray()); Assert.Contains("LOAD-00001", exportedCsv); Assert.Contains("LOAD-05000", exportedCsv);
        }
        finally
        {
            await using var admin = NpgsqlDataSource.Create(adminConnectionString); await using var connection = await admin.OpenConnectionAsync();
            foreach (var sql in new[] { "delete from foundation_catalog.catalog_import_row where tenant_id=$1", "delete from foundation_catalog.catalog_import_batch where tenant_id=$1", "delete from foundation_catalog.product_reference where tenant_id=$1", "delete from foundation_catalog.product_variant_attribute_value where tenant_id=$1", "delete from foundation_catalog.product_template_attribute where tenant_id=$1", "delete from foundation_catalog.product_attribute_value where tenant_id=$1", "delete from foundation_catalog.product_attribute where tenant_id=$1", "delete from foundation_catalog.product_barcode where tenant_id=$1", "delete from foundation_catalog.product_alias where tenant_id=$1", "delete from foundation_catalog.product_translation where tenant_id=$1", "delete from foundation_catalog.catalog_search_projection where tenant_id=$1", "delete from foundation_catalog.product_uom where tenant_id=$1", "delete from foundation_catalog.product_visibility where tenant_id=$1", "delete from foundation_catalog.product_variant where tenant_id=$1", "delete from foundation_catalog.product_template where tenant_id=$1", "delete from foundation_catalog.uom where tenant_id=$1", "delete from platform.outbox_message where tenant_id=$1", "delete from platform.audit_entry where tenant_id=$1", "delete from platform.tenant where id=$1" })
            { await using var cleanup = new NpgsqlCommand(sql, connection); cleanup.Parameters.AddWithValue(tenant); await cleanup.ExecuteNonQueryAsync(); }
        }
    }

    private static async Task ResetRuntimePasswordAsync(string adminConnectionString) { await using var admin = NpgsqlDataSource.Create(adminConnectionString); await using var connection = await admin.OpenConnectionAsync(); await using var command = new NpgsqlCommand("alter role erp_app password 'todir-app-dev'", connection); await command.ExecuteNonQueryAsync(); }

    private static string CreateXlsx(params string[][] rows)
    {
        XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main"; XNamespace officeRel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"; XNamespace packageRel = "http://schemas.openxmlformats.org/package/2006/relationships";
        using var output = new MemoryStream(); using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string path, XDocument document) { var entry = archive.CreateEntry(path); using var stream = entry.Open(); document.Save(stream); }
            Add("xl/workbook.xml", new XDocument(new XElement(main + "workbook", new XAttribute(XNamespace.Xmlns + "r", officeRel), new XElement(main + "sheets", new XElement(main + "sheet", new XAttribute("name", "Products"), new XAttribute("sheetId", "1"), new XAttribute(officeRel + "id", "rId1"))))));
            Add("xl/_rels/workbook.xml.rels", new XDocument(new XElement(packageRel + "Relationships", new XElement(packageRel + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"), new XAttribute("Target", "worksheets/sheet1.xml")))));
            var sheetRows = rows.Select((values, rowIndex) => new XElement(main + "row", new XAttribute("r", rowIndex + 1), values.Select((value, columnIndex) => new XElement(main + "c", new XAttribute("r", $"{(char)('A' + columnIndex)}{rowIndex + 1}"), new XAttribute("t", "inlineStr"), new XElement(main + "is", new XElement(main + "t", value))))));
            Add("xl/worksheets/sheet1.xml", new XDocument(new XElement(main + "worksheet", new XElement(main + "sheetData", sheetRows))));
        }
        return Convert.ToBase64String(output.ToArray());
    }
}
