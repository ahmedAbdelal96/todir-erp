CREATE SCHEMA business_hr AUTHORIZATION erp_owner;
REVOKE ALL ON SCHEMA business_hr FROM PUBLIC;
GRANT USAGE ON SCHEMA business_hr TO erp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA business_hr GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;

CREATE TABLE business_hr.employee (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL REFERENCES platform.tenant(id),
    arabic_name text NOT NULL,
    english_name text,
    preferred_name text,
    display_name text NOT NULL,
    display_name_normalized text NOT NULL,
    arabic_name_normalized text NOT NULL,
    english_name_normalized text,
    status text NOT NULL CHECK (status IN ('Active','Inactive','Archived')),
    membership_id uuid,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    version bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, id, membership_id),
    FOREIGN KEY (tenant_id, membership_id) REFERENCES platform.membership(tenant_id, id),
    CHECK (length(arabic_name) BETWEEN 1 AND 200),
    CHECK (english_name IS NULL OR length(english_name) BETWEEN 1 AND 200),
    CHECK (preferred_name IS NULL OR length(preferred_name) BETWEEN 1 AND 200),
    CHECK (length(display_name) BETWEEN 1 AND 200)
);
CREATE UNIQUE INDEX ux_hr_employee_membership ON business_hr.employee(tenant_id, membership_id) WHERE membership_id IS NOT NULL;
CREATE INDEX ix_hr_employee_search_ar ON business_hr.employee USING gin (arabic_name_normalized public.gin_trgm_ops) WHERE status <> 'Archived';
CREATE INDEX ix_hr_employee_search_en ON business_hr.employee USING gin (english_name_normalized public.gin_trgm_ops) WHERE status <> 'Archived' AND english_name_normalized IS NOT NULL;
CREATE INDEX ix_hr_employee_status ON business_hr.employee(tenant_id, status, id);
CREATE INDEX ix_hr_employee_import_duplicate_ar ON business_hr.employee(tenant_id, arabic_name_normalized) WHERE status<>'Archived';
CREATE INDEX ix_hr_employee_import_duplicate_en ON business_hr.employee(tenant_id, english_name_normalized) WHERE status<>'Archived' AND english_name_normalized IS NOT NULL;

CREATE TABLE business_hr.employment (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    employee_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    employment_number text NOT NULL,
    employment_type text NOT NULL CHECK (employment_type IN ('FullTime','PartTime','Temporary','FixedTerm','Probationary','Intern','Seasonal','Other')),
    status text NOT NULL CHECK (status IN ('PreHire','Active','Suspended','NoticePeriod','Terminated','Ended')),
    start_date date NOT NULL,
    end_date date,
    status_reason_code text,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    version bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, id, legal_entity_id),
    UNIQUE (tenant_id, id, employee_id),
    UNIQUE (tenant_id, legal_entity_id, employment_number),
    FOREIGN KEY (tenant_id, employee_id) REFERENCES business_hr.employee(tenant_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (end_date IS NULL OR end_date >= start_date),
    CHECK (status_reason_code IS NULL OR length(status_reason_code) BETWEEN 1 AND 80)
);
CREATE INDEX ix_hr_employment_employee ON business_hr.employment(tenant_id, employee_id, start_date DESC, id);
CREATE INDEX ix_hr_employment_entity_status ON business_hr.employment(tenant_id, legal_entity_id, status, start_date DESC, id);
CREATE INDEX ix_hr_employment_number ON business_hr.employment(tenant_id, legal_entity_id, employment_number);
CREATE UNIQUE INDEX ux_hr_employment_current_per_entity ON business_hr.employment(tenant_id,legal_entity_id,employee_id) WHERE status IN ('Active','Suspended','NoticePeriod');

CREATE TABLE business_hr.employee_import_reference (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    source_system text NOT NULL,
    legacy_employee_id text NOT NULL,
    batch_reference text NOT NULL,
    file_sha256 char(64) NOT NULL,
    row_reference text NOT NULL,
    employee_id uuid NOT NULL,
    employment_id uuid,
    data_quality text NOT NULL,
    imported_at timestamptz NOT NULL,
    imported_by uuid NOT NULL,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,source_system,legacy_employee_id),
    UNIQUE (tenant_id,source_system,batch_reference,row_reference),
    FOREIGN KEY (tenant_id,employee_id) REFERENCES business_hr.employee(tenant_id,id),
    FOREIGN KEY (tenant_id,employment_id,employee_id) REFERENCES business_hr.employment(tenant_id,id,employee_id),
    CHECK (length(source_system) BETWEEN 1 AND 80),
    CHECK (length(legacy_employee_id) BETWEEN 1 AND 160),
    CHECK (length(batch_reference) BETWEEN 1 AND 160),
    CHECK (length(row_reference) BETWEEN 1 AND 160),
    CHECK (data_quality IN ('Complete','Partial','UnknownHistory')),
    CHECK (file_sha256 ~ '^[0-9a-f]{64}$')
);
CREATE INDEX ix_hr_employee_import_batch ON business_hr.employee_import_reference(tenant_id,source_system,batch_reference,row_reference);

CREATE TABLE business_hr.department (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    code text NOT NULL,
    name_ar text NOT NULL,
    name_en text,
    parent_id uuid,
    status text NOT NULL CHECK (status IN ('Active','Archived')),
    version bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, id, legal_entity_id),
    UNIQUE (tenant_id, legal_entity_id, code),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    FOREIGN KEY (tenant_id, parent_id, legal_entity_id) REFERENCES business_hr.department(tenant_id, id, legal_entity_id),
    CHECK (length(code) BETWEEN 1 AND 40), CHECK (length(name_ar) BETWEEN 1 AND 160)
);
CREATE INDEX ix_hr_department_parent ON business_hr.department(tenant_id, legal_entity_id, parent_id, status);
CREATE FUNCTION business_hr.enforce_department_acyclic() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    PERFORM pg_advisory_xact_lock(hashtextextended(NEW.tenant_id::text || ':' || NEW.legal_entity_id::text || ':department-tree', 0));
    IF NEW.parent_id IS NOT NULL THEN
        IF NEW.parent_id = NEW.id OR EXISTS (
            WITH RECURSIVE ancestors(id) AS (
                SELECT NEW.parent_id
                UNION ALL
                SELECT d.parent_id FROM business_hr.department d JOIN ancestors a ON d.tenant_id = NEW.tenant_id AND d.legal_entity_id = NEW.legal_entity_id AND d.id = a.id WHERE d.parent_id IS NOT NULL
            ) SELECT 1 FROM ancestors WHERE id = NEW.id
        ) THEN RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'hr.department_hierarchy_cycle'; END IF;
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER trg_hr_department_acyclic BEFORE INSERT OR UPDATE OF parent_id, legal_entity_id
ON business_hr.department FOR EACH ROW EXECUTE FUNCTION business_hr.enforce_department_acyclic();

CREATE TABLE business_hr.position (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    code text NOT NULL,
    title_ar text NOT NULL,
    title_en text,
    status text NOT NULL CHECK (status IN ('Active','Archived')),
    version bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, id, legal_entity_id),
    UNIQUE (tenant_id, legal_entity_id, code),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (length(code) BETWEEN 1 AND 40), CHECK (length(title_ar) BETWEEN 1 AND 160)
);

CREATE TABLE business_hr.work_calendar (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    code text NOT NULL,
    name_ar text NOT NULL,
    name_en text,
    time_zone_id text NOT NULL,
    status text NOT NULL CHECK (status IN ('Active','Archived')),
    version bigint NOT NULL DEFAULT 1 CHECK (version>0),
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,id,legal_entity_id),
    UNIQUE (tenant_id,legal_entity_id,code),
    FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id),
    CHECK (length(code) BETWEEN 1 AND 40), CHECK (length(name_ar) BETWEEN 1 AND 160),
    CHECK (length(time_zone_id) BETWEEN 1 AND 100)
);
CREATE TABLE business_hr.work_calendar_weekly_pattern (
    tenant_id uuid NOT NULL,
    calendar_id uuid NOT NULL,
    day_of_week smallint NOT NULL CHECK (day_of_week BETWEEN 0 AND 6),
    is_working_day boolean NOT NULL,
    local_start time without time zone,
    local_end time without time zone,
    break_minutes smallint NOT NULL DEFAULT 0 CHECK (break_minutes BETWEEN 0 AND 720),
    PRIMARY KEY (tenant_id,calendar_id,day_of_week),
    FOREIGN KEY (tenant_id,calendar_id) REFERENCES business_hr.work_calendar(tenant_id,id),
    CHECK ((is_working_day AND local_start IS NOT NULL AND local_end IS NOT NULL AND local_end<>local_start)
        OR (NOT is_working_day AND local_start IS NULL AND local_end IS NULL AND break_minutes=0))
);
CREATE TABLE business_hr.work_calendar_holiday (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    calendar_id uuid NOT NULL,
    holiday_date date NOT NULL,
    name_ar text NOT NULL,
    name_en text,
    source_code text NOT NULL,
    status text NOT NULL DEFAULT 'Active' CHECK (status IN ('Active','Cancelled')),
    version bigint NOT NULL DEFAULT 1 CHECK (version>0),
    cancelled_by uuid,
    cancelled_at timestamptz,
    cancel_reason text,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    FOREIGN KEY (tenant_id,calendar_id) REFERENCES business_hr.work_calendar(tenant_id,id),
    CHECK (length(name_ar) BETWEEN 1 AND 160), CHECK (length(source_code) BETWEEN 1 AND 80),
    CHECK ((status='Active' AND cancelled_by IS NULL AND cancelled_at IS NULL AND cancel_reason IS NULL)
        OR (status='Cancelled' AND cancelled_by IS NOT NULL AND cancelled_at IS NOT NULL AND length(cancel_reason) BETWEEN 1 AND 500))
);
CREATE UNIQUE INDEX ux_hr_calendar_active_holiday ON business_hr.work_calendar_holiday(tenant_id,calendar_id,holiday_date) WHERE status='Active';
CREATE FUNCTION business_hr.protect_calendar_holiday_history() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.tenant_id<>OLD.tenant_id OR NEW.id<>OLD.id OR NEW.calendar_id<>OLD.calendar_id OR NEW.holiday_date<>OLD.holiday_date
       OR NEW.name_ar<>OLD.name_ar OR NEW.name_en IS DISTINCT FROM OLD.name_en OR NEW.source_code<>OLD.source_code
       OR NEW.created_by<>OLD.created_by OR NEW.created_at<>OLD.created_at THEN
        RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.calendar_holiday_evidence_immutable';
    END IF;
    IF NEW.status=OLD.status AND NEW.version=OLD.version AND NEW.cancelled_by IS NOT DISTINCT FROM OLD.cancelled_by
       AND NEW.cancelled_at IS NOT DISTINCT FROM OLD.cancelled_at AND NEW.cancel_reason IS NOT DISTINCT FROM OLD.cancel_reason THEN RETURN NEW; END IF;
    IF OLD.status<>'Active' OR NEW.status<>'Cancelled' OR NEW.version<>OLD.version+1 OR NEW.cancelled_by IS NULL OR NEW.cancelled_at IS NULL OR NEW.cancel_reason IS NULL THEN
        RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.calendar_holiday_transition_invalid';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER trg_hr_calendar_holiday_history BEFORE UPDATE ON business_hr.work_calendar_holiday
