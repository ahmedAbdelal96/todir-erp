using Core.Services;
using Core.Messaging;
using Core.Transactions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Modules.Projects.DTOs;
using Modules.Projects.Repositories;
using Modules.Projects.Services;
using Npgsql;
using Shared.Contracts.Invoicing;
using Shared.Contracts.Projects;
using Shared.Primitives.Identifiers;
using Worker;
using Xunit;

namespace Modules.Projects.Tests;

/// <summary>Persisted PJ40-PJ47 acceptance vectors. These tests require the isolated PostgreSQL test database.</summary>
public sealed class ProjectAcceptanceScenarioTests
{
    private static readonly TenantId AtlasTenant = new(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));
    private static readonly TenantId DeltaTenant = new(Guid.Parse("3f93b180-d6a9-3e50-9c92-6be0607e96eb"));
    private static readonly Guid AtlasEntity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");

    [Fact(DisplayName = "PJ40 simple project persists create, task, team, milestone, and health workflow")]
    public async Task Pj40SimpleProject()
    {
        await using var database = OpenDatabase();
        var service = CreateService(database);
        var actor = Guid.CreateVersion7();
        var project = await service.CreateAsync(AtlasTenant, actor, actor, new(AtlasEntity, $"PJ40-{Guid.NewGuid():N}", "InternalProject", null, null, new(2026, 1, 1), null), Guid.CreateVersion7(), CancellationToken.None);
        var task = await service.AddTaskAsync(AtlasTenant, actor, project.Id, new("Prepare delivery plan", null), CancellationToken.None);
        var team = await service.AddTeamMemberAsync(AtlasTenant, actor, project.Id, new(null, actor, "ProjectManager"), CancellationToken.None);
        var milestone = await service.AddMilestoneAsync(AtlasTenant, actor, project.Id, new("Plan complete", new(2026, 1, 31), task.Id), CancellationToken.None);
        var reached = await service.MilestoneActionAsync(AtlasTenant, actor, milestone.Id, "reach", new(), CancellationToken.None);
        var accepted = await service.MilestoneActionAsync(AtlasTenant, actor, milestone.Id, "accept", new("PM-ACCEPTED"), CancellationToken.None);
        var tasks = await service.ListTasksAsync(AtlasTenant, project.Id, 1, 50, CancellationToken.None);
        var milestones = await service.ListMilestonesAsync(AtlasTenant, project.Id, 1, 50, CancellationToken.None);
        var teamPage = await service.ListTeamAsync(AtlasTenant, project.Id, 1, 50, CancellationToken.None);

        Assert.Equal(task.Id, tasks.Items.Single().Id);
        Assert.Equal(team.Id, teamPage.Items.Single().Id);
        Assert.Equal("Reached", reached.Status);
        Assert.Equal("Accepted", accepted.Status);
        Assert.Equal("Accepted", milestones.Items.Single().Status);
        Assert.True(Convert.ToInt64(await ScalarAsync(database, "select count(*) from business_projects.project_audit_event where tenant_id=$1 and project_id=$2", AtlasTenant.Value, project.Id), System.Globalization.CultureInfo.InvariantCulture) > 0);
    }

    [Fact(DisplayName = "PJ41 professional services persists approved time, labor cost, and billable charge")]
    public async Task Pj41ProfessionalServices()
    {
        await using var database = OpenDatabase();
        var service = CreateService(database);
        var actor = Guid.CreateVersion7();
        var customer = await FindPartyAsync(database, AtlasTenant);
        var project = await service.CreateAsync(AtlasTenant, actor, actor, new(AtlasEntity, $"PJ41-{Guid.NewGuid():N}", "CustomerProject", customer, null, new(2026, 1, 1), null, BillingModel: "TimeAndMaterials", CurrencyCode: "EGP"), Guid.CreateVersion7(), CancellationToken.None);
        await service.CreateRateCardAsync(AtlasTenant, actor, new(AtlasEntity, $"PJ41-RATE-{Guid.NewGuid():N}", "PJ41 rates", "EGP", new(2026, 1, 1), null, [new("Consultant", actor, 100m, 150m)]), CancellationToken.None);
        var member = await service.AddTeamMemberAsync(AtlasTenant, actor, project.Id, new(actor, null, "Consultant", 100m, new(2026, 1, 1)), CancellationToken.None);
        var time = await service.CreateTimeAsync(AtlasTenant, actor, new(project.Id, null, actor, new(2026, 1, 2), 120, "Delivery", true, $"pj41:{Guid.NewGuid():N}"), CancellationToken.None);
        time = await service.TimeActionAsync(AtlasTenant, actor, time.Id, "submit", new(time.Version), CancellationToken.None);
        time = await service.TimeActionAsync(AtlasTenant, actor, time.Id, "approve", new(time.Version), CancellationToken.None);
        time = await service.TimeActionAsync(AtlasTenant, actor, time.Id, "finalize", new(time.Version), CancellationToken.None);
        var page = await service.ListTimeAsync(AtlasTenant, project.Id, 1, 50, CancellationToken.None);

        Assert.Equal(actor, member.EmployeeId);
        Assert.Equal("Finalized", time.State);
        Assert.Equal(200m, time.CostAmount);
        Assert.Equal(300m, time.BillingAmount);
        Assert.Single(page.Items);
        Assert.Equal(1L, await ScalarAsync(database, "select count(*) from business_projects.project_cost_fact where tenant_id=$1 and project_id=$2 and source_type='ProjectTimeEntry'", AtlasTenant.Value, project.Id));
        Assert.Equal(1L, await ScalarAsync(database, "select count(*) from business_projects.project_billable_charge where tenant_id=$1 and project_id=$2", AtlasTenant.Value, project.Id));
    }

    [Fact(DisplayName = "PJ42 project accountant persists immutable source cost, idempotency, and correction")]
    public async Task Pj42AccountantCostFact()
    {
        await using var database = OpenDatabase();
        var service = CreateService(database);
        var actor = Guid.CreateVersion7();
        var project = await service.CreateAsync(AtlasTenant, actor, actor, new(AtlasEntity, $"PJ42-{Guid.NewGuid():N}", "InternalProject", null, null, new(2026, 1, 1), null), Guid.CreateVersion7(), CancellationToken.None);
        var key = $"pj42:{Guid.NewGuid():N}";
        var request = new RecordCostFactRequest(project.Id, null, "expenses", "ExpenseFinalizedV1", $"expense:{Guid.NewGuid():N}", null, "Expense", "EGP", 125m, "EGP", 125m, 1m, new(2026, 1, 2), key, null, AtlasEntity);
        var first = await service.RecordCostFactAsync(AtlasTenant, actor, request, Guid.CreateVersion7(), CancellationToken.None);
        var retry = await service.RecordCostFactAsync(AtlasTenant, actor, request, Guid.CreateVersion7(), CancellationToken.None);
        var correction = await service.RecordCostFactAsync(AtlasTenant, actor, request with { OriginalAmount = 140m, BaseAmount = 140m, IdempotencyKey = $"{key}:correction", CorrectionOfId = first.Id }, Guid.CreateVersion7(), CancellationToken.None);

        Assert.Equal(first.Id, retry.Id);
        Assert.Equal("Corrected", (string?)await ScalarAsync(database, "select state from business_projects.project_cost_fact where tenant_id=$1 and id=$2", AtlasTenant.Value, first.Id));
        Assert.Equal(2L, await ScalarAsync(database, "select count(*) from business_projects.project_cost_fact where tenant_id=$1 and project_id=$2", AtlasTenant.Value, project.Id));
        Assert.Equal(140m, correction.BaseAmount);
    }

    [Fact(DisplayName = "PJ43 capital project persists cost fact and one CIP handoff request")]
    public async Task Pj43CapitalHandoff()
    {
        await using var database = OpenDatabase();
        var service = CreateService(database);
        var actor = Guid.CreateVersion7();
        var project = await service.CreateAsync(AtlasTenant, actor, actor, new(AtlasEntity, $"PJ43-{Guid.NewGuid():N}", "CapitalProject", null, null, new(2026, 1, 1), null), Guid.CreateVersion7(), CancellationToken.None);
        var cost = await service.RecordCostFactAsync(AtlasTenant, actor, new(project.Id, null, "expenses", "ExpenseFinalizedV1", $"capex:{Guid.NewGuid():N}", null, "Capitalizable", "EGP", 500m, "EGP", 500m, 1m, new(2026, 1, 3), $"pj43:{Guid.NewGuid():N}", null, AtlasEntity), Guid.CreateVersion7(), CancellationToken.None);
        var handoff = await service.CreateCapitalHandoffAsync(AtlasTenant, actor, project.Id, new(cost.Id, 500m, "EGP", $"pj43-handoff:{Guid.NewGuid():N}"), Guid.CreateVersion7(), CancellationToken.None);

        Assert.Equal("Requested", handoff.State);
        Assert.Equal(1L, await ScalarAsync(database, "select count(*) from business_projects.project_capital_handoff where tenant_id=$1 and id=$2", AtlasTenant.Value, handoff.Id));
        Assert.Equal(0L, await ScalarAsync(database, "select count(*) from information_schema.tables where table_schema='business_projects' and table_name='fixed_asset'"));
    }

    [Fact(DisplayName = "PJ44 billing persists a separate charge and partial invoice allocation")]
    public async Task Pj44SalesBilling()
    {
        await using var database = OpenDatabase();
        var service = CreateService(database, new StubInvoicingCapability());
        var actor = Guid.CreateVersion7();
        var customer = await FindPartyAsync(database, AtlasTenant);
        var project = await service.CreateAsync(AtlasTenant, actor, actor, new(AtlasEntity, $"PJ44-{Guid.NewGuid():N}", "CustomerProject", customer, null, new(2026, 1, 1), null, BillingModel: "FixedPrice", CurrencyCode: "EGP"), Guid.CreateVersion7(), CancellationToken.None);
        var charge = await service.CreateChargeAsync(AtlasTenant, actor, new(project.Id, "Milestone", $"milestone:{Guid.NewGuid():N}", new(2026, 1, 31), "Accepted milestone", "EGP", 600m, 1000m, $"pj44-charge:{Guid.NewGuid():N}"), CancellationToken.None);
        var invoice = await service.RequestInvoiceAsync(AtlasTenant, actor, actor, new(project.Id, [charge.Id], "EGP", 1000m, $"pj44-invoice:{Guid.NewGuid():N}"), Guid.CreateVersion7(), CancellationToken.None);
        invoice = await service.ApplyInvoiceResultAsync(AtlasTenant, actor, invoice.Id, new("Completed", Guid.CreateVersion7(), 600m), CancellationToken.None);

        Assert.Equal("Completed", invoice.State);
        Assert.Equal(600m, await ScalarAsync(database, "select allocated_amount from business_projects.project_invoice_allocation where tenant_id=$1 and invoice_request_id=$2", AtlasTenant.Value, invoice.Id));
        Assert.Equal(1L, await ScalarAsync(database, "select count(*) from business_projects.project_billable_charge where tenant_id=$1 and id=$2 and state='PartiallyInvoiced'", AtlasTenant.Value, charge.Id));
    }

    [Fact(DisplayName = "PJ45 financial summary reconciles budget, commitment, actual, forecast, billing, and basis")]
    public async Task Pj45Reconciliation()
    {
        await using var database = OpenDatabase();
        var service = CreateService(database);
        var actor = Guid.CreateVersion7();
        var customer = await FindPartyAsync(database, AtlasTenant);
        var project = await service.CreateAsync(AtlasTenant, actor, actor, new(AtlasEntity, $"PJ45-{Guid.NewGuid():N}", "CustomerProject", customer, null, new(2026, 1, 1), null, BillingModel: "FixedPrice", CurrencyCode: "EGP"), Guid.CreateVersion7(), CancellationToken.None);
        await service.CreateBudgetAsync(AtlasTenant, actor, project.Id, new("Approved budget", "EGP", 1000m), CancellationToken.None);
        await service.RecordCommitmentAsync(AtlasTenant, new(project.Id, "purchasing", $"po:{Guid.NewGuid():N}", new(2026, 1, 2), "EGP", 300m, 0m, $"pj45-po:{Guid.NewGuid():N}", AtlasEntity), CancellationToken.None);
        await service.RecordCostFactAsync(AtlasTenant, actor, new(project.Id, null, "expenses", "ExpenseFinalizedV1", $"cost:{Guid.NewGuid():N}", null, "Expense", "EGP", 200m, "EGP", 200m, 1m, new(2026, 1, 3), $"pj45-cost:{Guid.NewGuid():N}", null, AtlasEntity), Guid.CreateVersion7(), CancellationToken.None);
        await service.CreateForecastAsync(AtlasTenant, actor, project.Id, new(150m, true, "Remaining delivery estimate"), CancellationToken.None);
        await service.CreateBillingRuleAsync(AtlasTenant, actor, project.Id, new("FixedPrice", "EGP", 1200m), CancellationToken.None);
        var summary = await service.GetSummaryAsync(AtlasTenant, project.Id, CancellationToken.None);

        Assert.Equal(1000m, summary.Budget);
        Assert.Equal(200m, summary.ActualCost);
        Assert.Equal(300m, summary.OpenCommitment);
        Assert.Equal(650m, summary.Eac);
        Assert.Equal("ContractValue", summary.RevenueBasis);
        Assert.Equal(1200m, summary.RevenueBasisAmount);
        Assert.Equal(550m, summary.ExpectedMargin);
    }

    [Fact(DisplayName = "PJ46 cross-tenant project execution is isolated in PostgreSQL")]
    public async Task Pj46CrossModuleAllocation()
    {
        await using var database = OpenDatabase();
        var service = CreateService(database);
        var actor = Guid.CreateVersion7();
        var project = await service.CreateAsync(AtlasTenant, actor, actor, new(AtlasEntity, $"PJ46-{Guid.NewGuid():N}", "InternalProject", null, null, new(2026, 1, 1), null), Guid.CreateVersion7(), CancellationToken.None);
        await using var connection = await database.OpenConnectionAsync();
        await SetTenantAsync(connection, DeltaTenant);
        Assert.Equal(0L, await ScalarAsync(connection, "select count(*) from business_projects.project where tenant_id=$1 and id=$2", DeltaTenant.Value, project.Id));
        await SetTenantAsync(connection, AtlasTenant);
        Assert.Equal(1L, await ScalarAsync(connection, "select count(*) from business_projects.project where tenant_id=$1 and id=$2", AtlasTenant.Value, project.Id));
    }

    [Fact(DisplayName = "PJ47 source event consumer persists valid costs, rejects defaults, and is idempotent/order-safe")]
    public async Task Pj47SourceEventConsumer()
    {
        await using var database = OpenDatabase();
        var actor = Guid.CreateVersion7();
        var projectService = CreateService(database);
        var project = await projectService.CreateAsync(AtlasTenant, actor, actor,
            new(AtlasEntity, $"PJ47-{Guid.NewGuid():N}", "InternalProject", null, null, new(2026, 1, 1), null), Guid.CreateVersion7(), CancellationToken.None);
        var sourceId = Guid.CreateVersion7();
        var occurredAt = DateTimeOffset.UtcNow;
        var valid = new OutboxPublishedMessage(Guid.CreateVersion7(), "ExpenseFinalizedV1",
            System.Text.Json.JsonSerializer.Serialize(new { projectId = project.Id, legalEntityId = AtlasEntity, expenseId = sourceId, currencyCode = "EGP", baseAmount = 125m }),
            AtlasTenant, occurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), "expenses", null);
        var missingCurrency = new OutboxPublishedMessage(Guid.CreateVersion7(), "ExpenseFinalizedV1",
            System.Text.Json.JsonSerializer.Serialize(new { projectId = project.Id, legalEntityId = AtlasEntity, expenseId = Guid.CreateVersion7(), baseAmount = 99m }),
            AtlasTenant, occurredAt.AddSeconds(2), Guid.CreateVersion7(), Guid.CreateVersion7(), "expenses", null);
        var olderReplay = valid with { MessageId = Guid.CreateVersion7(), OccurredAt = occurredAt.AddSeconds(-2) };

        await using var provider = new ServiceCollection()
            .AddSingleton(database)
            .AddSingleton(projectService)
            .AddSingleton<IProjectSourceIntegrationCapabilities>(projectService)
            .AddSingleton<ProjectsIntegrationRepository>()
            .AddSingleton<IInboxStore, NpgsqlInboxStore>()
            .AddMassTransitTestHarness(config => config.AddConsumer<ProjectsSourceEventConsumer>())
            .BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(valid);
            Assert.True(await EventuallyAsync(async () =>
                Convert.ToInt64(await ScalarAsync(database, "select count(*) from business_projects.project_cost_fact where tenant_id=$1 and project_id=$2 and source_id=$3", AtlasTenant.Value, project.Id, sourceId.ToString("D")), System.Globalization.CultureInfo.InvariantCulture) == 1));
            await harness.Bus.Publish(valid);
            await harness.Bus.Publish(missingCurrency);
            await harness.Bus.Publish(olderReplay);

            Assert.True(await EventuallyAsync(async () =>
                Convert.ToInt64(await ScalarAsync(database, "select count(*) from business_projects.project_integration_event where tenant_id=$1 and message_id in ($2,$3)", AtlasTenant.Value, missingCurrency.MessageId, olderReplay.MessageId), System.Globalization.CultureInfo.InvariantCulture) == 2));
            Assert.Equal("Applied", await ScalarAsync(database, "select status from business_projects.project_integration_event where tenant_id=$1 and message_id=$2", AtlasTenant.Value, valid.MessageId));
            Assert.Equal("Ignored", await ScalarAsync(database, "select status from business_projects.project_integration_event where tenant_id=$1 and message_id=$2", AtlasTenant.Value, missingCurrency.MessageId));
            Assert.Equal("source_event_missing_currency", await ScalarAsync(database, "select reason from business_projects.project_integration_event where tenant_id=$1 and message_id=$2", AtlasTenant.Value, missingCurrency.MessageId));
            Assert.Equal("source_event_out_of_order", await ScalarAsync(database, "select reason from business_projects.project_integration_event where tenant_id=$1 and message_id=$2", AtlasTenant.Value, olderReplay.MessageId));
        }
        finally
        {
            await harness.Stop();
        }
    }

    private static NpgsqlDataSource OpenDatabase()
    {
        var value = Environment.GetEnvironmentVariable("TODIR_PROJECTS_TEST_CONNECTION") ?? Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION");
        Assert.False(string.IsNullOrWhiteSpace(value), "TODIR_PROJECTS_TEST_CONNECTION or TODIR_DATABASE_CONNECTION is required.");
        return NpgsqlDataSource.Create(value!);
    }

    private static ProjectService CreateService(NpgsqlDataSource database, IInvoicingSourceCapabilities? invoicing = null)
        => new(new ProjectsRepository(new NpgsqlTransactionCoordinator(database), new NpgsqlSequenceAllocator(database)), invoicing ?? new StubInvoicingCapability());

    private static async Task<Guid> FindPartyAsync(NpgsqlDataSource database, TenantId tenant)
    {
        await using var connection = await database.OpenConnectionAsync(); await SetTenantAsync(connection, tenant);
        return (Guid)(await ScalarAsync(connection, "select id from foundation_directory.party where tenant_id=$1 order by id limit 1", tenant.Value))!;
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, TenantId tenant)
    {
        await using var command = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection);
        command.Parameters.AddWithValue(tenant.Value.ToString("D")); await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(NpgsqlDataSource database, string sql, params object[] values)
    {
        await using var connection = await database.OpenConnectionAsync(); await SetTenantAsync(connection, AtlasTenant); return await ScalarAsync(connection, sql, values);
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql, params object[] values)
    {
        await using var command = new NpgsqlCommand(sql, connection); for (var index = 0; index < values.Length; index++) command.Parameters.AddWithValue(values[index]); return await command.ExecuteScalarAsync();
    }

    private static async Task<bool> EventuallyAsync(Func<Task<bool>> predicate)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            if (await predicate()) return true;
            await Task.Delay(100);
        }
        return false;
    }

    private sealed class StubInvoicingCapability : IInvoicingSourceCapabilities
    {
        public Task<Guid> CreateDraftFromSourceAsync(TenantId tenantId, Guid actorId, Guid membershipId, InvoiceSourceDocumentContract request, Guid correlationId, CancellationToken cancellationToken = default)
            => Task.FromResult(Guid.CreateVersion7());
    }
}
