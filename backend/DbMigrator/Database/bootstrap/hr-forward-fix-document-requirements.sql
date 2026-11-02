CREATE TABLE business_hr.employee_document_requirement (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    category text NOT NULL,
    category_key text NOT NULL,
    enabled boolean NOT NULL,
    created_by uuid NOT NULL,
    updated_by uuid NOT NULL,
    updated_at timestamptz NOT NULL,
    version bigint NOT NULL,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,legal_entity_id,category_key),
    CHECK (length(category) BETWEEN 1 AND 80),
    CHECK (category_key=upper(category)),
    CHECK (version>0)
);
CREATE INDEX ix_hr_document_requirement_enabled
    ON business_hr.employee_document_requirement(tenant_id,legal_entity_id,category_key)
    WHERE enabled;
ALTER TABLE business_hr.employee_document_requirement ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_hr.employee_document_requirement FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON business_hr.employee_document_requirement
    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
GRANT SELECT, INSERT, UPDATE ON business_hr.employee_document_requirement TO erp_app;
REVOKE DELETE ON business_hr.employee_document_requirement FROM erp_app;
