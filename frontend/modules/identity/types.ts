export type UserAccountStatus = "Active" | "Suspended";

export interface UserAccountSummary {
  readonly id: string;
  readonly tenantId: string;
  readonly name: string;
  readonly email: string;
  readonly loginPrefix: string;
  readonly status: UserAccountStatus | string;
  readonly mustChangePassword: boolean;
  readonly createdAt: string;
}

export interface CreatedUserAccount {
  readonly user: UserAccountSummary;
  readonly temporaryPassword: string;
}

export interface ResetPasswordResult {
  readonly temporaryPassword: string;
}

export interface RoleSummary {
  readonly id: string;
  readonly code: string;
  readonly name: string;
  readonly isSystemTemplate: boolean;
  readonly isActive: boolean;
  readonly version: number;
  readonly permissions: readonly string[];
}

export interface LoginDomainInfo {
  readonly loginDomain: string;
}

export interface CreateUserPayload {
  readonly name: string;
  readonly emailPrefix: string;
  readonly roleIds?: readonly string[] | undefined;
}

export interface ChangeLoginDomainPayload {
  readonly loginDomain: string;
}

export interface ListUsersParams {
  readonly search?: string | undefined;
  readonly active?: boolean | undefined;
  readonly offset?: number | undefined;
  readonly limit?: number | undefined;
  readonly page?: number | undefined;
}

export interface UserAccountPage {
  readonly items: readonly UserAccountSummary[];
  readonly totalCount: number;
  readonly page: number;
  readonly pageSize: number;
}
