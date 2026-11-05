CREATE SCHEMA business_crm AUTHORIZATION erp_owner;

CREATE TABLE business_crm.lead (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, display_name varchar(300) NOT NULL,
  company_name varchar(300), email varchar(320), phone varchar(80), normalized_email varchar(320), normalized_phone varchar(80),
  source varchar(64) NOT NULL, status varchar(24) NOT NULL DEFAULT 'New' CHECK (status IN ('New','Working','Qualified','Disqualified','Converted','Archived')),
  owner_id uuid, team_id uuid, priority varchar(16) NOT NULL DEFAULT 'Normal' CHECK (priority IN ('Low','Normal','High','Urgent')),
  score integer NOT NULL DEFAULT 0 CHECK (score BETWEEN 0 AND 100), party_id uuid, opportunity_id uuid, qualification jsonb NOT NULL DEFAULT '{}'::jsonb,
  next_action_at timestamptz, disqualified_reason varchar(1000), version bigint NOT NULL DEFAULT 1, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, converted_at timestamptz,
  PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,id)
);
CREATE TABLE business_crm.lead_assignment_history (tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, lead_id uuid NOT NULL, from_owner_id uuid, to_owner_id uuid, from_team_id uuid, to_team_id uuid, actor_id uuid NOT NULL, occurred_at timestamptz NOT NULL, reason varchar(500), PRIMARY KEY (tenant_id,id));
CREATE TABLE business_crm.lead_conversion (tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, lead_id uuid NOT NULL, party_id uuid NOT NULL, opportunity_id uuid NOT NULL, idempotency_key varchar(200) NOT NULL, payload_hash char(64) NOT NULL, converted_by uuid NOT NULL, converted_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,lead_id), UNIQUE (tenant_id,legal_entity_id,lead_id,idempotency_key));
CREATE TABLE business_crm.lead_external_ref (tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, lead_id uuid NOT NULL, source varchar(80) NOT NULL, external_id varchar(300) NOT NULL, payload_hash char(64) NOT NULL, created_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,source,external_id));

CREATE TABLE business_crm.crm_pipeline (tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, code varchar(64) NOT NULL, name varchar(200) NOT NULL, status varchar(16) NOT NULL DEFAULT 'Active' CHECK (status IN ('Active','Archived')), is_default boolean NOT NULL DEFAULT false, version bigint NOT NULL DEFAULT 1, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,code));
CREATE TABLE business_crm.crm_pipeline_stage (tenant_id uuid NOT NULL, id uuid NOT NULL, pipeline_id uuid NOT NULL, legal_entity_id uuid NOT NULL, name varchar(160) NOT NULL, sequence smallint NOT NULL CHECK (sequence > 0), probability numeric(9,6) NOT NULL DEFAULT 0 CHECK (probability BETWEEN 0 AND 1), stale_after_days integer NOT NULL DEFAULT 14 CHECK (stale_after_days > 0), is_won boolean NOT NULL DEFAULT false, is_lost boolean NOT NULL DEFAULT false, status varchar(16) NOT NULL DEFAULT 'Active' CHECK (status IN ('Active','Archived')), version bigint NOT NULL DEFAULT 1, created_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,pipeline_id,sequence), UNIQUE (tenant_id,legal_entity_id,id));
CREATE TABLE business_crm.opportunity (tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, party_id uuid, lead_id uuid, pipeline_id uuid NOT NULL, stage_id uuid NOT NULL, name varchar(300) NOT NULL, owner_id uuid, team_id uuid, estimated_amount numeric(20,6) NOT NULL CHECK (estimated_amount >= 0), currency_code char(3) NOT NULL, expected_close_date date, forecast_category varchar(24) NOT NULL DEFAULT 'Pipeline' CHECK (forecast_category IN ('Pipeline','BestCase','Commit','Won','Omitted')), status varchar(16) NOT NULL DEFAULT 'Open' CHECK (status IN ('Open','Won','Lost','Archived')), loss_reason varchar(200), competitor varchar(200), notes varchar(4000), next_action_at timestamptz, last_activity_at timestamptz, health_score integer NOT NULL DEFAULT 100 CHECK (health_score BETWEEN 0 AND 100), health_status varchar(16) NOT NULL DEFAULT 'Healthy' CHECK (health_status IN ('Healthy','AtRisk','Stale')), version bigint NOT NULL DEFAULT 1, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, closed_at timestamptz, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,id));
CREATE TABLE business_crm.opportunity_stage_history (tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, opportunity_id uuid NOT NULL, from_stage_id uuid, to_stage_id uuid NOT NULL, actor_id uuid NOT NULL, note varchar(1000), occurred_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id));
CREATE TABLE business_crm.opportunity_interest (tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, opportunity_id uuid NOT NULL, product_variant_id uuid, description varchar(1000) NOT NULL, quantity numeric(20,6) NOT NULL DEFAULT 1 CHECK (quantity > 0), PRIMARY KEY (tenant_id,id));
CREATE TABLE business_crm.opportunity_contact_role (tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, opportunity_id uuid NOT NULL, contact_person_id uuid NOT NULL, role varchar(120) NOT NULL, is_primary boolean NOT NULL DEFAULT false, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,opportunity_id,contact_person_id));
CREATE TABLE business_crm.opportunity_source_link (tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, opportunity_id uuid NOT NULL, target_module varchar(32) NOT NULL, target_type varchar(64) NOT NULL, target_id uuid NOT NULL, purpose varchar(64) NOT NULL, idempotency_key varchar(200) NOT NULL, payload_hash char(64) NOT NULL, created_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,opportunity_id,purpose), UNIQUE (tenant_id,target_module,target_type,target_id,purpose));

