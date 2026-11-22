using Npgsql;
using Modules.Catalog.DTOs;
using Modules.Catalog.Entities;
using Modules.Catalog.Repositories;
using Modules.Catalog.Services;
using Core.Transactions;
using Shared.Primitives.Identifiers;
using Xunit;

namespace CatalogTests;

[Collection(CatalogDatabaseGroup.Name)]
public sealed class CatalogBatchResolutionTests
{
    private static (string AdminConnection, string RuntimeConnection) GetRequiredConnections()
    {
        var db = Environment.GetEnvironmentVariable("TODIR_ACCEPTANCE_DB") ?? "todir_mfg_acceptance";
        var runtimePassword = Environment.GetEnvironmentVariable("TODIR_DB_PASSWORD")
            ?? Environment.GetEnvironmentVariable("ERP_DEV_PASSWORD")
            ?? "todir-app-dev";
        var adminPassword = Environment.GetEnvironmentVariable("TODIR_ADMIN_PASSWORD")
            ?? "postgres";

        var admin = Environment.GetEnvironmentVariable("TODIR_CATALOG_ADMIN_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_ADMIN_CONNECTION")
            ?? $"Host=localhost;Port=15432;Database={db};Username=postgres;Password={adminPassword}";
        var runtime = Environment.GetEnvironmentVariable("TODIR_CATALOG_RUNTIME_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? $"Host=localhost;Port=15432;Database={db};Username=erp_app;Password={runtimePassword}";

        try
        {
            using var connection = new NpgsqlConnection(runtime);
            connection.Open();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"PostgreSQL database is unavailable for CatalogBatchResolutionTests at '{runtime}'. Ensure PostgreSQL is running on port 15432: {ex.Message}");
        }

        return (admin, runtime);
    }

    [Fact]
    public async Task ResolveBatchThrowsWhenExceedingMaxBatchSize()
    {
        var (_, runtimeConnectionString) = GetRequiredConnections();

        await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
        var repo = new CatalogDetailRepository(new NpgsqlTransactionCoordinator(dataSource), dataSource);

        var largeBatch = Enumerable.Range(0, 501).Select(_ => Guid.CreateVersion7().ToString("D")).ToList();
        var ex = await Assert.ThrowsAsync<CatalogException>(() =>
            repo.ResolveBatchAsync(new TenantId(Guid.CreateVersion7()), largeBatch, CancellationToken.None));

        Assert.Equal("catalog.batch_size_exceeded", ex.Code);
    }

