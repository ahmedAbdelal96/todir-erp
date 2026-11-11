-- Forward-only privilege correction for report item upserts. Report items are
-- draft workflow data, not append-only audit facts.
grant update on business_expenses.expense_report_item to erp_app;
