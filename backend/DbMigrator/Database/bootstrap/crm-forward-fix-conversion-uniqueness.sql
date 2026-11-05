-- Prevent concurrent lead conversion requests from creating duplicate opportunities.
CREATE UNIQUE INDEX IF NOT EXISTS crm_opportunity_lead_unique
  ON business_crm.opportunity (tenant_id, legal_entity_id, lead_id)
  WHERE lead_id IS NOT NULL;
