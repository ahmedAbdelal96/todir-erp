# Consumer poison message

Stop automatic retries after the bounded policy is exhausted, retain the message error with tenant, endpoint, correlation, and causation identifiers, and quarantine the payload without exposing secrets. Correct the consumer or data defect, approve a replay through the Inbox idempotency path, and verify one durable business effect before clearing the error.
