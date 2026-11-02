import type { DecimalString, CurrencyCode } from "../../components/contracts/domain-values.ts";

export type PaymentDirection = "Incoming" | "Outgoing" | "InternalTransfer";
export type PaymentBusinessState = "Draft" | "Pending" | "Completed" | "Failed" | "Cancelled" | "Reversed";
export type PaymentMethod = "Cash" | "BankTransfer" | "Cheque" | "Card" | "PaymentGateway" | "Wallet" | "Other";
export type PaymentAccountType = "Bank" | "Cashbox" | "ProviderClearing" | "Wallet" | "InternalClearing";
export type PaymentAccountStatus = "Active" | "Suspended" | "Archived";
export type PaymentType =
  | "CustomerReceipt"
  | "SupplierPayment"
  | "CustomerAdvance"
  | "SupplierAdvance"
  | "CustomerRefund"
  | "SupplierRefund"
  | "ExpensePayment"
  | "Standalone"
  | "TransferLeg"
  | "ProviderFee";

export type PaymentFeeType =
  | "BankFee"
  | "ProviderFee"
  | "CardProcessingFee"
  | "FXFee"
  | "TransferFee"
  | "OtherApprovedFee";

export type PaymentFeeRecognitionState =
  | "Pending"
  | "Recognized"
  | "RecognizedImmediate"
  | "Reversed"
  | "Deferred"
  | "Estimated";

export type PaymentFeeDisposition = "Retain" | "Reverse";
export type PaymentLinkAmountPolicy = "Fixed" | "PartialAllowed" | "CustomerEnteredWithinLimits";

export interface PaymentAccount {
  readonly id: string;
  readonly legalEntityId: string;
  readonly branchId?: string | null | undefined;
  readonly code: string;
  readonly name: string;
  readonly type: PaymentAccountType;
  readonly currencyPolicy: string;
  readonly currencyCode?: string | null | undefined;
  readonly status: PaymentAccountStatus;
  readonly bankName?: string | null | undefined;
  readonly accountHolder?: string | null | undefined;
  readonly maskedIdentifier?: string | null | undefined;
  readonly secretRef?: string | null | undefined;
  readonly version: number;
}

export interface PaymentRecord {
  readonly id: string;
  readonly legalEntityId: string;
  readonly number: string;
  readonly branchId?: string | null | undefined;
  readonly direction: PaymentDirection;
  readonly type: PaymentType;
  readonly partyId?: string | null | undefined;
  readonly amount: DecimalString;
  readonly currencyCode: CurrencyCode;
  readonly method: PaymentMethod;
  readonly paymentAccountId: string;
  readonly paymentDate: string;
  readonly postingDate: string;
  readonly valueDate?: string | null | undefined;
  readonly bankReference?: string | null | undefined;
  readonly customerReference?: string | null | undefined;
  readonly supplierReference?: string | null | undefined;
  readonly sourceType?: string | null | undefined;
  readonly sourceReference?: string | null | undefined;
  readonly state: PaymentBusinessState;
  readonly completedAt?: string | null | undefined;
  readonly version: number;
}

export interface PaymentListQuery {
  readonly legalEntityId: string;
  readonly branchId?: string | undefined;
  readonly page?: number | undefined;
  readonly pageSize?: number | undefined;
  readonly search?: string | undefined;
  readonly direction?: PaymentDirection | undefined;
  readonly state?: PaymentBusinessState | undefined;
  readonly fromDate?: string | undefined;
  readonly toDate?: string | undefined;
}

export interface PaymentPage {
  readonly items: readonly PaymentRecord[];
  readonly page: number;
  readonly pageSize: number;
  readonly hasMore: boolean;
  readonly totalCount?: number | undefined;
}

export interface CreatePaymentRequest {
  readonly legalEntityId: string;
  readonly branchId?: string | null | undefined;
  readonly type: PaymentType;
  readonly partyId?: string | null | undefined;
  readonly amount: DecimalString;
  readonly currencyCode: CurrencyCode;
  readonly method: PaymentMethod;
  readonly paymentAccountId: string;
  readonly paymentDate: string;
  readonly postingDate: string;
  readonly valueDate?: string | null | undefined;
  readonly bankReference?: string | null | undefined;
  readonly customerReference?: string | null | undefined;
  readonly supplierReference?: string | null | undefined;
  readonly sourceType?: string | null | undefined;
  readonly sourceReference?: string | null | undefined;
}

