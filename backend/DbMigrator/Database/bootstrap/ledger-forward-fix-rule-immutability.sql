-- Used posting rule and accounting configuration versions are historical facts.
-- New versions must be created instead of mutating a version referenced by an entry.
CREATE OR REPLACE FUNCTION finance_ledger.prevent_used_version_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF TG_TABLE_NAME = 'posting_rule_version' AND (OLD.status = 'Active' OR EXISTS (SELECT 1 FROM finance_ledger.journal_entry e WHERE e.tenant_id = OLD.tenant_id AND e.posting_rule_version_id = OLD.id)) THEN
        RAISE EXCEPTION 'ledger.posting_rule_version_immutable' USING ERRCODE = '55000';
    END IF;
    IF TG_TABLE_NAME = 'accounting_configuration_version' AND EXISTS (SELECT 1 FROM finance_ledger.journal_entry e WHERE e.tenant_id = OLD.tenant_id AND e.accounting_configuration_version_id = OLD.id) THEN
        RAISE EXCEPTION 'ledger.accounting_configuration_immutable' USING ERRCODE = '55000';
    END IF;
    RETURN CASE WHEN TG_OP = 'DELETE' THEN OLD ELSE NEW END;
END; $$;

DROP TRIGGER IF EXISTS posting_rule_version_immutable ON finance_ledger.posting_rule_version;
CREATE TRIGGER posting_rule_version_immutable BEFORE UPDATE OR DELETE ON finance_ledger.posting_rule_version FOR EACH ROW EXECUTE FUNCTION finance_ledger.prevent_used_version_mutation();
DROP TRIGGER IF EXISTS accounting_configuration_version_immutable ON finance_ledger.accounting_configuration_version;
CREATE TRIGGER accounting_configuration_version_immutable BEFORE UPDATE OR DELETE ON finance_ledger.accounting_configuration_version FOR EACH ROW EXECUTE FUNCTION finance_ledger.prevent_used_version_mutation();
