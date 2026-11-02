CREATE TABLE business_hr.offboarding_case (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    employee_id uuid NOT NULL,
    employment_id uuid NOT NULL,
    reason_type text NOT NULL CHECK (reason_type IN ('Resignation','Termination','ContractExpiry','Retirement','TemporaryAssignmentEnd','Other')),
    reason_code text NOT NULL,
    notice_date date,
    last_working_date date NOT NULL,
    effective_end_date date NOT NULL,
    access_deactivation_on date NOT NULL,
    final_payroll_required boolean NOT NULL,
    status text NOT NULL CHECK (status IN ('Open','Completed')),
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    completed_at timestamptz,
    version bigint NOT NULL CHECK (version > 0),
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,employment_id),
    FOREIGN KEY (tenant_id,employment_id,legal_entity_id) REFERENCES business_hr.employment(tenant_id,id,legal_entity_id),
    FOREIGN KEY (tenant_id,employee_id) REFERENCES business_hr.employee(tenant_id,id),
    CHECK (length(trim(reason_code)) BETWEEN 1 AND 80),
    CHECK (last_working_date <= effective_end_date),
    CHECK (notice_date IS NULL OR notice_date <= last_working_date),
    CHECK ((status='Open' AND completed_at IS NULL) OR (status='Completed' AND completed_at IS NOT NULL))
);
CREATE INDEX ix_hr_offboarding_queue ON business_hr.offboarding_case(tenant_id,legal_entity_id,status,last_working_date,id);
CREATE FUNCTION business_hr.protect_offboarding_case_transition() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.tenant_id<>OLD.tenant_id OR NEW.id<>OLD.id OR NEW.legal_entity_id<>OLD.legal_entity_id OR NEW.employee_id<>OLD.employee_id
       OR NEW.employment_id<>OLD.employment_id OR NEW.reason_type<>OLD.reason_type OR NEW.reason_code<>OLD.reason_code
       OR NEW.notice_date IS DISTINCT FROM OLD.notice_date OR NEW.last_working_date<>OLD.last_working_date
       OR NEW.effective_end_date<>OLD.effective_end_date OR NEW.access_deactivation_on<>OLD.access_deactivation_on
       OR NEW.final_payroll_required<>OLD.final_payroll_required OR NEW.created_by<>OLD.created_by OR NEW.created_at<>OLD.created_at
       OR OLD.status<>'Open' OR NEW.status<>'Completed' OR NEW.version<>OLD.version+1 OR NEW.completed_at IS NULL THEN
        RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.offboarding_case_transition_invalid';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER trg_hr_offboarding_case_transition BEFORE UPDATE ON business_hr.offboarding_case
FOR EACH ROW EXECUTE FUNCTION business_hr.protect_offboarding_case_transition();

CREATE TABLE business_hr.offboarding_task (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    case_id uuid NOT NULL,
    code text NOT NULL,
    title_ar text NOT NULL,
    due_date date,
    external_reference text,
    status text NOT NULL CHECK (status IN ('Pending','Completed','Skipped')),
    completion_note text,
    completed_by uuid,
    completed_at timestamptz,
    version bigint NOT NULL CHECK (version > 0),
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,case_id,code),
    FOREIGN KEY (tenant_id,case_id) REFERENCES business_hr.offboarding_case(tenant_id,id),
    FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id),
    CHECK (code ~ '^[A-Z][A-Z0-9_]{1,59}$'),
    CHECK (length(trim(title_ar)) BETWEEN 1 AND 160),
    CHECK (external_reference IS NULL OR length(external_reference) BETWEEN 1 AND 160),
    CHECK (completion_note IS NULL OR length(completion_note)<=500),
    CHECK ((status='Pending' AND completed_at IS NULL AND completed_by IS NULL) OR (status<>'Pending' AND completed_at IS NOT NULL AND completed_by IS NOT NULL))
);
CREATE INDEX ix_hr_offboarding_task_queue ON business_hr.offboarding_task(tenant_id,legal_entity_id,status,due_date,case_id);
CREATE FUNCTION business_hr.protect_offboarding_task_transition() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.tenant_id<>OLD.tenant_id OR NEW.id<>OLD.id OR NEW.legal_entity_id<>OLD.legal_entity_id OR NEW.case_id<>OLD.case_id
       OR NEW.code<>OLD.code OR NEW.title_ar<>OLD.title_ar OR NEW.due_date IS DISTINCT FROM OLD.due_date
       OR NEW.external_reference IS DISTINCT FROM OLD.external_reference OR OLD.status<>'Pending' OR NEW.status<>'Completed'
       OR NEW.version<>OLD.version+1 OR NEW.completed_by IS NULL OR NEW.completed_at IS NULL THEN
        RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.offboarding_task_transition_invalid';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER trg_hr_offboarding_task_transition BEFORE UPDATE ON business_hr.offboarding_task
FOR EACH ROW EXECUTE FUNCTION business_hr.protect_offboarding_task_transition();

ALTER TABLE business_hr.offboarding_case ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_hr.offboarding_case FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON business_hr.offboarding_case
    USING (tenant_id=NULLIF(current_setting('app.tenant_id',true),'')::uuid)
    WITH CHECK (tenant_id=NULLIF(current_setting('app.tenant_id',true),'')::uuid);
ALTER TABLE business_hr.offboarding_task ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_hr.offboarding_task FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON business_hr.offboarding_task
    USING (tenant_id=NULLIF(current_setting('app.tenant_id',true),'')::uuid)
    WITH CHECK (tenant_id=NULLIF(current_setting('app.tenant_id',true),'')::uuid);
GRANT SELECT,INSERT,UPDATE ON business_hr.offboarding_case,business_hr.offboarding_task TO erp_app;
REVOKE DELETE ON business_hr.offboarding_case,business_hr.offboarding_task FROM erp_app;
