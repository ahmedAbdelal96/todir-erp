-- Runtime database boundary bootstrap.
--
-- Execute this script with the deployment/migration administrator. Passwords are
-- deliberately provisioned outside source control by the environment secret
-- manager; this script never embeds credentials.

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'erp_owner') THEN
        CREATE ROLE erp_owner
            NOLOGIN
            NOSUPERUSER
            NOCREATEDB
            NOCREATEROLE
            NOINHERIT
            NOREPLICATION
            NOBYPASSRLS;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'erp_app') THEN
        CREATE ROLE erp_app
            LOGIN
            NOSUPERUSER
            NOCREATEDB
            NOCREATEROLE
            NOINHERIT
            NOREPLICATION
            NOBYPASSRLS;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'erp_dispatcher') THEN
        CREATE ROLE erp_dispatcher
            NOLOGIN
            NOSUPERUSER
            NOCREATEDB
            NOCREATEROLE
            NOINHERIT
            NOREPLICATION
            BYPASSRLS;
    END IF;
END
$$;

-- Reassert the security posture on every deployment run so drift is corrected.
ALTER ROLE erp_owner NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS;
ALTER ROLE erp_app LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS;
ALTER ROLE erp_dispatcher NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION BYPASSRLS;

-- Platform schemas are owned by the migration role and are never owned by the
-- API/Worker runtime role. Business module schemas follow the same contract.
CREATE SCHEMA IF NOT EXISTS platform_tenancy AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS platform_org AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS platform_iam AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS platform_authz AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS platform_modules AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS platform_entitlements AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS platform_messaging AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS platform_audit AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS platform_numbering AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS platform_settings AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS platform_files AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS platform_custom_fields AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS platform_import AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS platform_notifications AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS platform_scheduling AUTHORIZATION erp_owner;

-- PUBLIC must not discover or create objects in owned schemas.
REVOKE ALL ON SCHEMA platform_tenancy, platform_org, platform_iam, platform_authz,
    platform_modules, platform_entitlements, platform_messaging, platform_audit,
    platform_numbering, platform_settings, platform_files, platform_custom_fields,
    platform_import, platform_notifications, platform_scheduling FROM PUBLIC;

GRANT USAGE ON SCHEMA platform_tenancy, platform_org, platform_iam, platform_authz,
    platform_modules, platform_entitlements, platform_messaging, platform_audit,
    platform_numbering, platform_settings, platform_files, platform_custom_fields,
    platform_import, platform_notifications, platform_scheduling TO erp_app;

-- New tables created by the owner are runtime-DML accessible by default. Existing
-- tables are granted explicitly by each owning module migration after creation.
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform_tenancy
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform_org
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform_iam
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform_authz
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform_modules
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform_entitlements
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform_messaging
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform_audit
    GRANT SELECT, INSERT, UPDATE ON TABLES TO erp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform_numbering
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform_settings
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform_files
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform_custom_fields
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform_import
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform_notifications
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform_scheduling
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;
