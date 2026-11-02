ALTER TABLE business_hr.offboarding_case
    ADD COLUMN target_status text NOT NULL DEFAULT 'Terminated' CHECK (target_status IN ('Terminated','Ended')),
    ADD COLUMN termination_status text NOT NULL DEFAULT 'Effective' CHECK (termination_status IN ('Scheduled','Effective')),
    ADD COLUMN termination_applied_at timestamptz DEFAULT now();

CREATE INDEX ix_hr_offboarding_effective_queue
    ON business_hr.offboarding_case(tenant_id,legal_entity_id,effective_end_date,id)
    WHERE termination_status='Scheduled';

CREATE OR REPLACE FUNCTION business_hr.protect_offboarding_case_transition() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE
    tasks_transition boolean := OLD.status='Open' AND NEW.status='Completed'
        AND NEW.completed_at IS NOT NULL AND OLD.completed_at IS NULL;
    termination_transition boolean := OLD.termination_status='Scheduled' AND NEW.termination_status='Effective'
        AND OLD.termination_applied_at IS NULL AND NEW.termination_applied_at IS NOT NULL;
BEGIN
    IF NEW.tenant_id<>OLD.tenant_id OR NEW.id<>OLD.id OR NEW.legal_entity_id<>OLD.legal_entity_id OR NEW.employee_id<>OLD.employee_id
       OR NEW.employment_id<>OLD.employment_id OR NEW.reason_type<>OLD.reason_type OR NEW.reason_code<>OLD.reason_code
       OR NEW.notice_date IS DISTINCT FROM OLD.notice_date OR NEW.last_working_date<>OLD.last_working_date
       OR NEW.effective_end_date<>OLD.effective_end_date OR NEW.access_deactivation_on<>OLD.access_deactivation_on
       OR NEW.final_payroll_required<>OLD.final_payroll_required OR NEW.target_status<>OLD.target_status
       OR NEW.created_by<>OLD.created_by OR NEW.created_at<>OLD.created_at OR NEW.version<>OLD.version+1
       OR (NEW.status IS DISTINCT FROM OLD.status AND NOT tasks_transition)
       OR (NEW.termination_status IS DISTINCT FROM OLD.termination_status AND NOT termination_transition)
       OR (NEW.completed_at IS DISTINCT FROM OLD.completed_at AND NOT tasks_transition)
       OR (NEW.termination_applied_at IS DISTINCT FROM OLD.termination_applied_at AND NOT termination_transition)
       OR (NOT tasks_transition AND NOT termination_transition) THEN
        RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.offboarding_case_transition_invalid';
    END IF;
    RETURN NEW;
END $$;
