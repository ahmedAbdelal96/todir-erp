CREATE INDEX IF NOT EXISTS ix_hr_attendance_daily_page
    ON business_hr.attendance_record(tenant_id, legal_entity_id, work_date DESC, employee_id, id);
