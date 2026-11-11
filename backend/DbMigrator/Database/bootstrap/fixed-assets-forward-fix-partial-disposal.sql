ALTER TABLE business_assets.asset_disposal
    ADD COLUMN IF NOT EXISTS quantity numeric(20,6),
    ADD COLUMN IF NOT EXISTS is_full_disposal boolean NOT NULL DEFAULT true,
    ADD COLUMN IF NOT EXISTS sale_reference text,
    ADD COLUMN IF NOT EXISTS idempotency_key text;

ALTER TABLE business_assets.asset_disposal NO FORCE ROW LEVEL SECURITY;
ALTER TABLE business_assets.fixed_asset NO FORCE ROW LEVEL SECURITY;
UPDATE business_assets.asset_disposal d
SET quantity = a.quantity,
    is_full_disposal = true
FROM business_assets.fixed_asset a
WHERE a.tenant_id=d.tenant_id AND a.id=d.asset_id AND d.quantity IS NULL;
ALTER TABLE business_assets.asset_disposal FORCE ROW LEVEL SECURITY;
ALTER TABLE business_assets.fixed_asset FORCE ROW LEVEL SECURITY;

ALTER TABLE business_assets.asset_disposal
    DROP CONSTRAINT IF EXISTS asset_disposal_quantity_check;
ALTER TABLE business_assets.asset_disposal
    ADD CONSTRAINT asset_disposal_quantity_check CHECK (quantity IS NULL OR quantity > 0);

ALTER TABLE business_assets.asset_disposal
    DROP CONSTRAINT IF EXISTS asset_disposal_tenant_id_asset_id_key;
CREATE UNIQUE INDEX IF NOT EXISTS ux_assets_full_disposal
    ON business_assets.asset_disposal(tenant_id,asset_id)
    WHERE is_full_disposal;

CREATE UNIQUE INDEX IF NOT EXISTS ux_assets_disposal_idempotency
    ON business_assets.asset_disposal(tenant_id,idempotency_key)
    WHERE idempotency_key IS NOT NULL;
