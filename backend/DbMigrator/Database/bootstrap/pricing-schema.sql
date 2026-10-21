CREATE SCHEMA foundation_pricing AUTHORIZATION erp_owner;
REVOKE ALL ON SCHEMA foundation_pricing FROM PUBLIC;
GRANT USAGE ON SCHEMA foundation_pricing TO erp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA foundation_pricing
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;

CREATE TABLE foundation_pricing.price_list (
    tenant_id uuid NOT NULL REFERENCES platform.tenant(id),
    id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    code text NOT NULL,
    name text NOT NULL,
    direction text NOT NULL CHECK (direction IN ('Selling','Buying')),
    currency_code text NOT NULL CHECK (currency_code ~ '^[A-Z]{3}$'),
    entity_scope_type text NOT NULL DEFAULT 'SingleLegalEntity' CHECK (entity_scope_type IN ('SingleLegalEntity','TenantShared')),
    currency_policy text NOT NULL DEFAULT 'ExactCurrency' CHECK (currency_policy IN ('ExactCurrency','AllowFinBaseConversion')),
    status text NOT NULL DEFAULT 'Active' CHECK (status IN ('Active','Archived')),
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    version bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,legal_entity_id,direction,code),
    FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id)
);

CREATE TABLE foundation_pricing.price_list_version (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    price_list_id uuid NOT NULL,
    version_no integer NOT NULL CHECK (version_no > 0),
    status text NOT NULL CHECK (status IN ('Draft','Scheduled','Active','Expired','Archived')),
    effective_from date NOT NULL,
    effective_to date,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    activated_at timestamptz,
    version bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,price_list_id,version_no),
    FOREIGN KEY (tenant_id,price_list_id) REFERENCES foundation_pricing.price_list(tenant_id,id),
    CHECK (effective_to IS NULL OR effective_to > effective_from)
);

CREATE TABLE foundation_pricing.price_group (
    tenant_id uuid NOT NULL REFERENCES platform.tenant(id),
    id uuid NOT NULL,
    code text NOT NULL,
    name text NOT NULL,
    status text NOT NULL DEFAULT 'Active' CHECK (status IN ('Active','Archived')),
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    version bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,code)
);

CREATE TABLE foundation_pricing.price_rule (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    price_list_version_id uuid NOT NULL,
    product_scope text NOT NULL CHECK (product_scope IN ('Variant','Template','Category','Brand','AllProducts')),
    variant_id uuid,
    template_id uuid,
    category_id uuid,
    category_depth integer NOT NULL DEFAULT 0 CHECK (category_depth >= 0),
    brand_id uuid,
    party_scope text NOT NULL CHECK (party_scope IN ('SpecificParty','PriceGroup','AllParties')),
    party_id uuid,
    price_group_id uuid,
    uom_id uuid,
    min_quantity numeric(28,12) NOT NULL DEFAULT 0 CHECK (min_quantity >= 0),
    max_quantity numeric(28,12),
    valid_from date,
    valid_to date,
    priority integer NOT NULL DEFAULT 0,
    result_kind text NOT NULL CHECK (result_kind IN ('FixedUnitPrice','PercentageDiscount','FixedDiscountPerUnit')),
    result_value numeric(28,12) NOT NULL CHECK (result_value >= 0),
    status text NOT NULL DEFAULT 'Active' CHECK (status IN ('Active','Archived')),
    version bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    FOREIGN KEY (tenant_id,price_list_version_id) REFERENCES foundation_pricing.price_list_version(tenant_id,id),
    FOREIGN KEY (tenant_id,price_group_id) REFERENCES foundation_pricing.price_group(tenant_id,id) DEFERRABLE INITIALLY DEFERRED,
    CHECK ((product_scope='Variant' AND variant_id IS NOT NULL AND template_id IS NULL AND category_id IS NULL AND brand_id IS NULL)
        OR (product_scope='Template' AND template_id IS NOT NULL AND variant_id IS NULL AND category_id IS NULL AND brand_id IS NULL)
        OR (product_scope='Category' AND category_id IS NOT NULL AND variant_id IS NULL AND template_id IS NULL AND brand_id IS NULL)
        OR (product_scope='Brand' AND brand_id IS NOT NULL AND variant_id IS NULL AND template_id IS NULL AND category_id IS NULL)
        OR (product_scope='AllProducts' AND variant_id IS NULL AND template_id IS NULL AND category_id IS NULL AND brand_id IS NULL)),
    CHECK ((party_scope='SpecificParty' AND party_id IS NOT NULL AND price_group_id IS NULL)
        OR (party_scope='PriceGroup' AND price_group_id IS NOT NULL AND party_id IS NULL)
        OR (party_scope='AllParties' AND party_id IS NULL AND price_group_id IS NULL)),
    CHECK (max_quantity IS NULL OR max_quantity > min_quantity),
    CHECK (valid_to IS NULL OR valid_from IS NULL OR valid_to > valid_from),
    CHECK (result_kind <> 'PercentageDiscount' OR result_value <= 100)
);

