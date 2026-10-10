# Tenant context

Tenant is the security boundary. Authentication derives one active tenant from the login domain and binds the session to that tenant; normal requests cannot select or switch tenant context. Database work runs in an explicit transaction and sets `SET LOCAL app.tenant_id`; the value cannot leak across pooled connections. Background messages carry tenant context and workers must establish it before reading tenant data. Login-domain routing is limited to the minimum control-plane lookup required before tenant-owned account access.
