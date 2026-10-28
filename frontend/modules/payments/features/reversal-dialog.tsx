import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { LocalDateField } from "../../../components/fields/local-date-field.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { usePaymentFees, useReversePayment } from "../hooks/payment-queries.ts";
import type {
  PaymentRecord,
  ReversePaymentRequest,
  PaymentFeeDispositionRequest,
  PaymentFeeDisposition
} from "../types.ts";

export interface ReversalDialogProps {
  readonly payment: PaymentRecord | null;
  readonly isOpen: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly legalEntityId: string;
  readonly branchId?: string | undefined;
  readonly onSuccess?: () => void;
}

export function ReversalDialog({
  payment,
  isOpen,
  onOpenChange,
  legalEntityId,
  branchId,
  onSuccess
}: ReversalDialogProps): React.JSX.Element {
  const { t } = useTranslation("payments");

  const [paymentDate, setPaymentDate] = useState<string>(new Date().toISOString().slice(0, 10));
  const [reasonCode, setReasonCode] = useState<string>("accounting-correction");
  const [errorMsg, setErrorMsg] = useState<string>("");

  const feesQuery = usePaymentFees(legalEntityId, payment?.id || "", branchId, Boolean(payment?.id && isOpen));
  const reverseMutation = useReversePayment();

  // Fee dispositions state: feeId -> { disposition, evidenceReference }
  const [feeChoices, setFeeChoices] = useState<Record<string, { disposition: PaymentFeeDisposition; evidence: string }>>({});

  useEffect(() => {
    if (feesQuery.data && feesQuery.data.length > 0) {
      const initial: Record<string, { disposition: PaymentFeeDisposition; evidence: string }> = {};
      feesQuery.data.forEach((fee) => {
        initial[fee.id] = { disposition: "Retain", evidence: "" };
      });
      setFeeChoices(initial);
    }
  }, [feesQuery.data]);

  if (!payment) return <></>;

  const getFeeTypeLabel = (ft: string) => {
    switch (ft) {
      case "BankFee": return t("feeType.bankfee");
      case "ProviderFee": return t("feeType.providerfee");
      case "CardProcessingFee": return t("feeType.cardprocessingfee");
      case "FXFee": return t("feeType.fxfee");
      case "TransferFee": return t("feeType.transferfee");
      case "OtherApprovedFee":
      default: return t("feeType.otherapprovedfee");
    }
  };

  const handleClose = () => {
    setErrorMsg("");
    onOpenChange(false);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg("");

    const feeDispositions: PaymentFeeDispositionRequest[] = [];
    if (feesQuery.data && feesQuery.data.length > 0) {
      for (const fee of feesQuery.data) {
        const choice = feeChoices[fee.id] || { disposition: "Retain", evidence: "" };
        if (choice.disposition === "Reverse" && !choice.evidence.trim()) {
          setErrorMsg(t("reversal.evidenceRequiredForFee"));
          return;
        }
        feeDispositions.push({
          feeId: fee.id,
          disposition: choice.disposition,
          evidenceReference: choice.disposition === "Reverse" ? choice.evidence.trim() : null
        });
      }
    }

    try {
      const request: ReversePaymentRequest = {
        paymentDate,
        postingDate: paymentDate,
        reasonCode,
        feeDispositions: feeDispositions.length > 0 ? feeDispositions : null
      };

      await reverseMutation.mutateAsync({
        legalEntityId,
        paymentId: payment.id,
        request,
        branchId
      });

      handleClose();
      onSuccess?.();
    } catch (err: unknown) {
      const problem = err as { problemDetails?: { code?: string; detail?: string } };
      const rawCode = problem?.problemDetails?.code;
      const normalizedCode = rawCode?.replace(/^payments\./, "").replace("payment_has_applications", "reversal_has_applications");
      if (normalizedCode && (t as any)(`errors.${normalizedCode}`, { defaultValue: "" })) {
        setErrorMsg((t as any)(`errors.${normalizedCode}`));
      } else if (problem?.problemDetails?.detail) {
        setErrorMsg(problem.problemDetails.detail);
      } else {
        setErrorMsg((err as Error).message || t("errors.unknown"));
      }
    }
  };

  const reasonOptions = [
    { id: "accounting-correction", label: t("reversal.reasonAccountingCorrection") },
    { id: "wrong-account", label: t("reversal.reasonWrongAccount") },
    { id: "bounced-cheque", label: t("reversal.reasonBouncedCheque") },
    { id: "duplicate-posting", label: t("reversal.reasonDuplicatePosting") },
    { id: "other", label: t("reversal.reasonOther") }
  ];

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) handleClose();
      }}
      title={t("reversal.title")}
      size="medium"
    >
      <form noValidate onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMsg && (
          <div
            style={{
              padding: "0.75rem 1rem",
              borderRadius: "6px",
              backgroundColor: "#fef2f2",
              border: "1px solid #fecaca",
              color: "#991b1b",
              fontSize: "0.875rem"
            }}
          >
            {errorMsg}
          </div>
        )}

        <div style={{ backgroundColor: "#fffbeb", border: "1px solid #fde68a", borderRadius: "6px", padding: "0.75rem 1rem", fontSize: "0.8125rem", color: "#92400e" }}>
          {t("reversal.warningNotice")}
        </div>

        {/* Original Payment Context */}
        <div
          style={{
            backgroundColor: "#f8fafc",
            border: "1px solid #cbd5e1",
            borderRadius: "6px",
            padding: "0.875rem 1rem",
            fontSize: "0.875rem"
          }}
        >
          <div style={{ display: "flex", justifyContent: "space-between", marginBottom: "0.25rem" }}>
            <span style={{ color: "#64748b" }}>{t("reversal.targetPayment")}:</span>
            <span style={{ fontWeight: 700 }}><bdi>{payment.number}</bdi></span>
          </div>
          <div style={{ display: "flex", justifyContent: "space-between" }}>
            <span style={{ color: "#64748b" }}>{t("reversal.amount")}:</span>
            <span style={{ fontWeight: 700 }}>
              <MoneyDisplay value={{ amount: payment.amount, currency: payment.currencyCode }} />
            </span>
          </div>
        </div>

        {/* Reason & Date */}
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <Select
            label={t("reversal.reasonLabel")}
            selectedKey={reasonCode}
            onSelectionChange={(val) => setReasonCode(String(val ?? ""))}
            options={reasonOptions}
            isRequired
          />

          <LocalDateField
            label={t("reversal.dateLabel")}
            value={paymentDate}
            onChange={setPaymentDate}
            isRequired
          />
        </div>

        {/* Fee Dispositions Section */}
        {feesQuery.data && feesQuery.data.length > 0 && (
          <div style={{ borderTop: "1px solid #e2e8f0", paddingTop: "1rem" }}>
            <h4 style={{ margin: "0 0 0.5rem 0", fontSize: "0.875rem", fontWeight: 700, color: "#102a43" }}>
              {t("reversal.feesSectionTitle")}
            </h4>
            <p style={{ margin: "0 0 0.75rem 0", fontSize: "0.8125rem", color: "#64748b" }}>
              {t("reversal.feesExplanation")}
            </p>

            <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
              {feesQuery.data.map((fee) => {
                const choice = feeChoices[fee.id] || { disposition: "Retain", evidence: "" };
                return (
                  <div
                    key={fee.id}
                    style={{
                      border: "1px solid #cbd5e1",
                      borderRadius: "6px",
                      padding: "0.75rem",
                      backgroundColor: "#ffffff"
                    }}
                  >
                    <div style={{ display: "flex", justifyContent: "space-between", marginBottom: "0.5rem" }}>
                      <span style={{ fontWeight: 600, fontSize: "0.8125rem" }}>
                        {getFeeTypeLabel(fee.type)}
                      </span>
                      <span style={{ fontWeight: 700, fontSize: "0.8125rem" }}>
                        {fee.amount} {fee.currencyCode}
                      </span>
                    </div>

                    <div style={{ display: "flex", gap: "1rem", marginBottom: "0.5rem" }}>
                      <label style={{ display: "flex", alignItems: "center", gap: "0.375rem", fontSize: "0.8125rem", cursor: "pointer" }}>
                        <input
                          type="radio"
                          name={`fee-disp-${fee.id}`}
                          value="Retain"
                          checked={choice.disposition === "Retain"}
                          onChange={() =>
                            setFeeChoices((prev) => ({
                              ...prev,
                              [fee.id]: { ...choice, disposition: "Retain" }
                            }))
                          }
                        />
                        <span>{t("reversal.retainFeeLabel")}</span>
                      </label>

                      <label style={{ display: "flex", alignItems: "center", gap: "0.375rem", fontSize: "0.8125rem", cursor: "pointer" }}>
                        <input
                          type="radio"
                          name={`fee-disp-${fee.id}`}
                          value="Reverse"
                          checked={choice.disposition === "Reverse"}
                          onChange={() =>
                            setFeeChoices((prev) => ({
                              ...prev,
                              [fee.id]: { ...choice, disposition: "Reverse" }
                            }))
                          }
                        />
                        <span>{t("reversal.reverseFeeLabel")}</span>
                      </label>
                    </div>

                    <div style={{ fontSize: "0.75rem", color: "#64748b", marginBottom: "0.5rem" }}>
                      {choice.disposition === "Retain"
                        ? t("reversal.retainFeeDescription")
                        : t("reversal.reverseFeeDescription")}
                    </div>

                    {choice.disposition === "Reverse" && (
                      <TextField
                        label={t("reversal.feeEvidenceLabel")}
                        value={choice.evidence}
                        onChange={(val) =>
                          setFeeChoices((prev) => ({
                            ...prev,
                            [fee.id]: { ...choice, evidence: val }
                          }))
                        }
                        placeholder={t("reversal.feeEvidencePlaceholder")}
                        isRequired
                      />
                    )}
                  </div>
                );
              })}
            </div>
          </div>
        )}

        {/* Actions */}
        <div
          style={{
            display: "flex",
            justifyContent: "flex-end",
            gap: "0.75rem",
            marginTop: "1rem",
            borderTop: "1px solid #e2e8f0",
            paddingTop: "1rem"
          }}
        >
          <Button variant="secondary" onPress={handleClose} isDisabled={reverseMutation.isPending}>
            {t("actions.cancel")}
          </Button>
          <Button variant="danger" type="submit" isDisabled={reverseMutation.isPending}>
            {reverseMutation.isPending ? t("reversal.submitting") : t("reversal.submitAction")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
