export type Currency = {
  readonly code: string;
  readonly numericCode: number;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly minorUnit: number;
  readonly status: string;
  readonly datasetVersion: string;
};

export type CurrencyDto = Currency;

export interface EntityCurrencyDto {
  readonly legalEntityId: string;
  readonly currencyCode: string;
  readonly enabled: boolean;
  readonly allowSales: boolean;
  readonly allowPurchase: boolean;
  readonly allowPayment: boolean;
  readonly allowManualRate: boolean;
  readonly status: string;
  readonly version: number;
}

export interface EnableCurrencyRequest {
  readonly allowSales?: boolean | undefined;
  readonly allowPurchase?: boolean | undefined;
  readonly allowPayment?: boolean | undefined;
  readonly allowManualRate?: boolean | undefined;
}

export type ExchangeRatePurpose =
  | "Accounting"
  | "Commercial"
  | "Contractual"
  | "BankActual"
  | "Settlement"
  | "Tax";

export type ExchangeRateStatus =
  | "Draft"
  | "Approved"
  | "Superseded"
  | "Rejected";

export interface ExchangeRateDto {
  readonly id: string;
  readonly legalEntityId: string;
  readonly fromCurrency: string;
  readonly toCurrency: string;
  readonly rate: string;
  readonly purpose: ExchangeRatePurpose;
  readonly effectiveDate: string;
  readonly sourceType: string;
  readonly providerReference?: string | null | undefined;
  readonly status: ExchangeRateStatus;
  readonly version: number;
}

export interface CreateExchangeRateRequest {
  readonly legalEntityId: string;
  readonly fromCurrency: string;
  readonly toCurrency: string;
  readonly rate: string;
  readonly purpose: ExchangeRatePurpose;
  readonly effectiveDate: string;
  readonly sourceType: string;
  readonly providerReference?: string | null | undefined;
  readonly reason?: string | null | undefined;
}

export interface ConvertMoneyRequest {
  readonly legalEntityId: string;
  readonly amount: string;
  readonly fromCurrency: string;
  readonly toCurrency: string;
  readonly purpose: ExchangeRatePurpose;
  readonly businessDate: string;
}

export interface ExchangeRateSnapshotDto {
  readonly tenantId: string;
  readonly legalEntityId: string;
  readonly approvedRateId?: string | null | undefined;
  readonly fromCurrency: string;
  readonly toCurrency: string;
  readonly rate: string;
  readonly purpose: ExchangeRatePurpose;
  readonly effectiveDate: string;
  readonly source: string;
  readonly version: number;
  readonly manualOverride: boolean;
  readonly capturedAt: string;
}

export interface ConvertedMoneyDto {
  readonly sourceAmount: string;
  readonly sourceCurrency: string;
  readonly targetAmount: string;
  readonly targetCurrency: string;
  readonly rate: string;
  readonly rateDate: string;
  readonly purpose: ExchangeRatePurpose;
  readonly snapshot?: ExchangeRateSnapshotDto | null | undefined;
}

export interface ExchangeRateSourceConfigDto {
  readonly id: string;
  readonly legalEntityId: string;
  readonly sourceCode: string;
  readonly fromCurrency: string;
  readonly toCurrency: string;
  readonly purpose: ExchangeRatePurpose;
  readonly priority: number;
  readonly enabled: boolean;
  readonly credentialReference?: string | null | undefined;
  readonly syncIntervalSeconds: number;
  readonly failureCount: number;
  readonly version: number;
}

export interface CreateExchangeRateSourceConfigRequest {
  readonly sourceCode: string;
  readonly fromCurrency: string;
  readonly toCurrency: string;
  readonly purpose: ExchangeRatePurpose;
  readonly priority: number;
  readonly credentialReference?: string | null | undefined;
  readonly syncIntervalSeconds?: number | undefined;
}

export type RoundingMode = "HalfUp" | "HalfEven" | "Down" | "Up";

export type RoundingContext =
  | "MoneySettlement"
  | "LineNetAmount"
  | "DocumentSubtotal"
  | "DocumentTotal"
  | "FXBaseAmount"
  | "PaymentScheduleLine"
  | "PaymentAllocation"
  | "CashRounding";

export type LineDocumentRoundingStrategy =
  | "RoundEachLineThenSum"
  | "SumExactLinesThenRound";

