# Foundation modules

Foundation modules use the same engineering quality bar and canonical workflow as business modules. They begin only when the approved Platform Kernel gates allow it. Before implementation, read root `AGENTS.md`, backend `AGENTS.md`, this file, `../modules/AGENTS.md`, the current module pack, Platform Kernel pack, Master Blueprint, engineering baseline, relevant ADRs, and dependency public contracts.

Directory, Catalog, FinBase, Tax, Pricing, and future foundation modules are owned business domains with strict contracts; they must not become shared-helper dumping grounds. Establish their purpose, owned data, single writer, tenant/legal-entity scope, invariants, dependencies, capabilities, permissions, transactions, events, integration boundaries and expected access patterns before coding.

Implement in the exact approved phase/order in the foundation module pack. No later phase may bypass an earlier exit gate. Do not create a module merely because its architecture is anticipated. Use the canonical structure and end-to-end Definition of Done in `../modules/AGENTS.md`; create only plan-applicable folders and capabilities.
