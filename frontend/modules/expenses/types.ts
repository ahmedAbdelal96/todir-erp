export type ExpenseState = "Draft" | "Submitted" | "Approved" | "Finalized" | "Reversed";
export type ApprovalState = "Pending" | "Approved" | "Rejected";
export type AccountingState = "NotFinalized" | "Pending" | "Posted" | "Reversed";
export type SettlementState = "NotSettled" | "ReimbursementPending" | "ReimbursementPaid" | "RepaymentPending" | "RepaymentSettled" | "AdvanceApplied" | "SettledDirect";
export type EvidenceState = "Missing" | "PendingVerification" | "Verified" | "Rejected";
export type PolicyState = "NotEvaluated" | "Passed" | "Warning" | "Violation" | "ExceptionApproved";
export type PaymentSource = "EmployeePaid" | "CompanyCard" | "PettyCash" | "Advance";

export interface ExpensePage<T> {
  readonly items: readonly T[];
  readonly totalCount: number;
  readonly page: number;
  readonly pageSize: number;
}

export interface ExpenseDto {
  readonly id: string;
  readonly legalEntityId: string;
  readonly employeeId?: string | null;
  readonly merchantName: string;
  readonly expenseDate: string;
  readonly amount: number;
  readonly currencyCode: string;
  readonly paymentSource: PaymentSource | string;
  readonly categoryVersionId: string;
  readonly expenseState: ExpenseState | string;
  readonly approvalState: ApprovalState | string;
  readonly accountingState: AccountingState | string;
  readonly settlementState: SettlementState | string;
  readonly evidenceState: EvidenceState | string;
  readonly policyState: PolicyState | string;
  readonly businessPurpose?: string | null;
  readonly description?: string | null;
  readonly version: number;
  readonly createdAt: string;
  readonly finalizedAt?: string | null;
  readonly baseAmount?: number | null;
  readonly baseCurrencyCode?: string | null;
  readonly taxAmount?: number | null;
  readonly financialContextSnapshot?: Record<string, unknown> | null;
  readonly taxSnapshot?: Record<string, unknown> | null;
  readonly approvalRequestId?: string | null;
}

export interface CreateExpenseRequest {
  readonly legalEntityId: string;
  readonly employeeId?: string | null;
  readonly merchantName: string;
  readonly expenseDate: string;
  readonly amount: number;
  readonly currencyCode: string;
  readonly paymentSource: PaymentSource | string;
  readonly categoryVersionId: string;
  readonly businessPurpose?: string | null;
  readonly description?: string | null;
}

export interface CreateMyExpenseRequest {
  readonly legalEntityId: string;
  readonly merchantName: string;
  readonly expenseDate: string;
  readonly amount: number;
  readonly currencyCode: string;
  readonly paymentSource: PaymentSource | string;
  readonly categoryVersionId: string;
  readonly businessPurpose?: string | null;
  readonly description?: string | null;
}

export interface UpdateExpenseRequest {
  readonly merchantName: string;
  readonly expenseDate: string;
  readonly amount: number;
  readonly currencyCode: string;
  readonly categoryVersionId: string;
  readonly businessPurpose?: string | null;
  readonly description?: string | null;
  readonly version: number;
}

export interface ExpenseActionRequest {
  readonly version: number;
  readonly reason?: string | null;
}

export interface ExpenseApprovalRequest {
  readonly reason?: string | null;
}

export interface ExpenseApprovalDecisionRequest {
  readonly requestId: string;
  readonly decision: "Approve" | "Reject" | string;
  readonly reason?: string | null;
}

export interface ExpenseApprovalDto {
  readonly requestId: string;
  readonly expenseId: string;
  readonly state: string;
  readonly legalEntityId: string;
  readonly resourceVersion: number;
  readonly policyVersionId?: string | null;
  readonly decidedBy?: string | null;
  readonly decisionReason?: string | null;
  readonly createdAt: string;
  readonly decidedAt?: string | null;
}

export interface ExpenseDuplicateCandidateDto {
  readonly expenseId: string;
  readonly merchantName: string;
  readonly expenseDate: string;
  readonly amount: number;
  readonly currencyCode: string;
  readonly matchReason: string;
  readonly merchantSimilarity: number;
}

