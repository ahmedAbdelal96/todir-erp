import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { useRecordQualityDecisionMutation, useUpdateQualityPolicyMutation } from "../../hooks/use-manufacturing.ts";
import type { QualityGate } from "../../types.ts";

export interface QualityGateDialogProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly orderId: string;
  readonly gate?: QualityGate | null;
}

export function QualityGateDialog({
  isOpen,
  onClose,
  orderId,
  gate
}: QualityGateDialogProps): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const decisionMutation = useRecordQualityDecisionMutation();
  const policyMutation = useUpdateQualityPolicyMutation();

  const [mode, setMode] = useState<"decision" | "policy">("decision");
  const [decisionState, setDecisionState] = useState("Passed");
  const [sourceRef, setSourceRef] = useState("");
  const [policyRequired, setPolicyRequired] = useState(gate?.required ?? true);
  const [policyReason, setPolicyReason] = useState("");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (mode === "decision") {
      decisionMutation.mutate(
        {
          orderId,
          request: {
            productionOperationId: gate?.productionOperationId,
            required: gate?.required ?? true,
            capabilityAvailable: true,
            decisionState,
            sourceRef: sourceRef || undefined
          }
        },
        {
          onSuccess: () => {
            onClose();
          }
        }
      );
    } else {
      if (!policyReason.trim()) return;
      policyMutation.mutate(
        {
          orderId,
          request: {
            productionOperationId: gate?.productionOperationId,
            required: policyRequired,
            reason: policyReason.trim()
          }
        },
        {
          onSuccess: () => {
            onClose();
          }
        }
      );
    }
  };

  const isPending = decisionMutation.isPending || policyMutation.isPending;

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("quality.title")}
      description={t("quality.updatePolicy")}
      size="small"
    >
      <div style={{ display: "flex", gap: "0.5rem", marginBottom: "1rem" }}>
        <button
          type="button"
          onClick={() => setMode("decision")}
          style={{
            flex: 1,
            padding: "0.5rem",
            borderRadius: "var(--radius-sm)",
            border: `1px solid ${mode === "decision" ? "var(--color-brand-primary)" : "var(--color-border-default)"}`,
            background: mode === "decision" ? "var(--color-brand-primary)" : "transparent",
            color: mode === "decision" ? "#fff" : "inherit",
            fontWeight: 600,
            cursor: "pointer"
          }}
        >
          {t("quality.decisionState")}
        </button>
        <button
          type="button"
          onClick={() => setMode("policy")}
          style={{
            flex: 1,
            padding: "0.5rem",
            borderRadius: "var(--radius-sm)",
            border: `1px solid ${mode === "policy" ? "var(--color-brand-primary)" : "var(--color-border-default)"}`,
            background: mode === "policy" ? "var(--color-brand-primary)" : "transparent",
            color: mode === "policy" ? "#fff" : "inherit",
            fontWeight: 600,
            cursor: "pointer"
          }}
        >
          {t("quality.updatePolicy")}
        </button>
      </div>

      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {mode === "decision" ? (
          <>
            <label style={{ display: "flex", flexDirection: "column", gap: "0.25rem", fontSize: "0.8125rem", fontWeight: 600 }}>
              {t("quality.decisionState")}
              <select
                value={decisionState}
                onChange={(e) => setDecisionState(e.target.value)}
                style={{
                  padding: "0.5rem",
                  borderRadius: "var(--radius-sm)",
                  border: "1px solid var(--color-border-default)",
                  background: "var(--color-surface-card)",
                  fontSize: "0.875rem"
                }}
              >
                <option value="Passed">{t("status.Passed")}</option>
                <option value="Failed">{t("status.Failed")}</option>
                <option value="Exempt">{t("status.Exempt")}</option>
                <option value="Blocked">{t("status.Blocked")}</option>
              </select>
            </label>

            <TextField
              label={t("orders.sourceRef")}
              value={sourceRef}
              onChange={(e) => setSourceRef(e.target.value)}
            />
          </>
        ) : (
          <>
            <label style={{ display: "flex", alignItems: "center", gap: "0.5rem", fontSize: "0.875rem", fontWeight: 600 }}>
              <input
                type="checkbox"
                checked={policyRequired}
                onChange={(e) => setPolicyRequired(e.target.checked)}
              />
              {t("quality.requiredGate")}
            </label>

            <TextField
              label={t("quality.exemptReason")}
              value={policyReason}
              onChange={(e) => setPolicyReason(e.target.value)}
              required
            />
          </>
        )}

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button type="button" variant="outline" onPress={onClose}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" variant="primary" isDisabled={isPending || (mode === "policy" && !policyReason.trim())}>
            {isPending ? t("common.loading") : t("common.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
