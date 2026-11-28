extern alias WorkerAssembly;

using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Core.Identity;
using Core.Messaging;
using Core.Services;
using Core.Transactions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Modules.FixedAssets.Services;
using Modules.Expenses.DTOs;
using Modules.Expenses.Repositories;
using Modules.Expenses.Services;
using Modules.Invoicing.Services;
using Modules.Ledger.Repositories;
using Modules.Planning;
using Modules.Inventory.DTOs;
using Modules.Inventory.Entities;
using Modules.Inventory.Services;
using Modules.CRM.DTOs;
using Modules.CRM.Services;
using Modules.Payments.DTOs;
using Modules.Payments.Entities;
using Modules.Payments.Services;
using Modules.Purchasing.DTOs;
using Modules.Purchasing.Services;
using Modules.Projects.DTOs;
using Modules.Projects.Entities;
using Modules.Projects.Repositories;
using Modules.Projects.Services;
using Modules.Sales.DTOs;
using Modules.Sales.Services;
using Npgsql;
using RabbitMQ.Client;
using Shared.Contracts.Projects;
using Shared.Contracts.Invoicing;
using Shared.Contracts.FinanceBase;
using Shared.Contracts.Platform;
using Shared.Contracts.Planning;
using Shared.Contracts.Subledger;
using Shared.Primitives.Identifiers;
using WorkerAssembly::Worker;
using Xunit;

namespace KernelIntegrationTests;

public sealed class ProjectsCrossModuleAcceptanceTests
{
    private static readonly TenantId AtlasTenant = new(Guid.Parse("c9fea3c8-249d-f150-a53a-84f5a891c82b"));
    private static readonly Guid AtlasEntity = Guid.Parse("aad8a34b-344c-9e5b-8d58-57f182fc6528");

