export type TreasuryAccountType = "Bank" | "Cash" | "PettyCash" | "DigitalWallet" | "ProviderSettlement";
export type TreasuryAccountStatus = "Draft" | "Active" | "Suspended" | "Closing" | "Closed";
export type TreasuryCurrencyPolicy = "SingleCurrency" | "MultiCurrency";

export interface TreasuryAccountDto {
  readonly id: string;
  readonly legalEntityId: string;
  readonly branchId?: string | null;
  readonly code: string;
  readonly name: string;
  readonly type: TreasuryAccountType;
  readonly status: TreasuryAccountStatus;
  readonly currencyPolicy: TreasuryCurrencyPolicy;
  readonly currencyCode?: string | null;
  readonly purpose: string;
  readonly liquidityClass: string;
  readonly maskedIdentifier?: string | null;
  readonly hasSensitiveIdentifier: boolean;
  readonly version: number;
  readonly createdAt: string;
  readonly updatedAt: string;
}

export interface TreasuryAccountPage {
  readonly items: readonly TreasuryAccountDto[];
  readonly page: number;
  readonly pageSize: number;
  readonly hasMore: boolean;
  readonly totalCount: number;
}

export interface StatementDto {
  readonly id: string;
  readonly treasuryAccountId: string;
  readonly statementNumber: string;
  readonly openingDate?: string | null;
  readonly closingDate?: string | null;
  readonly currencyCode: string;
  readonly processingState: string;
  readonly reconciliationState: string;
  readonly openingBalance?: number | null;
  readonly closingBalance?: number | null;
  readonly lineCount: number;
  readonly version: number;
}

export interface StatementPage {
  readonly items: readonly StatementDto[];
  readonly page: number;
  readonly pageSize: number;
  readonly hasMore: boolean;
  readonly totalCount: number;
}

export interface StatementLineDto {
  readonly id: string;
  readonly statementId: string;
  readonly externalLineId: string;
  readonly bookingDate: string;
  readonly valueDate?: string | null;
  readonly direction: "Debit" | "Credit" | string;
  readonly amount: number;
  readonly currencyCode: string;
  readonly reference?: string | null;
  readonly transactionCode?: string | null;
  readonly classification: string;
  readonly claimedAmount: number;
  readonly version: number;
}

export interface StatementLinePage {
  readonly items: readonly StatementLineDto[];
  readonly page: number;
  readonly pageSize: number;
  readonly hasMore: boolean;
  readonly totalCount: number;
}

export interface ReconciliationDto {
  readonly id: string;
  readonly treasuryAccountId: string;
  readonly asOfDate: string;
  readonly state: "Open" | "Finalized" | "Reopened" | string;
  readonly externalBalance: number;
  readonly bookBalance?: number | null;
  readonly difference?: number | null;
  readonly matchCount: number;
  readonly version: number;
  readonly ledgerSourceReference?: string | null;
  readonly ledgerLastPostedAt?: string | null;
  readonly ledgerPostedLineCount?: number;
}

export interface ReconciliationPage {
  readonly items: readonly ReconciliationDto[];
  readonly page: number;
  readonly pageSize: number;
  readonly hasMore: boolean;
  readonly totalCount: number;
}

export interface ReconciliationSuggestionDto {
  readonly statementLineId: string;
  readonly amount: number;
  readonly currencyCode: string;
  readonly reference?: string | null;
  readonly transactionCode?: string | null;
  readonly reason: string;
}

export interface CashCountDto {
  readonly id: string;
  readonly treasuryAccountId: string;
  readonly cutoffDate: string;
  readonly state: "Draft" | "Submitted" | "Approved" | "Finalized" | "Rejected" | string;
  readonly expectedAmount: number;
  readonly countedAmount: number;
  readonly difference: number;
  readonly custodianId: string;
  readonly version: number;
}

export interface CashCountPage {
  readonly items: readonly CashCountDto[];
  readonly page: number;
  readonly pageSize: number;
  readonly hasMore: boolean;
  readonly totalCount: number;
}

export interface CashPositionDto {
  readonly legalEntityId: string;
  readonly treasuryAccountId: string;
  readonly accountCode: string;
  readonly accountName: string;
  readonly accountType: TreasuryAccountType;
  readonly currencyCode: string;
  readonly bookBalance?: number | null;
  readonly externalBalance?: number | null;
  readonly availableBalance?: number | null;
  readonly inTransitAmount: number;
  readonly balanceBasis: string;
  readonly asOf?: string | null;
  readonly externalAsOf?: string | null;
  readonly isStale: boolean;
}

export interface ForecastItemDto {
  readonly id: string;
  readonly dueDate: string;
  readonly direction: "Inflow" | "Outflow" | string;
  readonly amount: number;
  readonly currencyCode: string;
  readonly sourceType: string;
  readonly description: string;
  readonly confidence: string;
  readonly treasuryAccountId?: string | null;
}