    [Fact]
    public async Task ResolveBatchReturnsEmptyForEmptyInput()
    {
        var (_, runtimeConnectionString) = GetRequiredConnections();

        await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
        var repo = new CatalogDetailRepository(new NpgsqlTransactionCoordinator(dataSource), dataSource);

        var result = await repo.ResolveBatchAsync(new TenantId(Guid.CreateVersion7()), [], CancellationToken.None);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ResolveBatchHandlesScaleDeduplicationAndProductsBeyondPage2()
    {
        var (adminConnectionString, runtimeConnectionString) = GetRequiredConnections();

        var tenantId = Guid.CreateVersion7();
        var uomId = Guid.CreateVersion7();

        await using (var admin = NpgsqlDataSource.Create(adminConnectionString))
        await using (var connection = await admin.OpenConnectionAsync())
        {
            await using var command = new NpgsqlCommand("insert into platform.tenant (id,slug,display_name,status,login_domain,normalized_login_domain,created_at,version) values ($1,$2,$3,'Active',$4,$4,now(),1)", connection);
            command.Parameters.AddWithValue(tenantId);
            command.Parameters.AddWithValue($"batch-scale-{tenantId:N}");
            command.Parameters.AddWithValue("Batch Scale Tenant");
            command.Parameters.AddWithValue($"batch-scale-{tenantId:N}.local");
            await command.ExecuteNonQueryAsync();
        }

        try
        {
            await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
            await using (var connection = await dataSource.OpenConnectionAsync())
            {
                await using var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection);
                context.Parameters.AddWithValue(tenantId.ToString("D"));
                await context.ExecuteNonQueryAsync();

                await using var insertUom = new NpgsqlCommand("insert into foundation_catalog.uom (tenant_id,id,code,name_ar,name_en,dimension,allow_fraction,precision,status,created_at,updated_at,version) values ($1,$2,'EA','قطعة','Piece','Count',false,0,'Active',now(),now(),1)", connection);
                insertUom.Parameters.AddWithValue(tenantId);
                insertUom.Parameters.AddWithValue(uomId);
                await insertUom.ExecuteNonQueryAsync();
            }

            var service = new ProductService(new Modules.Catalog.Repositories.ProductRepository(new NpgsqlTransactionCoordinator(dataSource), dataSource));
            var productIds = new List<Guid>();
            var defaultVariantIds = new List<Guid>();

            // Create 30 products (well beyond 1 standard page of 20 or 25)
            for (var i = 0; i < 30; i++)
            {
                var created = await service.CreateAsync(new TenantId(tenantId), Guid.CreateVersion7(), Guid.CreateVersion7(),
                    new CreateProductRequest($"Scale Product {i}", ProductNature.Goods, $"SKU-SCALE-{i:D3}", $"صنف تجريبي {i}", $"Scale Product {i}", null, null, uomId),
                    Guid.CreateVersion7(), null, CancellationToken.None);
                productIds.Add(created.Id);
                defaultVariantIds.Add(created.DefaultVariantId);
            }

            // Create 1 product with a secondary (non-default) variant
            var multiVariantProduct = await service.CreateAsync(new TenantId(tenantId), Guid.CreateVersion7(), Guid.CreateVersion7(),
                new CreateProductRequest("Multi Variant Product", ProductNature.Goods, "SKU-MULTI-001", "صنف متعدد المتغيرات", "Multi Variant Product", null, null, uomId),
                Guid.CreateVersion7(), null, CancellationToken.None);

            var detailRepo = new CatalogDetailRepository(new NpgsqlTransactionCoordinator(dataSource), dataSource);
            var detailService = new CatalogDetailService(detailRepo);
            var nonDefaultVariant = await detailService.CreateVariantAsync(new TenantId(tenantId), multiVariantProduct.Id, new CreateVariantRequest("SKU-MULTI-002", "Secondary"), CancellationToken.None);
            var nonDefaultVariantId = nonDefaultVariant.Id;

            // Construct large batch with:
            // 1. All default variants (30 items)
            // 2. Non-default variant (1 item)
            // 3. Product template ID (1 item)
            // 4. Duplicate identifiers
            // 5. 200+ synthetic missing identifiers
            var batchInput = new List<string>();
            foreach (var vid in defaultVariantIds)
            {
                batchInput.Add(vid.ToString("D"));
                batchInput.Add(vid.ToString("D")); // duplicate
            }
            batchInput.Add(nonDefaultVariantId.ToString("D"));
            batchInput.Add(nonDefaultVariantId.ToString("D")); // duplicate
            batchInput.Add(multiVariantProduct.Id.ToString("D")); // template ID

            // Add 180 missing UUIDs to scale past 200 items
            for (var i = 0; i < 180; i++)
            {
                batchInput.Add(Guid.CreateVersion7().ToString("D"));
            }

            Assert.True(batchInput.Count >= 240);

            var resolved = await detailRepo.ResolveBatchAsync(new TenantId(tenantId), batchInput, CancellationToken.None);

            // Expecting 31 unique resolved variants (30 default variants + 1 non-default variant + template matches default variant)
            Assert.True(resolved.Count >= 31);
            Assert.Contains(resolved, r => r.VariantId == nonDefaultVariantId && r.Sku == "SKU-MULTI-002");
            Assert.Contains(resolved, r => r.VariantId == defaultVariantIds[29]); // Product beyond page 2
            Assert.DoesNotContain(resolved, r => r.DisplayName == null);
        }
        finally
        {
            await using var admin = NpgsqlDataSource.Create(adminConnectionString);
            await using var connection = await admin.OpenConnectionAsync();
            foreach (var sql in new[] {
                "delete from foundation_catalog.catalog_search_projection where tenant_id=$1",
                "delete from foundation_catalog.product_visibility where tenant_id=$1",
                "delete from foundation_catalog.product_uom where tenant_id=$1",
                "delete from foundation_catalog.product_variant where tenant_id=$1",
                "delete from foundation_catalog.product_template where tenant_id=$1",
                "delete from foundation_catalog.uom where tenant_id=$1",
                "delete from platform.outbox_message where tenant_id=$1",
                "delete from platform.audit_entry where tenant_id=$1",
                "delete from platform.tenant where id=$1"
            })
            {
                await using var cleanup = new NpgsqlCommand(sql, connection);
                cleanup.Parameters.AddWithValue(tenantId);
                await cleanup.ExecuteNonQueryAsync();
            }
        }
    }

