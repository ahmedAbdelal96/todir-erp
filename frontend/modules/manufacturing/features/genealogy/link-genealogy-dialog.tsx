import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { useLinkGenealogyMutation, useOrdersQuery } from "../../hooks/use-manufacturing.ts";

export interface LinkGenealogyDialogProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly orderId?: string | undefined;
  readonly legalEntityId?: string | undefined;
}

export function LinkGenealogyDialog({ isOpen, onClose, orderId: propOrderId, legalEntityId }: LinkGenealogyDialogProps): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const linkMutation = useLinkGenealogyMutation();
  const { data: orders = [] } = useOrdersQuery(legalEntityId);

  const [selectedOrderId, setSelectedOrderId] = useState(propOrderId ?? "");
  const effectiveOrderId = propOrderId || selectedOrderId || orders[0]?.id || "";

  const [outputId, setOutputId] = useState("");
  const [inputEventId, setInputEventId] = useState("");
  const [inputQuantity, setInputQuantity] = useState("10");
  const [outputQuantity, setOutputQuantity] = useState("10");
  const [inputLotId, setInputLotId] = useState("");
  const [outputLotId, setOutputLotId] = useState("");
  const [certainty, setCertainty] = useState<"Exact" | "Possible" | "Commingled">("Exact");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!effectiveOrderId || !outputId.trim() || !inputEventId.trim()) return;

    linkMutation.mutate(
      {
        orderId: effectiveOrderId,
        request: {
          outputId: outputId.trim(),
          inputEventId: inputEventId.trim(),
          inputQuantity: parseFloat(inputQuantity) || 1,
          outputQuantity: parseFloat(outputQuantity) || 1,
          inputLotId: inputLotId.trim() || undefined,
          outputLotId: outputLotId.trim() || undefined,
          certainty,
          idempotencyKey: `gen-link-${effectiveOrderId}-${Date.now()}`
        }
      },
      {
        onSuccess: () => {
          onClose();
        }
      }
    );
  };

  const isLoading = linkMutation.isPending;

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("genealogy.linkGenealogy")}
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
        {!propOrderId && orders.length > 0 && (
          <div style={{ display: "flex", flexDirection: "column", gap: "6px" }}>
            <label style={{ fontSize: "13px", fontWeight: 600, color: "#1e293b" }}>
              {t("common.order")}
            </label>
            <select
              value={effectiveOrderId}
              onChange={(e) => setSelectedOrderId(e.target.value)}
              style={{
                padding: "8px 12px",
                borderRadius: "6px",
                border: "1px solid #cbd5e1",
                fontSize: "14px",
                background: "#ffffff"
              }}
              required
            >
              {orders.map((o) => (
                <option key={o.id} value={o.id}>
                  {o.orderNumber ?? o.number} ({o.orderedQty} {o.uomCode})
                </option>
              ))}
            </select>
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "12px" }}>
          <TextField
            label={t("genealogy.outputId")}
            value={outputId}
            onChange={(val) => setOutputId(val)}
            required
          />
          <TextField
            label={t("genealogy.inputEventId")}
            value={inputEventId}
            onChange={(val) => setInputEventId(val)}
            required
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "12px" }}>
          <TextField
            label={t("genealogy.outputLot")}
            value={outputLotId}
            onChange={(val) => setOutputLotId(val)}
          />
          <TextField
            label={t("genealogy.inputLot")}
            value={inputLotId}
            onChange={(val) => setInputLotId(val)}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "12px" }}>
          <TextField
            label={t("genealogy.consumedQty")}
            type="number"
            value={inputQuantity}
            onChange={(val) => setInputQuantity(val)}
            required
          />
          <TextField
            label={t("genealogy.producedQty")}
            type="number"
            value={outputQuantity}
            onChange={(val) => setOutputQuantity(val)}
            required
          />
        </div>

        <div style={{ display: "flex", flexDirection: "column", gap: "6px" }}>
          <label style={{ fontSize: "13px", fontWeight: 600, color: "#1e293b" }}>
            {t("genealogy.certainty")}
          </label>
          <select
            value={certainty}
            onChange={(e) => setCertainty(e.target.value as any)}
            style={{
              padding: "8px 12px",
              borderRadius: "6px",
              border: "1px solid #cbd5e1",
              fontSize: "14px",
              background: "#ffffff"
            }}
          >
            <option value="Exact">{t("genealogy.exactAttribution")}</option>
            <option value="Possible">{t("genealogy.possibleAttribution")}</option>
            <option value="Commingled">{t("genealogy.commingled")}</option>
          </select>
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "8px", marginTop: "12px" }}>
          <Button type="button" variant="secondary" onClick={onClose} disabled={isLoading}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" variant="primary" disabled={isLoading || !outputId || !inputEventId}>
            {isLoading ? t("common.loading") : t("common.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
