SET search_path = finance_payments, public;
SET row_security = off;

CREATE SCHEMA IF NOT EXISTS finance_payments AUTHORIZATION erp_owner;
REVOKE ALL ON SCHEMA finance_payments FROM PUBLIC;
GRANT USAGE ON SCHEMA finance_payments TO erp_app;

CREATE TABLE finance_payments.payment_account (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    branch_id uuid,
    code text NOT NULL,
    name text NOT NULL,
    account_type text NOT NULL,
    currency_policy text NOT NULL DEFAULT 'SingleCurrency',
    currency_code char(3),
    status text NOT NULL DEFAULT 'Active',
    bank_name text,
    account_holder text,
    masked_identifier text,
    secret_ref text,
    provider_config_id uuid,
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, id),
    UNIQUE (tenant_id, legal_entity_id, code),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (account_type IN ('Bank','Cashbox','ProviderClearing','Wallet','InternalClearing')),
    CHECK (currency_policy IN ('SingleCurrency','MultiCurrency')),
    CHECK ((currency_policy = 'SingleCurrency') = (currency_code IS NOT NULL)),
    CHECK (currency_code IS NULL OR currency_code ~ '^[A-Z]{3}$'),
    CHECK (status IN ('Active','Suspended','Archived')),
    CHECK (length(btrim(code)) BETWEEN 1 AND 80 AND length(btrim(name)) BETWEEN 1 AND 200),
    CHECK (masked_identifier IS NULL OR length(masked_identifier) <= 100),
    CHECK (secret_ref IS NULL OR length(secret_ref) <= 500),
    CHECK (version > 0)
);

CREATE TABLE finance_payments.payment_method_config (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    payment_account_id uuid,
    method text NOT NULL,
    branch_id uuid,
    channel text NOT NULL DEFAULT 'BackOffice',
    status text NOT NULL DEFAULT 'Active',
    effective_from date NOT NULL,
    effective_to date,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, payment_account_id) REFERENCES finance_payments.payment_account(tenant_id, legal_entity_id, id),
    CHECK (method IN ('Cash','BankTransfer','Cheque','Card','PaymentGateway','Wallet','Other')),
    CHECK (status IN ('Active','Inactive')),
    CHECK (effective_to IS NULL OR effective_to >= effective_from),
    CHECK (length(btrim(channel)) BETWEEN 1 AND 50)
);

CREATE TABLE finance_payments.payment (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    payment_number text NOT NULL,
    branch_id uuid,
    direction text NOT NULL,
    payment_type text NOT NULL,
    party_id uuid,
    amount numeric(20,6) NOT NULL,
    currency_code char(3) NOT NULL,
    method text NOT NULL,
    payment_account_id uuid NOT NULL,
    payment_date date NOT NULL,
    posting_date date NOT NULL,
    value_date date,
    provider_event_time timestamptz,
    bank_reference text,
    normalized_reference text,
    customer_reference text,
    supplier_reference text,
    remittance_reference text,
    provider_code text,
    provider_transaction_id text,
    source_type text,
    source_reference text,
    business_state text NOT NULL DEFAULT 'Draft',
    completed_at timestamptz,
    completed_by uuid,
    subledger_item_id uuid,
    accounting_fact_id uuid,
    reconciliation_state text NOT NULL DEFAULT 'Unmatched',
    applied_amount_projection numeric(20,6) NOT NULL DEFAULT 0,
    row_version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, id),
    UNIQUE (tenant_id, legal_entity_id, payment_number),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, payment_account_id) REFERENCES finance_payments.payment_account(tenant_id, legal_entity_id, id),
    CHECK (direction IN ('Incoming','Outgoing','InternalTransfer')),
    CHECK (payment_type IN ('CustomerReceipt','SupplierPayment','CustomerAdvance','SupplierAdvance','CustomerRefund','SupplierRefund','ExpensePayment','Standalone','TransferLeg','ProviderFee')),
    CHECK (amount > 0 AND currency_code ~ '^[A-Z]{3}$'),
    CHECK (method IN ('Cash','BankTransfer','Cheque','Card','PaymentGateway','Wallet','Other')),
    CHECK (business_state IN ('Draft','Pending','Completed','Failed','Cancelled','Reversed')),
    CHECK (reconciliation_state IN ('Unmatched','Matched','Reconciled')),
    CHECK (applied_amount_projection >= 0 AND applied_amount_projection <= amount),
    CHECK (row_version > 0),
    CHECK ((business_state = 'Completed') = (completed_at IS NOT NULL))
);

