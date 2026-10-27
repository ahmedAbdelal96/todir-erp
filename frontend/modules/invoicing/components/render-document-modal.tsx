import React from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { LocalDateDisplay } from "../../../components/fields/local-date-display.tsx";
import { FileTextIcon } from "../../../components/primitives/icons.tsx";
import type { InvoiceRenderModel } from "../types.ts";

export interface RenderDocumentModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly renderModel?: InvoiceRenderModel | null | undefined;
}

export function RenderDocumentModal({
  isOpen,
  onClose,
  renderModel
}: RenderDocumentModalProps): React.JSX.Element {
  const { t } = useTranslation("invoicing");

  if (!renderModel) return <></>;

  const isCustomer = renderModel.direction !== "Payable";

  const documentTitle =
    renderModel.documentType === "CreditNote"
      ? t("detail.creditNoteBadge")
      : renderModel.documentType === "DebitNote"
        ? t("detail.debitNoteBadge")
        : isCustomer
          ? t("detail.customerDocumentBadge")
          : t("detail.supplierDocumentBadge");

  const handlePrint = () => {
    window.print();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => { if (!open) onClose(); }}
      title={t("renderModel.title")}
      size="large"
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", padding: "1rem 0" }}>
        {/* Printable Paper Area */}
        <div
          id="invoice-printable-area"
          style={{
            backgroundColor: "#ffffff",
            padding: "2rem",
            borderRadius: "var(--radius-md)",
            border: "1px solid #cbd5e1",
            boxShadow: "0 4px 6px -1px rgba(0, 0, 0, 0.05)",
            fontFamily: "var(--font-sans, sans-serif)",
            color: "#0f172a"
          }}
        >
          {/* Document Header */}
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", borderBottom: "2px solid #102a43", paddingBottom: "1rem" }}>
            <div>
              <h1 style={{ margin: 0, fontSize: "1.5rem", fontWeight: 900, color: "#102a43" }}>
                {documentTitle}
              </h1>
              <p style={{ margin: "0.25rem 0 0", fontSize: "1.125rem", fontWeight: 700, color: "#475569" }}>
                <bdi>{renderModel.documentNumber}</bdi>
              </p>
            </div>
            <div style={{ textAlign: "end", fontSize: "0.875rem" }}>
              <div>
                <strong>{t("renderModel.invoiceDate")}: </strong>
                <LocalDateDisplay value={renderModel.documentDate} />
              </div>
              <div>
                <strong>{t("renderModel.invoiceNumber")}: </strong>
                <bdi>{renderModel.documentNumber}</bdi>
              </div>
            </div>
          </div>

          {/* Parties Grid */}
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "2rem", margin: "1.5rem 0", padding: "1rem", backgroundColor: "#f8fafc", borderRadius: "var(--radius-sm)" }}>
            <div>
              <span style={{ fontSize: "0.75rem", fontWeight: 800, color: "#64748b", textTransform: "uppercase" }}>
                {isCustomer ? t("renderModel.billedTo") : t("renderModel.billedFrom")}
              </span>
              <h3 style={{ margin: "0.25rem 0", fontSize: "1.125rem", fontWeight: 700 }}>
                {renderModel.partyDisplayName}
              </h3>
              {renderModel.partyLegalName && (
                <p style={{ margin: "0.125rem 0", fontSize: "0.8125rem", color: "#475569" }}>
                  {renderModel.partyLegalName}
                </p>
              )}
            </div>
          </div>

          {/* Lines Table */}
          <div style={{ overflowX: "auto" }}>
            <table style={{ width: "100%", minWidth: "550px", borderCollapse: "collapse", margin: "1.5rem 0", fontSize: "0.875rem" }}>
            <thead>
              <tr style={{ backgroundColor: "#102a43", color: "#ffffff" }}>
                <th style={{ padding: "0.625rem 0.75rem", textAlign: "start" }}>#</th>
                <th style={{ padding: "0.625rem 0.75rem", textAlign: "start" }}>{t("renderModel.description")}</th>
                <th style={{ padding: "0.625rem 0.75rem", textAlign: "center" }}>{t("renderModel.qty")}</th>
                <th style={{ padding: "0.625rem 0.75rem", textAlign: "end" }}>{t("renderModel.unitPrice")}</th>
                <th style={{ padding: "0.625rem 0.75rem", textAlign: "end" }}>{t("renderModel.subtotal")}</th>
                <th style={{ padding: "0.625rem 0.75rem", textAlign: "end" }}>{t("renderModel.tax")}</th>
                <th style={{ padding: "0.625rem 0.75rem", textAlign: "end" }}>{t("renderModel.total")}</th>
              </tr>
            </thead>
            <tbody>
              {renderModel.lines.map((l, i) => (
                <tr key={l.id || i} style={{ borderBottom: "1px solid #e2e8f0" }}>
                  <td style={{ padding: "0.625rem 0.75rem" }}>{i + 1}</td>
                  <td style={{ padding: "0.625rem 0.75rem", fontWeight: 600 }}>{l.description}</td>
                  <td style={{ padding: "0.625rem 0.75rem", textAlign: "center" }}>{l.quantity} {l.unitCode}</td>
                  <td style={{ padding: "0.625rem 0.75rem", textAlign: "end" }}>
                    <MoneyDisplay value={{ amount: l.finalUnitPrice, currency: renderModel.currencyCode }} />
                  </td>
                  <td style={{ padding: "0.625rem 0.75rem", textAlign: "end" }}>
                    <MoneyDisplay value={{ amount: l.netAmount, currency: renderModel.currencyCode }} />
                  </td>
                  <td style={{ padding: "0.625rem 0.75rem", textAlign: "end" }}>
                    <MoneyDisplay value={{ amount: l.taxAmount, currency: renderModel.currencyCode }} />
                  </td>
                  <td style={{ padding: "0.625rem 0.75rem", textAlign: "end", fontWeight: 700 }}>
                    <MoneyDisplay value={{ amount: l.grossAmount, currency: renderModel.currencyCode }} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          </div>

          {/* Totals Box */}
          <div style={{ display: "flex", justifyContent: "flex-end", marginTop: "1rem" }}>
            <div style={{ width: "320px", display: "flex", flexDirection: "column", gap: "0.5rem", fontSize: "0.875rem" }}>
              <div style={{ display: "flex", justifyContent: "space-between" }}>
                <span>{t("renderModel.subtotal")}:</span>
                <span style={{ fontWeight: 600 }}><MoneyDisplay value={{ amount: renderModel.totals.netAmount, currency: renderModel.currencyCode }} /></span>
              </div>
              <div style={{ display: "flex", justifyContent: "space-between" }}>
                <span>{t("renderModel.tax")}:</span>
                <span style={{ fontWeight: 600 }}><MoneyDisplay value={{ amount: renderModel.totals.taxAmount, currency: renderModel.currencyCode }} /></span>
              </div>
              <div style={{ borderTop: "2px solid #102a43", paddingTop: "0.5rem", display: "flex", justifyContent: "space-between", fontSize: "1.125rem", fontWeight: 900, color: "#102a43" }}>
                <span>{t("renderModel.total")}:</span>
                <span><MoneyDisplay value={{ amount: renderModel.totals.grossAmount, currency: renderModel.currencyCode }} /></span>
              </div>
            </div>
          </div>
        </div>

        {/* Modal Actions */}
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onPress={onClose}>
            {t("renderModel.close")}
          </Button>
          <Button variant="primary" onPress={handlePrint}>
            <FileTextIcon size={16} /> {t("renderModel.printButton")}
          </Button>
        </div>
      </div>
    </Dialog>
  );
}
