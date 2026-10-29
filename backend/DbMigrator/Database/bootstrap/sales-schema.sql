CREATE SCHEMA business_sales AUTHORIZATION erp_owner;

CREATE TABLE business_sales.sales_number_sequence (
  tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, quote_last bigint NOT NULL DEFAULT 0, order_last bigint NOT NULL DEFAULT 0,
  PRIMARY KEY (tenant_id, legal_entity_id)
);
CREATE TABLE business_sales.sales_customer_profile (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, party_id uuid NOT NULL,
  default_salesperson_id uuid, default_sales_team_id uuid, default_sales_channel_id uuid, default_warehouse_id uuid,
  preferred_fulfillment_mode varchar(32), assigned_sales_price_list_id uuid, sales_price_group_id uuid,
  default_quote_validity_days integer CHECK (default_quote_validity_days IS NULL OR default_quote_validity_days > 0),
  customer_po_required boolean NOT NULL DEFAULT false, status varchar(16) NOT NULL DEFAULT 'Active' CHECK (status IN ('Active','Inactive')),
  version bigint NOT NULL DEFAULT 1, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
  PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,party_id)
);
CREATE TABLE business_sales.sales_channel (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, code varchar(40) NOT NULL, name varchar(160) NOT NULL,
  status varchar(16) NOT NULL DEFAULT 'Active' CHECK (status IN ('Active','Inactive')), version bigint NOT NULL DEFAULT 1,
  created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,code)
);
CREATE TABLE business_sales.sales_team (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, code varchar(40) NOT NULL, name varchar(160) NOT NULL,
  status varchar(16) NOT NULL DEFAULT 'Active' CHECK (status IN ('Active','Inactive')), version bigint NOT NULL DEFAULT 1,
  created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,code)
);
CREATE TABLE business_sales.quotation (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, branch_id uuid, number varchar(64) NOT NULL,
  party_id uuid NOT NULL, quote_date date NOT NULL, valid_until date, currency_code char(3) NOT NULL, state varchar(16) NOT NULL CHECK (state IN ('Draft','Open','Accepted','Declined','Expired','Cancelled','Converted')),
  current_revision_number integer NOT NULL DEFAULT 1 CHECK (current_revision_number > 0), version bigint NOT NULL DEFAULT 1,
  customer_reference varchar(160), requested_delivery_date date, salesperson_id uuid, sales_team_id uuid, sales_channel_id uuid,
  created_by uuid NOT NULL, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
  PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,number), UNIQUE (tenant_id,legal_entity_id,id), UNIQUE (tenant_id,legal_entity_id,id,current_revision_number)
);
CREATE TABLE business_sales.quotation_revision (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, quotation_id uuid NOT NULL, revision_number integer NOT NULL,
  status varchar(16) NOT NULL CHECK (status IN ('Draft','Open','Accepted','Superseded')), snapshot jsonb NOT NULL DEFAULT '{}'::jsonb,
  net_total numeric(20,6) NOT NULL CHECK (net_total >= 0), discount_total numeric(20,6) NOT NULL CHECK (discount_total >= 0), gross_total numeric(20,6) NOT NULL CHECK (gross_total >= 0),
  pricing_evidence jsonb NOT NULL DEFAULT '{}'::jsonb, tax_preview_evidence jsonb NOT NULL DEFAULT '{}'::jsonb,
  created_by uuid NOT NULL, created_at timestamptz NOT NULL, sealed_at timestamptz,
  PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,quotation_id,revision_number), UNIQUE (tenant_id,legal_entity_id,id),
  FOREIGN KEY (tenant_id,legal_entity_id,quotation_id) REFERENCES business_sales.quotation(tenant_id,legal_entity_id,id)
);
CREATE TABLE business_sales.quotation_revision_line (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, revision_id uuid NOT NULL, sequence integer NOT NULL,
  product_variant_id uuid, unit_of_measure_id uuid, uom_code varchar(32), description varchar(1000) NOT NULL, quantity numeric(20,6) NOT NULL CHECK (quantity > 0),
  unit_price numeric(20,6) NOT NULL CHECK (unit_price >= 0), discount_amount numeric(20,6) NOT NULL DEFAULT 0 CHECK (discount_amount >= 0), net_amount numeric(20,6) NOT NULL CHECK (net_amount >= 0),
  pricing_snapshot jsonb NOT NULL DEFAULT '{}'::jsonb, tax_preview_snapshot jsonb NOT NULL DEFAULT '{}'::jsonb, requested_delivery_date date, warehouse_preference_id uuid,
  PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,revision_id,sequence), UNIQUE (tenant_id,legal_entity_id,id),
  FOREIGN KEY (tenant_id,legal_entity_id,revision_id) REFERENCES business_sales.quotation_revision(tenant_id,legal_entity_id,id)
);
CREATE TABLE business_sales.quotation_acceptance (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, quotation_id uuid NOT NULL, revision_number integer NOT NULL,
  accepted_at timestamptz NOT NULL, acceptance_method varchar(32) NOT NULL, customer_contact_id uuid, recorded_by uuid NOT NULL, evidence_reference varchar(500), note varchar(2000), idempotency_key varchar(200) NOT NULL,
  PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,quotation_id,revision_number), UNIQUE (tenant_id,legal_entity_id,quotation_id,idempotency_key),
  FOREIGN KEY (tenant_id,legal_entity_id,quotation_id) REFERENCES business_sales.quotation(tenant_id,legal_entity_id,id)
);
CREATE TABLE business_sales.sales_document_state_history (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, document_type varchar(32) NOT NULL, document_id uuid NOT NULL,
  revision_number integer, from_state varchar(32), to_state varchar(32) NOT NULL, actor_id uuid NOT NULL, reason varchar(1000), occurred_at timestamptz NOT NULL,
  PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,id)
);
CREATE TABLE business_sales.sales_conversion_link (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, source_type varchar(32) NOT NULL, source_id uuid NOT NULL, source_revision integer,
  target_module varchar(32) NOT NULL, target_type varchar(32) NOT NULL, target_id uuid NOT NULL, purpose varchar(64) NOT NULL, idempotency_key varchar(200) NOT NULL, payload_hash char(64) NOT NULL,
  created_by uuid NOT NULL, created_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id),
  UNIQUE (tenant_id,source_type,source_id,source_revision,target_module,target_type,purpose), UNIQUE (tenant_id,target_module,target_type,target_id,purpose), UNIQUE (tenant_id,source_type,source_id,idempotency_key)
);
CREATE TABLE business_sales.sales_order (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, branch_id uuid, number varchar(64) NOT NULL, quotation_id uuid, quotation_revision integer,
  party_id uuid NOT NULL, order_date date NOT NULL, currency_code char(3) NOT NULL, warehouse_id uuid,
  commercial_state varchar(16) NOT NULL CHECK (commercial_state IN ('Draft','Confirmed','Cancelled')),
  fulfillment_state varchar(24) NOT NULL CHECK (fulfillment_state IN ('NotRequired','Pending','PartiallyFulfilled','Fulfilled','Backordered')),
  invoicing_state varchar(24) NOT NULL CHECK (invoicing_state IN ('NotInvoiced','PartiallyInvoiced','Invoiced')), credit_state varchar(24) NOT NULL DEFAULT 'NotChecked',
  customer_reference varchar(160), requested_delivery_date date, version bigint NOT NULL DEFAULT 1, created_by uuid NOT NULL, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
  PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,number), UNIQUE (tenant_id,legal_entity_id,id),
  FOREIGN KEY (tenant_id,legal_entity_id,quotation_id,quotation_revision) REFERENCES business_sales.quotation(tenant_id,legal_entity_id,id,current_revision_number) DEFERRABLE INITIALLY DEFERRED
);
CREATE TABLE business_sales.sales_order_line (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, order_id uuid NOT NULL, sequence integer NOT NULL, product_variant_id uuid,
  unit_of_measure_id uuid, description varchar(1000) NOT NULL, ordered_quantity numeric(20,6) NOT NULL CHECK (ordered_quantity > 0), fulfilled_quantity numeric(20,6) NOT NULL DEFAULT 0 CHECK (fulfilled_quantity >= 0),
  cancelled_quantity numeric(20,6) NOT NULL DEFAULT 0 CHECK (cancelled_quantity >= 0), invoiced_quantity numeric(20,6) NOT NULL DEFAULT 0 CHECK (invoiced_quantity >= 0), unit_price numeric(20,6) NOT NULL CHECK (unit_price >= 0),
  PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,order_id,sequence), UNIQUE (tenant_id,legal_entity_id,id), FOREIGN KEY (tenant_id,legal_entity_id,order_id) REFERENCES business_sales.sales_order(tenant_id,legal_entity_id,id),
  CHECK (fulfilled_quantity + cancelled_quantity <= ordered_quantity), CHECK (invoiced_quantity <= fulfilled_quantity OR invoiced_quantity <= ordered_quantity)
);
CREATE TABLE business_sales.sales_return_request (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, branch_id uuid, party_id uuid NOT NULL, source_document_type varchar(32) NOT NULL, source_document_id uuid,
  state varchar(16) NOT NULL CHECK (state IN ('Requested','Approved','Executed','Rejected','Cancelled')), reason varchar(1000) NOT NULL, version bigint NOT NULL DEFAULT 1, created_by uuid NOT NULL, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
  PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,id)
);
CREATE TABLE business_sales.sales_return_request_line (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, return_id uuid NOT NULL, source_line_id uuid, product_variant_id uuid, description varchar(1000) NOT NULL, quantity numeric(20,6) NOT NULL CHECK (quantity > 0),
  PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,return_id,id), FOREIGN KEY (tenant_id,legal_entity_id,return_id) REFERENCES business_sales.sales_return_request(tenant_id,legal_entity_id,id)
);

