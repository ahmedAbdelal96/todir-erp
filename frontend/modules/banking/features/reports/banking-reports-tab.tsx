import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import {
  useBankActivity,
  useTransactionCodeMappings,
  useCreateTransactionCodeMapping
} from "../../hooks/use-banking-queries.ts";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Table, TableHeader, TableBody, TableRow, TableHead, TableCell } from "../../../../components/primitives/table.tsx";
import { BankingMoney } from "../../components/banking-money.tsx";
import { StatusBadge } from "../../../../components/fields/status-badge.tsx";
import { BdiText } from "../../../../components/formatting/bidi-text.tsx";
import { formatDate } from "../../../../components/formatting/date-formatter.ts";

interface Props {
  readonly legalEntityId: string;
}

export function BankingReportsTab({ legalEntityId }: Props): React.JSX.Element {
  const { t } = useTranslation("banking");
  const [activeSubTab, setActiveSubTab] = useState<"activity" | "mappings">("activity");

  // Filters for activity
  const [directionFilter, setDirectionFilter] = useState<string>("");
  const [classificationFilter, setClassificationFilter] = useState<string>("");

  const { data: activityData, isLoading: isActivityLoading } = useBankActivity(legalEntityId, {
    ...(directionFilter ? { direction: directionFilter } : {}),
    ...(classificationFilter ? { classification: classificationFilter } : {})
  });

  const { data: mappingsData, isLoading: isMappingsLoading } = useTransactionCodeMappings(legalEntityId);
  const createMappingMutation = useCreateTransactionCodeMapping(legalEntityId);

  // Mapping modal state
  const [isNewMappingOpen, setIsNewMappingOpen] = useState(false);
  const [newCode, setNewCode] = useState("");
  const [newClassification, setNewClassification] = useState("BankFee");
  const today = new Date().toISOString().slice(0, 10);
  const [effectiveFrom, setEffectiveFrom] = useState(today);

  const handleCreateMappingSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newCode.trim()) return;
    try {
      await createMappingMutation.mutateAsync({
        transactionCode: newCode.trim(),
        classification: newClassification,
        effectiveFrom: effectiveFrom || today
      });
      setIsNewMappingOpen(false);
      setNewCode("");
    } catch {
      // ignore
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Header bar */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
        <div>
          <h2 style={{ margin: 0, fontSize: "1.25rem", color: "var(--color-brand-primary)" }}>{t("reports.title")}</h2>
          <p style={{ margin: "0.25rem 0 0", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>{t("reports.subtitle")}</p>
        </div>
        {activeSubTab === "mappings" && (
          <Button variant="primary" onClick={() => setIsNewMappingOpen(true)}>
            {t("reports.newMappingButton")}
          </Button>
        )}
      </div>

      {/* Sub-tab navigation */}
      <div style={{ display: "flex", gap: "0.5rem" }}>
        <Button
          variant={activeSubTab === "activity" ? "primary" : "ghost"}
          size="compact"
          onClick={() => setActiveSubTab("activity")}
        >
          {t("reports.bankActivityTab")}
        </Button>
        <Button
          variant={activeSubTab === "mappings" ? "primary" : "ghost"}
          size="compact"
          onClick={() => setActiveSubTab("mappings")}
        >
          {t("reports.codeMappingsTab")}
        </Button>
      </div>

      {/* Filters (in activity view) */}
      {activeSubTab === "activity" && (
        <Surface style={{ padding: "1rem" }}>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "1rem" }}>
            <div>
              <Select
                label={t("reports.directionFilter")}
                selectedKey={directionFilter}
                onSelectionChange={(key) => setDirectionFilter(key ? String(key) : "")}
                options={[
                  { id: "", label: t("reports.allDirections") },
                  { id: "Credit", label: t("reports.credit") },
                  { id: "Debit", label: t("reports.debit") }
                ]}
              />
            </div>
            <div>
              <Select
                label={t("statements.classification")}
                selectedKey={classificationFilter}
                onSelectionChange={(key) => setClassificationFilter(key ? String(key) : "")}
                options={[
                  { id: "", label: t("reports.allDirections") },
                  { id: "BankFee", label: t("statements.classifications.BankFee") },
                  { id: "InterestIncome", label: t("statements.classifications.InterestIncome") },
                  { id: "InterestCharge", label: t("statements.classifications.InterestCharge") },
                  { id: "DirectDebit", label: t("statements.classifications.DirectDebit") },
                  { id: "UnknownReceipt", label: t("statements.classifications.UnknownReceipt") }
                ]}
              />
            </div>
          </div>
        </Surface>
      )}

      {/* Main Table Surface */}
      <Surface style={{ padding: "1.25rem" }}>
        {activeSubTab === "activity" ? (
          isActivityLoading ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
              {t("common.loading")}
            </div>
          ) : (activityData?.items ?? []).length === 0 ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
              {t("common.noData")}
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t("statements.bookingDate")}</TableHead>
                  <TableHead>{t("statements.direction")}</TableHead>
                  <TableHead align="end">{t("statements.amount")}</TableHead>
                  <TableHead align="end">{t("statements.claimedAmount")}</TableHead>
                  <TableHead align="end">{t("statements.remainingAmount")}</TableHead>
                  <TableHead>{t("statements.reference")}</TableHead>
                  <TableHead>{t("statements.transactionCode")}</TableHead>
                  <TableHead>{t("statements.classification")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {activityData?.items.map((row) => (
                  <TableRow key={row.statementLineId}>
                    <TableCell><BdiText>{formatDate(row.bookingDate)}</BdiText></TableCell>
                    <TableCell>
                      <StatusBadge
                        intent={row.direction === "Credit" ? "success" : "neutral"}
                        label={row.direction === "Credit" ? t("reports.credit") : t("reports.debit")}
                      />
                    </TableCell>
                    <TableCell align="end"><BankingMoney amount={row.amount} currency={row.currencyCode} /></TableCell>
                    <TableCell align="end"><BankingMoney amount={row.claimedAmount} currency={row.currencyCode} /></TableCell>
                    <TableCell align="end" style={{ fontWeight: 600 }}><BankingMoney amount={row.remainingAmount} currency={row.currencyCode} /></TableCell>
                    <TableCell style={{ fontSize: "0.8125rem" }}>{row.reference || "—"}</TableCell>
                    <TableCell><BdiText>{row.transactionCode || "—"}</BdiText></TableCell>
                    <TableCell>
                      <StatusBadge intent="info" label={<BdiText>{row.classification}</BdiText>} />
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )
        ) : (
          isMappingsLoading ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
              {t("common.loading")}
            </div>
          ) : (mappingsData ?? []).length === 0 ? (
            <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
              {t("common.noData")}
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t("reports.transactionCode")}</TableHead>
                  <TableHead>{t("statements.classification")}</TableHead>
                  <TableHead>{t("reports.effectiveFrom")}</TableHead>
                  <TableHead>{t("reports.effectiveTo")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {mappingsData?.map((m) => (
                  <TableRow key={m.id}>
                    <TableCell style={{ fontWeight: 600 }}><BdiText>{m.transactionCode}</BdiText></TableCell>
                    <TableCell><BdiText>{m.classification}</BdiText></TableCell>
                    <TableCell><BdiText>{formatDate(m.effectiveFrom)}</BdiText></TableCell>
                    <TableCell>{m.effectiveTo ? <BdiText>{formatDate(m.effectiveTo)}</BdiText> : "—"}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )
        )}
      </Surface>

      {/* New Mapping Modal */}
      <Dialog isOpen={isNewMappingOpen} onClose={() => setIsNewMappingOpen(false)} title={t("reports.newMappingButton")}>
        <form onSubmit={handleCreateMappingSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem", minWidth: "320px", maxWidth: "440px" }}>
          <TextField
            label={t("reports.transactionCode")}
            value={newCode}
            onChange={(val) => setNewCode(val)}
            isRequired
            autoFocus
          />

          <div>
            <Select
              label={t("statements.classificationLabel")}
              selectedKey={newClassification}
              onSelectionChange={(key) => setNewClassification(key ? String(key) : "BankFee")}
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
            label={t("reports.effectiveFrom")}
            type="date"
            value={effectiveFrom}
            onChange={(val) => setEffectiveFrom(val)}
            isRequired
          />

          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem", borderTop: "1px solid var(--color-border-subtle)", paddingTop: "1rem" }}>
            <Button type="button" variant="secondary" onClick={() => setIsNewMappingOpen(false)}>
              {t("accounts.cancel")}
            </Button>
            <Button type="submit" variant="primary" isDisabled={createMappingMutation.isPending}>
              {createMappingMutation.isPending ? t("common.loading") : t("common.save")}
            </Button>
          </div>
        </form>
      </Dialog>
    </div>
  );
}
