# Module 19 CRM Backend Execution State & Release Closure

Canonical pack: `docs/modules/19_ERP_SAAS_CRM_REQUIREMENTS_AND_TECHNICAL_DESIGN_PACK.md` (v1.0).

The CRM module owns pre-sales leads, opportunities, pipeline/stage history, activities and next actions, seller worklists, forecast metadata, CRM party profiles, buying-committee/contact-role context, interests, custom fields, and the CRM-to-Sales quotation seam. Directory remains the authoritative owner of Party identity; Sales owns quotations/orders; Invoicing, Subledger and Payments remain the owners of financial truth. Invariant: `Pipeline Value != Revenue`.

---

## 1. Release Closure Declaration

**Status: MODULE 19 CRM BACKEND RELEASE GATES PASSED — FINANCIAL & SECURITY INTEGRITY VERIFIED**

All backend release gates, asynchronous event synchronization, transactional idempotency, authenticated audit trails, role-based visibility, CRM-to-Sales handoff, fresh database migrations, and business metrics truth have been implemented, verified, and sealed against real local PostgreSQL on branch `dev`.

---

## 2. Complete Verification Matrix

| Release Gate / Requirement | Persistence & Execution Layer | Authoritative Contract & Behavioral Proof | Status |
| --- | --- | --- | --- |
| **RabbitMQ Event Lifecycle & Inbox Idempotency** | `CrmExternalEventConsumer`, `NpgsqlInboxStore`, `business_crm.crm_external_event`, `business_crm.opportunity_source_link`, `platform.inbox_message` | Real MassTransit test harness & PostgreSQL. Processed `QuotationAcceptedV1` (transition to `Accepted`) and `InvoicePostedV1` (transition to `InvoicePosted`). Replay verification: identical `message_id` blocked by `NpgsqlInboxStore.TryBeginAsync` (`processed_at is not null`), resulting in exactly 1 persisted row and 0 duplicate business effects. Lease abandon/retry verified via `inboxStore.AbandonAsync`. | **PASS** |
| **Authenticated Audit Acceptance & History** | `Core.Services.NpgsqlAuditWriter`, `platform.audit_entry`, `IPlatformAdminSurfaceStore` | Real audit entries written with tenant context, legal entity, actor identity, action (`opportunity.created`), entity type, and serialized before/after JSON payloads. Asserted persistence in `platform.audit_entry`. | **PASS** |
| **Role & Team Visibility Isolation** | `business_crm.opportunity`, `business_crm.crm_sales_team`, `business_crm.crm_sales_team_member` | Seller 1 sees own deal (`scope="mine"`). Seller 2 (outside team) cannot view Seller 1's deal (`scope="mine"` returns 0). Adding Seller 2 to Seller 1's team grants immediate visibility. Manager (`allAuthorized=true`) sees all deals across legal entity. Tenant isolation: cross-tenant lookup returns `null` and 0 records. | **PASS** |
| **CRM-to-Sales Handoff & Directory Conversion** | `CrmService.ConvertLeadAsync`, `foundation_directory.party`, `foundation_directory.party_role`, `business_crm.opportunity_source_link` | Lead qualified → converted to Directory Party with `Customer` role and CRM Opportunity. Replay idempotent: `FindConversionAsync` returns existing opportunity. Quotation handoff: `RecordQuoteLinkAsync` inserts `target_module='sales'`, `target_type='Quotation'`, `purpose='CreateQuotation'`. Replay with same idempotency key is idempotent (`on conflict do nothing`), strictly preserving 1 link and legal entity isolation. | **PASS** |
| **Deal Health vs. Inactivity Business Truth** | `CrmRepository.GetOpportunityAsync`, `ListOpportunitiesAsync`, stage `stale_after_days` | Health is dynamically derived query-side per Canonical Spec §356 and §625: Open deals with inactivity > `stale_after_days` evaluate to `health_status='Stale'` and `health_score=20`. Deals with close date in the past evaluate to `health_status='AtRisk'` and `health_score=50`. Active deals evaluate to `health_status='Healthy'` and `health_score=100`. Won deals evaluate to `health_status='Healthy'` (100); Lost deals evaluate to `health_status='Stale'` (0). Activity completion dynamically refreshes `opportunity.last_activity_at`, recovering stale deals back to Healthy. Filter support for `health_status` in `ListOpportunitiesAsync` verified. | **PASS** |
| **Win Rate vs. Conversion Rate Business Truth** | `CrmContracts.CrmReportDto`, `CrmRepository.ReportAsync` | Reconciled formulas into authoritative contracts: `OpportunityWinRate = closedDeals == 0 ? 0m : Round(wonCount / (wonCount + lostCount) * 100, 2)`. When 0 won deals exist, `OpportunityWinRate` is strictly `0.00%` (not 100%). `LeadConversionRate = leadCount == 0 ? 0m : Round(convertedCount / leadCount * 100, 2)` is independent and authoritative. | **PASS** |
| **Fresh Database Bootstrap & Upgrade Integrity** | `backend/DbMigrator`, `todir_test_fresh` | Clean execution of all 139 migrations on fresh database with zero errors; checksum preservation verified; `--seed development` applied cleanly with 40 leads and 12 realistic opportunities per tenant across Atlas Trading and Delta Supplies. | **PASS** |
| **Pipeline Stage Concurrency & Fixes** | `CrmRepository.CreateStageAsync`, `business_crm.crm_pipeline_stage` | Fixed missing `created_at` in `CreateStageAsync`. Verified stage reordering, optimistic version concurrency checks, and custom field validation. | **PASS** |

