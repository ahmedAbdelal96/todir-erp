DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conrelid = 'business_assets.fixed_asset'::regclass
          AND conname = 'fixed_asset_lifecycle_state_check'
    ) THEN
        ALTER TABLE business_assets.fixed_asset DROP CONSTRAINT fixed_asset_lifecycle_state_check;
    END IF;
END $$;

ALTER TABLE business_assets.fixed_asset
    ADD CONSTRAINT fixed_asset_lifecycle_state_check
    CHECK (lifecycle_state IN ('Draft','UnderConstruction','Operational','Capitalized','Disposed','Archived'));
