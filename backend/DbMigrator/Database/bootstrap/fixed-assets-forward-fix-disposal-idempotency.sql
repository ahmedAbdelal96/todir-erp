ALTER TABLE business_assets.asset_disposal
    ADD COLUMN IF NOT EXISTS quantity_was_explicit boolean NOT NULL DEFAULT false;
