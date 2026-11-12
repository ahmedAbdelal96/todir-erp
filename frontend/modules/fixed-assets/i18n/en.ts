export const en = {
  moduleTitle: "Fixed Assets & Custody",
  moduleSubtitle: "Enterprise asset register, custody tracking, depreciation lifecycle, and capitalization reconciliation",
  tabs: {
    register: "Asset Register",
    custody: "Custody & Operations",
    capitalization: "Capitalization & CIP",
    depreciation: "Books & Depreciation",
    reports: "Reconciliation & Reports",
    myAssets: "My Assigned Assets"
  },
  kpis: {
    totalAssets: "Total Registered Assets",
    grossCost: "Total Historical Cost",
    accumulatedDepreciation: "Accumulated Depreciation",
    netBookValue: "Net Book Value (NBV)",
    activeCustodyCount: "Assets Under Active Custody",
    cipActiveCost: "Construction in Progress (CIP)"
  },
  states: {
    Draft: "Draft",
    UnderConstruction: "Under Construction",
    Active: "Active / In Service",
    FullyDepreciated: "Fully Depreciated",
    Disposed: "Disposed",
    WrittenOff: "Written Off"
  },
  natures: {
    Land: "Land",
    Building: "Building & Construction",
    LeaseholdImprovement: "Leasehold Improvement",
    Vehicle: "Vehicle & Fleet",
    Equipment: "Machinery & Equipment",
    Furniture: "Office Furniture & Fixtures",
    Tool: "Tools & Instruments",
    CIP: "Construction in Progress",
    Intangible: "Intangible Asset / Software"
  },
  tracking: {
    Individual: "Individually Serialized",
    Grouped: "Grouped Homogeneous",
    Bulk: "Bulk / Volumetric"
  },
  assignmentTypes: {
    Employee: "Employee Custody",
    Department: "Department / Division",
    Location: "Operational Site",
    Unassigned: "Unassigned / Warehouse"
  },
  verificationStatuses: {
    Draft: "Draft",
    Active: "In Progress",
    Completed: "Completed & Closed",
    Cancelled: "Cancelled"
  },
  verificationResults: {
    Found: "Found & Matching",
    Missing: "Missing / Not Found",
    WrongLocation: "Wrong Location",
    WrongCustodian: "Wrong Custodian",
    Damaged: "Damaged / Maintenance Required"
  },
  depreciationStatuses: {
    Draft: "Draft",
    Calculated: "Calculated & Ready",
    Posted: "Posted to General Ledger",
    Reversed: "Reversed"
  },
  candidateStatuses: {
    Pending: "Pending Review",
    Reviewed: "Reviewed & Approved",
    Allocated: "Fully Allocated",
    Rejected: "Rejected / Ineligible"
  },
  candidateSources: {
    SupplierInvoice: "Supplier Invoice",
    InventoryIssue: "Inventory Internal Issue",
    ExpenseClaim: "Expense Claim",
    PurchasingOrder: "Received Purchase Order",
    Manual: "Manual Approved Entry"
  },
  disposalTypes: {
    Scrap: "Scrap / Decommission",
    Loss: "Loss",
    Theft: "Theft",
    Sale: "Capital Asset Sale",
    Donation: "Donation"
  },
  actions: {
    createAsset: "New Asset",
    assignAsset: "Assign / Transfer Custody",
    returnAsset: "Return Custody",
    recordHandover: "Record Handover",
    scanLookup: "Barcode / QR Scan",
    massTransfer: "Mass Custody Transfer",
    addCost: "Capital Cost Addition",
    estimateChange: "Estimate Change",
    impairment: "Record Impairment",
    revaluation: "Revaluation Proposal",
    dispose: "Dispose Asset",
    sell: "Sell Asset",
    reinstate: "Reinstate Asset",
    newCategory: "New Category",
    newLocation: "New Location",
    newBook: "New Asset Book",
    addBookToAsset: "Attach Book to Asset",
    prepareDepreciation: "Prepare Depreciation Run",
    postDepreciation: "Post to Ledger",
    reverseDepreciation: "Reverse Depreciation",
    newCampaign: "New Verification Campaign",
    verifyAsset: "Record Verification Result",
    closeCampaign: "Close Campaign",
    reviewCandidate: "Review Source",
    allocateCandidate: "Allocate Capital Cost",
    correctAllocation: "Correct Allocation",
    newCip: "New CIP Project",
    addCipCost: "Add Cost to CIP",
    capitalizeCip: "Capitalize CIP Project",
    splitCip: "Split CIP Project",
    combineCip: "Combine Multi-CIP",
    splitGrouped: "Split Grouped Asset",
    exportCsv: "Export Asset Register (CSV)",
    drilldown: "Transaction Source Drilldown",
    refresh: "Refresh Data",
    close: "Close",
    cancel: "Cancel",
    confirm: "Confirm",
    submit: "Save & Commit",
    save: "Save",
    filter: "Filter"
  },
  register: {
    title: "Asset Register & Custody",
    searchPlaceholder: "Search by asset number, name, tag, or serial...",
    filterState: "Asset Status",
    filterNature: "Asset Nature",
    filterAll: "All",
    colNumber: "Asset Number",
    colName: "Asset Name",
    colCategory: "Category & Nature",
    colLocation: "Physical Location",
    colCustodian: "Custodian / Responsibility",
    colState: "Lifecycle Status",
    colQty: "Quantity",
    colCost: "Gross Cost",
    colNbv: "Net Book Value (NBV)",
    actions: "Actions",
    emptyTitle: "No assets found matching the criteria",
    emptyDescription: "No fixed assets match the selected filter parameters. You can register a new asset to get started.",
    noPermissionTitle: "Unauthorized to view financial records",
    noPermissionDescription: "You lack the required permission to inspect financial and book values of fixed assets."
  },
  drawer: {
    title: "Asset 360° Profile",
    overviewTab: "Overview & Technical Info",
    booksTab: "Books & Accounting Balances",
    custodyTab: "Custody, Locations & Handover",
    scheduleTab: "Depreciation Schedule Projection",
    documentsTab: "Documents & Evidence",
    timelineTab: "Change History & Audit",
    drilldownTab: "Cost Entries & Provenance",
    identityCard: "Asset Identity & Classification",
    tagLabel: "Internal Asset Tag / Barcode",
    serialLabel: "Manufacturer Serial Number",
    productRefLabel: "Catalog Product Reference",
    acquisitionDateLabel: "Acquisition / Purchase Date",
    capitalizationDateLabel: "Approved Capitalization Date",
    placedInServiceLabel: "Placed in Service Date",
    quantityLabel: "Current Quantity",
    currentLocationLabel: "Current Approved Location",
    currentCustodianLabel: "Current Custodian / Responsible Person",
    parentAssetLabel: "Parent Group / Assembly",
    booksSummary: "Registered Asset Accounting Books",
    noBooksDescription: "This asset is not attached to any financial books yet.",
    scheduleTitle: "Projected Financial Depreciation Over Useful Life",
    scheduleEmpty: "Depreciation schedule is not available or asset is non-depreciating (e.g. Land).",
    timelineEmpty: "No audit timeline events found for this asset.",
    drilldownEmpty: "No cost transaction history recorded for this asset."
  },
  custody: {
    title: "Field Operations & Custody Verification",
    subtitle: "Manage physical sites, handover tracking, custody transfers, and physical audit campaigns",
    locationsTitle: "Operational Sites & Buildings",
    campaignsTitle: "Physical Verification Campaigns",
    massTransferTitle: "Mass Custody Transfer",
    scanTitle: "Barcode & QR Code Quick Scan",
    noLocations: "No operational locations registered yet.",
    noCampaigns: "No active verification campaigns.",
    startCampaignNotice: "A verification campaign creates an immutable snapshot of registered assets to conduct physical audits without interrupting business operations."
  },
  capitalization: {
    title: "Capitalization & Capital Projects (CIP)",
    subtitle: "Manage acquisition costs originating from invoices and inventory, and track Construction in Progress",
    candidatesTitle: "Eligible Capitalization Sources",
    cipTitle: "Construction in Progress (CIP)",
    noCandidates: "No pending capitalization source items.",
    noCip: "No CIP projects under construction.",
    allocateHint: "A source candidate can be allocated to a single asset or split across multiple assets preserving balanced financial integrity."
  },
  depreciation: {
    title: "Asset Books & Monthly Depreciation Runs",
    subtitle: "Manage corporate and statutory books, calculate depreciation, and post journal entries to the General Ledger",
    booksTitle: "Approved Asset Books",
    runsTitle: "Monthly Depreciation Runs Log",
    noBooks: "No asset books defined yet.",
    noRuns: "No depreciation runs performed yet.",
    postConfirm: "Are you sure you want to post this depreciation run to the General Ledger? This will generate immutable financial entries.",
    reverseConfirm: "Reversing this run will generate compensating reversal entries in the General Ledger. A business reason is required."
  },
  reports: {
    title: "Asset Register Reports & Ledger Reconciliation",
    subtitle: "Reconcile asset books with General Ledger control accounts, and export statutory register records for audits",
    reconciliationTitle: "Book-to-GL Reconciliation",
    registerReportTitle: "Comprehensive Asset Register as-of Date",
    exportBtn: "Export Register to CSV",
    asOfDateLabel: "As of Date",
    balancedStatus: "Fully Balanced with Zero Variance",
    unbalancedStatus: "Variance Detected — Review Required",
    grossDiff: "Gross Cost Variance",
    depDiff: "Accumulated Depreciation Variance"
  },
  myAssets: {
    title: "My Assigned Company Assets",
    subtitle: "List of all equipment, vehicles, and devices assigned to your personal custody",
    noAssets: "No company assets currently assigned to your custody.",
    returnAction: "Request Return / Handover"
  },
  modals: {
    newAsset: {
      title: "Register New Fixed Asset",
      description: "Enter asset identification, classification, nature, and technical attributes",
      nameLabel: "Asset Name *",
      namePlaceholder: "e.g. Dell Latitude 7440 Laptop",
      categoryLabel: "Asset Category *",
      assetTagLabel: "Internal Tag / Barcode",
      serialLabel: "Manufacturer Serial Number",
      quantityLabel: "Quantity *",
      acquisitionDateLabel: "Acquisition Date *",
      placedInServiceLabel: "Placed in Service Date",
      parentAssetLabel: "Parent Group / Assembly (Optional)",
      descriptionLabel: "Additional Description & Notes"
    },
    assign: {
      title: "Assign / Transfer Asset Custody",
      description: "Specify the employee, department, or location responsible for the asset",
      typeLabel: "Assignment Target Type *",
      employeeLabel: "Receiving Employee",
      departmentLabel: "Department / Division",
      locationLabel: "Physical Location / Office",
      custodianLabel: "Operational Custodian Name",
      effectiveFromLabel: "Effective Start Date *",
      effectiveToLabel: "Expected End Date (Optional)",
      conditionLabel: "Physical Condition at Handover",
      accessoriesLabel: "Accessories & Included Equipment",
      ackRefLabel: "Handover Acknowledgement Reference"
    },
    return: {
      title: "Return Asset Custody to Warehouse",
      description: "Document asset return, terminate custody, and assign warehouse storage location",
      effectiveFromLabel: "Return Date *",
      locationLabel: "Storage Location / Warehouse *",
      conditionLabel: "Physical Condition Upon Return",
      accessoriesLabel: "Returned Accessories",
      ackRefLabel: "Warehouse Receipt Acknowledgement"
    },
    dispose: {
      title: "Dispose Fixed Asset",
      description: "Record asset retirement and calculate capital gain or loss",
      typeLabel: "Disposal Method / Reason *",
      dateLabel: "Accounting Disposal Date *",
      proceedsLabel: "Disposal Proceeds (if applicable)",
      directCostsLabel: "Direct Decommissioning / Disposal Costs",
      reasonLabel: "Business Justification *",
      quantityLabel: "Disposed Quantity (for partial disposal)"
    },
    reinstate: {
      title: "Reinstate Disposed Asset",
      description: "Restore previously disposed asset and create compensating accounting reversals",
      dateLabel: "Approved Reinstatement Date *",
      reasonLabel: "Reinstatement Justification *"
    },
    cipCapitalize: {
      title: "Fully Capitalize CIP Project",
      description: "Convert capital work-in-progress into an active in-service fixed asset",
      targetCategoryLabel: "Final Operating Asset Category *",
      targetNameLabel: "In-Service Asset Name",
      capitalizationDateLabel: "Capitalization Date *",
      placedInServiceLabel: "Placed in Service Date *"
    },
    prepareRun: {
      title: "Prepare Monthly Depreciation Run",
      description: "Calculate depreciation entries for all book assets over the target period",
      bookLabel: "Asset Book *",
      periodStartLabel: "Period Start Date *",
      periodEndLabel: "Period End Date *"
    },
    reverseRun: {
      title: "Reverse Posted Depreciation Run",
      description: "Cancel posted ledger entries for this run with documented business reason",
      reversalDateLabel: "Reversal Date *",
      reasonLabel: "Authorized Reversal Reason *"
    },
    verification: {
      title: "Record Physical Verification Finding",
      description: "Document field audit observation and location/custody match",
      resultLabel: "Verification Finding *",
      observedLocationLabel: "Observed Physical Location (if different)",
      notesLabel: "Auditor Notes & Observations"
    }
  }
} as const;

export const fixedAssetsEn = en;
