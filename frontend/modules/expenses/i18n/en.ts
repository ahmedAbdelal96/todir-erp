export const expensesEn = {
  title: "Expenses & Reimbursements",
  subtitle: "Manage business spend, advances, corporate cards, and claim settlements",
  activeEntity: "Active Legal Entity",
  badge: "ERP Module 21",
  defaultEntity: "Atlas Commercial Group",

  tabs: {
    selfService: "My Expenses & Self-Service",
    review: "Review & Approvals",
    advances: "Business Advances",
    corporateCards: "Corporate Cards",
    reimbursements: "Reimbursements & Repayments",
    reports: "Reports & Spend Analysis",
    settings: "Setup & Policies"
  },

  selfService: {
    title: "Employee Expense & Advance Portal",
    description: "Capture daily expenses, track approval status, request business advances, and track reimbursements",
    newExpense: "New Expense",
    requestAdvance: "Request Advance",
    subtabs: {
      myExpenses: "My Expenses",
      myAdvances: "My Business Advances",
      myCards: "Corporate Card Transactions",
      myReimbursements: "My Reimbursements",
      myRepayments: "My Repayments to Company",
      myReports: "Expense Reports"
    },
    emptyExpenses: "No expenses recorded yet. Click 'New Expense' to start.",
    emptyAdvances: "No business advances recorded yet.",
    emptyCards: "No corporate card transactions requiring action.",
    emptyReimbursements: "No pending reimbursements for you at this time.",
    emptyRepayments: "No repayments due to the company.",
    emptyReports: "No expense reports recorded."
  },

  review: {
    title: "Expense Review & Approval Workbench",
    description: "Review submitted expenses, verify receipts, enforce policy limits, and finalize transactions",
    searchPlaceholder: "Search by merchant name or business purpose...",
    filterAll: "All States",
    filterSubmitted: "Pending Approval",
    filterApproved: "Approved",
    filterFinalized: "Finalized to Ledger",
    filterReversed: "Reversed",
    empty: "No expenses match the current search filters.",
    approveAction: "Approve",
    rejectAction: "Reject",
    finalizeAction: "Finalize to Ledger",
    reverseAction: "Reverse Entry",
    inspectAction: "Inspect Details",
    approvalCol: "Approval",
    postingCol: "Posting",
    settlementCol: "Settlement"
  },

  advances: {
    title: "Business Advances & Settlement",
    description: "Manage business advances issued to employees for travel or business missions and reconcile against expenses",
    requestAdvance: "Request Advance",
    searchPlaceholder: "Search advances...",
    filterAll: "All Advances",
    filterRequested: "Pending Review",
    filterApproved: "Approved, Pending Issue",
    filterIssued: "Issued & Active",
    filterSettled: "Fully Settled",
    overdueWarning: "Advance is past its settlement due date",
    empty: "No business advances recorded yet.",
    issueAction: "Issue Advance",
    applyAction: "Apply Expense",
    returnAction: "Record Return",
    writeOffAction: "Write Off",
    progressLabel: "Advance Settlement Progress:",
    disbursed: "Disbursed",
    applied: "Applied",
    returned: "Returned",
    outstanding: "Outstanding"
  },

  corporateCards: {
    title: "Corporate Cards & Reconciliation",
    description: "Import corporate credit card transactions, match expenses, identify personal charges, and reconcile statements",
    importTransaction: "Import Transaction",
    reconciliationTitle: "Statement Reconciliation Summary",
    filterAll: "All Transactions",
    filterUnmatched: "Unmatched",
    filterMatched: "Matched",
    filterPersonal: "Personal Charge",
    filterIgnored: "Ignored",
    empty: "No corporate card transactions recorded in this legal entity.",
    matchAction: "Match to Expense",
    createExpenseAction: "Create Expense from Card",
    personalAction: "Mark as Personal Charge",
    ignoreAction: "Ignore",
    clearAction: "Clear Transaction",
    adjustAction: "Record Adjustment",
    bankReference: "Bank Reference"
  },

  reimbursements: {
    title: "Reimbursements & Settlement Workbench",
    description: "Prepare reimbursement payment batches for employees and track incoming repayments for personal card spend",
    prepareBatch: "Prepare Reimbursement Batch",
    subtabs: {
      toEmployees: "Reimbursements to Employees",
      fromEmployees: "Repayments from Employees"
    },
    batchesTitle: "Prepared Reimbursement Batches",
    payBatchAction: "Execute Payment",
    settleRepaymentAction: "Record Collection",
    emptyBatches: "No pending reimbursement batches.",
    emptyRepayments: "No repayments due from employees.",
    batchId: "Batch ID",
    preparedDate: "Prepared Date",
    batchTotal: "Batch Total",
    batchPreparedStatus: "Ready for Payment",
    reference: "Reference",
    entryDate: "Entry Date",
    totalAmount: "Total Amount",
    collectedAmount: "Collected",
    remainingAmount: "Remaining",
    collectedStatus: "Collected",
    dueStatus: "Due",
    claimDate: "Claim Date",
    dueAmount: "Due Amount",
    paidAmount: "Paid Amount"
  },

  reports: {
    title: "Spend Analysis & Expense Reports",
    description: "Analyze total company spend by category, employee, or payment source with accounting export",
    exportCsv: "Export Spend Report (CSV)",
    groupByCategory: "Group by Category",
    groupByEmployee: "Group by Employee",
    groupBySource: "Group by Payment Source",
    fromDate: "From Date",
    toDate: "To Date",
    empty: "No expense data found for the selected period.",
    totalSpend: "Total Spend",
    totalBase: "Total in Base Currency",
    totalTax: "Total Recoverable Tax",
    groupByLabel: "Grouping & Aggregation Metric",
    groupHeader: "Group / Category",
    expenseCount: "Expense Count",
    unclassified: "Unclassified",
    reportTitle: "Report Title",
    createdAt: "Created At"
  },

  settingsTab: {
    title: "Expense Setup & Policy Governance",
    description: "Configure categories, spend limits, audit policies, and travel allowance rates",
    newCategory: "New Category",
    newPolicy: "New Policy",
    newRatePolicy: "New Travel/Mileage Rate",
    importExpenses: "Bulk Import Expenses",
    subtabs: {
      categories: "Expense Categories",
      policies: "Audit Policies & Limits",
      ratePolicies: "Mileage & Per Diem Rates",
      imports: "Import Batches"
    },
    emptyCategories: "No categories defined.",
    emptyPolicies: "No expense policies defined.",
    emptyRatePolicies: "No mileage or per diem rates defined.",
    emptyImports: "No prior import batches.",
    categoryCode: "Category Code",
    categoryName: "Category Name",
    publishedVersion: "Published Version",
    publishedActive: "Published & Active",
    draftStatus: "Draft",
    newVersionAction: "+ New Version",
    policyCode: "Policy Code",
    policyName: "Policy Name",
    newRulesAction: "+ New Rules",
    rateKind: "Kind",
    rateValue: "Rate",
    effectiveFrom: "Effective From",
    publishAction: "Publish & Activate"
  },

  kpi: {
    pendingApproval: "Pending Approval",
    missingEvidence: "Missing Receipts",
    reimbursementPending: "Reimbursements Due",
    personalRepaymentDue: "Repayments Due from Staff",
    outstandingAdvances: "Active Advances",
    statementAmount: "Statement Total",
    matchedAmount: "Matched Transactions",
    personalAmount: "Personal Charges",
    unclassifiedAmount: "Unclassified"
  },

  states: {
    expense: {
      Draft: "Draft",
      Submitted: "Submitted",
      Approved: "Approved",
      Finalized: "Finalized",
      Reversed: "Reversed"
    },
    approval: {
      Pending: "Pending Review",
      Approved: "Approved",
      Rejected: "Rejected"
    },
    accounting: {
      NotFinalized: "Not Finalized",
      Pending: "Posting Pending",
      Posted: "Posted to Ledger",
      Reversed: "Reversed"
    },
    settlement: {
      NotSettled: "Not Settled",
      ReimbursementPending: "Reimbursement Pending",
      ReimbursementPaid: "Reimbursement Paid",
      RepaymentPending: "Repayment Pending",
      RepaymentSettled: "Repayment Settled",
      AdvanceApplied: "Advance Applied",
      SettledDirect: "Directly Settled"
    },
    evidence: {
      Missing: "Missing",
      PendingVerification: "Pending Verification",
      Verified: "Verified",
      Rejected: "Rejected"
    },
    policy: {
      NotEvaluated: "Not Evaluated",
      Passed: "Compliant",
      Warning: "Warning",
      Violation: "Violation",
      ExceptionApproved: "Exception Approved"
    },
    source: {
      EmployeePaid: "Employee Paid (Personal Funds)",
      CompanyCard: "Corporate Card",
      PettyCash: "Petty Cash",
      Advance: "Business Advance"
    },
    advance: {
      Requested: "Requested",
      Approved: "Approved",
      Issued: "Issued",
      Applied: "Partially Applied",
      Settled: "Settled"
    },
    card: {
      Unmatched: "Unmatched",
      Matched: "Matched",
      Personal: "Personal Charge",
      Ignored: "Ignored"
    }
  },

  common: {
    merchant: "Merchant / Vendor",
    date: "Date",
    amount: "Amount",
    currency: "Currency",
    source: "Payment Source",
    category: "Category",
    purpose: "Business Purpose",
    description: "Description & Notes",
    status: "Status",
    actions: "Actions",
    save: "Save",
    cancel: "Cancel",
    confirm: "Confirm",
    submit: "Submit for Approval",
    close: "Close",
    loading: "Loading...",
    errorTitle: "An unexpected error occurred",
    retry: "Retry",
    reason: "Reason for Action (Mandatory)",
    notAvailable: "N/A",
    yes: "Yes",
    no: "No",
    file: "Attached File",
    uploadReceipt: "Attach Receipt",
    declareMissingReceipt: "Declare Missing Receipt",
    details: "Details",
    history: "Audit History",
    participants: "Participants",
    itemizations: "Itemizations",
    mileage: "Mileage Allowance",
    perDiem: "Per Diem Allowance",
    dimensions: "Dimensions & Cost Centers"
  },

  modals: {
    newExpense: {
      title: "Record New Expense",
      description: "Enter expense details, merchant, amount, and payment source",
      merchantLabel: "Merchant or Provider Name",
      dateLabel: "Expense Date",
      amountLabel: "Amount",
      currencyLabel: "Currency",
      sourceLabel: "Payment Source",
      categoryLabel: "Expense Category",
      purposeLabel: "Business Purpose",
      descriptionLabel: "Additional Notes",
      submitButton: "Record Expense"
    },
    declareMissingReceipt: {
      title: "Declare Missing Receipt",
      description: "In accordance with corporate financial policy, missing receipts require an explicit declaration and explanation",
      reasonLabel: "Reason Receipt is Unavailable",
      reasonPlaceholder: "e.g. Vendor did not provide a receipt / lost during travel...",
      submitButton: "Submit Declaration"
    },
    requestAdvance: {
      title: "Request Business Advance",
      description: "Request an upfront advance for business travel or operational mission",
      purposeLabel: "Mission & Purpose",
      amountLabel: "Requested Amount",
      currencyLabel: "Currency",
      dueDateLabel: "Expected Settlement Due Date",
      submitButton: "Submit Advance Request"
    },
    issueAdvance: {
      title: "Issue Business Advance",
      description: "Confirm payment disbursement through the selected payment account",
      accountLabel: "Disbursement Account",
      methodLabel: "Payment Method",
      amountLabel: "Disbursed Amount",
      paymentDateLabel: "Payment Date",
      postingDateLabel: "Ledger Posting Date",
      submitButton: "Confirm Disbursement",
      methods: {
        bankTransfer: "Bank Transfer",
        cash: "Cash",
        cheque: "Cheque"
      }
    },
    applyAdvance: {
      title: "Apply Expense Against Advance",
      description: "Offset an approved expense against the active business advance balance",
      expenseLabel: "Expense to Apply",
      amountLabel: "Amount to Deduct from Advance",
      submitButton: "Apply Offset"
    },
    returnAdvance: {
      title: "Return Unspent Advance Funds",
      description: "Record the return of remaining unspent funds to the company",
      amountLabel: "Amount Returned",
      submitButton: "Confirm Return"
    },
    writeOffAdvance: {
      title: "Request Advance Write-Off",
      description: "Request writing off an outstanding uncollectible advance balance",
      amountLabel: "Write-Off Amount",
      reasonLabel: "Accounting Justification & Reason",
      submitButton: "Submit Write-Off Request"
    },
    prepareBatch: {
      title: "Prepare Reimbursement Batch",
      description: "Group finalized approved expenses into a single payment batch",
      currencyLabel: "Batch Currency",
      submitButton: "Prepare Batch"
    },
    payBatch: {
      title: "Pay Reimbursement Batch",
      description: "Execute bank or cash disbursement for the reimbursement batch",
      accountLabel: "Payment Account",
      methodLabel: "Payment Method",
      paymentDateLabel: "Payment Date",
      postingDateLabel: "Posting Date",
      submitButton: "Confirm Payment"
    },
    settleRepayment: {
      title: "Collect Employee Repayment",
      description: "Record incoming repayment from an employee for personal card spending",
      accountLabel: "Receiving Account",
      methodLabel: "Collection Method",
      amountLabel: "Amount Received",
      paymentDateLabel: "Received Date",
      postingDateLabel: "Posting Date",
      submitButton: "Confirm Collection"
    },
    matchCard: {
      title: "Match Card Transaction to Expense",
      description: "Link a corporate credit card transaction to an approved expense claim",
      expenseIdLabel: "Expense Record ID",
      submitButton: "Confirm Match"
    },
    createExpenseFromCard: {
      title: "Create Expense from Card Transaction",
      description: "Use transaction merchant and amount to create an expense claim",
      categoryLabel: "Expense Category",
      purposeLabel: "Business Purpose",
      descriptionLabel: "Additional Notes",
      submitButton: "Create Expense"
    },
    recordAdjustment: {
      title: "Record Card Adjustment",
      description: "Record refunds, disputes, or FX corrections for the card transaction",
      kindLabel: "Adjustment Kind (Credit / Dispute / Reversal)",
      referenceLabel: "Bank Reference",
      amountLabel: "Amount",
      currencyLabel: "Currency",
      reasonLabel: "Adjustment Reason",
      submitButton: "Record Adjustment"
    },
    newCategory: {
      title: "New Expense Category",
      description: "Define category code, name, and receipt verification requirements",
      codeLabel: "Category Code",
      nameLabel: "Category Name",
      requiresReceiptLabel: "Requires Receipt",
      submitButton: "Create Category"
    },
    newCategoryVersion: {
      title: "Publish Category Version",
      description: "Update receipt threshold and effective validity dates",
      requiresReceiptLabel: "Requires Receipt",
      thresholdLabel: "Receipt Threshold (Optional)",
      effectiveFromLabel: "Effective From",
      effectiveToLabel: "Effective To (Optional)",
      submitButton: "Publish Version"
    },
    newPolicy: {
      title: "New Expense Policy",
      description: "Define policy rule header and code",
      codeLabel: "Policy Code",
      nameLabel: "Policy Name",
      submitButton: "Create Policy"
    },
    newPolicyVersion: {
      title: "Publish Policy Version Rules",
      description: "Specify rules JSON and effective date range",
      rulesLabel: "Policy Rules (JSON format)",
      effectiveFromLabel: "Effective From",
      effectiveToLabel: "Effective To",
      submitButton: "Publish Rules",
      invalidJson: "Invalid JSON format for policy rules"
    },
    newRatePolicy: {
      title: "New Travel/Mileage Rate Policy",
      description: "Set rate allowances for mileage or daily per diem",
      kindLabel: "Allowance Kind",
      kindMileage: "Mileage Allowance",
      kindPerDiem: "Per Diem Allowance",
      rateLabel: "Rate / Amount",
      currencyLabel: "Currency",
      effectiveFromLabel: "Effective From",
      effectiveToLabel: "Effective To",
      vehicleTypeLabel: "Vehicle Type (Mileage)",
      locationCodeLabel: "Location Code (Per Diem)",
      breakfastReductionLabel: "Breakfast Deduction",
      lunchReductionLabel: "Lunch Deduction",
      dinnerReductionLabel: "Dinner Deduction",
      submitButton: "Save Rate Policy"
    },
    importExpenses: {
      title: "Bulk Import Expenses",
      description: "Upload and ingest batch expense records into the system",
      fileLabel: "Records Data (JSON array of rows)",
      submitButton: "Start Import",
      invalidArray: "Input must be a valid JSON array",
      invalidJson: "Invalid JSON syntax"
    }
  },

  drawer: {
    title: "Expense 360 Profile & Details",
    subtitle: "Financial records, receipts, policy audit findings, and approval history",
    overview: "Overview",
    evidenceTab: "Receipts & Documents",
    policyTab: "Policy Audit & Exceptions",
    itemizationTab: "Itemizations",
    participantsTab: "Participants",
    travelTab: "Travel & Allowance",
    dimensionsTab: "Dimensions",
    historyTab: "Audit History",
    noEvidence: "No receipts attached for this expense.",
    addEvidence: "Attach Document",
    missingReceiptDeclared: "A missing receipt declaration has been submitted for this expense with authorized justification.",
    noItemizations: "No itemizations recorded.",
    addItemization: "Add Itemization",
    noParticipants: "No participants recorded.",
    addParticipant: "Add Participant",
    noDimensions: "No dimensions assigned.",
    setDimension: "Set Dimension",
    requestException: "Request Policy Exception",
    approveException: "Approve Exception",
    rejectException: "Reject Exception",
    financialOutcome: "Financial & Ledger Outcome",
    baseAmount: "Amount in Base Currency",
    taxAmount: "Recoverable Tax Amount",
    exceptionReasonPlaceholder: "Explain justification for exceeding policy limit...",
    itemDescriptionPlaceholder: "Item description",
    participantRole: "Role",
    participantNamePlaceholder: "Participant name",
    dimensionCode: "Dimension Code",
    dimensionValue: "Value"
  }
} as const;
