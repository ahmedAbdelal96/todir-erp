# Ephemeral staging infrastructure

This OpenTofu stack deliberately targets the repository-controlled Docker engine used by CI and local release verification. It provisions a private network, PostgreSQL, RabbitMQ, MinIO, API, two clustered Worker instances, and Web containers from digest-pinned image references supplied by the caller.

The stack is not a production cloud topology. Production provider selection remains an explicit architecture/owner decision; the Docker provider makes the staging contract reproducible without silently selecting a cloud vendor. RabbitMQ uses the repository TLS configuration and a private network; the caller supplies the short-lived RabbitMQ and Web certificate directories. PostgreSQL administrator, `erp_app` runtime, RabbitMQ, MinIO root, and MinIO `private-files` credentials are separate inputs; only the migrator receives the PostgreSQL administrator credential, and the API/Workers receive only the `erp_app` and RabbitMQ credentials. Data Protection keys are persisted in a named volume and encrypted with the caller-supplied short-lived PFX. MinIO is private to the network, its `private-files` bucket is created with anonymous access disabled, and its non-root identity receives only the checked-in bucket policy. The staging-only replay verification route is enabled only by `staging_replay_token`; it is not mapped when `TODIR_STAGING_VERIFICATION` is false.

Run from this directory:

```text
tofu init -backend=false
tofu validate
tofu plan -out=staging.plan
tofu apply staging.plan
```

All application image variables must be references with the release commit SHA as the tag and a trailing immutable `@sha256:<digest>`. `latest`, mutable tags, and tag-only references are rejected by variable validation. The staging caller must provide `rabbit_tls_directory`, `web_tls_directory`, `data_protection_certificate_base64`, `data_protection_certificate_password`, `entitlement_hmac_key`, and an ephemeral `staging_replay_token`; no credentials are stored in this repository. Set `failure_injection` only for an explicit controlled staging drill; leave it empty for normal deployments. The MinIO bucket identity is provisioned for the Platform Files adapter, but the API is not given a browser endpoint by this stack; enable `TODIR_FILES_*` only with a reviewed endpoint reachable by both the API and browser and with the approved malware-scanner boundary.

## State and secret contract

OpenTofu marks sensitive variables as sensitive in plan output, but that does not encrypt state. The Docker provider records container environment and command arguments in state, so a shared or staging run must use the approved encrypted remote state backend with access control, TLS, locking, retention, and audit logging. A local backend is allowed only for a disposable developer run; never copy its state to Git, artifacts, tickets, or shared storage, and securely remove it after destroying the stack. Secret rotation requires a new plan/apply with overlapping credentials only for the documented provider transition, followed by revocation of the old credentials.

The default web bind is loopback-only. Binding the staging web boundary to a non-loopback address requires an explicitly approved ingress and network policy; PostgreSQL, RabbitMQ, and MinIO have no host-published ports in this stack. The web container receives the reserved `reverse_proxy_ip`, and the API trusts that address only; it does not trust every container on the private subnet.
