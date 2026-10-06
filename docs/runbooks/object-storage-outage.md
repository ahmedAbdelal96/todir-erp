# Object storage outage

Keep file metadata status explicit, stop issuing new download signatures when the object store cannot verify availability, and return a bounded retryable error. Restore private object-store access, verify bucket policy and tenant object-key boundaries, then reconcile quarantined and ready metadata without making objects public.
