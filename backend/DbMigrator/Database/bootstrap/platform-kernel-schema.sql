--
-- PostgreSQL database dump
--

-- Dumped from database version 17.0
-- Dumped by pg_dump version 17.0

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Name: platform; Type: SCHEMA; Schema: -; Owner: -
--

CREATE SCHEMA IF NOT EXISTS platform;


--
-- Name: claim_pending_outbox(integer); Type: FUNCTION; Schema: platform; Owner: -
--

CREATE FUNCTION platform.claim_pending_outbox(limit_count integer) RETURNS TABLE(id uuid, message_type text, payload text, tenant_id uuid, occurred_at timestamp with time zone, correlation_id uuid, causation_id uuid, source_module text, trace_parent text, attempts integer)
    LANGUAGE sql SECURITY DEFINER
    SET search_path TO 'platform', 'pg_catalog'
    AS $$
    UPDATE platform.outbox_message AS message
    SET attempts = message.attempts + 1
    WHERE message.id IN
    (
        SELECT pending.id
        FROM platform.outbox_message AS pending
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


--
-- Name: expire_support_access_grants(); Type: FUNCTION; Schema: platform; Owner: -
--

CREATE FUNCTION platform.expire_support_access_grants() RETURNS TABLE(id uuid, tenant_id uuid, support_actor_id uuid, reason text, approved_by uuid, expires_at timestamp with time zone, status text, created_at timestamp with time zone)
    LANGUAGE sql SECURITY DEFINER
    SET search_path TO 'platform', 'pg_catalog'
    AS $$
    UPDATE platform.support_access_grant
    SET status = 'Expired'
    WHERE expires_at <= now()
      AND status IN ('Requested', 'Approved', 'Active')
    RETURNING id, tenant_id, support_actor_id, reason, approved_by, expires_at, status, created_at;
$$;


--
-- Name: list_active_memberships(uuid); Type: FUNCTION; Schema: platform; Owner: -
--

CREATE FUNCTION platform.list_active_memberships(p_identity_id uuid) RETURNS TABLE(id uuid, tenant_id uuid, identity_id uuid, display_name text, status text, default_legal_entity_id uuid, default_branch_id uuid, locale text, timezone text, created_at timestamp with time zone, version bigint)
    LANGUAGE sql SECURITY DEFINER
    SET search_path TO 'platform', 'pg_catalog'
    AS $$
    SELECT m.id, m.tenant_id, m.identity_id, m.display_name, m.status,
           m.default_legal_entity_id, m.default_branch_id, m.locale,
           m.timezone, m.created_at, m.version
      FROM platform.membership m
     WHERE m.identity_id = p_identity_id
       AND m.status = 'Active'
$$;


--
-- Name: observe_pending_outbox(); Type: FUNCTION; Schema: platform; Owner: -
--

CREATE FUNCTION platform.observe_pending_outbox() RETURNS TABLE(pending_count bigint, oldest_age_seconds double precision)
    LANGUAGE sql SECURITY DEFINER
    SET search_path TO 'platform', 'pg_catalog'
    AS $$
    SELECT count(*)::bigint,
        coalesce(extract(epoch from now() - min(occurred_at)), 0)::double precision
    FROM platform.outbox_message
    WHERE status = 'Pending';
$$;


--
-- Name: resolve_identity_tenant(uuid); Type: FUNCTION; Schema: platform; Owner: -
--

CREATE FUNCTION platform.resolve_identity_tenant(p_identity_id uuid) RETURNS TABLE(tenant_id uuid, status text)
    LANGUAGE sql SECURITY DEFINER
    SET search_path TO 'platform', 'pg_catalog'
    AS $$
    SELECT i.tenant_id, t.status
      FROM platform.identity i
      JOIN platform.tenant t ON t.id = i.tenant_id
     WHERE i.id = p_identity_id
     LIMIT 1
$$;


--
-- Name: resolve_login_domain(text); Type: FUNCTION; Schema: platform; Owner: -
--

CREATE FUNCTION platform.resolve_login_domain(p_domain text) RETURNS TABLE(tenant_id uuid, status text, login_domain text)
    LANGUAGE sql SECURITY DEFINER
    SET search_path TO 'platform', 'pg_catalog'
    AS $$
    SELECT t.id, t.status, t.login_domain
      FROM platform.tenant t
     WHERE t.normalized_login_domain = lower(trim(p_domain))
     LIMIT 1
$$;


SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- Name: audit_entry; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.audit_entry (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
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
    occurred_at timestamp with time zone NOT NULL,
    metadata_json jsonb
);



--
-- Name: auth_challenge; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.auth_challenge (
    id uuid NOT NULL,
    token_digest text NOT NULL,
    identity_id uuid NOT NULL,
    next_step text NOT NULL,
    attempts integer DEFAULT 0 NOT NULL,
    expires_at timestamp with time zone NOT NULL,
    consumed_at timestamp with time zone,
    created_at timestamp with time zone NOT NULL,
    mfa_verified boolean DEFAULT false NOT NULL,
    CONSTRAINT auth_challenge_attempts_check CHECK ((attempts >= 0)),
    CONSTRAINT ck_auth_challenge_next_step CHECK ((next_step = ANY (ARRAY['VerifyMfa'::text, 'ChangePassword'::text])))
);


--
-- Name: branch; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.branch (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    code text NOT NULL,
    name text NOT NULL,
    timezone_override text,
    status text NOT NULL,
    created_at timestamp with time zone NOT NULL,
    version bigint NOT NULL
);



--
-- Name: custom_field_definition; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.custom_field_definition (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    target_entity_type text NOT NULL,
    stable_key text NOT NULL,
    label_ar text NOT NULL,
    label_en text NOT NULL,
    field_type text NOT NULL,
    required_ui boolean DEFAULT false NOT NULL,
    default_value jsonb,
    validation jsonb,
    visibility text,
    status text NOT NULL,
    version bigint DEFAULT 1 NOT NULL
);



--
-- Name: entitlement_snapshot; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.entitlement_snapshot (
    tenant_id uuid NOT NULL,
    snapshot_version bigint NOT NULL,
    issued_at timestamp with time zone NOT NULL,
    valid_until timestamp with time zone NOT NULL,
    key_id text NOT NULL,
    modules jsonb NOT NULL,
    features jsonb NOT NULL,
    limits jsonb NOT NULL,
    scopes jsonb NOT NULL,
    signature text NOT NULL
);



--
-- Name: file_record; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.file_record (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    owner_module text NOT NULL,
    owner_entity_type text NOT NULL,
    owner_entity_id uuid NOT NULL,
    object_key text NOT NULL,
    original_name text NOT NULL,
    content_type text NOT NULL,
    size bigint NOT NULL,
    sha256 text NOT NULL,
    status text NOT NULL,
    classification text NOT NULL,
    created_by uuid NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT file_record_size_check CHECK ((size >= 0)),
    CONSTRAINT file_record_status_check CHECK ((status = ANY (ARRAY['Requested'::text, 'Uploading'::text, 'Quarantined'::text, 'Scanned'::text, 'Ready'::text, 'Rejected'::text])))
);



--
-- Name: identity; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.identity (
    id uuid NOT NULL,
    normalized_email text NOT NULL,
    email_verified boolean DEFAULT false NOT NULL,
    password_hash text,
    phone text,
    status text NOT NULL,
    created_at timestamp with time zone NOT NULL,
    last_login_at timestamp with time zone,
    security_stamp uuid NOT NULL,
    version bigint NOT NULL,
    mfa_secret_protected text,
    mfa_enabled boolean DEFAULT false NOT NULL,
    failed_login_count integer DEFAULT 0 NOT NULL,
    locked_until timestamp with time zone,
    tenant_id uuid NOT NULL,
    email text NOT NULL,
    name text NOT NULL,
    login_prefix text NOT NULL,
    must_change_password boolean DEFAULT false NOT NULL,
    CONSTRAINT ck_identity_email_domain CHECK ((POSITION(('@'::text) IN (email)) > 1)),
    CONSTRAINT identity_status_check CHECK ((status = ANY (ARRAY['Active'::text, 'Suspended'::text, 'Revoked'::text])))
);



--
-- Name: import_batch; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.import_batch (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    module_id text NOT NULL,
    importer_id text NOT NULL,
    status text NOT NULL,
    file_id uuid NOT NULL,
    mapping_json jsonb NOT NULL,
    progress numeric(5,2) NOT NULL,
    created_by uuid NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT import_batch_progress_check CHECK (((progress >= (0)::numeric) AND (progress <= (100)::numeric)))
);



--
-- Name: import_chunk_job; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.import_chunk_job (
    tenant_id uuid NOT NULL,
    batch_id uuid NOT NULL,
    start_row integer NOT NULL,
    row_count integer NOT NULL,
    scheduled_at timestamp with time zone NOT NULL,
    CONSTRAINT import_chunk_job_row_count_check CHECK ((row_count > 0)),
    CONSTRAINT import_chunk_job_start_row_check CHECK ((start_row >= 0))
);



--
-- Name: import_error; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.import_error (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    batch_id uuid NOT NULL,
    row_number integer NOT NULL,
    column_key text,
    code text NOT NULL,
    message_params jsonb NOT NULL,
    severity text NOT NULL
);



--
-- Name: inbox_message; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.inbox_message (
    consumer_endpoint text NOT NULL,
    message_id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    received_at timestamp with time zone NOT NULL,
    lease_until timestamp with time zone,
    attempts integer DEFAULT 0 NOT NULL,
    processed_at timestamp with time zone,
    CONSTRAINT inbox_message_attempts_check CHECK ((attempts >= 0))
);



--
-- Name: job_schedule; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.job_schedule (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    job_key text NOT NULL,
    cron_expression text NOT NULL,
    payload jsonb NOT NULL,
    status text NOT NULL,
    next_run_at timestamp with time zone,
    last_run_at timestamp with time zone,
    version bigint DEFAULT 1 NOT NULL,
    CONSTRAINT job_schedule_status_check CHECK ((status = ANY (ARRAY['Active'::text, 'Paused'::text, 'Completed'::text, 'Failed'::text])))
);



--
-- Name: legal_entity; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.legal_entity (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    code text NOT NULL,
    legal_name text NOT NULL,
    trade_name text,
    country_code text NOT NULL,
    base_currency_code text NOT NULL,
    timezone text NOT NULL,
    locale text NOT NULL,
    status text NOT NULL,
    created_at timestamp with time zone NOT NULL,
    version bigint NOT NULL
);



--
-- Name: membership; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.membership (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    identity_id uuid NOT NULL,
    display_name text NOT NULL,
    status text NOT NULL,
    default_legal_entity_id uuid,
    default_branch_id uuid,
    locale text,
    timezone text,
    created_at timestamp with time zone NOT NULL,
    version bigint NOT NULL,
    CONSTRAINT membership_status_check CHECK ((status = ANY (ARRAY['Invited'::text, 'Active'::text, 'Suspended'::text, 'Revoked'::text])))
);



--
-- Name: membership_role_assignment; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.membership_role_assignment (
    tenant_id uuid NOT NULL,
    membership_id uuid NOT NULL,
    role_id uuid NOT NULL,
    scope_type text NOT NULL,
    scope_id uuid,
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    CONSTRAINT membership_role_assignment_scope_type_check CHECK ((scope_type = ANY (ARRAY['Tenant'::text, 'LegalEntity'::text, 'Branch'::text])))
);



--
-- Name: message_error; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.message_error (
    id uuid NOT NULL,
    message_id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    consumer_endpoint text NOT NULL,
    attempt integer NOT NULL,
    error_code text NOT NULL,
    detail text NOT NULL,
    occurred_at timestamp with time zone NOT NULL,
    resolved boolean DEFAULT false NOT NULL,
    CONSTRAINT message_error_attempt_check CHECK ((attempt > 0))
);



--
-- Name: mfa_recovery_code; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.mfa_recovery_code (
    identity_id uuid NOT NULL,
    digest text NOT NULL,
    created_at timestamp with time zone NOT NULL,
    consumed_at timestamp with time zone
);


--
-- Name: module_instance; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.module_instance (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    module_id text NOT NULL,
    scope_type text NOT NULL,
    scope_id uuid,
    status text NOT NULL,
    mode text,
    manifest_version text NOT NULL,
    go_live_at timestamp with time zone,
    activated_at timestamp with time zone,
    read_only_at timestamp with time zone,
    disabled_at timestamp with time zone,
    version bigint DEFAULT 1 NOT NULL,
    CONSTRAINT module_instance_scope_type_check CHECK ((scope_type = ANY (ARRAY['tenant'::text, 'legal_entity'::text]))),
    CONSTRAINT module_instance_status_check CHECK ((status = ANY (ARRAY['Requested'::text, 'Validating'::text, 'Provisioning'::text, 'AwaitingGoLive'::text, 'Active'::text, 'WindDown'::text, 'ReadOnly'::text, 'Disabled'::text, 'Failed'::text])))
);



--
-- Name: module_operation; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.module_operation (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    module_instance_id uuid NOT NULL,
    operation_type text NOT NULL,
    status text NOT NULL,
    requested_by uuid NOT NULL,
    idempotency_key text NOT NULL,
    started_at timestamp with time zone,
    completed_at timestamp with time zone,
    error_code text,
    error_details_ref text
);



--
-- Name: notification; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.notification (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    recipient_ref text NOT NULL,
    template_key text NOT NULL,
    channel text NOT NULL,
    payload jsonb NOT NULL,
    status text NOT NULL,
    attempts integer DEFAULT 0 NOT NULL,
    created_at timestamp with time zone NOT NULL,
    sent_at timestamp with time zone
);



--
-- Name: notification_delivery; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.notification_delivery (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    notification_id uuid NOT NULL,
    channel text NOT NULL,
    status text NOT NULL,
    attempt_at timestamp with time zone NOT NULL,
    error_code text
);



--
-- Name: outbox_message; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.outbox_message (
    id uuid NOT NULL,
    message_type text NOT NULL,
    payload jsonb NOT NULL,
    tenant_id uuid NOT NULL,
    occurred_at timestamp with time zone NOT NULL,
    correlation_id uuid NOT NULL,
    causation_id uuid NOT NULL,
    source_module text NOT NULL,
    trace_parent text,
    status text NOT NULL,
    attempts integer DEFAULT 0 NOT NULL,
    next_attempt_at timestamp with time zone,
    published_at timestamp with time zone,
    last_error text,
    CONSTRAINT outbox_message_status_check CHECK ((status = ANY (ARRAY['Pending'::text, 'Published'::text, 'Failed'::text])))
);



--
-- Name: qrtz_blob_triggers; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.qrtz_blob_triggers (
    sched_name text NOT NULL,
    trigger_name text NOT NULL,
    trigger_group text NOT NULL,
    blob_data bytea
);


--
-- Name: qrtz_calendars; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.qrtz_calendars (
    sched_name text NOT NULL,
    calendar_name text NOT NULL,
    calendar bytea NOT NULL
);


--
-- Name: qrtz_cron_triggers; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.qrtz_cron_triggers (
    sched_name text NOT NULL,
    trigger_name text NOT NULL,
    trigger_group text NOT NULL,
    cron_expression text NOT NULL,
    time_zone_id text
);


--
-- Name: qrtz_fired_triggers; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.qrtz_fired_triggers (
    sched_name text NOT NULL,
    entry_id text NOT NULL,
    trigger_name text NOT NULL,
    trigger_group text NOT NULL,
    instance_name text NOT NULL,
    fired_time bigint NOT NULL,
    sched_time bigint NOT NULL,
    priority integer NOT NULL,
    state text NOT NULL,
    job_name text,
    job_group text,
    is_nonconcurrent boolean NOT NULL,
    requests_recovery boolean
);


--
-- Name: qrtz_job_details; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.qrtz_job_details (
    sched_name text NOT NULL,
    job_name text NOT NULL,
    job_group text NOT NULL,
    description text,
    job_class_name text NOT NULL,
    is_durable boolean NOT NULL,
    is_nonconcurrent boolean NOT NULL,
    is_update_data boolean NOT NULL,
    requests_recovery boolean NOT NULL,
    job_data bytea
);


--
-- Name: qrtz_locks; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.qrtz_locks (
    sched_name text NOT NULL,
    lock_name text NOT NULL
);


--
-- Name: qrtz_paused_trigger_grps; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.qrtz_paused_trigger_grps (
    sched_name text NOT NULL,
    trigger_group text NOT NULL
);


--
-- Name: qrtz_scheduler_state; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.qrtz_scheduler_state (
    sched_name text NOT NULL,
    instance_name text NOT NULL,
    last_checkin_time bigint NOT NULL,
    checkin_interval bigint NOT NULL
);


--
-- Name: qrtz_simple_triggers; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.qrtz_simple_triggers (
    sched_name text NOT NULL,
    trigger_name text NOT NULL,
    trigger_group text NOT NULL,
    repeat_count bigint NOT NULL,
    repeat_interval bigint NOT NULL,
    times_triggered bigint NOT NULL
);


--
-- Name: qrtz_simprop_triggers; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.qrtz_simprop_triggers (
    sched_name text NOT NULL,
    trigger_name text NOT NULL,
    trigger_group text NOT NULL,
    str_prop_1 text,
    str_prop_2 text,
    str_prop_3 text,
    int_prop_1 integer,
    int_prop_2 integer,
    long_prop_1 bigint,
    long_prop_2 bigint,
    dec_prop_1 numeric,
    dec_prop_2 numeric,
    bool_prop_1 boolean,
    bool_prop_2 boolean,
    time_zone_id text
);


--
-- Name: qrtz_triggers; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.qrtz_triggers (
    sched_name text NOT NULL,
    trigger_name text NOT NULL,
    trigger_group text NOT NULL,
    job_name text NOT NULL,
    job_group text NOT NULL,
    description text,
    next_fire_time bigint,
    prev_fire_time bigint,
    priority integer,
    trigger_state text NOT NULL,
    trigger_type text NOT NULL,
    start_time bigint NOT NULL,
    end_time bigint,
    calendar_name text,
    misfire_instr smallint,
    job_data bytea
);


--
-- Name: role; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.role (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    code text NOT NULL,
    name text NOT NULL,
    is_system_template boolean DEFAULT false NOT NULL,
    is_active boolean DEFAULT true NOT NULL,
    version bigint DEFAULT 1 NOT NULL
);



--
-- Name: role_permission; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.role_permission (
    tenant_id uuid NOT NULL,
    role_id uuid NOT NULL,
    permission_code text NOT NULL
);



--
-- Name: sequence_definition; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.sequence_definition (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    sequence_key text NOT NULL,
    legal_entity_id uuid,
    branch_id uuid,
    scope_key text,
    period_key text,
    prefix_template text NOT NULL,
    padding integer NOT NULL,
    mode text NOT NULL,
    next_value bigint NOT NULL,
    version bigint DEFAULT 1 NOT NULL,
    CONSTRAINT sequence_definition_mode_check CHECK ((mode = ANY (ARRAY['Standard'::text, 'GapControlled'::text, 'RegulatoryStrategy'::text]))),
    CONSTRAINT sequence_definition_next_value_check CHECK ((next_value > 0)),
    CONSTRAINT sequence_definition_padding_check CHECK (((padding >= 1) AND (padding <= 20)))
);



--
-- Name: setting_definition; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.setting_definition (
    id uuid NOT NULL,
    setting_key text NOT NULL,
    data_type text NOT NULL,
    scope text NOT NULL,
    default_value jsonb NOT NULL,
    sensitive boolean DEFAULT false NOT NULL,
    owner_module text NOT NULL,
    version bigint DEFAULT 1 NOT NULL,
    CONSTRAINT setting_definition_data_type_check CHECK ((data_type = ANY (ARRAY['string'::text, 'boolean'::text, 'integer'::text, 'number'::text, 'json'::text]))),
    CONSTRAINT setting_definition_scope_check CHECK ((scope = ANY (ARRAY['Tenant'::text, 'LegalEntity'::text, 'Branch'::text, 'User'::text])))
);


--
-- Name: setting_value; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.setting_value (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    setting_definition_id uuid NOT NULL,
    scope text NOT NULL,
    scope_id uuid NOT NULL,
    value jsonb NOT NULL,
    version bigint DEFAULT 1 NOT NULL
);



--
-- Name: support_access_grant; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.support_access_grant (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    support_actor_id uuid NOT NULL,
    reason text NOT NULL,
    approved_by uuid,
    expires_at timestamp with time zone NOT NULL,
    status text NOT NULL,
    created_at timestamp with time zone NOT NULL,
    CONSTRAINT support_access_grant_status_check CHECK ((status = ANY (ARRAY['Requested'::text, 'Approved'::text, 'Active'::text, 'Expired'::text, 'Revoked'::text])))
);



--
-- Name: tenant; Type: TABLE; Schema: platform; Owner: -
--

CREATE TABLE platform.tenant (
    id uuid NOT NULL,
    slug text NOT NULL,
    display_name text NOT NULL,
    status text NOT NULL,
    created_at timestamp with time zone NOT NULL,
    version bigint NOT NULL,
    login_domain text NOT NULL,
    normalized_login_domain text NOT NULL,
    CONSTRAINT ck_tenant_login_domain_normalized CHECK (((login_domain = lower(TRIM(BOTH FROM login_domain))) AND (normalized_login_domain = login_domain) AND (POSITION(('@'::text) IN (login_domain)) = 0) AND (POSITION(('/'::text) IN (login_domain)) = 0) AND (POSITION((':'::text) IN (login_domain)) = 0)))
);



--
-- Name: audit_entry audit_entry_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.audit_entry
    ADD CONSTRAINT audit_entry_pkey PRIMARY KEY (id);


--
-- Name: auth_challenge auth_challenge_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.auth_challenge
    ADD CONSTRAINT auth_challenge_pkey PRIMARY KEY (id);


--
-- Name: auth_challenge auth_challenge_token_digest_key; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.auth_challenge
    ADD CONSTRAINT auth_challenge_token_digest_key UNIQUE (token_digest);


--
-- Name: branch branch_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.branch
    ADD CONSTRAINT branch_pkey PRIMARY KEY (id);


--
-- Name: branch branch_tenant_id_id_key; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.branch
    ADD CONSTRAINT branch_tenant_id_id_key UNIQUE (tenant_id, id);


--
-- Name: branch branch_tenant_id_legal_entity_id_code_key; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.branch
    ADD CONSTRAINT branch_tenant_id_legal_entity_id_code_key UNIQUE (tenant_id, legal_entity_id, code);


--
-- Name: custom_field_definition custom_field_definition_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.custom_field_definition
    ADD CONSTRAINT custom_field_definition_pkey PRIMARY KEY (id);


--
-- Name: custom_field_definition custom_field_definition_tenant_id_target_entity_type_stable_key; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.custom_field_definition
    ADD CONSTRAINT custom_field_definition_tenant_id_target_entity_type_stable_key UNIQUE (tenant_id, target_entity_type, stable_key);


--
-- Name: entitlement_snapshot entitlement_snapshot_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.entitlement_snapshot
    ADD CONSTRAINT entitlement_snapshot_pkey PRIMARY KEY (tenant_id, snapshot_version);


--
-- Name: file_record file_record_object_key_key; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.file_record
    ADD CONSTRAINT file_record_object_key_key UNIQUE (object_key);


--
-- Name: file_record file_record_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.file_record
    ADD CONSTRAINT file_record_pkey PRIMARY KEY (id);


--
-- Name: identity identity_normalized_email_key; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.identity
    ADD CONSTRAINT identity_normalized_email_key UNIQUE (normalized_email);


--
-- Name: identity identity_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.identity
    ADD CONSTRAINT identity_pkey PRIMARY KEY (id);


--
-- Name: import_batch import_batch_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.import_batch
    ADD CONSTRAINT import_batch_pkey PRIMARY KEY (id);


--
-- Name: import_batch import_batch_tenant_id_id_key; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.import_batch
    ADD CONSTRAINT import_batch_tenant_id_id_key UNIQUE (tenant_id, id);


--
-- Name: import_chunk_job import_chunk_job_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.import_chunk_job
    ADD CONSTRAINT import_chunk_job_pkey PRIMARY KEY (tenant_id, batch_id, start_row);


--
-- Name: import_error import_error_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.import_error
    ADD CONSTRAINT import_error_pkey PRIMARY KEY (id);


--
-- Name: inbox_message inbox_message_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.inbox_message
    ADD CONSTRAINT inbox_message_pkey PRIMARY KEY (consumer_endpoint, message_id);


--
-- Name: job_schedule job_schedule_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.job_schedule
    ADD CONSTRAINT job_schedule_pkey PRIMARY KEY (id);


--
-- Name: job_schedule job_schedule_tenant_id_job_key_key; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.job_schedule
    ADD CONSTRAINT job_schedule_tenant_id_job_key_key UNIQUE (tenant_id, job_key);


--
-- Name: legal_entity legal_entity_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.legal_entity
    ADD CONSTRAINT legal_entity_pkey PRIMARY KEY (id);


--
-- Name: legal_entity legal_entity_tenant_id_code_key; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.legal_entity
    ADD CONSTRAINT legal_entity_tenant_id_code_key UNIQUE (tenant_id, code);


--
-- Name: legal_entity legal_entity_tenant_id_id_key; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.legal_entity
    ADD CONSTRAINT legal_entity_tenant_id_id_key UNIQUE (tenant_id, id);


--
-- Name: membership membership_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.membership
    ADD CONSTRAINT membership_pkey PRIMARY KEY (id);


--
-- Name: membership_role_assignment membership_role_assignment_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.membership_role_assignment
    ADD CONSTRAINT membership_role_assignment_pkey PRIMARY KEY (id);


--
-- Name: membership membership_tenant_id_id_key; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.membership
    ADD CONSTRAINT membership_tenant_id_id_key UNIQUE (tenant_id, id);


--
-- Name: membership membership_tenant_id_identity_id_key; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.membership
    ADD CONSTRAINT membership_tenant_id_identity_id_key UNIQUE (tenant_id, identity_id);


--
-- Name: message_error message_error_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.message_error
    ADD CONSTRAINT message_error_pkey PRIMARY KEY (id);


--
-- Name: mfa_recovery_code mfa_recovery_code_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.mfa_recovery_code
    ADD CONSTRAINT mfa_recovery_code_pkey PRIMARY KEY (identity_id, digest);


--
-- Name: module_instance module_instance_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.module_instance
    ADD CONSTRAINT module_instance_pkey PRIMARY KEY (id);


--
-- Name: module_instance module_instance_tenant_id_id_key; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.module_instance
    ADD CONSTRAINT module_instance_tenant_id_id_key UNIQUE (tenant_id, id);


--
-- Name: module_instance module_instance_tenant_id_module_id_scope_type_scope_id_key; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.module_instance
    ADD CONSTRAINT module_instance_tenant_id_module_id_scope_type_scope_id_key UNIQUE (tenant_id, module_id, scope_type, scope_id);


--
-- Name: module_operation module_operation_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.module_operation
    ADD CONSTRAINT module_operation_pkey PRIMARY KEY (id);


--
-- Name: module_operation module_operation_tenant_id_idempotency_key_key; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.module_operation
    ADD CONSTRAINT module_operation_tenant_id_idempotency_key_key UNIQUE (tenant_id, idempotency_key);


--
-- Name: notification_delivery notification_delivery_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.notification_delivery
    ADD CONSTRAINT notification_delivery_pkey PRIMARY KEY (id);


--
-- Name: notification notification_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.notification
    ADD CONSTRAINT notification_pkey PRIMARY KEY (id);


--
-- Name: notification notification_tenant_id_id_key; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.notification
    ADD CONSTRAINT notification_tenant_id_id_key UNIQUE (tenant_id, id);


--
-- Name: outbox_message outbox_message_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.outbox_message
    ADD CONSTRAINT outbox_message_pkey PRIMARY KEY (id);


--
-- Name: qrtz_blob_triggers qrtz_blob_triggers_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.qrtz_blob_triggers
    ADD CONSTRAINT qrtz_blob_triggers_pkey PRIMARY KEY (sched_name, trigger_name, trigger_group);


--
-- Name: qrtz_calendars qrtz_calendars_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.qrtz_calendars
    ADD CONSTRAINT qrtz_calendars_pkey PRIMARY KEY (sched_name, calendar_name);


--
-- Name: qrtz_cron_triggers qrtz_cron_triggers_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.qrtz_cron_triggers
    ADD CONSTRAINT qrtz_cron_triggers_pkey PRIMARY KEY (sched_name, trigger_name, trigger_group);


--
-- Name: qrtz_fired_triggers qrtz_fired_triggers_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.qrtz_fired_triggers
    ADD CONSTRAINT qrtz_fired_triggers_pkey PRIMARY KEY (sched_name, entry_id);


--
-- Name: qrtz_job_details qrtz_job_details_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.qrtz_job_details
    ADD CONSTRAINT qrtz_job_details_pkey PRIMARY KEY (sched_name, job_name, job_group);


--
-- Name: qrtz_locks qrtz_locks_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.qrtz_locks
    ADD CONSTRAINT qrtz_locks_pkey PRIMARY KEY (sched_name, lock_name);


--
-- Name: qrtz_paused_trigger_grps qrtz_paused_trigger_grps_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.qrtz_paused_trigger_grps
    ADD CONSTRAINT qrtz_paused_trigger_grps_pkey PRIMARY KEY (sched_name, trigger_group);


--
-- Name: qrtz_scheduler_state qrtz_scheduler_state_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.qrtz_scheduler_state
    ADD CONSTRAINT qrtz_scheduler_state_pkey PRIMARY KEY (sched_name, instance_name);


--
-- Name: qrtz_simple_triggers qrtz_simple_triggers_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.qrtz_simple_triggers
    ADD CONSTRAINT qrtz_simple_triggers_pkey PRIMARY KEY (sched_name, trigger_name, trigger_group);


--
-- Name: qrtz_simprop_triggers qrtz_simprop_triggers_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.qrtz_simprop_triggers
    ADD CONSTRAINT qrtz_simprop_triggers_pkey PRIMARY KEY (sched_name, trigger_name, trigger_group);


--
-- Name: qrtz_triggers qrtz_triggers_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.qrtz_triggers
    ADD CONSTRAINT qrtz_triggers_pkey PRIMARY KEY (sched_name, trigger_name, trigger_group);


--
-- Name: role_permission role_permission_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.role_permission
    ADD CONSTRAINT role_permission_pkey PRIMARY KEY (tenant_id, role_id, permission_code);


--
-- Name: role role_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.role
    ADD CONSTRAINT role_pkey PRIMARY KEY (id);


--
-- Name: role role_tenant_id_code_key; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.role
    ADD CONSTRAINT role_tenant_id_code_key UNIQUE (tenant_id, code);


--
-- Name: role role_tenant_id_id_key; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.role
    ADD CONSTRAINT role_tenant_id_id_key UNIQUE (tenant_id, id);


--
-- Name: sequence_definition sequence_definition_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.sequence_definition
    ADD CONSTRAINT sequence_definition_pkey PRIMARY KEY (id);


--
-- Name: sequence_definition sequence_definition_tenant_id_sequence_key_legal_entity_id__key; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.sequence_definition
    ADD CONSTRAINT sequence_definition_tenant_id_sequence_key_legal_entity_id__key UNIQUE (tenant_id, sequence_key, legal_entity_id, branch_id, scope_key, period_key);


--
-- Name: setting_definition setting_definition_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.setting_definition
    ADD CONSTRAINT setting_definition_pkey PRIMARY KEY (id);


--
-- Name: setting_definition setting_definition_setting_key_key; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.setting_definition
    ADD CONSTRAINT setting_definition_setting_key_key UNIQUE (setting_key);


--
-- Name: setting_value setting_value_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.setting_value
    ADD CONSTRAINT setting_value_pkey PRIMARY KEY (id);


--
-- Name: setting_value setting_value_tenant_id_setting_definition_id_scope_scope_i_key; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.setting_value
    ADD CONSTRAINT setting_value_tenant_id_setting_definition_id_scope_scope_i_key UNIQUE (tenant_id, setting_definition_id, scope, scope_id);


--
-- Name: support_access_grant support_access_grant_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.support_access_grant
    ADD CONSTRAINT support_access_grant_pkey PRIMARY KEY (id);


--
-- Name: tenant tenant_pkey; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.tenant
    ADD CONSTRAINT tenant_pkey PRIMARY KEY (id);


--
-- Name: tenant tenant_slug_key; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.tenant
    ADD CONSTRAINT tenant_slug_key UNIQUE (slug);


--
-- Name: identity ux_identity_tenant_id; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.identity
    ADD CONSTRAINT ux_identity_tenant_id UNIQUE (tenant_id, id);


--
-- Name: identity ux_identity_tenant_prefix; Type: CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.identity
    ADD CONSTRAINT ux_identity_tenant_prefix UNIQUE (tenant_id, login_prefix);


--
-- Name: idx_qrtz_ft_job_group; Type: INDEX; Schema: platform; Owner: -
--

CREATE INDEX idx_qrtz_ft_job_group ON platform.qrtz_fired_triggers USING btree (job_group);


--
-- Name: idx_qrtz_ft_job_name; Type: INDEX; Schema: platform; Owner: -
--

CREATE INDEX idx_qrtz_ft_job_name ON platform.qrtz_fired_triggers USING btree (job_name);


--
-- Name: idx_qrtz_ft_job_req_recovery; Type: INDEX; Schema: platform; Owner: -
--

CREATE INDEX idx_qrtz_ft_job_req_recovery ON platform.qrtz_fired_triggers USING btree (requests_recovery);


--
-- Name: idx_qrtz_ft_trig_group; Type: INDEX; Schema: platform; Owner: -
--

CREATE INDEX idx_qrtz_ft_trig_group ON platform.qrtz_fired_triggers USING btree (trigger_group);


--
-- Name: idx_qrtz_ft_trig_inst_name; Type: INDEX; Schema: platform; Owner: -
--

CREATE INDEX idx_qrtz_ft_trig_inst_name ON platform.qrtz_fired_triggers USING btree (instance_name);


--
-- Name: idx_qrtz_ft_trig_name; Type: INDEX; Schema: platform; Owner: -
--

CREATE INDEX idx_qrtz_ft_trig_name ON platform.qrtz_fired_triggers USING btree (trigger_name);


--
-- Name: idx_qrtz_ft_trig_nm_gp; Type: INDEX; Schema: platform; Owner: -
--

CREATE INDEX idx_qrtz_ft_trig_nm_gp ON platform.qrtz_fired_triggers USING btree (sched_name, trigger_name, trigger_group);


--
-- Name: idx_qrtz_j_req_recovery; Type: INDEX; Schema: platform; Owner: -
--

CREATE INDEX idx_qrtz_j_req_recovery ON platform.qrtz_job_details USING btree (requests_recovery);


--
-- Name: idx_qrtz_t_next_fire_time; Type: INDEX; Schema: platform; Owner: -
--

CREATE INDEX idx_qrtz_t_next_fire_time ON platform.qrtz_triggers USING btree (next_fire_time);


--
-- Name: idx_qrtz_t_nft_st; Type: INDEX; Schema: platform; Owner: -
--

CREATE INDEX idx_qrtz_t_nft_st ON platform.qrtz_triggers USING btree (next_fire_time, trigger_state);


--
-- Name: idx_qrtz_t_state; Type: INDEX; Schema: platform; Owner: -
--

CREATE INDEX idx_qrtz_t_state ON platform.qrtz_triggers USING btree (trigger_state);


--
-- Name: ix_assignment_lookup; Type: INDEX; Schema: platform; Owner: -
--

CREATE INDEX ix_assignment_lookup ON platform.membership_role_assignment USING btree (tenant_id, membership_id, scope_type, scope_id);


--
-- Name: ix_audit_tenant_time; Type: INDEX; Schema: platform; Owner: -
--

CREATE INDEX ix_audit_tenant_time ON platform.audit_entry USING btree (tenant_id, occurred_at DESC);


--
-- Name: ix_auth_challenge_expiry; Type: INDEX; Schema: platform; Owner: -
--

CREATE INDEX ix_auth_challenge_expiry ON platform.auth_challenge USING btree (expires_at) WHERE (consumed_at IS NULL);


--
-- Name: ix_auth_challenge_identity_step; Type: INDEX; Schema: platform; Owner: -
--

CREATE INDEX ix_auth_challenge_identity_step ON platform.auth_challenge USING btree (identity_id, next_step, expires_at) WHERE (consumed_at IS NULL);


--
-- Name: ix_branch_tenant_legal_entity_status; Type: INDEX; Schema: platform; Owner: -
--

CREATE INDEX ix_branch_tenant_legal_entity_status ON platform.branch USING btree (tenant_id, legal_entity_id, status);


--
-- Name: ix_identity_tenant_email; Type: INDEX; Schema: platform; Owner: -
--

CREATE INDEX ix_identity_tenant_email ON platform.identity USING btree (tenant_id, normalized_email);


--
-- Name: ix_identity_tenant_name_prefix; Type: INDEX; Schema: platform; Owner: -
--

CREATE INDEX ix_identity_tenant_name_prefix ON platform.identity USING btree (tenant_id, lower(name) text_pattern_ops);


--
-- Name: ix_identity_tenant_status_name; Type: INDEX; Schema: platform; Owner: -
--

CREATE INDEX ix_identity_tenant_status_name ON platform.identity USING btree (tenant_id, status, name, id);


--
-- Name: ix_inbox_active; Type: INDEX; Schema: platform; Owner: -
--

CREATE INDEX ix_inbox_active ON platform.inbox_message USING btree (tenant_id, lease_until) WHERE (processed_at IS NULL);


--
-- Name: ix_legal_entity_tenant_status; Type: INDEX; Schema: platform; Owner: -
--

CREATE INDEX ix_legal_entity_tenant_status ON platform.legal_entity USING btree (tenant_id, status);


--
-- Name: ix_membership_identity_status; Type: INDEX; Schema: platform; Owner: -
--

CREATE INDEX ix_membership_identity_status ON platform.membership USING btree (identity_id, status);


--
-- Name: ix_membership_tenant_status; Type: INDEX; Schema: platform; Owner: -
--

CREATE INDEX ix_membership_tenant_status ON platform.membership USING btree (tenant_id, status);


--
-- Name: ix_message_error_unresolved; Type: INDEX; Schema: platform; Owner: -
--

CREATE INDEX ix_message_error_unresolved ON platform.message_error USING btree (tenant_id, occurred_at DESC) WHERE (resolved = false);


--
-- Name: ix_mfa_recovery_available; Type: INDEX; Schema: platform; Owner: -
--

CREATE INDEX ix_mfa_recovery_available ON platform.mfa_recovery_code USING btree (identity_id, digest) WHERE (consumed_at IS NULL);


--
-- Name: ix_notification_delivery_lookup; Type: INDEX; Schema: platform; Owner: -
--

CREATE INDEX ix_notification_delivery_lookup ON platform.notification_delivery USING btree (tenant_id, notification_id, attempt_at DESC);


--
-- Name: ix_notification_pending; Type: INDEX; Schema: platform; Owner: -
--

CREATE INDEX ix_notification_pending ON platform.notification USING btree (tenant_id, created_at, id) WHERE (status = 'Pending'::text);


--
-- Name: ix_outbox_pending; Type: INDEX; Schema: platform; Owner: -
--

CREATE INDEX ix_outbox_pending ON platform.outbox_message USING btree (status, next_attempt_at, occurred_at) WHERE (status = 'Pending'::text);


--
-- Name: ix_role_permission_lookup; Type: INDEX; Schema: platform; Owner: -
--

CREATE INDEX ix_role_permission_lookup ON platform.role_permission USING btree (tenant_id, permission_code);


--
-- Name: ix_support_access_expiry; Type: INDEX; Schema: platform; Owner: -
--

CREATE INDEX ix_support_access_expiry ON platform.support_access_grant USING btree (tenant_id, expires_at, status);


--
-- Name: ux_membership_identity; Type: INDEX; Schema: platform; Owner: -
--

CREATE UNIQUE INDEX ux_membership_identity ON platform.membership USING btree (identity_id);


--
-- Name: ux_membership_role_assignment_scope; Type: INDEX; Schema: platform; Owner: -
--

CREATE UNIQUE INDEX ux_membership_role_assignment_scope ON platform.membership_role_assignment USING btree (tenant_id, membership_id, role_id, scope_type, scope_id) NULLS NOT DISTINCT;


--
-- Name: ux_sequence_definition_scope; Type: INDEX; Schema: platform; Owner: -
--

CREATE UNIQUE INDEX ux_sequence_definition_scope ON platform.sequence_definition USING btree (tenant_id, sequence_key, legal_entity_id, branch_id, scope_key, period_key) NULLS NOT DISTINCT;


--
-- Name: ux_tenant_normalized_login_domain; Type: INDEX; Schema: platform; Owner: -
--

CREATE UNIQUE INDEX ux_tenant_normalized_login_domain ON platform.tenant USING btree (normalized_login_domain);


--
-- Name: audit_entry audit_entry_tenant_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.audit_entry
    ADD CONSTRAINT audit_entry_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: auth_challenge auth_challenge_identity_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.auth_challenge
    ADD CONSTRAINT auth_challenge_identity_id_fkey FOREIGN KEY (identity_id) REFERENCES platform.identity(id) ON DELETE CASCADE;


--
-- Name: branch branch_tenant_id_legal_entity_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.branch
    ADD CONSTRAINT branch_tenant_id_legal_entity_id_fkey FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id);


