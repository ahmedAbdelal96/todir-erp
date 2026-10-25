DO $$
DECLARE constraint_name text;
BEGIN
    SELECT c.conname INTO constraint_name
    FROM pg_constraint c
    WHERE c.conrelid = 'finance_subledger.open_item_application'::regclass
      AND c.contype = 'c'
      AND replace(pg_get_constraintdef(c.oid), ' ', '') ILIKE '%debit_applied_amount=credit_applied_amount%';
    IF constraint_name IS NOT NULL THEN
        EXECUTE format('ALTER TABLE finance_subledger.open_item_application DROP CONSTRAINT %I', constraint_name);
    END IF;
END
$$;

ALTER TABLE finance_subledger.open_item_application
    ADD COLUMN IF NOT EXISTS debit_fx_snapshot jsonb,
    ADD COLUMN IF NOT EXISTS credit_fx_snapshot jsonb,
    ADD COLUMN IF NOT EXISTS settlement_base_amount numeric(20,6) NOT NULL DEFAULT 0;

ALTER TABLE finance_subledger.open_item_application
    ADD CONSTRAINT open_item_application_settlement_base_nonnegative
    CHECK (settlement_base_amount >= 0);
