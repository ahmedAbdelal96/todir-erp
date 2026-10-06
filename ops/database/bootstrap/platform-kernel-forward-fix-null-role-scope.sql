-- Tenant-scoped role assignments intentionally have no scope_id.

ALTER TABLE platform_authz.membership_role_assignment
    ALTER COLUMN scope_id DROP NOT NULL;
