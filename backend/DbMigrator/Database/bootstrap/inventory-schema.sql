CREATE SCHEMA business_inventory AUTHORIZATION erp_owner;

CREATE TABLE business_inventory.warehouse (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, branch_id uuid,
  code varchar(40) NOT NULL, name varchar(200) NOT NULL,
  location_mode varchar(32) NOT NULL CHECK (location_mode IN ('WarehouseOnly','Locations','HierarchicalLocations')),
  default_location_id uuid NOT NULL, status varchar(16) NOT NULL CHECK (status IN ('Active','Suspended','Archived')),
  version bigint NOT NULL DEFAULT 1, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
  PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,code), UNIQUE (tenant_id,legal_entity_id,id)
);
CREATE TABLE business_inventory.movement_sequence (
  tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, last_value bigint NOT NULL CHECK(last_value > 0),
  PRIMARY KEY(tenant_id,legal_entity_id)
);

CREATE TABLE business_inventory.location (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, warehouse_id uuid NOT NULL,
  parent_location_id uuid, code varchar(60) NOT NULL, name varchar(200) NOT NULL, location_type varchar(32) NOT NULL,
  barcode varchar(120), hidden boolean NOT NULL DEFAULT false, can_hold_stock boolean NOT NULL DEFAULT true,
  can_receive_into boolean NOT NULL DEFAULT true, can_pick_from boolean NOT NULL DEFAULT true,
  status varchar(16) NOT NULL CHECK (status IN ('Active','Archived')), created_at timestamptz NOT NULL,
  PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,warehouse_id,id), UNIQUE (tenant_id,legal_entity_id,warehouse_id,id), UNIQUE (tenant_id,warehouse_id,code),
  FOREIGN KEY (tenant_id,legal_entity_id,warehouse_id) REFERENCES business_inventory.warehouse(tenant_id,legal_entity_id,id),
  FOREIGN KEY (tenant_id,warehouse_id,parent_location_id) REFERENCES business_inventory.location(tenant_id,warehouse_id,id)
);
ALTER TABLE business_inventory.warehouse ADD CONSTRAINT warehouse_default_location_fk
  FOREIGN KEY (tenant_id,id,default_location_id) REFERENCES business_inventory.location(tenant_id,warehouse_id,id) DEFERRABLE INITIALLY DEFERRED;

CREATE TABLE business_inventory.product_inventory_profile (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, product_variant_id uuid NOT NULL,
  inventory_state varchar(16) NOT NULL CHECK (inventory_state IN ('NonStocked','StockTracked')),
  tracking_mode varchar(16) NOT NULL CHECK (tracking_mode IN ('Quantity','Lot','Serial')),
  valuation_mode varchar(16) NOT NULL CHECK (valuation_mode IN ('NonValuated','Perpetual')),
  costing_method varchar(20) CHECK (costing_method IN ('MovingAverage','FIFO')),
  negative_stock_policy varchar(36) NOT NULL DEFAULT 'Disallow' CHECK (negative_stock_policy IN ('Disallow','AllowWithWarningAndPermission','Allow')),
  default_warehouse_id uuid, reservation_mode varchar(16) NOT NULL DEFAULT 'Disabled' CHECK (reservation_mode IN ('Disabled','Advisory','Enforced')),
  status varchar(16) NOT NULL DEFAULT 'Active' CHECK (status IN ('Active','Archived')),
  version bigint NOT NULL DEFAULT 1, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
  PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,product_variant_id),
  FOREIGN KEY (tenant_id,legal_entity_id,default_warehouse_id) REFERENCES business_inventory.warehouse(tenant_id,legal_entity_id,id),
  CHECK ((inventory_state='NonStocked' AND tracking_mode='Quantity' AND valuation_mode='NonValuated' AND costing_method IS NULL)
      OR (inventory_state='StockTracked' AND ((valuation_mode='Perpetual' AND costing_method IS NOT NULL) OR (valuation_mode='NonValuated' AND costing_method IS NULL))))
);

CREATE TABLE business_inventory.lot (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, product_variant_id uuid NOT NULL,
  lot_number varchar(120) NOT NULL, manufactured_on date, expires_on date, supplier_reference varchar(160),
  status varchar(20) NOT NULL DEFAULT 'Active' CHECK (status IN ('Active','Blocked','Archived')),
  created_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,product_variant_id,lot_number),
  UNIQUE (tenant_id,legal_entity_id,product_variant_id,id),
  FOREIGN KEY (tenant_id,legal_entity_id,product_variant_id) REFERENCES business_inventory.product_inventory_profile(tenant_id,legal_entity_id,product_variant_id),
  CHECK (manufactured_on IS NULL OR expires_on IS NULL OR manufactured_on <= expires_on)
);

