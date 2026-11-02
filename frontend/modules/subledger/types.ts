/**
 * Subledger Module Types
 * Reflects canonical backend SubledgerContracts and presentation models.
 */

export type SubledgerDirection = "Receivable" | "Payable";
export type SubledgerPolarity = "Debit" | "Credit";

export type OpenItemStatus =
  | "Open"
  | "PartiallySettled"
  | "Settled"
  | "Reversed"
  | "Cancelled";

export interface PartyOpenItem {
  readonly id: string;
  readonly legalEntityId: string;
  readonly partyId: string;
  readonly direction: SubledgerDirection;
  readonly polarity: SubledgerPolarity;
  readonly sourceType: string;
  readonly sourceReference: string;
  readonly sourceScheduleReference?: string | null;
  readonly sourceDisplayNumber?: string | null;
  readonly documentDate: string;
  readonly postingDate: string;
  readonly dueDate: string;
  readonly transactionCurrency: string;
  readonly originalTransactionAmount: string;
  readonly settledTransactionAmount: string;
  readonly openTransactionAmount: string;
  readonly baseCurrency: string;
  readonly originalBaseAmount: string;
  readonly settledBaseCarryingAmount: string;
  readonly openBaseCarryingAmount: string;
  readonly controlAccountRole: string;
  readonly controlAccountReference: string;
  readonly status: string;
  readonly rowVersion: number;
  readonly paymentTermSnapshot?: unknown;
  readonly historicFxSnapshot?: unknown;
}

export interface OpenItemPage {
  readonly items: readonly PartyOpenItem[];
  readonly nextCursor?: string | null;
}

export interface OpenItemListFilter {
  readonly legalEntityId: string;
  readonly partyId?: string;
  readonly direction?: SubledgerDirection;
  readonly openOnly?: boolean;
  readonly limit?: number;
  readonly cursor?: string | undefined;
  readonly search?: string;
  readonly dueFrom?: string;
  readonly dueTo?: string;
  readonly currencyCode?: string;
  readonly overdueOnly?: boolean;
  readonly asOfDate?: string;
  readonly status?: string;
  readonly sort?: "idAsc" | "dueDateAsc" | "dueDateDesc";
}

export interface PartyCurrencyBalance {
  readonly currencyCode: string;
  readonly amountDue: string;
  readonly availableCredits: string;
  readonly netBalance: string;
  readonly overdueExposure: string;
}

export interface PartyBalanceResult {
  readonly partyId: string;
  readonly legalEntityId: string;
  readonly direction: SubledgerDirection;
  readonly asOfDate: string;
  readonly currencies: readonly PartyCurrencyBalance[];
  readonly creditLimitAmount?: string | null;
  readonly creditLimitCurrency?: string | null;
  readonly availableCreditAmount?: string | null;
  readonly exposureVersion: number;
  readonly asOfInstant?: string;
}

export interface AgingBucketResult {
  readonly currencyCode: string;
  readonly current: string;
  readonly days1To30: string;
  readonly days31To60: string;
  readonly days61To90: string;
  readonly days91Plus: string;
}

export interface PartyStatementLine {
  readonly effectiveDate: string;
  readonly postingDate: string;
  readonly sourceSequence: number;
  readonly sourceId: string;
  readonly sourceType: string;
  readonly sourceReference: string;
  readonly currencyCode: string;
  readonly amount: string;
  readonly runningBalance: string;
}

export interface PartyStatementResult {
  readonly partyId: string;
  readonly legalEntityId: string;
  readonly direction: SubledgerDirection;
  readonly fromDate: string;
  readonly toDate: string;
  readonly lines: readonly PartyStatementLine[];
  readonly closingBalances: Readonly<Record<string, string>>;
  readonly hasMore?: boolean;
  readonly page?: number;
}

export interface PartyFinancialProfile {
  readonly id: string;
  readonly partyId: string;
  readonly legalEntityId: string;
  readonly defaultReceivablePaymentTermId?: string | null;
  readonly defaultPayablePaymentTermId?: string | null;
  readonly creditLimitAmount?: string | null;
  readonly creditLimitCurrency?: string | null;
  readonly receivablePolicyCode?: string | null;
  readonly payablePolicyCode?: string | null;
  readonly version: number;
  readonly updatedAt: string;
}

export interface UpdateFinancialProfilePayload {
  readonly legalEntityId: string;
  readonly defaultReceivablePaymentTermId?: string | null;
  readonly defaultPayablePaymentTermId?: string | null;
  readonly creditLimitAmount?: string | null;
  readonly creditLimitCurrency?: string | null;
  readonly receivablePolicyCode?: string | null;
  readonly payablePolicyCode?: string | null;
  readonly expectedVersion: number;
}

export interface AutoApplySuggestionQuery {
  readonly legalEntityId: string;
  readonly sourceOpenItemId: string;
  readonly reference?: string | null;
  readonly policy: string;
  readonly maximumLines?: number;
}

export interface AutoApplySuggestionLine {
  readonly debitItemId: string;
  readonly creditItemId: string;
  readonly debitAmount: string;
  readonly creditAmount: string;
  readonly expectedDebitVersion: number;
  readonly expectedCreditVersion: number;
  readonly reasonCode: string;
  readonly confidenceClass: string;
  readonly currencyCode: string;
  readonly debitRemaining: string;
  readonly creditRemaining: string;
}

export interface AutoApplySuggestionResult {
  readonly sourceOpenItemId: string;
  readonly sourceAvailable: string;
  readonly currencyCode: string;
  readonly suggestions: readonly AutoApplySuggestionLine[];
  readonly remainingSource: string;
  readonly crossCurrencyExcluded: boolean;
}

