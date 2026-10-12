-- Runtime database boundary bootstrap.
--
-- Execute this script with the deployment/migration administrator. Passwords are
-- deliberately provisioned outside source control by the environment secret
-- manager; this script never embeds credentials.

-- Search indexes in the canonical module baselines depend on PostgreSQL's
-- approved trigram operator class. Install it before module schemas run.
CREATE EXTENSION IF NOT EXISTS pg_trgm WITH SCHEMA public;

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
CREATE SCHEMA IF NOT EXISTS platform AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS platform AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS platform AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS platform AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS platform AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS platform AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS platform AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS platform AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS platform AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS platform AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS platform AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS platform AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS platform AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS platform AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS platform AUTHORIZATION erp_owner;
CREATE SCHEMA IF NOT EXISTS platform_migration AUTHORIZATION erp_owner;

-- PUBLIC must not discover or create objects in owned schemas.
REVOKE ALL ON SCHEMA platform, platform, platform, platform,
    platform, platform, platform, platform,
    platform, platform, platform, platform,
    platform, platform, platform, platform_migration FROM PUBLIC;

GRANT USAGE ON SCHEMA platform, platform, platform, platform,
    platform, platform, platform, platform,
    platform, platform, platform, platform,
    platform, platform, platform TO erp_app;

-- New tables created by the owner are runtime-DML accessible by default. Existing
-- tables are granted explicitly by each owning module migration after creation.
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform
    GRANT SELECT, INSERT, UPDATE ON TABLES TO erp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;
ALTER DEFAULT PRIVILEGES FOR ROLE erp_owner IN SCHEMA platform
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO erp_app;
