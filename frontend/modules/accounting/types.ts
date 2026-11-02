import type { DecimalString, CurrencyCode } from "../../components/contracts/domain-values.ts";

// ============================================================================
// Ledger Modes & Readiness Contracts
// ============================================================================

export type LedgerMode = "Managed" | "Full";

export type LedgerReadinessState = "Ready" | "Warning" | "Blocked";

export interface LedgerReadinessReason {
  readonly code: string;
  readonly message: string;
  readonly capability?: string | null;
}

export interface LedgerCapabilityReadiness {
  readonly capability: string;
  readonly state: LedgerReadinessState;
  readonly reasons: readonly LedgerReadinessReason[];
}

export interface LedgerReadinessDto {
  readonly legalEntityId: string;
  readonly mode: LedgerMode;
  readonly state: LedgerReadinessState;
  readonly capabilities: readonly LedgerCapabilityReadiness[];
  readonly version: number;
}

export interface ManagedLedgerSetupRequest {
  readonly templateCode?: string;
  readonly mode?: LedgerMode;
  readonly fiscalYearStart?: string | null;
  readonly fiscalYearEnd?: string | null;
}

export interface ManagedLedgerSetupDto {
  readonly legalEntityId: string;
  readonly mode: LedgerMode;
  readonly initialized: boolean;
  readonly configurationVersionId: string;
  readonly accountsCreated: number;
  readonly journalsCreated: number;
  readonly rolesCreated: number;
  readonly mappingsCreated: number;
  readonly periodsCreated: number;
  readonly readiness: LedgerReadinessDto;
}

export interface LedgerModeDto {
  readonly legalEntityId: string;
  readonly mode: LedgerMode;
  readonly version: number;
}

// ============================================================================
// Chart of Accounts & Dimensions
// ============================================================================

export type AccountType = "Asset" | "Liability" | "Equity" | "Revenue" | "Expense";
export type NormalBalance = "Debit" | "Credit";
export type AccountStatus = "Active" | "Archived";

export interface CreateAccountRequest {
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly type: AccountType;
  readonly subtype: string;
  readonly parentAccountId?: string | null;
  readonly allowDirectPosting?: boolean;
}

export interface AccountDto {
  readonly id: string;
  readonly legalEntityId: string;
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly type: AccountType;
  readonly subtype: string;
  readonly normalBalance: NormalBalance;
  readonly parentAccountId?: string | null;
  readonly allowDirectPosting: boolean;
  readonly status: AccountStatus;
  readonly version: number;
}

export interface AccountSearchPageDto {
  readonly items: readonly AccountDto[];
  readonly nextCursor?: string | null;
}

export interface AccountBalanceDto {
  readonly accountId: string;
  readonly debit: number;
  readonly credit: number;
  readonly balance: number;
  readonly currencyCode: string;
  readonly from?: string | null;
  readonly to?: string | null;
}

export interface AuditSummaryDto {
  readonly action: string;
  readonly entityId: string;
  readonly actorId?: string | null;
  readonly occurredAt: string;
  readonly reason?: string | null;
}

export interface AccountWorkspaceDto {
  readonly account: AccountDto;
  readonly recentActivity: readonly GeneralLedgerRowDto[];
  readonly balance: AccountBalanceDto;
  readonly dimensions: readonly EntryExplanationDimensionDto[];
  readonly configuration: readonly LedgerReadinessReason[];
  readonly audit: readonly AuditSummaryDto[];
}

// ============================================================================
// Journals
// ============================================================================

export type JournalType =
  | "Sales"
  | "Purchases"
  | "Bank"
  | "Cash"
  | "Inventory"
  | "Payroll"
  | "General"
  | "Opening"
  | "Fx"
  | "Tax";

export interface CreateJournalRequest {
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly type: JournalType;
}

export interface JournalDto {
  readonly id: string;
  readonly legalEntityId: string;
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly type: JournalType;
  readonly status: string;
  readonly version: number;
}

// ============================================================================
// Fiscal Years, Periods & Locks
// ============================================================================

export type FiscalYearStatus = "Open" | "Closed";
export type PeriodStatus = "Open" | "SoftClosed" | "Closed";
export type FinancialLockType = "General" | "Tax" | "Advisor" | "Module";

export interface CreateFiscalYearRequest {
  readonly code: string;
  readonly startDate: string;
  readonly endDate: string;
}

