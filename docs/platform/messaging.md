# Messaging reliability

Messages use a versioned envelope with message, tenant, correlation, causation, actor, source, and trace metadata. Delivery is at-least-once. The outbox is committed with business state; the inbox key is endpoint plus message ID; acknowledgements occur only after durable consumer work commits. Critical production endpoints use quorum queues and stable non-tenant queue names.

The outbox is FORCE-RLS protected. Cross-tenant worker claiming is restricted to the fixed-parameter `claim_pending_outbox` security-definer function owned by the non-login `erp_dispatcher` role; the worker still applies the message tenant context before marking a row. No application connection can select the outbox across tenants.