export interface RoundingPolicyContextRuleRequest {
  readonly context: RoundingContext;
  readonly mode: RoundingMode;
}

export interface RoundingPolicyDto {
  readonly id: string;
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly mode: RoundingMode;
  readonly moneyScale: number;
  readonly cashIncrement: string;
  readonly status: string;
  readonly version: number;
  readonly lineDocumentStrategy: LineDocumentRoundingStrategy;
  readonly contextRules?: readonly RoundingPolicyContextRuleRequest[] | null | undefined;
}

export interface CreateRoundingPolicyRequest {
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly mode: RoundingMode;
  readonly moneyScale: number;
  readonly cashIncrement: string;
  readonly contextRules?: readonly RoundingPolicyContextRuleRequest[] | null | undefined;
  readonly lineDocumentStrategy?: LineDocumentRoundingStrategy | undefined;
}

export interface FinancialPolicyDto {
  readonly legalEntityId: string;
  readonly roundingPolicyId: string;
  readonly exchangeRateMaxAgeDays: number;
  readonly manualRateOverrideAllowed: boolean;
  readonly version: number;
}

export interface UpdateFinancialPolicyRequest {
  readonly roundingPolicyId: string;
  readonly exchangeRateMaxAgeDays: number;
  readonly manualRateOverrideAllowed: boolean;
  readonly version: number;
}

export type PaymentAllocation = "Percentage" | "Fixed" | "Balance";

export type DueDateBasis =
  | "Immediate"
  | "DaysAfterDate"
  | "EndOfMonth"
  | "DaysAfterEndOfMonth"
  | "DayOfFollowingMonth";

export interface PaymentTermRuleRequest {
  readonly sequence: number;
  readonly allocationType: PaymentAllocation;
  readonly percentage?: string | number | null | undefined;
  readonly fixedAmount?: string | null | undefined;
  readonly dueDateBasis: DueDateBasis;
  readonly days: number;
  readonly dayOfMonth?: number | null | undefined;
  readonly monthOffset: number;
}

export type PaymentTermRule = PaymentTermRuleRequest;

export interface PaymentTermListItemDto {
  readonly id: string;
  readonly code: string;
  readonly displayNameAr: string;
  readonly displayNameEn: string;
  readonly status: string;
  readonly version: number;
  readonly currentVersionNumber?: number | null | undefined;
  readonly currentEffectiveFrom?: string | null | undefined;
}

export interface PaymentTermVersionDto {
  readonly id: string;
  readonly versionNumber: number;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null | undefined;
  readonly status: string;
  readonly description?: string | null | undefined;
  readonly rules: readonly PaymentTermRuleRequest[];
  readonly termVersion: number;
}

export interface PaymentTermHistoryDto {
  readonly id: string;
  readonly code: string;
  readonly displayNameAr: string;
  readonly displayNameEn: string;
  readonly status: string;
  readonly version: number;
  readonly versions: readonly PaymentTermVersionDto[];
}

export interface PaymentTermDto {
  readonly id: string;
  readonly code: string;
  readonly displayNameAr: string;
  readonly displayNameEn: string;
  readonly versionNumber: number;
  readonly effectiveFrom: string;
  readonly rules: readonly PaymentTermRuleRequest[];
}

export interface CreatePaymentTermRequest {
  readonly code: string;
  readonly displayNameAr: string;
  readonly displayNameEn: string;
  readonly description?: string | null | undefined;
  readonly effectiveFrom: string;
  readonly rules: readonly PaymentTermRuleRequest[];
}

export interface CreatePaymentTermVersionRequest {
  readonly effectiveFrom: string;
  readonly description?: string | null | undefined;
  readonly rules: readonly PaymentTermRuleRequest[];
  readonly expectedTermVersion: number;
}

export interface PaymentTermLifecycleRequest {
  readonly expectedTermVersion: number;
}

export interface PaymentScheduleRequest {
  readonly totalAmount: string;
  readonly basisDate: string;
  readonly scale: number;
  readonly rules: readonly PaymentTermRuleRequest[];
}

export interface PaymentScheduleLineDto {
  readonly sequence: number;
  readonly amount: string;
  readonly dueDate: string;
  readonly allocation: PaymentAllocation;
}

export type PaymentScheduleLine = PaymentScheduleLineDto;