export interface FiscalYearDto {
  readonly id: string;
  readonly legalEntityId: string;
  readonly code: string;
  readonly startDate: string;
  readonly endDate: string;
  readonly status: FiscalYearStatus;
  readonly version: number;
}

export interface CreatePeriodRequest {
  readonly fiscalYearId: string;
  readonly code: string;
  readonly startDate: string;
  readonly endDate: string;
  readonly sequence: number;
}

export interface PeriodDto {
  readonly id: string;
  readonly fiscalYearId: string;
  readonly code: string;
  readonly startDate: string;
  readonly endDate: string;
  readonly sequence: number;
  readonly status: PeriodStatus;
  readonly version: number;
}

export interface PeriodCloseCheckDto {
  readonly periodId: string;
  readonly ready: boolean;
  readonly checks: readonly LedgerReadinessReason[];
  readonly warnings: readonly LedgerReadinessReason[];
  readonly blockers: readonly LedgerReadinessReason[];
}

export interface CreateFinancialLockRequest {
  readonly lockType: FinancialLockType;
  readonly startDate: string;
  readonly endDate?: string | null;
  readonly reason: string;
}

export interface FinancialLockDto {
  readonly id: string;
  readonly legalEntityId: string;
  readonly lockType: FinancialLockType;
  readonly startDate: string;
  readonly endDate?: string | null;
  readonly status: string;
  readonly reason: string;
  readonly version: number;
}

// ============================================================================
// Journal Entries & Explanation
// ============================================================================

export interface EntryLineDto {
  readonly sequence: number;
  readonly accountId: string;
  readonly debit: number;
  readonly credit: number;
  readonly currencyCode: string;
  readonly amount: number;
  readonly role: string;
  readonly description?: string | null;
}

export interface JournalEntryDto {
  readonly id: string;
  readonly journalId: string;
  readonly factId?: string | null;
  readonly entryNumber: string;
  readonly postingDate: string;
  readonly periodDate: string;
  readonly status: string;
  readonly currencyCode: string;
  readonly lines: readonly EntryLineDto[];
}

export interface JournalEntryPageDto {
  readonly items: readonly JournalEntryDto[];
  readonly nextCursor?: string | null;
}

export interface ReverseEntryRequest {
  readonly reversalDate: string;
  readonly reason: string;
  readonly autoReversalDate?: string | null;
}

export interface EntryExplanationDimensionDto {
  readonly code: string;
  readonly valueId: string;
  readonly valueCode: string;
}

export interface EntryExplanationLineDto {
  readonly lineId: string;
  readonly sequence: number;
  readonly accountRole: string;
  readonly accountId: string;
  readonly accountCode: string;
  readonly accountName: string;
  readonly mappingId?: string | null;
  readonly mappingVersion?: number | null;
  readonly mappingVersionId?: string | null;
  readonly debit: number;
  readonly credit: number;
  readonly currencyCode: string;
  readonly transactionDebit: number;
  readonly transactionCredit: number;
  readonly baseDebit: number;
  readonly baseCredit: number;
  readonly exchangeRate?: number | null;
  readonly whyThisAccount: string;
  readonly dimensions: readonly EntryExplanationDimensionDto[];
}

export interface EntryExplanationDto {
  readonly entryId: string;
  readonly entryNumber: string;
  readonly journalId: string;
  readonly factId?: string | null;
  readonly postingDate: string;
  readonly accountingPeriod: string;
  readonly factType: string;
  readonly factVersion: number;
  readonly postingRuleVersionId: string;
  readonly accountingConfigurationVersionId: string;
  readonly baseCurrencyCode: string;
  readonly sourceModule: string;
  readonly sourceReference: string;
  readonly lines: readonly EntryExplanationLineDto[];
}

export interface AccountDeterminationExplanation {
  readonly accountRole: string;
  readonly mappingReference: string;
  readonly mappingVersion: number;
  readonly mappingId: string;
  readonly mappingVersionId: string;
  readonly selectedAccountCode: string;
  readonly selectedAccountId: string;
  readonly legalEntityId: string;
  readonly specificityRank: number;
  readonly specificityContext: string;
}

export interface LedgerPostingResult {
  readonly factId: string;
  readonly journalEntryId: string;
  readonly entryNumber: string;
  readonly status: string;
  readonly alreadyPosted: boolean;
  readonly determinations?: readonly AccountDeterminationExplanation[] | null;
}

// ============================================================================
// General Journal Drafts & Posting Preview
// ============================================================================

