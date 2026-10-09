# Directory module rules

- `foundation_directory` is the single writer for Party identity, roles, identifiers, contact points and addresses.
- Party is tenant-owned and is never hard-deleted. Archive, restore and merge-redirect semantics must preserve history.
- All persistence must use the Platform Kernel transaction coordinator for writes, tenant context, audit and outbox behavior.
- Do not read another module's tables directly and do not place customer, supplier, accounting or inventory profile logic here.
- Every tenant-owned table requires composite tenant-safe foreign keys and enabled/forced RLS.
- API permission checks are required in addition to UI visibility. Identifier values are masked unless the identifier-view permission is present.
- Search normalization must remain deterministic and Arabic-safe. Do not add country-specific legal validation without an approved country pack.
- Keep contracts stable, bounded and pagination-based. Do not add compatibility routes for the retired pre-directory paths.
