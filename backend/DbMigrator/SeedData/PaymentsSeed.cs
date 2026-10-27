using System.Security.Cryptography;
using System.Text;

namespace DbMigrator.SeedData;

public static class PaymentsSeed
{
    public static async Task<SeedPaymentSummary> SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var accountIds = new List<Guid>();
        var requestCount = 0;
        foreach (var (entity, branch, suffix) in new[]
        {
            (context.EntityId, (Guid?)context.BranchId, "main"),
            (SeedIds.TenantSecondaryEntity(context.Tenant.Id), (Guid?)SeedIds.TenantSecondaryBranch(context.Tenant.Id), "secondary")
        })
        {
            var accounts = new[]
            {
                new Account("BANK-EGP", $"الحساب التشغيلي - {context.Tenant.TradeName ?? context.Tenant.DisplayName}", "Bank", "SingleCurrency", "EGP", (Guid?)null, "بنك مصر", context.Tenant.LegalName, "•••• 4821"),
                new Account("CASH-MAIN", "الخزينة الرئيسية", "Cashbox", "SingleCurrency", "EGP", branch, null, null, null),
                new Account("GATEWAY-CLEARING", "مقاصة بوابة الدفع", "ProviderClearing", "SingleCurrency", "EGP", (Guid?)null, null, null, null),
                new Account("BANK-USD", "حساب العملة الأجنبية - دولار أمريكي", "Bank", "SingleCurrency", "USD", (Guid?)null, "البنك التجاري الدولي", context.Tenant.LegalName, "•••• 9307")
            };
            foreach (var (account, index) in accounts.Select((value, index) => (value, index)))
            {
                var id = SeedIds.Child(context.Tenant.Id, $"payment-account:{entity:N}:{suffix}", index + 1);
                await context.ExecuteAsync("""
                    insert into finance_payments.payment_account(tenant_id,legal_entity_id,id,branch_id,code,name,account_type,currency_policy,currency_code,status,bank_name,account_holder,masked_identifier,secret_ref,version,created_at,updated_at)
                    values($1,$2,$3,$4,$5,$6,$7,$8,$9,'Active',$10,$11,$12,null,1,$13,$13)
                    on conflict(tenant_id,legal_entity_id,code) do nothing
                    """, cancellationToken, context.Tenant.Id, entity, id, account.BranchId, $"{account.Code}-{suffix.ToUpperInvariant()}", account.Name, account.Type,
                    account.CurrencyPolicy, account.Currency, account.BankName, account.AccountHolder, account.MaskedIdentifier, context.AsOf);
                accountIds.Add(id);
                var method = account.Type switch { "Cashbox" => "Cash", "ProviderClearing" => "PaymentGateway", _ => "BankTransfer" };
                var methodId = SeedIds.Child(context.Tenant.Id, $"payment-method:{entity:N}:{suffix}:{method}", 1);
                await context.ExecuteAsync("insert into finance_payments.payment_method_config(tenant_id,legal_entity_id,id,payment_account_id,method,branch_id,channel,status,effective_from,created_at) values($1,$2,$3,$4,$5,$6,'BackOffice','Active','2026-01-01',$7) on conflict(tenant_id,id) do nothing", cancellationToken, context.Tenant.Id, entity, methodId, id, method, account.BranchId, context.AsOf);
            }

            var gatewayAccountId = SeedIds.Child(context.Tenant.Id, $"payment-account:{entity:N}:{suffix}", 3);
            var providerId = SeedIds.Child(context.Tenant.Id, $"payment-provider:{entity:N}:{suffix}", 1);
            var providerMerchant = $"REF-{context.Tenant.Id.ToString("N")[..8].ToUpperInvariant()}-{suffix.ToUpperInvariant()}";
            await context.ExecuteAsync("""
                insert into finance_payments.provider_config(tenant_id,legal_entity_id,id,provider_code,environment,merchant_reference,api_version,capabilities,supported_currencies,supported_methods,payment_account_id,secret_ref,status,version,created_at,updated_at)
                values($1,$2,$3,'reference','Sandbox',$4,'2026-01',cast($5 as jsonb),array['EGP']::char(3)[],array['PaymentGateway']::text[],$6,null,'Active',1,$7,$7)
                on conflict(tenant_id,legal_entity_id,id) do nothing
                """, cancellationToken, context.Tenant.Id, entity, providerId, providerMerchant,
                """{"createPaymentRequest":true,"getPaymentStatus":true,"capture":false,"cancel":true,"refund":true,"webhooks":false}""",
                gatewayAccountId, context.AsOf);

            var requestId = SeedIds.Child(context.Tenant.Id, $"payment-request:awaiting-action:{entity:N}:{suffix}", 1);
            var attemptId = SeedIds.Child(context.Tenant.Id, $"payment-attempt:awaiting-action:{entity:N}:{suffix}", 1);
            var operationId = SeedIds.Child(context.Tenant.Id, $"payment-operation:awaiting-action:{entity:N}:{suffix}", 1);
            var merchantReference = $"PAY-{context.Tenant.Id.ToString("N")[..8].ToUpperInvariant()}-{suffix.ToUpperInvariant()}-001";
            var idempotencyKey = $"seed-payment-request-{suffix}";
            var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{requestId:D}:18500:EGP"))).ToLowerInvariant();
            await context.ExecuteAsync("""
                insert into finance_payments.payment_request(tenant_id,legal_entity_id,id,branch_id,direction,party_id,amount,currency_code,provider_config_id,source_type,source_reference,merchant_reference,workflow_state,expires_at,created_by,created_at,updated_at)
                values($1,$2,$3,$4,'Incoming',$5,18500.00,'EGP',$6,'DevelopmentSeed',$7,$8,'RequiresAction',$9,$10,$11,$11)
                on conflict(tenant_id,legal_entity_id,id) do nothing
                """, cancellationToken, context.Tenant.Id, entity, requestId, branch, SeedIds.Party(context.Tenant.Id, 2), providerId,
                $"customer-receipt:{suffix}:awaiting-customer", merchantReference, context.AsOf.AddDays(7), context.AdministratorId, context.AsOf);
            await context.ExecuteAsync("""
                insert into finance_payments.provider_operation(tenant_id,legal_entity_id,id,provider_config_id,operation_type,aggregate_type,aggregate_id,idempotency_key,request_hash,state,attempts,created_at,updated_at)
                values($1,$2,$3,$4,'Create','PaymentRequest',$5,$6,$7,'Succeeded',1,$8,$8)
                on conflict(tenant_id,id) do nothing
                """, cancellationToken, context.Tenant.Id, entity, operationId, providerId, requestId, idempotencyKey, requestHash, context.AsOf);
            await context.ExecuteAsync("""
                insert into finance_payments.payment_attempt(tenant_id,legal_entity_id,id,payment_request_id,attempt_number,provider_config_id,provider_operation_id,provider_state,state,idempotency_key,created_at,updated_at)
                values($1,$2,$3,$4,1,$5,$6,'RequiresAction','RequiresAction',$7,$8,$8)
                on conflict(tenant_id,id) do nothing
                """, cancellationToken, context.Tenant.Id, entity, attemptId, requestId, providerId, operationId.ToString("D"), idempotencyKey, context.AsOf);
            requestCount++;
        }
        return new(accountIds.Count, accountIds.Count, requestCount);
    }

    private sealed record Account(string Code, string Name, string Type, string CurrencyPolicy, string Currency, Guid? BranchId,
        string? BankName, string? AccountHolder, string? MaskedIdentifier);
}

public sealed record SeedPaymentSummary(int PaymentAccounts, int MethodConfigurations, int PendingProviderRequests);