export interface ExpenseExceptionSummaryDto {
  readonly pendingApproval: number;
  readonly missingEvidence: number;
  readonly reimbursementPending: number;
  readonly reimbursementAmount: number;
  readonly personalRepaymentDue: number;
  readonly personalRepaymentAmount: number;
  readonly outstandingAdvances: number;
  readonly outstandingAdvanceAmount: number;
}

export interface ExpenseSpendReportRowDto {
  readonly groupBy: string;
  readonly groupKey: string;
  readonly originalAmount: number;
  readonly baseAmount: number;
  readonly taxAmount: number;
  readonly expenseCount: number;
}

export interface ExpenseCategoryDto {
  readonly id: string;
  readonly legalEntityId: string;
  readonly code: string;
  readonly name: string;
  readonly status: "Active" | "Archived" | string;
  readonly publishedVersionId?: string | null;
}

export interface CreateExpenseCategoryRequest {
  readonly legalEntityId: string;
  readonly code: string;
  readonly name: string;
  readonly requiresReceipt?: boolean;
}

export interface ExpenseCategoryVersionDto {
  readonly id: string;
  readonly categoryId: string;
  readonly version: number;
  readonly status: "Draft" | "Published" | "Archived" | string;
  readonly requiresReceipt: boolean;
  readonly receiptThreshold?: number | null;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
}

export interface CreateExpenseCategoryVersionRequest {
  readonly requiresReceipt: boolean;
  readonly receiptThreshold?: number | null;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
}

export interface PublishExpenseCategoryVersionRequest {
  readonly effectiveTo?: string | null;
}

export interface ExpenseEvidenceDto {
  readonly id: string;
  readonly expenseId: string;
  readonly evidenceType: string;
  readonly fileReference: string;
  readonly status: "Clean" | "PendingScan" | "Rejected" | string;
  readonly createdAt: string;
  readonly replacesEvidenceId?: string | null;
}

export interface AddExpenseEvidenceRequest {
  readonly evidenceType: string;
  readonly fileReference: string;
  readonly replacesEvidenceId?: string | null;
}

export interface CreateExpenseEvidenceUploadRequest {
  readonly fileName: string;
  readonly contentType: string;
  readonly sizeBytes: number;
  readonly classification?: string;
}

export interface ExpenseEvidenceUploadIntentDto {
  readonly fileRef: string;
  readonly url: string;
  readonly fields: Record<string, string>;
  readonly contentType: string;
  readonly sizeBytes: number;
  readonly expiresAt: string;
}

export interface CompleteExpenseEvidenceUploadRequest {
  readonly fileRef: string;
  readonly evidenceType?: string;
  readonly replacesEvidenceId?: string | null;
}

export interface ExpenseOcrJobDto {
  readonly id: string;
  readonly expenseId: string;
  readonly evidenceId: string;
  readonly state: "Pending" | "Processing" | "Succeeded" | "Failed" | string;
  readonly attemptCount: number;
  readonly suggestions?: Record<string, unknown> | null;
  readonly failureCode?: string | null;
  readonly failureReason?: string | null;
  readonly requestedAt: string;
  readonly startedAt?: string | null;
  readonly completedAt?: string | null;
}

export interface CompleteExpenseOcrManuallyRequest {
  readonly suggestions: Record<string, unknown>;
  readonly reason: string;
}

export interface DeclareMissingReceiptRequest {
  readonly reason: string;
}

export interface MissingReceiptDeclarationDto {
  readonly id: string;
  readonly expenseId: string;
  readonly reason: string;
  readonly status: "Declared" | "Approved" | "Waived" | string;
  readonly declaredBy: string;
  readonly declaredAt: string;
  readonly resolvedAt?: string | null;
}

export interface ReimbursementDto {
  readonly id: string;
  readonly expenseId: string;
  readonly employeeId?: string | null;
  readonly amount: number;
  readonly settledAmount: number;
  readonly currencyCode: string;
  readonly status: "Pending" | "Paid" | "Failed" | string;
  readonly createdAt: string;
}

export interface ExpenseItemizationDto {
  readonly id: string;
  readonly expenseId: string;
  readonly description: string;
  readonly amount: number;
  readonly taxAmount: number;
  readonly currencyCode: string;
}

export interface CreateExpenseItemizationRequest {
  readonly description: string;
  readonly amount: number;
  readonly taxAmount?: number;
  readonly currencyCode?: string;
}

export interface ExpenseHistoryDto {
  readonly id: string;
  readonly expenseId: string;
  readonly action: string;
  readonly toState: Record<string, unknown>;
  readonly occurredAt: string;
}

