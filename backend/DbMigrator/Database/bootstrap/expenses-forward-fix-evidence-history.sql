alter table business_expenses.expense_evidence add column if not exists replaces_evidence_id uuid;
create index if not exists ix_expenses_evidence_replacement on business_expenses.expense_evidence(tenant_id,legal_entity_id,expense_id,replaces_evidence_id);