CREATE TABLE finance_payments.payment_idempotency (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    scope text NOT NULL,
    idempotency_key text NOT NULL,
    request_hash char(64) NOT NULL,
    result_type text,
    result_id uuid,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, scope, idempotency_key),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    CHECK (length(btrim(scope)) BETWEEN 1 AND 100),
    CHECK (length(btrim(idempotency_key)) BETWEEN 1 AND 200),
    CHECK ((result_type IS NULL) = (result_id IS NULL))
);

CREATE TABLE finance_payments.payment_fee (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    payment_id uuid NOT NULL,
    fee_type text NOT NULL,
    amount numeric(20,6) NOT NULL,
    currency_code char(3) NOT NULL,
    recognition_state text NOT NULL DEFAULT 'Pending',
    source_reference text,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, payment_id) REFERENCES finance_payments.payment(tenant_id, legal_entity_id, id),
    CHECK (fee_type IN ('BankFee','ProviderFee','CardProcessingFee','FXFee','TransferFee','OtherApprovedFee')),
    CHECK (amount >= 0 AND currency_code ~ '^[A-Z]{3}$'),
    CHECK (recognition_state IN ('Pending','Recognized','Reversed'))
);

CREATE TABLE finance_payments.payment_relationship (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    payment_id uuid NOT NULL,
    related_payment_id uuid NOT NULL,
    relationship_type text NOT NULL,
    amount numeric(20,6) NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, payment_id, related_payment_id, relationship_type),
    FOREIGN KEY (tenant_id, legal_entity_id, payment_id) REFERENCES finance_payments.payment(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, related_payment_id) REFERENCES finance_payments.payment(tenant_id, legal_entity_id, id),
    CHECK (payment_id <> related_payment_id),
    CHECK (relationship_type IN ('RefundOf','ReversalOf','TransferSource','TransferDestination','CorrectionOf')),
    CHECK (amount > 0)
);

CREATE TABLE finance_payments.payment_refund (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    original_payment_id uuid NOT NULL,
    refund_payment_id uuid,
    amount numeric(20,6) NOT NULL,
    currency_code char(3) NOT NULL,
    reason_code text NOT NULL,
    provider_refund_id text,
    status text NOT NULL DEFAULT 'Pending',
    idempotency_key text NOT NULL,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    completed_at timestamptz,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, idempotency_key),
    FOREIGN KEY (tenant_id, legal_entity_id, original_payment_id) REFERENCES finance_payments.payment(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, refund_payment_id) REFERENCES finance_payments.payment(tenant_id, legal_entity_id, id),
    CHECK (amount > 0 AND currency_code ~ '^[A-Z]{3}$'),
    CHECK (status IN ('Pending','Completed','Failed','Cancelled')),
    CHECK (length(btrim(reason_code)) BETWEEN 1 AND 120),
    CHECK ((status = 'Completed') = (completed_at IS NOT NULL))
);

CREATE TABLE finance_payments.payment_reversal (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    original_payment_id uuid NOT NULL,
    reversal_payment_id uuid NOT NULL,
    reason_code text NOT NULL,
    status text NOT NULL DEFAULT 'Completed',
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, original_payment_id),
    UNIQUE (tenant_id, legal_entity_id, reversal_payment_id),
    FOREIGN KEY (tenant_id, legal_entity_id, original_payment_id) REFERENCES finance_payments.payment(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, reversal_payment_id) REFERENCES finance_payments.payment(tenant_id, legal_entity_id, id),
    CHECK (original_payment_id <> reversal_payment_id),
    CHECK (length(btrim(reason_code)) BETWEEN 1 AND 120),
    CHECK (status IN ('Completed','Cancelled'))
);

