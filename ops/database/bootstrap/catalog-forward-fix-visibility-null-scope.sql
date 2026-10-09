ALTER TABLE foundation_catalog.product_visibility ADD COLUMN IF NOT EXISTS visibility_id uuid;
UPDATE foundation_catalog.product_visibility SET visibility_id = gen_random_uuid() WHERE visibility_id IS NULL;
ALTER TABLE foundation_catalog.product_visibility ALTER COLUMN visibility_id SET DEFAULT gen_random_uuid();
ALTER TABLE foundation_catalog.product_visibility ALTER COLUMN visibility_id SET NOT NULL;
ALTER TABLE foundation_catalog.product_visibility DROP CONSTRAINT IF EXISTS product_visibility_pkey;
ALTER TABLE foundation_catalog.product_visibility ALTER COLUMN legal_entity_id DROP NOT NULL;
ALTER TABLE foundation_catalog.product_visibility ADD PRIMARY KEY (tenant_id, visibility_id);
DROP INDEX IF EXISTS foundation_catalog.product_visibility_tenant_id_template_id_scope_type_legal_entity_id_key;
CREATE UNIQUE INDEX IF NOT EXISTS ux_catalog_visibility_scope ON foundation_catalog.product_visibility (tenant_id, template_id, scope_type, (coalesce(legal_entity_id, '00000000-0000-0000-0000-000000000000'::uuid)));
