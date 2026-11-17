import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { Radio, RadioGroup } from "../../../components/primitives/radio.tsx";
import { LocalDateField } from "../../../components/fields/local-date-field.tsx";
import { ProductPicker, type ProductPickerSelection } from "../../catalog/public.ts";
import { useSaveProductProfileMutation } from "../hooks/tax-queries.ts";
import { extractTaxError } from "../services/tax-error-map.ts";
import type { TaxCategoryItem, TaxProfileDto } from "../types.ts";

export interface ProductProfileModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly profile?: TaxProfileDto | null | undefined;
  readonly categories: readonly TaxCategoryItem[];
  readonly defaultEffectiveDate: string;
}

export function ProductProfileModal({
  isOpen,
  onClose,
  legalEntityId,
  profile,
  categories,
  defaultEffectiveDate
}: ProductProfileModalProps): React.JSX.Element | null {
  const { t } = useTranslation("tax");

  const [scope, setScope] = useState<"Template" | "Variant">(
    profile?.productVariantId ? "Variant" : "Template"
  );
  const [productTemplateId, setProductTemplateId] = useState(profile?.productTemplateId || "");
  const [productVariantId, setProductVariantId] = useState(profile?.productVariantId || "");
  const [categoryId, setCategoryId] = useState(
    profile?.categoryId || categories.find((c) => c.scope === "Product")?.id || ""
  );
  const [effectiveFrom, setEffectiveFrom] = useState(defaultEffectiveDate);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const mutation = useSaveProductProfileMutation();
  const isEditing = Boolean(profile?.id);

  const productCategories = categories.filter((c) => c.scope === "Product");

  const handleSave = async () => {
    if (!productTemplateId || (scope === "Variant" && !productVariantId)) return;
    setErrorMessage(null);
    try {
      await mutation.mutateAsync({
        id: profile?.id,
        request: {
          productTemplateId,
          productVariantId: scope === "Variant" && productVariantId ? productVariantId : null,
          legalEntityId: legalEntityId || null,
          categoryId,
          effectiveFrom
        }
      });
      onClose();
    } catch (err) {
      const mapped = extractTaxError(err);
      setErrorMessage(t(mapped.messageKey, { defaultValue: mapped.fallbackEn }));
    }
  };

  const footer = (
    <div style={{ display: "flex", gap: "var(--space-2)", justifyContent: "flex-end" }}>
      <Button variant="secondary" onPress={onClose}>
        {t("actions.cancel")}
      </Button>
      <Button variant="primary" onPress={handleSave} isLoading={mutation.isPending}>
        {isEditing ? t("productProfiles.createVersionAction") : t("actions.save")}
      </Button>
    </div>
  );

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
      title={isEditing ? t("productProfiles.editTitle") : t("productProfiles.createTitle")}
      description={isEditing ? t("productProfiles.editDescription") : t("productProfiles.createDescription")}
      footer={footer}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMessage && (
          <div
            role="alert"
            style={{
              padding: "0.75rem",
              borderRadius: "var(--radius-md)",
              backgroundColor: "var(--color-status-danger-subtle, #fee2e2)",
              color: "var(--color-status-danger-text, #991b1b)",
              fontSize: "0.875rem"
            }}
          >
            {errorMessage}
          </div>
        )}

        {!isEditing && (
          <RadioGroup
            label={t("productProfiles.scope")}
            value={scope}
            onChange={(val) => setScope(val as "Template" | "Variant")}
          >
            <Radio value="Template" label={t("productProfiles.scopeTemplate")} />
            <Radio value="Variant" label={t("productProfiles.scopeVariant")} />
          </RadioGroup>
        )}

        <p style={{ margin: 0, fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
          {scope === "Template"
            ? t("productProfiles.templateGuidance")
            : t("productProfiles.variantGuidance")}
        </p>

        {isEditing ? (
          <TextField
            label={scope === "Variant" ? t("productProfiles.variantReference") : t("productProfiles.templateReference")}
            value={scope === "Variant" ? productVariantId : productTemplateId}
            isReadOnly
          />
        ) : (
          <ProductPicker
            label={scope === "Variant" ? t("productProfiles.variantReference") : t("productProfiles.templateReference")}
            value={productVariantId || undefined}
            onChange={(selection: ProductPickerSelection) => {
              setProductTemplateId(selection.templateId);
              setProductVariantId(selection.variantId);
            }}
            isRequired
            enableQuickCreate={false}
          />
        )}

        <Select
          label={t("productProfiles.taxCategory")}
          selectedKey={categoryId}
          onSelectionChange={(key) => setCategoryId(key as string)}
          options={productCategories.map((c) => ({
            id: c.id,
            label: `${c.name} (${c.code})`
          }))}
          isRequired
        />

        <LocalDateField
          label={t("productProfiles.effectiveFrom")}
          value={effectiveFrom}
          onChange={setEffectiveFrom}
          isRequired
        />

        {isEditing && (
          <div
            style={{
              padding: "0.75rem",
              borderRadius: "var(--radius-sm)",
              backgroundColor: "var(--color-status-info-subtle, #eff6ff)",
              color: "var(--color-status-info-text, #1e40af)",
              fontSize: "0.8125rem"
            }}
          >
            {t("productProfiles.versioningNotice")}
          </div>
        )}
      </div>
    </Dialog>
  );
}
