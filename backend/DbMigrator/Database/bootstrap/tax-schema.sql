CREATE SCHEMA foundation_tax;
GRANT USAGE ON SCHEMA foundation_tax TO erp_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA foundation_tax TO erp_app;

CREATE TABLE foundation_tax.tax_capability_state (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    capability text NOT NULL CHECK (capability IN ('Disabled','Enabled')),
    effective_from date NOT NULL,
    effective_to date,
    version bigint NOT NULL DEFAULT 1,
    updated_by uuid NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, legal_entity_id, effective_from),
    CHECK (effective_to IS NULL OR effective_to > effective_from)
);

CREATE TABLE foundation_tax.tax_registration (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    registration_series_id uuid NOT NULL DEFAULT gen_random_uuid(),
    supersedes_id uuid,
    country_code text NOT NULL CHECK (country_code ~ '^[A-Z]{2}$'),
    scheme_code text NOT NULL,
    registration_number text,
    status text NOT NULL CHECK (status IN ('NotRegistered','PendingRegistration','Registered','RegistrationException','Deregistered')),
    effective_from date NOT NULL,
    effective_to date,
    authority text,
    evidence_reference text,
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,id,registration_series_id),
    FOREIGN KEY (tenant_id,supersedes_id) REFERENCES foundation_tax.tax_registration(tenant_id,id),
    CHECK (effective_to IS NULL OR effective_to > effective_from),
    CHECK (status <> 'Registered' OR registration_number IS NOT NULL)
);
CREATE INDEX tax_registration_history ON foundation_tax.tax_registration(tenant_id,registration_series_id,effective_from);
CREATE UNIQUE INDEX tax_registration_current_number ON foundation_tax.tax_registration(tenant_id,country_code,scheme_code,registration_number) WHERE effective_to IS NULL AND registration_number IS NOT NULL AND status <> 'Deregistered';

CREATE TABLE foundation_tax.tax_registration_member (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    registration_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    effective_from date NOT NULL,
    effective_to date,
    membership_status text NOT NULL CHECK (membership_status IN ('Active','Pending','Ended')),
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,registration_id,legal_entity_id,effective_from),
    FOREIGN KEY (tenant_id,registration_id) REFERENCES foundation_tax.tax_registration(tenant_id,id),
    CHECK (effective_to IS NULL OR effective_to > effective_from)
);

