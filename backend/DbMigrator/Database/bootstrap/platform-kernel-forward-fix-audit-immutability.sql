-- Audit history is append-only. Runtime users may create audit records through
-- owned application flows but can never rewrite or remove historical entries.
REVOKE UPDATE, DELETE ON TABLE platform.audit_entry FROM erp_app;
