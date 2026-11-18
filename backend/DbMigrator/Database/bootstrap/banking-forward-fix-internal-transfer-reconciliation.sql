alter table finance_banking.transfer_reconciliation_projection add column if not exists source_currency_code char(3);
alter table finance_banking.transfer_reconciliation_projection add column if not exists destination_currency_code char(3);
alter table finance_banking.transfer_reconciliation_projection add column if not exists version bigint not null default 1;
update finance_banking.transfer_reconciliation_projection
set source_currency_code=coalesce(source_currency_code,currency_code),
    destination_currency_code=coalesce(destination_currency_code,currency_code)
where source_currency_code is null or destination_currency_code is null;