--
-- Name: custom_field_definition custom_field_definition_tenant_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.custom_field_definition
    ADD CONSTRAINT custom_field_definition_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: entitlement_snapshot entitlement_snapshot_tenant_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.entitlement_snapshot
    ADD CONSTRAINT entitlement_snapshot_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: file_record file_record_tenant_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.file_record
    ADD CONSTRAINT file_record_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: identity fk_identity_tenant; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.identity
    ADD CONSTRAINT fk_identity_tenant FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: membership fk_membership_identity_tenant; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.membership
    ADD CONSTRAINT fk_membership_identity_tenant FOREIGN KEY (tenant_id, identity_id) REFERENCES platform.identity(tenant_id, id);


--
-- Name: import_batch import_batch_tenant_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.import_batch
    ADD CONSTRAINT import_batch_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: import_chunk_job import_chunk_job_tenant_id_batch_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.import_chunk_job
    ADD CONSTRAINT import_chunk_job_tenant_id_batch_id_fkey FOREIGN KEY (tenant_id, batch_id) REFERENCES platform.import_batch(tenant_id, id);


--
-- Name: import_chunk_job import_chunk_job_tenant_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.import_chunk_job
    ADD CONSTRAINT import_chunk_job_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: import_error import_error_tenant_id_batch_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.import_error
    ADD CONSTRAINT import_error_tenant_id_batch_id_fkey FOREIGN KEY (tenant_id, batch_id) REFERENCES platform.import_batch(tenant_id, id);


