CREATE TABLE IF NOT EXISTS business_crm.crm_custom_field_definition (
  tenant_id uuid NOT NULL,
  id uuid NOT NULL,
  legal_entity_id uuid NOT NULL,
  entity_type varchar(24) NOT NULL CHECK (entity_type IN ('Lead','Opportunity')),
  field_key varchar(80) NOT NULL,
  label varchar(200) NOT NULL,
  data_type varchar(16) NOT NULL CHECK (data_type IN ('Text','Integer','Decimal','Boolean','Date')),
  required_stage_id uuid,
  status varchar(16) NOT NULL DEFAULT 'Active' CHECK (status IN ('Active','Archived')),
  version bigint NOT NULL DEFAULT 1,
  created_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (tenant_id,id),
  UNIQUE (tenant_id,legal_entity_id,entity_type,field_key)
);
CREATE TABLE IF NOT EXISTS business_crm.crm_custom_field_value (
  tenant_id uuid NOT NULL,
  id uuid NOT NULL,
  legal_entity_id uuid NOT NULL,
  entity_type varchar(24) NOT NULL CHECK (entity_type IN ('Lead','Opportunity')),
  entity_id uuid NOT NULL,
  field_id uuid NOT NULL,
  value jsonb NOT NULL,
  version bigint NOT NULL DEFAULT 1,
  updated_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (tenant_id,id),
  UNIQUE (tenant_id,entity_type,entity_id,field_id)
);
CREATE INDEX IF NOT EXISTS crm_custom_field_definition_scope_idx ON business_crm.crm_custom_field_definition(tenant_id,legal_entity_id,entity_type,status);
CREATE INDEX IF NOT EXISTS crm_custom_field_value_entity_idx ON business_crm.crm_custom_field_value(tenant_id,legal_entity_id,entity_type,entity_id);
ALTER TABLE business_crm.crm_custom_field_definition ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_crm.crm_custom_field_value ENABLE ROW LEVEL SECURITY;
DO $$ BEGIN
  CREATE POLICY tenant_isolation ON business_crm.crm_custom_field_definition USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid) WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
DO $$ BEGIN
  CREATE POLICY tenant_isolation ON business_crm.crm_custom_field_value USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid) WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
GRANT SELECT, INSERT, UPDATE, DELETE ON business_crm.crm_custom_field_definition, business_crm.crm_custom_field_value TO erp_app;
