ALTER TABLE foundation_tax.tax_pack_activation
    ADD COLUMN IF NOT EXISTS pack_id text,
    ADD COLUMN IF NOT EXISTS engine_contract_version text,
    ADD COLUMN IF NOT EXISTS status text,
    ADD COLUMN IF NOT EXISTS source_kind text,
    ADD COLUMN IF NOT EXISTS source_title text,
    ADD COLUMN IF NOT EXISTS capabilities jsonb NOT NULL DEFAULT '{}'::jsonb,
    ADD COLUMN IF NOT EXISTS rule_set_references jsonb NOT NULL DEFAULT '[]'::jsonb,
    ADD COLUMN IF NOT EXISTS golden_vector_count integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS golden_vector_status text NOT NULL DEFAULT 'Missing',
    ADD COLUMN IF NOT EXISTS validation_status text NOT NULL DEFAULT 'NotValidated',
    ADD COLUMN IF NOT EXISTS validation_issues jsonb NOT NULL DEFAULT '[]'::jsonb,
    ADD COLUMN IF NOT EXISTS installed_at timestamptz NOT NULL DEFAULT now(),
    ADD COLUMN IF NOT EXISTS installed_by uuid,
    ADD COLUMN IF NOT EXISTS validated_at timestamptz,
    ADD COLUMN IF NOT EXISTS validated_by uuid,
    ADD COLUMN IF NOT EXISTS scheduled_at timestamptz,
    ADD COLUMN IF NOT EXISTS scheduled_by uuid,
    ADD COLUMN IF NOT EXISTS scheduled_effective_from date,
    ADD COLUMN IF NOT EXISTS activated_by uuid,
    ADD COLUMN IF NOT EXISTS superseded_at timestamptz,
    ADD COLUMN IF NOT EXISTS superseded_by uuid,
    ADD COLUMN IF NOT EXISTS retired_at timestamptz,
    ADD COLUMN IF NOT EXISTS retired_by uuid,
    ADD COLUMN IF NOT EXISTS retirement_reason text;

ALTER TABLE foundation_tax.tax_pack_activation NO FORCE ROW LEVEL SECURITY;

UPDATE foundation_tax.tax_pack_activation
SET pack_id = COALESCE(pack_id, pack_code),
    engine_contract_version = COALESCE(engine_contract_version, 'tax_calculation_v1'),
    status = COALESCE(status, 'Installed'),
    source_kind = COALESCE(source_kind, 'Statutory'),
    source_title = COALESCE(source_title, authority_reference)
WHERE pack_id IS NULL OR engine_contract_version IS NULL OR status IS NULL OR source_kind IS NULL OR source_title IS NULL;

ALTER TABLE foundation_tax.tax_pack_activation
    ALTER COLUMN pack_id SET NOT NULL,
    ALTER COLUMN engine_contract_version SET NOT NULL,
    ALTER COLUMN status SET NOT NULL,
    ALTER COLUMN source_kind SET NOT NULL,
    ALTER COLUMN source_title SET NOT NULL;

ALTER TABLE foundation_tax.tax_pack_activation
    DROP CONSTRAINT IF EXISTS tax_pack_activation_status_check,
    DROP CONSTRAINT IF EXISTS tax_pack_activation_source_kind_check,
    DROP CONSTRAINT IF EXISTS tax_pack_activation_vector_status_check,
    DROP CONSTRAINT IF EXISTS tax_pack_activation_validation_status_check;

ALTER TABLE foundation_tax.tax_pack_activation
    ADD CONSTRAINT tax_pack_activation_status_check CHECK (status IN ('Installed','Validated','Scheduled','Active','Superseded','Retired')),
    ADD CONSTRAINT tax_pack_activation_source_kind_check CHECK (source_kind IN ('Statutory','TestOnly')),
    ADD CONSTRAINT tax_pack_activation_vector_status_check CHECK (golden_vector_status IN ('Missing','Pending','Passed','Failed')),
    ADD CONSTRAINT tax_pack_activation_validation_status_check CHECK (validation_status IN ('NotValidated','Valid','Invalid')),
    ADD CONSTRAINT tax_pack_activation_vector_count_check CHECK (golden_vector_count >= 0);

ALTER TABLE foundation_tax.tax_pack_activation
    DROP CONSTRAINT IF EXISTS tax_pack_activation_review_check;

ALTER TABLE foundation_tax.tax_pack_activation
    ADD CONSTRAINT tax_pack_activation_review_check CHECK (review_status IN ('Unreviewed','InReview','Approved','Rejected'));

CREATE UNIQUE INDEX IF NOT EXISTS tax_pack_active_scope
    ON foundation_tax.tax_pack_activation(tenant_id, country_code, pack_code, effective_from)
    WHERE status IN ('Active','Scheduled');

CREATE INDEX IF NOT EXISTS tax_pack_resolution_lookup
    ON foundation_tax.tax_pack_activation(tenant_id, country_code, status, effective_from, effective_to);

CREATE INDEX IF NOT EXISTS tax_pack_lifecycle_history
    ON foundation_tax.tax_pack_activation(tenant_id, country_code, pack_code, installed_at DESC);

GRANT SELECT, INSERT, UPDATE, DELETE ON foundation_tax.tax_pack_activation TO erp_app;

ALTER TABLE foundation_tax.tax_pack_activation FORCE ROW LEVEL SECURITY;
