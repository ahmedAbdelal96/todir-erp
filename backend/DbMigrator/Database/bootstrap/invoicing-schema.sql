SET search_path = business_invoicing, public;
SET row_security = off;

CREATE SCHEMA IF NOT EXISTS business_invoicing AUTHORIZATION erp_owner;
REVOKE ALL ON SCHEMA business_invoicing FROM PUBLIC;
GRANT USAGE ON SCHEMA business_invoicing TO erp_app;

CREATE TABLE business_invoicing.invoice (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    branch_id uuid,
    direction text NOT NULL,
    document_type text NOT NULL DEFAULT 'Invoice',
    lifecycle_state text NOT NULL DEFAULT 'Draft',
    party_id uuid NOT NULL,
    address_id uuid,
    document_date date NOT NULL,
    posting_date date NOT NULL,
    tax_date date NOT NULL,
    supply_date date,
    currency_code char(3) NOT NULL,
    price_mode text NOT NULL DEFAULT 'Exclusive',
    payment_term_id uuid,
    document_discount_amount numeric(20,6) NOT NULL DEFAULT 0,
    supplier_invoice_number text,
    supplier_invoice_number_key text,
    document_number text,
    source_module text,
    source_document_id uuid,
    source_purpose text,
    source_idempotency_key text,
    source_payload_hash char(64),
    net_total numeric(20,6),
    taxable_total numeric(20,6),
    discount_total numeric(20,6),
    charge_total numeric(20,6),
    tax_total numeric(20,6),
    gross_total numeric(20,6),
    rounding_adjustment numeric(20,6),
    finalization_key text,
    finalized_at timestamptz,
    finalized_by uuid,
    journal_entry_id uuid,
    row_version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, id),
    UNIQUE (tenant_id, legal_entity_id, document_number),
    UNIQUE (tenant_id, legal_entity_id, finalization_key),
    CONSTRAINT ux_invoice_source_document UNIQUE (tenant_id, legal_entity_id, source_module, source_document_id, source_purpose),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (direction IN ('Receivable','Payable')),
    CHECK (document_type IN ('Invoice','CreditNote','DebitNote')),
    CHECK (lifecycle_state IN ('Draft','Finalized','CorrectedOrCancelled','CancelledDraft')),
    CHECK (currency_code ~ '^[A-Z]{3}$'),
    CHECK (price_mode IN ('Exclusive','Inclusive')),
    CHECK (row_version > 0),
    CHECK (document_discount_amount >= 0),
    CHECK ((lifecycle_state IN ('Draft','CancelledDraft')) = (finalized_at IS NULL)),
    CHECK ((lifecycle_state IN ('Draft','CancelledDraft')) = (document_number IS NULL)),
    CHECK ((lifecycle_state IN ('Draft','CancelledDraft')) = (gross_total IS NULL)),
    CHECK (supplier_invoice_number IS NULL OR length(btrim(supplier_invoice_number)) BETWEEN 1 AND 160),
    CHECK (supplier_invoice_number_key IS NULL OR length(btrim(supplier_invoice_number_key)) BETWEEN 1 AND 160),
    CHECK ((source_module IS NULL) = (source_document_id IS NULL)),
    CHECK ((source_module IS NULL) = (source_purpose IS NULL)),
    CHECK ((source_module IS NULL) = (source_idempotency_key IS NULL)),
    CHECK ((source_module IS NULL) = (source_payload_hash IS NULL)),
    CHECK (source_module IS NULL OR source_module IN ('sales','purchasing')),
    CHECK (source_purpose IS NULL OR length(btrim(source_purpose)) BETWEEN 1 AND 80),
    CHECK (source_idempotency_key IS NULL OR length(btrim(source_idempotency_key)) BETWEEN 1 AND 200),
    CHECK (source_payload_hash IS NULL OR source_payload_hash ~ '^[0-9a-f]{64}$'),
    CHECK (net_total IS NULL OR net_total >= 0),
    CHECK (taxable_total IS NULL OR taxable_total >= 0),
    CHECK (discount_total IS NULL OR discount_total >= 0),
    CHECK (charge_total IS NULL OR charge_total >= 0),
    CHECK (tax_total IS NULL OR tax_total >= 0),
    CHECK (gross_total IS NULL OR gross_total >= 0)
);

