using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Modules.Service.DTOs;
using Modules.Service.Services;
using Npgsql;
using Shared.Primitives.Identifiers;
using Xunit;

namespace IntegrationTests;

public sealed class ServiceCrossModuleAcceptanceTests
{
    private static readonly TenantId AtlasTenant = new(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));

    [Fact(DisplayName = "SV50 Service real part, invoice, project and offline outcomes reconcile")]
    public async Task RealServiceOutcomesReconcileAcrossOwnedModules()
    {
        var connectionString = Environment.GetEnvironmentVariable("TODIR_SERVICE_TEST_CONNECTION")
            ?? Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Development")
            .UseSetting("TODIR_DATABASE_CONNECTION", connectionString)
            .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
        var service = factory.Services.GetRequiredService<ServiceService>();
        var actor = await FindActorAsync(connectionString, AtlasTenant);
        var entity = await ScalarAsync<Guid>(connectionString, AtlasTenant,
            "select id from platform.legal_entity where tenant_id=$1 and status='Active' order by id limit 1");
        var customer = await ScalarAsync<Guid>(connectionString, AtlasTenant,
            "select p.id from foundation_directory.party p join foundation_directory.party_role r on r.tenant_id=p.tenant_id and r.party_id=p.id where p.tenant_id=$1 and p.status='Active' and r.role_code='Customer' and r.status='Active' order by p.id limit 1");
        var installedBase = await ScalarAsync<Guid>(connectionString, AtlasTenant,
            "select id from business_service.installed_base_item where tenant_id=$1 and state='Active' order by id limit 1");
        var product = await ScalarAsync<Guid>(connectionString, AtlasTenant,
            "select product_variant_id from business_inventory.stock_balance where tenant_id=$1 and on_hand_base_quantity > 0 order by product_variant_id limit 1");
        var uom = await ScalarAsync<Guid>(connectionString, AtlasTenant,
            "select pu.uom_id from foundation_catalog.product_uom pu where pu.tenant_id=$1 and pu.variant_id=$2 and pu.is_base and pu.status='Active' limit 1", product);
        var warehouse = await ScalarAsync<Guid>(connectionString, AtlasTenant,
            "select id from business_inventory.warehouse where tenant_id=$1 and legal_entity_id=$2 and code='MAIN'", entity);
        var location = await ScalarAsync<Guid>(connectionString, AtlasTenant,
            "select default_location_id from business_inventory.warehouse where tenant_id=$1 and id=$2", warehouse);

        var work = await service.CreateWorkOrderAsync(AtlasTenant, actor.ActorId, Guid.CreateVersion7(),
            new CreateWorkOrderRequest(entity, "CustomerFieldService", "SV50 customer repair", "Acceptance repair",
                customer, installedBase, null, null, null, BillingOwner: "Service"), CancellationToken.None);
        var part = await service.AddPartAsync(AtlasTenant, actor.ActorId, work.Id,
            new CreatePartRequirementRequest(product, 1m, "PACK", "Acceptance part", entity, warehouse, location, uom), CancellationToken.None);

        var reserved = await service.ApplyPartActionAsync(AtlasTenant, actor.ActorId, part.Id, "reserve",
            new PartActionRequest(1m, IdempotencyKey: $"sv50-reserve:{work.Id:D}"), CancellationToken.None);
        await service.ApplyPartActionAsync(AtlasTenant, actor.ActorId, part.Id, "issue",
            new PartActionRequest(1m, IdempotencyKey: $"sv50-issue:{work.Id:D}"), CancellationToken.None);
        var consumed = await service.ApplyPartActionAsync(AtlasTenant, actor.ActorId, part.Id, "consume",
            new PartActionRequest(1m, IdempotencyKey: $"sv50-consume:{work.Id:D}"), CancellationToken.None);
        Assert.Equal(1m, consumed.ConsumedQuantity);
        Assert.Equal(3L, await ScalarAsync<long>(connectionString, AtlasTenant,
            "select count(*) from business_service.work_part_movement where tenant_id=$1 and part_requirement_id=$2 and state='Processed'", part.Id));
        Assert.True(await ScalarAsync<long>(connectionString, AtlasTenant,
            "select count(*) from business_inventory.inventory_operation where tenant_id=$1 and source_module='service' and source_document_id=$2 and status='Confirmed'", work.Id) >= 1);

        var charge = await service.CreateChargeAsync(AtlasTenant, actor.ActorId, work.Id,
            new CreateChargeRequest("Travel", "SV50 customer travel", 20m, 75m), CancellationToken.None);
        var requested = await service.RequestInvoiceAsync(AtlasTenant, actor.ActorId, actor.MembershipId,
            Guid.CreateVersion7(), charge.Id, CancellationToken.None);
        Assert.NotNull(requested.InvoiceId);
        var invoiced = await service.AllocateInvoiceAsync(AtlasTenant, actor.ActorId, charge.Id,
            new InvoiceAllocationRequest(requested.InvoiceId!.Value, 75m), CancellationToken.None);
        Assert.Equal("Invoiced", invoiced.State);
        Assert.Equal(1L, await ScalarAsync<long>(connectionString, AtlasTenant,
            "select count(*) from business_service.service_invoice_allocation where tenant_id=$1 and charge_id=$2 and invoice_id=$3", charge.Id, requested.InvoiceId.Value));

        var project = await ScalarAsync<Guid>(connectionString, AtlasTenant,
            "select id from business_projects.project where tenant_id=$1 and lifecycle_state='Active' order by id limit 1");
        var projectWork = await service.CreateWorkOrderAsync(AtlasTenant, actor.ActorId, Guid.CreateVersion7(),
            new CreateWorkOrderRequest(entity, "InternalMaintenance", "SV50 project maintenance", "Project-owned service",
                null, null, null, null, project, BillingOwner: "Projects"), CancellationToken.None);
        var projectCharge = await service.CreateChargeAsync(AtlasTenant, actor.ActorId, projectWork.Id,
            new CreateChargeRequest("Labor", "SV50 project labor", 30m, 30m), CancellationToken.None);
        Assert.Equal(1L, await ScalarAsync<long>(connectionString, AtlasTenant,
            "select count(*) from business_service.service_charge where tenant_id=$1 and id=$2 and project_cost_fact_id is not null", projectCharge.Id));
        Assert.Equal(1L, await ScalarAsync<long>(connectionString, AtlasTenant,
            "select count(*) from business_projects.project_cost_fact where tenant_id=$1 and project_id=$2 and source_module='service' and source_id=$3", project, projectCharge.Id.ToString("D")));

        var offline = await service.SubmitOfflineAsync(AtlasTenant, actor.ActorId, Guid.CreateVersion7(),
            new OfflineCommandRequest(Guid.CreateVersion7(), "checklist.answer", work.Id, null, null,
                Json("{\"workOrderId\":\"" + work.Id + "\",\"questionKey\":\"sv50-safety\",\"questionType\":\"Boolean\",\"answerJson\":\"true\",\"required\":true}")), CancellationToken.None);
        Assert.Equal("Processed", offline.State);
        Assert.DoesNotContain("accepted", offline.ResultJson ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static string Json(string value) => value;

    private static async Task<(Guid ActorId, Guid MembershipId)> FindActorAsync(string connectionString, TenantId tenant)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await SetTenantAsync(connection, tenant);
        await using var command = new NpgsqlCommand("select i.id,m.id from platform.identity i join platform.membership m on m.tenant_id=i.tenant_id and m.identity_id=i.id where i.tenant_id=$1 and i.email='admin@atlas.com' and i.status='Active' and m.status='Active' limit 1", connection);
        command.Parameters.AddWithValue(tenant.Value);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "Atlas administrator is required for Service cross-module acceptance.");
        return (reader.GetGuid(0), reader.GetGuid(1));
    }

    private static async Task<T> ScalarAsync<T>(string connectionString, TenantId tenant, string sql, params object[] values)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await SetTenantAsync(connection, tenant);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue(tenant.Value);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        var result = await command.ExecuteScalarAsync();
        Assert.NotNull(result);
        return (T)Convert.ChangeType(result, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, TenantId tenant)
    {
        await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection);
        command.Parameters.AddWithValue(tenant.Value.ToString("D"));
        await command.ExecuteNonQueryAsync();
    }
}
