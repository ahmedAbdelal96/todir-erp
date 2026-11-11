ALTER TABLE business_assets.asset_capitalization_candidate
    ADD COLUMN IF NOT EXISTS economic_source_key text;

ALTER TABLE business_assets.asset_capitalization_candidate NO FORCE ROW LEVEL SECURITY;
UPDATE business_assets.asset_capitalization_candidate
SET economic_source_key = lower(btrim(source_type)) || ':' || lower(btrim(source_reference))
WHERE economic_source_key IS NULL;
ALTER TABLE business_assets.asset_capitalization_candidate FORCE ROW LEVEL SECURITY;

ALTER TABLE business_assets.asset_capitalization_candidate
    ALTER COLUMN economic_source_key SET NOT NULL;

ALTER TABLE business_assets.asset_capitalization_candidate
    DROP CONSTRAINT IF EXISTS asset_capitalization_candidate_economic_source_key_check;
ALTER TABLE business_assets.asset_capitalization_candidate
    ADD CONSTRAINT asset_capitalization_candidate_economic_source_key_check
    CHECK (length(btrim(economic_source_key)) BETWEEN 3 AND 300);

CREATE UNIQUE INDEX IF NOT EXISTS ux_assets_capitalization_economic_source
    ON business_assets.asset_capitalization_candidate(tenant_id,economic_source_key);
