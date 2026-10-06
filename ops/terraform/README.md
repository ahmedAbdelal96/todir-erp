# Ephemeral staging infrastructure

This OpenTofu stack deliberately targets the repository-controlled Docker engine used by CI and local release verification. It provisions a private network, PostgreSQL, RabbitMQ, MinIO, API, Worker, and Web containers from immutable image references supplied by the caller.

The stack is not a production cloud topology. Production provider selection remains an explicit architecture/owner decision; the Docker provider makes the staging contract reproducible without silently selecting a cloud vendor.

Run from this directory:

```text
tofu init -backend=false
tofu validate
tofu plan -out=staging.plan
tofu apply staging.plan
```

All application image variables must be commit-SHA tags. `latest` is rejected by the variables validation.