CREATE TABLE foundation_tax.tax_scheme (
    tenant_id uuid NOT NULL, code text NOT NULL, country_code text NOT NULL,
    name text NOT NULL, status text NOT NULL CHECK (status IN ('Active','Inactive')),
    source_kind text NOT NULL DEFAULT 'TenantConfiguration' CHECK (source_kind IN ('CountryPack','TenantConfiguration','SystemReference')),
    mutable boolean NOT NULL DEFAULT true,
    PRIMARY KEY (tenant_id,code), CHECK (country_code ~ '^[A-Z]{2}$'), CHECK (source_kind='TenantConfiguration' OR NOT mutable)
);
CREATE TABLE foundation_tax.tax_type (
    tenant_id uuid NOT NULL, code text NOT NULL, scheme_code text NOT NULL,
    name text NOT NULL, status text NOT NULL CHECK (status IN ('Active','Inactive')),
    source_kind text NOT NULL DEFAULT 'TenantConfiguration' CHECK (source_kind IN ('CountryPack','TenantConfiguration','SystemReference')),
    mutable boolean NOT NULL DEFAULT true,
    PRIMARY KEY (tenant_id,code), FOREIGN KEY (tenant_id,scheme_code) REFERENCES foundation_tax.tax_scheme(tenant_id,code), CHECK (source_kind='TenantConfiguration' OR NOT mutable)
);
CREATE TABLE foundation_tax.tax_jurisdiction (
    tenant_id uuid NOT NULL, code text NOT NULL, country_code text NOT NULL,
    parent_code text, subdivision_type text NOT NULL, name text NOT NULL,
    status text NOT NULL CHECK (status IN ('Active','Inactive')),
    source_kind text NOT NULL DEFAULT 'TenantConfiguration' CHECK (source_kind IN ('CountryPack','TenantConfiguration','SystemReference')),
    mutable boolean NOT NULL DEFAULT true,
    PRIMARY KEY (tenant_id,code), CHECK (country_code ~ '^[A-Z]{2}$'), CHECK (source_kind='TenantConfiguration' OR NOT mutable)
);
CREATE TABLE foundation_tax.tax_category (
    tenant_id uuid NOT NULL, id uuid NOT NULL, code text NOT NULL, name text NOT NULL,
    scope text NOT NULL CHECK (scope IN ('Product','Party','Charge')),
    status text NOT NULL CHECK (status IN ('Active','Inactive')),
    source_kind text NOT NULL DEFAULT 'TenantConfiguration' CHECK (source_kind IN ('CountryPack','TenantConfiguration','SystemReference')),
    mutable boolean NOT NULL DEFAULT true,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,code,scope), CHECK (source_kind='TenantConfiguration' OR NOT mutable)
);
CREATE TABLE foundation_tax.tax_policy (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, policy_code text NOT NULL,
    rounding_strategy text NOT NULL CHECK (rounding_strategy IN ('PerLine','PerTaxComponent','PerDocument')),
    money_scale smallint NOT NULL CHECK (money_scale BETWEEN 0 AND 8),
    tax_date_basis text NOT NULL CHECK (tax_date_basis IN ('Explicit','SupplyDate','DocumentDate','PostingDate')),
    timezone_id text NOT NULL, version bigint NOT NULL DEFAULT 1, updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,legal_entity_id)
);
CREATE TABLE foundation_tax.party_tax_profile (
    tenant_id uuid NOT NULL, id uuid NOT NULL, party_id uuid NOT NULL,
    profile_series_id uuid NOT NULL DEFAULT gen_random_uuid(), supersedes_id uuid,
    legal_entity_id uuid, registration_id uuid, country_code text NOT NULL,
    scheme_code text, category_id uuid, treatment text, registration_number text,
    registration_country_code text, registration_status text,
    reverse_charge_eligible boolean NOT NULL DEFAULT false,
    exemption_evidence_reference text, effective_from date NOT NULL, effective_to date,
    version bigint NOT NULL DEFAULT 1, created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), FOREIGN KEY (tenant_id,supersedes_id) REFERENCES foundation_tax.party_tax_profile(tenant_id,id), FOREIGN KEY (tenant_id,registration_id) REFERENCES foundation_tax.tax_registration(tenant_id,id),
    FOREIGN KEY (tenant_id,category_id) REFERENCES foundation_tax.tax_category(tenant_id,id),
    CHECK (effective_to IS NULL OR effective_to > effective_from),
    CHECK (treatment IS NULL OR treatment IN ('StandardRated','ReducedRated','ZeroRated','Exempt','OutOfScope','ReverseCharge','Import','Export','SpecialScheme'))
);
CREATE TABLE foundation_tax.product_tax_profile (
    tenant_id uuid NOT NULL, id uuid NOT NULL, product_template_id uuid NOT NULL, product_variant_id uuid,
    profile_series_id uuid NOT NULL DEFAULT gen_random_uuid(), supersedes_id uuid,
    legal_entity_id uuid, country_code text, scheme_code text, category_id uuid NOT NULL,
    effective_from date NOT NULL, effective_to date, version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), FOREIGN KEY (tenant_id,supersedes_id) REFERENCES foundation_tax.product_tax_profile(tenant_id,id), FOREIGN KEY (tenant_id,category_id) REFERENCES foundation_tax.tax_category(tenant_id,id),
    CHECK (effective_to IS NULL OR effective_to > effective_from)
);
CREATE TABLE foundation_tax.tax_pack_activation (
    tenant_id uuid NOT NULL, id uuid NOT NULL, country_code text NOT NULL, pack_code text NOT NULL,
    pack_version text NOT NULL, authority_reference text NOT NULL, effective_from date NOT NULL,
    effective_to date, review_status text NOT NULL CHECK (review_status IN ('Unreviewed','InReview','Approved','Rejected')),
    reviewed_by uuid, reviewed_at timestamptz, activated_at timestamptz,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,country_code,pack_code,pack_version),
    CHECK (effective_to IS NULL OR effective_to > effective_from),
    CHECK (review_status <> 'Approved' OR (reviewed_by IS NOT NULL AND reviewed_at IS NOT NULL))
);
CREATE TABLE foundation_tax.tax_rule_set (
    tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid, country_code text NOT NULL, scheme_code text,
    pack_activation_id uuid, version integer NOT NULL, status text NOT NULL CHECK (status IN ('Draft','Approved','Scheduled','Active','Superseded','Retired')),
    effective_from date NOT NULL, effective_to date, source_kind text NOT NULL CHECK (source_kind IN ('CountryPack','TenantConfiguration','GovernmentIntegrationSystemReference')),
    mutable boolean NOT NULL, approved_by uuid, approved_at timestamptz, activated_at timestamptz,
    created_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,country_code,version),
    FOREIGN KEY (tenant_id,pack_activation_id) REFERENCES foundation_tax.tax_pack_activation(tenant_id,id),
    FOREIGN KEY (tenant_id,scheme_code) REFERENCES foundation_tax.tax_scheme(tenant_id,code),
    CHECK (effective_to IS NULL OR effective_to > effective_from),
    CHECK (source_kind = 'TenantConfiguration' OR mutable = false)
);
CREATE TABLE foundation_tax.tax_stack_definition (
    tenant_id uuid NOT NULL, id uuid NOT NULL, rule_set_id uuid NOT NULL, code text NOT NULL,
    status text NOT NULL CHECK (status IN ('Draft','Approved','Active','Retired')),
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,rule_set_id,code),
    FOREIGN KEY (tenant_id,rule_set_id) REFERENCES foundation_tax.tax_rule_set(tenant_id,id)
);
CREATE TABLE foundation_tax.tax_rule_version (
    tenant_id uuid NOT NULL, id uuid NOT NULL, rule_id uuid NOT NULL, version integer NOT NULL,
    treatment text NOT NULL CHECK (treatment IN ('StandardRated','ReducedRated','ZeroRated','Exempt','OutOfScope','ReverseCharge','Import','Export','SpecialScheme')),
    effective_from date NOT NULL, effective_to date, source_reference text NOT NULL, legal_reason_code text,
    locked_by_country_pack boolean NOT NULL, immutable boolean NOT NULL,
    created_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,rule_id,version),
    CHECK (effective_to IS NULL OR effective_to > effective_from), CHECK (NOT locked_by_country_pack OR immutable)
);
CREATE TABLE foundation_tax.tax_rule (
    tenant_id uuid NOT NULL, id uuid NOT NULL, rule_set_id uuid NOT NULL, stack_id uuid,
    rule_version_id uuid NOT NULL, explicit_priority integer NOT NULL DEFAULT 0,
    registration_id uuid, jurisdiction_code text, direction text CHECK (direction IN ('Sales','Purchase')),
    party_category_id uuid, product_category_id uuid, document_kind text, transaction_type text,
    place_of_supply_country text, tax_date_from date NOT NULL, tax_date_to date,
    legal_reason_code text, legal_reason_text text,
    PRIMARY KEY (tenant_id,id), FOREIGN KEY (tenant_id,rule_set_id) REFERENCES foundation_tax.tax_rule_set(tenant_id,id),
    FOREIGN KEY (tenant_id,stack_id) REFERENCES foundation_tax.tax_stack_definition(tenant_id,id),
    FOREIGN KEY (tenant_id,rule_version_id) REFERENCES foundation_tax.tax_rule_version(tenant_id,id),
    FOREIGN KEY (tenant_id,registration_id) REFERENCES foundation_tax.tax_registration(tenant_id,id),
    FOREIGN KEY (tenant_id,party_category_id) REFERENCES foundation_tax.tax_category(tenant_id,id),
    FOREIGN KEY (tenant_id,product_category_id) REFERENCES foundation_tax.tax_category(tenant_id,id),
    CHECK (tax_date_to IS NULL OR tax_date_to > tax_date_from)
);
ALTER TABLE foundation_tax.tax_rule_version
    ADD CONSTRAINT tax_rule_version_rule_fk
    FOREIGN KEY (tenant_id,rule_id) REFERENCES foundation_tax.tax_rule(tenant_id,id)
    DEFERRABLE INITIALLY DEFERRED;
