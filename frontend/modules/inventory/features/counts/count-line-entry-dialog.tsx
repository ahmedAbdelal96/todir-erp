import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { QuantityDisplay } from "../../../../components/fields/quantity-display.tsx";
import { InventoryProductDisplay } from "../../components/inventory-product-display.tsx";
import { toQty } from "../../utils.ts";
import type { PhysicalCountLine } from "../../types.ts";

interface CountLineEntryDialogProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly line: PhysicalCountLine;
  readonly isBlind: boolean;
  readonly isSaving: boolean;
  readonly onSave: (quantity: number) => Promise<void>;
}

export function CountLineEntryDialog({ isOpen, onClose, line, isBlind, isSaving, onSave }: CountLineEntryDialogProps): React.JSX.Element {
  const { t } = useTranslation("inventory");
  const [value, setValue] = useState(line.counted === null || line.counted === undefined ? "" : String(line.counted));
  const [error, setError] = useState<string | null>(null);

  const handleSave = async () => {
    const quantity = Number(value);
    if (!Number.isFinite(quantity) || quantity < 0) {
      setError(t("errors.quantityInvalid"));
      return;
    }
    setError(null);
    try {
      await onSave(quantity);
    } catch {
      setError(t("counts.linesLoadError"));
    }
  };

  return (
    <Dialog isOpen={isOpen} onOpenChange={(open) => !open && onClose()} title={t("counts.lineEntryTitle")} description={t("counts.lineEntryDescription")} size="small">
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div style={{ padding: "0.875rem", backgroundColor: "var(--color-surface-subtle, #f8fafc)", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)" }}>
          <span style={{ display: "block", marginBottom: "0.5rem", color: "var(--color-content-muted)", fontSize: "0.75rem" }}>{t("counts.lineProduct")}</span>
          <InventoryProductDisplay productVariantId={line.productVariantId} displayName={line.productDisplayName} sku={line.productSku} />
        </div>
        {!isBlind && <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: "0.75rem", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)" }}>
          <span>{t("counts.lineExpected")}</span>
          <QuantityDisplay value={toQty(line.expected, line.baseUomCode ?? "EA")} />
        </div>}
        <TextField type="number" label={t("counts.lineActual")} value={value} onChange={setValue} isRequired autoFocus inputMode="decimal" />
        {error && <div role="alert" style={{ color: "var(--color-status-danger)", fontSize: "0.8125rem" }}>{error}</div>}
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.625rem" }}>
          <Button variant="secondary" onPress={onClose} isDisabled={isSaving}>{t("common.cancel")}</Button>
          <Button variant="primary" onPress={() => void handleSave()} isLoading={isSaving}>{t("counts.saveCountResult")}</Button>
        </div>
      </div>
    </Dialog>
  );
}
