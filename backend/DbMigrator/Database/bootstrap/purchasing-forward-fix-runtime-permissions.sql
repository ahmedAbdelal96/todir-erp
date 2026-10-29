-- Runtime role needs UPDATE for SELECT ... FOR UPDATE on invoice-link replay
-- rows and for the atomic invoice-link workflow.
GRANT SELECT, INSERT, UPDATE ON business_purchasing.purchase_order_invoice_link TO erp_app;
