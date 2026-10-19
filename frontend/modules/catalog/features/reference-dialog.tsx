import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select, SelectItem } from "../../../components/primitives/select.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { useAddProductReference } from "../hooks/catalog-queries.ts";
import type { AddReferenceRequest, ProductDetails, ReferenceType } from "../types.ts";

export interface ReferenceDialogProps {
  readonly isOpen: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly product: ProductDetails;
  readonly onAdded?: () => void;
}

export function ReferenceDialog({
  isOpen,
  onOpenChange,
  product,
  onAdded
}: ReferenceDialogProps): React.JSX.Element {
  const { t } = useTranslation("catalog");
  const [referenceType, setReferenceType] = useState<ReferenceType>("Customer");
  const [referenceCode, setReferenceCode] = useState("");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const addReference = useAddProductReference();

  const resetForm = () => {
    setReferenceType("Customer");
    setReferenceCode("");
    setErrorMsg(null);
  };

  const handleClose = (open: boolean) => {
    if (!open) resetForm();
    onOpenChange(open);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!referenceCode.trim()) {
      setErrorMsg(t("referenceDialog.errorCodeRequired"));
      return;
    }

    const input: AddReferenceRequest = {
      variantId: product.defaultVariantId,
      referenceType,
      referenceCode: referenceCode.trim(),
      partyId: null
    };

    try {
      await addReference.mutateAsync({
        productId: product.id,
        input
      });
      handleClose(false);
      onAdded?.();
    } catch {
      setErrorMsg(t("referenceDialog.genericError"));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={handleClose}
      title={t("referenceDialog.title")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMsg ? (
          <Alert variant="danger" title={t("common.error")}>{errorMsg}</Alert>
        ) : null}

        <p style={{ color: "var(--color-content-secondary)", fontSize: "0.875rem", margin: 0 }}>
          {t("referenceDialog.description")}
        </p>

        <Select
          label={t("referenceDialog.typeLabel")}
          selectedKey={referenceType}
          onSelectionChange={(key) => setReferenceType(String(key) as ReferenceType)}
        >
          <SelectItem id="Customer">{t("referenceDialog.typeCustomer")}</SelectItem>
          <SelectItem id="Supplier">{t("referenceDialog.typeSupplier")}</SelectItem>
        </Select>

        <TextField
          label={t("referenceDialog.codeLabel")}
          value={referenceCode}
          onChange={setReferenceCode}
          placeholder={t("referenceDialog.codePlaceholder")}
          description={t("referenceDialog.codeHint")}
          isRequired
          autoFocus
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" type="button" onPress={() => handleClose(false)}>
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isLoading={addReference.isPending}>
            {t("referenceDialog.submit")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
