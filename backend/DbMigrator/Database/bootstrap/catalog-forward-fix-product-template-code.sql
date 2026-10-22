CREATE SEQUENCE IF NOT EXISTS foundation_catalog.product_template_code_seq;

ALTER TABLE foundation_catalog.product_template
    ADD COLUMN IF NOT EXISTS product_template_code text;

WITH current_max AS (
    SELECT COALESCE(max(substring(product_template_code from '^PT-([0-9]+)$')::bigint), 0) AS value
    FROM foundation_catalog.product_template
), assigned AS (
    SELECT tenant_id, id,
           'PT-' || lpad((current_max.value + row_number() OVER (ORDER BY tenant_id, created_at, id))::text, 8, '0') AS code
    FROM foundation_catalog.product_template
    CROSS JOIN current_max
    WHERE product_template_code IS NULL
)
UPDATE foundation_catalog.product_template p
SET product_template_code = assigned.code
FROM assigned
WHERE p.tenant_id = assigned.tenant_id AND p.id = assigned.id;

SELECT setval('foundation_catalog.product_template_code_seq',
              GREATEST(COALESCE((SELECT max(substring(product_template_code from '^PT-([0-9]+)$')::bigint)
                                 FROM foundation_catalog.product_template), 0), 1),
              EXISTS (SELECT 1 FROM foundation_catalog.product_template
                      WHERE product_template_code ~ '^PT-[0-9]+$'));

ALTER TABLE foundation_catalog.product_template
    ALTER COLUMN product_template_code SET NOT NULL,
    ALTER COLUMN product_template_code SET DEFAULT ('PT-' || lpad(nextval('foundation_catalog.product_template_code_seq')::text, 8, '0'));

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'product_template_code_format_check'
                   AND conrelid = 'foundation_catalog.product_template'::regclass) THEN
        ALTER TABLE foundation_catalog.product_template
            ADD CONSTRAINT product_template_code_format_check
            CHECK (product_template_code ~ '^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$');
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'product_template_code_nonempty_check'
                   AND conrelid = 'foundation_catalog.product_template'::regclass) THEN
        ALTER TABLE foundation_catalog.product_template
            ADD CONSTRAINT product_template_code_nonempty_check
            CHECK (length(btrim(product_template_code)) > 0);
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS product_template_tenant_code_uq
    ON foundation_catalog.product_template (tenant_id, upper(btrim(product_template_code)));

GRANT USAGE, SELECT ON SEQUENCE foundation_catalog.product_template_code_seq TO erp_app;
