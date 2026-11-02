CREATE TABLE business_hr.hr_reminder (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    employee_id uuid NOT NULL,
    employment_id uuid,
    reminder_type text NOT NULL CHECK (reminder_type IN ('ContractExpiry','DocumentExpiry','CredentialExpiry','ProbationEnd','InsuranceExpiry','OnboardingTaskDue','OffboardingTaskDue')),
    source_type text NOT NULL,
    source_id uuid NOT NULL,
    due_date date NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,legal_entity_id,reminder_type,source_id,due_date),
    FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id),
    FOREIGN KEY (tenant_id,employee_id) REFERENCES business_hr.employee(tenant_id,id),
    FOREIGN KEY (tenant_id,employment_id,legal_entity_id) REFERENCES business_hr.employment(tenant_id,id,legal_entity_id),
    CHECK (length(source_type) BETWEEN 1 AND 40)
);
CREATE INDEX ix_hr_reminder_inbox ON business_hr.hr_reminder(tenant_id,legal_entity_id,due_date,id);
ALTER TABLE business_hr.hr_reminder ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_hr.hr_reminder FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON business_hr.hr_reminder
    USING (tenant_id=NULLIF(current_setting('app.tenant_id',true),'')::uuid)
    WITH CHECK (tenant_id=NULLIF(current_setting('app.tenant_id',true),'')::uuid);
GRANT SELECT,INSERT ON business_hr.hr_reminder TO erp_app;
REVOKE UPDATE,DELETE ON business_hr.hr_reminder FROM erp_app;

CREATE FUNCTION business_hr.list_hr_reminder_scopes() RETURNS TABLE(tenant_id uuid,legal_entity_id uuid)
LANGUAGE plpgsql SECURITY DEFINER
SET search_path TO 'business_hr','pg_catalog'
AS $$
BEGIN
    RETURN QUERY SELECT DISTINCT scope.tenant_id,scope.legal_entity_id FROM (
        SELECT e.tenant_id,e.legal_entity_id FROM business_hr.employment e WHERE e.status IN ('PreHire','Active','Suspended','NoticePeriod')
        UNION ALL SELECT c.tenant_id,c.legal_entity_id FROM business_hr.employment_contract c WHERE c.status='Active' AND (c.end_date IS NOT NULL OR c.probation_end IS NOT NULL)
        UNION ALL SELECT d.tenant_id,em.legal_entity_id FROM business_hr.employee_document d JOIN business_hr.employment em ON em.tenant_id=d.tenant_id AND em.employee_id=d.employee_id WHERE d.status='Active' AND d.expiry_date IS NOT NULL AND em.status IN ('PreHire','Active','Suspended','NoticePeriod')
        UNION ALL SELECT cr.tenant_id,em.legal_entity_id FROM business_hr.employee_credential cr JOIN business_hr.employment em ON em.tenant_id=cr.tenant_id AND em.employee_id=cr.employee_id WHERE cr.status='Active' AND cr.expiry_date IS NOT NULL AND em.status IN ('PreHire','Active','Suspended','NoticePeriod')
        UNION ALL SELECT i.tenant_id,i.legal_entity_id FROM business_hr.employee_insurance_enrollment i WHERE i.status='Active' AND i.effective_to IS NOT NULL
        UNION ALL SELECT c.tenant_id,c.legal_entity_id FROM business_hr.onboarding_task t JOIN business_hr.onboarding_case c ON c.tenant_id=t.tenant_id AND c.id=t.case_id WHERE t.status='Pending' AND t.due_date IS NOT NULL AND c.status='Open'
        UNION ALL SELECT c.tenant_id,c.legal_entity_id FROM business_hr.offboarding_task t JOIN business_hr.offboarding_case c ON c.tenant_id=t.tenant_id AND c.id=t.case_id WHERE t.status='Pending' AND t.due_date IS NOT NULL AND c.status='Open'
    ) scope;
END;
$$;
REVOKE ALL ON FUNCTION business_hr.list_hr_reminder_scopes() FROM PUBLIC;
GRANT EXECUTE ON FUNCTION business_hr.list_hr_reminder_scopes() TO erp_app;
