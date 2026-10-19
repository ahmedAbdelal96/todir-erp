ALTER TABLE foundation_catalog.product_template
    ADD COLUMN IF NOT EXISTS product_source text NOT NULL DEFAULT 'Manual',
    ADD COLUMN IF NOT EXISTS source_batch_id uuid;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'foundation_catalog.product_template'::regclass AND conname = 'product_template_source_check') THEN
        ALTER TABLE foundation_catalog.product_template
            ADD CONSTRAINT product_template_source_check CHECK (product_source = ANY (ARRAY['Manual'::text, 'Import'::text, 'Api'::text, 'Integration'::text])) NOT VALID;
    END IF;
END $$;
ALTER TABLE foundation_catalog.product_template VALIDATE CONSTRAINT product_template_source_check;

CREATE TABLE IF NOT EXISTS foundation_catalog.catalog_import_batch (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    file_name text NOT NULL,
    status text NOT NULL,
    total_rows integer NOT NULL,
    create_count integer NOT NULL DEFAULT 0,
    update_count integer NOT NULL DEFAULT 0,
    review_count integer NOT NULL DEFAULT 0,
    error_count integer NOT NULL DEFAULT 0,
    created_by uuid NOT NULL,
    created_membership_id uuid NOT NULL,
    created_at timestamptz NOT NULL,
    committed_at timestamptz,
    CONSTRAINT catalog_import_batch_pkey PRIMARY KEY (tenant_id, id),
    CONSTRAINT catalog_import_batch_status_check CHECK (status = ANY (ARRAY['Previewed'::text, 'Committed'::text, 'Failed'::text])),
    CONSTRAINT catalog_import_batch_rows_check CHECK (total_rows >= 0 AND create_count >= 0 AND update_count >= 0 AND review_count >= 0 AND error_count >= 0),
    CONSTRAINT catalog_import_batch_tenant_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id)
);

CREATE TABLE IF NOT EXISTS foundation_catalog.catalog_import_row (
    tenant_id uuid NOT NULL,
    batch_id uuid NOT NULL,
    row_number integer NOT NULL,
    row_data jsonb NOT NULL,
    normalized_sku text,
    normalized_barcode text,
    match_template_id uuid,
    match_variant_id uuid,
    target_template_id uuid NOT NULL,
    target_variant_id uuid NOT NULL,
    category_id uuid,
    brand_id uuid,
    base_uom_id uuid,
    classification text NOT NULL,
    errors text[] NOT NULL DEFAULT ARRAY[]::text[],
    processed_at timestamptz,
    CONSTRAINT catalog_import_row_pkey PRIMARY KEY (tenant_id, batch_id, row_number),
    CONSTRAINT catalog_import_row_classification_check CHECK (classification = ANY (ARRAY['Create'::text, 'Update'::text, 'Review'::text, 'Error'::text])),
    CONSTRAINT catalog_import_row_batch_fkey FOREIGN KEY (tenant_id, batch_id) REFERENCES foundation_catalog.catalog_import_batch(tenant_id, id) ON DELETE CASCADE,
    CONSTRAINT catalog_import_row_template_fkey FOREIGN KEY (tenant_id, match_template_id) REFERENCES foundation_catalog.product_template(tenant_id, id),
    CONSTRAINT catalog_import_row_variant_fkey FOREIGN KEY (tenant_id, match_variant_id) REFERENCES foundation_catalog.product_variant(tenant_id, id),
    CONSTRAINT catalog_import_row_category_fkey FOREIGN KEY (tenant_id, category_id) REFERENCES foundation_catalog.product_category(tenant_id, id),
    CONSTRAINT catalog_import_row_brand_fkey FOREIGN KEY (tenant_id, brand_id) REFERENCES foundation_catalog.brand(tenant_id, id),
    CONSTRAINT catalog_import_row_uom_fkey FOREIGN KEY (tenant_id, base_uom_id) REFERENCES foundation_catalog.uom(tenant_id, id)
);

CREATE INDEX IF NOT EXISTS ix_catalog_import_batch_recent ON foundation_catalog.catalog_import_batch (tenant_id, created_at DESC, id);
CREATE INDEX IF NOT EXISTS ix_catalog_import_row_pending ON foundation_catalog.catalog_import_row (tenant_id, batch_id, classification, row_number) WHERE processed_at IS NULL;

ALTER TABLE foundation_catalog.catalog_import_batch ENABLE ROW LEVEL SECURITY;
ALTER TABLE foundation_catalog.catalog_import_batch FORCE ROW LEVEL SECURITY;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_policy WHERE polrelid = 'foundation_catalog.catalog_import_batch'::regclass AND polname = 'tenant_isolation') THEN
        CREATE POLICY tenant_isolation ON foundation_catalog.catalog_import_batch
            USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
            WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
    END IF;
END $$;

ALTER TABLE foundation_catalog.catalog_import_row ENABLE ROW LEVEL SECURITY;
ALTER TABLE foundation_catalog.catalog_import_row FORCE ROW LEVEL SECURITY;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_policy WHERE polrelid = 'foundation_catalog.catalog_import_row'::regclass AND polname = 'tenant_isolation') THEN
        CREATE POLICY tenant_isolation ON foundation_catalog.catalog_import_row
            USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
            WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
    END IF;
END $$;

GRANT SELECT, INSERT, UPDATE, DELETE ON foundation_catalog.catalog_import_batch TO erp_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON foundation_catalog.catalog_import_row TO erp_app;
