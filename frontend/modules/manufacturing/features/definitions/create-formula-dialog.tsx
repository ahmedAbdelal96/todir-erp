import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { useCreateFormulaMutation, useCreateFormulaVersionMutation } from "../../hooks/use-manufacturing.ts";
import type { FormulaIngredientInput, FormulaOutputInput, OutputType } from "../../types.ts";

export interface CreateFormulaDialogProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
}

export function CreateFormulaDialog({ isOpen, onClose }: CreateFormulaDialogProps): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const createFormulaMutation = useCreateFormulaMutation();
  const createVersionMutation = useCreateFormulaVersionMutation();

  const [productVariantId, setProductVariantId] = useState("");
  const [code, setCode] = useState("");
  const [name, setName] = useState("");

  // Version fields
  const [baseBatchQty, setBaseBatchQty] = useState("100");
  const [outputUomCode, setOutputUomCode] = useState("L");
  const [expectedYield, setExpectedYield] = useState("0.98");

  const [ingredients, setIngredients] = useState<FormulaIngredientInput[]>([
    { productVariantId: "", quantity: 50, uomCode: "KG", fixedQuantity: false }
  ]);

  const [outputs, setOutputs] = useState<FormulaOutputInput[]>([
    { productVariantId: "", outputType: "Primary", quantity: 100, uomCode: "L" }
  ]);

  const addIngredient = () => {
    setIngredients([...ingredients, { productVariantId: "", quantity: 10, uomCode: "KG", fixedQuantity: false }]);
  };

  const removeIngredient = (index: number) => {
    setIngredients(ingredients.filter((_, i) => i !== index));
  };

  const updateIngredient = (index: number, patch: Partial<FormulaIngredientInput>) => {
    setIngredients(ingredients.map((ing, i) => (i === index ? { ...ing, ...patch } : ing)));
  };

  const addOutput = () => {
    setOutputs([...outputs, { productVariantId: "", outputType: "CoProduct", quantity: 5, uomCode: "L" }]);
  };

  const removeOutput = (index: number) => {
    setOutputs(outputs.filter((_, i) => i !== index));
  };

  const updateOutput = (index: number, patch: Partial<FormulaOutputInput>) => {
    setOutputs(outputs.map((out, i) => (i === index ? { ...out, ...patch } : out)));
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!productVariantId.trim() || !code.trim() || !name.trim()) return;

    createFormulaMutation.mutate(
      {
        productVariantId: productVariantId.trim(),
        code: code.trim().toUpperCase(),
        name: name.trim()
      },
      {
        onSuccess: (formula) => {
          const validIngredients = ingredients.filter((i) => i.productVariantId.trim());
          const validOutputs = outputs.filter((o) => o.productVariantId.trim());

          if (validIngredients.length > 0) {
            createVersionMutation.mutate({
              formulaId: formula.id,
              request: {
                validFrom: new Date().toISOString().slice(0, 10),
                baseBatchQty: parseFloat(baseBatchQty) || 100,
                outputUomCode: outputUomCode.trim().toUpperCase(),
                expectedYield: parseFloat(expectedYield) || 1.0,
                ingredients: validIngredients.map((i) => ({
                  ...i,
                  productVariantId: i.productVariantId.trim(),
                  uomCode: i.uomCode.trim().toUpperCase()
                })),
                outputs: validOutputs.map((o) => ({
                  ...o,
                  productVariantId: o.productVariantId.trim(),
                  uomCode: o.uomCode.trim().toUpperCase()
                }))
              }
            });
          }
          onClose();
        }
      }
    );
  };

  const isPending = createFormulaMutation.isPending || createVersionMutation.isPending;

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("definitions.addFormula")}
      description={t("definitions.formulaDescription")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("definitions.formulaCode")}
            value={code}
            onChange={(e) => setCode(e.target.value)}
            required
          />

          <TextField
            label={t("definitions.formulaName")}
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr 1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("orders.product")}
            value={productVariantId}
            onChange={(e) => setProductVariantId(e.target.value)}
            required
          />

          <TextField
            label={t("definitions.baseOutputQty")}
            type="number"
            value={baseBatchQty}
            onChange={(e) => setBaseBatchQty(e.target.value)}
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

          <TextField
            label={t("definitions.expectedYield")}
            type="number"
            value={expectedYield}
            onChange={(e) => setExpectedYield(e.target.value)}
            min="0.01"
            max="1"
            step="0.01"
            required
          />
        </div>

        {/* Ingredients */}
        <section>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "0.5rem" }}>
            <h4 style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-brand-primary)" }}>{t("definitions.ingredientsTitle")}</h4>
            <Button type="button" size="small" variant="outline" onPress={addIngredient}>
              + {t("definitions.addIngredient")}
            </Button>
          </div>
          {ingredients.map((ing, idx) => (
            <div key={idx} style={{ display: "grid", gridTemplateColumns: "2fr 1fr 1fr auto", gap: "0.5rem", marginBottom: "0.5rem" }}>
              <TextField
                value={ing.productVariantId}
                onChange={(e) => updateIngredient(idx, { productVariantId: e.target.value })}
              />
              <TextField
                type="number"
                value={String(ing.quantity)}
                onChange={(e) => updateIngredient(idx, { quantity: parseFloat(e.target.value) || 0 })}
                min="0.0001"
                step="any"
              />
              <TextField
                value={ing.uomCode}
                onChange={(e) => updateIngredient(idx, { uomCode: e.target.value })}
              />
              <Button type="button" size="small" variant="destructive" onPress={() => removeIngredient(idx)} isDisabled={ingredients.length === 1}>
                ×
              </Button>
            </div>
          ))}
        </section>

        {/* Outputs */}
        <section>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "0.5rem" }}>
            <h4 style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-brand-primary)" }}>{t("definitions.outputsTitle")}</h4>
            <Button type="button" size="small" variant="outline" onPress={addOutput}>
              + {t("definitions.addOutput")}
            </Button>
          </div>
          {outputs.map((out, idx) => (
            <div key={idx} style={{ display: "grid", gridTemplateColumns: "2fr 1.5fr 1fr 1fr auto", gap: "0.5rem", marginBottom: "0.5rem" }}>
              <TextField
                value={out.productVariantId}
                onChange={(e) => updateOutput(idx, { productVariantId: e.target.value })}
              />
              <select
                value={out.outputType}
                onChange={(e) => updateOutput(idx, { outputType: e.target.value as OutputType })}
                style={{ padding: "0.5rem", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-default)", background: "var(--color-surface-card)" }}
              >
                <option value="Primary">{t("definitions.primaryOutput")}</option>
                <option value="CoProduct">{t("definitions.coProduct")}</option>
                <option value="ByProduct">{t("definitions.byProduct")}</option>
                <option value="Waste">{t("definitions.waste")}</option>
              </select>
              <TextField
                type="number"
                value={String(out.quantity)}
                onChange={(e) => updateOutput(idx, { quantity: parseFloat(e.target.value) || 0 })}
                min="0.0001"
                step="any"
              />
              <TextField
                value={out.uomCode}
                onChange={(e) => updateOutput(idx, { uomCode: e.target.value })}
              />
              <Button type="button" size="small" variant="destructive" onPress={() => removeOutput(idx)} isDisabled={outputs.length === 1}>
                ×
              </Button>
            </div>
          ))}
        </section>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button type="button" variant="outline" onPress={onClose}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" variant="primary" isDisabled={isPending || !code.trim() || !name.trim() || !productVariantId.trim()}>
            {isPending ? t("common.loading") : t("definitions.addFormula")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
