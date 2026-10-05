# Testing

Cross-system tests belong in these repository categories: `tests/architecture/`, `tests/platform/`, `tests/integration/`, `tests/messaging/`, `tests/security/`, `tests/e2e/`, and `tests/performance/`. Module-local unit tests may live with the module only when its approved testing structure establishes that location.

Tests must use deterministic data, setup and cleanup; remain isolated and independent of execution order; and test meaningful behavior rather than coverage alone. Do not use sleep-based flaky synchronization when deterministic signaling is possible. Do not depend on live production services, and do not mock away the behavior being tested. Use realistic database testing where persistence behavior matters.

Test tenant isolation/RLS, architecture boundaries, authorization/security, messaging/idempotency/retry, APIs and end-to-end workflows as applicable. Performance tests must state workload and measurement intent. Never delete or weaken a test merely to make CI green; a failing test that reveals a defect requires fixing the defect or escalating a governing conflict.
