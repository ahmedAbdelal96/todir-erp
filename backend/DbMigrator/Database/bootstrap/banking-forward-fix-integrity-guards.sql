SET row_security = off;

CREATE OR REPLACE FUNCTION finance_banking.guard_validated_statement_immutability()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF OLD.processing_state = 'Validated' AND (
        NEW.statement_number IS DISTINCT FROM OLD.statement_number OR NEW.opening_date IS DISTINCT FROM OLD.opening_date OR
        NEW.closing_date IS DISTINCT FROM OLD.closing_date OR NEW.currency_code IS DISTINCT FROM OLD.currency_code OR
        NEW.opening_balance IS DISTINCT FROM OLD.opening_balance OR NEW.closing_balance IS DISTINCT FROM OLD.closing_balance OR
        NEW.source_type IS DISTINCT FROM OLD.source_type OR NEW.source_reference IS DISTINCT FROM OLD.source_reference OR
        NEW.treasury_account_id IS DISTINCT FROM OLD.treasury_account_id
    ) THEN
        RAISE EXCEPTION 'validated bank statements are immutable; import a superseding correction' USING ERRCODE = '55000';
    END IF;
    RETURN NEW;
END $$;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgname = 'trg_validated_statement_immutability' AND tgrelid = 'finance_banking.external_statement'::regclass) THEN
        CREATE TRIGGER trg_validated_statement_immutability BEFORE UPDATE ON finance_banking.external_statement FOR EACH ROW EXECUTE FUNCTION finance_banking.guard_validated_statement_immutability();
    END IF;
END $$;

CREATE OR REPLACE FUNCTION finance_banking.guard_finalized_cash_count_immutability()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF OLD.state = 'Finalized' AND (NEW.treasury_account_id IS DISTINCT FROM OLD.treasury_account_id OR NEW.cutoff_date IS DISTINCT FROM OLD.cutoff_date OR NEW.custodian_id IS DISTINCT FROM OLD.custodian_id OR NEW.expected_amount IS DISTINCT FROM OLD.expected_amount OR NEW.counted_amount IS DISTINCT FROM OLD.counted_amount OR NEW.difference IS DISTINCT FROM OLD.difference) THEN
        RAISE EXCEPTION 'finalized cash counts are immutable' USING ERRCODE = '55000';
    END IF;
    RETURN NEW;
END $$;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgname = 'trg_finalized_cash_count_immutability' AND tgrelid = 'finance_banking.cash_count'::regclass) THEN
        CREATE TRIGGER trg_finalized_cash_count_immutability BEFORE UPDATE ON finance_banking.cash_count FOR EACH ROW EXECUTE FUNCTION finance_banking.guard_finalized_cash_count_immutability();
    END IF;
END $$;

CREATE OR REPLACE FUNCTION finance_banking.guard_finalized_reconciliation_immutability()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF OLD.state = 'Finalized' AND (NEW.state <> 'Reopened' OR NEW.treasury_account_id IS DISTINCT FROM OLD.treasury_account_id OR NEW.statement_id IS DISTINCT FROM OLD.statement_id OR NEW.as_of_date IS DISTINCT FROM OLD.as_of_date OR NEW.external_balance IS DISTINCT FROM OLD.external_balance OR NEW.book_balance IS DISTINCT FROM OLD.book_balance OR NEW.difference IS DISTINCT FROM OLD.difference OR NEW.notes IS DISTINCT FROM OLD.notes) THEN
        RAISE EXCEPTION 'finalized reconciliations are immutable; reopen before correction' USING ERRCODE = '55000';
    END IF;
    RETURN NEW;
END $$;

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgname = 'trg_finalized_reconciliation_immutability' AND tgrelid = 'finance_banking.reconciliation_session'::regclass) THEN
        CREATE TRIGGER trg_finalized_reconciliation_immutability BEFORE UPDATE ON finance_banking.reconciliation_session FOR EACH ROW EXECUTE FUNCTION finance_banking.guard_finalized_reconciliation_immutability();
    END IF;
END $$;

RESET row_security;
RESET search_path;
