# ADR-007 — Transactional Outbox and Inbox Idempotency

Status: Accepted

## Decision

Business transactions write the platform outbox. A dispatcher publishes only committed rows with confirms; consumers claim a stable endpoint/message key in a durable inbox, use a lease for crash recovery, and acknowledge only after durable work.

## Consequences

Broker outages preserve database intent and redelivery cannot create a second inbox effect. Consumer business operations must remain idempotent beyond the inbox row.

## Evidence

`backend/Core/Messaging/InboxStore.cs`, `backend/DbMigrator/Database/bootstrap/platform-kernel-schema.sql`, and the staging crash-after-commit drill.
