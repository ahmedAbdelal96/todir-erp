ALTER TABLE business_assets.asset_opening_import_line
    ADD COLUMN IF NOT EXISTS category_id uuid,
    ADD COLUMN IF NOT EXISTS asset_name text,
    ADD COLUMN IF NOT EXISTS book_definition_id uuid,
    ADD COLUMN IF NOT EXISTS acquisition_date date,
    ADD COLUMN IF NOT EXISTS placed_in_service_date date,
    ADD COLUMN IF NOT EXISTS residual_value numeric(20,6) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS useful_life_months integer,
    ADD COLUMN IF NOT EXISTS depreciation_method text NOT NULL DEFAULT 'StraightLine';

ALTER TABLE business_assets.asset_opening_import_line NO FORCE ROW LEVEL SECURITY;
ALTER TABLE business_assets.asset_category NO FORCE ROW LEVEL SECURITY;
ALTER TABLE business_assets.asset_book_definition NO FORCE ROW LEVEL SECURITY;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'asset_opening_line_category_fk') THEN
        ALTER TABLE business_assets.asset_opening_import_line
            ADD CONSTRAINT asset_opening_line_category_fk
            FOREIGN KEY (tenant_id,category_id) REFERENCES business_assets.asset_category(tenant_id,id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'asset_opening_line_book_fk') THEN
        ALTER TABLE business_assets.asset_opening_import_line
            ADD CONSTRAINT asset_opening_line_book_fk
            FOREIGN KEY (tenant_id,book_definition_id) REFERENCES business_assets.asset_book_definition(tenant_id,id);
    END IF;
END $$;

ALTER TABLE business_assets.asset_opening_import_line
    ADD CONSTRAINT asset_opening_line_values_ck
    CHECK (residual_value >= 0 AND residual_value <= original_cost
        AND (useful_life_months IS NULL OR useful_life_months > 0)
        AND depreciation_method IN ('StraightLine','NoDepreciation','DecliningBalance'));

ALTER TABLE business_assets.asset_opening_import_line FORCE ROW LEVEL SECURITY;
ALTER TABLE business_assets.asset_category FORCE ROW LEVEL SECURITY;
ALTER TABLE business_assets.asset_book_definition FORCE ROW LEVEL SECURITY;

CREATE INDEX IF NOT EXISTS ix_assets_opening_line_books
    ON business_assets.asset_opening_import_line(tenant_id,import_id,book_definition_id);

GRANT SELECT, INSERT, UPDATE ON business_assets.asset_opening_import_line TO erp_app;
