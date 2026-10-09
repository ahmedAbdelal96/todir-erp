CREATE SCHEMA IF NOT EXISTS foundation_finbase AUTHORIZATION erp_owner;
REVOKE ALL ON SCHEMA foundation_finbase FROM PUBLIC;
GRANT USAGE ON SCHEMA foundation_finbase TO erp_app;

CREATE TABLE IF NOT EXISTS foundation_finbase.currency_reference
(
    code text PRIMARY KEY CHECK (code ~ '^[A-Z]{3}$'), numeric_code smallint NOT NULL CHECK (numeric_code BETWEEN 1 AND 999),
    name_ar text NOT NULL, name_en text NOT NULL, minor_unit smallint NOT NULL CHECK (minor_unit BETWEEN 0 AND 8),
    status text NOT NULL CHECK (status IN ('Active','Deprecated')), valid_from date NOT NULL, valid_to date,
    dataset_version text NOT NULL, CHECK (valid_to IS NULL OR valid_to >= valid_from)
);

CREATE TABLE IF NOT EXISTS foundation_finbase.legal_entity_currency
(
    tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant(id), legal_entity_id uuid NOT NULL,
    currency_code text NOT NULL REFERENCES foundation_finbase.currency_reference(code), enabled boolean NOT NULL,
    allow_sales boolean NOT NULL DEFAULT true, allow_purchase boolean NOT NULL DEFAULT true, allow_payment boolean NOT NULL DEFAULT true,
    allow_manual_rate boolean NOT NULL DEFAULT false, status text NOT NULL CHECK (status IN ('Active','Disabled')),
    version bigint NOT NULL DEFAULT 1, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, legal_entity_id, currency_code), FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform_org.legal_entity(tenant_id,id)
);

CREATE TABLE IF NOT EXISTS foundation_finbase.financial_rounding_policy
(
    tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant(id), id uuid NOT NULL, code text NOT NULL,
    name_ar text NOT NULL, name_en text NOT NULL, mode text NOT NULL CHECK (mode IN ('HalfUp','HalfEven','Down','Up')),
    money_scale smallint NOT NULL CHECK (money_scale BETWEEN 0 AND 8), cash_increment numeric(28,8) NOT NULL CHECK (cash_increment > 0),
    status text NOT NULL CHECK (status IN ('Active','Archived')), version bigint NOT NULL DEFAULT 1, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,code)
);

CREATE TABLE IF NOT EXISTS foundation_finbase.financial_policy
(
    tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant(id), legal_entity_id uuid NOT NULL,
    rounding_policy_id uuid NOT NULL, exchange_rate_max_age_days integer NOT NULL CHECK (exchange_rate_max_age_days BETWEEN 0 AND 3650),
    manual_rate_override_allowed boolean NOT NULL DEFAULT false, version bigint NOT NULL DEFAULT 1, updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,legal_entity_id), FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform_org.legal_entity(tenant_id,id),
    FOREIGN KEY (tenant_id,rounding_policy_id) REFERENCES foundation_finbase.financial_rounding_policy(tenant_id,id)
);

CREATE TABLE IF NOT EXISTS foundation_finbase.exchange_rate
(
    tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant(id), id uuid NOT NULL, legal_entity_id uuid NOT NULL,
    from_currency text NOT NULL REFERENCES foundation_finbase.currency_reference(code), to_currency text NOT NULL REFERENCES foundation_finbase.currency_reference(code),
    rate numeric(28,12) NOT NULL CHECK (rate > 0), purpose text NOT NULL CHECK (purpose IN ('Accounting','Commercial','Contractual','BankActual','Settlement','Tax')),
    effective_date date NOT NULL, source_type text NOT NULL CHECK (source_type IN ('Manual','Provider','Bank','Contract')),
    provider_reference text, status text NOT NULL CHECK (status IN ('Draft','Approved','Superseded','Rejected')),
    approved_at timestamptz, approved_by uuid, version bigint NOT NULL DEFAULT 1, created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform_org.legal_entity(tenant_id,id), CHECK (from_currency <> to_currency)
);
CREATE INDEX IF NOT EXISTS ix_finbase_rate_resolution ON foundation_finbase.exchange_rate(tenant_id,legal_entity_id,from_currency,to_currency,purpose,effective_date DESC,id) WHERE status='Approved';