    [Fact]
    public async Task ResolveBatchTenantIsolationAndInactiveFiltering()
    {
        var (adminConnectionString, runtimeConnectionString) = GetRequiredConnections();

        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        var uomId = Guid.CreateVersion7();

        await using (var admin = NpgsqlDataSource.Create(adminConnectionString))
        await using (var connection = await admin.OpenConnectionAsync())
        {
            await using var command = new NpgsqlCommand(@"insert into platform.tenant (id,slug,display_name,status,login_domain,normalized_login_domain,created_at,version)
                values ($1,$2,$3,'Active',$4,$4,now(),1), ($5,$6,$7,'Active',$8,$8,now(),1)", connection);
            command.Parameters.AddWithValue(tenantA);
            command.Parameters.AddWithValue($"iso-a-{tenantA:N}");
            command.Parameters.AddWithValue("Tenant A");
            command.Parameters.AddWithValue($"iso-a-{tenantA:N}.local");
            command.Parameters.AddWithValue(tenantB);
            command.Parameters.AddWithValue($"iso-b-{tenantB:N}");
            command.Parameters.AddWithValue("Tenant B");
            command.Parameters.AddWithValue($"iso-b-{tenantB:N}.local");
            await command.ExecuteNonQueryAsync();
        }

        try
        {
            await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
            await using (var connection = await dataSource.OpenConnectionAsync())
            {
                await using var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection);
                context.Parameters.AddWithValue(tenantA.ToString("D"));
                await context.ExecuteNonQueryAsync();

                await using var insertUom = new NpgsqlCommand("insert into foundation_catalog.uom (tenant_id,id,code,name_ar,name_en,dimension,allow_fraction,precision,status,created_at,updated_at,version) values ($1,$2,'EA','قطعة','Piece','Count',false,0,'Active',now(),now(),1)", connection);
                insertUom.Parameters.AddWithValue(tenantA);
                insertUom.Parameters.AddWithValue(uomId);
                await insertUom.ExecuteNonQueryAsync();
            }

            var service = new ProductService(new Modules.Catalog.Repositories.ProductRepository(new NpgsqlTransactionCoordinator(dataSource), dataSource));
            var activeProduct = await service.CreateAsync(new TenantId(tenantA), Guid.CreateVersion7(), Guid.CreateVersion7(),
                new CreateProductRequest("Active Product", ProductNature.Goods, "SKU-ACTIVE-01", "صنف نشط", "Active Product", null, null, uomId),
                Guid.CreateVersion7(), null, CancellationToken.None);

            var inactiveProduct = await service.CreateAsync(new TenantId(tenantA), Guid.CreateVersion7(), Guid.CreateVersion7(),
                new CreateProductRequest("Inactive Product", ProductNature.Goods, "SKU-INACTIVE-01", "صنف غير نشط", "Inactive Product", null, null, uomId),
                Guid.CreateVersion7(), null, CancellationToken.None);

            // Mark inactiveProduct as Inactive in projection
            await using (var connection = await dataSource.OpenConnectionAsync())
            {
                await using var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection);
                context.Parameters.AddWithValue(tenantA.ToString("D"));
                await context.ExecuteNonQueryAsync();

                await using var updateStatus = new NpgsqlCommand("update foundation_catalog.catalog_search_projection set status='Inactive' where tenant_id=$1 and template_id=$2", connection);
                updateStatus.Parameters.AddWithValue(tenantA);
                updateStatus.Parameters.AddWithValue(inactiveProduct.Id);
                await updateStatus.ExecuteNonQueryAsync();
            }

            var detailRepo = new CatalogDetailRepository(new NpgsqlTransactionCoordinator(dataSource), dataSource);

            var queryBatch = new List<string>
            {
                activeProduct.DefaultVariantId.ToString("D"),
                inactiveProduct.DefaultVariantId.ToString("D")
            };

            // 1. Resolve for Tenant A: Active product must be returned, Inactive product must be excluded
            var resolvedA = await detailRepo.ResolveBatchAsync(new TenantId(tenantA), queryBatch, CancellationToken.None);
            Assert.Single(resolvedA);
            Assert.Equal(activeProduct.DefaultVariantId, resolvedA[0].VariantId);
            Assert.DoesNotContain(resolvedA, r => r.VariantId == inactiveProduct.DefaultVariantId);

            // 2. Tenant isolation: Resolve for Tenant B with identical IDs -> Must return 0
            var resolvedB = await detailRepo.ResolveBatchAsync(new TenantId(tenantB), queryBatch, CancellationToken.None);
            Assert.Empty(resolvedB);
        }
        finally
        {
            await using var admin = NpgsqlDataSource.Create(adminConnectionString);
            await using var connection = await admin.OpenConnectionAsync();
            foreach (var sql in new[] {
                "delete from foundation_catalog.catalog_search_projection where tenant_id in ($1,$2)",
                "delete from foundation_catalog.product_visibility where tenant_id in ($1,$2)",
                "delete from foundation_catalog.product_uom where tenant_id in ($1,$2)",
                "delete from foundation_catalog.product_variant where tenant_id in ($1,$2)",
                "delete from foundation_catalog.product_template where tenant_id in ($1,$2)",
                "delete from foundation_catalog.uom where tenant_id in ($1,$2)",
                "delete from platform.outbox_message where tenant_id in ($1,$2)",
                "delete from platform.audit_entry where tenant_id in ($1,$2)",
                "delete from platform.tenant where id in ($1,$2)"
            })
            {
                await using var cleanup = new NpgsqlCommand(sql, connection);
                cleanup.Parameters.AddWithValue(tenantA);
                cleanup.Parameters.AddWithValue(tenantB);
                await cleanup.ExecuteNonQueryAsync();
            }
        }
    }

    [Fact]
    public async Task ResolveBatchRespectsLegalEntityVisibilityScope()
    {
        var (adminConnectionString, runtimeConnectionString) = GetRequiredConnections();

        var tenantId = Guid.CreateVersion7();
        var entityA = Guid.CreateVersion7();
        var entityB = Guid.CreateVersion7();
        var uomId = Guid.CreateVersion7();

        await using (var admin = NpgsqlDataSource.Create(adminConnectionString))
        await using (var connection = await admin.OpenConnectionAsync())
        {
            await using var command = new NpgsqlCommand("insert into platform.tenant (id,slug,display_name,status,login_domain,normalized_login_domain,created_at,version) values ($1,$2,$3,'Active',$4,$4,now(),1)", connection);
            command.Parameters.AddWithValue(tenantId);
            command.Parameters.AddWithValue($"vis-{tenantId:N}");
            command.Parameters.AddWithValue("Visibility Tenant");
            command.Parameters.AddWithValue($"vis-{tenantId:N}.local");
            await command.ExecuteNonQueryAsync();
        }

        try
        {
            await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
            await using (var connection = await dataSource.OpenConnectionAsync())
            {
                await using var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection);
                context.Parameters.AddWithValue(tenantId.ToString("D"));
                await context.ExecuteNonQueryAsync();

                await using var insertUom = new NpgsqlCommand("insert into foundation_catalog.uom (tenant_id,id,code,name_ar,name_en,dimension,allow_fraction,precision,status,created_at,updated_at,version) values ($1,$2,'EA','قطعة','Piece','Count',false,0,'Active',now(),now(),1)", connection);
                insertUom.Parameters.AddWithValue(tenantId);
                insertUom.Parameters.AddWithValue(uomId);
                await insertUom.ExecuteNonQueryAsync();
            }

            var service = new ProductService(new Modules.Catalog.Repositories.ProductRepository(new NpgsqlTransactionCoordinator(dataSource), dataSource));
            var product = await service.CreateAsync(new TenantId(tenantId), Guid.CreateVersion7(), Guid.CreateVersion7(),
                new CreateProductRequest("Scoped Product", ProductNature.Goods, "SKU-SCOPED-01", "صنف مقيد بالجهة", "Scoped Product", null, null, uomId),
                Guid.CreateVersion7(), null, CancellationToken.None);

            // Change product visibility to LegalEntity scope for entityA only
            await using (var connection = await dataSource.OpenConnectionAsync())
            {
                await using var context = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection);
                context.Parameters.AddWithValue(tenantId.ToString("D"));
                await context.ExecuteNonQueryAsync();

                await using (var deleteVisibility = new NpgsqlCommand("delete from foundation_catalog.product_visibility where tenant_id=$1 and template_id=$2", connection))
                {
                    deleteVisibility.Parameters.AddWithValue(tenantId);
                    deleteVisibility.Parameters.AddWithValue(product.Id);
                    await deleteVisibility.ExecuteNonQueryAsync();
                }

                await using (var insertVisibility = new NpgsqlCommand(@"insert into foundation_catalog.product_visibility (tenant_id, template_id, scope_type, legal_entity_id, visibility_id)
                    values ($1, $2, 'LegalEntity', $3, $4)", connection))
                {
                    insertVisibility.Parameters.AddWithValue(tenantId);
                    insertVisibility.Parameters.AddWithValue(product.Id);
                    insertVisibility.Parameters.AddWithValue(entityA);
                    insertVisibility.Parameters.AddWithValue(Guid.CreateVersion7());
                    await insertVisibility.ExecuteNonQueryAsync();
                }
            }

            var detailRepo = new CatalogDetailRepository(new NpgsqlTransactionCoordinator(dataSource), dataSource);
            var query = new List<string> { product.DefaultVariantId.ToString("D") };

            // 1. Resolve specifying matching entityA -> Must be resolved
            var resolvedForA = await detailRepo.ResolveBatchAsync(new TenantId(tenantId), query, CancellationToken.None, legalEntityId: entityA);
            Assert.Single(resolvedForA);
            Assert.Equal(product.DefaultVariantId, resolvedForA[0].VariantId);

            // 2. Resolve specifying non-matching entityB -> Must NOT be returned
            var resolvedForB = await detailRepo.ResolveBatchAsync(new TenantId(tenantId), query, CancellationToken.None, legalEntityId: entityB);
            Assert.Empty(resolvedForB);
        }
        finally
        {
            await using var admin = NpgsqlDataSource.Create(adminConnectionString);
            await using var connection = await admin.OpenConnectionAsync();
            foreach (var sql in new[] {
                "delete from foundation_catalog.catalog_search_projection where tenant_id=$1",
                "delete from foundation_catalog.product_visibility where tenant_id=$1",
                "delete from foundation_catalog.product_uom where tenant_id=$1",
                "delete from foundation_catalog.product_variant where tenant_id=$1",
                "delete from foundation_catalog.product_template where tenant_id=$1",
                "delete from foundation_catalog.uom where tenant_id=$1",
                "delete from platform.outbox_message where tenant_id=$1",
                "delete from platform.audit_entry where tenant_id=$1",
                "delete from platform.tenant where id=$1"
            })
            {
                await using var cleanup = new NpgsqlCommand(sql, connection);
                cleanup.Parameters.AddWithValue(tenantId);
                await cleanup.ExecuteNonQueryAsync();
            }
        }
    }
}