CREATE TABLE business_sales.sales_order_reservation (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, order_id uuid NOT NULL,
  order_line_id uuid NOT NULL, inventory_reservation_id uuid NOT NULL, quantity numeric(20,6) NOT NULL CHECK (quantity > 0),
  state varchar(16) NOT NULL DEFAULT 'Active' CHECK (state IN ('Active','Released','Consumed')),
  created_at timestamptz NOT NULL, released_at timestamptz,
  PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,order_line_id), UNIQUE (tenant_id,inventory_reservation_id),
  FOREIGN KEY (tenant_id,legal_entity_id,order_id) REFERENCES business_sales.sales_order(tenant_id,legal_entity_id,id),
  FOREIGN KEY (tenant_id,legal_entity_id,order_line_id) REFERENCES business_sales.sales_order_line(tenant_id,legal_entity_id,id)
);
CREATE TABLE business_sales.sales_fulfillment (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, order_id uuid NOT NULL,
  idempotency_key varchar(200) NOT NULL, operation_id uuid, state varchar(16) NOT NULL CHECK (state IN ('Completed','Failed')),
  created_by uuid NOT NULL, created_at timestamptz NOT NULL,
  PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,id), UNIQUE (tenant_id,order_id,idempotency_key),
  FOREIGN KEY (tenant_id,legal_entity_id,order_id) REFERENCES business_sales.sales_order(tenant_id,legal_entity_id,id)
);
CREATE TABLE business_sales.sales_fulfillment_line (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, fulfillment_id uuid NOT NULL,
  order_line_id uuid NOT NULL, requested_quantity numeric(20,6) NOT NULL CHECK (requested_quantity > 0),
  fulfilled_quantity numeric(20,6) NOT NULL CHECK (fulfilled_quantity >= 0), inventory_operation_id uuid,
  PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,fulfillment_id,order_line_id),
  FOREIGN KEY (tenant_id,legal_entity_id,fulfillment_id) REFERENCES business_sales.sales_fulfillment(tenant_id,legal_entity_id,id),
  FOREIGN KEY (tenant_id,legal_entity_id,order_line_id) REFERENCES business_sales.sales_order_line(tenant_id,legal_entity_id,id),
  CHECK (fulfilled_quantity <= requested_quantity)
);
CREATE TABLE business_sales.sales_credit_decision (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, order_id uuid NOT NULL,
  decision varchar(24) NOT NULL CHECK (decision IN ('Allow','Warn','Block','RequireApproval','Override')),
  policy_code varchar(32) NOT NULL, available_credit numeric(20,6), proposed_exposure numeric(20,6) NOT NULL,
  exposure_version bigint, reason varchar(1000), actor_id uuid NOT NULL, decided_at timestamptz NOT NULL,
  PRIMARY KEY (tenant_id,id), FOREIGN KEY (tenant_id,legal_entity_id,order_id) REFERENCES business_sales.sales_order(tenant_id,legal_entity_id,id)
);

