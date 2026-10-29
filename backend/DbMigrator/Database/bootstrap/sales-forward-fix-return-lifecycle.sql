-- Durable Sales return orchestration state and typed cross-module results.
ALTER TABLE business_sales.sales_return_request
  DROP CONSTRAINT IF EXISTS sales_return_request_state_check;

ALTER TABLE business_sales.sales_return_request
  ADD CONSTRAINT sales_return_request_state_check
  CHECK (state IN ('Requested','Approved','Processing','Executed','Failed','Rejected','Cancelled'));

ALTER TABLE business_sales.sales_return_request
  ADD COLUMN IF NOT EXISTS execution_idempotency_key varchar(200),
  ADD COLUMN IF NOT EXISTS inventory_receipt_id uuid,
  ADD COLUMN IF NOT EXISTS credit_note_id uuid,
  ADD COLUMN IF NOT EXISTS failure_code varchar(160),
  ADD COLUMN IF NOT EXISTS completed_at timestamptz;

ALTER TABLE business_sales.sales_return_request_line
  ADD COLUMN IF NOT EXISTS warehouse_id uuid,
  ADD COLUMN IF NOT EXISTS location_id uuid,
  ADD COLUMN IF NOT EXISTS unit_of_measure_id uuid,
  ADD COLUMN IF NOT EXISTS lot_id uuid,
  ADD COLUMN IF NOT EXISTS serial_id uuid,
  ADD COLUMN IF NOT EXISTS stock_status varchar(32) NOT NULL DEFAULT 'Available',
  ADD COLUMN IF NOT EXISTS inventory_operation_id uuid;

CREATE UNIQUE INDEX IF NOT EXISTS ux_sales_return_execution_key
  ON business_sales.sales_return_request(tenant_id, legal_entity_id, execution_idempotency_key)
  WHERE execution_idempotency_key IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_sales_return_source_line
  ON business_sales.sales_return_request_line(tenant_id, legal_entity_id, source_line_id);

ALTER TABLE business_sales.sales_order_reservation
  ADD COLUMN IF NOT EXISTS reserved_quantity numeric(20,6),
  ADD COLUMN IF NOT EXISTS consumed_quantity numeric(20,6) NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS released_quantity numeric(20,6) NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS remaining_quantity numeric(20,6);

UPDATE business_sales.sales_order_reservation
SET reserved_quantity = COALESCE(reserved_quantity, quantity),
    remaining_quantity = COALESCE(remaining_quantity, quantity)
WHERE reserved_quantity IS NULL OR remaining_quantity IS NULL;

ALTER TABLE business_sales.sales_order_reservation
  ALTER COLUMN reserved_quantity SET NOT NULL,
  ALTER COLUMN remaining_quantity SET NOT NULL;

ALTER TABLE business_sales.sales_order_reservation
  DROP CONSTRAINT IF EXISTS sales_order_reservation_projection_check;

ALTER TABLE business_sales.sales_order_reservation
  ADD CONSTRAINT sales_order_reservation_projection_check
  CHECK (reserved_quantity > 0 AND consumed_quantity >= 0 AND released_quantity >= 0
    AND remaining_quantity >= 0
    AND consumed_quantity + released_quantity + remaining_quantity = reserved_quantity);

CREATE TABLE IF NOT EXISTS business_sales.sales_reservation_consumption (
  tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, order_id uuid NOT NULL,
  order_line_id uuid NOT NULL, inventory_reservation_id uuid NOT NULL, inventory_operation_id uuid NOT NULL,
  quantity numeric(20,6) NOT NULL CHECK (quantity > 0), idempotency_key varchar(200) NOT NULL, created_at timestamptz NOT NULL,
  PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,order_line_id,idempotency_key), UNIQUE (tenant_id,inventory_operation_id),
  FOREIGN KEY (tenant_id,legal_entity_id,order_id) REFERENCES business_sales.sales_order(tenant_id,legal_entity_id,id),
  FOREIGN KEY (tenant_id,legal_entity_id,order_line_id) REFERENCES business_sales.sales_order_line(tenant_id,legal_entity_id,id)
);

CREATE INDEX IF NOT EXISTS ix_sales_reservation_consumption_order
  ON business_sales.sales_reservation_consumption(tenant_id, legal_entity_id, order_id, created_at DESC, id);

REVOKE ALL ON TABLE business_sales.sales_reservation_consumption FROM PUBLIC;
GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE business_sales.sales_reservation_consumption TO erp_app;
ALTER TABLE business_sales.sales_reservation_consumption ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_sales.sales_reservation_consumption FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON business_sales.sales_reservation_consumption;
CREATE POLICY tenant_isolation ON business_sales.sales_reservation_consumption
  USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
  WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
