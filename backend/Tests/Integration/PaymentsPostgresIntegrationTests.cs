using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using Modules.Payments.DTOs;
using Modules.Payments.Entities;
using Modules.Payments.Services;
using Modules.Expenses.Repositories;
using Modules.Expenses.Services;
using Modules.Hr.DTOs;
using Modules.Hr.Entities;
using Modules.Hr.Repositories;
using Npgsql;
using Shared.Primitives.Identifiers;
using Xunit;
using Xunit.Abstractions;

namespace KernelIntegrationTests;

[Collection("database-integration")]
public sealed class PaymentsPostgresIntegrationTests : DatabaseIntegrationTestBase
{
    private readonly ITestOutputHelper output;

    public PaymentsPostgresIntegrationTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public async Task ConfirmedProviderDeclineCanBeRetriedAsANewIdempotentAttempt()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for payment integration tests.");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for payment integration tests.");
        var fixture = await ReadFixtureAsync(adminConnectionString);
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnectionString);
            builder.UseSetting("TODIR_RABBITMQ_HOST", string.Empty);
        });
        var payments = factory.Services.GetRequiredService<PaymentsService>(); var tenant = new TenantId(fixture.TenantId);
        await using var admin = new NpgsqlConnection(adminConnectionString); await admin.OpenAsync();
        var providerAccount = await payments.CreateAccountAsync(tenant, fixture.ActorId, fixture.MembershipId,
            new(fixture.LegalEntityId, null, "RETRY-" + Guid.CreateVersion7().ToString("N")[^8..], "Retry provider", PaymentAccountType.ProviderClearing,
                "MultiCurrency", null, null, null, null, null), Guid.CreateVersion7(), CancellationToken.None);
        var provider = await payments.CreateProviderConfigAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(),
            new(fixture.LegalEntityId, "reference", "Sandbox", "RETRY-" + Guid.CreateVersion7().ToString("N")[^8..], "2026-01",
                new(true, true, false, true, false, true), ["EGP"], ["PaymentGateway"], providerAccount.Id, "secret://payments/reference/retry-test"), CancellationToken.None);
        var unknownRequest = await payments.CreatePaymentIntentAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(),
            new(fixture.LegalEntityId, null, provider.Id, fixture.PartyId, "9.00", "EGP", null, null, DateTimeOffset.UtcNow.AddDays(1)),
            "simulate-timeout-" + Guid.CreateVersion7().ToString("N"), CancellationToken.None);
        var unknownOperationId = (Guid)(await ScalarAsync(admin, "select id from finance_payments.provider_operation where tenant_id=$1 and legal_entity_id=$2 and aggregate_id=$3 and operation_type='Create'", fixture.TenantId, fixture.LegalEntityId, unknownRequest.Id))!;
        await payments.StartReferenceProviderOperationAsync(tenant, unknownOperationId, CancellationToken.None);
        var syncedUnknown = await payments.SyncReferenceProviderStatusAsync(tenant, fixture.LegalEntityId, unknownRequest.Id, CancellationToken.None);
        Assert.Equal("Pending", syncedUnknown.State);
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_payments.payment_attempt where tenant_id=$1 and legal_entity_id=$2 and payment_request_id=$3 and state='Unknown' and provider_transaction_id is null", fixture.TenantId, fixture.LegalEntityId, unknownRequest.Id));
        Assert.Equal(0L, await CountAsync(admin, "select count(*) from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and source_type='PaymentRequest' and source_reference=$3", fixture.TenantId, fixture.LegalEntityId, unknownRequest.Id.ToString("D")));
        var retryUnknown = await Assert.ThrowsAsync<PaymentException>(() => payments.RetryPaymentIntentAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(), fixture.LegalEntityId, unknownRequest.Id,
            "unsafe-unknown-retry-" + Guid.CreateVersion7().ToString("N"), CancellationToken.None));
        Assert.Equal("payments.provider_status_sync_required", retryUnknown.Code);
        var cancelledUnknown = await payments.CancelProviderPaymentRequestAsync(tenant, fixture.LegalEntityId, unknownRequest.Id, CancellationToken.None);
        Assert.Equal("Cancelled", cancelledUnknown.State);
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_payments.payment_attempt where tenant_id=$1 and legal_entity_id=$2 and payment_request_id=$3 and state='Cancelled'", fixture.TenantId, fixture.LegalEntityId, unknownRequest.Id));
        Assert.Equal(0L, await CountAsync(admin, "select count(*) from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and source_type='PaymentRequest' and source_reference=$3", fixture.TenantId, fixture.LegalEntityId, unknownRequest.Id.ToString("D")));

        var request = await payments.CreatePaymentIntentAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(),
            new(fixture.LegalEntityId, null, provider.Id, fixture.PartyId, "18.00", "EGP", null, null, DateTimeOffset.UtcNow.AddDays(1)),
            "retry-initial-" + Guid.CreateVersion7().ToString("N"), CancellationToken.None);
        var operationId = (Guid)(await ScalarAsync(admin, "select id from finance_payments.provider_operation where tenant_id=$1 and legal_entity_id=$2 and aggregate_id=$3 and operation_type='Create'", fixture.TenantId, fixture.LegalEntityId, request.Id))!;
        await payments.StartReferenceProviderOperationAsync(tenant, operationId, CancellationToken.None);
        var transactionId = (string)(await ScalarAsync(admin, "select provider_transaction_id from finance_payments.payment_attempt where tenant_id=$1 and legal_entity_id=$2 and payment_request_id=$3", fixture.TenantId, fixture.LegalEntityId, request.Id))!;
        const string signingSecret = "retry-reference-webhook-secret";
        var secretVariable = EnvironmentProviderSecretResolver.SecretEnvironmentVariableName("secret://payments/reference/retry-test");
        var previousSecret = Environment.GetEnvironmentVariable(secretVariable); Environment.SetEnvironmentVariable(secretVariable, signingSecret);
        try
        {
            var body = JsonSerializer.SerializeToUtf8Bytes(new
            {
                eventId = "declined-" + Guid.CreateVersion7().ToString("N"),
                merchantReference = request.MerchantReference,
                transactionId,
                eventType = "payment.failed",
                occurredAt = DateTimeOffset.UtcNow
            });
            var signature = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(signingSecret), body)).ToLowerInvariant();
            await payments.ReceiveReferenceWebhookAsync(tenant, provider.Id, body, signature, CancellationToken.None);
        }
        finally { Environment.SetEnvironmentVariable(secretVariable, previousSecret); }
        Assert.Equal(0L, await CountAsync(admin, "select count(*) from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and source_type='PaymentRequest' and source_reference=$3", fixture.TenantId, fixture.LegalEntityId, request.Id.ToString("D")));
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_payments.payment_request where tenant_id=$1 and legal_entity_id=$2 and id=$3 and workflow_state='Failed'", fixture.TenantId, fixture.LegalEntityId, request.Id));

        var retryKey = "retry-" + Guid.CreateVersion7().ToString("N");
        var retry = await payments.RetryPaymentIntentAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(), fixture.LegalEntityId, request.Id, retryKey, CancellationToken.None);
        var retryAgain = await payments.RetryPaymentIntentAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(), fixture.LegalEntityId, request.Id, retryKey, CancellationToken.None);
        Assert.Equal(2, retry.AttemptNumber); Assert.Equal(retry.AttemptId, retryAgain.AttemptId);
        Assert.Equal(2L, await CountAsync(admin, "select count(*) from finance_payments.payment_attempt where tenant_id=$1 and legal_entity_id=$2 and payment_request_id=$3", fixture.TenantId, fixture.LegalEntityId, request.Id));
        var retryOperationId = Guid.Parse((string)(await ScalarAsync(admin, "select provider_operation_id from finance_payments.payment_attempt where tenant_id=$1 and legal_entity_id=$2 and payment_request_id=$3 and attempt_number=2", fixture.TenantId, fixture.LegalEntityId, request.Id))!);
        await payments.StartReferenceProviderOperationAsync(tenant, retryOperationId, CancellationToken.None);
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_payments.payment_attempt where tenant_id=$1 and legal_entity_id=$2 and payment_request_id=$3 and attempt_number=2 and state='RequiresAction' and provider_transaction_id is not null", fixture.TenantId, fixture.LegalEntityId, request.Id));
    }

    [Fact]
    public async Task ManualCompletionLatencyUsesWarmupThenBoundedRealPostgresSamples()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for payment integration tests.");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for payment integration tests.");
        var fixture = await ReadFixtureAsync(adminConnectionString);
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnectionString);
            builder.UseSetting("TODIR_RABBITMQ_HOST", string.Empty);
        });
        var payments = factory.Services.GetRequiredService<PaymentsService>();
        var tenant = new TenantId(fixture.TenantId);
        var command = new CreatePaymentRequest(fixture.LegalEntityId, null, PaymentType.CustomerReceipt, fixture.PartyId,
            "2.00", "EGP", PaymentMethod.BankTransfer, fixture.PaymentAccountId,
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1), null, null, null, null, "PerformanceSample", null);
        var durations = new List<double>(25);
        for (var sample = 0; sample < 28; sample++)
        {
            var draft = await payments.CreateDraftAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(),
                command with { BankReference = $"PERF-{Guid.CreateVersion7():N}" }, $"perf-create-{Guid.CreateVersion7():N}", CancellationToken.None);
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var completed = await payments.CompleteManualAsync(tenant, fixture.ActorId, fixture.MembershipId, fixture.LegalEntityId,
                draft.Id, $"perf-complete-{Guid.CreateVersion7():N}", Guid.CreateVersion7(), CancellationToken.None);
            timer.Stop();
            Assert.Equal(PaymentBusinessState.Completed, completed.State);
            if (sample >= 3) durations.Add(timer.Elapsed.TotalMilliseconds);
        }
        var ordered = durations.Order().ToArray();
        static double Percentile(double[] values, double percentile) => values[Math.Max(0, (int)Math.Ceiling(values.Length * percentile) - 1)];
        output.WriteLine($"manual_completion_samples={ordered.Length} p50_ms={Percentile(ordered, .50):F2} p95_ms={Percentile(ordered, .95):F2} p99_ms={Percentile(ordered, .99):F2} errors=0");
    }

    [Fact]
    public async Task ReceiveAndApplyLatencyUsesWarmupThenBoundedRealPostgresSamples()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for payment integration tests.");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for payment integration tests.");
        var fixture = await ReadFixtureAsync(adminConnectionString);
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnectionString);
            builder.UseSetting("TODIR_RABBITMQ_HOST", string.Empty);
        });
        var payments = factory.Services.GetRequiredService<PaymentsService>();
        var tenant = new TenantId(fixture.TenantId);
        var command = new CreatePaymentRequest(fixture.LegalEntityId, null, PaymentType.CustomerReceipt, fixture.PartyId,
            "2.00", "EGP", PaymentMethod.BankTransfer, fixture.PaymentAccountId,
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1), null, null, null, null, "ReceiveApplySample", null);
        var durations = new List<double>(10);
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        for (var sample = 0; sample < 12; sample++)
        {
            var draft = await payments.CreateDraftAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(),
                command with { BankReference = $"APPLY-PERF-{Guid.CreateVersion7():N}" }, $"apply-perf-create-{Guid.CreateVersion7():N}", CancellationToken.None);
            var completed = await payments.CompleteManualAsync(tenant, fixture.ActorId, fixture.MembershipId, fixture.LegalEntityId,
                draft.Id, $"apply-perf-complete-{Guid.CreateVersion7():N}", Guid.CreateVersion7(), CancellationToken.None);
            Assert.Equal(PaymentBusinessState.Completed, completed.State);
            var targetId = (Guid)(await ScalarAsync(admin, "select target.id from finance_subledger.party_open_item source join finance_subledger.party_open_item target on target.tenant_id=source.tenant_id and target.legal_entity_id=source.legal_entity_id and target.party_id=source.party_id and target.direction=source.direction and target.polarity='Debit' and target.transaction_currency=source.transaction_currency and target.base_currency=source.base_currency and target.control_account_role=source.control_account_role and target.control_account_reference=source.control_account_reference and target.control_configuration_reference=source.control_configuration_reference where source.tenant_id=$1 and source.legal_entity_id=$2 and source.source_type='Payment' and source.source_reference=$3 and source.polarity='Credit' and target.source_type<>'Payment' and target.is_open and target.open_transaction_amount>=1 order by target.open_transaction_amount desc,target.id limit 1", fixture.TenantId, fixture.LegalEntityId, draft.Id.ToString("D")))!;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var application = await payments.ApplyToPaymentAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(), fixture.LegalEntityId, null,
                draft.Id, new(new(2026, 10, 1), new(2026, 10, 1), [new(targetId, "1.00")]), $"apply-perf-{Guid.CreateVersion7():N}", CancellationToken.None);
            timer.Stop(); Assert.Single(application.ApplicationIds);
            if (sample >= 2) durations.Add(timer.Elapsed.TotalMilliseconds);
        }
        var ordered = durations.Order().ToArray();
        static double Percentile(double[] values, double percentile) => values[Math.Max(0, (int)Math.Ceiling(values.Length * percentile) - 1)];
        output.WriteLine($"receive_apply_samples={ordered.Length} p50_ms={Percentile(ordered, .50):F2} p95_ms={Percentile(ordered, .95):F2} p99_ms={Percentile(ordered, .99):F2} errors=0");
    }

    [Fact]
    public async Task ConcurrentCompletionCreatesOnePaymentNumberSubledgerItemLedgerEntryAndOutboxEvent()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for payment integration tests.");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for payment integration tests.");
        var fixture = await ReadFixtureAsync(adminConnectionString);

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnectionString);
            builder.UseSetting("TODIR_RABBITMQ_HOST", string.Empty);
        });
        var payments = factory.Services.GetRequiredService<PaymentsService>();
        var tenant = new TenantId(fixture.TenantId);
        var command = new CreatePaymentRequest(fixture.LegalEntityId, null, PaymentType.CustomerReceipt, fixture.PartyId,
            "125.75", "EGP", PaymentMethod.BankTransfer, fixture.PaymentAccountId,
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1), null, "INTEGRATION-" + Guid.CreateVersion7().ToString("N"), null, null, "Manual", null);
        var idempotencyKey = "payments-integration-" + Guid.CreateVersion7().ToString("N");
        var draft = await payments.CreateDraftAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(), command, idempotencyKey, CancellationToken.None);
        var retry = await payments.CreateDraftAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(), command, idempotencyKey, CancellationToken.None);
        Assert.Equal(draft.Id, retry.Id);
        Assert.Equal(draft.Number, retry.Number);

        var completionKeys = Enumerable.Range(0, 12).Select(_ => "complete-" + Guid.CreateVersion7().ToString("N")).ToArray();
        var completed = await Task.WhenAll(completionKeys.Select(key => payments.CompleteManualAsync(tenant, fixture.ActorId, fixture.MembershipId,
            fixture.LegalEntityId, draft.Id, key, Guid.CreateVersion7(), CancellationToken.None)));
        Assert.All(completed, payment => Assert.Equal(PaymentBusinessState.Completed, payment.State));

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and id=$3 and business_state='Completed'", fixture.TenantId, fixture.LegalEntityId, draft.Id));
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and source_type='Payment' and source_reference=$3", fixture.TenantId, fixture.LegalEntityId, draft.Id.ToString("D")));
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_ledger.accounting_fact where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Posted'", fixture.TenantId, fixture.LegalEntityId, draft.Id));
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_ledger.journal_entry where tenant_id=$1 and legal_entity_id=$2 and fact_id=$3", fixture.TenantId, fixture.LegalEntityId, draft.Id));
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from platform.outbox_message where tenant_id=$1 and source_module='payments' and causation_id=$2 and message_type='PaymentCompletedV1'", fixture.TenantId, draft.Id));
        await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var immutable = new NpgsqlCommand("update finance_payments.payment set amount=amount+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3", admin);
            immutable.Parameters.AddWithValue(fixture.TenantId); immutable.Parameters.AddWithValue(fixture.LegalEntityId); immutable.Parameters.AddWithValue(draft.Id);
            await immutable.ExecuteNonQueryAsync();
        });
        await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var immutableReference = new NpgsqlCommand("update finance_payments.payment set bank_reference='ALTERED' where tenant_id=$1 and legal_entity_id=$2 and id=$3", admin);
            immutableReference.Parameters.AddWithValue(fixture.TenantId); immutableReference.Parameters.AddWithValue(fixture.LegalEntityId); immutableReference.Parameters.AddWithValue(draft.Id);
            await immutableReference.ExecuteNonQueryAsync();
        });

        var supplierPartyId = (Guid)(await ScalarAsync(admin, "select party.id from foundation_directory.party party join foundation_directory.party_role role on role.tenant_id=party.tenant_id and role.party_id=party.id and role.role_code='Supplier' and role.status='Active' join finance_subledger.party_open_item item on item.tenant_id=party.tenant_id and item.party_id=party.id and item.legal_entity_id=$2 and item.direction='Payable' and item.is_open where party.tenant_id=$1 and party.status='Active' order by party.id limit 1", fixture.TenantId, fixture.LegalEntityId))!;
        var outgoingDraft = await payments.CreateDraftAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(),
            command with { Type = PaymentType.SupplierPayment, PartyId = supplierPartyId, Amount = "30.00", BankReference = $"OUT-{Guid.CreateVersion7():N}", CustomerReference = null, SupplierReference = "SUPPLIER-SETTLEMENT" },
            $"outgoing-{Guid.CreateVersion7():N}", CancellationToken.None);
        var outgoing = await payments.CompleteManualAsync(tenant, fixture.ActorId, fixture.MembershipId, fixture.LegalEntityId,
            outgoingDraft.Id, $"outgoing-complete-{Guid.CreateVersion7():N}", Guid.CreateVersion7(), CancellationToken.None);
        Assert.Equal(PaymentBusinessState.Completed, outgoing.State);
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and source_type='Payment' and source_reference=$3 and direction='Payable'", fixture.TenantId, fixture.LegalEntityId, outgoing.Id.ToString("D")));
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_ledger.accounting_fact where tenant_id=$1 and legal_entity_id=$2 and id=$3 and fact_type='PaymentMade' and status='Posted'", fixture.TenantId, fixture.LegalEntityId, outgoing.Id));

        var advance = await payments.CreateDraftAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(),
            command with { Type = PaymentType.CustomerAdvance, Amount = "12.00", BankReference = $"ADV-{Guid.CreateVersion7():N}" },
            $"advance-{Guid.CreateVersion7():N}", CancellationToken.None);
        Assert.Equal(PaymentBusinessState.Completed, (await payments.CompleteManualAsync(tenant, fixture.ActorId, fixture.MembershipId,
            fixture.LegalEntityId, advance.Id, $"advance-complete-{Guid.CreateVersion7():N}", Guid.CreateVersion7(), CancellationToken.None)).State);

        foreach (var failpoint in new[]
                 {
                     ("subledger", "finance_subledger.party_open_item", "INSERT"),
                     ("ledger", "finance_ledger.accounting_fact", "INSERT"),
                     ("outbox", "platform.outbox_message", "INSERT"),
                     ("completion_record", "finance_payments.payment", "UPDATE")
                 })
        {
            var failedDraft = await payments.CreateDraftAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(),
                command with { BankReference = $"ATOMICITY-{failpoint.Item1}-{Guid.CreateVersion7():N}" },
                $"atomicity-create-{failpoint.Item1}-{Guid.CreateVersion7():N}", CancellationToken.None);
            var completionKey = $"atomicity-complete-{failpoint.Item1}-{Guid.CreateVersion7():N}";
            await InstallFailureTriggerAsync(adminConnectionString, failpoint.Item2, failpoint.Item3);
            try
            {
                await Assert.ThrowsAsync<PostgresException>(() => payments.CompleteManualAsync(tenant, fixture.ActorId, fixture.MembershipId,
                    fixture.LegalEntityId, failedDraft.Id, completionKey, Guid.CreateVersion7(), CancellationToken.None));
            }
            finally { await RemoveFailureTriggerAsync(adminConnectionString, failpoint.Item2); }

            Assert.Equal(PaymentBusinessState.Draft, (await payments.GetAsync(tenant, fixture.LegalEntityId, null, failedDraft.Id, CancellationToken.None))!.State);
            Assert.Equal(0L, await CountAsync(admin, "select count(*) from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and source_type='Payment' and source_reference=$3", fixture.TenantId, fixture.LegalEntityId, failedDraft.Id.ToString("D")));
            Assert.Equal(0L, await CountAsync(admin, "select count(*) from finance_ledger.accounting_fact where tenant_id=$1 and legal_entity_id=$2 and id=$3", fixture.TenantId, fixture.LegalEntityId, failedDraft.Id));
            Assert.Equal(0L, await CountAsync(admin, "select count(*) from platform.outbox_message where tenant_id=$1 and source_module='payments' and causation_id=$2 and message_type='PaymentCompletedV1'", fixture.TenantId, failedDraft.Id));

            var recovered = await payments.CompleteManualAsync(tenant, fixture.ActorId, fixture.MembershipId, fixture.LegalEntityId, failedDraft.Id,
                completionKey, Guid.CreateVersion7(), CancellationToken.None);
            Assert.Equal(PaymentBusinessState.Completed, recovered.State);
            Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and source_type='Payment' and source_reference=$3", fixture.TenantId, fixture.LegalEntityId, failedDraft.Id.ToString("D")));
            Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_ledger.accounting_fact where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Posted'", fixture.TenantId, fixture.LegalEntityId, failedDraft.Id));
            Assert.Equal(1L, await CountAsync(admin, "select count(*) from platform.outbox_message where tenant_id=$1 and source_module='payments' and causation_id=$2 and message_type='PaymentCompletedV1'", fixture.TenantId, failedDraft.Id));
        }

        var compatibleTargetId = (Guid)(await ScalarAsync(admin, "select target.id from finance_subledger.party_open_item source join finance_subledger.party_open_item target on target.tenant_id=source.tenant_id and target.legal_entity_id=source.legal_entity_id and target.party_id=source.party_id and target.direction=source.direction and target.polarity='Debit' and target.transaction_currency=source.transaction_currency and target.base_currency=source.base_currency and target.control_account_role=source.control_account_role and target.control_account_reference=source.control_account_reference and target.control_configuration_reference=source.control_configuration_reference where source.tenant_id=$1 and source.legal_entity_id=$2 and source.source_type='Payment' and source.source_reference=$3 and source.polarity='Credit' and target.source_type<>'Payment' and target.is_open and target.open_transaction_amount>=25 order by target.open_transaction_amount desc,target.id limit 1", fixture.TenantId, fixture.LegalEntityId, draft.Id.ToString("D")))!;
        var application = await payments.ApplyToPaymentAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(), fixture.LegalEntityId, null,
            draft.Id, new(new(2026, 10, 1), new(2026, 10, 1), [new(compatibleTargetId, "25.00")]),
            "apply-" + Guid.CreateVersion7().ToString("N"), CancellationToken.None);
        Assert.False(application.AlreadyApplied);
        Assert.Single(application.ApplicationIds);
        var appliedReversalError = await Assert.ThrowsAsync<PaymentException>(() => payments.ReverseAsync(tenant, fixture.ActorId, fixture.MembershipId,
            Guid.CreateVersion7(), new(new(2026, 10, 1), new(2026, 10, 1), "applied-payment"), fixture.LegalEntityId, draft.Id,
            "reverse-applied-" + Guid.CreateVersion7().ToString("N"), CancellationToken.None));
        Assert.Equal("payments.reversal_has_applications", appliedReversalError.Code);
        await Assert.ThrowsAsync<PaymentException>(() => payments.CreateRefundDraftAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(),
            fixture.LegalEntityId, draft.Id, new(fixture.PaymentAccountId, PaymentMethod.BankTransfer, "100.76", new(2026, 10, 1), new(2026, 10, 1), null, "applied-balance", null),
            "refund-applied-" + Guid.CreateVersion7().ToString("N"), CancellationToken.None));

        var refundSource = await payments.CreateDraftAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(), command with { Amount = "50.00", BankReference = "INTEGRATION-REFUND-" + Guid.CreateVersion7().ToString("N") },
            "refund-source-" + Guid.CreateVersion7().ToString("N"), CancellationToken.None);
        var refundSourceCompleted = await payments.CompleteManualAsync(tenant, fixture.ActorId, fixture.MembershipId, fixture.LegalEntityId,
            refundSource.Id, "refund-source-complete-" + Guid.CreateVersion7().ToString("N"), Guid.CreateVersion7(), CancellationToken.None);
        Assert.Equal(PaymentBusinessState.Completed, refundSourceCompleted.State);
        var refundAttempts = await Task.WhenAll(Enumerable.Range(0, 2).Select(async index =>
        {
            try
            {
                return await payments.CreateRefundDraftAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(), fixture.LegalEntityId,
                    refundSource.Id, new(fixture.PaymentAccountId, PaymentMethod.BankTransfer, "30.00", new(2026, 10, 1), new(2026, 10, 1), null,
                        "concurrent-refund", null), "refund-race-" + index + "-" + Guid.CreateVersion7().ToString("N"), CancellationToken.None);
            }
            catch (PaymentException) { return null; }
        }));
        var reservedRefund = Assert.Single(refundAttempts, attempt => attempt is not null)!;
        Assert.Single(refundAttempts, attempt => attempt is null);
        await payments.CancelPendingAsync(tenant, fixture.ActorId, fixture.MembershipId, fixture.LegalEntityId, null, reservedRefund.Id,
            "refund-race-cancel-" + Guid.CreateVersion7().ToString("N"), Guid.CreateVersion7(), CancellationToken.None);
        var refund = await payments.CreateRefundDraftAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(), fixture.LegalEntityId,
            refundSource.Id, new(fixture.PaymentAccountId, PaymentMethod.BankTransfer, "25.00", new(2026, 10, 1), new(2026, 10, 1), null, "customer-request", "REFUND-TEST"),
            "refund-" + Guid.CreateVersion7().ToString("N"), CancellationToken.None);
        Assert.Equal(PaymentType.CustomerRefund, refund.Type);
        await Assert.ThrowsAsync<PaymentException>(() => payments.ApplyToPaymentAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(), fixture.LegalEntityId, null,
            refundSource.Id, new(new(2026, 10, 1), new(2026, 10, 1), [new(fixture.TargetOpenItemId, "10.00")]),
            "apply-during-refund-" + Guid.CreateVersion7().ToString("N"), CancellationToken.None));
        var completedRefund = await payments.CompleteManualAsync(tenant, fixture.ActorId, fixture.MembershipId, fixture.LegalEntityId,
            refund.Id, "refund-complete-" + Guid.CreateVersion7().ToString("N"), Guid.CreateVersion7(), CancellationToken.None);
        Assert.Equal(PaymentBusinessState.Completed, completedRefund.State);
        await Assert.ThrowsAsync<PaymentException>(() => payments.CreateRefundDraftAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(),
            fixture.LegalEntityId, refundSource.Id, new(fixture.PaymentAccountId, PaymentMethod.BankTransfer, "25.01", new(2026, 10, 1), new(2026, 10, 1), null, "over-refund", null),
            "refund-over-" + Guid.CreateVersion7().ToString("N"), CancellationToken.None));
        var finalRefund = await payments.CreateRefundDraftAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(), fixture.LegalEntityId,
            refundSource.Id, new(fixture.PaymentAccountId, PaymentMethod.BankTransfer, "25.00", new(2026, 10, 1), new(2026, 10, 1), null, "remaining-balance", null),
            "refund-final-" + Guid.CreateVersion7().ToString("N"), CancellationToken.None);
        await payments.CompleteManualAsync(tenant, fixture.ActorId, fixture.MembershipId, fixture.LegalEntityId, finalRefund.Id,
            "refund-final-complete-" + Guid.CreateVersion7().ToString("N"), Guid.CreateVersion7(), CancellationToken.None);
        Assert.Equal(50m, (decimal)(await ScalarAsync(admin, "select sum(amount) from finance_payments.payment_refund where tenant_id=$1 and legal_entity_id=$2 and original_payment_id=$3 and status='Completed'", fixture.TenantId, fixture.LegalEntityId, refundSource.Id))!);
        Assert.Equal(PaymentBusinessState.Completed, (await payments.GetAsync(tenant, fixture.LegalEntityId, null, refundSource.Id, CancellationToken.None))!.State);
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_payments.payment_refund where tenant_id=$1 and legal_entity_id=$2 and refund_payment_id=$3 and status='Completed'", fixture.TenantId, fixture.LegalEntityId, refund.Id));
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_ledger.accounting_fact where tenant_id=$1 and legal_entity_id=$2 and id=$3 and fact_type='RefundPaid' and status='Posted'", fixture.TenantId, fixture.LegalEntityId, refund.Id));

        var feePayment = await payments.CreateDraftAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(), command with
        { Amount = "40.00", BankReference = "INTEGRATION-FEE-" + Guid.CreateVersion7().ToString("N") },
            "fee-payment-" + Guid.CreateVersion7().ToString("N"), CancellationToken.None);
        var feeKey = "payment-fee-" + Guid.CreateVersion7().ToString("N");
        var fee = await payments.AddFeeAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(), fixture.LegalEntityId, feePayment.Id,
            new(PaymentFeeType.BankFee, "2.50", "EGP", "BANK-FEE-REF-1"), feeKey, CancellationToken.None);
        var feeRetry = await payments.AddFeeAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(), fixture.LegalEntityId, feePayment.Id,
            new(PaymentFeeType.BankFee, "2.50", "EGP", "BANK-FEE-REF-1"), feeKey, CancellationToken.None);
        Assert.Equal(fee.Id, feeRetry.Id);
        var providerFee = await payments.AddFeeAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(), fixture.LegalEntityId, feePayment.Id,
            new(PaymentFeeType.ProviderFee, "1.25", "EGP", "PROVIDER-FEE-REF-1"), "provider-fee-" + Guid.CreateVersion7().ToString("N"), CancellationToken.None);
        await payments.CompleteManualAsync(tenant, fixture.ActorId, fixture.MembershipId, fixture.LegalEntityId, feePayment.Id,
            "fee-complete-" + Guid.CreateVersion7().ToString("N"), Guid.CreateVersion7(), CancellationToken.None);
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_payments.payment_fee where tenant_id=$1 and legal_entity_id=$2 and id=$3 and recognition_state='Recognized'", fixture.TenantId, fixture.LegalEntityId, fee.Id));
        Assert.Equal(2L, await CountAsync(admin, "select count(*) from finance_ledger.journal_entry_line line join finance_ledger.journal_entry entry on entry.tenant_id=line.tenant_id and entry.legal_entity_id=line.legal_entity_id and entry.id=line.journal_entry_id where entry.tenant_id=$1 and entry.legal_entity_id=$2 and entry.fact_id=$3", fixture.TenantId, fixture.LegalEntityId, feePayment.Id));
        Assert.Equal(2L, await CountAsync(admin, "select count(*) from finance_ledger.journal_entry_line line join finance_ledger.journal_entry entry on entry.tenant_id=line.tenant_id and entry.legal_entity_id=line.legal_entity_id and entry.id=line.journal_entry_id where entry.tenant_id=$1 and entry.legal_entity_id=$2 and entry.fact_id=$3 and entry.fact_type='PaymentFeeRecognized'", fixture.TenantId, fixture.LegalEntityId, fee.Id));
        Assert.Equal(2L, (await payments.ListFeesAsync(tenant, fixture.LegalEntityId, feePayment.Id, CancellationToken.None)).Count);
        var missingDispositions = await Assert.ThrowsAsync<PaymentException>(() => payments.ReverseAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(),
            new(new(2026, 10, 1), new(2026, 10, 1), "payment-cancelled", []), fixture.LegalEntityId, feePayment.Id,
            "fee-reversal-missing-" + Guid.CreateVersion7().ToString("N"), CancellationToken.None));
        Assert.Equal("payments.reversal_fee_disposition_required", missingDispositions.Code);
        var feeReversalKey = "fee-reversal-" + Guid.CreateVersion7().ToString("N");
        var feeReversalRequest = new ReversePaymentRequest(new(2026, 10, 1), new(2026, 10, 1), "payment-cancelled",
        [
            new(fee.Id, PaymentFeeDisposition.Retain),
            new(providerFee.Id, PaymentFeeDisposition.Reverse, "PROVIDER-REFUND-CASE-731")
        ]);
        var feeReversal = await payments.ReverseAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(),
            feeReversalRequest, fixture.LegalEntityId, feePayment.Id, feeReversalKey, CancellationToken.None);
        var feeReversalRetry = await payments.ReverseAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(),
            feeReversalRequest, fixture.LegalEntityId, feePayment.Id, feeReversalKey, CancellationToken.None);
        Assert.Equal(feeReversal.Id, feeReversalRetry.Id);
        var changedDisposition = await Assert.ThrowsAsync<PaymentException>(() => payments.ReverseAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(),
            feeReversalRequest with { FeeDispositions = [new(fee.Id, PaymentFeeDisposition.Reverse, "BANK-REFUND-CASE"), new(providerFee.Id, PaymentFeeDisposition.Retain)] },
            fixture.LegalEntityId, feePayment.Id, feeReversalKey, CancellationToken.None));
        Assert.Equal("payments.idempotency_conflict", changedDisposition.Code);
        Assert.Equal("Recognized", (string)(await ScalarAsync(admin, "select recognition_state from finance_payments.payment_fee where tenant_id=$1 and legal_entity_id=$2 and id=$3", fixture.TenantId, fixture.LegalEntityId, fee.Id))!);
        Assert.Equal("Recognized", (string)(await ScalarAsync(admin, "select recognition_state from finance_payments.payment_fee where tenant_id=$1 and legal_entity_id=$2 and id=$3", fixture.TenantId, fixture.LegalEntityId, providerFee.Id))!);
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_payments.payment_fee_disposition where tenant_id=$1 and legal_entity_id=$2 and payment_fee_id=$3 and disposition='Retain' and corrective_fact_id is null", fixture.TenantId, fixture.LegalEntityId, fee.Id));
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_payments.payment_fee_disposition where tenant_id=$1 and legal_entity_id=$2 and payment_fee_id=$3 and disposition='Reverse' and evidence_reference='PROVIDER-REFUND-CASE-731'", fixture.TenantId, fixture.LegalEntityId, providerFee.Id));
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_ledger.accounting_fact where tenant_id=$1 and legal_entity_id=$2 and id=(select corrective_fact_id from finance_payments.payment_fee_disposition where tenant_id=$1 and legal_entity_id=$2 and payment_fee_id=$3) and fact_type='PaymentFeeReversed' and status='Posted'", fixture.TenantId, fixture.LegalEntityId, providerFee.Id));
        Assert.Equal(2L, await CountAsync(admin, "select count(*) from finance_ledger.journal_entry_line line join finance_ledger.journal_entry entry on entry.tenant_id=line.tenant_id and entry.legal_entity_id=line.legal_entity_id and entry.id=line.journal_entry_id where entry.tenant_id=$1 and entry.legal_entity_id=$2 and entry.fact_type='PaymentFeeReversed' and entry.source_reference=$3", fixture.TenantId, fixture.LegalEntityId, $"payment-fee-reversal:{providerFee.Id:D}"));
        Assert.Equal(0L, await CountAsync(admin, "select count(*) from finance_ledger.journal_entry_line line join finance_ledger.journal_entry entry on entry.tenant_id=line.tenant_id and entry.legal_entity_id=line.legal_entity_id and entry.id=line.journal_entry_id where entry.tenant_id=$1 and entry.legal_entity_id=$2 and entry.fact_id in ($3,(select corrective_fact_id from finance_payments.payment_fee_disposition where tenant_id=$1 and legal_entity_id=$2 and payment_fee_id=$4)) group by line.account_id having sum(line.base_debit-line.base_credit)<>0", fixture.TenantId, fixture.LegalEntityId, providerFee.Id, providerFee.Id));
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_ledger.accounting_fact where tenant_id=$1 and legal_entity_id=$2 and id=(select corrective_fact_id from finance_payments.payment_fee_disposition where tenant_id=$1 and legal_entity_id=$2 and payment_fee_id=$3) and payload_jsonb->>'evidenceReference'='PROVIDER-REFUND-CASE-731' and payload_jsonb->>'originalFeeFactId'=$3::text", fixture.TenantId, fixture.LegalEntityId, providerFee.Id));
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_payments.payment_reversal where tenant_id=$1 and legal_entity_id=$2 and original_payment_id=$3 and reversal_payment_id=$4", fixture.TenantId, fixture.LegalEntityId, feePayment.Id, feeReversal.Id));
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from platform.outbox_message where tenant_id=$1 and source_module='payments' and causation_id=$2 and message_type='PaymentReversedV1' and payload->'feeDispositions' @> jsonb_build_array(jsonb_build_object('feeId',$3::text,'disposition','Retain')) and payload->'feeDispositions' @> jsonb_build_array(jsonb_build_object('feeId',$4::text,'disposition','Reverse'))", fixture.TenantId, feePayment.Id, fee.Id, providerFee.Id));
        await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var immutableFee = new NpgsqlCommand("update finance_payments.payment_fee set amount=amount+1 where tenant_id=$1 and legal_entity_id=$2 and id=$3", admin);
            immutableFee.Parameters.AddWithValue(fixture.TenantId); immutableFee.Parameters.AddWithValue(fixture.LegalEntityId); immutableFee.Parameters.AddWithValue(providerFee.Id);
            await immutableFee.ExecuteNonQueryAsync();
        });
        await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var immutableDisposition = new NpgsqlCommand("update finance_payments.payment_fee_disposition set evidence_reference='altered' where tenant_id=$1 and legal_entity_id=$2 and payment_fee_id=$3", admin);
            immutableDisposition.Parameters.AddWithValue(fixture.TenantId); immutableDisposition.Parameters.AddWithValue(fixture.LegalEntityId); immutableDisposition.Parameters.AddWithValue(providerFee.Id);
            await immutableDisposition.ExecuteNonQueryAsync();
        });

        var failurePayment = await payments.CreateDraftAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(), command with
        { Amount = "12.00", BankReference = "REVERSAL-ATOMICITY-" + Guid.CreateVersion7().ToString("N") },
            "reversal-atomicity-payment-" + Guid.CreateVersion7().ToString("N"), CancellationToken.None);
        var failureFee = await payments.AddFeeAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(), fixture.LegalEntityId, failurePayment.Id,
            new(PaymentFeeType.BankFee, "0.75", "EGP", "ATOMICITY-FEE-REF"), "reversal-atomicity-fee-" + Guid.CreateVersion7().ToString("N"), CancellationToken.None);
        await payments.CompleteManualAsync(tenant, fixture.ActorId, fixture.MembershipId, fixture.LegalEntityId, failurePayment.Id,
            "reversal-atomicity-complete-" + Guid.CreateVersion7().ToString("N"), Guid.CreateVersion7(), CancellationToken.None);
        var triggerSuffix = Guid.CreateVersion7().ToString("N");
        var triggerName = $"reject_fee_correction_{triggerSuffix}";
        var functionName = $"reject_fee_correction_fn_{triggerSuffix}";
        var atomicityKey = "reversal-atomicity-key-" + Guid.CreateVersion7().ToString("N");
        await using (var inject = new NpgsqlCommand($"create function public.{functionName}() returns trigger language plpgsql as $$ begin if new.fact_type='PaymentFeeReversed' then raise exception 'injected payment fee reversal failure'; end if; return new; end $$; create trigger {triggerName} before insert on finance_ledger.accounting_fact for each row execute function public.{functionName}()", admin))
            await inject.ExecuteNonQueryAsync();
        try
        {
            await Assert.ThrowsAsync<PostgresException>(() => payments.ReverseAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(),
                new(new(2026, 10, 1), new(2026, 10, 1), "bank-confirmed-reversal", [new(failureFee.Id, PaymentFeeDisposition.Reverse, "BANK-CASE-ATOMIC-15")]),
                fixture.LegalEntityId, failurePayment.Id, atomicityKey, CancellationToken.None));
            Assert.Equal(PaymentBusinessState.Completed, (await payments.GetAsync(tenant, fixture.LegalEntityId, null, failurePayment.Id, CancellationToken.None))!.State);
            Assert.Equal(0L, await CountAsync(admin, "select count(*) from finance_payments.payment_reversal where tenant_id=$1 and legal_entity_id=$2 and original_payment_id=$3", fixture.TenantId, fixture.LegalEntityId, failurePayment.Id));
            Assert.Equal(0L, await CountAsync(admin, "select count(*) from finance_payments.payment_fee_disposition where tenant_id=$1 and legal_entity_id=$2 and payment_fee_id=$3", fixture.TenantId, fixture.LegalEntityId, failureFee.Id));
            Assert.Equal(0L, await CountAsync(admin, "select count(*) from finance_ledger.accounting_fact where tenant_id=$1 and legal_entity_id=$2 and source_reference=$3 and fact_type='PaymentFeeReversed'", fixture.TenantId, fixture.LegalEntityId, $"payment-fee-reversal:{failureFee.Id:D}"));
        }
        finally
        {
            await using var removeInjection = new NpgsqlCommand($"drop trigger if exists {triggerName} on finance_ledger.accounting_fact; drop function if exists public.{functionName}()", admin);
            await removeInjection.ExecuteNonQueryAsync();
        }
        var recoveredAtomicityReversal = await payments.ReverseAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(),
            new(new(2026, 10, 1), new(2026, 10, 1), "bank-confirmed-reversal", [new(failureFee.Id, PaymentFeeDisposition.Reverse, "BANK-CASE-ATOMIC-15")]),
            fixture.LegalEntityId, failurePayment.Id, atomicityKey, CancellationToken.None);
        Assert.Equal(PaymentBusinessState.Reversed, (await payments.GetAsync(tenant, fixture.LegalEntityId, null, failurePayment.Id, CancellationToken.None))!.State);
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_ledger.accounting_fact where tenant_id=$1 and legal_entity_id=$2 and fact_type='PaymentFeeReversed' and source_reference=$3 and status='Posted'", fixture.TenantId, fixture.LegalEntityId, $"payment-fee-reversal:{failureFee.Id:D}"));
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_payments.payment_fee_disposition where tenant_id=$1 and legal_entity_id=$2 and payment_fee_id=$3 and corrective_fact_id is not null", fixture.TenantId, fixture.LegalEntityId, failureFee.Id));

        var reversalSource = await payments.CreateDraftAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(), command with
        { Amount = "15.00", BankReference = "INTEGRATION-REVERSAL-" + Guid.CreateVersion7().ToString("N") },
            "reversal-source-" + Guid.CreateVersion7().ToString("N"), CancellationToken.None);
        await payments.CompleteManualAsync(tenant, fixture.ActorId, fixture.MembershipId, fixture.LegalEntityId, reversalSource.Id,
            "reversal-source-complete-" + Guid.CreateVersion7().ToString("N"), Guid.CreateVersion7(), CancellationToken.None);
        var reversalKey = "payment-reversal-" + Guid.CreateVersion7().ToString("N");
        var reversal = await payments.ReverseAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(),
            new(new(2026, 10, 1), new(2026, 10, 1), "duplicate-bank-transfer"), fixture.LegalEntityId, reversalSource.Id, reversalKey, CancellationToken.None);
        var reversalRetry = await payments.ReverseAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(),
            new(new(2026, 10, 1), new(2026, 10, 1), "duplicate-bank-transfer"), fixture.LegalEntityId, reversalSource.Id, reversalKey, CancellationToken.None);
        Assert.Equal(reversal.Id, reversalRetry.Id);
        Assert.Equal(PaymentBusinessState.Reversed, (await payments.GetAsync(tenant, fixture.LegalEntityId, null, reversalSource.Id, CancellationToken.None))!.State);
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_payments.payment_reversal where tenant_id=$1 and legal_entity_id=$2 and original_payment_id=$3 and reversal_payment_id=$4 and status='Completed'", fixture.TenantId, fixture.LegalEntityId, reversalSource.Id, reversal.Id));
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_ledger.accounting_fact where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status='Posted'", fixture.TenantId, fixture.LegalEntityId, reversal.Id));
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from platform.outbox_message where tenant_id=$1 and source_module='payments' and causation_id=$2 and message_type='PaymentReversedV1'", fixture.TenantId, reversalSource.Id));

        var destinationAccount = await payments.CreateAccountAsync(tenant, fixture.ActorId, fixture.MembershipId,
            new(fixture.LegalEntityId, null, "BANK-TRANSFER-" + Guid.CreateVersion7().ToString("N")[^8..], "Integration transfer account", PaymentAccountType.Bank,
                "SingleCurrency", "EGP", "Integration Bank", "Atlas Trading", "••••5678", null), Guid.CreateVersion7(), CancellationToken.None);
        var transferRequest = new CreateInternalTransferRequest(fixture.LegalEntityId, fixture.PaymentAccountId, destinationAccount.Id,
            "200.00", "EGP", new(2026, 10, 1), new(2026, 10, 1), null, "Integration transfer");
        var transferKey = "transfer-" + Guid.CreateVersion7().ToString("N");
        var transferResults = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => payments.CreateInternalTransferAsync(tenant, fixture.ActorId,
            fixture.MembershipId, Guid.CreateVersion7(), transferRequest, transferKey, CancellationToken.None)));
        var transfer = transferResults[0];
        Assert.All(transferResults, result => Assert.Equal(transfer.Id, result.Id));
        Assert.Equal(PaymentDirection.InternalTransfer, transfer.SourcePayment.Direction);
        Assert.Equal(PaymentDirection.InternalTransfer, transfer.DestinationPayment.Direction);
        Assert.Null(transfer.SourcePayment.PartyId);
        Assert.Null(transfer.DestinationPayment.PartyId);
        Assert.Equal(transfer.SourcePayment.Amount, transfer.DestinationPayment.Amount);
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_payments.internal_transfer where tenant_id=$1 and legal_entity_id=$2 and id=$3 and source_amount=destination_amount and source_currency=destination_currency and status='Completed'", fixture.TenantId, fixture.LegalEntityId, transfer.Id));
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_ledger.accounting_fact where tenant_id=$1 and legal_entity_id=$2 and id=$3 and fact_type='InternalTransferCompleted' and status='Posted'", fixture.TenantId, fixture.LegalEntityId, transfer.Id));
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from platform.outbox_message where tenant_id=$1 and source_module='payments' and causation_id=$2 and message_type='InternalTransferCompletedV1'", fixture.TenantId, transfer.Id));
        Assert.Equal(0L, await CountAsync(admin, "select count(*) from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and source_type='Payment' and source_reference in ($3,$4)", fixture.TenantId, fixture.LegalEntityId, transfer.SourcePayment.Id.ToString("D"), transfer.DestinationPayment.Id.ToString("D")));

        var crossCurrencyTransfer = await payments.CreateInternalTransferAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(),
            new(fixture.LegalEntityId, fixture.PaymentAccountId, fixture.UsdPaymentAccountId, "480.00", "EGP", new(2026, 10, 1),
                new(2026, 10, 1), null, "Integration FX transfer", "USD"), "transfer-fx-" + Guid.CreateVersion7().ToString("N"), CancellationToken.None);
        Assert.Equal("EGP", crossCurrencyTransfer.SourceCurrencyCode);
        Assert.Equal("USD", crossCurrencyTransfer.DestinationCurrencyCode);
        Assert.Equal(480m, crossCurrencyTransfer.SourceAmount);
        Assert.True(crossCurrencyTransfer.DestinationAmount > 0m);
        Assert.False(string.IsNullOrWhiteSpace(crossCurrencyTransfer.FxSnapshot));
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_payments.internal_transfer where tenant_id=$1 and legal_entity_id=$2 and id=$3 and source_currency<>destination_currency and fx_rate>0 and fx_snapshot is not null", fixture.TenantId, fixture.LegalEntityId, crossCurrencyTransfer.Id));
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_ledger.accounting_fact where tenant_id=$1 and legal_entity_id=$2 and id=$3 and fact_type='InternalTransferCompleted' and status='Posted'", fixture.TenantId, fixture.LegalEntityId, crossCurrencyTransfer.Id));

        var providerAccount = await payments.CreateAccountAsync(tenant, fixture.ActorId, fixture.MembershipId,
            new(fixture.LegalEntityId, null, "PROVIDER-" + Guid.CreateVersion7().ToString("N")[^8..], "Reference provider clearing", PaymentAccountType.ProviderClearing,
                "MultiCurrency", null, null, null, null, null), Guid.CreateVersion7(), CancellationToken.None);
        var provider = await payments.CreateProviderConfigAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(),
            new(fixture.LegalEntityId, "reference", "Sandbox", "ATLAS-TEST-" + Guid.CreateVersion7().ToString("N")[^8..], "2026-01",
                new(true, true, false, true, true, true), ["EGP"], ["PaymentGateway"], providerAccount.Id, "secret://payments/reference/test"), CancellationToken.None);
        Assert.True(provider.HasSecretReference);
        var paymentIntent = await payments.CreatePaymentIntentAsync(tenant, fixture.ActorId, fixture.MembershipId, Guid.CreateVersion7(),
            new(fixture.LegalEntityId, null, provider.Id, fixture.PartyId, "75.00", "EGP", "CustomerInvoice", Guid.CreateVersion7().ToString("D"), DateTimeOffset.UtcNow.AddDays(2)),
            "intent-" + Guid.CreateVersion7().ToString("N"), CancellationToken.None);
        var paymentRequestDetails = await payments.GetPaymentRequestDetailsAsync(tenant, fixture.LegalEntityId, paymentIntent.Id, CancellationToken.None);
        Assert.NotNull(paymentRequestDetails);
        Assert.Single(paymentRequestDetails.Attempts);
        Assert.Equal("Created", paymentRequestDetails.Attempts[0].State);
        var requestPage = await payments.ListPaymentRequestsAsync(tenant, new(fixture.LegalEntityId, 1, 1, paymentIntent.MerchantReference, null), CancellationToken.None);
        Assert.Single(requestPage.Items);
        Assert.False(requestPage.HasMore);
        var paymentLink = await payments.CreatePaymentLinkAsync(tenant, new(paymentIntent.Id, DateTimeOffset.UtcNow.AddDays(1)), fixture.LegalEntityId, CancellationToken.None);
        Assert.Equal("Pending", paymentIntent.State);
        Assert.Equal("Active", paymentLink.Status);
        Assert.Equal(97, paymentLink.Token.Length);
        var publicFixedLink = await payments.GetPublicPaymentLinkAsync(paymentLink.Token, CancellationToken.None);
        Assert.NotNull(publicFixedLink);
        Assert.Equal("Fixed", publicFixedLink.AmountPolicy);
        Assert.Equal(75m, publicFixedLink.Amount);
        var boundedLink = await payments.CreatePaymentLinkAsync(tenant,
            new(paymentIntent.Id, DateTimeOffset.UtcNow.AddDays(1), PaymentLinkAmountPolicy.CustomerEnteredWithinLimits, "10.00", "75.00"),
            fixture.LegalEntityId, CancellationToken.None);
        var publicBoundedLink = await payments.GetPublicPaymentLinkAsync(boundedLink.Token, CancellationToken.None);
        Assert.NotNull(publicBoundedLink);
        Assert.Equal("CustomerEnteredWithinLimits", publicBoundedLink.AmountPolicy);
        Assert.Equal(10m, publicBoundedLink.MinimumAmount);
        Assert.Equal(75m, publicBoundedLink.MaximumAmount);
        await Assert.ThrowsAsync<PaymentException>(() => payments.CreatePaymentLinkAsync(tenant,
            new(paymentIntent.Id, DateTimeOffset.UtcNow.AddDays(1), PaymentLinkAmountPolicy.PartialAllowed, "10.00", "80.00"),
            fixture.LegalEntityId, CancellationToken.None));
        Assert.Equal(0L, await CountAsync(admin, "select count(*) from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and source_type='PaymentRequest' and source_reference=$3 and business_state='Completed'", fixture.TenantId, fixture.LegalEntityId, paymentIntent.Id.ToString("D")));
        await payments.CancelPaymentLinkAsync(tenant, fixture.LegalEntityId, paymentIntent.Id, paymentLink.Id, CancellationToken.None);
        Assert.Null(await payments.GetPublicPaymentLinkAsync(paymentLink.Token, CancellationToken.None));
        var activePaymentLink = await payments.CreatePaymentLinkAsync(tenant, new(paymentIntent.Id, DateTimeOffset.UtcNow.AddDays(1)), fixture.LegalEntityId, CancellationToken.None);
        var providerOperationId = (Guid)(await ScalarAsync(admin, "select id from finance_payments.provider_operation where tenant_id=$1 and legal_entity_id=$2 and aggregate_id=$3 and operation_type='Create'", fixture.TenantId, fixture.LegalEntityId, paymentIntent.Id))!;
        await payments.StartReferenceProviderOperationAsync(tenant, providerOperationId, CancellationToken.None);
        await payments.StartReferenceProviderOperationAsync(tenant, providerOperationId, CancellationToken.None);
        Assert.Equal(0L, await CountAsync(admin, "select count(*) from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and source_type='PaymentRequest' and source_reference=$3 and business_state='Completed'", fixture.TenantId, fixture.LegalEntityId, paymentIntent.Id.ToString("D")));
        var providerTransactionId = (string)(await ScalarAsync(admin, "select provider_transaction_id from finance_payments.payment_attempt where tenant_id=$1 and legal_entity_id=$2 and payment_request_id=$3", fixture.TenantId, fixture.LegalEntityId, paymentIntent.Id))!;
        const string webhookSecret = "integration-reference-webhook-secret";
        var eventId = "event-" + Guid.CreateVersion7().ToString("N");
        var eventTime = DateTimeOffset.UtcNow;
        var webhookBody = JsonSerializer.SerializeToUtf8Bytes(new { eventId, merchantReference = paymentIntent.MerchantReference, transactionId = providerTransactionId, eventType = "payment.succeeded", occurredAt = eventTime });
        var validSignature = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(webhookSecret), webhookBody)).ToLowerInvariant();
        var secretVariable = EnvironmentProviderSecretResolver.SecretEnvironmentVariableName("secret://payments/reference/test");
        var previousSecret = Environment.GetEnvironmentVariable(secretVariable);
        Environment.SetEnvironmentVariable(secretVariable, webhookSecret);
        try
        {
            var invalidSignature = await Assert.ThrowsAsync<PaymentException>(() => payments.ReceiveReferenceWebhookAsync(tenant, provider.Id, webhookBody,
                "sha256=" + new string('0', 64), CancellationToken.None));
            Assert.Equal("payments.webhook_signature_invalid", invalidSignature.Code);
            Assert.Equal(0L, await CountAsync(admin, "select count(*) from finance_payments.provider_webhook_inbox where tenant_id=$1 and provider_config_id=$2 and provider_event_id=$3", fixture.TenantId, provider.Id, eventId));
            await InstallFailureTriggerAsync(adminConnectionString, "finance_ledger.accounting_fact", "INSERT");
            try
            {
                await Assert.ThrowsAsync<PostgresException>(() => payments.ReceiveReferenceWebhookAsync(tenant, provider.Id, webhookBody, validSignature, CancellationToken.None));
            }
            finally { await RemoveFailureTriggerAsync(adminConnectionString, "finance_ledger.accounting_fact"); }
            Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_payments.provider_webhook_inbox where tenant_id=$1 and provider_config_id=$2 and provider_event_id=$3 and processing_state='Received'", fixture.TenantId, provider.Id, eventId));
            var recoveredIntent = await payments.SyncReferenceProviderStatusAsync(tenant, fixture.LegalEntityId, paymentIntent.Id, CancellationToken.None);
            Assert.Equal("Succeeded", recoveredIntent.State);
            await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => payments.ReceiveReferenceWebhookAsync(tenant, provider.Id, webhookBody, validSignature, CancellationToken.None)));
            Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and source_type='PaymentRequest' and source_reference=$3 and business_state='Completed' and provider_code='reference' and provider_transaction_id is not null", fixture.TenantId, fixture.LegalEntityId, paymentIntent.Id.ToString("D")));
            Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and source_type='Payment' and source_reference=(select id::text from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and source_type='PaymentRequest' and source_reference=$3)", fixture.TenantId, fixture.LegalEntityId, paymentIntent.Id.ToString("D")));
            Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_ledger.accounting_fact where tenant_id=$1 and legal_entity_id=$2 and id=(select id from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and source_type='PaymentRequest' and source_reference=$3) and status='Posted'", fixture.TenantId, fixture.LegalEntityId, paymentIntent.Id.ToString("D")));
            Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_payments.payment_attempt where tenant_id=$1 and legal_entity_id=$2 and payment_request_id=$3 and state='Succeeded' and provider_transaction_id is not null", fixture.TenantId, fixture.LegalEntityId, paymentIntent.Id));
            Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_payments.payment_request where tenant_id=$1 and legal_entity_id=$2 and id=$3 and workflow_state='Succeeded'", fixture.TenantId, fixture.LegalEntityId, paymentIntent.Id));
            Assert.Equal(1L, await CountAsync(admin, "select count(*) from platform.outbox_message where tenant_id=$1 and source_module='payments' and message_type='PaymentCompletedV1' and causation_id=(select id from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and source_type='PaymentRequest' and source_reference=$3)", fixture.TenantId, fixture.LegalEntityId, paymentIntent.Id.ToString("D")));
            Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_payments.provider_webhook_inbox where tenant_id=$1 and provider_config_id=$2 and provider_event_id=$3 and signature_verified and processing_state='Processed'", fixture.TenantId, provider.Id, eventId));
            Assert.Equal(0L, await CountAsync(admin, "select count(*) from finance_payments.payment_link where tenant_id=$1 and id=$2 and status<>'Completed'", fixture.TenantId, activePaymentLink.Id));

            var providerRefundKey = "provider-refund-" + Guid.CreateVersion7().ToString("N");
            var providerRefundRequest = new CreateRefundRequest(providerAccount.Id, PaymentMethod.PaymentGateway, "5.00", new(2026, 10, 1), new(2026, 10, 1), null, "provider-refund-test", null);
            var originalProviderPaymentId = (Guid)(await ScalarAsync(admin, "select id from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and source_type='PaymentRequest' and source_reference=$3", fixture.TenantId, fixture.LegalEntityId, paymentIntent.Id.ToString("D")))!;
            await InstallFailureTriggerAsync(adminConnectionString, "finance_ledger.accounting_fact", "INSERT");
            try
            {
                await Assert.ThrowsAsync<PostgresException>(() => payments.CreateProviderRefundAsync(tenant, fixture.ActorId, fixture.MembershipId,
                    Guid.CreateVersion7(), fixture.LegalEntityId, originalProviderPaymentId, providerRefundRequest, providerRefundKey, CancellationToken.None));
            }
            finally { await RemoveFailureTriggerAsync(adminConnectionString, "finance_ledger.accounting_fact"); }
            var pendingProviderRefundId = (Guid)(await ScalarAsync(admin, "select refund_payment_id from finance_payments.payment_refund where tenant_id=$1 and legal_entity_id=$2 and original_payment_id=(select id from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and source_type='PaymentRequest' and source_reference=$3) and idempotency_key=$4", fixture.TenantId, fixture.LegalEntityId, paymentIntent.Id.ToString("D"), providerRefundKey))!;
            Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_payments.provider_operation operation join finance_payments.payment_refund refund on refund.tenant_id=operation.tenant_id and refund.legal_entity_id=operation.legal_entity_id and refund.id=operation.aggregate_id where operation.tenant_id=$1 and operation.legal_entity_id=$2 and refund.refund_payment_id=$3 and operation.operation_type='Refund' and operation.state='Succeeded'", fixture.TenantId, fixture.LegalEntityId, pendingProviderRefundId));
            var recoveredProviderRefund = await payments.SyncProviderRefundStatusAsync(tenant, fixture.ActorId, fixture.MembershipId, fixture.LegalEntityId, pendingProviderRefundId, CancellationToken.None);
            Assert.Equal("Succeeded", recoveredProviderRefund.ProviderState); Assert.Equal(PaymentBusinessState.Completed, recoveredProviderRefund.Refund.State);
            Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_subledger.party_open_item where tenant_id=$1 and legal_entity_id=$2 and source_type='Payment' and source_reference=$3", fixture.TenantId, fixture.LegalEntityId, pendingProviderRefundId.ToString("D")));
            Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_ledger.accounting_fact where tenant_id=$1 and legal_entity_id=$2 and id=$3 and fact_type='RefundPaid' and status='Posted'", fixture.TenantId, fixture.LegalEntityId, pendingProviderRefundId));

            var lateFailure = JsonSerializer.SerializeToUtf8Bytes(new { eventId = eventId + "-late-failure", merchantReference = paymentIntent.MerchantReference, transactionId = providerTransactionId, eventType = "payment.failed", occurredAt = eventTime.AddMinutes(-5) });
            var lateFailureSignature = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(webhookSecret), lateFailure)).ToLowerInvariant();
            await payments.ReceiveReferenceWebhookAsync(tenant, provider.Id, lateFailure, lateFailureSignature, CancellationToken.None);
            var lateAuthorization = JsonSerializer.SerializeToUtf8Bytes(new { eventId = eventId + "-late-authorization", merchantReference = paymentIntent.MerchantReference, transactionId = providerTransactionId, eventType = "payment.authorized", occurredAt = eventTime.AddMinutes(-8) });
            var lateAuthorizationSignature = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(webhookSecret), lateAuthorization)).ToLowerInvariant();
            await payments.ReceiveReferenceWebhookAsync(tenant, provider.Id, lateAuthorization, lateAuthorizationSignature, CancellationToken.None);
            Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and source_type='PaymentRequest' and source_reference=$3 and business_state='Completed'", fixture.TenantId, fixture.LegalEntityId, paymentIntent.Id.ToString("D")));
        }
        finally { Environment.SetEnvironmentVariable(secretVariable, previousSecret); }

        await using var runtime = new NpgsqlConnection(runtimeConnectionString);
        await runtime.OpenAsync();
        Assert.Equal(0L, await ScalarCountAsync(runtime, "select count(*) from finance_payments.payment_account"));
        await using (var setOtherTenant = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", runtime))
        { setOtherTenant.Parameters.AddWithValue(fixture.OtherTenantId.ToString("D")); await setOtherTenant.ExecuteNonQueryAsync(); }
        Assert.Equal(0L, await CountAsync(runtime, "select count(*) from finance_payments.payment_account where tenant_id=$1", fixture.TenantId));
    }

    [Fact]
    public async Task EmployeeReimbursementBatchCompletesThroughPaymentsAndLedgerExactlyOnce()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for payment integration tests.");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for payment integration tests.");
        var fixture = await ReadFixtureAsync(adminConnectionString);
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnectionString);
            builder.UseSetting("TODIR_RABBITMQ_HOST", string.Empty);
        });
        var expenses = factory.Services.GetRequiredService<ExpenseRepository>();
        var tenant = new TenantId(fixture.TenantId);
        var batch = await expenses.PrepareReimbursementBatchAsync(tenant, fixture.LegalEntityId, "EGP", CancellationToken.None);
        Assert.Equal("Prepared", batch.State);
        Assert.Equal(320m, batch.TotalAmount);

        var paid = await expenses.PayReimbursementBatchAsync(tenant, fixture.LegalEntityId, batch.Id,
            fixture.ActorId, fixture.MembershipId, fixture.PaymentAccountId, "BankTransfer",
            new(2026, 10, 1), new(2026, 10, 1), batch.Version, CancellationToken.None);
        Assert.Equal("Paid", paid.State);
        Assert.Equal(batch.TotalAmount, paid.TotalAmount);

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        var sourceReference = $"expenses-reimbursement:{batch.Id:D}";
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and source_type='expenses' and source_reference=$3 and business_state='Completed'", fixture.TenantId, fixture.LegalEntityId, sourceReference));
        var paymentId = (Guid)(await ScalarAsync(admin, "select id from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and source_type='expenses' and source_reference=$3 and business_state='Completed'", fixture.TenantId, fixture.LegalEntityId, sourceReference))!;
        Assert.Equal(320m, (decimal)(await ScalarAsync(admin, "select amount from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and id=$3", fixture.TenantId, fixture.LegalEntityId, paymentId))!);
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_ledger.accounting_fact where tenant_id=$1 and legal_entity_id=$2 and id=$3 and fact_type='PaymentMade' and status='Posted'", fixture.TenantId, fixture.LegalEntityId, paymentId));
        var journal = await ReadPaymentJournalAsync(admin, fixture.TenantId, fixture.LegalEntityId, paymentId);
        Assert.Equal(320m, journal.Debit);
        Assert.Equal(320m, journal.Credit);
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_ledger.journal_entry_line l join finance_ledger.journal_entry e on e.tenant_id=l.tenant_id and e.legal_entity_id=l.legal_entity_id and e.id=l.journal_entry_id join finance_ledger.account_mapping_version mv on mv.tenant_id=l.tenant_id and mv.legal_entity_id=l.legal_entity_id and mv.target_account_id=l.account_id join finance_ledger.account_mapping m on m.tenant_id=mv.tenant_id and m.legal_entity_id=mv.legal_entity_id and m.id=mv.mapping_id join finance_ledger.account_role r on r.tenant_id=m.tenant_id and r.legal_entity_id=m.legal_entity_id and r.id=m.role_id where e.tenant_id=$1 and e.legal_entity_id=$2 and e.fact_id=$3 and r.code='EmployeeReimbursementPayable'", fixture.TenantId, fixture.LegalEntityId, paymentId));
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from business_expenses.expense_reimbursement_obligation where tenant_id=$1 and legal_entity_id=$2 and status='Settled' and settled_amount=amount", fixture.TenantId, fixture.LegalEntityId));
        var retry = await expenses.PayReimbursementBatchAsync(tenant, fixture.LegalEntityId, batch.Id,
            fixture.ActorId, fixture.MembershipId, fixture.PaymentAccountId, "BankTransfer",
            new(2026, 10, 1), new(2026, 10, 1), paid.Version, CancellationToken.None);
        Assert.Equal("Paid", retry.State);
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and source_type='expenses' and source_reference=$3", fixture.TenantId, fixture.LegalEntityId, sourceReference));
    }

    [Fact]
    public async Task EmployeeRepaymentSettlesThroughIncomingPaymentAndLedgerExactlyOnce()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION") ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for payment integration tests.");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION") ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for payment integration tests.");
        var fixture = await ReadFixtureAsync(adminConnectionString);
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Development"); builder.UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnectionString); builder.UseSetting("TODIR_RABBITMQ_HOST", string.Empty); });
        var expenses = factory.Services.GetRequiredService<ExpenseRepository>(); var tenant = new TenantId(fixture.TenantId); var employeeId = await ReadEmployeeIdAsync(adminConnectionString, fixture.TenantId, fixture.LegalEntityId); var card = await expenses.ImportCardTransactionAsync(tenant, fixture.LegalEntityId, new($"repayment-accept-{Guid.CreateVersion7():N}", "Hotel", new(2026, 10, 15), 75m), CancellationToken.None); await expenses.MarkCardTransactionPersonalAsync(tenant, fixture.LegalEntityId, card.Id, new(employeeId), CancellationToken.None); var repayments = await expenses.ListEmployeeRepaymentsAsync(tenant, fixture.LegalEntityId, null, "Requested", 1, 10, CancellationToken.None); var repayment = Assert.Single(repayments.Items, x => x.SourceReference == $"corporate-card:{card.Id:D}");
        var settled = await expenses.SettleEmployeeRepaymentAsync(tenant, fixture.LegalEntityId, repayment.Id, fixture.ActorId, fixture.MembershipId, new(fixture.PaymentAccountId, "BankTransfer", 75m, new(2026, 10, 15), new(2026, 10, 15), repayment.Version), CancellationToken.None);
        Assert.Equal("Recorded", settled.State); Assert.Equal(75m, settled.SettledAmount); Assert.NotNull(settled.PaymentId);
        await using var admin = new NpgsqlConnection(adminConnectionString); await admin.OpenAsync(); var sourceReference = $"expenses-repayment:{repayment.Id:D}"; Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and payment_type='EmployeeRepayment' and source_type='ExpensesEmployeeRepayment' and business_state='Completed' and source_reference=$3", fixture.TenantId, fixture.LegalEntityId, sourceReference)); Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_ledger.accounting_fact where tenant_id=$1 and legal_entity_id=$2 and id=$3 and fact_type='PaymentReceived' and status='Posted'", fixture.TenantId, fixture.LegalEntityId, settled.PaymentId!)); var journal = await ReadPaymentJournalAsync(admin, fixture.TenantId, fixture.LegalEntityId, settled.PaymentId!.Value); Assert.Equal(75m, journal.Debit); Assert.Equal(75m, journal.Credit); Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_ledger.journal_entry_line l join finance_ledger.journal_entry e on e.tenant_id=l.tenant_id and e.legal_entity_id=l.legal_entity_id and e.id=l.journal_entry_id join finance_ledger.account_mapping_version mv on mv.tenant_id=l.tenant_id and mv.legal_entity_id=l.legal_entity_id and mv.target_account_id=l.account_id join finance_ledger.account_mapping m on m.tenant_id=mv.tenant_id and m.legal_entity_id=mv.legal_entity_id and m.id=mv.mapping_id join finance_ledger.account_role r on r.tenant_id=m.tenant_id and r.legal_entity_id=m.legal_entity_id and r.id=m.role_id where e.tenant_id=$1 and e.legal_entity_id=$2 and e.fact_id=$3 and r.code='EmployeeReceivable'", fixture.TenantId, fixture.LegalEntityId, settled.PaymentId.Value));
        var retry = await expenses.SettleEmployeeRepaymentAsync(tenant, fixture.LegalEntityId, repayment.Id, fixture.ActorId, fixture.MembershipId, new(fixture.PaymentAccountId, "BankTransfer", 75m, new(2026, 10, 15), new(2026, 10, 15), settled.Version), CancellationToken.None); Assert.Equal("Recorded", retry.State); Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and source_type='ExpensesEmployeeRepayment' and source_reference=$3", fixture.TenantId, fixture.LegalEntityId, sourceReference));
    }

    [Fact]
    public async Task BusinessAdvanceIssuanceCreatesOutgoingPaymentAndEmployeeReceivableExactlyOnce()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION") ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for payment integration tests.");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION") ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for payment integration tests.");
        var fixture = await ReadFixtureAsync(adminConnectionString);
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => { builder.UseEnvironment("Development"); builder.UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnectionString); builder.UseSetting("TODIR_RABBITMQ_HOST", string.Empty); });
        var expenses = factory.Services.GetRequiredService<ExpenseRepository>();
        var tenant = new TenantId(fixture.TenantId);
        var employeeActor = await ReadEmployeeActorAsync(adminConnectionString, fixture.TenantId, fixture.LegalEntityId);
        var employeeId = employeeActor.EmployeeId;
        var advance = await expenses.CreateAdvanceAsync(tenant, new(fixture.LegalEntityId, employeeId, "Project travel advance", 250m), CancellationToken.None);
        advance = await expenses.TransitionAdvanceAsync(tenant, fixture.LegalEntityId, advance.Id, advance.Version, "approve", CancellationToken.None);
        var issued = await expenses.IssueAdvanceAsync(tenant, fixture.LegalEntityId, advance.Id, fixture.ActorId, fixture.MembershipId,
            new(fixture.PaymentAccountId, "BankTransfer", 250m, new(2026, 10, 15), new(2026, 10, 15), advance.Version), CancellationToken.None);
        Assert.Equal("Issued", issued.State);
        Assert.Equal(250m, issued.IssuedAmount);
        Assert.NotNull(issued.IssuancePaymentId);

        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        var sourceReference = $"expenses-advance:{advance.Id:D}";
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from business_expenses.business_advance_issuance where tenant_id=$1 and legal_entity_id=$2 and advance_id=$3 and state='Completed' and payment_id=$4", fixture.TenantId, fixture.LegalEntityId, advance.Id, issued.IssuancePaymentId.Value));
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and payment_type='ExpensePayment' and direction='Outgoing' and source_type='ExpensesAdvance' and source_reference=$3 and business_state='Completed'", fixture.TenantId, fixture.LegalEntityId, sourceReference));
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_ledger.accounting_fact where tenant_id=$1 and legal_entity_id=$2 and id=$3 and fact_type='PaymentMade' and status='Posted'", fixture.TenantId, fixture.LegalEntityId, issued.IssuancePaymentId!.Value));
        var journal = await ReadPaymentJournalAsync(admin, fixture.TenantId, fixture.LegalEntityId, issued.IssuancePaymentId.Value);
        Assert.Equal(250m, journal.Debit);
        Assert.Equal(250m, journal.Credit);
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_ledger.journal_entry_line l join finance_ledger.journal_entry e on e.tenant_id=l.tenant_id and e.legal_entity_id=l.legal_entity_id and e.id=l.journal_entry_id join finance_ledger.account_mapping_version mv on mv.tenant_id=l.tenant_id and mv.legal_entity_id=l.legal_entity_id and mv.target_account_id=l.account_id join finance_ledger.account_mapping m on m.tenant_id=mv.tenant_id and m.legal_entity_id=mv.legal_entity_id and m.id=mv.mapping_id join finance_ledger.account_role r on r.tenant_id=m.tenant_id and r.legal_entity_id=m.legal_entity_id and r.id=m.role_id where e.tenant_id=$1 and e.legal_entity_id=$2 and e.fact_id=$3 and r.code='EmployeeReceivable'", fixture.TenantId, fixture.LegalEntityId, issued.IssuancePaymentId.Value));
        var retry = await expenses.IssueAdvanceAsync(tenant, fixture.LegalEntityId, advance.Id, fixture.ActorId, fixture.MembershipId,
            new(fixture.PaymentAccountId, "BankTransfer", 250m, new(2026, 10, 15), new(2026, 10, 15), issued.Version), CancellationToken.None);
        Assert.Equal(issued.IssuancePaymentId, retry.IssuancePaymentId);
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and source_type='ExpensesAdvance' and source_reference=$3", fixture.TenantId, fixture.LegalEntityId, sourceReference));
    }

    [Fact]
    public async Task ExpensesFinancialFactsReconcileAcrossLedgerPaymentsAndAdvanceBalance()
    {
        var adminConnectionString = Environment.GetEnvironmentVariable("TODIR_P04_ADMIN_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_P04_ADMIN_CONNECTION for payment integration tests.");
        var runtimeConnectionString = Environment.GetEnvironmentVariable("TODIR_DATABASE_CONNECTION")
            ?? throw new InvalidOperationException("Set TODIR_DATABASE_CONNECTION for payment integration tests.");
        var fixture = await ReadFixtureAsync(adminConnectionString);
        var tenant = new TenantId(fixture.TenantId);
        var employeeActor = await ReadEmployeeActorAsync(adminConnectionString, fixture.TenantId, fixture.LegalEntityId);
        var employeeId = employeeActor.EmployeeId;
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("TODIR_DATABASE_CONNECTION", runtimeConnectionString);
            builder.UseSetting("TODIR_RABBITMQ_HOST", string.Empty);
        });
        var expenses = factory.Services.GetRequiredService<ExpenseRepository>();
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();

        var categoryVersionId = (Guid)(await ScalarAsync(admin, "select v.id from business_expenses.expense_category_version v join business_expenses.expense_category c on c.tenant_id=v.tenant_id and c.id=v.category_id where v.tenant_id=$1 and c.legal_entity_id=$2 and v.status='Published' order by v.effective_from desc limit 1", fixture.TenantId, fixture.LegalEntityId) ?? throw new InvalidOperationException("The canonical Expenses category fixture is unavailable."));
        var approver = await ReadApproverAsync(admin, fixture.TenantId);
        await using (var grant = new NpgsqlCommand("insert into platform.role_permission(tenant_id,role_id,permission_code) select $1,a.role_id,'expenses.approve' from platform.membership_role_assignment a where a.tenant_id=$1 and a.membership_id=$2 on conflict do nothing", admin))
        { grant.Parameters.AddWithValue(fixture.TenantId); grant.Parameters.AddWithValue(approver.MembershipId); await grant.ExecuteNonQueryAsync(); }
        await using (var employeeGrant = new NpgsqlCommand("insert into platform.role_permission(tenant_id,role_id,permission_code) select $1,a.role_id,'expenses.submit' from platform.membership_role_assignment a where a.tenant_id=$1 and a.membership_id=$2 on conflict do nothing", admin))
        { employeeGrant.Parameters.AddWithValue(fixture.TenantId); employeeGrant.Parameters.AddWithValue(employeeActor.MembershipId); await employeeGrant.ExecuteNonQueryAsync(); }

        var expense = await expenses.CreateAsync(tenant, new(fixture.LegalEntityId, employeeId, "Reconciliation expense", new(2026, 11, 9), 125m, "EGP", "CompanyPaid", categoryVersionId, "Customer meeting"), CancellationToken.None);
        await expenses.DeclareMissingReceiptAsync(tenant, fixture.ActorId, fixture.LegalEntityId, expense.Id, new("Receipt unavailable for reconciliation acceptance"), CancellationToken.None);
        var submitted = await expenses.TransitionAsync(tenant, fixture.LegalEntityId, expense.Id, new(expense.Version), "submit", CancellationToken.None);
        await expenses.EvaluatePolicyAsync(tenant, fixture.LegalEntityId, expense.Id, CancellationToken.None);
        var requested = await expenses.RequestApprovalAsync(tenant, employeeActor.IdentityId, employeeActor.MembershipId, fixture.LegalEntityId, expense.Id, "Reconciliation review", CancellationToken.None);
        await expenses.DecideApprovalAsync(tenant, approver.IdentityId, approver.MembershipId, fixture.LegalEntityId, expense.Id, new(requested.RequestId, "Approved", "Reconciliation approved"), CancellationToken.None);
        var approvedExpense = (await expenses.GetAsync(tenant, fixture.LegalEntityId, expense.Id, CancellationToken.None))!;
        var finalized = await expenses.TransitionAsync(tenant, fixture.LegalEntityId, expense.Id, new(approvedExpense.Version), "finalize", CancellationToken.None);
        Assert.Equal("Posted", finalized.AccountingState);

        var card = await expenses.ImportCardTransactionAsync(tenant, fixture.LegalEntityId, new($"reconciliation-card-{Guid.CreateVersion7():N}", "Reconciliation Card", new(2026, 11, 9), 75m), CancellationToken.None);
        await expenses.MarkCardTransactionPersonalAsync(tenant, fixture.LegalEntityId, card.Id, new(employeeId), CancellationToken.None);
        var repayment = (await expenses.ListEmployeeRepaymentsAsync(tenant, fixture.LegalEntityId, employeeId, "Requested", 1, 20, CancellationToken.None)).Items.Single(x => x.SourceReference == $"corporate-card:{card.Id:D}");
        var settledRepayment = await expenses.SettleEmployeeRepaymentAsync(tenant, fixture.LegalEntityId, repayment.Id, fixture.ActorId, fixture.MembershipId, new(fixture.PaymentAccountId, "BankTransfer", 75m, new(2026, 11, 9), new(2026, 11, 9), repayment.Version), CancellationToken.None);
        Assert.Equal("Recorded", settledRepayment.State);

        var advanceEmployeeId = await ReadUnlinkedEmployeeIdAsync(adminConnectionString, fixture.TenantId, fixture.LegalEntityId);
        var advance = await expenses.CreateAdvanceAsync(tenant, new(fixture.LegalEntityId, advanceEmployeeId, "Reconciliation advance", 250m, "EGP", new(2026, 12, 1)), CancellationToken.None);
        var approvedAdvance = await expenses.TransitionAdvanceAsync(tenant, fixture.LegalEntityId, advance.Id, advance.Version, "approve", CancellationToken.None);
        var issuedAdvance = await expenses.IssueAdvanceAsync(tenant, fixture.LegalEntityId, advance.Id, fixture.ActorId, fixture.MembershipId, new(fixture.PaymentAccountId, "BankTransfer", 250m, new(2026, 11, 9), new(2026, 11, 9), approvedAdvance.Version), CancellationToken.None);
        var hr = factory.Services.GetRequiredService<HrRepository>();
        var employment = await ReadEmploymentAsync(adminConnectionString, fixture.TenantId, fixture.LegalEntityId, advanceEmployeeId);
        var terminationDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        await hr.ChangeEmploymentStatusAsync(tenant, fixture.ActorId, fixture.MembershipId, advanceEmployeeId, employment.EmploymentId, fixture.LegalEntityId,
            new(EmploymentStatus.Terminated, terminationDate, "VOLUNTARY_RESIGNATION", employment.Version,
                new("Resignation", terminationDate.AddDays(-1), terminationDate, true, "LastWorkingDay")), Guid.CreateVersion7(), CancellationToken.None);
        var outstandingAfterTermination = await expenses.ListAdvancesAsync(tenant, fixture.LegalEntityId, "Issued", advanceEmployeeId, false, 1, 20, CancellationToken.None);
        var retainedAdvance = Assert.Single(outstandingAfterTermination.Items, item => item.Id == advance.Id);
        Assert.Equal(250m, retainedAdvance.IssuedAmount - retainedAdvance.AppliedAmount - retainedAdvance.ReturnedAmount - retainedAdvance.WrittenOffAmount);
        var settledAdvance = await expenses.ReturnAdvanceAsync(tenant, fixture.LegalEntityId, advance.Id, new(250m, issuedAdvance.Version), CancellationToken.None);
        Assert.Equal("Settled", settledAdvance.State);
        Assert.Equal(0m, settledAdvance.IssuedAmount - settledAdvance.AppliedAmount - settledAdvance.ReturnedAmount - settledAdvance.WrittenOffAmount);

        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_ledger.accounting_fact where tenant_id=$1 and legal_entity_id=$2 and source_module='expenses' and source_reference=$3 and fact_type='ExpenseRecognized' and status='Posted'", fixture.TenantId, fixture.LegalEntityId, expense.Id.ToString("D")));
        Assert.Equal(125m, Convert.ToDecimal(await ScalarAsync(admin, "select (payload_jsonb->>'baseAmount')::numeric from finance_ledger.accounting_fact where tenant_id=$1 and legal_entity_id=$2 and source_module='expenses' and source_reference=$3 and fact_type='ExpenseRecognized' and status='Posted'", fixture.TenantId, fixture.LegalEntityId, expense.Id.ToString("D")), CultureInfo.InvariantCulture));
        Assert.Equal(75m, Convert.ToDecimal(await ScalarAsync(admin, "select amount from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and source_type='ExpensesEmployeeRepayment' and source_reference=$3 and business_state='Completed'", fixture.TenantId, fixture.LegalEntityId, $"expenses-repayment:{repayment.Id:D}"), CultureInfo.InvariantCulture));
        Assert.Equal(250m, Convert.ToDecimal(await ScalarAsync(admin, "select amount from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and source_type='ExpensesAdvance' and source_reference=$3 and business_state='Completed'", fixture.TenantId, fixture.LegalEntityId, $"expenses-advance:{advance.Id:D}"), CultureInfo.InvariantCulture));
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_ledger.accounting_fact where tenant_id=$1 and legal_entity_id=$2 and id=$3 and fact_type='PaymentReceived' and status='Posted'", fixture.TenantId, fixture.LegalEntityId, settledRepayment.PaymentId!));
        Assert.Equal(1L, await CountAsync(admin, "select count(*) from finance_ledger.accounting_fact where tenant_id=$1 and legal_entity_id=$2 and id=$3 and fact_type='PaymentMade' and status='Posted'", fixture.TenantId, fixture.LegalEntityId, issuedAdvance.IssuancePaymentId!));
    }

    private static async Task<Fixture> ReadFixtureAsync(string adminConnectionString)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            select tenant.id, entity.id, party.id, account.id, identity.id, membership.id, target.id,
                   (select usd.id from finance_payments.payment_account usd where usd.tenant_id=tenant.id and usd.legal_entity_id=entity.id and usd.code='BANK-USD-MAIN' and usd.status='Active' limit 1),
                   (select id from platform.tenant where slug='delta-supplies')
            from platform.tenant tenant
            join platform.legal_entity entity on entity.tenant_id=tenant.id and entity.code='MAIN'
            join foundation_directory.party party on party.tenant_id=tenant.id and party.status='Active'
            join foundation_directory.party_role role on role.tenant_id=party.tenant_id and role.party_id=party.id and role.role_code='Customer' and role.status='Active'
            join finance_subledger.party_open_item target on target.tenant_id=tenant.id and target.legal_entity_id=entity.id and target.party_id=party.id and target.direction='Receivable' and target.polarity='Debit' and target.is_open and target.transaction_currency='EGP' and target.open_transaction_amount >= 25
            join finance_payments.payment_account account on account.tenant_id=tenant.id and account.legal_entity_id=entity.id and account.code='BANK-EGP-MAIN' and account.status='Active'
            join platform.identity identity on identity.tenant_id=tenant.id and identity.email='admin@atlas.com' and identity.status='Active'
            join platform.membership membership on membership.tenant_id=tenant.id and membership.identity_id=identity.id and membership.status='Active'
            where tenant.slug='atlas-trading'
              and exists(select 1 from finance_subledger.party_open_item payment_source where payment_source.tenant_id=target.tenant_id and payment_source.legal_entity_id=target.legal_entity_id and payment_source.party_id=target.party_id and payment_source.source_type='Payment' and payment_source.direction='Receivable' and payment_source.polarity='Credit' and payment_source.transaction_currency=target.transaction_currency and payment_source.base_currency=target.base_currency and payment_source.control_account_role=target.control_account_role and payment_source.control_account_reference=target.control_account_reference and payment_source.control_configuration_reference=target.control_configuration_reference)
            order by target.open_transaction_amount desc, party.id
            limit 1
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("The canonical Atlas development payment fixture is unavailable.");
        if (reader.IsDBNull(7)) throw new InvalidOperationException("The canonical Atlas development USD payment account is unavailable.");
        return new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3), reader.GetGuid(4), reader.GetGuid(5), reader.GetGuid(6), reader.GetGuid(7), reader.GetGuid(8));
    }

    private static async Task<(Guid IdentityId, Guid MembershipId)> ReadApproverAsync(NpgsqlConnection connection, Guid tenantId)
    {
        await using var command = new NpgsqlCommand("select i.id,m.id from platform.identity i join platform.membership m on m.tenant_id=i.tenant_id and m.identity_id=i.id where i.tenant_id=$1 and i.email='accountant@atlas.com' and m.status='Active'", connection);
        command.Parameters.AddWithValue(tenantId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("The canonical Expenses approver fixture is unavailable.");
        return (reader.GetGuid(0), reader.GetGuid(1));
    }

    private static async Task<long> CountAsync(NpgsqlConnection connection, string sql, params object[] values)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private static async Task<Guid> ReadEmployeeIdAsync(string adminConnectionString, Guid tenantId, Guid legalEntityId)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("select e.id from business_hr.employee e join business_hr.employment employment on employment.tenant_id=e.tenant_id and employment.employee_id=e.id where e.tenant_id=$1 and employment.legal_entity_id=$2 and e.status='Active' and employment.status in ('Active','Suspended','NoticePeriod') order by employment.employment_number,e.id limit 1", connection);
        command.Parameters.AddWithValue(tenantId);
        command.Parameters.AddWithValue(legalEntityId);
        return (Guid)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("The canonical Atlas HR employee fixture is unavailable."));
    }

    private static async Task<(Guid IdentityId, Guid MembershipId, Guid EmployeeId)> ReadEmployeeActorAsync(string adminConnectionString, Guid tenantId, Guid legalEntityId)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("select i.id,m.id,e.id from platform.identity i join platform.membership m on m.tenant_id=i.tenant_id and m.identity_id=i.id join business_hr.employee e on e.tenant_id=m.tenant_id and e.membership_id=m.id join business_hr.employment employment on employment.tenant_id=e.tenant_id and employment.employee_id=e.id where i.tenant_id=$1 and employment.legal_entity_id=$2 and i.status='Active' and m.status='Active' and e.status='Active' and employment.status in ('Active','Suspended','NoticePeriod') order by employment.employment_number,e.id limit 1", connection);
        command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(legalEntityId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("The canonical Atlas employee actor fixture is unavailable.");
        return (reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2));
    }

    private static async Task<(Guid EmploymentId, long Version)> ReadEmploymentAsync(string adminConnectionString, Guid tenantId, Guid legalEntityId, Guid employeeId)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("select id,version from business_hr.employment where tenant_id=$1 and legal_entity_id=$2 and employee_id=$3 and status in ('Active','Suspended','NoticePeriod') order by start_date desc,id limit 1", connection);
        command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(legalEntityId); command.Parameters.AddWithValue(employeeId);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("The canonical active employment fixture is unavailable.");
        return (reader.GetGuid(0), reader.GetInt64(1));
    }

    private static async Task<Guid> ReadUnlinkedEmployeeIdAsync(string adminConnectionString, Guid tenantId, Guid legalEntityId)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("select e.id from business_hr.employee e join business_hr.employment em on em.tenant_id=e.tenant_id and em.employee_id=e.id where e.tenant_id=$1 and e.membership_id is null and e.status='Active' and em.legal_entity_id=$2 and em.status in ('Active','Suspended','NoticePeriod') order by em.employment_number,e.id limit 1", connection);
        command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(legalEntityId);
        return (Guid)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("The isolated active employee fixture is unavailable."));
    }

    private static async Task<long> ScalarCountAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql, params object[] values)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        foreach (var value in values) command.Parameters.AddWithValue(value);
        return await command.ExecuteScalarAsync();
    }

    private static async Task<(decimal Debit, decimal Credit)> ReadPaymentJournalAsync(NpgsqlConnection connection, Guid tenantId, Guid entityId, Guid paymentId)
    {
        await using var command = new NpgsqlCommand("select coalesce(sum(l.debit),0),coalesce(sum(l.credit),0) from finance_ledger.journal_entry e join finance_ledger.journal_entry_line l on l.tenant_id=e.tenant_id and l.legal_entity_id=e.legal_entity_id and l.journal_entry_id=e.id where e.tenant_id=$1 and e.legal_entity_id=$2 and e.fact_id=$3", connection);
        command.Parameters.AddWithValue(tenantId); command.Parameters.AddWithValue(entityId); command.Parameters.AddWithValue(paymentId);
        await using var reader = await command.ExecuteReaderAsync(); Assert.True(await reader.ReadAsync());
        return (reader.GetDecimal(0), reader.GetDecimal(1));
    }

    private static async Task InstallFailureTriggerAsync(string adminConnectionString, string table, string eventName)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using var function = new NpgsqlCommand("create or replace function finance_payments.test_fail_payment_atomicity() returns trigger language plpgsql as $$ begin raise exception 'payments.atomicity_test_failpoint' using errcode='P0001'; end $$", connection);
        await function.ExecuteNonQueryAsync();
        await using var trigger = new NpgsqlCommand($"create trigger test_payment_atomicity_fail before {eventName} on {table} for each row execute function finance_payments.test_fail_payment_atomicity()", connection);
        await trigger.ExecuteNonQueryAsync();
    }

    private static async Task RemoveFailureTriggerAsync(string adminConnectionString, string table)
    {
        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync();
        await using (var trigger = new NpgsqlCommand($"drop trigger if exists test_payment_atomicity_fail on {table}", connection)) await trigger.ExecuteNonQueryAsync();
        await using var function = new NpgsqlCommand("drop function if exists finance_payments.test_fail_payment_atomicity()", connection);
        await function.ExecuteNonQueryAsync();
    }

    private sealed record Fixture(Guid TenantId, Guid LegalEntityId, Guid PartyId, Guid PaymentAccountId, Guid ActorId, Guid MembershipId,
        Guid TargetOpenItemId, Guid UsdPaymentAccountId, Guid OtherTenantId);
}
