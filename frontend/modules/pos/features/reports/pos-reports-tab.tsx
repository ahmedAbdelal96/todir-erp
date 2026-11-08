import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "../../../../components/primitives/button.tsx";
import { BidiText } from "../../../../components/primitives/bidi-text.tsx";
import {
  useSalesReportQuery,
  usePaymentMixReportQuery,
  useCashDifferencesReportQuery,
  useDiscountSummaryReportQuery,
} from "../../hooks/use-pos-queries.ts";

export interface PosReportsTabProps {
  readonly legalEntityId: string;
}

export function PosReportsTab({ legalEntityId }: PosReportsTabProps): React.JSX.Element {
  const { t } = useTranslation("pos");
  const today = new Date().toISOString().slice(0, 10);
  const [fromDate, setFromDate] = useState<string>(today);
  const [toDate, setToDate] = useState<string>(today);
  const [reportType, setReportType] = useState<"sales" | "payment-mix" | "cash-differences" | "discounts">("sales");

  const salesQuery = useSalesReportQuery(legalEntityId, fromDate, toDate, 1, 50);
  const paymentMixQuery = usePaymentMixReportQuery(legalEntityId, fromDate, toDate);
  const cashDifferencesQuery = useCashDifferencesReportQuery(legalEntityId, 1, 50);
  const discountsQuery = useDiscountSummaryReportQuery(legalEntityId, fromDate, toDate);

  const salesList = (salesQuery.data as any)?.items || [];
  const cashDiffList = (cashDifferencesQuery.data as any)?.items || [];
  const paymentMix = paymentMixQuery.data;
  const discounts = discountsQuery.data;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      {/* Header Bar */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
        <div>
          <h2 style={{ fontSize: "1.25rem", fontWeight: 700, margin: 0, color: "var(--color-content-default)" }}>
            {t("reports.tabTitle")}
          </h2>
          <p style={{ margin: "0.25rem 0 0", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
            {t("reports.tabSubtitle")}
          </p>
        </div>

        {/* Date Filter Controls */}
        <div style={{ display: "flex", alignItems: "flex-end", gap: "0.5rem" }}>
          <div>
            <label style={{ fontSize: "0.75rem", fontWeight: 600, display: "block", marginBottom: "0.25rem" }}>
              {t("reports.fromDate")}
            </label>
            <input
              type="date"
              value={fromDate}
              onChange={(e) => setFromDate(e.target.value)}
              style={{
                padding: "0.4rem 0.6rem",
                borderRadius: "var(--radius-sm)",
                border: "1px solid var(--color-border-default)",
                background: "var(--color-surface-card)",
              }}
            />
          </div>

          <div>
            <label style={{ fontSize: "0.75rem", fontWeight: 600, display: "block", marginBottom: "0.25rem" }}>
              {t("reports.toDate")}
            </label>
            <input
              type="date"
              value={toDate}
              onChange={(e) => setToDate(e.target.value)}
              style={{
                padding: "0.4rem 0.6rem",
                borderRadius: "var(--radius-sm)",
                border: "1px solid var(--color-border-default)",
                background: "var(--color-surface-card)",
              }}
            />
          </div>

          <Button
            size="compact"
            variant="secondary"
            onClick={() => {
              salesQuery.refetch();
              paymentMixQuery.refetch();
              discountsQuery.refetch();
            }}
          >
            {t("reports.applyFilter")}
          </Button>
        </div>
      </div>

      {/* Report Sub-Tabs */}
      <div style={{ display: "flex", gap: "0.5rem", borderBottom: "1px solid #cbd5e1", paddingBottom: "0.5rem" }}>
        <Button
          variant={reportType === "sales" ? "primary" : "ghost"}
          size="compact"
          onClick={() => setReportType("sales")}
        >
          {t("reports.subTabSales")}
        </Button>
        <Button
          variant={reportType === "payment-mix" ? "primary" : "ghost"}
          size="compact"
          onClick={() => setReportType("payment-mix")}
        >
          {t("reports.subTabPaymentMix")}
        </Button>
        <Button
          variant={reportType === "cash-differences" ? "primary" : "ghost"}
          size="compact"
          onClick={() => setReportType("cash-differences")}
        >
          {t("reports.subTabCashDifferences")}
        </Button>
        <Button
          variant={reportType === "discounts" ? "primary" : "ghost"}
          size="compact"
          onClick={() => setReportType("discounts")}
        >
          {t("reports.subTabDiscounts")}
        </Button>
      </div>

      {/* Sales Report View */}
      {reportType === "sales" && (
        <div
          style={{
            backgroundColor: "#ffffff",
            borderRadius: "var(--radius-lg)",
            border: "1px solid #cbd5e1",
            overflowX: "auto",
            boxShadow: "0 1px 3px 0 rgba(0, 0, 0, 0.05)",
          }}
        >
          <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem", textAlign: "start" }}>
            <thead>
              <tr
                style={{
                  backgroundColor: "var(--color-surface-hover)",
                  borderBottom: "1px solid #cbd5e1",
                  color: "var(--color-content-muted)",
                  fontWeight: 600,
                }}
              >
                <th style={{ padding: "0.75rem 1rem" }}>{t("reports.colSaleId")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("reports.colDate")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("reports.colCustomer")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("reports.colSubtotal")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("reports.colTax")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("reports.colTotal")}</th>
              </tr>
            </thead>
            <tbody>
              {salesList.length > 0 ? (
                salesList.map((sale: any, idx: number) => (
                  <tr key={sale.id || idx} style={{ borderBottom: idx === salesList.length - 1 ? "none" : "1px solid #e2e8f0" }}>
                    <td style={{ padding: "0.75rem 1rem", fontWeight: 700 }}>
                      <BidiText>{sale.receiptNumber || sale.id?.slice(0, 8) || "—"}</BidiText>
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <BidiText>{sale.createdAt ? new Date(sale.createdAt).toLocaleDateString() : today}</BidiText>
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <BidiText>{sale.customerName || t("workspace.walkInCustomer")}</BidiText>
                    </td>
                    <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                      <BidiText>{(sale.subtotal || 0).toFixed(2)}</BidiText>
                    </td>
                    <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                      <BidiText>{(sale.taxTotal || 0).toFixed(2)}</BidiText>
                    </td>
                    <td style={{ padding: "0.75rem 1rem", textAlign: "end", fontWeight: 700, color: "var(--color-brand-primary)" }}>
                      <BidiText>{(sale.total || 0).toFixed(2)}</BidiText>
                    </td>
                  </tr>
                ))
              ) : (
                <tr>
                  <td colSpan={6} style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted)" }}>
                    {t("reports.emptySales")}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {/* Payment Mix View */}
      {reportType === "payment-mix" && (
        <div
          style={{
            backgroundColor: "#ffffff",
            padding: "1.5rem",
            borderRadius: "var(--radius-lg)",
            border: "1px solid #cbd5e1",
            boxShadow: "0 1px 3px 0 rgba(0, 0, 0, 0.05)",
          }}
        >
          <h3 style={{ fontSize: "1.125rem", margin: "0 0 1rem", color: "var(--color-content-default)" }}>
            {t("reports.paymentMixTitle")}
          </h3>

          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(12rem, 1fr))", gap: "1rem" }}>
            <div style={{ padding: "1rem", borderRadius: "var(--radius-md)", backgroundColor: "var(--color-surface-hover)", border: "1px solid #cbd5e1" }}>
              <div style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>{t("tender.cash")}</div>
              <div style={{ fontSize: "1.5rem", fontWeight: 800, color: "var(--color-content-default)" }}>
                <BidiText>{(paymentMix?.cashTotal || 0).toFixed(2)}</BidiText>
              </div>
            </div>

            <div style={{ padding: "1rem", borderRadius: "var(--radius-md)", backgroundColor: "var(--color-surface-hover)", border: "1px solid #cbd5e1" }}>
              <div style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>{t("tender.card")}</div>
              <div style={{ fontSize: "1.5rem", fontWeight: 800, color: "var(--color-content-default)" }}>
                <BidiText>{(paymentMix?.cardTotal || 0).toFixed(2)}</BidiText>
              </div>
            </div>

            <div style={{ padding: "1rem", borderRadius: "var(--radius-md)", backgroundColor: "var(--color-surface-hover)", border: "1px solid #cbd5e1" }}>
              <div style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>{t("tender.credit")}</div>
              <div style={{ fontSize: "1.5rem", fontWeight: 800, color: "var(--color-content-default)" }}>
                <BidiText>{(paymentMix?.creditTotal || 0).toFixed(2)}</BidiText>
              </div>
            </div>

            <div style={{ padding: "1rem", borderRadius: "var(--radius-md)", backgroundColor: "rgba(59, 130, 246, 0.08)", border: "1px solid rgba(59, 130, 246, 0.3)" }}>
              <div style={{ fontSize: "0.8125rem", color: "var(--color-brand-primary)" }}>{t("reports.totalCollected")}</div>
              <div style={{ fontSize: "1.5rem", fontWeight: 900, color: "var(--color-brand-primary)" }}>
                <BidiText>{((paymentMix?.cashTotal || 0) + (paymentMix?.cardTotal || 0) + (paymentMix?.creditTotal || 0)).toFixed(2)}</BidiText>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Cash Differences View */}
      {reportType === "cash-differences" && (
        <div
          style={{
            backgroundColor: "#ffffff",
            borderRadius: "var(--radius-lg)",
            border: "1px solid #cbd5e1",
            overflowX: "auto",
            boxShadow: "0 1px 3px 0 rgba(0, 0, 0, 0.05)",
          }}
        >
          <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem", textAlign: "start" }}>
            <thead>
              <tr
                style={{
                  backgroundColor: "var(--color-surface-hover)",
                  borderBottom: "1px solid #cbd5e1",
                  color: "var(--color-content-muted)",
                  fontWeight: 600,
                }}
              >
                <th style={{ padding: "0.75rem 1rem" }}>{t("reports.colSession")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("reports.colCashier")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("session.expectedCash")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("session.countedCash")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("session.variance")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("reports.colReason")}</th>
              </tr>
            </thead>
            <tbody>
              {cashDiffList.length > 0 ? (
                cashDiffList.map((item: any, idx: number) => {
                  const varVal = item.variance || 0;
                  const varFormatted = new Intl.NumberFormat("en-US", { signDisplay: "exceptZero", minimumFractionDigits: 2 }).format(varVal);
                  return (
                  <tr key={item.sessionId || idx} style={{ borderBottom: idx === cashDiffList.length - 1 ? "none" : "1px solid #e2e8f0" }}>
                    <td style={{ padding: "0.75rem 1rem", fontWeight: 700 }}>
                      <BidiText>{(item.sessionId || "").slice(0, 8)}</BidiText>
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <BidiText>{item.cashierName || "—"}</BidiText>
                    </td>
                    <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                      <BidiText>{(item.expectedCash || 0).toFixed(2)}</BidiText>
                    </td>
                    <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                      <BidiText>{(item.countedCash || 0).toFixed(2)}</BidiText>
                    </td>
                    <td
                      style={{
                        padding: "0.75rem 1rem",
                        textAlign: "end",
                        fontWeight: 700,
                        color: varVal < 0 ? "var(--color-status-danger)" : "var(--color-status-success)",
                      }}
                    >
                      <BidiText>{varFormatted}</BidiText>
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      {item.varianceReason || "—"}
                    </td>
                  </tr>
                );})
              ) : (
                <tr>
                  <td colSpan={6} style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted)" }}>
                    {t("reports.emptyDiscrepancies")}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      )}

      {/* Discounts Summary View */}
      {reportType === "discounts" && (
        <div
          style={{
            backgroundColor: "#ffffff",
            padding: "1.5rem",
            borderRadius: "var(--radius-lg)",
            border: "1px solid #cbd5e1",
            boxShadow: "0 1px 3px 0 rgba(0, 0, 0, 0.05)",
          }}
        >
          <h3 style={{ fontSize: "1.125rem", margin: "0 0 1rem", color: "var(--color-content-default)" }}>
            {t("reports.discountSummaryTitle")}
          </h3>

          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
            <div style={{ padding: "1rem", borderRadius: "var(--radius-md)", backgroundColor: "var(--color-surface-hover)", border: "1px solid #cbd5e1" }}>
              <div style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>{t("reports.totalDiscountsGranted")}</div>
              <div style={{ fontSize: "1.5rem", fontWeight: 800, color: "var(--color-status-success)" }}>
                <BidiText>{(discounts?.totalDiscounts || 0).toFixed(2)}</BidiText>
              </div>
            </div>

            <div style={{ padding: "1rem", borderRadius: "var(--radius-md)", backgroundColor: "var(--color-surface-hover)", border: "1px solid #cbd5e1" }}>
              <div style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>{t("reports.discountCount")}</div>
              <div style={{ fontSize: "1.5rem", fontWeight: 800, color: "var(--color-content-default)" }}>
                <BidiText>{discounts?.discountedTransactionsCount || 0}</BidiText>
              </div>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
