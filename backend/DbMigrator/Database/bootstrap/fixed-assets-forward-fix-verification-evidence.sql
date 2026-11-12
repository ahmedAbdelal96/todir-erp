BEGIN;

ALTER TABLE business_assets.asset_verification_line
    ADD COLUMN IF NOT EXISTS evidence jsonb;

GRANT SELECT, INSERT, UPDATE ON business_assets.asset_verification_line TO erp_app;

COMMIT;
