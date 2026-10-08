export interface TenantContext {
  readonly tenantId: string;
  readonly displayName: string;
  readonly slug?: string | undefined;
}

export interface UserContext {
  readonly userId: string;
  readonly email: string;
  readonly displayName: string;
  readonly roles: readonly string[];
}

export interface LocaleContext {
  readonly language: "ar" | "en";
  readonly direction: "rtl" | "ltr";
  readonly numeralSystem: "latin" | "arabic-indic";
}
