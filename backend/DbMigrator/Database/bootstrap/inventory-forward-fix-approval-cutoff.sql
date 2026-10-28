CREATE TABLE IF NOT EXISTS business_inventory.inventory_policy (
  tenant_id uuid NOT NULL,
  legal_entity_id uuid NOT NULL,
  adjustment_approval_mode varchar(24) NOT NULL DEFAULT 'DirectConfirm'
    CHECK (adjustment_approval_mode IN ('DirectConfirm','ApprovalRequired')),
  separation_of_duties boolean NOT NULL DEFAULT true,
  operational_cutoff_date date,
  version bigint NOT NULL DEFAULT 1,
  created_at timestamptz NOT NULL,
  updated_at timestamptz NOT NULL,
  PRIMARY KEY (tenant_id, legal_entity_id)
);

CREATE TABLE IF NOT EXISTS business_inventory.inventory_adjustment_request (
  tenant_id uuid NOT NULL,
  id uuid NOT NULL,
  legal_entity_id uuid NOT NULL,
  product_variant_id uuid NOT NULL,
  warehouse_id uuid NOT NULL,
  location_id uuid,
  entered_uom_id uuid NOT NULL,
  quantity numeric(20,6) NOT NULL CHECK (quantity > 0),
  direction varchar(16) NOT NULL CHECK (direction IN ('Increase','Decrease')),
  reason varchar(32) NOT NULL,
  status varchar(20) NOT NULL CHECK (status IN ('Draft','PendingApproval','Approved','Posted','Rejected','Cancelled')),
  approval_mode varchar(24) NOT NULL CHECK (approval_mode IN ('DirectConfirm','ApprovalRequired')),
  stock_status varchar(24) NOT NULL,
  lot_id uuid,
  serial_id uuid,
  unit_cost numeric(20,6),
  currency_code varchar(3),
  operational_date date NOT NULL,
  posting_date date NOT NULL CHECK (posting_date >= operational_date),
  idempotency_key varchar(200) NOT NULL,
  created_by uuid NOT NULL,
  approved_by uuid,
  posted_operation_id uuid,
  rejection_reason varchar(500),
  created_at timestamptz NOT NULL,
  updated_at timestamptz NOT NULL,
  version bigint NOT NULL DEFAULT 1,
  PRIMARY KEY (tenant_id, id),
  UNIQUE (tenant_id, legal_entity_id, idempotency_key),
  FOREIGN KEY (tenant_id, legal_entity_id, product_variant_id) REFERENCES business_inventory.product_inventory_profile(tenant_id, legal_entity_id, product_variant_id),
  FOREIGN KEY (tenant_id, legal_entity_id, warehouse_id, location_id) REFERENCES business_inventory.location(tenant_id, legal_entity_id, warehouse_id, id),
  FOREIGN KEY (tenant_id, legal_entity_id, posted_operation_id) REFERENCES business_inventory.inventory_operation(tenant_id, legal_entity_id, id)
);
CREATE INDEX IF NOT EXISTS inventory_adjustment_request_status_idx ON business_inventory.inventory_adjustment_request(tenant_id, legal_entity_id, status, created_at DESC, id DESC);

DO $$
DECLARE table_name text;
BEGIN
  FOREACH table_name IN ARRAY ARRAY['inventory_policy','inventory_adjustment_request'] LOOP
    EXECUTE format('ALTER TABLE business_inventory.%I ENABLE ROW LEVEL SECURITY', table_name);
    EXECUTE format('ALTER TABLE business_inventory.%I FORCE ROW LEVEL SECURITY', table_name);
    EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON business_inventory.%I', table_name);
    EXECUTE format('CREATE POLICY tenant_isolation ON business_inventory.%I USING (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid) WITH CHECK (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid)', table_name);
  END LOOP;
END $$;
