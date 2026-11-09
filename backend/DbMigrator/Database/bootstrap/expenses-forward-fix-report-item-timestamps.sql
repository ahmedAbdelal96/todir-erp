alter table business_expenses.expense_report_item
  add column if not exists created_at timestamptz not null default now();

create index if not exists ix_expenses_report_items_scope
  on business_expenses.expense_report_item(tenant_id, legal_entity_id, report_id, created_at, id);
