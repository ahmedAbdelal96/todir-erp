import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useCreateTreasuryAccount } from "../../hooks/use-banking-queries.ts";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import type { TreasuryAccountType, TreasuryCurrencyPolicy } from "../../types.ts";

interface Props {
  readonly legalEntityId: string;
  readonly isOpen: boolean;
  readonly onClose: () => void;
}

export function CreateTreasuryAccountModal({ legalEntityId, isOpen, onClose }: Props): React.JSX.Element | null {
  const { t } = useTranslation("banking");
  const createMutation = useCreateTreasuryAccount(legalEntityId);

  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [type, setType] = useState<TreasuryAccountType>("Bank");
  const [currencyPolicy, setCurrencyPolicy] = useState<TreasuryCurrencyPolicy>("SingleCurrency");
  const [currencyCode, setCurrencyCode] = useState("EGP");
  const [purpose, setPurpose] = useState("Operating");
  const [liquidityClass, setLiquidityClass] = useState("CashEquivalent");
  const [maskedIdentifier, setMaskedIdentifier] = useState("");
  const [allowedUses, setAllowedUses] = useState("Disbursements,Receipts,Payroll");
  const [secretRef, setSecretRef] = useState("");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(null);

    if (!code.trim() || !name.trim()) {
      setErrorMsg(t("common.validationError"));
      return;
    }

    try {
      await createMutation.mutateAsync({
        legalEntityId,
        code: code.trim(),
        name: name.trim(),
        type,
        currencyPolicy,
        currencyCode: currencyPolicy === "SingleCurrency" ? currencyCode.trim() : null,
        purpose: purpose.trim(),
        liquidityClass: liquidityClass.trim(),
        maskedIdentifier: maskedIdentifier.trim() || null,
        allowedUses: allowedUses ? allowedUses.split(",").map((s) => s.trim()).filter(Boolean) : [],
        secretRef: secretRef.trim() || null
      });
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.error");
      setErrorMsg(msg);
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("accounts.createModalTitle")}>
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem", minWidth: "320px", maxWidth: "480px" }}>
        {errorMsg && (
          <div role="alert" style={{ padding: "0.75rem", background: "var(--color-surface-danger)", color: "var(--color-status-danger)", borderRadius: "var(--radius-sm)", fontSize: "0.875rem" }}>
            {errorMsg}
          </div>
        )}

        <TextField
          label={t("accounts.codeLabel")}
          value={code}
          onChange={(val) => setCode(val)}
          isRequired
          autoFocus
        />

        <TextField
          label={t("accounts.nameLabel")}
          value={name}
          onChange={(val) => setName(val)}
          isRequired
        />

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <div>
            <Select
              label={t("accounts.typeLabel")}
              selectedKey={type}
              onSelectionChange={(key) => setType((key as TreasuryAccountType) || "Bank")}
              options={[
                { id: "Bank", label: t("accountTypes.Bank") },
                { id: "Cash", label: t("accountTypes.Cash") },
                { id: "PettyCash", label: t("accountTypes.PettyCash") },
                { id: "DigitalWallet", label: t("accountTypes.DigitalWallet") },
                { id: "ProviderSettlement", label: t("accountTypes.ProviderSettlement") }
              ]}
            />
          </div>

          <div>
            <Select
              label={t("accounts.currencyPolicyLabel")}
              selectedKey={currencyPolicy}
              onSelectionChange={(key) => setCurrencyPolicy((key as TreasuryCurrencyPolicy) || "SingleCurrency")}
              options={[
                { id: "SingleCurrency", label: t("currencyPolicies.SingleCurrency") },
                { id: "MultiCurrency", label: t("currencyPolicies.MultiCurrency") }
              ]}
            />
          </div>
        </div>

        {currencyPolicy === "SingleCurrency" && (
          <TextField
            label={t("accounts.currencyCodeLabel")}
            value={currencyCode}
            onChange={(val) => setCurrencyCode(val)}
            isRequired
          />
        )}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("accounts.purposeLabel")}
            value={purpose}
            onChange={(val) => setPurpose(val)}
            isRequired
          />
          <TextField
            label={t("accounts.liquidityClassLabel")}
            value={liquidityClass}
            onChange={(val) => setLiquidityClass(val)}
            isRequired
          />
        </div>

        <TextField
          label={t("accounts.maskedIdentifierLabel")}
          value={maskedIdentifier}
          onChange={(val) => setMaskedIdentifier(val)}
        />

        <TextField
          label={t("accounts.allowedUsesLabel")}
          value={allowedUses}
          onChange={(val) => setAllowedUses(val)}
        />

        <TextField
          label={t("accounts.secretRefLabel")}
          value={secretRef}
          onChange={(val) => setSecretRef(val)}
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem", borderTop: "1px solid var(--color-border-subtle)", paddingTop: "1rem" }}>
          <Button type="button" variant="secondary" onClick={onClose} isDisabled={createMutation.isPending}>
            {t("accounts.cancel")}
          </Button>
          <Button type="submit" variant="primary" isDisabled={createMutation.isPending}>
            {createMutation.isPending ? t("common.loading") : t("accounts.createSubmit")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
