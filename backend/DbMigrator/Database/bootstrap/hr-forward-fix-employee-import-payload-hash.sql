ALTER TABLE business_hr.employee_import_reference
    ADD COLUMN IF NOT EXISTS row_payload_sha256 text;

ALTER TABLE business_hr.employee_import_reference
    DROP CONSTRAINT IF EXISTS ck_hr_employee_import_payload_hash;

ALTER TABLE business_hr.employee_import_reference
    ADD CONSTRAINT ck_hr_employee_import_payload_hash
    CHECK (row_payload_sha256 IS NULL OR row_payload_sha256 ~ '^[0-9a-f]{64}$');

COMMENT ON COLUMN business_hr.employee_import_reference.row_payload_sha256 IS
    'Canonical imported employee/employment row hash used to distinguish idempotent replay from changed source data.';
