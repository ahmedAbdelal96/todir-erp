# ADR-006 — RabbitMQ and MassTransit Transport

Status: Accepted

## Decision

Use MassTransit as the .NET transport abstraction over RabbitMQ. Production-facing durable endpoints use stable responsibility-owned names, publisher confirmations, TLS, bounded prefetch and quorum queue declarations.

## Consequences

Transport concerns remain replaceable behind the platform messaging contracts, while broker reliability is tested through the real RabbitMQ integration path.

## Evidence

`src/backend/platform/platform-kernel/Messaging/MessagingHostingExtensions.cs`, `ops/containers/rabbitmq.conf`, and staging broker verification.
