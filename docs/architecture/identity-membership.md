# Identity and membership

Authentication identity is global and independent from tenant membership. A
single identity may have multiple memberships, while every membership is
tenant-scoped and may carry default legal-entity and branch references.

Membership status is explicit (`Invited`, `Active`, `Suspended`, `Revoked`).
Tenant RLS protects memberships, and session validation must resolve an active
membership before establishing tenant context. Employee records are not part of
this kernel boundary.
