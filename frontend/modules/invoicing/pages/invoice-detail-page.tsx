import React, { useState, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate, useParams } from "react-router-dom";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { LocalDateDisplay } from "../../../components/fields/local-date-display.tsx";
import { FileTextIcon, ShieldCheckIcon, AlertCircleIcon } from "../../../components/primitives/icons.tsx";
import { InvoiceStatusGroup, getSettlementStatus, getRegulatoryStatus } from "../components/invoice-status-pill.tsx";
import { InvoiceTotalsCard } from "../components/invoice-totals-card.tsx";
import { InvoiceScheduleTable } from "../components/invoice-schedule-table.tsx";
import { InvoiceCorrectionDialog } from "../features/invoice-correction-dialog.tsx";
import { RenderDocumentModal } from "../components/render-document-modal.tsx";
import {
  useInvoicingScope,
  useInvoicingPermissions,
  useCustomerInvoiceDetail,
  useSupplierInvoiceDetail,
  useCustomerRenderModel,
  useSupplierRenderModel,
  useCreateCustomerCorrectionMutation,
  useCreateSupplierCorrectionMutation
} from "../hooks/invoice-queries.ts";
import type { InvoiceCorrectionRequest, InvoiceRelatedDocument, InvoiceDocumentType } from "../types.ts";

export interface InvoiceDetailPageProps {
  readonly direction: "customer" | "supplier";
}

