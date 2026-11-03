export const en = {
  language: { arabic: "Arabic", english: "English", switchToArabic: "Switch to Arabic", switchToEnglish: "Switch to English" },
  shell: {
    workspace: "Workspace", legalEntity: "Legal entity", branch: "Branch", emptyWorkspace: "Choose a workspace to begin",
    updatingContext: "Updating context", signOut: "Sign out", modules: "Modules & services", administrator: "System administrator",
    collapseSidebar: "Show sidebar icons only", expandSidebar: "Show sidebar labels",
    enterFullscreen: "Enter full screen", exitFullscreen: "Exit full screen",
    favorites: "Favorites", recentPages: "Recent pages", addFavorite: "Add {{name}} to favorites", removeFavorite: "Remove {{name}} from favorites",
    navigationPreferencesSaveFailed: "Could not save this favorite. Try again.",
    displayPreferencesSaveFailed: "Could not save your display preferences to your account.",
    couldNotLoadSession: "Could not load the session", couldNotUpdateScope: "Could not update the organization scope"
  },
  preferences: {
    language: "Language", theme: "Theme", alternateTheme: "Dark", defaultTheme: "Default",
    density: "Display density", compact: "Compact", comfortable: "Comfortable", profile: "Profile"
  },
  shortcuts: {
    title: "Keyboard shortcuts", description: "Use shortcuts to reach common tools faster.",
    openSearch: "Open quick search", toggleSidebar: "Collapse or expand the sidebar", openHelp: "Show these shortcuts"
  },
  actions: {
    save: "Save", cancel: "Cancel", close: "Close", confirm: "Confirm", retry: "Retry",
    back: "Back", create: "Create", add: "Add", edit: "Edit", delete: "Delete", search: "Search",
    filter: "Filter", clear: "Clear", continue: "Continue", refresh: "Refresh", yes: "Yes", no: "No"
  },
  states: {
    loading: "Loading...", saving: "Saving...", empty: "No data to display",
    error: "The request could not be completed. Try again.", required: "This field is required.",
    active: "Active", inactive: "Inactive", enabled: "Enabled", disabled: "Disabled"
  },
  access: {
    deniedTitle: "Access denied", deniedDescription: "You do not have permission to access this resource.",
    errorTitle: "An unexpected error occurred", errorDescription: "The requested data could not be loaded.",
    shellErrorTitle: "An unexpected application error occurred",
    shellErrorDescription: "A temporary problem occurred while displaying this page. Try again or return to the home page."
  },
  accessibility: { mainNavigation: "Main navigation", closeDialog: "Close dialog" }
  ,pagination: { navigation: "Pagination navigation", total: "Total records:", perPage: "Per page:", previous: "Previous", next: "Next", page: "Page {{page}}" },
  components: {
    loadErrorTitle: "Could not load the data", loadErrorMessage: "An unexpected error occurred while retrieving data from the server. Please try again.",
    retry: "Retry", loading: "Loading data...", searchPlaceholder: "Search...", searchField: "Search field", clearSearch: "Clear search",
    activeFilters: "Active filters: {{count}}", resetFilters: "Reset",
    applyFilters: "Apply", applyingFilters: "Applying...", confirm: "Confirm", cancel: "Cancel", processing: "Processing...",
    fileChoose: "Choose a file", fileDrop: "or drag and drop a file here", fileSelected: "Selected file: {{name}}", removeFile: "Remove file",
    fileTooLarge: "The file \"{{name}}\" exceeds the maximum allowed size ({{size}}).", fileBrowse: "Drag and drop a file here, or click to browse",
    supportedFormats: "Supported formats: {{formats}}", allFileTypes: "All file types are supported", maxFileSize: "Maximum size: {{size}}", deleteFile: "Delete file {{name}}",
    selectOption: "Select an option...", closeDialog: "Close dialog", closeAlert: "Close alert", progress: "Progress indicator",
    validationSummary: "Please correct the following errors before continuing:", unsavedChanges: "You have unsaved changes. Are you sure you want to leave this page?",
    showPassword: "Show password", hidePassword: "Hide password", copyValue: "Copy value", copied: "Copied",
    wizardSteps: "Wizard steps", breadcrumbNavigation: "Breadcrumb navigation",
    noRows: "No records found", noRowsFiltered: "No records match the current search or filters", loadingRecords: "Loading records...",
    createdAt: "Created:", updatedAt: "Last updated:", byActor: "by {{actor}}",
    statusConfirmed: "Confirmed", statusDraft: "Draft", statusCancelled: "Cancelled", statusUnknown: "Status unavailable",
    notifications: "Notifications", closeNotification: "Close notification",
    accountingPosted: "Posted", accountingUnposted: "Not posted", approvalApproved: "Approved",
    approvalRejected: "Rejected", approvalPending: "Pending approval", complianceValid: "Compliant",
    settlementPaid: "Paid in full", settlementPartial: "Partially paid", settlementUnpaid: "Unpaid", documentLocked: "Document is locked",
    createNewOption: "Create new {{name}}...", noOptionsFound: "No matching results"
  }
  ,grid: {
    selectAll: "Select all", searchOoo: "Search...", blanks: "Blanks", noMatches: "No matching options",
    filterOoo: "Filter...", equals: "Equals", notEqual: "Not equal", lessThan: "Less than", greaterThan: "Greater than",
    lessThanOrEqual: "Less than or equal", greaterThanOrEqual: "Greater than or equal", inRange: "In range",
    contains: "Contains", notContains: "Does not contain", startsWith: "Begins with", endsWith: "Ends with",
    noRows: "No records found", page: "Page", of: "of", to: "to", next: "Next", previous: "Previous"
  },
  pageHelp: {
    triggerAriaLabel: "Page help and user guide",
    triggerTooltip: "User guide & page help",
    defaultTitle: "Page User Guide & Help",
    defaultSubtitle: "Instructions and insights to help you complete your daily work easily and quickly",
    purposeTitle: "What is this page for?",
    workflowTitle: "Step-by-step workflow & usage",
    conceptsTitle: "Key concepts & terminology",
    proTipsTitle: "Pro tips to speed up your work",
    faqsTitle: "Frequently asked questions & mistake prevention",
    close: "Understood, close guide"
  },
  commandPalette: {
    title: "Command Palette & Quick Navigation",
    placeholder: "Search screens, quick actions, or shortcuts... (Ctrl + K)",
    noResults: "No matching results found",
    openAria: "Quick Search & Commands",
    triggerPlaceholder: "Search workspace & actions...",
    categoryActions: "Quick Actions",
    categoryNavigation: "System Screens & Modules",
    categoryRecent: "Recent Records & Operations",
    categoryRecords: "Records & Data",
    badgeRecent: "Recent",
    navigate: "Navigate",
    select: "Select",
    dismiss: "Close",
    keyEnter: "Enter",
    keyEsc: "Esc",
    shortcutCtrl: "Ctrl",
    shortcutK: "K",
    spotlightBrand: "TODIR Spotlight",
    subtitleCustomerInvoices: "Manage and create customer invoices",
    subtitleSupplierInvoices: "Manage and record vendor bills",
    subtitleCatalog: "Manage products, services, and pricing",
    subtitleDirectory: "Customer, vendor, and contact directory",
    subtitleSubledger: "Settle and reconcile open accounts",
    subtitleFinbase: "Chart of accounts and ledgers",
    subtitleTax: "Tax declarations and compliance",
    actionNewCustomerInvoice: "Create New Customer Invoice",
    actionNewSupplierInvoice: "Create New Supplier Bill",
    actionNewProduct: "Add New Product to Catalog",
    actionNewParty: "Add New Contact (Customer / Supplier)",
    actionOpenSubledger: "Settle Open Items & Claims",
    actionChartOfAccounts: "Chart of Accounts & Ledgers",
    actionTaxDeclarations: "Tax Declarations & VAT"
  },
  draftRecovery: {
    bannerTitle: "Auto-saved draft found",
    bannerDescription: "Unsaved form data was automatically preserved from your previous session.",
    restoreButton: "Restore Draft",
    discardButton: "Discard & Clear",
    restoredNotice: "Draft restored successfully",
    discardedNotice: "Temporary draft cleared",
    unsavedWarning: "You have unsaved changes. Are you sure you want to leave this page?"
  },
  batchActions: {
    selectedCount: "{{count}} items selected",
    clearSelection: "Clear Selection",
    exportSelected: "Export Selected",
    printSelected: "Batch Print",
    deleteSelected: "Delete Selected",
    changeStatus: "Change Status"
  },
  undoToast: {
    undoAction: "Undo",
    actionSuccess: "Operation completed successfully",
    actionUndone: "Operation undone successfully"
  },
  quickPeek: {
    title: "Quick Peek",
    closeAria: "Close quick peek",
    loading: "Loading preview details...",
    empty: "No additional details to display",
    openFullRecord: "Open Full Record"
  },
  networkStatus: {
    offlineTitle: "No Internet Connection",
    offlineMessage: "Your input is safely saved locally. Reconnection will happen automatically once online.",
    onlineTitle: "Connection Restored",
    onlineMessage: "Successfully reconnected to the system and refreshed data."
  }
} as const;
