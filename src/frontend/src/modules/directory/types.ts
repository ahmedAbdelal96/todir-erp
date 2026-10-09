export type PartyType = "Person" | "Organization";
export type PartyStatus = "Draft" | "Active" | "Blocked" | "Archived" | "Merged";
export type PartyRoleCode = "Customer" | "Supplier";

export interface PartyRole { readonly code: PartyRoleCode; readonly status: "Active" | "Inactive"; readonly version: number; }
export interface PartyIdentifier { readonly id: string; readonly type: string; readonly countryCode?: string | null; readonly value: string; readonly authority?: string | null; readonly isPrimary: boolean; readonly verificationStatus: string; }
export interface ContactPoint { readonly id: string; readonly type: "Email" | "Phone" | "Website" | "Other"; readonly value: string; readonly label?: string | null; readonly isPrimary: boolean; }
export type ContactInfo = ContactPoint;
export interface Address { readonly id: string; readonly usage: "General" | "Billing" | "Shipping" | "Registered"; readonly countryCode: string; readonly city?: string | null; readonly region?: string | null; readonly postalCode?: string | null; readonly line1: string; readonly line2?: string | null; readonly isDefault: boolean; }
export interface Party {
  readonly id: string; readonly partyType: PartyType; readonly status: PartyStatus; readonly displayName: string; readonly legalName?: string | null; readonly tradeName?: string | null;
  readonly nameAr?: string | null; readonly nameEn?: string | null; readonly firstName?: string | null; readonly middleName?: string | null; readonly lastName?: string | null;
  readonly countryCode?: string | null; readonly primaryLanguage?: string | null; readonly roles: readonly PartyRole[]; readonly identifiers: readonly PartyIdentifier[];
  readonly contactPoints: readonly ContactPoint[]; readonly addresses: readonly Address[]; readonly createdAt: string; readonly updatedAt: string; readonly version: number;
}
export interface CreatePartyInput {
  readonly partyType: PartyType; readonly displayName: string; readonly legalName?: string | undefined; readonly tradeName?: string | undefined; readonly nameAr?: string | undefined; readonly nameEn?: string | undefined;
  readonly firstName?: string | undefined; readonly middleName?: string | undefined; readonly lastName?: string | undefined; readonly countryCode?: string | undefined; readonly primaryLanguage?: string | undefined; readonly roles: readonly PartyRoleCode[];
  readonly identifiers?: readonly { type: string; value: string; countryCode?: string | undefined; authority?: string | undefined; isPrimary?: boolean | undefined }[] | undefined;
  readonly contactPoints?: readonly { type: "Email" | "Phone" | "Website" | "Other"; value: string; label?: string | undefined; isPrimary?: boolean | undefined }[] | undefined;
  readonly addresses?: readonly { usage: "General" | "Billing" | "Shipping" | "Registered"; countryCode: string; city?: string | undefined; region?: string | undefined; postalCode?: string | undefined; line1: string; line2?: string | undefined; isDefault?: boolean | undefined }[] | undefined;
}