FOR EACH ROW EXECUTE FUNCTION business_hr.protect_calendar_holiday_history();

CREATE TABLE business_hr.shift_definition (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    code text NOT NULL,
    name_ar text NOT NULL,
    name_en text,
    start_local time without time zone NOT NULL,
    end_local time without time zone NOT NULL,
    is_overnight boolean NOT NULL DEFAULT false,
    break_minutes smallint NOT NULL DEFAULT 0 CHECK (break_minutes BETWEEN 0 AND 720),
    grace_minutes smallint NOT NULL DEFAULT 0 CHECK (grace_minutes BETWEEN 0 AND 240),
    status text NOT NULL CHECK (status IN ('Active','Archived')),
    version bigint NOT NULL DEFAULT 1 CHECK (version>0),
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,id,legal_entity_id),
    UNIQUE (tenant_id,legal_entity_id,code),
    FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id),
    CHECK (length(code) BETWEEN 1 AND 40), CHECK (length(name_ar) BETWEEN 1 AND 160),
    CHECK ((is_overnight AND end_local<start_local) OR (NOT is_overnight AND end_local>start_local)),
    CHECK (break_minutes < extract(epoch FROM (end_local-start_local+CASE WHEN is_overnight THEN interval '24 hours' ELSE interval '0 hours' END))/60)
);

CREATE TABLE business_hr.employment_assignment (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    employment_id uuid NOT NULL,
    branch_id uuid,
    department_id uuid,
    position_id uuid,
    manager_employment_id uuid,
    work_calendar_id uuid,
    effective_from date NOT NULL,
    effective_to date,
    reason_code text NOT NULL,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    version bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    PRIMARY KEY (tenant_id, id),
    FOREIGN KEY (tenant_id, employment_id, legal_entity_id) REFERENCES business_hr.employment(tenant_id, id, legal_entity_id),
    FOREIGN KEY (tenant_id, branch_id) REFERENCES platform.branch(tenant_id, id),
    FOREIGN KEY (tenant_id, department_id, legal_entity_id) REFERENCES business_hr.department(tenant_id, id, legal_entity_id),
    FOREIGN KEY (tenant_id, position_id, legal_entity_id) REFERENCES business_hr.position(tenant_id, id, legal_entity_id),
    FOREIGN KEY (tenant_id, manager_employment_id, legal_entity_id) REFERENCES business_hr.employment(tenant_id, id, legal_entity_id),
    FOREIGN KEY (tenant_id, work_calendar_id, legal_entity_id) REFERENCES business_hr.work_calendar(tenant_id,id,legal_entity_id),
    CHECK (effective_to IS NULL OR effective_to > effective_from),
    CHECK (manager_employment_id IS NULL OR manager_employment_id <> employment_id)
);
CREATE INDEX ix_hr_assignment_asof ON business_hr.employment_assignment(tenant_id, legal_entity_id, employment_id, effective_from DESC, id);
CREATE INDEX ix_hr_assignment_manager ON business_hr.employment_assignment(tenant_id, legal_entity_id, manager_employment_id, effective_from DESC) WHERE manager_employment_id IS NOT NULL;
CREATE INDEX ix_hr_assignment_branch ON business_hr.employment_assignment(tenant_id, legal_entity_id, branch_id, effective_from DESC) WHERE branch_id IS NOT NULL;

CREATE TABLE business_hr.shift_assignment (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    employment_id uuid NOT NULL,
    shift_definition_id uuid NOT NULL,
    effective_from date NOT NULL,
    effective_to date,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,id,employment_id),
    FOREIGN KEY (tenant_id,employment_id,legal_entity_id) REFERENCES business_hr.employment(tenant_id,id,legal_entity_id),
    FOREIGN KEY (tenant_id,shift_definition_id,legal_entity_id) REFERENCES business_hr.shift_definition(tenant_id,id,legal_entity_id),
    CHECK (effective_to IS NULL OR effective_to>effective_from)
);
CREATE INDEX ix_hr_shift_assignment_asof ON business_hr.shift_assignment(tenant_id,legal_entity_id,employment_id,effective_from DESC,id);
CREATE FUNCTION business_hr.enforce_shift_assignment_non_overlap() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    PERFORM pg_advisory_xact_lock(hashtextextended(NEW.tenant_id::text || ':' || NEW.employment_id::text || ':shift', 0));
    IF EXISTS (
        SELECT 1 FROM business_hr.shift_assignment existing
        WHERE existing.tenant_id=NEW.tenant_id AND existing.employment_id=NEW.employment_id AND existing.id<>NEW.id
          AND NEW.effective_from<COALESCE(existing.effective_to,'infinity'::date)
          AND existing.effective_from<COALESCE(NEW.effective_to,'infinity'::date)
    ) THEN RAISE EXCEPTION USING ERRCODE='23505', MESSAGE='hr.shift_assignment_overlap'; END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER trg_hr_shift_assignment_non_overlap BEFORE INSERT OR UPDATE ON business_hr.shift_assignment
FOR EACH ROW EXECUTE FUNCTION business_hr.enforce_shift_assignment_non_overlap();
CREATE FUNCTION business_hr.protect_shift_assignment_history() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.tenant_id<>OLD.tenant_id OR NEW.id<>OLD.id OR NEW.legal_entity_id<>OLD.legal_entity_id OR NEW.employment_id<>OLD.employment_id
       OR NEW.shift_definition_id<>OLD.shift_definition_id OR NEW.effective_from<>OLD.effective_from OR NEW.created_by<>OLD.created_by OR NEW.created_at<>OLD.created_at THEN
        RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.shift_assignment_history_immutable';
    END IF;
    IF NEW.effective_to IS NOT DISTINCT FROM OLD.effective_to THEN RETURN NEW; END IF;
    IF OLD.effective_to IS NOT NULL OR NEW.effective_to IS NULL OR NEW.effective_to<=OLD.effective_from THEN
        RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.shift_assignment_transition_invalid';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER trg_hr_shift_assignment_history BEFORE UPDATE ON business_hr.shift_assignment
FOR EACH ROW EXECUTE FUNCTION business_hr.protect_shift_assignment_history();

CREATE FUNCTION business_hr.enforce_assignment_non_overlap() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    PERFORM pg_advisory_xact_lock(hashtextextended(NEW.tenant_id::text || ':' || NEW.employment_id::text, 0));
    IF NEW.branch_id IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM platform.branch b WHERE b.tenant_id = NEW.tenant_id AND b.id = NEW.branch_id AND b.legal_entity_id = NEW.legal_entity_id
    ) THEN RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'hr.assignment_branch_entity_mismatch'; END IF;
    IF EXISTS (
        SELECT 1 FROM business_hr.employment_assignment existing
        WHERE existing.tenant_id = NEW.tenant_id AND existing.employment_id = NEW.employment_id AND existing.id <> NEW.id
          AND NEW.effective_from < COALESCE(existing.effective_to, 'infinity'::date)
          AND existing.effective_from < COALESCE(NEW.effective_to, 'infinity'::date)
    ) THEN
        RAISE EXCEPTION USING ERRCODE = '23P01', MESSAGE = 'hr.assignment_effective_dates_overlap';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER trg_hr_assignment_non_overlap BEFORE INSERT OR UPDATE OF tenant_id, legal_entity_id, employment_id, branch_id, effective_from, effective_to
ON business_hr.employment_assignment FOR EACH ROW EXECUTE FUNCTION business_hr.enforce_assignment_non_overlap();
CREATE FUNCTION business_hr.enforce_manager_acyclic() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    PERFORM pg_advisory_xact_lock(hashtextextended(NEW.tenant_id::text || ':' || NEW.legal_entity_id::text || ':manager-graph', 0));
    IF NEW.manager_employment_id IS NOT NULL AND EXISTS (
        WITH RECURSIVE candidate_dates(as_of) AS (
            SELECT NEW.effective_from
            UNION
            SELECT a.effective_from FROM business_hr.employment_assignment a
            WHERE a.tenant_id=NEW.tenant_id AND a.legal_entity_id=NEW.legal_entity_id AND a.id<>NEW.id
              AND a.effective_from>NEW.effective_from AND a.effective_from<COALESCE(NEW.effective_to,'infinity'::date)
            UNION
            SELECT a.effective_to FROM business_hr.employment_assignment a
            WHERE a.tenant_id=NEW.tenant_id AND a.legal_entity_id=NEW.legal_entity_id AND a.id<>NEW.id
              AND a.effective_to>NEW.effective_from AND a.effective_to<COALESCE(NEW.effective_to,'infinity'::date)
        ), manager_chain(as_of,id,visited) AS (
            SELECT d.as_of, NEW.manager_employment_id, ARRAY[NEW.manager_employment_id]::uuid[] FROM candidate_dates d
            UNION ALL
            SELECT c.as_of, a.manager_employment_id, c.visited || a.manager_employment_id
            FROM manager_chain c
            JOIN business_hr.employment_assignment a ON a.tenant_id=NEW.tenant_id AND a.legal_entity_id=NEW.legal_entity_id AND a.employment_id=c.id
                AND a.id<>NEW.id AND a.effective_from<=c.as_of AND (a.effective_to IS NULL OR a.effective_to>c.as_of)
            WHERE a.manager_employment_id IS NOT NULL AND NOT a.manager_employment_id = ANY(c.visited)
        ) SELECT 1 FROM manager_chain WHERE id = NEW.employment_id
    ) THEN RAISE EXCEPTION USING ERRCODE = '23514', MESSAGE = 'hr.manager_hierarchy_cycle'; END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER trg_hr_manager_acyclic BEFORE INSERT OR UPDATE OF tenant_id, legal_entity_id, employment_id, manager_employment_id, effective_from, effective_to
ON business_hr.employment_assignment FOR EACH ROW EXECUTE FUNCTION business_hr.enforce_manager_acyclic();

