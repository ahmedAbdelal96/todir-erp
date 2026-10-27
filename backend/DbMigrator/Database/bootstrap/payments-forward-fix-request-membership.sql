ALTER TABLE finance_payments.payment_request
    ADD COLUMN IF NOT EXISTS created_by_membership_id uuid;
