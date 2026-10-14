import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { DocumentPage, type DocumentSummaryItem, type DocumentStatusGroup } from "../../../components/pages/document-page.tsx";
import { useInvoiceRecord, usePostInvoice, useInvoicePreview } from "../hooks/invoice-queries.ts";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { QuantityDisplay } from "../../../components/fields/quantity-display.tsx";
import { PercentageDisplay } from "../../../components/fields/percentage-display.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { LocalDateDisplay } from "../../../components/fields/local-date-display.tsx";

export interface InvoiceDocumentPageProps {
  readonly invoiceId?: string | undefined;
  readonly onBack?: (() => void) | undefined;
}

export function InvoiceDocumentPage({
  invoiceId = "inv-001",
  onBack
}: InvoiceDocumentPageProps): React.JSX.Element {
  const { t } = useTranslation("invoicing");
  const { data: invoice, isLoading, error } = useInvoiceRecord(invoiceId);
  const postInvoiceMutation = usePostInvoice();
  const [concurrencyError, setConcurrencyError] = useState<string | null>(null);
  const [isCreditNoteDialogOpen, setIsCreditNoteDialogOpen] = useState(false);

  if (isLoading) {
    return <div style={{ padding: "2rem", textAlign: "center" }}>{t("document.loading")}</div>;
  }

  if (error || !invoice) {
    return (
      <div style={{ padding: "2rem", textAlign: "center" }}>
        <p>{t("document.loadError")}</p>
        {onBack && <Button onPress={onBack}>{t("document.back")}</Button>}
      </div>
    );
  }

  const handlePostDocument = async () => {
    setConcurrencyError(null);
    try {
      await postInvoiceMutation.mutateAsync({
        id: invoice.id,
        etag: invoice.etag
      });
    } catch (err: any) {
      if (err?.status === 412 || err?.status === 409) {
        setConcurrencyError(t("document.concurrentEdit"));
      } else {
        setConcurrencyError(t("document.postError"));
      }
    }
  };

  const statusGroup: DocumentStatusGroup = {
    lifecycle: invoice.lifecycleStatus,
    approval: invoice.approvalStatus,
    compliance: invoice.complianceStatus,
    accounting: invoice.accountingStatus,
    settlement: invoice.settlementStatus,
    isLocked: invoice.isLocked,
    lockedReason: invoice.lockedReason ?? (invoice.accountingStatus === "Posted" ? t("document.postedLocked") : undefined)
  };

  const summaryItems: DocumentSummaryItem[] = [
    {
      label: t("document.subtotal"),
      value: <MoneyDisplay value={{ amount: invoice.subtotal, currency: invoice.currency }} />
    },
    {
      label: t("document.totalDiscount"),
      value: <MoneyDisplay value={{ amount: invoice.totalDiscount, currency: invoice.currency }} />
    },
    {
      label: t("document.totalTax"),
      value: <MoneyDisplay value={{ amount: invoice.totalTax, currency: invoice.currency }} />
    },
    {
      label: t("document.totalDue"),
      value: <MoneyDisplay value={{ amount: invoice.totalAmount, currency: invoice.currency }} />,
      isEmphasized: true
    }
  ];

  return (
    <DocumentPage
      documentType={t("document.type")}
      documentNumber={invoice.invoiceNumber}
      statuses={statusGroup}
      breadcrumbs={[
        { label: t("document.sales"), href: "/invoicing" },
        { label: t("title"), href: "/invoicing", onClick: onBack },
        { label: invoice.invoiceNumber }
      ]}
      headerActions={
        <>
          {onBack && <Button variant="secondary" onPress={onBack}>{t("document.back")}</Button>}
          {invoice.isLocked ? (
            <Button
              variant="secondary"
              onPress={() => setIsCreditNoteDialogOpen(true)}
            >
              {t("document.issueCreditNote")}
            </Button>
          ) : (
            <Button
              variant="primary"
              onPress={handlePostDocument}
              isDisabled={postInvoiceMutation.isPending || invoice.accountingStatus === "Posted"}
            >
              {postInvoiceMutation.isPending ? t("document.posting") : t("document.postToAccounts")}
            </Button>
          )}
        </>
      }
      metadataSection={
        <div>
          {concurrencyError && (
            <div style={{ marginBottom: "1rem", padding: "0.75rem", backgroundColor: "var(--color-surface-danger-subtle)", color: "var(--color-status-danger)", borderRadius: "var(--radius-md)" }} role="alert">
              {concurrencyError}
            </div>
          )}

          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "1rem" }}>
            <div>
              <div style={{ fontSize: "0.875rem", color: "var(--color-content-muted)" }}>{t("document.customer")}</div>
              <div style={{ fontWeight: 600 }}>{invoice.customerName}</div>
            </div>
            <div>
              <div style={{ fontSize: "0.875rem", color: "var(--color-content-muted)" }}>{t("document.issueDate")}</div>
              <div style={{ fontWeight: 600 }}><LocalDateDisplay value={invoice.issueDate} /></div>
            </div>
            <div>
              <div style={{ fontSize: "0.875rem", color: "var(--color-content-muted)" }}>{t("document.dueDate")}</div>
              <div style={{ fontWeight: 600 }}><LocalDateDisplay value={invoice.dueDate} /></div>
            </div>
            <div>
              <div style={{ fontSize: "0.875rem", color: "var(--color-content-muted)" }}>{t("document.currency")}</div>
              <div style={{ fontWeight: 600 }}>{invoice.currency}</div>
            </div>
          </div>
        </div>
      }
      linesSection={
        <div style={{ overflowX: "auto" }}>
          <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start", fontSize: "0.875rem" }}>
            <thead>
              <tr style={{ borderBottom: "1px solid var(--color-border-default)", backgroundColor: "var(--color-surface-subtle)" }}>
                <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>#</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("document.lineDescription")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("document.quantity")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "center" }}>{t("document.unit")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("document.unitPrice")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("document.discount")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("document.tax")}</th>
                <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("document.total")}</th>
              </tr>
            </thead>
            <tbody>
              {invoice.items.map((line, index) => (
                <tr key={line.id} style={{ borderBottom: "1px solid var(--color-border-default)" }}>
                  <td style={{ padding: "0.75rem 1rem" }}>{index + 1}</td>
                  <td style={{ padding: "0.75rem 1rem", fontWeight: 500 }}>{line.description}</td>
                  <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                    <QuantityDisplay value={{ value: line.quantity, unitOfMeasure: line.uom }} />
                  </td>
                  <td style={{ padding: "0.75rem 1rem", textAlign: "center" }}>{line.uom}</td>
                  <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                    <MoneyDisplay value={{ amount: line.unitPrice, currency: invoice.currency }} />
                  </td>
                  <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                    {line.discountRate ? <PercentageDisplay value={line.discountRate} /> : "—"}
                  </td>
                  <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                    <PercentageDisplay value={line.taxRate} />
                  </td>
                  <td style={{ padding: "0.75rem 1rem", textAlign: "end", fontWeight: 600 }}>
                    <MoneyDisplay value={{ amount: line.lineTotal, currency: invoice.currency }} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      }
      summaryItems={summaryItems}
      notesSection={
        invoice.notes ? (
          <div style={{ fontSize: "0.875rem", color: "var(--color-content-muted)", backgroundColor: "var(--color-surface-subtle)", padding: "1rem", borderRadius: "var(--radius-md)" }}>
            <div style={{ fontWeight: 600, marginBottom: "0.25rem" }}>{t("document.notesAndTerms")}</div>
            <div>{invoice.notes}</div>
          </div>
        ) : undefined
      }
    >
      <Dialog
        isOpen={isCreditNoteDialogOpen}
        onOpenChange={setIsCreditNoteDialogOpen}
        title={t("document.creditNoteTitle")}
        description={t("document.creditNoteDescription", { number: invoice.invoiceNumber })}
      >
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem", padding: "0.5rem 0" }}>
          <p style={{ margin: 0, fontSize: "0.875rem" }}>
            {t("document.creditNoteWarning")}
          </p>
          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem" }}>
            <Button variant="secondary" onPress={() => setIsCreditNoteDialogOpen(false)}>{t("document.cancel")}</Button>
            <Button variant="primary" onPress={() => setIsCreditNoteDialogOpen(false)}>{t("document.continueCreditNote")}</Button>
          </div>
        </div>
      </Dialog>
    </DocumentPage>
  );
}
