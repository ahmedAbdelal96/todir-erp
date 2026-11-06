extern alias WorkerAssembly;

using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Core.Identity;
using Core.Messaging;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Modules.CRM.DTOs;
using Modules.CRM.Repositories;
using Modules.CRM.Services;
using Npgsql;
using RabbitMQ.Client;
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

        // 1. Create opportunity and source links for quotation, invoice, and payment
        var opportunity = await repository.CreateOpportunityAsync(tenant, fixture.ActorId, new(fixture.EntityId, null, null, fixture.PipelineId, fixture.OpenStageId, "Event Lifecycle Deal", 50000m, "EGP", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30))), default);
        var quotationId = Guid.CreateVersion7();
        var quoteKey = "quote-link-" + Guid.CreateVersion7().ToString("N");
        await repository.RecordQuoteLinkAsync(tenant, fixture.EntityId, opportunity.Id, quotationId, quoteKey, default);

        var invoiceId = Guid.CreateVersion7();
        var paymentId = Guid.CreateVersion7();
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

            await using var insertPaymentLink = new NpgsqlCommand("insert into business_crm.opportunity_source_link(tenant_id,id,legal_entity_id,opportunity_id,target_module,target_type,target_id,purpose,idempotency_key,payload_hash,created_at) values($1,$2,$3,$4,'payments','Payment',$5,'CreatePayment',$6,$7,now()) on conflict do nothing", conn);
            insertPaymentLink.Parameters.AddWithValue(tenant.Value);
            insertPaymentLink.Parameters.AddWithValue(Guid.CreateVersion7());
            insertPaymentLink.Parameters.AddWithValue(fixture.EntityId);
            insertPaymentLink.Parameters.AddWithValue(opportunity.Id);
            insertPaymentLink.Parameters.AddWithValue(paymentId);
            insertPaymentLink.Parameters.AddWithValue("payment-key-" + paymentId.ToString("N"));
            insertPaymentLink.Parameters.AddWithValue(new string('b', 64));
            await insertPaymentLink.ExecuteNonQueryAsync();
        }

        // 2. Start genuine RabbitMQ Worker OS process
        var rabbitHost = Environment.GetEnvironmentVariable("TODIR_RABBITMQ_HOST") ?? "localhost";
        var rabbitPort = int.TryParse(Environment.GetEnvironmentVariable("TODIR_RABBITMQ_PORT"), out var p) ? p : 5672;
        var rabbitUser = Environment.GetEnvironmentVariable("TODIR_RABBITMQ_USER") ?? "todir";
        var rabbitPassword = Environment.GetEnvironmentVariable("TODIR_RABBITMQ_PASSWORD") ?? "todir-dev-only";
        var rabbitVHost = Environment.GetEnvironmentVariable("TODIR_RABBITMQ_VHOST") ?? "/erp-dev";

        var worker = StartWorker(runtime, rabbitHost, rabbitPort, rabbitUser, rabbitPassword, rabbitVHost);
        IBusControl? busControl = null;

        try
        {
            // 3. Connect to RabbitMQ and wait for Worker to declare queue crm-external-event-projection
            var rabbitFactory = new ConnectionFactory
            {
                HostName = rabbitHost,
                Port = rabbitPort,
                VirtualHost = rabbitVHost,
                UserName = rabbitUser,
                Password = rabbitPassword
            };
            await using (var rabbitConn = await rabbitFactory.CreateConnectionAsync())
            {
                var declared = false;
                for (var i = 0; i < 40; i++)
                {
                    try
                    {
                        await using var ch = await rabbitConn.CreateChannelAsync();
                        await ch.QueueDeclarePassiveAsync(CrmExternalEventConsumer.EndpointName);
                        declared = true;
                        break;
                    }
                    catch
                    {
                        await Task.Delay(500);
                    }
                }
                Assert.True(declared, "Worker failed to declare the CRM consumer queue in RabbitMQ within 20 seconds.");
            }

            // 4. Start MassTransit RabbitMQ client to publish events over the real broker
            var hostUri = new UriBuilder("rabbitmq", rabbitHost)
            {
                Port = rabbitPort,
                Path = $"/{Uri.EscapeDataString(rabbitVHost.StartsWith('/') ? rabbitVHost : "/" + rabbitVHost)}"
            }.Uri;

            busControl = Bus.Factory.CreateUsingRabbitMq(cfg =>
            {
                cfg.Host(hostUri, h =>
                {
                    h.Username(rabbitUser);
                    h.Password(rabbitPassword);
                });
            });
            await busControl.StartAsync();

            // 5. Deliver QuotationAcceptedV1 message through real RabbitMQ
            var quoteMsgId = Guid.CreateVersion7();
            var quotePayload = JsonSerializer.Serialize(new { tenantId = tenant.Value, legalEntityId = fixture.EntityId, quotationId });
            var quoteMessage = new OutboxPublishedMessage(quoteMsgId, "QuotationAcceptedV1", quotePayload, tenant, DateTimeOffset.UtcNow, Guid.CreateVersion7(), Guid.CreateVersion7(), "sales", null);

            await busControl.Publish(quoteMessage);

            // Poll PostgreSQL until source link transitions to Accepted
            var quoteAccepted = false;
            for (var i = 0; i < 30; i++)
            {
                await using var conn = await dataSource.OpenConnectionAsync();
                await using var setTenant = new NpgsqlCommand("select set_config('app.tenant_id', $1, false)", conn);
                setTenant.Parameters.AddWithValue(tenant.Value.ToString("D"));
                await setTenant.ExecuteNonQueryAsync();

                await using var checkLink = new NpgsqlCommand("select external_state, last_external_event_at from business_crm.opportunity_source_link where tenant_id=$1 and opportunity_id=$2 and target_module='sales' and target_id=$3", conn);
                checkLink.Parameters.AddWithValue(tenant.Value);
                checkLink.Parameters.AddWithValue(opportunity.Id);
                checkLink.Parameters.AddWithValue(quotationId);
                await using var rdr = await checkLink.ExecuteReaderAsync();
                if (await rdr.ReadAsync() && !rdr.IsDBNull(0) && rdr.GetString(0) == "Accepted")
                {
                    quoteAccepted = true;
                    break;
                }
                await Task.Delay(500);
            }
            Assert.True(quoteAccepted, "QuotationAcceptedV1 did not transition source-link to Accepted via genuine RabbitMQ + Worker.");

            // Verify CRM timeline has the external event
            var events = await repository.ListExternalEventsAsync(tenant, fixture.EntityId, "QuotationAcceptedV1", 1, 10, default);
            Assert.Contains(events.Items, x => x.MessageId == quoteMsgId && x.MessageType == "QuotationAcceptedV1" && x.EntityId == quotationId);

            // 6. Test Event Replay & Inbox Idempotency: Send the exact same message again over RabbitMQ!
            await busControl.Publish(quoteMessage);
            await Task.Delay(1500);

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

            // 7. Test InvoicePostedV1 delivery through real RabbitMQ
            var invMsgId = Guid.CreateVersion7();
            var invPayload = JsonSerializer.Serialize(new { tenantId = tenant.Value, legalEntityId = fixture.EntityId, invoiceId });
            var invMessage = new OutboxPublishedMessage(invMsgId, "InvoicePostedV1", invPayload, tenant, DateTimeOffset.UtcNow, Guid.CreateVersion7(), Guid.CreateVersion7(), "invoicing", null);
            await busControl.Publish(invMessage);

            var invoicePosted = false;
            for (var i = 0; i < 30; i++)
            {
                await using var conn = await dataSource.OpenConnectionAsync();
                await using var setTenant = new NpgsqlCommand("select set_config('app.tenant_id', $1, false)", conn);
                setTenant.Parameters.AddWithValue(tenant.Value.ToString("D"));
                await setTenant.ExecuteNonQueryAsync();

                await using var checkInvLink = new NpgsqlCommand("select external_state from business_crm.opportunity_source_link where tenant_id=$1 and opportunity_id=$2 and target_module='invoicing' and target_id=$3", conn);
                checkInvLink.Parameters.AddWithValue(tenant.Value);
                checkInvLink.Parameters.AddWithValue(opportunity.Id);
                checkInvLink.Parameters.AddWithValue(invoiceId);
                var checkInvRaw = await checkInvLink.ExecuteScalarAsync();
                if (checkInvRaw is string invState && invState == "InvoicePosted")
                {
                    invoicePosted = true;
                    break;
                }
                await Task.Delay(500);
            }
            Assert.True(invoicePosted, "InvoicePostedV1 did not transition source-link to InvoicePosted via genuine RabbitMQ + Worker.");

            // 8. Test PaymentCompletedV1 delivery through real RabbitMQ
            var payMsgId = Guid.CreateVersion7();
            var payPayload = JsonSerializer.Serialize(new { tenantId = tenant.Value, legalEntityId = fixture.EntityId, paymentId });
            var payMessage = new OutboxPublishedMessage(payMsgId, "PaymentCompletedV1", payPayload, tenant, DateTimeOffset.UtcNow, Guid.CreateVersion7(), Guid.CreateVersion7(), "payments", null);
            await busControl.Publish(payMessage);

            var paymentCompleted = false;
            for (var i = 0; i < 30; i++)
            {
                await using var conn = await dataSource.OpenConnectionAsync();
                await using var setTenant = new NpgsqlCommand("select set_config('app.tenant_id', $1, false)", conn);
                setTenant.Parameters.AddWithValue(tenant.Value.ToString("D"));
                await setTenant.ExecuteNonQueryAsync();

                await using var checkPayLink = new NpgsqlCommand("select external_state from business_crm.opportunity_source_link where tenant_id=$1 and opportunity_id=$2 and target_module='payments' and target_id=$3", conn);
                checkPayLink.Parameters.AddWithValue(tenant.Value);
                checkPayLink.Parameters.AddWithValue(opportunity.Id);
                checkPayLink.Parameters.AddWithValue(paymentId);
                var checkPayRaw = await checkPayLink.ExecuteScalarAsync();
                if (checkPayRaw is string payState && payState == "PaymentCompleted")
                {
                    paymentCompleted = true;
                    break;
                }
                await Task.Delay(500);
            }
            Assert.True(paymentCompleted, "PaymentCompletedV1 did not transition source-link to PaymentCompleted via genuine RabbitMQ + Worker.");

            // 9. Test Inbox Lease Abandon & Retry behavior directly against PostgreSQL Inbox Store
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
            if (busControl is not null)
            {
                try { await busControl.StopAsync(); } catch { }
            }
            await StopWorkerAsync(worker);
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

            await using var insertVis = new NpgsqlCommand("insert into foundation_directory.party_visibility(tenant_id, party_id, scope_type, visibility_id) values($1, $2, 'TenantWide', $3) on conflict do nothing", conn);
            insertVis.Parameters.AddWithValue(tenant.Value);
            insertVis.Parameters.AddWithValue(partyId);
            insertVis.Parameters.AddWithValue(Guid.CreateVersion7());
            await insertVis.ExecuteNonQueryAsync();
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

        // 4. Genuine CRM-to-Sales Quotation Handoff via WebApplicationFactory
        await using var adminConn = new NpgsqlConnection(admin);
        await adminConn.OpenAsync();
        var suffix = Guid.CreateVersion7().ToString("N");
        var quoteUserEmail = $"crm-quoter-{suffix}@{fixture.TenantLoginDomain}";
        var quoteUserIdentity = Guid.CreateVersion7();
        var quoteUserMembership = Guid.CreateVersion7();
        var quoteUserRole = Guid.CreateVersion7();
        const string quotePassword = "QuoteUserPass123!Secure";

        await CreateTestIdentityAsync(adminConn, fixture.TenantId, quoteUserIdentity, quoteUserMembership, quoteUserRole, quoteUserEmail, quotePassword,
            ["crm.opportunity.read", "crm.opportunity.quote", "sales.quotation.create", "crm.team.manage"]);

        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
                .UseEnvironment("Development")
                .UseSetting("TODIR_DATABASE_CONNECTION", runtime)
                .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
            await LoginAsync(client, quoteUserEmail, quotePassword, fixture.TenantId);
            await SetContextAsync(client, fixture.EntityId);

            var quoteHandoffKey = "quote-handoff-" + Guid.CreateVersion7().ToString("N");
            var handoffReq = new QuoteHandoffRequest(
                quoteHandoffKey,
                DateOnly.FromDateTime(DateTime.UtcNow),
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)),
                "EGP",
                [new QuoteHandoffLine(null, null, "باقة استشارات ERP معتمدة", 1m, 85000m, 0m, "EA")],
                "REF-CRM-SALES-HANDOFF"
            );

            var handoffRes = await PostJsonAsync(client, $"/api/v1/crm/legal-entities/{fixture.EntityId}/opportunities/{conversionOpportunity.Id}/quotation", handoffReq);
            var handoffContent = await handoffRes.Content.ReadAsStringAsync();
            Assert.True(handoffRes.StatusCode == HttpStatusCode.Created, $"Expected Created but got {handoffRes.StatusCode}: {handoffContent}");
            var quotationId = await handoffRes.Content.ReadFromJsonAsync<Guid>();
            Assert.NotEqual(Guid.Empty, quotationId);

            // 5. Verify Real Persisted Quotation in PostgreSQL (business_sales.quotation & quotation_revision)
            await using (var conn = await dataSource.OpenConnectionAsync())
            {
                await using var setTenant = new NpgsqlCommand("select set_config('app.tenant_id', $1, false)", conn);
                setTenant.Parameters.AddWithValue(tenant.Value.ToString("D"));
                await setTenant.ExecuteNonQueryAsync();

                await using var quoteCmd = new NpgsqlCommand("select state, party_id, legal_entity_id, currency_code from business_sales.quotation where tenant_id=$1 and id=$2", conn);
                quoteCmd.Parameters.AddWithValue(tenant.Value);
                quoteCmd.Parameters.AddWithValue(quotationId);
                await using var quoteRdr = await quoteCmd.ExecuteReaderAsync();
                Assert.True(await quoteRdr.ReadAsync(), "Quotation was not found in business_sales.quotation table.");
                Assert.Equal("Draft", quoteRdr.GetString(0));
                Assert.Equal(partyId, quoteRdr.GetGuid(1));
                Assert.Equal(fixture.EntityId, quoteRdr.GetGuid(2));
                Assert.Equal("EGP", quoteRdr.GetString(3));
                await quoteRdr.CloseAsync();

                await using var revCmd = new NpgsqlCommand("select gross_total, net_total from business_sales.quotation_revision where tenant_id=$1 and quotation_id=$2 and revision_number=1", conn);
                revCmd.Parameters.AddWithValue(tenant.Value);
                revCmd.Parameters.AddWithValue(quotationId);
                await using var revRdr = await revCmd.ExecuteReaderAsync();
                Assert.True(await revRdr.ReadAsync(), "Quotation revision was not found in business_sales.quotation_revision.");
                Assert.Equal(85000m, revRdr.GetDecimal(0));
                Assert.Equal(85000m, revRdr.GetDecimal(1));
                await revRdr.CloseAsync();

                // Verify source link in business_crm.opportunity_source_link
                await using var linkCmd = new NpgsqlCommand("select target_module, target_type, target_id, purpose, idempotency_key from business_crm.opportunity_source_link where tenant_id=$1 and opportunity_id=$2 and purpose='CreateQuotation'", conn);
                linkCmd.Parameters.AddWithValue(tenant.Value);
                linkCmd.Parameters.AddWithValue(conversionOpportunity.Id);
                await using var linkRdr = await linkCmd.ExecuteReaderAsync();
                Assert.True(await linkRdr.ReadAsync(), "Source link was not recorded in business_crm.opportunity_source_link.");
                Assert.Equal("sales", linkRdr.GetString(0));
                Assert.Equal("Quotation", linkRdr.GetString(1));
                Assert.Equal(quotationId, linkRdr.GetGuid(2));
                Assert.Equal("CreateQuotation", linkRdr.GetString(3));
                Assert.Equal(quoteHandoffKey, linkRdr.GetString(4));
                await linkRdr.CloseAsync();

                // Verify audit entry in platform.audit_entry
                await using var auditCmd = new NpgsqlCommand("select action, entity_type from platform.audit_entry where tenant_id=$1 and module='crm' and action='opportunity.quotation_created' and entity_id=$2", conn);
                auditCmd.Parameters.AddWithValue(tenant.Value);
                auditCmd.Parameters.AddWithValue(conversionOpportunity.Id);
                await using var auditRdr = await auditCmd.ExecuteReaderAsync();
                Assert.True(await auditRdr.ReadAsync(), "Audit entry was not written for opportunity quotation handoff.");
                await auditRdr.CloseAsync();
            }

            // 6. Test Idempotent Retry: Calling the handoff endpoint again with the same request & key
            var replayRes = await PostJsonAsync(client, $"/api/v1/crm/legal-entities/{fixture.EntityId}/opportunities/{conversionOpportunity.Id}/quotation", handoffReq);
            Assert.Equal(HttpStatusCode.Created, replayRes.StatusCode);
            var replayQuotationId = await replayRes.Content.ReadFromJsonAsync<Guid>();
            Assert.Equal(quotationId, replayQuotationId);

            // Verify zero duplicate quotations and zero duplicate links in PostgreSQL
            await using (var conn = await dataSource.OpenConnectionAsync())
            {
                await using var setTenant = new NpgsqlCommand("select set_config('app.tenant_id', $1, false)", conn);
                setTenant.Parameters.AddWithValue(tenant.Value.ToString("D"));
                await setTenant.ExecuteNonQueryAsync();

                await using var countQuoteCmd = new NpgsqlCommand("select count(*) from business_sales.quotation where tenant_id=$1 and party_id=$2 and customer_reference='REF-CRM-SALES-HANDOFF'", conn);
                countQuoteCmd.Parameters.AddWithValue(tenant.Value);
                countQuoteCmd.Parameters.AddWithValue(partyId);
                var quoteCount = (long)(await countQuoteCmd.ExecuteScalarAsync())!;
                Assert.Equal(1L, quoteCount);

                await using var countLinkCmd = new NpgsqlCommand("select count(*) from business_crm.opportunity_source_link where tenant_id=$1 and opportunity_id=$2 and purpose='CreateQuotation'", conn);
                countLinkCmd.Parameters.AddWithValue(tenant.Value);
                countLinkCmd.Parameters.AddWithValue(conversionOpportunity.Id);
                var linkCount = (long)(await countLinkCmd.ExecuteScalarAsync())!;
                Assert.Equal(1L, linkCount);
            }
        }
        finally
        {
            await CleanupTestIdentityAsync(adminConn, fixture.TenantId, quoteUserIdentity, quoteUserMembership, quoteUserRole);
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

        await using var adminConn = new NpgsqlConnection(admin);
        await adminConn.OpenAsync();

        var suffix = Guid.CreateVersion7().ToString("N");
        const string password = "TestPassword123!Secure";

        // 1. Seller Identity in Tenant A: has lead/opportunity permissions, NO audit permission
        var sellerEmail = $"seller-{suffix}@{fixture.TenantLoginDomain}";
        var sellerIdentity = Guid.CreateVersion7();
        var sellerMembership = Guid.CreateVersion7();
        var sellerRole = Guid.CreateVersion7();
        await CreateTestIdentityAsync(adminConn, fixture.TenantId, sellerIdentity, sellerMembership, sellerRole, sellerEmail, password,
            ["crm.lead.create", "crm.lead.read", "crm.lead.edit", "crm.opportunity.create", "crm.opportunity.read", "crm.activity.create", "crm.activity.complete"]);

        // 2. Manager Identity in Tenant A: has audit read and team manage permissions
        var managerEmail = $"manager-{suffix}@{fixture.TenantLoginDomain}";
        var managerIdentity = Guid.CreateVersion7();
        var managerMembership = Guid.CreateVersion7();
        var managerRole = Guid.CreateVersion7();
        await CreateTestIdentityAsync(adminConn, fixture.TenantId, managerIdentity, managerMembership, managerRole, managerEmail, password,
            ["crm.lead.read", "crm.opportunity.read", "crm.audit.read", "crm.team.manage", "crm.pipeline.manage"]);

        // 3. Unauthorized Identity in Tenant A: no CRM permissions
        var unauthEmail = $"unauth-{suffix}@{fixture.TenantLoginDomain}";
        var unauthIdentity = Guid.CreateVersion7();
        var unauthMembership = Guid.CreateVersion7();
        var unauthRole = Guid.CreateVersion7();
        await CreateTestIdentityAsync(adminConn, fixture.TenantId, unauthIdentity, unauthMembership, unauthRole, unauthEmail, password,
            ["platform.organization.read"]);

        // 4. Cross-Tenant Identity in Tenant B (delta.com)
        var crossEmail = $"cross-{suffix}@{fixture.OtherTenantLoginDomain}";
        var crossIdentity = Guid.CreateVersion7();
        var crossMembership = Guid.CreateVersion7();
        var crossRole = Guid.CreateVersion7();
        await CreateTestIdentityAsync(adminConn, fixture.OtherTenantId, crossIdentity, crossMembership, crossRole, crossEmail, password,
            ["crm.lead.read", "crm.audit.read"]);

        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
                .UseEnvironment("Development")
                .UseSetting("TODIR_DATABASE_CONNECTION", runtime)
                .UseSetting("TODIR_RABBITMQ_HOST", string.Empty));

            // Test 1: Anonymous request is rejected with 401 Unauthorized
            using var anonymousClient = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
            var anonRes = await anonymousClient.GetAsync($"/api/v1/crm/legal-entities/{fixture.EntityId}/leads");
            Assert.Equal(HttpStatusCode.Unauthorized, anonRes.StatusCode);

            // Test 2: Unauthorized identity is rejected with 403 Forbidden
            using var unauthClient = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
            await LoginAsync(unauthClient, unauthEmail, password, fixture.TenantId);
            await SetContextAsync(unauthClient, fixture.EntityId);
            var unauthLeadsRes = await unauthClient.GetAsync($"/api/v1/crm/legal-entities/{fixture.EntityId}/leads");
            Assert.Equal(HttpStatusCode.Forbidden, unauthLeadsRes.StatusCode);
            var unauthAuditRes = await unauthClient.GetAsync($"/api/v1/crm/legal-entities/{fixture.EntityId}/audit");
            Assert.Equal(HttpStatusCode.Forbidden, unauthAuditRes.StatusCode);

            // Test 3: Seller creates a lead via HTTP API -> 201 Created and audit entry is written
            using var sellerClient = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
            await LoginAsync(sellerClient, sellerEmail, password, fixture.TenantId);
            await SetContextAsync(sellerClient, fixture.EntityId);

            var createLeadReq = new CreateLeadRequest(fixture.EntityId, "عميل تجريبي بائع", "شركة التقنية الذكية", $"lead-{suffix}@example.test", "01099887766", "Campaign");
            var leadKey = "lead-key-" + Guid.CreateVersion7().ToString("N");
            var createRes = await PostJsonAsync(sellerClient, $"/api/v1/crm/legal-entities/{fixture.EntityId}/leads", createLeadReq, leadKey);
            Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
            var createdLead = await createRes.Content.ReadFromJsonAsync<LeadDto>();
            Assert.NotNull(createdLead);

            // Seller sees own lead in "mine" scope
            var sellerLeadsRes = await sellerClient.GetAsync($"/api/v1/crm/legal-entities/{fixture.EntityId}/leads?scope=mine");
            Assert.Equal(HttpStatusCode.OK, sellerLeadsRes.StatusCode);
            var sellerLeads = await sellerLeadsRes.Content.ReadFromJsonAsync<CrmPage<LeadDto>>();
            Assert.Contains(sellerLeads!.Items, x => x.Id == createdLead!.Id);

            // Seller CANNOT view audit (lacks crm.audit.read) -> 403 Forbidden
            var sellerAuditRes = await sellerClient.GetAsync($"/api/v1/crm/legal-entities/{fixture.EntityId}/audit");
            Assert.Equal(HttpStatusCode.Forbidden, sellerAuditRes.StatusCode);

            // Test 4: Manager (has crm.audit.read) retrieves audit trail -> 200 OK
            using var managerClient = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
            await LoginAsync(managerClient, managerEmail, password, fixture.TenantId);
            await SetContextAsync(managerClient, fixture.EntityId);

            var managerAuditRes = await managerClient.GetAsync($"/api/v1/crm/legal-entities/{fixture.EntityId}/audit");
            Assert.Equal(HttpStatusCode.OK, managerAuditRes.StatusCode);
            var auditEntries = await managerAuditRes.Content.ReadFromJsonAsync<List<AuditEntryResponse>>();
            Assert.NotNull(auditEntries);
            Assert.Contains(auditEntries!, x => x.Action == "lead.created" && x.ActorId == sellerIdentity && x.EntityId == createdLead!.Id);

            // Manager with crm.team.manage sees seller's lead in "all" scope
            var managerAllLeadsRes = await managerClient.GetAsync($"/api/v1/crm/legal-entities/{fixture.EntityId}/leads?scope=all");
            Assert.Equal(HttpStatusCode.OK, managerAllLeadsRes.StatusCode);
            var managerLeads = await managerAllLeadsRes.Content.ReadFromJsonAsync<CrmPage<LeadDto>>();
            Assert.Contains(managerLeads!.Items, x => x.Id == createdLead!.Id);

            // Test 5: Cross-Tenant identity (from Delta) attempting to access Atlas legal entity is rejected
            using var crossClient = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = true });
            await LoginAsync(crossClient, crossEmail, password, fixture.OtherTenantId);
            await SetContextAsync(crossClient, fixture.OtherEntityId);
            var crossLeadsRes = await crossClient.GetAsync($"/api/v1/crm/legal-entities/{fixture.EntityId}/leads");
            Assert.Equal(HttpStatusCode.Forbidden, crossLeadsRes.StatusCode);
            var crossAuditRes = await crossClient.GetAsync($"/api/v1/crm/legal-entities/{fixture.EntityId}/audit");
            Assert.Equal(HttpStatusCode.Forbidden, crossAuditRes.StatusCode);
        }
        finally
        {
            await CleanupTestIdentityAsync(adminConn, fixture.TenantId, sellerIdentity, sellerMembership, sellerRole);
            await CleanupTestIdentityAsync(adminConn, fixture.TenantId, managerIdentity, managerMembership, managerRole);
            await CleanupTestIdentityAsync(adminConn, fixture.TenantId, unauthIdentity, unauthMembership, unauthRole);
            await CleanupTestIdentityAsync(adminConn, fixture.OtherTenantId, crossIdentity, crossMembership, crossRole);
        }
    }

    private static Process StartWorker(string connectionString, string rabbitHost, int rabbitPort, string rabbitUser, string rabbitPassword, string rabbitVHost)
    {
        var repository = new DirectoryInfo(Environment.GetEnvironmentVariable("TODIR_REPOSITORY_ROOT") ?? AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "backend", "DbMigrator", "DbMigrator.csproj"))) repository = repository.Parent;
        if (repository is null) throw new InvalidOperationException("The repository root could not be located for the Worker process.");
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
        worker.Start();
        return worker;
    }

    private static async Task StopWorkerAsync(Process? worker)
    {
        if (worker is null) return;
        if (!worker.HasExited)
        {
            try { worker.Kill(true); } catch { }
            await worker.WaitForExitAsync();
        }
        worker.Dispose();
    }

    private static async Task LoginAsync(HttpClient client, string email, string password, Guid tenantId)
    {
        var csrfResponse = await client.GetAsync("/api/v1/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
        var csrf = await csrfResponse.Content.ReadFromJsonAsync<CsrfTokenResponse>();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email, password })
        };
        if (csrf?.Token is not null) request.Headers.Add("X-CSRF-TOKEN", csrf.Token);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Authenticated", body.GetProperty("state").GetString());
        Assert.Equal(tenantId, body.GetProperty("tenantId").GetGuid());
    }

    private static async Task SetContextAsync(HttpClient client, Guid legalEntityId)
    {
        var csrfResponse = await client.GetAsync("/api/v1/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
        var csrf = await csrfResponse.Content.ReadFromJsonAsync<CsrfTokenResponse>();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/context/organization")
        {
            Content = JsonContent.Create(new { legalEntityId })
        };
        if (csrf?.Token is not null) request.Headers.Add("X-CSRF-TOKEN", csrf.Token);
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string path, object body, string? idempotencyKey = null)
    {
        var csrfResponse = await client.GetAsync("/api/v1/auth/csrf");
        Assert.Equal(HttpStatusCode.OK, csrfResponse.StatusCode);
        var csrf = await csrfResponse.Content.ReadFromJsonAsync<CsrfTokenResponse>();
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body)
        };
        if (csrf?.Token is not null) request.Headers.Add("X-CSRF-TOKEN", csrf.Token);
        if (!string.IsNullOrWhiteSpace(idempotencyKey)) request.Headers.Add("Idempotency-Key", idempotencyKey);
        return await client.SendAsync(request);
    }

    private static async Task CreateTestIdentityAsync(NpgsqlConnection admin, Guid tenantId, Guid identityId, Guid membershipId, Guid roleId, string email, string password, string[] permissions)
    {
        var hash = IdentityConventions.HashPassword(password);
        var prefix = "u" + Guid.CreateVersion7().ToString("N")[..20];
        var stamp = Guid.CreateVersion7();

        await using var cmd = new NpgsqlCommand($@"
            set role erp_owner;
            set app.tenant_id = '{tenantId}';
            insert into platform.identity (id, tenant_id, email, normalized_email, login_prefix, name, email_verified, password_hash, status, created_at, security_stamp, version)
            values ('{identityId}', '{tenantId}', '{email}', '{email.ToUpperInvariant()}', '{prefix}', 'Test User {prefix}', true, '{hash}', 'Active', now(), '{stamp}', 1)
            on conflict (tenant_id, id) do nothing;

            insert into platform.membership (id, tenant_id, identity_id, display_name, status, created_at, version)
            values ('{membershipId}', '{tenantId}', '{identityId}', 'Test User {prefix}', 'Active', now(), 1)
            on conflict (tenant_id, id) do nothing;

            insert into platform.role (id, tenant_id, code, name, is_system_template, is_active, version)
            values ('{roleId}', '{tenantId}', 'role-{prefix}', 'Test Role {prefix}', false, true, 1)
            on conflict (tenant_id, id) do nothing;

            insert into platform.membership_role_assignment (tenant_id, membership_id, role_id, scope_type)
            values ('{tenantId}', '{membershipId}', '{roleId}', 'Tenant')
            on conflict do nothing;
        ", admin);
        await cmd.ExecuteNonQueryAsync();

        foreach (var p in permissions)
        {
            await using var pCmd = new NpgsqlCommand($"insert into platform.role_permission (tenant_id, role_id, permission_code) values ('{tenantId}', '{roleId}', '{p}') on conflict do nothing;", admin);
            await pCmd.ExecuteNonQueryAsync();
        }

        await using var resetCmd = new NpgsqlCommand("reset app.tenant_id; reset role;", admin);
        await resetCmd.ExecuteNonQueryAsync();
    }

    private static async Task CleanupTestIdentityAsync(NpgsqlConnection admin, Guid tenantId, Guid identityId, Guid membershipId, Guid roleId)
    {
        await using var cmd = new NpgsqlCommand($@"
            set role erp_owner;
            set app.tenant_id = '{tenantId}';
            delete from platform.membership_role_assignment where tenant_id = '{tenantId}' and membership_id = '{membershipId}';
            delete from platform.role_permission where tenant_id = '{tenantId}' and role_id = '{roleId}';
            delete from platform.role where tenant_id = '{tenantId}' and id = '{roleId}';
            delete from platform.membership where tenant_id = '{tenantId}' and id = '{membershipId}';
            delete from platform.identity where tenant_id = '{tenantId}' and id = '{identityId}';
            reset app.tenant_id;
            reset role;
        ", admin);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<Fixture> FixtureAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        const string sql = "select t.id, t.login_domain, e.id, i.id, p.id, (select id from business_crm.crm_pipeline_stage where tenant_id=t.id and pipeline_id=p.id and not is_won and not is_lost order by sequence limit 1), (select id from business_crm.crm_pipeline_stage where tenant_id=t.id and pipeline_id=p.id and not is_won and not is_lost order by sequence desc limit 1), (select id from business_crm.crm_pipeline_stage where tenant_id=t.id and pipeline_id=p.id and is_won limit 1), (select id from business_crm.crm_pipeline_stage where tenant_id=t.id and pipeline_id=p.id and is_lost limit 1), t2.id, t2.login_domain, e2.id from platform.tenant t join platform.legal_entity e on e.tenant_id=t.id join platform.identity i on i.tenant_id=t.id and i.login_prefix='admin' join business_crm.crm_pipeline p on p.tenant_id=t.id and p.legal_entity_id=e.id and p.code='DEFAULT' join platform.tenant t2 on t2.id <> t.id join platform.legal_entity e2 on e2.tenant_id=t2.id order by t.id limit 1";
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("CRM seed fixture is missing.");
        return new(reader.GetGuid(0), reader.GetString(1), reader.GetGuid(2), reader.GetGuid(3), reader.GetGuid(4), reader.GetGuid(5), reader.GetGuid(6), reader.GetGuid(7), reader.GetGuid(8), reader.GetGuid(9), reader.GetString(10), reader.GetGuid(11));
    }

    private sealed record AuditEntryResponse(Guid Id, Guid? LegalEntityId, Guid? BranchId, string ActorType, Guid? ActorId, Guid? MembershipId, string Module, string Action, string EntityType, Guid EntityId, string? Reason, DateTimeOffset OccurredAt, string? TraceId);
    private sealed record CsrfTokenResponse(string Token);
    private sealed record Fixture(Guid TenantId, string TenantLoginDomain, Guid EntityId, Guid ActorId, Guid PipelineId, Guid OpenStageId, Guid ProposalStageId, Guid WonStageId, Guid LostStageId, Guid OtherTenantId, string OtherTenantLoginDomain, Guid OtherEntityId);
    private sealed class CrmPostgresFactAttribute : FactAttribute { public CrmPostgresFactAttribute() { if (Environment.GetEnvironmentVariable("TODIR_CRM_TEST_DATABASE") != "1") Skip = "Set TODIR_CRM_TEST_DATABASE=1 for the disposable PostgreSQL CRM acceptance gate."; } }
}