CREATE TABLE business_inventory.serial (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, product_variant_id uuid NOT NULL,
  serial_number varchar(160) NOT NULL, status varchar(20) NOT NULL DEFAULT 'PendingReceipt'
    CHECK (status IN ('PendingReceipt','OnHand','Reserved','InTransit','Issued','Returned','QualityHold','Scrapped')),
  current_warehouse_id uuid, current_location_id uuid, created_at timestamptz NOT NULL,
  PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,product_variant_id,serial_number),
  UNIQUE (tenant_id,legal_entity_id,product_variant_id,id),
  FOREIGN KEY (tenant_id,legal_entity_id,product_variant_id) REFERENCES business_inventory.product_inventory_profile(tenant_id,legal_entity_id,product_variant_id),
  FOREIGN KEY (tenant_id,legal_entity_id,current_warehouse_id,current_location_id) REFERENCES business_inventory.location(tenant_id,legal_entity_id,warehouse_id,id) DEFERRABLE INITIALLY DEFERRED
);

CREATE TABLE business_inventory.inventory_operation (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, operation_number varchar(64) NOT NULL,
  operation_type varchar(20) NOT NULL CHECK (operation_type IN ('Receipt','Issue','Transfer','Return','Adjustment','Opening','Reversal','StatusChange')),
  status varchar(16) NOT NULL CHECK (status IN ('Draft','Confirmed','Reversed')),
  source_module varchar(40) NOT NULL, source_document_id uuid, idempotency_key varchar(200) NOT NULL,
  idempotency_hash char(64) NOT NULL, operational_date date NOT NULL, posting_date date NOT NULL,
  reason_code varchar(32), reason text, actor_id uuid NOT NULL, correlation_id uuid NOT NULL,
  project_id uuid, task_id uuid, cost_category text,
  created_at timestamptz NOT NULL, confirmed_at timestamptz, reversal_of_id uuid,
  PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,id), UNIQUE (tenant_id,legal_entity_id,operation_number),
  UNIQUE (tenant_id,legal_entity_id,source_module,idempotency_key),
  FOREIGN KEY (tenant_id,legal_entity_id,reversal_of_id) REFERENCES business_inventory.inventory_operation(tenant_id,legal_entity_id,id),
  CHECK (posting_date >= operational_date), CHECK (length(reason) <= 1000)
);

CREATE TABLE business_inventory.stock_movement_entry (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, operation_id uuid NOT NULL,
  sequence bigint NOT NULL, product_variant_id uuid NOT NULL, warehouse_id uuid NOT NULL, location_id uuid NOT NULL,
  stock_status varchar(24) NOT NULL CHECK (stock_status IN ('Available','QualityHold','Quarantine','Damaged','ReturnsInspection','Blocked','ScrapPending','InTransit')),
  lot_id uuid, serial_id uuid, entered_quantity numeric(20,6) NOT NULL CHECK (entered_quantity > 0),
  entered_uom_id uuid NOT NULL, conversion_factor_snapshot numeric(20,10) NOT NULL CHECK (conversion_factor_snapshot > 0),
  base_quantity numeric(20,6) NOT NULL CHECK (base_quantity > 0), signed_base_quantity numeric(20,6) NOT NULL CHECK (signed_base_quantity <> 0),
  source_module varchar(40) NOT NULL, source_document_id uuid, operational_date date NOT NULL, posting_date date NOT NULL,
  effective_at timestamptz NOT NULL, movement_type varchar(20) NOT NULL,
  created_by uuid NOT NULL, created_at timestamptz NOT NULL,
  PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,id), UNIQUE (tenant_id,legal_entity_id,sequence),
  FOREIGN KEY (tenant_id,legal_entity_id,operation_id) REFERENCES business_inventory.inventory_operation(tenant_id,legal_entity_id,id),
  FOREIGN KEY (tenant_id,legal_entity_id,product_variant_id) REFERENCES business_inventory.product_inventory_profile(tenant_id,legal_entity_id,product_variant_id),
  FOREIGN KEY (tenant_id,legal_entity_id,product_variant_id,lot_id) REFERENCES business_inventory.lot(tenant_id,legal_entity_id,product_variant_id,id) DEFERRABLE INITIALLY DEFERRED,
  FOREIGN KEY (tenant_id,legal_entity_id,product_variant_id,serial_id) REFERENCES business_inventory.serial(tenant_id,legal_entity_id,product_variant_id,id) DEFERRABLE INITIALLY DEFERRED,
  FOREIGN KEY (tenant_id,legal_entity_id,warehouse_id,location_id) REFERENCES business_inventory.location(tenant_id,legal_entity_id,warehouse_id,id),
  CHECK (abs(signed_base_quantity) = base_quantity), CHECK (base_quantity = entered_quantity * conversion_factor_snapshot), CHECK (posting_date >= operational_date)
);
CREATE INDEX stock_movement_history_idx ON business_inventory.stock_movement_entry(tenant_id,legal_entity_id,product_variant_id,effective_at DESC,sequence DESC,id DESC);
CREATE INDEX stock_movement_source_idx ON business_inventory.stock_movement_entry(tenant_id,legal_entity_id,source_module,source_document_id,effective_at DESC,id DESC);

