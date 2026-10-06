# Deploy

1. Build API, worker, migrator, and web images from the release commit and tag every image with that immutable commit SHA.
2. Scan the images and record the results with the release evidence.
3. Apply the OpenTofu staging plan. The dedicated migrator is the only component allowed to change the database schema.
4. Verify `/health`, the web shell, database connectivity, worker readiness, and the API contract before promotion.
5. Promote the exact image digests after approval; never deploy `latest`.
