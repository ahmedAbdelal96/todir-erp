CREATE TABLE business_hr.employment_assignment_import_reference (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    source_system text NOT NULL,
    legacy_assignment_id text NOT NULL,
    batch_reference text NOT NULL,
    file_sha256 char(64) NOT NULL,
    row_reference text NOT NULL,
    row_payload_sha256 char(64) NOT NULL,
    assignment_id uuid NOT NULL,
    imported_at timestamptz NOT NULL,
    imported_by uuid NOT NULL,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,source_system,legacy_assignment_id),
    UNIQUE (tenant_id,source_system,batch_reference,row_reference),
    FOREIGN KEY (tenant_id,assignment_id) REFERENCES business_hr.employment_assignment(tenant_id,id),
    CHECK (length(source_system) BETWEEN 1 AND 80),
    CHECK (length(legacy_assignment_id) BETWEEN 1 AND 160),
    CHECK (length(batch_reference) BETWEEN 1 AND 160),
    CHECK (length(row_reference) BETWEEN 1 AND 160),
    CHECK (file_sha256 ~ '^[0-9a-f]{64}$'),
    CHECK (row_payload_sha256 ~ '^[0-9a-f]{64}$')
);
CREATE INDEX ix_hr_assignment_import_batch ON business_hr.employment_assignment_import_reference(tenant_id,source_system,batch_reference,row_reference);
ALTER TABLE business_hr.employment_assignment_import_reference ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_hr.employment_assignment_import_reference FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON business_hr.employment_assignment_import_reference
    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
GRANT SELECT, INSERT ON business_hr.employment_assignment_import_reference TO erp_app;
REVOKE UPDATE, DELETE ON business_hr.employment_assignment_import_reference FROM erp_app;