CREATE TABLE business_hr.employment_contract (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    employment_id uuid NOT NULL,
    contract_number text,
    contract_type text NOT NULL,
    start_date date NOT NULL,
    end_date date,
    probation_start date,
    probation_end date,
    work_calendar_id uuid,
    working_hours_basis text,
    notice_period_terms text,
    compensation_agreement_ref uuid,
    country_provider_version text,
    auto_renewal_terms text,
    status text NOT NULL CHECK (status IN ('Draft','Active','Expired','Ended','Superseded','CancelledBeforeStart')),
    signed_file_ref uuid,
    renewal_of_contract_id uuid,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    version bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,id,employment_id),
    FOREIGN KEY (tenant_id,employment_id,legal_entity_id) REFERENCES business_hr.employment(tenant_id,id,legal_entity_id),
    FOREIGN KEY (tenant_id,renewal_of_contract_id,employment_id) REFERENCES business_hr.employment_contract(tenant_id,id,employment_id),
    CHECK (length(contract_type) BETWEEN 1 AND 60),
    CHECK (end_date IS NULL OR end_date >= start_date),
    CHECK ((probation_start IS NULL AND probation_end IS NULL) OR (probation_start IS NOT NULL AND probation_end IS NOT NULL AND probation_end >= probation_start)),
    CHECK (probation_start IS NULL OR (probation_start >= start_date AND (end_date IS NULL OR probation_end <= end_date)))
);
CREATE INDEX ix_hr_contract_search ON business_hr.employment_contract(tenant_id,legal_entity_id,status,end_date,start_date,id);
CREATE INDEX ix_hr_contract_employment ON business_hr.employment_contract(tenant_id,employment_id,start_date DESC,id);
CREATE FUNCTION business_hr.enforce_active_contract_non_overlap() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.status = 'Active' THEN
        PERFORM pg_advisory_xact_lock(hashtextextended(NEW.tenant_id::text || ':' || NEW.employment_id::text || ':contracts', 0));
        IF EXISTS (
            SELECT 1 FROM business_hr.employment_contract c
            WHERE c.tenant_id=NEW.tenant_id AND c.employment_id=NEW.employment_id AND c.id<>NEW.id AND c.status='Active'
              AND NEW.start_date <= COALESCE(c.end_date,'infinity'::date)
              AND c.start_date <= COALESCE(NEW.end_date,'infinity'::date)
        ) THEN RAISE EXCEPTION USING ERRCODE='23P01', MESSAGE='hr.contract_active_dates_overlap'; END IF;
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER trg_hr_contract_non_overlap BEFORE INSERT OR UPDATE OF status,start_date,end_date ON business_hr.employment_contract
FOR EACH ROW EXECUTE FUNCTION business_hr.enforce_active_contract_non_overlap();
CREATE TABLE business_hr.employment_contract_amendment (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    contract_id uuid NOT NULL,
    employment_id uuid NOT NULL,
    effective_date date NOT NULL,
    change_type text NOT NULL,
    previous_snapshot jsonb NOT NULL,
    new_terms jsonb NOT NULL,
    reason text NOT NULL,
    file_ref uuid,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    FOREIGN KEY (tenant_id,contract_id,employment_id) REFERENCES business_hr.employment_contract(tenant_id,id,employment_id),
    CHECK (length(change_type) BETWEEN 1 AND 80), CHECK (length(reason) BETWEEN 1 AND 1000)
);
CREATE INDEX ix_hr_contract_amendment_history ON business_hr.employment_contract_amendment(tenant_id,contract_id,effective_date DESC,created_at DESC,id);

CREATE TABLE business_hr.identifier_lookup_key (
    tenant_id uuid PRIMARY KEY REFERENCES platform.tenant(id),
    protected_key text NOT NULL,
    created_at timestamptz NOT NULL
);
CREATE TABLE business_hr.employee_identifier (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    employee_id uuid NOT NULL,
    identifier_type text NOT NULL,
    country_code text,
    encrypted_value text NOT NULL,
    lookup_hash bytea NOT NULL,
    masked_display text NOT NULL,
    issue_date date,
    expiry_date date,
    issuer text,
    status text NOT NULL CHECK (status IN ('Active','Revoked')),
    file_ref uuid,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    version bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,id,employee_id),
    FOREIGN KEY (tenant_id,employee_id) REFERENCES business_hr.employee(tenant_id,id),
    CHECK (length(identifier_type) BETWEEN 1 AND 60),
    CHECK (country_code IS NULL OR country_code ~ '^[A-Z]{2}$'),
    CHECK (expiry_date IS NULL OR issue_date IS NULL OR expiry_date >= issue_date),
    CHECK (length(masked_display) BETWEEN 4 AND 200),
    CHECK (octet_length(lookup_hash)=32)
);
CREATE UNIQUE INDEX ux_hr_employee_identifier_value ON business_hr.employee_identifier(tenant_id,identifier_type,COALESCE(country_code,''),lookup_hash);
CREATE INDEX ix_hr_identifier_expiry ON business_hr.employee_identifier(tenant_id,expiry_date,id) WHERE status='Active' AND expiry_date IS NOT NULL;
CREATE FUNCTION business_hr.protect_identifier_ciphertext() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.tenant_id<>OLD.tenant_id OR NEW.employee_id<>OLD.employee_id OR NEW.identifier_type<>OLD.identifier_type
       OR NEW.country_code IS DISTINCT FROM OLD.country_code OR NEW.encrypted_value<>OLD.encrypted_value OR NEW.lookup_hash<>OLD.lookup_hash
       OR NEW.masked_display<>OLD.masked_display OR NEW.issue_date IS DISTINCT FROM OLD.issue_date OR NEW.expiry_date IS DISTINCT FROM OLD.expiry_date
       OR NEW.issuer IS DISTINCT FROM OLD.issuer OR NEW.file_ref IS DISTINCT FROM OLD.file_ref THEN
        RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.identifier_immutable';
    END IF;
    IF OLD.status='Revoked' AND NEW.status<>'Revoked' THEN RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.identifier_revoked_immutable'; END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER trg_hr_identifier_immutable BEFORE UPDATE ON business_hr.employee_identifier
FOR EACH ROW EXECUTE FUNCTION business_hr.protect_identifier_ciphertext();

CREATE TABLE business_hr.employee_document (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    employee_id uuid NOT NULL,
    category text NOT NULL,
    sensitivity text NOT NULL CHECK (sensitivity IN ('Normal','Confidential','Medical','Restricted')),
    file_ref uuid NOT NULL,
    sha256 text NOT NULL,
    content_type text NOT NULL,
    size_bytes bigint NOT NULL CHECK (size_bytes >= 0),
    expiry_date date,
    verification_status text NOT NULL CHECK (verification_status IN ('Pending','Verified','Rejected')),
    status text NOT NULL CHECK (status IN ('Active','Superseded','Archived')),
    supersedes_document_id uuid,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    version bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,id,employee_id),
    UNIQUE (tenant_id,file_ref),
    FOREIGN KEY (tenant_id,employee_id) REFERENCES business_hr.employee(tenant_id,id),
    FOREIGN KEY (tenant_id,supersedes_document_id,employee_id) REFERENCES business_hr.employee_document(tenant_id,id,employee_id),
    CHECK (length(category) BETWEEN 1 AND 80),
    CHECK (sha256 ~ '^[0-9a-f]{64}$'),
    CHECK (length(content_type) BETWEEN 3 AND 120)
);
CREATE INDEX ix_hr_document_employee ON business_hr.employee_document(tenant_id,employee_id,status,category,created_at DESC,id DESC);
CREATE INDEX ix_hr_document_expiry ON business_hr.employee_document(tenant_id,expiry_date,id) WHERE status='Active' AND expiry_date IS NOT NULL;
CREATE FUNCTION business_hr.protect_document_evidence() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.tenant_id<>OLD.tenant_id OR NEW.employee_id<>OLD.employee_id OR NEW.category<>OLD.category OR NEW.sensitivity<>OLD.sensitivity
       OR NEW.file_ref<>OLD.file_ref OR NEW.sha256<>OLD.sha256 OR NEW.content_type<>OLD.content_type OR NEW.size_bytes<>OLD.size_bytes
       OR NEW.expiry_date IS DISTINCT FROM OLD.expiry_date OR NEW.supersedes_document_id IS DISTINCT FROM OLD.supersedes_document_id THEN
        RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.document_evidence_immutable';
    END IF;
    IF OLD.status IN ('Superseded','Archived') AND NEW.status<>OLD.status THEN RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.document_history_immutable'; END IF;
    IF OLD.verification_status<>'Pending' AND NEW.verification_status<>OLD.verification_status THEN RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.document_verification_immutable'; END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER trg_hr_document_evidence_immutable BEFORE UPDATE ON business_hr.employee_document
FOR EACH ROW EXECUTE FUNCTION business_hr.protect_document_evidence();

CREATE TABLE business_hr.employee_qualification (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    employee_id uuid NOT NULL,
    qualification_type text NOT NULL,
    name text NOT NULL,
    institution text NOT NULL,
    field_of_study text,
    level text,
    completed_on date,
    file_ref uuid,
    status text NOT NULL CHECK (status IN ('Active','Superseded')),
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    FOREIGN KEY (tenant_id,employee_id) REFERENCES business_hr.employee(tenant_id,id),
    CHECK (length(qualification_type) BETWEEN 1 AND 60), CHECK (length(name) BETWEEN 1 AND 200), CHECK (length(institution) BETWEEN 1 AND 200),
    CHECK (field_of_study IS NULL OR length(field_of_study)<=200), CHECK (level IS NULL OR length(level)<=80)
);
CREATE INDEX ix_hr_qualification_employee ON business_hr.employee_qualification(tenant_id,employee_id,status,completed_on DESC,id);
CREATE TABLE business_hr.employee_credential (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    employee_id uuid NOT NULL,
    credential_type text NOT NULL,
    name text NOT NULL,
    issuer text NOT NULL,
    country_code text,
    issued_on date,
    expiry_date date,
    file_ref uuid,
    verification_status text NOT NULL CHECK (verification_status IN ('Pending','Verified','Rejected')),
    status text NOT NULL CHECK (status IN ('Active','Revoked')),
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    version bigint NOT NULL DEFAULT 1 CHECK (version>0),
    PRIMARY KEY (tenant_id,id),
    FOREIGN KEY (tenant_id,employee_id) REFERENCES business_hr.employee(tenant_id,id),
    CHECK (length(credential_type) BETWEEN 1 AND 60), CHECK (length(name) BETWEEN 1 AND 200), CHECK (length(issuer) BETWEEN 1 AND 200),
    CHECK (country_code IS NULL OR country_code ~ '^[A-Z]{2}$'), CHECK (expiry_date IS NULL OR issued_on IS NULL OR expiry_date>=issued_on)
);
CREATE INDEX ix_hr_credential_expiry ON business_hr.employee_credential(tenant_id,expiry_date,employee_id,id) WHERE status='Active' AND expiry_date IS NOT NULL;
CREATE FUNCTION business_hr.protect_credential_evidence() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.tenant_id<>OLD.tenant_id OR NEW.id<>OLD.id OR NEW.employee_id<>OLD.employee_id OR NEW.credential_type<>OLD.credential_type
       OR NEW.name<>OLD.name OR NEW.issuer<>OLD.issuer OR NEW.country_code IS DISTINCT FROM OLD.country_code
       OR NEW.issued_on IS DISTINCT FROM OLD.issued_on OR NEW.expiry_date IS DISTINCT FROM OLD.expiry_date
       OR NEW.file_ref IS DISTINCT FROM OLD.file_ref OR NEW.status<>OLD.status OR NEW.created_by<>OLD.created_by OR NEW.created_at<>OLD.created_at THEN
        RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.credential_evidence_immutable';
    END IF;
    IF NEW.verification_status=OLD.verification_status AND NEW.version=OLD.version THEN RETURN NEW; END IF;
    IF OLD.verification_status<>'Pending' OR NEW.verification_status NOT IN ('Verified','Rejected') OR NEW.version<>OLD.version+1 THEN
        RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.credential_verification_transition_invalid';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER trg_hr_credential_immutable BEFORE UPDATE ON business_hr.employee_credential
