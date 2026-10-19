import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select, SelectItem } from "../../../components/primitives/select.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import {
  useCreateVariant,
  useProductAttributes,
  useCreateAttribute,
  useCreateAttributeValue,
  useAssignTemplateAttribute
} from "../hooks/catalog-queries.ts";
import type { ProductDetails, TemplateAttribute } from "../types.ts";

export interface VariantMatrixDialogProps {
  readonly isOpen: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly product: ProductDetails;
  readonly onCreated?: () => void;
}

export function VariantMatrixDialog({
  isOpen,
  onOpenChange,
  product,
  onCreated
}: VariantMatrixDialogProps): React.JSX.Element {
  const { t } = useTranslation("catalog");
  const attributesQuery = useProductAttributes(product.id);
  const attributes = attributesQuery.data ?? [];

  const [selectedValues, setSelectedValues] = useState<Record<string, string>>({});
  const [sku, setSku] = useState("");
  const [displaySuffix, setDisplaySuffix] = useState("");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  // New attribute creation state
  const [isAddingAttribute, setIsAddingAttribute] = useState(false);
  const [newAttrCode, setNewAttrCode] = useState("");
  const [newAttrNameAr, setNewAttrNameAr] = useState("");
  const [newAttrNameEn, setNewAttrNameEn] = useState("");

  const createVariant = useCreateVariant();
  const createAttr = useCreateAttribute();
  const createVal = useCreateAttributeValue();
  const assignAttr = useAssignTemplateAttribute();

  const resetForm = () => {
    setSelectedValues({});
    setSku("");
    setDisplaySuffix("");
    setErrorMsg(null);
    setIsAddingAttribute(false);
    setNewAttrCode("");
    setNewAttrNameAr("");
    setNewAttrNameEn("");
  };

  const handleClose = (open: boolean) => {
    if (!open) resetForm();
    onOpenChange(open);
  };

  const handleSelectAttributeValue = (attrId: string, valId: string) => {
    const updated = { ...selectedValues, [attrId]: valId };
    setSelectedValues(updated);

    // Auto-generate suffix from selected value names
    const suffixParts: string[] = [];
    for (const attr of attributes) {
      const vId = updated[attr.attribute.id];
      if (vId) {
        const foundVal = attr.values.find((v) => v.id === vId);
        if (foundVal) suffixParts.push(foundVal.nameAr || foundVal.code);
      }
    }
    if (suffixParts.length > 0) {
      setDisplaySuffix(suffixParts.join(" / "));
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(null);

    // Check if required attributes have values
    for (const attr of attributes) {
      if (attr.isRequired && !selectedValues[attr.attribute.id]) {
        setErrorMsg(t("variantDialog.errorAttributeRequired", { name: attr.attribute.nameAr || attr.attribute.code }));
        return;
      }
    }

    try {
      await createVariant.mutateAsync({
        productId: product.id,
        input: {
          sku: sku.trim() || undefined,
          displaySuffix: displaySuffix.trim() || undefined,
          attributeValues: selectedValues
        }
      });
      handleClose(false);
      onCreated?.();
    } catch (err: unknown) {
      const problem = (err as { problemDetails?: { extensions?: { code?: string } } })?.problemDetails;
      const code = problem?.extensions?.code;
      if (code === "catalog.variant_combination_exists") {
        setErrorMsg(t("errors.variantCombinationExists"));
      } else if (code === "catalog.sku_duplicate") {
        setErrorMsg(t("errors.skuExists"));
      } else {
        setErrorMsg(t("variantDialog.genericError"));
      }
    }
  };

  const handleCreateNewAttribute = async () => {
    if (!newAttrCode.trim() || !newAttrNameAr.trim() || !newAttrNameEn.trim()) return;
    try {
      const attr = await createAttr.mutateAsync({
        code: newAttrCode.trim().toUpperCase(),
        nameAr: newAttrNameAr.trim(),
        nameEn: newAttrNameEn.trim(),
        isVariantDefining: true
      });
      await assignAttr.mutateAsync({
        productId: product.id,
        attributeId: attr.id,
        input: { isRequired: true, displayOrder: attributes.length }
      });
      setIsAddingAttribute(false);
      setNewAttrCode("");
      setNewAttrNameAr("");
      setNewAttrNameEn("");
      void attributesQuery.refetch();
    } catch {
      setErrorMsg(t("variantDialog.createAttributeFailed"));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={handleClose}
      title={t("variantDialog.title")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMsg ? (
          <Alert variant="danger" title={t("common.error")}>{errorMsg}</Alert>
        ) : null}

        <p style={{ color: "var(--color-content-secondary)", fontSize: "0.875rem", margin: 0 }}>
          {t("variantDialog.description", { name: product.displayName })}
        </p>

        {attributes.length === 0 ? (
          <div style={{ padding: "1rem", background: "var(--color-surface-subtle)", borderRadius: "var(--radius-sm)", textAlign: "center" }}>
            <p style={{ margin: "0 0 0.75rem 0", fontSize: "0.875rem" }}>
              {t("variantDialog.noAttributesAssigned")}
            </p>
            {!isAddingAttribute ? (
              <Button variant="secondary" size="compact" type="button" onPress={() => setIsAddingAttribute(true)}>
                + {t("variantDialog.addAttributeAction")}
              </Button>
            ) : null}
          </div>
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
            {attributes.map((tmplAttr) => (
              <div key={tmplAttr.attribute.id} style={{ display: "flex", flexDirection: "column", gap: "0.25rem" }}>
                <label style={{ fontSize: "0.8125rem", fontWeight: 600 }}>
                  {tmplAttr.attribute.nameAr} ({tmplAttr.attribute.nameEn}){tmplAttr.isRequired ? " *" : ""}
                </label>
                <Select
                  selectedKey={selectedValues[tmplAttr.attribute.id] ?? ""}
                  onSelectionChange={(key) => handleSelectAttributeValue(tmplAttr.attribute.id, String(key))}
                  placeholder={t("variantDialog.selectValuePlaceholder")}
                  isRequired={tmplAttr.isRequired}
                >
                  {tmplAttr.values.map((val) => (
                    <SelectItem key={val.id} id={val.id}>
                      {val.nameAr} ({val.code})
                    </SelectItem>
                  ))}
                </Select>
              </div>
            ))}
          </div>
        )}

        {isAddingAttribute ? (
          <div style={{ border: "1px dashed var(--color-border-default)", padding: "0.75rem", borderRadius: "var(--radius-sm)", display: "flex", flexDirection: "column", gap: "0.5rem" }}>
            <span style={{ fontSize: "0.8125rem", fontWeight: 600 }}>{t("variantDialog.newAttributeTitle")}</span>
            <TextField
              label={t("variantDialog.attributeCode")}
              value={newAttrCode}
              onChange={setNewAttrCode}
              placeholder={t("variantDialog.attributeCodePlaceholder")}
            />
            <TextField
              label={t("variantDialog.attributeNameAr")}
              value={newAttrNameAr}
              onChange={setNewAttrNameAr}
              placeholder={t("variantDialog.attributeNameArPlaceholder")}
            />
            <TextField
              label={t("variantDialog.attributeNameEn")}
              value={newAttrNameEn}
              onChange={setNewAttrNameEn}
              placeholder={t("variantDialog.attributeNameEnPlaceholder")}
            />
            <div style={{ display: "flex", gap: "0.5rem", justifyContent: "flex-end" }}>
              <Button variant="ghost" size="compact" onPress={() => setIsAddingAttribute(false)}>
                {t("common.cancel")}
              </Button>
              <Button variant="secondary" size="compact" onPress={handleCreateNewAttribute}>
                {t("variantDialog.saveAttribute")}
              </Button>
            </div>
          </div>
        ) : null}

        <TextField
          label={t("variantDialog.displaySuffixLabel")}
          value={displaySuffix}
          onChange={setDisplaySuffix}
          placeholder={t("variantDialog.displaySuffixPlaceholder")}
          description={t("variantDialog.displaySuffixHint")}
        />

        <TextField
          label={t("variantDialog.skuLabel")}
          value={sku}
          onChange={setSku}
          placeholder={t("variantDialog.skuPlaceholder")}
          description={t("variantDialog.skuHint")}
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" type="button" onPress={() => handleClose(false)}>
            {t("common.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isLoading={createVariant.isPending}
            isDisabled={attributes.length === 0}
          >
            {t("variantDialog.submit")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
