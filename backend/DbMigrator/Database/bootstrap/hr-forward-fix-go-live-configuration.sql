CREATE TABLE business_hr.legal_entity_go_live_configuration (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    go_live_date date NOT NULL,
    employee_migration_strategy text NOT NULL CHECK (employee_migration_strategy IN ('ActiveOnly','ActiveAndHistorical')),
    attendance_history_boundary date,
    leave_opening_boundary date,
    document_migration_strategy text NOT NULL CHECK (document_migration_strategy IN ('RequiredOnly','AllAvailable')),
    reason text NOT NULL,
    updated_by uuid NOT NULL,
    updated_at timestamptz NOT NULL,
    version bigint NOT NULL CHECK (version > 0),
    PRIMARY KEY (tenant_id,legal_entity_id),
    FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id),
    CHECK (attendance_history_boundary IS NULL OR attendance_history_boundary <= go_live_date),
    CHECK (leave_opening_boundary IS NULL OR leave_opening_boundary <= go_live_date),
    CHECK (length(reason) BETWEEN 1 AND 500)
);
ALTER TABLE business_hr.legal_entity_go_live_configuration ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_hr.legal_entity_go_live_configuration FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON business_hr.legal_entity_go_live_configuration
    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
GRANT SELECT, INSERT, UPDATE ON business_hr.legal_entity_go_live_configuration TO erp_app;
REVOKE DELETE ON business_hr.legal_entity_go_live_configuration FROM erp_app;
CREATE FUNCTION business_hr.protect_go_live_configuration_version() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.tenant_id<>OLD.tenant_id OR NEW.legal_entity_id<>OLD.legal_entity_id OR NEW.version<>OLD.version+1
       OR NEW.updated_at<OLD.updated_at OR length(trim(NEW.reason))=0 THEN
        RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.go_live_configuration_transition_invalid';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER trg_hr_go_live_configuration_version BEFORE UPDATE ON business_hr.legal_entity_go_live_configuration
FOR EACH ROW EXECUTE FUNCTION business_hr.protect_go_live_configuration_version();
