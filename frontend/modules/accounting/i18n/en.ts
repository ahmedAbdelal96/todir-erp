export const en = {
  title: "General ledger",
  journal: {
    loading: "Loading journal entry...", error: "The journal entry could not be loaded or does not exist.", back: "Back",
    accountCodeName: "Account code and name", memo: "Description", debit: "Debit", credit: "Credit", costCenter: "Cost center",
    documentType: "Manual journal entry", generalLedger: "General ledger", entries: "Journal entries", postPending: "Posting...", post: "Post journal",
    entryDate: "Journal date", period: "Accounting period", balanceStatus: "Balance status", balanced: "Journal is balanced", unbalanced: "Journal is unbalanced",
    addLine: "Add journal line", totalDebit: "Total debit", totalCredit: "Total credit", difference: "Difference", description: "Journal description",
    postedLocked: "The journal is posted to the general ledger", periodClosed: "The financial period is closed"
  },
  accountPicker: { label: "Financial account", searching: "Searching...", placeholder: "Search by account code or name..." }
} as const;