export type GeneralJournalDraftStatus = "Draft" | "Submitted" | "Approved" | "Posted";

export interface GeneralJournalLineRequest {
  readonly accountId: string;
  readonly debit: number;
  readonly credit: number;
  readonly transactionCurrencyCode: string;
  readonly transactionDebit: number;
  readonly transactionCredit: number;
  readonly baseDebit: number;
  readonly baseCredit: number;
  readonly exchangeRate?: number;
  readonly dimensionValueIds?: readonly string[] | null;
  readonly description?: string | null;
}

export interface CreateGeneralJournalDraftRequest {
  readonly journalId: string;
  readonly postingDate: string;
  readonly description: string;
  readonly baseCurrencyCode: string;
  readonly idempotencyKey: string;
  readonly lines: readonly GeneralJournalLineRequest[];
}

export interface GeneralJournalDraftDto {
  readonly id: string;
  readonly legalEntityId: string;
  readonly journalId: string;
  readonly postingDate: string;
  readonly description: string;
  readonly baseCurrencyCode: string;
  readonly status: GeneralJournalDraftStatus;
  readonly lines: readonly GeneralJournalLineRequest[];
}

export interface PostingPreviewRequest {
  readonly postingDate: string;
  readonly journalId: string;
  readonly description: string;
  readonly baseCurrencyCode: string;
  readonly lines: readonly GeneralJournalLineRequest[];
}

export interface PostingPreviewLineDto {
  readonly sequence: number;
  readonly accountId: string;
  readonly debit: number;
  readonly credit: number;
  readonly baseDebit: number;
  readonly baseCredit: number;
  readonly currencyCode: string;
  readonly transactionDebit: number;
  readonly transactionCredit: number;
  readonly exchangeRate?: number | null;
  readonly accountRole: string;
  readonly mappingId?: string | null;
  readonly mappingVersion?: number | null;
  readonly mappingVersionId?: string | null;
  readonly description?: string | null;
  readonly dimensions: readonly EntryExplanationDimensionDto[];
}

export interface PostingPreviewDto {
  readonly journalId: string;
  readonly postingDate: string;
  readonly postingRuleVersionId: string;
  readonly accountingConfigurationVersionId: string;
  readonly baseCurrencyCode: string;
  readonly lines: readonly PostingPreviewLineDto[];
  readonly totalDebit: number;
  readonly totalCredit: number;
  readonly warnings: readonly LedgerReadinessReason[];
}

// ============================================================================
// Opening Balances
// ============================================================================

export interface OpeningBalanceLineRequest {
  readonly accountId: string;
  readonly debit: number;
  readonly credit: number;
  readonly currencyCode: string;
  readonly baseDebit: number;
  readonly baseCredit: number;
  readonly exchangeRate?: number;
  readonly legacyAccountCode?: string | null;
  readonly legacyReference?: string | null;
}

export interface CreateOpeningBalanceBatchRequest {
  readonly batchIdentity: string;
  readonly fileHash: string;
  readonly legacySystem?: string | null;
  readonly goLiveDate: string;
  readonly lines: readonly OpeningBalanceLineRequest[];
}

export interface OpeningBalancePreviewDto {
  readonly batchId: string;
  readonly balanced: boolean;
  readonly totalDebit: number;
  readonly totalCredit: number;
  readonly validationErrors: readonly string[];
  readonly lines: readonly OpeningBalanceLineRequest[];
}

// ============================================================================
// Reports: Trial Balance, General Ledger, Reconciliation
// ============================================================================

export interface TrialBalanceRowDto {
  readonly accountId: string;
  readonly accountCode: string;
  readonly accountName: string;
  readonly debit: number;
  readonly credit: number;
  readonly balance: number;
  readonly currencyCode: string;
}

export interface TrialBalancePageDto {
  readonly items: readonly TrialBalanceRowDto[];
  readonly nextCursor?: string | null;
  readonly totalCount: number;
  readonly totalDebit: number;
  readonly totalCredit: number;
}

export interface GeneralLedgerRowDto {
  readonly entryId: string;
  readonly lineId: string;
  readonly entryNumber: string;
  readonly postingDate: string;
  readonly accountId: string;
  readonly accountCode: string;
  readonly accountName: string;
  readonly debit: number;
  readonly credit: number;
  readonly baseDebit: number;
  readonly baseCredit: number;
  readonly currencyCode: string;
  readonly sourceModule: string;
  readonly sourceReference: string;
  readonly sequence: number;
  readonly runningBalance?: number | null;
}

