# ADR-021 — Canonical Module Schema Topology

Status: Accepted

## Decision

The database uses one owned PostgreSQL schema per real module boundary. The
Platform Kernel is one module and owns the `platform` schema. Foundation and
business modules use a stable layer prefix plus their module ID, such as
`foundation_catalog`, `foundation_directory`, and `business_sales`.
`platform_migration` is reserved for migrator metadata, and `public` is not an
application data schema.

Capability names such as identity, authorization, audit, messaging, settings,
and notifications do not create additional schemas when they remain owned by
the Platform Kernel. Their table names and code boundaries remain explicit.

Tenant isolation is enforced by `tenant_id`, PostgreSQL RLS, and runtime role
permissions; schemas are ownership boundaries, not tenant partitions.

## Rules

- A module manifest owns exactly one schema.
- A schema is never shared by two modules.
- A module never reads or writes another module's tables directly.
- Cross-module access uses public contracts, capabilities, or events.
- New modules use `foundation_<module-id>` or `business_<module-id>` according
  to the layer declared by their manifest.
- A schema split requires an explicit ownership, privilege, lifecycle, or
  deployment reason and an ADR.
- Database changes use controlled forward migrations; applied migrations are
  immutable.

## Target topology

```text
platform          Platform Kernel
foundation_catalog Catalog
foundation_directory Directory
foundation_finbase Finance Base
business_<module> Business modules
sample            Sample / verification module
platform_migration Migration history only
public            PostgreSQL extensions only
```

## Consequences

The current capability-level `platform_*` schemas are consolidated through a
forward migration. Existing SQL migration files remain immutable for checksum
safety; active code and future migrations use the canonical names. Existing
foundation/business schema prefixes remain because they are the approved layer
convention in the module packs.

Persistence access remains module-owned. The approved long-term data access
shape is one `DbContext` per module where EF Core is used, with explicit SQL
retained for PostgreSQL policies, security-definer functions, migrations, and
measured hot paths. A global ERP `DbContext` is forbidden.
