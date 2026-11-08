alter table business_expenses.business_advance
  add column if not exists settlement_due_date date;

create index if not exists ix_expenses_advances_overdue
  on business_expenses.business_advance(tenant_id, legal_entity_id, settlement_due_date, state)
  where settlement_due_date is not null;
