# Ephemeral staging infrastructure

This OpenTofu stack deliberately targets the repository-controlled Docker engine used by CI and local release verification. It provisions a private network, PostgreSQL, RabbitMQ, MinIO, API, Worker, and Web containers from immutable image references supplied by the caller.

The stack is not a production cloud topology. Production provider selection remains an explicit architecture/owner decision; the Docker provider makes the staging contract reproducible without silently selecting a cloud vendor. RabbitMQ uses the repository TLS configuration and a private network; the caller supplies the short-lived RabbitMQ and Web certificate directories. Data Protection keys are persisted in a named volume and encrypted with the caller-supplied short-lived PFX. MinIO is private to the network and its `private-files` bucket is created with anonymous access disabled. The staging-only replay verification route is enabled only by `staging_replay_token`; it is not mapped when `TODIR_STAGING_VERIFICATION` is false.

Run from this directory:

```text
tofu init -backend=false
tofu validate
tofu plan -out=staging.plan
tofu apply staging.plan
```

All application image variables must be commit-SHA tags. `latest` is rejected by the variables validation. The staging caller must provide `rabbit_tls_directory`, `web_tls_directory`, `data_protection_certificate_base64`, `data_protection_certificate_password`, and an ephemeral `staging_replay_token`; no credentials are stored in this repository. Set `failure_injection` only for an explicit controlled staging drill; leave it empty for normal deployments.