export interface ExpenseParticipantDto {
  readonly id: string;
  readonly expenseId: string;
  readonly partyId?: string | null;
  readonly displayName: string;
  readonly role: string;
}

export interface AddExpenseParticipantRequest {
  readonly partyId?: string | null;
  readonly displayName: string;
  readonly role?: string;
}

export interface ExpensePolicyEvaluationDto {
  readonly id: string;
  readonly expenseId: string;
  readonly state: "Passed" | "Warning" | "Violation" | string;
  readonly snapshot: Record<string, unknown>;
  readonly evaluatedAt: string;
}

export interface ExpensePolicyExceptionDto {
  readonly id: string;
  readonly expenseId: string;
  readonly legalEntityId: string;
  readonly findingCode: string;
  readonly reason: string;
  readonly state: "Requested" | "Approved" | "Rejected" | string;
  readonly approvalRequestId?: string | null;
  readonly requestedBy: string;
  readonly approvedBy?: string | null;
  readonly version: number;
  readonly createdAt: string;
  readonly decidedAt?: string | null;
}

export interface RequestExpensePolicyExceptionRequest {
  readonly findingCode: string;
  readonly reason: string;
  readonly version: number;
}

export interface DecideExpensePolicyExceptionRequest {
  readonly approvalRequestId: string;
  readonly decision: "Approve" | "Reject" | string;
  readonly reason?: string | null;
}

export interface ExpensePolicyDto {
  readonly id: string;
  readonly legalEntityId: string;
  readonly code: string;
  readonly name: string;
  readonly status: "Active" | "Archived" | string;
}

export interface ExpensePolicyVersionDto {
  readonly id: string;
  readonly policyId: string;
  readonly version: number;
  readonly status: "Draft" | "Published" | "Retired" | string;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly rules: Record<string, unknown>;
}

export interface CreateExpensePolicyRequest {
  readonly legalEntityId: string;
  readonly code: string;
  readonly name: string;
}

export interface CreateExpensePolicyVersionRequest {
  readonly rules: Record<string, unknown>;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
}

export interface PublishExpensePolicyVersionRequest {
  readonly effectiveTo?: string | null;
}

export interface ExpenseReportDto {
  readonly id: string;
  readonly legalEntityId: string;
  readonly employeeId?: string | null;
  readonly title: string;
  readonly state: "Draft" | "Submitted" | "Approved" | "Rejected" | "Finalized" | string;
  readonly totalAmount: number;
  readonly currencyCode: string;
  readonly version: number;
  readonly createdAt: string;
}

export interface CreateExpenseReportRequest {
  readonly legalEntityId: string;
  readonly employeeId?: string | null;
  readonly title: string;
  readonly currencyCode?: string;
}

export interface ExpenseReportItemDto {
  readonly id: string;
  readonly reportId: string;
  readonly expenseId: string;
  readonly amount: number;
  readonly createdAt: string;
}

export interface AddExpenseReportItemRequest {
  readonly expenseId: string;
  readonly amount: number;
}

export interface ExpenseReportHistoryDto {
  readonly id: string;
  readonly reportId: string;
  readonly action: string;
  readonly toState: Record<string, unknown>;
  readonly occurredAt: string;
}

export interface ImportExpenseRowRequest {
  readonly externalReference: string;
  readonly employeeId?: string | null;
  readonly merchantName: string;
  readonly expenseDate: string;
  readonly amount: number;
  readonly currencyCode: string;
  readonly paymentSource: string;
  readonly categoryVersionId: string;
  readonly businessPurpose?: string | null;
  readonly description?: string | null;
}

export interface ImportExpensesRequest {
  readonly legalEntityId: string;
  readonly idempotencyKey: string;
  readonly rows: readonly ImportExpenseRowRequest[];
}

export interface ExpenseImportBatchDto {
  readonly id: string;
  readonly legalEntityId: string;
  readonly idempotencyKey: string;
  readonly state: "Completed" | "Failed" | "Processing" | string;
  readonly totalRows: number;
  readonly createdCount: number;
  readonly errorCount: number;
  readonly createdAt: string;
  readonly completedAt?: string | null;
}

export interface ExpenseImportRowDto {
  readonly batchId: string;
  readonly rowNumber: number;
  readonly externalReference: string;
  readonly expenseId?: string | null;
  readonly status: "Created" | "Error" | string;
  readonly errorCode?: string | null;
  readonly rawPayload: Record<string, unknown>;
}

