CREATE TABLE IF NOT EXISTS platform."OpenIddictApplications" (
    "Id" text NOT NULL,
    "ApplicationType" varchar(50),
    "ClientId" varchar(100),
    "ClientSecret" text,
    "ClientType" varchar(50),
    "ConcurrencyToken" varchar(50),
    "ConsentType" varchar(50),
    "DisplayName" text,
    "DisplayNames" text,
    "JsonWebKeySet" text,
    "Permissions" text,
    "PostLogoutRedirectUris" text,
    "Properties" text,
    "RedirectUris" text,
    "Requirements" text,
    "Settings" text,
    CONSTRAINT "PK_OpenIddictApplications" PRIMARY KEY ("Id")
);

CREATE TABLE IF NOT EXISTS platform."OpenIddictScopes" (
    "Id" text NOT NULL,
    "ConcurrencyToken" varchar(50),
    "Description" text,
    "Descriptions" text,
    "DisplayName" text,
    "DisplayNames" text,
    "Name" varchar(200),
    "Properties" text,
    "Resources" text,
    CONSTRAINT "PK_OpenIddictScopes" PRIMARY KEY ("Id")
);

CREATE TABLE IF NOT EXISTS platform."OpenIddictAuthorizations" (
    "Id" text NOT NULL,
    "ApplicationId" text,
    "ConcurrencyToken" varchar(50),
    "CreationDate" timestamptz,
    "Properties" text,
    "Scopes" text,
    "Status" varchar(50),
    "Subject" varchar(400),
    "Type" varchar(50),
    CONSTRAINT "PK_OpenIddictAuthorizations" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_OpenIddictAuthorizations_OpenIddictApplications_Application~" FOREIGN KEY ("ApplicationId") REFERENCES platform."OpenIddictApplications" ("Id")
);

CREATE TABLE IF NOT EXISTS platform."OpenIddictTokens" (
    "Id" text NOT NULL,
    "ApplicationId" text,
    "AuthorizationId" text,
    "ConcurrencyToken" varchar(50),
    "CreationDate" timestamptz,
    "ExpirationDate" timestamptz,
    "Payload" text,
    "Properties" text,
    "RedemptionDate" timestamptz,
    "ReferenceId" varchar(100),
    "Status" varchar(50),
    "Subject" varchar(400),
    "Type" varchar(150),
    CONSTRAINT "PK_OpenIddictTokens" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_OpenIddictTokens_OpenIddictApplications_ApplicationId" FOREIGN KEY ("ApplicationId") REFERENCES platform."OpenIddictApplications" ("Id"),
    CONSTRAINT "FK_OpenIddictTokens_OpenIddictAuthorizations_AuthorizationId" FOREIGN KEY ("AuthorizationId") REFERENCES platform."OpenIddictAuthorizations" ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_OpenIddictApplications_ClientId" ON platform."OpenIddictApplications" ("ClientId");
CREATE INDEX IF NOT EXISTS "IX_OpenIddictAuthorizations_ApplicationId_Status_Subject_Type" ON platform."OpenIddictAuthorizations" ("ApplicationId", "Status", "Subject", "Type");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_OpenIddictScopes_Name" ON platform."OpenIddictScopes" ("Name");
CREATE INDEX IF NOT EXISTS "IX_OpenIddictTokens_ApplicationId_Status_Subject_Type" ON platform."OpenIddictTokens" ("ApplicationId", "Status", "Subject", "Type");
CREATE INDEX IF NOT EXISTS "IX_OpenIddictTokens_AuthorizationId" ON platform."OpenIddictTokens" ("AuthorizationId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_OpenIddictTokens_ReferenceId" ON platform."OpenIddictTokens" ("ReferenceId");

GRANT SELECT, INSERT, UPDATE, DELETE ON TABLE platform."OpenIddictApplications", platform."OpenIddictScopes", platform."OpenIddictAuthorizations", platform."OpenIddictTokens" TO erp_app;

INSERT INTO platform."OpenIddictScopes" ("Id", "Name", "DisplayName", "Resources")
VALUES ('scope-tenant-read', 'tenant.read', 'Tenant-scoped ERP API', '["todir-api"]')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO platform."OpenIddictApplications" ("Id", "ApplicationType", "ClientId", "ClientType", "ConsentType", "DisplayName", "Permissions", "RedirectUris", "Requirements")
VALUES (
    'application-todir-mobile',
    'native',
    'todir-mobile',
    'public',
    'implicit',
    'TODIR Mobile',
    '["ept:authorization","ept:token","gt:authorization_code","gt:refresh_token","rst:code","scp:openid","scp:profile","scp:offline_access","scp:tenant.read"]',
    '["com.todir.app:/oauth2redirect"]',
    '["pkce"]'
)
ON CONFLICT ("Id") DO UPDATE SET
    "ApplicationType" = EXCLUDED."ApplicationType",
    "ClientType" = EXCLUDED."ClientType",
    "ConsentType" = EXCLUDED."ConsentType",
    "Permissions" = EXCLUDED."Permissions",
    "RedirectUris" = EXCLUDED."RedirectUris",
    "Requirements" = EXCLUDED."Requirements";
