import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select, SelectItem } from "../../../components/primitives/select.tsx";
import { Checkbox } from "../../../components/primitives/checkbox.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { useAddBarcode, useCatalogUoms } from "../hooks/catalog-queries.ts";
import type { AddBarcodeRequest, BarcodeType, ProductDetails } from "../types.ts";

export interface BarcodeDialogProps {
  readonly isOpen: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly product: ProductDetails;
  readonly variantId?: string | undefined;
  readonly onAdded?: () => void;
}

export function BarcodeDialog({
  isOpen,
  onOpenChange,
  product,
  variantId,
  onAdded
}: BarcodeDialogProps): React.JSX.Element {
  const { t } = useTranslation("catalog");
  const [value, setValue] = useState("");
  const [barcodeType, setBarcodeType] = useState<BarcodeType>("Ean13");
  const [uomId, setUomId] = useState("");
  const [isPrimary, setIsPrimary] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const uoms = useCatalogUoms().data ?? [];
  const addBarcode = useAddBarcode();

  const resetForm = () => {
    setValue("");
    setBarcodeType("Ean13");
    setUomId("");
    setIsPrimary(false);
    setErrorMsg(null);
  };

  const handleClose = (open: boolean) => {
    if (!open) resetForm();
    onOpenChange(open);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!value.trim()) {
      setErrorMsg(t("barcodeDialog.errorValueRequired"));
      return;
    }

    const input: AddBarcodeRequest = {
      value: value.trim(),
      barcodeType,
      uomId: uomId || null,
      isPrimary
    };

    try {
      await addBarcode.mutateAsync({
        productId: product.id,
        variantId: variantId || product.defaultVariantId,
        input
      });
      handleClose(false);
      onAdded?.();
    } catch (err: unknown) {
      const problem = (err as { problemDetails?: { extensions?: { code?: string } } })?.problemDetails;
      const code = problem?.extensions?.code;
      if (code === "catalog.barcode_duplicate") {
        setErrorMsg(t("errors.barcodeExists"));
      } else {
        setErrorMsg(t("barcodeDialog.genericError"));
      }
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={handleClose}
      title={t("barcodeDialog.title")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMsg ? (
          <Alert variant="danger" title={t("common.error")}>{errorMsg}</Alert>
        ) : null}

        <TextField
          label={t("barcodeDialog.valueLabel")}
          value={value}
          onChange={setValue}
          placeholder={t("barcodeDialog.valuePlaceholder")}
          isRequired
          autoFocus
        />

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <Select
            label={t("barcodeDialog.typeLabel")}
            selectedKey={barcodeType}
            onSelectionChange={(key) => setBarcodeType(String(key) as BarcodeType)}
          >
            <SelectItem id="Ean13">{t("barcodeDialog.typeEan13")}</SelectItem>
            <SelectItem id="Ean8">{t("barcodeDialog.typeEan8")}</SelectItem>
            <SelectItem id="Upc">{t("barcodeDialog.typeUpc")}</SelectItem>
            <SelectItem id="Code128">{t("barcodeDialog.typeCode128")}</SelectItem>
            <SelectItem id="Other">{t("barcodeDialog.typeOther")}</SelectItem>
          </Select>

          <Select
            label={t("barcodeDialog.uomLabel")}
            selectedKey={uomId}
            onSelectionChange={(key) => setUomId(String(key))}
            placeholder={t("barcodeDialog.noUomPlaceholder")}
          >
            <SelectItem id="">{t("barcodeDialog.noUom")}</SelectItem>
            {uoms.map((u) => (
              <SelectItem key={u.id} id={u.id}>
                {u.code} — {u.nameAr}
              </SelectItem>
            ))}
          </Select>
        </div>

        <Checkbox isSelected={isPrimary} onChange={setIsPrimary}>
          {t("barcodeDialog.isPrimaryLabel")}
        </Checkbox>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" type="button" onPress={() => handleClose(false)}>
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isLoading={addBarcode.isPending}>
            {t("barcodeDialog.submit")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