CREATE TABLE IF NOT EXISTS foundation_finbase.payment_term
(
    tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant(id), id uuid NOT NULL, code text NOT NULL, display_name_ar text NOT NULL, display_name_en text NOT NULL,
    status text NOT NULL CHECK (status IN ('Active','Archived')), version bigint NOT NULL DEFAULT 1, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,code)
);
CREATE TABLE IF NOT EXISTS foundation_finbase.payment_term_version
(
    tenant_id uuid NOT NULL, id uuid NOT NULL, payment_term_id uuid NOT NULL, version_number integer NOT NULL CHECK (version_number > 0),
    effective_from date NOT NULL, effective_to date, status text NOT NULL CHECK (status IN ('Draft','Active','Retired')), description text,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,payment_term_id,version_number), FOREIGN KEY (tenant_id,payment_term_id) REFERENCES foundation_finbase.payment_term(tenant_id,id), CHECK (effective_to IS NULL OR effective_to >= effective_from)
);
CREATE TABLE IF NOT EXISTS foundation_finbase.payment_term_rule
(
    tenant_id uuid NOT NULL, id uuid NOT NULL, version_id uuid NOT NULL, sequence_no integer NOT NULL CHECK (sequence_no > 0),
    allocation_type text NOT NULL CHECK (allocation_type IN ('Percentage','Fixed','Balance')), percentage numeric(18,8), fixed_amount numeric(28,8),
    due_date_basis text NOT NULL CHECK (due_date_basis IN ('InvoiceDate','DeliveryDate','MonthEnd')), days integer NOT NULL DEFAULT 0, day_of_month smallint, month_offset integer NOT NULL DEFAULT 0,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,version_id,sequence_no), FOREIGN KEY (tenant_id,version_id) REFERENCES foundation_finbase.payment_term_version(tenant_id,id),
    CHECK ((allocation_type='Percentage' AND percentage >= 0 AND percentage <= 100 AND fixed_amount IS NULL) OR (allocation_type='Fixed' AND fixed_amount IS NOT NULL AND percentage IS NULL) OR (allocation_type='Balance' AND percentage IS NULL AND fixed_amount IS NULL))
);

GRANT SELECT,INSERT,UPDATE ON ALL TABLES IN SCHEMA foundation_finbase TO erp_app;
ALTER TABLE foundation_finbase.currency_reference ENABLE ROW LEVEL SECURITY;
CREATE POLICY currency_reference_read ON foundation_finbase.currency_reference FOR SELECT TO erp_app USING (true);
DO $$ DECLARE table_name text; BEGIN
  FOREACH table_name IN ARRAY ARRAY['legal_entity_currency','financial_rounding_policy','financial_policy','exchange_rate','payment_term','payment_term_version','payment_term_rule'] LOOP
    EXECUTE format('ALTER TABLE foundation_finbase.%I ENABLE ROW LEVEL SECURITY', table_name);
    EXECUTE format('ALTER TABLE foundation_finbase.%I FORCE ROW LEVEL SECURITY', table_name);
    EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON foundation_finbase.%I', table_name);
    EXECUTE format('CREATE POLICY tenant_isolation ON foundation_finbase.%I USING (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid) WITH CHECK (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid)', table_name);
  END LOOP;
END $$;

INSERT INTO foundation_finbase.currency_reference(code,numeric_code,name_ar,name_en,minor_unit,status,valid_from,dataset_version) VALUES
 ('AED',784,'درهم إماراتي','UAE Dirham',2,'Active','2020-01-01','2026-01'),('EGP',818,'جنيه مصري','Egyptian Pound',2,'Active','2020-01-01','2026-01'),('EUR',978,'يورو','Euro',2,'Active','2020-01-01','2026-01'),('GBP',826,'جنيه إسترليني','Pound Sterling',2,'Active','2020-01-01','2026-01'),('KWD',414,'دينار كويتي','Kuwaiti Dinar',3,'Active','2020-01-01','2026-01'),('SAR',682,'ريال سعودي','Saudi Riyal',2,'Active','2020-01-01','2026-01'),('USD',840,'دولار أمريكي','US Dollar',2,'Active','2020-01-01','2026-01')
 ON CONFLICT (code) DO UPDATE SET name_ar=excluded.name_ar,name_en=excluded.name_en,minor_unit=excluded.minor_unit,status=excluded.status,dataset_version=excluded.dataset_version;