CREATE TABLE business_crm.crm_activity (tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, lead_id uuid, opportunity_id uuid, party_id uuid, owner_id uuid, type varchar(24) NOT NULL CHECK (type IN ('Call','Meeting','Task','Email','WhatsApp','Note')), subject varchar(300) NOT NULL, state varchar(16) NOT NULL DEFAULT 'Scheduled' CHECK (state IN ('Scheduled','Completed','Cancelled')), due_at timestamptz NOT NULL, occurred_at timestamptz, completed_at timestamptz, outcome varchar(1000), notes varchar(4000), next_action_at timestamptz, version bigint NOT NULL DEFAULT 1, idempotency_key varchar(200), created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,idempotency_key));
CREATE TABLE business_crm.crm_party_profile (tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, party_id uuid NOT NULL, owner_id uuid, team_id uuid, segment varchar(80), relationship_health varchar(16) NOT NULL DEFAULT 'Unknown' CHECK (relationship_health IN ('Unknown','Healthy','AtRisk','Dormant')), do_not_contact boolean NOT NULL DEFAULT false, notes varchar(4000), version bigint NOT NULL DEFAULT 1, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,party_id));
CREATE TABLE business_crm.crm_sales_team (tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, code varchar(64) NOT NULL, name varchar(200) NOT NULL, manager_id uuid, status varchar(16) NOT NULL DEFAULT 'Active' CHECK (status IN ('Active','Inactive')), PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,code));
CREATE TABLE business_crm.crm_sales_team_member (tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, team_id uuid NOT NULL, user_id uuid NOT NULL, role varchar(24) NOT NULL DEFAULT 'Member', active_from date NOT NULL, active_to date, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,team_id,user_id,active_from));
CREATE TABLE business_crm.crm_loss_reason (tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, code varchar(64) NOT NULL, name varchar(200) NOT NULL, status varchar(16) NOT NULL DEFAULT 'Active', PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,code));
CREATE TABLE business_crm.crm_forecast_snapshot (tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, as_of_date date NOT NULL, currency_code char(3) NOT NULL, pipeline_amount numeric(20,6) NOT NULL, weighted_amount numeric(20,6) NOT NULL, best_case_amount numeric(20,6) NOT NULL, commit_amount numeric(20,6) NOT NULL, won_amount numeric(20,6) NOT NULL, payload jsonb NOT NULL DEFAULT '{}'::jsonb, created_by uuid NOT NULL, created_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,as_of_date,currency_code));

CREATE INDEX crm_lead_inbox_idx ON business_crm.lead(tenant_id,legal_entity_id,status,created_at DESC);
CREATE INDEX crm_lead_owner_idx ON business_crm.lead(tenant_id,legal_entity_id,owner_id,status,next_action_at);
CREATE INDEX crm_lead_search_idx ON business_crm.lead(tenant_id,legal_entity_id,normalized_email,normalized_phone);
CREATE INDEX crm_opp_pipeline_idx ON business_crm.opportunity(tenant_id,legal_entity_id,pipeline_id,stage_id,status,expected_close_date);
CREATE INDEX crm_opp_owner_idx ON business_crm.opportunity(tenant_id,legal_entity_id,owner_id,status,expected_close_date);
CREATE INDEX crm_opp_health_idx ON business_crm.opportunity(tenant_id,legal_entity_id,health_status,status,updated_at);
CREATE INDEX crm_activity_worklist_idx ON business_crm.crm_activity(tenant_id,legal_entity_id,owner_id,state,due_at);
CREATE INDEX crm_activity_opp_idx ON business_crm.crm_activity(tenant_id,legal_entity_id,opportunity_id,due_at DESC);

ALTER TABLE business_crm.lead ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_crm.lead_assignment_history ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_crm.lead_conversion ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_crm.lead_external_ref ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_crm.crm_pipeline ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_crm.crm_pipeline_stage ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_crm.opportunity ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_crm.opportunity_stage_history ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_crm.opportunity_interest ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_crm.opportunity_contact_role ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_crm.opportunity_source_link ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_crm.crm_activity ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_crm.crm_party_profile ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_crm.crm_sales_team ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_crm.crm_sales_team_member ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_crm.crm_loss_reason ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_crm.crm_forecast_snapshot ENABLE ROW LEVEL SECURITY;

DO $$
DECLARE item text;
BEGIN
  FOREACH item IN ARRAY ARRAY['lead','lead_assignment_history','lead_conversion','lead_external_ref','crm_pipeline','crm_pipeline_stage','opportunity','opportunity_stage_history','opportunity_interest','opportunity_contact_role','opportunity_source_link','crm_activity','crm_party_profile','crm_sales_team','crm_sales_team_member','crm_loss_reason','crm_forecast_snapshot']
  LOOP
    EXECUTE format('CREATE POLICY tenant_isolation ON business_crm.%I USING (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid) WITH CHECK (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid)', item);
  END LOOP;
END $$;
