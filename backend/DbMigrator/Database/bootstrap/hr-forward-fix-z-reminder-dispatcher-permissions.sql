GRANT USAGE ON SCHEMA business_hr TO erp_dispatcher;
GRANT SELECT (tenant_id,legal_entity_id,id,employee_id,status) ON business_hr.employment TO erp_dispatcher;
GRANT SELECT (tenant_id,legal_entity_id,id,employment_id,status,end_date,probation_end) ON business_hr.employment_contract TO erp_dispatcher;
GRANT SELECT (tenant_id,employee_id,status,expiry_date) ON business_hr.employee_document,business_hr.employee_credential TO erp_dispatcher;
GRANT SELECT (tenant_id,legal_entity_id,employment_id,status,effective_to) ON business_hr.employee_insurance_enrollment TO erp_dispatcher;
GRANT SELECT (tenant_id,legal_entity_id,case_id,status,due_date) ON business_hr.onboarding_task,business_hr.offboarding_task TO erp_dispatcher;
GRANT SELECT (tenant_id,id,legal_entity_id,employee_id,employment_id,status) ON business_hr.onboarding_case,business_hr.offboarding_case TO erp_dispatcher;
