using System.Globalization;
using System.Diagnostics;
using System.Text.Json;
using Core.Messaging;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Quality.DTOs;
using Modules.Quality.Entities;
using Modules.Quality.Repositories;
using Modules.Inventory.DTOs;
using Modules.Inventory.Entities;
using Modules.Inventory.Services;
using Modules.Purchasing.DTOs;
using Modules.Purchasing.Services;
using Npgsql;
using RabbitMQ.Client;
using Shared.Primitives.Identifiers;
using Shared.Contracts.Quality;
using Todir.Inventory.Contracts;
using Xunit;

namespace KernelIntegrationTests;

[Collection("Quality acceptance")]
public sealed class QualityBackendAcceptanceTests
{
    private static readonly TenantId AtlasTenant = new(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));
    private static readonly Guid AtlasEntity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");

    [Fact(DisplayName = "QA-01 receiving hold inspection pass releases the authoritative inventory status")]
    public async Task Qa01ReceivingHoldInspectionPassReleasesInventory()
    {
        var connection = RequireConnection();
        using var factory = CreateFactory(connection);
        var quality = factory.Services.GetRequiredService<QualityRepository>();
        var stock = await ReadStockAsync(connection, factory.Services.GetRequiredService<InventoryService>());
        var before = await StockQuantityAsync(connection, stock.ProductVariantId, stock.WarehouseId, stock.LocationId, "Available");
        var supplier = (Guid)(await ScalarAsync(connection, "select supplier_party_id from business_quality.quality_plan_applicability where tenant_id=$1 and product_variant_id=$2 and supplier_party_id is not null limit 1", AtlasTenant.Value, stock.ProductVariantId))!;
        var receipt = await quality.ResolvePurchaseReceiptAsync(AtlasTenant.Value, Guid.CreateVersion7(), AtlasEntity, Guid.CreateVersion7(), supplier, $"qa-01-receipt:{Guid.CreateVersion7():D}", [new QualityReceiptLine(Guid.CreateVersion7(), stock.ProductVariantId, 1m, null, null)], CancellationToken.None);
        Assert.True(receipt.QualityRequired);
        var inspectionId = Assert.Single(receipt.InspectionIds);
        var inspection = await CompleteInspectionAsync(quality, connection, inspectionId, 10m);
        var hold = await quality.CreateRestrictionAsync(AtlasTenant, Guid.CreateVersion7(), new("QualityHold", "QA01", stock.ProductVariantId, stock.WarehouseId, stock.LocationId, Quantity: 1m, UnitOfMeasureId: stock.UnitId, InspectionId: inspection.Id), AtlasEntity, CancellationToken.None);
        Assert.Equal("Confirmed", hold.OwnerExecutionStatus);
        Assert.Equal("QualityHold", await StockStatusAsync(connection, stock, "QualityHold"));
        var released = await quality.ReleaseRestrictionAsync(AtlasTenant, Guid.CreateVersion7(), hold.Id, new("QA-01 passed", 1m, stock.UnitId), CancellationToken.None);
        Assert.Equal("Released", released.Status);
        Assert.Equal("Available", await StockStatusAsync(connection, stock, "Available"));
        Assert.Equal(before, await StockQuantityAsync(connection, stock.ProductVariantId, stock.WarehouseId, stock.LocationId, "Available"));
    }

    [Fact(DisplayName = "QA-02 failed inspection NCR disposition executes the actual owner release")]
    public async Task Qa02FailedInspectionNcrDispositionExecutesOwnerRelease()
    {
        var connection = RequireConnection();
        using var factory = CreateFactory(connection);
        var quality = factory.Services.GetRequiredService<QualityRepository>();
        var stock = await ReadStockAsync(connection, factory.Services.GetRequiredService<InventoryService>());
        var inspection = await CreateInspectionAsync(quality, connection, stock, "QA-02", 11m);
        Assert.Equal("Failed", inspection.DecisionState);
        var issue = await quality.CreateIssueAsync(AtlasTenant, Guid.CreateVersion7(), AtlasEntity, new("MaterialDefect", "QA-02 defect", "Failed receiving characteristic", "Major", inspection.Id), CancellationToken.None);
        var ncr = await quality.CreateNcrAsync(AtlasTenant, Guid.CreateVersion7(), AtlasEntity, new(issue.Id, "QA-02 NCR", "Contain failed material"), CancellationToken.None);
        var hold = await quality.CreateRestrictionAsync(AtlasTenant, Guid.CreateVersion7(), new("QualityHold", "QA02", stock.ProductVariantId, stock.WarehouseId, stock.LocationId, Quantity: 1m, UnitOfMeasureId: stock.UnitId, InspectionId: inspection.Id), AtlasEntity, CancellationToken.None);
        var disposition = await quality.CreateDispositionAsync(AtlasTenant, Guid.CreateVersion7(), AtlasEntity, new("Accept", "Authorized QA-02 disposition", inspection.Id, 1m, UnitOfMeasureId: stock.UnitId), CancellationToken.None);
        await quality.ApproveDispositionAsync(AtlasTenant, Guid.CreateVersion7(), disposition.Id, new("QA-02 manager approval"), CancellationToken.None);
        var executed = await quality.ExecuteDispositionAsync(AtlasTenant, Guid.CreateVersion7(), disposition.Id, CancellationToken.None);
        Assert.Equal("Executed", executed.State);
        Assert.Equal("Released", (await quality.GetRestrictionAsync(AtlasTenant, hold.Id, CancellationToken.None))!.Status);
        Assert.Equal(1L, await CountAsync(connection, "select count(*) from business_quality.nonconformance where tenant_id=$1 and id=$2", AtlasTenant.Value, ncr.Id));
    }

    [Fact(DisplayName = "QA-04 partial release preserves the remaining restricted quantity")]
    public async Task Qa04PartialReleaseConservesRestrictedQuantity()
    {
        var connection = RequireConnection();
        using var factory = CreateFactory(connection);
        var quality = factory.Services.GetRequiredService<QualityRepository>();
        var stock = await ReadStockAsync(connection, factory.Services.GetRequiredService<InventoryService>());
        var before = await StockQuantityAsync(connection, stock.ProductVariantId, stock.WarehouseId, stock.LocationId, "Available") + await StockQuantityAsync(connection, stock.ProductVariantId, stock.WarehouseId, stock.LocationId, "QualityHold");
        var hold = await quality.CreateRestrictionAsync(AtlasTenant, Guid.CreateVersion7(), new("QualityHold", "QA04", stock.ProductVariantId, stock.WarehouseId, stock.LocationId, Quantity: 3m, UnitOfMeasureId: stock.UnitId), AtlasEntity, CancellationToken.None);
        var firstRequest = new ReleaseRestrictionRequest("QA-04 partial acceptance", 1m, stock.UnitId, IdempotencyKey: $"qa-04-1:{Guid.CreateVersion7():D}");
        var partial = await quality.ReleaseRestrictionAsync(AtlasTenant, Guid.CreateVersion7(), hold.Id, firstRequest, CancellationToken.None);
        Assert.Equal("Active", partial.Status);
        var replay = await quality.ReleaseRestrictionAsync(AtlasTenant, Guid.CreateVersion7(), hold.Id, firstRequest, CancellationToken.None);
        Assert.Equal(partial.OwnerOperationId, replay.OwnerOperationId);
        var competing = await Task.WhenAll(
            AttemptReleaseAsync(quality, hold.Id, new ReleaseRestrictionRequest("QA-04 competing A", 1m, stock.UnitId, IdempotencyKey: $"qa-04-2:{Guid.CreateVersion7():D}")),
            AttemptReleaseAsync(quality, hold.Id, new ReleaseRestrictionRequest("QA-04 competing B", 1m, stock.UnitId, IdempotencyKey: $"qa-04-3:{Guid.CreateVersion7():D}")));
        Assert.InRange(competing.Count(result => result.Success), 1, 2);
        Assert.InRange(competing.Count(result => !result.Success), 0, 1);
        var current = await quality.GetRestrictionAsync(AtlasTenant, hold.Id, CancellationToken.None);
        Assert.NotNull(current);
        if (current!.ReleasedQuantity < current.Quantity)
            await quality.ReleaseRestrictionAsync(AtlasTenant, Guid.CreateVersion7(), hold.Id, new("QA-04 final release", current.Quantity - current.ReleasedQuantity, stock.UnitId, IdempotencyKey: $"qa-04-final:{Guid.CreateVersion7():D}"), CancellationToken.None);
        var final = await quality.GetRestrictionAsync(AtlasTenant, hold.Id, CancellationToken.None);
        Assert.NotNull(final);
        Assert.Equal(3m, final!.ReleasedQuantity);
        Assert.Equal("Released", final.Status);
        Assert.Equal(3L, await CountAsync(connection, "select count(*) from business_quality.quality_restriction_release_operation where tenant_id=$1 and restriction_id=$2 and state='Confirmed'", AtlasTenant.Value, hold.Id));
        var after = await StockQuantityAsync(connection, stock.ProductVariantId, stock.WarehouseId, stock.LocationId, "Available") + await StockQuantityAsync(connection, stock.ProductVariantId, stock.WarehouseId, stock.LocationId, "QualityHold");
        Assert.Equal(before, after);
    }

    [Fact(DisplayName = "QA-07 certificate stores an immutable approved inspection snapshot")]
    public async Task Qa07CertificateStoresHistoricalInspectionSnapshot()
    {
        var connection = RequireConnection();
        using var factory = CreateFactory(connection);
        var quality = factory.Services.GetRequiredService<QualityRepository>();
        var stock = await ReadStockAsync(connection, factory.Services.GetRequiredService<InventoryService>());
        var inspection = await CreatePassedInspectionAsync(quality, connection, stock, "QA-07");
        var certificate = await quality.IssueCertificateAsync(AtlasTenant, Guid.CreateVersion7(), new(inspection.Id, "COA", "QA-07"), AtlasEntity, CancellationToken.None);
        var snapshot = (string)(await ScalarAsync(connection, "select snapshot::text from business_quality.quality_certificate_version where tenant_id=$1 and certificate_id=$2", AtlasTenant.Value, certificate.Id))!;
        Assert.Contains(inspection.Id.ToString("D"), snapshot, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("10", snapshot, StringComparison.Ordinal);
        Assert.Equal("Issued", (string)(await ScalarAsync(connection, "select state from business_quality.quality_certificate where tenant_id=$1 and id=$2", AtlasTenant.Value, certificate.Id))!);
    }

    [Fact(DisplayName = "QA-08 failed downstream execution remains recoverable and exactly once")]
    public async Task Qa08FailedOwnerExecutionDoesNotReleaseQualityLocally()
    {
        var connection = RequireConnection();
        using var factory = CreateFactory(connection);
        var quality = factory.Services.GetRequiredService<QualityRepository>();
        var stock = await ReadStockAsync(connection, factory.Services.GetRequiredService<InventoryService>());
        await Assert.ThrowsAsync<QualityException>(() => quality.CreateRestrictionAsync(AtlasTenant, Guid.CreateVersion7(), new("QualityHold", "QA08-FAIL", stock.ProductVariantId, stock.WarehouseId, stock.LocationId, Quantity: stock.AvailableQuantity + 100m, UnitOfMeasureId: stock.UnitId), AtlasEntity, CancellationToken.None));
        var hold = await quality.CreateRestrictionAsync(AtlasTenant, Guid.CreateVersion7(), new("QualityHold", "QA08-RECOVER", stock.ProductVariantId, stock.WarehouseId, stock.LocationId, Quantity: 2m, UnitOfMeasureId: stock.UnitId), AtlasEntity, CancellationToken.None);
        var inventory = factory.Services.GetRequiredService<InventoryService>();
        var qualityHoldAfterCreate = await StockQuantityAsync(connection, stock.ProductVariantId, stock.WarehouseId, stock.LocationId, "QualityHold");
        var drainedQuantity = qualityHoldAfterCreate - 1m;
        await inventory.AdjustAsync(AtlasTenant.Value, Guid.CreateVersion7(), new InventoryAdjustmentRequest(stock.ProductVariantId, AtlasEntity, stock.WarehouseId, stock.LocationId, drainedQuantity, stock.UnitId, "Decrease", "Scrap", DateOnly.FromDateTime(DateTime.UtcNow.Date), DateOnly.FromDateTime(DateTime.UtcNow.Date), $"qa-08-drain:{Guid.CreateVersion7():D}", "QualityHold"), CancellationToken.None);
        var recoveryRequest = new ReleaseRestrictionRequest("QA-08 recovery", 2m, stock.UnitId, IdempotencyKey: $"qa-08-recovery:{Guid.CreateVersion7():D}");
        await Assert.ThrowsAsync<QualityException>(() => quality.ReleaseRestrictionAsync(AtlasTenant, Guid.CreateVersion7(), hold.Id, recoveryRequest, CancellationToken.None));
        Assert.Equal("Unknown", (string)(await ScalarAsync(connection, "select state from business_quality.quality_restriction_release_operation where tenant_id=$1 and restriction_id=$2 and idempotency_key=$3", AtlasTenant.Value, hold.Id, recoveryRequest.IdempotencyKey!))!);
        await inventory.AdjustAsync(AtlasTenant.Value, Guid.CreateVersion7(), new InventoryAdjustmentRequest(stock.ProductVariantId, AtlasEntity, stock.WarehouseId, stock.LocationId, drainedQuantity, stock.UnitId, "Increase", "DataCorrection", DateOnly.FromDateTime(DateTime.UtcNow.Date), DateOnly.FromDateTime(DateTime.UtcNow.Date), $"qa-08-restore:{Guid.CreateVersion7():D}", "QualityHold"), CancellationToken.None);
        var recovered = await quality.ReleaseRestrictionAsync(AtlasTenant, Guid.CreateVersion7(), hold.Id, recoveryRequest, CancellationToken.None);
        var replay = await quality.ReleaseRestrictionAsync(AtlasTenant, Guid.CreateVersion7(), hold.Id, recoveryRequest, CancellationToken.None);
        Assert.Equal("Released", recovered.Status);
        Assert.Equal(recovered.OwnerOperationId, replay.OwnerOperationId);
        Assert.Equal("Confirmed", (string)(await ScalarAsync(connection, "select state from business_quality.quality_restriction_release_operation where tenant_id=$1 and restriction_id=$2 and idempotency_key=$3", AtlasTenant.Value, hold.Id, recoveryRequest.IdempotencyKey!))!);
        await Assert.ThrowsAsync<QualityException>(() => quality.ReleaseRestrictionAsync(AtlasTenant, Guid.CreateVersion7(), hold.Id, new("QA-08 duplicate", 1m, stock.UnitId, IdempotencyKey: $"qa-08-duplicate:{Guid.CreateVersion7():D}"), CancellationToken.None));
        var releaseOwnerOperation = (Guid)(await ScalarAsync(connection, "select owner_operation_id from business_quality.quality_restriction_release_operation where tenant_id=$1 and restriction_id=$2 and idempotency_key=$3", AtlasTenant.Value, hold.Id, recoveryRequest.IdempotencyKey!))!;
        Assert.Equal(1L, await CountAsync(connection, "select count(*) from business_inventory.inventory_operation where tenant_id=$1 and id=$2 and source_module='inventory' and status='Confirmed'", AtlasTenant.Value, releaseOwnerOperation));
    }

    [Fact(DisplayName = "QA-10 duplicate source fingerprints are idempotent")]
    public async Task Qa10DuplicateSourceFingerprintReturnsOneInspection()
    {
        var connection = RequireConnection();
        using var factory = CreateFactory(connection);
        var quality = factory.Services.GetRequiredService<QualityRepository>();
        var stock = await ReadStockAsync(connection, factory.Services.GetRequiredService<InventoryService>());
        var fingerprint = $"qa-10:{Guid.NewGuid():N}";
        var request = new CreateInspectionRequest(AtlasEntity, "Receiving", stock.ProductVariantId, 1m, PlanVersionId: await PlanVersionAsync(connection), LotReference: "QA-10", SourceFingerprint: fingerprint);
        var first = await quality.CreateInspectionAsync(AtlasTenant, Guid.CreateVersion7(), request, CancellationToken.None);
        var retry = await quality.CreateInspectionAsync(AtlasTenant, Guid.CreateVersion7(), request, CancellationToken.None);
        Assert.Equal(first.Id, retry.Id);
        Assert.Equal(1L, await CountAsync(connection, "select count(*) from business_quality.inspection where tenant_id=$1 and source_fingerprint=$2", AtlasTenant.Value, fingerprint));
    }

    [Fact(DisplayName = "QA-03 failed production quality decision blocks continuation evidence")]
    public async Task Qa03FailedProductionDecisionIsAuthoritative()
    {
        var connection = RequireConnection();
        using var factory = CreateFactory(connection);
        var quality = factory.Services.GetRequiredService<QualityRepository>();
        var stock = await ReadStockAsync(connection, factory.Services.GetRequiredService<InventoryService>());
        await using var productionDb = new NpgsqlConnection(connection); await productionDb.OpenAsync(); await SetTenantAsync(productionDb);
        var orderId = (Guid)(await ScalarAsync(productionDb, "select id from business_manufacturing.production_order where tenant_id=$1 and legal_entity_id=$2 and state not in ('Completed','Closed','Cancelled') order by id limit 1", AtlasTenant.Value, AtlasEntity))!;
        var operationId = (Guid)(await ScalarAsync(productionDb, "select id from business_manufacturing.production_operation where tenant_id=$1 and production_order_id=$2 and state <> 'Complete' order by sequence_no,id limit 1", AtlasTenant.Value, orderId))!;
        var productVariantId = (Guid)(await ScalarAsync(productionDb, "select product_variant_id from business_manufacturing.production_order where tenant_id=$1 and id=$2", AtlasTenant.Value, orderId))!;
        var inspection = await quality.CreateInspectionAsync(AtlasTenant, Guid.CreateVersion7(), new(AtlasEntity, "Receiving", productVariantId, 1m, ProductionOrderId: orderId, ProductionOperationId: operationId, PlanVersionId: await PlanVersionAsync(connection), SourceFingerprint: $"qa-03:{Guid.NewGuid():N}"), CancellationToken.None);
        var sample = (Guid)(await ScalarAsync(connection, "select id from business_quality.quality_sample where tenant_id=$1 and inspection_id=$2 limit 1", AtlasTenant.Value, inspection.Id))!;
        var characteristic = (Guid)(await ScalarAsync(connection, "select characteristic_id from business_quality.quality_plan_characteristic where tenant_id=$1 and plan_version_id=$2 limit 1", AtlasTenant.Value, await PlanVersionAsync(connection)))!;
        await quality.AddResultAsync(AtlasTenant, Guid.CreateVersion7(), inspection.Id, new(characteristic, NumericValue: 11m, UomCode: "KG", SampleId: sample), CancellationToken.None);
        await quality.CompleteInspectionAsync(AtlasTenant, Guid.CreateVersion7(), inspection.Id, new("QA-03 failed gate"), CancellationToken.None);
        var disposition = await quality.CreateDispositionAsync(AtlasTenant, Guid.CreateVersion7(), AtlasEntity, new("Rework", "QA-03 rework after failed production gate", inspection.Id, 1m), CancellationToken.None);
        await quality.ApproveDispositionAsync(AtlasTenant, Guid.CreateVersion7(), disposition.Id, new("QA-03 quality manager approval"), CancellationToken.None);
        var executed = await quality.ExecuteDispositionAsync(AtlasTenant, Guid.CreateVersion7(), disposition.Id, CancellationToken.None);
        Assert.Equal("Executed", executed.State);
        Assert.Equal("Confirmed", executed.OwnerExecutionStatus);
        Assert.Equal("OnHold", (string)(await ScalarAsync(connection, "select state from business_manufacturing.production_order where tenant_id=$1 and id=$2", AtlasTenant.Value, orderId))!);
        Assert.Equal("Blocked", (string)(await ScalarAsync(connection, "select state from business_manufacturing.production_operation where tenant_id=$1 and id=$2", AtlasTenant.Value, operationId))!);
        Assert.Equal(1L, await CountAsync(connection, "select count(*) from business_manufacturing.production_external_reference where tenant_id=$1 and production_order_id=$2 and source_id=$3 and purpose='Rework'", AtlasTenant.Value, orderId, disposition.Id.ToString("D")));
        var decision = await quality.GetProductionDecisionAsync(AtlasTenant, AtlasEntity, orderId, operationId, CancellationToken.None);
        Assert.NotNull(decision);
        Assert.Equal("Failed", decision!.DecisionState);
    }

    [Fact(DisplayName = "QA-05 NCR containment CAPA action effectiveness and closure are persisted")]
    public async Task Qa05NcrCapaLifecyclePersistsEvidence()
    {
        var connection = RequireConnection();
        using var factory = CreateFactory(connection);
        var quality = factory.Services.GetRequiredService<QualityRepository>();
        var issue = await quality.CreateIssueAsync(AtlasTenant, Guid.CreateVersion7(), AtlasEntity, new("ProcessDefect", "QA-05 issue", "NCR lifecycle", "Major"), CancellationToken.None);
        var ncr = await quality.CreateNcrAsync(AtlasTenant, Guid.CreateVersion7(), AtlasEntity, new(issue.Id, "QA-05 NCR", "Contain and correct"), CancellationToken.None);
        await quality.AddContainmentAsync(AtlasTenant, Guid.CreateVersion7(), ncr.Id, new("Segregate", "Quarantine affected lot", Quantity: 1m), CancellationToken.None);
        var capa = await quality.CreateCapaAsync(AtlasTenant, Guid.CreateVersion7(), AtlasEntity, new("QA-05 corrective action", "Verified root cause", NcrId: ncr.Id), CancellationToken.None);
        await quality.AddCapaRootCauseAsync(AtlasTenant, Guid.CreateVersion7(), capa.Id, new("Process", "Incorrect receiving setup", true), CancellationToken.None);
        var action = await quality.AddCapaActionAsync(AtlasTenant, Guid.CreateVersion7(), capa.Id, new("Correct", "Correct setup and retrain", Guid.CreateVersion7(), DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(3))), CancellationToken.None);
        await quality.CompleteCapaActionAsync(AtlasTenant, Guid.CreateVersion7(), action.Id, new("training-evidence"), CancellationToken.None);
        await quality.RecordEffectivenessAsync(AtlasTenant, Guid.CreateVersion7(), capa.Id, new(true, "effectiveness-evidence", "No recurrence"), CancellationToken.None);
        Assert.Equal("Closed", (await quality.GetCapaAsync(AtlasTenant, capa.Id, CancellationToken.None))!.State);
        Assert.Equal("Contained", await ScalarAsync(connection, "select state from business_quality.nonconformance where tenant_id=$1 and id=$2", AtlasTenant.Value, ncr.Id));
    }

    [Fact(DisplayName = "QA-06 supplier SCAR response review and effectiveness states are persisted")]
    public async Task Qa06SupplierScarLifecyclePersistsResponseAndReview()
    {
        var connection = RequireConnection();
        using var factory = CreateFactory(connection);
        var quality = factory.Services.GetRequiredService<QualityRepository>();
        var supplier = (Guid)(await ScalarAsync(connection, "select p.id from foundation_directory.party p join foundation_directory.party_role r on r.tenant_id=p.tenant_id and r.party_id=p.id where p.tenant_id=$1 and r.role_code='Supplier' and p.status='Active' order by p.id limit 1", AtlasTenant.Value))!;
        var scar = await quality.CreateScarAsync(AtlasTenant, Guid.CreateVersion7(), new(AtlasEntity, supplier, "QA-06 SCAR", "Supplier corrective response"), CancellationToken.None);
        await quality.SubmitScarResponseAsync(AtlasTenant, Guid.CreateVersion7(), scar.Id, new("Supplier response", "supplier-evidence"), CancellationToken.None);
        var reviewed = await quality.ReviewScarAsync(AtlasTenant, Guid.CreateVersion7(), scar.Id, new("Accept", "Verified response"), CancellationToken.None);
        Assert.Equal("Accepted", reviewed.State);
        Assert.Equal("Accepted", await ScalarAsync(connection, "select state from business_quality.supplier_corrective_action_request where tenant_id=$1 and id=$2", AtlasTenant.Value, scar.Id));
    }

    [Fact(DisplayName = "QA-09 Quality RLS denies empty and cross-tenant reads")]
    public async Task Qa09QualityRlsDeniesEmptyAndCrossTenantReads()
    {
        var connection = RequireConnection();
        await using var db = new NpgsqlConnection(connection); await db.OpenAsync();
        Assert.Equal(0L, Convert.ToInt64(await ScalarUnscopedAsync(db, "select count(*) from business_quality.quality_plan"), CultureInfo.InvariantCulture));
        await SetTenantAsync(db);
        Assert.True(Convert.ToInt64(await ScalarAsync(db, "select count(*) from business_quality.quality_plan where tenant_id=$1", AtlasTenant.Value), CultureInfo.InvariantCulture) > 0);
        Assert.Equal(0L, Convert.ToInt64(await ScalarAsync(db, "select count(*) from business_quality.quality_plan where tenant_id=$1", Guid.Parse("d3e6d8b7-1f72-4f3e-9b3b-0a8ef8f6bb8b")), CultureInfo.InvariantCulture));
    }

    [Fact(DisplayName = "QA-10 real RabbitMQ Worker Inbox applies StockReceived exactly once")]
    public async Task Qa10RealWorkerAppliesStockReceivedExactlyOnce()
    {
        var connection = RequireConnection();
        using var factory = CreateFactory(connection);
        var stock = await ReadStockAsync(connection, factory.Services.GetRequiredService<InventoryService>());
        var supplier = (Guid)(await ScalarAsync(connection, "select supplier_party_id from business_quality.quality_plan_applicability a join business_quality.quality_plan p on p.tenant_id=a.tenant_id and p.id=a.plan_id where a.tenant_id=$1 and p.legal_entity_id=$2 and p.event_type='Receiving' and a.product_variant_id=$3 limit 1", AtlasTenant.Value, AtlasEntity, stock.ProductVariantId))!;
        var message = new OutboxPublishedMessage(Guid.CreateVersion7(), "StockReceivedV1", JsonSerializer.Serialize(new { legalEntityId = AtlasEntity, productVariantId = stock.ProductVariantId, supplierPartyId = supplier, quantity = 1m, operationId = Guid.CreateVersion7() }), AtlasTenant, DateTimeOffset.UtcNow, Guid.CreateVersion7(), Guid.CreateVersion7(), "purchasing", null, 0);
        var rabbitHost = Environment.GetEnvironmentVariable("TODIR_RABBITMQ_HOST") ?? "127.0.0.1";
        var rabbitPort = int.TryParse(Environment.GetEnvironmentVariable("TODIR_RABBITMQ_PORT"), out var port) ? port : 5672;
        var rabbitUser = Environment.GetEnvironmentVariable("TODIR_RABBITMQ_USER") ?? "todir";
        var rabbitPassword = Environment.GetEnvironmentVariable("TODIR_RABBITMQ_PASSWORD") ?? "todir-dev-only";
        var rabbitVHost = Environment.GetEnvironmentVariable("TODIR_RABBITMQ_VHOST") ?? "/erp-dev";
        using var worker = StartQualityWorker(connection, rabbitHost, rabbitPort, rabbitUser, rabbitPassword, rabbitVHost);
        IBusControl? bus = null;
        try
        {
            var rabbitFactory = new ConnectionFactory { HostName = rabbitHost, Port = rabbitPort, VirtualHost = rabbitVHost, UserName = rabbitUser, Password = rabbitPassword };
            await using (var rabbitConnection = await rabbitFactory.CreateConnectionAsync())
            {
                var declared = false;
                for (var attempt = 0; attempt < 40; attempt++)
                {
                    try { await using var channel = await rabbitConnection.CreateChannelAsync(); await channel.QueueDeclarePassiveAsync("quality-integration"); declared = true; break; }
                    catch { await Task.Delay(500); }
                }
                Assert.True(declared, "The Quality Worker did not declare its integration queue within 20 seconds.");
            }
            var hostUri = new UriBuilder("rabbitmq", rabbitHost) { Port = rabbitPort, Path = $"/{Uri.EscapeDataString(rabbitVHost.StartsWith('/') ? rabbitVHost : "/" + rabbitVHost)}" }.Uri;
            bus = Bus.Factory.CreateUsingRabbitMq(config => config.Host(hostUri, host => { host.Username(rabbitUser); host.Password(rabbitPassword); }));
            await bus.StartAsync();
            await bus.Publish(message);
            Assert.True(await EventuallyAsync(async () => Convert.ToInt64(await ScalarAsync(connection, "select count(*) from business_quality.quality_integration_event where tenant_id=$1 and message_id=$2 and status='Applied'", AtlasTenant.Value, message.MessageId), CultureInfo.InvariantCulture) == 1));
            Assert.Equal(1L, await CountAsync(connection, "select count(*) from business_quality.inspection where tenant_id=$1 and source_fingerprint=$2", AtlasTenant.Value, $"integration:{message.MessageId:D}"));
            await bus.Publish(message);
            await Task.Delay(750);
            Assert.Equal(1L, await CountAsync(connection, "select count(*) from business_quality.inspection where tenant_id=$1 and source_fingerprint=$2", AtlasTenant.Value, $"integration:{message.MessageId:D}"));
            Assert.Equal(1L, await CountAsync(connection, "select count(*) from platform.inbox_message where tenant_id=$1 and consumer_endpoint=$2 and message_id=$3 and processed_at is not null", AtlasTenant.Value, "quality-integration", message.MessageId));
        }
        finally
        {
            if (bus is not null) await bus.StopAsync();
            StopWorker(worker);
        }
    }

    [Fact(DisplayName = "QA-11 actual Purchasing receipt creates mandatory QualityHold before any disposition")]
    public async Task Qa11ActualPurchasingReceiptCreatesMandatoryQualityHold()
    {
        var connection = RequireConnection();
        using var factory = CreateFactory(connection);
        var purchasing = factory.Services.GetRequiredService<PurchasingService>();
        var (order, line, warehouseId, locationId) = await ReadFreshPurchaseLineAsync(connection, purchasing);
        var inventory = factory.Services.GetRequiredService<InventoryService>();
        var quality = factory.Services.GetRequiredService<QualityRepository>();
        await EnsureReceivingQualityPlanAsync(connection, quality, line.ProductVariantId!.Value);
        await quality.SaveSupplierProfileAsync(AtlasTenant, Guid.CreateVersion7(), new(AtlasEntity, order.SupplierPartyId, ReceivingInspectionRequired: true), CancellationToken.None);
        var (lotId, serialId) = await EnsureReceiptTrackingAsync(connection, inventory, line.ProductVariantId!.Value);
        var before = await StockQuantityAsync(connection, line.ProductVariantId!.Value, warehouseId, locationId, "QualityHold");

        var result = await purchasing.ReceiveAsync(AtlasTenant, Guid.CreateVersion7(), AtlasEntity, order.Id,
            new ReceivePurchaseOrderRequest(AtlasEntity, order.Version, $"qa-11-receipt:{Guid.CreateVersion7():D}",
                DateOnly.FromDateTime(DateTime.UtcNow.Date), DateOnly.FromDateTime(DateTime.UtcNow.Date),
                [new PurchaseReceiptLineRequest(line.Id, line.ProductVariantId.Value, warehouseId, locationId, line.UnitOfMeasureId!.Value, 1m, LotId: lotId, SerialId: serialId)]),
            Guid.CreateVersion7(), CancellationToken.None);

        Assert.NotEmpty(result.ReceiptIds);
        Assert.Equal(before + 1m, await StockQuantityAsync(connection, line.ProductVariantId.Value, warehouseId, locationId, "QualityHold"));
        Assert.Equal(1L, await CountAsync(connection, "select count(*) from business_quality.inspection where tenant_id=$1 and source_document_id=$2 and event_type='Receiving'", AtlasTenant.Value, order.Id));
        Assert.Equal("QualityHold", await StockStatusAsync(connection, new StockRef(line.ProductVariantId.Value, warehouseId, locationId, line.UnitOfMeasureId.Value, 0m), "QualityHold"));
    }

    [Fact(DisplayName = "QA-12 supplier Return consumes QualityHold without releasing Available stock and replays exactly once")]
    public async Task Qa12SupplierReturnNeverLeaksRestrictedStock()
    {
        var connection = RequireConnection();
        using var factory = CreateFactory(connection);
        var quality = factory.Services.GetRequiredService<QualityRepository>();
        var purchasing = factory.Services.GetRequiredService<PurchasingService>();
        var inventory = factory.Services.GetRequiredService<InventoryService>();
        var (order, line, warehouseId, locationId) = await ReadFreshPurchaseLineAsync(connection, purchasing);
        await EnsureReceivingQualityPlanAsync(connection, quality, line.ProductVariantId!.Value);
        await quality.SaveSupplierProfileAsync(AtlasTenant, Guid.CreateVersion7(), new(AtlasEntity, order.SupplierPartyId, ReceivingInspectionRequired: true), CancellationToken.None);
        var (lotId, serialId) = await EnsureReceiptTrackingAsync(connection, inventory, line.ProductVariantId!.Value);
        var receiveKey = $"qa-12-receipt:{Guid.CreateVersion7():D}";
        await purchasing.ReceiveAsync(AtlasTenant, Guid.CreateVersion7(), AtlasEntity, order.Id,
            new ReceivePurchaseOrderRequest(AtlasEntity, order.Version, receiveKey, DateOnly.FromDateTime(DateTime.UtcNow.Date), DateOnly.FromDateTime(DateTime.UtcNow.Date),
                [new PurchaseReceiptLineRequest(line.Id, line.ProductVariantId!.Value, warehouseId, locationId, line.UnitOfMeasureId!.Value, 1m, LotId: lotId, SerialId: serialId)]), Guid.CreateVersion7(), CancellationToken.None);

        var inspectionId = (Guid)(await ScalarAsync(connection, "select id from business_quality.inspection where tenant_id=$1 and source_document_id=$2 and event_type='Receiving' order by created_at desc limit 1", AtlasTenant.Value, order.Id))!;
        await inventory.ChangeQualityStatusAsync(AtlasTenant.Value, Guid.CreateVersion7(), new(AtlasEntity, line.ProductVariantId.Value, warehouseId, locationId, line.UnitOfMeasureId.Value, 1m, "QualityHold", "Available", DateOnly.FromDateTime(DateTime.UtcNow.Date), DateOnly.FromDateTime(DateTime.UtcNow.Date), $"qa-12-move-to-available:{Guid.CreateVersion7():D}", lotId, serialId), CancellationToken.None);

        var hold = await quality.CreateRestrictionAsync(AtlasTenant, Guid.CreateVersion7(), new("QualityHold", "QA12", line.ProductVariantId.Value, warehouseId, locationId, LotId: lotId, SerialId: serialId, Quantity: 1m, UnitOfMeasureId: line.UnitOfMeasureId.Value, InspectionId: inspectionId), AtlasEntity, CancellationToken.None);
        var availableBefore = await StockQuantityAsync(connection, line.ProductVariantId.Value, warehouseId, locationId, "Available");
        var holdBefore = await StockQuantityAsync(connection, line.ProductVariantId.Value, warehouseId, locationId, "QualityHold");
        var disposition = await quality.CreateDispositionAsync(AtlasTenant, Guid.CreateVersion7(), AtlasEntity, new("Return", "QA-12 supplier return", inspectionId, 1m, UnitOfMeasureId: line.UnitOfMeasureId.Value), CancellationToken.None);
        await quality.ApproveDispositionAsync(AtlasTenant, Guid.CreateVersion7(), disposition.Id, new("QA-12 approval"), CancellationToken.None);

        var executed = await quality.ExecuteDispositionAsync(AtlasTenant, Guid.CreateVersion7(), disposition.Id, CancellationToken.None);
        var replay = await quality.ExecuteDispositionAsync(AtlasTenant, Guid.CreateVersion7(), disposition.Id, CancellationToken.None);
        Assert.Equal("Executed", executed.State);
        Assert.Equal(executed.OwnerOperationId, replay.OwnerOperationId);
        Assert.Equal("Released", (await quality.GetRestrictionAsync(AtlasTenant, hold.Id, CancellationToken.None))!.Status);
        Assert.Equal(availableBefore, await StockQuantityAsync(connection, line.ProductVariantId.Value, warehouseId, locationId, "Available"));
        Assert.Equal(holdBefore - 1m, await StockQuantityAsync(connection, line.ProductVariantId.Value, warehouseId, locationId, "QualityHold"));
        Assert.Equal(1L, await CountAsync(connection, "select count(*) from business_purchasing.supplier_return_request where tenant_id=$1 and idempotency_key=$2 and state='Executed'", AtlasTenant.Value, $"quality-disposition:{disposition.Id:D}"));
        Assert.Equal(1L, await CountAsync(connection, "select count(*) from business_inventory.inventory_operation where tenant_id=$1 and legal_entity_id=$2 and source_module='purchasing' and source_document_id=(select id from business_purchasing.supplier_return_request where tenant_id=$1 and idempotency_key=$3)", AtlasTenant.Value, AtlasEntity, $"quality-disposition:{disposition.Id:D}"));
        Assert.Equal(0L, await CountAsync(connection, "select count(*) from business_quality.quality_restriction_release_operation where tenant_id=$1 and restriction_id=$2 and idempotency_key=$3", AtlasTenant.Value, hold.Id, $"quality-disposition:{disposition.Id:D}:release"));
    }

    [Fact(DisplayName = "QA-13 Unknown disposition reconciliation retries the owner idempotency identity exactly once")]
    public async Task Qa13UnknownDispositionReconcilesExactlyOnce()
    {
        var connection = RequireConnection();
        using var factory = CreateFactory(connection);
        var quality = factory.Services.GetRequiredService<QualityRepository>();
        var stock = await ReadStockAsync(connection, factory.Services.GetRequiredService<InventoryService>());
        var inspection = await CreatePassedInspectionAsync(quality, connection, stock, "QA-13");
        var hold = await quality.CreateRestrictionAsync(AtlasTenant, Guid.CreateVersion7(), new("QualityHold", "QA13", stock.ProductVariantId, stock.WarehouseId, stock.LocationId, Quantity: 1m, UnitOfMeasureId: stock.UnitId, InspectionId: inspection.Id), AtlasEntity, CancellationToken.None);
        var disposition = await quality.CreateDispositionAsync(AtlasTenant, Guid.CreateVersion7(), AtlasEntity, new("Accept", "QA-13 reconcile", inspection.Id, 1m, UnitOfMeasureId: stock.UnitId), CancellationToken.None);
        await quality.ApproveDispositionAsync(AtlasTenant, Guid.CreateVersion7(), disposition.Id, new("QA-13 approval"), CancellationToken.None);
        var key = $"quality-disposition:{disposition.Id:D}";
        await ExecuteAsync(connection, "update business_quality.quality_disposition set owner_execution_status='Unknown',owner_idempotency_key=$3,execution_error_code='simulated_unknown' where tenant_id=$1 and id=$2", AtlasTenant.Value, disposition.Id, key);

        var reconciled = await quality.ReconcileDispositionAsync(AtlasTenant, Guid.CreateVersion7(), disposition.Id, CancellationToken.None);
        var replay = await quality.ReconcileDispositionAsync(AtlasTenant, Guid.CreateVersion7(), disposition.Id, CancellationToken.None);
        Assert.Equal("Executed", reconciled.State);
        Assert.Equal(reconciled.OwnerOperationId, replay.OwnerOperationId);
        Assert.Equal("Released", (await quality.GetRestrictionAsync(AtlasTenant, hold.Id, CancellationToken.None))!.Status);
        Assert.Equal(1L, await CountAsync(connection, "select count(*) from business_quality.quality_restriction_release_operation where tenant_id=$1 and restriction_id=$2 and state='Confirmed'", AtlasTenant.Value, hold.Id));
        Assert.Equal(1L, await CountAsync(connection, "select count(*) from business_inventory.inventory_operation where tenant_id=$1 and id=$2 and status='Confirmed'", AtlasTenant.Value, reconciled.OwnerOperationId!.Value));
    }

    [Fact(DisplayName = "QA-14 Purchasing failure keeps supplier-return quantity restricted")]
    public async Task Qa14SupplierReturnFailureKeepsQuantityRestricted()
    {
        var connection = RequireConnection();
        using var factory = CreateFactory(connection);
        var quality = factory.Services.GetRequiredService<QualityRepository>();
        var stock = await ReadStockAsync(connection, factory.Services.GetRequiredService<InventoryService>());
        var missingPurchaseOrderId = Guid.CreateVersion7();
        var inspection = await quality.CreateInspectionAsync(AtlasTenant, Guid.CreateVersion7(), new(AtlasEntity, "Receiving", stock.ProductVariantId, 1m, SourceDocumentId: missingPurchaseOrderId, PlanVersionId: await PlanVersionAsync(connection), SourceFingerprint: $"qa-14:{Guid.CreateVersion7():N}"), CancellationToken.None);
        var hold = await quality.CreateRestrictionAsync(AtlasTenant, Guid.CreateVersion7(), new("QualityHold", "QA14", stock.ProductVariantId, stock.WarehouseId, stock.LocationId, Quantity: 1m, UnitOfMeasureId: stock.UnitId, InspectionId: inspection.Id), AtlasEntity, CancellationToken.None);
        var before = await StockQuantityAsync(connection, stock.ProductVariantId, stock.WarehouseId, stock.LocationId, "QualityHold");
        var disposition = await quality.CreateDispositionAsync(AtlasTenant, Guid.CreateVersion7(), AtlasEntity, new("Return", "QA-14 invalid supplier return", inspection.Id, 1m, UnitOfMeasureId: stock.UnitId), CancellationToken.None);
        await quality.ApproveDispositionAsync(AtlasTenant, Guid.CreateVersion7(), disposition.Id, new("QA-14 approval"), CancellationToken.None);

        var error = await Assert.ThrowsAsync<QualityException>(() => quality.ExecuteDispositionAsync(AtlasTenant, Guid.CreateVersion7(), disposition.Id, CancellationToken.None));
        Assert.Equal("quality.supplier_return_purchase_order_not_found", error.Code);
        Assert.Equal("Failed", (string)(await ScalarAsync(connection, "select owner_execution_status from business_quality.quality_disposition where tenant_id=$1 and id=$2", AtlasTenant.Value, disposition.Id))!);
        Assert.Equal("Active", (await quality.GetRestrictionAsync(AtlasTenant, hold.Id, CancellationToken.None))!.Status);
        Assert.Equal(before, await StockQuantityAsync(connection, stock.ProductVariantId, stock.WarehouseId, stock.LocationId, "QualityHold"));
        Assert.Equal(0L, await CountAsync(connection, "select count(*) from business_purchasing.supplier_return_request where tenant_id=$1 and idempotency_key=$2", AtlasTenant.Value, $"quality-disposition:{disposition.Id:D}"));
    }

    private static async Task<Modules.Quality.DTOs.InspectionView> CreatePassedInspectionAsync(QualityRepository quality, string connection, StockRef stock, string key)
        => await CreateInspectionAsync(quality, connection, stock, key, 10m);

    private static async Task<(Modules.Purchasing.DTOs.PurchaseOrderView Order, PurchaseOrderLineView Line, Guid WarehouseId, Guid LocationId)> ReadFreshPurchaseLineAsync(string connection, PurchasingService purchasing)
    {
        var orderId = (Guid)(await ScalarAsync(connection, "select po.id from business_purchasing.purchase_order po join business_purchasing.purchase_order_line l on l.tenant_id=po.tenant_id and l.purchase_order_id=po.id join business_inventory.product_inventory_profile p on p.tenant_id=po.tenant_id and p.legal_entity_id=po.legal_entity_id and p.product_variant_id=l.product_variant_id where po.tenant_id=$1 and po.legal_entity_id=$2 and po.state in ('Approved','Open','PartiallyCompleted') and l.received_quantity=0 and not exists (select 1 from business_quality.inspection i where i.tenant_id=po.tenant_id and i.source_document_id=po.id and i.event_type='Receiving') order by po.id limit 1", AtlasTenant.Value, AtlasEntity))!;
        var order = await purchasing.GetAsync(AtlasTenant, AtlasEntity, orderId, false, CancellationToken.None) ?? throw new InvalidOperationException("QA acceptance requires a seeded approved purchase order.");
        var line = order.Lines.First(item => item.ProductVariantId is not null && item.UnitOfMeasureId is not null);
        var warehouseId = (Guid)(await ScalarAsync(connection, "select id from business_inventory.warehouse where tenant_id=$1 and legal_entity_id=$2 and status='Active' order by id limit 1", AtlasTenant.Value, AtlasEntity))!;
        var locationId = (Guid)(await ScalarAsync(connection, "select default_location_id from business_inventory.warehouse where tenant_id=$1 and id=$2", AtlasTenant.Value, warehouseId))!;
        return (order, line, warehouseId, locationId);
    }

    private static async Task<(Guid? LotId, Guid? SerialId)> EnsureReceiptTrackingAsync(string connection, InventoryService inventory, Guid productVariantId)
    {
        var tracking = (string?)(await ScalarAsync(connection, "select tracking_mode from business_inventory.product_inventory_profile where tenant_id=$1 and legal_entity_id=$2 and product_variant_id=$3", AtlasTenant.Value, AtlasEntity, productVariantId));
        if (string.Equals(tracking, "Lot", StringComparison.OrdinalIgnoreCase))
            return ((await inventory.CreateLotAsync(AtlasTenant, Guid.CreateVersion7(), AtlasEntity, new CreateLotRequest(productVariantId, $"QA-{Guid.CreateVersion7():N}", DateOnly.FromDateTime(DateTime.UtcNow.Date), null, "quality-acceptance"), Guid.CreateVersion7(), CancellationToken.None)).Id, null);
        if (string.Equals(tracking, "Serial", StringComparison.OrdinalIgnoreCase))
            return (null, (await inventory.CreateSerialAsync(AtlasTenant, Guid.CreateVersion7(), AtlasEntity, new CreateSerialRequest(productVariantId, $"QA-{Guid.CreateVersion7():N}"), Guid.CreateVersion7(), CancellationToken.None)).Id);
        return (null, null);
    }

    private static async Task EnsureReceivingQualityPlanAsync(string connection, QualityRepository quality, Guid productVariantId)
    {
        var planId = (Guid)(await ScalarAsync(connection, "select id from business_quality.quality_plan where tenant_id=$1 and legal_entity_id=$2 and event_type='Receiving' and status='Active' order by id limit 1", AtlasTenant.Value, AtlasEntity))!;
        var exists = await CountAsync(connection, "select count(*) from business_quality.quality_plan_applicability where tenant_id=$1 and plan_id=$2 and product_variant_id=$3", AtlasTenant.Value, planId, productVariantId);
        if (exists == 0)
            await quality.CreateApplicabilityAsync(AtlasTenant, Guid.CreateVersion7(), planId, new(productVariantId), CancellationToken.None);
    }

    private static async Task<Modules.Quality.DTOs.InspectionView> CreateInspectionAsync(QualityRepository quality, string connection, StockRef stock, string key, decimal value)
    {
        var inspection = await quality.CreateInspectionAsync(AtlasTenant, Guid.CreateVersion7(), new(AtlasEntity, "Receiving", stock.ProductVariantId, 1m, PlanVersionId: await PlanVersionAsync(connection), SourceFingerprint: $"{key}:{Guid.NewGuid():N}"), CancellationToken.None);
        var sampleId = (Guid)(await ScalarAsync(connection, "select id from business_quality.quality_sample where tenant_id=$1 and inspection_id=$2 order by sample_no limit 1", AtlasTenant.Value, inspection.Id))!;
        var characteristicId = (Guid)(await ScalarAsync(connection, "select characteristic_id from business_quality.quality_plan_characteristic where tenant_id=$1 and plan_version_id=$2 limit 1", AtlasTenant.Value, await PlanVersionAsync(connection)))!;
        await quality.AddResultAsync(AtlasTenant, Guid.CreateVersion7(), inspection.Id, new(characteristicId, NumericValue: value, UomCode: "KG", SampleId: sampleId), CancellationToken.None);
        return await quality.CompleteInspectionAsync(AtlasTenant, Guid.CreateVersion7(), inspection.Id, new("QA acceptance"), CancellationToken.None);
    }

    private sealed record StockRef(Guid ProductVariantId, Guid WarehouseId, Guid LocationId, Guid UnitId, decimal AvailableQuantity);
    private static async Task<StockRef> ReadStockAsync(string connection, InventoryService inventory)
    {
        await using var db = new NpgsqlConnection(connection); await db.OpenAsync(); await SetTenantAsync(db);
        await using var command = new NpgsqlCommand("select b.product_variant_id,b.warehouse_id,b.location_id,u.uom_id,b.on_hand_base_quantity-b.reserved_base_quantity from business_inventory.stock_balance b join foundation_catalog.product_uom u on u.tenant_id=b.tenant_id and u.variant_id=b.product_variant_id and u.is_base and u.status='Active' where b.tenant_id=$1 and b.legal_entity_id=$2 and b.stock_status='Available' and b.on_hand_base_quantity-b.reserved_base_quantity>=1 and exists(select 1 from business_quality.quality_plan p join business_quality.quality_plan_version v on v.tenant_id=p.tenant_id and v.plan_id=p.id and v.status='Active' join business_quality.quality_plan_applicability a on a.tenant_id=p.tenant_id and a.plan_id=p.id where p.tenant_id=b.tenant_id and p.legal_entity_id=b.legal_entity_id and p.event_type='Receiving' and a.product_variant_id=b.product_variant_id) order by b.product_variant_id limit 1", db);
        command.Parameters.AddWithValue(AtlasTenant.Value); command.Parameters.AddWithValue(AtlasEntity);
        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync()) return new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3), reader.GetDecimal(4));
        await reader.DisposeAsync();
        var product = (Guid)(await ScalarAsync(connection, "select a.product_variant_id from business_quality.quality_plan_applicability a join business_quality.quality_plan p on p.tenant_id=a.tenant_id and p.id=a.plan_id join foundation_catalog.product_uom u on u.tenant_id=a.tenant_id and u.variant_id=a.product_variant_id and u.is_base and u.status='Active' where a.tenant_id=$1 and p.legal_entity_id=$2 and p.event_type='Receiving' and a.product_variant_id is not null order by a.product_variant_id limit 1", AtlasTenant.Value, AtlasEntity))!;
        await using var warehouse = new NpgsqlCommand("select id,default_location_id from business_inventory.warehouse where tenant_id=$1 and legal_entity_id=$2 and status='Active' order by id limit 1", db); warehouse.Parameters.AddWithValue(AtlasTenant.Value); warehouse.Parameters.AddWithValue(AtlasEntity);
        await using var warehouseReader = await warehouse.ExecuteReaderAsync(); Assert.True(await warehouseReader.ReadAsync(), "QA acceptance requires a seeded Inventory warehouse."); var warehouseId = warehouseReader.GetGuid(0); var locationId = warehouseReader.GetGuid(1); await warehouseReader.DisposeAsync();
        var unit = (Guid)(await ScalarAsync(connection, "select uom_id from foundation_catalog.product_uom where tenant_id=$1 and variant_id=$2 and is_base and status='Active' order by effective_from desc limit 1", AtlasTenant.Value, product))!;
        await inventory.ReceiveAsync(AtlasTenant, Guid.CreateVersion7(), AtlasEntity, new ConfirmStockMovementRequest(product, warehouseId, locationId, unit, 10m, DateOnly.FromDateTime(DateTime.UtcNow.Date), DateOnly.FromDateTime(DateTime.UtcNow.Date), $"qa-seed:{Guid.NewGuid():N}", "quality-acceptance", Guid.CreateVersion7(), StockStatus.Available), Guid.CreateVersion7(), null, CancellationToken.None);
        return await ReadStockAsync(connection, inventory);
    }

    private static async Task<Guid> PlanVersionAsync(string connection) => (Guid)(await ScalarAsync(connection, "select v.id from business_quality.quality_plan_version v join business_quality.quality_plan p on p.tenant_id=v.tenant_id and p.id=v.plan_id where v.tenant_id=$1 and p.legal_entity_id=$2 and p.event_type='Receiving' and v.status='Active' order by v.version_no desc limit 1", AtlasTenant.Value, AtlasEntity))!;
    private static async Task<Modules.Quality.DTOs.InspectionView> CompleteInspectionAsync(QualityRepository quality, string connection, Guid inspectionId, decimal value)
    {
        var sample = (Guid)(await ScalarAsync(connection, "select id from business_quality.quality_sample where tenant_id=$1 and inspection_id=$2 order by sample_no limit 1", AtlasTenant.Value, inspectionId))!;
        var planVersion = (Guid)(await ScalarAsync(connection, "select plan_version_id from business_quality.inspection where tenant_id=$1 and id=$2", AtlasTenant.Value, inspectionId))!;
        var characteristic = (Guid)(await ScalarAsync(connection, "select characteristic_id from business_quality.quality_plan_characteristic where tenant_id=$1 and plan_version_id=$2 limit 1", AtlasTenant.Value, planVersion))!;
        await quality.AddResultAsync(AtlasTenant, Guid.CreateVersion7(), inspectionId, new(characteristic, NumericValue: value, UomCode: "KG", SampleId: sample), CancellationToken.None);
        return await quality.CompleteInspectionAsync(AtlasTenant, Guid.CreateVersion7(), inspectionId, new("QA-01 receiving acceptance"), CancellationToken.None);
    }
    private static async Task<(bool Success, QualityRestrictionView? Result)> AttemptReleaseAsync(QualityRepository quality, Guid restrictionId, ReleaseRestrictionRequest request)
    {
        try { return (true, await quality.ReleaseRestrictionAsync(AtlasTenant, Guid.CreateVersion7(), restrictionId, request, CancellationToken.None)); }
        catch (QualityException) { return (false, null); }
    }
    private static async Task<decimal> StockQuantityAsync(string connection, Guid product, Guid warehouse, Guid location, string status) => Convert.ToDecimal(await ScalarAsync(connection, "select coalesce(sum(on_hand_base_quantity),0) from business_inventory.stock_balance where tenant_id=$1 and legal_entity_id=$2 and product_variant_id=$3 and warehouse_id=$4 and location_id=$5 and stock_status=$6", AtlasTenant.Value, AtlasEntity, product, warehouse, location, status), CultureInfo.InvariantCulture);
    private static async Task<string> StockStatusAsync(string connection, StockRef stock, string status) => (string)(await ScalarAsync(connection, "select stock_status from business_inventory.stock_balance where tenant_id=$1 and legal_entity_id=$2 and product_variant_id=$3 and warehouse_id=$4 and location_id=$5 and stock_status=$6 and on_hand_base_quantity>0 limit 1", AtlasTenant.Value, AtlasEntity, stock.ProductVariantId, stock.WarehouseId, stock.LocationId, status))!;
    private static async Task<long> CountAsync(string connection, string sql, params object[] values) => Convert.ToInt64(await ScalarAsync(connection, sql, values), CultureInfo.InvariantCulture);
    private static async Task<int> ExecuteAsync(string connection, string sql, params object[] values)
    { await using var db = new NpgsqlConnection(connection); await db.OpenAsync(); await SetTenantAsync(db); await using var command = new NpgsqlCommand(sql, db); for (var i = 0; i < values.Length; i++) command.Parameters.AddWithValue(values[i]); return await command.ExecuteNonQueryAsync(); }
    private static async Task<object?> ScalarAsync(string connection, string sql, params object[] values)
    { await using var db = new NpgsqlConnection(connection); await db.OpenAsync(); await SetTenantAsync(db); await using var command = new NpgsqlCommand(sql, db); for (var i = 0; i < values.Length; i++) command.Parameters.AddWithValue(values[i]); return await command.ExecuteScalarAsync(); }
    private static async Task<object?> ScalarAsync(NpgsqlConnection db, string sql, params object[] values) { await using var command = new NpgsqlCommand(sql, db); for (var i = 0; i < values.Length; i++) command.Parameters.AddWithValue(values[i]); return await command.ExecuteScalarAsync(); }
    private static async Task<object?> ScalarUnscopedAsync(NpgsqlConnection db, string sql) { await using var command = new NpgsqlCommand(sql, db); return await command.ExecuteScalarAsync(); }
    private static async Task SetTenantAsync(NpgsqlConnection connection) { await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1::text,false)", connection); command.Parameters.AddWithValue(AtlasTenant.Value.ToString("D")); await command.ExecuteNonQueryAsync(); }
    private static WebApplicationFactory<Program> CreateFactory(string connection) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Development").ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["TODIR_DATABASE_CONNECTION"] = connection })).UseSetting("TODIR_DATABASE_CONNECTION", connection).UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
    private static Process StartQualityWorker(string connection, string host, int port, string user, string password, string vhost)
    {
        var repository = new DirectoryInfo(Environment.GetEnvironmentVariable("TODIR_REPOSITORY_ROOT") ?? AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "backend", "DbMigrator", "DbMigrator.csproj"))) repository = repository.Parent;
        Assert.NotNull(repository);
        var process = new Process { StartInfo = new ProcessStartInfo("dotnet", "run --configuration Release --project backend/Worker/Worker.csproj --no-build --no-restore --no-launch-profile") { WorkingDirectory = repository!.FullName, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
        process.StartInfo.Environment["DOTNET_ENVIRONMENT"] = "Development"; process.StartInfo.Environment["TODIR_DATABASE_CONNECTION"] = connection; process.StartInfo.Environment["TODIR_RABBITMQ_HOST"] = host; process.StartInfo.Environment["TODIR_RABBITMQ_PORT"] = port.ToString(CultureInfo.InvariantCulture); process.StartInfo.Environment["TODIR_RABBITMQ_USER"] = user; process.StartInfo.Environment["TODIR_RABBITMQ_PASSWORD"] = password; process.StartInfo.Environment["TODIR_RABBITMQ_VHOST"] = vhost; process.StartInfo.Environment["TODIR_RABBITMQ_CONSUMER"] = "true"; process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine(); return process;
    }
    private static void StopWorker(Process process) { try { if (!process.HasExited) process.Kill(true); } catch { } process.Dispose(); }
    private static async Task<bool> EventuallyAsync(Func<Task<bool>> predicate)
    { for (var i = 0; i < 40; i++) { if (await predicate()) return true; await Task.Delay(500); } return false; }
    private static string RequireConnection() => Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION") ?? Environment.GetEnvironmentVariable("TODIR_QUALITY_TEST_CONNECTION") ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION or TODIR_QUALITY_TEST_CONNECTION.");
}

[CollectionDefinition("Quality acceptance", DisableParallelization = true)]
public sealed class QualityAcceptanceGroup { }
