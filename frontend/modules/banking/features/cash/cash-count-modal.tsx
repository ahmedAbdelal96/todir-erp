import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useCreateCashCount } from "../../hooks/use-banking-queries.ts";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "../../../../components/primitives/table.tsx";
import { BankingMoney } from "../../components/banking-money.tsx";

interface Props {
  readonly legalEntityId: string;
  readonly accountId: string;
  readonly currencyCode?: string;
  readonly isOpen: boolean;
  readonly onClose: () => void;
}

interface DenominationRow {
  denomination: string;
  unitValue: number;
  quantity: number;
}

const DEFAULT_DENOMINATIONS: readonly { denomination: string; unitValue: number }[] = [
  { denomination: "200", unitValue: 200 },
  { denomination: "100", unitValue: 100 },
  { denomination: "50", unitValue: 50 },
  { denomination: "20", unitValue: 20 },
  { denomination: "10", unitValue: 10 },
  { denomination: "5", unitValue: 5 },
  { denomination: "1", unitValue: 1 },
  { denomination: "0.50", unitValue: 0.5 }
];

export function CashCountModal({ legalEntityId, accountId, currencyCode = "EGP", isOpen, onClose }: Props): React.JSX.Element | null {
  const { t } = useTranslation("banking");
  const createMutation = useCreateCashCount(legalEntityId, accountId);

  const today = new Date().toISOString().slice(0, 10);
  const [cutoffDate, setCutoffDate] = useState(today);
  const [custodianId, setCustodianId] = useState("");
  const [expectedAmount, setExpectedAmount] = useState("5000");
  const [denominations, setDenominations] = useState<DenominationRow[]>(
    DEFAULT_DENOMINATIONS.map((d) => ({ ...d, quantity: 0 }))
  );
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  if (!isOpen) return null;

  const totalCounted = denominations.reduce((acc, d) => acc + d.unitValue * d.quantity, 0);
  const expectedNum = parseFloat(expectedAmount) || 0;
  const variance = Math.round((totalCounted - expectedNum) * 100) / 100;

  const handleQuantityChange = (index: number, val: string) => {
    const qty = parseInt(val, 10) || 0;
    setDenominations((prev) =>
      prev.map((item, i) => (i === index ? { ...item, quantity: Math.max(0, qty) } : item))
    );
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(null);

    const validCustodian = custodianId.trim() || "00000000-0000-0000-0000-000000000001";

    try {
      await createMutation.mutateAsync({
        cutoffDate,
        custodianId: validCustodian,
        expectedAmount: expectedNum,
        denominations: denominations
          .filter((d) => d.quantity > 0)
          .map((d) => ({
            denomination: d.denomination,
            unitValue: d.unitValue,
            quantity: d.quantity
          }))
      });
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.error");
      setErrorMsg(msg);
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("cash.countModalTitle")}>
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem", minWidth: "320px", maxWidth: "620px" }}>
        <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
          {t("cash.countModalSubtitle")}
        </p>

        {errorMsg && (
          <div role="alert" style={{ padding: "0.75rem", background: "var(--color-surface-danger)", color: "var(--color-status-danger)", borderRadius: "var(--radius-sm)", fontSize: "0.875rem" }}>
            {errorMsg}
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("cash.cutoffDate")}
            type="date"
            value={cutoffDate}
            onChange={(val) => setCutoffDate(val)}
            isRequired
          />
          <TextField
            label={t("cash.expectedAmountLabel")}
            type="number"
            value={expectedAmount}
            onChange={(val) => setExpectedAmount(val)}
            isRequired
          />
        </div>

        <TextField
          label={t("cash.custodian")}
          value={custodianId}
          onChange={(val) => setCustodianId(val)}
          placeholder={t("cash.custodianPlaceholder")}
        />

        {/* Denomination Grid */}
        <div style={{ maxHeight: "280px", overflowY: "auto", border: "1px solid var(--color-border-subtle)", borderRadius: "var(--radius-sm)" }}>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t("cash.denomination")}</TableHead>
                <TableHead align="end">{t("cash.unitValue")}</TableHead>
                <TableHead style={{ width: "120px" }}>{t("cash.quantity")}</TableHead>
                <TableHead align="end">{t("cash.lineTotal")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {denominations.map((row, index) => {
                const lineTotal = row.unitValue * row.quantity;
                return (
                  <TableRow key={row.denomination}>
                    <TableCell style={{ fontWeight: 600 }}>{row.denomination} {currencyCode}</TableCell>
                    <TableCell align="end">
                      <BankingMoney amount={row.unitValue} currency={currencyCode} />
                    </TableCell>
                    <TableCell>
                      <input
                        type="number"
                        min="0"
                        value={row.quantity || ""}
                        placeholder="0"
                        onChange={(e) => handleQuantityChange(index, e.target.value)}
                        style={{
                          width: "100%",
                          padding: "0.375rem 0.5rem",
                          border: "1px solid var(--color-border-default)",
                          borderRadius: "var(--radius-sm)",
                          textAlign: "center"
                        }}
                        aria-label={t("cash.countForDenom", { denom: row.denomination })}
                      />
                    </TableCell>
                    <TableCell align="end" style={{ fontWeight: 600 }}>
                      <BankingMoney amount={lineTotal} currency={currencyCode} />
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </div>

        {/* Summary Footer */}
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem", background: "var(--color-surface-page)", padding: "1rem", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-subtle)" }}>
          <div>
            <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>{t("cash.totalCountedLabel")}</span>
            <span style={{ fontSize: "1.25rem", fontWeight: 700, color: "var(--color-brand-primary)" }}>
              <BankingMoney amount={totalCounted} currency={currencyCode} />
            </span>
          </div>
          <div>
            <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>{t("cash.differenceLabel")}</span>
            <span style={{ fontSize: "1.25rem", fontWeight: 700, color: variance === 0 ? "#16a34a" : variance < 0 ? "var(--color-status-danger)" : "#2563eb" }}>
              <BankingMoney amount={variance} currency={currencyCode} />
            </span>
          </div>
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button type="button" variant="secondary" onClick={onClose} isDisabled={createMutation.isPending}>
            {t("accounts.cancel")}
          </Button>
          <Button type="submit" variant="primary" isDisabled={createMutation.isPending}>
            {createMutation.isPending ? t("common.loading") : t("cash.countSubmit")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
