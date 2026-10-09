CREATE SCHEMA IF NOT EXISTS foundation_directory AUTHORIZATION erp_owner;
REVOKE ALL ON SCHEMA foundation_directory FROM PUBLIC;
GRANT USAGE ON SCHEMA foundation_directory TO erp_app;
CREATE EXTENSION IF NOT EXISTS pg_trgm;

CREATE TABLE IF NOT EXISTS foundation_directory.party
(
    tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant(id), id uuid NOT NULL, party_type text NOT NULL CHECK (party_type IN ('Person','Organization')),
    status text NOT NULL CHECK (status IN ('Draft','Active','Blocked','Archived','Merged')), display_name text NOT NULL CHECK (length(display_name) BETWEEN 1 AND 300), display_name_normalized text NOT NULL,
    legal_name text, trade_name text, name_ar text, name_en text, first_name text, middle_name text, last_name text, country_code text, primary_language text,
    created_by uuid NOT NULL, updated_by uuid NOT NULL, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, version bigint NOT NULL DEFAULT 1,
    PRIMARY KEY (tenant_id, id), UNIQUE (tenant_id, id)
);
CREATE TABLE IF NOT EXISTS foundation_directory.party_role
(
    tenant_id uuid NOT NULL, party_id uuid NOT NULL, role_code text NOT NULL CHECK (role_code IN ('Customer','Supplier')), status text NOT NULL CHECK (status IN ('Active','Inactive')),
    created_by uuid NOT NULL, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, version bigint NOT NULL DEFAULT 1,
    PRIMARY KEY (tenant_id, party_id, role_code), FOREIGN KEY (tenant_id, party_id) REFERENCES foundation_directory.party(tenant_id, id)
);
CREATE TABLE IF NOT EXISTS foundation_directory.party_identifier
(
    tenant_id uuid NOT NULL, id uuid NOT NULL, party_id uuid NOT NULL, identifier_type text NOT NULL, country_code text, value text NOT NULL, normalized_value text NOT NULL,
    authority text, valid_from date, valid_to date, is_primary boolean NOT NULL DEFAULT false, verification_status text NOT NULL DEFAULT 'Unverified', created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id), UNIQUE (tenant_id, party_id, identifier_type, normalized_value), FOREIGN KEY (tenant_id, party_id) REFERENCES foundation_directory.party(tenant_id, id)
);
CREATE TABLE IF NOT EXISTS foundation_directory.contact_person
(
    tenant_id uuid NOT NULL, id uuid NOT NULL, display_name text NOT NULL, display_name_normalized text NOT NULL, job_title text, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id)
);
CREATE TABLE IF NOT EXISTS foundation_directory.contact_point
(
    tenant_id uuid NOT NULL, id uuid NOT NULL, party_id uuid, contact_person_id uuid, contact_type text NOT NULL CHECK (contact_type IN ('Email','Phone','Website','Other')),
    value text NOT NULL, normalized_value text NOT NULL, label text, is_primary boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id), CHECK ((party_id IS NOT NULL) <> (contact_person_id IS NOT NULL)),
    FOREIGN KEY (tenant_id, party_id) REFERENCES foundation_directory.party(tenant_id, id), FOREIGN KEY (tenant_id, contact_person_id) REFERENCES foundation_directory.contact_person(tenant_id, id)
);
CREATE TABLE IF NOT EXISTS foundation_directory.party_contact
(
    tenant_id uuid NOT NULL, party_id uuid NOT NULL, contact_person_id uuid NOT NULL, relationship_type text, is_primary boolean NOT NULL DEFAULT false,
    PRIMARY KEY (tenant_id, party_id, contact_person_id), FOREIGN KEY (tenant_id, party_id) REFERENCES foundation_directory.party(tenant_id, id), FOREIGN KEY (tenant_id, contact_person_id) REFERENCES foundation_directory.contact_person(tenant_id, id)
);
CREATE TABLE IF NOT EXISTS foundation_directory.address
(
    tenant_id uuid NOT NULL, id uuid NOT NULL, country_code text NOT NULL, city text, region text, postal_code text, line1 text NOT NULL, line2 text, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id)
);
CREATE TABLE IF NOT EXISTS foundation_directory.party_address
(
    tenant_id uuid NOT NULL, party_id uuid NOT NULL, address_id uuid NOT NULL, usage text NOT NULL CHECK (usage IN ('General','Billing','Shipping','Registered')), is_default boolean NOT NULL DEFAULT false,
    PRIMARY KEY (tenant_id, party_id, address_id, usage), FOREIGN KEY (tenant_id, party_id) REFERENCES foundation_directory.party(tenant_id, id), FOREIGN KEY (tenant_id, address_id) REFERENCES foundation_directory.address(tenant_id, id)
);
CREATE TABLE IF NOT EXISTS foundation_directory.party_alias
(
    tenant_id uuid NOT NULL, id uuid NOT NULL, party_id uuid NOT NULL, alias text NOT NULL, alias_normalized text NOT NULL, alias_type text, created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id), FOREIGN KEY (tenant_id, party_id) REFERENCES foundation_directory.party(tenant_id, id)
);
CREATE TABLE IF NOT EXISTS foundation_directory.party_relationship
(
    tenant_id uuid NOT NULL, id uuid NOT NULL, from_party_id uuid NOT NULL, to_party_id uuid NOT NULL, relationship_type text NOT NULL, valid_from date, valid_to date, created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id), FOREIGN KEY (tenant_id, from_party_id) REFERENCES foundation_directory.party(tenant_id, id), FOREIGN KEY (tenant_id, to_party_id) REFERENCES foundation_directory.party(tenant_id, id), CHECK (from_party_id <> to_party_id)
);
CREATE TABLE IF NOT EXISTS foundation_directory.party_merge_redirect
(
    tenant_id uuid NOT NULL, source_party_id uuid NOT NULL, target_party_id uuid NOT NULL, merged_at timestamptz NOT NULL, merged_by uuid NOT NULL,
    PRIMARY KEY (tenant_id, source_party_id), FOREIGN KEY (tenant_id, source_party_id) REFERENCES foundation_directory.party(tenant_id, id), FOREIGN KEY (tenant_id, target_party_id) REFERENCES foundation_directory.party(tenant_id, id)
);
CREATE TABLE IF NOT EXISTS foundation_directory.party_visibility
(
    tenant_id uuid NOT NULL, party_id uuid NOT NULL, scope_type text NOT NULL CHECK (scope_type IN ('TenantWide','LegalEntity','Branch')), legal_entity_id uuid, branch_id uuid,
    PRIMARY KEY (tenant_id, party_id, scope_type, legal_entity_id, branch_id), FOREIGN KEY (tenant_id, party_id) REFERENCES foundation_directory.party(tenant_id, id)
);
CREATE TABLE IF NOT EXISTS foundation_directory.external_reference
(
    tenant_id uuid NOT NULL, id uuid NOT NULL, party_id uuid NOT NULL, system_code text NOT NULL, external_key text NOT NULL, created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id), UNIQUE (tenant_id, system_code, external_key), FOREIGN KEY (tenant_id, party_id) REFERENCES foundation_directory.party(tenant_id, id)
);
CREATE TABLE IF NOT EXISTS foundation_directory.duplicate_candidate
(
    tenant_id uuid NOT NULL, id uuid NOT NULL, party_id uuid NOT NULL, candidate_party_id uuid NOT NULL, reason text NOT NULL, confidence numeric(5,4) NOT NULL, status text NOT NULL DEFAULT 'Open', created_at timestamptz NOT NULL, resolved_at timestamptz,
    PRIMARY KEY (tenant_id, id), FOREIGN KEY (tenant_id, party_id) REFERENCES foundation_directory.party(tenant_id, id), FOREIGN KEY (tenant_id, candidate_party_id) REFERENCES foundation_directory.party(tenant_id, id), CHECK (party_id <> candidate_party_id)
);

