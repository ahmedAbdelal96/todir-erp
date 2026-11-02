ALTER TABLE business_hr.leave_balance_movement
    ADD COLUMN IF NOT EXISTS expires_on date;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='business_hr.leave_balance_movement'::regclass AND conname='ck_hr_leave_movement_expiry_source') THEN
        ALTER TABLE business_hr.leave_balance_movement ADD CONSTRAINT ck_hr_leave_movement_expiry_source
            CHECK (expires_on IS NULL OR (movement_type='CarryOver' AND quantity>0));
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_hr_leave_entitlement_expiry
    ON business_hr.leave_balance_movement(tenant_id,employment_id,leave_type_id,expires_on,id)
    WHERE movement_type='CarryOver' AND quantity>0 AND expires_on IS NOT NULL;

CREATE TABLE IF NOT EXISTS business_hr.leave_balance_allocation (
    tenant_id uuid NOT NULL,
    movement_id uuid NOT NULL,
    entitlement_movement_id uuid NOT NULL,
    PRIMARY KEY (tenant_id,movement_id),
    FOREIGN KEY (tenant_id,movement_id) REFERENCES business_hr.leave_balance_movement(tenant_id,id),
    FOREIGN KEY (tenant_id,entitlement_movement_id) REFERENCES business_hr.leave_balance_movement(tenant_id,id),
    CHECK (movement_id<>entitlement_movement_id)
);
CREATE INDEX IF NOT EXISTS ix_hr_leave_allocation_entitlement
    ON business_hr.leave_balance_allocation(tenant_id,entitlement_movement_id,movement_id);

CREATE OR REPLACE FUNCTION business_hr.validate_leave_balance_allocation() RETURNS trigger LANGUAGE plpgsql AS $$
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
DROP TRIGGER IF EXISTS trg_hr_leave_balance_allocation_scope ON business_hr.leave_balance_allocation;
CREATE TRIGGER trg_hr_leave_balance_allocation_scope BEFORE INSERT ON business_hr.leave_balance_allocation
FOR EACH ROW EXECUTE FUNCTION business_hr.validate_leave_balance_allocation();

CREATE OR REPLACE FUNCTION business_hr.protect_leave_balance_allocation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.leave_balance_allocation_immutable'; END $$;
DROP TRIGGER IF EXISTS trg_hr_leave_balance_allocation_immutable ON business_hr.leave_balance_allocation;
CREATE TRIGGER trg_hr_leave_balance_allocation_immutable BEFORE UPDATE OR DELETE ON business_hr.leave_balance_allocation
FOR EACH ROW EXECUTE FUNCTION business_hr.protect_leave_balance_allocation();

ALTER TABLE business_hr.leave_balance_allocation ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_hr.leave_balance_allocation FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON business_hr.leave_balance_allocation;
CREATE POLICY tenant_isolation ON business_hr.leave_balance_allocation
    USING (tenant_id=NULLIF(current_setting('app.tenant_id',true),'')::uuid)
    WITH CHECK (tenant_id=NULLIF(current_setting('app.tenant_id',true),'')::uuid);
GRANT SELECT,INSERT ON business_hr.leave_balance_allocation TO erp_app;
REVOKE UPDATE,DELETE ON business_hr.leave_balance_allocation FROM erp_app;