export function InvoiceDetailPage({ direction }: InvoiceDetailPageProps): React.JSX.Element {
  const { t } = useTranslation("invoicing");
  const navigate = useNavigate();
  const { id = "" } = useParams<{ id: string }>();
  const isCustomer = direction === "customer";

  const { legalEntityId } = useInvoicingScope();
  const perms = useInvoicingPermissions();

  const customerDetailQuery = useCustomerInvoiceDetail(id, legalEntityId, isCustomer && Boolean(id));
  const supplierDetailQuery = useSupplierInvoiceDetail(id, legalEntityId, !isCustomer && Boolean(id));
  const detail = isCustomer ? customerDetailQuery.data : supplierDetailQuery.data;
  const isLoading = isCustomer ? customerDetailQuery.isLoading : supplierDetailQuery.isLoading;
  const isError = isCustomer ? customerDetailQuery.isError : supplierDetailQuery.isError;

  const customerRenderQuery = useCustomerRenderModel(id, legalEntityId, false);
  const supplierRenderQuery = useSupplierRenderModel(id, legalEntityId, false);

  const createCustomerCorrection = useCreateCustomerCorrectionMutation();
  const createSupplierCorrection = useCreateSupplierCorrectionMutation();

  const [isCorrectionOpen, setIsCorrectionOpen] = useState(false);
  const [correctionType, setCorrectionType] = useState<"CreditNote" | "DebitNote">("CreditNote");
  const [isRenderModalOpen, setIsRenderModalOpen] = useState(false);
  const [renderData, setRenderData] = useState<any>(null);
  const [createdCorrections, setCreatedCorrections] = useState<InvoiceRelatedDocument[]>([]);

  const handleOpenPrint = async () => {
    if (isCustomer) {
      const res = await customerRenderQuery.refetch();
      setRenderData(res.data);
    } else {
      const res = await supplierRenderQuery.refetch();
      setRenderData(res.data);
    }
    setIsRenderModalOpen(true);
  };

  const handleOpenCreditNote = () => {
    setCorrectionType("CreditNote");
    setIsCorrectionOpen(true);
  };

  const handleOpenDebitNote = () => {
    setCorrectionType("DebitNote");
    setIsCorrectionOpen(true);
  };

  const handleCorrectionSubmit = async (request: InvoiceCorrectionRequest) => {
    let result: any;
    if (isCustomer) {
      result = await createCustomerCorrection.mutateAsync({ id, legalEntityId, request });
      await customerDetailQuery.refetch();
    } else {
      result = await createSupplierCorrection.mutateAsync({ id, legalEntityId, request });
      await supplierDetailQuery.refetch();
    }

    if (result) {
      const docType = (result.documentType || request.documentType) as InvoiceDocumentType;
      const docNum = result.documentNumber || (docType === "CreditNote" ? (isCustomer ? "CN-2026-0001" : "PINV-CN-001") : (isCustomer ? "DN-2026-0001" : "PINV-DN-001"));
      const newRel: InvoiceRelatedDocument = {
        id: result.id || `corr-${Date.now()}`,
        documentNumber: docNum,
        documentType: docType,
        lifecycle: "Finalized",
        amount: detail?.totals?.grossAmount ?? "0.00",
        date: new Date().toISOString().slice(0, 10),
        relationship: docType === "CreditNote" ? t("detail.creditNoteBadge") : t("detail.debitNoteBadge")
      };
      setCreatedCorrections((prev) => [...prev, newRel]);
    }
  };

  const allRelatedDocuments = useMemo(() => {
    const fromApi = detail?.relatedDocuments ?? [];
    const combined = [...fromApi];
    for (const created of createdCorrections) {
      if (!combined.some((c) => c.id === created.id || c.documentNumber === created.documentNumber)) {
        combined.push(created);
      }
    }
    return combined;
  }, [detail?.relatedDocuments, createdCorrections]);

  if (isLoading || !legalEntityId) {
    return (
      <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-secondary)" }}>
        <p>{t("document.loading")}</p>
      </div>
    );
  }

  if (isError || !detail) {
    return (
      <div style={{ padding: "3rem", textAlign: "center" }}>
        <p style={{ color: "#dc2626", fontWeight: 700 }}>{t("document.loadError")}</p>
        <Button variant="secondary" onPress={() => navigate("/invoicing")}>
          {t("detail.backToList")}
        </Button>
      </div>
    );
  }

  const isCredit = detail.documentType === "CreditNote";
  const isDebit = detail.documentType === "DebitNote";
  const isFinalized = detail.lifecycle === "Finalized";

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", padding: "1.25rem", maxWidth: "1280px", margin: "0 auto" }}>
      {/* Header & Breadcrumbs */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: "0.75rem" }}>
        <div>
          <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)", marginBottom: "0.25rem" }}>
            <span style={{ cursor: "pointer" }} onClick={() => navigate("/invoicing")}>{t("title")}</span>
            <span>/</span>
            <span style={{ cursor: "pointer" }} onClick={() => navigate("/invoicing")}>
              {isCustomer ? t("tabs.customerInvoices") : t("tabs.supplierInvoices")}
            </span>
            <span>/</span>
            <bdi>{detail.documentNumber || detail.id}</bdi>
          </div>
          <div style={{ display: "flex", alignItems: "center", gap: "0.75rem", flexWrap: "wrap" }}>
            <h1 style={{ margin: 0, fontSize: "1.5rem", fontWeight: 900, color: "#102a43" }}>
              <bdi>{detail.documentNumber || t("draft.draftFallback", { id: detail.id.slice(0, 8) })}</bdi>
            </h1>
            <InvoiceStatusGroup
              lifecycle={detail.lifecycle}
              documentType={detail.documentType}
              settlementSummary={detail.settlementSummary}
              regulatorySummary={detail.regulatorySummary}
              journalEntryId={detail.journalEntryId}
            />
          </div>
        </div>

        {/* Top Actions */}
        <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", flexWrap: "wrap" }}>
          <Button variant="secondary" size="compact" onPress={() => navigate("/invoicing")}>
            {t("detail.backToList")}
          </Button>

          {/* Print / Render Model */}
          <Button variant="secondary" size="compact" onPress={handleOpenPrint}>
            <FileTextIcon size={14} /> {t("detail.printAction")}
          </Button>

          {/* Credit Note (Correction) */}
          {isFinalized && !isCredit && perms.canCreateCreditNote && (
            <Button variant="secondary" size="compact" onPress={handleOpenCreditNote}>
              {t("detail.issueCreditNoteAction")}
            </Button>
          )}

          {/* Debit Note */}
          {isFinalized && !isDebit && perms.canCreateDebitNote && (
            <Button variant="secondary" size="compact" onPress={handleOpenDebitNote}>
              {t("detail.issueDebitNoteAction")}
            </Button>
          )}
        </div>
      </div>

      {/* Immutability Notice Banner */}
      {isFinalized && (
        <div
          style={{
            padding: "0.75rem 1rem",
            borderRadius: "var(--radius-md)",
            backgroundColor: "#f0f4f8",
            border: "1px solid #cbd5e1",
            color: "#334e68",
            fontSize: "0.8125rem",
            display: "flex",
            alignItems: "center",
            gap: "0.5rem"
          }}
        >
          <ShieldCheckIcon size={18} style={{ color: "#2563eb", flexShrink: 0 }} />
          <span>{t("detail.immutableNotice")}</span>
        </div>
      )}

      {/* 1. Metadata Surfaces Grid */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(320px, 1fr))", gap: "1.25rem" }}>
        {/* Party Details Surface */}
        <Surface title={t("detail.partySectionTitle")}>
          <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem", fontSize: "0.875rem" }}>
            <div>
              <span style={{ fontSize: "0.75rem", color: "#64748b" }}>
                {isCustomer ? t("draft.customerParty") : t("draft.supplierParty")}
              </span>
              <div style={{ fontWeight: 700, fontSize: "1rem", color: "#0f172a" }}>
                {detail.partyDisplayName}
              </div>
            </div>
            {detail.partyLegalName && (
              <div>
                <span style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("detail.partyLegalNameLabel")}</span>
                <div style={{ color: "#334155" }}>{detail.partyLegalName}</div>
              </div>
            )}
            {!isCustomer && detail.supplierInvoiceNumber && (
              <div style={{ marginTop: "0.25rem", padding: "0.375rem 0.5rem", backgroundColor: "#fef3c7", borderRadius: "var(--radius-sm)", border: "1px solid #fde68a", display: "inline-block" }}>
                <span style={{ fontSize: "0.75rem", fontWeight: 700, color: "#92400e" }}>
                  {t("detail.supplierReference")}: <bdi>{detail.supplierInvoiceNumber}</bdi>
                </span>
              </div>
            )}
          </div>
        </Surface>

        {/* Dates & Terms Surface */}
        <Surface title={t("detail.datesSectionTitle")}>
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem", fontSize: "0.875rem" }}>
            <div>
              <span style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("draft.documentDate")}</span>
              <div style={{ fontWeight: 600 }}><LocalDateDisplay value={detail.documentDate} /></div>
            </div>
            <div>
              <span style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("draft.postingDate")}</span>
              <div style={{ fontWeight: 600 }}><LocalDateDisplay value={detail.postingDate} /></div>
            </div>
            <div>
              <span style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("draft.taxDate")}</span>
              <div style={{ fontWeight: 600 }}><LocalDateDisplay value={detail.taxDate} /></div>
            </div>
            <div>
              <span style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("draft.currency")}</span>
              <div style={{ fontWeight: 700 }}><bdi>{detail.currencyCode}</bdi></div>
            </div>
          </div>
        </Surface>
      </div>

      {/* 2. Line Items Table Surface */}
      <Surface title={t("detail.linesSectionTitle")}>
        {/* Desktop View Table */}
        <div className="ui-responsive-financial-desktop" style={{ overflowX: "auto" }}>
          <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.8125rem", textAlign: "start" }}>
            <thead>
              <tr style={{ backgroundColor: "#102a43", color: "#ffffff", borderBottom: "1px solid #0c1f31" }}>
                <th style={{ padding: "0.625rem 0.75rem" }}>#</th>
                <th style={{ padding: "0.625rem 0.75rem" }}>{t("draft.description")}</th>
                <th style={{ padding: "0.625rem 0.75rem", textAlign: "center" }}>{t("draft.quantity")}</th>
                <th style={{ padding: "0.625rem 0.75rem", textAlign: "end" }}>{t("draft.unitPrice")}</th>
                <th style={{ padding: "0.625rem 0.75rem", textAlign: "end" }}>{t("draft.netAmount")}</th>
                <th style={{ padding: "0.625rem 0.75rem", textAlign: "end" }}>{t("draft.taxAmount")}</th>
                <th style={{ padding: "0.625rem 0.75rem", textAlign: "end" }}>{t("draft.totalAmount")}</th>
              </tr>
            </thead>
            <tbody>
              {detail.lines.map((l, index) => (
                <tr
                  key={l.id || index}
                  style={{
                    borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)",
                    backgroundColor: index % 2 === 0 ? "#ffffff" : "var(--color-surface-subtle, #f8fafc)"
                  }}
                >
                  <td style={{ padding: "0.625rem 0.75rem" }}>{l.sequence || index + 1}</td>
                  <td style={{ padding: "0.625rem 0.75rem", fontWeight: 600 }}>{l.description}</td>
                  <td style={{ padding: "0.625rem 0.75rem", textAlign: "center" }}>
                    {l.quantity} <bdi dir="ltr" style={{ color: "#64748b" }}>{l.unitCode || t("draft.defaultUnit")}</bdi>
                  </td>
                  <td style={{ padding: "0.625rem 0.75rem", textAlign: "end" }}>
                    <MoneyDisplay value={{ amount: l.finalUnitPrice, currency: detail.currencyCode }} />
                  </td>
                  <td style={{ padding: "0.625rem 0.75rem", textAlign: "end" }}>
                    <MoneyDisplay value={{ amount: l.netAmount, currency: detail.currencyCode }} />
                  </td>
                  <td style={{ padding: "0.625rem 0.75rem", textAlign: "end" }}>
                    <MoneyDisplay value={{ amount: l.taxAmount, currency: detail.currencyCode }} />
                  </td>
                  <td style={{ padding: "0.625rem 0.75rem", textAlign: "end", fontWeight: 700, color: "#102a43" }}>
                    <MoneyDisplay value={{ amount: l.grossAmount, currency: detail.currencyCode }} />
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Mobile 390px View Cards */}
        <div className="ui-responsive-financial-mobile">
          {detail.lines.map((l, index) => (
            <div
              key={l.id || index}
              style={{
                backgroundColor: "var(--color-surface-card, #ffffff)",
                border: "1px solid var(--color-border-default, #cbd5e1)",
                borderRadius: "var(--radius-md, 0.5rem)",
                padding: "0.875rem",
                boxShadow: "var(--shadow-sm, 0 1px 2px rgba(0,0,0,0.05))",
                display: "flex",
                flexDirection: "column",
                gap: "0.5rem"
              }}
            >
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", gap: "0.5rem" }}>
                <div style={{ display: "flex", alignItems: "center", gap: "0.375rem" }}>
                  <span style={{ fontSize: "0.75rem", fontWeight: 700, backgroundColor: "#102a43", color: "#ffffff", borderRadius: "0.25rem", padding: "0.125rem 0.375rem" }}>
                    #{l.sequence || index + 1}
                  </span>
                  <span style={{ fontWeight: 700, color: "var(--color-brand-primary, #102a43)", fontSize: "0.875rem" }}>{l.description}</span>
                </div>
                <span style={{ fontSize: "0.8125rem", color: "#475569", fontWeight: 600, whiteSpace: "nowrap" }}>
                  {l.quantity} <bdi dir="ltr">{l.unitCode || t("draft.defaultUnit")}</bdi>
                </span>
              </div>

              <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.5rem", marginTop: "0.25rem", fontSize: "0.75rem", borderTop: "1px solid #f1f5f9", paddingTop: "0.5rem" }}>
                <div>
                  <span style={{ color: "#64748b", display: "block" }}>{t("draft.unitPrice")}</span>
                  <span style={{ fontWeight: 600 }}><MoneyDisplay value={{ amount: l.finalUnitPrice, currency: detail.currencyCode }} /></span>
                </div>
                <div>
                  <span style={{ color: "#64748b", display: "block" }}>{t("draft.netAmount")}</span>
                  <span style={{ fontWeight: 600 }}><MoneyDisplay value={{ amount: l.netAmount, currency: detail.currencyCode }} /></span>
                </div>
                <div>
                  <span style={{ color: "#64748b", display: "block" }}>{t("draft.taxAmount")}</span>
                  <span style={{ fontWeight: 600 }}><MoneyDisplay value={{ amount: l.taxAmount, currency: detail.currencyCode }} /></span>
                </div>
                <div>
                  <span style={{ color: "#64748b", display: "block" }}>{t("draft.totalAmount")}</span>
                  <span style={{ fontWeight: 800, color: "#102a43" }}><MoneyDisplay value={{ amount: l.grossAmount, currency: detail.currencyCode }} /></span>
                </div>
              </div>
            </div>
          ))}
        </div>
      </Surface>

      {/* 3. Totals & Payment Schedule Grid */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(320px, 1fr))", gap: "1.25rem", alignItems: "start" }}>
        {/* Payment Schedule */}
        <Surface title={t("detail.paymentScheduleTitle")}>
          <InvoiceScheduleTable schedule={detail.paymentSchedule} />
        </Surface>

        {/* Financial Totals */}
        <InvoiceTotalsCard
          totals={detail.totals}
          fallbackCurrency={detail.currencyCode}
        />
      </div>

      {/* 4. Subledger Settlement & Ledger Accounting Grid */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(320px, 1fr))", gap: "1.25rem" }}>
        {/* Subledger Settlement Surface */}
        <Surface title={t("detail.settlementTitle")}>
          <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem", fontSize: "0.875rem" }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <span style={{ color: "#64748b" }}>{t("detail.settlementStatusLabel")}:</span>
              <span style={{ fontWeight: 700, color: "#102a43" }}>{getSettlementStatus(detail.settlementSummary, t)}</span>
            </div>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <span style={{ color: "#64748b" }}>{t("detail.remainingAmountLabel")}:</span>
              <span style={{ fontWeight: 800 }}>
                <MoneyDisplay value={{ amount: detail.totals?.grossAmount ?? "0.00", currency: detail.currencyCode }} />
              </span>
            </div>
            <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem", color: "#64748b" }}>
              {t("detail.subledgerNotice")}
            </p>
          </div>
        </Surface>

        {/* Ledger Accounting Surface (Protected for Finance) */}
        {perms.canPreviewAccounting && (
          <Surface title={t("detail.accountingTitle")}>
            <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem", fontSize: "0.875rem" }}>
              {detail.journalEntryId ? (
                <>
                  <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                    <span style={{ color: "#64748b" }}>{t("detail.journalEntryReference")}:</span>
                    <bdi style={{ fontWeight: 700, color: "#2563eb" }}>{detail.journalEntryId.slice(0, 13)}...</bdi>
                  </div>
                  <div>
                    <Button
                      variant="secondary"
                      size="compact"
                      onPress={() => navigate(`/accounting`)}
                    >
                      {t("detail.viewInLedger")}
                    </Button>
                  </div>
                </>
              ) : (
                <div style={{ color: "#64748b" }}>{t("detail.noJournalNotice")}</div>
              )}
            </div>
          </Surface>
        )}
      </div>

      {/* 5. Regulatory Compliance Surface (Protected for Compliance) */}
      {perms.canViewRegulatory && (
        <Surface title={t("detail.regulatoryTitle")}>
          <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem", fontSize: "0.875rem" }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <span style={{ color: "#64748b" }}>{t("detail.regulatoryStateLabel")}:</span>
              <span style={{ fontWeight: 700 }}>{getRegulatoryStatus(detail.regulatorySummary, t)}</span>
            </div>
          </div>
        </Surface>
      )}

      {/* 6. Linked Corrections & Document History Surface */}
      <Surface title={t("detail.correctionsTitle")}>
        {allRelatedDocuments.length > 0 ? (
          <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
            {allRelatedDocuments.map((rel) => (
              <div
                key={rel.id}
                style={{
                  display: "flex",
                  justifyContent: "space-between",
                  alignItems: "center",
                  padding: "0.625rem 0.75rem",
                  backgroundColor: "var(--color-surface-subtle, #f8fafc)",
                  border: "1px solid var(--color-border-default, #cbd5e1)",
                  borderRadius: "var(--radius-sm, 0.25rem)"
                }}
              >
                <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
                  <span style={{ fontWeight: 700, color: "var(--color-brand-primary, #102a43)" }}>{rel.documentNumber}</span>
                  <StatusBadge
                    intent={rel.documentType === "CreditNote" ? "warning" : "info"}
                    label={rel.documentType === "CreditNote" ? t("detail.creditNoteBadge") : t("detail.debitNoteBadge")}
                  />
                  {rel.relationship && (
                    <span style={{ fontSize: "0.75rem", color: "#64748b" }}>({rel.relationship})</span>
                  )}
                </div>
                <div style={{ display: "flex", alignItems: "center", gap: "1rem" }}>
                  <span style={{ fontSize: "0.8125rem", color: "#64748b" }}>
                    <LocalDateDisplay value={rel.date} />
                  </span>
                  <span style={{ fontWeight: 800 }}>
                    <MoneyDisplay value={{ amount: rel.amount, currency: detail.currencyCode }} />
                  </span>
                </div>
              </div>
            ))}
          </div>
        ) : (
          <div style={{ color: "#64748b", fontSize: "0.875rem" }}>
            {t("detail.noCorrections")}
          </div>
        )}
      </Surface>

      {/* Correction Dialog */}
      <InvoiceCorrectionDialog
        isOpen={isCorrectionOpen}
        onClose={() => setIsCorrectionOpen(false)}
        invoice={detail}
        documentType={correctionType}
        onSubmit={handleCorrectionSubmit}
        isSubmitting={createCustomerCorrection.isPending || createSupplierCorrection.isPending}
      />

      {/* Print / Render Model Modal */}
      <RenderDocumentModal
        isOpen={isRenderModalOpen}
        onClose={() => setIsRenderModalOpen(false)}
        renderModel={renderData}
      />
    </div>
  );
}
