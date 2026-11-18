import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import {
  useStatementLines,
  useClassifyStatementLine,
  useCreatePaymentProposal
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
import type { StatementDto, StatementLineDto } from "../../types.ts";

interface Props {
  readonly legalEntityId: string;
  readonly statement: StatementDto | null;
  readonly isOpen: boolean;
  readonly onClose: () => void;
}

export function StatementLinesModal({ legalEntityId, statement, isOpen, onClose }: Props): React.JSX.Element | null {
  const { t } = useTranslation("banking");
  const [page] = useState(1);
  const pageSize = 20;

  const [selectedLineForClassify, setSelectedLineForClassify] = useState<StatementLineDto | null>(null);
  const [classification, setClassification] = useState("BankFee");
  const [explanation, setExplanation] = useState("");

  const { data, isLoading, refetch } = useStatementLines(legalEntityId, statement?.id ?? "", page, pageSize);
  const classifyMutation = useClassifyStatementLine(legalEntityId, statement?.id ?? "");
  const proposalMutation = useCreatePaymentProposal(legalEntityId, statement?.id ?? "");

  if (!isOpen || !statement) return null;

  const handleClassifySubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedLineForClassify) return;
    try {
      await classifyMutation.mutateAsync({
        lineId: selectedLineForClassify.id,
        request: {
          classification,
          explanation: explanation.trim() || null
        }
      });
      setSelectedLineForClassify(null);
      setExplanation("");
      void refetch();
    } catch {
      // ignore
    }
  };

  const handleCreateProposal = async (line: StatementLineDto) => {
    try {
      await proposalMutation.mutateAsync({
        lineId: line.id,
        request: {
          evidence: { lineReference: line.reference, amount: line.amount }
        }
      });
      void refetch();
    } catch {
      // ignore
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={`${t("statements.linesModalTitle")} — ${statement.statementNumber}`}>
      <div style={{ minWidth: "360px", width: "min(96vw, 1100px)", maxWidth: "1100px", display: "flex", flexDirection: "column", gap: "1rem" }}>
        {/* Classify Sub-Form */}
        {selectedLineForClassify && (
          <form onSubmit={handleClassifySubmit} style={{ padding: "1rem", background: "var(--color-surface-page)", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-brand-primary)" }}>
            <h4 style={{ margin: "0 0 0.75rem", fontSize: "0.9375rem" }}>
              {t("statements.classifyModalTitle")} — <BdiText>{selectedLineForClassify.externalLineId}</BdiText>
            </h4>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
              <div>
                <Select
                  label={t("statements.classificationLabel")}
                  selectedKey={classification}
                  onSelectionChange={(key) => setClassification(key ? String(key) : "BankFee")}
                  options={[
                    { id: "BankFee", label: t("statements.classifications.BankFee") },
                    { id: "InterestIncome", label: t("statements.classifications.InterestIncome") },
                    { id: "InterestCharge", label: t("statements.classifications.InterestCharge") },
                    { id: "DirectDebit", label: t("statements.classifications.DirectDebit") },
                    { id: "UnknownReceipt", label: t("statements.classifications.UnknownReceipt") }
                  ]}
                />
              </div>
              <TextField
                label={t("statements.explanationLabel")}
                value={explanation}
                onChange={(val) => setExplanation(val)}
              />
            </div>
            <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "0.75rem" }}>
              <Button type="button" size="compact" variant="ghost" onClick={() => setSelectedLineForClassify(null)}>
                {t("accounts.cancel")}
              </Button>
              <Button type="submit" size="compact" variant="primary" isDisabled={classifyMutation.isPending}>
                {classifyMutation.isPending ? t("common.loading") : t("statements.classifySubmit")}
              </Button>
            </div>
          </form>
        )}

        {/* Lines Table */}
        {isLoading ? (
          <div style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted)" }}>
            {t("common.loading")}
          </div>
        ) : (data?.items ?? []).length === 0 ? (
          <div style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted)" }}>
            {t("common.noData")}
          </div>
        ) : (
          <div style={{ overflowX: "auto", border: "1px solid var(--color-border-subtle)", borderRadius: "var(--radius-sm)" }}>
            <Table style={{ minWidth: "920px" }}>
              <TableHeader>
                <TableRow>
                  <TableHead style={{ minWidth: "120px", whiteSpace: "nowrap" }}>{t("statements.lineExternalId")}</TableHead>
                  <TableHead style={{ minWidth: "100px", whiteSpace: "nowrap" }}>{t("statements.bookingDate")}</TableHead>
                  <TableHead style={{ minWidth: "90px", whiteSpace: "nowrap" }}>{t("statements.direction")}</TableHead>
                  <TableHead align="end" style={{ minWidth: "120px", whiteSpace: "nowrap" }}>{t("statements.amount")}</TableHead>
                  <TableHead align="end" style={{ minWidth: "120px", whiteSpace: "nowrap" }}>{t("statements.claimedAmount")}</TableHead>
                  <TableHead style={{ minWidth: "110px", whiteSpace: "nowrap" }}>{t("statements.reference")}</TableHead>
                  <TableHead style={{ minWidth: "130px", whiteSpace: "nowrap" }}>{t("statements.classification")}</TableHead>
                  <TableHead align="end" style={{ minWidth: "190px", whiteSpace: "nowrap" }}>{t("accounts.actions")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data?.items.map((line) => (
                  <TableRow key={line.id}>
                    <TableCell style={{ fontWeight: 600, whiteSpace: "nowrap" }}>
                      <BdiText>{line.externalLineId}</BdiText>
                    </TableCell>
                    <TableCell style={{ whiteSpace: "nowrap" }}>
                      <BdiText>{formatDate(line.bookingDate)}</BdiText>
                    </TableCell>
                    <TableCell style={{ whiteSpace: "nowrap" }}>
                      <StatusBadge
                        intent={line.direction === "Credit" ? "success" : "neutral"}
                        label={line.direction === "Credit" ? t("reports.credit") : t("reports.debit")}
                      />
                    </TableCell>
                    <TableCell align="end" style={{ whiteSpace: "nowrap" }}>
                      <BankingMoney amount={line.amount} currency={line.currencyCode} />
                    </TableCell>
                    <TableCell align="end" style={{ whiteSpace: "nowrap" }}>
                      <BankingMoney amount={line.claimedAmount} currency={line.currencyCode} />
                    </TableCell>
                    <TableCell style={{ fontSize: "0.8125rem", whiteSpace: "nowrap" }}>{line.reference || "—"}</TableCell>
                    <TableCell style={{ whiteSpace: "nowrap" }}>
                      <StatusBadge
                        intent={line.classification !== "Unclassified" ? "info" : "neutral"}
                        label={line.classification}
                      />
                    </TableCell>
                    <TableCell align="end" style={{ whiteSpace: "nowrap" }}>
                      <div style={{ display: "flex", gap: "0.375rem", justifyContent: "flex-end" }}>
                        <Button
                          size="compact"
                          variant="secondary"
                          onClick={() => setSelectedLineForClassify(line)}
                        >
                          {t("statements.classifyButton")}
                        </Button>
                        <Button
                          size="compact"
                          variant="ghost"
                          onClick={() => handleCreateProposal(line)}
                          isDisabled={proposalMutation.isPending}
                        >
                          {t("statements.createProposalButton")}
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}

        <div style={{ display: "flex", justifyContent: "flex-end", borderTop: "1px solid var(--color-border-subtle)", paddingTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose}>{t("common.close")}</Button>
        </div>
      </div>
    </Dialog>
  );
}
