ALTER TABLE business_hr.employee_document NO FORCE ROW LEVEL SECURITY;
ALTER TABLE business_hr.employment NO FORCE ROW LEVEL SECURITY;

ALTER TABLE business_hr.employee_document
    ADD COLUMN IF NOT EXISTS employment_id uuid,
    ADD COLUMN IF NOT EXISTS legal_entity_id uuid,
    ADD COLUMN IF NOT EXISTS scope_status text NOT NULL DEFAULT 'Resolved';

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conrelid = 'business_hr.employee_document'::regclass
          AND conname = 'employee_document_employment_owner_fkey'
    ) THEN
        ALTER TABLE business_hr.employee_document
            ADD CONSTRAINT employee_document_employment_owner_fkey
            FOREIGN KEY (tenant_id, employment_id, employee_id)
            REFERENCES business_hr.employment(tenant_id, id, employee_id);
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conrelid = 'business_hr.employee_document'::regclass
          AND conname = 'employee_document_scope_status_check'
    ) THEN
        ALTER TABLE business_hr.employee_document
            ADD CONSTRAINT employee_document_scope_status_check
            CHECK (scope_status IN ('Resolved', 'Unresolved'));
    END IF;
END $$;

WITH employee_scope AS (
    SELECT tenant_id, employee_id,
        (array_agg(legal_entity_id ORDER BY legal_entity_id))[1] AS legal_entity_id,
        (array_agg(id ORDER BY id))[1] AS employment_id
    FROM business_hr.employment
    GROUP BY tenant_id, employee_id
    HAVING count(DISTINCT legal_entity_id) = 1
)
UPDATE business_hr.employee_document AS document
SET legal_entity_id = employee_scope.legal_entity_id,
    employment_id = CASE WHEN (
        SELECT count(*) FROM business_hr.employment AS employment
        WHERE employment.tenant_id = employee_scope.tenant_id
          AND employment.employee_id = employee_scope.employee_id
    ) = 1 THEN employee_scope.employment_id END
FROM employee_scope
WHERE document.tenant_id = employee_scope.tenant_id
  AND document.employee_id = employee_scope.employee_id
  AND document.legal_entity_id IS NULL
  AND document.employment_id IS NULL;

UPDATE business_hr.employee_document AS document
SET scope_status = 'Unresolved'
WHERE document.legal_entity_id IS NULL
  AND document.employment_id IS NULL
  AND EXISTS (
      SELECT 1 FROM business_hr.employment AS employment
      WHERE employment.tenant_id = document.tenant_id
        AND employment.employee_id = document.employee_id
      GROUP BY employment.tenant_id, employment.employee_id
      HAVING count(DISTINCT employment.legal_entity_id) > 1
  );

CREATE INDEX IF NOT EXISTS ix_hr_document_employment_scope
    ON business_hr.employee_document(tenant_id, legal_entity_id, employment_id, status, category, created_at DESC, id DESC)
    WHERE scope_status = 'Resolved';

CREATE OR REPLACE FUNCTION business_hr.guard_employee_document_scope() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP = 'UPDATE' AND (
        NEW.employment_id IS DISTINCT FROM OLD.employment_id
        OR NEW.legal_entity_id IS DISTINCT FROM OLD.legal_entity_id
        OR NEW.scope_status IS DISTINCT FROM OLD.scope_status
    ) THEN
        RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.document_employment_scope_immutable';
    END IF;

    IF NEW.employment_id IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM business_hr.employment AS employment
        WHERE employment.tenant_id = NEW.tenant_id
          AND employment.id = NEW.employment_id
          AND employment.employee_id = NEW.employee_id
          AND employment.legal_entity_id = NEW.legal_entity_id
    ) THEN
        RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.document_employment_scope_invalid';
    END IF;

    IF NEW.supersedes_document_id IS NOT NULL AND NOT EXISTS (
        SELECT 1
        FROM business_hr.employee_document AS prior
        WHERE prior.tenant_id = NEW.tenant_id
          AND prior.id = NEW.supersedes_document_id
          AND prior.employee_id = NEW.employee_id
          AND prior.employment_id IS NOT DISTINCT FROM NEW.employment_id
          AND prior.legal_entity_id IS NOT DISTINCT FROM NEW.legal_entity_id
          AND prior.scope_status = NEW.scope_status
    ) THEN
        RAISE EXCEPTION USING ERRCODE='23514', MESSAGE='hr.document_supersession_scope_invalid';
    END IF;

    RETURN NEW;
END $$;

DROP TRIGGER IF EXISTS trg_hr_document_employment_scope ON business_hr.employee_document;
CREATE TRIGGER trg_hr_document_employment_scope
    BEFORE INSERT OR UPDATE OF employment_id, legal_entity_id, scope_status, supersedes_document_id ON business_hr.employee_document
    FOR EACH ROW EXECUTE FUNCTION business_hr.guard_employee_document_scope();

ALTER TABLE business_hr.employee_document FORCE ROW LEVEL SECURITY;
ALTER TABLE business_hr.employment FORCE ROW LEVEL SECURITY;