CREATE TABLE business_inventory.stock_balance (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, product_variant_id uuid NOT NULL,
  warehouse_id uuid NOT NULL, location_id uuid NOT NULL,
  stock_status varchar(24) NOT NULL CHECK (stock_status IN ('Available','QualityHold','Quarantine','Damaged','ReturnsInspection','Blocked','ScrapPending','InTransit')),
  lot_id uuid, serial_id uuid, on_hand_base_quantity numeric(20,6) NOT NULL DEFAULT 0,
  reserved_base_quantity numeric(20,6) NOT NULL DEFAULT 0 CHECK (reserved_base_quantity >= 0),
  version bigint NOT NULL DEFAULT 1, last_movement_sequence bigint NOT NULL DEFAULT 0,
  PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,id), UNIQUE NULLS NOT DISTINCT (tenant_id,legal_entity_id,product_variant_id,warehouse_id,location_id,stock_status,lot_id,serial_id),
  FOREIGN KEY (tenant_id,legal_entity_id,warehouse_id,location_id) REFERENCES business_inventory.location(tenant_id,legal_entity_id,warehouse_id,id),
  FOREIGN KEY (tenant_id,legal_entity_id,product_variant_id) REFERENCES business_inventory.product_inventory_profile(tenant_id,legal_entity_id,product_variant_id),
  FOREIGN KEY (tenant_id,legal_entity_id,product_variant_id,lot_id) REFERENCES business_inventory.lot(tenant_id,legal_entity_id,product_variant_id,id),
  FOREIGN KEY (tenant_id,legal_entity_id,product_variant_id,serial_id) REFERENCES business_inventory.serial(tenant_id,legal_entity_id,product_variant_id,id),
  CHECK (serial_id IS NULL OR abs(on_hand_base_quantity) <= 1)
);
CREATE INDEX stock_balance_availability_idx ON business_inventory.stock_balance(tenant_id,legal_entity_id,product_variant_id,warehouse_id,stock_status);
CREATE INDEX stock_balance_locator_idx ON business_inventory.stock_balance(tenant_id,legal_entity_id,product_variant_id,warehouse_id,location_id);
CREATE UNIQUE INDEX stock_balance_one_active_position_per_serial ON business_inventory.stock_balance(tenant_id,legal_entity_id,serial_id) WHERE serial_id IS NOT NULL AND on_hand_base_quantity > 0;

