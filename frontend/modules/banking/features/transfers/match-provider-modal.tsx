import React, { useState, useEffect, useMemo } from "react";
import { useTranslation } from "react-i18next";
import {
  useBankStatements,
  useStatementLines,
  useMatchProviderSettlement,
  useTreasuryAccounts
} from "../../hooks/use-banking-queries.ts";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "../../../../components/primitives/table.tsx";
import { BankingMoney } from "../../components/banking-money.tsx";
import { StatusBadge } from "../../../../components/fields/status-badge.tsx";
import { BdiText } from "../../../../components/formatting/bidi-text.tsx";
import { formatDate } from "../../../../components/formatting/date-formatter.ts";
import type { ProviderSettlementDto, StatementLineDto } from "../../types.ts";

interface Props {
  readonly legalEntityId: string;
  readonly settlement: ProviderSettlementDto | null;
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly onMatched?: () => void;
}

export function MatchProviderModal({
  legalEntityId,
  settlement,
  isOpen,
  onClose,
  onMatched
}: Props): React.JSX.Element | null {
  const { t } = useTranslation("banking");
  const matchMutation = useMatchProviderSettlement(legalEntityId);

  const [selectedBankAccountId, setSelectedBankAccountId] = useState<string>("");
  const [selectedStatementId, setSelectedStatementId] = useState<string>("");
  const [selectedLineId, setSelectedLineId] = useState<string>("");
  const [manualLineId, setManualLineId] = useState<string>("");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  // Fetch bank accounts for the entity to find where the payout was deposited
  const { data: accountsData } = useTreasuryAccounts(legalEntityId, { pageSize: 100 });
  const bankAccounts = useMemo(() => {
    return (accountsData?.items ?? []).filter((a) => a.type === "Bank" && a.status === "Active");
  }, [accountsData]);

  // Default to the bank account matching the settlement currency, or first active bank
  useEffect(() => {
    if (bankAccounts.length > 0 && !selectedBankAccountId) {
      const match = bankAccounts.find((a) => a.currencyCode === settlement?.currencyCode) ?? bankAccounts[0];
      if (match) {
        setSelectedBankAccountId(match.id);
      }
    }
  }, [bankAccounts, selectedBankAccountId, settlement]);

  // Fetch statements for the chosen bank account
  const { data: statementsData, isLoading: isStatementsLoading } = useBankStatements(
    legalEntityId,
    selectedBankAccountId,
    1,
    50
  );
  const statements = useMemo(() => statementsData?.items ?? [], [statementsData]);

  // Default to first statement when statements load
  useEffect(() => {
    if (statements.length > 0 && !selectedStatementId) {
      const first = statements[0];
      if (first) {
        setSelectedStatementId(first.id);
      }
    }
  }, [statements, selectedStatementId]);

  // Fetch lines for the chosen statement
  const { data: linesData, isLoading: isLinesLoading } = useStatementLines(
    legalEntityId,
    selectedStatementId,
    1,
    100
  );
  const allLines = useMemo(() => linesData?.items ?? [], [linesData]);

  // Filter eligible lines: same currency, Credit direction, and available amount covers net payout
  const eligibleLines = useMemo(() => {
    if (!settlement) return [];
    return allLines.filter((line: StatementLineDto) => {
      const isSameCurrency = line.currencyCode === settlement.currencyCode;
      const remainingAmount = Number(line.amount) - (Number(line.claimedAmount) || 0);
      return isSameCurrency && remainingAmount >= Number(settlement.netPayoutAmount);
    });
  }, [allLines, settlement]);

  // Reset state on open
  useEffect(() => {
    if (isOpen) {
      setErrorMsg(null);
      setSelectedLineId("");
      setManualLineId("");
    }
  }, [isOpen]);

  if (!isOpen || !settlement) return null;

  const activeLineId = manualLineId.trim() || selectedLineId;
  const isMatchDisabled = !activeLineId || matchMutation.isPending || settlement.status === "Matched";

  const handleSelectLine = (line: StatementLineDto) => {
    setSelectedLineId(line.id);
    setManualLineId("");
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMsg(null);

    if (!activeLineId) {
      setErrorMsg(t("common.validationError"));
      return;
    }

    try {
      await matchMutation.mutateAsync({
        settlementId: settlement.id,
        request: {
          statementLineId: activeLineId,
          version: settlement.version
        }
      });
      onMatched?.();
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.error");
      setErrorMsg(msg);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={`${t("transfers.matchProviderModalTitle", "مطابقة تسوية بوابة الدفع مع كشف البنك")} — ${settlement.providerCode}`}
    >
      <form
        onSubmit={handleSubmit}
        style={{
          display: "flex",
          flexDirection: "column",
          gap: "1.25rem",
          minWidth: "340px",
          maxWidth: "min(96vw, 680px)"
        }}
      >
        {errorMsg && (
          <div
            role="alert"
            data-testid="match-provider-error"
            style={{
              padding: "0.75rem",
              background: "var(--color-surface-danger)",
              color: "var(--color-status-danger)",
              borderRadius: "var(--radius-sm)",
              fontSize: "0.875rem"
            }}
          >
            {errorMsg}
          </div>
        )}

        {/* Settlement Summary Card */}
        <div
          style={{
            padding: "1rem",
            backgroundColor: "var(--color-surface-subtle)",
            borderRadius: "var(--radius-md)",
            border: "1px solid var(--color-border-subtle)",
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(140px, 1fr))",
            gap: "0.75rem",
            fontSize: "0.875rem"
          }}
        >
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block", fontSize: "0.75rem" }}>
              {t("transfers.providerCode")}
            </span>
            <strong>{settlement.providerCode}</strong>
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block", fontSize: "0.75rem" }}>
              {t("transfers.providerRef")}
            </span>
            <BdiText>{settlement.providerReference}</BdiText>
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block", fontSize: "0.75rem" }}>
              {t("transfers.grossAmount")}
            </span>
            <BankingMoney amount={settlement.grossAmount} currency={settlement.currencyCode} />
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block", fontSize: "0.75rem" }}>
              {t("transfers.feesAmount")}
            </span>
            <BankingMoney amount={settlement.feesAmount} currency={settlement.currencyCode} />
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block", fontSize: "0.75rem" }}>
              {t("transfers.netPayout")}
            </span>
            <strong style={{ color: "var(--color-brand-primary)", fontSize: "1rem" }}>
              <BankingMoney amount={settlement.netPayoutAmount} currency={settlement.currencyCode} />
            </strong>
          </div>
          <div>
            <span style={{ color: "var(--color-content-muted)", display: "block", fontSize: "0.75rem" }}>
              {t("accounts.status")}
            </span>
            <StatusBadge
              intent={settlement.status === "Matched" ? "success" : "warning"}
              label={<BdiText>{settlement.status}</BdiText>}
            />
          </div>
        </div>

        {/* Target Bank Account Selector */}
        <div>
          <Select
            data-testid="match-provider-bank-account"
            label={t("transfers.targetBankAccount", "الحساب البنكي المستلم للدفعة")}
            selectedKey={selectedBankAccountId}
            onSelectionChange={(key) => {
              setSelectedBankAccountId(key ? String(key) : "");
              setSelectedStatementId("");
              setSelectedLineId("");
            }}
            options={bankAccounts.map((acc) => ({
              id: acc.id,
              label: `${acc.name} (${acc.code}) - ${acc.currencyCode || ""}`
            }))}
            isDisabled={bankAccounts.length === 0}
          />
        </div>

        {/* Bank Statement Selector */}
        <div>
          <Select
            label={t("transfers.selectStatementLabel", "اختر كشف الحساب البنكي المرتبط")}
            selectedKey={selectedStatementId}
            onSelectionChange={(key) => {
              setSelectedStatementId(key ? String(key) : "");
              setSelectedLineId("");
            }}
            options={statements.map((s) => ({
              id: s.id,
              label: `${s.statementNumber} (${formatDate(s.openingDate)} — ${formatDate(s.closingDate)})`
            }))}
            isDisabled={isStatementsLoading || statements.length === 0}
          />
          {statements.length === 0 && !isStatementsLoading && (
            <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
              {t("transfers.noStatementsForAccount", "لا توجد كشوف حسابات مسجلة لهذا الحساب البنكي.")}
            </p>
          )}
        </div>

        {/* Candidate Statement Lines Table */}
        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 500, marginBottom: "0.5rem" }}>
            {t("transfers.availableLinesTitle", "حركات الإيداع البنكية المتاحة للمطابقة")}
          </label>

          {isLinesLoading ? (
            <div style={{ padding: "1.5rem", textAlign: "center", color: "var(--color-content-muted)" }}>
              {t("common.loading")}
            </div>
          ) : eligibleLines.length === 0 ? (
            <div
              style={{
                padding: "1rem",
                textAlign: "center",
                backgroundColor: "var(--color-surface-subtle)",
                borderRadius: "var(--radius-sm)",
                color: "var(--color-content-muted)",
                fontSize: "0.875rem"
              }}
            >
              {t(
                "transfers.noEligibleLines",
                "لا توجد حركات إيداع بنكية متوافقة في هذا الكشف تغطي صافي مبلغ التسوية."
              )}
            </div>
          ) : (
            <div style={{ maxHeight: "200px", overflowY: "auto", border: "1px solid var(--color-border-subtle)", borderRadius: "var(--radius-sm)" }}>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead style={{ width: "40px" }}></TableHead>
                    <TableHead>{t("statements.bookingDate", "التاريخ")}</TableHead>
                    <TableHead>{t("transfers.bankRef", "المرجع")}</TableHead>
                    <TableHead align="end">{t("statements.amount", "المبلغ")}</TableHead>
                    <TableHead align="end">{t("transfers.availableAmount", "المتاح")}</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {eligibleLines.map((line: StatementLineDto) => {
                    const remaining = Number(line.amount) - (Number(line.claimedAmount) || 0);
                    const isSelected = selectedLineId === line.id;
                    return (
                      <TableRow
                        key={line.id}
                        data-testid={`candidate-line-${line.id}`}
                        onClick={() => handleSelectLine(line)}
                        style={{
                          cursor: "pointer",
                          backgroundColor: isSelected ? "var(--color-surface-primary-subtle, rgba(0,100,200,0.08))" : undefined
                        }}
                      >
                        <TableCell>
                          <input
                            type="radio"
                            name="statementLineSelection"
                            checked={isSelected}
                            onChange={() => handleSelectLine(line)}
                            aria-label={`Select line ${line.reference || line.id}`}
                          />
                        </TableCell>
                        <TableCell><BdiText>{formatDate(line.bookingDate)}</BdiText></TableCell>
                        <TableCell><BdiText>{line.reference || line.externalLineId || "—"}</BdiText></TableCell>
                        <TableCell align="end">
                          <BankingMoney amount={line.amount} currency={line.currencyCode} />
                        </TableCell>
                        <TableCell align="end" style={{ fontWeight: 600 }}>
                          <BankingMoney amount={remaining} currency={line.currencyCode} />
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </div>
          )}
        </div>

        {/* Selected / Manual Line ID field */}
        <TextField
          label={t("transfers.statementLineIdLabel", "معرّف حركة كشف الحساب")}
          value={activeLineId}
          onChange={(val) => {
            setManualLineId(val);
            if (val !== selectedLineId) setSelectedLineId("");
          }}
          placeholder="00000000-0000-0000-0000-000000000000"
          isRequired
        />

        {/* Modal Actions */}
        <div
          style={{
            display: "flex",
            justifyContent: "flex-end",
            gap: "0.75rem",
            marginTop: "0.5rem",
            borderTop: "1px solid var(--color-border-subtle)",
            paddingTop: "1rem"
          }}
        >
          <Button type="button" variant="secondary" onClick={onClose} isDisabled={matchMutation.isPending}>
            {t("accounts.cancel")}
          </Button>
          <Button
            type="submit"
            variant="primary"
            data-testid="submit-match-provider"
            isDisabled={isMatchDisabled}
          >
            {matchMutation.isPending ? t("common.loading") : t("transfers.confirmMatch", "تأكيد مطابقة التسوية")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
