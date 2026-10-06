# Files

File bytes live behind an S3-compatible private object-store adapter; the platform owns metadata and lifecycle. Object keys are generated from trusted IDs, never filenames. Uploads are quarantined until signature, MIME, size, and malware checks pass. Signed URLs are issued only after tenant and owner authorization.