CREATE TABLE foundation_tax.tax_component_definition (
    tenant_id uuid NOT NULL, id uuid NOT NULL, stack_id uuid NOT NULL, sequence integer NOT NULL,
    tax_type_code text NOT NULL, tax_code text NOT NULL, treatment text NOT NULL,
    calculation_method text NOT NULL CHECK (calculation_method IN ('PercentageOfNet','PercentageExtractedFromGross','FixedPerUnit','FixedPerLine','PercentageOfModifiedBase')),
    rate numeric(12,8), fixed_amount numeric(28,8), currency_code text NOT NULL,
    base_component_sequence integer, recoverable_percentage numeric(7,4), legal_reason_code text, reporting_code text,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,stack_id,sequence),
    FOREIGN KEY (tenant_id,stack_id) REFERENCES foundation_tax.tax_stack_definition(tenant_id,id),
    CHECK ((calculation_method IN ('PercentageOfNet','PercentageExtractedFromGross','PercentageOfModifiedBase') AND rate IS NOT NULL AND fixed_amount IS NULL) OR (calculation_method IN ('FixedPerUnit','FixedPerLine') AND fixed_amount IS NOT NULL AND rate IS NULL)),
    CHECK (rate IS NULL OR rate BETWEEN 0 AND 1000), CHECK (fixed_amount IS NULL OR fixed_amount >= 0),
    CHECK (recoverable_percentage IS NULL OR recoverable_percentage BETWEEN 0 AND 100),
    CHECK (base_component_sequence IS NULL OR base_component_sequence <> sequence)
);
CREATE TABLE foundation_tax.tax_audit (
    tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid, actor_id uuid NOT NULL,
    operation text NOT NULL, entity_type text NOT NULL, entity_id uuid NOT NULL,
    before_state jsonb, after_state jsonb, occurred_at timestamptz NOT NULL, correlation_id uuid NOT NULL,
    PRIMARY KEY (tenant_id,id)
);
CREATE TABLE foundation_tax.tax_import_batch (
    tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, import_type text NOT NULL,
    status text NOT NULL CHECK (status IN ('Previewed','Ready','Committed','Rejected')),
    created_by uuid NOT NULL, created_at timestamptz NOT NULL, source_reference text,
    row_count integer NOT NULL DEFAULT 0, accepted_count integer NOT NULL DEFAULT 0,
    rejected_count integer NOT NULL DEFAULT 0, skipped_count integer NOT NULL DEFAULT 0,
    committed_at timestamptz, PRIMARY KEY (tenant_id,id), CHECK (row_count>=0),
    CHECK (accepted_count>=0 AND rejected_count>=0 AND skipped_count>=0)
);
CREATE TABLE foundation_tax.tax_import_row (
    tenant_id uuid NOT NULL, id uuid NOT NULL, batch_id uuid NOT NULL, row_number integer NOT NULL,
    classification text NOT NULL CHECK (classification IN ('Create','CreateNewVersion','InvalidTaxNumberFormat','InvalidTaxCategory','InvalidScheme','InvalidTreatment','UnknownProduct','AmbiguousProduct','UnknownParty','AmbiguousParty','AmbiguousReference','EffectiveDateOverlap','DuplicateInput','LockedStatutoryContent','Conflict')),
    row_data jsonb NOT NULL, message text, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,batch_id,row_number),
    FOREIGN KEY (tenant_id,batch_id) REFERENCES foundation_tax.tax_import_batch(tenant_id,id)
);
CREATE TABLE foundation_tax.tax_snapshot (
    tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL,
    source_document_kind text NOT NULL, source_document_id uuid NOT NULL, source_line_id uuid,
    tax_date date NOT NULL, calculation_version text NOT NULL, rule_set_version text,
    evaluation_state text NOT NULL, treatment text, result jsonb NOT NULL,
    created_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id),
    UNIQUE NULLS NOT DISTINCT (tenant_id,source_document_kind,source_document_id,source_line_id)
);

