extern alias WorkerAssembly;

using System.Diagnostics;
using System.Text.Json;
using Core.Messaging;
using Core.Services;
using Core.Transactions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Modules.FixedAssets.Services;
using Modules.Expenses.DTOs;
using Modules.Expenses.Repositories;
using Modules.Expenses.Services;
using Modules.Invoicing.Services;
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
        using var worker = StartProjectsWorker(connectionString, rabbitHost, rabbitPort, rabbitUser, rabbitPassword, rabbitVHost);
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
            Assert.Equal(1L, Convert.ToInt64(await ScalarAsync(connectionString, "select count(*) from business_projects.project_integration_event where tenant_id=$1 and message_id=$2 and status='Applied'", AtlasTenant.Value, acceptedMessage.MessageId), System.Globalization.CultureInfo.InvariantCulture));
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

    private static WebApplicationFactory<Program> CreateFactory(string connectionString) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
        .UseEnvironment("Development")
        .UseSetting("TODIR_DATABASE_CONNECTION", connectionString)
        .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));

    private static string RequireConnection() => Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION") ?? Environment.GetEnvironmentVariable("TODIR_PROJECTS_TEST_CONNECTION") ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION or TODIR_PROJECTS_TEST_CONNECTION.");

    private static Process StartProjectsWorker(string connectionString, string rabbitHost, int rabbitPort, string rabbitUser, string rabbitPassword, string rabbitVHost)
    {
        var repository = new DirectoryInfo(Environment.GetEnvironmentVariable("TODIR_REPOSITORY_ROOT") ?? AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "backend", "DbMigrator", "DbMigrator.csproj"))) repository = repository.Parent;
        if (repository is null) throw new InvalidOperationException("The repository root could not be located for the Projects Worker process.");
        var worker = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet", "run --project backend/Worker/Worker.csproj --no-build --no-restore --no-launch-profile")
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
        worker.OutputDataReceived += (_, args) => { if (args.Data is not null) Console.WriteLine($"[projects-worker] {args.Data}"); };
        worker.ErrorDataReceived += (_, args) => { if (args.Data is not null) Console.Error.WriteLine($"[projects-worker] {args.Data}"); };
        worker.Start();
        worker.BeginOutputReadLine();
        worker.BeginErrorReadLine();
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
