-- Add granular daily line items and authoritative calculated amounts to per-diem detail
alter table business_expenses.per_diem_expense_detail add column if not exists daily_lines jsonb;
alter table business_expenses.per_diem_expense_detail add column if not exists calculated_amount numeric(19,4);

-- Revoke broad delete privileges and enforce least-privilege for runtime role erp_app
revoke delete on all tables in schema business_expenses from erp_app;

-- Ensure select, insert, update across business_expenses tables
grant select, insert, update on all tables in schema business_expenses to erp_app;

-- Grant delete strictly on draft-eligible mutable tables
grant delete on business_expenses.expense to erp_app;
grant delete on business_expenses.expense_itemization to erp_app;
grant delete on business_expenses.expense_participant to erp_app;
grant delete on business_expenses.expense_dimension to erp_app;
grant delete on business_expenses.expense_evidence to erp_app;
grant delete on business_expenses.expense_ocr_job to erp_app;
grant delete on business_expenses.mileage_expense_detail to erp_app;
grant delete on business_expenses.per_diem_expense_detail to erp_app;
grant delete on business_expenses.expense_missing_receipt_declaration to erp_app;
grant delete on business_expenses.expense_policy_exception to erp_app;
grant delete on business_expenses.expense_policy_evaluation to erp_app;
grant delete on business_expenses.expense_history to erp_app;
grant delete on business_expenses.business_advance_application to erp_app;
grant delete on business_expenses.expense_card_allocation to erp_app;
grant delete on business_expenses.expense_report_item to erp_app;
