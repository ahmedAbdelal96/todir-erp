# Messaging reliability

Messages use a versioned envelope with message, tenant, correlation, causation, actor, source, and trace metadata. Delivery is at-least-once. The outbox is committed with business state; the inbox key is endpoint plus message ID; acknowledgements occur only after durable consumer work commits. Critical production endpoints use quorum queues and stable non-tenant queue names.

The outbox is FORCE-RLS protected. Cross-tenant worker claiming is restricted to the fixed-parameter `claim_pending_outbox` security-definer function owned by the non-login `erp_dispatcher` role; that role has only the schema and table privileges required by the two approved dispatcher functions. The worker still applies the message tenant context before marking a row. No application connection can select the outbox across tenants.

Inbox claims use a five-minute lease and an attempt counter. A concurrent delivery cannot claim an active lease; a delivery whose consumer process dies after the Inbox transaction commits can reclaim the row after the lease expires, while a processed row remains permanently idempotent. Staging can explicitly inject one process failure after the Inbox commit and before the broker acknowledgement to verify this redelivery path.

The worker observes pending-outbox count and oldest age through a fixed-parameter security-definer function owned by `erp_dispatcher`; operational metrics contain module/endpoint dimensions only and never tenant identifiers. Critical staging endpoints are asserted to be quorum queues before outage/recovery drills.

Consume faults are recorded through the MassTransit consume observer without persisting exception payloads or sensitive details. The approved replay service republishes the original outbox message with its stable message ID, requires an unresolved error record and an operator reason, marks the error resolved only after publisher confirmation, and writes a tenant-scoped audit entry. Inbox idempotency makes a retry safe if the operator retries after an audit or database interruption.