CREATE TABLE finance_payments.internal_transfer (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    source_payment_id uuid NOT NULL,
    destination_payment_id uuid NOT NULL,
    source_account_id uuid NOT NULL,
    destination_account_id uuid NOT NULL,
    source_amount numeric(20,6) NOT NULL,
    source_currency char(3) NOT NULL,
    destination_amount numeric(20,6) NOT NULL,
    destination_currency char(3) NOT NULL,
    fx_rate numeric(24,12),
    fx_snapshot jsonb,
    fee_amount numeric(20,6) NOT NULL DEFAULT 0,
    status text NOT NULL DEFAULT 'Completed',
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, source_payment_id),
    UNIQUE (tenant_id, legal_entity_id, destination_payment_id),
    FOREIGN KEY (tenant_id, legal_entity_id, source_payment_id) REFERENCES finance_payments.payment(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, destination_payment_id) REFERENCES finance_payments.payment(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, source_account_id) REFERENCES finance_payments.payment_account(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, destination_account_id) REFERENCES finance_payments.payment_account(tenant_id, legal_entity_id, id),
    CHECK (source_account_id <> destination_account_id),
    CHECK (source_payment_id <> destination_payment_id),
    CHECK (source_amount > 0 AND destination_amount > 0 AND fee_amount >= 0),
    CHECK (source_currency ~ '^[A-Z]{3}$' AND destination_currency ~ '^[A-Z]{3}$'),
    CHECK ((source_currency = destination_currency AND source_amount = destination_amount AND fx_rate IS NULL) OR (source_currency <> destination_currency AND fx_rate > 0 AND fx_snapshot IS NOT NULL)),
    CHECK (status IN ('Pending','Completed','Failed','Cancelled'))
);

CREATE TABLE finance_payments.provider_config (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    provider_code text NOT NULL,
    environment text NOT NULL,
    merchant_reference text NOT NULL,
    api_version text NOT NULL,
    capabilities jsonb NOT NULL DEFAULT '{}'::jsonb,
    supported_currencies char(3)[] NOT NULL DEFAULT '{}',
    supported_methods text[] NOT NULL DEFAULT '{}',
    payment_account_id uuid NOT NULL,
    secret_ref text,
    status text NOT NULL DEFAULT 'Active',
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, id),
    UNIQUE (tenant_id, legal_entity_id, provider_code, merchant_reference),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, payment_account_id) REFERENCES finance_payments.payment_account(tenant_id, legal_entity_id, id),
    CHECK (environment IN ('Sandbox','Production')),
    CHECK (status IN ('Active','Suspended','Archived')),
    CHECK (length(btrim(provider_code)) BETWEEN 1 AND 80 AND length(btrim(api_version)) BETWEEN 1 AND 80),
    CHECK (secret_ref IS NULL OR length(secret_ref) <= 500),
    CHECK (version > 0)
);

ALTER TABLE finance_payments.payment_account
    ADD CONSTRAINT fk_payment_account_provider_config FOREIGN KEY (tenant_id, legal_entity_id, provider_config_id) REFERENCES finance_payments.provider_config(tenant_id, legal_entity_id, id);

CREATE TABLE finance_payments.payment_request (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    branch_id uuid,
    direction text NOT NULL,
    party_id uuid,
    amount numeric(20,6) NOT NULL,
    currency_code char(3) NOT NULL,
    provider_config_id uuid,
    source_type text,
    source_reference text,
    merchant_reference text NOT NULL,
    workflow_state text NOT NULL DEFAULT 'Created',
    expires_at timestamptz,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, id),
    UNIQUE (tenant_id, legal_entity_id, merchant_reference),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, provider_config_id) REFERENCES finance_payments.provider_config(tenant_id, legal_entity_id, id),
    CHECK (direction IN ('Incoming','Outgoing')),
    CHECK (amount > 0 AND currency_code ~ '^[A-Z]{3}$'),
    CHECK (workflow_state IN ('Created','Pending','RequiresAction','Succeeded','Failed','Cancelled','Expired'))
);

CREATE TABLE finance_payments.payment_attempt (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    payment_request_id uuid NOT NULL,
    attempt_number integer NOT NULL,
    provider_config_id uuid NOT NULL,
    provider_operation_id text,
    provider_transaction_id text,
    provider_state text,
    state text NOT NULL DEFAULT 'Created',
    failure_code text,
    failure_message_safe text,
    idempotency_key text NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, payment_request_id, attempt_number),
    UNIQUE (tenant_id, legal_entity_id, provider_config_id, provider_transaction_id),
    FOREIGN KEY (tenant_id, legal_entity_id, payment_request_id) REFERENCES finance_payments.payment_request(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, provider_config_id) REFERENCES finance_payments.provider_config(tenant_id, legal_entity_id, id),
    CHECK (attempt_number > 0),
    CHECK (state IN ('Created','RequiresAction','Authorized','Succeeded','Failed','Cancelled','Unknown')),
    CHECK (length(btrim(idempotency_key)) BETWEEN 1 AND 200)
);

