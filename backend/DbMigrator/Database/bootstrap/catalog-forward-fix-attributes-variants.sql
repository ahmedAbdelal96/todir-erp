ALTER TABLE foundation_catalog.product_variant
    ADD COLUMN IF NOT EXISTS display_suffix text,
    ADD COLUMN IF NOT EXISTS combination_signature text NOT NULL DEFAULT '';

CREATE TABLE IF NOT EXISTS foundation_catalog.product_attribute (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    code text NOT NULL,
    normalized_code text NOT NULL,
    name_ar text NOT NULL,
    name_en text NOT NULL,
    is_variant_defining boolean NOT NULL DEFAULT false,
    status text NOT NULL DEFAULT 'Active',
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    CONSTRAINT product_attribute_pkey PRIMARY KEY (tenant_id, id),
    CONSTRAINT product_attribute_code_uq UNIQUE (tenant_id, normalized_code),
    CONSTRAINT product_attribute_status_ck CHECK (status IN ('Active', 'Archived')),
    CONSTRAINT product_attribute_code_ck CHECK (length(normalized_code) BETWEEN 1 AND 64)
);

CREATE TABLE IF NOT EXISTS foundation_catalog.product_attribute_value (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    attribute_id uuid NOT NULL,
    code text NOT NULL,
    normalized_code text NOT NULL,
    name_ar text NOT NULL,
    name_en text NOT NULL,
    status text NOT NULL DEFAULT 'Active',
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    CONSTRAINT product_attribute_value_pkey PRIMARY KEY (tenant_id, id),
    CONSTRAINT product_attribute_value_code_uq UNIQUE (tenant_id, attribute_id, normalized_code),
    CONSTRAINT product_attribute_value_attribute_fk FOREIGN KEY (tenant_id, attribute_id)
        REFERENCES foundation_catalog.product_attribute (tenant_id, id),
    CONSTRAINT product_attribute_value_status_ck CHECK (status IN ('Active', 'Archived'))
);

CREATE TABLE IF NOT EXISTS foundation_catalog.product_template_attribute (
    tenant_id uuid NOT NULL,
    template_id uuid NOT NULL,
    attribute_id uuid NOT NULL,
    display_order integer NOT NULL DEFAULT 0,
    is_required boolean NOT NULL DEFAULT true,
    CONSTRAINT product_template_attribute_pkey PRIMARY KEY (tenant_id, template_id, attribute_id),
    CONSTRAINT product_template_attribute_template_fk FOREIGN KEY (tenant_id, template_id)
        REFERENCES foundation_catalog.product_template (tenant_id, id),
    CONSTRAINT product_template_attribute_attribute_fk FOREIGN KEY (tenant_id, attribute_id)
        REFERENCES foundation_catalog.product_attribute (tenant_id, id),
    CONSTRAINT product_template_attribute_order_ck CHECK (display_order >= 0)
);

CREATE TABLE IF NOT EXISTS foundation_catalog.product_variant_attribute_value (
    tenant_id uuid NOT NULL,
    variant_id uuid NOT NULL,
    attribute_id uuid NOT NULL,
    attribute_value_id uuid NOT NULL,
    CONSTRAINT product_variant_attribute_value_pkey PRIMARY KEY (tenant_id, variant_id, attribute_id),
    CONSTRAINT product_variant_attribute_value_variant_fk FOREIGN KEY (tenant_id, variant_id)
        REFERENCES foundation_catalog.product_variant (tenant_id, id),
    CONSTRAINT product_variant_attribute_value_attribute_fk FOREIGN KEY (tenant_id, attribute_id)
        REFERENCES foundation_catalog.product_attribute (tenant_id, id),
    CONSTRAINT product_variant_attribute_value_value_fk FOREIGN KEY (tenant_id, attribute_value_id)
        REFERENCES foundation_catalog.product_attribute_value (tenant_id, id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_catalog_variant_combination
    ON foundation_catalog.product_variant (tenant_id, product_template_id, combination_signature)
    WHERE (NOT is_default_variant AND combination_signature <> '' AND status = 'Active');

CREATE INDEX IF NOT EXISTS ix_catalog_attribute_values
    ON foundation_catalog.product_attribute_value (tenant_id, attribute_id, status, normalized_code);

CREATE INDEX IF NOT EXISTS ix_catalog_variant_attribute_values
    ON foundation_catalog.product_variant_attribute_value (tenant_id, attribute_id, attribute_value_id);

DO $$
DECLARE table_name text;
BEGIN
    FOREACH table_name IN ARRAY ARRAY[
        'product_attribute',
        'product_attribute_value',
        'product_template_attribute',
        'product_variant_attribute_value'
    ] LOOP
        EXECUTE format('ALTER TABLE foundation_catalog.%I ENABLE ROW LEVEL SECURITY', table_name);
        EXECUTE format('ALTER TABLE foundation_catalog.%I FORCE ROW LEVEL SECURITY', table_name);
        EXECUTE format(
            'CREATE POLICY tenant_isolation ON foundation_catalog.%I USING (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid) WITH CHECK (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid)',
            table_name
        );
        EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE foundation_catalog.%I TO erp_app', table_name);
    END LOOP;
END $$;