CREATE FUNCTION foundation_tax.reject_tax_snapshot_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'tax.snapshot_immutable' USING ERRCODE = '55000';
END
$$;
CREATE TRIGGER tax_snapshot_immutable BEFORE UPDATE OR DELETE ON foundation_tax.tax_snapshot
FOR EACH ROW EXECUTE FUNCTION foundation_tax.reject_tax_snapshot_mutation();

CREATE FUNCTION foundation_tax.guard_locked_rule_version() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF OLD.immutable OR OLD.locked_by_country_pack THEN
        RAISE EXCEPTION 'tax.rule_version_immutable' USING ERRCODE = '55000';
    END IF;
    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;
    RETURN NEW;
END
$$;
CREATE TRIGGER tax_rule_version_immutable BEFORE UPDATE OR DELETE ON foundation_tax.tax_rule_version
FOR EACH ROW EXECUTE FUNCTION foundation_tax.guard_locked_rule_version();

CREATE FUNCTION foundation_tax.guard_active_component_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE old_tenant uuid; old_stack uuid; new_tenant uuid; new_stack uuid;
BEGIN
    old_tenant := OLD.tenant_id;
    old_stack := OLD.stack_id;
    IF TG_OP <> 'DELETE' THEN
        new_tenant := NEW.tenant_id;
        new_stack := NEW.stack_id;
    END IF;
    IF EXISTS (
        SELECT 1 FROM foundation_tax.tax_stack_definition stack
        JOIN foundation_tax.tax_rule_set rule_set ON rule_set.tenant_id=stack.tenant_id AND rule_set.id=stack.rule_set_id
        WHERE (stack.tenant_id=old_tenant AND stack.id=old_stack OR stack.tenant_id=new_tenant AND stack.id=new_stack)
          AND rule_set.status IN ('Approved','Scheduled','Active','Superseded','Retired')
          AND (rule_set.source_kind='CountryPack' OR NOT rule_set.mutable)
    ) THEN
        RAISE EXCEPTION 'tax.locked_component_immutable' USING ERRCODE = '55000';
    END IF;
    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;
    RETURN NEW;
