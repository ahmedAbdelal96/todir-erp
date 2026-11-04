export type PayrollGroupFrequency = "Monthly" | "Weekly" | "Biweekly" | "SemiMonthly";
export type HolidayAdjustment = "NextWorkingDay" | "PreviousWorkingDay" | "None";
export type PayrollPeriodStatus = "Open" | "Processing" | "Finalized" | "Cancelled";
export type PayrollRunType = "Regular" | "OffCycle" | "Retro" | "FinalSettlement" | "Adjustment" | "BonusOnly" | "Retroactive";
export type CalculationState = "Draft" | "Captured" | "Calculating" | "Calculated" | "Failed";
export type FinalizationState = "Open" | "Finalized" | "Cancelled";
export type PaymentState = "NotReady" | "Ready" | "Requested" | "Partial" | "Paid" | "Failed";
export type ApprovalState = "NotRequired" | "Pending" | "Approved" | "Rejected";
export type StatutoryMode = "NotManaged" | "Manual" | "Automatic";
export type ManualInputPolicy = "Block" | "Skip";
export type ReceivableType = "Advance" | "Loan";
export type ReceivableStatus = "Draft" | "Approved" | "Disbursed" | "Paused" | "Settled" | "WrittenOff" | "Cancelled";
export type LoanActionType = "Pause" | "Resume" | "Reschedule" | "ExternalRepayment" | "EarlySettlement" | "WriteOff";
export type OpeningBalanceType = "NetPay" | "Tax" | "Insurance" | "Loan" | "Advance" | "Other";
export type ComponentCategory = "Earning" | "EmployeeDeduction" | "EmployerContribution" | "Informational" | "Recovery" | "Deduction";
export type ComponentDirection = "Credit" | "Debit" | "Memo";
export type ComponentInputMode = "Formula" | "Manual" | "Provider" | "Import" | "TimeAndAttendance";

export interface PayrollComponent {
  readonly id: string;
  readonly code: string;
  readonly name: string;
  readonly category: ComponentCategory;
  readonly direction: ComponentDirection;
  readonly inputMode: ComponentInputMode;
  readonly status: "Active" | "Retired" | "Draft";
}

export interface CreatePayrollComponentRequest {
  readonly code: string;
  readonly name: string;
  readonly category: string;
  readonly direction: string;
  readonly inputMode: string;
}

export interface CreatePayrollComponentVersionRequest {
  readonly effectiveFrom: string;
  readonly formula: unknown;
  readonly roundingScale?: number;
  readonly providerCode?: string | null;
}

export interface PayrollComponentVersion {
  readonly id: string;
  readonly componentId: string;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly status: string;
  readonly roundingScale: number;
  readonly providerCode?: string | null;
}

export interface SalaryStructure {
  readonly id: string;
  readonly legalEntityId: string;
  readonly code: string;
  readonly name: string;
  readonly status: "Active" | "Archived" | "Draft";
  readonly componentsCount?: number;
}

export interface CreateSalaryStructureRequest {
  readonly legalEntityId: string;
  readonly code: string;
  readonly name: string;
  readonly currencyCode: string;
}

export interface CreateSalaryStructureVersionRequest {
  readonly effectiveFrom: string;
  readonly currencyCode: string;
}

export interface SalaryStructureVersion {
  readonly id: string;
  readonly salaryStructureId: string;
  readonly versionNumber: number;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly status: string;
  readonly currencyCode: string;
}

export interface EmployeePayrollAssignment {
  readonly id: string;
  readonly employmentId: string;
  readonly employeeName?: string | null;
  readonly employmentNumber?: string | null;
  readonly payrollGroupId: string;
  readonly payrollGroupName?: string | null;
  readonly baseSalary: number;
  readonly currencyCode: string;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
}

export interface CreatePayrollAssignmentRequest {
  readonly legalEntityId: string;
  readonly payrollGroupId: string;
  readonly salaryStructureVersionId: string;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly baseSalary: number;
  readonly currencyCode: string;
}

export interface PayrollGroup {
  readonly id: string;
  readonly legalEntityId: string;
  readonly code: string;
  readonly name: string;
  readonly frequency: PayrollGroupFrequency;
  readonly payDay: number;
  readonly currencyCode: string;
  readonly status: string;
  readonly holidayAdjustment: HolidayAdjustment;
}

