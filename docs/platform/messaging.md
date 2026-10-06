# Messaging reliability

Messages use a versioned envelope with message, tenant, correlation, causation, actor, source, and trace metadata. Delivery is at-least-once. The outbox is committed with business state; the inbox key is endpoint plus message ID; acknowledgements occur only after durable consumer work commits. Critical production endpoints use quorum queues and stable non-tenant queue names.

The outbox is FORCE-RLS protected. Cross-tenant worker claiming is restricted to the fixed-parameter `claim_pending_outbox` security-definer function owned by the non-login `erp_dispatcher` role; that role has only the schema and table privileges required by the two approved dispatcher functions. The worker still applies the message tenant context before marking a row. No application connection can select the outbox across tenants.

Inbox claims use a five-minute lease and an attempt counter. A concurrent delivery cannot claim an active lease; a delivery whose consumer process dies after the Inbox transaction commits can reclaim the row after the lease expires, while a processed row remains permanently idempotent. Staging can explicitly inject one process failure after the Inbox commit and before the broker acknowledgement to verify this redelivery path.