export interface ApplicationLineCommand {
  readonly debitItemId: string;
  readonly creditItemId: string;
  readonly amount: string;
  readonly creditAmount?: string | null;
  readonly expectedDebitVersion?: number | null;
  readonly expectedCreditVersion?: number | null;
}

export interface ApplyOpenItemsCommand {
  readonly legalEntityId: string;
  readonly partyId: string;
  readonly direction: SubledgerDirection;
  readonly idempotencyKey: string;
  readonly applicationDate: string;
  readonly settlementDate: string;
  readonly lines: readonly ApplicationLineCommand[];
}

export interface ApplicationPreviewLine {
  readonly debitItemId: string;
  readonly creditItemId: string;
  readonly debitAppliedAmount: string;
  readonly creditAppliedAmount: string;
  readonly expectedDebitVersion: number;
  readonly expectedCreditVersion: number;
  readonly debitRemainingAmount: string;
  readonly creditRemainingAmount: string;
  readonly fxImpactBase: string;
  readonly baseCurrencyCode: string;
}

export interface ApplicationPreviewResult {
  readonly partyId: string;
  readonly legalEntityId: string;
  readonly direction: SubledgerDirection;
  readonly lines: readonly ApplicationPreviewLine[];
  readonly totalDebitApplied: string;
  readonly totalCreditApplied: string;
  readonly totalFxImpactBase: string;
}

export interface SubledgerApplicationResult {
  readonly batchId: string;
  readonly alreadyApplied: boolean;
  readonly applicationIds: readonly string[];
}

export interface ReverseApplicationCommand {
  readonly legalEntityId: string;
  readonly applicationId: string;
  readonly idempotencyKey: string;
  readonly effectiveDate: string;
  readonly reason: string;
}

export interface CreateWriteOffCommand {
  readonly legalEntityId: string;
  readonly openItemId: string;
  readonly amount: string;
  readonly reasonCode: string;
  readonly reasonText: string;
  readonly effectiveDate: string;
  readonly idempotencyKey: string;
}

export interface WriteOffResult {
  readonly id: string;
  readonly requestId: string;
  readonly openItemId: string;
  readonly amount: string;
  readonly currencyCode: string;
  readonly baseAmount: string;
  readonly baseCurrencyCode: string;
  readonly effectiveDate: string;
  readonly ledgerFactId: string;
  readonly approvedBy: string;
  readonly alreadyCompleted: boolean;
}

export interface UpdateWriteOffPolicyPayload {
  readonly legalEntityId: string;
  readonly direction: SubledgerDirection;
  readonly currencyCode: string;
  readonly maximumAmount: string;
  readonly approvalThreshold: string;
  readonly expectedVersion: number;
}

export interface ReconciliationQuery {
  readonly legalEntityId: string;
  readonly asOfDate: string;
}

export interface ReconciliationSourceContext {
  readonly direction: SubledgerDirection;
  readonly controlRole: string;
  readonly controlAccountReference: string;
  readonly controlConfigurationReference: string;
  readonly baseCurrencyCode: string;
  readonly subledgerBaseBalance: number;
}

export interface ReconciliationDetail {
  readonly id: string;
  readonly direction: SubledgerDirection;
  readonly controlRole: string;
  readonly controlAccountReference: string;
  readonly controlConfigurationReference: string;
  readonly ledgerAccountId?: string | null;
  readonly subledgerBaseBalance: string;
  readonly ledgerBaseBalance: string;
  readonly variance: string;
  readonly contextResolved: boolean;
}

export interface ReconciliationResult {
  readonly id: string;
  readonly legalEntityId: string;
  readonly asOfDate: string;
  readonly baseCurrencyCode: string;
  readonly subledgerBaseBalance: string;
  readonly ledgerBaseBalance: string;
  readonly variance: string;
  readonly isBalanced: boolean;
  readonly createdAt: string;
  readonly details: readonly ReconciliationDetail[];
}

export interface CreateOpeningSubledgerBatchCommand {
  readonly legalEntityId: string;
  readonly batchIdentity: string;
  readonly goLiveDate: string;
  readonly baseCurrencyCode: string;
  readonly controlConfigurationReference: string;
  readonly idempotencyKey: string;
}

export interface OpeningSubledgerLineCommand {
  readonly rowKey: string;
  readonly partyId: string;
  readonly direction: SubledgerDirection;
  readonly polarity: SubledgerPolarity;
  readonly sourceReference: string;
  readonly documentDate: string;
  readonly dueDate: string;
  readonly transactionCurrency: string;
  readonly transactionAmount: string;
  readonly baseCurrency: string;
  readonly baseAmount: string;
  readonly controlAccountRole: string;
  readonly controlAccountReference: string;
  readonly controlConfigurationReference: string;
  readonly historicFxSnapshot?: string | null;
}

export interface OpeningSubledgerBatchResult {
  readonly id: string;
  readonly batchIdentity: string;
  readonly goLiveDate: string;
  readonly status: string;
  readonly lineCount: number;
  readonly openItemIds: readonly string[];
  readonly alreadyCompleted: boolean;
}

export interface OpeningSubledgerValidationDirection {
  readonly direction: SubledgerDirection;
  readonly subledgerBaseBalance: string;
  readonly ledgerBaseBalance: string;
  readonly variance: string;
  readonly isBalanced: boolean;
}

export interface OpeningSubledgerValidationResult {
  readonly batchId: string;
  readonly status: string;
  readonly lineCount: number;
  readonly isBalanced: boolean;
  readonly directions: readonly OpeningSubledgerValidationDirection[];
}
