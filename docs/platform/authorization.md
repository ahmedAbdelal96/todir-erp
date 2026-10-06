# Authorization

Modules own their permission catalog through manifests. Roles contain permission entries and memberships receive roles at Tenant, Legal Entity, or Branch scope. Backend authorization is authoritative; frontend checks only shape navigation and affordances. Cached decisions are short-lived and invalidated when membership, role, assignment, or module state changes.