export interface CreateInternalTransferRequest {
  readonly legalEntityId: string;
  readonly sourcePaymentAccountId: string;
  readonly destinationPaymentAccountId: string;
  readonly amount: DecimalString;
  readonly currencyCode: CurrencyCode;
  readonly paymentDate: string;
  readonly postingDate: string;
  readonly valueDate?: string | null | undefined;
  readonly reference?: string | null | undefined;
  readonly destinationCurrencyCode?: string | null | undefined;
}

export interface InternalTransferResult {
  readonly id: string;
  readonly sourcePayment: PaymentRecord;
  readonly destinationPayment: PaymentRecord;
  readonly sourceAmount: number;
  readonly sourceCurrencyCode: string;
  readonly destinationAmount: number;
  readonly destinationCurrencyCode: string;
  readonly baseCurrencyCode: string;
  readonly fxSnapshot?: string | null | undefined;
  readonly completedAt: string;
}

export interface CreateRefundRequest {
  readonly paymentAccountId: string;
  readonly method: PaymentMethod;
  readonly amount: DecimalString;
  readonly paymentDate: string;
  readonly postingDate: string;
  readonly valueDate?: string | null | undefined;
  readonly reasonCode: string;
  readonly bankReference?: string | null | undefined;
}

export interface ReversePaymentRequest {
  readonly paymentDate: string;
  readonly postingDate: string;
  readonly reasonCode: string;
  readonly feeDispositions?: readonly PaymentFeeDispositionRequest[] | null | undefined;
}

export interface PaymentFeeDispositionRequest {
  readonly feeId: string;
  readonly disposition: PaymentFeeDisposition;
  readonly evidenceReference?: string | null | undefined;
}

export interface CreatePaymentFeeRequest {
  readonly type: PaymentFeeType;
  readonly amount: DecimalString;
  readonly currencyCode: CurrencyCode;
  readonly sourceReference?: string | null | undefined;
}

export interface PaymentFeeResult {
  readonly id: string;
  readonly type: PaymentFeeType;
  readonly amount: number;
  readonly currencyCode: string;
  readonly recognitionState: PaymentFeeRecognitionState | string;
}

export interface ApplyPaymentLineRequest {
  readonly targetOpenItemId: string;
  readonly amount: DecimalString;
}

export interface ApplyPaymentRequest {
  readonly applicationDate: string;
  readonly settlementDate: string;
  readonly lines: readonly ApplyPaymentLineRequest[];
}

export interface CreatePaymentIntentRequest {
  readonly legalEntityId: string;
  readonly branchId?: string | null | undefined;
  readonly providerConfigId: string;
  readonly partyId?: string | null | undefined;
  readonly amount: DecimalString;
  readonly currencyCode: CurrencyCode;
  readonly sourceType?: string | null | undefined;
  readonly sourceReference?: string | null | undefined;
  readonly expiresAt?: string | null | undefined;
}

export interface PaymentIntentResult {
  readonly id: string;
  readonly legalEntityId: string;
  readonly merchantReference: string;
  readonly amount: number;
  readonly currencyCode: string;
  readonly state: string;
  readonly attemptId: string;
  readonly attemptNumber: number;
}

export interface PaymentAttemptReadModel {
  readonly id: string;
  readonly attemptNumber: number;
  readonly state: string;
  readonly providerState?: string | null | undefined;
  readonly failureCode?: string | null | undefined;
  readonly safeFailureMessage?: string | null | undefined;
  readonly createdAt: string;
  readonly updatedAt: string;
}

export interface PaymentRequestDetails {
  readonly request: PaymentIntentResult;
  readonly attempts: readonly PaymentAttemptReadModel[];
  readonly expiresAt?: string | null | undefined;
}

