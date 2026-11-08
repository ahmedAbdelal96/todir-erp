using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Core.Services;
using Core.Transactions;
using Modules.Payments.DTOs;
using Modules.Payments.Entities;
using Npgsql;
using Shared.Contracts.FinanceBase;
using Shared.Contracts.Ledger;
using Shared.Contracts.Subledger;
using Shared.Contracts.Payments;
using Shared.Primitives.Identifiers;

namespace Modules.Payments.Repositories;

public sealed class PaymentsRepository(ITransactionCoordinator transactions, ITransactionalSequenceAllocator sequences, NpgsqlDataSource dataSource, TimeProvider clock,
    ITransactionalSubledgerOpenItemCapability subledger, ITransactionalPaymentLedgerPostingCapability ledger,
    ITransactionalInvoiceLedgerPostingCapability ledgerConfiguration, ISubledgerCapabilities subledgerRead)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<PaymentAccount> CreateAccountAsync(TenantId tenant, Guid actor, Guid membership, Guid correlationId, CreatePaymentAccountCommand request, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
            var currencyPolicy = request.CurrencyPolicy.Trim();
            var currency = currencyPolicy == "SingleCurrency" ? PaymentRules.NormalizeCurrency(request.CurrencyCode) : null;
            if (currencyPolicy is not ("SingleCurrency" or "MultiCurrency")) throw new PaymentException("payments.currency_policy_invalid", "Select a supported currency policy.");
            if (request.Type == PaymentAccountType.Cashbox && request.BranchId is null) throw new PaymentException("payments.cashbox_branch_required", "A cashbox must belong to a branch.");
            if (request.Type is PaymentAccountType.Cashbox && request.SecretRef is not null) throw new PaymentException("payments.cashbox_secret_invalid", "Cashboxes cannot use bank or provider secrets.");
            if (request.MaskedIdentifier is { } identifier && identifier.Count(char.IsAsciiDigit) > 4) throw new PaymentException("payments.bank_identifier_must_be_masked", "Store only the final four digits of a bank identifier.");
            if (request.SecretRef is { } secretRef && !secretRef.StartsWith("secret://", StringComparison.Ordinal) && !secretRef.StartsWith("vault://", StringComparison.Ordinal))
                throw new PaymentException("payments.secret_reference_invalid", "Sensitive values must be stored in an approved secret manager and referenced here.");
            await using var command = new NpgsqlCommand("""
                insert into finance_payments.payment_account
                (tenant_id,legal_entity_id,id,branch_id,code,name,account_type,currency_policy,currency_code,status,bank_name,account_holder,masked_identifier,secret_ref,version,created_at,updated_at)
                values($1,$2,$3,$4,$5,$6,$7,$8,$9,'Active',$10,$11,$12,$13,1,$14,$14)
                returning id
                """, context.Connection, context.Transaction);
            Add(command, tenant.Value, request.LegalEntityId, id, Db(request.BranchId), request.Code.Trim(), request.Name.Trim(), request.Type.ToString(), currencyPolicy, Db(currency), Db(Clean(request.BankName)), Db(Clean(request.AccountHolder)), Db(Clean(request.MaskedIdentifier)), Db(Clean(request.SecretRef)), now);
            await command.ExecuteScalarAsync(token);
            var defaultMethod = request.Type switch
            {
                PaymentAccountType.Cashbox => PaymentMethod.Cash,
                PaymentAccountType.ProviderClearing => PaymentMethod.PaymentGateway,
                PaymentAccountType.Wallet => PaymentMethod.Wallet,
                _ => PaymentMethod.BankTransfer
            };
            await using (var method = new NpgsqlCommand("insert into finance_payments.payment_method_config(tenant_id,legal_entity_id,id,payment_account_id,method,branch_id,channel,status,effective_from,created_at) values($1,$2,$3,$4,$5,$6,'BackOffice','Active',$7,$8)", context.Connection, context.Transaction))
            {
                Add(method, tenant.Value, request.LegalEntityId, Guid.CreateVersion7(), id, defaultMethod.ToString(), Db(request.BranchId), DateOnly.MinValue, now);
                await method.ExecuteNonQueryAsync(token);
            }
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new(Guid.CreateVersion7(), tenant, request.LegalEntityId, request.BranchId, "User", actor, membership,
                "payments", "payment_account.created", "payment_account", id, null,
                JsonSerializer.Serialize(new { code = request.Code.Trim(), type = request.Type.ToString(), currencyPolicy, currency }, JsonOptions), null,
                "api", correlationId, null, now, null), token);
            return new PaymentAccount(id, request.LegalEntityId, request.BranchId, request.Code.Trim(), request.Name.Trim(), request.Type,
                currencyPolicy, currency, PaymentAccountStatus.Active, Clean(request.BankName), Clean(request.AccountHolder), Clean(request.MaskedIdentifier), Clean(request.SecretRef), 1);
        }, ct);

    public Task<ProviderRefundOperation> CreateProviderRefundOperationAsync(TenantId tenant, Guid actor, Guid membership, Guid correlationId,
        Guid legalEntityId, Guid originalPaymentId, Guid refundPaymentId, string idempotencyKey, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Trim().Length > 200)
                throw new PaymentException("payments.idempotency_key_required", "A valid retry key is required for this provider refund.");
            var requestHash = Hash(new { legalEntityId, originalPaymentId, refundPaymentId });
            await using (var reserve = new NpgsqlCommand("insert into finance_payments.payment_idempotency(tenant_id,id,scope,idempotency_key,request_hash,created_at) values($1,$2,'create-provider-refund',$3,$4,$5) on conflict(tenant_id,scope,idempotency_key) do nothing", context.Connection, context.Transaction))
            { Add(reserve, tenant.Value, Guid.CreateVersion7(), idempotencyKey.Trim(), requestHash, clock.GetUtcNow()); await reserve.ExecuteNonQueryAsync(token); }
            await using (var key = new NpgsqlCommand("select request_hash,result_id from finance_payments.payment_idempotency where tenant_id=$1 and scope='create-provider-refund' and idempotency_key=$2 for update", context.Connection, context.Transaction))
            {
                Add(key, tenant.Value, idempotencyKey.Trim()); await using var reader = await key.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new PaymentException("payments.idempotency_unavailable", "The provider refund could not be reserved.");
                var priorHash = reader.GetString(0).Trim(); var priorId = reader.IsDBNull(1) ? (Guid?)null : reader.GetGuid(1); await reader.CloseAsync();
                if (priorHash != requestHash) throw new PaymentException("payments.idempotency_conflict", "This provider refund key was already used for different details.");
                if (priorId is { } existingId) return await GetProviderRefundOperationInTransactionAsync(context, tenant, legalEntityId, existingId, token)
                    ?? throw new PaymentException("payments.idempotency_result_missing", "The original provider refund is unavailable.");
            }
            Guid providerConfigId; string providerTransactionId; Guid refundId; decimal amount; string currency;
            await using (var load = new NpgsqlCommand("select config.id,attempt.provider_transaction_id,refund.id,refund.amount,refund.currency_code,config.capabilities,config.status,original.business_state,refund.status,refund.original_payment_id,refund.refund_payment_id from finance_payments.payment_refund refund join finance_payments.payment original on original.tenant_id=refund.tenant_id and original.legal_entity_id=refund.legal_entity_id and original.id=refund.original_payment_id join finance_payments.payment_request request on request.tenant_id=original.tenant_id and request.legal_entity_id=original.legal_entity_id and request.id::text=original.source_reference and original.source_type='PaymentRequest' join finance_payments.provider_config config on config.tenant_id=request.tenant_id and config.legal_entity_id=request.legal_entity_id and config.id=request.provider_config_id and config.provider_code='reference' and config.environment='Sandbox' join lateral(select * from finance_payments.payment_attempt item where item.tenant_id=request.tenant_id and item.legal_entity_id=request.legal_entity_id and item.payment_request_id=request.id order by item.attempt_number desc limit 1) attempt on true where refund.tenant_id=$1 and refund.legal_entity_id=$2 and refund.original_payment_id=$3 and refund.refund_payment_id=$4 and attempt.state='Succeeded' and attempt.provider_transaction_id is not null for update of refund", context.Connection, context.Transaction))
            {
                Add(load, tenant.Value, legalEntityId, originalPaymentId, refundPaymentId); await using var reader = await load.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new PaymentException("payments.provider_refund_unavailable", "This refund is not associated with a completed provider payment.");
                providerConfigId = reader.GetGuid(0); providerTransactionId = reader.GetString(1); refundId = reader.GetGuid(2); amount = reader.GetDecimal(3); currency = reader.GetString(4).Trim();
                var capabilities = reader.GetFieldValue<System.Text.Json.JsonDocument>(5); var configStatus = reader.GetString(6); var paymentState = reader.GetString(7); var refundState = reader.GetString(8);
                if (configStatus != "Active" || paymentState != "Completed" || refundState != "Pending"
                    || !capabilities.RootElement.TryGetProperty("refund", out var refundEnabled) || refundEnabled.ValueKind != System.Text.Json.JsonValueKind.True)
                    throw new PaymentException("payments.provider_refund_unsupported", "The selected provider does not allow a refund for this payment.");
                if (reader.GetGuid(9) != originalPaymentId || reader.GetGuid(10) != refundPaymentId)
                    throw new PaymentException("payments.provider_refund_scope_invalid", "The provider refund does not match the selected payment.");
            }
            var operationId = Guid.CreateVersion7(); var now = clock.GetUtcNow(); var providerKey = idempotencyKey.Trim();
            var operationHash = Hash(new { refundId, originalPaymentId, providerTransactionId, amount, currency });
            await using (var operation = new NpgsqlCommand("insert into finance_payments.provider_operation(tenant_id,legal_entity_id,id,provider_config_id,operation_type,aggregate_type,aggregate_id,idempotency_key,request_hash,state,attempts,created_at,updated_at) values($1,$2,$3,$4,'Refund','Refund',$5,$6,$7,'Pending',0,$8,$8)", context.Connection, context.Transaction))
            { Add(operation, tenant.Value, legalEntityId, operationId, providerConfigId, refundId, providerKey, operationHash, now); await operation.ExecuteNonQueryAsync(token); }
            await using (var idem = new NpgsqlCommand("update finance_payments.payment_idempotency set result_type='ProviderRefund',result_id=$4 where tenant_id=$1 and scope='create-provider-refund' and idempotency_key=$2 and request_hash=$3", context.Connection, context.Transaction))
            { Add(idem, tenant.Value, providerKey, requestHash, operationId); await idem.ExecuteNonQueryAsync(token); }
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new(Guid.CreateVersion7(), tenant, legalEntityId, null,
                "User", actor, membership, "payments", "provider.refund_requested", "payment_refund", refundId, null,
                JsonSerializer.Serialize(new { refundPaymentId, originalPaymentId, amount, currency }, JsonOptions), null, "api", correlationId, null, now, null), token);
            return new ProviderRefundOperation(operationId, refundId, refundPaymentId, originalPaymentId, providerConfigId, providerTransactionId, providerKey, "Pending", amount, currency);
        }, ct);

    public async Task<ProviderRefundOperation?> GetProviderRefundOperationAsync(TenantId tenant, Guid operationId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select operation.id,refund.id,refund.refund_payment_id,refund.original_payment_id,operation.provider_config_id,attempt.provider_transaction_id,operation.idempotency_key,operation.state,refund.amount,refund.currency_code from finance_payments.provider_operation operation join finance_payments.payment_refund refund on refund.tenant_id=operation.tenant_id and refund.legal_entity_id=operation.legal_entity_id and refund.id=operation.aggregate_id join finance_payments.payment original on original.tenant_id=refund.tenant_id and original.legal_entity_id=refund.legal_entity_id and original.id=refund.original_payment_id join finance_payments.payment_request request on request.tenant_id=original.tenant_id and request.legal_entity_id=original.legal_entity_id and request.id::text=original.source_reference join lateral(select * from finance_payments.payment_attempt item where item.tenant_id=request.tenant_id and item.legal_entity_id=request.legal_entity_id and item.payment_request_id=request.id order by item.attempt_number desc limit 1) attempt on true where operation.tenant_id=$1 and operation.id=$2 and operation.operation_type='Refund' and operation.aggregate_type='Refund'", connection);
        Add(command, tenant.Value, operationId); await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3), reader.GetGuid(4), reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.GetDecimal(8), reader.GetString(9).Trim()) : null;
    }

    public async Task<Guid?> GetProviderRefundOperationIdAsync(TenantId tenant, Guid legalEntityId, Guid refundPaymentId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select operation.id from finance_payments.provider_operation operation join finance_payments.payment_refund refund on refund.tenant_id=operation.tenant_id and refund.legal_entity_id=operation.legal_entity_id and refund.id=operation.aggregate_id where operation.tenant_id=$1 and operation.legal_entity_id=$2 and refund.refund_payment_id=$3 and operation.operation_type='Refund' and operation.aggregate_type='Refund' order by operation.created_at desc limit 1", connection);
        Add(command, tenant.Value, legalEntityId, refundPaymentId); var value = await command.ExecuteScalarAsync(ct); return value is Guid id ? id : null;
    }

    public Task RecordProviderRefundResultAsync(TenantId tenant, ProviderRefundOperation operation, ReferenceProviderRefundResult result,
        CancellationToken ct) => transactions.ExecuteAsync(tenant, async (context, token) =>
    {
        if (result.Status is not ("Succeeded" or "Failed" or "Unknown") || result.Status == "Succeeded" && string.IsNullOrWhiteSpace(result.RefundId))
            throw new PaymentException("payments.provider_refund_result_invalid", "The provider returned an unsupported refund state.");
        var state = result.Status == "Succeeded" ? "Succeeded" : result.Status == "Failed" ? "Failed" : "Unknown";
        await using var command = new NpgsqlCommand("update finance_payments.provider_operation set state=$3,attempts=attempts+1,last_error_code=case when $3='Unknown' then 'ProviderStatusUnknown' when $3='Failed' then 'ProviderRefundFailed' else null end,updated_at=$4 where tenant_id=$1 and id=$2 and operation_type='Refund' and state in ('Pending','InProgress','Unknown','Succeeded','Failed')", context.Connection, context.Transaction);
        Add(command, tenant.Value, operation.OperationId, state, result.ConfirmedAt);
        if (await command.ExecuteNonQueryAsync(token) != 1) throw new PaymentException("payments.provider_refund_state_invalid", "The provider refund is no longer eligible for reconciliation.");
        if (state == "Succeeded")
        {
            await using var refund = new NpgsqlCommand("update finance_payments.payment_refund item set provider_refund_id=$3 from finance_payments.provider_operation operation where item.tenant_id=$1 and operation.tenant_id=$1 and operation.id=$2 and operation.operation_type='Refund' and operation.aggregate_type='Refund' and item.legal_entity_id=operation.legal_entity_id and item.id=operation.aggregate_id and item.status='Pending' and (item.provider_refund_id is null or item.provider_refund_id=$3)", context.Connection, context.Transaction);
            Add(refund, tenant.Value, operation.OperationId, result.RefundId);
            if (await refund.ExecuteNonQueryAsync(token) != 1) throw new PaymentException("payments.provider_refund_state_invalid", "The provider refund reference could not be recorded.");
        }
        else if (state == "Failed")
        {
            await using var failRefund = new NpgsqlCommand("update finance_payments.payment_refund refund set status='Failed' from finance_payments.provider_operation operation where refund.tenant_id=$1 and operation.tenant_id=$1 and operation.id=$2 and operation.operation_type='Refund' and refund.legal_entity_id=operation.legal_entity_id and refund.id=operation.aggregate_id and refund.status='Pending'", context.Connection, context.Transaction);
            Add(failRefund, tenant.Value, operation.OperationId); await failRefund.ExecuteNonQueryAsync(token);
            await using var failPayment = new NpgsqlCommand("update finance_payments.payment payment set business_state='Failed',row_version=row_version+1,updated_at=$3 from finance_payments.payment_refund refund join finance_payments.provider_operation operation on operation.tenant_id=refund.tenant_id and operation.legal_entity_id=refund.legal_entity_id and operation.aggregate_id=refund.id where payment.tenant_id=$1 and refund.tenant_id=$1 and operation.id=$2 and payment.id=refund.refund_payment_id and payment.business_state='Draft'", context.Connection, context.Transaction);
            Add(failPayment, tenant.Value, operation.OperationId, result.ConfirmedAt); await failPayment.ExecuteNonQueryAsync(token);
            await using var projection = new NpgsqlCommand("update finance_payments.payment_list_projection set business_state='Failed',updated_at=$3 where tenant_id=$1 and payment_id=(select refund.refund_payment_id from finance_payments.payment_refund refund join finance_payments.provider_operation operation on operation.tenant_id=refund.tenant_id and operation.legal_entity_id=refund.legal_entity_id and operation.aggregate_id=refund.id where operation.tenant_id=$1 and operation.id=$2)", context.Connection, context.Transaction);
            Add(projection, tenant.Value, operation.OperationId, result.ConfirmedAt); await projection.ExecuteNonQueryAsync(token);
        }
        return true;
    }, ct);

    private static async Task<ProviderRefundOperation?> GetProviderRefundOperationInTransactionAsync(ITransactionContext context, TenantId tenant,
        Guid legalEntityId, Guid operationId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select operation.id,refund.id,refund.refund_payment_id,refund.original_payment_id,operation.provider_config_id,attempt.provider_transaction_id,operation.idempotency_key,operation.state,refund.amount,refund.currency_code from finance_payments.provider_operation operation join finance_payments.payment_refund refund on refund.tenant_id=operation.tenant_id and refund.legal_entity_id=operation.legal_entity_id and refund.id=operation.aggregate_id join finance_payments.payment original on original.tenant_id=refund.tenant_id and original.legal_entity_id=refund.legal_entity_id and original.id=refund.original_payment_id join finance_payments.payment_request request on request.tenant_id=original.tenant_id and request.legal_entity_id=original.legal_entity_id and request.id::text=original.source_reference join lateral(select * from finance_payments.payment_attempt item where item.tenant_id=request.tenant_id and item.legal_entity_id=request.legal_entity_id and item.payment_request_id=request.id order by item.attempt_number desc limit 1) attempt on true where operation.tenant_id=$1 and operation.legal_entity_id=$2 and operation.id=$3 and operation.operation_type='Refund'", context.Connection, context.Transaction);
        Add(command, tenant.Value, legalEntityId, operationId); await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3), reader.GetGuid(4), reader.GetString(5), reader.GetString(6), reader.GetString(7), reader.GetDecimal(8), reader.GetString(9).Trim()) : null;
    }

    public async Task<IReadOnlyList<PaymentAccount>> ListAccountsAsync(TenantId tenant, Guid legalEntityId, Guid? branchId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        var accountSql = "select id,legal_entity_id,branch_id,code,name,account_type,currency_policy,currency_code,status,bank_name,account_holder,masked_identifier,secret_ref,version from finance_payments.payment_account where tenant_id=$1 and legal_entity_id=$2";
        if (branchId is not null) accountSql += " and branch_id=$3";
        accountSql += " order by code,id";
        await using var command = new NpgsqlCommand(accountSql, connection);
        Add(command, tenant.Value, legalEntityId); if (branchId is { } branch) Add(command, branch);
        var result = new List<PaymentAccount>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(new(reader.GetGuid(0), reader.GetGuid(1), GuidValue(reader, 2), reader.GetString(3).Trim(), reader.GetString(4), Enum.Parse<PaymentAccountType>(reader.GetString(5)), reader.GetString(6), StringValue(reader, 7), Enum.Parse<PaymentAccountStatus>(reader.GetString(8)), StringValue(reader, 9), StringValue(reader, 10), StringValue(reader, 11), null, reader.GetInt64(13)));
        return result;
    }

    public Task UpdateAccountStatusAsync(TenantId tenant, Guid actor, Guid membership, Guid correlationId, Guid legalEntityId, Guid accountId,
        Guid? branchId, PaymentAccountStatus status, CancellationToken ct) => transactions.ExecuteAsync<bool>(tenant, async (context, token) =>
    {
        var now = clock.GetUtcNow();
        var branchClause = branchId is null ? string.Empty : " and branch_id=$6";
        await using var command = new NpgsqlCommand($"update finance_payments.payment_account set status=$4,version=version+1,updated_at=$5 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and status <> 'Archived'{branchClause}", context.Connection, context.Transaction);
        Add(command, tenant.Value, legalEntityId, accountId, status.ToString(), now); if (branchId is { } branch) Add(command, branch);
        if (await command.ExecuteNonQueryAsync(token) != 1)
        {
            var existsClause = branchId is null ? string.Empty : " and branch_id=$4";
            await using var exists = new NpgsqlCommand($"select exists(select 1 from finance_payments.payment_account where tenant_id=$1 and legal_entity_id=$2 and id=$3{existsClause})", context.Connection, context.Transaction);
            Add(exists, tenant.Value, legalEntityId, accountId); if (branchId is { } scopeBranch) Add(exists, scopeBranch);
            if ((bool?)await exists.ExecuteScalarAsync(token) != true) throw new PaymentException("payments.account_not_found", "The payment account was not found.");
            throw new PaymentException("payments.account_archived", "An archived payment account cannot be reactivated.");
        }
        await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new(Guid.CreateVersion7(), tenant, legalEntityId, branchId, "User", actor, membership,
            "payments", "payment_account.status_changed", "payment_account", accountId, null,
            JsonSerializer.Serialize(new { status = status.ToString() }, JsonOptions), null, "api", correlationId, null, now, null), token);
        return true;
    }, ct);

    public Task<PaymentRecord> CreateDraftAsync(TenantId tenant, Guid actor, Guid membership, Guid correlationId, CreatePaymentCommand request, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            PaymentRules.Validate(request);
            var payloadHash = Hash(request);
            await using (var reserve = new NpgsqlCommand("insert into finance_payments.payment_idempotency(tenant_id,id,scope,idempotency_key,request_hash,created_at) values($1,$2,'create-payment',$3,$4,$5) on conflict(tenant_id,scope,idempotency_key) do nothing", context.Connection, context.Transaction))
            { Add(reserve, tenant.Value, Guid.CreateVersion7(), request.IdempotencyKey.Trim(), payloadHash, clock.GetUtcNow()); await reserve.ExecuteNonQueryAsync(token); }
            await using (var lookup = new NpgsqlCommand("select request_hash,result_id from finance_payments.payment_idempotency where tenant_id=$1 and scope='create-payment' and idempotency_key=$2 for update", context.Connection, context.Transaction))
            {
                Add(lookup, tenant.Value, request.IdempotencyKey.Trim()); await using var reader = await lookup.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new PaymentException("payments.idempotency_unavailable", "The payment request could not be reserved.");
                var priorHash = reader.GetString(0).Trim(); var priorId = reader.IsDBNull(1) ? (Guid?)null : reader.GetGuid(1); await reader.CloseAsync();
                if (!string.Equals(priorHash, payloadHash, StringComparison.Ordinal)) throw new PaymentException("payments.idempotency_conflict", "This request key was already used for different payment details.");
                if (priorId is { } existingId) return await GetPaymentInTransactionAsync(context, tenant, request.LegalEntityId, existingId, token)
                    ?? throw new PaymentException("payments.idempotency_result_missing", "The original payment request result is unavailable.");
            }

            await using (var account = new NpgsqlCommand("select account_type,status,currency_policy,currency_code,branch_id from finance_payments.payment_account where tenant_id=$1 and legal_entity_id=$2 and id=$3 for share", context.Connection, context.Transaction))
            {
                Add(account, tenant.Value, request.LegalEntityId, request.PaymentAccountId); await using var reader = await account.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new PaymentException("payments.account_not_found", "The selected payment account is unavailable.");
                var accountType = reader.GetString(0); var status = reader.GetString(1); var currencyPolicy = reader.GetString(2); var currency = StringValue(reader, 3); var accountBranch = GuidValue(reader, 4); await reader.CloseAsync();
                if (status != "Active") throw new PaymentException("payments.account_inactive", "The selected payment account is suspended or archived.");
                if (currencyPolicy == "SingleCurrency" && !string.Equals(currency, PaymentRules.NormalizeCurrency(request.CurrencyCode), StringComparison.Ordinal)) throw new PaymentException("payments.account_currency_mismatch", "The payment currency does not match the selected account.");
                if (accountBranch is { } branchId && branchId != request.BranchId) throw new PaymentException("payments.cashbox_scope_invalid", "The selected cashbox is not available for this branch.");
                if (accountType == "Cashbox" && request.Method != PaymentMethod.Cash) throw new PaymentException("payments.cashbox_method_invalid", "Cashboxes accept cash movements only.");
                if (!AccountAcceptsMethod(accountType, request.Method)) throw new PaymentException("payments.account_method_invalid", "The selected payment account cannot be used for this payment method.");
            }
            await using (var method = new NpgsqlCommand("select exists(select 1 from finance_payments.payment_method_config where tenant_id=$1 and legal_entity_id=$2 and method=$3 and status='Active' and effective_from<=$4 and (effective_to is null or effective_to >= $4) and (payment_account_id is null or payment_account_id=$5) and (branch_id is null or branch_id is not distinct from $6))", context.Connection, context.Transaction))
            {
                Add(method, tenant.Value, request.LegalEntityId, request.Method.ToString(), request.PaymentDate, request.PaymentAccountId, Db(request.BranchId));
                if ((bool?)await method.ExecuteScalarAsync(token) != true) throw new PaymentException("payments.method_not_enabled", "This payment method is not enabled for the selected account, branch, and date.");
            }

            var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
            // Payment numbers are unique per legal entity, not per branch; keep the allocator scope aligned with the database key.
            var number = await sequences.AllocateAsync(context, new("payments.payment", tenant, request.LegalEntityId, null, null, request.PaymentDate.ToString("yyyy", System.Globalization.CultureInfo.InvariantCulture), "PAY-", 6, 1), token);
            await using (var insert = new NpgsqlCommand("""
                insert into finance_payments.payment(tenant_id,legal_entity_id,id,payment_number,branch_id,direction,payment_type,party_id,amount,currency_code,method,payment_account_id,payment_date,posting_date,value_date,bank_reference,normalized_reference,customer_reference,supplier_reference,source_type,source_reference,provider_code,provider_transaction_id,business_state,row_version,created_at,updated_at)
                values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,$18,$19,$20,$21,$22,$23,'Draft',1,$24,$24)
                """, context.Connection, context.Transaction))
            {
                var amount = PaymentRules.ParseAmount(request.Amount); var currency = PaymentRules.NormalizeCurrency(request.CurrencyCode); var bankReference = Clean(request.BankReference);
                Add(insert, tenant.Value, request.LegalEntityId, id, number, Db(request.BranchId), request.Direction.ToString(), request.Type.ToString(), Db(request.PartyId), amount, currency, request.Method.ToString(), request.PaymentAccountId, request.PaymentDate, request.PostingDate, Db(request.ValueDate), Db(bankReference), Db(NormalizeReference(bankReference)), Db(Clean(request.CustomerReference)), Db(Clean(request.SupplierReference)), Db(Clean(request.SourceType)), Db(Clean(request.SourceReference)), Db(Clean(request.ProviderCode)), Db(Clean(request.ProviderTransactionId)), now);
                await insert.ExecuteNonQueryAsync(token);
            }
            await using (var updateKey = new NpgsqlCommand("update finance_payments.payment_idempotency set result_type='Payment',result_id=$4 where tenant_id=$1 and scope='create-payment' and idempotency_key=$2 and request_hash=$3", context.Connection, context.Transaction))
            { Add(updateKey, tenant.Value, request.IdempotencyKey.Trim(), payloadHash, id); if (await updateKey.ExecuteNonQueryAsync(token) != 1) throw new PaymentException("payments.idempotency_result_missing", "The payment request result could not be recorded."); }
            await UpsertProjectionAsync(context, tenant, request.LegalEntityId, id, number, request.Amount, request.CurrencyCode, request.Direction.ToString(), request.Method.ToString(), "Draft", request.BankReference, now, token);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new(Guid.CreateVersion7(), tenant, request.LegalEntityId, request.BranchId,
                "User", actor, membership, "payments", "payment.created", "payment", id, null,
                JsonSerializer.Serialize(new { number, amount = PaymentRules.ParseAmount(request.Amount), currencyCode = PaymentRules.NormalizeCurrency(request.CurrencyCode), request.Direction, request.Type }, JsonOptions),
                null, "api", correlationId, null, now, null), token);
            context.AddOutbox(new(Guid.CreateVersion7(), "PaymentCreatedV1", JsonSerializer.Serialize(new { paymentId = id, legalEntityId = request.LegalEntityId, number }, JsonOptions), tenant, now, correlationId, id, "payments"));
            return new PaymentRecord(id, request.LegalEntityId, number, request.BranchId, request.Direction, request.Type, request.PartyId,
                PaymentRules.ParseAmount(request.Amount), PaymentRules.NormalizeCurrency(request.CurrencyCode), request.Method, request.PaymentAccountId,
                request.PaymentDate, request.PostingDate, request.ValueDate, Clean(request.BankReference), Clean(request.CustomerReference), Clean(request.SupplierReference), Clean(request.SourceType), Clean(request.SourceReference), PaymentBusinessState.Draft, null, 1);
        }, ct);

    public Task<IReadOnlyList<Guid>> PreparePayrollPaymentsAsync(TenantId tenant, Guid actor, Guid membership, Guid correlationId,
        Guid legalEntityId, Guid runId, IReadOnlyList<PayrollDisbursementInstruction> instructions, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var result = new List<Guid>(instructions.Count);
            foreach (var instruction in instructions)
            {
                if (instruction.Amount <= 0m) continue;
                var currency = PaymentRules.NormalizeCurrency(instruction.CurrencyCode);
                Guid accountId;
                await using (var account = new NpgsqlCommand("select a.id from finance_payments.payment_account a join finance_payments.payment_method_config m on m.tenant_id=a.tenant_id and m.legal_entity_id=a.legal_entity_id and m.payment_account_id=a.id where a.tenant_id=$1 and a.legal_entity_id=$2 and a.account_type='Bank' and a.status='Active' and a.currency_policy='SingleCurrency' and a.currency_code=$3 and m.method='BankTransfer' and m.status='Active' and m.effective_from<=current_date and (m.effective_to is null or m.effective_to>=current_date) order by a.id limit 1", context.Connection, context.Transaction))
                {
                    Add(account, tenant.Value, legalEntityId, currency);
                    accountId = (Guid?)await account.ExecuteScalarAsync(token) ?? throw new PaymentException("payments.payroll_account_unavailable", "No active payroll bank account is configured for this currency.");
                }

                var sourceReference = $"{runId:D}:{instruction.InstructionId:D}";
                await using (var existing = new NpgsqlCommand("select id from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and source_type='Payroll' and source_reference=$3", context.Connection, context.Transaction))
                {
                    Add(existing, tenant.Value, legalEntityId, sourceReference);
                    var existingId = await existing.ExecuteScalarAsync(token);
                    if (existingId is Guid already) { result.Add(already); continue; }
                }

                var paymentId = Guid.CreateVersion7();
                var now = clock.GetUtcNow();
                var paymentDate = DateOnly.FromDateTime(now.UtcDateTime);
                var number = await sequences.AllocateAsync(context, new("payments.payment", tenant, legalEntityId, null, null,
                    paymentDate.ToString("yyyy", System.Globalization.CultureInfo.InvariantCulture), "PAY-", 6, 1), token);
                await using (var insert = new NpgsqlCommand("insert into finance_payments.payment(tenant_id,legal_entity_id,id,payment_number,branch_id,direction,payment_type,party_id,amount,currency_code,method,payment_account_id,payment_date,posting_date,value_date,source_type,source_reference,business_state,row_version,created_at,updated_at) values($1,$2,$3,$4,null,'Outgoing','ExpensePayment',null,$5,$6,'BankTransfer',$7,$8,$8,null,'Payroll',$9,'Draft',1,$10,$10)", context.Connection, context.Transaction))
                {
                    Add(insert, tenant.Value, legalEntityId, paymentId, number, instruction.Amount, currency, accountId, paymentDate, sourceReference, now);
                    await insert.ExecuteNonQueryAsync(token);
                }
                await UpsertProjectionAsync(context, tenant, legalEntityId, paymentId, number, instruction.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture), currency, "Outgoing", "BankTransfer", "Draft", null, now, token);
                await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new(Guid.CreateVersion7(), tenant, legalEntityId, null,
                    "Payroll", actor, membership, "payments", "payment.created", "payment", paymentId, null,
                    JsonSerializer.Serialize(new { number, amount = instruction.Amount, currencyCode = currency, runId, instructionId = instruction.InstructionId }, JsonOptions),
                    null, "worker", correlationId, null, now, null), token);
                context.AddOutbox(new(Guid.CreateVersion7(), "PaymentCreatedV1", JsonSerializer.Serialize(new
                {
                    paymentId,
                    legalEntityId,
                    number,
                    sourceType = "Payroll",
                    runId,
                    instructionId = instruction.InstructionId
                }, JsonOptions), tenant, now, correlationId, paymentId, "payments"));
                result.Add(paymentId);
            }
            return (IReadOnlyList<Guid>)result;
        }, ct);

    public Task<InternalTransferResult> CreateInternalTransferAsync(TenantId tenant, Guid actor, Guid membership, Guid correlationId,
        CreateInternalTransferRequest request, string idempotencyKey, FinancialCalculationContextSnapshotContract sourceContext,
        FinancialCalculationContextSnapshotContract destinationContext, decimal sourceAmount, decimal destinationAmount,
        decimal sourceBaseAmount, decimal destinationBaseAmount, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var amount = PaymentRules.ParseAmount(request.Amount);
            var sourceCurrency = PaymentRules.NormalizeCurrency(request.CurrencyCode);
            var destinationCurrency = PaymentRules.NormalizeCurrency(request.DestinationCurrencyCode ?? sourceCurrency);
            if (request.LegalEntityId == Guid.Empty || request.SourcePaymentAccountId == Guid.Empty || request.DestinationPaymentAccountId == Guid.Empty
                || request.SourcePaymentAccountId == request.DestinationPaymentAccountId || string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Trim().Length > 200)
                throw new PaymentException("payments.transfer_invalid", "Transfer details are incomplete.");
            if (sourceAmount != amount || sourceContext.TransactionCurrencyCode != sourceCurrency || destinationContext.TransactionCurrencyCode != destinationCurrency
                || sourceContext.BaseCurrencyCode != destinationContext.BaseCurrencyCode)
                throw new PaymentException("payments.currency_context_changed", "The approved currency context no longer matches this transfer.");

            var requestHash = Hash(new { request.LegalEntityId, request.SourcePaymentAccountId, request.DestinationPaymentAccountId, amount, sourceCurrency, destinationCurrency, request.PaymentDate, request.PostingDate, request.ValueDate, reference = Clean(request.Reference) });
            await using (var reserve = new NpgsqlCommand("insert into finance_payments.payment_idempotency(tenant_id,id,scope,idempotency_key,request_hash,created_at) values($1,$2,'create-internal-transfer',$3,$4,$5) on conflict(tenant_id,scope,idempotency_key) do nothing", context.Connection, context.Transaction))
            { Add(reserve, tenant.Value, Guid.CreateVersion7(), idempotencyKey.Trim(), requestHash, clock.GetUtcNow()); await reserve.ExecuteNonQueryAsync(token); }
            await using (var lookup = new NpgsqlCommand("select request_hash,result_id from finance_payments.payment_idempotency where tenant_id=$1 and scope='create-internal-transfer' and idempotency_key=$2 for update", context.Connection, context.Transaction))
            {
                Add(lookup, tenant.Value, idempotencyKey.Trim()); await using var reader = await lookup.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new PaymentException("payments.idempotency_unavailable", "The transfer request could not be reserved.");
                var priorHash = reader.GetString(0).Trim(); var priorId = reader.IsDBNull(1) ? (Guid?)null : reader.GetGuid(1); await reader.CloseAsync();
                if (priorHash != requestHash) throw new PaymentException("payments.idempotency_conflict", "This transfer key was already used for different details.");
                if (priorId is { } existingId) return await GetTransferInTransactionAsync(context, tenant, request.LegalEntityId, existingId, token)
                    ?? throw new PaymentException("payments.idempotency_result_missing", "The original transfer result is unavailable.");
            }

            var accounts = new Dictionary<Guid, TransferAccount>();
            foreach (var accountId in new[] { request.SourcePaymentAccountId, request.DestinationPaymentAccountId })
            {
                await using var account = new NpgsqlCommand("select id,branch_id,code,account_type,status,currency_policy,currency_code from finance_payments.payment_account where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction);
                Add(account, tenant.Value, request.LegalEntityId, accountId); await using var reader = await account.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new PaymentException("payments.account_not_found", "A selected payment account is unavailable.");
                var selected = new TransferAccount(reader.GetGuid(0), GuidValue(reader, 1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), StringValue(reader, 6));
                await reader.CloseAsync();
                if (selected.Status != "Active") throw new PaymentException("payments.account_inactive", "Suspended or archived accounts cannot take part in a transfer.");
                if (selected.Type is not ("Bank" or "Cashbox" or "Wallet" or "InternalClearing")) throw new PaymentException("payments.transfer_account_invalid", "Select an operational bank, cashbox, wallet, or internal clearing account.");
                var accountCurrency = accountId == request.SourcePaymentAccountId ? sourceCurrency : destinationCurrency;
                if (selected.Policy == "SingleCurrency" && selected.Currency != accountCurrency) throw new PaymentException("payments.account_currency_mismatch", "The transfer currency is not supported by a selected payment account.");
                accounts.Add(accountId, selected);
            }
            var source = accounts[request.SourcePaymentAccountId]; var destination = accounts[request.DestinationPaymentAccountId];
            var now = clock.GetUtcNow();
            var baseCurrency = sourceContext.BaseCurrencyCode;
            if (sourceBaseAmount <= 0m || destinationBaseAmount <= 0m || destinationAmount <= 0m)
                throw new PaymentException("payments.fx_amount_invalid", "The approved exchange-rate snapshot produced an invalid transfer amount.");
            var transferId = Guid.CreateVersion7(); var sourcePaymentId = Guid.CreateVersion7(); var destinationPaymentId = Guid.CreateVersion7();
            var sourceNumber = await sequences.AllocateAsync(context, new("payments.payment", tenant, request.LegalEntityId, null, null, request.PaymentDate.ToString("yyyy", System.Globalization.CultureInfo.InvariantCulture), "PAY-", 6, 1), token);
            var destinationNumber = await sequences.AllocateAsync(context, new("payments.payment", tenant, request.LegalEntityId, null, null, request.PaymentDate.ToString("yyyy", System.Globalization.CultureInfo.InvariantCulture), "PAY-", 6, 1), token);
            var reference = Clean(request.Reference);
            var sourceLeg = new PaymentRecord(sourcePaymentId, request.LegalEntityId, sourceNumber, source.BranchId, PaymentDirection.InternalTransfer, PaymentType.TransferLeg, null,
                amount, sourceCurrency, TransferMethod(source.Type), source.Id, request.PaymentDate, request.PostingDate, request.ValueDate, reference, null, null, "InternalTransfer", transferId.ToString("D"), PaymentBusinessState.Completed, now, 1);
            var destinationLeg = new PaymentRecord(destinationPaymentId, request.LegalEntityId, destinationNumber, destination.BranchId, PaymentDirection.InternalTransfer, PaymentType.TransferLeg, null,
                destinationAmount, destinationCurrency, TransferMethod(destination.Type), destination.Id, request.PaymentDate, request.PostingDate, request.ValueDate, reference, null, null, "InternalTransfer", transferId.ToString("D"), PaymentBusinessState.Completed, now, 1);
            await InsertTransferLegAsync(context, tenant, sourceLeg, actor, transferId, now, token);
            await InsertTransferLegAsync(context, tenant, destinationLeg, actor, transferId, now, token);
            var fxSnapshot = JsonSerializer.Serialize(new
            {
                source = sourceContext.ExchangeRate,
                destination = destinationContext.ExchangeRate,
                sourceAmount = amount,
                sourceCurrency,
                sourceBaseAmount,
                destinationAmount,
                destinationCurrency,
                destinationBaseAmount,
                baseCurrency
            }, JsonOptions);
            await using (var transfer = new NpgsqlCommand("insert into finance_payments.internal_transfer(tenant_id,legal_entity_id,id,source_payment_id,destination_payment_id,source_account_id,destination_account_id,source_amount,source_currency,destination_amount,destination_currency,fx_rate,fx_snapshot,fee_amount,status,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13::jsonb,0,'Completed',$14)", context.Connection, context.Transaction))
            {
                object? fxRate = sourceCurrency == destinationCurrency ? null : decimal.Round(destinationAmount / amount, 12, MidpointRounding.AwayFromZero);
                Add(transfer, tenant.Value, request.LegalEntityId, transferId, sourcePaymentId, destinationPaymentId, source.Id, destination.Id,
                    amount, sourceCurrency, destinationAmount, destinationCurrency, fxRate, Db(fxSnapshot), now);
                await transfer.ExecuteNonQueryAsync(token);
            }
            await InsertTransferRelationshipAsync(context, tenant, request.LegalEntityId, sourcePaymentId, destinationPaymentId, "TransferDestination", amount, now, token);
            await InsertTransferRelationshipAsync(context, tenant, request.LegalEntityId, destinationPaymentId, sourcePaymentId, "TransferSource", amount, now, token);
            var fact = new AccountingFactEnvelope<IAccountingFactPayload>(transferId, tenant, new(request.LegalEntityId), AccountingFactTypes.InternalTransferCompleted, 1,
                "payments", transferId.ToString("D"), $"internal-transfer:{transferId:D}", request.PaymentDate, request.PostingDate,
                new InternalTransferFactPayload(amount, sourceCurrency, sourceBaseAmount, destinationAmount, destinationCurrency, destinationBaseAmount,
                    baseCurrency, source.Code, destination.Code, RateReference(sourceContext, sourceCurrency), RateReference(destinationContext, destinationCurrency),
                    sourceContext.ExchangeRate is null ? null : JsonSerializer.Serialize(sourceContext.ExchangeRate, JsonOptions),
                    destinationContext.ExchangeRate is null ? null : JsonSerializer.Serialize(destinationContext.ExchangeRate, JsonOptions)), correlationId, actor);
            await ledger.PostInternalTransferAsync(context, fact, (InternalTransferFactPayload)fact.Payload, token);
            await using (var completeKey = new NpgsqlCommand("update finance_payments.payment_idempotency set result_type='InternalTransfer',result_id=$4 where tenant_id=$1 and scope='create-internal-transfer' and idempotency_key=$2 and request_hash=$3", context.Connection, context.Transaction))
            { Add(completeKey, tenant.Value, idempotencyKey.Trim(), requestHash, transferId); if (await completeKey.ExecuteNonQueryAsync(token) != 1) throw new PaymentException("payments.idempotency_result_missing", "The transfer result could not be recorded."); }
            await UpsertProjectionAsync(context, tenant, request.LegalEntityId, sourceLeg.Id, sourceLeg.Number, amount.ToString(System.Globalization.CultureInfo.InvariantCulture), sourceCurrency, "InternalTransfer", sourceLeg.Method.ToString(), "Completed", reference, now, token);
            await UpsertProjectionAsync(context, tenant, request.LegalEntityId, destinationLeg.Id, destinationLeg.Number, destinationAmount.ToString(System.Globalization.CultureInfo.InvariantCulture), destinationCurrency, "InternalTransfer", destinationLeg.Method.ToString(), "Completed", reference, now, token);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new(Guid.CreateVersion7(), tenant, request.LegalEntityId, null, "User", actor, membership,
                "payments", "payment.internal_transfer_completed", "internal_transfer", transferId, null,
                JsonSerializer.Serialize(new { sourcePaymentId, destinationPaymentId, sourceAmount = amount, sourceCurrency, destinationAmount, destinationCurrency, sourceAccountId = source.Id, destinationAccountId = destination.Id }, JsonOptions),
                null, "api", correlationId, null, now, null), token);
            context.AddOutbox(new(Guid.CreateVersion7(), "InternalTransferCompletedV1", JsonSerializer.Serialize(new { transferId, sourcePaymentId, destinationPaymentId, sourceNumber, destinationNumber, sourceAmount = amount, sourceCurrency, destinationAmount, destinationCurrency }, JsonOptions), tenant, now, correlationId, transferId, "payments"));
            var evidence = sourceCurrency == destinationCurrency ? null : fxSnapshot;
            return new InternalTransferResult(transferId, sourceLeg, destinationLeg, amount, sourceCurrency, destinationAmount, destinationCurrency, baseCurrency, evidence, now);
        }, ct);

    private static async Task InsertTransferLegAsync(ITransactionContext context, TenantId tenant, PaymentRecord payment, Guid actor, Guid transferId, DateTimeOffset now, CancellationToken ct)
    {
        await using var insert = new NpgsqlCommand("insert into finance_payments.payment(tenant_id,legal_entity_id,id,payment_number,branch_id,direction,payment_type,party_id,amount,currency_code,method,payment_account_id,payment_date,posting_date,value_date,bank_reference,normalized_reference,source_type,source_reference,business_state,completed_at,completed_by,accounting_fact_id,row_version,created_at,updated_at) values($1,$2,$3,$4,$5,'InternalTransfer','TransferLeg',null,$6,$7,$8,$9,$10,$11,$12,$13,$14,'InternalTransfer',$15,'Completed',$16,$17,$18,1,$16,$16)", context.Connection, context.Transaction);
        Add(insert, tenant.Value, payment.LegalEntityId, payment.Id, payment.Number, Db(payment.BranchId), payment.Amount, payment.CurrencyCode, payment.Method.ToString(), payment.PaymentAccountId,
            payment.PaymentDate, payment.PostingDate, Db(payment.ValueDate), Db(payment.BankReference), Db(NormalizeReference(payment.BankReference)), payment.SourceReference, now, actor, transferId);
        await insert.ExecuteNonQueryAsync(ct);
    }

    private static async Task InsertTransferRelationshipAsync(ITransactionContext context, TenantId tenant, Guid entity, Guid payment, Guid relatedPayment, string relationshipType, decimal amount, DateTimeOffset now, CancellationToken ct)
    {
        await using var relationship = new NpgsqlCommand("insert into finance_payments.payment_relationship(tenant_id,legal_entity_id,id,payment_id,related_payment_id,relationship_type,amount,created_at) values($1,$2,$3,$4,$5,$6,$7,$8)", context.Connection, context.Transaction);
        Add(relationship, tenant.Value, entity, Guid.CreateVersion7(), payment, relatedPayment, relationshipType, amount, now);
        await relationship.ExecuteNonQueryAsync(ct);
    }

    public Task<PaymentRecord> CreateRefundDraftAsync(TenantId tenant, Guid actor, Guid membership, Guid correlationId, CreateRefundCommand request, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var amount = PaymentRules.ParseAmount(request.Amount);
            if (request.OriginalPaymentId == Guid.Empty || request.PaymentAccountId == Guid.Empty || string.IsNullOrWhiteSpace(request.IdempotencyKey)
                || request.IdempotencyKey.Trim().Length > 200 || string.IsNullOrWhiteSpace(request.ReasonCode) || request.ReasonCode.Trim().Length > 120)
                throw new PaymentException("payments.refund_request_invalid", "Refund details are incomplete.");
            var requestHash = Hash(new { request.LegalEntityId, request.OriginalPaymentId, request.PaymentAccountId, request.Method, amount, request.PaymentDate, request.PostingDate, request.ValueDate, reason = request.ReasonCode.Trim(), bankReference = Clean(request.BankReference) });
            await using (var reserve = new NpgsqlCommand("insert into finance_payments.payment_idempotency(tenant_id,id,scope,idempotency_key,request_hash,created_at) values($1,$2,'create-refund',$3,$4,$5) on conflict(tenant_id,scope,idempotency_key) do nothing", context.Connection, context.Transaction))
            { Add(reserve, tenant.Value, Guid.CreateVersion7(), request.IdempotencyKey.Trim(), requestHash, clock.GetUtcNow()); await reserve.ExecuteNonQueryAsync(token); }
            await using (var lookup = new NpgsqlCommand("select request_hash,result_id from finance_payments.payment_idempotency where tenant_id=$1 and scope='create-refund' and idempotency_key=$2 for update", context.Connection, context.Transaction))
            {
                Add(lookup, tenant.Value, request.IdempotencyKey.Trim()); await using var prior = await lookup.ExecuteReaderAsync(token);
                if (!await prior.ReadAsync(token)) throw new PaymentException("payments.idempotency_unavailable", "The refund request could not be reserved.");
                var previousHash = prior.GetString(0).Trim(); var previousId = prior.IsDBNull(1) ? (Guid?)null : prior.GetGuid(1); await prior.CloseAsync();
                if (previousHash != requestHash) throw new PaymentException("payments.idempotency_conflict", "This refund key was already used for different details.");
                if (previousId is { } existingId) return await GetPaymentInTransactionAsync(context, tenant, request.LegalEntityId, existingId, token)
                    ?? throw new PaymentException("payments.idempotency_result_missing", "The original refund result is unavailable.");
            }

            Guid? branchId; Guid partyId; string currencyCode;
            await using (var original = new NpgsqlCommand("select branch_id,party_id,currency_code,payment_type from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and id=$3 and direction='Incoming' and business_state='Completed' for update", context.Connection, context.Transaction))
            {
                Add(original, tenant.Value, request.LegalEntityId, request.OriginalPaymentId); await using var reader = await original.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new PaymentException("payments.refund_original_unavailable", "Only a completed incoming payment can be refunded.");
                branchId = GuidValue(reader, 0); if (reader.IsDBNull(1)) throw new PaymentException("payments.refund_party_required", "The original payment has no customer to refund.");
                partyId = reader.GetGuid(1); currencyCode = reader.GetString(2).Trim();
                var originalType = reader.GetString(3);
                if (originalType is not ("CustomerReceipt" or "CustomerAdvance")) throw new PaymentException("payments.refund_original_type_invalid", "This payment type does not support a customer refund.");
            }
            var settlement = await subledgerRead.GetSourceSettlementSummaryAsync(tenant, new(request.LegalEntityId), "Payment", request.OriginalPaymentId, token)
                ?? throw new PaymentException("payments.refund_subledger_item_missing", "The original payment's available party balance could not be confirmed.");
            var refundableBasis = ParseRequired(settlement.OpenAmount);
            await using (var total = new NpgsqlCommand("select coalesce(sum(amount),0) from finance_payments.payment_refund where tenant_id=$1 and legal_entity_id=$2 and original_payment_id=$3 and status in ('Pending','Completed')", context.Connection, context.Transaction))
            {
                Add(total, tenant.Value, request.LegalEntityId, request.OriginalPaymentId);
                var refunded = (decimal)(await total.ExecuteScalarAsync(token) ?? 0m);
                if (refunded + amount > refundableBasis) throw new PaymentException("payments.refund_exceeds_refundable_amount", "The refund amount exceeds the remaining unapplied balance. Reverse existing applications first.");
            }

            string accountType; string accountStatus; string accountPolicy; string? accountCurrency; Guid? accountBranch;
            await using (var account = new NpgsqlCommand("select account_type,status,currency_policy,currency_code,branch_id from finance_payments.payment_account where tenant_id=$1 and legal_entity_id=$2 and id=$3 for share", context.Connection, context.Transaction))
            {
                Add(account, tenant.Value, request.LegalEntityId, request.PaymentAccountId); await using var reader = await account.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new PaymentException("payments.account_not_found", "The selected payment account is unavailable.");
                accountType = reader.GetString(0); accountStatus = reader.GetString(1); accountPolicy = reader.GetString(2); accountCurrency = StringValue(reader, 3); accountBranch = GuidValue(reader, 4);
            }
            if (accountStatus != "Active") throw new PaymentException("payments.account_inactive", "The selected payment account is suspended or archived.");
            if (accountPolicy == "SingleCurrency" && accountCurrency != currencyCode) throw new PaymentException("payments.account_currency_mismatch", "The refund account currency differs from the original payment.");
            if (accountBranch is { } scopedBranch && scopedBranch != branchId) throw new PaymentException("payments.cashbox_scope_invalid", "The selected payment account is not available for the original payment branch.");
            if (!AccountAcceptsMethod(accountType, request.Method)) throw new PaymentException("payments.account_method_invalid", "The selected payment account cannot use this refund method.");
            await using (var method = new NpgsqlCommand("select exists(select 1 from finance_payments.payment_method_config where tenant_id=$1 and legal_entity_id=$2 and method=$3 and status='Active' and effective_from<=$4 and (effective_to is null or effective_to >= $4) and (payment_account_id is null or payment_account_id=$5) and (branch_id is null or branch_id is not distinct from $6))", context.Connection, context.Transaction))
            { Add(method, tenant.Value, request.LegalEntityId, request.Method.ToString(), request.PaymentDate, request.PaymentAccountId, Db(branchId)); if ((bool?)await method.ExecuteScalarAsync(token) != true) throw new PaymentException("payments.method_not_enabled", "This refund method is not enabled for the selected account and date."); }

            var id = Guid.CreateVersion7(); var refundId = Guid.CreateVersion7(); var now = clock.GetUtcNow();
            var number = await sequences.AllocateAsync(context, new("payments.payment", tenant, request.LegalEntityId, null, null, request.PaymentDate.ToString("yyyy", System.Globalization.CultureInfo.InvariantCulture), "PAY-", 6, 1), token);
            await using (var insert = new NpgsqlCommand("insert into finance_payments.payment(tenant_id,legal_entity_id,id,payment_number,branch_id,direction,payment_type,party_id,amount,currency_code,method,payment_account_id,payment_date,posting_date,value_date,bank_reference,normalized_reference,business_state,row_version,created_at,updated_at) values($1,$2,$3,$4,$5,'Outgoing','CustomerRefund',$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,'Draft',1,$16,$16)", context.Connection, context.Transaction))
            { var bankReference = Clean(request.BankReference); Add(insert, tenant.Value, request.LegalEntityId, id, number, Db(branchId), partyId, amount, currencyCode, request.Method.ToString(), request.PaymentAccountId, request.PaymentDate, request.PostingDate, Db(request.ValueDate), Db(bankReference), Db(NormalizeReference(bankReference)), now); await insert.ExecuteNonQueryAsync(token); }
            await using (var refund = new NpgsqlCommand("insert into finance_payments.payment_refund(tenant_id,legal_entity_id,id,original_payment_id,refund_payment_id,amount,currency_code,reason_code,status,idempotency_key,created_by,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,'Pending',$9,$10,$11)", context.Connection, context.Transaction))
            { Add(refund, tenant.Value, request.LegalEntityId, refundId, request.OriginalPaymentId, id, amount, currencyCode, request.ReasonCode.Trim(), request.IdempotencyKey.Trim(), actor, now); await refund.ExecuteNonQueryAsync(token); }
            await using (var relationship = new NpgsqlCommand("insert into finance_payments.payment_relationship(tenant_id,legal_entity_id,id,payment_id,related_payment_id,relationship_type,amount,created_at) values($1,$2,$3,$4,$5,'RefundOf',$6,$7)", context.Connection, context.Transaction))
            { Add(relationship, tenant.Value, request.LegalEntityId, Guid.CreateVersion7(), id, request.OriginalPaymentId, amount, now); await relationship.ExecuteNonQueryAsync(token); }
            await using (var updateKey = new NpgsqlCommand("update finance_payments.payment_idempotency set result_type='Payment',result_id=$4 where tenant_id=$1 and scope='create-refund' and idempotency_key=$2 and request_hash=$3", context.Connection, context.Transaction))
            { Add(updateKey, tenant.Value, request.IdempotencyKey.Trim(), requestHash, id); if (await updateKey.ExecuteNonQueryAsync(token) != 1) throw new PaymentException("payments.idempotency_result_missing", "The refund result could not be recorded."); }
            await UpsertProjectionAsync(context, tenant, request.LegalEntityId, id, number, request.Amount, currencyCode, "Outgoing", request.Method.ToString(), "Draft", request.BankReference, now, token);
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new(Guid.CreateVersion7(), tenant, request.LegalEntityId, branchId, "User", actor, membership, "payments", "payment.refund_created", "payment", id, null,
                JsonSerializer.Serialize(new { number, amount, currencyCode, originalPaymentId = request.OriginalPaymentId, reasonCode = request.ReasonCode.Trim() }, JsonOptions), null, "api", correlationId, null, now, null), token);
            return new PaymentRecord(id, request.LegalEntityId, number, branchId, PaymentDirection.Outgoing, PaymentType.CustomerRefund, partyId, amount, currencyCode, request.Method,
                request.PaymentAccountId, request.PaymentDate, request.PostingDate, request.ValueDate, Clean(request.BankReference), null, null, "PaymentRefund", request.OriginalPaymentId.ToString("D"), PaymentBusinessState.Draft, null, 1);
        }, ct);

    public Task<PaymentRecord> ReversePaymentAsync(TenantId tenant, Guid actor, Guid membership, Guid correlationId,
        ReversePaymentCommand request, FinancialCalculationContextSnapshotContract financialContext, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var reason = request.ReasonCode.Trim();
            if (request.PaymentId == Guid.Empty || string.IsNullOrWhiteSpace(reason) || reason.Length > 120
                || string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Trim().Length > 200)
                throw new PaymentException("payments.reversal_request_invalid", "Enter a reason and a valid request key to reverse this payment.");

            await using var load = new NpgsqlCommand("""
                select p.id,p.legal_entity_id,p.payment_number,p.branch_id,p.direction,p.payment_type,p.party_id,p.amount,p.currency_code,p.method,
                       p.payment_account_id,p.payment_date,p.posting_date,p.value_date,p.bank_reference,p.customer_reference,p.supplier_reference,
                       p.source_type,p.source_reference,p.business_state,p.completed_at,p.row_version,a.code
                from finance_payments.payment p join finance_payments.payment_account a
                  on a.tenant_id=p.tenant_id and a.legal_entity_id=p.legal_entity_id and a.id=p.payment_account_id
                where p.tenant_id=$1 and p.legal_entity_id=$2 and p.id=$3 for update of p
                """, context.Connection, context.Transaction);
            Add(load, tenant.Value, request.LegalEntityId, request.PaymentId);
            await using var reader = await load.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new PaymentException("payments.payment_not_found", "The payment was not found.");
            var original = ReadPayment(reader); var accountCode = reader.GetString(22); await reader.CloseAsync();
            if (original.Direction == PaymentDirection.InternalTransfer || original.PartyId is not { } partyId)
                throw new PaymentException("payments.reversal_not_available", "Only customer or supplier payments can be reversed.");
            if (!string.Equals(financialContext.TransactionCurrencyCode, original.CurrencyCode, StringComparison.OrdinalIgnoreCase))
                throw new PaymentException("payments.currency_context_changed", "The approved currency context no longer matches this reversal.");

            var feeRows = new List<(Guid Id, string Type, decimal Amount, string Currency, string State)>();
            await using (var loadFees = new NpgsqlCommand("select id,fee_type,amount,currency_code,recognition_state from finance_payments.payment_fee where tenant_id=$1 and legal_entity_id=$2 and payment_id=$3 order by id for update", context.Connection, context.Transaction))
            {
                Add(loadFees, tenant.Value, request.LegalEntityId, request.PaymentId);
                await using var feeReader = await loadFees.ExecuteReaderAsync(token);
                while (await feeReader.ReadAsync(token)) feeRows.Add((feeReader.GetGuid(0), feeReader.GetString(1), feeReader.GetDecimal(2), feeReader.GetString(3).Trim(), feeReader.GetString(4)));
            }
            if (feeRows.Count > 100) throw new PaymentException("payments.reversal_fee_limit", "This payment has too many fees to reverse in one operation.");
            var dispositions = request.FeeDispositions ?? [];
            if (feeRows.Count > 0 && dispositions.Count == 0)
                throw new PaymentException("payments.reversal_fee_disposition_required", "Choose whether to retain or reverse each recognized fee.");
            if (dispositions.Select(item => item.FeeId).Distinct().Count() != dispositions.Count
                || dispositions.Count != feeRows.Count || feeRows.Any(fee => dispositions.All(item => item.FeeId != fee.Id)))
                throw new PaymentException("payments.reversal_fee_disposition_invalid", "Provide exactly one disposition for every fee on this payment.");
            var feesById = feeRows.ToDictionary(fee => fee.Id);
            foreach (var disposition in dispositions)
            {
                if (!feesById.TryGetValue(disposition.FeeId, out var fee) || !Enum.IsDefined(disposition.Disposition))
                    throw new PaymentException("payments.reversal_fee_disposition_invalid", "A fee disposition does not match this payment.");
                if (fee.State != "Recognized") throw new PaymentException("payments.reversal_fee_state_invalid", "Only recognized fees can be disposed during payment reversal.");
                if (disposition.Disposition == PaymentFeeDisposition.Reverse
                    && (string.IsNullOrWhiteSpace(disposition.EvidenceReference) || disposition.EvidenceReference.Trim().Length > 500))
                    throw new PaymentException("payments.reversal_fee_evidence_required", "Add the bank or provider evidence reference before reversing a fee.");
                if (disposition.Disposition == PaymentFeeDisposition.Retain && !string.IsNullOrWhiteSpace(disposition.EvidenceReference)
                    && disposition.EvidenceReference.Trim().Length > 500)
                    throw new PaymentException("payments.reversal_fee_evidence_invalid", "The fee evidence reference is too long.");
            }
            var normalizedDispositions = dispositions.OrderBy(item => item.FeeId).Select(item => new
            {
                item.FeeId,
                item.Disposition,
                EvidenceReference = string.IsNullOrWhiteSpace(item.EvidenceReference) ? null : item.EvidenceReference.Trim()
            }).ToArray();
            var hash = Hash(new { request.LegalEntityId, request.PaymentId, request.PaymentDate, request.PostingDate, reason, FeeDispositions = normalizedDispositions });
            await using (var reserve = new NpgsqlCommand("insert into finance_payments.payment_idempotency(tenant_id,id,scope,idempotency_key,request_hash,created_at) values($1,$2,'reverse-payment',$3,$4,$5) on conflict(tenant_id,scope,idempotency_key) do nothing", context.Connection, context.Transaction))
            { Add(reserve, tenant.Value, Guid.CreateVersion7(), request.IdempotencyKey.Trim(), hash, clock.GetUtcNow()); await reserve.ExecuteNonQueryAsync(token); }
            await using (var key = new NpgsqlCommand("select request_hash,result_id from finance_payments.payment_idempotency where tenant_id=$1 and scope='reverse-payment' and idempotency_key=$2 for update", context.Connection, context.Transaction))
            {
                Add(key, tenant.Value, request.IdempotencyKey.Trim()); await using var prior = await key.ExecuteReaderAsync(token);
                if (!await prior.ReadAsync(token)) throw new PaymentException("payments.idempotency_unavailable", "The reversal request could not be reserved.");
                var oldHash = prior.GetString(0).Trim(); var oldId = prior.IsDBNull(1) ? (Guid?)null : prior.GetGuid(1); await prior.CloseAsync();
                if (oldHash != hash) throw new PaymentException("payments.idempotency_conflict", "This reversal key was already used for different details.");
                if (oldId is { } existing) return await GetPaymentInTransactionAsync(context, tenant, request.LegalEntityId, existing, token)
                    ?? throw new PaymentException("payments.idempotency_result_missing", "The reversal result is unavailable.");
            }
            if (original.State != PaymentBusinessState.Completed)
                throw new PaymentException("payments.reversal_not_available", "Only completed customer or supplier payments can be reversed.");

            var summary = await subledgerRead.GetSourceSettlementSummaryAsync(tenant, new(request.LegalEntityId), "Payment", request.PaymentId, token);
            if (summary is null) throw new PaymentException("payments.reversal_subledger_item_missing", "The payment settlement history could not be confirmed.");
            if (ParseRequired(summary.SettledAmount) != 0m)
                throw new PaymentException("payments.reversal_has_applications", "Reverse or unapply the payment's existing applications before reversing the payment.");
            await using (var priorReversal = new NpgsqlCommand("select exists(select 1 from finance_payments.payment_reversal where tenant_id=$1 and legal_entity_id=$2 and original_payment_id=$3)", context.Connection, context.Transaction))
            { Add(priorReversal, tenant.Value, request.LegalEntityId, request.PaymentId); if ((bool?)await priorReversal.ExecuteScalarAsync(token) == true) throw new PaymentException("payments.reversal_already_exists", "This payment already has a reversal."); }
            await using (var refund = new NpgsqlCommand("select exists(select 1 from finance_payments.payment_refund where tenant_id=$1 and legal_entity_id=$2 and original_payment_id=$3 and status in ('Pending','Completed'))", context.Connection, context.Transaction))
            { Add(refund, tenant.Value, request.LegalEntityId, request.PaymentId); if ((bool?)await refund.ExecuteScalarAsync(token) == true) throw new PaymentException("payments.reversal_refund_exists", "Cancel pending refunds and resolve completed refunds before reversing this payment."); }
            var snapshot = financialContext.ExchangeRate;
            var rate = snapshot is null ? 1m : ParseRequired(snapshot.Rate);
            if (financialContext.BaseCurrencyCode != original.CurrencyCode && snapshot is null)
                throw new PaymentException("payments.fx_rate_missing", "An approved exchange rate is required to reverse this payment.");
            var rounding = financialContext.RoundingPolicy.ContextRules.SingleOrDefault(rule => rule.Context == FinancialRoundingContext.MoneySettlement)?.Mode ?? FinancialRoundingMode.HalfUp;
            var midpoint = rounding switch { FinancialRoundingMode.HalfEven => MidpointRounding.ToEven, FinancialRoundingMode.Down => MidpointRounding.ToZero, _ => MidpointRounding.AwayFromZero };
            var baseAmount = decimal.Round(original.Amount * rate, financialContext.RoundingPolicy.MoneyScale, midpoint);
            if (baseAmount <= 0m) throw new PaymentException("payments.fx_snapshot_invalid", "The approved exchange rate produced an invalid reversal amount.");
            var reversalId = Guid.CreateVersion7(); var reversalRecordId = Guid.CreateVersion7(); var now = clock.GetUtcNow();
            var reversalNumber = await sequences.AllocateAsync(context, new("payments.payment", tenant, request.LegalEntityId, null, null, request.PaymentDate.ToString("yyyy", System.Globalization.CultureInfo.InvariantCulture), "PAY-", 6, 1), token);
            var reversesIncoming = original.Direction == PaymentDirection.Incoming;
            var reversalType = reversesIncoming ? PaymentType.CustomerRefund : PaymentType.SupplierRefund;
            var reversalDirection = reversesIncoming ? PaymentDirection.Outgoing : PaymentDirection.Incoming;
            await using (var insert = new NpgsqlCommand("insert into finance_payments.payment(tenant_id,legal_entity_id,id,payment_number,branch_id,direction,payment_type,party_id,amount,currency_code,method,payment_account_id,payment_date,posting_date,value_date,source_type,source_reference,business_state,row_version,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,'PaymentReversal',$16,'Draft',1,$17,$17)", context.Connection, context.Transaction))
            { Add(insert, tenant.Value, request.LegalEntityId, reversalId, reversalNumber, Db(original.BranchId), reversalDirection.ToString(), reversalType.ToString(), partyId, original.Amount, original.CurrencyCode, original.Method.ToString(), original.PaymentAccountId, request.PaymentDate, request.PostingDate, Db(original.ValueDate), original.Id.ToString("D"), now); await insert.ExecuteNonQueryAsync(token); }

            var configId = await ledgerConfiguration.GetActiveConfigurationVersionIdAsync(context, tenant, new(request.LegalEntityId), token);
            var settlementRole = reversesIncoming ? "Receivable" : "Payable";
            var partyDirection = reversesIncoming ? SubledgerDirectionContract.Receivable : SubledgerDirectionContract.Payable;
            var polarity = reversesIncoming ? SubledgerPolarityContract.Debit : SubledgerPolarityContract.Credit;
            var openItem = new OpenItemSnapshotContract(reversalId.ToString("D"), null, reversalNumber, request.PaymentDate, request.PostingDate, request.PostingDate,
                original.CurrencyCode, original.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture), financialContext.BaseCurrencyCode,
                baseAmount.ToString(System.Globalization.CultureInfo.InvariantCulture), settlementRole, $"role:{settlementRole}", $"configuration:{configId:D}",
                snapshot is null ? null : JsonSerializer.Serialize(snapshot, JsonOptions), 1);
            var itemId = await subledger.CreatePaymentOpenItemAsync(context, tenant, new(new(request.LegalEntityId), partyId, [openItem], actor, correlationId), partyDirection, polarity, token);
            var factType = reversesIncoming ? AccountingFactTypes.RefundPaid : AccountingFactTypes.PaymentReceived;
            var payload = new PaymentFactPayload(original.Amount, original.CurrencyCode, "Default", settlementRole, reversalDirection.ToString(), accountCode,
                partyId, baseAmount, financialContext.BaseCurrencyCode, snapshot?.ApprovedRateId?.ToString("D") ?? (snapshot is null ? "base-currency" : snapshot.Rate));
            var fact = new AccountingFactEnvelope<IAccountingFactPayload>(reversalId, tenant, new(request.LegalEntityId), factType, 1, "payments", reversalId.ToString("D"),
                $"payment-reversal:{original.Id:D}", request.PaymentDate, request.PostingDate, payload, correlationId, actor);
            await ledger.PostPaymentAsync(context, fact, payload, token);
            await using (var completeReversal = new NpgsqlCommand("update finance_payments.payment set business_state='Completed',completed_at=$4,completed_by=$5,subledger_item_id=$6,accounting_fact_id=$3,row_version=row_version+1,updated_at=$4 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and business_state='Draft'", context.Connection, context.Transaction))
            { Add(completeReversal, tenant.Value, request.LegalEntityId, reversalId, now, actor, itemId); if (await completeReversal.ExecuteNonQueryAsync(token) != 1) throw new PaymentException("payments.payment_state_conflict", "The reversal movement could not be completed."); }
            await using (var relation = new NpgsqlCommand("insert into finance_payments.payment_relationship(tenant_id,legal_entity_id,id,payment_id,related_payment_id,relationship_type,amount,created_at) values($1,$2,$3,$4,$5,'ReversalOf',$6,$7)", context.Connection, context.Transaction))
            { Add(relation, tenant.Value, request.LegalEntityId, Guid.CreateVersion7(), reversalId, original.Id, original.Amount, now); await relation.ExecuteNonQueryAsync(token); }
            await using (var reversal = new NpgsqlCommand("insert into finance_payments.payment_reversal(tenant_id,legal_entity_id,id,original_payment_id,reversal_payment_id,reason_code,status,created_by,created_at) values($1,$2,$3,$4,$5,$6,'Completed',$7,$8)", context.Connection, context.Transaction))
            { Add(reversal, tenant.Value, request.LegalEntityId, reversalRecordId, original.Id, reversalId, reason, actor, now); await reversal.ExecuteNonQueryAsync(token); }
            var correctiveFactsByFeeId = new Dictionary<Guid, Guid>();
            foreach (var disposition in normalizedDispositions)
            {
                var fee = feesById[disposition.FeeId];
                var dispositionId = Guid.CreateVersion7();
                var evidence = disposition.EvidenceReference;
                await using (var saveDisposition = new NpgsqlCommand("insert into finance_payments.payment_fee_disposition(tenant_id,legal_entity_id,id,original_payment_id,reversal_payment_id,payment_fee_id,disposition,evidence_reference,reason_code,created_by,created_at,corrective_fact_id) values($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12)", context.Connection, context.Transaction))
                {
                    var correctiveFactId = disposition.Disposition == PaymentFeeDisposition.Reverse ? dispositionId : (Guid?)null;
                    Add(saveDisposition, tenant.Value, request.LegalEntityId, dispositionId, original.Id, reversalId, fee.Id,
                        disposition.Disposition.ToString(), Db(evidence), reason, actor, now, Db(correctiveFactId));
                    await saveDisposition.ExecuteNonQueryAsync(token);
                }
                if (disposition.Disposition == PaymentFeeDisposition.Reverse)
                {
                    correctiveFactsByFeeId.Add(fee.Id, dispositionId);
                    var correctiveFact = new AccountingFactEnvelope<IAccountingFactPayload>(dispositionId, tenant,
                        new(request.LegalEntityId), AccountingFactTypes.PaymentFeeReversed, 1, "payments",
                        $"payment-fee-reversal:{fee.Id:D}", $"payment-fee-reversal:{fee.Id:D}", request.PaymentDate,
                        request.PostingDate, new PaymentFeeReversalRequestPayload(fee.Id, evidence!), correlationId, actor);
                    await ledger.PostPaymentFeeReversalAsync(context, correctiveFact, fee.Id, evidence!, token);
                }
                await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new(Guid.CreateVersion7(), tenant,
                    request.LegalEntityId, original.BranchId, "User", actor, membership, "payments",
                    disposition.Disposition == PaymentFeeDisposition.Retain ? "payment.fee_retained_on_reversal" : "payment.fee_reversed",
                    "payment_fee", fee.Id, null, JsonSerializer.Serialize(new
                    {
                        originalPaymentId = original.Id,
                        reversalPaymentId = reversalId,
                        fee.Type,
                        fee.Amount,
                        fee.Currency,
                        disposition = disposition.Disposition.ToString(),
                        evidenceReference = evidence,
                        reason
                    }, JsonOptions),
                    null, "api", correlationId, null, now, null), token);
            }
            await using (var update = new NpgsqlCommand("update finance_payments.payment set business_state='Reversed',row_version=row_version+1,updated_at=$4 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and business_state='Completed'", context.Connection, context.Transaction))
            { Add(update, tenant.Value, request.LegalEntityId, original.Id, now); if (await update.ExecuteNonQueryAsync(token) != 1) throw new PaymentException("payments.payment_state_conflict", "The payment changed before reversal."); }
            await using (var updateKey = new NpgsqlCommand("update finance_payments.payment_idempotency set result_type='Payment',result_id=$4 where tenant_id=$1 and scope='reverse-payment' and idempotency_key=$2 and request_hash=$3", context.Connection, context.Transaction))
            { Add(updateKey, tenant.Value, request.IdempotencyKey.Trim(), hash, reversalId); await updateKey.ExecuteNonQueryAsync(token); }
            await UpsertProjectionAsync(context, tenant, request.LegalEntityId, reversalId, reversalNumber, original.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture), original.CurrencyCode,
                reversalDirection.ToString(), original.Method.ToString(), "Completed", original.BankReference, now, token);
            var feeDispositionSummary = normalizedDispositions.Select(item => new
            {
                feeId = item.FeeId,
                disposition = item.Disposition.ToString(),
                correctiveFactId = correctiveFactsByFeeId.TryGetValue(item.FeeId, out var correctiveFactId) ? correctiveFactId : (Guid?)null
            }).ToArray();
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new(Guid.CreateVersion7(), tenant, request.LegalEntityId, original.BranchId,
                "User", actor, membership, "payments", "payment.reversed", "payment", original.Id, null,
                JsonSerializer.Serialize(new { original.Number, reversalNumber, original.Amount, original.CurrencyCode, reason, feeDispositions = feeDispositionSummary }, JsonOptions), null, "api", correlationId, null, now, null), token);
            context.AddOutbox(new(Guid.CreateVersion7(), "PaymentReversedV1", JsonSerializer.Serialize(new { paymentId = original.Id, reversalPaymentId = reversalId, reversalNumber, feeDispositions = feeDispositionSummary }, JsonOptions), tenant, now, correlationId, original.Id, "payments"));
            return new PaymentRecord(reversalId, request.LegalEntityId, reversalNumber, original.BranchId, reversalDirection, reversalType, partyId,
                original.Amount, original.CurrencyCode, original.Method, original.PaymentAccountId, request.PaymentDate, request.PostingDate, original.ValueDate,
                original.BankReference, original.CustomerReference, original.SupplierReference, "PaymentReversal", original.Id.ToString("D"), PaymentBusinessState.Completed, now, 1);
        }, ct);

    public Task<PaymentIntentResult> CreatePaymentIntentAsync(TenantId tenant, Guid actor, Guid membership, Guid correlationId,
        CreatePaymentIntentRequest request, string idempotencyKey, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var amount = PaymentRules.ParseAmount(request.Amount);
            var currency = PaymentRules.NormalizeCurrency(request.CurrencyCode);
            if (request.LegalEntityId == Guid.Empty || request.ProviderConfigId == Guid.Empty || string.IsNullOrWhiteSpace(idempotencyKey)
                || idempotencyKey.Trim().Length > 200 || request.ExpiresAt is { } expires && expires <= clock.GetUtcNow())
                throw new PaymentException("payments.request_invalid", "Payment request details are incomplete or expired.");
            if (request.PartyId is null || request.PartyId == Guid.Empty)
                throw new PaymentException("payments.party_required", "Select the customer whose payment is being requested.");
            var hash = Hash(new { request.LegalEntityId, request.BranchId, request.ProviderConfigId, request.PartyId, amount, currency, request.SourceType, request.SourceReference, request.ExpiresAt });
            await using (var reserve = new NpgsqlCommand("insert into finance_payments.payment_idempotency(tenant_id,id,scope,idempotency_key,request_hash,created_at) values($1,$2,'create-payment-intent',$3,$4,$5) on conflict(tenant_id,scope,idempotency_key) do nothing", context.Connection, context.Transaction))
            { Add(reserve, tenant.Value, Guid.CreateVersion7(), idempotencyKey.Trim(), hash, clock.GetUtcNow()); await reserve.ExecuteNonQueryAsync(token); }
            await using (var key = new NpgsqlCommand("select request_hash,result_id from finance_payments.payment_idempotency where tenant_id=$1 and scope='create-payment-intent' and idempotency_key=$2 for update", context.Connection, context.Transaction))
            {
                Add(key, tenant.Value, idempotencyKey.Trim()); await using var reader = await key.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new PaymentException("payments.idempotency_unavailable", "The payment request could not be reserved.");
                var oldHash = reader.GetString(0).Trim(); var oldId = reader.IsDBNull(1) ? (Guid?)null : reader.GetGuid(1); await reader.CloseAsync();
                if (oldHash != hash) throw new PaymentException("payments.idempotency_conflict", "This request key was already used for different payment details.");
                if (oldId is { } previous) return await GetPaymentIntentInTransactionAsync(context, tenant, request.LegalEntityId, previous, token)
                    ?? throw new PaymentException("payments.idempotency_result_missing", "The original payment request is unavailable.");
            }
            await using (var provider = new NpgsqlCommand("select capabilities,supported_currencies,supported_methods,status from finance_payments.provider_config where tenant_id=$1 and legal_entity_id=$2 and id=$3 for share", context.Connection, context.Transaction))
            {
                Add(provider, tenant.Value, request.LegalEntityId, request.ProviderConfigId); await using var reader = await provider.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new PaymentException("payments.provider_config_unavailable", "The selected payment provider is unavailable.");
                var capabilities = reader.GetFieldValue<System.Text.Json.JsonDocument>(0); var currencies = reader.GetFieldValue<string[]>(1);
                var methods = reader.GetFieldValue<string[]>(2); var status = reader.GetString(3); await reader.CloseAsync();
                if (status != "Active" || !capabilities.RootElement.TryGetProperty("createPaymentRequest", out var enabled) || enabled.ValueKind != System.Text.Json.JsonValueKind.True)
                    throw new PaymentException("payments.provider_operation_unsupported", "This provider is not configured to create payment requests.");
                if (currencies.Length > 0 && !currencies.Contains(currency, StringComparer.OrdinalIgnoreCase)) throw new PaymentException("payments.provider_currency_unsupported", "This provider does not accept the selected currency.");
                if (!methods.Contains(PaymentMethod.PaymentGateway.ToString(), StringComparer.Ordinal))
                    throw new PaymentException("payments.provider_method_unsupported", "This provider is not configured for online payment collection.");
            }
            var now = clock.GetUtcNow(); var id = Guid.CreateVersion7(); var attemptId = Guid.CreateVersion7(); var operationId = Guid.CreateVersion7();
            var merchant = $"REQ-{id:N}";
            await using (var insert = new NpgsqlCommand("insert into finance_payments.payment_request(tenant_id,legal_entity_id,id,branch_id,direction,party_id,amount,currency_code,provider_config_id,source_type,source_reference,merchant_reference,workflow_state,expires_at,created_by,created_by_membership_id,created_at,updated_at) values($1,$2,$3,$4,'Incoming',$5,$6,$7,$8,$9,$10,$11,'Pending',$12,$13,$14,$15,$15)", context.Connection, context.Transaction))
            { Add(insert, tenant.Value, request.LegalEntityId, id, Db(request.BranchId), Db(request.PartyId), amount, currency, request.ProviderConfigId, Db(Clean(request.SourceType)), Db(Clean(request.SourceReference)), merchant, Db(request.ExpiresAt), actor, membership, now); await insert.ExecuteNonQueryAsync(token); }
            await using (var attempt = new NpgsqlCommand("insert into finance_payments.payment_attempt(tenant_id,legal_entity_id,id,payment_request_id,attempt_number,provider_config_id,provider_operation_id,state,idempotency_key,created_at,updated_at) values($1,$2,$3,$4,1,$5,$6::text,'Created',$7,$8,$8)", context.Connection, context.Transaction))
            { Add(attempt, tenant.Value, request.LegalEntityId, attemptId, id, request.ProviderConfigId, operationId, idempotencyKey.Trim(), now); await attempt.ExecuteNonQueryAsync(token); }
            await using (var operation = new NpgsqlCommand("insert into finance_payments.provider_operation(tenant_id,legal_entity_id,id,provider_config_id,operation_type,aggregate_type,aggregate_id,idempotency_key,request_hash,state,attempts,next_attempt_at,created_at,updated_at) values($1,$2,$3,$4,'Create','PaymentRequest',$5,$6,$7,'Pending',0,$8,$9,$9)", context.Connection, context.Transaction))
            { Add(operation, tenant.Value, request.LegalEntityId, operationId, request.ProviderConfigId, id, idempotencyKey.Trim(), hash, now, now); await operation.ExecuteNonQueryAsync(token); }
            await using (var idem = new NpgsqlCommand("update finance_payments.payment_idempotency set result_type='PaymentRequest',result_id=$4 where tenant_id=$1 and scope='create-payment-intent' and idempotency_key=$2 and request_hash=$3", context.Connection, context.Transaction))
            { Add(idem, tenant.Value, idempotencyKey.Trim(), hash, id); await idem.ExecuteNonQueryAsync(token); }
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new(Guid.CreateVersion7(), tenant, request.LegalEntityId, request.BranchId,
                "User", actor, null, "payments", "payment_request.created", "payment_request", id, null,
                JsonSerializer.Serialize(new { merchantReference = merchant, amount, currency }, JsonOptions), null, "api", correlationId, null, now, null), token);
            context.AddOutbox(new(Guid.CreateVersion7(), "PaymentProviderOperationRequestedV1", JsonSerializer.Serialize(new { operationId, requestId = id, attemptId, operation = "Create" }, JsonOptions), tenant, now, correlationId, id, "payments"));
            return new PaymentIntentResult(id, request.LegalEntityId, merchant, amount, currency, "Pending", attemptId, 1);
        }, ct);

    public Task<PaymentIntentResult> RetryPaymentIntentAsync(TenantId tenant, Guid actor, Guid membership, Guid correlationId,
        Guid legalEntityId, Guid requestId, string idempotencyKey, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            if (legalEntityId == Guid.Empty || requestId == Guid.Empty || string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Trim().Length > 200)
                throw new PaymentException("payments.request_invalid", "Payment retry details are incomplete.");
            var requestHash = Hash(new { legalEntityId, requestId });
            await using (var reserve = new NpgsqlCommand("insert into finance_payments.payment_idempotency(tenant_id,id,scope,idempotency_key,request_hash,created_at) values($1,$2,'retry-payment-intent',$3,$4,$5) on conflict(tenant_id,scope,idempotency_key) do nothing", context.Connection, context.Transaction))
            { Add(reserve, tenant.Value, Guid.CreateVersion7(), idempotencyKey.Trim(), requestHash, clock.GetUtcNow()); await reserve.ExecuteNonQueryAsync(token); }
            await using (var key = new NpgsqlCommand("select request_hash,result_id from finance_payments.payment_idempotency where tenant_id=$1 and scope='retry-payment-intent' and idempotency_key=$2 for update", context.Connection, context.Transaction))
            {
                Add(key, tenant.Value, idempotencyKey.Trim()); await using var reader = await key.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new PaymentException("payments.idempotency_unavailable", "The payment retry could not be reserved.");
                var priorHash = reader.GetString(0).Trim(); var priorAttempt = reader.IsDBNull(1) ? (Guid?)null : reader.GetGuid(1); await reader.CloseAsync();
                if (priorHash != requestHash) throw new PaymentException("payments.idempotency_conflict", "This retry key was already used for another payment request.");
                if (priorAttempt is { } attemptId) return await GetPaymentIntentAttemptAsync(context, tenant, legalEntityId, attemptId, token)
                    ?? throw new PaymentException("payments.idempotency_result_missing", "The original payment retry is unavailable.");
            }

            Guid providerConfigId; string workflowState; DateTimeOffset? expiresAt; string requestCurrency;
            await using (var request = new NpgsqlCommand("select provider_config_id,workflow_state,expires_at,currency_code from finance_payments.payment_request where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction))
            {
                Add(request, tenant.Value, legalEntityId, requestId); await using var reader = await request.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new PaymentException("payments.request_not_found", "The payment request was not found.");
                providerConfigId = reader.GetGuid(0); workflowState = reader.GetString(1); expiresAt = reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTimeOffset>(2); requestCurrency = reader.GetString(3).Trim(); await reader.CloseAsync();
            }
            if (expiresAt is { } expiry && expiry <= clock.GetUtcNow() || workflowState is "Succeeded" or "Cancelled" or "Expired")
                throw new PaymentException("payments.request_not_retryable", "This payment request is no longer available for another attempt.");
            await using (var provider = new NpgsqlCommand("select capabilities,supported_currencies,supported_methods,status from finance_payments.provider_config where tenant_id=$1 and legal_entity_id=$2 and id=$3 for share", context.Connection, context.Transaction))
            {
                Add(provider, tenant.Value, legalEntityId, providerConfigId); await using var reader = await provider.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new PaymentException("payments.provider_config_unavailable", "The selected payment provider is unavailable.");
                var capabilities = reader.GetFieldValue<System.Text.Json.JsonDocument>(0); var currencies = reader.GetFieldValue<string[]>(1);
                var methods = reader.GetFieldValue<string[]>(2); var status = reader.GetString(3); await reader.CloseAsync();
                if (status != "Active" || !capabilities.RootElement.TryGetProperty("createPaymentRequest", out var enabled) || enabled.ValueKind != System.Text.Json.JsonValueKind.True)
                    throw new PaymentException("payments.provider_operation_unsupported", "This provider is not available to create another payment attempt.");
                if (currencies.Length > 0 && !currencies.Contains(requestCurrency, StringComparer.OrdinalIgnoreCase)
                    || !methods.Contains(PaymentMethod.PaymentGateway.ToString(), StringComparer.Ordinal))
                    throw new PaymentException("payments.provider_method_unsupported", "This provider is not configured for this payment currency or method.");
            }
            int attemptNumber; string previousState;
            await using (var previous = new NpgsqlCommand("select id,attempt_number,state from finance_payments.payment_attempt where tenant_id=$1 and legal_entity_id=$2 and payment_request_id=$3 order by attempt_number desc limit 1 for update", context.Connection, context.Transaction))
            {
                Add(previous, tenant.Value, legalEntityId, requestId); await using var reader = await previous.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new PaymentException("payments.attempt_missing", "The payment request has no execution history.");
                attemptNumber = reader.GetInt32(1); previousState = reader.GetString(2); await reader.CloseAsync();
            }
            if (previousState != "Failed")
                throw new PaymentException(previousState == "Unknown" ? "payments.provider_status_sync_required" : "payments.attempt_not_retryable",
                    previousState == "Unknown" ? "Check the provider status before retrying this payment." : "Only a confirmed failed attempt can be retried.");
            if (attemptNumber >= 5) throw new PaymentException("payments.attempt_limit_reached", "This payment request has reached its retry limit.");
            Guid attemptIdNew = Guid.CreateVersion7(); Guid operationId = Guid.CreateVersion7(); var now = clock.GetUtcNow();
            var hash = Hash(new { requestId, attemptNumber = attemptNumber + 1, idempotencyKey = idempotencyKey.Trim() });
            await using (var attempt = new NpgsqlCommand("insert into finance_payments.payment_attempt(tenant_id,legal_entity_id,id,payment_request_id,attempt_number,provider_config_id,provider_operation_id,state,idempotency_key,created_at,updated_at) select $1,$2,$3,$4,$5,$6,$7::text,'Created',$8,$9,$9 from finance_payments.payment_request where tenant_id=$1 and legal_entity_id=$2 and id=$4", context.Connection, context.Transaction))
            { Add(attempt, tenant.Value, legalEntityId, attemptIdNew, requestId, attemptNumber + 1, providerConfigId, operationId, idempotencyKey.Trim(), now); if (await attempt.ExecuteNonQueryAsync(token) != 1) throw new PaymentException("payments.attempt_create_failed", "A new provider attempt could not be created."); }
            await using (var operation = new NpgsqlCommand("insert into finance_payments.provider_operation(tenant_id,legal_entity_id,id,provider_config_id,operation_type,aggregate_type,aggregate_id,idempotency_key,request_hash,state,attempts,next_attempt_at,created_at,updated_at) values($1,$2,$3,$4,'Create','PaymentRequest',$5,$6,$7,'Pending',0,$8,$9,$9)", context.Connection, context.Transaction))
            { Add(operation, tenant.Value, legalEntityId, operationId, providerConfigId, requestId, idempotencyKey.Trim(), hash, now, now); await operation.ExecuteNonQueryAsync(token); }
            await using (var updateRequest = new NpgsqlCommand("update finance_payments.payment_request set workflow_state='Pending',updated_at=$4 where tenant_id=$1 and legal_entity_id=$2 and id=$3", context.Connection, context.Transaction))
            { Add(updateRequest, tenant.Value, legalEntityId, requestId, now); await updateRequest.ExecuteNonQueryAsync(token); }
            await using (var idem = new NpgsqlCommand("update finance_payments.payment_idempotency set result_type='PaymentAttempt',result_id=$4 where tenant_id=$1 and scope='retry-payment-intent' and idempotency_key=$2 and request_hash=$3", context.Connection, context.Transaction))
            { Add(idem, tenant.Value, idempotencyKey.Trim(), requestHash, attemptIdNew); await idem.ExecuteNonQueryAsync(token); }
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new(Guid.CreateVersion7(), tenant, legalEntityId, null,
                "User", actor, membership, "payments", "payment_request.attempt_retried", "payment_request", requestId, null,
                JsonSerializer.Serialize(new { attemptNumber = attemptNumber + 1 }, JsonOptions), null, "api", correlationId, null, now, null), token);
            context.AddOutbox(new(Guid.CreateVersion7(), "PaymentProviderOperationRequestedV1", JsonSerializer.Serialize(new { operationId, requestId, attemptId = attemptIdNew, operation = "Create" }, JsonOptions), tenant, now, correlationId, requestId, "payments"));
            var requestDetails = await GetPaymentIntentInTransactionAsync(context, tenant, legalEntityId, requestId, token)
                ?? throw new PaymentException("payments.request_not_found", "The payment request was not found.");
            return requestDetails with { State = "Pending", AttemptId = attemptIdNew, AttemptNumber = attemptNumber + 1 };
        }, ct);

    private static async Task<PaymentIntentResult?> GetPaymentIntentAttemptAsync(ITransactionContext context, TenantId tenant, Guid legalEntityId, Guid attemptId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select request.id,request.legal_entity_id,request.merchant_reference,request.amount,request.currency_code,request.workflow_state,attempt.id,attempt.attempt_number from finance_payments.payment_attempt attempt join finance_payments.payment_request request on request.tenant_id=attempt.tenant_id and request.legal_entity_id=attempt.legal_entity_id and request.id=attempt.payment_request_id where attempt.tenant_id=$1 and attempt.legal_entity_id=$2 and attempt.id=$3", context.Connection, context.Transaction);
        Add(command, tenant.Value, legalEntityId, attemptId); await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetDecimal(3), reader.GetString(4).Trim(), reader.GetString(5), reader.GetGuid(6), reader.GetInt32(7)) : null;
    }

    public async Task<ProviderPaymentOperation?> GetReferenceProviderOperationAsync(TenantId tenant, Guid operationId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("""
            select operation.id, request.id, attempt.id, request.legal_entity_id, request.branch_id, request.party_id,
                   config.payment_account_id, request.created_by,
                   coalesce(request.created_by_membership_id,(select membership.id from platform.membership membership
                       where membership.tenant_id=request.tenant_id and membership.identity_id=request.created_by and membership.status='Active'
                       order by membership.id limit 1)), request.amount,
                   request.currency_code, request.merchant_reference, operation.state, attempt.provider_transaction_id, operation.idempotency_key, config.capabilities
            from finance_payments.provider_operation operation
            join finance_payments.payment_request request on request.tenant_id=operation.tenant_id
                and request.legal_entity_id=operation.legal_entity_id and request.id=operation.aggregate_id
            join finance_payments.provider_config config on config.tenant_id=operation.tenant_id
                and config.legal_entity_id=operation.legal_entity_id and config.id=operation.provider_config_id
            join lateral (select * from finance_payments.payment_attempt candidate
                where candidate.tenant_id=request.tenant_id and candidate.legal_entity_id=request.legal_entity_id
                  and candidate.payment_request_id=request.id
                  and (candidate.provider_operation_id=operation.id::text or (candidate.provider_operation_id is null and candidate.attempt_number=1))
                order by candidate.attempt_number desc limit 1) attempt on true
            where operation.tenant_id=$1 and operation.id=$2 and operation.operation_type='Create'
              and operation.aggregate_type='PaymentRequest' and config.provider_code='reference' and config.environment='Sandbox'
            """, connection);
        Add(command, tenant.Value, operationId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var capabilityDocument = reader.GetFieldValue<System.Text.Json.JsonDocument>(15);
        var capabilities = System.Text.Json.JsonSerializer.Deserialize<ProviderOperationCapabilities>(capabilityDocument.RootElement.GetRawText(), JsonOptions)
            ?? throw new PaymentException("payments.provider_capabilities_invalid", "The provider capability matrix is invalid.");
        return new ProviderPaymentOperation(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetGuid(3), GuidValue(reader, 4),
                GuidValue(reader, 5), reader.GetGuid(6), reader.GetGuid(7), GuidValue(reader, 8), reader.GetDecimal(9), reader.GetString(10).Trim(),
                reader.GetString(11), reader.GetString(12), StringValue(reader, 13), reader.GetString(14), capabilities);
    }

    public Task RecordReferenceProviderResultAsync(TenantId tenant, ProviderPaymentOperation operation, ReferenceProviderResult result,
        CancellationToken ct) => transactions.ExecuteAsync(tenant, async (context, token) =>
    {
        if (result.Status is not ("Unknown" or "RequiresAction" or "Authorized" or "Succeeded" or "Failed" or "Cancelled"))
            throw new PaymentException("payments.provider_result_invalid", "The reference provider returned an unsupported request state.");
        if (result.Status is not ("Unknown" or "Cancelled") && string.IsNullOrWhiteSpace(result.TransactionId))
            throw new PaymentException("payments.provider_result_invalid", "The provider returned a financial state without a transaction reference.");
        var attemptState = result.Status switch { "Succeeded" => "Succeeded", "Failed" => "Failed", "Cancelled" => "Cancelled", "Authorized" => "Authorized", "RequiresAction" => "RequiresAction", _ => "Unknown" };
        var operationState = result.Status == "Unknown" ? "Unknown" : result.Status == "Failed" ? "Failed" : "Succeeded";
        var requestState = result.Status switch { "Unknown" => "Pending", "Failed" => "Failed", "Cancelled" => "Cancelled", _ => "RequiresAction" };
        await using (var updateAttempt = new NpgsqlCommand("update finance_payments.payment_attempt set state=$7,provider_state=$7,provider_transaction_id=coalesce(nullif($4::text,''),provider_transaction_id),updated_at=$5 where tenant_id=$1 and legal_entity_id=$2 and payment_request_id=$3 and id=$6 and (provider_transaction_id is null or provider_transaction_id=nullif($4::text,'')) and state in ('Created','RequiresAction','Authorized','Succeeded','Unknown','Failed','Cancelled')", context.Connection, context.Transaction))
        {
            Add(updateAttempt, tenant.Value, operation.LegalEntityId, operation.RequestId, result.TransactionId, result.ConfirmedAt, operation.AttemptId, attemptState);
            if (await updateAttempt.ExecuteNonQueryAsync(token) != 1) throw new PaymentException("payments.provider_attempt_conflict", "The provider attempt already has a different transaction result.");
        }
        await using var updateOperation = new NpgsqlCommand("update finance_payments.provider_operation set state=$4,attempts=attempts+1,last_error_code=case when $4='Failed' then 'ProviderDeclined' when $4='Unknown' then 'ProviderStatusUnknown' else null end,updated_at=$5 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and state in ('Pending','InProgress','Unknown','Succeeded','Failed')", context.Connection, context.Transaction);
        Add(updateOperation, tenant.Value, operation.LegalEntityId, operation.OperationId, operationState, result.ConfirmedAt);
        if (await updateOperation.ExecuteNonQueryAsync(token) != 1) throw new PaymentException("payments.provider_operation_state_invalid", "The provider operation is no longer eligible for completion.");
        await using var updateRequest = new NpgsqlCommand("update finance_payments.payment_request set workflow_state=$4,updated_at=$5 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and workflow_state in ('Pending','RequiresAction','Failed','Cancelled') and not exists(select 1 from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and source_type='PaymentRequest' and source_reference=$3::text and business_state='Completed')", context.Connection, context.Transaction);
        Add(updateRequest, tenant.Value, operation.LegalEntityId, operation.RequestId, requestState, result.ConfirmedAt);
        if (await updateRequest.ExecuteNonQueryAsync(token) != 1) throw new PaymentException("payments.request_state_invalid", "The payment request cannot move to provider authorization.");
        if (requestState == "Cancelled")
        {
            await using var cancelLinks = new NpgsqlCommand("update finance_payments.payment_link set status='Cancelled',cancelled_at=$4 where tenant_id=$1 and legal_entity_id=$2 and payment_request_id=$3 and status='Active'", context.Connection, context.Transaction);
            Add(cancelLinks, tenant.Value, operation.LegalEntityId, operation.RequestId, result.ConfirmedAt); await cancelLinks.ExecuteNonQueryAsync(token);
        }
        return true;
    }, ct);

    public async Task<Guid?> GetLatestReferenceProviderOperationIdAsync(TenantId tenant, Guid legalEntityId, Guid requestId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select nullif(attempt.provider_operation_id,'') from finance_payments.payment_attempt attempt where attempt.tenant_id=$1 and attempt.legal_entity_id=$2 and attempt.payment_request_id=$3 order by attempt.attempt_number desc limit 1", connection);
        Add(command, tenant.Value, legalEntityId, requestId); var value = (string?)await command.ExecuteScalarAsync(ct);
        if (Guid.TryParse(value, out var operationId)) return operationId;
        await using var fallback = new NpgsqlCommand("select id from finance_payments.provider_operation where tenant_id=$1 and legal_entity_id=$2 and aggregate_type='PaymentRequest' and aggregate_id=$3 and operation_type='Create' order by created_at desc limit 1", connection);
        Add(fallback, tenant.Value, legalEntityId, requestId); var found = await fallback.ExecuteScalarAsync(ct);
        return found is Guid id ? id : null;
    }

    public async Task<PaymentIntentResult?> GetPaymentIntentAsync(TenantId tenant, Guid legalEntityId, Guid requestId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select id,legal_entity_id,merchant_reference,amount,currency_code,workflow_state from finance_payments.payment_request where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection);
        Add(command, tenant.Value, legalEntityId, requestId); await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var result = new PaymentIntentResult(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetDecimal(3), reader.GetString(4).Trim(), reader.GetString(5), Guid.Empty, 0);
        await reader.CloseAsync();
        await using var attempt = new NpgsqlCommand("select id,attempt_number from finance_payments.payment_attempt where tenant_id=$1 and legal_entity_id=$2 and payment_request_id=$3 order by attempt_number desc limit 1", connection);
        Add(attempt, tenant.Value, legalEntityId, requestId); await using var attemptReader = await attempt.ExecuteReaderAsync(ct);
        return await attemptReader.ReadAsync(ct) ? result with { AttemptId = attemptReader.GetGuid(0), AttemptNumber = attemptReader.GetInt32(1) } : result;
    }

    public async Task<PaymentRequestDetails?> GetPaymentRequestDetailsAsync(TenantId tenant, Guid legalEntityId, Guid requestId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        DateTimeOffset? expiresAt;
        PaymentIntentResult request;
        await using (var command = new NpgsqlCommand("select id,legal_entity_id,merchant_reference,amount,currency_code,workflow_state,expires_at from finance_payments.payment_request where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection))
        {
            Add(command, tenant.Value, legalEntityId, requestId); await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;
            expiresAt = reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6);
            request = new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetDecimal(3), reader.GetString(4).Trim(), reader.GetString(5), Guid.Empty, 0);
        }
        var attempts = new List<PaymentAttemptReadModel>();
        await using (var command = new NpgsqlCommand("select id,attempt_number,state,provider_state,failure_code,failure_message_safe,created_at,updated_at from finance_payments.payment_attempt where tenant_id=$1 and legal_entity_id=$2 and payment_request_id=$3 order by attempt_number desc limit 20", connection))
        {
            Add(command, tenant.Value, legalEntityId, requestId); await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) attempts.Add(new(reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetFieldValue<DateTimeOffset>(6), reader.GetFieldValue<DateTimeOffset>(7)));
        }
        if (attempts.Count > 0) request = request with { AttemptId = attempts[0].Id, AttemptNumber = attempts[0].AttemptNumber };
        return new(request, attempts, expiresAt);
    }

    public async Task<PaymentRequestPage> ListPaymentRequestsAsync(TenantId tenant, PaymentRequestListQuery query, CancellationToken ct)
    {
        var page = Math.Clamp(query.Page, 1, 1_000_000); var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var state = string.IsNullOrWhiteSpace(query.State) ? null : query.State.Trim();
        if (state is not null && state is not ("Created" or "Pending" or "RequiresAction" or "Succeeded" or "Failed" or "Cancelled" or "Expired"))
            throw new PaymentException("payments.request_state_invalid", "Choose a valid payment request status.");
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : $"{query.Search.Trim().ToLowerInvariant()}%";
        await using var connection = await OpenAsync(tenant, ct);
        await using var countCommand = new NpgsqlCommand("select count(*) from finance_payments.payment_request request where request.tenant_id=$1 and request.legal_entity_id=$2 and ($3::text is null or request.workflow_state=$3) and ($4::text is null or lower(request.merchant_reference) like $4)", connection);
        Add(countCommand, tenant.Value, query.LegalEntityId, Db(state), Db(search));
        var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
        await using var command = new NpgsqlCommand("""
            select request.id,request.legal_entity_id,request.merchant_reference,request.amount,request.currency_code,request.workflow_state,
                   coalesce(attempt.id,'00000000-0000-0000-0000-000000000000'::uuid),coalesce(attempt.attempt_number,0)
            from finance_payments.payment_request request
            left join lateral (select id,attempt_number from finance_payments.payment_attempt where tenant_id=request.tenant_id and legal_entity_id=request.legal_entity_id and payment_request_id=request.id order by attempt_number desc limit 1) attempt on true
            where request.tenant_id=$1 and request.legal_entity_id=$2 and ($3::text is null or request.workflow_state=$3)
              and ($4::text is null or lower(request.merchant_reference) like $4)
            order by request.created_at desc,request.id desc limit $5 offset $6
            """, connection);
        Add(command, tenant.Value, query.LegalEntityId, Db(state), Db(search), pageSize + 1, checked((page - 1) * pageSize));
        var items = new List<PaymentIntentResult>(pageSize + 1); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetDecimal(3),
                reader.GetString(4).Trim(), reader.GetString(5), reader.GetGuid(6), reader.GetInt32(7)));
        }
        var hasMore = items.Count > pageSize; if (hasMore) items.RemoveAt(items.Count - 1);
        return new(items, page, pageSize, hasMore, total);
    }

    public async Task<Guid?> GetReferencePaymentRequestIdAsync(TenantId tenant, Guid providerConfigId, string merchantReference, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select id from finance_payments.payment_request where tenant_id=$1 and provider_config_id=$2 and merchant_reference=$3", connection);
        Add(command, tenant.Value, providerConfigId, merchantReference); var value = await command.ExecuteScalarAsync(ct);
        return value is Guid id ? id : null;
    }

    public Task MarkReferenceOperationWebhooksProcessedAsync(TenantId tenant, ProviderPaymentOperation operation, CancellationToken ct) => transactions.ExecuteAsync(tenant, async (context, token) =>
    {
        await using var command = new NpgsqlCommand("update finance_payments.provider_webhook_inbox inbox set processing_state='Processed',processed_at=coalesce(processed_at,$4) from finance_payments.payment_attempt attempt where inbox.tenant_id=$1 and inbox.legal_entity_id=$2 and attempt.tenant_id=inbox.tenant_id and attempt.legal_entity_id=inbox.legal_entity_id and attempt.id=$3 and attempt.provider_transaction_id=inbox.object_reference and inbox.provider_config_id=attempt.provider_config_id and inbox.processing_state in ('Received','Processing')", context.Connection, context.Transaction);
        Add(command, tenant.Value, operation.LegalEntityId, operation.AttemptId, clock.GetUtcNow()); await command.ExecuteNonQueryAsync(token);
        return true;
    }, ct);

    public async Task<string?> GetReferenceProviderSecretRefAsync(TenantId tenant, Guid providerConfigId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select secret_ref from finance_payments.provider_config where tenant_id=$1 and id=$2 and provider_code='reference' and environment='Sandbox' and status='Active'", connection);
        Add(command, tenant.Value, providerConfigId);
        return (string?)await command.ExecuteScalarAsync(ct);
    }

    public Task<ProviderWebhookReceipt> AcceptReferenceWebhookAsync(TenantId tenant, Guid providerConfigId, ReferenceWebhookEnvelope webhook,
        byte[] encryptedPayload, string payloadHash, CancellationToken ct) => transactions.ExecuteAsync(tenant, async (context, token) =>
    {
        Guid legalEntityId;
        await using (var config = new NpgsqlCommand("select legal_entity_id,capabilities from finance_payments.provider_config where tenant_id=$1 and id=$2 and provider_code='reference' and environment='Sandbox' and status='Active' for share", context.Connection, context.Transaction))
        {
            Add(config, tenant.Value, providerConfigId); await using var reader = await config.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new PaymentException("payments.provider_config_unavailable", "The reference provider configuration is unavailable.");
            legalEntityId = reader.GetGuid(0);
            var capabilities = reader.GetFieldValue<System.Text.Json.JsonDocument>(1);
            if (!capabilities.RootElement.TryGetProperty("webhooks", out var webhooksEnabled) || webhooksEnabled.ValueKind != System.Text.Json.JsonValueKind.True)
                throw new PaymentException("payments.provider_webhooks_unsupported", "This provider is not configured to accept webhook events.");
        }
        Guid requestId;
        await using (var request = new NpgsqlCommand("select id from finance_payments.payment_request where tenant_id=$1 and legal_entity_id=$2 and provider_config_id=$3 and merchant_reference=$4 for update", context.Connection, context.Transaction))
        {
            Add(request, tenant.Value, legalEntityId, providerConfigId, webhook.MerchantReference);
            var id = await request.ExecuteScalarAsync(token);
            requestId = id is Guid found ? found : throw new PaymentException("payments.provider_request_unavailable", "The provider payment request is unavailable.");
        }
        var inboxId = Guid.CreateVersion7(); var now = clock.GetUtcNow();
        await using (var insert = new NpgsqlCommand("insert into finance_payments.provider_webhook_inbox(tenant_id,legal_entity_id,id,provider_config_id,provider_event_id,object_reference,provider_api_version,event_type,occurred_at,received_at,payload_sha256,encrypted_payload,signature_verified,processing_state) select $1,$2,$3,$4,$5,$6,config.api_version,$7,$8,$9,$10,$11,true,'Received' from finance_payments.provider_config config where config.tenant_id=$1 and config.legal_entity_id=$2 and config.id=$4 on conflict(tenant_id,provider_config_id,provider_event_id,object_reference) do nothing returning id", context.Connection, context.Transaction))
        {
            Add(insert, tenant.Value, legalEntityId, inboxId, providerConfigId, webhook.EventId, webhook.TransactionId, webhook.EventType,
                webhook.OccurredAt, now, payloadHash, encryptedPayload);
            var inserted = await insert.ExecuteScalarAsync(token);
            if (inserted is not Guid accepted)
            {
                await using var existing = new NpgsqlCommand("select id,processing_state from finance_payments.provider_webhook_inbox where tenant_id=$1 and provider_config_id=$2 and provider_event_id=$3 and object_reference=$4", context.Connection, context.Transaction);
                Add(existing, tenant.Value, providerConfigId, webhook.EventId, webhook.TransactionId);
                await using var reader = await existing.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new PaymentException("payments.webhook_dedupe_unavailable", "The provider event could not be resolved.");
                var existingId = reader.GetGuid(0); var state = reader.GetString(1); await reader.CloseAsync();
                return new ProviderWebhookReceipt(existingId, state == "Processed" ? null : await FindCreateOperationAsync(context, tenant, legalEntityId, requestId, webhook.TransactionId, token), true, state != "Processed" && webhook.EventType == "payment.succeeded");
            }
        }
        var operationId = await FindCreateOperationAsync(context, tenant, legalEntityId, requestId, webhook.TransactionId, token);
        await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new(Guid.CreateVersion7(), tenant, legalEntityId, null,
            "System", null, null, "payments", "provider.webhook_received", "provider_webhook_inbox", inboxId, null,
            JsonSerializer.Serialize(new { webhook.EventId, webhook.EventType, webhook.OccurredAt, payloadSha256 = payloadHash }, JsonOptions),
            null, "provider_webhook", operationId, null, now, null), token);
        if (webhook.EventType != "payment.succeeded")
        {
            if (webhook.EventType == "payment.failed")
            {
                int? failedAttempt = null; string? providerOperationId = null;
                await using (var fail = new NpgsqlCommand("update finance_payments.payment_attempt set state='Failed',provider_state='Failed',failure_code='Declined',failure_message_safe='The provider declined this payment attempt.',updated_at=$5 where tenant_id=$1 and legal_entity_id=$2 and payment_request_id=$3 and provider_transaction_id=$4 and state in ('Created','RequiresAction','Unknown') returning attempt_number,provider_operation_id", context.Connection, context.Transaction))
                {
                    Add(fail, tenant.Value, legalEntityId, requestId, webhook.TransactionId, now); await using var reader = await fail.ExecuteReaderAsync(token);
                    if (await reader.ReadAsync(token)) { failedAttempt = reader.GetInt32(0); providerOperationId = StringValue(reader, 1); }
                }
                if (failedAttempt is { } failedNumber)
                {
                    if (Guid.TryParse(providerOperationId, out var failedOperationId))
                    {
                        await using var failOperation = new NpgsqlCommand("update finance_payments.provider_operation set state='Failed',attempts=attempts+1,last_error_code='ProviderDeclined',updated_at=$4 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and state in ('Pending','InProgress','Unknown','Succeeded')", context.Connection, context.Transaction);
                        Add(failOperation, tenant.Value, legalEntityId, failedOperationId, now); await failOperation.ExecuteNonQueryAsync(token);
                    }
                    await using var failRequest = new NpgsqlCommand("update finance_payments.payment_request set workflow_state='Failed',updated_at=$5 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and workflow_state in ('Pending','RequiresAction','Failed') and not exists(select 1 from finance_payments.payment_attempt newer where newer.tenant_id=$1 and newer.legal_entity_id=$2 and newer.payment_request_id=$3 and newer.attempt_number>$4 and newer.state not in ('Failed','Cancelled'))", context.Connection, context.Transaction);
                    Add(failRequest, tenant.Value, legalEntityId, requestId, failedNumber, now); await failRequest.ExecuteNonQueryAsync(token);
                }
            }
            await using var ignore = new NpgsqlCommand("update finance_payments.provider_webhook_inbox set processing_state='Processed',processed_at=$3 where tenant_id=$1 and id=$2", context.Connection, context.Transaction);
            Add(ignore, tenant.Value, inboxId, now); await ignore.ExecuteNonQueryAsync(token);
            return new ProviderWebhookReceipt(inboxId, null, false, false);
        }
        await using (var attempt = new NpgsqlCommand("update finance_payments.payment_attempt set state='Succeeded',provider_state='Succeeded',updated_at=$5 where tenant_id=$1 and legal_entity_id=$2 and payment_request_id=$3 and provider_transaction_id=$4 and state in ('RequiresAction','Succeeded','Unknown')", context.Connection, context.Transaction))
        {
            Add(attempt, tenant.Value, legalEntityId, requestId, webhook.TransactionId, now);
            if (await attempt.ExecuteNonQueryAsync(token) != 1) throw new PaymentException("payments.provider_event_stale", "The provider event does not match the active payment attempt.");
        }
        await using (var operation = new NpgsqlCommand("update finance_payments.provider_operation set state='Succeeded',updated_at=$4 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and state in ('Succeeded','Unknown','InProgress')", context.Connection, context.Transaction))
        { Add(operation, tenant.Value, legalEntityId, operationId, now); if (await operation.ExecuteNonQueryAsync(token) != 1) throw new PaymentException("payments.provider_operation_state_invalid", "The provider operation cannot accept this event."); }
        return new ProviderWebhookReceipt(inboxId, operationId, false, true);
    }, ct);

    public Task MarkReferenceWebhookProcessedAsync(TenantId tenant, Guid inboxId, CancellationToken ct) => transactions.ExecuteAsync(tenant, async (context, token) =>
    {
        await using var command = new NpgsqlCommand("update finance_payments.provider_webhook_inbox set processing_state='Processed',processed_at=coalesce(processed_at,$3) where tenant_id=$1 and id=$2 and processing_state in ('Received','Processing','Processed')", context.Connection, context.Transaction);
        Add(command, tenant.Value, inboxId, clock.GetUtcNow());
        if (await command.ExecuteNonQueryAsync(token) != 1) throw new PaymentException("payments.webhook_state_invalid", "The provider event is no longer eligible for processing.");
        return true;
    }, ct);

    private static async Task<Guid> FindCreateOperationAsync(ITransactionContext context, TenantId tenant, Guid legalEntityId, Guid requestId, string transactionId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select operation.id from finance_payments.payment_attempt attempt join finance_payments.provider_operation operation on operation.tenant_id=attempt.tenant_id and operation.legal_entity_id=attempt.legal_entity_id and (attempt.provider_operation_id=operation.id::text or (attempt.provider_operation_id is null and operation.operation_type='Create')) where attempt.tenant_id=$1 and attempt.legal_entity_id=$2 and attempt.payment_request_id=$3 and attempt.provider_transaction_id=$4 and operation.aggregate_type='PaymentRequest' and operation.aggregate_id=$3 and operation.operation_type='Create' order by attempt.attempt_number desc limit 1", context.Connection, context.Transaction);
        Add(command, tenant.Value, legalEntityId, requestId, transactionId);
        return (Guid?)await command.ExecuteScalarAsync(ct) ?? throw new PaymentException("payments.provider_operation_not_found", "The payment provider operation is unavailable.");
    }

    public Task MarkProviderRequestSucceededAsync(TenantId tenant, Guid legalEntityId, Guid requestId, DateTimeOffset completedAt,
        CancellationToken ct) => transactions.ExecuteAsync(tenant, async (context, token) =>
    {
        await using var updateRequest = new NpgsqlCommand("update finance_payments.payment_request set workflow_state='Succeeded',updated_at=$4 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and workflow_state in ('Pending','RequiresAction','Succeeded')", context.Connection, context.Transaction);
        Add(updateRequest, tenant.Value, legalEntityId, requestId, completedAt);
        if (await updateRequest.ExecuteNonQueryAsync(token) != 1) throw new PaymentException("payments.request_state_invalid", "The payment request cannot be completed from its current state.");
        await using var updateLink = new NpgsqlCommand("update finance_payments.payment_link set status='Completed' where tenant_id=$1 and legal_entity_id=$2 and payment_request_id=$3 and status='Active'", context.Connection, context.Transaction);
        Add(updateLink, tenant.Value, legalEntityId, requestId); await updateLink.ExecuteNonQueryAsync(token);
        return true;
    }, ct);

    public Task<PaymentProviderResult> CreateProviderConfigAsync(TenantId tenant, Guid actor, Guid membership, Guid correlationId,
        CreatePaymentProviderRequest request, CancellationToken ct) => transactions.ExecuteAsync(tenant, async (context, token) =>
    {
        var providerCode = request.ProviderCode.Trim(); var merchantReference = request.MerchantReference.Trim(); var apiVersion = request.ApiVersion.Trim();
        if (request.LegalEntityId == Guid.Empty || request.PaymentAccountId == Guid.Empty || providerCode.Length is < 1 or > 80
            || merchantReference.Length is < 1 or > 160 || apiVersion.Length is < 1 or > 80
            || request.Environment is not ("Sandbox" or "Production") || request.SupportedCurrencies.Count == 0
            || request.SupportedMethods.Count == 0)
            throw new PaymentException("payments.provider_config_invalid", "Provider configuration details are incomplete.");
        var currencies = request.SupportedCurrencies.Select(PaymentRules.NormalizeCurrency).Distinct(StringComparer.Ordinal).ToArray();
        var methods = request.SupportedMethods.Select(method => Enum.TryParse<PaymentMethod>(method, true, out var parsed) && Enum.IsDefined(parsed)
            ? parsed.ToString() : throw new PaymentException("payments.provider_method_invalid", "A supported payment method is invalid.")).Distinct(StringComparer.Ordinal).ToArray();
        if (request.SecretRef is { } secret && !secret.StartsWith("secret://", StringComparison.Ordinal) && !secret.StartsWith("vault://", StringComparison.Ordinal))
            throw new PaymentException("payments.secret_reference_invalid", "Use an approved secret reference; provider credentials cannot be stored here.");
        await using (var account = new NpgsqlCommand("select account_type,status from finance_payments.payment_account where tenant_id=$1 and legal_entity_id=$2 and id=$3 for share", context.Connection, context.Transaction))
        {
            Add(account, tenant.Value, request.LegalEntityId, request.PaymentAccountId); await using var reader = await account.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new PaymentException("payments.account_not_found", "The provider clearing account was not found.");
            if (reader.GetString(0) != "ProviderClearing" || reader.GetString(1) != "Active") throw new PaymentException("payments.provider_account_invalid", "Select an active provider clearing account.");
        }
        var id = Guid.CreateVersion7(); var now = clock.GetUtcNow(); var capabilities = JsonSerializer.Serialize(request.Capabilities, JsonOptions);
        await using (var insert = new NpgsqlCommand("insert into finance_payments.provider_config(tenant_id,legal_entity_id,id,provider_code,environment,merchant_reference,api_version,capabilities,supported_currencies,supported_methods,payment_account_id,secret_ref,status,version,created_at,updated_at) values($1,$2,$3,$4,$5,$6,$7,$8::jsonb,$9,$10,$11,$12,'Active',1,$13,$13)", context.Connection, context.Transaction))
        {
            Add(insert, tenant.Value, request.LegalEntityId, id, providerCode, request.Environment, merchantReference, apiVersion,
                capabilities, currencies, methods, request.PaymentAccountId, Db(request.SecretRef?.Trim()), now);
            await insert.ExecuteNonQueryAsync(token);
        }
        await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new(Guid.CreateVersion7(), tenant, request.LegalEntityId, null, "User", actor, membership,
            "payments", "provider_config.created", "provider_config", id, null,
            JsonSerializer.Serialize(new { providerCode, request.Environment, merchantReference, apiVersion, hasSecretReference = request.SecretRef is not null }, JsonOptions), null,
            "api", correlationId, null, now, null), token);
        return new PaymentProviderResult(id, request.LegalEntityId, providerCode, request.Environment, merchantReference, apiVersion,
            request.Capabilities, currencies, methods, request.PaymentAccountId, request.SecretRef is not null, "Active", 1);
    }, ct);

    public Task<PaymentFeeResult> AddFeeAsync(TenantId tenant, Guid actor, Guid membership, Guid correlationId, Guid legalEntityId,
        Guid paymentId, CreatePaymentFeeRequest request, string idempotencyKey, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            var amount = PaymentRules.ParseAmount(request.Amount); var currency = PaymentRules.NormalizeCurrency(request.CurrencyCode);
            if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Trim().Length > 200 || request.SourceReference?.Length > 240)
                throw new PaymentException("payments.fee_request_invalid", "Fee details are incomplete.");
            var hash = Hash(new { legalEntityId, paymentId, request.Type, amount, currency, request.SourceReference });
            await using (var reserve = new NpgsqlCommand("insert into finance_payments.payment_idempotency(tenant_id,id,scope,idempotency_key,request_hash,created_at) values($1,$2,'create-payment-fee',$3,$4,$5) on conflict(tenant_id,scope,idempotency_key) do nothing", context.Connection, context.Transaction))
            { Add(reserve, tenant.Value, Guid.CreateVersion7(), idempotencyKey.Trim(), hash, clock.GetUtcNow()); await reserve.ExecuteNonQueryAsync(token); }
            await using (var key = new NpgsqlCommand("select request_hash,result_id from finance_payments.payment_idempotency where tenant_id=$1 and scope='create-payment-fee' and idempotency_key=$2 for update", context.Connection, context.Transaction))
            {
                Add(key, tenant.Value, idempotencyKey.Trim()); await using var reader = await key.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token)) throw new PaymentException("payments.idempotency_unavailable", "The fee request could not be reserved.");
                var oldHash = reader.GetString(0).Trim(); var oldId = reader.IsDBNull(1) ? (Guid?)null : reader.GetGuid(1); await reader.CloseAsync();
                if (oldHash != hash) throw new PaymentException("payments.idempotency_conflict", "This fee key was already used for different details.");
                if (oldId is { } previousFeeId) return await ReadFeeAsync(context.Connection, context.Transaction, tenant, legalEntityId, previousFeeId, token)
                    ?? throw new PaymentException("payments.idempotency_result_missing", "The original fee is unavailable.");
            }
            PaymentRecord payment;
            await using (var load = new NpgsqlCommand("select id,legal_entity_id,payment_number,branch_id,direction,payment_type,party_id,amount,currency_code,method,payment_account_id,payment_date,posting_date,value_date,bank_reference,customer_reference,supplier_reference,source_type,source_reference,business_state,completed_at,row_version from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction))
            { Add(load, tenant.Value, legalEntityId, paymentId); await using var reader = await load.ExecuteReaderAsync(token); if (!await reader.ReadAsync(token)) throw new PaymentException("payments.payment_not_found", "The payment was not found."); payment = ReadPayment(reader); }
            if (payment.State is not (PaymentBusinessState.Draft or PaymentBusinessState.Pending)) throw new PaymentException("payments.fee_payment_immutable", "Fees can only be added before payment completion.");
            if (currency != payment.CurrencyCode) throw new PaymentException("payments.fee_currency_mismatch", "A payment fee must use the payment currency.");
            await using (var existing = new NpgsqlCommand("select coalesce(sum(amount),0) from finance_payments.payment_fee where tenant_id=$1 and legal_entity_id=$2 and payment_id=$3 and recognition_state='Pending'", context.Connection, context.Transaction))
            { Add(existing, tenant.Value, legalEntityId, paymentId); var total = (decimal)(await existing.ExecuteScalarAsync(token) ?? 0m); if (payment.Direction == PaymentDirection.Incoming && total + amount >= payment.Amount) throw new PaymentException("payments.fee_exceeds_gross", "The fee must be less than the incoming payment amount."); }
            var id = Guid.CreateVersion7(); var now = clock.GetUtcNow();
            await using (var insert = new NpgsqlCommand("insert into finance_payments.payment_fee(tenant_id,legal_entity_id,id,payment_id,fee_type,amount,currency_code,recognition_state,source_reference,created_at) values($1,$2,$3,$4,$5,$6,$7,'Pending',$8,$9)", context.Connection, context.Transaction))
            { Add(insert, tenant.Value, legalEntityId, id, paymentId, request.Type.ToString(), amount, currency, Db(Clean(request.SourceReference)), now); await insert.ExecuteNonQueryAsync(token); }
            await using (var update = new NpgsqlCommand("update finance_payments.payment_idempotency set result_type='PaymentFee',result_id=$4 where tenant_id=$1 and scope='create-payment-fee' and idempotency_key=$2 and request_hash=$3", context.Connection, context.Transaction))
            { Add(update, tenant.Value, idempotencyKey.Trim(), hash, id); await update.ExecuteNonQueryAsync(token); }
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new(Guid.CreateVersion7(), tenant, legalEntityId, payment.BranchId, "User", actor, membership,
                "payments", "payment.fee_added", "payment_fee", id, null, JsonSerializer.Serialize(new { payment.Number, type = request.Type.ToString(), amount, currency }, JsonOptions), null,
                "api", correlationId, null, now, null), token);
            return new PaymentFeeResult(id, request.Type, amount, currency, "Pending");
        }, ct);

    private static async Task<PaymentFeeResult?> ReadFeeAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, TenantId tenant, Guid entity, Guid feeId, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select id,fee_type,amount,currency_code,recognition_state from finance_payments.payment_fee where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection, transaction);
        Add(command, tenant.Value, entity, feeId); await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(reader.GetGuid(0), Enum.Parse<PaymentFeeType>(reader.GetString(1)), reader.GetDecimal(2), reader.GetString(3).Trim(), reader.GetString(4)) : null;
    }

    public Task<PaymentLinkResult> CreatePaymentLinkAsync(TenantId tenant, Guid legalEntityId, CreatePaymentLinkRequest request,
        CancellationToken ct) => transactions.ExecuteAsync(tenant, async (context, token) =>
    {
        var requestId = request.PaymentRequestId;
        var now = clock.GetUtcNow(); if (request.ExpiresAt <= now) throw new PaymentException("payments.link_expiry_invalid", "Choose a future expiry time.");
        await using var paymentRequest = new NpgsqlCommand("select provider_config_id,workflow_state,expires_at,amount from finance_payments.payment_request where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction);
        Add(paymentRequest, tenant.Value, legalEntityId, requestId); await using var reader = await paymentRequest.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) throw new PaymentException("payments.request_not_found", "The payment request was not found.");
        var providerId = reader.GetGuid(0); var state = reader.GetString(1); var requestExpiry = reader.IsDBNull(2) ? (DateTimeOffset?)null : reader.GetFieldValue<DateTimeOffset>(2);
        var requestAmount = reader.GetDecimal(3); await reader.CloseAsync();
        if (state is "Succeeded" or "Cancelled" or "Failed" || requestExpiry is { } requestLimit && requestLimit <= now)
            throw new PaymentException("payments.link_request_unavailable", "This payment request is no longer available for a payment link.");
        decimal? minimum = null; decimal? maximum = null;
        if (request.AmountPolicy == PaymentLinkAmountPolicy.Fixed)
        {
            if (request.MinimumAmount is not null || request.MaximumAmount is not null)
                throw new PaymentException("payments.link_amount_policy_invalid", "A fixed payment link does not accept amount limits.");
        }
        else
        {
            minimum = request.MinimumAmount is null ? null : PaymentRules.ParseAmount(request.MinimumAmount);
            maximum = request.MaximumAmount is null ? requestAmount : PaymentRules.ParseAmount(request.MaximumAmount);
            if (minimum is null || minimum > maximum || maximum > requestAmount)
                throw new PaymentException("payments.link_amount_policy_invalid", "Set valid payment limits within the requested amount.");
        }
        var tokenValue = $"{tenant.Value:N}.{Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()}";
        var tokenHash = Hash(new { tokenValue }); var id = Guid.CreateVersion7();
        var effectiveExpiry = requestExpiry is { } limit && limit < request.ExpiresAt ? limit : request.ExpiresAt;
        await using (var insert = new NpgsqlCommand("insert into finance_payments.payment_link(tenant_id,legal_entity_id,id,payment_request_id,provider_config_id,public_token_hash,amount_policy,minimum_amount,maximum_amount,status,expires_at,created_at) values($1,$2,$3,$4,$5,$6,$7,$8,$9,'Active',$10,$11)", context.Connection, context.Transaction))
        { Add(insert, tenant.Value, legalEntityId, id, requestId, providerId, tokenHash, request.AmountPolicy.ToString(), Db(minimum), Db(maximum), effectiveExpiry, now); await insert.ExecuteNonQueryAsync(token); }
        return new PaymentLinkResult(id, requestId, tokenValue, "Active", effectiveExpiry);
    }, ct);

    public async Task<PublicPaymentLinkDetails?> GetPublicPaymentLinkAsync(string publicToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(publicToken) || publicToken.Length != 97 || publicToken[32] != '.'
            || !Guid.TryParseExact(publicToken[..32], "N", out var tenantId)
            || !publicToken.AsSpan(33).ToArray().All(Uri.IsHexDigit)) return null;
        await using var connection = await OpenAsync(new TenantId(tenantId), ct);
        await using var command = new NpgsqlCommand("select r.amount,r.currency_code,l.amount_policy,l.minimum_amount,l.maximum_amount,l.status,l.expires_at from finance_payments.payment_link l join finance_payments.payment_request r on r.tenant_id=l.tenant_id and r.legal_entity_id=l.legal_entity_id and r.id=l.payment_request_id where l.public_token_hash=$1 and l.status='Active' and l.expires_at>now() and r.workflow_state not in ('Succeeded','Cancelled','Failed')", connection);
        Add(command, Hash(new { tokenValue = publicToken })); await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new PublicPaymentLinkDetails(reader.GetDecimal(0), reader.GetString(1).Trim(), reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetDecimal(3), reader.IsDBNull(4) ? null : reader.GetDecimal(4), reader.GetString(5), reader.GetFieldValue<DateTimeOffset>(6)) : null;
    }

    public Task CancelPaymentLinkAsync(TenantId tenant, Guid legalEntityId, Guid requestId, Guid linkId, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var link = new NpgsqlCommand("update finance_payments.payment_link set status='Cancelled',cancelled_at=$5 where tenant_id=$1 and legal_entity_id=$2 and payment_request_id=$3 and id=$4 and status='Active' and expires_at>=$5", context.Connection, context.Transaction);
            Add(link, tenant.Value, legalEntityId, requestId, linkId, clock.GetUtcNow());
            if (await link.ExecuteNonQueryAsync(token) != 1) throw new PaymentException("payments.link_not_cancellable", "This payment link is no longer active.");
            return true;
        }, ct);

    public Task<T> ApplyWhilePaymentLockedAsync<T>(TenantId tenant, Guid legalEntityId, Guid paymentId,
        Func<CancellationToken, Task<T>> apply, CancellationToken ct) => transactions.ExecuteAsync(tenant, async (context, token) =>
    {
        await using var payment = new NpgsqlCommand("select business_state from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and id=$3 for update", context.Connection, context.Transaction);
        Add(payment, tenant.Value, legalEntityId, paymentId);
        var state = (string?)await payment.ExecuteScalarAsync(token) ?? throw new PaymentException("payments.payment_not_found", "The payment was not found.");
        if (state != "Completed") throw new PaymentException("payments.payment_not_applicable", "Only a completed payment can be applied.");
        await using var refunds = new NpgsqlCommand("select exists(select 1 from finance_payments.payment_refund where tenant_id=$1 and legal_entity_id=$2 and original_payment_id=$3 and status in ('Pending','Completed'))", context.Connection, context.Transaction);
        Add(refunds, tenant.Value, legalEntityId, paymentId);
        if ((bool?)await refunds.ExecuteScalarAsync(token) == true)
            throw new PaymentException("payments.application_refund_in_progress", "This payment cannot be applied while a refund is reserved or has been completed.");
        return await apply(token);
    }, ct);

    public async Task<PaymentPage> ListAsync(TenantId tenant, PaymentListQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page); var pageSize = Math.Clamp(query.PageSize, 1, 100);
        await using var connection = await OpenAsync(tenant, ct);
        var branchFilter = query.BranchId is null ? string.Empty : " and p.branch_id=$3";
        var searchParameter = query.BranchId is null ? 3 : 4;
        var directionParameter = searchParameter + 1; var stateParameter = searchParameter + 2;
        var fromParameter = searchParameter + 3; var toParameter = searchParameter + 4;
        var offsetParameter = searchParameter + 5; var limitParameter = searchParameter + 6;
        var countSql = $"select count(*) from finance_payments.payment p left join finance_payments.payment_list_projection x on x.tenant_id=p.tenant_id and x.payment_id=p.id where p.tenant_id=$1 and p.legal_entity_id=$2{branchFilter} and (${searchParameter}::text is null or x.normalized_search like '%' || ${searchParameter} || '%') and (${directionParameter}::text is null or p.direction=${directionParameter}) and (${stateParameter}::text is null or p.business_state=${stateParameter}) and (${fromParameter}::date is null or p.payment_date >= ${fromParameter}) and (${toParameter}::date is null or p.payment_date <= ${toParameter})";
        await using var countCommand = new NpgsqlCommand(countSql, connection);
        Add(countCommand, tenant.Value, query.LegalEntityId); if (query.BranchId is { } countBranch) Add(countCommand, countBranch);
        Add(countCommand, NormalizeSearch(query.Search), query.Direction?.ToString(), query.State?.ToString(), Db(query.FromDate), Db(query.ToDate));
        var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
        var sql = $"""
            select p.id,p.legal_entity_id,p.payment_number,p.branch_id,p.direction,p.payment_type,p.party_id,p.amount,p.currency_code,p.method,p.payment_account_id,p.payment_date,p.posting_date,p.value_date,p.bank_reference,p.customer_reference,p.supplier_reference,p.source_type,p.source_reference,p.business_state,p.completed_at,p.row_version
            from finance_payments.payment p left join finance_payments.payment_list_projection x on x.tenant_id=p.tenant_id and x.payment_id=p.id
            where p.tenant_id=$1 and p.legal_entity_id=$2{branchFilter} and (${searchParameter}::text is null or x.normalized_search like '%' || ${searchParameter} || '%') and (${directionParameter}::text is null or p.direction=${directionParameter}) and (${stateParameter}::text is null or p.business_state=${stateParameter}) and (${fromParameter}::date is null or p.payment_date >= ${fromParameter}) and (${toParameter}::date is null or p.payment_date <= ${toParameter})
            order by p.payment_date desc,p.id desc offset ${offsetParameter} limit ${limitParameter}
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        Add(command, tenant.Value, query.LegalEntityId); if (query.BranchId is { } branch) Add(command, branch);
        Add(command, NormalizeSearch(query.Search), query.Direction?.ToString(), query.State?.ToString(), Db(query.FromDate), Db(query.ToDate), (page - 1) * pageSize, pageSize + 1);
        var result = new List<PaymentRecord>(); await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(ReadPayment(reader));
        var more = result.Count > pageSize; if (more) result.RemoveAt(pageSize);
        return new(result, page, pageSize, more, total);
    }

    public async Task<PaymentRecord?> GetAsync(TenantId tenant, Guid legalEntityId, Guid? branchId, Guid id, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        var branchFilter = branchId is null ? string.Empty : " and branch_id=$4";
        await using var command = new NpgsqlCommand($"select id,legal_entity_id,payment_number,branch_id,direction,payment_type,party_id,amount,currency_code,method,payment_account_id,payment_date,posting_date,value_date,bank_reference,customer_reference,supplier_reference,source_type,source_reference,business_state,completed_at,row_version from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and id=$3{branchFilter}", connection);
        Add(command, tenant.Value, legalEntityId, id); if (branchId is { } branch) Add(command, branch); await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadPayment(reader) : null;
    }

    public async Task<PosCapturedPaymentReference?> FindCapturedPaymentAsync(TenantId tenant, Guid legalEntityId, Guid paymentRequestId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select id,payment_account_id,amount,currency_code,method,business_state from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and source_type='PaymentRequest' and source_reference=$3 and business_state='Completed' order by completed_at desc limit 1", connection);
        Add(command, tenant.Value, legalEntityId, paymentRequestId.ToString("D")); await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? new(reader.GetGuid(0), reader.GetGuid(1), reader.GetDecimal(2), reader.GetString(3).Trim(), reader.GetString(4), reader.GetString(5)) : null;
    }

    public async Task<IReadOnlyList<PaymentFeeResult>> ListFeesAsync(TenantId tenant, Guid legalEntityId, Guid paymentId, CancellationToken ct)
    {
        await using var connection = await OpenAsync(tenant, ct);
        await using var command = new NpgsqlCommand("select id,fee_type,amount,currency_code,recognition_state from finance_payments.payment_fee where tenant_id=$1 and legal_entity_id=$2 and payment_id=$3 order by created_at,id limit 101", connection);
        Add(command, tenant.Value, legalEntityId, paymentId);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var fees = new List<PaymentFeeResult>();
        while (await reader.ReadAsync(ct)) fees.Add(new(reader.GetGuid(0), Enum.Parse<PaymentFeeType>(reader.GetString(1)), reader.GetDecimal(2), reader.GetString(3).Trim(), reader.GetString(4)));
        if (fees.Count > 100) throw new PaymentException("payments.fee_limit_exceeded", "This payment has more than 100 fees; contact Finance before changing its reversal.");
        return fees;
    }

    public Task<PaymentRecord> CompleteManualAsync(TenantId tenant, Guid actor, Guid membership, Guid legalEntityId, Guid paymentId,
        string idempotencyKey, FinancialCalculationContextSnapshotContract financialContext, Guid correlationId, CancellationToken ct) =>
        transactions.ExecuteAsync(tenant, async (context, token) =>
        {
            await using var load = new NpgsqlCommand("""
                select p.id,p.legal_entity_id,p.payment_number,p.branch_id,p.direction,p.payment_type,p.party_id,p.amount,p.currency_code,p.method,
                       p.payment_account_id,p.payment_date,p.posting_date,p.value_date,p.bank_reference,p.customer_reference,p.supplier_reference,
                       p.source_type,p.source_reference,p.business_state,p.completed_at,p.row_version,a.code,a.status,a.account_type
                from finance_payments.payment p join finance_payments.payment_account a
                  on a.tenant_id=p.tenant_id and a.legal_entity_id=p.legal_entity_id and a.id=p.payment_account_id
                where p.tenant_id=$1 and p.legal_entity_id=$2 and p.id=$3 for update of p,a
                """, context.Connection, context.Transaction);
            Add(load, tenant.Value, legalEntityId, paymentId); await using var reader = await load.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token)) throw new PaymentException("payments.payment_not_found", "The payment was not found.");
            var payment = ReadPayment(reader); var accountCode = reader.GetString(22); var accountStatus = reader.GetString(23); var accountType = reader.GetString(24); await reader.CloseAsync();
            if (payment.State == PaymentBusinessState.Completed) return payment;
            if (payment.Type == PaymentType.CustomerRefund)
            {
                await using var providerRefund = new NpgsqlCommand("select exists(select 1 from finance_payments.payment_refund refund join finance_payments.provider_operation operation on operation.tenant_id=refund.tenant_id and operation.legal_entity_id=refund.legal_entity_id and operation.aggregate_type='Refund' and operation.aggregate_id=refund.id and operation.operation_type='Refund' where refund.tenant_id=$1 and refund.legal_entity_id=$2 and refund.refund_payment_id=$3 and operation.state<>'Succeeded')", context.Connection, context.Transaction);
                Add(providerRefund, tenant.Value, legalEntityId, payment.Id);
                if ((bool?)await providerRefund.ExecuteScalarAsync(token) == true)
                    throw new PaymentException("payments.provider_refund_confirmation_required", "Wait for the provider to confirm this refund or cancel the provider request before recording a manual refund.");
            }
            if (payment.State is not (PaymentBusinessState.Draft or PaymentBusinessState.Pending)) throw new PaymentException("payments.payment_not_completable", "Only a draft or pending payment can be completed.");
            if (accountStatus != "Active") throw new PaymentException("payments.account_inactive", "A suspended or archived account cannot receive a payment.");
            var isPayrollSettlement = string.Equals(payment.SourceType, "Payroll", StringComparison.OrdinalIgnoreCase)
                && payment.Direction == PaymentDirection.Outgoing && payment.Type == PaymentType.ExpensePayment;
            var isExpenseSettlement = string.Equals(payment.SourceType, "Expenses", StringComparison.OrdinalIgnoreCase)
                && payment.Direction == PaymentDirection.Outgoing && payment.Type == PaymentType.ExpensePayment;
            var isAnonymousPosCash = string.Equals(payment.SourceType, "pos", StringComparison.OrdinalIgnoreCase)
                && payment.Type == PaymentType.CustomerReceipt && payment.Direction == PaymentDirection.Incoming && payment.PartyId is null;
            if (!isPayrollSettlement && !isExpenseSettlement && !isAnonymousPosCash && payment.PartyId is null) throw new PaymentException("payments.party_required", "A party is required until this payment type has an approved accounting mapping.");
            if (!isPayrollSettlement && !isExpenseSettlement && payment.Type is not (PaymentType.CustomerReceipt or PaymentType.CustomerAdvance or PaymentType.SupplierPayment or PaymentType.SupplierAdvance or PaymentType.CustomerRefund or PaymentType.SupplierRefund))
                throw new PaymentException("payments.manual_completion_type_unsupported", "This payment type requires a configured business workflow before completion.");
            if (!string.Equals(financialContext.TransactionCurrencyCode, payment.CurrencyCode, StringComparison.OrdinalIgnoreCase))
                throw new PaymentException("payments.currency_context_changed", "The approved currency context no longer matches this payment.");

            var payloadHash = Hash(new { payment.Id, payment.LegalEntityId, idempotencyKey = idempotencyKey.Trim() });
            await using (var reserve = new NpgsqlCommand("insert into finance_payments.payment_idempotency(tenant_id,id,scope,idempotency_key,request_hash,created_at) values($1,$2,'complete-payment',$3,$4,$5) on conflict(tenant_id,scope,idempotency_key) do nothing", context.Connection, context.Transaction))
            { Add(reserve, tenant.Value, Guid.CreateVersion7(), idempotencyKey.Trim(), payloadHash, clock.GetUtcNow()); await reserve.ExecuteNonQueryAsync(token); }
            await using (var lookup = new NpgsqlCommand("select request_hash,result_id from finance_payments.payment_idempotency where tenant_id=$1 and scope='complete-payment' and idempotency_key=$2 for update", context.Connection, context.Transaction))
            {
                Add(lookup, tenant.Value, idempotencyKey.Trim()); await using var prior = await lookup.ExecuteReaderAsync(token);
                if (!await prior.ReadAsync(token)) throw new PaymentException("payments.idempotency_unavailable", "The completion request could not be reserved.");
                var oldHash = prior.GetString(0).Trim(); var oldId = prior.IsDBNull(1) ? (Guid?)null : prior.GetGuid(1); await prior.CloseAsync();
                if (oldHash != payloadHash) throw new PaymentException("payments.idempotency_conflict", "This completion key was already used for another request.");
                if (oldId is { } resultId && resultId != payment.Id) throw new PaymentException("payments.idempotency_conflict", "This completion key was already used for another payment.");
            }

            var snapshot = financialContext.ExchangeRate;
            var rate = snapshot is null ? 1m : ParseRequired(snapshot.Rate);
            if (financialContext.BaseCurrencyCode != payment.CurrencyCode && snapshot is null) throw new PaymentException("payments.fx_rate_missing", "An approved exchange rate is required to complete this payment.");
            var rounding = financialContext.RoundingPolicy.ContextRules.SingleOrDefault(rule => rule.Context == FinancialRoundingContext.MoneySettlement)?.Mode
                ?? FinancialRoundingMode.HalfUp;
            var midpoint = rounding switch { FinancialRoundingMode.HalfEven => MidpointRounding.ToEven, FinancialRoundingMode.Down => MidpointRounding.ToZero, _ => MidpointRounding.AwayFromZero };
            var baseAmount = decimal.Round(payment.Amount * rate, financialContext.RoundingPolicy.MoneyScale, midpoint);
            if (baseAmount <= 0m) throw new PaymentException("payments.fx_snapshot_invalid", "The approved exchange rate produced an invalid base amount.");
            var pendingFees = new List<(Guid Id, string Type, decimal Amount)>();
            await using (var fees = new NpgsqlCommand("select id,fee_type,amount from finance_payments.payment_fee where tenant_id=$1 and legal_entity_id=$2 and payment_id=$3 and recognition_state='Pending' order by id", context.Connection, context.Transaction))
            {
                Add(fees, tenant.Value, legalEntityId, payment.Id);
                await using var feeRows = await fees.ExecuteReaderAsync(token);
                while (await feeRows.ReadAsync(token)) pendingFees.Add((feeRows.GetGuid(0), feeRows.GetString(1), feeRows.GetDecimal(2)));
            }
            var feeAmount = pendingFees.Sum(fee => fee.Amount);
            if (payment.Direction == PaymentDirection.Incoming && feeAmount >= payment.Amount) throw new PaymentException("payments.fee_exceeds_gross", "Payment fees must be less than the incoming amount.");
            var baseFeeAmount = feeAmount == 0m ? 0m : decimal.Round(feeAmount * rate, financialContext.RoundingPolicy.MoneyScale, midpoint);
            if (feeAmount > 0m && baseFeeAmount <= 0m) throw new PaymentException("payments.fee_amount_invalid", "The approved exchange rate produced an invalid fee amount.");
            var configurationId = isAnonymousPosCash ? Guid.Empty : await ledgerConfiguration.GetActiveConfigurationVersionIdAsync(context, tenant, new(legalEntityId), token);
            var settlementRole = isPayrollSettlement ? "PayrollPayable" : isExpenseSettlement ? "EmployeeReimbursementPayable" : payment.Type switch
            {
                PaymentType.CustomerReceipt or PaymentType.CustomerAdvance or PaymentType.CustomerRefund => "Receivable",
                PaymentType.SupplierPayment or PaymentType.SupplierAdvance or PaymentType.SupplierRefund => "Payable",
                _ => throw new PaymentException("payments.manual_completion_type_unsupported", "This payment type requires a configured business workflow before completion.")
            };
            var snapshotJson = snapshot is null ? null : JsonSerializer.Serialize(snapshot, JsonOptions);
            var openItem = new OpenItemSnapshotContract(payment.Id.ToString("D"), null, payment.Number, payment.PaymentDate, payment.PostingDate,
                payment.PostingDate, payment.CurrencyCode, payment.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture), financialContext.BaseCurrencyCode,
                baseAmount.ToString(System.Globalization.CultureInfo.InvariantCulture), settlementRole, $"role:{settlementRole}", $"configuration:{configurationId:D}", snapshotJson, 1);
            Guid? itemId = null;
            if (!isPayrollSettlement && !isExpenseSettlement && !isAnonymousPosCash)
            {
                var partyDirection = settlementRole == "Receivable" ? SubledgerDirectionContract.Receivable : SubledgerDirectionContract.Payable;
                var partyPolarity = payment.Type switch
                {
                    PaymentType.CustomerReceipt or PaymentType.CustomerAdvance or PaymentType.SupplierRefund => SubledgerPolarityContract.Credit,
                    PaymentType.SupplierPayment or PaymentType.SupplierAdvance or PaymentType.CustomerRefund => SubledgerPolarityContract.Debit,
                    _ => throw new PaymentException("payments.manual_completion_type_unsupported", "This payment type requires a configured business workflow before completion.")
                };
                itemId = await subledger.CreatePaymentOpenItemAsync(context, tenant, new(new(legalEntityId), payment.PartyId!.Value, [openItem], actor, correlationId), partyDirection, partyPolarity, token);
            }
            if (isAnonymousPosCash && feeAmount > 0m)
                throw new PaymentException("payments.anonymous_cash_fees_unsupported", "Anonymous POS cash receipts cannot contain payment fees.");
            if (!isAnonymousPosCash)
            {
                var factType = payment.Type == PaymentType.CustomerRefund ? AccountingFactTypes.RefundPaid
                    : payment.Direction == PaymentDirection.Incoming ? AccountingFactTypes.PaymentReceived : AccountingFactTypes.PaymentMade;
                var factPayload = new PaymentFactPayload(payment.Amount, payment.CurrencyCode, "Default", settlementRole, payment.Direction.ToString(),
                    accountCode, payment.PartyId, baseAmount, financialContext.BaseCurrencyCode, snapshot?.ApprovedRateId?.ToString("D") ?? (snapshot is null ? "base-currency" : snapshot.Rate),
                    feeAmount, baseFeeAmount);
                var fact = new AccountingFactEnvelope<IAccountingFactPayload>(payment.Id, tenant, new(legalEntityId), factType, 1, "payments", payment.Id.ToString("D"),
                    $"payment:{payment.Id:D}", payment.PaymentDate, payment.PostingDate, factPayload, correlationId, actor);
                await ledger.PostPaymentAsync(context, fact, factPayload, token);
                foreach (var fee in pendingFees)
                {
                    var feeBaseAmount = decimal.Round(fee.Amount * rate, financialContext.RoundingPolicy.MoneyScale, midpoint);
                    if (feeBaseAmount <= 0m) throw new PaymentException("payments.fee_amount_invalid", "The approved exchange rate produced an invalid fee amount.");
                    var feePayload = new PaymentFeeFactPayload(fee.Amount, payment.CurrencyCode, feeBaseAmount, financialContext.BaseCurrencyCode,
                        accountCode, fee.Type, snapshot?.ApprovedRateId?.ToString("D") ?? (snapshot is null ? "base-currency" : snapshot.Rate));
                    var feeFact = new AccountingFactEnvelope<IAccountingFactPayload>(fee.Id, tenant, new(legalEntityId), AccountingFactTypes.PaymentFeeRecognized, 1,
                        "payments", $"payment-fee:{fee.Id:D}", $"payment-fee:{fee.Id:D}", payment.PaymentDate, payment.PostingDate, feePayload, correlationId, actor);
                    await ledger.PostPaymentFeeAsync(context, feeFact, feePayload, token);
                }
                if (feeAmount > 0m)
                {
                    await using var recognizeFees = new NpgsqlCommand("update finance_payments.payment_fee set recognition_state='Recognized' where tenant_id=$1 and legal_entity_id=$2 and payment_id=$3 and recognition_state='Pending'", context.Connection, context.Transaction);
                    Add(recognizeFees, tenant.Value, legalEntityId, payment.Id);
                    if (await recognizeFees.ExecuteNonQueryAsync(token) == 0) throw new PaymentException("payments.fee_state_invalid", "Payment fee recognition could not be recorded.");
                }
            }
            var completedAt = clock.GetUtcNow();
            await using (var update = new NpgsqlCommand("update finance_payments.payment set business_state='Completed',completed_at=$4,completed_by=$5,subledger_item_id=$6,accounting_fact_id=$7,row_version=row_version+1,updated_at=$4 where tenant_id=$1 and legal_entity_id=$2 and id=$3 and business_state in ('Draft','Pending')", context.Connection, context.Transaction))
            { Add(update, tenant.Value, legalEntityId, payment.Id, completedAt, actor, payment.PartyId is null ? DBNull.Value : itemId, isAnonymousPosCash ? DBNull.Value : payment.Id); if (await update.ExecuteNonQueryAsync(token) != 1) throw new PaymentException("payments.payment_state_conflict", "The payment changed before completion."); }
            await using (var updateKey = new NpgsqlCommand("update finance_payments.payment_idempotency set result_type='Payment',result_id=$4 where tenant_id=$1 and scope='complete-payment' and idempotency_key=$2 and request_hash=$3", context.Connection, context.Transaction))
            { Add(updateKey, tenant.Value, idempotencyKey.Trim(), payloadHash, payment.Id); await updateKey.ExecuteNonQueryAsync(token); }
            await using (var projection = new NpgsqlCommand("update finance_payments.payment_list_projection set business_state='Completed',updated_at=$4 where tenant_id=$1 and legal_entity_id=$2 and payment_id=$3", context.Connection, context.Transaction))
            { Add(projection, tenant.Value, legalEntityId, payment.Id, completedAt); await projection.ExecuteNonQueryAsync(token); }
            if (payment.Type == PaymentType.CustomerRefund)
            {
                await using var updateRefund = new NpgsqlCommand("update finance_payments.payment_refund set status='Completed',completed_at=$4 where tenant_id=$1 and legal_entity_id=$2 and refund_payment_id=$3 and status='Pending'", context.Connection, context.Transaction);
                Add(updateRefund, tenant.Value, legalEntityId, payment.Id, completedAt);
                if (await updateRefund.ExecuteNonQueryAsync(token) != 1) throw new PaymentException("payments.refund_state_invalid", "The refund request is not ready to complete.");
            }
            var eventType = payment.Type == PaymentType.CustomerRefund ? "PaymentRefundedV1" : "PaymentCompletedV1";
            context.AddOutbox(new(Guid.CreateVersion7(), eventType, JsonSerializer.Serialize(new { paymentId = payment.Id, payment.LegalEntityId, payment.Number, payment.Direction, payment.Amount, payment.CurrencyCode, payment.PartyId, payment.SourceType, payment.SourceReference }, JsonOptions), tenant, completedAt, correlationId, payment.Id, "payments"));
            await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new(Guid.CreateVersion7(), tenant, legalEntityId, payment.BranchId, "User", actor, membership, "payments", "payment.completed", "payment", payment.Id, null, JsonSerializer.Serialize(new { payment.Number, payment.Amount, payment.CurrencyCode, payment.Direction, accountType }, JsonOptions), null, "api", correlationId, null, completedAt, null), token);
            return payment with { State = PaymentBusinessState.Completed, CompletedAt = completedAt, Version = payment.Version + 1 };
        }, ct);

    public Task<PaymentRecord> CancelPendingAsync(TenantId tenant, Guid actor, Guid membership, Guid legalEntityId, Guid? branchId, Guid paymentId,
        string idempotencyKey, Guid correlationId, CancellationToken ct) => transactions.ExecuteAsync(tenant, async (context, token) =>
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Trim().Length > 200)
            throw new PaymentException("payments.idempotency_key_required", "A valid idempotency key is required.");
        var branchFilter = branchId is null ? string.Empty : " and branch_id=$4";
        await using var load = new NpgsqlCommand($"select id,legal_entity_id,payment_number,branch_id,direction,payment_type,party_id,amount,currency_code,method,payment_account_id,payment_date,posting_date,value_date,bank_reference,customer_reference,supplier_reference,source_type,source_reference,business_state,completed_at,row_version from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and id=$3{branchFilter} for update", context.Connection, context.Transaction);
        Add(load, tenant.Value, legalEntityId, paymentId); if (branchId is { } branch) Add(load, branch); await using var reader = await load.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) throw new PaymentException("payments.payment_not_found", "The payment was not found.");
        var payment = ReadPayment(reader); await reader.CloseAsync();
        var payloadHash = Hash(new { payment.Id, payment.LegalEntityId, operation = "cancel" });
        await using (var reserve = new NpgsqlCommand("insert into finance_payments.payment_idempotency(tenant_id,id,scope,idempotency_key,request_hash,created_at) values($1,$2,'cancel-payment',$3,$4,$5) on conflict(tenant_id,scope,idempotency_key) do nothing", context.Connection, context.Transaction))
        { Add(reserve, tenant.Value, Guid.CreateVersion7(), idempotencyKey.Trim(), payloadHash, clock.GetUtcNow()); await reserve.ExecuteNonQueryAsync(token); }
        await using (var key = new NpgsqlCommand("select request_hash,result_id from finance_payments.payment_idempotency where tenant_id=$1 and scope='cancel-payment' and idempotency_key=$2 for update", context.Connection, context.Transaction))
        {
            Add(key, tenant.Value, idempotencyKey.Trim()); await using var prior = await key.ExecuteReaderAsync(token);
            if (!await prior.ReadAsync(token)) throw new PaymentException("payments.idempotency_unavailable", "The cancellation request could not be reserved.");
            var priorHash = prior.GetString(0).Trim(); var priorId = prior.IsDBNull(1) ? (Guid?)null : prior.GetGuid(1); await prior.CloseAsync();
            if (priorHash != payloadHash || priorId is { } oldId && oldId != payment.Id) throw new PaymentException("payments.idempotency_conflict", "This cancellation key was already used for a different payment.");
            if (priorId is not null && payment.State == PaymentBusinessState.Cancelled) return payment;
        }
        if (payment.State == PaymentBusinessState.Cancelled) return payment;
        if (payment.State is not (PaymentBusinessState.Draft or PaymentBusinessState.Pending)) throw new PaymentException("payments.payment_not_cancellable", "Only a draft or pending payment can be cancelled.");
        var now = clock.GetUtcNow();
        await using (var update = new NpgsqlCommand("update finance_payments.payment set business_state='Cancelled',row_version=row_version+1,updated_at=$4 where tenant_id=$1 and legal_entity_id=$2 and id=$3", context.Connection, context.Transaction))
        { Add(update, tenant.Value, legalEntityId, payment.Id, now); if (await update.ExecuteNonQueryAsync(token) != 1) throw new PaymentException("payments.payment_state_conflict", "The payment changed before cancellation."); }
        await using (var refund = new NpgsqlCommand("update finance_payments.payment_refund set status='Cancelled' where tenant_id=$1 and legal_entity_id=$2 and refund_payment_id=$3 and status='Pending'", context.Connection, context.Transaction))
        { Add(refund, tenant.Value, legalEntityId, payment.Id); await refund.ExecuteNonQueryAsync(token); }
        await using (var updateKey = new NpgsqlCommand("update finance_payments.payment_idempotency set result_type='Payment',result_id=$4 where tenant_id=$1 and scope='cancel-payment' and idempotency_key=$2 and request_hash=$3", context.Connection, context.Transaction))
        { Add(updateKey, tenant.Value, idempotencyKey.Trim(), payloadHash, payment.Id); await updateKey.ExecuteNonQueryAsync(token); }
        await using (var projection = new NpgsqlCommand("update finance_payments.payment_list_projection set business_state='Cancelled',updated_at=$4 where tenant_id=$1 and legal_entity_id=$2 and payment_id=$3", context.Connection, context.Transaction))
        { Add(projection, tenant.Value, legalEntityId, payment.Id, now); await projection.ExecuteNonQueryAsync(token); }
        await NpgsqlAuditWriter.WriteAsync(context.Connection, context.Transaction, new(Guid.CreateVersion7(), tenant, legalEntityId, payment.BranchId, "User", actor, membership, "payments", "payment.cancelled", "payment", payment.Id, null,
            JsonSerializer.Serialize(new { payment.Number, payment.Amount, payment.CurrencyCode }, JsonOptions), null, "api", correlationId, null, now, null), token);
        context.AddOutbox(new(Guid.CreateVersion7(), "PaymentCancelledV1", JsonSerializer.Serialize(new { paymentId = payment.Id, payment.Number }, JsonOptions), tenant, now, correlationId, payment.Id, "payments"));
        return payment with { State = PaymentBusinessState.Cancelled, Version = payment.Version + 1 };
    }, ct);

    private static async Task<PaymentRecord?> GetPaymentAsync(NpgsqlConnection connection, TenantId tenant, Guid entity, Guid id, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select id,legal_entity_id,payment_number,branch_id,direction,payment_type,party_id,amount,currency_code,method,payment_account_id,payment_date,posting_date,value_date,bank_reference,customer_reference,supplier_reference,source_type,source_reference,business_state,completed_at,row_version from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and id=$3", connection);
        Add(command, tenant.Value, entity, id); await using var reader = await command.ExecuteReaderAsync(ct); return await reader.ReadAsync(ct) ? ReadPayment(reader) : null;
    }

    private static async Task<PaymentRecord?> GetPaymentInTransactionAsync(ITransactionContext context, TenantId tenant, Guid entity, Guid id, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select id,legal_entity_id,payment_number,branch_id,direction,payment_type,party_id,amount,currency_code,method,payment_account_id,payment_date,posting_date,value_date,bank_reference,customer_reference,supplier_reference,source_type,source_reference,business_state,completed_at,row_version from finance_payments.payment where tenant_id=$1 and legal_entity_id=$2 and id=$3", context.Connection, context.Transaction);
        Add(command, tenant.Value, entity, id); await using var reader = await command.ExecuteReaderAsync(ct); return await reader.ReadAsync(ct) ? ReadPayment(reader) : null;
    }

    private static async Task<PaymentIntentResult?> GetPaymentIntentInTransactionAsync(ITransactionContext context, TenantId tenant, Guid entity, Guid id, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("select id,legal_entity_id,merchant_reference,amount,currency_code,workflow_state from finance_payments.payment_request where tenant_id=$1 and legal_entity_id=$2 and id=$3", context.Connection, context.Transaction);
        Add(command, tenant.Value, entity, id); await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var result = new PaymentIntentResult(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), reader.GetDecimal(3), reader.GetString(4).Trim(), reader.GetString(5), Guid.Empty, 0);
        await reader.CloseAsync();
        await using var attempt = new NpgsqlCommand("select id,attempt_number from finance_payments.payment_attempt where tenant_id=$1 and legal_entity_id=$2 and payment_request_id=$3 order by attempt_number desc limit 1", context.Connection, context.Transaction);
        Add(attempt, tenant.Value, entity, id); await using var attemptReader = await attempt.ExecuteReaderAsync(ct);
        return await attemptReader.ReadAsync(ct) ? result with { AttemptId = attemptReader.GetGuid(0), AttemptNumber = attemptReader.GetInt32(1) } : result;
    }

    private static async Task<InternalTransferResult?> GetTransferInTransactionAsync(ITransactionContext context, TenantId tenant, Guid entity, Guid transferId, CancellationToken ct)
    {
        Guid sourceId; Guid destinationId; decimal sourceAmount; string sourceCurrency; decimal destinationAmount; string destinationCurrency; string? snapshot; DateTimeOffset completedAt;
        await using (var command = new NpgsqlCommand("select source_payment_id,destination_payment_id,source_amount,source_currency,destination_amount,destination_currency,fx_snapshot,created_at from finance_payments.internal_transfer where tenant_id=$1 and legal_entity_id=$2 and id=$3", context.Connection, context.Transaction))
        {
            Add(command, tenant.Value, entity, transferId); await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;
            sourceId = reader.GetGuid(0); destinationId = reader.GetGuid(1); sourceAmount = reader.GetDecimal(2); sourceCurrency = reader.GetString(3).Trim();
            destinationAmount = reader.GetDecimal(4); destinationCurrency = reader.GetString(5).Trim(); snapshot = StringValue(reader, 6); completedAt = reader.GetFieldValue<DateTimeOffset>(7);
        }
        var source = await GetPaymentInTransactionAsync(context, tenant, entity, sourceId, ct);
        var destination = await GetPaymentInTransactionAsync(context, tenant, entity, destinationId, ct);
        if (source is null || destination is null || snapshot is null) return null;
        using var evidence = JsonDocument.Parse(snapshot);
        var baseCurrency = evidence.RootElement.GetProperty("baseCurrency").GetString() ?? sourceCurrency;
        var publicFxSnapshot = sourceCurrency == destinationCurrency ? null : snapshot;
        return new(transferId, source, destination, sourceAmount, sourceCurrency, destinationAmount, destinationCurrency, baseCurrency, publicFxSnapshot, completedAt);
    }

    private static async Task UpsertProjectionAsync(ITransactionContext context, TenantId tenant, Guid entity, Guid id, string number, string amount, string currency,
        string direction, string method, string state, string? reference, DateTimeOffset now, CancellationToken ct)
    {
        var search = NormalizeSearch(string.Join(' ', number, reference));
        await using var command = new NpgsqlCommand("insert into finance_payments.payment_list_projection(tenant_id,legal_entity_id,payment_id,payment_number,account_name,amount,currency_code,direction,method,business_state,normalized_search,updated_at) values($1,$2,$3,$4,'', $5,$6,$7,$8,$9,$10,$11) on conflict(tenant_id,payment_id) do update set business_state=excluded.business_state,normalized_search=excluded.normalized_search,updated_at=excluded.updated_at", context.Connection, context.Transaction);
        Add(command, tenant.Value, entity, id, number, PaymentRules.ParseAmount(amount), PaymentRules.NormalizeCurrency(currency), direction, method, state, search, now);
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task<NpgsqlConnection> OpenAsync(TenantId tenant, CancellationToken ct)
    {
        var connection = await dataSource.OpenConnectionAsync(ct);
        await using var scope = new NpgsqlCommand("select set_config('app.tenant_id',$1,false)", connection);
        scope.Parameters.AddWithValue(tenant.Value.ToString("D"));
        await scope.ExecuteNonQueryAsync(ct);
        return connection;
    }

    private static PaymentRecord ReadPayment(NpgsqlDataReader reader) => new(reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2), GuidValue(reader, 3), Enum.Parse<PaymentDirection>(reader.GetString(4)), Enum.Parse<PaymentType>(reader.GetString(5)), GuidValue(reader, 6), reader.GetDecimal(7), reader.GetString(8).Trim(), Enum.Parse<PaymentMethod>(reader.GetString(9)), reader.GetGuid(10), DateOnly.FromDateTime(reader.GetDateTime(11)), DateOnly.FromDateTime(reader.GetDateTime(12)), reader.IsDBNull(13) ? null : DateOnly.FromDateTime(reader.GetDateTime(13)), StringValue(reader, 14), StringValue(reader, 15), StringValue(reader, 16), StringValue(reader, 17), StringValue(reader, 18), Enum.Parse<PaymentBusinessState>(reader.GetString(19)), reader.IsDBNull(20) ? null : reader.GetFieldValue<DateTimeOffset>(20), reader.GetInt64(21));
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOptions)))).ToLowerInvariant();
    private static decimal ParseRequired(string value) => decimal.Parse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? NormalizeReference(string? value) => string.IsNullOrWhiteSpace(value) ? null : string.Concat(value.Where(char.IsLetterOrDigit)).ToUpperInvariant();
    private static string? NormalizeSearch(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    private static bool AccountAcceptsMethod(string accountType, PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => accountType == "Cashbox",
        PaymentMethod.BankTransfer or PaymentMethod.Cheque => accountType == "Bank",
        PaymentMethod.PaymentGateway or PaymentMethod.Card => accountType == "ProviderClearing",
        PaymentMethod.Wallet => accountType == "Wallet",
        _ => accountType is "Bank" or "Cashbox" or "ProviderClearing" or "Wallet"
    };
    private static PaymentMethod TransferMethod(string accountType) => accountType switch
    {
        "Cashbox" => PaymentMethod.Cash,
        "Wallet" => PaymentMethod.Wallet,
        _ => PaymentMethod.BankTransfer
    };
    private static string? RateReference(FinancialCalculationContextSnapshotContract context, string currency) => currency == context.BaseCurrencyCode
        ? "base-currency"
        : context.ExchangeRate?.ApprovedRateId?.ToString("D") ?? context.ExchangeRate?.Rate;
    private sealed record TransferAccount(Guid Id, Guid? BranchId, string Code, string Type, string Status, string Policy, string? Currency);
    private static object Db(object? value) => value ?? DBNull.Value;
    private static Guid? GuidValue(NpgsqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
    private static string? StringValue(NpgsqlDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal).Trim();
    private static void Add(NpgsqlCommand command, params object?[] values) { foreach (var value in values) command.Parameters.AddWithValue(Db(value)); }
}
