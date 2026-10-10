# Authorization

Modules own their permission catalog through manifests. Roles contain permission entries and the tenant-bound authorization subject receives roles at Tenant, Legal Entity, or Branch scope. Backend authorization is authoritative; frontend checks only shape navigation and affordances. Cached decisions are short-lived and invalidated when the authorization subject, role, assignment, or module state changes.
