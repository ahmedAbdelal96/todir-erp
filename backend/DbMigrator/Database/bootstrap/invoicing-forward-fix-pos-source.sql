ALTER TABLE business_invoicing.invoice DROP CONSTRAINT IF EXISTS invoice_source_module_check;
ALTER TABLE business_invoicing.invoice ADD CONSTRAINT invoice_source_module_check CHECK (source_module IS NULL OR source_module IN ('sales','purchasing','pos'));