CREATE INDEX IF NOT EXISTS ix_directory_party_list ON foundation_directory.party(tenant_id, status, updated_at DESC, id);
CREATE INDEX IF NOT EXISTS ix_directory_party_name ON foundation_directory.party USING gin (display_name_normalized gin_trgm_ops);
CREATE INDEX IF NOT EXISTS ix_directory_identifier_lookup ON foundation_directory.party_identifier(tenant_id, identifier_type, normalized_value);
CREATE INDEX IF NOT EXISTS ix_directory_contact_lookup ON foundation_directory.contact_point(tenant_id, contact_type, normalized_value);
CREATE INDEX IF NOT EXISTS ix_directory_role_lookup ON foundation_directory.party_role(tenant_id, role_code, status, party_id);
CREATE INDEX IF NOT EXISTS ix_directory_duplicate_party ON foundation_directory.duplicate_candidate(tenant_id, party_id, status);

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA foundation_directory TO erp_app;
DO $$ DECLARE table_name text; BEGIN
  FOREACH table_name IN ARRAY ARRAY['party','party_role','party_identifier','contact_person','contact_point','party_contact','address','party_address','party_alias','party_relationship','party_merge_redirect','party_visibility','external_reference','duplicate_candidate'] LOOP
    EXECUTE format('ALTER TABLE foundation_directory.%I ENABLE ROW LEVEL SECURITY', table_name);
    EXECUTE format('ALTER TABLE foundation_directory.%I FORCE ROW LEVEL SECURITY', table_name);
    EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON foundation_directory.%I', table_name);
    EXECUTE format('CREATE POLICY tenant_isolation ON foundation_directory.%I USING (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid) WITH CHECK (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid)', table_name);
  END LOOP;
END $$;
