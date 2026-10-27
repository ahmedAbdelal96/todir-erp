ALTER TABLE finance_payments.payment DROP CONSTRAINT IF EXISTS payment_check2;
ALTER TABLE finance_payments.payment
    ADD CONSTRAINT payment_completed_timestamp_check
    CHECK ((business_state IN ('Completed','Reversed')) = (completed_at IS NOT NULL));
