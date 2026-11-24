INSERT INTO platform."OpenIddictScopes" ("Id", "Name", "DisplayName", "Resources")
VALUES ('scope-tenant-write', 'tenant.write', 'Tenant-scoped ERP write API', '["todir-api"]')
ON CONFLICT ("Id") DO UPDATE SET
    "Name" = EXCLUDED."Name",
    "DisplayName" = EXCLUDED."DisplayName",
    "Resources" = EXCLUDED."Resources";

UPDATE platform."OpenIddictApplications"
SET "Permissions" = replace("Permissions", 'scp:tenant.read"]', 'scp:tenant.read","scp:tenant.write"]')
WHERE "ClientId" = 'todir-mobile'
  AND position('scp:tenant.write' in coalesce("Permissions", '')) = 0;
