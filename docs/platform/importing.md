# Imports

Imports progress through upload, detect, map, validate, preview, dry run, execute, reconcile, and completion/failure states. Large imports are chunked background work and are resumable by batch and chunk idempotency keys. Rollback semantics are declared by the importer and never promised for posted legal data.
