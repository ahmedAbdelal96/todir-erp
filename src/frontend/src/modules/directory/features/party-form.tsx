import React from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { partyFormSchema, type PartyFormData } from "../schemas/party-schema.ts";
import { FormField } from "../../../design-system/forms/form-field.tsx";
import { FormSection } from "../../../design-system/forms/form-section.tsx";
import { FormActions } from "../../../design-system/forms/form-actions.tsx";
import { TextField } from "../../../design-system/primitives/text-field.tsx";
import { Select } from "../../../design-system/primitives/select.tsx";
import { Checkbox } from "../../../design-system/primitives/checkbox.tsx";
import { PhoneField } from "../../../design-system/fields/phone-field.tsx";
import { EmailField } from "../../../design-system/fields/email-field.tsx";

export interface PartyFormProps {
  readonly initialData?: Partial<PartyFormData> | undefined;
  readonly onSubmit: (data: PartyFormData) => Promise<void>;
  readonly onCancel?: (() => void) | undefined;
  readonly isSubmitting?: boolean | undefined;
  readonly isLocked?: boolean | undefined;
}

export function PartyForm({
  initialData,
  onSubmit,
  onCancel,
  isSubmitting = false,
  isLocked = false
}: PartyFormProps): React.JSX.Element {
  const {
    control,
    handleSubmit,
    watch,
    setValue,
    formState: { isDirty, errors }
  } = useForm<PartyFormData>({
    resolver: zodResolver(partyFormSchema),
    defaultValues: {
      partyType: initialData?.partyType ?? "Organization",
      displayName: initialData?.displayName ?? "",
      legalName: initialData?.legalName ?? "",
      tradeName: initialData?.tradeName ?? "",
      taxId: initialData?.taxId ?? "",
      nationalId: initialData?.nationalId ?? "",
      primaryEmail: initialData?.primaryEmail ?? "",
      primaryPhone: initialData?.primaryPhone ?? "",
      roles: initialData?.roles ?? ["Customer"]
    }
  });

  const selectedPartyType = watch("partyType");
  const selectedRoles = watch("roles") || [];

  const handleRoleToggle = (role: "Customer" | "Vendor" | "Employee" | "Partner") => {
    const exists = selectedRoles.includes(role);
    const updated = exists
      ? selectedRoles.filter((r) => r !== role)
      : [...selectedRoles, role];
    setValue("roles", updated, { shouldDirty: true, shouldValidate: true });
  };

  return (
    <form
      onSubmit={handleSubmit(onSubmit)}
      style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}
    >
      <FormSection title="البيانات الأساسية" description="تصنيف الطرف والاسم التجاري">
        <FormField
          name="partyType"
          control={control}
          label="نوع الطرف"
          isRequired
          isLocked={isLocked}
          render={({ field, label, isRequired, errorMessage }) => (
            <Select
              label={label}
              isRequired={isRequired}
              errorMessage={errorMessage}
              selectedKey={field.value}
              onSelectionChange={(key) => field.onChange(key as "Person" | "Organization")}
              options={[
                { id: "Organization", label: "منشأة / شركة" },
                { id: "Person", label: "فرد / شخص" }
              ]}
            />
          )}
        />

        <FormField
          name="displayName"
          control={control}
          label={selectedPartyType === "Organization" ? "الاسم التجاري" : "الاسم الكامل"}
          isRequired
          isLocked={isLocked}
          render={({ field, label, isRequired, errorMessage }) => (
            <TextField
              label={label}
              isRequired={isRequired}
              errorMessage={errorMessage}
              value={field.value}
              onChange={field.onChange}
              placeholder={selectedPartyType === "Organization" ? "أدخل الاسم التجاري..." : "أدخل الاسم الكامل..."}
            />
          )}
        />

        {selectedPartyType === "Organization" && (
          <FormField
            name="legalName"
            control={control}
            label="الاسم القانوني الرسمي"
            isLocked={isLocked}
            render={({ field, label, errorMessage }) => (
              <TextField
                label={label}
                errorMessage={errorMessage}
                value={field.value}
                onChange={field.onChange}
                placeholder="الاسم المسجل في السجل التجاري"
              />
            )}
          />
        )}

        <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
          <span style={{ fontSize: "0.875rem", fontWeight: 500, color: "var(--color-content-primary)" }}>
            الأدوار التجارية <span style={{ color: "var(--color-status-danger)" }}>*</span>
          </span>
          <div style={{ display: "flex", gap: "1rem", flexWrap: "wrap" }}>
            {([
              { key: "Customer", label: "عميل" },
              { key: "Vendor", label: "مورد" },
              { key: "Employee", label: "موظف" },
              { key: "Partner", label: "شريك" }
            ] as const).map(({ key, label }) => (
              <Checkbox
                key={key}
                isSelected={selectedRoles.includes(key)}
                onChange={() => handleRoleToggle(key)}
                isLocked={isLocked}
              >
                {label}
              </Checkbox>
            ))}
          </div>
          {errors.roles && (
            <span style={{ fontSize: "0.75rem", color: "var(--color-status-danger)" }}>
              {errors.roles.message}
            </span>
          )}
        </div>
      </FormSection>

      <FormSection title="البيانات الضريبية والرسمية" description="الرقم الضريبي أو الهوية الوطنية">
        {selectedPartyType === "Organization" ? (
          <FormField
            name="taxId"
            control={control}
            label="الرقم الضريبي"
            isLocked={isLocked}
            render={({ field, label, errorMessage }) => (
              <TextField
                label={label}
                errorMessage={errorMessage}
                value={field.value}
                onChange={field.onChange}
                placeholder="300000000000003"
              />
            )}
          />
        ) : (
          <FormField
            name="nationalId"
            control={control}
            label="رقم الهوية الوطنية / الإقامة"
            isLocked={isLocked}
            render={({ field, label, errorMessage }) => (
              <TextField
                label={label}
                errorMessage={errorMessage}
                value={field.value}
                onChange={field.onChange}
                placeholder="1000000000"
              />
            )}
          />
        )}
      </FormSection>

      <FormSection title="قنوات الاتصال" description="بيانات التواصل الأساسية">
        <FormField
          name="primaryEmail"
          control={control}
          label="البريد الإلكتروني"
          isLocked={isLocked}
          render={({ field, label, errorMessage }) => (
            <EmailField
              label={label}
              errorMessage={errorMessage}
              value={field.value}
              onChange={field.onChange}
            />
          )}
        />

        <FormField
          name="primaryPhone"
          control={control}
          label="رقم الهاتف"
          isLocked={isLocked}
          render={({ field, label, errorMessage }) => (
            <PhoneField
              label={label}
              errorMessage={errorMessage}
              value={field.value}
              onChange={field.onChange}
            />
          )}
        />
      </FormSection>

      <FormActions
        submitLabel="حفظ البيانات"
        cancelLabel="إلغاء"
        isSubmitting={isSubmitting}
        isDirty={isDirty}
        isLocked={isLocked}
        onCancel={onCancel}
      />
    </form>
  );
}