CREATE TABLE business_invoicing.invoice_line (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    invoice_id uuid NOT NULL,
    id uuid NOT NULL,
    sequence integer NOT NULL,
    product_variant_id uuid,
    unit_of_measure_id uuid,
    unit_code_snapshot text,
    product_evidence jsonb,
    unit_evidence jsonb,
    description text NOT NULL,
    quantity numeric(20,6) NOT NULL,
    explicit_unit_price numeric(20,6),
    price_override_reason text,
    business_category_code text,
    line_discount numeric(20,6) NOT NULL DEFAULT 0,
    line_net_total numeric(20,6),
    line_tax_total numeric(20,6),
    line_gross_total numeric(20,6),
    source_line_reference text,
    PRIMARY KEY (tenant_id, invoice_id, id),
    UNIQUE (tenant_id, invoice_id, sequence),
    UNIQUE (tenant_id, legal_entity_id, invoice_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, invoice_id) REFERENCES business_invoicing.invoice(tenant_id, legal_entity_id, id),
    CHECK (sequence > 0 AND quantity > 0 AND line_discount >= 0),
    CHECK ((line_net_total IS NULL AND line_tax_total IS NULL AND line_gross_total IS NULL) OR
        (line_net_total IS NOT NULL AND line_tax_total IS NOT NULL AND line_gross_total = line_net_total + line_tax_total)),
    CHECK (explicit_unit_price IS NULL OR explicit_unit_price >= 0),
    CHECK (price_override_reason IS NULL OR length(btrim(price_override_reason)) BETWEEN 1 AND 500),
    CHECK (business_category_code IS NULL OR length(btrim(business_category_code)) BETWEEN 1 AND 80),
    CHECK (length(btrim(description)) BETWEEN 1 AND 1000),
    CHECK (source_line_reference IS NULL OR length(btrim(source_line_reference)) <= 200)
);

CREATE TABLE business_invoicing.invoice_party_snapshot (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    invoice_id uuid NOT NULL,
    party_id uuid NOT NULL,
    schema_version integer NOT NULL,
    evidence jsonb NOT NULL,
    captured_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, invoice_id),
    FOREIGN KEY (tenant_id, legal_entity_id, invoice_id) REFERENCES business_invoicing.invoice(tenant_id, legal_entity_id, id),
    CHECK (schema_version > 0)
);

CREATE TABLE business_invoicing.invoice_address_snapshot (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    invoice_id uuid NOT NULL,
    address_id uuid,
    schema_version integer NOT NULL,
    evidence jsonb NOT NULL,
    captured_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, invoice_id),
    FOREIGN KEY (tenant_id, legal_entity_id, invoice_id) REFERENCES business_invoicing.invoice(tenant_id, legal_entity_id, id),
    CHECK (schema_version > 0)
);

CREATE TABLE business_invoicing.invoice_price_snapshot (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    invoice_id uuid NOT NULL,
    line_id uuid NOT NULL,
    resolved_unit_price numeric(20,6),
    final_unit_price numeric(20,6) NOT NULL,
    evidence jsonb NOT NULL,
    schema_version integer NOT NULL,
    PRIMARY KEY (tenant_id, invoice_id, line_id),
    FOREIGN KEY (tenant_id, legal_entity_id, invoice_id, line_id) REFERENCES business_invoicing.invoice_line(tenant_id, legal_entity_id, invoice_id, id),
    CHECK (resolved_unit_price IS NULL OR resolved_unit_price >= 0),
    CHECK (final_unit_price >= 0 AND schema_version > 0)
);

CREATE TABLE business_invoicing.invoice_tax_component (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    invoice_id uuid NOT NULL,
    line_id uuid NOT NULL,
    sequence integer NOT NULL,
    tax_code text NOT NULL,
    tax_type_code text NOT NULL,
    treatment text NOT NULL,
    tax_base numeric(20,6) NOT NULL,
    tax_amount numeric(20,6) NOT NULL,
    evidence jsonb NOT NULL,
    PRIMARY KEY (tenant_id, invoice_id, line_id, sequence),
    FOREIGN KEY (tenant_id, legal_entity_id, invoice_id, line_id) REFERENCES business_invoicing.invoice_line(tenant_id, legal_entity_id, invoice_id, id),
    CHECK (sequence > 0 AND tax_base >= 0 AND tax_amount >= 0)
);