CREATE TABLE foundation_pricing.price_entry (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    price_list_version_id uuid NOT NULL,
    variant_id uuid NOT NULL,
    uom_id uuid NOT NULL,
    party_id uuid,
    price_group_id uuid,
    min_quantity numeric(28,12) NOT NULL DEFAULT 0 CHECK (min_quantity >= 0),
    max_quantity numeric(28,12),
    unit_price numeric(28,12) NOT NULL CHECK (unit_price >= 0),
    valid_from date,
    valid_to date,
    priority integer NOT NULL DEFAULT 0,
    status text NOT NULL DEFAULT 'Active' CHECK (status IN ('Active','Archived')),
    version bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    FOREIGN KEY (tenant_id,price_list_version_id) REFERENCES foundation_pricing.price_list_version(tenant_id,id),
    FOREIGN KEY (tenant_id,price_group_id) REFERENCES foundation_pricing.price_group(tenant_id,id),
    CHECK (max_quantity IS NULL OR max_quantity > min_quantity),
    CHECK (valid_to IS NULL OR valid_from IS NULL OR valid_to > valid_from),
    CHECK (party_id IS NULL OR price_group_id IS NULL)
);

CREATE TABLE foundation_pricing.price_group_member (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    price_group_id uuid NOT NULL,
    party_id uuid NOT NULL,
    valid_from date NOT NULL,
    valid_to date,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,price_group_id,party_id,valid_from),
    FOREIGN KEY (tenant_id,price_group_id) REFERENCES foundation_pricing.price_group(tenant_id,id),
    CHECK (valid_to IS NULL OR valid_to > valid_from)
);

CREATE TABLE foundation_pricing.price_list_assignment (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    direction text NOT NULL CHECK (direction IN ('Selling','Buying')),
    price_list_id uuid NOT NULL,
    party_id uuid,
    price_group_id uuid,
    is_default boolean NOT NULL DEFAULT false,
    valid_from date NOT NULL,
    valid_to date,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    FOREIGN KEY (tenant_id,price_list_id) REFERENCES foundation_pricing.price_list(tenant_id,id),
    FOREIGN KEY (tenant_id,price_group_id) REFERENCES foundation_pricing.price_group(tenant_id,id),
    FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id),
    CHECK (valid_to IS NULL OR valid_to > valid_from),
    CHECK (is_default OR party_id IS NOT NULL OR price_group_id IS NOT NULL),
    CHECK (party_id IS NULL OR price_group_id IS NULL)
);

CREATE UNIQUE INDEX price_list_assignment_default_uq ON foundation_pricing.price_list_assignment(tenant_id,legal_entity_id,direction) WHERE is_default;
CREATE INDEX price_list_entity_direction_idx ON foundation_pricing.price_list(tenant_id,legal_entity_id,direction,status);
CREATE INDEX price_version_effective_idx ON foundation_pricing.price_list_version(tenant_id,price_list_id,status,effective_from,effective_to);
CREATE INDEX price_rule_variant_idx ON foundation_pricing.price_rule(tenant_id,price_list_version_id,variant_id,uom_id,min_quantity) WHERE status='Active';
CREATE INDEX price_rule_template_idx ON foundation_pricing.price_rule(tenant_id,price_list_version_id,template_id,uom_id,min_quantity) WHERE status='Active';
CREATE INDEX price_rule_category_idx ON foundation_pricing.price_rule(tenant_id,price_list_version_id,category_id,category_depth,uom_id,min_quantity) WHERE status='Active';
CREATE INDEX price_rule_party_idx ON foundation_pricing.price_rule(tenant_id,price_list_version_id,party_id,price_group_id) WHERE status='Active';
CREATE INDEX price_rule_general_idx ON foundation_pricing.price_rule(tenant_id,price_list_version_id,product_scope,party_scope,uom_id,min_quantity) WHERE status='Active';
CREATE INDEX price_entry_variant_idx ON foundation_pricing.price_entry(tenant_id,price_list_version_id,variant_id,uom_id,min_quantity) WHERE status='Active';
CREATE INDEX price_entry_party_idx ON foundation_pricing.price_entry(tenant_id,price_list_version_id,party_id,price_group_id) WHERE status='Active';
CREATE INDEX price_group_member_party_idx ON foundation_pricing.price_group_member(tenant_id,party_id,valid_from,valid_to);
CREATE INDEX price_assignment_context_idx ON foundation_pricing.price_list_assignment(tenant_id,legal_entity_id,direction,party_id,price_group_id,valid_from,valid_to);