export interface PayrollPeriod {
  readonly id: string;
  readonly payrollGroupId: string;
  readonly periodStart: string;
  readonly periodEnd: string;
  readonly inputCutoff: string;
  readonly plannedPayDate: string;
  readonly status: PayrollPeriodStatus;
}

export interface PayrollRun {
  readonly id: string;
  readonly payrollGroupId: string;
  readonly periodId?: string | null;
  readonly runNumber: string;
  readonly runType: PayrollRunType;
  readonly calculationState: CalculationState;
  readonly finalizationState: FinalizationState;
  readonly paymentState: PaymentState;
  readonly totalGross: number;
  readonly totalDeductions: number;
  readonly totalNet: number;
  readonly employeeCount: number;
}

export interface PayrollEmployee {
  readonly employmentId: string;
  readonly status: string;
  readonly gross: number;
  readonly deductions: number;
  readonly net: number;
  readonly employerCost: number;
  readonly exceptionCode?: string | null;
}

export interface Payslip {
  readonly id: string;
  readonly payslipNumber: string;
  readonly employmentId: string;
  readonly gross: number;
  readonly deductions: number;
  readonly net: number;
  readonly employerCost: number;
  readonly status: string;
  readonly finalizedAt?: string | null;
  readonly employeeName?: string | null;
  readonly employmentNumber?: string | null;
}

