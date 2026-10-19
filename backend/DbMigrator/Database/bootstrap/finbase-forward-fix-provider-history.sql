ALTER TABLE foundation_finbase.exchange_rate
    ADD COLUMN IF NOT EXISTS provider_event_key text;

CREATE UNIQUE INDEX IF NOT EXISTS ux_finbase_provider_rate_event
    ON foundation_finbase.exchange_rate (tenant_id, legal_entity_id, provider_event_key)
    WHERE provider_event_key IS NOT NULL;

CREATE TABLE IF NOT EXISTS foundation_finbase.exchange_rate_source_config (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    source_code text NOT NULL,
    from_currency text NOT NULL,
    to_currency text NOT NULL,
    purpose text NOT NULL,
    priority smallint NOT NULL,
    enabled boolean NOT NULL DEFAULT true,
    credential_reference text,
    sync_interval_seconds integer NOT NULL DEFAULT 3600,
    failure_count integer NOT NULL DEFAULT 0,
    last_error_code text,
    next_attempt_at timestamptz,
    last_synced_at timestamptz,
    version bigint NOT NULL DEFAULT 1,
    CONSTRAINT exchange_rate_source_config_pk PRIMARY KEY (tenant_id, id),
    CONSTRAINT exchange_rate_source_config_pair_check CHECK (from_currency <> to_currency),
    CONSTRAINT exchange_rate_source_config_purpose_check CHECK (purpose IN ('Accounting', 'Commercial', 'Contractual', 'BankActual', 'Settlement', 'Tax')),
    CONSTRAINT exchange_rate_source_config_source_check CHECK (source_code ~ '^[a-z][a-z0-9_-]{1,63}$'),
    CONSTRAINT exchange_rate_source_config_priority_check CHECK (priority > 0),
    CONSTRAINT exchange_rate_source_config_interval_check CHECK (sync_interval_seconds BETWEEN 60 AND 86400),
    CONSTRAINT exchange_rate_source_config_failures_check CHECK (failure_count >= 0),
    CONSTRAINT exchange_rate_source_config_error_code_check CHECK (last_error_code IS NULL OR last_error_code ~ '^[a-z0-9_.-]{1,80}$'),
    CONSTRAINT exchange_rate_source_config_credential_reference_check CHECK (credential_reference IS NULL OR credential_reference ~ '^(secret-ref|vault-ref|env-ref):[A-Za-z0-9/_.-]+$'),
    CONSTRAINT exchange_rate_source_config_version_check CHECK (version > 0),
    CONSTRAINT exchange_rate_source_config_unique UNIQUE (tenant_id, legal_entity_id, source_code, from_currency, to_currency, purpose)
);

CREATE INDEX IF NOT EXISTS ix_finbase_rate_source_due
    ON foundation_finbase.exchange_rate_source_config (enabled, next_attempt_at, priority, tenant_id, legal_entity_id);

ALTER TABLE foundation_finbase.exchange_rate_source_config ENABLE ROW LEVEL SECURITY;
ALTER TABLE foundation_finbase.exchange_rate_source_config FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON foundation_finbase.exchange_rate_source_config
    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
GRANT SELECT, INSERT, UPDATE ON TABLE foundation_finbase.exchange_rate_source_config TO erp_app;
GRANT SELECT, UPDATE ON TABLE foundation_finbase.exchange_rate_source_config TO erp_dispatcher;
GRANT USAGE ON SCHEMA foundation_finbase TO erp_dispatcher;

CREATE OR REPLACE FUNCTION foundation_finbase.claim_due_exchange_rate_sources(limit_count integer)
RETURNS TABLE(tenant_id uuid, id uuid, legal_entity_id uuid, source_code text, from_currency text, to_currency text, purpose text, priority smallint, credential_reference text, sync_interval_seconds integer, failure_count integer)
LANGUAGE sql
SECURITY DEFINER
SET search_path TO 'foundation_finbase', 'pg_catalog'
AS $$
    UPDATE foundation_finbase.exchange_rate_source_config AS source
       SET next_attempt_at = now() + make_interval(secs => source.sync_interval_seconds)
     WHERE (source.tenant_id, source.id) IN
     (
         SELECT due.tenant_id, due.id
           FROM foundation_finbase.exchange_rate_source_config AS due
          WHERE due.enabled AND (due.next_attempt_at IS NULL OR due.next_attempt_at <= now())
          ORDER BY due.priority, due.next_attempt_at NULLS FIRST, due.tenant_id, due.id
          FOR UPDATE SKIP LOCKED
          LIMIT LEAST(GREATEST(limit_count, 1), 100)
     )
    RETURNING source.tenant_id, source.id, source.legal_entity_id, source.source_code,
              source.from_currency, source.to_currency, source.purpose, source.priority,
              source.credential_reference, source.sync_interval_seconds, source.failure_count;
$$;

REVOKE ALL ON FUNCTION foundation_finbase.claim_due_exchange_rate_sources(integer) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION foundation_finbase.claim_due_exchange_rate_sources(integer) TO erp_app;

CREATE OR REPLACE FUNCTION foundation_finbase.protect_exchange_rate_history()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        IF OLD.status IN ('Approved', 'Superseded') THEN
            RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = 'finbase.rate_history_immutable';
        END IF;
        RETURN OLD;
    END IF;

    IF OLD.status IN ('Approved', 'Superseded') THEN
        IF OLD.status = 'Approved'
           AND NEW.status = 'Superseded'
           AND NEW.version = OLD.version + 1
           AND (to_jsonb(NEW) - 'status' - 'version') = (to_jsonb(OLD) - 'status' - 'version') THEN
            RETURN NEW;
        END IF;
        RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = 'finbase.rate_history_immutable';
    END IF;

    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS trg_finbase_protect_exchange_rate_history ON foundation_finbase.exchange_rate;
CREATE TRIGGER trg_finbase_protect_exchange_rate_history
    BEFORE UPDATE OR DELETE ON foundation_finbase.exchange_rate
    FOR EACH ROW EXECUTE FUNCTION foundation_finbase.protect_exchange_rate_history();
