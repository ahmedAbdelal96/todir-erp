export type PartyType = "Person" | "Organization";
export type PartyStatus = "Active" | "Suspended" | "Archived";
export type PartyRole = "Customer" | "Vendor" | "Employee" | "Partner";

export interface ContactInfo {
  readonly id?: string | undefined;
  readonly type: "Email" | "Phone" | "Address";
  readonly value: string;
  readonly isPrimary: boolean;
  readonly label?: string | undefined;
}

export interface Party {
  readonly id: string;
  readonly partyNumber: string;
  readonly partyType: PartyType;
  readonly status: PartyStatus;
  readonly roles: readonly PartyRole[];
  readonly displayName: string;
  readonly legalName?: string | undefined;
  readonly tradeName?: string | undefined;
  readonly taxId?: string | undefined;
  readonly nationalId?: string | undefined;
  readonly primaryEmail?: string | undefined;
  readonly primaryPhone?: string | undefined;
  readonly createdAt: string;
  readonly updatedAt?: string | undefined;
}

export interface CreatePartyInput {
  readonly partyType: PartyType;
  readonly displayName: string;
  readonly legalName?: string | undefined;
  readonly tradeName?: string | undefined;
  readonly taxId?: string | undefined;
  readonly nationalId?: string | undefined;
  readonly primaryEmail?: string | undefined;
  readonly primaryPhone?: string | undefined;
  readonly roles: readonly PartyRole[];
}
