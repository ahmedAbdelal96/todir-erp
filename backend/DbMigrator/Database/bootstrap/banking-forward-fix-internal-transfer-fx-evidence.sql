alter table finance_banking.transfer_reconciliation_projection add column if not exists base_currency_code char(3);
alter table finance_banking.transfer_reconciliation_projection add column if not exists fx_snapshot jsonb;
