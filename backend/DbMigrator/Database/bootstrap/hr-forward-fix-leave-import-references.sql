CREATE TABLE business_hr.leave_import_reference (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    source_system text NOT NULL CHECK (length(source_system) BETWEEN 1 AND 80),
    batch_reference text NOT NULL CHECK (length(batch_reference) BETWEEN 1 AND 160),
    file_sha256 text NOT NULL CHECK (file_sha256 ~ '^[0-9a-f]{64}$'),
    row_reference text NOT NULL CHECK (length(row_reference) BETWEEN 1 AND 160),
    row_payload_sha256 text NOT NULL CHECK (row_payload_sha256 ~ '^[0-9a-f]{64}$'),
    employment_id uuid NOT NULL,
    leave_type_id uuid NOT NULL,
    movement_id uuid NOT NULL,
    imported_by uuid NOT NULL,
    imported_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,source_system,batch_reference,row_reference),
    FOREIGN KEY (tenant_id,movement_id) REFERENCES business_hr.leave_balance_movement(tenant_id,id),
    FOREIGN KEY (tenant_id,employment_id) REFERENCES business_hr.employment(tenant_id,id),
    FOREIGN KEY (tenant_id,leave_type_id) REFERENCES business_hr.leave_type(tenant_id,id)
);
CREATE INDEX ix_hr_leave_import_movement ON business_hr.leave_import_reference(tenant_id,movement_id);
CREATE UNIQUE INDEX ux_hr_leave_opening_balance ON business_hr.leave_balance_movement(tenant_id,employment_id,leave_type_id) WHERE movement_type='Opening';
ALTER TABLE business_hr.leave_import_reference ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_hr.leave_import_reference FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON business_hr.leave_import_reference
    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
GRANT SELECT, INSERT ON business_hr.leave_import_reference TO erp_app;
REVOKE UPDATE, DELETE ON business_hr.leave_import_reference FROM erp_app;