CREATE TABLE finance_payments.provider_operation (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    provider_config_id uuid NOT NULL,
    operation_type text NOT NULL,
    aggregate_type text NOT NULL,
    aggregate_id uuid NOT NULL,
    idempotency_key text NOT NULL,
    request_hash char(64) NOT NULL,
    state text NOT NULL DEFAULT 'Pending',
    attempts integer NOT NULL DEFAULT 0,
    next_attempt_at timestamptz,
    last_error_code text,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, provider_config_id, idempotency_key),
    FOREIGN KEY (tenant_id, legal_entity_id, provider_config_id) REFERENCES finance_payments.provider_config(tenant_id, legal_entity_id, id),
    CHECK (operation_type IN ('Create','Status','Capture','Cancel','Refund','VerifyWebhook')),
    CHECK (aggregate_type IN ('PaymentRequest','Payment','Refund','PaymentLink')),
    CHECK (state IN ('Pending','InProgress','Succeeded','Failed','Unknown','DeadLetter')),
    CHECK (attempts >= 0)
);

CREATE TABLE finance_payments.provider_webhook_inbox (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    provider_config_id uuid NOT NULL,
    provider_event_id text NOT NULL,
    object_reference text,
    provider_api_version text NOT NULL,
    event_type text NOT NULL,
    occurred_at timestamptz,
    received_at timestamptz NOT NULL,
    payload_sha256 char(64) NOT NULL,
    encrypted_payload bytea,
    signature_verified boolean NOT NULL,
    processing_state text NOT NULL DEFAULT 'Received',
    error_code text,
    processed_at timestamptz,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, provider_config_id, provider_event_id, object_reference),
    FOREIGN KEY (tenant_id, legal_entity_id, provider_config_id) REFERENCES finance_payments.provider_config(tenant_id, legal_entity_id, id),
    CHECK (length(btrim(provider_event_id)) BETWEEN 1 AND 240),
    CHECK (processing_state IN ('Received','Processing','Processed','Duplicate','Rejected','DeadLetter')),
    CHECK (NOT signature_verified OR processing_state <> 'Rejected')
);

CREATE TABLE finance_payments.payment_link (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    payment_request_id uuid NOT NULL,
    provider_config_id uuid NOT NULL,
    provider_link_reference text,
    public_token_hash char(64) NOT NULL,
    status text NOT NULL DEFAULT 'Active',
    expires_at timestamptz NOT NULL,
    created_at timestamptz NOT NULL,
    cancelled_at timestamptz,
    PRIMARY KEY (tenant_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, payment_request_id) REFERENCES finance_payments.payment_request(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, provider_config_id) REFERENCES finance_payments.provider_config(tenant_id, legal_entity_id, id),
    UNIQUE (tenant_id, legal_entity_id, provider_config_id, provider_link_reference),
    CHECK (status IN ('Active','Completed','Cancelled','Expired')),
    CHECK ((status = 'Cancelled') = (cancelled_at IS NOT NULL))
);

CREATE TABLE finance_payments.payment_attachment_ref (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    payment_id uuid NOT NULL,
    file_ref uuid NOT NULL,
    role text NOT NULL,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, payment_id, file_ref, role),
    FOREIGN KEY (tenant_id, legal_entity_id, payment_id) REFERENCES finance_payments.payment(tenant_id, legal_entity_id, id),
    CHECK (role IN ('Receipt','PaymentProof','RemittanceAdvice'))
);

