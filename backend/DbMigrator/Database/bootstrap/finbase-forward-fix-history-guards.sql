CREATE OR REPLACE FUNCTION foundation_finbase.protect_rounding_policy_history()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = 'finbase.rounding_policy_history_immutable';
END;
$$;

DROP TRIGGER IF EXISTS trg_finbase_protect_rounding_policy_history ON foundation_finbase.financial_rounding_policy;
CREATE TRIGGER trg_finbase_protect_rounding_policy_history
    BEFORE UPDATE OR DELETE ON foundation_finbase.financial_rounding_policy
    FOR EACH ROW EXECUTE FUNCTION foundation_finbase.protect_rounding_policy_history();

CREATE OR REPLACE FUNCTION foundation_finbase.protect_payment_term_version_history()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        IF OLD.status = 'Draft' THEN RETURN OLD; END IF;
        RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = 'finbase.payment_term_history_immutable';
    END IF;

    IF OLD.status = 'Draft' THEN
        IF NEW.status IN ('Draft', 'Active', 'Retired') THEN RETURN NEW; END IF;
    ELSIF OLD.status = 'Active' AND NEW.status = 'Retired'
       AND NEW.effective_to IS NOT NULL AND NEW.effective_to >= OLD.effective_from
       AND (to_jsonb(NEW) - 'status' - 'effective_to') = (to_jsonb(OLD) - 'status' - 'effective_to') THEN
        RETURN NEW;
    END IF;

    RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = 'finbase.payment_term_history_immutable';
END;
$$;

DROP TRIGGER IF EXISTS trg_finbase_protect_payment_term_version_history ON foundation_finbase.payment_term_version;
CREATE TRIGGER trg_finbase_protect_payment_term_version_history
    BEFORE UPDATE OR DELETE ON foundation_finbase.payment_term_version
    FOR EACH ROW EXECUTE FUNCTION foundation_finbase.protect_payment_term_version_history();

CREATE OR REPLACE FUNCTION foundation_finbase.protect_payment_term_rule_history()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    parent_status text;
    row_to_check jsonb;
BEGIN
    SELECT status INTO parent_status
      FROM foundation_finbase.payment_term_version
     WHERE tenant_id = OLD.tenant_id AND id = OLD.version_id;

    IF parent_status = 'Draft' THEN
        IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
        RETURN NEW;
    END IF;

    RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = 'finbase.payment_term_history_immutable';
END;
$$;

DROP TRIGGER IF EXISTS trg_finbase_protect_payment_term_rule_history ON foundation_finbase.payment_term_rule;
CREATE TRIGGER trg_finbase_protect_payment_term_rule_history
    BEFORE UPDATE OR DELETE ON foundation_finbase.payment_term_rule
    FOR EACH ROW EXECUTE FUNCTION foundation_finbase.protect_payment_term_rule_history();

CREATE OR REPLACE FUNCTION foundation_finbase.protect_payment_term_identity()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = 'finbase.payment_term_history_immutable';
    END IF;

    IF OLD.status = 'Active' AND NEW.status = 'Archived'
       AND NEW.version = OLD.version + 1
       AND (to_jsonb(NEW) - 'status' - 'version' - 'updated_at') = (to_jsonb(OLD) - 'status' - 'version' - 'updated_at') THEN
        RETURN NEW;
    END IF;

    IF OLD.status = 'Active' AND NEW.status = 'Active'
       AND NEW.version = OLD.version + 1
       AND (to_jsonb(NEW) - 'version' - 'updated_at') = (to_jsonb(OLD) - 'version' - 'updated_at') THEN
        RETURN NEW;
    END IF;

    RAISE EXCEPTION USING ERRCODE = '55000', MESSAGE = 'finbase.payment_term_history_immutable';
END;
$$;

DROP TRIGGER IF EXISTS trg_finbase_protect_payment_term_identity ON foundation_finbase.payment_term;
CREATE TRIGGER trg_finbase_protect_payment_term_identity
    BEFORE UPDATE OR DELETE ON foundation_finbase.payment_term
    FOR EACH ROW EXECUTE FUNCTION foundation_finbase.protect_payment_term_identity();
