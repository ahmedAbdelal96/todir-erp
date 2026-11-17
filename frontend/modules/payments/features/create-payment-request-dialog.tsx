import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { PartyPicker } from "../../directory/public.ts";
import { PaymentCurrencyPicker } from "../components/payment-currency-picker.tsx";
import { useCreatePaymentRequest, useCreatePaymentLink } from "../hooks/payment-queries.ts";
import type {
  CreatePaymentIntentRequest,
  CreatePaymentLinkRequest,
  PaymentLinkAmountPolicy
} from "../types.ts";

export interface CreatePaymentRequestDialogProps {
  readonly isOpen: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly legalEntityId: string;
  readonly branchId?: string | undefined;
  readonly providerConfigId?: string | undefined;
  readonly onSuccess?: () => void;
}

export function CreatePaymentRequestDialog({
  isOpen,
  onOpenChange,
  legalEntityId,
  branchId,
  providerConfigId,
  onSuccess
}: CreatePaymentRequestDialogProps): React.JSX.Element {
  const { t } = useTranslation("payments");

  const [partyId, setPartyId] = useState<string>("");
  const [amount, setAmount] = useState<string>("");
  const [currencyCode, setCurrencyCode] = useState<string>("");
  const [generateLink, setGenerateLink] = useState<boolean>(true);
  const [amountPolicy, setAmountPolicy] = useState<PaymentLinkAmountPolicy>("Fixed");
  const [expiryDays, setExpiryDays] = useState<string>("7");
  const [generatedLinkUrl, setGeneratedLinkUrl] = useState<string>("");
  const [errorMsg, setErrorMsg] = useState<string>("");

  const createRequestMutation = useCreatePaymentRequest();
  const createLinkMutation = useCreatePaymentLink();

  const isSubmitting = createRequestMutation.isPending || createLinkMutation.isPending;

  const handleClose = () => {
    setPartyId("");
    setAmount("");
    setCurrencyCode("");
    setGenerateLink(true);
    setAmountPolicy("Fixed");
    setExpiryDays("7");
    setGeneratedLinkUrl("");
    setErrorMsg("");
    onOpenChange(false);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg("");

    const numAmount = Number(amount);
    if (!amount || isNaN(numAmount) || numAmount <= 0) {
      setErrorMsg(t("errors.amount_invalid"));
      return;
    }

    try {
      const expiresAt = new Date();
      expiresAt.setDate(expiresAt.getDate() + Number(expiryDays));

      // Use provided providerConfigId or fallback standard
      const configId = providerConfigId || "00000000-0000-0000-0000-000000000000";

      const intentRequest: CreatePaymentIntentRequest = {
        legalEntityId,
        branchId: branchId || null,
        providerConfigId: configId,
        partyId: partyId || null,
        amount: Number(amount).toFixed(2),
        currencyCode: currencyCode as any,
        expiresAt: expiresAt.toISOString()
      };

      const createdIntent = await createRequestMutation.mutateAsync({
        legalEntityId,
        request: intentRequest
      });

      if (generateLink) {
        const linkRequest: CreatePaymentLinkRequest = {
          paymentRequestId: createdIntent.id,
          expiresAt: expiresAt.toISOString(),
          amountPolicy
        };

        const linkResult = await createLinkMutation.mutateAsync({
          legalEntityId,
          requestId: createdIntent.id,
          request: linkRequest
        });

        const fullUrl = `${window.location.origin}/payments/pay/${linkResult.token}`;
        setGeneratedLinkUrl(fullUrl);
      } else {
        handleClose();
        onSuccess?.();
      }
    } catch (err: unknown) {
      const problem = err as { problemDetails?: { code?: string; detail?: string } };
      const code = problem?.problemDetails?.code;
      if (code && (t as any)(`errors.${code}`, { defaultValue: "" })) {
        setErrorMsg((t as any)(`errors.${code}`));
      } else {
        setErrorMsg((err as Error).message || t("errors.unknown"));
      }
    }
  };

  const policyOptions = [
    { value: "Fixed", label: (t as any)("link.policyFixed") },
    { value: "PartialAllowed", label: (t as any)("link.policyPartial") },
    { value: "CustomerEnteredWithinLimits", label: (t as any)("link.policyCustom") }
  ];

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) handleClose();
      }}
      title={t("requestCreate.title")}
      size="medium"
    >
      {generatedLinkUrl ? (
        <div style={{ padding: "1rem 0" }}>
          <div
            style={{
              padding: "1rem",
              backgroundColor: "#f0fdf4",
              border: "1px solid #bbf7d0",
              borderRadius: "6px",
              color: "#166534",
              marginBottom: "1rem"
            }}
          >
            <h4 style={{ margin: "0 0 0.5rem 0", fontWeight: 700 }}>{t("requestCreate.linkGeneratedSuccess")}</h4>
            <p style={{ margin: 0, fontSize: "0.875rem" }}>{t("requestCreate.linkGeneratedDesc")}</p>
          </div>

          <div style={{ display: "flex", gap: "0.5rem", marginBottom: "1.5rem" }}>
            <input
              type="text"
              readOnly
              value={generatedLinkUrl}
              style={{
                flex: 1,
                padding: "0.5rem 0.75rem",
                fontSize: "0.875rem",
                border: "1px solid #cbd5e1",
                borderRadius: "6px",
                backgroundColor: "#f8fafc"
              }}
            />
            <Button
              variant="secondary"
              onPress={() => {
                navigator.clipboard.writeText(generatedLinkUrl);
              }}
            >
              {t("requestCreate.copyLink")}
            </Button>
          </div>

          <div style={{ display: "flex", justifyContent: "flex-end" }}>
            <Button
              variant="primary"
              onPress={() => {
                handleClose();
                onSuccess?.();
              }}
            >
              {t("actions.done")}
            </Button>
          </div>
        </div>
      ) : (
        <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
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

          {/* Party Selection (Optional) */}
          <PartyPicker
            label={t("requestCreate.customerLabel")}
            value={partyId || undefined}
            onChange={(id) => setPartyId(id)}
            roleFilter="Customer"
            enableQuickCreate={false}
          />

          {/* Amount & Currency */}
          <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr", gap: "0.75rem" }}>
            <TextField
              label={t("requestCreate.amountLabel")}
              value={amount}
              onChange={setAmount}
              placeholder="0.00"
              isRequired
              type="number"
            />
            <PaymentCurrencyPicker
              legalEntityId={legalEntityId}
              label={t("requestCreate.currencyLabel")}
              value={currencyCode}
              onChange={setCurrencyCode}
              isRequired
            />
          </div>

          {/* Expiry */}
          <TextField
            label={t("requestCreate.expiryDaysLabel")}
            type="number"
            value={expiryDays}
            onChange={setExpiryDays}
            isRequired
          />

          {/* Link Generation Toggle */}
          <div style={{ borderTop: "1px solid #e2e8f0", paddingTop: "1rem" }}>
            <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", marginBottom: "0.75rem" }}>
              <input
                type="checkbox"
                id="req-gen-link"
                checked={generateLink}
                onChange={(e) => setGenerateLink(e.target.checked)}
                style={{ width: "16px", height: "16px", cursor: "pointer" }}
              />
              <label htmlFor="req-gen-link" style={{ fontSize: "0.875rem", cursor: "pointer", fontWeight: 600 }}>
                {t("requestCreate.generateLinkCheckbox")}
              </label>
            </div>

            {generateLink && (
              <Select
                label={t("requestCreate.policyLabel")}
                value={amountPolicy}
                onChange={(val) => setAmountPolicy(val as PaymentLinkAmountPolicy)}
                options={policyOptions}
                isRequired
              />
            )}
          </div>

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
            <Button variant="secondary" onPress={handleClose} isDisabled={isSubmitting}>
              {t("actions.cancel")}
            </Button>
            <Button variant="primary" type="submit" isDisabled={isSubmitting}>
              {isSubmitting ? t("requestCreate.submitting") : t("requestCreate.submitAction")}
            </Button>
          </div>
        </form>
      )}
    </Dialog>
  );
}
