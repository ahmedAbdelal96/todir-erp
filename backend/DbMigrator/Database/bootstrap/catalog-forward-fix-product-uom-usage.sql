ALTER TABLE foundation_catalog.product_uom
    ADD COLUMN IF NOT EXISTS usage_flags text[] NOT NULL DEFAULT '{}';

ALTER TABLE foundation_catalog.product_uom
    ADD COLUMN IF NOT EXISTS rounding_precision smallint;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'product_uom_rounding_precision_check'
          AND conrelid = 'foundation_catalog.product_uom'::regclass
    ) THEN
        ALTER TABLE foundation_catalog.product_uom
            ADD CONSTRAINT product_uom_rounding_precision_check
            CHECK (rounding_precision IS NULL OR rounding_precision BETWEEN 0 AND 6);
    END IF;
END $$;