    [Fact(DisplayName = "PJ46 real accepted Sales quotation creates one linked Customer Project and preserves commercial baseline")]
    public async Task RealAcceptedSalesQuotationCreatesOneLinkedProject()
    {
        var connectionString = RequireConnection();
        using var factory = CreateFactory(connectionString);
        var sales = factory.Services.GetRequiredService<SalesService>();
        var crm = factory.Services.GetRequiredService<CrmService>();
        var projects = factory.Services.GetRequiredService<ProjectService>();
        var actor = Guid.CreateVersion7();
        var customer = await FindPartyAsync(connectionString, AtlasTenant);
        var pipeline = (await crm.ListPipelinesAsync(AtlasTenant, AtlasEntity, CancellationToken.None)).First(item => item.Stages.Count > 0);
        var opportunity = await crm.CreateOpportunityAsync(AtlasTenant, actor,
            new(AtlasEntity, customer, null, pipeline.Id, pipeline.Stages[0].Id, "PJ46 accepted opportunity", 2500m, "EGP", new(2026, 4, 1)), true, CancellationToken.None);
        var quotationId = await crm.CreateQuotationAsync(AtlasTenant, actor, AtlasEntity, opportunity.Id,
            new($"pj46-sales-quote:{opportunity.Id:D}", new(2026, 3, 1), new(2026, 4, 1), "EGP",
                [new(null, null, "Accepted implementation scope", 1m, 2500m, 0m, "EA")], "PJ46-SALES-SOURCE"), true, CancellationToken.None);
        var draft = await sales.GetQuotationAsync(AtlasTenant, AtlasEntity, quotationId, CancellationToken.None);
        Assert.NotNull(draft);
        await sales.OpenQuotationAsync(AtlasTenant, actor, AtlasEntity, quotationId, new(draft!.Version), CancellationToken.None);
        await sales.AcceptQuotationAsync(AtlasTenant, actor, AtlasEntity, quotationId,
            new(1, "CustomerApproval", "PJ46-ACCEPTED", "Accepted for delivery", $"pj46-sales-accept:{quotationId:D}"), CancellationToken.None);

        var request = new CreateProjectFromSalesSourceRequest(
            AtlasEntity,
            quotationId,
            1,
            "PJ46 accepted commercial delivery",
            "CustomerProject",
            null,
            new(2026, 3, 1),
            new(2026, 6, 30),
            "FixedPrice",
            2500m,
            null,
            "Created from accepted Sales quotation",
            $"pj46-sales-project:{quotationId:D}:1");
        var concurrent = await Task.WhenAll(
            projects.CreateFromSalesSourceAsync(AtlasTenant, actor, actor, request, Guid.CreateVersion7(), CancellationToken.None),
            projects.CreateFromSalesSourceAsync(AtlasTenant, actor, actor, request, Guid.CreateVersion7(), CancellationToken.None));
        var first = concurrent[0];
        var retry = concurrent[1];

        Assert.Equal(first.Id, retry.Id);
        Assert.Equal(customer, first.CustomerPartyId);
        Assert.Equal("CustomerProject", first.ProjectType);
        Assert.Equal("FixedPrice", first.BillingModel);
        Assert.Equal("EGP", first.CurrencyCode);
        Assert.Equal("SalesQuotation", first.SourceType);
        Assert.Equal($"{quotationId:D}:Revision:1", first.SourceRef);
        Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(connectionString, "select count(*) from business_projects.project_external_reference where tenant_id=$1 and project_id=$2 and source_type='SalesQuotation' and source_id=$3 and purpose='AcceptedRevision:1'", AtlasTenant.Value, first.Id, quotationId.ToString("D")), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(2500m, Convert.ToDecimal(await ScalarAsync(connectionString, "select contract_value from business_projects.project_billing_rule where tenant_id=$1 and project_id=$2 and status='Active'", AtlasTenant.Value, first.Id), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("EGP", await ScalarAsync(connectionString, "select currency_code from business_projects.project_billing_rule where tenant_id=$1 and project_id=$2 and status='Active'", AtlasTenant.Value, first.Id));

        var acceptedBeforeRevision = await sales.GetQuotationAsync(AtlasTenant, AtlasEntity, quotationId, CancellationToken.None);
        await sales.ReviseQuotationAsync(AtlasTenant, actor, AtlasEntity, quotationId, acceptedBeforeRevision!.Version,
            [new(null, null, "Approved change-order candidate", 1m, 3000m, 0m, "EA")], CancellationToken.None);
        var preserved = await projects.GetAsync(AtlasTenant, first.Id, CancellationToken.None);
        Assert.Equal($"{quotationId:D}:Revision:1", preserved!.SourceRef);
        await Assert.ThrowsAsync<ProjectException>(() => projects.CreateFromSalesSourceAsync(AtlasTenant, actor, actor, request,
            Guid.CreateVersion7(), CancellationToken.None));

        var draftQuotationId = await sales.CreateQuotationAsync(AtlasTenant, actor,
            new(AtlasEntity, null, customer, "EGP", new(2026, 3, 2), null,
                [new(null, null, "Draft scope", 1m, 100m, 0m, "EA")], "PJ46-DRAFT-SOURCE"), CancellationToken.None);
        await Assert.ThrowsAsync<ProjectException>(() => projects.CreateFromSalesSourceAsync(AtlasTenant, actor, actor,
            request with { QuotationId = draftQuotationId, IdempotencyKey = $"pj46-sales-project:{draftQuotationId:D}:1" }, Guid.CreateVersion7(), CancellationToken.None));
        await Assert.ThrowsAsync<ProjectException>(() => projects.CreateFromSalesSourceAsync(AtlasTenant, actor, actor,
            request with { AcceptedRevision = 2, IdempotencyKey = $"pj46-sales-project:{quotationId:D}:2" }, Guid.CreateVersion7(), CancellationToken.None));
        await Assert.ThrowsAsync<ProjectException>(() => projects.CreateFromSalesSourceAsync(AtlasTenant, actor, actor,
            request with { LegalEntityId = Guid.CreateVersion7(), IdempotencyKey = $"pj46-sales-project:{quotationId:D}:wrong-entity" }, Guid.CreateVersion7(), CancellationToken.None));
        await Assert.ThrowsAsync<ProjectException>(() => projects.CreateFromSalesSourceAsync(new TenantId(Guid.CreateVersion7()), actor, actor,
            request with { IdempotencyKey = $"pj46-sales-project:{quotationId:D}:wrong-tenant" }, Guid.CreateVersion7(), CancellationToken.None));
    }

    [Fact(DisplayName = "PJ45 real finalized Expense allocation reconciles one Project expense fact")]
    public async Task RealExpenseFinalizationReconcilesProjectExpenseCost()
    {
        var connectionString = RequireConnection();
        using var factory = CreateFactory(connectionString);
        var projects = factory.Services.GetRequiredService<ProjectService>();
        await using var expenseDataSource = NpgsqlDataSource.Create(connectionString);
        var expenses = new ExpenseRepository(expenseDataSource,
            factory.Services.GetRequiredService<ITransactionCoordinator>(),
            factory.Services.GetRequiredService<ITransactionalLedgerPostingCapability>(),
            approvals: factory.Services.GetRequiredService<IPlatformApprovalCapability>(),
            financeBase: factory.Services.GetService<IFinanceBaseCapabilities>());
        var actorMembership = await FindAdminActorMembershipAsync(connectionString, AtlasTenant);
        var approverMembership = await FindApprovalActorMembershipAsync(connectionString, AtlasTenant, actorMembership.ActorId);
        var project = await projects.CreateAsync(AtlasTenant, actorMembership.ActorId, actorMembership.MembershipId,
            new(AtlasEntity, $"PJ45-EXPENSE-{Guid.NewGuid():N}", "InternalProject", null, null, DateOnly.FromDateTime(DateTime.UtcNow.Date), null), Guid.CreateVersion7(), CancellationToken.None);
        var categoryVersion = (Guid)(await ScalarAsync(connectionString,
            "select v.id from business_expenses.expense_category_version v join business_expenses.expense_category c on c.tenant_id=v.tenant_id and c.id=v.category_id where v.tenant_id=$1 and c.legal_entity_id=$2 and v.status='Published' order by v.effective_from desc limit 1",
            AtlasTenant.Value, AtlasEntity))!;
        var expense = await expenses.CreateAsync(AtlasTenant,
            new CreateExpenseRequest(AtlasEntity, null, "Project delivery expense", DateOnly.FromDateTime(DateTime.UtcNow.Date), 125m,
                "EGP", "CompanyPaid", categoryVersion, "Project delivery", ProjectId: project.Id, CostCategory: "Expense"), CancellationToken.None);
        await expenses.AddEvidenceAsync(AtlasTenant, AtlasEntity, expense.Id, new("Receipt", $"file://projects/{expense.Id:D}"), CancellationToken.None);
        var submitted = await expenses.TransitionAsync(AtlasTenant, AtlasEntity, expense.Id, new(expense.Version), "submit", CancellationToken.None);
        await expenses.EvaluatePolicyAsync(AtlasTenant, AtlasEntity, expense.Id, CancellationToken.None);
        var approval = await expenses.RequestApprovalAsync(AtlasTenant, actorMembership.ActorId, actorMembership.MembershipId, AtlasEntity, expense.Id, "PJ45 acceptance", CancellationToken.None);
        var decided = await expenses.DecideApprovalAsync(AtlasTenant, approverMembership.ActorId, approverMembership.MembershipId, AtlasEntity, expense.Id,
            new(approval.RequestId, "Approved", "PJ45 approved"), CancellationToken.None);
        var finalized = await expenses.TransitionAsync(AtlasTenant, AtlasEntity, expense.Id, new(decided.ResourceVersion + 1), "finalize", CancellationToken.None);
        Assert.Equal("Finalized", finalized.ExpenseState);
        var message = await ReadOutboxAsync(connectionString, AtlasTenant, "ExpenseFinalizedV1", expense.Id);

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var inbox = new NpgsqlInboxStore(dataSource);
        var evidence = new ProjectsIntegrationRepository(dataSource);
        await using var provider = new ServiceCollection()
            .AddSingleton<IProjectSourceIntegrationCapabilities>(projects)
            .AddSingleton(evidence)
            .AddSingleton<IInboxStore>(inbox)
            .AddMassTransitTestHarness(config => config.AddConsumer<ProjectsSourceEventConsumer>())
            .BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(message);
            await harness.Bus.Publish(message);
            Assert.True(await EventuallyAsync(async () => Convert.ToInt64(await ScalarAsync(connectionString,
                "select count(*) from business_projects.project_cost_fact where tenant_id=$1 and project_id=$2 and source_module='expenses' and source_id=$3",
                AtlasTenant.Value, project.Id, expense.Id.ToString("D")), System.Globalization.CultureInfo.InvariantCulture) == 1));
            Assert.Equal(125m, Convert.ToDecimal(await ScalarAsync(connectionString,
                "select base_amount from business_projects.project_cost_fact where tenant_id=$1 and project_id=$2 and source_module='expenses' and source_id=$3",
                AtlasTenant.Value, project.Id, expense.Id.ToString("D")), System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact(DisplayName = "PJ45 real Projects source event flows through local RabbitMQ Worker and Inbox exactly once")]
    public async Task RealProjectsSourceEventFlowsThroughLocalWorkerAndInboxExactlyOnce()
    {
        var connectionString = RequireConnection();
        using var factory = CreateFactory(connectionString);
        var projects = factory.Services.GetRequiredService<ProjectService>();
        await using var expenseDataSource = NpgsqlDataSource.Create(connectionString);
        var expenses = new ExpenseRepository(expenseDataSource,
            factory.Services.GetRequiredService<ITransactionCoordinator>(),
            factory.Services.GetRequiredService<ITransactionalLedgerPostingCapability>(),
            approvals: factory.Services.GetRequiredService<IPlatformApprovalCapability>(),
            financeBase: factory.Services.GetService<IFinanceBaseCapabilities>());
        var actorMembership = await FindAdminActorMembershipAsync(connectionString, AtlasTenant);
        var approverMembership = await FindApprovalActorMembershipAsync(connectionString, AtlasTenant, actorMembership.ActorId);
        var project = await projects.CreateAsync(AtlasTenant, actorMembership.ActorId, actorMembership.MembershipId,
            new(AtlasEntity, $"PJ45-WORKER-{Guid.NewGuid():N}", "InternalProject", null, null, DateOnly.FromDateTime(DateTime.UtcNow.Date), null), Guid.CreateVersion7(), CancellationToken.None);
        var categoryVersion = (Guid)(await ScalarAsync(connectionString,
            "select v.id from business_expenses.expense_category_version v join business_expenses.expense_category c on c.tenant_id=v.tenant_id and c.id=v.category_id where v.tenant_id=$1 and c.legal_entity_id=$2 and v.status='Published' order by v.effective_from desc limit 1",
            AtlasTenant.Value, AtlasEntity))!;
        var expense = await expenses.CreateAsync(AtlasTenant,
            new CreateExpenseRequest(AtlasEntity, null, "Local Worker project expense", DateOnly.FromDateTime(DateTime.UtcNow.Date), 137m,
                "EGP", "CompanyPaid", categoryVersion, "Local Worker acceptance", ProjectId: project.Id, CostCategory: "Expense"), CancellationToken.None);
        await expenses.AddEvidenceAsync(AtlasTenant, AtlasEntity, expense.Id, new("Receipt", $"file://projects/{expense.Id:D}"), CancellationToken.None);
        var submitted = await expenses.TransitionAsync(AtlasTenant, AtlasEntity, expense.Id, new(expense.Version), "submit", CancellationToken.None);
        await expenses.EvaluatePolicyAsync(AtlasTenant, AtlasEntity, expense.Id, CancellationToken.None);
        var approval = await expenses.RequestApprovalAsync(AtlasTenant, actorMembership.ActorId, actorMembership.MembershipId, AtlasEntity, expense.Id, "PJ45 Worker acceptance", CancellationToken.None);
        var decided = await expenses.DecideApprovalAsync(AtlasTenant, approverMembership.ActorId, approverMembership.MembershipId, AtlasEntity, expense.Id,
            new(approval.RequestId, "Approved", "PJ45 Worker approved"), CancellationToken.None);
        var finalized = await expenses.TransitionAsync(AtlasTenant, AtlasEntity, expense.Id, new(decided.ResourceVersion + 1), "finalize", CancellationToken.None);
        Assert.Equal("Finalized", finalized.ExpenseState);
        var message = await ReadOutboxAsync(connectionString, AtlasTenant, "ExpenseFinalizedV1", expense.Id);

        var rabbitHost = Environment.GetEnvironmentVariable("TODIR_RABBITMQ_HOST") ?? "localhost";
        var rabbitPort = int.TryParse(Environment.GetEnvironmentVariable("TODIR_RABBITMQ_PORT"), out var port) ? port : 5672;
        var rabbitUser = Environment.GetEnvironmentVariable("TODIR_RABBITMQ_USER") ?? "todir";
        var rabbitPassword = Environment.GetEnvironmentVariable("TODIR_RABBITMQ_PASSWORD") ?? "todir-dev-only";
        var rabbitVHost = Environment.GetEnvironmentVariable("TODIR_RABBITMQ_VHOST") ?? "/erp-dev";
        using var worker = await StartProjectsWorkerAsync(connectionString, rabbitHost, rabbitPort, rabbitUser, rabbitPassword, rabbitVHost);
        IBusControl? bus = null;
        try
        {
            var rabbitFactory = new ConnectionFactory
            {
                HostName = rabbitHost,
                Port = rabbitPort,
                VirtualHost = rabbitVHost,
                UserName = rabbitUser,
                Password = rabbitPassword
            };
            await using (var rabbitConnection = await rabbitFactory.CreateConnectionAsync())
            {
                var declared = false;
                for (var attempt = 0; attempt < 40; attempt++)
                {
                    try
                    {
                        await using var channel = await rabbitConnection.CreateChannelAsync();
                        await channel.QueueDeclarePassiveAsync(ProjectsSourceEventConsumer.EndpointName);
                        declared = true;
                        break;
                    }
                    catch
                    {
                        await Task.Delay(500);
                    }
                }
                Assert.True(declared, "The Projects Worker did not declare its RabbitMQ source-integration queue within 20 seconds.");
            }

            var hostUri = new UriBuilder("rabbitmq", rabbitHost)
            {
                Port = rabbitPort,
                Path = $"/{Uri.EscapeDataString(rabbitVHost.StartsWith('/') ? rabbitVHost : "/" + rabbitVHost)}"
            }.Uri;
            bus = Bus.Factory.CreateUsingRabbitMq(config =>
            {
                config.Host(hostUri, host =>
                {
                    host.Username(rabbitUser);
                    host.Password(rabbitPassword);
                });
            });
            await bus.StartAsync();
            await bus.Publish(message);

            Assert.True(await EventuallyAsync(async () =>
                Convert.ToInt64(await ScalarAsync(connectionString,
                    "select count(*) from business_projects.project_cost_fact where tenant_id=$1 and project_id=$2 and source_module='expenses' and source_id=$3",
                    AtlasTenant.Value, project.Id, expense.Id.ToString("D")), System.Globalization.CultureInfo.InvariantCulture) == 1));
            Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(connectionString,
                "select count(*) from business_projects.project_integration_event where tenant_id=$1 and message_id=$2 and status='Applied'",
                AtlasTenant.Value, message.MessageId), System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(connectionString,
                "select count(*) from platform.inbox_message where tenant_id=$1 and consumer_endpoint=$2 and message_id=$3 and processed_at is not null",
                AtlasTenant.Value, ProjectsSourceEventConsumer.EndpointName, message.MessageId), System.Globalization.CultureInfo.InvariantCulture));

            await bus.Publish(message);
            await Task.Delay(1500);
            Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(connectionString,
                "select count(*) from business_projects.project_cost_fact where tenant_id=$1 and project_id=$2 and source_module='expenses' and source_id=$3",
                AtlasTenant.Value, project.Id, expense.Id.ToString("D")), System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(connectionString,
                "select count(*) from business_projects.project_integration_event where tenant_id=$1 and message_id=$2",
                AtlasTenant.Value, message.MessageId), System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            if (bus is not null)
            {
                await bus.StopAsync();
            }
            await StopProjectsWorkerAsync(worker);
        }
    }

    [Fact(DisplayName = "PJ45 real Inventory valuation issue and reversal reconcile one Project material fact")]
    public async Task RealInventoryIssueAndReversalReconcileProjectMaterialCost()
    {
        var connectionString = RequireConnection();
        using var factory = CreateFactory(connectionString);
        var projects = factory.Services.GetRequiredService<ProjectService>();
        var inventory = factory.Services.GetRequiredService<InventoryService>();
        var actor = Guid.CreateVersion7();
        var project = await projects.CreateAsync(AtlasTenant, actor, actor,
            new(AtlasEntity, $"PJ45-INVENTORY-{Guid.NewGuid():N}", "InternalProject", null, null, new(2026, 3, 1), null), Guid.CreateVersion7(), CancellationToken.None);
        var variantId = (Guid)(await ScalarAsync(connectionString, "select p.id from business_inventory.product_inventory_profile i join foundation_catalog.product_variant p on p.tenant_id=i.tenant_id and p.id=i.product_variant_id join foundation_catalog.product_uom u on u.tenant_id=p.tenant_id and u.variant_id=p.id and u.is_base and u.status='Active' and u.effective_from<=now() and (u.effective_to is null or u.effective_to>now()) where i.tenant_id=$1 and i.legal_entity_id=$2 and i.status='Active' and i.valuation_mode='Perpetual' and p.status='Active' and exists(select 1 from foundation_catalog.product_visibility v where v.tenant_id=p.tenant_id and v.template_id=p.product_template_id and (v.scope_type='TenantWide' or (v.scope_type='LegalEntity' and v.legal_entity_id=$2))) order by p.id limit 1", AtlasTenant.Value, AtlasEntity))!;
        var warehouse = await ReadInventoryWarehouseAsync(connectionString, AtlasTenant, AtlasEntity);
        var unitId = (Guid)(await ScalarAsync(connectionString, "select uom_id from foundation_catalog.product_uom where tenant_id=$1 and variant_id=$2 and is_base and status='Active' order by effective_from desc limit 1", AtlasTenant.Value, variantId))!;
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        await inventory.ReceiveAsync(AtlasTenant, actor, AtlasEntity,
            new ConfirmStockMovementRequest(variantId, warehouse.WarehouseId, warehouse.LocationId, unitId, 2m, date, date,
                $"pj45-inventory-receipt:{Guid.NewGuid():N}", "inventory", null, UnitCost: 100m, CurrencyCode: "EGP"),
            Guid.CreateVersion7(), null, CancellationToken.None);
        var issue = await inventory.IssueAsync(AtlasTenant, actor, AtlasEntity,
            new ConfirmStockMovementRequest(variantId, warehouse.WarehouseId, warehouse.LocationId, unitId, 1m, date, date,
                $"pj45-inventory-issue:{project.Id:D}:{Guid.NewGuid():N}", "projects", project.Id, ProjectId: project.Id, CostCategory: "Material"),
            Guid.CreateVersion7(), null, CancellationToken.None);
        var issuedMessage = await ReadOutboxAsync(connectionString, AtlasTenant, "StockIssuedV1", issue.OperationId);
        using var issuedPayload = JsonDocument.Parse(issuedMessage.Payload);
        var issuedCost = issuedPayload.RootElement.GetProperty("costAmount").GetDecimal();
        Assert.Equal(100m, issuedCost);

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var inbox = new NpgsqlInboxStore(dataSource);
        var evidence = new ProjectsIntegrationRepository(dataSource);
        await using var provider = new ServiceCollection()
            .AddSingleton<IProjectSourceIntegrationCapabilities>(projects)
            .AddSingleton(evidence)
            .AddSingleton<IInboxStore>(inbox)
            .AddMassTransitTestHarness(config => config.AddConsumer<ProjectsSourceEventConsumer>())
            .BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(issuedMessage);
            await harness.Bus.Publish(issuedMessage);
            Assert.True(await EventuallyAsync(async () => Convert.ToInt64(await ScalarAsync(connectionString,
                "select count(*) from business_projects.project_cost_fact where tenant_id=$1 and project_id=$2 and source_module='inventory' and source_id=$3",
                AtlasTenant.Value, project.Id, issue.OperationId.ToString("D")), System.Globalization.CultureInfo.InvariantCulture) == 1));
            Assert.Equal(issuedCost, Convert.ToDecimal(await ScalarAsync(connectionString,
                "select base_amount from business_projects.project_cost_fact where tenant_id=$1 and project_id=$2 and source_module='inventory' and source_id=$3",
                AtlasTenant.Value, project.Id, issue.OperationId.ToString("D")), System.Globalization.CultureInfo.InvariantCulture));

            var reversal = await inventory.ReverseOperationAsync(AtlasTenant.Value, actor, AtlasEntity, issue.OperationId, date, date,
                $"pj45-inventory-reversal:{issue.OperationId:D}", CancellationToken.None);
            var reversedMessage = await ReadOutboxAsync(connectionString, AtlasTenant, "StockReversedV1", reversal);
            await harness.Bus.Publish(reversedMessage);
            Assert.True(await EventuallyAsync(async () => Convert.ToInt64(await ScalarAsync(connectionString,
                "select count(*) from business_projects.project_cost_fact where tenant_id=$1 and project_id=$2 and source_module='inventory' and source_id=$3 and state='Corrected'",
                AtlasTenant.Value, project.Id, issue.OperationId.ToString("D")), System.Globalization.CultureInfo.InvariantCulture) == 1));
            Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(connectionString,
                "select count(*) from business_projects.project_integration_event where tenant_id=$1 and message_id=$2",
                AtlasTenant.Value, reversedMessage.MessageId), System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact(DisplayName = "PJ35 real close exception outbox reaches the platform notification store")]
    public async Task RealCloseExceptionEnqueuesManagerNotification()
    {
        var connectionString = RequireConnection();
        using var factory = CreateFactory(connectionString);
        var projects = factory.Services.GetRequiredService<ProjectService>();
        var actor = Guid.CreateVersion7();
        var project = await projects.CreateAsync(AtlasTenant, actor, actor,
            new(AtlasEntity, $"PJ35-NOTIFICATION-{Guid.NewGuid():N}", "InternalProject", null, null, new(2026, 3, 1), null), Guid.CreateVersion7(), CancellationToken.None);
        await projects.AddTeamMemberAsync(AtlasTenant, actor, project.Id, new(null, actor, "ProjectManager", IsManager: true), CancellationToken.None);
        var exception = await projects.AddCloseExceptionAsync(AtlasTenant, actor, project.Id,
            new("external_dependency", "dependency-35", "Awaiting external handoff"), CancellationToken.None);
        var message = await ReadOutboxAsync(connectionString, AtlasTenant, "ProjectCloseExceptionCreatedV1", exception.Id);

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        var inbox = new NpgsqlInboxStore(dataSource);
        await using var provider = new ServiceCollection()
            .AddSingleton<INotificationStore>(new NpgsqlNotificationStore(dataSource))
            .AddSingleton<IInboxStore>(inbox)
            .AddMassTransitTestHarness(config => config.AddConsumer<ProjectsNotificationConsumer>())
            .BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(message);
            Assert.True(await EventuallyAsync(async () => Convert.ToInt64(await ScalarAsync(connectionString,
                "select count(*) from platform.notification where tenant_id=$1 and template_key='projects.close_exception_created' and payload->>'exceptionId'=$2",
                AtlasTenant.Value, exception.Id.ToString("D")), System.Globalization.CultureInfo.InvariantCulture) == 1));
            Assert.Equal($"user:{actor:D}", await ScalarAsync(connectionString,
                "select recipient_ref from platform.notification where tenant_id=$1 and template_key='projects.close_exception_created' and payload->>'exceptionId'=$2",
                AtlasTenant.Value, exception.Id.ToString("D")));
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact(DisplayName = "PJ43 real Projects to Fixed Assets handoff delivers accept and reject results idempotently")]
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
        var evidence = new ProjectsIntegrationRepository(dataSource);
        await using var provider = new ServiceCollection()
            .AddSingleton(fixedAssets)
            .AddSingleton<IProjectSourceIntegrationCapabilities>(projectService)
            .AddSingleton(evidence)
            .AddSingleton<IInboxStore>(inbox)
            .AddMassTransitTestHarness(config =>
            {
                config.AddConsumer<ProjectCapitalHandoffConsumer>();
                config.AddConsumer<ProjectsSourceEventConsumer>();
            })
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

            var accepted = await fixedAssets.ReviewCapitalizationCandidateAsync(AtlasTenant, actor, candidate.Id, new("Approve", null), CancellationToken.None);
            Assert.Equal("Approved", accepted.Status);
            var acceptedMessage = await ReadOutboxAsync(connectionString, AtlasTenant, "FixedAssetCapitalizationAcceptedV1", candidate.Id);
            await harness.Bus.Publish(acceptedMessage);
            Assert.True(await EventuallyAsync(async () => (string?)await ScalarAsync(connectionString, "select state from business_projects.project_capital_handoff where tenant_id=$1 and id=$2", AtlasTenant.Value, handoff.Id) == "Accepted"));
            Assert.True(await EventuallyAsync(async () => Convert.ToInt64(await ScalarAsync(connectionString, "select count(*) from business_projects.project_integration_event where tenant_id=$1 and message_id=$2 and status='Applied'", AtlasTenant.Value, acceptedMessage.MessageId), System.Globalization.CultureInfo.InvariantCulture) == 1));
            await harness.Bus.Publish(acceptedMessage);
            await Task.Delay(250);
            Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(connectionString, "select count(*) from business_projects.project_integration_event where tenant_id=$1 and message_id=$2", AtlasTenant.Value, acceptedMessage.MessageId), System.Globalization.CultureInfo.InvariantCulture));

