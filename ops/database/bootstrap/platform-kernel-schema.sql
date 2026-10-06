-- Platform Kernel persistence. Run as erp_owner through the controlled migrator.
-- Every tenant-owned table has a tenant key, forced RLS, and runtime DML only.

CREATE TABLE IF NOT EXISTS platform_iam.membership_invitation
(
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant (id),
    identity_id uuid NOT NULL REFERENCES platform_iam.identity (id),
    token_digest text NOT NULL UNIQUE,
    expires_at timestamptz NOT NULL,
    accepted_at timestamptz,
    created_at timestamptz NOT NULL
);

CREATE TABLE IF NOT EXISTS platform_authz.role
(
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant (id),
    code text NOT NULL,
    name text NOT NULL,
    is_system_template boolean NOT NULL DEFAULT false,
    is_active boolean NOT NULL DEFAULT true,
    version bigint NOT NULL DEFAULT 1,
    UNIQUE (tenant_id, id), UNIQUE (tenant_id, code)
);
CREATE TABLE IF NOT EXISTS platform_authz.role_permission
(
    tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant (id),
    role_id uuid NOT NULL,
    permission_code text NOT NULL,
    PRIMARY KEY (tenant_id, role_id, permission_code),
    FOREIGN KEY (tenant_id, role_id) REFERENCES platform_authz.role (tenant_id, id)
);
CREATE TABLE IF NOT EXISTS platform_authz.membership_role_assignment
(
    tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant (id),
    membership_id uuid NOT NULL,
    role_id uuid NOT NULL,
    scope_type text NOT NULL CHECK (scope_type IN ('Tenant', 'LegalEntity', 'Branch')),
    scope_id uuid,
    PRIMARY KEY (tenant_id, membership_id, role_id, scope_type, scope_id),
    FOREIGN KEY (tenant_id, membership_id) REFERENCES platform_iam.membership (tenant_id, id),
    FOREIGN KEY (tenant_id, role_id) REFERENCES platform_authz.role (tenant_id, id)
);
CREATE INDEX IF NOT EXISTS ix_role_permission_lookup ON platform_authz.role_permission (tenant_id, permission_code);
CREATE INDEX IF NOT EXISTS ix_assignment_lookup ON platform_authz.membership_role_assignment (tenant_id, membership_id, scope_type, scope_id);

CREATE TABLE IF NOT EXISTS platform_modules.module_instance
(
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant (id),
    module_id text NOT NULL,
    scope_type text NOT NULL CHECK (scope_type IN ('tenant', 'legal_entity')),
    scope_id uuid,
    status text NOT NULL CHECK (status IN ('Requested', 'Validating', 'Provisioning', 'AwaitingGoLive', 'Active', 'WindDown', 'ReadOnly', 'Disabled', 'Failed')),
    mode text,
    manifest_version text NOT NULL,
    go_live_at timestamptz,
    activated_at timestamptz,
    read_only_at timestamptz,
    disabled_at timestamptz,
    version bigint NOT NULL DEFAULT 1,
    UNIQUE (tenant_id, id),
    UNIQUE (tenant_id, module_id, scope_type, scope_id)
);
CREATE TABLE IF NOT EXISTS platform_modules.module_operation
(
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant (id),
    module_instance_id uuid NOT NULL,
    operation_type text NOT NULL,
    status text NOT NULL,
    requested_by uuid NOT NULL,
    idempotency_key text NOT NULL,
    started_at timestamptz,
    completed_at timestamptz,
    error_code text,
    error_details_ref text,
    UNIQUE (tenant_id, idempotency_key),
    FOREIGN KEY (tenant_id, module_instance_id) REFERENCES platform_modules.module_instance (tenant_id, id)
);
CREATE TABLE IF NOT EXISTS platform_entitlements.entitlement_snapshot
(
    tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant (id),
    snapshot_version bigint NOT NULL,
    issued_at timestamptz NOT NULL,
    valid_until timestamptz NOT NULL,
    key_id text NOT NULL,
    modules jsonb NOT NULL,
    features jsonb NOT NULL,
    limits jsonb NOT NULL,
    scopes jsonb NOT NULL,
    signature text NOT NULL,
    PRIMARY KEY (tenant_id, snapshot_version)
);

