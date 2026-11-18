import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useImportStatement } from "../../hooks/use-banking-queries.ts";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "../../../../components/primitives/table.tsx";
import type { ImportStatementLineRequest } from "../../types.ts";

interface Props {
  readonly legalEntityId: string;
  readonly accountId: string;
  readonly defaultCurrency?: string;
  readonly isOpen: boolean;
  readonly onClose: () => void;
}

interface EditableStatementLine {
  id: string;
  externalLineId: string;
  bookingDate: string;
  valueDate: string;
  direction: "Credit" | "Debit";
  amount: string;
  counterpartyName: string;
  transactionCode: string;
  reference: string;
}

export function ImportStatementModal({
  legalEntityId,
  accountId,
  defaultCurrency = "EGP",
  isOpen,
  onClose
}: Props): React.JSX.Element | null {
  const { t } = useTranslation("banking");
  const importMutation = useImportStatement(legalEntityId, accountId);

  const today = new Date().toISOString().slice(0, 10);
  const thirtyDaysAgo = new Date(Date.now() - 30 * 86400000).toISOString().slice(0, 10);
  const [mode, setMode] = useState<"Manual" | "CSV">("Manual");
  const [statementNumber, setStatementNumber] = useState("");
  const [openingDate, setOpeningDate] = useState(thirtyDaysAgo);
  const [closingDate, setClosingDate] = useState(today);
  const [currencyCode, setCurrencyCode] = useState(defaultCurrency);
  const [openingBalance, setOpeningBalance] = useState("100000");
  const [closingBalance, setClosingBalance] = useState("125000");
  const [sourceReference, setSourceReference] = useState("MT940-BATCH");

  // Manual statement lines
  const [lines, setLines] = useState<EditableStatementLine[]>([
    {
      id: "line-1",
      externalLineId: "",
      bookingDate: today,
      valueDate: today,
      direction: "Credit",
      amount: "",
      counterpartyName: "",
      transactionCode: "DEP",
      reference: ""
    }
  ]);

  // CSV input
  const [csvText, setCsvText] = useState("");
  const [csvParsedLines, setCsvParsedLines] = useState<ImportStatementLineRequest[]>([]);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleAddLine = () => {
    setLines((prev) => [
      ...prev,
      {
        id: `line-${prev.length + 1}`,
        externalLineId: "",
        bookingDate: closingDate || today,
        valueDate: closingDate || today,
        direction: "Credit",
        amount: "",
        counterpartyName: "",
        transactionCode: "DEP",
        reference: ""
      }
    ]);
  };

  const handleRemoveLine = (id: string) => {
    setLines((prev) => (prev.length > 1 ? prev.filter((l) => l.id !== id) : prev));
  };

  const handleLineChange = (id: string, field: keyof EditableStatementLine, value: string) => {
    setLines((prev) =>
      prev.map((l) => (l.id === id ? { ...l, [field]: value } : l))
    );
  };

  const handleParseCsv = () => {
    setErrorMsg(null);
    if (!csvText.trim()) {
      setErrorMsg(t("common.validationError"));
      return;
    }

    const rows = csvText
      .trim()
      .split("\n")
      .map((r) => r.trim())
      .filter((r) => r.length > 0);

    const parsed: ImportStatementLineRequest[] = [];
    for (let i = 0; i < rows.length; i++) {
      const row = rows[i]!;
      // Skip header if first row starts with letters
      if (i === 0 && row.toLowerCase().includes("externallineid")) continue;

      const cols = row.split(",").map((c) => c.trim().replace(/^["']|["']$/g, ""));
      if (cols.length < 4) continue;

      const [extId, date, dir, amt, counterparty, ref, code] = cols;
      const parsedAmount = parseFloat(amt || "0");
      if (!extId || isNaN(parsedAmount) || parsedAmount <= 0) continue;

      const normDir = dir?.toLowerCase() === "debit" || dir?.toLowerCase() === "dr" ? "Debit" : "Credit";
      parsed.push({
        externalLineId: extId,
        bookingDate: date || closingDate || today,
        valueDate: date || closingDate || today,
        direction: normDir,
        amount: parsedAmount,
        currencyCode: currencyCode.trim(),
        reference: ref || null,
        transactionCode: code || (normDir === "Credit" ? "CR-DEP" : "DB-WD"),
        counterpartyName: counterparty || null,
        rawPayload: JSON.stringify({ csvRow: row })
      });
    }

    if (parsed.length === 0) {
      setErrorMsg(t("statements.csvParseFailed"));
      return;
    }

    setCsvParsedLines(parsed);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(null);

    if (!statementNumber.trim()) {
      setErrorMsg(t("common.validationError"));
      return;
    }

    let finalLines: ImportStatementLineRequest[] = [];

    if (mode === "Manual") {
      for (const l of lines) {
        const amt = parseFloat(l.amount);
        if (!l.externalLineId.trim() || isNaN(amt) || amt <= 0) {
          setErrorMsg(t("statements.lineValidationError"));
          return;
        }
        finalLines.push({
          externalLineId: l.externalLineId.trim(),
          bookingDate: l.bookingDate || today,
          valueDate: l.valueDate || l.bookingDate || today,
          direction: l.direction,
          amount: amt,
          currencyCode: currencyCode.trim(),
          reference: l.reference.trim() || null,
          transactionCode: l.transactionCode.trim() || (l.direction === "Credit" ? "CR-DEP" : "DB-WD"),
          counterpartyName: l.counterpartyName.trim() || null,
          rawPayload: JSON.stringify({
            entryType: "ManualStatementEntry",
            enteredAt: new Date().toISOString()
          })
        });
      }
    } else {
      if (csvParsedLines.length === 0) {
        setErrorMsg(t("statements.csvNoLines"));
        return;
      }
      finalLines = csvParsedLines;
    }

    try {
      await importMutation.mutateAsync({
        request: {
          statementNumber: statementNumber.trim(),
          openingDate: openingDate || null,
          closingDate: closingDate || null,
          currencyCode: currencyCode.trim(),
          openingBalance: openingBalance ? parseFloat(openingBalance) : null,
          closingBalance: closingBalance ? parseFloat(closingBalance) : null,
          sourceType: mode === "Manual" ? "ManualEntry" : "CSV",
          sourceReference: sourceReference.trim() || null,
          lines: finalLines
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
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem", minWidth: "360px", maxWidth: "720px" }}>
        {errorMsg && (
          <div role="alert" style={{ padding: "0.75rem", background: "var(--color-surface-danger)", color: "var(--color-status-danger)", borderRadius: "var(--radius-sm)", fontSize: "0.875rem" }}>
            {errorMsg}
          </div>
        )}

        {/* Import Mode Tabs */}
        <div style={{ display: "flex", gap: "0.5rem", borderBottom: "1px solid var(--color-border-subtle)", paddingBottom: "0.5rem" }}>
          <Button
            type="button"
            variant={mode === "Manual" ? "primary" : "secondary"}
            onClick={() => setMode("Manual")}
          >
            {t("statements.manualEntryTab")}
          </Button>
          <Button
            type="button"
            variant={mode === "CSV" ? "primary" : "secondary"}
            onClick={() => setMode("CSV")}
          >
            {t("statements.csvImportTab")}
          </Button>
        </div>

        {/* Statement Header Fields */}
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
            onChange={(val) => setCurrencyCode(val.toUpperCase())}
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

        <div>
          <TextField
            label={t("statements.sourceReferenceLabel")}
            value={sourceReference}
            onChange={(val) => setSourceReference(val)}
          />
        </div>

        {/* Mode-specific Body */}
        {mode === "Manual" ? (
          <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem", borderTop: "1px solid var(--color-border-subtle)", paddingTop: "0.75rem" }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <span style={{ fontWeight: 600, fontSize: "0.875rem", color: "var(--color-brand-primary)" }}>
                {t("statements.statementLinesTitle")} ({lines.length})
              </span>
              <Button type="button" variant="secondary" onClick={handleAddLine}>
                + {t("statements.addLineButton")}
              </Button>
            </div>

            <div style={{ maxHeight: "240px", overflowY: "auto", border: "1px solid var(--color-border-subtle)", borderRadius: "var(--radius-sm)" }}>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>{t("statements.externalLineId")}</TableHead>
                    <TableHead>{t("statements.bookingDate")}</TableHead>
                    <TableHead>{t("statements.direction")}</TableHead>
                    <TableHead>{t("statements.amount")}</TableHead>
                    <TableHead>{t("statements.counterparty")}</TableHead>
                    <TableHead>{t("statements.reference")}</TableHead>
                    <TableHead>{t("accounts.actions")}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {lines.map((l) => (
                    <TableRow key={l.id}>
                      <TableCell>
                        <input
                          type="text"
                          value={l.externalLineId}
                          placeholder={t("statements.externalLineId")}
                          onChange={(e) => handleLineChange(l.id, "externalLineId", e.target.value)}
                          style={{ width: "100px", padding: "0.25rem", border: "1px solid var(--color-border-subtle)", borderRadius: "var(--radius-sm)" }}
                          required
                        />
                      </TableCell>
                      <TableCell>
                        <input
                          type="date"
                          value={l.bookingDate}
                          onChange={(e) => handleLineChange(l.id, "bookingDate", e.target.value)}
                          style={{ width: "120px", padding: "0.25rem", border: "1px solid var(--color-border-subtle)", borderRadius: "var(--radius-sm)" }}
                        />
                      </TableCell>
                      <TableCell>
                        <select
                          value={l.direction}
                          onChange={(e) => handleLineChange(l.id, "direction", e.target.value as "Credit" | "Debit")}
                          style={{ padding: "0.25rem", border: "1px solid var(--color-border-subtle)", borderRadius: "var(--radius-sm)" }}
                        >
                          <option value="Credit">{t("reports.credit")}</option>
                          <option value="Debit">{t("reports.debit")}</option>
                        </select>
                      </TableCell>
                      <TableCell>
                        <input
                          type="number"
                          step="0.01"
                          value={l.amount}
                          placeholder="0.00"
                          onChange={(e) => handleLineChange(l.id, "amount", e.target.value)}
                          style={{ width: "90px", padding: "0.25rem", border: "1px solid var(--color-border-subtle)", borderRadius: "var(--radius-sm)" }}
                          required
                        />
                      </TableCell>
                      <TableCell>
                        <input
                          type="text"
                          value={l.counterpartyName}
                          placeholder={t("statements.counterparty")}
                          onChange={(e) => handleLineChange(l.id, "counterpartyName", e.target.value)}
                          style={{ width: "110px", padding: "0.25rem", border: "1px solid var(--color-border-subtle)", borderRadius: "var(--radius-sm)" }}
                        />
                      </TableCell>
                      <TableCell>
                        <input
                          type="text"
                          value={l.reference}
                          placeholder={t("statements.reference")}
                          onChange={(e) => handleLineChange(l.id, "reference", e.target.value)}
                          style={{ width: "110px", padding: "0.25rem", border: "1px solid var(--color-border-subtle)", borderRadius: "var(--radius-sm)" }}
                        />
                      </TableCell>
                      <TableCell>
                        {lines.length > 1 && (
                          <Button type="button" variant="ghost" onClick={() => handleRemoveLine(l.id)}>
                            ✕
                          </Button>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          </div>
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem", borderTop: "1px solid var(--color-border-subtle)", paddingTop: "0.75rem" }}>
            <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
              {t("statements.csvFormatGuide")}
              <code>{t("statements.csvFormatColumns")}</code>
            </div>
            <textarea
              rows={5}
              value={csvText}
              onChange={(e) => setCsvText(e.target.value)}
              placeholder={t("statements.csvPlaceholder")}
              style={{
                width: "100%",
                padding: "0.5rem",
                fontFamily: "monospace",
                fontSize: "0.8125rem",
                border: "1px solid var(--color-border-subtle)",
                borderRadius: "var(--radius-sm)",
                backgroundColor: "var(--color-surface-page)"
              }}
            />
            <div style={{ display: "flex", justifyContent: "flex-end" }}>
              <Button type="button" variant="secondary" onClick={handleParseCsv}>
                {t("statements.parseCsvButton")}
              </Button>
            </div>
            {csvParsedLines.length > 0 && (
              <div style={{ fontSize: "0.875rem", color: "var(--color-status-success)", fontWeight: 500 }}>
                ✓ {csvParsedLines.length} {t("statements.linesParsedSuccess")}
              </div>
            )}
          </div>
        )}

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
