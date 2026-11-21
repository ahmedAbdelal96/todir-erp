import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
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
            onChange={(e) => setCode(e.target.value)}
            required
          />

          <TextField
            label={t("definitions.bomName")}
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("orders.product")}
            value={productVariantId}
            onChange={(e) => setProductVariantId(e.target.value)}
            required
          />

          <TextField
            label={t("definitions.baseOutputQty")}
            type="number"
            value={baseOutputQty}
            onChange={(e) => setBaseOutputQty(e.target.value)}
            min="0.0001"
            step="any"
            required
          />

          <TextField
            label={t("orders.uom")}
            value={outputUomCode}
            onChange={(e) => setOutputUomCode(e.target.value)}
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
                <TextField
                  label={idx === 0 ? t("definitions.componentId") : undefined}
                  value={line.componentProductVariantId}
                  onChange={(e) => updateLine(idx, { componentProductVariantId: e.target.value })}
                />
                <TextField
                  label={idx === 0 ? t("common.quantity") : undefined}
                  type="number"
                  value={String(line.requiredQty)}
                  onChange={(e) => updateLine(idx, { requiredQty: parseFloat(e.target.value) || 0 })}
                  min="0.0001"
                  step="any"
                />
                <TextField
                  label={idx === 0 ? t("common.unit") : undefined}
                  value={line.uomCode}
                  onChange={(e) => updateLine(idx, { uomCode: e.target.value })}
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
