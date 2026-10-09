CREATE SCHEMA IF NOT EXISTS foundation_catalog AUTHORIZATION erp_owner;
REVOKE ALL ON SCHEMA foundation_catalog FROM PUBLIC;
GRANT USAGE ON SCHEMA foundation_catalog TO erp_app;

CREATE TABLE IF NOT EXISTS foundation_catalog.product_category
(
 tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant(id), id uuid NOT NULL, name text NOT NULL CHECK (length(name) BETWEEN 1 AND 200), name_normalized text NOT NULL, parent_id uuid NULL, status text NOT NULL CHECK (status IN ('Active','Archived')), version bigint NOT NULL DEFAULT 1, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
 PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,id), UNIQUE (tenant_id,name_normalized), FOREIGN KEY (tenant_id,parent_id) REFERENCES foundation_catalog.product_category(tenant_id,id)
);
CREATE TABLE IF NOT EXISTS foundation_catalog.brand
(
 tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant(id), id uuid NOT NULL, name text NOT NULL CHECK (length(name) BETWEEN 1 AND 200), name_normalized text NOT NULL, status text NOT NULL CHECK (status IN ('Active','Archived')), version bigint NOT NULL DEFAULT 1, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
 PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,id), UNIQUE (tenant_id,name_normalized)
);
CREATE TABLE IF NOT EXISTS foundation_catalog.uom_dimension
(
 tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant(id), id uuid NOT NULL, code text NOT NULL, name text NOT NULL, status text NOT NULL CHECK (status IN ('Active','Archived')), PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,id), UNIQUE (tenant_id,code)
);
CREATE TABLE IF NOT EXISTS foundation_catalog.uom
(
 tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant(id), id uuid NOT NULL, code text NOT NULL CHECK (length(code) BETWEEN 1 AND 32), name_ar text NOT NULL, name_en text NOT NULL, dimension text NOT NULL CHECK (dimension IN ('Count','Mass','Length','Area','Volume','Time','BusinessUnit')), allow_fraction boolean NOT NULL, precision smallint NOT NULL CHECK (precision BETWEEN 0 AND 6), status text NOT NULL CHECK (status IN ('Active','Archived')), version bigint NOT NULL DEFAULT 1, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
 PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,id), UNIQUE (tenant_id,code)
);
CREATE TABLE IF NOT EXISTS foundation_catalog.product_template
(
 tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant(id), id uuid NOT NULL, display_name text NOT NULL CHECK (length(display_name) BETWEEN 1 AND 300), display_name_normalized text NOT NULL, name_ar text, name_en text, product_nature text NOT NULL CHECK (product_nature IN ('Goods','Service')), category_id uuid NULL, brand_id uuid NULL, status text NOT NULL CHECK (status IN ('Active','Archived')), created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, version bigint NOT NULL DEFAULT 1,
 PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,id), FOREIGN KEY (tenant_id,category_id) REFERENCES foundation_catalog.product_category(tenant_id,id), FOREIGN KEY (tenant_id,brand_id) REFERENCES foundation_catalog.brand(tenant_id,id)
);
CREATE TABLE IF NOT EXISTS foundation_catalog.product_variant
(
 tenant_id uuid NOT NULL, id uuid NOT NULL, product_template_id uuid NOT NULL, sku text, normalized_sku text, is_default_variant boolean NOT NULL, status text NOT NULL CHECK (status IN ('Active','Archived')), version bigint NOT NULL DEFAULT 1, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
 PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,id), UNIQUE (tenant_id,normalized_sku), FOREIGN KEY (tenant_id,product_template_id) REFERENCES foundation_catalog.product_template(tenant_id,id)
);
CREATE TABLE IF NOT EXISTS foundation_catalog.product_uom
(
 tenant_id uuid NOT NULL, id uuid NOT NULL, variant_id uuid NOT NULL, uom_id uuid NOT NULL, factor_to_base numeric(19,6) NOT NULL CHECK (factor_to_base > 0), is_base boolean NOT NULL, effective_from timestamptz NOT NULL, effective_to timestamptz NULL, status text NOT NULL CHECK (status IN ('Active','Archived')), version bigint NOT NULL DEFAULT 1,
 PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,id), FOREIGN KEY (tenant_id,variant_id) REFERENCES foundation_catalog.product_variant(tenant_id,id), FOREIGN KEY (tenant_id,uom_id) REFERENCES foundation_catalog.uom(tenant_id,id), CHECK (effective_to IS NULL OR effective_to > effective_from), CHECK ((is_base AND factor_to_base = 1) OR NOT is_base)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_catalog_one_current_base ON foundation_catalog.product_uom(tenant_id,variant_id) WHERE is_base AND status='Active' AND effective_to IS NULL;
