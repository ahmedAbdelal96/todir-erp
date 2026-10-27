ALTER TABLE business_invoicing.invoice_relationship
    ADD COLUMN idempotency_key text,
    ADD COLUMN payload_hash char(64);

ALTER TABLE business_invoicing.invoice_relationship
    ADD CONSTRAINT ck_invoice_relationship_correction_idempotency CHECK (
        (idempotency_key IS NULL AND payload_hash IS NULL)
        OR (relationship_type IN ('Credits', 'Debits') AND idempotency_key IS NOT NULL AND payload_hash IS NOT NULL)
    ),
    ADD CONSTRAINT ck_invoice_relationship_correction_idempotency_values CHECK (
        (idempotency_key IS NULL OR length(btrim(idempotency_key)) BETWEEN 1 AND 200)
        AND (payload_hash IS NULL OR payload_hash ~ '^[0-9a-f]{64}$')
    );

CREATE UNIQUE INDEX ux_invoice_relationship_correction_idempotency
    ON business_invoicing.invoice_relationship (tenant_id, legal_entity_id, related_invoice_id, relationship_type, idempotency_key)
    WHERE idempotency_key IS NOT NULL;
