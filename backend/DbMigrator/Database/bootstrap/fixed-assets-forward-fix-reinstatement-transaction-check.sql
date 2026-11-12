ALTER TABLE business_assets.asset_book_transaction
    DROP CONSTRAINT IF EXISTS asset_book_transaction_transaction_type_check;

ALTER TABLE business_assets.asset_book_transaction
    DROP CONSTRAINT IF EXISTS asset_book_transaction_type_check;

ALTER TABLE business_assets.asset_book_transaction
    ADD CONSTRAINT asset_book_transaction_transaction_type_check
    CHECK (transaction_type IN ('Opening','Acquisition','CapitalAddition','CostReduction','Depreciation','Impairment','Revaluation','Disposal','Reversal','Reinstatement'));
