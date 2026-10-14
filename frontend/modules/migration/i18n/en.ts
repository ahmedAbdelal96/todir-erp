export const en = {
  title: "Import wizard",
  wizard: {
    title: "Party data import wizard", uploadTitle: "Upload file", uploadDescription: "Choose a data file (CSV / Excel)",
    mappingTitle: "Map fields", mappingDescription: "Connect file columns to system fields", validationTitle: "Validate and test",
    validationStepDescription: "Check data quality and run a preview", executionTitle: "Run import", executionDescription: "Import records into the database",
    displayName: "Business / company name", legalName: "Legal name", taxId: "Tax ID (VAT)", partyType: "Party type (organization / individual)", email: "Email address", phone: "Phone number",
    selectFile: "Choose the data file to import", supportedFormats: "Supported formats: CSV and Excel (.xlsx). Maximum file size: 25 MB.", selectedFile: "Selected file", records: "records",
    mapColumnsTitle: "Map file columns to system fields", mapColumnsDescription: "Match each imported column to the corresponding field in the Directory.",
    sourceColumn: "File column", targetField: "System field", fieldStatus: "Field status", mapColumn: "Map {{column}}", ignoreColumn: "— Ignore this column —",
    requiredField: "Required *", optionalField: "Optional", validationResults: "Validation and test run results",
    validationDescription: "The data structure was checked against validation rules before any records are written to the database.", totalRecords: "Total records",
    validRecords: "Records ready to import", invalidRecords: "Records with errors", sampleErrors: "Detected errors (sample):",
    sampleErrorTax: "Row {{row}}: The tax ID is invalid. It must contain 15 digits and start with 3.", sampleErrorEmail: "Row {{row}}: The email address format is invalid.",
    sampleErrorName: "Row {{row}}: The business name is empty but required.", completeTitle: "Partial import completed",
    completeDescription: "{{created}} records were imported into the Directory. {{errors}} invalid records were excluded, and an error reconciliation report was created.",
    downloadErrors: "Download excluded error report (CSV)", backToDirectory: "Back to Directory"
  }
} as const;
