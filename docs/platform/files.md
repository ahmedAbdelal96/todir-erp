# Files

File bytes live behind an S3-compatible private object-store adapter; the platform owns metadata and lifecycle. Object keys are generated from trusted IDs, never filenames. Uploads are quarantined until signature, MIME, size, and malware checks pass. Signed URLs are issued only after tenant and owner authorization.

## Private downloads

The Core capability issues a short-lived `GET` URL only for a `Ready` file record whose tenant, owner module, owner entity type, and owner entity ID all match the caller's request. The stored object key must also match the trusted owner-key prefix. The bucket remains private; the API never proxies file bytes. Callers must authorize the business user and any sensitivity-specific permission before asking this capability to sign a URL. Signed URLs are credentials: do not place them in audit payloads, application logs, analytics, or persistent UI state.

Configure the adapter with `TODIR_FILES_BUCKET`. `TODIR_FILES_PUBLIC_ENDPOINT` is the endpoint reachable by the user's browser (for local MinIO, for example, `http://localhost:19000`); the service and the browser must resolve the same endpoint. Optional `TODIR_FILES_ACCESS_KEY` and `TODIR_FILES_SECRET_KEY` must be configured together. Without them, the AWS SDK default credential chain is used. `TODIR_FILES_REGION` defaults to `us-east-1`. `TODIR_FILES_SCANNER_HOST` and `TODIR_FILES_SCANNER_PORT` configure the ClamAV daemon (local compose defaults to `127.0.0.1:3310`); uploads fail closed while it is unavailable. The capability clamps URL validity to 30 seconds through 15 minutes; sensitive HR document access currently requests five minutes. Local and production buckets must disable anonymous/public access.

Platform Files uploads use a five-minute signed POST policy for an unguessable incoming object key. The policy binds the key, private ACL, MIME type, and exact declared byte length; the maximum is 25 MiB. Completion validates the bytes, extension/MIME agreement, file signature, and malware scan before copying the exact scanned bytes to the immutable final key and marking metadata Ready. Only Ready objects can be referenced by business modules. Successful and rejected uploads remove incoming objects. Operators must configure an `incoming/` lifecycle rule to expire abandoned uploads after one day; the adapter does not overwrite bucket lifecycle policy.

If storage is not configured or unavailable, metadata stays intact and download requests fail with a stable service error. They must never fall back to an untrusted or public object URL.