export interface BusinessAdvanceDto {
  readonly id: string;
  readonly legalEntityId: string;
  readonly employeeId: string;
  readonly purpose: string;
  readonly amount: number;
  readonly issuedAmount: number;
  readonly appliedAmount: number;
  readonly returnedAmount: number;
  readonly currencyCode: string;
  readonly state: "Requested" | "Approved" | "Issued" | "Applied" | "Settled" | string;
  readonly version: number;
  readonly createdAt: string;
  readonly issuancePaymentId?: string | null;
  readonly settlementDueDate?: string | null;
  readonly isOverdue?: boolean;
  readonly writtenOffAmount: number;
}

export interface CreateBusinessAdvanceRequest {
  readonly legalEntityId: string;
  readonly employeeId: string;
  readonly purpose: string;
  readonly amount: number;
  readonly currencyCode?: string;
  readonly settlementDueDate?: string | null;
}

export interface ApplyBusinessAdvanceRequest {
  readonly expenseId: string;
  readonly amount: number;
  readonly expenseCurrencyCode?: string | null;
  readonly advanceEquivalentAmount?: number | null;
  readonly exchangeRate?: number | null;
  readonly exchangeRateReference?: string | null;
}

export interface ReturnBusinessAdvanceRequest {
  readonly amount: number;
  readonly version: number;
}

export interface RequestBusinessAdvanceWriteOffRequest {
  readonly amount: number;
  readonly reason: string;
  readonly version: number;
  readonly effectiveDate?: string | null;
  readonly idempotencyKey?: string | null;
}

export interface DecideBusinessAdvanceWriteOffRequest {
  readonly approvalRequestId: string;
  readonly decision: "Approve" | "Reject" | string;
  readonly reason?: string | null;
}

export interface BusinessAdvanceWriteOffDto {
  readonly id: string;
  readonly advanceId: string;
  readonly legalEntityId: string;
  readonly amount: number;
  readonly currencyCode: string;
  readonly baseAmount: number;
  readonly baseCurrencyCode: string;
  readonly reason: string;
  readonly effectiveDate: string;
  readonly state: string;
  readonly approvalState: string;
  readonly approvalRequestId?: string | null;
  readonly requestedBy?: string | null;
  readonly approvedBy?: string | null;
  readonly ledgerFactId?: string | null;
  readonly ledgerEntryId?: string | null;
  readonly version: number;
  readonly createdAt: string;
  readonly approvedAt?: string | null;
  readonly fxSnapshot?: Record<string, unknown> | null;
}

export interface IssueBusinessAdvanceRequest {
  readonly paymentAccountId: string;
  readonly method: string;
  readonly amount: number;
  readonly paymentDate: string;
  readonly postingDate: string;
  readonly version: number;
}

export interface ExpenseCardTransactionDto {
  readonly id: string;
  readonly legalEntityId: string;
  readonly providerReference: string;
  readonly merchantName: string;
  readonly transactionDate: string;
  readonly amount: number;
  readonly currencyCode: string;
  readonly state: "Unmatched" | "Matched" | "Personal" | "Ignored" | string;
  readonly employeeId?: string | null;
  readonly matchedExpenseId?: string | null;
}

export interface ImportExpenseCardTransactionRequest {
  readonly providerReference: string;
  readonly merchantName: string;
  readonly transactionDate: string;
  readonly amount: number;
  readonly currencyCode?: string;
  readonly initialState?: string;
}

export interface CreateExpenseFromCardTransactionRequest {
  readonly categoryVersionId: string;
  readonly businessPurpose?: string | null;
  readonly description?: string | null;
}

export interface ExpenseCardAdjustmentDto {
  readonly id: string;
  readonly cardTransactionId: string;
  readonly kind: "Credit" | "Refund" | "Dispute" | "Reversal" | string;
  readonly providerReference: string;
  readonly amount: number;
  readonly currencyCode: string;
  readonly state: string;
  readonly reason: string;
  readonly createdAt: string;
  readonly appliedAt?: string | null;
}

export interface ExpenseCardReconciliationDto {
  readonly totalTransactions: number;
  readonly statementAmount: number;
  readonly matchedAmount: number;
  readonly allocatedAmount: number;
  readonly personalAmount: number;
  readonly creditedAmount: number;
  readonly refundedAmount: number;
  readonly disputedAmount: number;
  readonly reversedAmount: number;
  readonly unclassifiedAmount: number;
}

