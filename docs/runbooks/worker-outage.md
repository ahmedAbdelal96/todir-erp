# Worker outage

Check process health, database readiness, broker connectivity, and outbox age. Restart or replace the worker from the same immutable image, verify it resumes claims with tenant context, and inspect duplicate/idempotency and poison-message counters. Do not bypass the outbox or acknowledge work before the consumer transaction is durable.