FOR EACH ROW EXECUTE FUNCTION business_hr.protect_credential_evidence();

CREATE TABLE business_hr.employee_insurance_enrollment (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    employment_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    scheme_code text NOT NULL,
    provider_code text,
    encrypted_member_number text,
    masked_member_number text,
    effective_from date NOT NULL,
    effective_to date,
    status text NOT NULL CHECK (status IN ('Draft','Active','Ended','Rejected')),
    country_provider_version text,
    evidence_file_ref uuid,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    version bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    PRIMARY KEY (tenant_id,id),
    FOREIGN KEY (tenant_id,employment_id,legal_entity_id) REFERENCES business_hr.employment(tenant_id,id,legal_entity_id),
    CHECK (length(scheme_code) BETWEEN 1 AND 80),
    CHECK (provider_code IS NULL OR length(provider_code) BETWEEN 1 AND 80),
    CHECK (effective_to IS NULL OR effective_to >= effective_from),
    CHECK ((encrypted_member_number IS NULL) = (masked_member_number IS NULL))
);
CREATE INDEX ix_hr_insurance_as_of ON business_hr.employee_insurance_enrollment(tenant_id,employment_id,effective_from DESC,effective_to) WHERE status='Active';
CREATE FUNCTION business_hr.enforce_insurance_non_overlap() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    PERFORM pg_advisory_xact_lock(hashtextextended(NEW.tenant_id::text || ':' || NEW.employment_id::text || ':' || NEW.scheme_code,0));
    IF NEW.status='Active' AND EXISTS (
        SELECT 1 FROM business_hr.employee_insurance_enrollment existing_row
         WHERE existing_row.tenant_id=NEW.tenant_id AND existing_row.employment_id=NEW.employment_id AND existing_row.scheme_code=NEW.scheme_code
           AND existing_row.status='Active' AND existing_row.id<>NEW.id
           AND daterange(existing_row.effective_from,existing_row.effective_to,'[]') && daterange(NEW.effective_from,NEW.effective_to,'[]')
    ) THEN RAISE EXCEPTION USING ERRCODE='23P01', MESSAGE='hr.insurance_dates_overlap'; END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER trg_hr_insurance_non_overlap BEFORE INSERT OR UPDATE ON business_hr.employee_insurance_enrollment
FOR EACH ROW EXECUTE FUNCTION business_hr.enforce_insurance_non_overlap();
CREATE FUNCTION business_hr.protect_insurance_enrollment_history() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.tenant_id<>OLD.tenant_id OR NEW.id<>OLD.id OR NEW.employment_id<>OLD.employment_id OR NEW.legal_entity_id<>OLD.legal_entity_id
       OR NEW.scheme_code<>OLD.scheme_code OR NEW.provider_code IS DISTINCT FROM OLD.provider_code
       OR NEW.encrypted_member_number IS DISTINCT FROM OLD.encrypted_member_number OR NEW.masked_member_number IS DISTINCT FROM OLD.masked_member_number
       OR NEW.effective_from<>OLD.effective_from OR NEW.effective_to IS DISTINCT FROM OLD.effective_to
       OR NEW.country_provider_version IS DISTINCT FROM OLD.country_provider_version OR NEW.evidence_file_ref IS DISTINCT FROM OLD.evidence_file_ref
       OR NEW.created_by<>OLD.created_by OR NEW.created_at<>OLD.created_at THEN
        RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.insurance_evidence_immutable';
    END IF;
    IF OLD.status<>'Draft' OR NEW.status NOT IN ('Draft','Rejected') THEN
        RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.insurance_status_transition_invalid';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER trg_hr_insurance_history BEFORE UPDATE ON business_hr.employee_insurance_enrollment
FOR EACH ROW EXECUTE FUNCTION business_hr.protect_insurance_enrollment_history();

CREATE TABLE business_hr.employment_compensation_agreement (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    employment_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    currency_code char(3) NOT NULL,
    amount numeric(19,4) NOT NULL CHECK (amount > 0),
    pay_frequency text NOT NULL CHECK (pay_frequency IN ('Hourly','Daily','Weekly','Biweekly','Monthly','Quarterly','Annual','Other')),
    effective_from date NOT NULL,
    effective_to date,
    status text NOT NULL CHECK (status IN ('Draft','Active','Superseded','Ended')),
    contract_id uuid,
    reason text NOT NULL,
    evidence_file_ref uuid,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    version bigint NOT NULL DEFAULT 1 CHECK (version > 0),
    PRIMARY KEY (tenant_id,id),
    FOREIGN KEY (tenant_id,employment_id,legal_entity_id) REFERENCES business_hr.employment(tenant_id,id,legal_entity_id),
    FOREIGN KEY (tenant_id,contract_id,employment_id) REFERENCES business_hr.employment_contract(tenant_id,id,employment_id),
    CHECK (currency_code ~ '^[A-Z]{3}$'), CHECK (effective_to IS NULL OR effective_to >= effective_from),
    CHECK (length(reason) BETWEEN 1 AND 1000)
);
CREATE INDEX ix_hr_compensation_history ON business_hr.employment_compensation_agreement(tenant_id,employment_id,effective_from DESC,id DESC);
CREATE FUNCTION business_hr.enforce_compensation_non_overlap() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    PERFORM pg_advisory_xact_lock(hashtextextended(NEW.tenant_id::text || ':' || NEW.employment_id::text,1));
    IF NEW.status='Active' AND EXISTS (
        SELECT 1 FROM business_hr.employment_compensation_agreement existing_row
         WHERE existing_row.tenant_id=NEW.tenant_id AND existing_row.employment_id=NEW.employment_id AND existing_row.status='Active' AND existing_row.id<>NEW.id
           AND daterange(existing_row.effective_from,existing_row.effective_to,'[]') && daterange(NEW.effective_from,NEW.effective_to,'[]')
    ) THEN RAISE EXCEPTION USING ERRCODE='23P01', MESSAGE='hr.compensation_dates_overlap'; END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER trg_hr_compensation_non_overlap BEFORE INSERT OR UPDATE ON business_hr.employment_compensation_agreement
FOR EACH ROW EXECUTE FUNCTION business_hr.enforce_compensation_non_overlap();
CREATE FUNCTION business_hr.protect_compensation_history() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.tenant_id<>OLD.tenant_id OR NEW.id<>OLD.id OR NEW.employment_id<>OLD.employment_id OR NEW.legal_entity_id<>OLD.legal_entity_id
       OR NEW.currency_code<>OLD.currency_code OR NEW.amount<>OLD.amount OR NEW.pay_frequency<>OLD.pay_frequency
       OR NEW.effective_from<>OLD.effective_from OR NEW.contract_id IS DISTINCT FROM OLD.contract_id OR NEW.reason<>OLD.reason
       OR NEW.evidence_file_ref IS DISTINCT FROM OLD.evidence_file_ref OR NEW.created_by<>OLD.created_by OR NEW.created_at<>OLD.created_at THEN
        RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.compensation_terms_immutable';
    END IF;
    IF NEW.effective_to IS NOT DISTINCT FROM OLD.effective_to AND NEW.status=OLD.status AND NEW.version=OLD.version THEN RETURN NEW; END IF;
    IF NOT (OLD.status='Active' AND NEW.status='Superseded' AND NEW.effective_to IS NOT NULL AND NEW.effective_to>=OLD.effective_from AND NEW.version=OLD.version+1) THEN
        RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.compensation_transition_invalid';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER trg_hr_compensation_history BEFORE UPDATE ON business_hr.employment_compensation_agreement
FOR EACH ROW EXECUTE FUNCTION business_hr.protect_compensation_history();