CREATE INDEX ix_sales_quotation_list ON business_sales.quotation(tenant_id,legal_entity_id,state,updated_at DESC,id);
CREATE INDEX ix_sales_quotation_party ON business_sales.quotation(tenant_id,legal_entity_id,party_id,quote_date DESC,id);
CREATE INDEX ix_sales_order_list ON business_sales.sales_order(tenant_id,legal_entity_id,commercial_state,updated_at DESC,id);
CREATE INDEX ix_sales_history_document ON business_sales.sales_document_state_history(tenant_id,legal_entity_id,document_type,document_id,occurred_at DESC,id);
CREATE INDEX ix_sales_return_party ON business_sales.sales_return_request(tenant_id,legal_entity_id,party_id,created_at DESC,id);
CREATE INDEX ix_sales_order_reservation_order ON business_sales.sales_order_reservation(tenant_id,legal_entity_id,order_id,state);
CREATE INDEX ix_sales_fulfillment_order ON business_sales.sales_fulfillment(tenant_id,legal_entity_id,order_id,created_at DESC,id);
CREATE INDEX ix_sales_credit_decision_order ON business_sales.sales_credit_decision(tenant_id,legal_entity_id,order_id,decided_at DESC,id);

REVOKE ALL ON SCHEMA business_sales FROM PUBLIC;
GRANT USAGE ON SCHEMA business_sales TO erp_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA business_sales TO erp_app;

DO $$ DECLARE table_name text; BEGIN
  FOREACH table_name IN ARRAY ARRAY['sales_number_sequence','sales_customer_profile','sales_channel','sales_team','quotation','quotation_revision','quotation_revision_line','quotation_acceptance','sales_document_state_history','sales_conversion_link','sales_order','sales_order_line','sales_return_request','sales_return_request_line','sales_order_reservation','sales_fulfillment','sales_fulfillment_line','sales_credit_decision'] LOOP
    EXECUTE format('ALTER TABLE business_sales.%I ENABLE ROW LEVEL SECURITY', table_name);
    EXECUTE format('ALTER TABLE business_sales.%I FORCE ROW LEVEL SECURITY', table_name);
    EXECUTE format('CREATE POLICY tenant_isolation ON business_sales.%I USING (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid) WITH CHECK (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid)', table_name);
  END LOOP;
END $$;
