SET search_path = business_accounting, public;
SET row_security = off;

-- Statement lines are versioned configuration, not financial facts. A stable code
-- prevents ambiguous presentation and accidental duplicate inclusion per version.
CREATE UNIQUE INDEX IF NOT EXISTS ux_accounting_statement_line_code
    ON business_accounting.financial_statement_line (tenant_id, definition_id, version, code);

CREATE INDEX IF NOT EXISTS ix_accounting_statement_line_type
    ON business_accounting.financial_statement_line (tenant_id, definition_id, version, account_type);