CREATE TABLE business_inventory.reservation (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, product_variant_id uuid NOT NULL,
  warehouse_id uuid NOT NULL, location_id uuid, lot_id uuid, serial_id uuid, source_module varchar(40) NOT NULL,
  source_document_id uuid NOT NULL, source_line_id uuid, requested_quantity numeric(20,6) NOT NULL CHECK (requested_quantity > 0),
  reserved_quantity numeric(20,6) NOT NULL CHECK (reserved_quantity >= 0),
  idempotency_key varchar(200) NOT NULL, request_hash char(64) NOT NULL,
  status varchar(16) NOT NULL CHECK (status IN ('Active','Released','Consumed','Expired','Cancelled')),
  expires_at timestamptz, created_by uuid NOT NULL, created_at timestamptz NOT NULL, version bigint NOT NULL DEFAULT 1,
  PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,id), UNIQUE NULLS NOT DISTINCT (tenant_id,legal_entity_id,source_module,source_document_id,source_line_id),
  FOREIGN KEY(tenant_id,legal_entity_id,product_variant_id) REFERENCES business_inventory.product_inventory_profile(tenant_id,legal_entity_id,product_variant_id),
  FOREIGN KEY(tenant_id,legal_entity_id,warehouse_id) REFERENCES business_inventory.warehouse(tenant_id,legal_entity_id,id),
  FOREIGN KEY(tenant_id,legal_entity_id,warehouse_id,location_id) REFERENCES business_inventory.location(tenant_id,legal_entity_id,warehouse_id,id),
  FOREIGN KEY(tenant_id,legal_entity_id,product_variant_id,lot_id) REFERENCES business_inventory.lot(tenant_id,legal_entity_id,product_variant_id,id),
  FOREIGN KEY(tenant_id,legal_entity_id,product_variant_id,serial_id) REFERENCES business_inventory.serial(tenant_id,legal_entity_id,product_variant_id,id),
  CHECK (reserved_quantity <= requested_quantity)
);
CREATE INDEX reservation_active_position_idx ON business_inventory.reservation(tenant_id,legal_entity_id,product_variant_id,warehouse_id,location_id) WHERE status='Active';
CREATE TABLE business_inventory.reservation_allocation (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, reservation_id uuid NOT NULL,
  warehouse_id uuid NOT NULL, location_id uuid NOT NULL, stock_status varchar(24) NOT NULL,
  lot_id uuid, serial_id uuid, quantity numeric(20,6) NOT NULL CHECK(quantity>0),
  PRIMARY KEY(tenant_id,id),
  FOREIGN KEY(tenant_id,legal_entity_id,reservation_id) REFERENCES business_inventory.reservation(tenant_id,legal_entity_id,id),
  FOREIGN KEY(tenant_id,legal_entity_id,warehouse_id,location_id) REFERENCES business_inventory.location(tenant_id,legal_entity_id,warehouse_id,id)
);
CREATE INDEX reservation_allocation_position_idx ON business_inventory.reservation_allocation(tenant_id,legal_entity_id,warehouse_id,location_id,stock_status,lot_id,serial_id);
CREATE TABLE business_inventory.reservation_event (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, reservation_id uuid NOT NULL,
  event_type varchar(20) NOT NULL CHECK(event_type IN ('Created','Released','Consumed','Expired','Cancelled')),
  quantity numeric(20,6) NOT NULL CHECK(quantity>=0), actor_id uuid NOT NULL, occurred_at timestamptz NOT NULL, evidence jsonb NOT NULL DEFAULT '{}'::jsonb,
  PRIMARY KEY(tenant_id,id), FOREIGN KEY(tenant_id,legal_entity_id,reservation_id) REFERENCES business_inventory.reservation(tenant_id,legal_entity_id,id)
);
CREATE INDEX reservation_event_history_idx ON business_inventory.reservation_event(tenant_id,reservation_id,occurred_at,id);