--
-- Name: import_error import_error_tenant_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.import_error
    ADD CONSTRAINT import_error_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: inbox_message inbox_message_tenant_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.inbox_message
    ADD CONSTRAINT inbox_message_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: job_schedule job_schedule_tenant_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.job_schedule
    ADD CONSTRAINT job_schedule_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: legal_entity legal_entity_tenant_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.legal_entity
    ADD CONSTRAINT legal_entity_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: membership membership_identity_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.membership
    ADD CONSTRAINT membership_identity_id_fkey FOREIGN KEY (identity_id) REFERENCES platform.identity(id);


--
-- Name: membership_role_assignment membership_role_assignment_tenant_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.membership_role_assignment
    ADD CONSTRAINT membership_role_assignment_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: membership_role_assignment membership_role_assignment_tenant_id_membership_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.membership_role_assignment
    ADD CONSTRAINT membership_role_assignment_tenant_id_membership_id_fkey FOREIGN KEY (tenant_id, membership_id) REFERENCES platform.membership(tenant_id, id);


--
-- Name: membership_role_assignment membership_role_assignment_tenant_id_role_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.membership_role_assignment
    ADD CONSTRAINT membership_role_assignment_tenant_id_role_id_fkey FOREIGN KEY (tenant_id, role_id) REFERENCES platform.role(tenant_id, id);


