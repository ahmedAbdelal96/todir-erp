import React from "react";
import { useTranslation } from "react-i18next";
import { useForm, Controller } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { createPartyFormSchema, type PartyFormData } from "../validation/party-schema.ts";
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
  const { t } = useTranslation("directory");
  const partyFormSchema = createPartyFormSchema((key) => t(key));
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
        title={t("partyForm.sectionIdentity")}
        description={t("partyForm.identityDescription")}
      >
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "1rem" }}>
          <div>
            <Controller
              name="partyType"
              control={control}
              render={({ field }) => (
                <Select
                  label={t("partyForm.entityType")}
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
                  <SelectItem id="Organization" textValue={t("partyForm.organization")}>{t("partyForm.organization")}</SelectItem>
                  <SelectItem id="Person" textValue={t("partyForm.person")}>{t("partyForm.person")}</SelectItem>
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
                  label={type === "Person" ? t("partyForm.fullDisplayName") : t("partyForm.businessDisplayName")}
                  placeholder={type === "Person" ? t("partyForm.personExample") : t("partyForm.organizationExample")}
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
                  label={t("partyForm.legalName")}
                  placeholder={t("partyForm.legalNamePlaceholder")}
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
                    label={t("partyForm.tradeName")}
                    placeholder={t("partyForm.tradeNamePlaceholder")}
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
                      label={t("partyForm.firstName")}
                      placeholder={t("partyForm.firstNamePlaceholder")}
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
                      label={t("partyForm.middleName")}
                      placeholder={t("partyForm.middleNamePlaceholder")}
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
                      label={t("partyForm.lastName")}
                      placeholder={t("partyForm.lastNamePlaceholder")}
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
                  label={t("partyForm.country")}
                  selectedKey={field.value ?? "EG"}
                  onSelectionChange={(key) => field.onChange(String(key ?? "EG"))}
                  isDisabled={isLocked}
                >
                  <SelectItem id="EG" textValue={`${t("partyForm.egypt")} (EG)`}>{t("partyForm.egypt")} (EG)</SelectItem>
                  <SelectItem id="SA" textValue={`${t("partyForm.saudi")} (SA)`}>{t("partyForm.saudi")} (SA)</SelectItem>
                  <SelectItem id="AE" textValue={`${t("partyForm.uae")} (AE)`}>{t("partyForm.uae")} (AE)</SelectItem>
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
                  label={t("partyForm.correspondenceLanguage")}
                  selectedKey={field.value ?? "ar"}
                  onSelectionChange={(key) => field.onChange(String(key ?? "ar"))}
                  isDisabled={isLocked}
                >
                  <SelectItem id="ar" textValue={t("partyForm.arabicDefault")}>{t("partyForm.arabicDefault")}</SelectItem>
                  <SelectItem id="en" textValue={t("partyForm.english")}>{t("partyForm.english")}</SelectItem>
                </Select>
              )}
            />
          </div>
        </div>
      </FormSection>

      {/* 2. الأدوار التجارية */}
      <FormSection
        title={t("partyForm.roles")}
        description={t("partyForm.rolesDescription")}
      >
        <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
          <div style={{ display: "flex", gap: "2rem", flexWrap: "wrap", alignItems: "center" }}>
            <Checkbox
              isSelected={roles.includes("Customer")}
              onChange={() => toggleRole("Customer")}
              isLocked={isLocked}
            >
              <span style={{ fontWeight: 600 }}>{t("partyForm.customerRole")}</span>
            </Checkbox>

            <Checkbox
              isSelected={roles.includes("Supplier")}
              onChange={() => toggleRole("Supplier")}
              isLocked={isLocked}
            >
              <span style={{ fontWeight: 600 }}>{t("partyForm.supplierRole")}</span>
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
        title={t("partyForm.identifiers")}
        description={t("partyForm.identifiersDescription")}
      >
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "1rem" }}>
          <div>
            <Controller
              name="identifierType"
              control={control}
              render={({ field }) => (
                <Select
                  label={t("partyForm.identifierType")}
                  selectedKey={field.value || "none"}
                  onSelectionChange={(key) => field.onChange(key === "none" ? "" : String(key ?? ""))}
                  isDisabled={isLocked}
                >
                  <SelectItem id="none" textValue={t("partyForm.noIdentifier")}>{t("partyForm.noIdentifier")}</SelectItem>
                  <SelectItem id="TaxId" textValue={`${t("partyForm.taxId")} (VAT)`}>{t("partyForm.taxId")} (VAT)</SelectItem>
                  <SelectItem id="CommercialRegister" textValue={`${t("partyForm.commercialRegister")} (CR)`}>{t("partyForm.commercialRegister")} (CR)</SelectItem>
                  <SelectItem id="NationalId" textValue={t("partyForm.nationalId")}>{t("partyForm.nationalId")}</SelectItem>
                  <SelectItem id="InternalReference" textValue={t("partyForm.internalReference")}>{t("partyForm.internalReference")}</SelectItem>
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
                  label={t("partyForm.identifierValue")}
                  placeholder={t("partyForm.identifierExample")}
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
        title={t("partyForm.contactChannels")}
        description={t("partyForm.contactDescription")}
      >
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))", gap: "1rem" }}>
          <Controller
            name="email"
            control={control}
            render={({ field }) => (
              <EmailField
                label={t("partyForm.email")}
                placeholder={t("partyForm.emailPlaceholder")}
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
                label={t("partyForm.phone")}
                placeholder={t("partyForm.phonePlaceholder")}
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
        title={t("partyForm.address")}
        description={t("partyForm.addressDescription")}
      >
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "1rem" }}>
          <div style={{ gridColumn: "1 / -1" }}>
            <Controller
              name="addressLine1"
              control={control}
              render={({ field }) => (
                <TextField
                  label={t("partyForm.addressLine")}
                  placeholder={t("partyForm.addressPlaceholder")}
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
                  label={t("partyForm.city")}
                  placeholder={t("partyForm.cityExample")}
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
        submitLabel={t("partyForm.save")}
        cancelLabel={t("partyForm.cancel")}
        isSubmitting={isSubmitting}
        isDirty={isDirty}
        isLocked={isLocked}
        onCancel={onCancel}
      />
    </form>
  );
}
