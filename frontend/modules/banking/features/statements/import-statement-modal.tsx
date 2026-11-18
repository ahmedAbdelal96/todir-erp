import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useImportStatement } from "../../hooks/use-banking-queries.ts";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import type { ImportStatementLineRequest } from "../../types.ts";

interface Props {
  readonly legalEntityId: string;
  readonly accountId: string;
  readonly isOpen: boolean;
  readonly onClose: () => void;
}

export function ImportStatementModal({ legalEntityId, accountId, isOpen, onClose }: Props): React.JSX.Element | null {
  const { t } = useTranslation("banking");
  const importMutation = useImportStatement(legalEntityId, accountId);

  const [statementNumber, setStatementNumber] = useState("");
  const [openingDate, setOpeningDate] = useState("");
  const [closingDate, setClosingDate] = useState("");
  const [currencyCode, setCurrencyCode] = useState("EGP");
  const [openingBalance, setOpeningBalance] = useState("10000");
  const [closingBalance, setClosingBalance] = useState("15000");
  const [sourceType, setSourceType] = useState("JSON");
  const [sourceReference, setSourceReference] = useState("");

  // Default sample line for quick testing
  const [lineAmount, setLineAmount] = useState("5000");
  const [lineDirection, setLineDirection] = useState<"Credit" | "Debit">("Credit");
  const [lineReference, setLineReference] = useState("Customer Wire Transfer");

  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(null);

    if (!statementNumber.trim()) {
      setErrorMsg(t("common.validationError"));
      return;
    }

    const parsedAmount = parseFloat(lineAmount) || 0;
    const today = new Date().toISOString().slice(0, 10);

    const lines: ImportStatementLineRequest[] = [
      {
        externalLineId: `LINE-${Date.now()}-1`,
        bookingDate: closingDate || today,
        valueDate: closingDate || today,
        direction: lineDirection,
        amount: parsedAmount,
        currencyCode: currencyCode.trim(),
        reference: lineReference.trim() || null,
        transactionCode: lineDirection === "Credit" ? "CR-DEP" : "DB-WD",
        counterpartyName: "Standard Corporate Partner",
        rawPayload: JSON.stringify({ note: "Statement line payload" })
      }
    ];

    try {
      await importMutation.mutateAsync({
        request: {
          statementNumber: statementNumber.trim(),
          openingDate: openingDate || null,
          closingDate: closingDate || null,
          currencyCode: currencyCode.trim(),
          openingBalance: openingBalance ? parseFloat(openingBalance) : null,
          closingBalance: closingBalance ? parseFloat(closingBalance) : null,
          sourceType: sourceType.trim(),
          sourceReference: sourceReference.trim() || null,
          lines
        }
      });
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.error");
      setErrorMsg(msg);
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("statements.importModalTitle")}>
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem", minWidth: "320px", maxWidth: "520px" }}>
        {errorMsg && (
          <div role="alert" style={{ padding: "0.75rem", background: "var(--color-surface-danger)", color: "var(--color-status-danger)", borderRadius: "var(--radius-sm)", fontSize: "0.875rem" }}>
            {errorMsg}
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("statements.statementNumberLabel")}
            value={statementNumber}
            onChange={(val) => setStatementNumber(val)}
            isRequired
            autoFocus
          />
          <TextField
            label={t("statements.currencyLabel")}
            value={currencyCode}
            onChange={(val) => setCurrencyCode(val)}
            isRequired
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("statements.openingDate")}
            type="date"
            value={openingDate}
            onChange={(val) => setOpeningDate(val)}
          />
          <TextField
            label={t("statements.closingDate")}
            type="date"
            value={closingDate}
            onChange={(val) => setClosingDate(val)}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("statements.openingBalance")}
            type="number"
            value={openingBalance}
            onChange={(val) => setOpeningBalance(val)}
          />
          <TextField
            label={t("statements.closingBalance")}
            type="number"
            value={closingBalance}
            onChange={(val) => setClosingBalance(val)}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("statements.sourceTypeLabel")}
            value={sourceType}
            onChange={(val) => setSourceType(val)}
            isRequired
          />
          <TextField
            label={t("statements.sourceReferenceLabel")}
            value={sourceReference}
            onChange={(val) => setSourceReference(val)}
          />
        </div>

        {/* Quick Statement Line Entry */}
        <div style={{ padding: "0.875rem", background: "var(--color-surface-page)", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-subtle)" }}>
          <div style={{ fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.5rem", color: "var(--color-brand-primary)" }}>
            {t("statements.linesJsonLabel")}
          </div>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.5rem" }}>
            <TextField
              label={t("statements.amount")}
              type="number"
              value={lineAmount}
              onChange={(val) => setLineAmount(val)}
              isRequired
            />
            <div>
              <Select
                label={t("statements.direction")}
                selectedKey={lineDirection}
                onSelectionChange={(key) => setLineDirection((key as "Credit" | "Debit") || "Credit")}
                options={[
                  { id: "Credit", label: t("reports.credit") },
                  { id: "Debit", label: t("reports.debit") }
                ]}
              />
            </div>
          </div>
          <div style={{ marginTop: "0.5rem" }}>
            <TextField
              label={t("statements.reference")}
              value={lineReference}
              onChange={(val) => setLineReference(val)}
            />
          </div>
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem", borderTop: "1px solid var(--color-border-subtle)", paddingTop: "1rem" }}>
          <Button type="button" variant="secondary" onClick={onClose} isDisabled={importMutation.isPending}>
            {t("accounts.cancel")}
          </Button>
          <Button type="submit" variant="primary" isDisabled={importMutation.isPending}>
            {importMutation.isPending ? t("common.loading") : t("statements.importSubmit")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
