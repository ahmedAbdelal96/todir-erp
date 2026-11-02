# ADR-016 — Private Object Storage

Status: Accepted

## Decision

The platform owns file metadata and lifecycle while an S3-compatible private adapter owns bytes. Object keys are generated from trusted identifiers; signed access is short-lived and bound to tenant, file and status. Anonymous bucket access is disabled.

## Consequences

Original filenames cannot become storage paths, and object-store outages do not make metadata appear downloadable or public. A caller first authorizes access in its owning business module, then Core verifies tenant, owner, and `Ready` status before issuing a short-lived GET URL. URLs are secrets and are excluded from audit and logs. Browser-facing endpoint configuration must be reachable from the user's browser; private bucket policy remains mandatory.

## Evidence

`backend/Core/Security/WebhookAndFileAccess.cs`, file policy tests, and staging MinIO initialization.
