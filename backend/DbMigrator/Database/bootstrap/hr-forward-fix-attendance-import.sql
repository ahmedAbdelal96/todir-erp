CREATE TABLE business_hr.attendance_import_reference (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    employee_id uuid NOT NULL,
    employment_id uuid NOT NULL,
    punch_id uuid NOT NULL,
    source_system text NOT NULL,
    source_reference text NOT NULL,
    batch_reference text NOT NULL,
    row_reference text NOT NULL,
    file_sha256 char(64) NOT NULL,
    payload_sha256 char(64) NOT NULL,
    imported_at timestamptz NOT NULL,
    imported_by uuid NOT NULL,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,source_system,source_reference),
    UNIQUE (tenant_id,source_system,batch_reference,row_reference),
    UNIQUE (tenant_id,punch_id),
    FOREIGN KEY (tenant_id,employment_id,legal_entity_id) REFERENCES business_hr.employment(tenant_id,id,legal_entity_id),
    FOREIGN KEY (tenant_id,employment_id,employee_id) REFERENCES business_hr.employment(tenant_id,id,employee_id),
    FOREIGN KEY (tenant_id,punch_id) REFERENCES business_hr.attendance_punch(tenant_id,id),
    CHECK (length(source_system) BETWEEN 1 AND 80),
    CHECK (length(source_reference) BETWEEN 1 AND 160),
    CHECK (length(batch_reference) BETWEEN 1 AND 160),
    CHECK (length(row_reference) BETWEEN 1 AND 160),
    CHECK (file_sha256 ~ '^[0-9a-f]{64}$'),
    CHECK (payload_sha256 ~ '^[0-9a-f]{64}$')
);
CREATE INDEX ix_hr_attendance_import_batch ON business_hr.attendance_import_reference(tenant_id,source_system,batch_reference,row_reference);
CREATE INDEX ix_hr_attendance_import_employment ON business_hr.attendance_import_reference(tenant_id,legal_entity_id,employment_id,imported_at DESC);
ALTER TABLE business_hr.attendance_import_reference ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_hr.attendance_import_reference FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON business_hr.attendance_import_reference
    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
GRANT SELECT, INSERT ON business_hr.attendance_import_reference TO erp_app;
REVOKE UPDATE, DELETE ON business_hr.attendance_import_reference FROM erp_app;
