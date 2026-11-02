ALTER TABLE business_hr.hr_reminder DROP CONSTRAINT hr_reminder_reminder_type_check;
ALTER TABLE business_hr.hr_reminder ADD CONSTRAINT hr_reminder_reminder_type_check
    CHECK (reminder_type IN ('ContractExpiry','DocumentExpiry','CredentialExpiry','ProbationEnd','InsuranceExpiry','OnboardingTaskDue','OffboardingTaskDue','LeaveBalanceExpiry'));