--
-- Name: membership membership_tenant_id_default_branch_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.membership
    ADD CONSTRAINT membership_tenant_id_default_branch_id_fkey FOREIGN KEY (tenant_id, default_branch_id) REFERENCES platform.branch(tenant_id, id);


--
-- Name: membership membership_tenant_id_default_legal_entity_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.membership
    ADD CONSTRAINT membership_tenant_id_default_legal_entity_id_fkey FOREIGN KEY (tenant_id, default_legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id);


--
-- Name: membership membership_tenant_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.membership
    ADD CONSTRAINT membership_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: message_error message_error_tenant_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.message_error
    ADD CONSTRAINT message_error_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: mfa_recovery_code mfa_recovery_code_identity_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.mfa_recovery_code
    ADD CONSTRAINT mfa_recovery_code_identity_id_fkey FOREIGN KEY (identity_id) REFERENCES platform.identity(id);


--
-- Name: module_instance module_instance_tenant_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.module_instance
    ADD CONSTRAINT module_instance_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: module_operation module_operation_tenant_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.module_operation
    ADD CONSTRAINT module_operation_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: module_operation module_operation_tenant_id_module_instance_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.module_operation
    ADD CONSTRAINT module_operation_tenant_id_module_instance_id_fkey FOREIGN KEY (tenant_id, module_instance_id) REFERENCES platform.module_instance(tenant_id, id);


