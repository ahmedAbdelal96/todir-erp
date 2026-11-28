# Current-state reconciliation

## Repository and ownership evidence

The inspected checkout is `D:\Web\full-projects\todir-erp`, branch `dev`, at `c02f12e5c33f5f0ebd5e3866d447b79eb3b124d6`. The only pre-existing untracked change observed was `frontend/modules/planning/`; it is unrelated to this mission and was not read into, staged, or modified.

The module registry exposes layers L0 through L4. Planning is `L4`, `business-module`, tenant-scoped, owns `business_planning`, and declares owner capabilities as optional inputs. Subledger is `L2`, owns `finance_subledger`, and exposes `subledger.open-items`, `subledger.aging`, `subledger.party-exposure`, `subledger.settlement-state`, and related capabilities. Sales is L3 and remains an orchestrator; it does not own customer balance, payment, invoice, stock, or ledger truth.

The API host explicitly registers and maps each current module, including Planning. The current Worker explicitly registers existing module consumers and hosted services but not Planning or Analysis. This is evidence that module registration is explicit and that an Analysis module cannot be assumed to become active from a manifest alone.

## Current truth versus obsolete assumptions

| Area | Verified current state | P1 consequence |
|---|---|---|
| Planning | Runtime owner and read-only source-provider registration exist; backend closure is documented. | Do not design a second “planning forecast” truth or repeat the old gap. |
| Subledger | Open items, applications, reversals, due dates, balances and source versions are owned here. | Collections Hygiene may consume scoped facts; it may not write or duplicate settlement truth. |
| Payments / Invoicing | Payment movement and invoice documents remain separate from allocation/open-item truth. | Analysis must not infer payment or revenue from a convenience table. |
| Worker | One operational host with MassTransit/RabbitMQ, Quartz and hosted services. | Heavy Analysis computation needs a separately bounded process role in P2. |
| Navigation | Existing permission-driven module registry and contextual routes exist; no global Analysis destination is approved. | Surface a business route in the owning module. |
| RLS / authorization | Existing tenant, legal-entity, branch and effective-access controls are authoritative. | Analysis reuses them; no parallel ACL or “AI” permission engine. |
| Analysis | No production implementation exists. | All routes, tables, DTOs and job names in this package are proposed P2 contracts. |

## Duplicate-truth decisions

- Banking forecast remains Banking-owned; Planning forecast remains Planning-owned; CRM pipeline forecast remains CRM-owned. Analysis may compare or explain facts only through published read capabilities.
- Ledger remains account-level accounting truth. Subledger remains party/open-item/settlement truth. Inventory remains stock and valuation truth. Sales remains commercial orchestration truth.
- Analysis owns only definitions, run metadata, job attempts, immutable result snapshots, evidence references, recommendation records and review/outcome audit.

## Evidence sources

- [P0 final handoff](evidence/p0/P0_FINAL_ACCEPTANCE_AND_HANDOFF.md)
- [Planning backend closure register](../engineering/BUDGETING_BACKEND_FINAL_CLOSURE_REGISTER.md)
- `backend/Api/Program.cs`, `backend/Worker/Program.cs`
- Current manifests under `backend/Modules/*/module.manifest.json`
- `backend/Modules/Subledger/SubledgerModuleExtensions.cs` and `SubledgerPlanningOwnerSource.cs`
- Module Packs 10, 12, 13, 14, 19 and 28 under `docs/modules/`
- [Implementation structure map](docs/18_IMPLEMENTATION_STRUCTURE_MODULE_MAP.md)