CREATE TABLE IF NOT EXISTS foundation_catalog.product_barcode
(
 tenant_id uuid NOT NULL, id uuid NOT NULL, variant_id uuid NOT NULL, uom_id uuid NULL, barcode_type text NOT NULL, value text NOT NULL, normalized_value text NOT NULL, is_primary boolean NOT NULL, status text NOT NULL CHECK (status IN ('Active','Archived')), created_at timestamptz NOT NULL,
 PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,normalized_value), FOREIGN KEY (tenant_id,variant_id) REFERENCES foundation_catalog.product_variant(tenant_id,id), FOREIGN KEY (tenant_id,uom_id) REFERENCES foundation_catalog.uom(tenant_id,id)
);
CREATE TABLE IF NOT EXISTS foundation_catalog.product_alias
(
 tenant_id uuid NOT NULL, id uuid NOT NULL, variant_id uuid NOT NULL, alias text NOT NULL, normalized_alias text NOT NULL, alias_type text NOT NULL, language_code text NULL, created_at timestamptz NOT NULL,
 PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,variant_id,normalized_alias), FOREIGN KEY (tenant_id,variant_id) REFERENCES foundation_catalog.product_variant(tenant_id,id)
);
CREATE TABLE IF NOT EXISTS foundation_catalog.product_translation
(
 tenant_id uuid NOT NULL, id uuid NOT NULL, template_id uuid NOT NULL, language_code text NOT NULL, name text NOT NULL, description text NULL, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,template_id,language_code), FOREIGN KEY (tenant_id,template_id) REFERENCES foundation_catalog.product_template(tenant_id,id)
);
CREATE TABLE IF NOT EXISTS foundation_catalog.product_reference
(
 tenant_id uuid NOT NULL, id uuid NOT NULL, variant_id uuid NOT NULL, reference_type text NOT NULL CHECK (reference_type IN ('Customer','Supplier','External')), party_id uuid NULL, reference_code text NOT NULL, status text NOT NULL CHECK (status IN ('Active','Archived')), PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,reference_type,party_id,reference_code), FOREIGN KEY (tenant_id,variant_id) REFERENCES foundation_catalog.product_variant(tenant_id,id)
);
CREATE TABLE IF NOT EXISTS foundation_catalog.product_visibility
(
 tenant_id uuid NOT NULL, template_id uuid NOT NULL, scope_type text NOT NULL CHECK (scope_type IN ('TenantWide','LegalEntity')), legal_entity_id uuid NULL, PRIMARY KEY (tenant_id,template_id,scope_type,legal_entity_id), FOREIGN KEY (tenant_id,template_id) REFERENCES foundation_catalog.product_template(tenant_id,id)
);
CREATE TABLE IF NOT EXISTS foundation_catalog.catalog_search_projection
(
 tenant_id uuid NOT NULL, variant_id uuid NOT NULL, template_id uuid NOT NULL, display_name text NOT NULL, display_name_normalized text NOT NULL, name_ar text, name_en text, sku text, normalized_sku text, category_id uuid NULL, category_name text NULL, base_uom_code text NULL, product_nature text NOT NULL, status text NOT NULL, updated_at timestamptz NOT NULL,
 PRIMARY KEY (tenant_id,variant_id), FOREIGN KEY (tenant_id,variant_id) REFERENCES foundation_catalog.product_variant(tenant_id,id)
);

CREATE INDEX IF NOT EXISTS ix_catalog_product_list ON foundation_catalog.catalog_search_projection(tenant_id,status,updated_at DESC,variant_id);
CREATE INDEX IF NOT EXISTS ix_catalog_product_search ON foundation_catalog.catalog_search_projection USING gin (display_name_normalized gin_trgm_ops);
CREATE INDEX IF NOT EXISTS ix_catalog_sku ON foundation_catalog.product_variant(tenant_id,normalized_sku) WHERE normalized_sku IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_catalog_barcode ON foundation_catalog.product_barcode(tenant_id,normalized_value) WHERE status='Active';
CREATE INDEX IF NOT EXISTS ix_catalog_category ON foundation_catalog.product_template(tenant_id,category_id,status);
CREATE INDEX IF NOT EXISTS ix_catalog_uom ON foundation_catalog.product_uom(tenant_id,variant_id,uom_id,status,effective_from DESC);

GRANT SELECT,INSERT,UPDATE,DELETE ON ALL TABLES IN SCHEMA foundation_catalog TO erp_app;
DO $$ DECLARE table_name text; BEGIN
 FOREACH table_name IN ARRAY ARRAY['product_category','brand','uom_dimension','uom','product_template','product_variant','product_uom','product_barcode','product_alias','product_translation','product_reference','product_visibility','catalog_search_projection'] LOOP
  EXECUTE format('ALTER TABLE foundation_catalog.%I ENABLE ROW LEVEL SECURITY',table_name);
  EXECUTE format('ALTER TABLE foundation_catalog.%I FORCE ROW LEVEL SECURITY',table_name);
  EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON foundation_catalog.%I',table_name);
  EXECUTE format('CREATE POLICY tenant_isolation ON foundation_catalog.%I USING (tenant_id = NULLIF(current_setting(''app.tenant_id'',true),'''')::uuid) WITH CHECK (tenant_id = NULLIF(current_setting(''app.tenant_id'',true),'''')::uuid)',table_name);
 END LOOP;
END $$;
