# Message replay

Quarantine the failed message and inspect its error record without exposing payload secrets. Correct the consumer defect, approve a replay with a reason and operator identity, then replay through the inbox/idempotency path. Verify duplicate delivery produces one business effect and retain the original error and replay audit evidence.