CREATE TABLE finance_payments.payment_list_projection (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    payment_id uuid NOT NULL,
    payment_number text NOT NULL,
    party_display_name text,
    account_name text NOT NULL,
    amount numeric(20,6) NOT NULL,
    currency_code char(3) NOT NULL,
    direction text NOT NULL,
    method text NOT NULL,
    business_state text NOT NULL,
    applied_amount numeric(20,6) NOT NULL DEFAULT 0,
    reconciliation_state text NOT NULL DEFAULT 'Unmatched',
    normalized_search text NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, payment_id),
    FOREIGN KEY (tenant_id, legal_entity_id, payment_id) REFERENCES finance_payments.payment(tenant_id, legal_entity_id, id),
    CHECK (amount > 0 AND applied_amount >= 0 AND applied_amount <= amount)
);

CREATE INDEX ix_payment_list_scope_date ON finance_payments.payment(tenant_id, legal_entity_id, payment_date DESC, id DESC);
CREATE INDEX ix_payment_list_state ON finance_payments.payment(tenant_id, legal_entity_id, business_state, payment_date DESC, id DESC);
CREATE INDEX ix_payment_list_party ON finance_payments.payment(tenant_id, legal_entity_id, party_id, payment_date DESC, id DESC);
CREATE INDEX ix_payment_account_branch ON finance_payments.payment_account(tenant_id, legal_entity_id, branch_id, status, code);
CREATE INDEX ix_provider_recovery ON finance_payments.provider_operation(state, next_attempt_at) WHERE state IN ('Pending','Unknown','Failed');
CREATE INDEX ix_payment_webhook_processing ON finance_payments.provider_webhook_inbox(processing_state, received_at) WHERE processing_state IN ('Received','DeadLetter');
CREATE INDEX ix_payment_request_state ON finance_payments.payment_request(tenant_id, legal_entity_id, workflow_state, created_at DESC);

CREATE OR REPLACE FUNCTION finance_payments.guard_payment_history() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'payments.payment_history_immutable' USING ERRCODE = '55000';
    END IF;
    IF OLD.business_state = 'Completed'
       AND ROW(NEW.tenant_id, NEW.legal_entity_id, NEW.id, NEW.direction, NEW.payment_type, NEW.party_id,
               NEW.amount, NEW.currency_code, NEW.method, NEW.payment_account_id, NEW.payment_date,
               NEW.posting_date, NEW.value_date, NEW.provider_code, NEW.provider_transaction_id, NEW.completed_at)
           IS DISTINCT FROM
           ROW(OLD.tenant_id, OLD.legal_entity_id, OLD.id, OLD.direction, OLD.payment_type, OLD.party_id,
               OLD.amount, OLD.currency_code, OLD.method, OLD.payment_account_id, OLD.payment_date,
               OLD.posting_date, OLD.value_date, OLD.provider_code, OLD.provider_transaction_id, OLD.completed_at) THEN
        RAISE EXCEPTION 'payments.completed_payment_immutable' USING ERRCODE = '55000';
    END IF;
    IF OLD.business_state = 'Completed' AND NEW.business_state NOT IN ('Completed','Reversed') THEN
        RAISE EXCEPTION 'payments.completed_payment_immutable' USING ERRCODE = '55000';
    END IF;
    RETURN NEW;
END
$$;
CREATE TRIGGER tr_payments_payment_history
    BEFORE UPDATE OR DELETE ON finance_payments.payment
    FOR EACH ROW EXECUTE FUNCTION finance_payments.guard_payment_history();

DO $$
DECLARE item record;
BEGIN
    FOR item IN
        SELECT relation.relname AS table_name
        FROM pg_class relation
        JOIN pg_namespace namespace ON namespace.oid = relation.relnamespace
        WHERE namespace.nspname = 'finance_payments'
          AND relation.relkind = 'r'
          AND EXISTS (SELECT 1 FROM pg_attribute attribute WHERE attribute.attrelid = relation.oid AND attribute.attname = 'tenant_id' AND NOT attribute.attisdropped)
    LOOP
        EXECUTE format('ALTER TABLE finance_payments.%I ENABLE ROW LEVEL SECURITY', item.table_name);
        EXECUTE format('CREATE POLICY tenant_isolation ON finance_payments.%I USING (tenant_id = current_setting(''app.tenant_id'', true)::uuid) WITH CHECK (tenant_id = current_setting(''app.tenant_id'', true)::uuid)', item.table_name);
        EXECUTE format('GRANT SELECT, INSERT, UPDATE ON finance_payments.%I TO erp_app', item.table_name);
    END LOOP;
END
$$;

RESET row_security;
RESET search_path;
