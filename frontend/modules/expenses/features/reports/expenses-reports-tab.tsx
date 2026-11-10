import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Table } from "../../../../components/primitives/table.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Pagination } from "../../../../components/primitives/pagination.tsx";
import { useExpenseSpendReportQuery } from "../../hooks/use-expenses-queries.ts";
import { useIsMobile } from "../../hooks/use-is-mobile.ts";
import { expensesApi } from "../../services/expenses-api.ts";

export interface ExpensesReportsTabProps {
  readonly legalEntityId: string;
}

export function ExpensesReportsTab({ legalEntityId }: ExpensesReportsTabProps): React.JSX.Element {
  const { t } = useTranslation("expenses");
  const isMobile = useIsMobile(768);

  const [groupBy, setGroupBy] = useState<"category" | "employee" | "source">("category");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);

  const reportQuery = useExpenseSpendReportQuery(legalEntityId, {
    groupBy,
    from: from || null,
    to: to || null,
    search: search.trim() || null,
    page,
    pageSize: 15
  });

  const handleExportCsv = () => {
    const url = expensesApi.exportReportsCsvUrl(legalEntityId);
    window.open(url, "_blank");
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      <Surface style={{ padding: "1.25rem", display: "flex", flexDirection: "column", gap: "1.25rem" }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
          <div>
            <h2 style={{ margin: 0, fontSize: "1.125rem", color: "var(--color-content-primary, #0f172a)" }}>
              {t("reports.title")}
            </h2>
            <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>
              {t("reports.description")}
            </p>
          </div>

          <Button variant="secondary" onClick={handleExportCsv}>
            {t("reports.exportCsv")}
          </Button>
        </div>

        {/* Filters */}
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "1rem", alignItems: "flex-end" }}>
          <Select
            label={t("reports.groupByLabel")}
            selectedKey={groupBy}
            onSelectionChange={(key) => {
              if (key) {
                setGroupBy(String(key) as any);
                setPage(1);
              }
            }}
            options={[
              { id: "category", label: t("reports.groupByCategory") },
              { id: "employee", label: t("reports.groupByEmployee") },
              { id: "source", label: t("reports.groupBySource") }
            ]}
          />

          <TextField
            label={t("reports.fromDate")}
            type="date"
            value={from}
            onChange={(val) => {
              setFrom(val);
              setPage(1);
            }}
          />

          <TextField
            label={t("reports.toDate")}
            type="date"
            value={to}
            onChange={(val) => {
              setTo(val);
              setPage(1);
            }}
          />

          <TextField
            placeholder={t("review.searchPlaceholder")}
            value={search}
            onChange={(val) => {
              setSearch(val);
              setPage(1);
            }}
          />
        </div>

        {/* Spend Report Data Table / Mobile Cards */}
        {reportQuery.isLoading ? (
          <div style={{ padding: "3rem", textAlign: "center" }}>{t("common.loading")}</div>
        ) : !reportQuery.data?.items || reportQuery.data.items.length === 0 ? (
          <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
            {t("reports.empty")}
          </div>
        ) : (
          <>
            {isMobile ? (
              <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
                {reportQuery.data.items.map((row) => (
                  <div
                    key={`${row.groupBy}-${row.groupKey}`}
                    style={{
                      backgroundColor: "#ffffff",
                      border: "1px solid #cbd5e1",
                      borderRadius: "0.5rem",
                      padding: "0.875rem",
                      display: "flex",
                      flexDirection: "column",
                      gap: "0.5rem"
                    }}
                  >
                    <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                      <span style={{ fontWeight: 600, fontSize: "0.875rem" }}>
                        {row.groupKey || t("reports.unclassified")}
                      </span>
                      <span
                        style={{
                          padding: "0.125rem 0.5rem",
                          borderRadius: "var(--radius-full, 9999px)",
                          fontSize: "0.75rem",
                          backgroundColor: "#f1f5f9",
                          color: "#475569"
                        }}
                      >
                        {row.expenseCount} {t("reports.expenseCount")}
                      </span>
                    </div>

                    <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.375rem", fontSize: "0.8125rem" }}>
                      <div>
                        <span style={{ color: "var(--color-content-muted, #64748b)" }}>{t("reports.totalSpend")}: </span>
                        <bdi style={{ fontWeight: 700 }}>{row.originalAmount.toFixed(2)}</bdi>
                      </div>
                      <div>
                        <span style={{ color: "var(--color-content-muted, #64748b)" }}>{t("reports.totalBase")}: </span>
                        <bdi>{row.baseAmount.toFixed(2)}</bdi>
                      </div>
                      <div>
                        <span style={{ color: "var(--color-content-muted, #64748b)" }}>{t("reports.totalTax")}: </span>
                        <bdi>{row.taxAmount.toFixed(2)}</bdi>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <div style={{ width: "100%", overflowX: "auto" }}>
                <Table style={{ minWidth: "600px" }}>
                  <thead>
                    <tr>
                      <th>{t("reports.groupHeader")}</th>
                      <th>{t("reports.totalSpend")}</th>
                      <th>{t("reports.totalBase")}</th>
                      <th>{t("reports.totalTax")}</th>
                      <th>{t("reports.expenseCount")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {reportQuery.data.items.map((row) => (
                      <tr key={`${row.groupBy}-${row.groupKey}`}>
                        <td style={{ fontWeight: 600 }}>{row.groupKey || t("reports.unclassified")}</td>
                        <td style={{ fontWeight: 600 }}>
                          <bdi>{row.originalAmount.toFixed(2)}</bdi>
                        </td>
                        <td>
                          <bdi>{row.baseAmount.toFixed(2)}</bdi>
                        </td>
                        <td>
                          <bdi>{row.taxAmount.toFixed(2)}</bdi>
                        </td>
                        <td>{row.expenseCount}</td>
                      </tr>
                    ))}
                  </tbody>
                </Table>
              </div>
            )}

            {reportQuery.data.totalCount > 15 && (
              <div style={{ display: "flex", justifyContent: "center", marginTop: "1rem" }}>
                <Pagination
                  page={page}
                  totalPages={Math.ceil(reportQuery.data.totalCount / 15)}
                  onPageChange={setPage}
                />
              </div>
            )}
          </>
        )}
      </Surface>
    </div>
  );
}
