ALTER TABLE business_hr.employee_document
    ADD CONSTRAINT uq_hr_document_import_target UNIQUE (tenant_id,id,employee_id);

CREATE TABLE business_hr.employee_document_import_reference (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    source_system text NOT NULL,
    legacy_document_id text NOT NULL,
    batch_reference text NOT NULL,
    file_sha256 char(64) NOT NULL,
    row_reference text NOT NULL,
    row_payload_sha256 char(64) NOT NULL,
    employee_id uuid NOT NULL,
    document_id uuid NOT NULL,
    data_quality text NOT NULL CHECK (data_quality IN ('Complete','Partial','UnknownHistory')),
    imported_at timestamptz NOT NULL,
    imported_by uuid NOT NULL,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,source_system,legacy_document_id),
    UNIQUE (tenant_id,source_system,batch_reference,row_reference),
    FOREIGN KEY (tenant_id,document_id,employee_id) REFERENCES business_hr.employee_document(tenant_id,id,employee_id),
    CHECK (length(source_system) BETWEEN 1 AND 80),
    CHECK (length(legacy_document_id) BETWEEN 1 AND 160),
    CHECK (length(batch_reference) BETWEEN 1 AND 160),
    CHECK (length(row_reference) BETWEEN 1 AND 160),
    CHECK (file_sha256 ~ '^[0-9a-f]{64}$'),
    CHECK (row_payload_sha256 ~ '^[0-9a-f]{64}$')
);
CREATE INDEX ix_hr_document_import_batch ON business_hr.employee_document_import_reference(tenant_id,source_system,batch_reference,row_reference);
ALTER TABLE business_hr.employee_document_import_reference ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_hr.employee_document_import_reference FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON business_hr.employee_document_import_reference
    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
GRANT SELECT, INSERT ON business_hr.employee_document_import_reference TO erp_app;
REVOKE UPDATE, DELETE ON business_hr.employee_document_import_reference FROM erp_app;