export interface GeneralLedgerPageDto {
  readonly items: readonly GeneralLedgerRowDto[];
  readonly nextCursor?: string | null;
}

export interface ProjectionReconciliationDifferenceDto {
  readonly accountId: string;
  readonly periodId: string;
  readonly fiscalYearId?: string | null;
  readonly currencyCode: string;
  readonly canonicalDebit: number;
  readonly projectionDebit: number;
  readonly debitDifference: number;
  readonly canonicalCredit: number;
  readonly projectionCredit: number;
  readonly creditDifference: number;
  readonly canonicalBalance: number;
  readonly projectionBalance: number;
  readonly balanceDifference: number;
  readonly status: string;
}

export interface ProjectionReconciliationResultDto {
  readonly healthy: boolean;
  readonly differences: readonly ProjectionReconciliationDifferenceDto[];
}

export interface ProjectionRebuildResult {
  readonly rowsRebuilt: number;
  readonly reconciled: boolean;
}

// ============================================================================
// Dimensions Management
// ============================================================================

export interface DimensionDefinitionRequest {
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly valueOwnerModule?: string;
}

export interface DimensionValueRequest {
  readonly definitionId: string;
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly externalOwnerModule?: string | null;
  readonly externalReferenceId?: string | null;
}

export interface DimensionPolicyRequest {
  readonly definitionId: string;
  readonly scopeType: string;
  readonly scopeReference: string;
  readonly requirement: "Required" | "Optional" | "Forbidden";
}

// ============================================================================
// Module Pack 16: Accounting Health, Reconciliations & Statements Contracts
// ============================================================================

export type HealthSeverity = "Info" | "Attention" | "Blocking";
export type HealthOverallStatus = "Healthy" | "Attention" | "Blocking";

export interface AccountingHealthItem {
  readonly code: string;
  readonly severity: HealthSeverity;
  readonly status: string;
  readonly message: string;
  readonly source?: string | null;
}

export interface AccountingHealthView {
  readonly legalEntityId: string;
  readonly asOfDate: string;
  readonly status: HealthOverallStatus;
  readonly items: readonly AccountingHealthItem[];
}

export interface OwnerFinancialSummary {
  readonly legalEntityId: string;
  readonly asOfDate: string;
  readonly currencyCode: string;
  readonly cashBookValue: number;
  readonly receivables: number;
  readonly payables: number;
  readonly inventoryValue?: number | null;
  readonly profit: number;
  readonly health: HealthOverallStatus;
}

export type ReconciliationType = "AR" | "AP" | "Inventory";
export type ReconciliationStatus = "Reconciled" | "Difference" | "NotReady" | "NotApplicable";

export interface SubledgerReconciliationView {
  readonly id: string;
  readonly type: string;
  readonly asOfDate: string;
  readonly sourceBalance?: number | null;
  readonly ledgerBalance?: number | null;
  readonly difference?: number | null;
  readonly currencyCode?: string | null;
  readonly status: ReconciliationStatus;
  readonly diagnostics: readonly string[];
  readonly createdAt: string;
}

export type ReconciliationRunView = SubledgerReconciliationView;

export interface ReconciliationIssueView {
  readonly id: string;
  readonly reconciliationRunId: string;
  readonly code: string;
  readonly severity: string;
  readonly status: string;
  readonly createdAt: string;
  readonly resolvedAt?: string | null;
}

export interface ReconciliationPageDto {
  readonly items: readonly SubledgerReconciliationView[];
  readonly nextCursor?: string | null;
}

export interface ReconciliationIssuePageDto {
  readonly items: readonly ReconciliationIssueView[];
  readonly nextCursor?: string | null;
}

export interface CloseCheckView {
  readonly code: string;
  readonly severity: string;
  readonly status: string;
  readonly message: string;
}

export interface ClosePreviewRequest {
  readonly periodId: string;
  readonly idempotencyKey: string;
  readonly reason?: string | null | undefined;
}

export interface CloseRunView {
  readonly id: string;
  readonly periodId: string;
  readonly status: string;
  readonly checks: readonly CloseCheckView[];
  readonly createdAt: string;
}

export interface ReopenPeriodRequest {
  readonly reason: string;
  readonly idempotencyKey: string;
}