CREATE TABLE business_hr.hr_adjustment (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    employee_id uuid NOT NULL,
    employment_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    kind text NOT NULL CHECK (kind IN ('Allowance','DisciplinaryAction')),
    adjustment_type text NOT NULL,
    effective_from date NOT NULL,
    effective_to date,
    recurrence text NOT NULL CHECK (recurrence IN ('OneTime','Recurring','Informational')),
    payroll_impact text NOT NULL CHECK (payroll_impact IN ('None','SendToPayroll')),
    amount numeric(18,4),
    currency_code char(3),
    reason_code text NOT NULL,
    reason text NOT NULL,
    evidence_file_ref uuid,
    status text NOT NULL CHECK (status IN ('Draft','Submitted','Approved','Rejected','Cancelled')),
    version bigint NOT NULL DEFAULT 1 CHECK (version>0),
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    submitted_by uuid,
    submitted_at timestamptz,
    approved_by uuid,
    approved_at timestamptz,
    decision_reason text,
    PRIMARY KEY (tenant_id,id),
    FOREIGN KEY (tenant_id,employment_id,legal_entity_id) REFERENCES business_hr.employment(tenant_id,id,legal_entity_id),
    FOREIGN KEY (tenant_id,employment_id,employee_id) REFERENCES business_hr.employment(tenant_id,id,employee_id),
    CHECK (effective_to IS NULL OR effective_to>=effective_from),
    CHECK (length(adjustment_type) BETWEEN 1 AND 80), CHECK (length(reason_code) BETWEEN 1 AND 80), CHECK (length(reason) BETWEEN 1 AND 1000),
    CHECK ((amount IS NULL AND currency_code IS NULL) OR (amount>=0 AND currency_code ~ '^[A-Z]{3}$')),
    CHECK (payroll_impact='None' OR (kind='Allowance' AND amount IS NOT NULL AND currency_code IS NOT NULL AND recurrence<>'Informational')),
    CHECK ((status='Draft' AND submitted_by IS NULL AND submitted_at IS NULL AND approved_by IS NULL AND approved_at IS NULL AND decision_reason IS NULL)
        OR (status='Submitted' AND submitted_by IS NOT NULL AND submitted_at IS NOT NULL AND approved_by IS NULL AND approved_at IS NULL AND decision_reason IS NULL)
        OR (status='Approved' AND submitted_by IS NOT NULL AND submitted_at IS NOT NULL AND approved_by IS NOT NULL AND approved_at IS NOT NULL AND decision_reason IS NULL)
        OR (status='Rejected' AND submitted_by IS NOT NULL AND submitted_at IS NOT NULL AND approved_by IS NOT NULL AND approved_at IS NOT NULL AND length(decision_reason) BETWEEN 1 AND 500)
        OR (status='Cancelled' AND decision_reason IS NOT NULL AND length(decision_reason) BETWEEN 1 AND 500))
);
CREATE INDEX ix_hr_adjustment_payroll ON business_hr.hr_adjustment(tenant_id,legal_entity_id,effective_from,status,employment_id,id) WHERE status='Approved' AND payroll_impact='SendToPayroll';
CREATE INDEX ix_hr_adjustment_history ON business_hr.hr_adjustment(tenant_id,employment_id,effective_from DESC,id);
CREATE FUNCTION business_hr.protect_hr_adjustment_history() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.tenant_id<>OLD.tenant_id OR NEW.id<>OLD.id OR NEW.employee_id<>OLD.employee_id OR NEW.employment_id<>OLD.employment_id OR NEW.legal_entity_id<>OLD.legal_entity_id
       OR NEW.kind<>OLD.kind OR NEW.adjustment_type<>OLD.adjustment_type OR NEW.effective_from<>OLD.effective_from OR NEW.effective_to IS DISTINCT FROM OLD.effective_to
       OR NEW.recurrence<>OLD.recurrence OR NEW.payroll_impact<>OLD.payroll_impact OR NEW.amount IS DISTINCT FROM OLD.amount OR NEW.currency_code IS DISTINCT FROM OLD.currency_code
       OR NEW.reason_code<>OLD.reason_code OR NEW.reason<>OLD.reason OR NEW.evidence_file_ref IS DISTINCT FROM OLD.evidence_file_ref OR NEW.created_by<>OLD.created_by OR NEW.created_at<>OLD.created_at
       OR NEW.version<>OLD.version+1 THEN RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.adjustment_evidence_immutable'; END IF;
    IF OLD.status='Draft' AND NEW.status='Submitted' AND NEW.approved_by IS NULL THEN RETURN NEW; END IF;
    IF OLD.status IN ('Draft','Submitted') AND NEW.status='Cancelled' AND NEW.approved_by IS NULL AND NEW.approved_at IS NULL AND NEW.decision_reason IS NOT NULL THEN RETURN NEW; END IF;
    IF OLD.status='Submitted' AND NEW.status IN ('Approved','Rejected') AND NEW.approved_by IS NOT NULL AND NEW.approved_at IS NOT NULL THEN RETURN NEW; END IF;
    RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.adjustment_transition_invalid';
END $$;
CREATE TRIGGER trg_hr_adjustment_history BEFORE UPDATE ON business_hr.hr_adjustment
FOR EACH ROW EXECUTE FUNCTION business_hr.protect_hr_adjustment_history();

CREATE TABLE business_hr.attendance_punch (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    employee_id uuid NOT NULL,
    employment_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    occurred_at timestamptz NOT NULL,
    local_time_snapshot timestamp without time zone NOT NULL,
    time_zone_id text NOT NULL,
    direction text NOT NULL CHECK (direction IN ('In','Out','Unknown')),
    source_type text NOT NULL CHECK (source_type IN ('Manual','Web','Imported','API','Device')),
    source_reference text,
    idempotency_key text NOT NULL,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,employment_id,idempotency_key),
    FOREIGN KEY (tenant_id,employment_id,legal_entity_id) REFERENCES business_hr.employment(tenant_id,id,legal_entity_id),
    FOREIGN KEY (tenant_id,employment_id,employee_id) REFERENCES business_hr.employment(tenant_id,id,employee_id),
    CHECK (length(time_zone_id) BETWEEN 1 AND 100), CHECK (length(idempotency_key) BETWEEN 8 AND 120),
    CHECK (source_reference IS NULL OR length(source_reference) BETWEEN 1 AND 200)
);
CREATE UNIQUE INDEX ux_hr_punch_source_reference ON business_hr.attendance_punch(tenant_id,employment_id,source_type,source_reference) WHERE source_reference IS NOT NULL;
CREATE INDEX ix_hr_punch_normalize ON business_hr.attendance_punch(tenant_id,employment_id,occurred_at,id);
CREATE FUNCTION business_hr.protect_attendance_punch() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.attendance_punch_immutable';
END $$;
CREATE TRIGGER trg_hr_punch_immutable BEFORE UPDATE OR DELETE ON business_hr.attendance_punch
FOR EACH ROW EXECUTE FUNCTION business_hr.protect_attendance_punch();

CREATE TABLE business_hr.attendance_record (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    employee_id uuid NOT NULL,
    employment_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    work_date date NOT NULL,
    work_calendar_id uuid,
    shift_assignment_id uuid,
    time_zone_id text NOT NULL,
    planned_start_local time without time zone,
    planned_end_local time without time zone,
    planned_break_minutes smallint NOT NULL DEFAULT 0 CHECK (planned_break_minutes BETWEEN 0 AND 720),
    grace_minutes smallint NOT NULL DEFAULT 0 CHECK (grace_minutes BETWEEN 0 AND 240),
    actual_first_in timestamptz,
    actual_last_out timestamptz,
    worked_minutes integer NOT NULL DEFAULT 0 CHECK (worked_minutes>=0),
    late_minutes integer NOT NULL DEFAULT 0 CHECK (late_minutes>=0),
    early_leave_minutes integer NOT NULL DEFAULT 0 CHECK (early_leave_minutes>=0),
    overtime_eligible_minutes integer NOT NULL DEFAULT 0 CHECK (overtime_eligible_minutes>=0),
    attendance_status text NOT NULL CHECK (attendance_status IN ('Present','Partial','Absent','OnApprovedLeave','RestDay','Holiday','Exception')),
    normalization_version integer NOT NULL CHECK (normalization_version>0),
    lock_state text NOT NULL DEFAULT 'Open' CHECK (lock_state IN ('Open','LockedForPayroll')),
    payroll_reference text,
    version bigint NOT NULL DEFAULT 1 CHECK (version>0),
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,employment_id,work_date),
    FOREIGN KEY (tenant_id,employment_id,legal_entity_id) REFERENCES business_hr.employment(tenant_id,id,legal_entity_id),
    FOREIGN KEY (tenant_id,employment_id,employee_id) REFERENCES business_hr.employment(tenant_id,id,employee_id),
    FOREIGN KEY (tenant_id,work_calendar_id,legal_entity_id) REFERENCES business_hr.work_calendar(tenant_id,id,legal_entity_id),
    FOREIGN KEY (tenant_id,shift_assignment_id,employment_id) REFERENCES business_hr.shift_assignment(tenant_id,id,employment_id),
    CHECK ((lock_state='Open' AND payroll_reference IS NULL) OR (lock_state='LockedForPayroll' AND payroll_reference IS NOT NULL))
);
CREATE INDEX ix_hr_attendance_list ON business_hr.attendance_record(tenant_id,legal_entity_id,work_date DESC,attendance_status,employment_id,id);
CREATE INDEX ix_hr_attendance_employee_history ON business_hr.attendance_record(tenant_id,employment_id,work_date DESC,id DESC);
CREATE FUNCTION business_hr.protect_attendance_record_payroll_lock() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF OLD.lock_state='LockedForPayroll' THEN RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.attendance_record_payroll_locked'; END IF;
    IF NEW.lock_state='LockedForPayroll' THEN
        IF NEW.id<>OLD.id OR NEW.tenant_id<>OLD.tenant_id OR NEW.employee_id<>OLD.employee_id OR NEW.employment_id<>OLD.employment_id
           OR NEW.legal_entity_id<>OLD.legal_entity_id OR NEW.work_date<>OLD.work_date OR NEW.work_calendar_id IS DISTINCT FROM OLD.work_calendar_id OR NEW.shift_assignment_id IS DISTINCT FROM OLD.shift_assignment_id
           OR NEW.time_zone_id<>OLD.time_zone_id OR NEW.planned_start_local IS DISTINCT FROM OLD.planned_start_local OR NEW.planned_end_local IS DISTINCT FROM OLD.planned_end_local OR NEW.planned_break_minutes<>OLD.planned_break_minutes OR NEW.grace_minutes<>OLD.grace_minutes
           OR NEW.actual_first_in IS DISTINCT FROM OLD.actual_first_in OR NEW.actual_last_out IS DISTINCT FROM OLD.actual_last_out
           OR NEW.worked_minutes<>OLD.worked_minutes OR NEW.late_minutes<>OLD.late_minutes OR NEW.early_leave_minutes<>OLD.early_leave_minutes
           OR NEW.overtime_eligible_minutes<>OLD.overtime_eligible_minutes OR NEW.attendance_status<>OLD.attendance_status
           OR NEW.normalization_version<>OLD.normalization_version OR NEW.payroll_reference IS NULL OR NEW.version<>OLD.version+1 THEN
            RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.attendance_lock_mutated_record';
        END IF;
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER trg_hr_attendance_lock BEFORE UPDATE ON business_hr.attendance_record
FOR EACH ROW EXECUTE FUNCTION business_hr.protect_attendance_record_payroll_lock();

