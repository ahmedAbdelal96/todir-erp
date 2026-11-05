ALTER TABLE business_crm.opportunity_source_link ADD COLUMN IF NOT EXISTS external_state varchar(32);
ALTER TABLE business_crm.opportunity_source_link ADD COLUMN IF NOT EXISTS last_external_event_at timestamptz;
CREATE TABLE IF NOT EXISTS business_crm.crm_external_event (
  tenant_id uuid NOT NULL,
  id uuid NOT NULL,
  message_id uuid NOT NULL,
  legal_entity_id uuid,
  message_type varchar(120) NOT NULL,
  entity_id uuid,
  payload jsonb NOT NULL,
  occurred_at timestamptz NOT NULL,
  processed_at timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (tenant_id,id),
  UNIQUE (tenant_id,message_id)
);
CREATE INDEX IF NOT EXISTS crm_external_event_entity_idx ON business_crm.crm_external_event(tenant_id,legal_entity_id,message_type,occurred_at DESC);
ALTER TABLE business_crm.crm_external_event ENABLE ROW LEVEL SECURITY;
DO $$ BEGIN
  CREATE POLICY tenant_isolation ON business_crm.crm_external_event USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid) WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
EXCEPTION WHEN duplicate_object THEN NULL; END $$;
GRANT SELECT, INSERT, UPDATE ON business_crm.crm_external_event TO erp_app;
