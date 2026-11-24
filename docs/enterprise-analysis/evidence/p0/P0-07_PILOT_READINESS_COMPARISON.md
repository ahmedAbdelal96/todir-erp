# P0-07 — Pilot Readiness Comparison

| Candidate | Owner data surfaces | Main missing proof | Current disposition |
|---|---|---|---|
| Collections | Subledger, Invoicing, Payments, Banking, Accounting | Portfolio read contract, bank/cash timing, allocation semantics, temporal reconstruction, scoped ownership | `PROPOSED / NOT APPROVED` |
| Sales performance | Sales, Invoicing, CRM, Payments | Order/invoice/posting basis, returns/tax/FX rules, owner read capability | `POSSIBLE / VERIFY` |
| Inventory health | Inventory, Catalog, Purchasing, Sales | Availability and valuation basis, reservations, cost/warehouse semantics | `POSSIBLE / VERIFY` |
| CRM forecast | CRM | Official-vs-derived forecast boundary and historical snapshot semantics | `POSSIBLE / VERIFY` |
| Manufacturing | Manufacturing, Inventory, Catalog, Accounting | Actual-vs-plan, capacity and cost completeness | `POSSIBLE / VERIFY` |
| FP&A | No confirmed runtime owner in current module inventory | Budget/forecast owner contracts and module boundary | `DEFERRED / BLOCKED` |

## Independent assessment

Collections has strong business value but is not the safest first implementation until its portfolio and temporal truth are proven. A narrower owner-module pilot (for example CRM forecast or a bounded Sales analysis) could reduce architectural risk, but only after P1 selects a canonical read contract and confirms the same security and performance gates.