CREATE TABLE IF NOT EXISTS platform_messaging.outbox_message
(
    id uuid PRIMARY KEY,
    message_type text NOT NULL,
    payload jsonb NOT NULL,
    tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant (id),
    occurred_at timestamptz NOT NULL,
    correlation_id uuid NOT NULL,
    causation_id uuid NOT NULL,
    source_module text NOT NULL,
    trace_parent text,
    status text NOT NULL CHECK (status IN ('Pending', 'Published', 'Failed')),
    attempts integer NOT NULL DEFAULT 0,
    next_attempt_at timestamptz,
    published_at timestamptz,
    last_error text
);
ALTER TABLE platform_messaging.outbox_message ADD COLUMN IF NOT EXISTS trace_parent text;
CREATE INDEX IF NOT EXISTS ix_outbox_pending ON platform_messaging.outbox_message (status, next_attempt_at, occurred_at) WHERE status = 'Pending';
CREATE OR REPLACE FUNCTION platform_messaging.claim_pending_outbox(limit_count integer)
RETURNS TABLE
(
    id uuid,
    message_type text,
    payload text,
    tenant_id uuid,
    occurred_at timestamptz,
    correlation_id uuid,
    causation_id uuid,
    source_module text,
    trace_parent text,
    attempts integer
)
LANGUAGE sql
SECURITY DEFINER
SET search_path = platform_messaging, pg_catalog
AS $$
    UPDATE platform_messaging.outbox_message AS message
    SET attempts = message.attempts + 1
    WHERE message.id IN
    (
        SELECT pending.id
        FROM platform_messaging.outbox_message AS pending
        WHERE pending.status = 'Pending'
          AND (pending.next_attempt_at IS NULL OR pending.next_attempt_at <= now())
        ORDER BY pending.occurred_at
        FOR UPDATE SKIP LOCKED
        LIMIT LEAST(GREATEST(limit_count, 1), 1000)
    )
    RETURNING message.id, message.message_type, message.payload::text, message.tenant_id,
        message.occurred_at, message.correlation_id, message.causation_id,
        message.source_module, message.trace_parent, message.attempts;
$$;
GRANT EXECUTE ON FUNCTION platform_messaging.claim_pending_outbox(integer) TO erp_app;
CREATE TABLE IF NOT EXISTS platform_messaging.inbox_message
(
    consumer_endpoint text NOT NULL,
    message_id uuid NOT NULL,
    tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant (id),
    received_at timestamptz NOT NULL,
    processed_at timestamptz,
    PRIMARY KEY (consumer_endpoint, message_id)
);
CREATE TABLE IF NOT EXISTS platform_messaging.message_error
(
    id uuid PRIMARY KEY,
    message_id uuid NOT NULL,
    tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant (id),
    consumer_endpoint text NOT NULL,
    attempt integer NOT NULL CHECK (attempt > 0),
    error_code text NOT NULL,
    detail text NOT NULL,
    occurred_at timestamptz NOT NULL,
    resolved boolean NOT NULL DEFAULT false
);
CREATE INDEX IF NOT EXISTS ix_message_error_unresolved
    ON platform_messaging.message_error (tenant_id, occurred_at DESC)
    WHERE resolved = false;

CREATE TABLE IF NOT EXISTS platform_audit.audit_entry
(
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant (id),
    legal_entity_id uuid,
    branch_id uuid,
    actor_type text NOT NULL,
    actor_id uuid,
    membership_id uuid,
    module text NOT NULL,
    action text NOT NULL,
    entity_type text NOT NULL,
    entity_id uuid NOT NULL,
    before_json jsonb,
    after_json jsonb,
    changed_fields jsonb,
    reason text,
    source text NOT NULL,
    correlation_id uuid NOT NULL,
    trace_id text,
    occurred_at timestamptz NOT NULL,
    metadata_json jsonb
);
CREATE INDEX IF NOT EXISTS ix_audit_tenant_time ON platform_audit.audit_entry (tenant_id, occurred_at DESC);

CREATE TABLE IF NOT EXISTS platform_numbering.sequence_definition
(
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant (id),
    sequence_key text NOT NULL,
    legal_entity_id uuid,
    branch_id uuid,
    scope_key text,
    period_key text,
    prefix_template text NOT NULL,
    padding integer NOT NULL CHECK (padding BETWEEN 1 AND 20),
    mode text NOT NULL CHECK (mode IN ('Standard', 'GapControlled', 'RegulatoryStrategy')),
    next_value bigint NOT NULL CHECK (next_value > 0),
    version bigint NOT NULL DEFAULT 1,
    UNIQUE (tenant_id, sequence_key, legal_entity_id, branch_id, scope_key, period_key)
);

CREATE TABLE IF NOT EXISTS platform_settings.setting_definition
(
    id uuid PRIMARY KEY,
    setting_key text NOT NULL UNIQUE,
    data_type text NOT NULL CHECK (data_type IN ('string', 'boolean', 'integer', 'number', 'json')),
    scope text NOT NULL CHECK (scope IN ('Tenant', 'LegalEntity', 'Branch', 'User')),
    default_value jsonb NOT NULL,
    sensitive boolean NOT NULL DEFAULT false,
    owner_module text NOT NULL,
    version bigint NOT NULL DEFAULT 1
);
CREATE TABLE IF NOT EXISTS platform_settings.setting_value
(
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant (id),
    setting_definition_id uuid NOT NULL REFERENCES platform_settings.setting_definition (id),
    scope text NOT NULL,
    scope_id uuid NOT NULL,
    value jsonb NOT NULL,
    version bigint NOT NULL DEFAULT 1,
    UNIQUE (tenant_id, setting_definition_id, scope, scope_id)
);

