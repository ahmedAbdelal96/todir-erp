-- Preserve the exact FinanceBase context used when an expense becomes an
-- accounting fact. These columns are intentionally nullable for historical
-- rows created before the financial-context capability was wired in.
alter table business_expenses.expense
    add column if not exists base_currency_code char(3),
    add column if not exists base_amount numeric(19,4),
    add column if not exists financial_context_snapshot jsonb;

do $$
begin
    if not exists (select 1 from pg_constraint where conname = 'expense_base_currency_code_check') then
        alter table business_expenses.expense add constraint expense_base_currency_code_check
            check (base_currency_code is null or length(base_currency_code) = 3);
    end if;
    if not exists (select 1 from pg_constraint where conname = 'expense_base_amount_check') then
        alter table business_expenses.expense add constraint expense_base_amount_check
            check (base_amount is null or base_amount > 0);
    end if;
end $$;

grant select, insert, update, delete on business_expenses.expense to erp_app;
