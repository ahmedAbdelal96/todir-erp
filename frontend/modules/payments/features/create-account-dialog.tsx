import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { useCreatePaymentAccount } from "../hooks/payment-queries.ts";
import type { PaymentAccountType, CreatePaymentAccountRequest } from "../types.ts";

export interface CreateAccountDialogProps {
  readonly isOpen: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly legalEntityId: string;
  readonly branchId?: string | undefined;
  readonly onSuccess?: () => void;
}

export function CreateAccountDialog({
  isOpen,
  onOpenChange,
  legalEntityId,
  branchId,
  onSuccess
}: CreateAccountDialogProps): React.JSX.Element {
  const { t } = useTranslation("payments");

  const [code, setCode] = useState<string>("");
  const [name, setName] = useState<string>("");
  const [type, setType] = useState<PaymentAccountType>("Bank");
  const [currencyPolicy, setCurrencyPolicy] = useState<string>("SingleCurrency");
  const [currencyCode, setCurrencyCode] = useState<string>("EGP");
  const [bankName, setBankName] = useState<string>("");
  const [accountHolder, setAccountHolder] = useState<string>("");
  const [maskedIdentifier, setMaskedIdentifier] = useState<string>("");
  const [errorMsg, setErrorMsg] = useState<string>("");

  const createAccountMutation = useCreatePaymentAccount();

  const resetForm = () => {
    setCode("");
    setName("");
    setType("Bank");
    setCurrencyPolicy("SingleCurrency");
    setCurrencyCode("EGP");
    setBankName("");
    setAccountHolder("");
    setMaskedIdentifier("");
    setErrorMsg("");
  };

  const handleClose = () => {
    resetForm();
    onOpenChange(false);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg("");

    if (!code.trim() || !name.trim()) {
      setErrorMsg(t("accountCreate.codeAndNameRequired"));
      return;
    }

    try {
      const request: CreatePaymentAccountRequest = {
        legalEntityId,
        branchId: type === "Cashbox" ? (branchId || null) : null,
        code: code.trim().toUpperCase(),
        name: name.trim(),
        type,
        currencyPolicy,
        currencyCode: currencyPolicy === "SingleCurrency" ? currencyCode.trim().toUpperCase() : null,
        bankName: type === "Bank" ? bankName.trim() || null : null,
        accountHolder: accountHolder.trim() || null,
        maskedIdentifier: maskedIdentifier.trim() || null
      };

      await createAccountMutation.mutateAsync({
        legalEntityId,
        request
      });

      handleClose();
      onSuccess?.();
    } catch (err: unknown) {
      const problem = err as { problemDetails?: { code?: string; detail?: string } };
      const codeError = problem?.problemDetails?.code;
      if (codeError && (t as any)(`errors.${codeError}`, { defaultValue: "" })) {
        setErrorMsg((t as any)(`errors.${codeError}`));
      } else {
        setErrorMsg((err as Error).message || t("errors.unknown"));
      }
    }
  };

  const typeOptions = [
    { id: "Bank", label: t("accountType.bank") },
    { id: "Cashbox", label: t("accountType.cashbox") },
    { id: "ProviderClearing", label: t("accountType.providerclearing") },
    { id: "Wallet", label: t("accountType.wallet") },
    { id: "InternalClearing", label: t("accountType.internalclearing") }
  ];

  const policyOptions = [
    { id: "SingleCurrency", label: t("accountCreate.singleCurrency") },
    { id: "MultiCurrency", label: t("accountCreate.multiCurrency") }
  ];

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) handleClose();
      }}
      title={t("accountCreate.title")}
      size="medium"
    >
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

        <div style={{ display: "grid", gridTemplateColumns: "1fr 2fr", gap: "0.75rem" }}>
          <TextField
            label={t("accountCreate.codeLabel")}
            value={code}
            onChange={setCode}
            placeholder={t("accountCreate.codePlaceholder")}
            isRequired
          />
          <TextField
            label={t("accountCreate.nameLabel")}
            value={name}
            onChange={setName}
            placeholder={t("accountCreate.namePlaceholder")}
            isRequired
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <Select
            label={t("accountCreate.typeLabel")}
            selectedKey={type}
            onSelectionChange={(val) => setType(String(val ?? "") as PaymentAccountType)}
            options={typeOptions}
            isRequired
          />
          <Select
            label={t("accountCreate.currencyPolicyLabel")}
            selectedKey={currencyPolicy}
            onSelectionChange={(val) => setCurrencyPolicy(String(val ?? ""))}
            options={policyOptions}
            isRequired
          />
        </div>

        {currencyPolicy === "SingleCurrency" && (
          <TextField
            label={t("accountCreate.currencyLabel")}
            value={currencyCode}
            onChange={setCurrencyCode}
            isRequired
          />
        )}

        {type === "Bank" && (
          <TextField
            label={t("accountCreate.bankNameLabel")}
            value={bankName}
            onChange={setBankName}
            placeholder={t("accountCreate.bankNamePlaceholder")}
          />
        )}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("accountCreate.accountHolderLabel")}
            value={accountHolder}
            onChange={setAccountHolder}
            placeholder={t("accountCreate.accountHolderPlaceholder")}
          />
          <TextField
            label={t("accountCreate.maskedIdentifierLabel")}
            value={maskedIdentifier}
            onChange={setMaskedIdentifier}
            placeholder={t("accountCreate.maskedIdentifierPlaceholder")}
          />
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
          <Button variant="secondary" onPress={handleClose} isDisabled={createAccountMutation.isPending}>
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={createAccountMutation.isPending}>
            {createAccountMutation.isPending ? t("accountCreate.submitting") : t("accountCreate.submitAction")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
