-- Reversal serializes against the immutable application row with SELECT FOR UPDATE.
-- PostgreSQL requires UPDATE privilege to take that lock; the immutable trigger
-- still rejects any attempt to alter or delete the application history.
GRANT UPDATE (id) ON finance_subledger.open_item_application TO erp_app;