CREATE TABLE business_invoicing.invoice_payment_schedule (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    invoice_id uuid NOT NULL,
    sequence integer NOT NULL,
    due_date date NOT NULL,
    amount numeric(20,6) NOT NULL,
    currency_code char(3) NOT NULL,
    term_id uuid,
    term_version_id uuid,
    evidence jsonb NOT NULL,
    PRIMARY KEY (tenant_id, invoice_id, sequence),
    FOREIGN KEY (tenant_id, legal_entity_id, invoice_id) REFERENCES business_invoicing.invoice(tenant_id, legal_entity_id, id),
    CHECK (sequence > 0 AND amount >= 0 AND currency_code ~ '^[A-Z]{3}$')
);

CREATE TABLE business_invoicing.invoice_charge (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, invoice_id uuid NOT NULL, id uuid NOT NULL,
    category text NOT NULL, description text NOT NULL, amount numeric(20,6) NOT NULL, tax_treatment text NOT NULL, business_category_code text,
    PRIMARY KEY (tenant_id, invoice_id, id),
    UNIQUE (tenant_id, legal_entity_id, invoice_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, invoice_id) REFERENCES business_invoicing.invoice(tenant_id, legal_entity_id, id),
    CHECK (amount >= 0 AND length(btrim(category)) BETWEEN 1 AND 80)
);

CREATE TABLE business_invoicing.invoice_charge_allocation (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, invoice_id uuid NOT NULL, charge_id uuid NOT NULL, line_id uuid NOT NULL,
    allocated_amount numeric(20,6) NOT NULL,
    PRIMARY KEY (tenant_id, invoice_id, charge_id, line_id),
    FOREIGN KEY (tenant_id, legal_entity_id, invoice_id, charge_id) REFERENCES business_invoicing.invoice_charge(tenant_id, legal_entity_id, invoice_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, invoice_id, line_id) REFERENCES business_invoicing.invoice_line(tenant_id, legal_entity_id, invoice_id, id),
    CHECK (allocated_amount >= 0)
);

CREATE TABLE business_invoicing.invoice_discount_allocation (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, invoice_id uuid NOT NULL, line_id uuid NOT NULL,
    discount_type text NOT NULL, source_amount numeric(20,6) NOT NULL, allocated_amount numeric(20,6) NOT NULL,
    PRIMARY KEY (tenant_id, invoice_id, line_id, discount_type),
    FOREIGN KEY (tenant_id, legal_entity_id, invoice_id, line_id) REFERENCES business_invoicing.invoice_line(tenant_id, legal_entity_id, invoice_id, id),
    CHECK (source_amount >= 0 AND allocated_amount >= 0)
);

CREATE TABLE business_invoicing.invoice_relationship (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, invoice_id uuid NOT NULL, related_invoice_id uuid NOT NULL,
    relationship_type text NOT NULL, amount numeric(20,6), reason text, created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, invoice_id, related_invoice_id, relationship_type),
    FOREIGN KEY (tenant_id, legal_entity_id, invoice_id) REFERENCES business_invoicing.invoice(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, related_invoice_id) REFERENCES business_invoicing.invoice(tenant_id, legal_entity_id, id),
    CHECK (invoice_id <> related_invoice_id AND relationship_type IN ('Corrects','Credits','Debits','Replaces','OriginatesFrom')),
    CHECK (amount IS NULL OR amount >= 0)
);

CREATE TABLE business_invoicing.invoice_attachment (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, invoice_id uuid NOT NULL, id uuid NOT NULL,
    file_ref uuid NOT NULL, attachment_role text NOT NULL, created_by uuid NOT NULL, created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, invoice_id, id),
    UNIQUE (tenant_id, invoice_id, file_ref, attachment_role),
    FOREIGN KEY (tenant_id, legal_entity_id, invoice_id) REFERENCES business_invoicing.invoice(tenant_id, legal_entity_id, id),
    CHECK (attachment_role IN ('CustomerPO','SupplierOriginalInvoice','Contract','DeliveryProof','TaxEvidence','OtherSupporting'))
);