export interface PaymentRequestListQuery {
  readonly legalEntityId: string;
  readonly page?: number | undefined;
  readonly pageSize?: number | undefined;
  readonly search?: string | undefined;
  readonly state?: string | undefined;
}

export interface PaymentRequestPage {
  readonly items: readonly PaymentIntentResult[];
  readonly page: number;
  readonly pageSize: number;
  readonly hasMore: boolean;
  readonly totalCount?: number | undefined;
}

export interface CreatePaymentLinkRequest {
  readonly paymentRequestId: string;
  readonly expiresAt: string;
  readonly amountPolicy?: PaymentLinkAmountPolicy | undefined;
  readonly minimumAmount?: string | null | undefined;
  readonly maximumAmount?: string | null | undefined;
}

export interface PaymentLinkResult {
  readonly id: string;
  readonly paymentRequestId: string;
  readonly token: string;
  readonly status: string;
  readonly expiresAt: string;
}

export interface PublicPaymentLinkDetails {
  readonly amount: number;
  readonly currencyCode: string;
  readonly amountPolicy: string;
  readonly minimumAmount?: number | null | undefined;
  readonly maximumAmount?: number | null | undefined;
  readonly status: string;
  readonly expiresAt: string;
}

export interface ProviderOperationCapabilities {
  readonly createPaymentRequest: boolean;
  readonly getPaymentStatus: boolean;
  readonly capture: boolean;
  readonly cancel: boolean;
  readonly refund: boolean;
  readonly webhooks: boolean;
}

export interface CreatePaymentProviderRequest {
  readonly legalEntityId: string;
  readonly providerCode: string;
  readonly environment: string;
  readonly merchantReference: string;
  readonly apiVersion: string;
  readonly capabilities: ProviderOperationCapabilities;
  readonly supportedCurrencies: readonly string[];
  readonly supportedMethods: readonly string[];
  readonly paymentAccountId: string;
  readonly secretRef?: string | null | undefined;
}

export interface PaymentProviderResult {
  readonly id: string;
  readonly legalEntityId: string;
  readonly providerCode: string;
  readonly environment: string;
  readonly merchantReference: string;
  readonly apiVersion: string;
  readonly capabilities: ProviderOperationCapabilities;
  readonly supportedCurrencies: readonly string[];
  readonly supportedMethods: readonly string[];
  readonly paymentAccountId: string;
  readonly hasSecretReference: boolean;
  readonly status: string;
  readonly version: number;
}

export interface CreatePaymentAccountRequest {
  readonly legalEntityId: string;
  readonly branchId?: string | null | undefined;
  readonly code: string;
  readonly name: string;
  readonly type: PaymentAccountType;
  readonly currencyPolicy: string;
  readonly currencyCode?: string | null | undefined;
  readonly bankName?: string | null | undefined;
  readonly accountHolder?: string | null | undefined;
  readonly maskedIdentifier?: string | null | undefined;
  readonly secretRef?: string | null | undefined;
}

export interface UpdatePaymentAccountStatusRequest {
  readonly branchId?: string | null | undefined;
  readonly status: PaymentAccountStatus;
}

export interface ProviderRefundResult {
  readonly refund: PaymentRecord;
  readonly providerState: string;
  readonly operationId: string;
}

// Backward-compatible spike types
export type PaymentStatus = "Settled" | "Processing" | "Rejected" | "Unknown";

export interface PaymentCommand {
  readonly partyId: string;
  readonly partyName: string;
  readonly amount: DecimalString;
  readonly currency: CurrencyCode;
  readonly paymentMethod: PaymentMethod | string;
  readonly referenceNumber: string;
  readonly sourceAccount: string;
  readonly destinationAccount: string;
  readonly notes?: string | undefined;
}

export interface PaymentResult {
  readonly paymentId: string;
  readonly referenceNumber: string;
  readonly status: PaymentStatus;
  readonly amount: DecimalString;
  readonly currency: CurrencyCode;
  readonly settledAt?: string | undefined;
  readonly message?: string | undefined;
}

export interface PaymentStatusCheck {
  readonly paymentId: string;
  readonly status: PaymentStatus;
  readonly settledAt?: string | undefined;
}