CREATE TABLE business_inventory.inventory_transfer (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, product_variant_id uuid NOT NULL,
  source_warehouse_id uuid NOT NULL, source_location_id uuid NOT NULL, destination_warehouse_id uuid NOT NULL,
  destination_location_id uuid NOT NULL, lot_id uuid, serial_id uuid, transfer_mode varchar(12) NOT NULL CHECK(transfer_mode IN ('Instant','InTransit')),
  status varchar(20) NOT NULL CHECK(status IN ('Draft','Shipped','PartiallyReceived','Received','Cancelled','Reversed')),
  requested_quantity numeric(20,6) NOT NULL CHECK(requested_quantity>0), entered_quantity numeric(20,6) NOT NULL CHECK(entered_quantity>0),
  entered_uom_id uuid NOT NULL, conversion_factor_snapshot numeric(20,10) NOT NULL CHECK(conversion_factor_snapshot>0),
  shipped_quantity numeric(20,6) NOT NULL DEFAULT 0 CHECK(shipped_quantity>=0),
  received_quantity numeric(20,6) NOT NULL DEFAULT 0 CHECK(received_quantity>=0), source_module varchar(40) NOT NULL,
  source_document_id uuid, idempotency_key varchar(200) NOT NULL, request_hash char(64) NOT NULL, actor_id uuid NOT NULL,
  operational_date date NOT NULL, posting_date date NOT NULL, created_at timestamptz NOT NULL, version bigint NOT NULL DEFAULT 1,
  PRIMARY KEY(tenant_id,id), UNIQUE(tenant_id,legal_entity_id,id), UNIQUE(tenant_id,legal_entity_id,source_module,idempotency_key),
  CHECK(source_warehouse_id<>destination_warehouse_id OR source_location_id<>destination_location_id),
  CHECK(shipped_quantity<=requested_quantity AND received_quantity<=shipped_quantity),
  CHECK(posting_date>=operational_date),
  FOREIGN KEY(tenant_id,legal_entity_id,source_warehouse_id) REFERENCES business_inventory.warehouse(tenant_id,legal_entity_id,id),
  FOREIGN KEY(tenant_id,legal_entity_id,source_warehouse_id,source_location_id) REFERENCES business_inventory.location(tenant_id,legal_entity_id,warehouse_id,id),
  FOREIGN KEY(tenant_id,legal_entity_id,destination_warehouse_id) REFERENCES business_inventory.warehouse(tenant_id,legal_entity_id,id),
  FOREIGN KEY(tenant_id,legal_entity_id,destination_warehouse_id,destination_location_id) REFERENCES business_inventory.location(tenant_id,legal_entity_id,warehouse_id,id),
  FOREIGN KEY(tenant_id,legal_entity_id,product_variant_id) REFERENCES business_inventory.product_inventory_profile(tenant_id,legal_entity_id,product_variant_id),
  FOREIGN KEY(tenant_id,legal_entity_id,product_variant_id,lot_id) REFERENCES business_inventory.lot(tenant_id,legal_entity_id,product_variant_id,id),
  FOREIGN KEY(tenant_id,legal_entity_id,product_variant_id,serial_id) REFERENCES business_inventory.serial(tenant_id,legal_entity_id,product_variant_id,id)
);
CREATE TABLE business_inventory.transfer_receipt (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, transfer_id uuid NOT NULL,
  operation_id uuid NOT NULL, received_quantity numeric(20,6) NOT NULL CHECK(received_quantity>0),
  idempotency_key varchar(200) NOT NULL, actor_id uuid NOT NULL, received_at timestamptz NOT NULL,
  PRIMARY KEY(tenant_id,id), UNIQUE(tenant_id,transfer_id,idempotency_key),
  FOREIGN KEY(tenant_id,legal_entity_id,transfer_id) REFERENCES business_inventory.inventory_transfer(tenant_id,legal_entity_id,id),
  FOREIGN KEY(tenant_id,legal_entity_id,operation_id) REFERENCES business_inventory.inventory_operation(tenant_id,legal_entity_id,id)
);
CREATE TABLE business_inventory.transfer_event (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, transfer_id uuid NOT NULL,
  event_type varchar(20) NOT NULL CHECK(event_type IN ('Created','Shipped','PartiallyReceived','Received','Cancelled','Reversed')),
  quantity numeric(20,6) NOT NULL CHECK(quantity>=0), actor_id uuid NOT NULL, occurred_at timestamptz NOT NULL,
  PRIMARY KEY(tenant_id,id), FOREIGN KEY(tenant_id,legal_entity_id,transfer_id) REFERENCES business_inventory.inventory_transfer(tenant_id,legal_entity_id,id)
);

CREATE TABLE business_inventory.valuation_entry (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, operation_id uuid NOT NULL,
  movement_id uuid NOT NULL, product_variant_id uuid NOT NULL, effective_at timestamptz NOT NULL,
  quantity_delta numeric(20,6) NOT NULL, value_delta numeric(20,6) NOT NULL, unit_cost numeric(20,6) NOT NULL CHECK(unit_cost >= 0),
  currency_code char(3) NOT NULL, costing_method varchar(20) NOT NULL CHECK(costing_method IN ('MovingAverage','FIFO')),
  fact_type varchar(40) NOT NULL, accounting_fact_id uuid, created_at timestamptz NOT NULL,
  PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,id), UNIQUE (tenant_id,legal_entity_id,movement_id),
  FOREIGN KEY (tenant_id,legal_entity_id,operation_id) REFERENCES business_inventory.inventory_operation(tenant_id,legal_entity_id,id),
  FOREIGN KEY (tenant_id,legal_entity_id,movement_id) REFERENCES business_inventory.stock_movement_entry(tenant_id,legal_entity_id,id),
  FOREIGN KEY (tenant_id,legal_entity_id,product_variant_id) REFERENCES business_inventory.product_inventory_profile(tenant_id,legal_entity_id,product_variant_id)
);
CREATE INDEX valuation_scope_history_idx ON business_inventory.valuation_entry(tenant_id,legal_entity_id,product_variant_id,effective_at,id);

