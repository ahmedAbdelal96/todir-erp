# Banking Canonical Ownership Decision

Module 25 establishes Banking as the owner of treasury accounts. Payments consumes the Banking capability and retains only a compatibility projection needed by existing payment documents and method configuration. Stable ids are preserved during migration; no payment, invoice, journal, POS, or subledger tables are moved into Banking.

The boundary prevents two modules from independently changing the same monetary account lifecycle and gives future statement connectors, reconciliation, cash controls, and cash forecasting one legal-entity/tenant-scoped source of truth.
