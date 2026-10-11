export type PartyType = "Person" | "Organization";
export type PartyStatus = "Draft" | "Active" | "Blocked" | "Archived" | "Merged";
export type PartyRoleCode = "Customer" | "Supplier";

export interface PartyRole {
  readonly code: PartyRoleCode;
  readonly status: "Active" | "Inactive";
  readonly version: number;
}

export interface PartyIdentifier {
  readonly id: string;
  readonly type: string;
  readonly countryCode?: string | null;
  readonly value: string;
  readonly authority?: string | null;
  readonly isPrimary: boolean;
  readonly verificationStatus: string;
}

export interface ContactPoint {
  readonly id: string;
  readonly type: "Email" | "Phone" | "Website" | "Other";
  readonly value: string;
  readonly label?: string | null;
  readonly isPrimary: boolean;
}

export type ContactInfo = ContactPoint;

export interface Address {
  readonly id: string;
  readonly usage: "General" | "Billing" | "Shipping" | "Registered";
  readonly countryCode: string;
  readonly city?: string | null;
  readonly region?: string | null;
  readonly postalCode?: string | null;
  readonly line1: string;
  readonly line2?: string | null;
  readonly isDefault: boolean;
}

/** Contact persons represent named individuals linked to a party (e.g. procurement manager). */
export interface ContactPersonDto {
  readonly id: string;
  readonly displayName: string;
  readonly jobTitle?: string | null;
  readonly department?: string | null;
  readonly status: string;
  readonly usages: readonly string[];
}

/** Alternative names / trade aliases registered for a party. */
export interface PartyAliasDto {
  readonly id: string;
  readonly alias: string;
  readonly languageCode?: string | null;
  readonly aliasType: string;
}

/** Visibility scope entry controlling which legal entity can see the party. */
export interface PartyVisibilityDto {
  readonly id: string;
  readonly scopeType: string;
  readonly legalEntityId?: string | null;
}

export interface Party {
  readonly id: string;
  readonly partyType: PartyType;
  readonly status: PartyStatus;
  readonly displayName: string;
  readonly legalName?: string | null;
  readonly tradeName?: string | null;
  readonly nameAr?: string | null;
  readonly nameEn?: string | null;
  readonly firstName?: string | null;
  readonly middleName?: string | null;
  readonly lastName?: string | null;
  readonly countryCode?: string | null;
  readonly primaryLanguage?: string | null;
  readonly roles: readonly PartyRole[];
  readonly identifiers: readonly PartyIdentifier[];
  readonly contactPoints: readonly ContactPoint[];
  readonly addresses: readonly Address[];
  readonly contactPersons?: readonly ContactPersonDto[] | null;
  readonly aliases?: readonly PartyAliasDto[] | null;
  readonly visibility?: readonly PartyVisibilityDto[] | null;
  readonly createdAt: string;
  readonly updatedAt: string;
  readonly version: number;
}

export interface IdentifierInput {
  readonly type: string;
  readonly value: string;
  readonly countryCode?: string | undefined;
  readonly authority?: string | undefined;
  readonly isPrimary?: boolean | undefined;
}

export interface ContactPointInput {
  readonly type: "Email" | "Phone" | "Website" | "Other";
  readonly value: string;
  readonly label?: string | undefined;
  readonly isPrimary?: boolean | undefined;
}

export interface AddressInput {
  readonly usage: "General" | "Billing" | "Shipping" | "Registered";
  readonly countryCode: string;
  readonly city?: string | undefined;
  readonly region?: string | undefined;
  readonly postalCode?: string | undefined;
  readonly line1: string;
  readonly line2?: string | undefined;
  readonly isDefault?: boolean | undefined;
}

export interface ContactPersonInput {
  readonly displayName: string;
  readonly firstName?: string | undefined;
  readonly lastName?: string | undefined;
  readonly jobTitle?: string | undefined;
  readonly department?: string | undefined;
  readonly languageCode?: string | undefined;
}