CREATE TABLE foundation_pricing.pricing_policy (
    tenant_id uuid NOT NULL REFERENCES platform.tenant(id),
    legal_entity_id uuid NOT NULL,
    currency_policy text NOT NULL CHECK (currency_policy IN ('ExactCurrency','AllowFinBaseConversion')),
    allow_uom_derived_price boolean NOT NULL DEFAULT false,
    version bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,legal_entity_id),
    FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id)
);

CREATE TABLE foundation_pricing.pricing_import_batch (
    tenant_id uuid NOT NULL REFERENCES platform.tenant(id),
    id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    price_list_id uuid NOT NULL,
    target_version_id uuid NOT NULL,
    status text NOT NULL CHECK (status IN ('Previewed','Ready','Committed','Rejected')),
    row_count integer NOT NULL CHECK (row_count >= 0),
    error_count integer NOT NULL CHECK (error_count >= 0),
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    FOREIGN KEY (tenant_id,price_list_id) REFERENCES foundation_pricing.price_list(tenant_id,id),
    FOREIGN KEY (tenant_id,target_version_id) REFERENCES foundation_pricing.price_list_version(tenant_id,id),
    FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id)
);
CREATE INDEX pricing_import_recent_idx ON foundation_pricing.pricing_import_batch(tenant_id,legal_entity_id,created_at DESC);

CREATE TABLE foundation_pricing.pricing_import_row (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    batch_id uuid NOT NULL,
    row_number integer NOT NULL CHECK (row_number > 0),
    raw_sku text NOT NULL,
    variant_id uuid,
    uom_id uuid,
    unit_price numeric(28,12),
    currency_code text,
    party_id uuid,
    price_group_id uuid,
    min_quantity numeric(28,12) NOT NULL DEFAULT 0 CHECK (min_quantity >= 0),
    max_quantity numeric(28,12),
    valid_from date,
    valid_to date,
    priority integer NOT NULL DEFAULT 0,
    rule_kind text CHECK (rule_kind IN ('FixedUnitPrice','PercentageDiscount','FixedDiscountPerUnit')),
    error_code text,
    error_message text,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,batch_id,row_number),
    FOREIGN KEY (tenant_id,batch_id) REFERENCES foundation_pricing.pricing_import_batch(tenant_id,id),
    CHECK ((error_code IS NULL AND variant_id IS NOT NULL AND uom_id IS NOT NULL AND unit_price IS NOT NULL AND currency_code IS NOT NULL) OR error_code IS NOT NULL),
    CHECK (max_quantity IS NULL OR max_quantity > min_quantity),
    CHECK (valid_to IS NULL OR valid_from IS NULL OR valid_to > valid_from),
    CHECK (party_id IS NULL OR price_group_id IS NULL),
    CHECK (rule_kind <> 'PercentageDiscount' OR unit_price <= 100)
);

CREATE TABLE foundation_pricing.pricing_audit (
    tenant_id uuid NOT NULL REFERENCES platform.tenant(id),
    id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    price_list_id uuid,
    version_id uuid,
    actor_id uuid NOT NULL,
    membership_id uuid NOT NULL,
    action text NOT NULL,
    occurred_at timestamptz NOT NULL,
    correlation_id uuid NOT NULL,
    details jsonb NOT NULL DEFAULT '{}'::jsonb,
    PRIMARY KEY (tenant_id,id),
    FOREIGN KEY (tenant_id,price_list_id) REFERENCES foundation_pricing.price_list(tenant_id,id),
    FOREIGN KEY (tenant_id,version_id) REFERENCES foundation_pricing.price_list_version(tenant_id,id),
    FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id)
);
CREATE INDEX pricing_audit_entity_time_idx ON foundation_pricing.pricing_audit(tenant_id,legal_entity_id,occurred_at DESC);

CREATE OR REPLACE FUNCTION foundation_pricing.guard_immutable_price_rule() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE version_status text;
BEGIN
    SELECT status INTO version_status FROM foundation_pricing.price_list_version WHERE tenant_id=COALESCE(NEW.tenant_id,OLD.tenant_id) AND id=COALESCE(NEW.price_list_version_id,OLD.price_list_version_id);
    IF version_status IS DISTINCT FROM 'Draft' THEN RAISE EXCEPTION 'pricing.version_immutable' USING ERRCODE='55000'; END IF;
    RETURN COALESCE(NEW,OLD);
