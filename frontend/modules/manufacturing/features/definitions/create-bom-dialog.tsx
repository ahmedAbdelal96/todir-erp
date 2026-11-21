import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { ProductPicker, type ProductPickerSelection } from "../../../catalog/public.ts";
import { useCreateBomMutation, useCreateBomVersionMutation } from "../../hooks/use-manufacturing.ts";
import type { BomLineInput } from "../../types.ts";

export interface CreateBomDialogProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
}

export function CreateBomDialog({ isOpen, onClose }: CreateBomDialogProps): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const createBomMutation = useCreateBomMutation();
  const createVersionMutation = useCreateBomVersionMutation();

  const [productVariantId, setProductVariantId] = useState("");
  const [code, setCode] = useState("");
  const [name, setName] = useState("");

  // Initial version fields
  const [baseOutputQty, setBaseOutputQty] = useState("1");
  const [outputUomCode, setOutputUomCode] = useState("EA");
  const [lines, setLines] = useState<BomLineInput[]>([
    { componentProductVariantId: "", requiredQty: 1, uomCode: "EA", scrapFactor: 0, consumptionMethod: "Manual", phantom: false }
  ]);

  const addLine = () => {
    setLines([...lines, { componentProductVariantId: "", requiredQty: 1, uomCode: "EA", scrapFactor: 0, consumptionMethod: "Manual", phantom: false }]);
  };

  const removeLine = (index: number) => {
    setLines(lines.filter((_, i) => i !== index));
  };

  const updateLine = (index: number, patch: Partial<BomLineInput>) => {
    setLines(lines.map((line, i) => (i === index ? { ...line, ...patch } : line)));
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!productVariantId.trim() || !code.trim() || !name.trim()) return;

    createBomMutation.mutate(
      {
        productVariantId: productVariantId.trim(),
        code: code.trim().toUpperCase(),
        name: name.trim()
      },
      {
        onSuccess: (bom) => {
          const validLines = lines.filter((l) => l.componentProductVariantId.trim());
          if (validLines.length > 0) {
            createVersionMutation.mutate({
              bomId: bom.id,
              request: {
                validFrom: new Date().toISOString().slice(0, 10),
                baseOutputQty: parseFloat(baseOutputQty) || 1,
                outputUomCode: outputUomCode.trim().toUpperCase(),
                lines: validLines.map((l) => ({
                  ...l,
                  componentProductVariantId: l.componentProductVariantId.trim(),
                  uomCode: l.uomCode.trim().toUpperCase()
                }))
              }
            });
          }
          onClose();
        }
      }
    );
  };

  const isPending = createBomMutation.isPending || createVersionMutation.isPending;

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("definitions.addBom")}
      description={t("definitions.bomDescription")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {createBomMutation.isError ? (
          <div role="alert" style={{ padding: "0.75rem", background: "#fef2f2", border: "1px solid #ef4444", borderRadius: "var(--radius-sm)", color: "#b91c1c", fontSize: "0.875rem" }}>
            {t("errors.generalFailure")}
          </div>
        ) : null}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("definitions.bomCode")}
            value={code}
            onChange={(val) => setCode(val)}
            required
          />

          <TextField
            label={t("definitions.bomName")}
            value={name}
            onChange={(val) => setName(val)}
            required
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr 1fr", gap: "0.75rem" }}>
          <ProductPicker
            label={t("orders.product")}
            value={productVariantId}
            isRequired
            onChange={(sel: ProductPickerSelection) => {
              setProductVariantId(sel.variantId);
              if (sel.baseUomCode && (!outputUomCode || outputUomCode === "EA")) {
                setOutputUomCode(sel.baseUomCode);
              }
            }}
          />

          <TextField
            label={t("definitions.baseOutputQty")}
            type="number"
            value={baseOutputQty}
            onChange={(val) => setBaseOutputQty(val)}
            min="0.0001"
            step="any"
            required
          />

          <TextField
            label={t("orders.uom")}
            value={outputUomCode}
            onChange={(val) => setOutputUomCode(val)}
            required
          />
        </div>

        {/* Lines */}
        <section>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "0.5rem" }}>
            <h4 style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-brand-primary)" }}>{t("definitions.linesTitle")}</h4>
            <Button type="button" size="small" variant="outline" onPress={addLine}>
              + {t("definitions.addLine")}
            </Button>
          </div>

          <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
            {lines.map((line, idx) => (
              <div key={idx} style={{ display: "grid", gridTemplateColumns: "2fr 1fr 1fr auto", gap: "0.5rem", alignItems: "end" }}>
                <ProductPicker
                  label={idx === 0 ? t("definitions.componentId") : undefined}
                  value={line.componentProductVariantId}
                  isRequired
                  onChange={(sel: ProductPickerSelection) => {
                    updateLine(idx, {
                      componentProductVariantId: sel.variantId,
                      uomCode: sel.baseUomCode || line.uomCode
                    });
                  }}
                />
                <TextField
                  label={idx === 0 ? t("common.quantity") : undefined}
                  type="number"
                  value={String(line.requiredQty)}
                  onChange={(val) => updateLine(idx, { requiredQty: parseFloat(val) || 0 })}
                  min="0.0001"
                  step="any"
                />
                <TextField
                  label={idx === 0 ? t("common.unit") : undefined}
                  value={line.uomCode}
                  onChange={(val) => updateLine(idx, { uomCode: val })}
                />
                <Button
                  type="button"
                  size="small"
                  variant="destructive"
                  onPress={() => removeLine(idx)}
                  isDisabled={lines.length === 1}
                >
                  ×
                </Button>
              </div>
            ))}
          </div>
        </section>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button type="button" variant="outline" onPress={onClose}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" variant="primary" isDisabled={isPending || !code.trim() || !name.trim() || !productVariantId.trim()}>
            {isPending ? t("common.loading") : t("definitions.addBom")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
