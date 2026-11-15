SET search_path = business_projects, public;
SET row_security = off;

CREATE UNIQUE INDEX IF NOT EXISTS ux_projects_sales_source_reference
    ON business_projects.project_external_reference (tenant_id, source_type, source_id, purpose);
