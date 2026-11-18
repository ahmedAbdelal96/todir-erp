import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import {
  useTreasuryAccounts,
  useBankStatements,
  useValidateStatement
} from "../../hooks/use-banking-queries.ts";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "../../../../components/primitives/table.tsx";
import { BankingMoney } from "../../components/banking-money.tsx";
import { StatusBadge } from "../../../../components/fields/status-badge.tsx";
import { BdiText } from "../../../../components/formatting/bidi-text.tsx";
import { formatDate } from "../../../../components/formatting/date-formatter.ts";
import { ImportStatementModal } from "./import-statement-modal.tsx";
import { StatementLinesModal } from "./statement-lines-modal.tsx";
import type { StatementDto } from "../../types.ts";

interface Props {
  readonly legalEntityId: string;
}

export function StatementsTab({ legalEntityId }: Props): React.JSX.Element {
  const { t } = useTranslation("banking");

  const { data: accountsData } = useTreasuryAccounts(legalEntityId);
  const accounts = (accountsData?.items ?? []).filter((a) => a.status === "Active" || a.status === "Suspended");

  const [selectedAccountId, setSelectedAccountId] = useState<string>("");
  const activeAccountId = selectedAccountId || (accounts.length > 0 ? (accounts[0]?.id ?? "") : "");

  const [isImportOpen, setIsImportOpen] = useState(false);
  const [selectedStatement, setSelectedStatement] = useState<StatementDto | null>(null);

  const { data: statementsData, isLoading, refetch } = useBankStatements(legalEntityId, activeAccountId);
  const validateMutation = useValidateStatement(legalEntityId, activeAccountId);

  const handleValidate = async (stmt: StatementDto) => {
    try {
      await validateMutation.mutateAsync({ statementId: stmt.id });
      void refetch();
    } catch {
      // ignore
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Header bar */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
        <div>
          <h2 style={{ margin: 0, fontSize: "1.25rem", color: "var(--color-brand-primary)" }}>{t("statements.title")}</h2>
          <p style={{ margin: "0.25rem 0 0", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>{t("statements.subtitle")}</p>
        </div>
        <Button variant="primary" onClick={() => setIsImportOpen(true)} isDisabled={!activeAccountId}>
          {t("statements.importStatementButton")}
        </Button>
      </div>

      {/* Account Selector Surface */}
      <Surface style={{ padding: "1rem" }}>
        <div style={{ maxWidth: "360px" }}>
          <Select
            label={t("statements.selectAccount")}
            selectedKey={activeAccountId}
            onSelectionChange={(key) => setSelectedAccountId(key ? String(key) : "")}
            options={accounts.map((acc) => ({
              id: acc.id,
              label: `${acc.name} (${acc.code}) - ${acc.currencyCode || t("currencyPolicies.MultiCurrency")}`
            }))}
          />
        </div>
      </Surface>

      {/* Statements Table Surface */}
      <Surface style={{ padding: "1.25rem" }}>
        {isLoading ? (
          <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
            {t("common.loading")}
          </div>
        ) : (statementsData?.items ?? []).length === 0 ? (
          <div style={{ padding: "3rem", textAlign: "center" }}>
            <h3 style={{ margin: "0 0 0.5rem", color: "var(--color-brand-primary)" }}>{t("statements.emptyTitle")}</h3>
            <p style={{ color: "var(--color-content-muted)", margin: "0 0 1.5rem" }}>{t("statements.emptyDescription")}</p>
            <Button variant="primary" onClick={() => setIsImportOpen(true)} isDisabled={!activeAccountId}>
              {t("statements.importStatementButton")}
            </Button>
          </div>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t("statements.statementNumber")}</TableHead>
                <TableHead>{t("statements.openingDate")}</TableHead>
                <TableHead>{t("statements.closingDate")}</TableHead>
                <TableHead align="end">{t("statements.openingBalance")}</TableHead>
                <TableHead align="end">{t("statements.closingBalance")}</TableHead>
                <TableHead>{t("statements.lineCount")}</TableHead>
                <TableHead>{t("statements.processingState")}</TableHead>
                <TableHead>{t("statements.reconciliationState")}</TableHead>
                <TableHead align="end">{t("accounts.actions")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {statementsData?.items.map((stmt) => (
                <TableRow key={stmt.id}>
                  <TableCell style={{ fontWeight: 600 }}>
                    <BdiText>{stmt.statementNumber}</BdiText>
                  </TableCell>
                  <TableCell>
                    {stmt.openingDate ? <BdiText>{formatDate(stmt.openingDate)}</BdiText> : "—"}
                  </TableCell>
                  <TableCell>
                    {stmt.closingDate ? <BdiText>{formatDate(stmt.closingDate)}</BdiText> : "—"}
                  </TableCell>
                  <TableCell align="end">
                    <BankingMoney amount={stmt.openingBalance ?? null} currency={stmt.currencyCode} />
                  </TableCell>
                  <TableCell align="end">
                    <BankingMoney amount={stmt.closingBalance ?? null} currency={stmt.currencyCode} />
                  </TableCell>
                  <TableCell>
                    <BdiText>{stmt.lineCount}</BdiText>
                  </TableCell>
                  <TableCell>
                    <StatusBadge
                      intent={stmt.processingState === "Validated" ? "success" : "neutral"}
                      label={stmt.processingState}
                    />
                  </TableCell>
                  <TableCell>
                    <StatusBadge
                      intent={stmt.reconciliationState === "Finalized" ? "success" : "warning"}
                      label={stmt.reconciliationState}
                    />
                  </TableCell>
                  <TableCell align="end">
                    <div style={{ display: "flex", gap: "0.375rem", justifyContent: "flex-end" }}>
                      <Button
                        size="compact"
                        variant="secondary"
                        onClick={() => setSelectedStatement(stmt)}
                      >
                        {t("statements.viewLines")}
                      </Button>
                      {stmt.processingState !== "Validated" && (
                        <Button
                          size="compact"
                          variant="ghost"
                          onClick={() => handleValidate(stmt)}
                          isDisabled={validateMutation.isPending}
                        >
                          {t("statements.validateAction")}
                        </Button>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </Surface>

      {/* Modals */}
      <ImportStatementModal
        legalEntityId={legalEntityId}
        accountId={activeAccountId}
        isOpen={isImportOpen}
        onClose={() => setIsImportOpen(false)}
      />

      <StatementLinesModal
        legalEntityId={legalEntityId}
        statement={selectedStatement}
        isOpen={Boolean(selectedStatement)}
        onClose={() => setSelectedStatement(null)}
      />
    </div>
  );
}