CREATE TABLE business_inventory.cost_layer (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, product_variant_id uuid NOT NULL,
  source_valuation_id uuid NOT NULL, effective_at timestamptz NOT NULL, original_quantity numeric(20,6) NOT NULL CHECK(original_quantity > 0),
  remaining_quantity numeric(20,6) NOT NULL CHECK(remaining_quantity >= 0 AND remaining_quantity <= original_quantity),
  unit_cost numeric(20,6) NOT NULL CHECK(unit_cost >= 0), currency_code char(3) NOT NULL, created_at timestamptz NOT NULL,
  PRIMARY KEY (tenant_id,id), UNIQUE(tenant_id,legal_entity_id,id), FOREIGN KEY (tenant_id,legal_entity_id,source_valuation_id) REFERENCES business_inventory.valuation_entry(tenant_id,legal_entity_id,id)
);
CREATE INDEX cost_layer_fifo_idx ON business_inventory.cost_layer(tenant_id,legal_entity_id,product_variant_id,effective_at,id) WHERE remaining_quantity > 0;
CREATE TABLE business_inventory.cost_layer_consumption (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, cost_layer_id uuid NOT NULL,
  movement_id uuid NOT NULL, quantity numeric(20,6) NOT NULL CHECK(quantity > 0),
  unit_cost numeric(20,6) NOT NULL CHECK(unit_cost >= 0), consumed_at timestamptz NOT NULL,
  PRIMARY KEY(tenant_id,id),
  FOREIGN KEY(tenant_id,legal_entity_id,cost_layer_id) REFERENCES business_inventory.cost_layer(tenant_id,legal_entity_id,id),
  FOREIGN KEY(tenant_id,legal_entity_id,movement_id) REFERENCES business_inventory.stock_movement_entry(tenant_id,legal_entity_id,id)
);
CREATE INDEX cost_layer_consumption_movement_idx ON business_inventory.cost_layer_consumption(tenant_id,movement_id);
CREATE TABLE business_inventory.cost_state (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, product_variant_id uuid NOT NULL,
  costing_method varchar(20) NOT NULL CHECK(costing_method IN ('MovingAverage','FIFO')),
  quantity numeric(20,6) NOT NULL DEFAULT 0 CHECK(quantity >= 0), carrying_value numeric(20,6) NOT NULL DEFAULT 0 CHECK(carrying_value >= 0),
  average_unit_cost numeric(20,10) NOT NULL DEFAULT 0 CHECK(average_unit_cost >= 0), currency_code char(3) NOT NULL,
  version bigint NOT NULL DEFAULT 1, updated_at timestamptz NOT NULL,
  PRIMARY KEY(tenant_id,id), UNIQUE(tenant_id,legal_entity_id,product_variant_id),
  CHECK (quantity <> 0 OR carrying_value = 0)
);
CREATE INDEX cost_state_valuation_idx ON business_inventory.cost_state(tenant_id,legal_entity_id,product_variant_id);

