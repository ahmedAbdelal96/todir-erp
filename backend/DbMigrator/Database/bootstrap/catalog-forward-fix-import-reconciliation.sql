ALTER TABLE foundation_catalog.catalog_import_batch
    ADD COLUMN IF NOT EXISTS created_count integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS updated_count integer NOT NULL DEFAULT 0;

ALTER TABLE foundation_catalog.catalog_import_batch
    DROP CONSTRAINT IF EXISTS catalog_import_batch_status_check;
ALTER TABLE foundation_catalog.catalog_import_batch
    ADD CONSTRAINT catalog_import_batch_status_check CHECK (status = ANY (ARRAY['Previewed'::text, 'Executing'::text, 'Committed'::text, 'Failed'::text]));

ALTER TABLE foundation_catalog.catalog_import_batch
    DROP CONSTRAINT IF EXISTS catalog_import_batch_rows_check;
ALTER TABLE foundation_catalog.catalog_import_batch
    ADD CONSTRAINT catalog_import_batch_rows_check CHECK (total_rows >= 0 AND create_count >= 0 AND update_count >= 0 AND review_count >= 0 AND error_count >= 0 AND created_count >= 0 AND updated_count >= 0);