export interface PayslipsPage {
  readonly items: readonly Payslip[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
  readonly hasMore: boolean;
}

export interface EmployeeReceivable {
  readonly id: string;
  readonly employmentId: string;
  readonly type: ReceivableType;
  readonly principal: number;
  readonly outstandingPrincipal: number;
  readonly currencyCode: string;
  readonly status: ReceivableStatus;
  readonly employeeName?: string | null;
  readonly employmentNumber?: string | null;
}

export interface ReceivableDisbursement {
  readonly id: string;
  readonly receivableId: string;
  readonly amount: number;
  readonly currencyCode: string;
  readonly status: string;
  readonly providerReference?: string | null;
}

export interface PayrollStatutorySettings {
  readonly id: string;
  readonly legalEntityId: string;
  readonly taxMode: StatutoryMode;
  readonly insuranceMode: StatutoryMode;
  readonly effectiveFrom: string;
  readonly version: number;
  readonly createdAt: string;
  readonly manualInputPolicy: ManualInputPolicy;
}

export interface UpdatePayrollStatutorySettingsRequest {
  readonly taxMode: StatutoryMode;
  readonly insuranceMode: StatutoryMode;
  readonly effectiveFrom: string;
  readonly expectedVersion: number;
  readonly manualInputPolicy?: ManualInputPolicy;
}

export interface PayrollInput {
  readonly id: string;
  readonly employmentId: string;
  readonly componentId: string;
  readonly inputDate: string;
  readonly amount: number;
  readonly currencyCode: string;
  readonly source: string;
  readonly status: string;
  readonly notes?: string | null;
  readonly employeeName?: string | null;
  readonly employmentNumber?: string | null;
  readonly componentCode?: string | null;
  readonly componentName?: string | null;
}

export interface PayrollSnapshot {
  readonly snapshotId: string;
  readonly runId: string;
  readonly periodStart: string;
  readonly periodEnd: string;
  readonly cutoffAt: string;
  readonly payloadSha256: string;
  readonly employmentCount: number;
}

export interface PayrollLoanActionRequest {
  readonly action: LoanActionType;
  readonly amount?: number | null;
  readonly effectiveDate?: string | null;
  readonly newInstallmentAmount?: number | null;
  readonly newDueDate?: string | null;
  readonly externalReference?: string | null;
  readonly reason?: string | null;
  readonly idempotencyKey?: string | null;
}

export interface PayrollLoanAction {
  readonly id: string;
  readonly receivableId: string;
  readonly action: string;
  readonly amount?: number | null;
  readonly effectiveDate: string;
  readonly externalReference?: string | null;
  readonly status: string;
}

export interface PayrollReportItem {
  readonly runId: string;
  readonly runNumber: string;
  readonly runType: string;
  readonly periodEnd?: string | null;
  readonly finalizationState: string;
  readonly paymentState: string;
  readonly gross: number;
  readonly deductions: number;
  readonly net: number;
  readonly employeeCount: number;
  readonly postedToLedger?: boolean;
  readonly accountingState?: string;
  readonly journalEntryNumber?: string | null;
}

export interface PayrollAccountingReconciliation {
  readonly runId: string;
  readonly accountingState: string;
  readonly isPosted: boolean;
  readonly journalEntryNumber?: string | null;
  readonly journalEntryId?: string | null;
  readonly totalGross: number;
  readonly totalNet: number;
  readonly currencyCode: string;
}

export interface PayrollReportSummary {
  readonly items: readonly PayrollReportItem[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
  readonly totalGross: number;
  readonly totalDeductions: number;
  readonly totalNet: number;
}

export interface PayrollImportRowRequest {
  readonly rowNumber: number;
  readonly rowKey: string;
  readonly amount: number;
  readonly currencyCode: string;
  readonly employmentId: string;
  readonly componentCode: string;
  readonly inputDate: string;
  readonly notes?: string | null;
}

export interface PayrollImportRequest {
  readonly importType: string;
  readonly sourceSystem: string;
  readonly batchReference: string;
  readonly fileSha256: string;
  readonly rows: readonly PayrollImportRowRequest[];
  readonly supplementalRunId?: string | null;
}

export interface PayrollImportRowResult {
  readonly rowNumber: number;
  readonly rowKey: string;
  readonly status: string;
  readonly issueCode?: string | null;
  readonly inputId?: string | null;
}

export interface PayrollImportResult {
  readonly batchId: string;
  readonly status: string;
  readonly rowCount: number;
  readonly rows: readonly PayrollImportRowResult[];
}

export interface PayrollOpeningBalanceRequest {
  readonly employmentId: string;
  readonly balanceType: OpeningBalanceType;
  readonly amount: number;
  readonly currencyCode: string;
  readonly asOfDate: string;
  readonly sourceReference: string;
  readonly installmentAmount?: number | null;
  readonly installmentCount?: number | null;
}

export interface PayrollOpeningBalance {
  readonly id: string;
  readonly employmentId: string;
  readonly balanceType: OpeningBalanceType;
  readonly amount: number;
  readonly currencyCode: string;
  readonly asOfDate: string;
  readonly sourceReference: string;
  readonly status: string;
}

export interface CreatePayrollGroupRequest {
  readonly legalEntityId: string;
  readonly code: string;
  readonly name: string;
  readonly frequency: PayrollGroupFrequency;
  readonly payDay: number;
  readonly currencyCode: string;
  readonly holidayAdjustment?: HolidayAdjustment;
}

export interface CreatePayrollPeriodRequest {
  readonly payrollGroupId: string;
  readonly periodStart: string;
  readonly periodEnd: string;
  readonly inputCutoff: string;
  readonly calculationDate: string;
  readonly plannedPayDate: string;
}

export interface CreatePayrollRunRequest {
  readonly payrollGroupId: string;
  readonly periodId?: string | null;
  readonly runType?: PayrollRunType;
  readonly sourceRunId?: string | null;
  readonly sourcePeriodId?: string | null;
  readonly settlementEmploymentId?: string | null;
  readonly effectiveDate?: string | null;
  readonly reasonCode?: string | null;
  readonly notes?: string | null;
}

export interface CreatePayrollInputRequest {
  readonly legalEntityId: string;
  readonly employmentId: string;
  readonly componentId: string;
  readonly inputDate: string;
  readonly amount: number;
  readonly currencyCode: string;
  readonly source: string;
  readonly idempotencyKey?: string | null;
  readonly notes?: string | null;
  readonly supplementalRunId?: string | null;
}

export interface CreateReceivableRequest {
  readonly legalEntityId: string;
  readonly employmentId: string;
  readonly type: ReceivableType;
  readonly principal: number;
  readonly currencyCode: string;
  readonly installmentAmount?: number | null;
  readonly installmentCount?: number | null;
}