export interface ContactPersonLinkInput {
  readonly contactPersonId: string;
  readonly usage?: string | undefined;
  readonly isPrimary?: boolean | undefined;
}

/** Combined request matching the backend AddContactPersonRequest. */
export interface AddContactPersonRequest {
  readonly person: ContactPersonInput;
  readonly link?: ContactPersonLinkInput | undefined;
}

export interface AliasInput {
  readonly alias: string;
  readonly languageCode?: string | undefined;
  readonly aliasType?: string | undefined;
}

export interface VisibilityInput {
  readonly scopeType: "TenantWide" | "SelectedLegalEntities" | "SingleLegalEntity" | "LegalEntity" | "Branch";
  readonly legalEntityId?: string | undefined;
}

export interface MergePartyRequest {
  readonly masterPartyId: string;
  readonly reason: string;
  readonly confirmIrreversible: boolean;
}

export interface CreatePartyInput {
  readonly partyType: PartyType;
  readonly displayName: string;
  readonly legalName?: string | undefined;
  readonly tradeName?: string | undefined;
  readonly nameAr?: string | undefined;
  readonly nameEn?: string | undefined;
  readonly firstName?: string | undefined;
  readonly middleName?: string | undefined;
  readonly lastName?: string | undefined;
  readonly countryCode?: string | undefined;
  readonly primaryLanguage?: string | undefined;
  readonly roles: readonly PartyRoleCode[];
  readonly identifiers?: readonly IdentifierInput[] | undefined;
  readonly contactPoints?: readonly ContactPointInput[] | undefined;
  readonly addresses?: readonly AddressInput[] | undefined;
}

export interface UpdatePartyInput {
  readonly displayName: string;
  readonly legalName?: string | undefined;
  readonly tradeName?: string | undefined;
  readonly nameAr?: string | undefined;
  readonly nameEn?: string | undefined;
  readonly firstName?: string | undefined;
  readonly middleName?: string | undefined;
  readonly lastName?: string | undefined;
  readonly countryCode?: string | undefined;
  readonly primaryLanguage?: string | undefined;
  readonly version: number;
}

export interface DuplicateCandidate {
  readonly id: string;
  readonly candidatePartyId: string;
  readonly reason: string;
  readonly confidence: number;
  readonly status: string;
}

export interface ReadinessCapabilityDto {
  readonly capability: string;
  readonly ready: boolean;
  readonly missing: readonly string[];
  readonly warnings: readonly string[];
}

export interface PartyReadiness {
  readonly partyId: string;
  readonly ready: boolean;
  readonly missing: readonly string[];
  readonly warnings: readonly string[];
  readonly capabilities?: readonly ReadinessCapabilityDto[] | null;
}

export interface AuditEntryDto {
  readonly id: string;
  readonly action: string;
  readonly entityType: string;
  readonly entityId: string;
  readonly reason?: string | null;
  readonly occurredAt: string;
  readonly afterJson?: string | null;
}

export interface DirectoryImportRow {
  readonly externalReference: string;
  readonly partyType: PartyType;
  readonly displayName: string;
  readonly countryCode?: string | undefined;
  readonly nameAr?: string | undefined;
  readonly nameEn?: string | undefined;
  readonly legalName?: string | undefined;
  readonly tradeName?: string | undefined;
  readonly role?: string | undefined;
  readonly identifierType?: string | undefined;
  readonly identifierValue?: string | undefined;
  readonly email?: string | undefined;
  readonly phone?: string | undefined;
  readonly addressLine1?: string | undefined;
  readonly city?: string | undefined;
}

export interface DirectoryImportRowResult {
  readonly externalReference: string;
  readonly action: string;
  readonly partyId?: string | null;
  readonly errors: readonly string[];
  readonly possibleMatches: readonly string[];
}

export interface DirectoryImportResult {
  readonly operationId: string;
  readonly committed: boolean;
  readonly rows: readonly DirectoryImportRowResult[];
}

export interface DirectoryMergeResult {
  readonly masterPartyId: string;
  readonly sourcePartyId: string;
  readonly status: string;
  readonly contributors: readonly string[];
}