--
-- Name: notification_delivery notification_delivery_tenant_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.notification_delivery
    ADD CONSTRAINT notification_delivery_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: notification_delivery notification_delivery_tenant_id_notification_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.notification_delivery
    ADD CONSTRAINT notification_delivery_tenant_id_notification_id_fkey FOREIGN KEY (tenant_id, notification_id) REFERENCES platform.notification(tenant_id, id);


--
-- Name: notification notification_tenant_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.notification
    ADD CONSTRAINT notification_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: outbox_message outbox_message_tenant_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.outbox_message
    ADD CONSTRAINT outbox_message_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: qrtz_blob_triggers qrtz_blob_triggers_sched_name_trigger_name_trigger_group_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.qrtz_blob_triggers
    ADD CONSTRAINT qrtz_blob_triggers_sched_name_trigger_name_trigger_group_fkey FOREIGN KEY (sched_name, trigger_name, trigger_group) REFERENCES platform.qrtz_triggers(sched_name, trigger_name, trigger_group) ON DELETE CASCADE;


--
-- Name: qrtz_cron_triggers qrtz_cron_triggers_sched_name_trigger_name_trigger_group_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.qrtz_cron_triggers
    ADD CONSTRAINT qrtz_cron_triggers_sched_name_trigger_name_trigger_group_fkey FOREIGN KEY (sched_name, trigger_name, trigger_group) REFERENCES platform.qrtz_triggers(sched_name, trigger_name, trigger_group) ON DELETE CASCADE;


