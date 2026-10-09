export type ImportEntityType = "Parties" | "Invoices" | "ChartOfAccounts";
export type ImportJobStatus =
  | "Pending"
  | "Validating"
  | "DryRunCompleted"
  | "Executing"
  | "Completed"
  | "PartialSuccess"
  | "Failed";

export interface ImportErrorItem {
  readonly row: number;
  readonly field: string;
  readonly code: string;
  readonly message: string;
  readonly severity: "error" | "warning";
}

export interface ImportJob {
  readonly id: string;
  readonly entityType: ImportEntityType;
  readonly fileName: string;
  readonly totalRows: number;
  readonly processedRows: number;
  readonly successCount: number;
  readonly errorCount: number;
  readonly status: ImportJobStatus;
  readonly errors: readonly ImportErrorItem[];
  readonly createdAt: string;
  readonly completedAt?: string | undefined;
}

export type FieldMapping = Record<string, string>;

export interface TargetFieldDefinition {
  readonly id: string;
  readonly label: string;
  readonly isRequired: boolean;
  readonly example?: string | undefined;
}
