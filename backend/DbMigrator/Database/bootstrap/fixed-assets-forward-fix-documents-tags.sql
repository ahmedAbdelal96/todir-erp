CREATE TABLE IF NOT EXISTS business_assets.asset_document_reference (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    asset_id uuid NOT NULL,
    file_ref uuid NOT NULL,
    role text NOT NULL,
    title text,
    sha256 text NOT NULL,
    content_type text NOT NULL,
    size_bytes bigint NOT NULL,
    classification text NOT NULL,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, asset_id, file_ref),
    FOREIGN KEY (tenant_id, asset_id) REFERENCES business_assets.fixed_asset(tenant_id, id),
    CHECK (role IN ('AcquisitionEvidence','Ownership','Warranty','Insurance','Maintenance','Verification','Other')),
    CHECK (char_length(trim(title)) <= 200),
    CHECK (sha256 ~ '^[0-9a-fA-F]{64}$'),
    CHECK (size_bytes >= 0),
    CHECK (char_length(trim(content_type)) BETWEEN 1 AND 200),
    CHECK (char_length(trim(classification)) BETWEEN 1 AND 100)
);

ALTER TABLE business_assets.asset_document_reference ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_assets.asset_document_reference FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON business_assets.asset_document_reference;
CREATE POLICY tenant_isolation ON business_assets.asset_document_reference
    USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);

CREATE INDEX IF NOT EXISTS ix_assets_documents_asset ON business_assets.asset_document_reference(tenant_id, asset_id, created_at DESC, id);
GRANT SELECT, INSERT ON business_assets.asset_document_reference TO erp_app;
