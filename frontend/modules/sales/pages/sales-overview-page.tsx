import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext, useNavigate } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { useLegalEntityContext } from "../../../core/runtime/runtime-provider.tsx";
import { useSalesQuotations } from "../hooks/use-sales-queries.ts";
import { SalesOverviewTab } from "../features/overview/sales-overview-tab.tsx";
import { NewQuotationModal } from "../features/quotations/new-quotation-modal.tsx";
import { QuotationDetailModal } from "../features/quotations/quotation-detail-modal.tsx";

export function SalesOverviewPage(): React.JSX.Element {
  const { t } = useTranslation("sales");
  const navigate = useNavigate();
  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();

  let contextEntityId: string | undefined;
  try {
    const context = useLegalEntityContext();
    contextEntityId = context?.legalEntityId ?? undefined;
  } catch {
    // Outside RuntimeProvider
  }
  const legalEntityId = outletCtx?.legalEntityId ?? contextEntityId ?? "";

  const [isNewQuotationOpen, setIsNewQuotationOpen] = useState(false);
  const [selectedQuotationId, setSelectedQuotationId] = useState<string | null>(null);

  const { data: quotesPage, isLoading } = useSalesQuotations(legalEntityId, {
    page: 1,
    pageSize: 50
  });

  const quotations = quotesPage?.items ?? [];

  if (!legalEntityId) {
    return (
      <div style={{ padding: "3rem", textAlign: "center", color: "var(--color-content-muted)" }}>
        {t("common.loading")}
      </div>
    );
  }

  return (
    <div
      data-testid="sales-overview-page"
      style={{
        display: "flex",
        flexDirection: "column",
        gap: "1.5rem",
        padding: "1.5rem 2rem",
        backgroundColor: "var(--color-surface-page, #f0f4f8)",
        minHeight: "100vh"
      }}
    >
      <SalesOverviewTab
        quotations={quotations}
        isLoading={isLoading}
        onSelectQuotation={(id) => setSelectedQuotationId(id)}
        onNewQuotation={() => setIsNewQuotationOpen(true)}
      />

      {isNewQuotationOpen && (
        <NewQuotationModal
          isOpen={isNewQuotationOpen}
          onClose={() => setIsNewQuotationOpen(false)}
          legalEntityId={legalEntityId}
          onCreated={(newId) => {
            setSelectedQuotationId(newId);
          }}
        />
      )}

      {selectedQuotationId && (
        <QuotationDetailModal
          quotationId={selectedQuotationId}
          legalEntityId={legalEntityId}
          onClose={() => setSelectedQuotationId(null)}
          onOrderCreated={() => {
            setSelectedQuotationId(null);
            navigate("/sales/orders");
          }}
        />
      )}
    </div>
  );
}
