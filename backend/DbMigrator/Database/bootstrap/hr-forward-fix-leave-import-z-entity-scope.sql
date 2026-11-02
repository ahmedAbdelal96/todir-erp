ALTER TABLE business_hr.leave_import_reference NO FORCE ROW LEVEL SECURITY;
ALTER TABLE business_hr.leave_balance_movement NO FORCE ROW LEVEL SECURITY;
ALTER TABLE business_hr.employment NO FORCE ROW LEVEL SECURITY;
ALTER TABLE business_hr.leave_type NO FORCE ROW LEVEL SECURITY;
ALTER TABLE business_hr.leave_import_reference
    ADD COLUMN IF NOT EXISTS legal_entity_id uuid;
UPDATE business_hr.leave_import_reference reference
SET legal_entity_id=movement.legal_entity_id
FROM business_hr.leave_balance_movement movement
WHERE movement.tenant_id=reference.tenant_id AND movement.id=reference.movement_id AND reference.legal_entity_id IS NULL;
ALTER TABLE business_hr.leave_import_reference
    DROP CONSTRAINT IF EXISTS fk_hr_leave_import_employment_entity,
    DROP CONSTRAINT IF EXISTS fk_hr_leave_import_leave_type_entity;
ALTER TABLE business_hr.leave_import_reference
    ALTER COLUMN legal_entity_id SET NOT NULL,
    ADD CONSTRAINT fk_hr_leave_import_employment_entity FOREIGN KEY (tenant_id,employment_id,legal_entity_id)
        REFERENCES business_hr.employment(tenant_id,id,legal_entity_id),
    ADD CONSTRAINT fk_hr_leave_import_leave_type_entity FOREIGN KEY (tenant_id,leave_type_id,legal_entity_id)
        REFERENCES business_hr.leave_type(tenant_id,id,legal_entity_id);
ALTER TABLE business_hr.leave_import_reference FORCE ROW LEVEL SECURITY;
ALTER TABLE business_hr.leave_balance_movement FORCE ROW LEVEL SECURITY;
ALTER TABLE business_hr.employment FORCE ROW LEVEL SECURITY;
ALTER TABLE business_hr.leave_type FORCE ROW LEVEL SECURITY;