END
$$;
CREATE TRIGGER tax_component_locked BEFORE UPDATE OR DELETE ON foundation_tax.tax_component_definition
FOR EACH ROW EXECUTE FUNCTION foundation_tax.guard_active_component_mutation();

CREATE INDEX tax_registration_entity_lookup ON foundation_tax.tax_registration_member(tenant_id,legal_entity_id,effective_from,effective_to);
CREATE INDEX tax_party_profile_effective ON foundation_tax.party_tax_profile(tenant_id,party_id,legal_entity_id,effective_from,effective_to);
CREATE INDEX tax_product_profile_effective ON foundation_tax.product_tax_profile(tenant_id,product_template_id,product_variant_id,legal_entity_id,effective_from,effective_to);
CREATE INDEX tax_party_profile_history ON foundation_tax.party_tax_profile(tenant_id,profile_series_id,effective_from);
CREATE INDEX tax_product_profile_history ON foundation_tax.product_tax_profile(tenant_id,profile_series_id,effective_from);
CREATE INDEX tax_rule_match ON foundation_tax.tax_rule(tenant_id,rule_set_id,direction,document_kind,product_category_id,party_category_id,tax_date_from,tax_date_to);
CREATE INDEX tax_rule_set_active ON foundation_tax.tax_rule_set(tenant_id,legal_entity_id,country_code,effective_from,effective_to) WHERE status IN ('Approved','Scheduled','Active');
CREATE INDEX tax_audit_entity_time ON foundation_tax.tax_audit(tenant_id,entity_type,entity_id,occurred_at DESC);

DO $$
DECLARE table_name text;
BEGIN
  FOREACH table_name IN ARRAY ARRAY['tax_capability_state','tax_registration','tax_registration_member','tax_scheme','tax_type','tax_jurisdiction','tax_category','tax_policy','party_tax_profile','product_tax_profile','tax_pack_activation','tax_rule_set','tax_stack_definition','tax_rule_version','tax_rule','tax_component_definition','tax_audit','tax_import_batch','tax_import_row','tax_snapshot']
  LOOP
    EXECUTE format('ALTER TABLE foundation_tax.%I ENABLE ROW LEVEL SECURITY',table_name);
    EXECUTE format('ALTER TABLE foundation_tax.%I FORCE ROW LEVEL SECURITY',table_name);
    EXECUTE format('CREATE POLICY tenant_isolation ON foundation_tax.%I USING (tenant_id=NULLIF(current_setting(''app.tenant_id'',true),'''')::uuid) WITH CHECK (tenant_id=NULLIF(current_setting(''app.tenant_id'',true),'''')::uuid)',table_name);
    EXECUTE format('GRANT SELECT,INSERT,UPDATE,DELETE ON foundation_tax.%I TO erp_app',table_name);
  END LOOP;
END $$;
