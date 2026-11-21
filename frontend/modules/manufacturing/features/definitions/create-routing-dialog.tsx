import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { useCreateRoutingMutation, useCreateRoutingVersionMutation, useWorkCentersQuery } from "../../hooks/use-manufacturing.ts";
import type { RoutingOperationInput } from "../../types.ts";

export interface CreateRoutingDialogProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
}

export function CreateRoutingDialog({ isOpen, onClose }: CreateRoutingDialogProps): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const createRoutingMutation = useCreateRoutingMutation();
  const createVersionMutation = useCreateRoutingVersionMutation();
  const workCentersQuery = useWorkCentersQuery();

  const [productVariantId, setProductVariantId] = useState("");
  const [code, setCode] = useState("");
  const [name, setName] = useState("");

  const [operations, setOperations] = useState<RoutingOperationInput[]>([
    { sequenceNo: 10, code: "OP-010", name: "إعداد وتجهيز المواد", setupMinutes: 15, runMinutesPerUnit: 2, qualityGateRequired: false }
  ]);

  const addOperation = () => {
    const nextSeq = operations.length > 0 ? Math.max(...operations.map((o) => o.sequenceNo)) + 10 : 10;
    setOperations([
      ...operations,
      {
        sequenceNo: nextSeq,
        code: `OP-${String(nextSeq).padStart(3, "0")}`,
        name: "",
        setupMinutes: 0,
        runMinutesPerUnit: 1,
        qualityGateRequired: false
      }
    ]);
  };

  const removeOperation = (index: number) => {
    setOperations(operations.filter((_, i) => i !== index));
  };

  const updateOperation = (index: number, patch: Partial<RoutingOperationInput>) => {
    setOperations(operations.map((op, i) => (i === index ? { ...op, ...patch } : op)));
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!code.trim() || !name.trim()) return;

    createRoutingMutation.mutate(
      {
        productVariantId: productVariantId.trim() || undefined,
        code: code.trim().toUpperCase(),
        name: name.trim()
      },
      {
        onSuccess: (routing) => {
          if (operations.length > 0) {
            createVersionMutation.mutate({
              routingId: routing.id,
              request: {
                validFrom: new Date().toISOString().slice(0, 10),
                operations: operations.map((op) => ({
                  ...op,
                  code: op.code.trim().toUpperCase(),
                  name: op.name.trim() || op.code.trim(),
                  workCenterId: op.workCenterId || undefined
                }))
              }
            });
          }
          onClose();
        }
      }
    );
  };

  const isLoading = createRoutingMutation.isPending || createVersionMutation.isPending;

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("definitions.addRouting")}
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "12px" }}>
          <TextField
            label={t("definitions.routingCode")}
            value={code}
            onChange={(e) => setCode(e.target.value)}
            required
          />
          <TextField
            label={t("definitions.routingName")}
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
          />
        </div>

        <TextField
          label={t("orders.product")}
          value={productVariantId}
          onChange={(e) => setProductVariantId(e.target.value)}
        />

        <div style={{ borderTop: "1px solid #cbd5e1", paddingTop: "12px" }}>
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "8px" }}>
            <span style={{ fontSize: "14px", fontWeight: 700, color: "#0f172a" }}>
              {t("definitions.routingOperations")}
            </span>
            <Button type="button" variant="secondary" onClick={addOperation}>
              + {t("definitions.addOperation")}
            </Button>
          </div>

          <div style={{ display: "flex", flexDirection: "column", gap: "8px", maxHeight: "240px", overflowY: "auto" }}>
            {operations.map((op, idx) => (
              <div
                key={idx}
                style={{
                  display: "grid",
                  gridTemplateColumns: "70px 100px 1fr 90px 90px 40px",
                  gap: "8px",
                  alignItems: "center",
                  background: "#f8fafc",
                  padding: "8px",
                  borderRadius: "6px",
                  border: "1px solid #e2e8f0"
                }}
              >
                <TextField
                  label={t("shopFloor.sequence")}
                  type="number"
                  value={String(op.sequenceNo)}
                  onChange={(e) => updateOperation(idx, { sequenceNo: parseInt(e.target.value, 10) || 10 })}
                />
                <TextField
                  label={t("common.code")}
                  value={op.code}
                  onChange={(e) => updateOperation(idx, { code: e.target.value })}
                />
                <TextField
                  label={t("shopFloor.operationName")}
                  value={op.name}
                  onChange={(e) => updateOperation(idx, { name: e.target.value })}
                />
                <TextField
                  label={t("common.setupMinutes")}
                  type="number"
                  value={String(op.setupMinutes ?? 0)}
                  onChange={(e) => updateOperation(idx, { setupMinutes: parseInt(e.target.value, 10) || 0 })}
                />
                <TextField
                  label={t("common.runMinutes")}
                  type="number"
                  value={String(op.runMinutesPerUnit ?? 1)}
                  onChange={(e) => updateOperation(idx, { runMinutesPerUnit: parseFloat(e.target.value) || 1 })}
                />
                <button
                  type="button"
                  onClick={() => removeOperation(idx)}
                  disabled={operations.length === 1}
                  style={{
                    background: "none",
                    border: "none",
                    color: "#dc2626",
                    cursor: operations.length === 1 ? "not-allowed" : "pointer",
                    fontWeight: 700,
                    marginTop: "16px"
                  }}
                >
                  ✕
                </button>
              </div>
            ))}
          </div>
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "8px", marginTop: "12px" }}>
          <Button type="button" variant="secondary" onClick={onClose} disabled={isLoading}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" variant="primary" disabled={isLoading || !code || !name}>
            {isLoading ? t("common.loading") : t("common.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
