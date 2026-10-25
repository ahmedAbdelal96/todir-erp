SET search_path = finance_subledger, public;
SET row_security = off;

DROP INDEX IF EXISTS finance_subledger.ix_subledger_match_open_display_normalized;
DROP INDEX IF EXISTS finance_subledger.ix_subledger_match_open_source_normalized;

RESET row_security;
RESET search_path;
