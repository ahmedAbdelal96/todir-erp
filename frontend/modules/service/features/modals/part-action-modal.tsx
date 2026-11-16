import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { ServiceSelect as Select } from "../../components/service-select.tsx";
import {
  useReservePartMutation,
  useIssuePartMutation,
  useConsumePartMutation,
  useReturnPartMutation
} from "../../hooks/use-service-queries.ts";
import type { WorkOrderPartDto } from "../../types.ts";

export type PartActionType = "reserve" | "issue" | "consume" | "return";

export interface PartActionModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly part: WorkOrderPartDto | null;
  readonly initialAction?: PartActionType;
}

export function PartActionModal({
  isOpen,
  onClose,
  legalEntityId,
  part,
  initialAction = "reserve"
}: PartActionModalProps): React.JSX.Element {
  const { t } = useTranslation("service");
  const reserveMutation = useReservePartMutation(legalEntityId);
  const issueMutation = useIssuePartMutation(legalEntityId);
  const consumeMutation = useConsumePartMutation(legalEntityId);
  const returnMutation = useReturnPartMutation(legalEntityId);

  const [action, setAction] = useState<PartActionType>(initialAction);
  const [quantity, setQuantity] = useState("1");
  const [location, setLocation] = useState("MAIN-WH");
  const [lotNumber, setLotNumber] = useState("");
  const [serialNumber, setSerialNumber] = useState("");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  if (!part) return <React.Fragment />;

  const isPending =
    reserveMutation.isPending ||
    issueMutation.isPending ||
    consumeMutation.isPending ||
    returnMutation.isPending;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const qty = Number(quantity);
    if (isNaN(qty) || qty <= 0) {
      setErrorMsg(t("validation.invalidQuantity"));
      return;
    }

    try {
      setErrorMsg(null);
      if (action === "reserve") {
        await reserveMutation.mutateAsync({
          legalEntityId,
          partId: part.id,
          quantity: qty,
          warehouseLocation: location.trim() || undefined
        });
      } else if (action === "issue") {
        await issueMutation.mutateAsync({
          legalEntityId,
          partId: part.id,
          quantity: qty,
          warehouseLocation: location.trim() || undefined,
          lotNumber: lotNumber.trim() || undefined,
          serialNumber: serialNumber.trim() || undefined
        });
      } else if (action === "consume") {
        await consumeMutation.mutateAsync({
          legalEntityId,
          partId: part.id,
          quantity: qty,
          lotNumber: lotNumber.trim() || undefined,
          serialNumber: serialNumber.trim() || undefined
        });
      } else if (action === "return") {
        await returnMutation.mutateAsync({
          legalEntityId,
          partId: part.id,
          quantity: qty,
          warehouseLocation: location.trim() || undefined
        });
      }
      onClose();
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : t("common.genericError"));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={`${t("parts.partActionTitle")} - ${part.itemId}`}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMsg && (
          <div
            style={{
              padding: "0.75rem 1rem",
              borderRadius: "var(--radius-sm, 0.375rem)",
              backgroundColor: "#fef2f2",
              border: "1px solid #fecaca",
              color: "#b91c1c",
              fontSize: "0.875rem"
            }}
          >
            {errorMsg}
          </div>
        )}

        {/* Part Summary Card */}
        <div
          style={{
            padding: "0.75rem 1rem",
            borderRadius: "var(--radius-md, 0.5rem)",
            backgroundColor: "var(--color-surface-subtle, #f8fafc)",
            border: "1px solid var(--color-border-subtle, #e2e8f0)",
            fontSize: "0.8125rem"
          }}
        >
          <div>
            <strong>{part.itemId}</strong> {part.description ? `(${part.description})` : ""}
          </div>
          <div style={{ marginTop: "0.25rem", color: "var(--color-content-secondary, #475569)" }}>
            {t("parts.required")}: {part.requiredQuantity} {part.uom} | {t("parts.reserved")}: {part.reservedQuantity} |{" "}
            {t("parts.issued")}: {part.issuedQuantity} | {t("parts.consumed")}: {part.consumedQuantity}
          </div>
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
            {t("parts.action")} *
          </label>
          <Select
            value={action}
            onChange={(val) => setAction(val as PartActionType)}
            options={[
              { value: "reserve", label: t("parts.actions.reserve") },
              { value: "issue", label: t("parts.actions.issue") },
              { value: "consume", label: t("parts.actions.consume") },
              { value: "return", label: t("parts.actions.return") }
            ]}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("parts.quantity")} *
            </label>
            <TextField
              value={quantity}
              onChange={(val) => setQuantity(val)}
              placeholder="1"
              isRequired
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("parts.warehouseLocation")}
            </label>
            <TextField
              value={location}
              onChange={(val) => setLocation(val)}
              placeholder={t("parts.binPlaceholder", "MAIN-WH / BIN-01")}
            />
          </div>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("parts.lotNumber")}
            </label>
            <TextField
              value={lotNumber}
              onChange={(val) => setLotNumber(val)}
              placeholder={t("parts.lotPlaceholder", "LOT-2026-X")}
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("parts.serialNumber")}
            </label>
            <TextField
              value={serialNumber}
              onChange={(val) => setSerialNumber(val)}
              placeholder={t("parts.serialPlaceholder", "SN-123456")}
            />
          </div>
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isLoading={isPending}>
            {t("common.confirm")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