export interface ForecastPage {
  readonly id: string;
  readonly asOfDate: string;
  readonly horizonDays: number;
  readonly status: string;
  readonly items: readonly ForecastItemDto[];
}

export interface ForecastVersionDto {
  readonly id: string;
  readonly asOfDate: string;
  readonly horizonDays: number;
  readonly status: string;
  readonly createdAt: string;
}

export interface AccountChangeRequestDto {
  readonly id: string;
  readonly treasuryAccountId: string;
  readonly status: "Pending" | "Approved" | "Rejected" | string;
  readonly payload: Record<string, unknown>;
  readonly reason: string;
  readonly requestedBy: string;
  readonly approvedBy?: string | null;
  readonly version: number;
  readonly createdAt: string;
  readonly decidedAt?: string | null;
}

export interface CashDifferenceResolutionDto {
  readonly id: string;
  readonly cashCountId: string;
  readonly reasonCode: string;
  readonly resolutionType: string;
  readonly amount: number;
  readonly evidenceReference?: string | null;
  readonly approvedBy?: string | null;
  readonly createdAt: string;
}

export interface CustodyTransferDto {
  readonly id: string;
  readonly treasuryAccountId: string;
  readonly fromCustodianId?: string | null;
  readonly toCustodianId: string;
  readonly effectiveAt: string;
  readonly reason: string;
  readonly createdBy: string;
}

export interface ConnectionDto {
  readonly id: string;
  readonly treasuryAccountId: string;
  readonly providerCode: string;
  readonly connectionRef: string;
  readonly status: "Connected" | "Disconnected" | "Error" | string;
  readonly lastSuccessAt?: string | null;
  readonly lastFailureAt?: string | null;
  readonly failureCode?: string | null;
  readonly version: number;
}

export interface BankOriginatedCandidateDto {
  readonly id: string;
  readonly statementLineId: string;
  readonly eventType: string;
  readonly status: "Proposed" | "Approved" | "Rejected" | string;
  readonly amount: number;
  readonly currencyCode: string;
  readonly evidence: Record<string, unknown>;
  readonly proposedAt: string;
  readonly reviewedBy?: string | null;
  readonly reviewedAt?: string | null;
}

export interface TransactionCodeMappingDto {
  readonly id: string;
  readonly transactionCode: string;
  readonly classification: string;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly version: number;
}

export interface ReconciliationRuleSetDto {
  readonly id: string;
  readonly name: string;
  readonly status: string;
  readonly version: number;
  readonly ruleVersion: number;
}

export interface TransferDto {
  readonly transferId: string;
  readonly state: string;
  readonly sourceAmount: number;
  readonly sourceCurrencyCode: string;
  readonly destinationAmount: number;
  readonly destinationCurrencyCode: string;
  readonly completedAt: string;
  readonly baseCurrencyCode: string;
  readonly fxSnapshot?: string | null;
}

export interface TransferReconciliationDto {
  readonly id: string;
  readonly transferReference: string;
  readonly sourceAccountId: string;
  readonly destinationAccountId: string;
  readonly state: "InTransit" | "Matched" | string;
  readonly sourceAmount: number;
  readonly sourceCurrencyCode?: string | null;
  readonly destinationAmount: number;
  readonly destinationCurrencyCode?: string | null;
  readonly baseCurrencyCode?: string | null;
  readonly fxSnapshot?: string | null;
  readonly inTransitAmount: number;
  readonly sourceEvidence?: string | null;
  readonly destinationEvidence?: string | null;
  readonly version: number;
  readonly updatedAt: string;
}

export interface TransferReconciliationPage {
  readonly items: readonly TransferReconciliationDto[];
  readonly page: number;
  readonly pageSize: number;
  readonly hasMore: boolean;
  readonly totalCount: number;
}

export interface OpeningReconciliationDto {
  readonly id: string;
  readonly treasuryAccountId: string;
  readonly ledgerOpeningBatchId: string;
  readonly openingAsOfDate: string;
  readonly currencyCode: string;
  readonly bookOpeningBalance: number;
  readonly externalOpeningBalance: number;
  readonly outstandingInTransitAmount: number;
  readonly difference: number;
  readonly sourceSystem: string;
  readonly legacyAccountId?: string | null;
  readonly batchId: string;
  readonly status: "Draft" | "Finalized" | string;
  readonly version: number;
  readonly createdAt: string;
  readonly finalizedAt?: string | null;
}

export interface OpeningReconciliationPage {
  readonly items: readonly OpeningReconciliationDto[];
  readonly page: number;
  readonly pageSize: number;
  readonly hasMore: boolean;
  readonly totalCount: number;
}

