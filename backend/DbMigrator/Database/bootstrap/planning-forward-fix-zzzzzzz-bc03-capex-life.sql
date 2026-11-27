-- Planning CapEx depreciation uses an explicit useful-life assumption.  It is
-- planning-only metadata and never creates an Asset or Ledger posting.
ALTER TABLE business_planning.capex_plan_item
    ADD COLUMN IF NOT EXISTS useful_life_months integer NOT NULL DEFAULT 60;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'capex_plan_item_useful_life_positive'
          AND conrelid = 'business_planning.capex_plan_item'::regclass
    ) THEN
        ALTER TABLE business_planning.capex_plan_item
            ADD CONSTRAINT capex_plan_item_useful_life_positive CHECK (useful_life_months > 0);
    END IF;
END $$;

GRANT SELECT, INSERT, UPDATE, DELETE ON business_planning.capex_plan_item TO erp_app;
