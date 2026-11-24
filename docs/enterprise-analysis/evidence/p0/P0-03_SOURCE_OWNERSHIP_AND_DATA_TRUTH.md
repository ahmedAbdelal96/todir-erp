# P0-03 — Source Ownership and Data Truth

**Result:** `PARTIALLY VERIFIED`  
**Decision:** Analysis must consume owner-module capabilities/contracts; it must not become a second operational source of truth.

## Verified owner surfaces

| Business fact | Current owner evidence | What is available |
|---|---|---|
| Receivables/payables open items | Subledger service and contracts | Open items, party balance, aging, statement, applications, reconciliation |
| Payments and cash receipts | Payments module | Draft/captured/refund/transfer flows and payment-account compatibility |
| Bank activity/reconciliation | Banking module | Bank activity, provider settlement, cash-to-ledger and reconciliation surfaces |
| Inventory availability/movements | Inventory module | Warehouse/stock/reservation contracts and module-owned persistence |
| Sales pipeline forecast | CRM module | Forecast, weighted amount, categories, stale/overdue signals, snapshots |
| Accounting truth | Accounting/Ledger modules | Trial balance, general ledger, reconciliation, close, statements and FX processes |

## Critical evidence from source

- `backend/Shared/Contracts/SubledgerContracts.cs` exposes party-scoped balance, aging and statement contracts, including `AsOfDate`, posting/effective dates, source sequence and running balance.
- `backend/Modules/Subledger/Services/SubledgerService.cs` delegates balance, aging and statement reads to the Subledger repository and enforces identity/query validation.
- `backend/Modules/CRM/Repositories/CrmRepository.cs` computes forecast totals from opportunity amount, probability, forecast category and expected-close date, and can persist a forecast snapshot.
- `backend/Modules/Payments/Services/PaymentsService.cs` models unallocated receipts and payment drafts as payment-owned flows; it does not establish bank confirmation by itself.
- RLS SQL consistently uses tenant-scoped policies based on `current_setting('app.tenant_id', true)` in the inspected schemas.

## Collections pilot gaps

The current contracts do not prove a certified portfolio-wide collections read model containing all of the following together: all parties, multi-owner visibility, historical/as-of reconstruction, cash confirmation timing, allocation state, legal-entity scope, and knowledge-time reproducibility. Party-level Subledger reads are not equivalent to a portfolio fact export.

Before locking Collections, P1 must define and prove:

1. invoice/open-item inclusion and exclusion rules;
2. payment receipt versus bank-confirmed cash semantics;
3. allocation, unapplied and partial-payment timing;
4. valid-time and knowledge-time behavior for corrections and late-arriving facts;
5. portfolio ownership scope and legal-entity/branch permissions;
6. currency, FX and overdue-day calculation rules.

