ALTER TABLE foundation_directory.party_visibility DROP CONSTRAINT IF EXISTS party_visibility_pkey;
ALTER TABLE foundation_directory.party_visibility ADD COLUMN IF NOT EXISTS visibility_id uuid;
UPDATE foundation_directory.party_visibility SET visibility_id = md5(tenant_id::text || ':' || party_id::text || ':' || scope_type || ':' || coalesce(legal_entity_id::text, '') || ':' || coalesce(branch_id::text, ''))::uuid WHERE visibility_id IS NULL;
ALTER TABLE foundation_directory.party_visibility ALTER COLUMN visibility_id SET NOT NULL;
ALTER TABLE foundation_directory.party_visibility ADD CONSTRAINT party_visibility_pkey PRIMARY KEY (tenant_id, visibility_id);
CREATE UNIQUE INDEX IF NOT EXISTS ux_directory_party_visibility_scope ON foundation_directory.party_visibility (tenant_id, party_id, scope_type, (coalesce(legal_entity_id, '00000000-0000-0000-0000-000000000000'::uuid)), (coalesce(branch_id, '00000000-0000-0000-0000-000000000000'::uuid)));