--
-- Name: qrtz_simple_triggers qrtz_simple_triggers_sched_name_trigger_name_trigger_group_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.qrtz_simple_triggers
    ADD CONSTRAINT qrtz_simple_triggers_sched_name_trigger_name_trigger_group_fkey FOREIGN KEY (sched_name, trigger_name, trigger_group) REFERENCES platform.qrtz_triggers(sched_name, trigger_name, trigger_group) ON DELETE CASCADE;


--
-- Name: qrtz_simprop_triggers qrtz_simprop_triggers_sched_name_trigger_name_trigger_grou_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.qrtz_simprop_triggers
    ADD CONSTRAINT qrtz_simprop_triggers_sched_name_trigger_name_trigger_grou_fkey FOREIGN KEY (sched_name, trigger_name, trigger_group) REFERENCES platform.qrtz_triggers(sched_name, trigger_name, trigger_group) ON DELETE CASCADE;


--
-- Name: qrtz_triggers qrtz_triggers_sched_name_job_name_job_group_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.qrtz_triggers
    ADD CONSTRAINT qrtz_triggers_sched_name_job_name_job_group_fkey FOREIGN KEY (sched_name, job_name, job_group) REFERENCES platform.qrtz_job_details(sched_name, job_name, job_group);


--
-- Name: role_permission role_permission_tenant_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.role_permission
    ADD CONSTRAINT role_permission_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: role_permission role_permission_tenant_id_role_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.role_permission
    ADD CONSTRAINT role_permission_tenant_id_role_id_fkey FOREIGN KEY (tenant_id, role_id) REFERENCES platform.role(tenant_id, id);


