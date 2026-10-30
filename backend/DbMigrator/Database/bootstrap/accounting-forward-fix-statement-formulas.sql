SET search_path = business_accounting, public;
SET row_security = off;

-- Formulas are intentionally constrained to references between named lines. They
-- never execute user supplied SQL and Accounting evaluates them against Ledger's
-- canonical balances.
ALTER TABLE business_accounting.financial_statement_line
    ADD COLUMN IF NOT EXISTS formula_line_codes text[] NOT NULL DEFAULT '{}';

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'accounting_statement_formula_codes_valid') THEN
        ALTER TABLE business_accounting.financial_statement_line
            ADD CONSTRAINT accounting_statement_formula_codes_valid
            CHECK (array_position(formula_line_codes, '') IS NULL);
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS ux_accounting_active_statement_kind
    ON business_accounting.financial_statement_definition (tenant_id, legal_entity_id, statement_kind)
    WHERE status = 'Active';
