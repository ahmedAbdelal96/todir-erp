# Secret rotation

Create the replacement secret in the approved secret manager, deploy consumers with an overlap window only where the protocol supports it, verify authentication and signing checks, revoke the old secret, and confirm it cannot be used. Never place a secret in source control, image layers, logs, command output, or an audit payload.