            var rejectedProject = await projectService.CreateAsync(AtlasTenant, actor, actor, new(AtlasEntity, $"PJ43-REJECT-{Guid.NewGuid():N}", "CapitalProject", null, null, new(2026, 1, 1), null), Guid.CreateVersion7(), CancellationToken.None);
            var rejectedCost = await projectService.RecordCostFactAsync(AtlasTenant, actor, new(rejectedProject.Id, null, "expenses", "ExpenseFinalizedV1", $"capex-reject:{Guid.NewGuid():N}", null, "Capitalizable", "EGP", 300m, "EGP", 300m, 1m, new(2026, 1, 3), $"pj43-reject:{Guid.NewGuid():N}", null, AtlasEntity), Guid.CreateVersion7(), CancellationToken.None);
            var rejectedHandoff = await projectService.CreateCapitalHandoffAsync(AtlasTenant, actor, rejectedProject.Id, new(rejectedCost.Id, 300m, "EGP", $"pj43-reject-handoff:{Guid.NewGuid():N}"), Guid.CreateVersion7(), CancellationToken.None);
            var rejectedRequest = await ReadOutboxAsync(connectionString, AtlasTenant, "ProjectCapitalHandoffRequestedV1", rejectedProject.Id);
            await harness.Bus.Publish(rejectedRequest);
            Assert.True(await EventuallyAsync(async () => (await fixedAssets.ListCapitalizationCandidatesAsync(AtlasTenant, AtlasEntity, null, 200, CancellationToken.None)).Any(item => item.SourceReference == rejectedCost.Id.ToString("D"))));
            var rejectedCandidate = (await fixedAssets.ListCapitalizationCandidatesAsync(AtlasTenant, AtlasEntity, null, 200, CancellationToken.None)).Single(item => item.SourceReference == rejectedCost.Id.ToString("D"));
            var rejected = await fixedAssets.ReviewCapitalizationCandidateAsync(AtlasTenant, actor, rejectedCandidate.Id, new("Reject", "Not eligible for capitalization"), CancellationToken.None);
            Assert.Equal("Rejected", rejected.Status);
            var rejectedMessage = await ReadOutboxAsync(connectionString, AtlasTenant, "FixedAssetCapitalizationRejectedV1", rejectedCandidate.Id);
            await harness.Bus.Publish(rejectedMessage);
            Assert.True(await EventuallyAsync(async () => (string?)await ScalarAsync(connectionString, "select state from business_projects.project_capital_handoff where tenant_id=$1 and id=$2", AtlasTenant.Value, rejectedHandoff.Id) == "Rejected"));
            Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(connectionString, "select count(*) from business_projects.project_integration_event where tenant_id=$1 and message_id=$2 and status='Applied'", AtlasTenant.Value, rejectedMessage.MessageId), System.Globalization.CultureInfo.InvariantCulture));
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

    [Fact(DisplayName = "PJ44 real invoicing subledger payments collection and reversal reconcile project settlement")]
    public async Task RealProjectCollectionSettlementAndReversalReconcileSummary()
    {
        var connectionString = RequireConnection();
        using var factory = CreateFactory(connectionString);
        var projectService = factory.Services.GetRequiredService<ProjectService>();
        var invoicing = factory.Services.GetRequiredService<InvoicingService>();
        var payments = factory.Services.GetRequiredService<PaymentsService>();
        var subledger = factory.Services.GetRequiredService<ISubledgerCapabilities>();
        var actorMembership = await FindAdminActorMembershipAsync(connectionString, AtlasTenant);
        var customer = await FindPartyAsync(connectionString, AtlasTenant);
        var paymentAccountId = await FindPaymentAccountAsync(connectionString, AtlasTenant, AtlasEntity);
        var project = await projectService.CreateAsync(AtlasTenant, actorMembership.ActorId, actorMembership.ActorId,
            new(AtlasEntity, $"PJ44-COLLECT-{Guid.NewGuid():N}", "CustomerProject", customer, null, new(2026, 1, 1), null,
                BillingModel: "FixedPrice", CurrencyCode: "EGP"), Guid.CreateVersion7(), CancellationToken.None);
        var charge = await projectService.CreateChargeAsync(AtlasTenant, actorMembership.ActorId,
            new(project.Id, "Milestone", $"milestone-collect:{Guid.NewGuid():N}", new(2026, 1, 31), "Collected milestone", "EGP", 700m, 1000m,
                $"pj44-collect-charge:{Guid.NewGuid():N}"), CancellationToken.None);
        var invoiceRequest = await projectService.RequestInvoiceAsync(AtlasTenant, actorMembership.ActorId, actorMembership.MembershipId,
            new(project.Id, [charge.Id], "EGP", 1000m, $"pj44-collect-invoice:{Guid.NewGuid():N}"), Guid.CreateVersion7(), CancellationToken.None);
        var draft = await invoicing.GetDraftAsync(AtlasTenant, AtlasEntity, invoiceRequest.InvoiceId!.Value, CancellationToken.None);
        var finalized = await invoicing.FinalizeAsync(AtlasTenant, actorMembership.ActorId, actorMembership.MembershipId, invoiceRequest.InvoiceId.Value,
            AtlasEntity, new(draft.Version, $"pj44-collect-finalize:{Guid.NewGuid():N}"), Guid.CreateVersion7(), CancellationToken.None);
        var invoiceMessage = await ReadOutboxAsync(connectionString, AtlasTenant, "InvoiceFinalizedV1", finalized.InvoiceId);

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
            await harness.Bus.Publish(invoiceMessage);
            var invoiceItem = (await subledger.ListOpenItemsAsync(AtlasTenant,
                new(new(AtlasEntity), customer, SubledgerDirectionContract.Receivable, true, 50, null,
                    Search: finalized.InvoiceId.ToString("D"), CurrencyCode: "EGP"), CancellationToken.None)).Items
                .Single(item => item.SourceType == "Invoice" && item.SourceReference == finalized.InvoiceId.ToString("D"));
            Assert.Equal("Open", invoiceItem.Status);

            var firstPayment = await payments.CreateDraftAsync(AtlasTenant, actorMembership.ActorId, actorMembership.MembershipId, Guid.CreateVersion7(),
                new(AtlasEntity, null, PaymentType.CustomerReceipt, customer, "400", "EGP", PaymentMethod.BankTransfer, paymentAccountId,
                    new(2026, 2, 1), new(2026, 2, 1), null, $"PJ44-RECEIPT-{Guid.CreateVersion7():N}", null, null, "Projects", project.Id.ToString("D")),
                $"pj44-payment-1:{Guid.NewGuid():N}", CancellationToken.None);
            await payments.CompleteManualAsync(AtlasTenant, actorMembership.ActorId, actorMembership.MembershipId, AtlasEntity, firstPayment.Id,
                $"pj44-payment-1-complete:{Guid.NewGuid():N}", Guid.CreateVersion7(), CancellationToken.None);
            var paymentItem = (await subledger.ListOpenItemsAsync(AtlasTenant,
                new(new(AtlasEntity), customer, SubledgerDirectionContract.Receivable, true, 50, null,
                    Search: firstPayment.Id.ToString("D"), CurrencyCode: "EGP"), CancellationToken.None)).Items
                .Single(item => item.SourceType == "Payment" && item.SourceReference == firstPayment.Id.ToString("D"));
            var firstApplication = await payments.ApplyToPaymentAsync(AtlasTenant, actorMembership.ActorId, actorMembership.MembershipId, Guid.CreateVersion7(),
                AtlasEntity, null, firstPayment.Id, new(new(2026, 2, 1), new(2026, 2, 1), [new(invoiceItem.Id, "400")]),
                $"pj44-application-1:{Guid.NewGuid():N}", CancellationToken.None);
            Assert.Single(firstApplication.ApplicationIds);
            var partiallySettled = await subledger.GetOpenItemAsync(AtlasTenant, new(AtlasEntity), invoiceItem.Id, CancellationToken.None);
            Assert.NotNull(partiallySettled);
            Assert.Equal("PartiallySettled", partiallySettled!.Status);
            var afterFirstPayment = await projectService.GetSummaryAsync(AtlasTenant, project.Id, CancellationToken.None);
            Assert.Equal(400m, afterFirstPayment.Collected);
            Assert.Equal(600m, afterFirstPayment.Outstanding);

            var firstPaymentMessage = await ReadOutboxAsync(connectionString, AtlasTenant, "PaymentCompletedV1", firstPayment.Id);
            await harness.Bus.Publish(firstPaymentMessage);
            Assert.True(await EventuallyAsync(async () => (string?)await ScalarAsync(connectionString, "select status from business_projects.project_integration_event where tenant_id=$1 and message_id=$2", AtlasTenant.Value, firstPaymentMessage.MessageId) == "Applied"));

            await subledger.ReverseAsync(AtlasTenant, actorMembership.MembershipId,
                new(new(AtlasEntity), firstApplication.ApplicationIds[0], $"pj44-application-reversal:{Guid.NewGuid():N}", new(2026, 2, 2), "Customer receipt correction", actorMembership.ActorId, Guid.CreateVersion7()), CancellationToken.None);
            var reopenedItem = await subledger.GetOpenItemAsync(AtlasTenant, new(AtlasEntity), invoiceItem.Id, CancellationToken.None);
            Assert.NotNull(reopenedItem);
            Assert.Equal("Open", reopenedItem!.Status);
            var reversedPayment = await payments.ReverseAsync(AtlasTenant, actorMembership.ActorId, actorMembership.MembershipId, Guid.CreateVersion7(),
                new(new(2026, 2, 2), new(2026, 2, 2), "Customer receipt correction"), AtlasEntity, firstPayment.Id,
                $"pj44-payment-reversal:{Guid.NewGuid():N}", CancellationToken.None);
            Assert.Equal(PaymentType.CustomerRefund, reversedPayment.Type);
            var reversedMessage = await ReadOutboxAsync(connectionString, AtlasTenant, "PaymentReversedV1", firstPayment.Id);
            await harness.Bus.Publish(reversedMessage);
            Assert.True(await EventuallyAsync(async () => (string?)await ScalarAsync(connectionString, "select status from business_projects.project_integration_event where tenant_id=$1 and message_id=$2", AtlasTenant.Value, reversedMessage.MessageId) == "Applied"));
            await harness.Bus.Publish(reversedMessage);
            await Task.Delay(250);
            Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(connectionString, "select count(*) from business_projects.project_integration_event where tenant_id=$1 and message_id=$2", AtlasTenant.Value, reversedMessage.MessageId), System.Globalization.CultureInfo.InvariantCulture));
            var afterReversal = await projectService.GetSummaryAsync(AtlasTenant, project.Id, CancellationToken.None);
            Assert.Equal(0m, afterReversal.Collected);
            Assert.Equal(1000m, afterReversal.Outstanding);

            var finalPayment = await payments.CreateDraftAsync(AtlasTenant, actorMembership.ActorId, actorMembership.MembershipId, Guid.CreateVersion7(),
                new(AtlasEntity, null, PaymentType.CustomerReceipt, customer, "1000", "EGP", PaymentMethod.BankTransfer, paymentAccountId,
                    new(2026, 2, 3), new(2026, 2, 3), null, $"PJ44-RECEIPT-{Guid.CreateVersion7():N}", null, null, "Projects", project.Id.ToString("D")),
                $"pj44-payment-final:{Guid.NewGuid():N}", CancellationToken.None);
            await payments.CompleteManualAsync(AtlasTenant, actorMembership.ActorId, actorMembership.MembershipId, AtlasEntity, finalPayment.Id,
                $"pj44-payment-final-complete:{Guid.NewGuid():N}", Guid.CreateVersion7(), CancellationToken.None);
            var finalApplication = await payments.ApplyToPaymentAsync(AtlasTenant, actorMembership.ActorId, actorMembership.MembershipId, Guid.CreateVersion7(),
                AtlasEntity, null, finalPayment.Id, new(new(2026, 2, 3), new(2026, 2, 3), [new(invoiceItem.Id, "1000")]),
                $"pj44-application-final:{Guid.NewGuid():N}", CancellationToken.None);
            Assert.Single(finalApplication.ApplicationIds);
            var settledItem = await subledger.GetOpenItemAsync(AtlasTenant, new(AtlasEntity), invoiceItem.Id, CancellationToken.None);
            Assert.NotNull(settledItem);
            Assert.Equal("Settled", settledItem!.Status);
            var finalSummary = await projectService.GetSummaryAsync(AtlasTenant, project.Id, CancellationToken.None);
            Assert.Equal(1000m, finalSummary.Collected);
            Assert.Equal(0m, finalSummary.Outstanding);
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
        var purchasing = factory.Services.GetRequiredService<PurchasingService>();
        var actor = Guid.CreateVersion7();
        var customer = await FindPartyAsync(connectionString, AtlasTenant);
        var supplier = await FindPartyAsync(connectionString, AtlasTenant, "Supplier");
        var project = await projectService.CreateAsync(AtlasTenant, actor, actor, new(AtlasEntity, $"PJ15-REAL-{Guid.NewGuid():N}", "CustomerProject", customer, null, new(2026, 1, 1), null, BillingModel: "CostPlus", CurrencyCode: "EGP"), Guid.CreateVersion7(), CancellationToken.None);
        var purchaseOrderId = await purchasing.CreateAsync(AtlasTenant, actor, new(AtlasEntity, null, supplier, "EGP", new(2026, 1, 15), null, false,
            [new PurchaseOrderLineRequest(null, null, "EA", "Supplier project actual", 2m, 500m, "Approved project test price")], project.Id, null, "Supplier"), true, Guid.CreateVersion7(), CancellationToken.None);
        var draftOrder = await purchasing.GetAsync(AtlasTenant, AtlasEntity, purchaseOrderId, true, CancellationToken.None);
        Assert.NotNull(draftOrder);
        var approvalCorrelation = Guid.CreateVersion7();
        await purchasing.ApproveAsync(AtlasTenant, actor, AtlasEntity, purchaseOrderId, new(draftOrder!.Version, $"pj15-real-approve:{Guid.NewGuid():N}"), approvalCorrelation, CancellationToken.None);
        var approvedOrder = await purchasing.GetAsync(AtlasTenant, AtlasEntity, purchaseOrderId, true, CancellationToken.None);
        Assert.NotNull(approvedOrder);
        var approvalMessage = await ReadOutboxAsync(connectionString, AtlasTenant, "PurchaseOrderApprovedV1", approvalCorrelation);
        var invoiceId = await purchasing.CreateSupplierInvoiceAsync(AtlasTenant, actor, actor, AtlasEntity, purchaseOrderId,
            new(approvedOrder!.Version, $"pj15-real-invoice:{Guid.NewGuid():N}", new(2026, 1, 15), new(2026, 1, 15), new(2026, 1, 15), $"SUP-PJ15-{Guid.NewGuid():N}", [new(approvedOrder.Lines.Single().Id, 1m)]), Guid.CreateVersion7(), CancellationToken.None);
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
            await harness.Bus.Publish(approvalMessage);
            Assert.True(await EventuallyAsync(async () => Convert.ToInt64(await ScalarAsync(connectionString, "select count(*) from business_projects.project_commitment where tenant_id=$1 and project_id=$2 and source_module='purchasing' and source_id=$3", AtlasTenant.Value, project.Id, purchaseOrderId.ToString("D")), System.Globalization.CultureInfo.InvariantCulture) == 1));
            await harness.Bus.Publish(approvalMessage);
            await harness.Bus.Publish(message);
            Assert.True(await EventuallyAsync(async () => Convert.ToInt64(await ScalarAsync(connectionString, "select count(*) from business_projects.project_cost_fact where tenant_id=$1 and project_id=$2 and source_module='invoicing' and source_id=$3", AtlasTenant.Value, project.Id, finalized.InvoiceId.ToString("D")), System.Globalization.CultureInfo.InvariantCulture) == 1));
            Assert.Equal(actualAmount, Convert.ToDecimal(await ScalarAsync(connectionString, "select actualized_amount from business_projects.project_commitment where tenant_id=$1 and project_id=$2 and source_module='purchasing' and source_id=$3", AtlasTenant.Value, project.Id, purchaseOrderId.ToString("D")), System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(1000m - actualAmount, Convert.ToDecimal(await ScalarAsync(connectionString, "select open_amount from business_projects.project_commitment where tenant_id=$1 and project_id=$2 and source_module='purchasing' and source_id=$3", AtlasTenant.Value, project.Id, purchaseOrderId.ToString("D")), System.Globalization.CultureInfo.InvariantCulture));
            await harness.Bus.Publish(message);
            await Task.Delay(250);
            Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(connectionString, "select count(*) from business_projects.project_cost_fact where tenant_id=$1 and project_id=$2 and source_module='invoicing' and source_id=$3", AtlasTenant.Value, project.Id, finalized.InvoiceId.ToString("D")), System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal("PartiallyInvoiced", await ScalarAsync(connectionString, "select invoicing_state from business_purchasing.purchase_order where tenant_id=$1 and id=$2", AtlasTenant.Value, purchaseOrderId));
            Assert.Equal(1m, Convert.ToDecimal(await ScalarAsync(connectionString, "select invoiced_quantity from business_purchasing.purchase_order_line where tenant_id=$1 and purchase_order_id=$2", AtlasTenant.Value, purchaseOrderId), System.Globalization.CultureInfo.InvariantCulture));

            var correctionInvoiceId = await invoicing.CreateCreditNoteDraftAsync(AtlasTenant, actor, actor, new InvoiceCreditNoteRequest(finalized.InvoiceId, AtlasEntity,
                new(2026, 1, 20), new(2026, 1, 20), new(2026, 1, 20), new(2026, 1, 20), "Supplier invoice quantity correction",
                $"pj15-real-correction:{Guid.NewGuid():N}", [new(draft.Lines.Single().Id, 0.4m)]), Guid.CreateVersion7(), CancellationToken.None);
            var correctionDraft = await invoicing.GetDraftAsync(AtlasTenant, AtlasEntity, correctionInvoiceId, CancellationToken.None);
            var correctionFinalized = await invoicing.FinalizeAsync(AtlasTenant, actor, actor, correctionInvoiceId, AtlasEntity,
                new(correctionDraft.Version, $"pj15-real-correction-finalize:{Guid.NewGuid():N}"), Guid.CreateVersion7(), CancellationToken.None);
            var correctionMessage = await ReadOutboxAsync(connectionString, AtlasTenant, "InvoiceCorrectedV1", correctionInvoiceId);
            using var correctionPayload = JsonDocument.Parse(correctionMessage.Payload);
            var correctionAmount = correctionPayload.RootElement.GetProperty("amount").GetDecimal();
            Assert.InRange(correctionAmount, 0.01m, actualAmount - 0.01m);
            await harness.Bus.Publish(correctionMessage);
            Assert.True(await EventuallyAsync(async () => Convert.ToInt64(await ScalarAsync(connectionString, "select count(*) from business_projects.project_cost_fact where tenant_id=$1 and project_id=$2 and source_module='invoicing' and source_type='SupplierInvoiceCorrection' and source_id=$3", AtlasTenant.Value, project.Id, correctionFinalized.InvoiceId.ToString("D")), System.Globalization.CultureInfo.InvariantCulture) == 1));
            Assert.Equal("Corrected", await ScalarAsync(connectionString, "select state from business_projects.project_cost_fact where tenant_id=$1 and project_id=$2 and source_module='invoicing' and source_id=$3", AtlasTenant.Value, project.Id, finalized.InvoiceId.ToString("D")));
            Assert.Equal(actualAmount - correctionAmount, Convert.ToDecimal(await ScalarAsync(connectionString, "select actualized_amount from business_projects.project_commitment where tenant_id=$1 and project_id=$2 and source_module='purchasing' and source_id=$3", AtlasTenant.Value, project.Id, purchaseOrderId.ToString("D")), System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(1000m - actualAmount + correctionAmount, Convert.ToDecimal(await ScalarAsync(connectionString, "select open_amount from business_projects.project_commitment where tenant_id=$1 and project_id=$2 and source_module='purchasing' and source_id=$3", AtlasTenant.Value, project.Id, purchaseOrderId.ToString("D")), System.Globalization.CultureInfo.InvariantCulture));
            await harness.Bus.Publish(correctionMessage);
            await Task.Delay(250);
            Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(connectionString, "select count(*) from business_projects.project_integration_event where tenant_id=$1 and message_id=$2", AtlasTenant.Value, correctionMessage.MessageId), System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact(DisplayName = "Controlled supplier invoice cannot bypass a missing Planning reservation")]
    public async Task ControlledSupplierInvoiceCannotBypassMissingPlanningReservation()
    {
        var connectionString = RequireConnection();
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for controlled reservation acceptance.");
        using var factory = CreateFactory(connectionString);
        var purchasing = factory.Services.GetRequiredService<PurchasingService>();
        var invoicing = factory.Services.GetRequiredService<InvoicingService>();
        var actor = Guid.CreateVersion7();
        var supplier = await FindPartyAsync(connectionString, AtlasTenant, "Supplier");
        var policyId = Guid.CreateVersion7();
        var bucketId = Guid.CreateVersion7();
        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync();
            await SetTenantAsync(admin, AtlasTenant);
            var planVersion = await ScalarAsync(admin, "select id from business_planning.plan_version where tenant_id=$1 and state='Published' order by id limit 1", AtlasTenant.Value);
            Assert.NotNull(planVersion);
            await using var cleanupReconciliation = new NpgsqlCommand("delete from business_planning.budget_control_owner_reconciliation where tenant_id=$1 and bucket_id in (select id from business_planning.budget_control_bucket where tenant_id=$1 and legal_entity_id=$2 and period_start='2026-11-01' and metric_code='OPEX' and dimension_key='{}'::jsonb)", admin);
            cleanupReconciliation.Parameters.AddWithValue(AtlasTenant.Value); cleanupReconciliation.Parameters.AddWithValue(AtlasEntity); await cleanupReconciliation.ExecuteNonQueryAsync();
            await using var cleanupFacts = new NpgsqlCommand("delete from business_planning.budget_consumption_fact where tenant_id=$1 and bucket_id in (select id from business_planning.budget_control_bucket where tenant_id=$1 and legal_entity_id=$2 and period_start='2026-11-01' and metric_code='OPEX' and dimension_key='{}'::jsonb)", admin);
            cleanupFacts.Parameters.AddWithValue(AtlasTenant.Value); cleanupFacts.Parameters.AddWithValue(AtlasEntity); await cleanupFacts.ExecuteNonQueryAsync();
            await using var cleanupReservations = new NpgsqlCommand("delete from business_planning.budget_reservation where tenant_id=$1 and bucket_id in (select id from business_planning.budget_control_bucket where tenant_id=$1 and legal_entity_id=$2 and period_start='2026-11-01' and metric_code='OPEX' and dimension_key='{}'::jsonb)", admin);
            cleanupReservations.Parameters.AddWithValue(AtlasTenant.Value); cleanupReservations.Parameters.AddWithValue(AtlasEntity); await cleanupReservations.ExecuteNonQueryAsync();
            await using var cleanupBucket = new NpgsqlCommand("delete from business_planning.budget_control_bucket where tenant_id=$1 and legal_entity_id=$2 and period_start='2026-11-01' and metric_code='OPEX' and dimension_key='{}'::jsonb", admin);
            cleanupBucket.Parameters.AddWithValue(AtlasTenant.Value); cleanupBucket.Parameters.AddWithValue(AtlasEntity); await cleanupBucket.ExecuteNonQueryAsync();
            await using var cleanupPolicy = new NpgsqlCommand("delete from business_planning.budget_control_policy where tenant_id=$1 and legal_entity_id=$2 and name in ('BC01 controlled invoice test','BC01 invoice vector')", admin);
            cleanupPolicy.Parameters.AddWithValue(AtlasTenant.Value); cleanupPolicy.Parameters.AddWithValue(AtlasEntity); await cleanupPolicy.ExecuteNonQueryAsync();
            await using var policy = new NpgsqlCommand("insert into business_planning.budget_control_policy(tenant_id,id,legal_entity_id,name,mode,basis,threshold_percent,active,control_horizon,carryforward_mode,negative_budget_allowed,shadow_mode,opening_reconciled,effective_from) values($1,$2,$3,'BC01 controlled invoice test','Block','ActualOpenCommitmentProposed',100,true,'Monthly','None',false,false,true,'2026-11-01')", admin);
            policy.Parameters.AddWithValue(AtlasTenant.Value); policy.Parameters.AddWithValue(policyId); policy.Parameters.AddWithValue(AtlasEntity);
            await policy.ExecuteNonQueryAsync();
            await using var bucket = new NpgsqlCommand("insert into business_planning.budget_control_bucket(tenant_id,id,policy_id,plan_version_id,legal_entity_id,period_start,metric_code,dimension_key,original_budget,revision_total,transfer_in,transfer_out,actual_total,commitment_total,precommitment_total,currency_code,row_version) values($1,$2,$3,$4,$5,'2026-11-01','OPEX','{}',150000,0,0,0,0,0,0,'EGP',1)", admin);
            bucket.Parameters.AddWithValue(AtlasTenant.Value); bucket.Parameters.AddWithValue(bucketId); bucket.Parameters.AddWithValue(policyId); bucket.Parameters.AddWithValue((Guid)planVersion!); bucket.Parameters.AddWithValue(AtlasEntity);
            await bucket.ExecuteNonQueryAsync();
            await using var reconciliation = new NpgsqlCommand("insert into business_planning.budget_control_owner_reconciliation(tenant_id,id,bucket_id,state,source_count,expected_actual,expected_commitment,expected_precommitment,discrepancy_amount,source_watermark,completed_at,requested_by,created_at) values($1,$2,$3,'Succeeded',0,0,0,0,0,'bc01-controlled-fixture',now(),$4,now())", admin);
            reconciliation.Parameters.AddWithValue(AtlasTenant.Value); reconciliation.Parameters.AddWithValue(Guid.CreateVersion7()); reconciliation.Parameters.AddWithValue(bucketId); reconciliation.Parameters.AddWithValue(actor);
            await reconciliation.ExecuteNonQueryAsync();
        }
        var orderId = await purchasing.CreateAsync(AtlasTenant, actor,
            new(AtlasEntity, null, supplier, "EGP", new(2026, 11, 15), null, false,
                [new PurchaseOrderLineRequest(null, null, "EA", "Controlled source without reservation", 1m, 100m, "BC01 controlled reservation test")], null, null, "OPEX"),
            true, Guid.CreateVersion7(), CancellationToken.None);
        var draftOrder = await purchasing.GetAsync(AtlasTenant, AtlasEntity, orderId, true, CancellationToken.None);
        Assert.NotNull(draftOrder);
        await purchasing.ApproveAsync(AtlasTenant, actor, AtlasEntity, orderId,
            new(draftOrder!.Version, $"bc01-missing-reservation-approve:{orderId:D}"), Guid.CreateVersion7(), CancellationToken.None);
        var approvedOrder = await purchasing.GetAsync(AtlasTenant, AtlasEntity, orderId, true, CancellationToken.None);
        Assert.NotNull(approvedOrder);
        var invoiceId = await purchasing.CreateSupplierInvoiceAsync(AtlasTenant, actor, actor, AtlasEntity, orderId,
            new(approvedOrder!.Version, $"bc01-missing-reservation-invoice:{orderId:D}", new(2026, 11, 15), new(2026, 11, 15), new(2026, 11, 15), $"BC01-{orderId:D}",
                [new(approvedOrder.Lines.Single().Id, 1m)]), Guid.CreateVersion7(), CancellationToken.None);
        var invoice = await invoicing.GetDraftAsync(AtlasTenant, AtlasEntity, invoiceId, CancellationToken.None);

        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync();
            await SetTenantAsync(admin, AtlasTenant);
            await using var delete = new NpgsqlCommand("delete from business_planning.budget_reservation where tenant_id=$1 and source_module='purchasing' and source_document_id=$2", admin);
            delete.Parameters.AddWithValue(AtlasTenant.Value);
            delete.Parameters.AddWithValue(orderId.ToString("D"));
            Assert.Equal(1, await delete.ExecuteNonQueryAsync());
        }

        var exception = await Assert.ThrowsAsync<PlanningBudgetControlException>(() => invoicing.FinalizeAsync(AtlasTenant, actor, actor,
            invoiceId, AtlasEntity, new(invoice!.Version, $"bc01-missing-reservation-finalize:{invoiceId:D}"), Guid.CreateVersion7(), CancellationToken.None));
        Assert.Equal("planning.budget_reservation_missing", exception.Code);
        Assert.Equal("Draft", (await invoicing.GetDraftAsync(AtlasTenant, AtlasEntity, invoiceId, CancellationToken.None)).Lifecycle.ToString());
    }

    [Fact(DisplayName = "Budget Control partial and final supplier invoices conserve Planning totals")]
    public async Task BudgetControlPartialAndFinalSupplierInvoicesConservePlanningTotals()
    {
        var connectionString = RequireConnection();
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for Planning financial acceptance.");
        using var factory = CreateFactory(connectionString);
        var purchasing = factory.Services.GetRequiredService<PurchasingService>();
        var invoicing = factory.Services.GetRequiredService<InvoicingService>();
        var actor = Guid.CreateVersion7();
        var supplier = await FindPartyAsync(connectionString, AtlasTenant, "Supplier");
        var policyId = Guid.CreateVersion7();
        var bucketId = Guid.CreateVersion7();
        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync();
            await SetTenantAsync(admin, AtlasTenant);
            var planVersion = await ScalarAsync(admin, "select id from business_planning.plan_version where tenant_id=$1 and state='Published' order by id limit 1", AtlasTenant.Value);
            Assert.NotNull(planVersion);
            await using var cleanupReconciliation = new NpgsqlCommand("delete from business_planning.budget_control_owner_reconciliation where tenant_id=$1 and bucket_id in (select id from business_planning.budget_control_bucket where tenant_id=$1 and legal_entity_id=$2 and period_start='2026-11-01' and metric_code='OPEX' and dimension_key='{}'::jsonb)", admin);
            cleanupReconciliation.Parameters.AddWithValue(AtlasTenant.Value); cleanupReconciliation.Parameters.AddWithValue(AtlasEntity); await cleanupReconciliation.ExecuteNonQueryAsync();
            await using var cleanupFacts = new NpgsqlCommand("delete from business_planning.budget_consumption_fact where tenant_id=$1 and bucket_id in (select id from business_planning.budget_control_bucket where tenant_id=$1 and legal_entity_id=$2 and period_start='2026-11-01' and metric_code='OPEX' and dimension_key='{}'::jsonb)", admin);
            cleanupFacts.Parameters.AddWithValue(AtlasTenant.Value); cleanupFacts.Parameters.AddWithValue(AtlasEntity); await cleanupFacts.ExecuteNonQueryAsync();
            await using var cleanupReservations = new NpgsqlCommand("delete from business_planning.budget_reservation where tenant_id=$1 and bucket_id in (select id from business_planning.budget_control_bucket where tenant_id=$1 and legal_entity_id=$2 and period_start='2026-11-01' and metric_code='OPEX' and dimension_key='{}'::jsonb)", admin);
            cleanupReservations.Parameters.AddWithValue(AtlasTenant.Value); cleanupReservations.Parameters.AddWithValue(AtlasEntity); await cleanupReservations.ExecuteNonQueryAsync();
            await using var cleanupBucket = new NpgsqlCommand("delete from business_planning.budget_control_bucket where tenant_id=$1 and legal_entity_id=$2 and period_start='2026-11-01' and metric_code='OPEX' and dimension_key='{}'::jsonb", admin);
            cleanupBucket.Parameters.AddWithValue(AtlasTenant.Value); cleanupBucket.Parameters.AddWithValue(AtlasEntity); await cleanupBucket.ExecuteNonQueryAsync();
            await using var cleanupPolicy = new NpgsqlCommand("delete from business_planning.budget_control_policy where tenant_id=$1 and legal_entity_id=$2 and name in ('BC01 controlled invoice test','BC01 invoice vector')", admin);
            cleanupPolicy.Parameters.AddWithValue(AtlasTenant.Value); cleanupPolicy.Parameters.AddWithValue(AtlasEntity); await cleanupPolicy.ExecuteNonQueryAsync();
            await using var policy = new NpgsqlCommand("insert into business_planning.budget_control_policy(tenant_id,id,legal_entity_id,name,mode,basis,threshold_percent,active,control_horizon,carryforward_mode,negative_budget_allowed,shadow_mode,opening_reconciled,effective_from) values($1,$2,$3,'BC01 invoice vector','Warn','ActualOpenCommitmentProposed',100,true,'Monthly','None',false,false,false,'2026-11-01')", admin);
            policy.Parameters.AddWithValue(AtlasTenant.Value); policy.Parameters.AddWithValue(policyId); policy.Parameters.AddWithValue(AtlasEntity); await policy.ExecuteNonQueryAsync();
            await using var bucket = new NpgsqlCommand("insert into business_planning.budget_control_bucket(tenant_id,id,policy_id,plan_version_id,legal_entity_id,period_start,metric_code,dimension_key,original_budget,revision_total,transfer_in,transfer_out,actual_total,commitment_total,precommitment_total,currency_code,row_version) values($1,$2,$3,$4,$5,'2026-11-01','OPEX','{}',150000,0,0,0,0,0,0,'EGP',1)", admin);
            bucket.Parameters.AddWithValue(AtlasTenant.Value); bucket.Parameters.AddWithValue(bucketId); bucket.Parameters.AddWithValue(policyId); bucket.Parameters.AddWithValue((Guid)planVersion!); bucket.Parameters.AddWithValue(AtlasEntity); await bucket.ExecuteNonQueryAsync();
        }

        var orderId = await purchasing.CreateAsync(AtlasTenant, actor,
            new(AtlasEntity, null, supplier, "EGP", new(2026, 11, 15), null, false,
                [new PurchaseOrderLineRequest(null, null, "EA", "BC01 invoice vector", 100m, 1000m, "BC01 financial vector")]),
            true, Guid.CreateVersion7(), CancellationToken.None);
        var order = await purchasing.GetAsync(AtlasTenant, AtlasEntity, orderId, true, CancellationToken.None);
        await purchasing.ApproveAsync(AtlasTenant, actor, AtlasEntity, orderId, new(order!.Version, $"bc01-vector-approve:{orderId:D}"), Guid.CreateVersion7(), CancellationToken.None);
        order = await purchasing.GetAsync(AtlasTenant, AtlasEntity, orderId, true, CancellationToken.None);
        var lineId = order!.Lines.Single().Id;

        var partialId = await purchasing.CreateSupplierInvoiceAsync(AtlasTenant, actor, actor, AtlasEntity, orderId,
            new(order.Version, $"bc01-vector-partial:{orderId:D}", new(2026, 11, 15), new(2026, 11, 15), new(2026, 11, 15), $"BC01-P-{orderId:D}", [new(lineId, 40m)]), Guid.CreateVersion7(), CancellationToken.None);
        var partial = await invoicing.GetDraftAsync(AtlasTenant, AtlasEntity, partialId, CancellationToken.None);
        await invoicing.FinalizeAsync(AtlasTenant, actor, actor, partialId, AtlasEntity, new(partial!.Version, $"bc01-vector-partial-finalize:{partialId:D}"), Guid.CreateVersion7(), CancellationToken.None);
        await AssertPlanningVectorAsync(adminConnectionString, bucketId, 40000m, 60000m, "Reserved");

        order = await purchasing.GetAsync(AtlasTenant, AtlasEntity, orderId, true, CancellationToken.None);
        var finalId = await purchasing.CreateSupplierInvoiceAsync(AtlasTenant, actor, actor, AtlasEntity, orderId,
            new(order!.Version, $"bc01-vector-final:{orderId:D}", new(2026, 11, 15), new(2026, 11, 15), new(2026, 11, 15), $"BC01-F-{orderId:D}", [new(lineId, 60m)]), Guid.CreateVersion7(), CancellationToken.None);
        var final = await invoicing.GetDraftAsync(AtlasTenant, AtlasEntity, finalId, CancellationToken.None);
        var finalResult = await invoicing.FinalizeAsync(AtlasTenant, actor, actor, finalId, AtlasEntity, new(final!.Version, $"bc01-vector-final-finalize:{finalId:D}"), Guid.CreateVersion7(), CancellationToken.None);
        var replay = await invoicing.FinalizeAsync(AtlasTenant, actor, actor, finalId, AtlasEntity, new(final!.Version, $"bc01-vector-final-finalize:{finalId:D}"), Guid.CreateVersion7(), CancellationToken.None);
        Assert.Equal(finalResult.InvoiceId, replay.InvoiceId);
        await AssertPlanningVectorAsync(adminConnectionString, bucketId, 100000m, 0m, "Consumed");

        var cancelOrderId = await purchasing.CreateAsync(AtlasTenant, actor,
            new(AtlasEntity, null, supplier, "EGP", new(2026, 11, 20), null, false,
                [new PurchaseOrderLineRequest(null, null, "EA", "BC01 cancellation vector", 10m, 1000m, "BC01 cancellation test")]),
            true, Guid.CreateVersion7(), CancellationToken.None);
        var cancelOrder = await purchasing.GetAsync(AtlasTenant, AtlasEntity, cancelOrderId, true, CancellationToken.None);
        await purchasing.ApproveAsync(AtlasTenant, actor, AtlasEntity, cancelOrderId, new(cancelOrder!.Version, $"bc01-cancel-approve:{cancelOrderId:D}"), Guid.CreateVersion7(), CancellationToken.None);
        cancelOrder = await purchasing.GetAsync(AtlasTenant, AtlasEntity, cancelOrderId, true, CancellationToken.None);
        var cancelLineId = cancelOrder!.Lines.Single().Id;
        var cancelInvoiceId = await purchasing.CreateSupplierInvoiceAsync(AtlasTenant, actor, actor, AtlasEntity, cancelOrderId,
            new(cancelOrder.Version, $"bc01-cancel-partial:{cancelOrderId:D}", new(2026, 11, 20), new(2026, 11, 20), new(2026, 11, 20), $"BC01-C-{cancelOrderId:D}", [new(cancelLineId, 4m)]), Guid.CreateVersion7(), CancellationToken.None);
        var cancelInvoice = await invoicing.GetDraftAsync(AtlasTenant, AtlasEntity, cancelInvoiceId, CancellationToken.None);
        await invoicing.FinalizeAsync(AtlasTenant, actor, actor, cancelInvoiceId, AtlasEntity,
            new(cancelInvoice!.Version, $"bc01-cancel-partial-finalize:{cancelInvoiceId:D}"), Guid.CreateVersion7(), CancellationToken.None);
        cancelOrder = await purchasing.GetAsync(AtlasTenant, AtlasEntity, cancelOrderId, true, CancellationToken.None);
        await purchasing.CancelAsync(AtlasTenant, actor, AtlasEntity, cancelOrderId,
            new(cancelOrder!.Version, $"bc01-cancel:{cancelOrderId:D}", "BC01 cancellation after partial invoice"), Guid.CreateVersion7(), CancellationToken.None);
        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync();
            await SetTenantAsync(admin, AtlasTenant);
            await using var reservation = new NpgsqlCommand("select count(*) filter (where state='Reserved'),coalesce(sum(amount) filter (where state='Released'),0) from business_planning.budget_reservation where tenant_id=$1 and source_module='purchasing' and source_document_id=$2", admin);
            reservation.Parameters.AddWithValue(AtlasTenant.Value); reservation.Parameters.AddWithValue(cancelOrderId.ToString("D"));
            await using var reader = await reservation.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(0L, reader.GetInt64(0));
            Assert.Equal(6000m, reader.GetDecimal(1));
        }
        await AssertPlanningActualAsync(adminConnectionString, bucketId, 104000m);
    }

    [Fact(DisplayName = "BC-T05 concurrent competing Purchasing approvals conserve one strict commitment")]
    public async Task ConcurrentCompetingPurchasingApprovalsConserveOneStrictCommitment()
    {
        var connectionString = RequireConnection();
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for concurrent Budget Control acceptance.");
        using var factory = CreateFactory(connectionString);
        var supplier = await FindPartyAsync(connectionString, AtlasTenant, "Supplier");
        var actor = Guid.CreateVersion7();
        var policyId = Guid.CreateVersion7();
        var bucketId = Guid.CreateVersion7();
        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync();
            await SetTenantAsync(admin, AtlasTenant);
            await ExecuteCommandAsync(admin, "delete from business_planning.budget_consumption_fact where tenant_id=$1 and bucket_id in (select b.id from business_planning.budget_control_bucket b join business_planning.budget_control_policy p on p.tenant_id=b.tenant_id and p.id=b.policy_id where p.tenant_id=$1 and p.name='BC01 concurrent approval')", AtlasTenant.Value);
            await ExecuteCommandAsync(admin, "delete from business_planning.budget_reservation where tenant_id=$1 and bucket_id in (select b.id from business_planning.budget_control_bucket b join business_planning.budget_control_policy p on p.tenant_id=b.tenant_id and p.id=b.policy_id where p.tenant_id=$1 and p.name='BC01 concurrent approval')", AtlasTenant.Value);
            await ExecuteCommandAsync(admin, "delete from business_planning.budget_control_reconciliation_run where tenant_id=$1 and bucket_id in (select b.id from business_planning.budget_control_bucket b join business_planning.budget_control_policy p on p.tenant_id=b.tenant_id and p.id=b.policy_id where p.tenant_id=$1 and p.name='BC01 concurrent approval')", AtlasTenant.Value);
            await ExecuteCommandAsync(admin, "delete from business_planning.budget_control_owner_reconciliation where tenant_id=$1 and bucket_id in (select b.id from business_planning.budget_control_bucket b join business_planning.budget_control_policy p on p.tenant_id=b.tenant_id and p.id=b.policy_id where p.tenant_id=$1 and p.name='BC01 concurrent approval')", AtlasTenant.Value);
            await ExecuteCommandAsync(admin, "delete from business_planning.budget_control_bucket where tenant_id=$1 and policy_id in (select id from business_planning.budget_control_policy where tenant_id=$1 and name='BC01 concurrent approval')", AtlasTenant.Value);
            await ExecuteCommandAsync(admin, "delete from business_planning.budget_control_policy where tenant_id=$1 and name='BC01 concurrent approval'", AtlasTenant.Value);
            var planVersion = await ScalarAsync(admin, "select id from business_planning.plan_version where tenant_id=$1 and state='Published' order by id limit 1", AtlasTenant.Value);
            Assert.NotNull(planVersion);
            await using var policy = new NpgsqlCommand("insert into business_planning.budget_control_policy(tenant_id,id,legal_entity_id,name,mode,basis,threshold_percent,active,control_horizon,carryforward_mode,negative_budget_allowed,shadow_mode,opening_reconciled,effective_from) values($1,$2,$3,'BC01 concurrent approval','Warn','ActualOpenCommitmentProposed',100,true,'Monthly','None',false,false,true,'2026-12-01')", admin);
            policy.Parameters.AddWithValue(AtlasTenant.Value); policy.Parameters.AddWithValue(policyId); policy.Parameters.AddWithValue(AtlasEntity); await policy.ExecuteNonQueryAsync();
            await using var bucket = new NpgsqlCommand("insert into business_planning.budget_control_bucket(tenant_id,id,policy_id,plan_version_id,legal_entity_id,period_start,metric_code,dimension_key,original_budget,currency_code) values($1,$2,$3,$4,$5,'2026-12-01','OPEX','{}',100000, 'EGP')", admin);
            bucket.Parameters.AddWithValue(AtlasTenant.Value); bucket.Parameters.AddWithValue(bucketId); bucket.Parameters.AddWithValue(policyId); bucket.Parameters.AddWithValue((Guid)planVersion!); bucket.Parameters.AddWithValue(AtlasEntity); await bucket.ExecuteNonQueryAsync();
            await using var reconciliation = new NpgsqlCommand("insert into business_planning.budget_control_owner_reconciliation(tenant_id,id,bucket_id,state,source_count,expected_actual,expected_commitment,expected_precommitment,discrepancy_amount,source_watermark,completed_at,requested_by) values($1,$2,$3,'Succeeded',0,0,0,0,0,'bc-t05-fixture',now(),$4)", admin);
            reconciliation.Parameters.AddWithValue(AtlasTenant.Value); reconciliation.Parameters.AddWithValue(Guid.CreateVersion7()); reconciliation.Parameters.AddWithValue(bucketId); reconciliation.Parameters.AddWithValue(actor); await reconciliation.ExecuteNonQueryAsync();
        }

        var purchasing = factory.Services.GetRequiredService<PurchasingService>();
        var orderIds = new List<Guid>();
        try
        {
            foreach (var suffix in new[] { "A", "B" })
            {
                var id = await purchasing.CreateAsync(AtlasTenant, actor,
                    new(AtlasEntity, null, supplier, "EGP", new(2026, 12, 10), null, true,
                        [new PurchaseOrderLineRequest(null, null, "EA", $"BC-T05 source {suffix}", 1m, 80000m, "OPEX")]),
                    true, Guid.CreateVersion7(), CancellationToken.None);
                orderIds.Add(id);
            }

            await ExecuteAdminAsync(adminConnectionString, "update business_planning.budget_control_policy set mode='Block' where tenant_id=$1 and id=$2", AtlasTenant.Value, policyId);
            var first = await purchasing.GetAsync(AtlasTenant, AtlasEntity, orderIds[0], true, CancellationToken.None);
            var second = await purchasing.GetAsync(AtlasTenant, AtlasEntity, orderIds[1], true, CancellationToken.None);
            Assert.NotNull(first); Assert.NotNull(second);
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var readyCount = 0;
            async Task<Exception?> TryApproveAsync(Guid id, long version)
            {
                try
                {
                    await using var scope = factory.Services.CreateAsyncScope();
                    var scopedPurchasing = scope.ServiceProvider.GetRequiredService<PurchasingService>();
                    if (Interlocked.Increment(ref readyCount) == 2) ready.SetResult();
                    await ready.Task;
                    await scopedPurchasing.ApproveAsync(AtlasTenant, actor, AtlasEntity, id,
                        new(version, $"bc-t05-approve:{id:D}"), Guid.CreateVersion7(), CancellationToken.None);
                    return null;
                }
                catch (Exception exception) { return exception; }
            }

            var results = await Task.WhenAll(TryApproveAsync(first!.Id, first.Version), TryApproveAsync(second!.Id, second.Version));
            Assert.Single(results, exception => exception is null);
            var rejected = Assert.Single(results, exception => exception is not null);
            Assert.IsType<PlanningBudgetControlException>(rejected);
            Assert.Equal("planning.budget_insufficient", ((PlanningBudgetControlException)rejected).Code);

            await using var verify = new NpgsqlConnection(adminConnectionString); await verify.OpenAsync(); await SetTenantAsync(verify, AtlasTenant);
            Assert.Equal(2L, Convert.ToInt64(await ScalarAsync(verify, "select count(*) from business_purchasing.purchase_order where tenant_id=$1 and id=any($2)", AtlasTenant.Value, orderIds.ToArray()), System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(verify, "select count(*) from business_purchasing.purchase_order where tenant_id=$1 and id=any($2) and state='Approved'", AtlasTenant.Value, orderIds.ToArray()), System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(verify, "select count(*) from business_purchasing.purchase_order where tenant_id=$1 and id=any($2) and state='PendingApproval'", AtlasTenant.Value, orderIds.ToArray()), System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(80000m, Convert.ToDecimal(await ScalarAsync(verify, "select coalesce(sum(amount),0) from business_planning.budget_reservation where tenant_id=$1 and bucket_id=$2 and reservation_kind='Commitment' and state in ('Reserved','Consumed')", AtlasTenant.Value, bucketId), System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(verify, "select count(*) from business_planning.budget_reservation where tenant_id=$1 and bucket_id=$2 and reservation_kind='Commitment'", AtlasTenant.Value, bucketId), System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(20000m, Convert.ToDecimal(await ScalarAsync(verify, "select b.original_budget-b.actual_total-coalesce((select sum(amount) from business_planning.budget_reservation where tenant_id=$1 and bucket_id=b.id and state='Reserved' and reservation_kind='Commitment'),0) from business_planning.budget_control_bucket b where b.tenant_id=$1 and b.id=$2", AtlasTenant.Value, bucketId), System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(verify, "select count(*) from platform.outbox_message where tenant_id=$1 and message_type='PurchaseOrderApprovedV1' and payload->>'purchaseOrderId'=any($2)", AtlasTenant.Value, orderIds.Select(id => id.ToString("D")).ToArray()), System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            await ExecuteAdminAsync(adminConnectionString, "delete from business_planning.budget_consumption_fact where tenant_id=$1 and bucket_id=$2", AtlasTenant.Value, bucketId);
            await ExecuteAdminAsync(adminConnectionString, "delete from business_planning.budget_reservation where tenant_id=$1 and bucket_id=$2", AtlasTenant.Value, bucketId);
            await ExecuteAdminAsync(adminConnectionString, "delete from business_planning.budget_control_reconciliation_run where tenant_id=$1 and bucket_id=$2", AtlasTenant.Value, bucketId);
            await ExecuteAdminAsync(adminConnectionString, "delete from business_planning.budget_control_owner_reconciliation where tenant_id=$1 and bucket_id=$2", AtlasTenant.Value, bucketId);
            await ExecuteAdminAsync(adminConnectionString, "delete from business_planning.budget_control_bucket where tenant_id=$1 and id=$2", AtlasTenant.Value, bucketId);
            await ExecuteAdminAsync(adminConnectionString, "delete from business_planning.budget_control_policy where tenant_id=$1 and id=$2", AtlasTenant.Value, policyId);
        }
    }

    [Fact(DisplayName = "BC-T06 Purchasing reservation rolls back with the owner transaction and retry succeeds once")]
    public async Task PurchasingReservationRollsBackWithOwnerTransactionAndRetrySucceedsOnce()
    {
        var connectionString = RequireConnection();
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for rollback acceptance.");
        using var factory = CreateFactory(connectionString);
        var purchasing = factory.Services.GetRequiredService<PurchasingService>();
        var supplier = await FindPartyAsync(connectionString, AtlasTenant, "Supplier");
        var actor = Guid.CreateVersion7();
        var policyId = Guid.CreateVersion7();
        var bucketId = Guid.CreateVersion7();
        var functionName = $"bc01_t06_fail_{Guid.CreateVersion7():N}";
        var triggerName = $"bc01_t06_trigger_{Guid.CreateVersion7():N}";
        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync(); await SetTenantAsync(admin, AtlasTenant);
            await ExecuteCommandAsync(admin, "delete from business_planning.budget_consumption_fact where tenant_id=$1 and bucket_id in (select b.id from business_planning.budget_control_bucket b join business_planning.budget_control_policy p on p.tenant_id=b.tenant_id and p.id=b.policy_id where p.tenant_id=$1 and p.name='BC01 rollback')", AtlasTenant.Value);
            await ExecuteCommandAsync(admin, "delete from business_planning.budget_reservation where tenant_id=$1 and bucket_id in (select b.id from business_planning.budget_control_bucket b join business_planning.budget_control_policy p on p.tenant_id=b.tenant_id and p.id=b.policy_id where p.tenant_id=$1 and p.name='BC01 rollback')", AtlasTenant.Value);
            await ExecuteCommandAsync(admin, "delete from business_planning.budget_control_reconciliation_run where tenant_id=$1 and bucket_id in (select b.id from business_planning.budget_control_bucket b join business_planning.budget_control_policy p on p.tenant_id=b.tenant_id and p.id=b.policy_id where p.tenant_id=$1 and p.name='BC01 rollback')", AtlasTenant.Value);
            await ExecuteCommandAsync(admin, "delete from business_planning.budget_control_owner_reconciliation where tenant_id=$1 and bucket_id in (select b.id from business_planning.budget_control_bucket b join business_planning.budget_control_policy p on p.tenant_id=b.tenant_id and p.id=b.policy_id where p.tenant_id=$1 and p.name='BC01 rollback')", AtlasTenant.Value);
            await ExecuteCommandAsync(admin, "delete from business_planning.budget_control_bucket where tenant_id=$1 and policy_id in (select id from business_planning.budget_control_policy where tenant_id=$1 and name='BC01 rollback')", AtlasTenant.Value);
            await ExecuteCommandAsync(admin, "delete from business_planning.budget_control_policy where tenant_id=$1 and name='BC01 rollback'", AtlasTenant.Value);
            var planVersion = await ScalarAsync(admin, "select id from business_planning.plan_version where tenant_id=$1 and state='Published' order by id limit 1", AtlasTenant.Value);
            Assert.NotNull(planVersion);
            await ExecuteCommandAsync(admin, "insert into business_planning.budget_control_policy(tenant_id,id,legal_entity_id,name,mode,basis,threshold_percent,active,control_horizon,carryforward_mode,negative_budget_allowed,shadow_mode,opening_reconciled,effective_from) values($1,$2,$3,'BC01 rollback','Block','ActualOpenCommitmentProposed',100,true,'Monthly','None',false,false,true,'2026-12-01')", AtlasTenant.Value, policyId, AtlasEntity);
            await ExecuteCommandAsync(admin, "insert into business_planning.budget_control_bucket(tenant_id,id,policy_id,plan_version_id,legal_entity_id,period_start,metric_code,dimension_key,original_budget,currency_code) values($1,$2,$3,$4,$5,'2026-12-01','OPEX','{}',100000,'EGP')", AtlasTenant.Value, bucketId, policyId, (Guid)planVersion!, AtlasEntity);
            await ExecuteCommandAsync(admin, "insert into business_planning.budget_control_owner_reconciliation(tenant_id,id,bucket_id,state,source_count,expected_actual,expected_commitment,expected_precommitment,discrepancy_amount,source_watermark,completed_at,requested_by) values($1,$2,$3,'Succeeded',0,0,0,0,0,'bc-t06-fixture',now(),$4)", AtlasTenant.Value, Guid.CreateVersion7(), bucketId, actor);
            await ExecuteCommandAsync(admin, $"create or replace function public.{functionName}() returns trigger language plpgsql as $$ begin raise exception 'bc01_t06_injected_owner_failure'; end; $$;");
            await ExecuteCommandAsync(admin, $"create trigger {triggerName} before insert on business_purchasing.purchase_order for each row execute function public.{functionName}();");
        }

        var request = new CreatePurchaseOrderRequest(AtlasEntity, null, supplier, "EGP", new(2026, 12, 11), null, true,
            [new PurchaseOrderLineRequest(null, null, "EA", "BC-T06 rollback source", 1m, 30000m, "OPEX")]);
        var approvedEventCountBefore = Convert.ToInt64(await ScalarAsync(adminConnectionString, "select count(*) from platform.outbox_message where tenant_id=$1 and message_type='PurchaseOrderApprovedV1'", AtlasTenant.Value), System.Globalization.CultureInfo.InvariantCulture);
        var orderCountBefore = Convert.ToInt64(await ScalarAsync(adminConnectionString, "select count(*) from business_purchasing.purchase_order where tenant_id=$1 and order_date='2026-12-11'", AtlasTenant.Value), System.Globalization.CultureInfo.InvariantCulture);
        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() => purchasing.CreateAsync(AtlasTenant, actor, request, true, Guid.CreateVersion7(), CancellationToken.None));
            await using (var verify = new NpgsqlConnection(adminConnectionString))
            {
                await verify.OpenAsync(); await SetTenantAsync(verify, AtlasTenant);
                Assert.Equal(0L, Convert.ToInt64(await ScalarAsync(verify, "select count(*) from business_planning.budget_reservation where tenant_id=$1 and bucket_id=$2", AtlasTenant.Value, bucketId), System.Globalization.CultureInfo.InvariantCulture));
                Assert.Equal(orderCountBefore, Convert.ToInt64(await ScalarAsync(verify, "select count(*) from business_purchasing.purchase_order where tenant_id=$1 and order_date='2026-12-11'", AtlasTenant.Value), System.Globalization.CultureInfo.InvariantCulture));
                Assert.Equal(approvedEventCountBefore, Convert.ToInt64(await ScalarAsync(verify, "select count(*) from platform.outbox_message where tenant_id=$1 and message_type='PurchaseOrderApprovedV1'", AtlasTenant.Value), System.Globalization.CultureInfo.InvariantCulture));
            }
            await ExecuteAdminAsync(adminConnectionString, $"drop trigger {triggerName} on business_purchasing.purchase_order");
            var retryId = await purchasing.CreateAsync(AtlasTenant, actor, request, true, Guid.CreateVersion7(), CancellationToken.None);
            await using var recovered = new NpgsqlConnection(adminConnectionString); await recovered.OpenAsync(); await SetTenantAsync(recovered, AtlasTenant);
            Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(recovered, "select count(*) from business_purchasing.purchase_order where tenant_id=$1 and id=$2 and state='PendingApproval'", AtlasTenant.Value, retryId), System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(recovered, "select count(*) from business_planning.budget_reservation where tenant_id=$1 and bucket_id=$2 and source_module='purchasing' and source_document_id=$3 and state='Reserved'", AtlasTenant.Value, bucketId, retryId.ToString("D")), System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(30000m, Convert.ToDecimal(await ScalarAsync(recovered, "select amount from business_planning.budget_reservation where tenant_id=$1 and bucket_id=$2 and source_module='purchasing' and source_document_id=$3", AtlasTenant.Value, bucketId, retryId.ToString("D")), System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            await ExecuteAdminAsync(adminConnectionString, $"drop trigger if exists {triggerName} on business_purchasing.purchase_order");
            await ExecuteAdminAsync(adminConnectionString, $"drop function if exists public.{functionName}()");
            await ExecuteAdminAsync(adminConnectionString, "delete from business_planning.budget_consumption_fact where tenant_id=$1 and bucket_id=$2", AtlasTenant.Value, bucketId);
            await ExecuteAdminAsync(adminConnectionString, "delete from business_planning.budget_reservation where tenant_id=$1 and bucket_id=$2", AtlasTenant.Value, bucketId);
            await ExecuteAdminAsync(adminConnectionString, "delete from business_planning.budget_control_reconciliation_run where tenant_id=$1 and bucket_id=$2", AtlasTenant.Value, bucketId);
            await ExecuteAdminAsync(adminConnectionString, "delete from business_planning.budget_control_owner_reconciliation where tenant_id=$1 and bucket_id=$2", AtlasTenant.Value, bucketId);
            await ExecuteAdminAsync(adminConnectionString, "delete from business_planning.budget_control_bucket where tenant_id=$1 and id=$2", AtlasTenant.Value, bucketId);
            await ExecuteAdminAsync(adminConnectionString, "delete from business_planning.budget_control_policy where tenant_id=$1 and id=$2", AtlasTenant.Value, policyId);
        }
    }

    [Fact(DisplayName = "BC-T10 canonical owner reconciliation agrees with real expense, requisition, and purchase order facts")]
    public async Task CanonicalOwnerReconciliationAgreesWithRealSourceWorkflows()
    {
        var connectionString = RequireConnection();
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for canonical owner reconciliation acceptance.");
        using var factory = CreateFactory(connectionString);
        var actor = await FindAdminActorMembershipAsync(connectionString, AtlasTenant);
        var expenseApprover = await FindApprovalActorMembershipAsync(connectionString, AtlasTenant, actor.ActorId);
        await ExecuteAdminAsync(adminConnectionString, "insert into platform.role_permission(tenant_id,role_id,permission_code) select $1,id,'purchasing.request.approve' from platform.role where tenant_id=$1 and code='accountant' on conflict do nothing", AtlasTenant.Value);
        var purchasingApprover = await FindPermissionActorMembershipAsync(connectionString, AtlasTenant, actor.ActorId, "purchasing.request.approve");
        var supplier = await FindPartyAsync(connectionString, AtlasTenant, "Supplier");
        var planning = factory.Services.GetRequiredService<PlanningRepository>();
        var purchasing = factory.Services.GetRequiredService<PurchasingService>();
        await using var expenseDataSource = NpgsqlDataSource.Create(connectionString);
        var expenses = new ExpenseRepository(expenseDataSource,
            factory.Services.GetRequiredService<ITransactionCoordinator>(),
            factory.Services.GetRequiredService<ITransactionalLedgerPostingCapability>(),
            approvals: factory.Services.GetRequiredService<IPlatformApprovalCapability>(),
            financeBase: factory.Services.GetService<IFinanceBaseCapabilities>(),
            budgetControl: factory.Services.GetRequiredService<ITransactionalPlanningBudgetControlCapability>());
        DateOnly period;
        DateOnly postingDate;
        var policyId = Guid.CreateVersion7();
        var bucketId = Guid.CreateVersion7();
        var planVersionId = Guid.Empty;
        var fiscalYearId = Guid.Empty;
        var accountingPeriodId = Guid.Empty;

        await using (var admin = new NpgsqlConnection(adminConnectionString))
        {
            await admin.OpenAsync();
            await SetTenantAsync(admin, AtlasTenant);
            var periodYear = 4628;
            while (Convert.ToBoolean(await ScalarAsync(admin, "select exists(select 1 from finance_ledger.journal_entry where tenant_id=$1 and legal_entity_id=$2 and posting_date between $3 and $4 union all select 1 from business_purchasing.purchase_order where tenant_id=$1 and legal_entity_id=$2 and order_date between $3 and $4 union all select 1 from business_purchasing.purchase_requisition where tenant_id=$1 and legal_entity_id=$2 and requested_date between $3 and $4)", AtlasTenant.Value, AtlasEntity, new DateOnly(periodYear, 1, 1), new DateOnly(periodYear, 12, 31)), System.Globalization.CultureInfo.InvariantCulture))
                periodYear++;
            period = new DateOnly(periodYear, 1, 1);
            postingDate = new DateOnly(periodYear, 1, 15);
            fiscalYearId = Guid.CreateVersion7();
            accountingPeriodId = Guid.CreateVersion7();
            await ExecuteCommandAsync(admin, "delete from business_planning.budget_consumption_fact where tenant_id=$1 and bucket_id in (select id from business_planning.budget_control_bucket where tenant_id=$1 and policy_id in (select id from business_planning.budget_control_policy where tenant_id=$1 and name='BC-T10 canonical sources'))", AtlasTenant.Value);
            await ExecuteCommandAsync(admin, "delete from business_planning.budget_reservation where tenant_id=$1 and bucket_id in (select id from business_planning.budget_control_bucket where tenant_id=$1 and policy_id in (select id from business_planning.budget_control_policy where tenant_id=$1 and name='BC-T10 canonical sources'))", AtlasTenant.Value);
            await ExecuteCommandAsync(admin, "delete from business_planning.budget_control_reconciliation_run where tenant_id=$1 and bucket_id in (select id from business_planning.budget_control_bucket where tenant_id=$1 and policy_id in (select id from business_planning.budget_control_policy where tenant_id=$1 and name='BC-T10 canonical sources'))", AtlasTenant.Value);
            await ExecuteCommandAsync(admin, "delete from business_planning.budget_control_owner_reconciliation where tenant_id=$1 and bucket_id in (select id from business_planning.budget_control_bucket where tenant_id=$1 and policy_id in (select id from business_planning.budget_control_policy where tenant_id=$1 and name='BC-T10 canonical sources'))", AtlasTenant.Value);
            await ExecuteCommandAsync(admin, "delete from business_planning.budget_control_bucket where tenant_id=$1 and policy_id in (select id from business_planning.budget_control_policy where tenant_id=$1 and name='BC-T10 canonical sources')", AtlasTenant.Value);
            await ExecuteCommandAsync(admin, "delete from business_planning.budget_control_policy where tenant_id=$1 and name='BC-T10 canonical sources'", AtlasTenant.Value);
            planVersionId = (Guid)(await ScalarAsync(admin, "select id from business_planning.plan_version where tenant_id=$1 and state='Published' order by id limit 1", AtlasTenant.Value))!;
            await ExecuteCommandAsync(admin, "insert into finance_ledger.fiscal_year(tenant_id,legal_entity_id,id,code,start_date,end_date,status,version,created_at,updated_at) values($1,$2,$3,$4,$5,$6,'Open',1,now(),now()) on conflict (tenant_id,legal_entity_id,code) do nothing", AtlasTenant.Value, AtlasEntity, fiscalYearId, $"BC01-FY{period.Year}", period, period.AddYears(1).AddDays(-1));
            await ExecuteCommandAsync(admin, "insert into finance_ledger.accounting_period(tenant_id,legal_entity_id,id,fiscal_year_id,code,start_date,end_date,sequence_no,status,version,created_at,updated_at) values($1,$2,$3,$4,'01',$5,$6,1,'Open',1,now(),now()) on conflict (tenant_id,fiscal_year_id,code) do nothing", AtlasTenant.Value, AtlasEntity, accountingPeriodId, fiscalYearId, period, period.AddMonths(1).AddDays(-1));
            await ExecuteCommandAsync(admin, "insert into business_planning.budget_control_policy(tenant_id,id,legal_entity_id,name,mode,basis,threshold_percent,active,control_horizon,carryforward_mode,negative_budget_allowed,shadow_mode,opening_reconciled,effective_from) values($1,$2,$3,'BC-T10 canonical sources','Warn','ActualOpenCommitmentProposed',100,true,'Monthly','None',false,false,false,$4)", AtlasTenant.Value, policyId, AtlasEntity, period);
            await ExecuteCommandAsync(admin, "insert into business_planning.budget_control_bucket(tenant_id,id,policy_id,plan_version_id,legal_entity_id,period_start,metric_code,dimension_key,original_budget,currency_code) values($1,$2,$3,$4,$5,$6,'OPEX','{}'::jsonb,200000,'EGP')", AtlasTenant.Value, bucketId, policyId, planVersionId, AtlasEntity, period);
        }

        var categoryVersion = (Guid)(await ScalarAsync(connectionString,
            "select v.id from business_expenses.expense_category_version v join business_expenses.expense_category c on c.tenant_id=v.tenant_id and c.id=v.category_id where v.tenant_id=$1 and c.legal_entity_id=$2 and v.status='Published' order by v.effective_from desc limit 1",
            AtlasTenant.Value, AtlasEntity))!;

        var purchaseOrderId = await purchasing.CreateAsync(AtlasTenant, actor.ActorId,
            new(AtlasEntity, null, supplier, "EGP", postingDate, null, false,
                [new PurchaseOrderLineRequest(null, null, "EA", "BC-T10 open commitment", 1m, 60000m, "BC-T10")]),
            true, Guid.CreateVersion7(), CancellationToken.None);
        var purchaseOrder = await purchasing.GetAsync(AtlasTenant, AtlasEntity, purchaseOrderId, true, CancellationToken.None);
        Assert.NotNull(purchaseOrder);
        await purchasing.ApproveAsync(AtlasTenant, actor.ActorId, AtlasEntity, purchaseOrderId,
            new(purchaseOrder!.Version, $"bc-t10-po-approve:{purchaseOrderId:D}"), Guid.CreateVersion7(), CancellationToken.None);

        var requisitionId = await purchasing.CreateRequisitionAsync(AtlasTenant, actor.ActorId,
            new(AtlasEntity, supplier, "EGP", postingDate, true,
                [new PurchaseRequisitionLineRequest(null, null, "EA", "BC-T10 open precommitment", 1m, 20000m)]), CancellationToken.None);
        var requisition = await purchasing.GetRequisitionAsync(AtlasTenant, AtlasEntity, requisitionId, CancellationToken.None);
        Assert.NotNull(requisition);
        await purchasing.SubmitRequisitionAsync(AtlasTenant, actor.ActorId, actor.MembershipId, AtlasEntity, requisitionId,
            new(requisition!.Version, $"bc-t10-pr-submit:{requisitionId:D}"), CancellationToken.None);
        var pending = await purchasing.GetRequisitionAsync(AtlasTenant, AtlasEntity, requisitionId, CancellationToken.None);
        Assert.NotNull(pending);
        await purchasing.DecideRequisitionAsync(AtlasTenant, purchasingApprover.ActorId, purchasingApprover.MembershipId, AtlasEntity, requisitionId,
            new(pending!.Version, $"bc-t10-pr-approve:{requisitionId:D}"), "Approved", CancellationToken.None);

        var expense = await expenses.CreateAsync(AtlasTenant,
            new CreateExpenseRequest(AtlasEntity, null, "BC-T10 canonical actual", postingDate, 40000m, "EGP", "CompanyPaid", categoryVersion,
                "BC-T10 recognized actual", "Canonical owner-source fixture", BudgetBucketId: bucketId), CancellationToken.None);
        await expenses.AddEvidenceAsync(AtlasTenant, AtlasEntity, expense.Id, new("Receipt", $"file://bc-t10/{expense.Id:D}"), CancellationToken.None);
        var submittedExpense = await expenses.TransitionAsync(AtlasTenant, AtlasEntity, expense.Id, new(expense.Version), "submit", CancellationToken.None);
        await expenses.EvaluatePolicyAsync(AtlasTenant, AtlasEntity, expense.Id, CancellationToken.None);
        var expenseApproval = await expenses.RequestApprovalAsync(AtlasTenant, actor.ActorId, actor.MembershipId, AtlasEntity, expense.Id, "BC-T10 actual approval", CancellationToken.None);
        var expenseApproved = await expenses.DecideApprovalAsync(AtlasTenant, expenseApprover.ActorId, expenseApprover.MembershipId, AtlasEntity, expense.Id,
            new(expenseApproval.RequestId, "Approved", "BC-T10 actual approved"), CancellationToken.None);
        await expenses.TransitionAsync(AtlasTenant, AtlasEntity, expense.Id, new(expenseApproved.ResourceVersion + 1), "finalize", CancellationToken.None);

        var availabilityBefore = await planning.AvailabilityAsync(AtlasTenant, bucketId, CancellationToken.None);
        Assert.Equal(40000m, availabilityBefore.Actual);
        Assert.Equal(60000m, availabilityBefore.Commitment);
        Assert.Equal(20000m, availabilityBefore.Precommitment);
        Assert.Equal(80000m, availabilityBefore.Available);

        using var authenticatedFactory = CreateFactory(connectionString);
        using var client = authenticatedFactory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
        await ResetAndLoginAsync(adminConnectionString, client, "admin@atlas.com");
        using var reconcile = await PostWithCsrfAsync(client, "/api/v1/planning/budget-control/reconcile", new { bucketId, rebuild = false });
        Assert.Equal(HttpStatusCode.Accepted, reconcile.StatusCode);
        var reconcileBody = await ReadJsonAsync(reconcile);
        Assert.Equal("Succeeded", reconcileBody.GetProperty("state").GetString());
        Assert.Equal(80000m, reconcileBody.GetProperty("discrepancyAmount").GetDecimal());

        using var rebuild = await PostWithCsrfAsync(client, "/api/v1/planning/budget-control/reconcile", new { bucketId, rebuild = true });
        Assert.Equal(HttpStatusCode.Accepted, rebuild.StatusCode);
        var rebuildBody = await ReadJsonAsync(rebuild);
        Assert.True(rebuildBody.GetProperty("rebuildApplied").GetBoolean());
        Assert.Equal(80000m, rebuildBody.GetProperty("discrepancyAmount").GetDecimal());

        using var verified = await PostWithCsrfAsync(client, "/api/v1/planning/budget-control/reconcile", new { bucketId, rebuild = false });
        Assert.Equal(HttpStatusCode.Accepted, verified.StatusCode);
        var verifiedBody = await ReadJsonAsync(verified);
        Assert.Equal("Succeeded", verifiedBody.GetProperty("state").GetString());
        Assert.Equal(0m, verifiedBody.GetProperty("discrepancyAmount").GetDecimal());

        await ExecuteAdminAsync(adminConnectionString, "update business_planning.budget_control_policy set mode='Block',opening_reconciled=false,strict_activation_at=null where tenant_id=$1 and id=$2", AtlasTenant.Value, policyId);
        await using (var dataSource = NpgsqlDataSource.Create(connectionString))
        {
            var coordinator = new NpgsqlTransactionCoordinator(dataSource);
            var capability = new PlanningBudgetControlCapability();
            var blocked = await Assert.ThrowsAsync<PlanningBudgetControlException>(() => coordinator.ExecuteAsync(AtlasTenant, (context, token) => capability.UpsertReservationAsync(context, AtlasTenant, actor.ActorId,
                new(AtlasEntity, bucketId, "bc01", $"activation:{bucketId:D}", "line", "v1", 1m, "EGP", PlanningBudgetReservationKind.Commitment, period), token), default));
            Assert.Equal("planning.budget_opening_reconciliation_required", blocked.Code);
        }

        await ExecuteAdminAsync(adminConnectionString, "update business_planning.budget_control_policy set opening_reconciled=true,strict_activation_at=now()-interval '1 minute' where tenant_id=$1 and id=$2", AtlasTenant.Value, policyId);
        var activation = await planning.ReconcileBudgetControlAsync(AtlasTenant, actor.ActorId, new(bucketId, false), CancellationToken.None);
        Assert.Equal("Succeeded", activation.State);
        Assert.Equal(0m, activation.DiscrepancyAmount);
        var availabilityAfter = await planning.AvailabilityAsync(AtlasTenant, bucketId, CancellationToken.None);
        Assert.Equal(80000m, availabilityAfter.Available);
        Assert.Equal(40000m, availabilityAfter.Actual);
        Assert.Equal(60000m, availabilityAfter.Commitment);
        Assert.Equal(20000m, availabilityAfter.Precommitment);

        await ExecuteAdminAsync(adminConnectionString, "update business_planning.budget_control_policy set mode='Warn',opening_reconciled=true,strict_activation_at=null where tenant_id=$1 and id=$2", AtlasTenant.Value, policyId);
    }

    private static async Task AssertPlanningVectorAsync(string connectionString, Guid bucketId, decimal expectedActual, decimal expectedOpenCommitment, string expectedReservationState)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(); await SetTenantAsync(connection, AtlasTenant);
        await using var command = new NpgsqlCommand("select b.actual_total,b.original_budget-b.actual_total-coalesce((select amount from business_planning.budget_reservation where tenant_id=$1 and bucket_id=$2 and source_module='purchasing' and state='Reserved' order by created_at desc limit 1),0), (select amount from business_planning.budget_reservation where tenant_id=$1 and bucket_id=$2 and source_module='purchasing' order by created_at desc limit 1), (select state from business_planning.budget_reservation where tenant_id=$1 and bucket_id=$2 and source_module='purchasing' order by created_at desc limit 1) from business_planning.budget_control_bucket b where b.tenant_id=$1 and b.id=$2", connection);
        command.Parameters.AddWithValue(AtlasTenant.Value); command.Parameters.AddWithValue(bucketId);
        await using var reader = await command.ExecuteReaderAsync(); Assert.True(await reader.ReadAsync());
        Assert.Equal(expectedActual, reader.GetDecimal(0)); Assert.Equal(150000m - expectedActual - expectedOpenCommitment, reader.GetDecimal(1));
        if (expectedReservationState == "Reserved") Assert.Equal(expectedOpenCommitment, reader.GetDecimal(2));
        Assert.Equal(expectedReservationState, reader.GetString(3));
    }

    private static async Task AssertPlanningActualAsync(string connectionString, Guid bucketId, decimal expectedActual)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(); await SetTenantAsync(connection, AtlasTenant);
        await using var command = new NpgsqlCommand("select actual_total from business_planning.budget_control_bucket where tenant_id=$1 and id=$2", connection);
        command.Parameters.AddWithValue(AtlasTenant.Value); command.Parameters.AddWithValue(bucketId);
        Assert.Equal(expectedActual, Convert.ToDecimal(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
    }

    private static async Task ExecuteAdminAsync(string connectionString, string sql, params object[] values)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(); await SetTenantAsync(connection, AtlasTenant);
        await using var command = new NpgsqlCommand(sql, connection); foreach (var value in values) command.Parameters.AddWithValue(value); await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteCommandAsync(NpgsqlConnection connection, string sql, params object[] values)
    {
        await using var command = new NpgsqlCommand(sql, connection); foreach (var value in values) command.Parameters.AddWithValue(value); await command.ExecuteNonQueryAsync();
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
        .UseEnvironment("Development")
        .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TODIR_DATABASE_CONNECTION"] = connectionString
        }))
        .UseSetting("TODIR_DATABASE_CONNECTION", connectionString)
        .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));

    private static string RequireConnection() => Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION") ?? Environment.GetEnvironmentVariable("TODIR_PROJECTS_TEST_CONNECTION") ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION or TODIR_PROJECTS_TEST_CONNECTION.");

    private static async Task<Process> StartProjectsWorkerAsync(string connectionString, string rabbitHost, int rabbitPort, string rabbitUser, string rabbitPassword, string rabbitVHost)
    {
        var repository = new DirectoryInfo(Environment.GetEnvironmentVariable("TODIR_REPOSITORY_ROOT") ?? AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "backend", "DbMigrator", "DbMigrator.csproj"))) repository = repository.Parent;
        if (repository is null) throw new InvalidOperationException("The repository root could not be located for the Projects Worker process.");
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = new Process
        {
            StartInfo = new ProcessStartInfo(
                "dotnet",
                $"run --configuration Release --project backend/Worker/Worker.csproj --no-build --no-restore --no-launch-profile -- --TODIR_DATABASE_CONNECTION=\"{connectionString.Replace("\"", "\\\"", StringComparison.Ordinal)}\"")
            {
                WorkingDirectory = repository.FullName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        worker.StartInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";
        worker.StartInfo.Environment["TODIR_DATABASE_CONNECTION"] = connectionString;
        worker.StartInfo.Environment["TODIR_RABBITMQ_HOST"] = rabbitHost;
        worker.StartInfo.Environment["TODIR_RABBITMQ_PORT"] = rabbitPort.ToString(System.Globalization.CultureInfo.InvariantCulture);
        worker.StartInfo.Environment["TODIR_RABBITMQ_USER"] = rabbitUser;
        worker.StartInfo.Environment["TODIR_RABBITMQ_PASSWORD"] = rabbitPassword;
        worker.StartInfo.Environment["TODIR_RABBITMQ_VHOST"] = rabbitVHost;
        worker.StartInfo.Environment["TODIR_RABBITMQ_CONSUMER"] = "true";
        worker.OutputDataReceived += (_, args) =>
        {
            if (args.Data is null) return;
            Console.WriteLine($"[projects-worker] {args.Data}");
            if (args.Data.Contains("Application started.", StringComparison.Ordinal)) started.TrySetResult(true);
        };
        worker.ErrorDataReceived += (_, args) => { if (args.Data is not null) Console.Error.WriteLine($"[projects-worker] {args.Data}"); };
        worker.Start();
        worker.BeginOutputReadLine();
        worker.BeginErrorReadLine();
        var completed = await Task.WhenAny(started.Task, Task.Delay(TimeSpan.FromSeconds(30)));
        if (completed != started.Task)
        {
            await StopProjectsWorkerAsync(worker);
            throw new InvalidOperationException("The Projects Worker did not reach Application started within 30 seconds.");
        }
        return worker;
    }

    private static async Task StopProjectsWorkerAsync(Process worker)
    {
        if (!worker.HasExited)
        {
            try { worker.Kill(true); } catch { }
            await worker.WaitForExitAsync();
        }
        worker.Dispose();
    }

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

    private static async Task<(Guid WarehouseId, Guid LocationId)> ReadInventoryWarehouseAsync(string connectionString, TenantId tenant, Guid entity)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(); await SetTenantAsync(connection, tenant);
        await using var command = new NpgsqlCommand("select id,default_location_id from business_inventory.warehouse where tenant_id=$1 and legal_entity_id=$2 and status='Active' order by id limit 1", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(entity);
        await using var reader = await command.ExecuteReaderAsync(); Assert.True(await reader.ReadAsync(), "A seeded active Inventory warehouse is required.");
        return (reader.GetGuid(0), reader.GetGuid(1));
    }

    private static async Task<(Guid ActorId, Guid MembershipId)> FindAdminActorMembershipAsync(string connectionString, TenantId tenant)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(); await SetTenantAsync(connection, tenant);
        await using var command = new NpgsqlCommand("select i.id,m.id from platform.identity i join platform.membership m on m.tenant_id=i.tenant_id and m.identity_id=i.id where i.tenant_id=$1 and i.email='admin@atlas.com' and i.status='Active' and m.status='Active' limit 1", connection);
        command.Parameters.AddWithValue(tenant.Value);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "The Atlas administrator membership is required for payment acceptance.");
        return (reader.GetGuid(0), reader.GetGuid(1));
    }

    private static async Task<(Guid ActorId, Guid MembershipId)> FindApprovalActorMembershipAsync(string connectionString, TenantId tenant, Guid excludedActor)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(); await SetTenantAsync(connection, tenant);
        await using var command = new NpgsqlCommand("select i.id,m.id from platform.identity i join platform.membership m on m.tenant_id=i.tenant_id and m.identity_id=i.id join platform.membership_role_assignment a on a.tenant_id=m.tenant_id and a.membership_id=m.id join platform.role r on r.tenant_id=a.tenant_id and r.id=a.role_id join platform.role_permission p on p.tenant_id=r.tenant_id and p.role_id=r.id where i.tenant_id=$1 and i.id<>$2 and i.status='Active' and m.status='Active' and p.permission_code='expenses.approve' order by i.email limit 1", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(excludedActor);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), "A second seeded Expenses approver is required for separation of duties.");
        return (reader.GetGuid(0), reader.GetGuid(1));
    }

    private static async Task<(Guid ActorId, Guid MembershipId)> FindPermissionActorMembershipAsync(string connectionString, TenantId tenant, Guid excludedActor, string permission)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(); await SetTenantAsync(connection, tenant);
        await using var command = new NpgsqlCommand("select i.id,m.id from platform.identity i join platform.membership m on m.tenant_id=i.tenant_id and m.identity_id=i.id join platform.membership_role_assignment a on a.tenant_id=m.tenant_id and a.membership_id=m.id join platform.role r on r.tenant_id=a.tenant_id and r.id=a.role_id join platform.role_permission p on p.tenant_id=r.tenant_id and p.role_id=r.id where i.tenant_id=$1 and i.id<>$2 and i.status='Active' and m.status='Active' and p.permission_code=$3 order by i.email limit 1", connection);
        command.Parameters.AddWithValue(tenant.Value); command.Parameters.AddWithValue(excludedActor); command.Parameters.AddWithValue(permission);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync(), $"An active Atlas membership with {permission} is required.");
        return (reader.GetGuid(0), reader.GetGuid(1));
    }

    private static async Task ResetAndLoginAsync(string adminConnectionString, HttpClient client, string email)
    {
        var password = Environment.GetEnvironmentVariable("SEED_DEFAULT_PASSWORD") ?? "Bc01Http20261125";
        await ExecuteAdminAsync(adminConnectionString, "update platform.identity set password_hash=$1,must_change_password=false,status='Active',security_stamp=gen_random_uuid(),version=version+1 where tenant_id=$2 and email=$3", IdentityConventions.HashPassword(password), AtlasTenant.Value, email);
        using var csrf = await client.GetAsync("/api/v1/auth/csrf"); csrf.EnsureSuccessStatusCode();
        var token = (await csrf.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = JsonContent.Create(new { email, password }) };
        request.Headers.Add("X-CSRF-TOKEN", token);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostWithCsrfAsync(HttpClient client, string path, object body)
    {
        using var csrf = await client.GetAsync("/api/v1/auth/csrf"); csrf.EnsureSuccessStatusCode();
        var token = (await csrf.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", token);
        return await client.SendAsync(request);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    { using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()); return document.RootElement.Clone(); }

    private static async Task<Guid> FindPaymentAccountAsync(string connectionString, TenantId tenant, Guid legalEntityId)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(); await SetTenantAsync(connection, tenant);
        var value = await ScalarAsync(connection, "select id from finance_payments.payment_account where tenant_id=$1 and legal_entity_id=$2 and status='Active' and account_type='Bank' and (currency_code='EGP' or currency_code is null) order by id limit 1", tenant.Value, legalEntityId);
        Assert.NotNull(value);
        return (Guid)value!;
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
