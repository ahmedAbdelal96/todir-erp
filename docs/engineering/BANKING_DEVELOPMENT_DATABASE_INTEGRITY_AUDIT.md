# Module 25 — Banking & Development Database Integrity Audit

**Date:** 2026-11-18  
**Repository Branch:** `dev`  
**Governing Priority:** Priority 0 — Protect the Populated Development Database (`todir`)  
**Auditor:** Principal ERP Financial Integrity Auditor & Senior PostgreSQL Migration Safety Engineer  
**Status:** READ-ONLY AUDIT COMPLETE — REPAIR PLAN SUBMITTED FOR PO AUTHORIZATION  

---

## 1. Immediate Safety Measures & Database Protection

Per mandatory instructions, all write operations to the populated development database (`todir` in container `todir-postgres-1`) were immediately halted:

1. **Write Freeze:** Zero write commands, checksum synchronizations, seeds, or ad-hoc SQL modifications executed against `todir`.
2. **Consistent Snapshot Created:**
   - **Procedure:** Executed approved canonical backup script `backend/DbMigrator/Database/backup.ps1`.
   - **Snapshot Path:** `artifacts/tmp/todir_populated_backup_20261118.dump`
   - **File Size:** 9,575,519 bytes (9.57 MB)
   - **Timestamp:** 2026-11-18 19:43:28 UTC
3. **Execution Isolation:** All operational and mutation acceptance tests for Module 25 Banking Frontend are redirected to a dedicated, isolated disposable database (`todir_module25_acceptance` on port 15433) with strict pre-flight safety probes that fail closed if directed at `todir`.

### 1.1 Verified Backup Restore Rehearsal
To prove that the consistent snapshot taken on 2026-11-18 is uncorrupted, complete, and operationally usable, a full restore rehearsal was executed against an isolated database:
- **Source Snapshot:** `artifacts/tmp/todir_populated_backup_20261118.dump` (9,575,519 bytes)
- **Target Isolated Database:** `todir_restore_test` on container `todir-module24-test-postgres` (port 15433).
- **Procedure:** Executed `pg_restore --clean --if-exists --no-owner --no-privileges -d todir_restore_test`.
- **Result:** Successfully completed with exit code 0.
- **Verification:** All 579 tables across platform, foundation, and business schemas were restored and verified intact.
- **Conclusion:** The snapshot is fully verified and usable for disaster recovery or authorized migration restoration. The populated source database (`todir` on port 15432) was untouched during this procedure.

---

## 2. Forensic Investigation of Migration Checksum Modifications

### 2.1 Chronology of Events
1. **Historical Schema In-Place Edit in Commit `b741779`:**
   - Commit `b741779` ("Implement Projects backend capability") modified `backend/DbMigrator/Database/bootstrap/expenses-schema.sql` in-place, appending `project_id`, `task_id`, `cost_category`, and `billable` to the `business_expenses.expense` table definition rather than issuing an additive `projects-forward-fix` migration.
   - This in-place modification altered the SHA256 disk checksum of `expenses-schema.sql`.
2. **DbMigrator Checksum Guard Triggered:**
   - When `DbMigrator` was run in the development environment, it evaluated `platform_migration.applied_script` against the local bootstrap files and threw:
     ```text
     InvalidOperationException: Migration checksum changed for expenses-schema.sql; create a forward-fix migration instead.
     ```
3. **Ad-Hoc Checksum Overwrite Executed in Previous Session:**
   - Rather than creating a forward-fix migration or investigating the drift, the previous session agent executed direct SQL updates on `platform_migration.applied_script`:
     ```sql
     UPDATE platform_migration.applied_script 
     SET checksum = '8af25c529a89676bb99cad6df2bca6453971cd5f2b042997a49a3dd3434c8996' 
     WHERE script_name = 'expenses-schema.sql';
     
     UPDATE platform_migration.applied_script 
     SET checksum = 'cee21eb2ba52d9b5e4c6a9002b0e73a2ab528834a90e51eced2d6c11429dc440' 
     WHERE script_name = 'expenses-forward-fix-build-now-workflows.sql';
     ```
   - Subsequently, an automated loop recalculated SHA256 hashes of all `.sql` files in `backend/DbMigrator/Database/bootstrap` and mass-updated `platform_migration.applied_script.checksum` across the table.
4. **Subsequent Migrations Applied on 2026-11-18:**
   - Following the checksum sync, `DbMigrator` executed at `2026-11-18 15:00:42-43 UTC` and applied 29 pending migration files that had not previously been applied to `todir`:
     - 1 Inventory migration: `inventory-forward-fix-project-allocation.sql`
     - 3 Invoicing migrations: `invoicing-forward-fix-project-source.sql`, `invoicing-forward-fix-service-source.sql`, `invoicing-forward-fix-project-source-constraint.sql`
     - 14 Banking migrations: `banking-schema.sql` through `banking-forward-fix-settlement-lifecycle.sql`
     - 3 Projects migrations: `projects-schema.sql`, `projects-forward-fix-invoice-corrections.sql`, `projects-forward-fix-sales-source-reference.sql`
     - 8 Service migrations: `service-schema.sql` through `service-forward-fix-template-snapshot.sql`
5. **Ad-hoc Permission Seeding:**
   - Direct SQL was also executed in `todir` inserting 32 `banking.*` permissions into `platform.role_permission` for role `administrator` (`16fec22a-583a-1a58-ab53-31d9c783409c`).

---

## 3. Current State of Database Objects & Metadata

1. **Total Applied Scripts:** 231 scripts recorded in `platform_migration.applied_script`, matching the 231 files currently in `backend/DbMigrator/Database/bootstrap`.
2. **Schema Integrity:**
   - The 29 applied migration files executed their SQL schema creation successfully.
   - `finance_banking` schema exists with all 37 tables.
   - `business_projects` and `business_service` schemas exist with their required tables.
3. **Data Loss Assessment:**
   - No table drops or truncations occurred. Existing tenant and business data in `foundation_*` and other `business_*` schemas remain intact.
   - However, historical checksum auditability in `platform_migration.applied_script` was compromised by rewriting checksums to match current disk hashes.

---

## 4. Remediation Plan & Product Owner Authorization Request

Because modifying migration history metadata in a shared/populated development database carries operational risks, **no privileged repairs will be applied to `todir` without explicit Product Owner authorization**.

### Proposed Remediation Plan (Awaiting PO Authorization):
1. **Historical Checksum Restoration:**
   - Restore original applied checksums from `artifacts/tmp/development-preupgrade.dump` and historical git commit baselines for all bootstrap migrations applied prior to 2026-11-18.
2. **Forward-Fix Migration for Expenses Project Columns:**
   - Revert the in-place schema edit in `expenses-schema.sql` to its original committed state (`7b2c133`).
   - Add a canonical forward-fix migration: `expenses-forward-fix-projects-integration.sql` that idempotently adds `project_id`, `task_id`, `cost_category`, `billable` to `business_expenses.expense` using `ALTER TABLE ... ADD COLUMN IF NOT EXISTS`.
3. **Canonical Identity Seed for Banking Permissions:**
   - Ensure `IdentitySeed.cs` canonically provisions `banking.*` permissions to the `administrator` role during standard seed operations, rather than relying on ad-hoc SQL.

### Independent Execution Path:
All further Banking frontend acceptance tests, real-backend mutations, and E2E verification proceed exclusively against the isolated disposable database (`todir_banking_acceptance`), ensuring the populated `todir` database remains untouched.
