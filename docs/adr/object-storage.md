# ADR-016 — Private Object Storage

Status: Accepted

## Decision

The platform owns file metadata and lifecycle while an S3-compatible private adapter owns bytes. Object keys are generated from trusted identifiers; signed access is short-lived and bound to tenant, file and status. Anonymous bucket access is disabled.

## Consequences

Original filenames cannot become storage paths, and object-store outages do not make metadata appear downloadable or public.

## Evidence

`src/backend/platform/platform-kernel/Security/WebhookAndFileAccess.cs`, file policy tests, and staging MinIO initialization.