CREATE TABLE business_invoicing.invoice_issued_artifact (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, invoice_id uuid NOT NULL,
    id uuid NOT NULL, file_ref uuid NOT NULL, sha256 char(64) NOT NULL,
    template_code text NOT NULL, template_version text NOT NULL, language_code text NOT NULL,
    created_by uuid NOT NULL, created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, invoice_id, id),
    UNIQUE (tenant_id, invoice_id, language_code, template_code, template_version),
    FOREIGN KEY (tenant_id, legal_entity_id, invoice_id) REFERENCES business_invoicing.invoice(tenant_id, legal_entity_id, id),
    CHECK (sha256 ~ '^[0-9a-f]{64}$'),
    CHECK (length(btrim(template_code)) BETWEEN 1 AND 100),
    CHECK (length(btrim(template_version)) BETWEEN 1 AND 100),
    CHECK (language_code ~ '^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$')
);

CREATE TABLE business_invoicing.invoice_finalization_record (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, invoice_id uuid NOT NULL,
    idempotency_key text NOT NULL, payload_hash char(64) NOT NULL, document_number text NOT NULL,
    journal_entry_id uuid NOT NULL, open_item_ids uuid[] NOT NULL, actor_id uuid NOT NULL, finalized_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, invoice_id),
    UNIQUE (tenant_id, legal_entity_id, idempotency_key),
    FOREIGN KEY (tenant_id, legal_entity_id, invoice_id) REFERENCES business_invoicing.invoice(tenant_id, legal_entity_id, id),
    CHECK (length(btrim(idempotency_key)) BETWEEN 1 AND 200)
);

CREATE TABLE business_invoicing.invoice_number_reference (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, invoice_id uuid NOT NULL,
    sequence_key text NOT NULL, period_key text NOT NULL, document_number text NOT NULL, allocated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, invoice_id),
    UNIQUE (tenant_id, legal_entity_id, sequence_key, period_key, document_number),
    FOREIGN KEY (tenant_id, legal_entity_id, invoice_id) REFERENCES business_invoicing.invoice(tenant_id, legal_entity_id, id)
);

CREATE TABLE business_invoicing.invoice_list_projection (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, invoice_id uuid NOT NULL,
    party_display_name text NOT NULL,
    settlement_summary text NOT NULL DEFAULT 'Unknown', regulatory_summary text NOT NULL DEFAULT 'Pending',
    due_date date, updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, invoice_id),
    FOREIGN KEY (tenant_id, legal_entity_id, invoice_id) REFERENCES business_invoicing.invoice(tenant_id, legal_entity_id, id)
);

CREATE TABLE business_invoicing.invoice_compliance_snapshot (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, invoice_id uuid NOT NULL,
    schema_version integer NOT NULL, snapshot jsonb NOT NULL, created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, invoice_id),
    FOREIGN KEY (tenant_id, legal_entity_id, invoice_id) REFERENCES business_invoicing.invoice(tenant_id, legal_entity_id, id),
    CHECK (schema_version > 0),
    CHECK (jsonb_typeof(snapshot) = 'object')
);

CREATE OR REPLACE FUNCTION business_invoicing.guard_invoice_immutability() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF OLD.lifecycle_state <> 'Draft' THEN RAISE EXCEPTION 'invoicing.finalized_immutable' USING ERRCODE = 'check_violation'; END IF;
    IF NEW.tenant_id <> OLD.tenant_id OR NEW.legal_entity_id <> OLD.legal_entity_id OR NEW.id <> OLD.id OR NEW.direction <> OLD.direction OR NEW.document_type <> OLD.document_type THEN
        RAISE EXCEPTION 'invoicing.identity_immutable' USING ERRCODE = 'check_violation';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER invoice_immutability BEFORE UPDATE ON business_invoicing.invoice FOR EACH ROW EXECUTE FUNCTION business_invoicing.guard_invoice_immutability();

