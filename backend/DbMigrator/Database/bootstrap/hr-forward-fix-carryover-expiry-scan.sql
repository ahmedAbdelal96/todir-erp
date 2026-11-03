CREATE INDEX IF NOT EXISTS ix_hr_leave_carryover_expiry_scan
    ON business_hr.leave_balance_movement(tenant_id, legal_entity_id, expires_on, employment_id, leave_type_id, id)
    WHERE movement_type='CarryOver' AND quantity>0 AND expires_on IS NOT NULL;
