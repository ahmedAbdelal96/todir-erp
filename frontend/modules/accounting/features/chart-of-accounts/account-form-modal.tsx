import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { useCreateAccount } from "../../hooks/use-ledger-queries.ts";
import { mapLedgerError } from "../../services/ledger-error-map.ts";
import type { AccountDto, AccountType, CreateAccountRequest } from "../../types.ts";

export interface AccountFormModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly accounts: readonly AccountDto[];
}

export function AccountFormModal({
  isOpen,
  onClose,
  legalEntityId,
  accounts
}: AccountFormModalProps): React.JSX.Element | null {
  const { t, i18n } = useTranslation("accounting");
  const createAccountMutation = useCreateAccount();

  const [code, setCode] = useState("");
  const [nameAr, setNameAr] = useState("");
  const [nameEn, setNameEn] = useState("");
  const [type, setType] = useState<AccountType>("Asset");
  const [subtype, setSubtype] = useState("Operating");
  const [parentAccountId, setParentAccountId] = useState<string>("");
  const [allowDirectPosting, setAllowDirectPosting] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const currentLang = i18n.language?.startsWith("ar") ? "ar" : "en";

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    if (!code.trim() || !nameAr.trim() || !nameEn.trim()) {
      setErrorMessage(t("account.validationRequiredFields"));
      return;
    }

    const request: CreateAccountRequest = {
      code: code.trim(),
      nameAr: nameAr.trim(),
      nameEn: nameEn.trim(),
      type,
      subtype: subtype.trim() || "Operating",
      parentAccountId: parentAccountId ? parentAccountId : null,
      allowDirectPosting
    };

    try {
      await createAccountMutation.mutateAsync({ legalEntityId, request });
      onClose();
      // Reset form
      setCode("");
      setNameAr("");
      setNameEn("");
      setParentAccountId("");
      setAllowDirectPosting(true);
    } catch (err) {
      setErrorMessage(mapLedgerError(err, currentLang));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={t("account.createNew")}
      size="medium"
      footer={
        <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
          <Button variant="secondary" onPress={onClose}>
            {t("common.cancel")}
          </Button>
          <Button
            variant="primary"
            isLoading={createAccountMutation.isPending}
            onPress={handleSubmit as unknown as () => void}
          >
            {t("common.save")}
          </Button>
        </div>
      }
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <TextField
          label={t("account.code")}
          value={code}
          onChange={setCode}
          isRequired
        />

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("account.nameAr")}
            value={nameAr}
            onChange={setNameAr}
            isRequired
          />
          <TextField
            label={t("account.nameEn")}
            value={nameEn}
            onChange={setNameEn}
            isRequired
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", marginBottom: "0.25rem", fontSize: "0.875rem", fontWeight: 600 }}>
              {t("account.type")}
            </label>
            <select
              value={type}
              onChange={(e) => setType(e.target.value as AccountType)}
              style={{
                width: "100%",
                padding: "0.5rem 0.75rem",
                borderRadius: "var(--radius-md, 0.5rem)",
                border: "1px solid var(--color-border-default)",
                backgroundColor: "var(--color-surface-card)"
              }}
            >
              <option value="Asset">{t("accountTypes.Asset")}</option>
              <option value="Liability">{t("accountTypes.Liability")}</option>
              <option value="Equity">{t("accountTypes.Equity")}</option>
              <option value="Revenue">{t("accountTypes.Revenue")}</option>
              <option value="Expense">{t("accountTypes.Expense")}</option>
            </select>
          </div>

          <TextField
            label={t("account.subtype")}
            value={subtype}
            onChange={setSubtype}
          />
        </div>

        <div>
          <label style={{ display: "block", marginBottom: "0.25rem", fontSize: "0.875rem", fontWeight: 600 }}>
            {t("account.parentAccount")}
          </label>
          <select
            value={parentAccountId}
            onChange={(e) => setParentAccountId(e.target.value)}
            style={{
              width: "100%",
              padding: "0.5rem 0.75rem",
              borderRadius: "var(--radius-md, 0.5rem)",
              border: "1px solid var(--color-border-default)",
              backgroundColor: "var(--color-surface-card)"
            }}
          >
            <option value="">{t("account.noParentRoot")}</option>
            {accounts.map((acc) => (
              <option key={acc.id} value={acc.id}>
                {acc.code} — {acc.nameAr} ({acc.nameEn})
              </option>
            ))}
          </select>
        </div>

        <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", marginTop: "0.25rem" }}>
          <input
            type="checkbox"
            id="allow-direct-posting"
            checked={allowDirectPosting}
            onChange={(e) => setAllowDirectPosting(e.target.checked)}
          />
          <label htmlFor="allow-direct-posting" style={{ fontSize: "0.875rem", cursor: "pointer" }}>
            {t("account.allowDirectPosting")}
          </label>
        </div>

        {errorMessage && (
          <div
            style={{
              padding: "0.75rem",
              borderRadius: "var(--radius-md, 0.5rem)",
              backgroundColor: "var(--color-status-danger-subtle, #fee2e2)",
              color: "var(--color-status-danger, #b91c1c)",
              fontSize: "0.875rem"
            }}
          >
            {errorMessage}
          </div>
        )}
      </form>
    </Dialog>
  );
}
