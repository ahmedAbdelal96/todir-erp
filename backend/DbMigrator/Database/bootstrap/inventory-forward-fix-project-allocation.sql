-- Inventory operations can carry optional project allocation metadata for the
-- Projects capability. Keep this additive so restored pre-Projects databases
-- can accept the current operation contract without rewriting history.
ALTER TABLE business_inventory.inventory_operation
  ADD COLUMN IF NOT EXISTS project_id uuid,
  ADD COLUMN IF NOT EXISTS task_id uuid,
  ADD COLUMN IF NOT EXISTS cost_category text;
