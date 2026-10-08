export interface LegalEntityContext {
  readonly legalEntityId: string;
  readonly code: string;
  readonly name: string;
  readonly baseCurrencyCode: string;
  readonly countryCode: string;
}

export interface BranchContext {
  readonly branchId: string;
  readonly legalEntityId: string;
  readonly code: string;
  readonly name: string;
}