export interface PosCashSettlementDto {
  readonly id: string;
  readonly posSessionId: string;
  readonly registerId: string;
  readonly branchId: string;
  readonly businessDate: string;
  readonly currencyCode: string;
  readonly expectedCash: number;
  readonly countedCash: number;
  readonly variance: number;
  readonly status: "Pending" | "Confirmed" | string;
  readonly treasuryAccountId?: string | null;
  readonly version: number;
  readonly createdAt: string;
  readonly updatedAt: string;
}

export interface PosCashSettlementPage {
  readonly items: readonly PosCashSettlementDto[];
  readonly page: number;
  readonly pageSize: number;
  readonly hasMore: boolean;
  readonly totalCount: number;
}

export interface ProviderSettlementDto {
  readonly id: string;
  readonly settlementAccountId: string;
  readonly providerCode: string;
  readonly providerReference: string;
  readonly periodFrom: string;
  readonly periodTo: string;
  readonly currencyCode: string;
  readonly grossAmount: number;
  readonly feesAmount: number;
  readonly adjustmentsAmount: number;
  readonly netPayoutAmount: number;
  readonly externalReference: string;
  readonly status: "Pending" | "Matched" | string;
  readonly statementLineId?: string | null;
  readonly version: number;
  readonly createdAt: string;
  readonly updatedAt: string;
}

export interface ProviderSettlementPage {
  readonly items: readonly ProviderSettlementDto[];
  readonly page: number;
  readonly pageSize: number;
  readonly hasMore: boolean;
  readonly totalCount: number;
}

export interface BankActivityDto {
  readonly statementLineId: string;
  readonly statementId: string;
  readonly treasuryAccountId: string;
  readonly bookingDate: string;
  readonly direction: "Debit" | "Credit" | string;
  readonly amount: number;
  readonly currencyCode: string;
  readonly reference?: string | null;
  readonly transactionCode?: string | null;
  readonly classification: string;
  readonly claimedAmount: number;
  readonly remainingAmount: number;
  readonly version: number;
}

export interface BankActivityPage {
  readonly items: readonly BankActivityDto[];
  readonly page: number;
  readonly pageSize: number;
  readonly hasMore: boolean;
  readonly totalCount: number;
}

export interface CashToLedgerReportDto {
  readonly legalEntityId: string;
  readonly baseCurrencyCode: string;
  readonly bankingBookBalance: number;
  readonly ledgerCashBalance: number;
  readonly difference: number;
  readonly externalReportedBalance: number;
  readonly inTransitAmount: number;
  readonly asOf: string;
  readonly ledgerBasis: string;
  readonly isBalanced: boolean;
}

export interface ChequeInTransitDto {
  readonly paymentId: string;
  readonly legalEntityId: string;
  readonly paymentNumber: string;
  readonly direction: "Incoming" | "Outgoing" | string;
  readonly amount: number;
  readonly currencyCode: string;
  readonly paymentAccountId: string;
  readonly paymentDate: string;
  readonly state: string;
  readonly bankReference?: string | null;
  readonly sourceReference?: string | null;
}

export interface ChequeInTransitPage {
  readonly items: readonly ChequeInTransitDto[];
  readonly page: number;
  readonly pageSize: number;
  readonly hasMore: boolean;
  readonly totalCount: number;
}

// Request contracts
export interface CreateTreasuryAccountRequest {
  readonly legalEntityId: string;
  readonly branchId?: string | null;
  readonly code: string;
  readonly name: string;
  readonly type: TreasuryAccountType;
  readonly currencyPolicy: TreasuryCurrencyPolicy;
  readonly currencyCode?: string | null;
  readonly purpose: string;
  readonly liquidityClass: string;
  readonly allowedUses?: readonly string[] | null;
  readonly maskedIdentifier?: string | null;
  readonly secretRef?: string | null;
}

export interface UpdateTreasuryAccountRequest {
  readonly name: string;
  readonly purpose: string;
  readonly liquidityClass: string;
  readonly version: number;
}

export interface StatusRequest {
  readonly reason?: string | null;
  readonly version?: number | null;
  readonly targetState?: string | null;
}

export interface ImportStatementLineRequest {
  readonly externalLineId: string;
  readonly bookingDate: string;
  readonly valueDate?: string | null;
  readonly direction: "Debit" | "Credit";
  readonly amount: number;
  readonly currencyCode: string;
  readonly reference?: string | null;
  readonly transactionCode?: string | null;
  readonly counterpartyName?: string | null;
  readonly rawPayload?: string | null;
}

export interface ImportStatementRequest {
  readonly statementNumber: string;
  readonly openingDate?: string | null;
  readonly closingDate?: string | null;
  readonly currencyCode: string;
  readonly openingBalance?: number | null;
  readonly closingBalance?: number | null;
  readonly sourceType: string;
  readonly sourceReference?: string | null;
  readonly lines: readonly ImportStatementLineRequest[];
}

