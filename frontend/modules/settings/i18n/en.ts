export const en = {
  title: "Company settings",
  subtitle: "Manage module settings from one place while keeping legal-entity scope explicit.",
  scope: {
    title: "Settings scope",
    description: "Settings apply to the legal entity selected in the top bar and do not affect other entities.",
    currentEntity: "Current legal entity",
    selected: "Selected legal entity",
    unavailable: "No legal entity is selected. Choose one from the top bar to view its settings.",
    unknown: "Not selected"
  },
  workspaces: {
    ariaLabel: "Company settings workspaces",
    company: {
      title: "Company data",
      description: "Company identity and core operating data"
    },
    finance: {
      title: "Finance & tax",
      description: "Currency, finance, and tax policies"
    },
    commercial: {
      title: "Sales & purchasing",
      description: "Commercial policies and trading terms"
    },
    operations: {
      title: "Inventory & operations",
      description: "Inventory, counting, and traceability policies"
    }
  },
  tabs: {
    ariaLabel: "Company settings sections",
    scrollPrev: "Previous sections",
    scrollNext: "Next sections",
    companyProfile: "Company data",
    financialFoundation: "Financial foundation",
    tax: "Tax",
    sales: "Sales",
    purchasing: "Purchasing",
    inventoryPolicy: "Inventory policies",
    warehouses: "Warehouses & locations",
    counting: "Counting & adjustments",
    traceability: "Traceability & valuation"
  },
  sectionUnavailable: {
    title: "This section is not available yet",
    description: "Settings for this section will be added here as its capabilities become ready, while keeping the same legal-entity scope."
  },
  inventory: {
    title: "Inventory policies",
    subtitle: "Set the adjustment approval method and operational controls for the current legal entity.",
    loading: "Loading inventory settings...",
    unavailable: "You do not have permission to view inventory settings.",
    loadError: "Inventory settings could not be loaded. Try again.",
    saveError: "Inventory settings could not be saved. Review the values and try again.",
    saved: "Inventory settings were saved successfully.",
    retry: "Try again",
    edit: "Edit inventory settings",
    editTitle: "Edit inventory settings",
    editDescription: "Update the inventory adjustment policy, then save your changes.",
    cancel: "Cancel",
    approvalMode: "Inventory adjustment approval",
    approvalModeDescription: "Choose whether an adjustment is posted directly or goes through an approval workflow.",
    directConfirm: "Direct posting",
    directConfirmDescription: "The adjustment is confirmed according to the user's permissions.",
    approvalRequired: "Approval required",
    approvalRequiredDescription: "The adjustment waits for approval before its inventory effect is posted.",
    separationOfDuties: "Enable separation of duties",
    separationOfDutiesDescription: "Prevents the adjustment initiator from approving or posting it when approval is enabled.",
    cutoffDate: "Operational cutoff date",
    cutoffDateDescription: "Optional: prevents inventory operations before this date after it is saved.",
    noCutoffDate: "No cutoff date",
    enabled: "Enabled",
    disabled: "Disabled",
    save: "Save inventory settings",
    savedVersion: "Last saved version: {{version}}",
    readOnly: "You can view these settings only. Inventory policy management permission is required to edit them.",
    currentScope: "Applies to: {{entity}}"
  }
} as const;
