# Tenant-bound identity and authorization subject

Every login account is created inside exactly one tenant and keeps that tenant
for its lifetime. The login domain routes the request to one active tenant,
then the account is loaded by tenant and normalized full email. There is no
global identity, cross-tenant account, organization-selection challenge, or
tenant-switching session.

The existing tenant-bound authorization subject is created with the account so
roles and legal-entity/branch scope remain owned by the same tenant; it is not
a login membership or a cross-company relationship. Employee records are not
part of this kernel boundary.
