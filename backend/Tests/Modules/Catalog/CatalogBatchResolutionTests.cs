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
    [Fact]
    public async Task ResolveBatchThrowsWhenExceedingMaxBatchSize()
    {
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_CATALOG_RUNTIME_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION");
        if (string.IsNullOrWhiteSpace(runtimeConnectionString)) return;

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
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_CATALOG_RUNTIME_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION");
        if (string.IsNullOrWhiteSpace(runtimeConnectionString)) return;

        await using var dataSource = NpgsqlDataSource.Create(runtimeConnectionString);
        var repo = new CatalogDetailRepository(new NpgsqlTransactionCoordinator(dataSource), dataSource);

        var result = await repo.ResolveBatchAsync(new TenantId(Guid.CreateVersion7()), [], CancellationToken.None);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ResolveBatchHandlesBatchScaleDeduplicationAndCrossTenantIsolation()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_CATALOG_ADMIN_CONNECTION");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_CATALOG_RUNTIME_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION");
        if (string.IsNullOrWhiteSpace(adminConnectionString) || string.IsNullOrWhiteSpace(runtimeConnectionString)) return;

        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        var uom = Guid.CreateVersion7();

        await using (var admin = NpgsqlDataSource.Create(adminConnectionString))
        await using (var connection = await admin.OpenConnectionAsync())
        {
            await using var command = new NpgsqlCommand("insert into platform.tenant (id,slug,display_name,status,login_domain,normalized_login_domain,created_at,version) values ($1,$2,$3,'Active',$4,$4,now(),1),($5,$6,$7,'Active',$8,$8,now(),1)", connection);
            command.Parameters.AddWithValue(tenantA);
            command.Parameters.AddWithValue($"batch-a-{tenantA:N}");
            command.Parameters.AddWithValue("Batch A");
            command.Parameters.AddWithValue($"batch-a-{tenantA:N}.local");
            command.Parameters.AddWithValue(tenantB);
            command.Parameters.AddWithValue($"batch-b-{tenantB:N}");
            command.Parameters.AddWithValue("Batch B");
            command.Parameters.AddWithValue($"batch-b-{tenantB:N}.local");
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
                await using var insert = new NpgsqlCommand("insert into foundation_catalog.uom (tenant_id,id,code,name_ar,name_en,dimension,allow_fraction,precision,status,created_at,updated_at,version) values ($1,$2,'EA','قطعة','Piece','Count',false,0,'Active',now(),now(),1)", connection);
                insert.Parameters.AddWithValue(tenantA);
                insert.Parameters.AddWithValue(uom);
                await insert.ExecuteNonQueryAsync();
            }

            var service = new ProductService(new Modules.Catalog.Repositories.ProductRepository(new NpgsqlTransactionCoordinator(dataSource), dataSource));
            var productIds = new List<Guid>();
            var variantIds = new List<Guid>();

            // Create 5 products for Tenant A
            for (var i = 0; i < 5; i++)
            {
                var created = await service.CreateAsync(new TenantId(tenantA), Guid.CreateVersion7(), Guid.CreateVersion7(),
                    new CreateProductRequest($"Batch Prod {i}", ProductNature.Goods, $"SKU-BATCH-{i:D3}", $"صنف تجريبي {i}", $"Batch Prod {i}", null, null, uom),
                    Guid.CreateVersion7(), null, CancellationToken.None);
                productIds.Add(created.Id);
                variantIds.Add(created.DefaultVariantId);
            }

            var detailRepo = new CatalogDetailRepository(new NpgsqlTransactionCoordinator(dataSource), dataSource);

            // 1. Resolve for Tenant A with duplicate identifiers and > 200 items (mix of real + synthetic)
            var inputBatch = new List<string>();
            // Add real variant IDs multiple times (duplicate test)
            foreach (var vid in variantIds)
            {
                inputBatch.Add(vid.ToString("D"));
                inputBatch.Add(vid.ToString("D")); // duplicate
            }
            // Add synthetic missing IDs to scale to 250 items (> 200 items scale test)
            for (var i = 0; i < 240; i++)
            {
                inputBatch.Add(Guid.CreateVersion7().ToString("D"));
            }

            Assert.True(inputBatch.Count >= 250);

            var resolvedA = await detailRepo.ResolveBatchAsync(new TenantId(tenantA), inputBatch, CancellationToken.None);
            Assert.Equal(5, resolvedA.Count); // Exactly 5 unique matching items, duplicates de-duplicated
            foreach (var item in resolvedA)
            {
                Assert.Contains(item.VariantId, variantIds);
                Assert.NotNull(item.DisplayName);
                Assert.NotNull(item.Sku);
            }

            // 2. Cross-tenant isolation: Tenant B resolves the same batch -> Must return 0
            var resolvedB = await detailRepo.ResolveBatchAsync(new TenantId(tenantB), inputBatch, CancellationToken.None);
            Assert.Empty(resolvedB);
        }
        finally
        {
            await using var admin = NpgsqlDataSource.Create(adminConnectionString);
            await using var connection = await admin.OpenConnectionAsync();
            foreach (var sql in new[] {
                "delete from foundation_catalog.catalog_search_projection where tenant_id in ($1,$2)",
                "delete from foundation_catalog.product_visibility where tenant_id in ($1,$2)",
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
}