--
-- Name: role role_tenant_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.role
    ADD CONSTRAINT role_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: sequence_definition sequence_definition_tenant_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.sequence_definition
    ADD CONSTRAINT sequence_definition_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: setting_value setting_value_setting_definition_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.setting_value
    ADD CONSTRAINT setting_value_setting_definition_id_fkey FOREIGN KEY (setting_definition_id) REFERENCES platform.setting_definition(id);


--
-- Name: setting_value setting_value_tenant_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.setting_value
    ADD CONSTRAINT setting_value_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: support_access_grant support_access_grant_tenant_id_fkey; Type: FK CONSTRAINT; Schema: platform; Owner: -
--

ALTER TABLE ONLY platform.support_access_grant
    ADD CONSTRAINT support_access_grant_tenant_id_fkey FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id);


--
-- Name: audit_entry; Type: ROW SECURITY; Schema: platform; Owner: -
--

ALTER TABLE platform.audit_entry ENABLE ROW LEVEL SECURITY;

--
-- Name: branch; Type: ROW SECURITY; Schema: platform; Owner: -
--

ALTER TABLE platform.branch ENABLE ROW LEVEL SECURITY;

--
-- Name: custom_field_definition; Type: ROW SECURITY; Schema: platform; Owner: -
--

ALTER TABLE platform.custom_field_definition ENABLE ROW LEVEL SECURITY;

--
-- Name: entitlement_snapshot; Type: ROW SECURITY; Schema: platform; Owner: -
--

ALTER TABLE platform.entitlement_snapshot ENABLE ROW LEVEL SECURITY;

--
-- Name: file_record; Type: ROW SECURITY; Schema: platform; Owner: -
--

ALTER TABLE platform.file_record ENABLE ROW LEVEL SECURITY;

--
-- Name: identity; Type: ROW SECURITY; Schema: platform; Owner: -
--

ALTER TABLE platform.identity ENABLE ROW LEVEL SECURITY;

--
-- Name: import_batch; Type: ROW SECURITY; Schema: platform; Owner: -
--

ALTER TABLE platform.import_batch ENABLE ROW LEVEL SECURITY;

--
-- Name: import_chunk_job; Type: ROW SECURITY; Schema: platform; Owner: -
--

ALTER TABLE platform.import_chunk_job ENABLE ROW LEVEL SECURITY;

--
-- Name: import_error; Type: ROW SECURITY; Schema: platform; Owner: -
--

ALTER TABLE platform.import_error ENABLE ROW LEVEL SECURITY;

--
-- Name: inbox_message; Type: ROW SECURITY; Schema: platform; Owner: -
--

ALTER TABLE platform.inbox_message ENABLE ROW LEVEL SECURITY;

--
-- Name: job_schedule; Type: ROW SECURITY; Schema: platform; Owner: -
--

ALTER TABLE platform.job_schedule ENABLE ROW LEVEL SECURITY;

--
-- Name: legal_entity; Type: ROW SECURITY; Schema: platform; Owner: -
--

ALTER TABLE platform.legal_entity ENABLE ROW LEVEL SECURITY;

--
-- Name: membership; Type: ROW SECURITY; Schema: platform; Owner: -
--

ALTER TABLE platform.membership ENABLE ROW LEVEL SECURITY;

--
-- Name: membership_role_assignment; Type: ROW SECURITY; Schema: platform; Owner: -
--

ALTER TABLE platform.membership_role_assignment ENABLE ROW LEVEL SECURITY;

--
-- Name: message_error; Type: ROW SECURITY; Schema: platform; Owner: -
--

ALTER TABLE platform.message_error ENABLE ROW LEVEL SECURITY;

--
-- Name: module_instance; Type: ROW SECURITY; Schema: platform; Owner: -
--

ALTER TABLE platform.module_instance ENABLE ROW LEVEL SECURITY;

--
-- Name: module_operation; Type: ROW SECURITY; Schema: platform; Owner: -
--

ALTER TABLE platform.module_operation ENABLE ROW LEVEL SECURITY;

--
-- Name: notification; Type: ROW SECURITY; Schema: platform; Owner: -
--

ALTER TABLE platform.notification ENABLE ROW LEVEL SECURITY;

--
-- Name: notification_delivery; Type: ROW SECURITY; Schema: platform; Owner: -
--

ALTER TABLE platform.notification_delivery ENABLE ROW LEVEL SECURITY;

--
-- Name: outbox_message; Type: ROW SECURITY; Schema: platform; Owner: -
--

ALTER TABLE platform.outbox_message ENABLE ROW LEVEL SECURITY;

--
-- Name: role; Type: ROW SECURITY; Schema: platform; Owner: -
--

ALTER TABLE platform.role ENABLE ROW LEVEL SECURITY;

--
-- Name: role_permission; Type: ROW SECURITY; Schema: platform; Owner: -
--

ALTER TABLE platform.role_permission ENABLE ROW LEVEL SECURITY;

--
-- Name: sequence_definition; Type: ROW SECURITY; Schema: platform; Owner: -
--

ALTER TABLE platform.sequence_definition ENABLE ROW LEVEL SECURITY;

--
-- Name: setting_value; Type: ROW SECURITY; Schema: platform; Owner: -
--

ALTER TABLE platform.setting_value ENABLE ROW LEVEL SECURITY;

--
-- Name: support_access_grant; Type: ROW SECURITY; Schema: platform; Owner: -
--

ALTER TABLE platform.support_access_grant ENABLE ROW LEVEL SECURITY;

--
-- Name: tenant; Type: ROW SECURITY; Schema: platform; Owner: -
--

ALTER TABLE platform.tenant ENABLE ROW LEVEL SECURITY;

--
-- Name: audit_entry tenant_isolation; Type: POLICY; Schema: platform; Owner: -
--

CREATE POLICY tenant_isolation ON platform.audit_entry USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: branch tenant_isolation; Type: POLICY; Schema: platform; Owner: -
--

CREATE POLICY tenant_isolation ON platform.branch USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: custom_field_definition tenant_isolation; Type: POLICY; Schema: platform; Owner: -
--

CREATE POLICY tenant_isolation ON platform.custom_field_definition USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: entitlement_snapshot tenant_isolation; Type: POLICY; Schema: platform; Owner: -
--

CREATE POLICY tenant_isolation ON platform.entitlement_snapshot USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: file_record tenant_isolation; Type: POLICY; Schema: platform; Owner: -
--

CREATE POLICY tenant_isolation ON platform.file_record USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: identity tenant_isolation; Type: POLICY; Schema: platform; Owner: -
--

CREATE POLICY tenant_isolation ON platform.identity USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: import_batch tenant_isolation; Type: POLICY; Schema: platform; Owner: -
--

CREATE POLICY tenant_isolation ON platform.import_batch USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: import_chunk_job tenant_isolation; Type: POLICY; Schema: platform; Owner: -
--

CREATE POLICY tenant_isolation ON platform.import_chunk_job USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: import_error tenant_isolation; Type: POLICY; Schema: platform; Owner: -
--

CREATE POLICY tenant_isolation ON platform.import_error USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: inbox_message tenant_isolation; Type: POLICY; Schema: platform; Owner: -
--

CREATE POLICY tenant_isolation ON platform.inbox_message USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: job_schedule tenant_isolation; Type: POLICY; Schema: platform; Owner: -
--

CREATE POLICY tenant_isolation ON platform.job_schedule USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: legal_entity tenant_isolation; Type: POLICY; Schema: platform; Owner: -
--

CREATE POLICY tenant_isolation ON platform.legal_entity USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: membership tenant_isolation; Type: POLICY; Schema: platform; Owner: -
--

CREATE POLICY tenant_isolation ON platform.membership USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: membership_role_assignment tenant_isolation; Type: POLICY; Schema: platform; Owner: -
--

CREATE POLICY tenant_isolation ON platform.membership_role_assignment USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: message_error tenant_isolation; Type: POLICY; Schema: platform; Owner: -
--

