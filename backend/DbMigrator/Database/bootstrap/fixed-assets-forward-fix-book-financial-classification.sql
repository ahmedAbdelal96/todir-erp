ALTER TABLE business_assets.asset_book_transaction
    ADD COLUMN IF NOT EXISTS accumulated_impairment numeric(20,6) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS revaluation_effect numeric(20,6) NOT NULL DEFAULT 0;

ALTER TABLE business_assets.asset_book_transaction NO FORCE ROW LEVEL SECURITY;
UPDATE business_assets.asset_book_transaction
SET accumulated_impairment = amount
WHERE transaction_type = 'Impairment' AND accumulated_impairment = 0;
ALTER TABLE business_assets.asset_book_transaction FORCE ROW LEVEL SECURITY;

ALTER TABLE business_assets.asset_book_transaction
    DROP CONSTRAINT IF EXISTS asset_book_transaction_financial_classification_check;
ALTER TABLE business_assets.asset_book_transaction
    ADD CONSTRAINT asset_book_transaction_financial_classification_check
    CHECK (accumulated_impairment >= 0 AND revaluation_effect >= 0);

CREATE INDEX IF NOT EXISTS ix_assets_book_transaction_financial_classification
    ON business_assets.asset_book_transaction(tenant_id,asset_book_id,transaction_type,transaction_date,id);
