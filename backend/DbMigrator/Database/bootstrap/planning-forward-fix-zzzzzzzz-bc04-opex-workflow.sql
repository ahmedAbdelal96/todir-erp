-- BC04 OPEX planning workflow.  Planning owns the projection; account and
-- cost-centre references remain opaque owner-contract identifiers.
CREATE TABLE IF NOT EXISTS business_planning.opex_plan_line (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    plan_version_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    department_ref uuid,
    category_code text NOT NULL,
    account_ref text,
    cost_center_ref text,
    period_start date NOT NULL,
    method text NOT NULL,
    base_amount numeric(19,4) NOT NULL DEFAULT 0,
    driver_value numeric(19,4) NOT NULL DEFAULT 0,
    rate numeric(19,4) NOT NULL DEFAULT 0,
    growth_percent numeric(9,4) NOT NULL DEFAULT 0,
    inflation_percent numeric(9,4) NOT NULL DEFAULT 0,
    planned_amount numeric(19,4) NOT NULL,
    currency_code char(3) NOT NULL,
    source text NOT NULL DEFAULT 'Manual',
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (tenant_id, id),
    FOREIGN KEY (tenant_id, plan_version_id) REFERENCES business_planning.plan_version(tenant_id, id),
    CHECK (method IN ('Fixed', 'Variable', 'Recurring', 'DriverBased')),
    CHECK (base_amount >= 0 AND driver_value >= 0 AND rate >= 0),
    CHECK (growth_percent >= -100 AND inflation_percent >= -100),
    CHECK (planned_amount >= 0)
);

CREATE INDEX IF NOT EXISTS ix_planning_opex_scope
    ON business_planning.opex_plan_line(tenant_id, plan_version_id, legal_entity_id, period_start, category_code);
CREATE UNIQUE INDEX IF NOT EXISTS ux_planning_opex_coordinate
    ON business_planning.opex_plan_line(tenant_id, plan_version_id, legal_entity_id,
        coalesce(department_ref, '00000000-0000-0000-0000-000000000000'::uuid), category_code,
        period_start, coalesce(account_ref, ''), coalesce(cost_center_ref, ''));

ALTER TABLE business_planning.opex_plan_line ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_planning.opex_plan_line FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON business_planning.opex_plan_line;
CREATE POLICY tenant_isolation ON business_planning.opex_plan_line
    USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
GRANT SELECT, INSERT, UPDATE, DELETE ON business_planning.opex_plan_line TO erp_app;