CREATE TABLE business_hr.attendance_overtime_request (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    employee_id uuid NOT NULL,
    employment_id uuid NOT NULL,
    attendance_record_id uuid NOT NULL,
    requested_minutes integer NOT NULL CHECK (requested_minutes>0),
    status text NOT NULL CHECK (status IN ('Pending','Approved','Rejected','Cancelled')),
    reason text NOT NULL,
    idempotency_key text NOT NULL,
    version bigint NOT NULL DEFAULT 1 CHECK (version>0),
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    decided_by uuid,
    decided_at timestamptz,
    decision_reason text,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,attendance_record_id),
    UNIQUE (tenant_id,idempotency_key),
    FOREIGN KEY (tenant_id,attendance_record_id) REFERENCES business_hr.attendance_record(tenant_id,id),
    FOREIGN KEY (tenant_id,employment_id,legal_entity_id) REFERENCES business_hr.employment(tenant_id,id,legal_entity_id),
    FOREIGN KEY (tenant_id,employment_id,employee_id) REFERENCES business_hr.employment(tenant_id,id,employee_id),
    CHECK (length(reason) BETWEEN 1 AND 1000), CHECK (length(idempotency_key) BETWEEN 8 AND 160),
    CHECK (decision_reason IS NULL OR length(decision_reason)<=500)
);
CREATE INDEX ix_hr_overtime_approval_queue ON business_hr.attendance_overtime_request(tenant_id,legal_entity_id,status,created_at,id);
CREATE TABLE business_hr.attendance_overtime_event (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    request_id uuid NOT NULL,
    event_type text NOT NULL CHECK (event_type IN ('Requested','Approved','Rejected','Cancelled')),
    from_status text,
    to_status text NOT NULL,
    actor_id uuid NOT NULL,
    reason text,
    occurred_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    FOREIGN KEY (tenant_id,request_id) REFERENCES business_hr.attendance_overtime_request(tenant_id,id)
);
CREATE FUNCTION business_hr.protect_attendance_overtime_event() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.attendance_overtime_event_immutable'; END $$;
CREATE TRIGGER trg_hr_attendance_overtime_event_immutable BEFORE UPDATE OR DELETE ON business_hr.attendance_overtime_event
FOR EACH ROW EXECUTE FUNCTION business_hr.protect_attendance_overtime_event();

CREATE TABLE business_hr.attendance_exception (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    employee_id uuid NOT NULL,
    employment_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    attendance_record_id uuid,
    attendance_punch_id uuid,
    exception_code text NOT NULL CHECK (exception_code IN ('MissingCheckIn','MissingCheckOut','DuplicatePunch','OutOfShiftPunch','PunchOnNonWorkingDay')),
    status text NOT NULL CHECK (status IN ('Open','Resolved')),
    reason text,
    created_at timestamptz NOT NULL,
    resolved_by uuid,
    resolved_at timestamptz,
    PRIMARY KEY (tenant_id,id),
    FOREIGN KEY (tenant_id,attendance_record_id) REFERENCES business_hr.attendance_record(tenant_id,id),
    FOREIGN KEY (tenant_id,attendance_punch_id) REFERENCES business_hr.attendance_punch(tenant_id,id),
    FOREIGN KEY (tenant_id,employment_id,legal_entity_id) REFERENCES business_hr.employment(tenant_id,id,legal_entity_id),
    CHECK ((attendance_record_id IS NOT NULL) <> (attendance_punch_id IS NOT NULL)),
    CHECK ((status='Open' AND resolved_by IS NULL AND resolved_at IS NULL) OR (status='Resolved' AND resolved_by IS NOT NULL AND resolved_at IS NOT NULL))
);
CREATE INDEX ix_hr_attendance_exception_queue ON business_hr.attendance_exception(tenant_id,legal_entity_id,created_at DESC,id) WHERE status='Open';
CREATE UNIQUE INDEX ux_hr_attendance_exception_record_open ON business_hr.attendance_exception(tenant_id,attendance_record_id,exception_code) WHERE status='Open' AND attendance_record_id IS NOT NULL;
CREATE UNIQUE INDEX ux_hr_attendance_exception_punch_open ON business_hr.attendance_exception(tenant_id,attendance_punch_id,exception_code) WHERE status='Open' AND attendance_punch_id IS NOT NULL;

CREATE TABLE business_hr.attendance_correction (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    attendance_record_id uuid NOT NULL,
    employment_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    before_snapshot jsonb NOT NULL,
    after_snapshot jsonb NOT NULL,
    reason_code text NOT NULL,
    reason text NOT NULL,
    status text NOT NULL DEFAULT 'Applied' CHECK (status IN ('Applied','PayrollDeltaPending')),
    corrected_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    FOREIGN KEY (tenant_id,attendance_record_id) REFERENCES business_hr.attendance_record(tenant_id,id),
    FOREIGN KEY (tenant_id,employment_id,legal_entity_id) REFERENCES business_hr.employment(tenant_id,id,legal_entity_id),
    CHECK (length(reason_code) BETWEEN 1 AND 80), CHECK (length(reason) BETWEEN 1 AND 500)
);
CREATE INDEX ix_hr_attendance_correction_history ON business_hr.attendance_correction(tenant_id,attendance_record_id,created_at DESC,id);

CREATE TABLE business_hr.payroll_period_snapshot (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    period_start date NOT NULL,
    period_end date NOT NULL,
    cutoff_at timestamptz NOT NULL,
    created_at timestamptz NOT NULL,
    contract_version integer NOT NULL CHECK (contract_version>0),
    payload_sha256 text NOT NULL CHECK (payload_sha256 ~ '^[0-9a-f]{64}$'),
    payload jsonb NOT NULL,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,legal_entity_id,period_start,period_end,cutoff_at),
    FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id),
    CHECK (period_end>=period_start)
);
CREATE INDEX ix_hr_payroll_snapshot_period ON business_hr.payroll_period_snapshot(tenant_id,legal_entity_id,period_start DESC,period_end DESC,created_at DESC);
CREATE FUNCTION business_hr.protect_payroll_snapshot() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.payroll_snapshot_immutable';
END $$;
CREATE TRIGGER trg_hr_payroll_snapshot_immutable BEFORE UPDATE OR DELETE ON business_hr.payroll_period_snapshot
FOR EACH ROW EXECUTE FUNCTION business_hr.protect_payroll_snapshot();

CREATE TABLE business_hr.leave_type (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    code text NOT NULL,
    name_ar text NOT NULL,
    name_en text,
    category text NOT NULL CHECK (category IN ('Annual','Sick','Unpaid','Emergency','Parental','Study','Other')),
    unit text NOT NULL CHECK (unit IN ('Days','Hours')),
    country_code char(2),
    country_provider_code text,
    status text NOT NULL CHECK (status IN ('Active','Archived')),
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,id,legal_entity_id),
    UNIQUE (tenant_id,legal_entity_id,code),
    FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id),
    CHECK (length(code) BETWEEN 1 AND 40), CHECK (length(name_ar) BETWEEN 1 AND 160),
    CHECK (country_code IS NULL OR country_code ~ '^[A-Z]{2}$'),
    CHECK (country_provider_code IS NULL OR length(country_provider_code) BETWEEN 1 AND 80)
);

CREATE TABLE business_hr.leave_policy (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    leave_type_id uuid NOT NULL,
    version integer NOT NULL CHECK (version>0),
    effective_from date NOT NULL,
    effective_to date,
    counting_method text NOT NULL CHECK (counting_method IN ('WorkingDays','CalendarDays')),
    approval_required boolean NOT NULL,
    partial_day_allowed boolean NOT NULL,
    negative_balance_policy text NOT NULL CHECK (negative_balance_policy IN ('Disallow','AllowWithinLimit','RequireApproval','Allow')),
    negative_balance_limit numeric(10,4) NOT NULL DEFAULT 0 CHECK (negative_balance_limit>=0),
    payroll_treatment text NOT NULL CHECK (payroll_treatment IN ('Paid','Unpaid','ProviderDetermined')),
    accrual_rule jsonb NOT NULL DEFAULT '{}'::jsonb,
    carryover_rule jsonb NOT NULL DEFAULT '{}'::jsonb,
    eligibility_rules jsonb NOT NULL DEFAULT '{}'::jsonb,
    country_provider_code text,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,leave_type_id,version),
    FOREIGN KEY (tenant_id,leave_type_id,legal_entity_id) REFERENCES business_hr.leave_type(tenant_id,id,legal_entity_id),
    CHECK (effective_to IS NULL OR effective_to>=effective_from),
    CHECK ((negative_balance_policy='AllowWithinLimit' AND negative_balance_limit>0) OR (negative_balance_policy<>'AllowWithinLimit' AND negative_balance_limit=0)),
    CHECK (accrual_rule ? 'mode' AND accrual_rule->>'mode' IN ('None','Manual','Monthly','Annual','CountryProvider'))
);
CREATE INDEX ix_hr_leave_policy_current ON business_hr.leave_policy(tenant_id,legal_entity_id,leave_type_id,effective_from DESC,version DESC);
CREATE FUNCTION business_hr.enforce_leave_policy_non_overlap() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    PERFORM pg_advisory_xact_lock(hashtextextended(NEW.tenant_id::text || ':' || NEW.leave_type_id::text || ':leave-policy',0));
    IF EXISTS (SELECT 1 FROM business_hr.leave_policy p WHERE p.tenant_id=NEW.tenant_id AND p.leave_type_id=NEW.leave_type_id AND p.id<>NEW.id
        AND daterange(p.effective_from,CASE WHEN p.effective_to IS NULL THEN NULL ELSE p.effective_to+1 END,'[)') && daterange(NEW.effective_from,CASE WHEN NEW.effective_to IS NULL THEN NULL ELSE NEW.effective_to+1 END,'[)'))
    THEN RAISE EXCEPTION USING ERRCODE='23P01', MESSAGE='hr.leave_policy_overlap'; END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER trg_hr_leave_policy_non_overlap BEFORE INSERT OR UPDATE ON business_hr.leave_policy
FOR EACH ROW EXECUTE FUNCTION business_hr.enforce_leave_policy_non_overlap();

CREATE TABLE business_hr.leave_balance_movement (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    employment_id uuid NOT NULL,
    leave_type_id uuid NOT NULL,
    effective_date date NOT NULL,
    movement_type text NOT NULL CHECK (movement_type IN ('Opening','Accrual','Grant','CarryOver','Adjustment','RequestReservation','LeaveTaken','CancellationRestore','Expiry','Correction')),
    quantity numeric(12,4) NOT NULL CHECK (quantity<>0),
    source_type text NOT NULL,
    source_reference text NOT NULL,
    idempotency_key text NOT NULL,
    policy_version integer,
    expires_on date,
    reason text NOT NULL,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,idempotency_key),
    FOREIGN KEY (tenant_id,employment_id,legal_entity_id) REFERENCES business_hr.employment(tenant_id,id,legal_entity_id),
    FOREIGN KEY (tenant_id,leave_type_id,legal_entity_id) REFERENCES business_hr.leave_type(tenant_id,id,legal_entity_id),
    CHECK (expires_on IS NULL OR (movement_type='CarryOver' AND quantity>0)),
    CHECK (length(source_type) BETWEEN 1 AND 40), CHECK (length(source_reference) BETWEEN 1 AND 120),
    CHECK (length(idempotency_key) BETWEEN 8 AND 160), CHECK (length(reason) BETWEEN 1 AND 500)
);
CREATE INDEX ix_hr_leave_balance_history ON business_hr.leave_balance_movement(tenant_id,employment_id,leave_type_id,effective_date,id);
CREATE INDEX ix_hr_leave_entitlement_expiry ON business_hr.leave_balance_movement(tenant_id,employment_id,leave_type_id,expires_on,id) WHERE movement_type='CarryOver' AND quantity>0 AND expires_on IS NOT NULL;
CREATE FUNCTION business_hr.protect_leave_balance_movement() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.leave_balance_movement_immutable';
END $$;
CREATE TRIGGER trg_hr_leave_balance_movement_immutable BEFORE UPDATE OR DELETE ON business_hr.leave_balance_movement
FOR EACH ROW EXECUTE FUNCTION business_hr.protect_leave_balance_movement();