CREATE TABLE business_inventory.physical_count (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, count_number varchar(64) NOT NULL,
  warehouse_id uuid NOT NULL, status varchar(16) NOT NULL CHECK(status IN ('Draft','Counting','Approved','Posted','Cancelled')),
  count_mode varchar(16) NOT NULL CHECK(count_mode IN ('Simple','Full','Cycle','Blind')), snapshot_at timestamptz NOT NULL,
  created_by uuid NOT NULL, created_at timestamptz NOT NULL, version bigint NOT NULL DEFAULT 1,
  PRIMARY KEY(tenant_id,id), UNIQUE(tenant_id,legal_entity_id,id), UNIQUE(tenant_id,legal_entity_id,count_number),
  FOREIGN KEY(tenant_id,legal_entity_id,warehouse_id) REFERENCES business_inventory.warehouse(tenant_id,legal_entity_id,id)
);
CREATE TABLE business_inventory.count_line (
  tenant_id uuid NOT NULL, id uuid NOT NULL, count_id uuid NOT NULL, legal_entity_id uuid NOT NULL,
  product_variant_id uuid NOT NULL, warehouse_id uuid NOT NULL, location_id uuid NOT NULL, stock_status varchar(24) NOT NULL,
  lot_id uuid, serial_id uuid, expected_quantity numeric(20,6) NOT NULL, counted_quantity numeric(20,6), counted_unit_cost numeric(20,6),
  difference_quantity numeric(20,6), entered_by uuid, entered_at timestamptz, posted_operation_id uuid,
  PRIMARY KEY(tenant_id,id), UNIQUE NULLS NOT DISTINCT(tenant_id,count_id,product_variant_id,warehouse_id,location_id,stock_status,lot_id,serial_id),
  FOREIGN KEY(tenant_id,legal_entity_id,count_id) REFERENCES business_inventory.physical_count(tenant_id,legal_entity_id,id),
  FOREIGN KEY(tenant_id,legal_entity_id,posted_operation_id) REFERENCES business_inventory.inventory_operation(tenant_id,legal_entity_id,id),
  FOREIGN KEY(tenant_id,warehouse_id,location_id) REFERENCES business_inventory.location(tenant_id,warehouse_id,id),
  FOREIGN KEY(tenant_id,legal_entity_id,product_variant_id) REFERENCES business_inventory.product_inventory_profile(tenant_id,legal_entity_id,product_variant_id),
  FOREIGN KEY(tenant_id,legal_entity_id,product_variant_id,lot_id) REFERENCES business_inventory.lot(tenant_id,legal_entity_id,product_variant_id,id),
  FOREIGN KEY(tenant_id,legal_entity_id,product_variant_id,serial_id) REFERENCES business_inventory.serial(tenant_id,legal_entity_id,product_variant_id,id),
  CHECK (counted_quantity IS NULL OR counted_quantity >= 0),
  CHECK (difference_quantity IS NULL OR difference_quantity=counted_quantity-expected_quantity)
);

CREATE TABLE business_inventory.inventory_audit (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, actor_id uuid NOT NULL,
  action varchar(80) NOT NULL, entity_type varchar(60) NOT NULL, entity_id uuid NOT NULL,
  occurred_at timestamptz NOT NULL, correlation_id uuid NOT NULL, evidence jsonb NOT NULL DEFAULT '{}'::jsonb,
  PRIMARY KEY(tenant_id,id)
);

REVOKE ALL ON SCHEMA business_inventory FROM PUBLIC;
GRANT USAGE ON SCHEMA business_inventory TO erp_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA business_inventory TO erp_app;

CREATE OR REPLACE FUNCTION business_inventory.reject_history_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  RAISE EXCEPTION 'inventory.immutable_history';
END $$;
CREATE TRIGGER stock_movement_immutable BEFORE UPDATE OR DELETE ON business_inventory.stock_movement_entry FOR EACH ROW EXECUTE FUNCTION business_inventory.reject_history_mutation();
CREATE TRIGGER valuation_entry_immutable BEFORE UPDATE OR DELETE ON business_inventory.valuation_entry FOR EACH ROW EXECUTE FUNCTION business_inventory.reject_history_mutation();
CREATE TRIGGER inventory_audit_immutable BEFORE UPDATE OR DELETE ON business_inventory.inventory_audit FOR EACH ROW EXECUTE FUNCTION business_inventory.reject_history_mutation();

DO $$
DECLARE table_name text;
BEGIN
  FOREACH table_name IN ARRAY ARRAY['warehouse','movement_sequence','location','product_inventory_profile','lot','serial','inventory_operation','stock_movement_entry','stock_balance','reservation','reservation_allocation','reservation_event','inventory_transfer','transfer_receipt','transfer_event','valuation_entry','cost_layer','cost_layer_consumption','cost_state','physical_count','count_line','inventory_audit'] LOOP
    EXECUTE format('ALTER TABLE business_inventory.%I ENABLE ROW LEVEL SECURITY', table_name);
    EXECUTE format('ALTER TABLE business_inventory.%I FORCE ROW LEVEL SECURITY', table_name);
    EXECUTE format('CREATE POLICY tenant_isolation ON business_inventory.%I USING (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid) WITH CHECK (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid)', table_name);
  END LOOP;
END $$;
