import React from "react";
import { useForm, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { partyFormSchema, type PartyFormData } from "../validation/party-schema.ts";
import { FormSection } from "../../../components/forms/form-section.tsx";
import { FormActions } from "../../../components/forms/form-actions.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select, SelectItem } from "../../../components/primitives/select.tsx";
import { Checkbox } from "../../../components/primitives/checkbox.tsx";
import { EmailField } from "../../../components/fields/email-field.tsx";
import { PhoneField } from "../../../components/fields/phone-field.tsx";

export interface PartyFormProps {
  readonly initialData?: Partial<PartyFormData> | undefined;
  readonly onSubmit: (data: PartyFormData) => Promise<void>;
  readonly onCancel?: (() => void) | undefined;
  readonly isSubmitting?: boolean;
  readonly isLocked?: boolean;
}

export function PartyForm({
  initialData,
  onSubmit,
  onCancel,
  isSubmitting = false,
  isLocked = false
}: PartyFormProps): React.JSX.Element {
  const {
    handleSubmit,
    watch,
    setValue,
    control,
    formState: { isDirty, errors }
  } = useForm<PartyFormData>({
    resolver: zodResolver(partyFormSchema),
    defaultValues: {
      partyType: initialData?.partyType ?? "Organization",
      displayName: initialData?.displayName ?? "",
      legalName: initialData?.legalName ?? "",
      tradeName: initialData?.tradeName ?? "",
      nameAr: initialData?.nameAr ?? "",
      nameEn: initialData?.nameEn ?? "",
      firstName: initialData?.firstName ?? "",
      middleName: initialData?.middleName ?? "",
      lastName: initialData?.lastName ?? "",
      countryCode: initialData?.countryCode ?? "EG",
      primaryLanguage: initialData?.primaryLanguage ?? "ar",
      roles: initialData?.roles ?? [],
      identifierType: initialData?.identifierType ?? "",
      identifierValue: initialData?.identifierValue ?? "",
      identifierCountryCode: initialData?.identifierCountryCode ?? "EG",
      email: initialData?.email ?? "",
      phone: initialData?.phone ?? "",
      addressLine1: initialData?.addressLine1 ?? "",
      addressCity: initialData?.addressCity ?? "",
      addressCountryCode: initialData?.addressCountryCode ?? "EG"
    }
  });

  const type = watch("partyType");
  const roles = watch("roles") ?? [];

  const toggleRole = (role: "Customer" | "Supplier") => {
    const updated = roles.includes(role)
      ? roles.filter(item => item !== role)
      : [...roles, role];
    setValue("roles", updated, { shouldDirty: true, shouldValidate: true });
  };

  return (
    <form
      onSubmit={handleSubmit(onSubmit)}
      style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}
      noValidate
    >
      {/* 1. الهوية الأساسية للطرف */}
      <FormSection
        title="الهوية ونوع الكيان"
        description="حدد ما إذا كان الطرف شركة ومنشأة تجارية أو فرداً مستقلاً، مع تسجيل الاسم المعتمد للتعامل."
      >
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "1rem" }}>
          <div>
            <Controller
              name="partyType"
              control={control}
              render={({ field }) => (
                <Select
                  label="نوع الكيان"
                  selectedKey={field.value}
                  onSelectionChange={(key) => {
                    const newType = String(key ?? "Organization") as "Person" | "Organization";
                    field.onChange(newType);
                    if (newType === "Organization") {
                      setValue("firstName", "");
                      setValue("middleName", "");
                      setValue("lastName", "");
                    }
                  }}
                  isDisabled={isLocked}
                >
                  <SelectItem id="Organization" textValue="منشأة / شركة">منشأة / شركة</SelectItem>
                  <SelectItem id="Person" textValue="فرد / شخص مستقل">فرد / شخص مستقل</SelectItem>
                </Select>
              )}
            />
          </div>

          <div>
            <Controller
              name="displayName"
              control={control}
              render={({ field }) => (
                <TextField
                  label={type === "Person" ? "الاسم المعروض بالكامل" : "الاسم التجاري / المعروض"}
                  placeholder={type === "Person" ? "مثال: أحمد محمود إبراهيم" : "مثال: أطلس للتجارة العامة"}
                  isRequired={true}
                  isLocked={isLocked}
                  errorMessage={errors.displayName?.message}
                  value={field.value}
                  onChange={field.onChange}
                />
              )}
            />
          </div>

          <div>
            <Controller
              name="legalName"
              control={control}
              render={({ field }) => (
                <TextField
                  label="الاسم القانوني المسجل"
                  placeholder="الاسم الرسمي في الأوراق الحكومية والسجلات"
                  isLocked={isLocked}
                  errorMessage={errors.legalName?.message}
                  value={field.value ?? ""}
                  onChange={field.onChange}
                />
              )}
            />
          </div>

          {type === "Organization" && (
            <div>
              <Controller
                name="tradeName"
                control={control}
                render={({ field }) => (
                  <TextField
                    label="الاسم التجاري المعروف"
                    placeholder="اسم العلامة التجارية أو الشهرة"
                    isLocked={isLocked}
                    errorMessage={errors.tradeName?.message}
                    value={field.value ?? ""}
                    onChange={field.onChange}
                  />
                )}
              />
            </div>
          )}

          {type === "Person" && (
            <>
              <div>
                <Controller
                  name="firstName"
                  control={control}
                  render={({ field }) => (
                    <TextField
                      label="الاسم الأول"
                      placeholder="الاسم الشخصي"
                      isLocked={isLocked}
                      errorMessage={errors.firstName?.message}
                      value={field.value ?? ""}
                      onChange={field.onChange}
                    />
                  )}
                />
              </div>
              <div>
                <Controller
                  name="middleName"
                  control={control}
                  render={({ field }) => (
                    <TextField
                      label="اسم الأب / الأوسط"
                      placeholder="الاسم الثاني"
                      isLocked={isLocked}
                      errorMessage={errors.middleName?.message}
                      value={field.value ?? ""}
                      onChange={field.onChange}
                    />
                  )}
                />
              </div>
              <div>
                <Controller
                  name="lastName"
                  control={control}
                  render={({ field }) => (
                    <TextField
                      label="اسم العائلة / اللقب"
                      placeholder="اللقب العائلي"
                      isLocked={isLocked}
                      errorMessage={errors.lastName?.message}
                      value={field.value ?? ""}
                      onChange={field.onChange}
                    />
                  )}
                />
              </div>
            </>
          )}

          <div>
            <Controller
              name="countryCode"
              control={control}
              render={({ field }) => (
                <Select
                  label="دولة التأسيس / الإقامة"
                  selectedKey={field.value ?? "EG"}
                  onSelectionChange={(key) => field.onChange(String(key ?? "EG"))}
                  isDisabled={isLocked}
                >
                  <SelectItem id="EG" textValue="جمهورية مصر العربية (EG)">جمهورية مصر العربية (EG)</SelectItem>
                  <SelectItem id="SA" textValue="المملكة العربية السعودية (SA)">المملكة العربية السعودية (SA)</SelectItem>
                  <SelectItem id="AE" textValue="الإمارات العربية المتحدة (AE)">الإمارات العربية المتحدة (AE)</SelectItem>
                </Select>
              )}
            />
          </div>

          <div>
            <Controller
              name="primaryLanguage"
              control={control}
              render={({ field }) => (
                <Select
                  label="لغة المراسلات الرسمية"
                  selectedKey={field.value ?? "ar"}
                  onSelectionChange={(key) => field.onChange(String(key ?? "ar"))}
                  isDisabled={isLocked}
                >
                  <SelectItem id="ar" textValue="العربية (الافتراضية)">العربية (الافتراضية)</SelectItem>
                  <SelectItem id="en" textValue="الإنجليزية">الإنجليزية</SelectItem>
                </Select>
              )}
            />
          </div>
        </div>
      </FormSection>

      {/* 2. الأدوار التجارية */}
      <FormSection
        title="الأدوار والعلاقات التجارية (اختياري)"
        description="تحديد دور الطرف كعميل أو مورد، أو يمكن تركه بدون أدوار كطرف عام في الدليل."
      >
        <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
          <div style={{ display: "flex", gap: "2rem", flexWrap: "wrap", alignItems: "center" }}>
            <Checkbox
              isSelected={roles.includes("Customer")}
              onChange={() => toggleRole("Customer")}
              isLocked={isLocked}
            >
              <span style={{ fontWeight: 600 }}>عميل (نقدم له مبيعات وخدمات)</span>
            </Checkbox>

            <Checkbox
              isSelected={roles.includes("Supplier")}
              onChange={() => toggleRole("Supplier")}
              isLocked={isLocked}
            >
              <span style={{ fontWeight: 600 }}>مورد (نستورد أو نشتري منه بضائع)</span>
            </Checkbox>
          </div>

          {errors.roles && (
            <span style={{ color: "var(--color-status-danger)", fontSize: "0.8125rem", fontWeight: 600 }}>
              {errors.roles.message}
            </span>
          )}
        </div>
      </FormSection>

      {/* 3. المعرفات الرسمية والضريبية */}
      <FormSection
        title="المعرفات والسجلات الحكومية"
        description="تسجيل الرقم الضريبي أو السجل التجاري لضمان التوثيق وإصدار الفواتير القانونية."
      >
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "1rem" }}>
          <div>
            <Controller
              name="identifierType"
              control={control}
              render={({ field }) => (
                <Select
                  label="نوع المعرف الرسمي"
                  selectedKey={field.value || "none"}
                  onSelectionChange={(key) => field.onChange(key === "none" ? "" : String(key ?? ""))}
                  isDisabled={isLocked}
                >
                  <SelectItem id="none" textValue="— بدون معرف حالياً —">— بدون معرف حالياً —</SelectItem>
                  <SelectItem id="TaxId" textValue="الرقم الضريبي (VAT / Tax ID)">الرقم الضريبي (VAT / Tax ID)</SelectItem>
                  <SelectItem id="CommercialRegister" textValue="السجل التجاري (CR)">السجل التجاري (CR)</SelectItem>
                  <SelectItem id="NationalId" textValue="الرقم القومي / الهوية الوطنية">الرقم القومي / الهوية الوطنية</SelectItem>
                  <SelectItem id="InternalReference" textValue="كود مرجعي داخلي">كود مرجعي داخلي</SelectItem>
                </Select>
              )}
            />
          </div>

          <div>
            <Controller
              name="identifierValue"
              control={control}
              render={({ field }) => (
                <TextField
                  label="قيمة ورقم المعرف"
                  placeholder="مثال: 300123456700003"
                  isLocked={isLocked}
                  errorMessage={errors.identifierValue?.message}
                  value={field.value ?? ""}
                  onChange={field.onChange}
                />
              )}
            />
          </div>
        </div>
      </FormSection>

      {/* 4. قنوات الاتصال والتواصل */}
      <FormSection
        title="قنوات الاتصال المباشرة"
        description="وسائل التواصل الأساسية لإرسال الفواتير والمطالبات والمراسلات اليومية."
      >
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))", gap: "1rem" }}>
          <Controller
            name="email"
            control={control}
            render={({ field }) => (
              <EmailField
                label="البريد الإلكتروني المعتمد"
                placeholder="contact@company.com"
                value={field.value ?? ""}
                onChange={field.onChange}
                isLocked={isLocked}
                errorMessage={errors.email?.message}
              />
            )}
          />

          <Controller
            name="phone"
            control={control}
            render={({ field }) => (
              <PhoneField
                label="رقم الهاتف للتواصل"
                placeholder="+20 100 000 0000"
                value={field.value ?? ""}
                onChange={field.onChange}
                isLocked={isLocked}
                errorMessage={errors.phone?.message}
              />
            )}
          />
        </div>
      </FormSection>

      {/* 5. المقر والعنوان */}
      <FormSection
        title="المقر والعنوان الجغرافي"
        description="العنوان المعتمد لعمليات الشحن والتوصيل والمقر الرئيسي."
      >
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "1rem" }}>
          <div style={{ gridColumn: "1 / -1" }}>
            <Controller
              name="addressLine1"
              control={control}
              render={({ field }) => (
                <TextField
                  label="العنوان التفصيلي"
                  placeholder="الشارع، رقم المبنى، الحي أو المنطقة الصناعية"
                  isLocked={isLocked}
                  errorMessage={errors.addressLine1?.message}
                  value={field.value ?? ""}
                  onChange={field.onChange}
                />
              )}
            />
          </div>

          <div>
            <Controller
              name="addressCity"
              control={control}
              render={({ field }) => (
                <TextField
                  label="المدينة / المركز"
                  placeholder="مثال: القاهرة، الجيزة، الإسكندرية"
                  isLocked={isLocked}
                  errorMessage={errors.addressCity?.message}
                  value={field.value ?? ""}
                  onChange={field.onChange}
                />
              )}
            />
          </div>
        </div>
      </FormSection>

      <FormActions
        submitLabel="حفظ بيانات الطرف التجاري"
        cancelLabel="إلغاء التعديل"
        isSubmitting={isSubmitting}
        isDirty={isDirty}
        isLocked={isLocked}
        onCancel={onCancel}
      />
    </form>
  );
}
