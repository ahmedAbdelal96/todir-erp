SET search_path = finance_subledger, public;
SET row_security = off;

DROP INDEX IF EXISTS finance_subledger.ix_subledger_match_open_context_due;

RESET row_security;
RESET search_path;
