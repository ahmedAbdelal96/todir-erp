extern alias WorkerAssembly;

using System.Text.Json;
using Core.Messaging;
using Core.Services;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Modules.FixedAssets.Services;
using Modules.Invoicing.Services;
using Modules.Projects.DTOs;
using Modules.Projects.Repositories;
using Modules.Projects.Services;
using Npgsql;
using Shared.Contracts.Projects;
using Shared.Contracts.Invoicing;
using Shared.Primitives.Identifiers;
using WorkerAssembly::Worker;
using Xunit;

namespace KernelIntegrationTests;

public sealed class ProjectsCrossModuleAcceptanceTests
{
    private static readonly TenantId AtlasTenant = new(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));
    private static readonly Guid AtlasEntity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");

    [Fact(DisplayName = "PJ43 real Projects to Fixed Assets handoff creates one capitalization candidate and suppresses retry")]
    public async Task RealCapitalHandoffCreatesFixedAssetCandidate()
    {
        var connectionString = RequireConnection();
        using var factory = CreateFactory(connectionString);
        var projectService = factory.Services.GetRequiredService<ProjectService>();
        var fixedAssets = factory.Services.GetRequiredService<FixedAssetService>();
        var actor = Guid.CreateVersion7();
        var project = await projectService.CreateAsync(AtlasTenant, actor, actor, new(AtlasEntity, $"PJ43-REAL-{Guid.NewGuid():N}", "CapitalProject", null, null, new(2026, 1, 1), null), Guid.CreateVersion7(), CancellationToken.None);
        var cost = await projectService.RecordCostFactAsync(AtlasTenant, actor, new(project.Id, null, "expenses", "ExpenseFinalizedV1", $"capex-real:{Guid.NewGuid():N}", null, "Capitalizable", "EGP", 500m, "EGP", 500m, 1m, new(2026, 1, 3), $"pj43-real:{Guid.NewGuid():N}", null, AtlasEntity), Guid.CreateVersion7(), CancellationToken.None);
        var handoff = await projectService.CreateCapitalHandoffAsync(AtlasTenant, actor, project.Id, new(cost.Id, 500m, "EGP", $"pj43-real-handoff:{Guid.NewGuid():N}"), Guid.CreateVersion7(), CancellationToken.None);
        var message = await ReadOutboxAsync(connectionString, AtlasTenant, "ProjectCapitalHandoffRequestedV1", project.Id);

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var inbox = new NpgsqlInboxStore(dataSource);
        await using var provider = new ServiceCollection()
            .AddSingleton(fixedAssets)
            .AddSingleton<IInboxStore>(inbox)
            .AddMassTransitTestHarness(config => config.AddConsumer<ProjectCapitalHandoffConsumer>())
            .BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(message);
            Assert.True(await EventuallyAsync(async () => (await fixedAssets.ListCapitalizationCandidatesAsync(AtlasTenant, AtlasEntity, null, 200, CancellationToken.None)).Any(candidate => candidate.SourceReference == cost.Id.ToString("D"))));
            await harness.Bus.Publish(message);
            Assert.True(await EventuallyAsync(async () => Convert.ToInt64(await ScalarAsync(connectionString, "select count(*) from business_assets.asset_capitalization_candidate where tenant_id=$1 and economic_source_key=$2", AtlasTenant.Value, $"project-handoff:{handoff.Id:D}")!, System.Globalization.CultureInfo.InvariantCulture) == 1));
            var candidate = (await fixedAssets.ListCapitalizationCandidatesAsync(AtlasTenant, AtlasEntity, null, 200, CancellationToken.None)).Single(item => item.SourceReference == cost.Id.ToString("D"));
            Assert.Equal("PendingReview", candidate.Status);
            Assert.Equal(500m, candidate.EligibleAmount);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact(DisplayName = "PJ44 real Projects to Invoicing flow creates and finalizes an authoritative invoice")]
    public async Task RealProjectInvoiceFinalizesAndProjectsConsumesEvent()
    {
        var connectionString = RequireConnection();
        using var factory = CreateFactory(connectionString);
        var projectService = factory.Services.GetRequiredService<ProjectService>();
        var invoicing = factory.Services.GetRequiredService<InvoicingService>();
        var actor = Guid.CreateVersion7();
        var customer = await FindPartyAsync(connectionString, AtlasTenant);
        var project = await projectService.CreateAsync(AtlasTenant, actor, actor, new(AtlasEntity, $"PJ44-REAL-{Guid.NewGuid():N}", "CustomerProject", customer, null, new(2026, 1, 1), null, BillingModel: "FixedPrice", CurrencyCode: "EGP"), Guid.CreateVersion7(), CancellationToken.None);
        var charge = await projectService.CreateChargeAsync(AtlasTenant, actor, new(project.Id, "Milestone", $"milestone-real:{Guid.NewGuid():N}", new(2026, 1, 31), "Accepted milestone", "EGP", 600m, 1000m, $"pj44-real-charge:{Guid.NewGuid():N}"), CancellationToken.None);
        var invoiceRequest = await projectService.RequestInvoiceAsync(AtlasTenant, actor, actor, new(project.Id, [charge.Id], "EGP", 1000m, $"pj44-real-invoice:{Guid.NewGuid():N}"), Guid.CreateVersion7(), CancellationToken.None);
        Assert.NotNull(invoiceRequest.InvoiceId);
        var draft = await invoicing.GetDraftAsync(AtlasTenant, AtlasEntity, invoiceRequest.InvoiceId.Value, CancellationToken.None);
        var finalized = await invoicing.FinalizeAsync(AtlasTenant, actor, actor, invoiceRequest.InvoiceId.Value, AtlasEntity, new(draft.Version, $"pj44-real-finalize:{Guid.NewGuid():N}"), Guid.CreateVersion7(), CancellationToken.None);
        Assert.Equal(invoiceRequest.InvoiceId.Value, finalized.InvoiceId);
        var message = await ReadOutboxAsync(connectionString, AtlasTenant, "InvoiceFinalizedV1", invoiceRequest.InvoiceId.Value);

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var inbox = new NpgsqlInboxStore(dataSource);
        var evidence = new ProjectsIntegrationRepository(dataSource);
        await using var provider = new ServiceCollection()
            .AddSingleton<IProjectSourceIntegrationCapabilities>(projectService)
            .AddSingleton(evidence)
            .AddSingleton<IInboxStore>(inbox)
            .AddMassTransitTestHarness(config => config.AddConsumer<ProjectsSourceEventConsumer>())
            .BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(message);
            Assert.True(await EventuallyAsync(async () => (string?)await ScalarAsync(connectionString, "select state from business_projects.project_invoice_request where tenant_id=$1 and id=$2", AtlasTenant.Value, invoiceRequest.Id) == "Completed"));
            Assert.Equal("Invoiced", await ScalarAsync(connectionString, "select state from business_projects.project_billable_charge where tenant_id=$1 and id=$2", AtlasTenant.Value, charge.Id));
            Assert.Equal(finalized.InvoiceId, await ScalarAsync(connectionString, "select invoice_id from business_projects.project_invoice_allocation where tenant_id=$1 and invoice_request_id=$2", AtlasTenant.Value, invoiceRequest.Id));
            Assert.Equal("Finalized", await ScalarAsync(connectionString, "select lifecycle_state from business_invoicing.invoice where tenant_id=$1 and id=$2", AtlasTenant.Value, finalized.InvoiceId));
            Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(connectionString, "select count(*) from business_projects.project_integration_event where tenant_id=$1 and message_id=$2", AtlasTenant.Value, message.MessageId), System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact(DisplayName = "PJ15 real supplier invoice actual reduces project commitment without double count")]
    public async Task RealSupplierInvoiceActualReconcilesProjectCommitment()
    {
        var connectionString = RequireConnection();
        using var factory = CreateFactory(connectionString);
        var projectService = factory.Services.GetRequiredService<ProjectService>();
        var invoicing = factory.Services.GetRequiredService<InvoicingService>();
        var actor = Guid.CreateVersion7();
        var customer = await FindPartyAsync(connectionString, AtlasTenant);
        var supplier = await FindPartyAsync(connectionString, AtlasTenant, "Supplier");
        var project = await projectService.CreateAsync(AtlasTenant, actor, actor, new(AtlasEntity, $"PJ15-REAL-{Guid.NewGuid():N}", "CustomerProject", customer, null, new(2026, 1, 1), null, BillingModel: "CostPlus", CurrencyCode: "EGP"), Guid.CreateVersion7(), CancellationToken.None);
        var purchaseOrderId = Guid.CreateVersion7();
        await projectService.RecordCommitmentAsync(AtlasTenant, new(project.Id, "purchasing", purchaseOrderId.ToString("D"), new(2026, 1, 15), "EGP", 1000m, 0m, $"pj15-real-commitment:{Guid.NewGuid():N}", AtlasEntity), CancellationToken.None);
        var source = new InvoiceSourceDocumentContract("purchasing", purchaseOrderId, $"SupplierInvoice:Project:{project.Id:D}", $"pj15-real-invoice:{Guid.NewGuid():N}", "Payable", AtlasEntity, null, supplier, null,
            new(2026, 1, 15), new(2026, 1, 15), new(2026, 1, 15), null, "EGP", "Exclusive", null, $"SUP-PJ15-{Guid.NewGuid():N}", null,
            [new InvoiceSourceLineContract(null, null, "EA", "Supplier project actual", "1", "300", null, null, "pj15-line")]);
        var invoiceId = await invoicing.CreateDraftFromSourceAsync(AtlasTenant, actor, actor, source, Guid.CreateVersion7(), CancellationToken.None);
        var draft = await invoicing.GetDraftAsync(AtlasTenant, AtlasEntity, invoiceId, CancellationToken.None);
        var finalized = await invoicing.FinalizeAsync(AtlasTenant, actor, actor, invoiceId, AtlasEntity, new(draft.Version, $"pj15-real-finalize:{Guid.NewGuid():N}"), Guid.CreateVersion7(), CancellationToken.None);
        var message = await ReadOutboxAsync(connectionString, AtlasTenant, "InvoiceFinalizedV1", invoiceId);
        using var payload = JsonDocument.Parse(message.Payload);
        var actualAmount = payload.RootElement.GetProperty("amount").GetDecimal();
        Assert.InRange(actualAmount, 0.01m, 999.99m);

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var inbox = new NpgsqlInboxStore(dataSource);
        var evidence = new ProjectsIntegrationRepository(dataSource);
        await using var provider = new ServiceCollection()
            .AddSingleton<IProjectSourceIntegrationCapabilities>(projectService)
            .AddSingleton(evidence)
            .AddSingleton<IInboxStore>(inbox)
            .AddMassTransitTestHarness(config => config.AddConsumer<ProjectsSourceEventConsumer>())
            .BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(message);
            Assert.True(await EventuallyAsync(async () => Convert.ToInt64(await ScalarAsync(connectionString, "select count(*) from business_projects.project_cost_fact where tenant_id=$1 and project_id=$2 and source_module='invoicing' and source_id=$3", AtlasTenant.Value, project.Id, finalized.InvoiceId.ToString("D")), System.Globalization.CultureInfo.InvariantCulture) == 1));
            Assert.Equal(actualAmount, Convert.ToDecimal(await ScalarAsync(connectionString, "select actualized_amount from business_projects.project_commitment where tenant_id=$1 and project_id=$2 and source_module='purchasing' and source_id=$3", AtlasTenant.Value, project.Id, purchaseOrderId.ToString("D")), System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(1000m - actualAmount, Convert.ToDecimal(await ScalarAsync(connectionString, "select open_amount from business_projects.project_commitment where tenant_id=$1 and project_id=$2 and source_module='purchasing' and source_id=$3", AtlasTenant.Value, project.Id, purchaseOrderId.ToString("D")), System.Globalization.CultureInfo.InvariantCulture));
            await harness.Bus.Publish(message);
            await Task.Delay(250);
            Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(connectionString, "select count(*) from business_projects.project_cost_fact where tenant_id=$1 and project_id=$2 and source_module='invoicing' and source_id=$3", AtlasTenant.Value, project.Id, finalized.InvoiceId.ToString("D")), System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            await harness.Stop();
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
        .UseEnvironment("Development")
        .UseSetting("TODIR_DATABASE_CONNECTION", connectionString)
        .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));

    private static string RequireConnection() => Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION") ?? Environment.GetEnvironmentVariable("TODIR_PROJECTS_TEST_CONNECTION") ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION or TODIR_PROJECTS_TEST_CONNECTION.");

    private static async Task<OutboxPublishedMessage> ReadOutboxAsync(string connectionString, TenantId tenant, string messageType, Guid causationId)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(); await SetTenantAsync(connection, tenant);
        await using var command = new NpgsqlCommand("select id,message_type,payload::text,tenant_id,occurred_at,correlation_id,causation_id,source_module,trace_parent,attempts from platform.outbox_message where tenant_id=$1 and message_type=$2 and causation_id=$3 order by occurred_at desc limit 1", connection); command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(messageType); command.Parameters.AddWithValue(causationId);
        await using var reader = await command.ExecuteReaderAsync(); Assert.True(await reader.ReadAsync(), $"Outbox message {messageType} was not persisted."); return new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), new TenantId(reader.GetGuid(3)), reader.GetFieldValue<DateTimeOffset>(4), reader.GetGuid(5), reader.GetGuid(6), reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8), reader.GetInt32(9));
    }

    private static async Task<Guid> FindPartyAsync(string connectionString, TenantId tenant, string roleCode = "Customer")
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(); await SetTenantAsync(connection, tenant); return (Guid)(await ScalarAsync(connection, "select p.id from foundation_directory.party p join foundation_directory.party_role r on r.tenant_id=p.tenant_id and r.party_id=p.id where p.tenant_id=$1 and p.status='Active' and r.role_code=$2 and r.status='Active' order by p.id limit 1", tenant.Value, roleCode))!;
    }

    private static async Task<object?> ScalarAsync(string connectionString, string sql, params object[] values)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(); await SetTenantAsync(connection, AtlasTenant); return await ScalarAsync(connection, sql, values);
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql, params object[] values)
    {
        await using var command = new NpgsqlCommand(sql, connection); for (var index = 0; index < values.Length; index++) command.Parameters.AddWithValue(values[index]); return await command.ExecuteScalarAsync();
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, TenantId tenant)
    {
        await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection); command.Parameters.AddWithValue(tenant.Value.ToString("D")); await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> EventuallyAsync(Func<Task<bool>> predicate)
    {
        for (var attempt = 0; attempt < 40; attempt++) { if (await predicate()) return true; await Task.Delay(100); }
        return false;
    }
}
