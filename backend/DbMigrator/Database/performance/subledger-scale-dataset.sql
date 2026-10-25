-- Disposable performance database only. Creates one million closed items,
-- one hundred thousand open hot-party items, and five million applications.
SET row_security = off;

DO $dataset$
DECLARE
    tenant uuid := 'c9fea3c8-249d-f150-a53a-84f5a891c82b';
    entity uuid := 'aad8a34b-344c-9e5b-8d58-57f182fc6528';
    closed_count integer := 500000;
    application_passes integer := 10;
BEGIN
    IF NOT EXISTS (SELECT 1 FROM platform.legal_entity WHERE tenant_id = tenant AND id = entity) THEN
        RAISE EXCEPTION 'Expected seeded Atlas legal entity is missing';
    END IF;

    INSERT INTO finance_subledger.party_open_item (
        tenant_id, legal_entity_id, id, party_id, direction, polarity, source_type,
        source_reference, source_schedule_reference, source_display_number,
        document_date, posting_date, due_date, transaction_currency,
        original_transaction_amount, settled_transaction_amount, open_transaction_amount,
        base_currency, original_base_amount, settled_base_carrying_amount, open_base_carrying_amount,
        control_account_role, control_account_reference, control_configuration_reference,
        historic_fx_snapshot, source_version, source_payload_hash, status, is_open,
        row_version, created_at, closed_at
    )
    SELECT tenant, entity, md5('s3-closed-debit-' || n)::uuid,
           md5('s3-party-' || ((n - 1) / 1000))::uuid, 'Receivable', 'Debit', 'Invoice',
           'S3-INV-' || n, NULL, 'S3-' || n,
           date '2024-01-01' + ((n - 1) % 900), date '2024-01-01' + ((n - 1) % 900),
           date '2024-01-01' + ((n - 1) % 900) + 30,
           CASE ((n - 1) / 1000) % 3 WHEN 0 THEN 'EGP' WHEN 1 THEN 'USD' ELSE 'EUR' END,
           100, 100, 0, 'EGP',
           CASE ((n - 1) / 1000) % 3 WHEN 0 THEN 100 WHEN 1 THEN 5000 ELSE 5300 END,
           CASE ((n - 1) / 1000) % 3 WHEN 0 THEN 100 WHEN 1 THEN 5000 ELSE 5300 END,
           0, 'Receivable', 'role:Receivable', 'configuration:performance-v1',
           NULL, 1, repeat('a',64), 'Settled', false, 11, now(), now()
    FROM generate_series(1, closed_count) AS n;

    INSERT INTO finance_subledger.party_open_item (
        tenant_id, legal_entity_id, id, party_id, direction, polarity, source_type,
        source_reference, source_schedule_reference, source_display_number,
        document_date, posting_date, due_date, transaction_currency,
        original_transaction_amount, settled_transaction_amount, open_transaction_amount,
        base_currency, original_base_amount, settled_base_carrying_amount, open_base_carrying_amount,
        control_account_role, control_account_reference, control_configuration_reference,
        historic_fx_snapshot, source_version, source_payload_hash, status, is_open,
        row_version, created_at, closed_at
    )
    SELECT tenant, entity, md5('s3-closed-credit-' || n)::uuid,
           md5('s3-party-' || ((n - 1) / 1000))::uuid, 'Receivable', 'Credit', 'Payment',
           'S3-PAY-' || n, NULL, 'S3-P-' || n,
           date '2024-01-01' + ((n - 1) % 900), date '2024-01-01' + ((n - 1) % 900),
           date '2024-01-01' + ((n - 1) % 900) + 30,
           CASE ((n - 1) / 1000) % 3 WHEN 0 THEN 'EGP' WHEN 1 THEN 'USD' ELSE 'EUR' END,
           100, 100, 0, 'EGP',
           CASE ((n - 1) / 1000) % 3 WHEN 0 THEN 100 WHEN 1 THEN 5000 ELSE 5300 END,
           CASE ((n - 1) / 1000) % 3 WHEN 0 THEN 100 WHEN 1 THEN 5000 ELSE 5300 END,
           0, 'Receivable', 'role:Receivable', 'configuration:performance-v1',
           NULL, 1, repeat('b',64), 'Settled', false, 11, now(), now()
    FROM generate_series(1, closed_count) AS n;

    INSERT INTO finance_subledger.party_open_item (
        tenant_id, legal_entity_id, id, party_id, direction, polarity, source_type,
        source_reference, source_schedule_reference, source_display_number,
        document_date, posting_date, due_date, transaction_currency,
        original_transaction_amount, settled_transaction_amount, open_transaction_amount,
        base_currency, original_base_amount, settled_base_carrying_amount, open_base_carrying_amount,
        control_account_role, control_account_reference, control_configuration_reference,
        historic_fx_snapshot, source_version, source_payload_hash, status, is_open,
        row_version, created_at, closed_at
    )
    SELECT tenant, entity, md5('s3-hot-open-' || n)::uuid,
           md5('s3-hot-party')::uuid, 'Receivable', 'Debit', 'Invoice',
           'S3-HOT-' || n, NULL, 'HOT-' || n,
           date '2024-01-01' + ((n - 1) % 900), date '2024-01-01' + ((n - 1) % 900),
           date '2024-01-01' + ((n - 1) % 900) + 30,
           CASE (n - 1) % 3 WHEN 0 THEN 'EGP' WHEN 1 THEN 'USD' ELSE 'EUR' END,
           100, 0, 100, 'EGP',
           CASE (n - 1) % 3 WHEN 0 THEN 100 WHEN 1 THEN 5000 ELSE 5300 END,
           0,
           CASE (n - 1) % 3 WHEN 0 THEN 100 WHEN 1 THEN 5000 ELSE 5300 END,
           'Receivable', 'role:Receivable', 'configuration:performance-v1',
           NULL, 1, repeat('c',64), 'Open', true, 1, now(), NULL
    FROM generate_series(1, 100000) AS n;

    INSERT INTO finance_subledger.application_batch (
        tenant_id, legal_entity_id, id, party_id, direction, operation, idempotency_key,
        payload_hash, application_date, settlement_date, status, actor_id, correlation_id,
        created_at, confirmed_at
    )
    SELECT tenant, entity, md5('s3-batch-' || p || '-' || g)::uuid,
           md5('s3-party-' || (g / 10))::uuid, 'Receivable', 'Apply',
           's3-performance-' || p || '-' || g, repeat(md5('payload-' || p || '-' || g),2),
           date '2025-01-01' + p, date '2025-01-01' + p, 'Confirmed',
           md5('s3-actor-' || g)::uuid, md5('s3-correlation-' || p || '-' || g)::uuid,
           now(), now()
    FROM generate_series(0, application_passes - 1) AS p
    CROSS JOIN generate_series(0, 4999) AS g;

    INSERT INTO finance_subledger.open_item_application (
        tenant_id, legal_entity_id, id, batch_id, debit_item_id, credit_item_id,
        debit_applied_amount, credit_applied_amount, base_currency,
        debit_base_carrying_amount_closed, credit_base_carrying_amount_closed,
        application_date, settlement_date, sequence_no, created_at
    )
    SELECT tenant, entity, md5('s3-app-' || p || '-' || n)::uuid,
           md5('s3-batch-' || p || '-' || ((n - 1) / 100))::uuid,
           md5('s3-closed-debit-' || n)::uuid, md5('s3-closed-credit-' || n)::uuid,
           10, 10, 'EGP',
           CASE (n - 1) / 1000 % 3 WHEN 0 THEN 10 WHEN 1 THEN 500 WHEN 2 THEN 530 END,
           CASE (n - 1) / 1000 % 3 WHEN 0 THEN 10 WHEN 1 THEN 500 WHEN 2 THEN 530 END,
           date '2025-01-01' + p, date '2025-01-01' + p,
           ((n - 1) % 100) + 1, now()
    FROM generate_series(0, application_passes - 1) AS p
    CROSS JOIN generate_series(1, closed_count) AS n;
END
$dataset$;

ANALYZE finance_subledger.party_open_item;
ANALYZE finance_subledger.application_batch;
ANALYZE finance_subledger.open_item_application;
