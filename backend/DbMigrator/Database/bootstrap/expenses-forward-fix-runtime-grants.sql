-- Forward-only privilege correction for runtime business tables omitted from
-- the initial audit privilege correction. Keep the append-only audit boundary.
grant update on business_expenses.business_advance_issuance to erp_app;
grant update on business_expenses.expense_card_allocation to erp_app;
grant update on business_expenses.expense_dimension to erp_app;
grant update on business_expenses.mileage_expense_detail to erp_app;
grant update on business_expenses.per_diem_expense_detail to erp_app;