CREATE TABLE IF NOT EXISTS platform_files.file_record
(
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant (id),
    owner_module text NOT NULL,
    owner_entity_type text NOT NULL,
    owner_entity_id uuid NOT NULL,
    object_key text NOT NULL UNIQUE,
    original_name text NOT NULL,
    content_type text NOT NULL,
    size bigint NOT NULL CHECK (size >= 0),
    sha256 text NOT NULL,
    status text NOT NULL CHECK (status IN ('Requested', 'Uploading', 'Quarantined', 'Scanned', 'Ready', 'Rejected')),
    classification text NOT NULL,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL
);
CREATE TABLE IF NOT EXISTS platform_custom_fields.custom_field_definition
(
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant (id),
    target_entity_type text NOT NULL,
    stable_key text NOT NULL,
    label_ar text NOT NULL,
    label_en text NOT NULL,
    field_type text NOT NULL,
    required_ui boolean NOT NULL DEFAULT false,
    default_value jsonb,
    validation jsonb,
    visibility text,
    status text NOT NULL,
    version bigint NOT NULL DEFAULT 1,
    UNIQUE (tenant_id, target_entity_type, stable_key)
);
CREATE TABLE IF NOT EXISTS platform_import.import_batch
(
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant (id),
    module_id text NOT NULL,
    importer_id text NOT NULL,
    status text NOT NULL,
    file_id uuid NOT NULL,
    mapping_json jsonb NOT NULL,
    progress numeric(5, 2) NOT NULL CHECK (progress BETWEEN 0 AND 100),
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    UNIQUE (tenant_id, id)
);
CREATE TABLE IF NOT EXISTS platform_import.import_error
(
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant (id),
    batch_id uuid NOT NULL,
    row_number integer NOT NULL,
    column_key text,
    code text NOT NULL,
    message_params jsonb NOT NULL,
    severity text NOT NULL,
    FOREIGN KEY (tenant_id, batch_id) REFERENCES platform_import.import_batch (tenant_id, id)
);
CREATE TABLE IF NOT EXISTS platform_notifications.notification
(
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant (id),
    recipient_ref text NOT NULL,
    template_key text NOT NULL,
    channel text NOT NULL,
    payload jsonb NOT NULL,
    status text NOT NULL,
    attempts integer NOT NULL DEFAULT 0,
    created_at timestamptz NOT NULL,
    sent_at timestamptz,
    UNIQUE (tenant_id, id)
);
CREATE TABLE IF NOT EXISTS platform_notifications.notification_delivery
(
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant (id),
    notification_id uuid NOT NULL,
    channel text NOT NULL,
    status text NOT NULL,
    attempt_at timestamptz NOT NULL,
    error_code text,
    FOREIGN KEY (tenant_id, notification_id) REFERENCES platform_notifications.notification (tenant_id, id)
);
CREATE TABLE IF NOT EXISTS platform_scheduling.job_schedule
(
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant (id),
    job_key text NOT NULL,
    cron_expression text NOT NULL,
    payload jsonb NOT NULL,
    status text NOT NULL CHECK (status IN ('Active', 'Paused', 'Completed', 'Failed')),
    next_run_at timestamptz,
    last_run_at timestamptz,
    version bigint NOT NULL DEFAULT 1,
    UNIQUE (tenant_id, job_key)
);
CREATE TABLE IF NOT EXISTS platform_iam.support_access_grant
(
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant (id),
    support_actor_id uuid NOT NULL,
    reason text NOT NULL,
    approved_by uuid,
    expires_at timestamptz NOT NULL,
    status text NOT NULL CHECK (status IN ('Requested', 'Approved', 'Active', 'Expired', 'Revoked')),
    created_at timestamptz NOT NULL
);

DO $$ DECLARE item record; BEGIN
  FOR item IN SELECT schemaname, tablename FROM (VALUES
    ('platform_iam','membership_invitation'), ('platform_authz','role'), ('platform_authz','role_permission'),
    ('platform_authz','membership_role_assignment'), ('platform_modules','module_instance'), ('platform_modules','module_operation'),
    ('platform_entitlements','entitlement_snapshot'), ('platform_messaging','outbox_message'), ('platform_messaging','inbox_message'), ('platform_messaging','message_error'),
    ('platform_audit','audit_entry'), ('platform_numbering','sequence_definition'), ('platform_settings','setting_value'),
    ('platform_files','file_record'), ('platform_custom_fields','custom_field_definition'), ('platform_import','import_batch'),
    ('platform_import','import_error'), ('platform_notifications','notification'), ('platform_notifications','notification_delivery'),
    ('platform_scheduling','job_schedule'), ('platform_iam','support_access_grant')
  ) AS t(schemaname, tablename) LOOP
    EXECUTE format('ALTER TABLE %I.%I ENABLE ROW LEVEL SECURITY', item.schemaname, item.tablename);
    EXECUTE format('ALTER TABLE %I.%I FORCE ROW LEVEL SECURITY', item.schemaname, item.tablename);
    EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON %I.%I', item.schemaname, item.tablename);
    EXECUTE format('CREATE POLICY tenant_isolation ON %I.%I USING (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid) WITH CHECK (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid)', item.schemaname, item.tablename);
  END LOOP;
END $$;

REVOKE UPDATE, DELETE ON platform_audit.audit_entry FROM erp_app;
