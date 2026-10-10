# Testing

## Absolute filename and folder-name prohibition

Never create or introduce a new test, fixture, project file, or folder whose basename contains `todir` in any casing. This is absolute; test paths must describe the subject or responsibility.

Cross-system tests belong in these repository categories: `backend/Tests/Architecture/`, `backend/Tests/Core/`, `backend/Tests/Integration/`, `backend/Tests/Security/`, and module-owned test folders under `backend/Tests/Modules/`. Frontend global and browser tests belong under `frontend/tests/`; module-local frontend tests belong under `frontend/modules/<module>/tests/`.

Tests must use deterministic data, setup and cleanup; remain isolated and independent of execution order; and test meaningful behavior rather than coverage alone. Do not use sleep-based flaky synchronization when deterministic signaling is possible. Do not depend on live production services, and do not mock away the behavior being tested. Use realistic database testing where persistence behavior matters.

New test and fixture filenames describe the subject and normally use the language convention, for example `TenantContextTests.cs` or `database-security-tests.csproj`; do not encode product or phase history in the filename. Keep test artifacts and generated results under the approved ignored artifact directories.

Test tenant isolation/RLS, architecture boundaries, authorization/security, messaging/idempotency/retry, APIs and end-to-end workflows as applicable. Performance tests must state workload and measurement intent. Never delete or weaken a test merely to make CI green; a failing test that reveals a defect requires fixing the defect or escalating a governing conflict.
