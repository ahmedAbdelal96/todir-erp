extern alias WorkerAssembly;

using System.Text.Json;
using Core.Messaging;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Modules.CRM.DTOs;
using Modules.CRM.Repositories;
using Modules.CRM.Services;
using Npgsql;
using Shared.Primitives.Identifiers;
using WorkerAssembly::Worker;
using Xunit;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class CrmPostgresIntegrationTests : DatabaseIntegrationTestBase
{
    [CrmPostgresFact]
    public async Task LeadOpportunityActivityForecastLifecyclePersistsAndIsIdempotent()
    {
        var admin = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")!;
        var runtime = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")!;
        var fixture = await FixtureAsync(admin);
        await using var dataSource = NpgsqlDataSource.Create(runtime);
        var repository = new CrmRepository(dataSource);
        var tenant = new TenantId(fixture.TenantId);
        var baseline = await repository.ForecastAsync(tenant, fixture.EntityId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60)), "EGP", default);
        var key = "crm-test-" + Guid.CreateVersion7().ToString("N");
        var lead = await repository.CreateLeadAsync(tenant, fixture.ActorId, new(fixture.EntityId, "اختبار فرصة", "شركة اختبار", "crm-test@example.test", "01099990000", "Integration"), key, default);
        var replay = await repository.CreateLeadAsync(tenant, fixture.ActorId, new(fixture.EntityId, "اختبار فرصة", "شركة اختبار", "crm-test@example.test", "01099990000", "Integration"), key, default);
        Assert.Equal(lead.Id, replay.Id);
        var duplicate = await repository.CreateLeadAsync(tenant, fixture.ActorId, new(fixture.EntityId, "اختبار فرصة مكرر", "شركة اختبار", "crm-test@example.test", "01099990000", "Integration"), key + "-duplicate", default);
        Assert.Contains(lead.Id, duplicate.DuplicateCandidateIds);
        var qualified = await repository.QualifyLeadAsync(tenant, fixture.ActorId, fixture.EntityId, lead.Id, new(null, "Need", "Budget", "Q4", "Manager", lead.Version), default);
        var opportunity = await repository.CreateOpportunityAsync(tenant, fixture.ActorId, new(fixture.EntityId, null, qualified.Id, fixture.PipelineId, fixture.OpenStageId, "فرصة اختبار", 12500m, "EGP", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20))), default);
        var moved = await repository.MoveStageAsync(tenant, fixture.ActorId, fixture.EntityId, opportunity.Id, new(fixture.ProposalStageId, opportunity.Version, "proposal"), default);
        var activity = await repository.CreateActivityAsync(tenant, fixture.ActorId, new(fixture.EntityId, null, opportunity.Id, null, "Call", "متابعة العميل", DateTimeOffset.UtcNow.AddDays(1), null, fixture.ActorId, "activity-" + Guid.CreateVersion7().ToString("N")), default);
        var completed = await repository.CompleteActivityAsync(tenant, fixture.ActorId, fixture.EntityId, activity.Id, new(activity.Version, "Completed", null, false), default);
        var forecast = await repository.ForecastAsync(tenant, fixture.EntityId, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60)), "EGP", default);
        Assert.Equal("Qualified", qualified.Status);
        Assert.Equal(fixture.ProposalStageId, moved.StageId);
        Assert.Equal("Completed", completed.State);
        Assert.Equal(12500m, forecast.PipelineAmount - baseline.PipelineAmount);
        Assert.Equal(baseline.OpenOpportunityCount + 1, forecast.OpenOpportunityCount);
        var otherTenantCount = await repository.ListLeadsAsync(new TenantId(fixture.OtherTenantId), fixture.OtherEntityId, fixture.ActorId, null, "crm-test@example.test", 1, 50, null, default);
        Assert.Equal(0, otherTenantCount.TotalCount);
    }

    [CrmPostgresFact]
    public async Task TeamVisibilityAndConversionUniquenessAreEnforced()
    {
        var admin = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")!;
        var runtime = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")!;
        var fixture = await FixtureAsync(admin);
        await using var dataSource = NpgsqlDataSource.Create(runtime);
        var repository = new CrmRepository(dataSource);
        var tenant = new TenantId(fixture.TenantId);
        var seller = Guid.CreateVersion7();
        var team = await repository.CreateTeamAsync(tenant, new(fixture.EntityId, "TEST-TEAM-" + Guid.CreateVersion7().ToString("N"), "CRM Test Team"), default);
        await repository.AddTeamMemberAsync(tenant, fixture.EntityId, team.Id, new(seller), default);
        var lead = await repository.CreateLeadAsync(tenant, fixture.ActorId, new(fixture.EntityId, "Team lead", null, "team-lead@example.test", null, "Integration", null, team.Id), "team-lead-" + Guid.CreateVersion7().ToString("N"), default);
        var visible = await repository.ListLeadsAsync(tenant, fixture.EntityId, seller, null, "team-lead@example.test", 1, 20, "mine", default);
        Assert.Contains(visible.Items, x => x.Id == lead.Id);
        var qualified = await repository.QualifyLeadAsync(tenant, fixture.ActorId, fixture.EntityId, lead.Id, new(null, "Need", null, null, null, lead.Version), default);
        var first = await repository.CreateOpportunityAsync(tenant, fixture.ActorId, new(fixture.EntityId, null, qualified.Id, fixture.PipelineId, fixture.OpenStageId, "Converted once", 100m, "EGP", null), default);
        var second = await repository.CreateOpportunityAsync(tenant, fixture.ActorId, new(fixture.EntityId, null, qualified.Id, fixture.PipelineId, fixture.OpenStageId, "Converted retry", 100m, "EGP", null), default);
        Assert.Equal(first.Id, second.Id);
        await repository.CreateActivityAsync(tenant, fixture.ActorId, new(fixture.EntityId, null, first.Id, null, "Call", "Overdue follow-up", DateTimeOffset.UtcNow.AddDays(-2), null, fixture.ActorId, "attention-" + Guid.CreateVersion7().ToString("N")), default);
        var attention = await repository.ListSellerActivitiesAsync(tenant, fixture.EntityId, fixture.ActorId, "attention", false, 1, 20, default);
        Assert.Contains(attention.Items, x => x.Subject == "Overdue follow-up");
    }

    [CrmPostgresFact]
    public async Task ConfigurationCustomFieldsAndForecastSnapshotPersistWithConcurrencyTokens()
    {
        var admin = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")!;
        var runtime = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")!;
        var fixture = await FixtureAsync(admin);
        await using var dataSource = NpgsqlDataSource.Create(runtime);
        var repository = new CrmRepository(dataSource);
        var tenant = new TenantId(fixture.TenantId);
        try
        {
            var fieldKey = "budget_" + Guid.CreateVersion7().ToString("N");
            var definition = await repository.CreateCustomFieldDefinitionAsync(tenant, new(fixture.EntityId, "Lead", fieldKey, "Budget", "Decimal"), default);
            var lead = await repository.CreateLeadAsync(tenant, fixture.ActorId, new(fixture.EntityId, "Custom field lead", null, "custom-field@example.test", null, "Integration"), "custom-field-" + Guid.CreateVersion7().ToString("N"), default);
            using var budget = JsonDocument.Parse("12500.50");
            var values = await repository.SetCustomFieldValuesAsync(tenant, fixture.ActorId, fixture.EntityId, new("Lead", lead.Id, new Dictionary<string, JsonElement> { [fieldKey] = budget.RootElement.Clone() }), default);
            Assert.Equal(fieldKey, values.Single().FieldKey);
            Assert.Contains((await repository.ListCustomFieldDefinitionsAsync(tenant, fixture.EntityId, "Lead", default)), item => item.Id == definition.Id);
            var testPipeline = await repository.CreatePipelineAsync(tenant, new(fixture.EntityId, "CF-PIPE-" + Guid.CreateVersion7().ToString("N"), "Custom Field Pipeline"), default);
            var stage1 = await repository.CreateStageAsync(tenant, fixture.EntityId, testPipeline.Id, new("Stage 1", 1, 0.1m, 14, false, false), default);
            var stage2 = await repository.CreateStageAsync(tenant, fixture.EntityId, testPipeline.Id, new("Stage 2", 2, 0.5m, 14, false, false), default);
            var reordered = await repository.ReorderStagesAsync(tenant, fixture.EntityId, testPipeline.Id, new([stage2.Id, stage1.Id], testPipeline.Version), default);
            Assert.Equal(2, reordered.Count);
            var requiredKey = "decision_" + Guid.CreateVersion7().ToString("N");
            await repository.CreateCustomFieldDefinitionAsync(tenant, new(fixture.EntityId, "Opportunity", requiredKey, "Decision maker", "Text", stage2.Id), default);
            var opportunity = await repository.CreateOpportunityAsync(tenant, fixture.ActorId, new(fixture.EntityId, null, null, testPipeline.Id, stage1.Id, "Required field transition", 100m, "EGP", null), default);
            await Assert.ThrowsAsync<CrmException>(() => repository.MoveStageAsync(tenant, fixture.ActorId, fixture.EntityId, opportunity.Id, new(stage2.Id, opportunity.Version), default));
            using var decision = JsonDocument.Parse("\"CFO\"");
            await repository.SetCustomFieldValuesAsync(tenant, fixture.ActorId, fixture.EntityId, new("Opportunity", opportunity.Id, new Dictionary<string, JsonElement> { [requiredKey] = decision.RootElement.Clone() }), default);
            var moved = await repository.MoveStageAsync(tenant, fixture.ActorId, fixture.EntityId, opportunity.Id, new(stage2.Id, opportunity.Version), default);
            Assert.Equal(stage2.Id, moved.StageId);
            var snapshot = await repository.CreateForecastSnapshotAsync(tenant, fixture.ActorId, fixture.EntityId, new(DateOnly.FromDateTime(DateTime.UtcNow), "EGP"), default);
            Assert.True(snapshot.OpenOpportunityCount >= 0);
        }
        finally
        {
            await using var conn = await dataSource.OpenConnectionAsync();
            await using var setTenant = new NpgsqlCommand("select set_config('app.tenant_id', $1, false)", conn);
            setTenant.Parameters.AddWithValue(tenant.Value.ToString("D"));
            await setTenant.ExecuteNonQueryAsync();

            await using var cleanupVal = new NpgsqlCommand("delete from business_crm.crm_custom_field_value where tenant_id=$1", conn);
            cleanupVal.Parameters.AddWithValue(tenant.Value);
            await cleanupVal.ExecuteNonQueryAsync();

            await using var cleanupDef = new NpgsqlCommand("delete from business_crm.crm_custom_field_definition where tenant_id=$1", conn);
            cleanupDef.Parameters.AddWithValue(tenant.Value);
            await cleanupDef.ExecuteNonQueryAsync();
        }
    }

    [CrmPostgresFact]
    public async Task ExternalEventLifecycleConsumerAndInboxIdempotency()
    {
        var admin = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")!;
        var runtime = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")!;
        var fixture = await FixtureAsync(admin);
        await using var dataSource = NpgsqlDataSource.Create(runtime);
        var repository = new CrmRepository(dataSource);
        var inboxStore = new NpgsqlInboxStore(dataSource);
        var tenant = new TenantId(fixture.TenantId);

        // 1. Create opportunity and sales quotation source link
        var opportunity = await repository.CreateOpportunityAsync(tenant, fixture.ActorId, new(fixture.EntityId, null, null, fixture.PipelineId, fixture.OpenStageId, "Event Lifecycle Deal", 50000m, "EGP", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30))), default);
        var quotationId = Guid.CreateVersion7();
        var quoteKey = "quote-link-" + Guid.CreateVersion7().ToString("N");
        await repository.RecordQuoteLinkAsync(tenant, fixture.EntityId, opportunity.Id, quotationId, quoteKey, default);

        var invoiceId = Guid.CreateVersion7();
        await using (var conn = await dataSource.OpenConnectionAsync())
        {
            await using var setTenant = new NpgsqlCommand("select set_config('app.tenant_id', $1, false)", conn);
            setTenant.Parameters.AddWithValue(tenant.Value.ToString("D"));
            await setTenant.ExecuteNonQueryAsync();

            await using var insertInvoiceLink = new NpgsqlCommand("insert into business_crm.opportunity_source_link(tenant_id,id,legal_entity_id,opportunity_id,target_module,target_type,target_id,purpose,idempotency_key,payload_hash,created_at) values($1,$2,$3,$4,'invoicing','Invoice',$5,'CreateInvoice',$6,$7,now()) on conflict do nothing", conn);
            insertInvoiceLink.Parameters.AddWithValue(tenant.Value);
            insertInvoiceLink.Parameters.AddWithValue(Guid.CreateVersion7());
            insertInvoiceLink.Parameters.AddWithValue(fixture.EntityId);
            insertInvoiceLink.Parameters.AddWithValue(opportunity.Id);
            insertInvoiceLink.Parameters.AddWithValue(invoiceId);
            insertInvoiceLink.Parameters.AddWithValue("invoice-key-" + invoiceId.ToString("N"));
            insertInvoiceLink.Parameters.AddWithValue(new string('a', 64));
            await insertInvoiceLink.ExecuteNonQueryAsync();
        }

        // 2. Setup MassTransit Test Harness with real CrmExternalEventConsumer and PostgreSQL
        var services = new ServiceCollection();
        services.AddSingleton(dataSource);
        services.AddSingleton<IInboxStore>(inboxStore);
        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<CrmExternalEventConsumer>();
        });
        await using var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();

        await harness.Start();
        try
        {
            // 3. Deliver QuotationAcceptedV1 message
            var quoteMsgId = Guid.CreateVersion7();
            var quotePayload = JsonSerializer.Serialize(new { tenantId = tenant.Value, legalEntityId = fixture.EntityId, quotationId });
            var quoteMessage = new OutboxPublishedMessage(quoteMsgId, "QuotationAcceptedV1", quotePayload, tenant, DateTimeOffset.UtcNow, Guid.CreateVersion7(), Guid.CreateVersion7(), "sales", null);

            await harness.Bus.Publish(quoteMessage);
            Assert.True(await harness.Consumed.Any<OutboxPublishedMessage>(x => x.Context.Message.MessageId == quoteMsgId), "QuotationAcceptedV1 was not consumed.");

            // Verify CRM timeline has the external event
            var events = await repository.ListExternalEventsAsync(tenant, fixture.EntityId, "QuotationAcceptedV1", 1, 10, default);
            Assert.Contains(events.Items, x => x.MessageId == quoteMsgId && x.MessageType == "QuotationAcceptedV1" && x.EntityId == quotationId);

            // Verify source link state transition to Accepted
            await using (var conn = await dataSource.OpenConnectionAsync())
            {
                await using var setTenant = new NpgsqlCommand("select set_config('app.tenant_id', $1, false)", conn);
                setTenant.Parameters.AddWithValue(tenant.Value.ToString("D"));
                await setTenant.ExecuteNonQueryAsync();

                await using var checkLink = new NpgsqlCommand("select external_state, last_external_event_at from business_crm.opportunity_source_link where tenant_id=$1 and opportunity_id=$2 and target_module='sales' and target_id=$3", conn);
                checkLink.Parameters.AddWithValue(tenant.Value);
                checkLink.Parameters.AddWithValue(opportunity.Id);
                checkLink.Parameters.AddWithValue(quotationId);
                await using var rdr = await checkLink.ExecuteReaderAsync();
                Assert.True(await rdr.ReadAsync());
                Assert.Equal("Accepted", rdr.GetString(0));
                Assert.False(rdr.IsDBNull(1));
            }

            // 4. Test Event Replay & Inbox Idempotency: Send the exact same message again!
            await harness.Bus.Publish(quoteMessage);
            await Task.Delay(500);

            // Verify zero duplicates in crm_external_event
            await using (var conn = await dataSource.OpenConnectionAsync())
            {
                await using var setTenant = new NpgsqlCommand("select set_config('app.tenant_id', $1, false)", conn);
                setTenant.Parameters.AddWithValue(tenant.Value.ToString("D"));
                await setTenant.ExecuteNonQueryAsync();

                await using var countCmd = new NpgsqlCommand("select count(*) from business_crm.crm_external_event where tenant_id=$1 and message_id=$2", conn);
                countCmd.Parameters.AddWithValue(tenant.Value);
                countCmd.Parameters.AddWithValue(quoteMsgId);
                var eventCount = (long)(await countCmd.ExecuteScalarAsync())!;
                Assert.Equal(1L, eventCount);
            }

            // 5. Test InvoicePostedV1 transition
            var invMsgId = Guid.CreateVersion7();
            var invPayload = JsonSerializer.Serialize(new { tenantId = tenant.Value, legalEntityId = fixture.EntityId, invoiceId });
            var invMessage = new OutboxPublishedMessage(invMsgId, "InvoicePostedV1", invPayload, tenant, DateTimeOffset.UtcNow, Guid.CreateVersion7(), Guid.CreateVersion7(), "invoicing", null);
            await harness.Bus.Publish(invMessage);
            Assert.True(await harness.Consumed.Any<OutboxPublishedMessage>(x => x.Context.Message.MessageId == invMsgId), "InvoicePostedV1 was not consumed.");

            await using (var conn = await dataSource.OpenConnectionAsync())
            {
                await using var setTenant = new NpgsqlCommand("select set_config('app.tenant_id', $1, false)", conn);
                setTenant.Parameters.AddWithValue(tenant.Value.ToString("D"));
                await setTenant.ExecuteNonQueryAsync();

                await using var checkInvLink = new NpgsqlCommand("select external_state from business_crm.opportunity_source_link where tenant_id=$1 and opportunity_id=$2 and target_module='invoicing' and target_id=$3", conn);
                checkInvLink.Parameters.AddWithValue(tenant.Value);
                checkInvLink.Parameters.AddWithValue(opportunity.Id);
                checkInvLink.Parameters.AddWithValue(invoiceId);
                var invState = (string)(await checkInvLink.ExecuteScalarAsync())!;
                Assert.Equal("InvoicePosted", invState);
            }

            // 6. Test Inbox Abandon & Retry behavior
            var retryMsgId = Guid.CreateVersion7();
            var beganFirst = await inboxStore.TryBeginAsync("test-retry-endpoint", retryMsgId, tenant, default);
            Assert.True(beganFirst, "Initial TryBeginAsync should succeed.");
            var beganSecond = await inboxStore.TryBeginAsync("test-retry-endpoint", retryMsgId, tenant, default);
            Assert.False(beganSecond, "Concurrent TryBeginAsync before lease expiration should fail.");
            await inboxStore.AbandonAsync("test-retry-endpoint", retryMsgId, tenant, default);
            var beganAfterAbandon = await inboxStore.TryBeginAsync("test-retry-endpoint", retryMsgId, tenant, default);
            Assert.True(beganAfterAbandon, "TryBeginAsync after AbandonAsync should succeed, enabling worker retry.");
        }
        finally
        {
            await harness.Stop();
        }
    }

    [CrmPostgresFact]
    public async Task DealHealthCalculationAndReportingMetricsBusinessTruth()
    {
        var admin = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")!;
        var runtime = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")!;
        var fixture = await FixtureAsync(admin);
        await using var dataSource = NpgsqlDataSource.Create(runtime);
        var repository = new CrmRepository(dataSource);
        var tenant = new TenantId(fixture.TenantId);

        // 1. Create a Healthy opportunity (active, close date in future)
        var healthyOpp = await repository.CreateOpportunityAsync(tenant, fixture.ActorId, new(fixture.EntityId, null, null, fixture.PipelineId, fixture.OpenStageId, "Healthy Opportunity " + Guid.CreateVersion7().ToString("N"), 20000m, "EGP", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(25))), default);
        var healthy = await repository.GetOpportunityAsync(tenant, fixture.EntityId, healthyOpp.Id, default);
        Assert.NotNull(healthy);
        Assert.Equal("Healthy", healthy!.HealthStatus);
        Assert.Equal(100, healthy.HealthScore);

        // 2. Create an AtRisk opportunity (expected close date in past)
        var atRiskOpp = await repository.CreateOpportunityAsync(tenant, fixture.ActorId, new(fixture.EntityId, null, null, fixture.PipelineId, fixture.OpenStageId, "At Risk Opportunity " + Guid.CreateVersion7().ToString("N"), 15000m, "EGP", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5))), default);
        var atRisk = await repository.GetOpportunityAsync(tenant, fixture.EntityId, atRiskOpp.Id, default);
        Assert.NotNull(atRisk);
        Assert.Equal("AtRisk", atRisk!.HealthStatus);
        Assert.Equal(50, atRisk.HealthScore);

        // 3. Create a Stale opportunity (inactivity > stale_after_days = 30)
        var staleOpp = await repository.CreateOpportunityAsync(tenant, fixture.ActorId, new(fixture.EntityId, null, null, fixture.PipelineId, fixture.OpenStageId, "Stale Opportunity " + Guid.CreateVersion7().ToString("N"), 10000m, "EGP", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20))), default);
        await using (var conn = await dataSource.OpenConnectionAsync())
        {
            await using var setTenant = new NpgsqlCommand("select set_config('app.tenant_id', $1, false)", conn);
            setTenant.Parameters.AddWithValue(tenant.Value.ToString("D"));
            await setTenant.ExecuteNonQueryAsync();

            await using var setStale = new NpgsqlCommand("update business_crm.opportunity set last_activity_at = now() - interval '40 days' where tenant_id = $1 and id = $2", conn);
            setStale.Parameters.AddWithValue(tenant.Value);
            setStale.Parameters.AddWithValue(staleOpp.Id);
            await setStale.ExecuteNonQueryAsync();
        }

        var stale = await repository.GetOpportunityAsync(tenant, fixture.EntityId, staleOpp.Id, default);
        Assert.NotNull(stale);
        Assert.Equal("Stale", stale!.HealthStatus);
        Assert.Equal(20, stale.HealthScore);

        // 4. Verify filtering by health_status in ListOpportunitiesAsync
        var staleList = await repository.ListOpportunitiesAsync(tenant, fixture.EntityId, null, "Stale", fixture.PipelineId, null, 1, 50, "all", default);
        Assert.Contains(staleList.Items, x => x.Id == staleOpp.Id);

        var atRiskList = await repository.ListOpportunitiesAsync(tenant, fixture.EntityId, null, "AtRisk", fixture.PipelineId, null, 1, 50, "all", default);
        Assert.Contains(atRiskList.Items, x => x.Id == atRiskOpp.Id);

        // 5. Test Health Recovery via Activity Completion
        var recoveryAct = await repository.CreateActivityAsync(tenant, fixture.ActorId, new(fixture.EntityId, null, staleOpp.Id, null, "Call", "Re-engagement Call", DateTimeOffset.UtcNow, null, fixture.ActorId, "act-recov-" + Guid.CreateVersion7().ToString("N")), default);
        await repository.CompleteActivityAsync(tenant, fixture.ActorId, fixture.EntityId, recoveryAct.Id, new(recoveryAct.Version, "Completed", "Client re-engaged", false), default);

        var recovered = await repository.GetOpportunityAsync(tenant, fixture.EntityId, staleOpp.Id, default);
        Assert.NotNull(recovered);
        Assert.Equal("Healthy", recovered!.HealthStatus);
        Assert.Equal(100, recovered.HealthScore);

        // 6. Test Won & Lost opportunity health
        var wonOpp = await repository.CreateOpportunityAsync(tenant, fixture.ActorId, new(fixture.EntityId, null, null, fixture.PipelineId, fixture.OpenStageId, "Won Deal " + Guid.CreateVersion7().ToString("N"), 30000m, "EGP", null), default);
        var won = await repository.MarkWonAsync(tenant, fixture.ActorId, fixture.EntityId, wonOpp.Id, new(wonOpp.Version, "Customer signed contract"), default);
        Assert.Equal("Healthy", won.HealthStatus);
        Assert.Equal(100, won.HealthScore);

        var lostOpp = await repository.CreateOpportunityAsync(tenant, fixture.ActorId, new(fixture.EntityId, null, null, fixture.PipelineId, fixture.OpenStageId, "Lost Deal " + Guid.CreateVersion7().ToString("N"), 10000m, "EGP", null), default);
        var lost = await repository.MarkLostAsync(tenant, fixture.ActorId, fixture.EntityId, lostOpp.Id, new(lostOpp.Version, "Competitor chosen"), default);
        Assert.Equal("Stale", lost.HealthStatus);
        Assert.Equal(0, lost.HealthScore);

        // 7. Verify Win Rate vs Conversion Rate business truth in ReportAsync
        var report = await repository.ReportAsync(tenant, fixture.EntityId, null, "EGP", default);
        Assert.True(report.WonOpportunityCount + report.LostOpportunityCount > 0);
        Assert.True(report.OpportunityWinRate >= 0 && report.OpportunityWinRate <= 100);
        Assert.True(report.LeadConversionRate >= 0 && report.LeadConversionRate <= 100);
    }

    [CrmPostgresFact]
    public async Task CrmToSalesHandoffDirectoryConversionAndIdempotency()
    {
        var admin = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")!;
        var runtime = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")!;
        var fixture = await FixtureAsync(admin);
        await using var dataSource = NpgsqlDataSource.Create(runtime);
        var repository = new CrmRepository(dataSource);
        var tenant = new TenantId(fixture.TenantId);

        // 1. Lead creation
        var leadKey = "handoff-lead-" + Guid.CreateVersion7().ToString("N");
        var lead = await repository.CreateLeadAsync(tenant, fixture.ActorId, new(fixture.EntityId, "محمود العربي", "مجموعة العربي للصناعة", "araby-" + Guid.CreateVersion7().ToString("N") + "@example.test", "01234567890", "Referral"), leadKey, default);

        // 2. Qualify lead
        var qualified = await repository.QualifyLeadAsync(tenant, fixture.ActorId, fixture.EntityId, lead.Id, new(null, "Manufacturing ERP", "500000", "Q1", "Managing Director", lead.Version), default);
        Assert.Equal("Qualified", qualified.Status);

        // 3. Directory Conversion & Opportunity Creation
        var partyId = Guid.CreateVersion7();
        await using (var conn = await dataSource.OpenConnectionAsync())
        {
            await using var setTenant = new NpgsqlCommand("select set_config('app.tenant_id', $1, false)", conn);
            setTenant.Parameters.AddWithValue(tenant.Value.ToString("D"));
            await setTenant.ExecuteNonQueryAsync();

            await using var insertParty = new NpgsqlCommand("insert into foundation_directory.party(tenant_id, id, party_type, status, display_name, display_name_normalized, legal_name, trade_name, created_by, updated_by, created_at, updated_at, version) values($1, $2, 'Organization', 'Active', 'مجموعة العربي للصناعة', 'مجموعة العربي للصناعة', 'مجموعة العربي للصناعة', 'العربي', $3, $3, now(), now(), 1) on conflict (tenant_id, id) do nothing", conn);
            insertParty.Parameters.AddWithValue(tenant.Value);
            insertParty.Parameters.AddWithValue(partyId);
            insertParty.Parameters.AddWithValue(fixture.ActorId);
            await insertParty.ExecuteNonQueryAsync();

            await using var insertRole = new NpgsqlCommand("insert into foundation_directory.party_role(tenant_id, party_id, role_code, status, created_by, created_at, updated_at, version) values($1, $2, 'Customer', 'Active', $3, now(), now(), 1) on conflict do nothing", conn);
            insertRole.Parameters.AddWithValue(tenant.Value);
            insertRole.Parameters.AddWithValue(partyId);
            insertRole.Parameters.AddWithValue(fixture.ActorId);
            await insertRole.ExecuteNonQueryAsync();
        }

        var conversionOpportunity = await repository.CreateOpportunityAsync(tenant, fixture.ActorId, new(fixture.EntityId, partyId, qualified.Id, fixture.PipelineId, fixture.OpenStageId, "مشروع أنظمة العربي", 250000m, "EGP", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(45))), default);
        var convKey = "conv-" + Guid.CreateVersion7().ToString("N");
        await repository.CompleteLeadConversionAsync(tenant, fixture.ActorId, fixture.EntityId, qualified.Id, partyId, conversionOpportunity.Id, convKey, qualified.Version, default);

        var convertedLead = await repository.GetLeadAsync(tenant, fixture.EntityId, qualified.Id, default);
        Assert.NotNull(convertedLead);
        Assert.Equal("Converted", convertedLead!.Status);

        // Replay conversion check: FindConversionAsync returns existing opportunity
        var existingConv = await repository.FindConversionAsync(tenant, fixture.EntityId, qualified.Id, default);
        Assert.Equal(conversionOpportunity.Id, existingConv);

        // 4. Sales Quotation Handoff: Link quote to opportunity
        var quotationId = Guid.CreateVersion7();
        var quoteHandoffKey = "quote-handoff-" + Guid.CreateVersion7().ToString("N");
        await repository.RecordQuoteLinkAsync(tenant, fixture.EntityId, conversionOpportunity.Id, quotationId, quoteHandoffKey, default);

        // Idempotent replay: calling RecordQuoteLinkAsync with the same key
        await repository.RecordQuoteLinkAsync(tenant, fixture.EntityId, conversionOpportunity.Id, quotationId, quoteHandoffKey, default);

        // Verify exactly 1 link was created (no duplicates)
        await using (var conn = await dataSource.OpenConnectionAsync())
        {
            await using var setTenant = new NpgsqlCommand("select set_config('app.tenant_id', $1, false)", conn);
            setTenant.Parameters.AddWithValue(tenant.Value.ToString("D"));
            await setTenant.ExecuteNonQueryAsync();

            await using var countCmd = new NpgsqlCommand("select count(*) from business_crm.opportunity_source_link where tenant_id=$1 and opportunity_id=$2 and purpose='CreateQuotation'", conn);
            countCmd.Parameters.AddWithValue(tenant.Value);
            countCmd.Parameters.AddWithValue(conversionOpportunity.Id);
            var linkCount = (long)(await countCmd.ExecuteScalarAsync())!;
            Assert.Equal(1L, linkCount);

            await using var getLinkCmd = new NpgsqlCommand("select legal_entity_id, target_module, target_type, target_id from business_crm.opportunity_source_link where tenant_id=$1 and opportunity_id=$2", conn);
            getLinkCmd.Parameters.AddWithValue(tenant.Value);
            getLinkCmd.Parameters.AddWithValue(conversionOpportunity.Id);
            await using var rdr = await getLinkCmd.ExecuteReaderAsync();
            Assert.True(await rdr.ReadAsync());
            Assert.Equal(fixture.EntityId, rdr.GetGuid(0));
            Assert.Equal("sales", rdr.GetString(1));
            Assert.Equal("Quotation", rdr.GetString(2));
            Assert.Equal(quotationId, rdr.GetGuid(3));
        }
    }

    [CrmPostgresFact]
    public async Task AuthenticatedAuditTrailAndRoleVisibility()
    {
        var admin = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")!;
        var runtime = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")!;
        var fixture = await FixtureAsync(admin);
        await using var dataSource = NpgsqlDataSource.Create(runtime);
        var repository = new CrmRepository(dataSource);
        var tenant = new TenantId(fixture.TenantId);

        var seller1 = Guid.CreateVersion7();
        var seller2 = Guid.CreateVersion7();

        // 1. Create a Sales Team and add Seller 1
        var team = await repository.CreateTeamAsync(tenant, new(fixture.EntityId, "AUDIT-TEAM-" + Guid.CreateVersion7().ToString("N"), "Enterprise Sales Team"), default);
        await repository.AddTeamMemberAsync(tenant, fixture.EntityId, team.Id, new(seller1), default);

        // 2. Seller 1 creates an opportunity assigned to their team
        var opp = await repository.CreateOpportunityAsync(tenant, seller1, new(fixture.EntityId, null, null, fixture.PipelineId, fixture.OpenStageId, "Audit Deal " + Guid.CreateVersion7().ToString("N"), 75000m, "EGP", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15)), seller1, team.Id), default);

        // 3. Visibility isolation: Seller 1 sees deal in "mine" scope, Seller 2 does NOT
        var seller1Deals = await repository.ListOpportunitiesAsync(tenant, fixture.EntityId, seller1, null, fixture.PipelineId, opp.Name, 1, 20, "mine", default);
        Assert.Contains(seller1Deals.Items, x => x.Id == opp.Id);

        var seller2Deals = await repository.ListOpportunitiesAsync(tenant, fixture.EntityId, seller2, null, fixture.PipelineId, opp.Name, 1, 20, "mine", default);
        Assert.DoesNotContain(seller2Deals.Items, x => x.Id == opp.Id);

        // 4. Team membership grants visibility: Add Seller 2 to the team
        await repository.AddTeamMemberAsync(tenant, fixture.EntityId, team.Id, new(seller2), default);
        var seller2TeamDeals = await repository.ListOpportunitiesAsync(tenant, fixture.EntityId, seller2, null, fixture.PipelineId, opp.Name, 1, 20, "mine", default);
        Assert.Contains(seller2TeamDeals.Items, x => x.Id == opp.Id);

        // 5. Manager with allAuthorized = true sees all deals across legal entity
        var managerDeals = await repository.ListOpportunitiesAsync(tenant, fixture.EntityId, null, null, fixture.PipelineId, opp.Name, 1, 20, "all", default);
        Assert.Contains(managerDeals.Items, x => x.Id == opp.Id);

        // 6. Cross-tenant isolation: Querying from another tenant cannot see Tenant 1's opportunity
        var crossTenantOpp = await repository.GetOpportunityAsync(new TenantId(fixture.OtherTenantId), fixture.OtherEntityId, opp.Id, default);
        Assert.Null(crossTenantOpp);
        var crossTenantDeals = await repository.ListOpportunitiesAsync(new TenantId(fixture.OtherTenantId), fixture.OtherEntityId, seller1, null, null, opp.Name, 1, 20, "mine", default);
        Assert.Equal(0, crossTenantDeals.TotalCount);

        // 7. Audit trail verification: Write CRM audit entry via NpgsqlAuditWriter
        var auditWriter = new Core.Services.NpgsqlAuditWriter(dataSource);
        var auditId = Guid.CreateVersion7();
        await auditWriter.WriteAsync(new Core.Services.AuditEntry(
            auditId,
            tenant,
            fixture.EntityId,
            null,
            "Human",
            seller1,
            null,
            "crm",
            "opportunity.created",
            "Opportunity",
            opp.Id,
            null,
            JsonSerializer.Serialize(new { opp.Id, opp.Name, opp.EstimatedAmount }),
            null,
            "integration-test",
            Guid.CreateVersion7(),
            null,
            DateTimeOffset.UtcNow,
            null
        ));

        // Verify audit entry is persisted in platform.audit_entry
        await using (var conn = await dataSource.OpenConnectionAsync())
        {
            await using var setTenant = new NpgsqlCommand("select set_config('app.tenant_id', $1, false)", conn);
            setTenant.Parameters.AddWithValue(tenant.Value.ToString("D"));
            await setTenant.ExecuteNonQueryAsync();

            await using var auditCmd = new NpgsqlCommand("select action, entity_type, entity_id, actor_id from platform.audit_entry where tenant_id=$1 and id=$2", conn);
            auditCmd.Parameters.AddWithValue(tenant.Value);
            auditCmd.Parameters.AddWithValue(auditId);
            await using var rdr = await auditCmd.ExecuteReaderAsync();
            Assert.True(await rdr.ReadAsync());
            Assert.Equal("opportunity.created", rdr.GetString(0));
            Assert.Equal("Opportunity", rdr.GetString(1));
            Assert.Equal(opp.Id, rdr.GetGuid(2));
            Assert.Equal(seller1, rdr.GetGuid(3));
        }
    }

    private static async Task<Fixture> FixtureAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        const string sql = "select t.id, e.id, i.id, p.id, (select id from business_crm.crm_pipeline_stage where tenant_id=t.id and pipeline_id=p.id and not is_won and not is_lost order by sequence limit 1), (select id from business_crm.crm_pipeline_stage where tenant_id=t.id and pipeline_id=p.id and not is_won and not is_lost order by sequence desc limit 1), (select id from business_crm.crm_pipeline_stage where tenant_id=t.id and pipeline_id=p.id and is_won limit 1), (select id from business_crm.crm_pipeline_stage where tenant_id=t.id and pipeline_id=p.id and is_lost limit 1), t2.id, e2.id from platform.tenant t join platform.legal_entity e on e.tenant_id=t.id join platform.identity i on i.tenant_id=t.id and i.login_prefix='admin' join business_crm.crm_pipeline p on p.tenant_id=t.id and p.legal_entity_id=e.id and p.code='DEFAULT' join platform.tenant t2 on t2.id <> t.id join platform.legal_entity e2 on e2.tenant_id=t2.id order by t.id limit 1";
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("CRM seed fixture is missing.");
        return new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3), reader.GetGuid(4), reader.GetGuid(5), reader.GetGuid(6), reader.GetGuid(7), reader.GetGuid(8), reader.GetGuid(9));
    }

    private sealed record Fixture(Guid TenantId, Guid EntityId, Guid ActorId, Guid PipelineId, Guid OpenStageId, Guid ProposalStageId, Guid WonStageId, Guid LostStageId, Guid OtherTenantId, Guid OtherEntityId);
    private sealed class CrmPostgresFactAttribute : FactAttribute { public CrmPostgresFactAttribute() { if (Environment.GetEnvironmentVariable("TODIR_CRM_TEST_DATABASE") != "1") Skip = "Set TODIR_CRM_TEST_DATABASE=1 for the disposable PostgreSQL CRM acceptance gate."; } }
}
