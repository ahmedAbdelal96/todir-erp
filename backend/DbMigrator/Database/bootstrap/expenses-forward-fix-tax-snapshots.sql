-- Tax Engine evidence is stored beside the expense so the calculation can be
-- reproduced without re-evaluating a later policy or country pack.
alter table business_expenses.expense
    add column if not exists tax_amount numeric(19,4) not null default 0,
    add column if not exists tax_snapshot jsonb;

do $$
begin
    if not exists (select 1 from pg_constraint where conname = 'expense_tax_amount_check') then
        alter table business_expenses.expense add constraint expense_tax_amount_check
            check (tax_amount >= 0);
    end if;
end $$;

grant select, insert, update, delete on business_expenses.expense to erp_app;
