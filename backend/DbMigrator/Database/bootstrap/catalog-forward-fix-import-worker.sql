ALTER TABLE foundation_catalog.catalog_import_batch
    DROP CONSTRAINT IF EXISTS catalog_import_batch_status_check;
ALTER TABLE foundation_catalog.catalog_import_batch
    ADD CONSTRAINT catalog_import_batch_status_check CHECK (status = ANY (ARRAY['Previewed'::text, 'Queued'::text, 'Executing'::text, 'Committed'::text, 'Failed'::text]));