export interface ValidateStatementRequest {
  readonly requireBalanceContinuity?: boolean;
}

export interface ClassifyStatementLineRequest {
  readonly classification: string;
  readonly explanation?: string | null;
}

export interface CreatePaymentProposalRequest {
  readonly evidence?: Record<string, unknown> | null;
}

export interface CreateReconciliationRequest {
  readonly legalEntityId: string;
  readonly asOfDate: string;
  readonly statementId: string;
  readonly bookBalance?: number | null;
  readonly notes?: string | null;
}

export interface PaymentMatchReference {
  readonly paymentId: string;
  readonly amount: number;
}

export interface CreateMatchRequest {
  readonly statementLineIds: readonly string[];
  readonly payments: readonly PaymentMatchReference[];
  readonly externalAmount: number;
  readonly systemAmount: number;
  readonly difference: number;
  readonly source: string;
  readonly explanation?: string | null;
}

export interface UnmatchRequest {
  readonly matchId: string;
  readonly version: number;
}

export interface ReconciliationStatusRequest {
  readonly targetState: string;
  readonly version: number;
}

export interface CashDenominationRequest {
  readonly denomination: string;
  readonly unitValue: number;
  readonly quantity: number;
}

export interface CreateCashCountRequest {
  readonly cutoffDate: string;
  readonly custodianId: string;
  readonly expectedAmount: number;
  readonly denominations: readonly CashDenominationRequest[];
}

export interface CashCountStatusRequest {
  readonly targetState: string;
  readonly version: number;
}

export interface ResolveCashDifferenceRequest {
  readonly reasonCode: string;
  readonly resolutionType: string;
  readonly amount: number;
  readonly evidenceReference?: string | null;
  readonly version: number;
}

export interface TransferCustodyRequest {
  readonly toCustodianId: string;
  readonly fromCustodianId?: string | null;
  readonly reason: string;
}

export interface CreateConnectionRequest {
  readonly providerCode: string;
  readonly connectionRef: string;
}

export interface CreateForecastVersionRequest {
  readonly legalEntityId: string;
  readonly asOfDate: string;
  readonly horizonDays: number;
  readonly status?: string | null;
}

export interface CreateForecastItemRequest {
  readonly legalEntityId: string;
  readonly forecastVersionId: string;
  readonly treasuryAccountId?: string | null;
  readonly dueDate: string;
  readonly direction: "Inflow" | "Outflow";
  readonly amount: number;
  readonly currencyCode: string;
  readonly sourceType: string;
  readonly description: string;
  readonly sourceReference?: Record<string, unknown> | null;
}

export interface CreateTransferRequest {
  readonly legalEntityId: string;
  readonly sourceTreasuryAccountId: string;
  readonly destinationTreasuryAccountId: string;
  readonly amount: number;
  readonly currencyCode: string;
  readonly paymentDate: string;
  readonly postingDate: string;
  readonly valueDate?: string | null;
  readonly reference?: string | null;
}

export interface ReconcileTransferRequest {
  readonly sourceStatementLineId?: string | null;
  readonly destinationStatementLineId?: string | null;
  readonly version: number;
  readonly explanation?: string | null;
}

export interface CreateOpeningReconciliationRequest {
  readonly legalEntityId: string;
  readonly treasuryAccountId: string;
  readonly ledgerOpeningBatchId: string;
  readonly openingAsOfDate: string;
  readonly currencyCode: string;
  readonly bookOpeningBalance: number;
  readonly externalOpeningBalance: number;
  readonly outstandingInTransitAmount: number;
  readonly sourceSystem: string;
  readonly legacyAccountId?: string | null;
  readonly batchId: string;
}

export interface FinalizeOpeningReconciliationRequest {
  readonly version: number;
}

export interface CreateAccountChangeRequest {
  readonly payload: Record<string, unknown>;
  readonly reason: string;
  readonly version: number;
}

export interface ChangeRequestDecisionRequest {
  readonly decision: "Approved" | "Rejected";
  readonly version: number;
}

export interface BankOriginatedDecisionRequest {
  readonly decision: "Approved" | "Rejected";
  readonly version: number;
}

export interface CreateTransactionCodeMappingRequest {
  readonly transactionCode: string;
  readonly classification: string;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
}

export interface CreateReconciliationRuleSetRequest {
  readonly name: string;
  readonly definition: Record<string, unknown>;
}

export interface ConfirmPosCashSettlementRequest {
  readonly treasuryAccountId: string;
  readonly version: number;
}

export interface MatchProviderSettlementRequest {
  readonly statementLineId: string;
  readonly version: number;
}