CREATE TABLE business_hr.leave_balance_allocation (
    tenant_id uuid NOT NULL,
    movement_id uuid NOT NULL,
    entitlement_movement_id uuid NOT NULL,
    PRIMARY KEY (tenant_id,movement_id),
    FOREIGN KEY (tenant_id,movement_id) REFERENCES business_hr.leave_balance_movement(tenant_id,id),
    FOREIGN KEY (tenant_id,entitlement_movement_id) REFERENCES business_hr.leave_balance_movement(tenant_id,id),
    CHECK (movement_id<>entitlement_movement_id)
);
CREATE INDEX ix_hr_leave_allocation_entitlement ON business_hr.leave_balance_allocation(tenant_id,entitlement_movement_id,movement_id);
CREATE FUNCTION business_hr.validate_leave_balance_allocation() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE movement_row business_hr.leave_balance_movement%ROWTYPE;
DECLARE entitlement_row business_hr.leave_balance_movement%ROWTYPE;
BEGIN
    SELECT * INTO STRICT movement_row FROM business_hr.leave_balance_movement WHERE tenant_id=NEW.tenant_id AND id=NEW.movement_id;
    SELECT * INTO STRICT entitlement_row FROM business_hr.leave_balance_movement WHERE tenant_id=NEW.tenant_id AND id=NEW.entitlement_movement_id;
    IF movement_row.employment_id<>entitlement_row.employment_id OR movement_row.leave_type_id<>entitlement_row.leave_type_id
       OR movement_row.legal_entity_id<>entitlement_row.legal_entity_id OR entitlement_row.quantity<=0 THEN
        RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.leave_balance_allocation_scope_invalid';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER trg_hr_leave_balance_allocation_scope BEFORE INSERT ON business_hr.leave_balance_allocation
FOR EACH ROW EXECUTE FUNCTION business_hr.validate_leave_balance_allocation();
CREATE FUNCTION business_hr.protect_leave_balance_allocation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.leave_balance_allocation_immutable'; END $$;
CREATE TRIGGER trg_hr_leave_balance_allocation_immutable BEFORE UPDATE OR DELETE ON business_hr.leave_balance_allocation
FOR EACH ROW EXECUTE FUNCTION business_hr.protect_leave_balance_allocation();

CREATE TABLE business_hr.leave_request (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    employee_id uuid NOT NULL,
    employment_id uuid NOT NULL,
    leave_type_id uuid NOT NULL,
    policy_id uuid NOT NULL,
    requested_from date NOT NULL,
    requested_to date NOT NULL,
    charged_quantity numeric(12,4) NOT NULL CHECK (charged_quantity>0),
    reason text,
    status text NOT NULL CHECK (status IN ('Draft','Pending','Approved','Rejected','Cancelled','Taken')),
    version bigint NOT NULL DEFAULT 1 CHECK (version>0),
    idempotency_key text NOT NULL,
    submitted_at timestamptz,
    decided_by uuid,
    decided_at timestamptz,
    decision_reason text,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,id,employment_id,leave_type_id,legal_entity_id),
    UNIQUE (tenant_id,idempotency_key),
    FOREIGN KEY (tenant_id,employment_id,legal_entity_id) REFERENCES business_hr.employment(tenant_id,id,legal_entity_id),
    FOREIGN KEY (tenant_id,employee_id) REFERENCES business_hr.employee(tenant_id,id),
    FOREIGN KEY (tenant_id,leave_type_id,legal_entity_id) REFERENCES business_hr.leave_type(tenant_id,id,legal_entity_id),
    FOREIGN KEY (tenant_id,policy_id) REFERENCES business_hr.leave_policy(tenant_id,id),
    CHECK (requested_to>=requested_from), CHECK (length(idempotency_key) BETWEEN 8 AND 160), CHECK (reason IS NULL OR length(reason)<=1000),
    CHECK (decision_reason IS NULL OR length(decision_reason)<=500)
);
CREATE INDEX ix_hr_leave_request_employee ON business_hr.leave_request(tenant_id,legal_entity_id,employment_id,status,requested_from DESC,id);
CREATE INDEX ix_hr_leave_request_queue ON business_hr.leave_request(tenant_id,legal_entity_id,status,submitted_at,id);

CREATE TABLE business_hr.leave_request_event (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    leave_request_id uuid NOT NULL,
    event_type text NOT NULL CHECK (event_type IN ('Created','Submitted','Approved','Rejected','Cancelled','Taken')),
    from_status text,
    to_status text NOT NULL,
    actor_id uuid NOT NULL,
    reason text,
    occurred_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    FOREIGN KEY (tenant_id,leave_request_id) REFERENCES business_hr.leave_request(tenant_id,id),
    CHECK (reason IS NULL OR length(reason)<=1000)
);
CREATE INDEX ix_hr_leave_request_event_history ON business_hr.leave_request_event(tenant_id,leave_request_id,occurred_at,id);
CREATE FUNCTION business_hr.protect_leave_request_event() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.leave_request_event_immutable'; END $$;
CREATE TRIGGER trg_hr_leave_request_event_immutable BEFORE UPDATE OR DELETE ON business_hr.leave_request_event
FOR EACH ROW EXECUTE FUNCTION business_hr.protect_leave_request_event();

CREATE TABLE business_hr.leave_request_charge (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    leave_request_id uuid NOT NULL,
    employment_id uuid NOT NULL,
    leave_type_id uuid NOT NULL,
    charge_date date NOT NULL,
    quantity numeric(12,4) NOT NULL CHECK (quantity>0),
    unit text NOT NULL CHECK (unit IN ('Days','Hours')),
    policy_version integer NOT NULL CHECK (policy_version>0),
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,leave_request_id,charge_date),
    FOREIGN KEY (tenant_id,leave_request_id,employment_id,leave_type_id,legal_entity_id) REFERENCES business_hr.leave_request(tenant_id,id,employment_id,leave_type_id,legal_entity_id)
);
CREATE INDEX ix_hr_leave_request_charge_period ON business_hr.leave_request_charge(tenant_id,legal_entity_id,employment_id,charge_date,leave_request_id);
CREATE FUNCTION business_hr.protect_leave_request_charge() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.leave_request_charge_immutable'; END $$;
CREATE TRIGGER trg_hr_leave_request_charge_immutable BEFORE UPDATE OR DELETE ON business_hr.leave_request_charge
FOR EACH ROW EXECUTE FUNCTION business_hr.protect_leave_request_charge();

CREATE TABLE business_hr.onboarding_template (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    code text NOT NULL,
    name_ar text NOT NULL,
    name_en text,
    version integer NOT NULL CHECK (version>0),
    status text NOT NULL CHECK (status IN ('Active','Archived')),
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,id,legal_entity_id),
    UNIQUE (tenant_id,legal_entity_id,code,version),
    FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id),
    CHECK (length(code) BETWEEN 1 AND 60), CHECK (length(name_ar) BETWEEN 1 AND 160)
);
CREATE TABLE business_hr.onboarding_template_task (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    template_id uuid NOT NULL,
    code text NOT NULL,
    title_ar text NOT NULL,
    title_en text,
    is_required boolean NOT NULL,
    due_days_from_start integer CHECK (due_days_from_start IS NULL OR due_days_from_start BETWEEN -90 AND 365),
    sort_order smallint NOT NULL CHECK (sort_order>=0),
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,template_id,code),
    FOREIGN KEY (tenant_id,template_id) REFERENCES business_hr.onboarding_template(tenant_id,id),
    CHECK (length(code) BETWEEN 1 AND 60), CHECK (length(title_ar) BETWEEN 1 AND 160)
);
CREATE TABLE business_hr.onboarding_case (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    employee_id uuid NOT NULL,
    employment_id uuid NOT NULL,
    template_id uuid,
    target_start_date date NOT NULL,
    status text NOT NULL CHECK (status IN ('Open','Completed','Cancelled')),
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    completed_at timestamptz,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,employment_id),
    FOREIGN KEY (tenant_id,employment_id,legal_entity_id) REFERENCES business_hr.employment(tenant_id,id,legal_entity_id),
    FOREIGN KEY (tenant_id,employee_id) REFERENCES business_hr.employee(tenant_id,id),
    FOREIGN KEY (tenant_id,template_id,legal_entity_id) REFERENCES business_hr.onboarding_template(tenant_id,id,legal_entity_id)
);
CREATE INDEX ix_hr_onboarding_case_queue ON business_hr.onboarding_case(tenant_id,legal_entity_id,status,target_start_date,id);
CREATE TABLE business_hr.onboarding_task (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    case_id uuid NOT NULL,
    code text NOT NULL,
    title_ar text NOT NULL,
    title_en text,
    is_required boolean NOT NULL,
    due_date date,
    assigned_membership_id uuid,
    status text NOT NULL CHECK (status IN ('Pending','Completed','Skipped')),
    version bigint NOT NULL DEFAULT 1 CHECK (version>0),
    completed_by uuid,
    completed_at timestamptz,
    completion_note text,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,case_id,code),
    FOREIGN KEY (tenant_id,case_id) REFERENCES business_hr.onboarding_case(tenant_id,id),
    CHECK (length(code) BETWEEN 1 AND 60), CHECK (length(title_ar) BETWEEN 1 AND 160), CHECK (completion_note IS NULL OR length(completion_note)<=500)
);
CREATE INDEX ix_hr_onboarding_task_queue ON business_hr.onboarding_task(tenant_id,legal_entity_id,status,due_date,case_id);
CREATE TABLE business_hr.onboarding_task_event (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    task_id uuid NOT NULL,
    event_type text NOT NULL CHECK (event_type IN ('Created','Completed','Skipped')),
    from_status text,
    to_status text NOT NULL,
    actor_id uuid NOT NULL,
    note text,
    occurred_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    FOREIGN KEY (tenant_id,task_id) REFERENCES business_hr.onboarding_task(tenant_id,id)
);
CREATE FUNCTION business_hr.protect_onboarding_task_event() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.onboarding_task_event_immutable'; END $$;
CREATE TRIGGER trg_hr_onboarding_task_event_immutable BEFORE UPDATE OR DELETE ON business_hr.onboarding_task_event
FOR EACH ROW EXECUTE FUNCTION business_hr.protect_onboarding_task_event();

