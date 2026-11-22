# Restricted-role navigation acceptance

The restricted-role Playwright suite runs against a fresh PostgreSQL database. It never writes the shared development database and never changes the canonical Development Seed.

From the repository root, after the local API and DbMigrator have been built:

```powershell
$env:TODIR_ACCEPTANCE_DISPOSABLE = "true"
$env:TODIR_RESTRICTED_ACCEPTANCE_PASSWORD = "<local-development-password>"
powershell -ExecutionPolicy Bypass -File frontend/tests/e2e/restricted-role-acceptance-fixture.ps1
```

The fixture creates a uniquely named database from `template0`, applies the checked-in DbMigrator migrations and Development Seed, provisions four test-only users and scoped roles, runs the real ASP.NET Core API, Vite frontend, and Playwright browser suite, then drops only that disposable database. Cleanup retries without terminating PostgreSQL sessions; `-KeepDatabase` is available for local diagnosis.

The provisioned roles cover HR self-service, Payroll self-payslip, Projects resource-load with `projects.manage_team` but without `projects.read`, and limited Expenses/Payments. The test verifies forbidden routes, query tampering, command palette filtering, project query gating, Arabic/English route rendering, and the real browser/API/database path.
