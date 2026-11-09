alter table business_expenses.business_advance_application
    add column if not exists expense_currency_code char(3),
    add column if not exists advance_equivalent_amount numeric(19,4),
    add column if not exists fx_snapshot jsonb;

-- The Expenses tables use FORCE RLS, including for the table owner.  The
-- migrator is the only privileged actor allowed to backfill this immutable
-- compatibility snapshot; restore FORCE RLS immediately after the update.
alter table business_expenses.expense no force row level security;
alter table business_expenses.business_advance_application no force row level security;
update business_expenses.business_advance_application
set expense_currency_code = coalesce(expense_currency_code, (select e.currency_code from business_expenses.expense e where e.tenant_id=business_advance_application.tenant_id and e.id=business_advance_application.expense_id)),
    advance_equivalent_amount = coalesce(advance_equivalent_amount, amount)
where expense_currency_code is null or advance_equivalent_amount is null;
alter table business_expenses.expense force row level security;
alter table business_expenses.business_advance_application force row level security;

alter table business_expenses.business_advance_application
    alter column expense_currency_code set not null,
    alter column advance_equivalent_amount set not null;

do $$
begin
    if not exists (select 1 from pg_constraint where conname = 'advance_application_equivalent_amount_check') then
        alter table business_expenses.business_advance_application add constraint advance_application_equivalent_amount_check check (advance_equivalent_amount > 0);
    end if;
end $$;

grant select, insert, update, delete on business_expenses.business_advance_application to erp_app;
