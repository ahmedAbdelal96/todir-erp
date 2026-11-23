/**
 * Module 27 Quality Management â€” Authoritative English Localization (en)
 * Complete 1:1 parity with qualityAr.
 */

export const qualityEn = {
  navigation: {
    quality: "Quality Management",
    overview: "Quality Overview",
    inspections: "Inspections & Testing",
    plans: "Quality Plans & Specs",
    characteristics: "Test Characteristics",
    samplingPlans: "Sampling Plans",
    restrictions: "Holds & Restrictions",
    dispositions: "Dispositions & MRB",
    issues: "Quality Issues Log",
    ncrs: "Non-Conformance Reports (NCR)",
    capas: "Corrective Actions (CAPA)",
    supplierQuality: "Supplier Quality & SCAR",
    certificates: "Certificates (COA / COC)"
  },

  tabs: {
    overview: "Overview & Priorities",
    inspections: "Inspections",
    plans: "Plans & Specs",
    characteristics: "Characteristics",
    samplingPlans: "Sampling Plans",
    restrictions: "Holds & Restrictions",
    dispositions: "Technical Dispositions",
    issues: "Issues",
    ncrs: "NCRs",
    capas: "CAPAs",
    supplierQuality: "Supplier Quality & SCAR",
    certificates: "Certificates (COA)"
  },

  kpi: {
    pendingInspections: "Pending Inspections",
    failedInspections: "Failed / Non-Conforming",
    activeHolds: "Active Material Holds",
    pendingDispositions: "Pending Dispositions",
    openNcrs: "Open NCRs",
    openCapas: "Active CAPAs",
    overdueScars: "Overdue SCARs"
  },

  status: {
    Draft: "Draft",
    Pending: "Pending",
    InProgress: "In Progress",
    Completed: "Completed",
    Passed: "Passed",
    Failed: "Failed",
    Concession: "Concession",
    Active: "Active",
    Released: "Released",
    NotRequired: "Not Required",
    Executing: "Executing Inventory",
    Confirmed: "Confirmed",
    Unknown: "Unknown (Reconciliation Required)",
    Open: "Open",
    UnderInvestigation: "Under Investigation",
    Resolved: "Resolved",
    Closed: "Closed",
    Investigating: "Investigating",
    ActionPending: "Action Pending",
    UnderReview: "Under Review",
    Ineffective: "Ineffective (Reopened)",
    Low: "Low Risk",
    Medium: "Medium Risk",
    High: "High Risk",
    Critical: "Critical Risk",
    Issued: "Issued",
    ResponsePending: "Response Pending",
    Accepted: "Accepted",
    Rejected: "Rejected",
    Revoked: "Revoked",
    Superseded: "Superseded"
  },

  eventTypes: {
    Receiving: "Goods Receiving (Purchasing)",
    InProcess: "In-Process (Manufacturing)",
    Final: "Final Product Inspection",
    RoutingOperation: "Specific Routing Operation",
    Shipping: "Customer Outbound Shipping",
    Periodic: "Periodic / Shelf-Life Inspection"
  },

  dataTypes: {
    Numeric: "Numeric Standard",
    Text: "Text / Technical Note",
    Boolean: "Pass / Fail (Boolean)"
  },

  samplingMethods: {
    FixedSize: "Fixed Sample Size",
    Percentage: "Batch Percentage",
    AqlStandard: "Military Standard (ISO 2859 / AQL)"
  },

  decisions: {
    UseAsIs: "Use As Is (Concession)",
    Rework: "Rework / Reprocess",
    Scrap: "Scrap / Dispose",
    ReturnToSupplier: "Return to Supplier",
    Regrade: "Downgrade / Regrade"
  },

  actions: {
    createPlan: "Create Quality Plan",
    createVersion: "Create New Version",
    activate: "Activate Version",
    addApplicability: "Add Applicability Scope",
    createCharacteristic: "Add Characteristic",
    createSpecification: "Create Specification",
    addPlanCharacteristic: "Add Plan Characteristic",
    createSamplingPlan: "Create Sampling Plan",
    createInspection: "Create Inspection Order",
    enterResults: "Record Measurements",
    completeInspection: "Complete Inspection",
    reinspect: "Reinspect",
    createRestriction: "Hold / Restrict Lot",
    releaseRestriction: "Release Stock (Partial/Full)",
    createDisposition: "Create Disposition",
    approveDisposition: "Approve Disposition",
    executeDisposition: "Execute Inventory Effect",
    reconcileDisposition: "Reconcile Effect",
    createIssue: "Log Quality Issue",
    createNcr: "Create NCR",
    addContainment: "Add Immediate Containment",
    createCapa: "Create CAPA",
    addAction: "Assign Corrective Action",
    completeAction: "Complete Action",
    addRootCause: "Add Root Cause",
    recordEffectiveness: "Record Effectiveness",
    saveSupplierProfile: "Save Supplier Quality Policy",
    createScar: "Issue SCAR to Supplier",
    submitScarResponse: "Record Supplier Response",
    reviewScar: "Review & Accept Response",
    issueCertificate: "Issue Certificate (COA/COC)",
    printCertificate: "Print Official Certificate",
    filter: "Filter Results",
    clearFilters: "Reset",
    refresh: "Refresh Data",
    cancel: "Cancel",
    save: "Save",
    saving: "Saving...",
    completing: "Completing...",
    complete: "Complete",
    submitting: "Submitting...",
    submit: "Submit",
    confirming: "Confirming...",
    confirm: "Confirm",
    issuing: "Issuing...",
    close: "Close",
    viewDetails: "View Details",
    openWorkspace: "Open Inspection Workbench"
  },

  fields: {
    legalEntity: "Legal Entity",
    code: "Code",
    name: "Name",
    status: "Status",
    eventType: "Inspection Event Type",
    lotQuantity: "Lot Quantity",
    lotReference: "Supplier / Internal Lot Number",
    sampleSize: "Required Sample Size",
    sampledQuantity: "Actually Sampled Quantity",
    defectCount: "Defect Count",
    conformingCount: "Conforming Quantity",
    nonConformingCount: "Non-Conforming Quantity",
    evaluation: "Final Evaluation",
    reinspectionCount: "Reinspection Cycles",
    holdReason: "Hold Reason",
    initialQuantity: "Initial Held Quantity",
    releasedQuantity: "Released Quantity",
    remainingQuantity: "Remaining Held Quantity",
    targetWarehouse: "Target Warehouse",
    targetLocation: "Target Location / Bin",
    reworkOrderNumber: "Rework Order Number",
    scrapReasonCode: "Scrap Reason Code",
    supplierRmaNumber: "Supplier RMA Number",
    notes: "Notes"
  },

  severity: {
    minor: "Minor",
    major: "Major",
    critical: "Critical"
  },

  risk: {
    low: "Low Risk",
    medium: "Medium Risk",
    high: "High Risk",
    critical: "Critical Risk"
  },

  issues: {
    title: "Issue Title",
    titlePlaceholder: "e.g. Hairline cracks observed on cast brackets",
    issueType: "Issue Type & Source",
    typeReceiving: "Receiving Inspection Defect",
    typeInProcess: "In-Process / Assembly Defect",
    typeCustomerComplaint: "Customer Complaint",
    typeAuditFinding: "Audit Finding",
    description: "Detailed Description",
    severity: "Severity Level",
    inspectionId: "Linked Inspection ID (Optional)",
    supplierPartyId: "Supplier Party ID (Optional)",
    optionalReference: "Optional — leave blank if not applicable",
    createTitle: "Log New Quality Issue"
  },

  ncrs: {
    title: "NCR Title",
    titlePlaceholder: "e.g. Inner diameter tolerance deviation in batch",
    description: "Defect Description & Out-of-Spec Justification",
    severity: "Severity",
    createTitle: "Create Non-Conformance Report (NCR)",
    addContainmentTitle: "Add Immediate Containment Action",
    containmentType: "Containment Action Type",
    quarantineStock: "Quarantine Inventory Immediately",
    haltProduction: "Halt Production Line",
    customerNotification: "Notify Impacted Customers",
    supplierHold: "Suspend Supplier Purchase Orders",
    actionDescription: "Containment Action Taken to Prevent Escape",
    productVariantId: "Product Variant ID",
    lotId: "Lot Number",
    quantity: "Impacted Quantity"
  },

  capas: {
    title: "CAPA Title",
    titlePlaceholder: "e.g. Thermal drift remediation in annealing ovens",
    type: "CAPA Type",
    typeCorrective: "Corrective Action (Prevent Recurrence)",
    typePreventive: "Preventive Action (Prevent Occurrence)",
    problemStatement: "Problem Statement (5W2H)",
    createTitle: "Create CAPA Case",
    addActionTitle: "Assign Execution Action",
    actionType: "Action Type",
    actionCorrective: "Direct Corrective Action",
    actionPreventive: "Root Cause Preventive Action",
    actionProcess: "SOP Process Modification",
    actionDescription: "Action Required Description",
    ownerId: "Assigned Owner",
    ownerPlaceholder: "Owner name or employee code",
    dueDate: "Due Date",
    completeActionTitle: "Confirm Action Completion",
    evidenceReference: "Implementation Evidence Reference",
    evidencePlaceholder: "Report number or verification doc URL",
    addRootCauseTitle: "Document Root Cause (Ishikawa 6M)",
    rootCauseCategory: "Root Cause Category",
    categoryMan: "Personnel & Training (Man)",
    categoryMachine: "Equipment & Calibration (Machine)",
    categoryMaterial: "Raw Materials & Suppliers (Material)",
    categoryMethod: "SOP & Work Instructions (Method)",
    categoryMeasurement: "Gauges & Measurement Precision (Measurement)",
    categoryEnvironment: "Work Environment (Environment)",
    rootCauseDescription: "Investigated Root Cause Explanation",
    rootCauseVerified: "Empirically Verified & Replicated",
    recordEffectivenessTitle: "Record & Certify CAPA Effectiveness",
    effectivenessResult: "Effectiveness Evaluation Result",
    effectiveConfirmed: "Effective (Target Achieved — Close CAPA)",
    ineffectiveRecurring: "Ineffective (Recurring Defect — Reopen)",
    evaluationNotes: "Evaluation Findings & Production Data Notes"
  },

  suppliers: {
    partyId: "Supplier Party ID",
    partyIdPlaceholder: "Supplier code in party directory",
    riskLevel: "Assigned Risk Level",
    targetPpm: "Target Defect PPM",
    receivingInspectionRequired: "Mandatory Receiving Inspection on All Shipments",
    createProfile: "Register Supplier Quality Policy",
    editProfile: "Edit Supplier Quality Parameters"
  },

  scars: {
    createTitle: "Issue Supplier Corrective Action Request (SCAR)",
    supplierPartyId: "Supplier Code",
    supplierPlaceholder: "Supplier code",
    title: "SCAR Title",
    titlePlaceholder: "e.g. Oxidized terminal shipments delivered",
    description: "Defect Details, Financial Impact & Requested 8D Plan",
    ncrId: "Linked NCR Number (Optional)",
    dueDate: "8D Submission Due Date",
    submitResponseTitle: "Record Supplier 8D Plan Response",
    responseExplanation: "Supplier's Containment, Root Cause, and Correction Plan",
    evidenceReference: "Supplier 8D Document Reference",
    reviewTitle: "Review & Validate Supplier Response",
    reviewDecision: "Review Decision",
    decisionAccepted: "Accept Response & Close SCAR (Accepted)",
    decisionRejected: "Reject Response & Request Revision (Rejected)",
    reviewNotes: "Quality Assurance Committee Review Notes"
  },

  certificates: {
    issueTitle: "Issue Quality Certificate (COA / COC)",
    certificateType: "Official Certificate Type",
    typeCoa: "Certificate of Analysis (COA)",
    typeCoc: "Certificate of Conformance (COC)",
    customerReference: "Customer PO / Order Reference",
    customerPlaceholder: "Customer purchase order or order reference",
    documentTitle: "Official Quality Certificate Document",
    coaFullName: "Certified Laboratory Certificate of Analysis (COA)",
    cocFullName: "Certified Certificate of Manufacturing Conformance (COC)",
    number: "Certificate Serial Number",
    inspectionId: "Source Inspection Reference",
    issuedAt: "Certification Date & Time",
    verificationStatus: "Integrity & Digital Verification",
    tamperProofVerified: "Tamper-Proof Digitally Verified",
    certificationStatement: "This certifies that the sampled units from this production lot have been thoroughly tested and found in full compliance with all technical, chemical, and physical specifications defined in the applicable quality standards."
  },

  messages: {
    loading: "Loading data from server...",
    noData: "No records found matching the current criteria.",
    notFound: "The requested record was not found or has been deleted.",
    success: "Operation completed successfully.",
    confirmAction: "Are you sure you want to execute this action? Confirmed operations cannot be undone.",
    passedNote: "Strict Operational Policy: Inspection Passed does not release held inventory automatically; explicit release is required.",
    reconcileNote: "Inventory Reconciliation: For pending inventory effects, status is queried and retried using the existing idempotency key to prevent double execution."
  }
};

export const en = qualityEn;
