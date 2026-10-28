CREATE TABLE business_inventory.negative_stock_condition (
  tenant_id uuid NOT NULL,
  id uuid NOT NULL,
  legal_entity_id uuid NOT NULL,
  product_variant_id uuid NOT NULL,
  warehouse_id uuid NOT NULL,
  location_id uuid NOT NULL,
  stock_status varchar(24) NOT NULL,
  lot_id uuid,
  serial_id uuid,
  condition_status varchar(12) NOT NULL CHECK (condition_status IN ('Active','Resolved')),
  shortfall_base_quantity numeric(20,6) NOT NULL CHECK (shortfall_base_quantity >= 0),
  opened_by_movement_id uuid NOT NULL,
  last_seen_movement_id uuid NOT NULL,
  resolved_by_movement_id uuid,
  opened_at timestamptz NOT NULL,
  resolved_at timestamptz,
  PRIMARY KEY (tenant_id,id),
  FOREIGN KEY (tenant_id,legal_entity_id,product_variant_id) REFERENCES business_inventory.product_inventory_profile(tenant_id,legal_entity_id,product_variant_id),
  FOREIGN KEY (tenant_id,legal_entity_id,warehouse_id,location_id) REFERENCES business_inventory.location(tenant_id,legal_entity_id,warehouse_id,id),
  FOREIGN KEY (tenant_id,legal_entity_id,opened_by_movement_id) REFERENCES business_inventory.stock_movement_entry(tenant_id,legal_entity_id,id),
  FOREIGN KEY (tenant_id,legal_entity_id,last_seen_movement_id) REFERENCES business_inventory.stock_movement_entry(tenant_id,legal_entity_id,id),
  FOREIGN KEY (tenant_id,legal_entity_id,resolved_by_movement_id) REFERENCES business_inventory.stock_movement_entry(tenant_id,legal_entity_id,id),
  CHECK ((condition_status='Active' AND resolved_at IS NULL AND resolved_by_movement_id IS NULL AND shortfall_base_quantity>0)
      OR (condition_status='Resolved' AND resolved_at IS NOT NULL AND resolved_by_movement_id IS NOT NULL AND shortfall_base_quantity=0))
);
CREATE UNIQUE INDEX negative_stock_condition_active_position_uq
  ON business_inventory.negative_stock_condition(tenant_id,legal_entity_id,product_variant_id,warehouse_id,location_id,stock_status,lot_id,serial_id)
  NULLS NOT DISTINCT WHERE condition_status='Active';
CREATE INDEX negative_stock_condition_history_idx
  ON business_inventory.negative_stock_condition(tenant_id,legal_entity_id,product_variant_id,condition_status,opened_at DESC,id DESC);
ALTER TABLE business_inventory.negative_stock_condition ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_inventory.negative_stock_condition FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON business_inventory.negative_stock_condition
  USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
  WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
GRANT SELECT, INSERT, UPDATE ON business_inventory.negative_stock_condition TO erp_app;