CREATE TABLE business_hr.attendance_payroll_lock (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    payroll_run_id uuid NOT NULL,
    payroll_snapshot_id uuid NOT NULL,
    payroll_reference text NOT NULL,
    period_start date NOT NULL,
    period_end date NOT NULL,
    locked_record_count integer NOT NULL CHECK (locked_record_count>=0),
    locked_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,payroll_run_id),
    UNIQUE (tenant_id,legal_entity_id,period_start,period_end),
    FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id),
    FOREIGN KEY (tenant_id,payroll_snapshot_id) REFERENCES business_hr.payroll_period_snapshot(tenant_id,id),
    CHECK (period_end>=period_start), CHECK (length(payroll_reference) BETWEEN 1 AND 120)
);
CREATE INDEX ix_hr_attendance_lock_period ON business_hr.attendance_payroll_lock(tenant_id,legal_entity_id,period_start DESC,locked_at DESC);
CREATE FUNCTION business_hr.protect_attendance_payroll_lock() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.attendance_payroll_lock_immutable';
END $$;
CREATE TRIGGER trg_hr_attendance_payroll_lock_immutable BEFORE UPDATE OR DELETE ON business_hr.attendance_payroll_lock
FOR EACH ROW EXECUTE FUNCTION business_hr.protect_attendance_payroll_lock();

CREATE TABLE business_hr.attendance_payroll_correction_delta (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    employment_id uuid NOT NULL,
    attendance_record_id uuid NOT NULL,
    correction_id uuid NOT NULL,
    payroll_snapshot_id uuid NOT NULL,
    payroll_reference text NOT NULL,
    before_snapshot jsonb NOT NULL,
    proposed_snapshot jsonb NOT NULL,
    reason_code text NOT NULL,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,correction_id),
    FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id),
    FOREIGN KEY (tenant_id,employment_id,legal_entity_id) REFERENCES business_hr.employment(tenant_id,id,legal_entity_id),
    FOREIGN KEY (tenant_id,attendance_record_id) REFERENCES business_hr.attendance_record(tenant_id,id),
    FOREIGN KEY (tenant_id,correction_id) REFERENCES business_hr.attendance_correction(tenant_id,id),
    FOREIGN KEY (tenant_id,payroll_snapshot_id) REFERENCES business_hr.payroll_period_snapshot(tenant_id,id),
    CHECK (length(payroll_reference) BETWEEN 1 AND 120),
    CHECK (length(reason_code) BETWEEN 1 AND 80)
);
CREATE INDEX ix_hr_attendance_payroll_delta_snapshot ON business_hr.attendance_payroll_correction_delta(tenant_id,payroll_snapshot_id,created_at,id);
CREATE FUNCTION business_hr.protect_attendance_payroll_delta() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.attendance_payroll_delta_immutable'; END $$;
CREATE TRIGGER trg_hr_attendance_payroll_delta_immutable BEFORE UPDATE OR DELETE ON business_hr.attendance_payroll_correction_delta
FOR EACH ROW EXECUTE FUNCTION business_hr.protect_attendance_payroll_delta();

CREATE TABLE business_hr.employment_event (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    employee_id uuid NOT NULL,
    employment_id uuid,
    event_type text NOT NULL,
    effective_on date NOT NULL,
    actor_id uuid NOT NULL,
    payload jsonb NOT NULL,
    correlation_id uuid NOT NULL,
    occurred_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    FOREIGN KEY (tenant_id, employee_id) REFERENCES business_hr.employee(tenant_id, id),
    FOREIGN KEY (tenant_id, employment_id, employee_id) REFERENCES business_hr.employment(tenant_id, id, employee_id),
    CHECK (length(event_type) BETWEEN 1 AND 80)
);
CREATE INDEX ix_hr_employment_event_timeline ON business_hr.employment_event(tenant_id, employee_id, effective_on DESC, occurred_at DESC, id);

CREATE TABLE business_hr.employee_timeline_projection (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    employee_id uuid NOT NULL,
    employment_id uuid,
    event_type text NOT NULL,
    effective_on date NOT NULL,
    occurred_at timestamptz NOT NULL,
    payload jsonb NOT NULL,
    PRIMARY KEY (tenant_id, id),
    FOREIGN KEY (tenant_id, id) REFERENCES business_hr.employment_event(tenant_id, id) ON DELETE CASCADE,
    FOREIGN KEY (tenant_id, employee_id) REFERENCES business_hr.employee(tenant_id, id),
    FOREIGN KEY (tenant_id, employment_id, employee_id) REFERENCES business_hr.employment(tenant_id, id, employee_id)
);
CREATE INDEX ix_hr_timeline_projection ON business_hr.employee_timeline_projection(tenant_id, employee_id, effective_on DESC, occurred_at DESC, id DESC);

CREATE TABLE business_hr.employee_list_projection (
    tenant_id uuid NOT NULL,
    employee_id uuid NOT NULL,
    display_name text NOT NULL,
    display_name_normalized text NOT NULL,
    arabic_name_normalized text NOT NULL,
    english_name text,
    english_name_normalized text,
    status text NOT NULL,
    projected_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, employee_id),
    FOREIGN KEY (tenant_id, employee_id) REFERENCES business_hr.employee(tenant_id, id) ON DELETE CASCADE
);
CREATE INDEX ix_hr_employee_projection_search ON business_hr.employee_list_projection USING gin (display_name_normalized public.gin_trgm_ops);
CREATE INDEX ix_hr_employee_projection_ar ON business_hr.employee_list_projection USING gin (arabic_name_normalized public.gin_trgm_ops);
CREATE INDEX ix_hr_employee_projection_en ON business_hr.employee_list_projection USING gin (english_name_normalized public.gin_trgm_ops) WHERE english_name_normalized IS NOT NULL;
CREATE INDEX ix_hr_employee_projection_page ON business_hr.employee_list_projection(tenant_id,display_name_normalized,employee_id);
CREATE INDEX ix_hr_employment_number_search ON business_hr.employment USING gin (lower(employment_number) public.gin_trgm_ops);

DO $$
DECLARE table_name text;
BEGIN
    FOREACH table_name IN ARRAY ARRAY['employee','employment','employee_import_reference','department','position','work_calendar','work_calendar_weekly_pattern','work_calendar_holiday','shift_definition','shift_assignment','employment_assignment','employment_contract','employment_contract_amendment','identifier_lookup_key','employee_identifier','employee_document','employee_qualification','employee_credential','employee_insurance_enrollment','employment_compensation_agreement','hr_adjustment','attendance_punch','attendance_record','attendance_exception','attendance_correction','attendance_overtime_request','attendance_overtime_event','payroll_period_snapshot','attendance_payroll_lock','attendance_payroll_correction_delta','leave_type','leave_policy','leave_balance_movement','leave_balance_allocation','leave_request','leave_request_event','leave_request_charge','onboarding_template','onboarding_template_task','onboarding_case','onboarding_task','onboarding_task_event','employment_event','employee_timeline_projection','employee_list_projection'] LOOP
        EXECUTE format('ALTER TABLE business_hr.%I ENABLE ROW LEVEL SECURITY', table_name);
        EXECUTE format('ALTER TABLE business_hr.%I FORCE ROW LEVEL SECURITY', table_name);
        EXECUTE format('CREATE POLICY tenant_isolation ON business_hr.%I USING (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid) WITH CHECK (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid)', table_name);
    END LOOP;
END $$;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA business_hr TO erp_app;
REVOKE UPDATE, DELETE ON business_hr.employment_event FROM erp_app;
REVOKE UPDATE, DELETE ON business_hr.employee_import_reference FROM erp_app;
REVOKE UPDATE, DELETE ON business_hr.employment_contract_amendment FROM erp_app;
REVOKE DELETE ON business_hr.employee_identifier FROM erp_app;
REVOKE DELETE ON business_hr.employee_document FROM erp_app;
REVOKE DELETE, UPDATE ON business_hr.employee_qualification FROM erp_app;
REVOKE DELETE ON business_hr.employee_credential FROM erp_app;
REVOKE DELETE ON business_hr.employee_insurance_enrollment FROM erp_app;
REVOKE UPDATE, DELETE ON business_hr.employee_insurance_enrollment FROM erp_app;
REVOKE DELETE ON business_hr.employment_compensation_agreement FROM erp_app;
REVOKE UPDATE, DELETE ON business_hr.attendance_punch FROM erp_app;
REVOKE UPDATE, DELETE ON business_hr.attendance_correction FROM erp_app;
REVOKE DELETE ON business_hr.attendance_overtime_event FROM erp_app;
REVOKE UPDATE, DELETE ON business_hr.payroll_period_snapshot FROM erp_app;
REVOKE UPDATE, DELETE ON business_hr.attendance_payroll_lock FROM erp_app;
REVOKE UPDATE, DELETE ON business_hr.attendance_payroll_correction_delta FROM erp_app;
REVOKE DELETE ON business_hr.leave_type FROM erp_app;
REVOKE UPDATE, DELETE ON business_hr.leave_balance_movement FROM erp_app;
REVOKE UPDATE, DELETE ON business_hr.leave_balance_allocation FROM erp_app;
REVOKE DELETE ON business_hr.leave_request_event FROM erp_app;
REVOKE UPDATE, DELETE ON business_hr.leave_request_charge FROM erp_app;
REVOKE DELETE ON business_hr.onboarding_template_task FROM erp_app;
REVOKE DELETE ON business_hr.onboarding_task_event FROM erp_app;
REVOKE DELETE ON business_hr.hr_adjustment FROM erp_app;
REVOKE UPDATE, DELETE ON business_hr.work_calendar_weekly_pattern FROM erp_app;
REVOKE DELETE ON business_hr.work_calendar_holiday FROM erp_app;
REVOKE DELETE ON business_hr.shift_assignment FROM erp_app;
REVOKE UPDATE, DELETE ON business_hr.identifier_lookup_key FROM erp_app;
GRANT EXECUTE ON FUNCTION business_hr.enforce_assignment_non_overlap() TO erp_app;
GRANT EXECUTE ON FUNCTION business_hr.enforce_department_acyclic() TO erp_app;
GRANT EXECUTE ON FUNCTION business_hr.enforce_manager_acyclic() TO erp_app;