export interface ApproveGoLiveRequest {
  readonly openingBatchId: string;
  readonly subledgerOpeningBatchId?: string | null;
  readonly financialGoLiveDate?: string | null;
  readonly reason: string;
  readonly idempotencyKey: string;
}

export interface GoLiveApprovalView {
  readonly legalEntityId: string;
  readonly openingBatchId: string;
  readonly subledgerOpeningBatchId?: string | null;
  readonly financialGoLiveDate: string;
  readonly status: string;
  readonly checks: readonly CloseCheckView[];
  readonly approvedAt: string;
}

export interface FxRevaluationRequest {
  readonly asOfDate: string;
  readonly idempotencyKey: string;
  readonly autoReversalDate?: string | null;
}

export interface FxRevaluationLineView {
  readonly id: string;
  readonly openItemId: string;
  readonly direction: string;
  readonly transactionCurrency: string;
  readonly openTransactionAmount: number;
  readonly baseCurrency: string;
  readonly carryingBaseAmount: number;
  readonly revaluedBaseAmount: number;
  readonly differenceAmount: number;
  readonly differenceRole: string;
  readonly exchangeRate: number;
  readonly journalEntryId?: string | null;
  readonly reversalEntryId?: string | null;
  readonly exchangeRateSnapshot: string;
}

export interface FxRevaluationRunView {
  readonly id: string;
  readonly asOfDate: string;
  readonly status: string;
  readonly autoReversalDate?: string | null;
  readonly lines: readonly FxRevaluationLineView[];
  readonly createdAt: string;
}

export interface FxRevaluationReversalRequest {
  readonly reversalDate: string;
  readonly reason: string;
}

export interface StatementLineView {
  readonly code: string;
  readonly name: string;
  readonly accountType: string;
  readonly amount: number;
  readonly accountIds: readonly string[];
  readonly lineKind: string;
  readonly parentLineCode?: string | null;
  readonly nameAr?: string | null;
  readonly nameEn?: string | null;
}

export type StatementKind = "ProfitAndLoss" | "BalanceSheet";

export interface FinancialStatementView {
  readonly kind: StatementKind;
  readonly fromDate: string;
  readonly toDate: string;
  readonly currencyCode: string;
  readonly total: number;
  readonly lines: readonly StatementLineView[];
  readonly isBalanced: boolean;
  readonly diagnostic?: string | null;
}

export interface FinancialStatementDefinitionLineRequest {
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly sequence: number;
  readonly accountType?: string | null;
  readonly accountIds?: readonly string[] | null;
  readonly lineKind?: string;
  readonly parentLineCode?: string | null;
  readonly arithmeticOperator?: string | null;
  readonly formulaLineCodes?: readonly string[] | null;
}

export interface FinancialStatementDefinitionRequest {
  readonly code: string;
  readonly kind: string;
  readonly lines: readonly FinancialStatementDefinitionLineRequest[];
  readonly effectiveFrom?: string | null;
}

export interface FinancialStatementDefinitionView {
  readonly id: string;
  readonly code: string;
  readonly kind: string;
  readonly status: string;
  readonly version: number;
  readonly lines: readonly FinancialStatementDefinitionLineRequest[];
}


export type JournalStatus = "Draft" | "Posted" | "Reversed";

export interface Account {
  readonly code: string;
  readonly name: string;
  readonly type: AccountType;
  readonly currency: CurrencyCode;
}

export interface JournalLine {
  readonly id: string;
  readonly accountCode: string;
  readonly accountName: string;
  readonly description: string;
  readonly debit: DecimalString;
  readonly credit: DecimalString;
  readonly costCenter?: string | undefined;
  readonly reference?: string | undefined;
}

export interface JournalEntry {
  readonly id: string;
  readonly entryNumber: string;
  readonly date: string;
  readonly description: string;
  readonly status: JournalStatus;
  readonly period: string;
  readonly isPeriodClosed: boolean;
  readonly lines: readonly JournalLine[];
  readonly debitTotal: DecimalString;
  readonly creditTotal: DecimalString;
  readonly isBalanced: boolean;
  readonly postedAt?: string | undefined;
  readonly postedBy?: string | undefined;
}

export interface JournalValidationResult {
  readonly isValid: boolean;
  readonly isBalanced: boolean;
  readonly debitTotal: DecimalString;
  readonly creditTotal: DecimalString;
  readonly difference: DecimalString;
  readonly lineErrors?: readonly {
    readonly lineIndex: number;
    readonly field: string;
    readonly message: string;
  }[] | undefined;
}