export interface RecordExpenseCardAdjustmentRequest {
  readonly kind: string;
  readonly providerReference: string;
  readonly amount: number;
  readonly currencyCode: string;
  readonly reason: string;
}

export interface ExpenseReimbursementBatchDto {
  readonly id: string;
  readonly legalEntityId: string;
  readonly state: "Prepared" | "Paid" | "Failed" | string;
  readonly currencyCode: string;
  readonly totalAmount: number;
  readonly version: number;
  readonly createdAt: string;
}

export interface PayExpenseReimbursementBatchRequest {
  readonly paymentAccountId: string;
  readonly method: string;
  readonly paymentDate: string;
  readonly postingDate: string;
  readonly version: number;
}

export interface MileageExpenseDetailDto {
  readonly id: string;
  readonly expenseId: string;
  readonly distance: number;
  readonly distanceUnit: "KM" | "MILE" | string;
  readonly rate: number;
  readonly calculatedAmount: number;
  readonly vehicleType?: string | null;
  readonly route?: string | null;
  readonly ratePolicyVersionId?: string | null;
}

export interface SetMileageDetailRequest {
  readonly distance: number;
  readonly rate: number;
  readonly distanceUnit?: string;
  readonly vehicleType?: string | null;
  readonly route?: string | null;
  readonly ratePolicyVersionId?: string | null;
}

export interface PerDiemExpenseDetailDto {
  readonly id: string;
  readonly expenseId: string;
  readonly days: number;
  readonly dailyRate: number;
  readonly providedMeals: number;
  readonly calculatedAmount: number;
  readonly destination?: string | null;
  readonly ratePolicyVersionId?: string | null;
}

export interface SetPerDiemDetailRequest {
  readonly days: number;
  readonly dailyRate: number;
  readonly providedMeals?: number;
  readonly destination?: string | null;
  readonly ratePolicyVersionId?: string | null;
}

export interface ExpenseRatePolicyDto {
  readonly id: string;
  readonly legalEntityId: string;
  readonly kind: "Mileage" | "PerDiem" | string;
  readonly version: number;
  readonly status: "Draft" | "Published" | "Archived" | string;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly vehicleType?: string | null;
  readonly locationCode?: string | null;
  readonly currencyCode: string;
  readonly rate: number;
  readonly breakfastReduction: number;
  readonly lunchReduction: number;
  readonly dinnerReduction: number;
}

export interface CreateExpenseRatePolicyRequest {
  readonly legalEntityId: string;
  readonly kind: "Mileage" | "PerDiem" | string;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly currencyCode: string;
  readonly rate: number;
  readonly vehicleType?: string | null;
  readonly locationCode?: string | null;
  readonly breakfastReduction?: number;
  readonly lunchReduction?: number;
  readonly dinnerReduction?: number;
}

export interface PublishExpenseRatePolicyRequest {
  readonly effectiveTo?: string | null;
}

export interface ExpenseDimensionDto {
  readonly id: string;
  readonly expenseId: string;
  readonly dimensionCode: string;
  readonly valueCode: string;
}

export interface SetExpenseDimensionRequest {
  readonly dimensionCode: string;
  readonly valueCode: string;
}

export interface MatchExpenseCardTransactionRequest {
  readonly expenseId: string;
  readonly expenseVersion?: number | null;
}

export interface AllocateExpenseCardTransactionRequest {
  readonly expenseId: string;
  readonly amount: number;
  readonly expenseVersion?: number | null;
}

export interface MarkExpenseCardPersonalRequest {
  readonly employeeId: string;
}

export interface ExpenseEmployeeRepaymentDto {
  readonly id: string;
  readonly legalEntityId: string;
  readonly employeeId: string;
  readonly sourceReference: string;
  readonly amount: number;
  readonly settledAmount: number;
  readonly currencyCode: string;
  readonly state: "Pending" | "Settled" | string;
  readonly createdAt: string;
  readonly recordedAt?: string | null;
  readonly paymentId?: string | null;
  readonly version: number;
}

export interface SettleEmployeeRepaymentRequest {
  readonly paymentAccountId: string;
  readonly method: string;
  readonly amount: number;
  readonly paymentDate: string;
  readonly postingDate: string;
  readonly version: number;
}