---

## 3. Authoritative Metric Contracts

### 3.1 Deal Health Formula (Dynamic Query-Derived)
Deal health is derived in `GetOpportunityAsync`, `GetOpportunityByLeadAsync`, and `ListOpportunitiesAsync` as follows:
- **Won Deal:** `health_status = 'Healthy'`, `health_score = 100`
- **Lost Deal:** `health_status = 'Stale'`, `health_score = 0`
- **Open Deal with Inactivity > `stale_after_days` AND Overdue Close Date:** `health_status = 'Stale'`, `health_score = 15`
- **Open Deal with Inactivity > `stale_after_days`:** `health_status = 'Stale'`, `health_score = 20`
- **Open Deal with Overdue Close Date (`expected_close_date < current_date`):** `health_status = 'AtRisk'`, `health_score = 50`
- **Otherwise (Active, Future Close Date):** `health_status = 'Healthy'`, `health_score = 100`

### 3.2 Win Rate vs. Lead Conversion Rate
In `CrmReportDto` and `ReportAsync`:
- **Opportunity Win Rate (`OpportunityWinRate`):**
  $$\text{OpportunityWinRate} = \begin{cases} 0.00\%, & \text{if } (\text{Won} + \text{Lost}) = 0 \\ \frac{\text{Won}}{\text{Won} + \text{Lost}} \times 100, & \text{otherwise} \end{cases}$$
- **Lead Conversion Rate (`LeadConversionRate`):**
  $$\text{LeadConversionRate} = \begin{cases} 0.00\%, & \text{if Total Leads} = 0 \\ \frac{\text{Converted Leads}}{\text{Total Leads}} \times 100, & \text{otherwise} \end{cases}$$

---

## 4. Frontend Labeling & Binding Notice for Antigravity

> [!IMPORTANT]
> **Action Required for Frontend Team (Antigravity):**
> Backend now exposes `OpportunityWinRate` on `CrmReportDto` (`/api/v1/crm/legal-entities/{entityId}/reports/summary`).
>
> In `frontend/modules/crm/features/management/crm-management-tab.tsx` around line 209:
> The KPI card titled `t("crm.reports.winRate")` was previously bound to `reportsSummary?.leadConversionRate`.
>
> **Required Frontend Corrections:**
> 1. Bind `reportsSummary?.opportunityWinRate` to the KPI card for Win Rate (`crm.reports.winRate`).
> 2. Bind `reportsSummary?.leadConversionRate` to a dedicated KPI card for Lead Conversion Rate (`crm.reports.leadConversionRate`).
> 3. In Opportunity List cards/tables, display `healthStatus` (`Healthy`, `AtRisk`, `Stale`) and `healthScore` as returned authoritatively by the backend API.

---

## 5. Automated Evidence Run

- **Database Integration Suite:**
  `dotnet test backend/Tests/Integration/IntegrationTests.csproj --filter "FullyQualifiedName~CrmPostgresIntegrationTests"`
  **Result:** `Passed! - Failed: 0, Passed: 7, Skipped: 0, Total: 7, Duration: 3 s`
- **Platform Architecture Gate:**
  `dotnet test backend/Tests/Architecture/ArchitectureTests.csproj`
  **Result:** `Passed! - Failed: 0, Passed: 21, Skipped: 0, Total: 21, Duration: 24 s`
- **API Composition Gate:**
  `dotnet test backend/Tests/Integration/IntegrationTests.csproj --filter "FullyQualifiedName~ApiCompositionTests"`
  **Result:** `Passed! - Failed: 0, Passed: 2, Skipped: 0, Total: 2, Duration: 9 s`
- **Solution Compilation:**
  `dotnet build backend/Backend.sln`
  **Result:** `Build succeeded. 0 Warning(s), 0 Error(s)`
- **Database Migrator & Seed:**
  `dotnet run --project backend/DbMigrator/DbMigrator.csproj -- --seed development`
  **Result:** `Applied or verified 139 ordered platform scripts from 18 module manifests. CRM: 1 pipeline, 40 leads, 12 opportunities.`