CREATE OR REPLACE FUNCTION business_invoicing.guard_draft_child_write() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE row_tenant uuid; row_entity uuid; row_invoice uuid;
BEGIN
    row_tenant := COALESCE(NEW.tenant_id, OLD.tenant_id);
    row_entity := COALESCE(NEW.legal_entity_id, OLD.legal_entity_id);
    row_invoice := COALESCE(NEW.invoice_id, OLD.invoice_id);
    IF NOT EXISTS (SELECT 1 FROM business_invoicing.invoice i WHERE i.tenant_id=row_tenant AND i.legal_entity_id=row_entity AND i.id=row_invoice AND i.lifecycle_state='Draft') THEN
        RAISE EXCEPTION 'invoicing.finalized_immutable' USING ERRCODE = 'check_violation';
    END IF;
    IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
    RETURN NEW;
END $$;
DO $$
DECLARE t text;
BEGIN
    FOREACH t IN ARRAY ARRAY['invoice_line','invoice_party_snapshot','invoice_address_snapshot','invoice_price_snapshot','invoice_tax_component','invoice_payment_schedule','invoice_charge','invoice_charge_allocation','invoice_discount_allocation','invoice_relationship'] LOOP
        EXECUTE format('CREATE TRIGGER %I_draft_guard BEFORE INSERT OR UPDATE OR DELETE ON business_invoicing.%I FOR EACH ROW EXECUTE FUNCTION business_invoicing.guard_draft_child_write()', t, t);
    END LOOP;
END $$;

DO $$
DECLARE t text;
BEGIN
    FOREACH t IN ARRAY ARRAY['invoice','invoice_line','invoice_party_snapshot','invoice_address_snapshot','invoice_price_snapshot','invoice_tax_component','invoice_payment_schedule','invoice_charge','invoice_charge_allocation','invoice_discount_allocation','invoice_relationship','invoice_attachment','invoice_issued_artifact','invoice_finalization_record','invoice_number_reference','invoice_list_projection','invoice_compliance_snapshot'] LOOP
        EXECUTE format('ALTER TABLE business_invoicing.%I ENABLE ROW LEVEL SECURITY', t);
        EXECUTE format('CREATE POLICY tenant_isolation ON business_invoicing.%I USING (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid) WITH CHECK (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid)', t);
    END LOOP;
END $$;

CREATE INDEX ix_invoice_entity_date ON business_invoicing.invoice(tenant_id, legal_entity_id, document_date DESC, id DESC);
CREATE INDEX ix_invoice_entity_party_date ON business_invoicing.invoice(tenant_id, legal_entity_id, party_id, document_date DESC, id DESC);
CREATE INDEX ix_invoice_entity_lifecycle_date ON business_invoicing.invoice(tenant_id, legal_entity_id, lifecycle_state, document_date DESC, id DESC);
CREATE INDEX ix_invoice_entity_number ON business_invoicing.invoice(tenant_id, legal_entity_id, document_number) WHERE document_number IS NOT NULL;
CREATE UNIQUE INDEX ux_invoice_supplier_external_number ON business_invoicing.invoice(tenant_id, legal_entity_id, party_id, supplier_invoice_number_key) WHERE direction='Payable' AND supplier_invoice_number_key IS NOT NULL AND lifecycle_state='Finalized';
CREATE INDEX ix_invoice_line_parent ON business_invoicing.invoice_line(tenant_id, legal_entity_id, invoice_id, sequence);
CREATE INDEX ix_invoice_projection_due ON business_invoicing.invoice_list_projection(tenant_id, legal_entity_id, due_date, invoice_id);

GRANT SELECT, INSERT, UPDATE ON business_invoicing.invoice TO erp_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON business_invoicing.invoice_line TO erp_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON business_invoicing.invoice_party_snapshot, business_invoicing.invoice_address_snapshot, business_invoicing.invoice_price_snapshot, business_invoicing.invoice_tax_component, business_invoicing.invoice_payment_schedule, business_invoicing.invoice_charge, business_invoicing.invoice_charge_allocation, business_invoicing.invoice_discount_allocation, business_invoicing.invoice_relationship TO erp_app;
GRANT SELECT, INSERT ON business_invoicing.invoice_attachment, business_invoicing.invoice_issued_artifact TO erp_app;
GRANT SELECT, INSERT ON business_invoicing.invoice_finalization_record, business_invoicing.invoice_number_reference TO erp_app;
GRANT SELECT, INSERT, UPDATE ON business_invoicing.invoice_list_projection TO erp_app;
GRANT SELECT, INSERT ON business_invoicing.invoice_compliance_snapshot TO erp_app;
