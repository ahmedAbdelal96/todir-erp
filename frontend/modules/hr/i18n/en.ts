export const en = {
  title: "Human Resources",
  subtitle: "Manage employees, organization structure, attendance, leave, and digital personnel records",
  helpButton: "User Guide & Instructions",
  helpTitle: "Human Resources Guide",
  helpDescription: "This module provides end-to-end management of employees, contracts, organizational assignments, attendance, and leave while strictly respecting privacy and accounting boundaries.",

  tabs: {
    home: "HR Home",
    directory: "Employee Directory",
    attendance: "Attendance",
    leave: "Leave & Balances",
    organization: "Organization",
    selfservice: "Self-Service",
    manager: "My Team",
    reports: "Reports",
    imports: "Migration & Import"
  },

  actions: {
    cancel: "Cancel",
    confirm: "Confirm",
    save: "Save",
    saving: "Saving...",
    loading: "Loading...",
    close: "Close",
    retry: "Retry",
    action: "Action",
    verify: "Verify",
    submit: "Submit",
    submitting: "Submitting...",
    upload: "Upload",
    uploading: "Uploading..."
  },

  employeeStatus: {
    Active: "Active",
    Inactive: "Inactive",
    Archived: "Archived"
  },

  employmentStatus: {
    PreHire: "Pre-Hire",
    Active: "Active",
    Suspended: "Suspended",
    NoticePeriod: "Notice Period",
    Terminated: "Terminated",
    Ended: "Ended"
  },

  employmentType: {
    FullTime: "Full Time",
    PartTime: "Part Time",
    Temporary: "Temporary",
    FixedTerm: "Fixed Term",
    Probationary: "Probationary",
    Intern: "Intern",
    Seasonal: "Seasonal",
    Other: "Other"
  },

  statuses: {
    Active: "Active",
    Inactive: "Inactive",
    Pending: "Pending",
    Approved: "Approved",
    Rejected: "Rejected",
    Cancelled: "Cancelled",
    Verified: "Verified",
    Draft: "Draft",
    Present: "Present",
    Absent: "Absent",
    Exception: "Exception",
    Late: "Late",
    Open: "Open",
    Locked: "Locked",
    PreHire: "Pre-Hire",
    Probationary: "Probationary",
    NoticePeriod: "Notice Period",
    Terminated: "Terminated",
    Ended: "Ended",
    Suspended: "Suspended",
    Archived: "Archived"
  },

  frequencies: {
    Monthly: "Monthly",
    Weekly: "Weekly",
    BiWeekly: "Bi-Weekly",
    Hourly: "Hourly"
  },

  home: {
    title: "HR Operations Center",
    subtitle: "Monitor daily operations, critical reminders, compliance, and expiring documents",
    actions: {
      newEmployee: "New Employee",
      viewAttendance: "Daily Attendance"
    },
    kpi: {
      activeEmployees: "Active Employees",
      activeEmployeesDesc: "Currently active staff across all branches",
      pendingLeave: "Pending Leave Requests",
      pendingLeaveDesc: "Awaiting supervisor or HR manager approval",
      missingAttendance: "Attendance Exceptions",
      missingAttendanceDesc: "Punches with missing checkout records",
      expiringDocs: "Expiring Documents",
      expiringDocsDesc: "Documents expiring within 30 days"
    },
    compliance: {
      title: "Upcoming Compliance Follow-up",
      description: "A concise view of documents and contracts that need review within 30 days.",
      expiringDocuments: "Documents needing follow-up",
      expiringContracts: "Contracts needing follow-up",
      expiringContractsDesc: "Contracts approaching their end date",
      noData: "No compliance follow-up data is available right now."
    },
    states: {
      loading: "Loading the HR summary...",
      error: "The HR summary could not be loaded. Please try again."
    },
    reminders: {
      title: "Operational Alerts & Compliance Tasks",
      empty: "No urgent alerts or reminders at this time.",
      category: "Category",
      task: "Task / Document",
      employee: "Employee",
      dueDate: "Due Date",
      severity: "Severity",
      categories: {
        DocumentExpiry: "Document Expiry",
        MissingCheckout: "Missing Checkout",
        ProbationReview: "Probation Review",
        ContractRenewal: "Contract Renewal"
      },
      severities: {
        danger: "Critical",
        warning: "Warning",
        info: "Info"
      }
    }
  },

  directory: {
    searchPlaceholder: "Search by name, employee number, or position...",
    newEmployeeAction: "New Employee",
    tableAria: "Employee directory table",
    emptyState: "No employees found matching the search criteria.",
    loading: "Loading...",
    viewProfile: "360 Profile",
    totalRecords: "Total records: {{count}}",
    columns: {
      number: "Employee #",
      name: "Employee Name",
      englishName: "English Name",
      startDate: "Hire Date",
      status: "Status",
      actions: "Actions"
    },
    newEmployeeModal: {
      title: "Add New Employee",
      arabicName: "Arabic Name",
      arabicNamePlaceholder: "Official legal name in Arabic...",
      englishName: "English Name",
      englishNamePlaceholder: "Full Name in English...",
      preferredName: "Preferred Name",
      preferredNamePlaceholder: "Preferred workplace name...",
      employmentType: "Employment Type",
      startDate: "Start Date",
      endDateOptional: "End Date (Optional)",
      submit: "Create Employee"
    }
  },

  workspace: {
    employeeNotFound: "Employee record not found.",
    backToDirectory: "Back to Directory",
    header: {
      englishName: "English Name",
      employmentNumber: "Employment #",
      startDate: "Start Date",
      employmentStatus: "Employment Status"
    },
    actions: {
      promoteTransfer: "Promote / Transfer",
      newContract: "New Contract",
      uploadDoc: "Upload Document",
      terminate: "Terminate",
      rehire: "Rehire"
    },
    tabs: {
      overview: "Overview",
      assignments: "Assignments & Promotions",
      contracts: "Contracts & Amendments",
      compensation: "Compensation & Benefits",
      documents: "Digital Personnel File",
      qualifications: "Qualifications & Licenses",
      insurance: "Insurance & Compliance",
      timeline: "History & Timeline"
    },
    overview: {
      currentAssignmentTitle: "Current Assignment",
      documentCompletenessTitle: "File Completeness & Mandatory Docs",
      verifiedCount: "Verified Documents",
      isComplete: "File Status",
      complete: "Complete",
      incomplete: "Incomplete",
      noCurrentAssignment: "No active organizational assignment recorded."
    },
    assignment: {
      historyTitle: "Assignment History & Promotions",
      promotionModalTitle: "Promote Employee & Assign Position",
      transferModalTitle: "Department or Branch Transfer",
      effectiveFrom: "Effective From",
      effectiveTo: "Effective To",
      effectiveToOptional: "Effective To (Optional)",
      reasonCode: "Reason",
      department: "Department",
      position: "Position",
      manager: "Direct Manager",
      version: "Version",
      current: "Current & Active",
      emptyHistory: "No prior assignment history recorded.",
      reasons: {
        Promotion: "Merit or Periodic Promotion",
        Transfer: "Department / Branch Transfer",
        Restructure: "Organizational Restructuring",
        InitialHire: "Initial Hiring",
        InitialHiring: "Initial Hiring"
      }
    },
    contracts: {
      title: "Employment Contracts & Renewals",
      newModalTitle: "New Employment Contract",
      renewModalTitle: "Renew Contract & Add Amendment",
      type: "Contract Type",
      startDate: "Start Date",
      endDate: "End Date",
      probationEndOptional: "Probation End (Optional)",
      workingHoursBasis: "Working hours arrangement",
      workingHoursBasisDescription: "Defines how working hours are calculated for this contract. Leave it blank to use the approved standard hours.",
      workingHoursBasisPlaceholder: "Example: Full-time standard hours",
      autoActivateLabel: "Activate contract immediately upon creation",
      status: "Status",
      actions: "Actions",
      indefinite: "Indefinite Duration",
      empty: "No employment contracts on record.",
      renewAction: "Renew / Extend",
      renewalEffectiveDate: "Renewal Effective Date",
      renewalTermsSummary: "Renewal Terms Summary",
      renewalTermsPlaceholder: "e.g., Renewed for 1 additional year with unchanged benefits...",
      renewalTermsRequired: "Please summarize the renewal or extension terms.",
      renewConfirm: "Confirm Renewal",
      types: {
        Definite: "Definite Duration",
        Indefinite: "Indefinite Duration",
        ProjectBased: "Project-Based",
        Seasonal: "Seasonal"
      }
    },
    compensation: {
      title: "Contractual Compensation & Salary History",
      amount: "Gross Amount",
      currency: "Currency",
      frequency: "Pay Frequency",
      effectiveFrom: "Effective From",
      status: "Status",
      empty: "No compensation agreements on record or permission restricted."
    },
    documents: {
      title: "Official Documents & Evidence",
      uploadModalTitle: "Upload Document to Personnel File",
      category: "Document Category",
      titleLabel: "Document Title",
      titlePlaceholder: "e.g., National ID Copy, Passport Scan...",
      titleRequired: "Document title is required.",
      expiryDate: "Expiry Date",
      expiryDateOptional: "Expiry Date (Optional)",
      fileName: "File Name",
      isSensitiveLabel: "Highly Sensitive (Restrict Access)",
      verified: "Verification",
      verify: "Verify",
      download: "View / Download",
      empty: "No documents uploaded for this employee.",
      fileRequired: "Document file is required.",
      fileTooLarge: "File size exceeds the allowed limit (15 MB).",
      invalidFileType: "File type not supported. Allowed formats: PDF and images.",
      categories: {
        NationalId: "National ID / Iqama",
        Passport: "Passport",
        WorkPermit: "Work Permit",
        Contract: "Signed Contract",
        Certificate: "Degree / Educational Certificate",
        EducationalCertificate: "Degree / Educational Certificate",
        Medical: "Medical Fitness Report",
        MedicalFitness: "Medical Fitness Report",
        Other: "Administrative Document"
      }
    },
    qualifications: {
      title: "Educational Degrees & Professional Credentials",
      level: "Degree Level",
      institution: "Institution / University",
      year: "Graduation Year",
      empty: "No qualifications recorded."
    },
    insurance: {
      title: "Medical & Social Insurance",
      scheme: "Insurance Scheme",
      memberNumber: "Member / Policy Number",
      effectiveFrom: "Effective Date",
      status: "Status",
      empty: "No insurance enrollments recorded."
    },
    timeline: {
      title: "Employee Lifecycle Timeline",
      empty: "No historical timeline events recorded yet."
    },
    termination: {
      modalTitle: "Terminate Employment",
      disclaimer: "This action will schedule employment termination and end active assignments while permanently preserving historical records.",
      reason: "Termination Reason",
      effectiveDate: "Last Working Date",
      reasons: {
        Resignation: "Voluntary Resignation",
        MutualAgreement: "Mutual Separation Agreement",
        ContractExpiration: "Contract Expiration without Renewal",
        Dismissal: "Statutory Dismissal",
        Retirement: "Retirement"
      }
    },
    rehire: {
      modalTitle: "Rehire Former Employee",
      disclaimer: "Rehiring establishes a new employment relationship for the existing employee, preserving prior records without duplicating identity.",
      employmentNumber: "New Employment # (Optional)",
      employmentNumberPlaceholder: "Leave blank for automatic generation...",
      employmentType: "Employment Type",
      startDate: "New Hire Start Date"
    }
  },

  attendance: {
    title: "Attendance Punches & Daily Records",
    tableAria: "Attendance records table",
    emptyState: "No attendance records found for the selected date range.",
    loading: "Loading...",
    missingCheckout: "Missing Checkout",
    selectEmployeeHelper: "Recording attendance requires an employee account or selected employee context",
    noEmploymentSelected: "Please select an employee to record attendance",
    filters: {
      fromDate: "From Date",
      toDate: "To Date"
    },
    actions: {
      punchIn: "Manual Check-In",
      punchOut: "Manual Check-Out",
      correct: "Correct Punch"
    },
    columns: {
      date: "Date",
      plannedSchedule: "Planned Schedule",
      firstIn: "First In",
      lastOut: "Last Out",
      workedHours: "Worked Hours",
      status: "Status",
      lockState: "Payroll Lock",
      actions: "Actions"
    },
    correctionModal: {
      title: "Correct Attendance Punch & Set Check-Out",
      recordDate: "Work Date",
      checkoutTime: "Actual Check-Out Time",
      reason: "Correction Reason",
      reasonPlaceholder: "e.g., Device offline, external client assignment..."
    }
  },

  leave: {
    title: "Leave Management & Requests",
    subtitle: "Submit requests, monitor leave usage, and track approvals",
    typeRequired: "Please select a leave type.",
    employeeRequired: "Please select the employee for this leave request.",
    selectEmployeeHelper: "Submitting a leave request requires an employee account or selected employee",
    daysUnit: "days",
    tableAria: "Leave requests table",
    emptyState: "No leave requests found in this register.",
    loading: "Loading...",
    columns: {
      from: "From Date",
      to: "To Date",
      chargedDays: "Charged Days",
      reason: "Reason",
      status: "Status",
      actions: "Actions"
    },
    actions: {
      newRequest: "New Leave Request",
      approve: "Approve",
      cancel: "Cancel Request"
    },
    newModal: {
      title: "Request Leave",
      leaveType: "Leave Type",
      fromDate: "From Date",
      toDate: "To Date",
      reasonOptional: "Reason / Notes (Optional)",
      reasonPlaceholder: "Enter additional details if applicable..."
    }
  },

  organization: {
    departmentsTitle: "Departments",
    positionsTitle: "Positions & Jobs",
    newDepartment: "New Department",
    newPosition: "New Position",
    code: "Code",
    nameAr: "Arabic Name",
    nameEn: "English Name",
    status: "Status",
    noDepartments: "No departments recorded.",
    noPositions: "No positions recorded."
  },

  selfservice: {
    title: "Employee Self-Service Portal",
    noProfileLinked: "No employee profile is currently linked to your user account.",
    employmentNumber: "Employment #",
    employmentType: "Contract Type",
    status: "Status"
  },

  manager: {
    leaveApprovalsTitle: "Pending Leave Approvals for My Team",
    teamTitle: "Direct Reports",
    from: "From Date",
    to: "To Date",
    chargedDays: "Requested Days",
    reason: "Reason",
    actions: "Actions",
    employeeNumber: "Employee #",
    name: "Employee Name",
    department: "Department",
    position: "Position",
    status: "Status",
    approveAction: "Approve",
    rejectAction: "Reject",
    noPendingApprovals: "No pending leave requests awaiting approval.",
    noTeamMembers: "No direct reports currently assigned to you."
  },

  reports: {
    title: "HR Reports & Compliance",
    attendanceReport: "Detailed Attendance Report",
    attendanceDesc: "Working hours, late arrivals, and absence records across staff",
    leaveBalancesReport: "Leave Balances & Movements Report",
    leaveBalancesDesc: "Annual leave usage, accrued amounts, and carryover status",
    expiriesReport: "Expiring Documents & Contracts",
    expiriesDesc: "Summary of resident permits, passports, and contracts nearing expiration",
    exportExcel: "Export as Excel"
  },

  imports: {
    title: "HR Data Migration & Import",
    description: "Import employee records from legacy spreadsheets with pre-validation and matching before committing.",
    previewAction: "Preview & Validate File",
    commitAction: "Confirm & Commit Import",
    totalRows: "Total Rows",
    importedRows: "Ready Rows",
    rejectedRows: "Rejected Rows",
    committedSuccess: "Data successfully imported and committed into the database.",
    resultsTable: "Validation Results",
    rowRef: "Row Ref",
    status: "Status",
    issues: "Issues / Errors",
    none: "None"
  },

  errors: {
    generic: "An unexpected error occurred while processing the request.",
    forbidden: "You do not have sufficient permissions to perform this action.",
    employeeArabicNameRequired: "Arabic name is required for employee creation.",
    employeeNameTooLong: "Employee name exceeds maximum length.",
    employmentNumberInvalid: "Employment number is invalid or duplicate.",
    employmentDateRangeInvalid: "Employment start date must precede end date.",
    leaveBalanceInsufficient: "Insufficient leave balance for this request.",
    leaveDatesOverlap: "Leave request dates overlap with an existing approved or pending request.",
    attendancePayrollLocked: "Attendance record is locked by finalized payroll and cannot be modified directly.",
    attendanceCorrectionReasonRequired: "A reason is mandatory for manual attendance corrections.",
    assignmentDateInvalid: "Assignment effective date range is invalid.",
    contractDateRangeInvalid: "Contract end date must be after the start date.",
    contractAlreadyEnded: "Contract is already ended and cannot be modified.",
    shiftBreakExceedsDuration: "Shift break duration cannot exceed total shift length.",
    disciplinaryAmountNotAllowed: "Monetary amount is not allowed on disciplinary actions directly; payroll rules govern deductions.",
    adjustmentPayrollInputInvalid: "Invalid payroll impact parameters for this HR adjustment.",
    insuranceAlreadyActive: "Active insurance enrollment already exists for this scheme.",
    fileScanFailed: "Document security scan failed or file rejected by antivirus scanner.",
    employeeStale: "The record was modified by another user. Please refresh and try again."
  }
} as const;
