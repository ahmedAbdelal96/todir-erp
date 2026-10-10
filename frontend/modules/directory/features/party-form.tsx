import React from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { partyFormSchema, type PartyFormData } from "../schemas/party-schema.ts";
import { FormSection } from "../../../components/forms/form-section.tsx";
import { FormActions } from "../../../components/forms/form-actions.tsx";
import { Checkbox } from "../../../components/primitives/checkbox.tsx";

export interface PartyFormProps { readonly initialData?: Partial<PartyFormData> | undefined; readonly onSubmit: (data: PartyFormData) => Promise<void>; readonly onCancel?: (() => void) | undefined; readonly isSubmitting?: boolean; readonly isLocked?: boolean; }
const inputStyle: React.CSSProperties = { width: "100%", boxSizing: "border-box", padding: "0.6rem 0.7rem", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-sm)", background: "var(--color-surface-card)", color: "var(--color-content-primary)" };

export function PartyForm({ initialData, onSubmit, onCancel, isSubmitting = false, isLocked = false }: PartyFormProps): React.JSX.Element {
  const { register, handleSubmit, watch, setValue, formState: { isDirty, errors } } = useForm<PartyFormData>({
    resolver: zodResolver(partyFormSchema),
    defaultValues: { partyType: initialData?.partyType ?? "Organization", displayName: initialData?.displayName ?? "", legalName: initialData?.legalName ?? "", tradeName: initialData?.tradeName ?? "", nameAr: initialData?.nameAr ?? "", nameEn: initialData?.nameEn ?? "", firstName: initialData?.firstName ?? "", middleName: initialData?.middleName ?? "", lastName: initialData?.lastName ?? "", countryCode: initialData?.countryCode ?? "EG", primaryLanguage: initialData?.primaryLanguage ?? "ar", roles: initialData?.roles ?? ["Customer"], identifierType: initialData?.identifierType ?? "", identifierValue: initialData?.identifierValue ?? "", identifierCountryCode: initialData?.identifierCountryCode ?? "EG", email: initialData?.email ?? "", phone: initialData?.phone ?? "", addressLine1: initialData?.addressLine1 ?? "", addressCity: initialData?.addressCity ?? "", addressCountryCode: initialData?.addressCountryCode ?? "EG" }
  });
  const type = watch("partyType"); const roles = watch("roles") ?? [];
  const toggleRole = (role: "Customer" | "Supplier") => setValue("roles", roles.includes(role) ? roles.filter(item => item !== role) : [...roles, role], { shouldDirty: true, shouldValidate: true });
  const field = (name: keyof PartyFormData, label: string, required = false) => <label style={{ display: "flex", flexDirection: "column", gap: "0.3rem" }}><span>{label}{required ? " *" : ""}</span><input disabled={isLocked} style={inputStyle} {...register(name)} />{errors[name] ? <small style={{ color: "var(--color-status-danger)" }}>{String(errors[name]?.message ?? "")}</small> : null}</label>;
  return <form onSubmit={handleSubmit(onSubmit)} style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
    <FormSection title="الهوية الأساسية" description="Party identity is tenant-wide and shared by all roles.">
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit,minmax(220px,1fr))", gap: "1rem" }}>
        <label style={{ display: "flex", flexDirection: "column", gap: "0.3rem" }}><span>نوع الطرف *</span><select disabled={isLocked} style={inputStyle} {...register("partyType")}><option value="Organization">منشأة / شركة</option><option value="Person">فرد / شخص</option></select></label>
        {field("displayName", type === "Person" ? "الاسم المعروض" : "الاسم التجاري / المعروض", true)}
        {field("legalName", "الاسم القانوني")}{field("tradeName", "الاسم التجاري")}{field("nameAr", "الاسم بالعربية")}{field("nameEn", "الاسم بالإنجليزية")}
        {type === "Person" ? <>{field("firstName", "الاسم الأول")}{field("middleName", "الاسم الأوسط")}{field("lastName", "اسم العائلة")}</> : null}
        {field("countryCode", "الدولة")}{field("primaryLanguage", "اللغة الأساسية")}
      </div>
    </FormSection>
    <FormSection title="الأدوار" description="Customer and Supplier are roles; they do not redefine Party identity.">
      <div style={{ display: "flex", gap: "1.5rem", flexWrap: "wrap" }}><Checkbox isSelected={roles.includes("Customer")} onChange={() => toggleRole("Customer")} isLocked={isLocked}>عميل</Checkbox><Checkbox isSelected={roles.includes("Supplier")} onChange={() => toggleRole("Supplier")} isLocked={isLocked}>مورد</Checkbox></div>
      {errors.roles ? <small style={{ color: "var(--color-status-danger)" }}>{String(errors.roles.message ?? "")}</small> : null}
    </FormSection>
    <FormSection title="المعرف الرسمي" description="Country-aware identifier data; validation remains policy-driven."><div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit,minmax(180px,1fr))", gap: "1rem" }}>{field("identifierType", "نوع المعرف")}{field("identifierValue", "قيمة المعرف")}{field("identifierCountryCode", "دولة المعرف")}</div></FormSection>
    <FormSection title="قنوات الاتصال"><div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit,minmax(220px,1fr))", gap: "1rem" }}>{field("email", "البريد الإلكتروني")}{field("phone", "الهاتف")}</div></FormSection>
    <FormSection title="العنوان الأساسي"><div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit,minmax(220px,1fr))", gap: "1rem" }}>{field("addressLine1", "العنوان")}{field("addressCity", "المدينة")}{field("addressCountryCode", "الدولة")}</div></FormSection>
    <FormActions submitLabel="حفظ الطرف" cancelLabel="إلغاء" isSubmitting={isSubmitting} isDirty={isDirty} isLocked={isLocked} onCancel={onCancel} />
  </form>;
}
