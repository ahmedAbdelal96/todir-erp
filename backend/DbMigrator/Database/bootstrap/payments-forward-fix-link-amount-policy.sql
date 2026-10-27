ALTER TABLE finance_payments.payment_link
    ADD COLUMN IF NOT EXISTS amount_policy text NOT NULL DEFAULT 'Fixed',
    ADD COLUMN IF NOT EXISTS minimum_amount numeric(20, 6),
    ADD COLUMN IF NOT EXISTS maximum_amount numeric(20, 6);

ALTER TABLE finance_payments.payment_link
    DROP CONSTRAINT IF EXISTS payment_link_amount_policy_check;

ALTER TABLE finance_payments.payment_link
    ADD CONSTRAINT payment_link_amount_policy_check CHECK (
        (amount_policy = 'Fixed' AND minimum_amount IS NULL AND maximum_amount IS NULL)
        OR (amount_policy IN ('PartialAllowed','CustomerEnteredWithinLimits')
            AND minimum_amount > 0 AND maximum_amount >= minimum_amount)
    );
