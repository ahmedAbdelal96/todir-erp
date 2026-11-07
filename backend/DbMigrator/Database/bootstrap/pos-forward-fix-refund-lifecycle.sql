ALTER TABLE business_pos.pos_return
    ADD COLUMN IF NOT EXISTS refund_payment_id uuid NULL,
    ADD COLUMN IF NOT EXISTS refunded_amount numeric(19,4) NOT NULL DEFAULT 0;
