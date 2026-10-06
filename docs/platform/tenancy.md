# Tenant context

Tenant is the security boundary. HTTP tenant selection is resolved to an active membership before application work begins. Database work runs in an explicit transaction and sets `SET LOCAL app.tenant_id`; the value cannot leak across pooled connections. Background messages carry tenant context and workers must establish it before reading tenant data.
