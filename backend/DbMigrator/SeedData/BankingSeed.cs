namespace DbMigrator.SeedData;

public static class BankingSeed
{
    public static async Task<SeedBankingSummary> SeedAsync(SeedContext context, CancellationToken cancellationToken)
    {
        var tenant = context.Tenant.Id;
        var now = context.AsOf;

        // Payments remains a compatibility projection. This idempotent bridge makes
        // accounts created by the existing development seed canonical Banking records.
        await context.ExecuteAsync("""
            insert into finance_banking.treasury_account
                (tenant_id,legal_entity_id,id,branch_id,code,name,account_type,status,currency_policy,currency_code,purpose,liquidity_class,allowed_uses,masked_identifier,secret_ref,version,created_at,updated_at)
            select p.tenant_id,p.legal_entity_id,p.id,p.branch_id,p.code,p.name,
                case p.account_type when 'Bank' then 'Bank' when 'Cashbox' then 'Cash' when 'Wallet' then 'DigitalWallet' when 'ProviderClearing' then 'ProviderSettlement' end,
                case p.status when 'Archived' then 'Closed' else p.status end,p.currency_policy,p.currency_code,'Development treasury account','Operational','[]'::jsonb,p.masked_identifier,p.secret_ref,p.version,p.created_at,p.updated_at
            from finance_payments.payment_account p
            where p.tenant_id=$1 and p.account_type <> 'InternalClearing'
            on conflict(tenant_id,id) do nothing
            """, cancellationToken, tenant);
        await context.ExecuteAsync("update finance_payments.payment_account p set treasury_account_id=p.id where p.tenant_id=$1 and p.account_type <> 'InternalClearing' and p.treasury_account_id is null", cancellationToken, tenant);
        await context.ExecuteAsync("""
            insert into finance_banking.treasury_account_balance_pocket(tenant_id,legal_entity_id,id,treasury_account_id,currency_code,is_primary,version,created_at,updated_at)
            select p.tenant_id,p.legal_entity_id,gen_random_uuid(),p.id,p.currency_code,true,1,p.created_at,p.updated_at
            from finance_payments.payment_account p
            where p.tenant_id=$1 and p.account_type <> 'InternalClearing' and p.currency_policy='SingleCurrency' and p.currency_code is not null
            on conflict(tenant_id,treasury_account_id,currency_code) do nothing
            """, cancellationToken, tenant);
        await context.ExecuteAsync("""
            insert into finance_banking.bank_account_profile(tenant_id,legal_entity_id,treasury_account_id,bank_name,account_holder,account_number_masked,created_at,updated_at)
            select p.tenant_id,p.legal_entity_id,p.id,coalesce(nullif(p.bank_name,''),'Development bank'),p.account_holder,p.masked_identifier,p.created_at,p.updated_at
            from finance_payments.payment_account p where p.tenant_id=$1 and p.account_type='Bank'
            on conflict(tenant_id,treasury_account_id) do nothing
            """, cancellationToken, tenant);
        await context.ExecuteAsync("""
            insert into finance_banking.cash_account_profile(tenant_id,legal_entity_id,treasury_account_id,location_description,created_at,updated_at)
            select p.tenant_id,p.legal_entity_id,p.id,coalesce(p.name,'Cash account'),p.created_at,p.updated_at
            from finance_payments.payment_account p where p.tenant_id=$1 and p.account_type='Cashbox'
            on conflict(tenant_id,treasury_account_id) do nothing
            """, cancellationToken, tenant);
        await context.ExecuteAsync("""
            insert into finance_banking.settlement_account_profile(tenant_id,legal_entity_id,treasury_account_id,provider_code,merchant_reference_masked,created_at,updated_at)
            select p.tenant_id,p.legal_entity_id,p.id,'development',p.masked_identifier,p.created_at,p.updated_at
            from finance_payments.payment_account p where p.tenant_id=$1 and p.account_type='ProviderClearing'
            on conflict(tenant_id,treasury_account_id) do nothing
            """, cancellationToken, tenant);
        await context.ExecuteAsync("""
            insert into finance_banking.cash_position_projection(tenant_id,legal_entity_id,treasury_account_id,currency_code,book_balance,external_balance,available_balance,in_transit_amount,balance_basis,source_watermark,as_of,external_as_of)
            select p.tenant_id,p.legal_entity_id,p.id,p.currency_code,
                null,
                case when p.account_type in ('Bank','Cashbox') then case when p.currency_code='USD' then 12500 else case when p.account_type='Cashbox' then 55000 else 250000 end end else null end,
                null,0,'DevelopmentSeed.ExternalEvidenceOnly',null,$2,$2
            from finance_payments.payment_account p where p.tenant_id=$1 and p.account_type <> 'InternalClearing' and p.currency_policy='SingleCurrency'
            on conflict(tenant_id,treasury_account_id,currency_code) do nothing
            """, cancellationToken, tenant, now);

        var statementCount = 0;
        foreach (var (entity, suffix) in new[] { (context.EntityId, "main"), (SeedIds.TenantSecondaryEntity(tenant), "secondary") })
        {
            foreach (var (accountIndex, currency, label) in new[] { (1, "EGP", "EGP operational statement"), (4, "USD", "USD operational statement") })
            {
                var accountId = SeedIds.Child(tenant, $"payment-account:{entity:N}:{suffix}", accountIndex);
                var statementId = SeedIds.Child(tenant, $"banking-statement:{entity:N}:{suffix}:{currency}", 1);
                var lineId = SeedIds.Child(tenant, $"banking-statement-line:{entity:N}:{suffix}:{currency}", 1);
                await context.ExecuteAsync("""
                    insert into finance_banking.external_statement(tenant_id,legal_entity_id,id,treasury_account_id,statement_number,opening_date,closing_date,currency_code,opening_balance,closing_balance,processing_state,reconciliation_state,source_type,source_reference,version,created_at,updated_at)
                    values($1,$2,$3,$4,$5,'2026-09-30','2026-10-01',$6,0,case when $6='USD' then 12500 else 250000 end,'Validated','Unreconciled','DevelopmentSeed',$7,1,$8,$8)
                    on conflict(tenant_id,id) do nothing
                    """, cancellationToken, tenant, entity, statementId, accountId, $"BANK-{suffix}-{currency}-001", currency, label, now);
                await context.ExecuteAsync("""
                    insert into finance_banking.external_statement_line(tenant_id,legal_entity_id,id,statement_id,treasury_account_id,external_line_id,booking_date,value_date,direction,amount,currency_code,reference,transaction_code,raw_payload,content_hash,classification,claimed_amount,version,created_at,updated_at)
                    values($1,$2,$3,$4,$5,$6,'2026-10-01','2026-10-01','Credit',case when $7='USD' then 12500 else 250000 end,$7,$8,'SEED', '{}'::jsonb,repeat('0',64),'Unclassified',0,1,$9,$9)
                    on conflict(tenant_id,id) do nothing
                    """, cancellationToken, tenant, entity, lineId, statementId, accountId, $"SEED-{suffix}-{currency}-001", currency, "Development seed opening movement", now);
                statementCount++;
            }
        }

        var reconciliationId = SeedIds.Child(tenant, "banking-reconciliation", 1);
        var mainEgpStatement = SeedIds.Child(tenant, $"banking-statement:{context.EntityId:N}:main:EGP", 1);
        var mainEgpAccount = SeedIds.Child(tenant, $"payment-account:{context.EntityId:N}:main", 1);
        await context.ExecuteAsync("insert into finance_banking.reconciliation_session(tenant_id,legal_entity_id,id,treasury_account_id,statement_id,as_of_date,state,external_balance,book_balance,difference,notes,version,created_at,updated_at) values($1,$2,$3,$4,$5,'2026-10-01','Open',250000,null,null,'Development seed reconciliation; Ledger evidence is intentionally required at runtime',1,$6,$6) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, context.EntityId, reconciliationId, mainEgpAccount, mainEgpStatement, now);

        var cashAccount = SeedIds.Child(tenant, $"payment-account:{context.EntityId:N}:main", 2);
        var cashCountId = SeedIds.Child(tenant, "banking-cash-count", 1);
        await context.ExecuteAsync("""
            insert into finance_banking.cash_count(tenant_id,legal_entity_id,id,treasury_account_id,cutoff_date,custodian_id,state,expected_amount,counted_amount,difference,version,created_at,submitted_at)
            values($1,$2,$3,$4,'2026-10-01',$5,'Submitted',55000,55000,0,1,$6,$6)
            on conflict(tenant_id,id) do nothing
            """, cancellationToken, tenant, context.EntityId, cashCountId, cashAccount, context.AdministratorId, now);
        await context.ExecuteAsync("insert into finance_banking.cash_count_denomination(tenant_id,id,cash_count_id,denomination,unit_value,quantity) values($1,$2,$3,'100 EGP',100,550) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, SeedIds.Child(tenant, "banking-cash-denomination", 1), cashCountId);

        var forecastId = SeedIds.Child(tenant, "banking-forecast", 1);
        var forecastItemId = SeedIds.Child(tenant, "banking-forecast-item", 1);
        await context.ExecuteAsync("insert into finance_banking.cash_forecast_version(tenant_id,legal_entity_id,id,as_of_date,horizon_days,status,created_by,created_at) values($1,$2,$3,'2026-10-01',30,'Published',$4,$5) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, context.EntityId, forecastId, context.AdministratorId, now);
        await context.ExecuteAsync("insert into finance_banking.cash_forecast_item(tenant_id,legal_entity_id,id,forecast_version_id,treasury_account_id,due_date,direction,amount,currency_code,source_type,source_reference,description,confidence) values($1,$2,$3,$4,$5,'2026-10-15','Inflow',10000,'EGP','DevelopmentSeed','{}'::jsonb,'Seeded customer collection','Expected') on conflict(tenant_id,id) do nothing", cancellationToken, tenant, context.EntityId, forecastItemId, forecastId, cashAccount);

        var mappingId = SeedIds.Child(tenant, "banking-transaction-code-mapping", 1);
        await context.ExecuteAsync("insert into finance_banking.statement_transaction_code_mapping(tenant_id,legal_entity_id,id,transaction_code,classification,effective_from,created_at,created_by) values($1,$2,$3,'SEED','Receipt','2026-01-01',$4,$5) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, context.EntityId, mappingId, now, context.AdministratorId);
        var ruleSetId = SeedIds.Child(tenant, "banking-reconciliation-rule-set", 1);
        var ruleVersionId = SeedIds.Child(tenant, "banking-reconciliation-rule-version", 1);
        await context.ExecuteAsync("insert into finance_banking.reconciliation_rule_set(tenant_id,legal_entity_id,id,name,status,created_at,updated_at) values($1,$2,$3,'Development matching rules','Active',$4,$4) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, context.EntityId, ruleSetId, now);
        await context.ExecuteAsync("insert into finance_banking.reconciliation_rule_version(tenant_id,legal_entity_id,id,rule_set_id,rule_version,priority,definition,created_at,created_by) values($1,$2,$3,$4,1,1,'{\"transactionCode\":\"SEED\",\"classification\":\"Receipt\"}'::jsonb,$5,$6) on conflict(tenant_id,id) do nothing", cancellationToken, tenant, context.EntityId, ruleVersionId, ruleSetId, now, context.AdministratorId);

        return new(statementCount, 1, 1, 1);
    }
}

public sealed record SeedBankingSummary(int Statements, int Reconciliations, int CashCounts, int Forecasts);
