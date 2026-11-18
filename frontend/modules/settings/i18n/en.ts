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
  categories: {
    title: "Module settings",
    description: "Start with inventory settings. Other module settings will be added here as their capabilities become ready.",
    inventory: {
      title: "Inventory settings",
      description: "Inventory adjustment policies, separation of duties, and operational cutoff date.",
      available: "Available now"
    },
    comingSoon: "Coming soon"
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
    save: "Save inventory settings",
    savedVersion: "Last saved version: {{version}}",
    readOnly: "You can view these settings only. Inventory policy management permission is required to edit them.",
    currentScope: "Applies to: {{entity}}"
  }
} as const;