END $$;
CREATE TRIGGER price_rule_immutable BEFORE INSERT OR UPDATE OR DELETE ON foundation_pricing.price_rule FOR EACH ROW EXECUTE FUNCTION foundation_pricing.guard_immutable_price_rule();
CREATE TRIGGER price_entry_immutable BEFORE INSERT OR UPDATE OR DELETE ON foundation_pricing.price_entry FOR EACH ROW EXECUTE FUNCTION foundation_pricing.guard_immutable_price_rule();

CREATE OR REPLACE FUNCTION foundation_pricing.guard_immutable_version() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF OLD.status IN ('Scheduled','Active','Expired','Archived') AND (NEW.price_list_id,NEW.version_no,NEW.effective_from,NEW.effective_to,NEW.created_by,NEW.created_at) IS DISTINCT FROM (OLD.price_list_id,OLD.version_no,OLD.effective_from,OLD.effective_to,OLD.created_by,OLD.created_at) THEN
        RAISE EXCEPTION 'pricing.version_immutable' USING ERRCODE='55000';
    END IF;
    IF OLD.status <> 'Draft' AND NEW.status NOT IN (OLD.status,'Active','Expired','Archived') THEN RAISE EXCEPTION 'pricing.version_transition_invalid' USING ERRCODE='55000'; END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER price_version_immutable BEFORE UPDATE ON foundation_pricing.price_list_version FOR EACH ROW EXECUTE FUNCTION foundation_pricing.guard_immutable_version();

CREATE OR REPLACE FUNCTION foundation_pricing.guard_immutable_audit() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'pricing.audit_immutable' USING ERRCODE='55000'; END $$;
CREATE TRIGGER pricing_audit_immutable BEFORE UPDATE OR DELETE ON foundation_pricing.pricing_audit FOR EACH ROW EXECUTE FUNCTION foundation_pricing.guard_immutable_audit();

DO $$
DECLARE table_name text;
BEGIN
    FOREACH table_name IN ARRAY ARRAY['price_list','price_list_version','price_entry','price_rule','price_group','price_group_member','price_list_assignment','pricing_policy','pricing_import_batch','pricing_import_row','pricing_audit'] LOOP
        EXECUTE format('ALTER TABLE foundation_pricing.%I ENABLE ROW LEVEL SECURITY',table_name);
        EXECUTE format('CREATE POLICY tenant_isolation ON foundation_pricing.%I USING (tenant_id=NULLIF(current_setting(''app.tenant_id'',true),'''')::uuid) WITH CHECK (tenant_id=NULLIF(current_setting(''app.tenant_id'',true),'''')::uuid)',table_name);
        EXECUTE format('GRANT SELECT,INSERT,UPDATE,DELETE ON foundation_pricing.%I TO erp_app',table_name);
    END LOOP;
END $$;
REVOKE UPDATE,DELETE ON foundation_pricing.pricing_audit FROM erp_app;

CREATE OR REPLACE FUNCTION foundation_pricing.claim_due_price_versions(p_limit integer)
RETURNS TABLE(tenant_id uuid,legal_entity_id uuid,version_id uuid,business_date date)
LANGUAGE sql
SECURITY DEFINER
SET search_path = pg_catalog, foundation_pricing, platform
AS $$
    SELECT v.tenant_id,le.id,v.id,timezone(le.timezone,statement_timestamp())::date
    FROM foundation_pricing.price_list_version v
    JOIN foundation_pricing.price_list pl ON pl.tenant_id=v.tenant_id AND pl.id=v.price_list_id
    JOIN platform.legal_entity le ON le.tenant_id=v.tenant_id AND le.id=pl.legal_entity_id
    WHERE v.status='Scheduled'
      AND v.effective_from<=timezone(le.timezone,statement_timestamp())::date
      AND pl.status='Active'
      AND le.status='Active'
    ORDER BY v.effective_from,v.tenant_id,v.id
    LIMIT greatest(1,least(p_limit,100));
$$;
ALTER FUNCTION foundation_pricing.claim_due_price_versions(integer) OWNER TO erp_owner;
CREATE POLICY scheduled_activation_system_read ON foundation_pricing.price_list_version FOR SELECT TO erp_owner USING (status='Scheduled');
CREATE POLICY scheduled_activation_list_read ON foundation_pricing.price_list FOR SELECT TO erp_owner USING (true);
CREATE POLICY scheduled_activation_entity_read ON platform.legal_entity FOR SELECT TO erp_owner USING (status='Active');
REVOKE ALL ON FUNCTION foundation_pricing.claim_due_price_versions(integer) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION foundation_pricing.claim_due_price_versions(integer) TO erp_app;
