alter table business_expenses.expense add column if not exists approval_request_id uuid;
create index if not exists ix_expense_approval_request on business_expenses.expense(tenant_id,legal_entity_id,approval_request_id) where approval_request_id is not null;
