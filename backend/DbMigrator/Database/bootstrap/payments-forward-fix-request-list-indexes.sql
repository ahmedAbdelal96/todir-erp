CREATE INDEX IF NOT EXISTS ix_payment_request_list
    ON finance_payments.payment_request (tenant_id, legal_entity_id, created_at DESC, id DESC);

CREATE INDEX IF NOT EXISTS ix_payment_request_merchant_search
    ON finance_payments.payment_request (tenant_id, legal_entity_id, lower(merchant_reference) text_pattern_ops);