CREATE POLICY tenant_isolation ON platform.message_error USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: module_instance tenant_isolation; Type: POLICY; Schema: platform; Owner: -
--

CREATE POLICY tenant_isolation ON platform.module_instance USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: module_operation tenant_isolation; Type: POLICY; Schema: platform; Owner: -
--

CREATE POLICY tenant_isolation ON platform.module_operation USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: notification tenant_isolation; Type: POLICY; Schema: platform; Owner: -
--

CREATE POLICY tenant_isolation ON platform.notification USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: notification_delivery tenant_isolation; Type: POLICY; Schema: platform; Owner: -
--

CREATE POLICY tenant_isolation ON platform.notification_delivery USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: outbox_message tenant_isolation; Type: POLICY; Schema: platform; Owner: -
--

CREATE POLICY tenant_isolation ON platform.outbox_message USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: role tenant_isolation; Type: POLICY; Schema: platform; Owner: -
--

CREATE POLICY tenant_isolation ON platform.role USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: role_permission tenant_isolation; Type: POLICY; Schema: platform; Owner: -
--

CREATE POLICY tenant_isolation ON platform.role_permission USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: sequence_definition tenant_isolation; Type: POLICY; Schema: platform; Owner: -
--

CREATE POLICY tenant_isolation ON platform.sequence_definition USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: setting_value tenant_isolation; Type: POLICY; Schema: platform; Owner: -
--

CREATE POLICY tenant_isolation ON platform.setting_value USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: support_access_grant tenant_isolation; Type: POLICY; Schema: platform; Owner: -
--

CREATE POLICY tenant_isolation ON platform.support_access_grant USING ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: tenant tenant_isolation; Type: POLICY; Schema: platform; Owner: -
--

CREATE POLICY tenant_isolation ON platform.tenant USING ((id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)) WITH CHECK ((id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid));


--
-- Name: SCHEMA platform; Type: ACL; Schema: -; Owner: -
--

GRANT USAGE ON SCHEMA platform TO erp_app;
GRANT USAGE ON SCHEMA platform TO erp_dispatcher;


--
-- Name: FUNCTION claim_pending_outbox(limit_count integer); Type: ACL; Schema: platform; Owner: -
--

GRANT ALL ON FUNCTION platform.claim_pending_outbox(limit_count integer) TO erp_app;


--
-- Name: FUNCTION expire_support_access_grants(); Type: ACL; Schema: platform; Owner: -
--

GRANT ALL ON FUNCTION platform.expire_support_access_grants() TO erp_app;


--
-- Name: FUNCTION list_active_memberships(p_identity_id uuid); Type: ACL; Schema: platform; Owner: -
--

GRANT ALL ON FUNCTION platform.list_active_memberships(p_identity_id uuid) TO erp_app;


--
-- Name: FUNCTION observe_pending_outbox(); Type: ACL; Schema: platform; Owner: -
--

GRANT ALL ON FUNCTION platform.observe_pending_outbox() TO erp_app;


--
-- Name: FUNCTION resolve_identity_tenant(p_identity_id uuid); Type: ACL; Schema: platform; Owner: -
--

REVOKE ALL ON FUNCTION platform.resolve_identity_tenant(p_identity_id uuid) FROM PUBLIC;
GRANT ALL ON FUNCTION platform.resolve_identity_tenant(p_identity_id uuid) TO erp_app;


--
-- Name: FUNCTION resolve_login_domain(p_domain text); Type: ACL; Schema: platform; Owner: -
--

REVOKE ALL ON FUNCTION platform.resolve_login_domain(p_domain text) FROM PUBLIC;
GRANT ALL ON FUNCTION platform.resolve_login_domain(p_domain text) TO erp_app;


--
-- Name: TABLE audit_entry; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.audit_entry TO erp_app;


--
-- Name: TABLE auth_challenge; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.auth_challenge TO erp_app;


--
-- Name: TABLE branch; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.branch TO erp_app;


--
-- Name: TABLE custom_field_definition; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.custom_field_definition TO erp_app;


--
-- Name: TABLE entitlement_snapshot; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.entitlement_snapshot TO erp_app;


--
-- Name: TABLE file_record; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.file_record TO erp_app;


--
-- Name: TABLE identity; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.identity TO erp_app;
GRANT SELECT ON TABLE platform.identity TO erp_dispatcher;


--
-- Name: TABLE import_batch; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.import_batch TO erp_app;


--
-- Name: TABLE import_chunk_job; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.import_chunk_job TO erp_app;


--
-- Name: TABLE import_error; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.import_error TO erp_app;


--
-- Name: TABLE inbox_message; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.inbox_message TO erp_app;


--
-- Name: TABLE job_schedule; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.job_schedule TO erp_app;


--
-- Name: TABLE legal_entity; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.legal_entity TO erp_app;


--
-- Name: TABLE membership; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.membership TO erp_app;
GRANT SELECT ON TABLE platform.membership TO erp_dispatcher;


--
-- Name: TABLE membership_role_assignment; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.membership_role_assignment TO erp_app;


--
-- Name: TABLE message_error; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.message_error TO erp_app;


--
-- Name: TABLE mfa_recovery_code; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.mfa_recovery_code TO erp_app;


--
-- Name: TABLE module_instance; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.module_instance TO erp_app;


--
-- Name: TABLE module_operation; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.module_operation TO erp_app;


--
-- Name: TABLE notification; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.notification TO erp_app;


--
-- Name: TABLE notification_delivery; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.notification_delivery TO erp_app;


--
-- Name: TABLE outbox_message; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.outbox_message TO erp_app;
GRANT SELECT,UPDATE ON TABLE platform.outbox_message TO erp_dispatcher;


--
-- Name: TABLE qrtz_blob_triggers; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.qrtz_blob_triggers TO erp_app;


--
-- Name: TABLE qrtz_calendars; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.qrtz_calendars TO erp_app;


--
-- Name: TABLE qrtz_cron_triggers; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.qrtz_cron_triggers TO erp_app;


--
-- Name: TABLE qrtz_fired_triggers; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.qrtz_fired_triggers TO erp_app;


--
-- Name: TABLE qrtz_job_details; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.qrtz_job_details TO erp_app;


--
-- Name: TABLE qrtz_locks; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.qrtz_locks TO erp_app;


--
-- Name: TABLE qrtz_paused_trigger_grps; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.qrtz_paused_trigger_grps TO erp_app;


--
-- Name: TABLE qrtz_scheduler_state; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.qrtz_scheduler_state TO erp_app;


--
-- Name: TABLE qrtz_simple_triggers; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.qrtz_simple_triggers TO erp_app;


--
-- Name: TABLE qrtz_simprop_triggers; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.qrtz_simprop_triggers TO erp_app;


--
-- Name: TABLE qrtz_triggers; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.qrtz_triggers TO erp_app;


--
-- Name: TABLE role; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.role TO erp_app;


--
-- Name: TABLE role_permission; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.role_permission TO erp_app;


--
-- Name: TABLE sequence_definition; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.sequence_definition TO erp_app;


--
-- Name: TABLE setting_definition; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.setting_definition TO erp_app;


--
-- Name: TABLE setting_value; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.setting_value TO erp_app;


--
-- Name: TABLE support_access_grant; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.support_access_grant TO erp_app;
GRANT SELECT,UPDATE ON TABLE platform.support_access_grant TO erp_dispatcher;


--
-- Name: TABLE tenant; Type: ACL; Schema: platform; Owner: -
--

GRANT SELECT,INSERT,DELETE,UPDATE ON TABLE platform.tenant TO erp_app;
GRANT SELECT ON TABLE platform.tenant TO erp_dispatcher;


--
-- Name: DEFAULT PRIVILEGES FOR SEQUENCES; Type: DEFAULT ACL; Schema: platform; Owner: -
--

ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform GRANT ALL ON SEQUENCES TO erp_app;


--
-- Name: DEFAULT PRIVILEGES FOR TABLES; Type: DEFAULT ACL; Schema: platform; Owner: -
--

ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform GRANT SELECT,INSERT,DELETE,UPDATE ON TABLES TO erp_app;


--
-- PostgreSQL database dump complete
--

