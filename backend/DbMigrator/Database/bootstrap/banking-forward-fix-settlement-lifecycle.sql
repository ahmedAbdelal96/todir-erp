SET row_security = off;

ALTER TABLE finance_banking.pos_cash_settlement_projection
    ADD COLUMN IF NOT EXISTS version bigint NOT NULL DEFAULT 1;
ALTER TABLE finance_banking.provider_settlement_projection
    ADD COLUMN IF NOT EXISTS version bigint NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS statement_line_id uuid,
    ADD COLUMN IF NOT EXISTS matched_at timestamptz;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='provider_settlement_statement_line_fk') THEN
        ALTER TABLE finance_banking.provider_settlement_projection
            ADD CONSTRAINT provider_settlement_statement_line_fk
            FOREIGN KEY (tenant_id, legal_entity_id, statement_line_id)
            REFERENCES finance_banking.external_statement_line(tenant_id, legal_entity_id, id);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_banking_provider_settlement_line
    ON finance_banking.provider_settlement_projection(tenant_id, legal_entity_id, statement_line_id)
    WHERE statement_line_id IS NOT NULL;
GRANT SELECT, INSERT, UPDATE ON finance_banking.pos_cash_settlement_projection TO erp_app;
GRANT SELECT, INSERT, UPDATE ON finance_banking.provider_settlement_projection TO erp_app;
RESET row_security;
RESET search_path;
