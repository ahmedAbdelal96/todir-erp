import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useParams, useSearchParams } from "react-router-dom";
import { useQueryTab } from "../../../core/routing/use-query-tab.ts";
import { PageHeader } from "../../../components/pages/page-header.tsx";
import { Surface } from "../../../components/pages/surface.tsx";
import { PartyPicker } from "../../directory/public.ts";
import { SubledgerBalanceCard } from "../components/subledger-balance-card.tsx";
import { ApplicationWorkspace } from "../features/application-workspace.tsx";
import { AgingReportView } from "../features/aging-report-view.tsx";
import { PartyStatementView } from "../features/party-statement-view.tsx";
import { FinancialProfileCard } from "../features/financial-profile-card.tsx";
import { useSubledgerScope, usePartyExposureQuery } from "../hooks/subledger-queries.ts";
import type { SubledgerDirection } from "../types.ts";

export function PartyFinancePage(): React.JSX.Element {
  const { t } = useTranslation("subledger");
  const { legalEntityId } = useSubledgerScope();
  const params = useParams<{ partyId?: string }>();
  const [searchParams, setSearchParams] = useSearchParams();

  const initialParty =
    params.partyId ||
    searchParams.get("partyId") ||
    "";

  const initialDirection: SubledgerDirection =
    (searchParams.get("direction") as SubledgerDirection) || "Receivable";

  const [partyId, setPartyId] = useState<string>(initialParty);
  const [direction, setDirection] = useState<SubledgerDirection>(initialDirection);
  const [activeTab, setActiveTab] = useQueryTab({
    defaultTab: "cockpit",
    allowedTabs: ["cockpit", "application", "aging", "statement"] as const
  });

  // Query customer (AR) and supplier (AP) exposure
  const customerExposureQuery = usePartyExposureQuery(
    legalEntityId,
    partyId,
    "Receivable",
    undefined,
    Boolean(legalEntityId && partyId)
  );

  const supplierExposureQuery = usePartyExposureQuery(
    legalEntityId,
    partyId,
    "Payable",
    undefined,
    Boolean(legalEntityId && partyId)
  );

  const handleApplyParty = (newPartyId: string) => {
    setPartyId(newPartyId);
    setSearchParams((current) => {
      const next = new URLSearchParams(current);
      next.set("partyId", newPartyId);
      next.set("direction", direction);
      return next;
    }, { replace: true });
  };

  return (
    <div className="space-y-6">
      <PageHeader
        title={t("partyFinancial.title")}
        description={t("partyFinancial.selectPrompt")}
      />

      {/* Party Switcher Bar */}
      <Surface variant="card" padding="normal" className="bg-white border border-slate-200 rounded-lg shadow-sm">
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div style={{ minWidth: "18rem", maxWidth: "30rem", flex: 1 }}>
            <PartyPicker
              label={t("partyPage.businessParty")}
              value={partyId || undefined}
              onChange={handleApplyParty}
              enableQuickCreate={false}
              placeholder={t("openItems.selectParty")}
            />
          </div>

          {/* Direction toggle */}
          <div className="flex items-center space-x-1 rtl:space-x-reverse bg-slate-100 p-1 rounded-md border border-slate-300">
            <button
              type="button"
              onClick={() => setDirection("Receivable")}
              className={`px-3 py-1.5 text-xs font-semibold rounded ${
                direction === "Receivable"
                  ? "bg-slate-900 text-white shadow-sm"
                  : "text-slate-700 hover:text-slate-900"
              }`}
            >
              {t("partyFinancial.customerSide")}
            </button>
            <button
              type="button"
              onClick={() => setDirection("Payable")}
              className={`px-3 py-1.5 text-xs font-semibold rounded ${
                direction === "Payable"
                  ? "bg-slate-900 text-white shadow-sm"
                  : "text-slate-700 hover:text-slate-900"
              }`}
            >
              {t("partyFinancial.supplierSide")}
            </button>
          </div>
        </div>
      </Surface>

      {/* Tabs */}
      <div className="border-b border-slate-300 flex items-center gap-2">
        <button
          type="button"
          onClick={() => setActiveTab("cockpit")}
          className={`px-4 py-2 text-xs font-bold border-b-2 transition-colors ${
            activeTab === "cockpit"
              ? "border-slate-900 text-slate-900"
              : "border-transparent text-slate-600 hover:text-slate-900"
          }`}
        >
          {t("partyPage.financialCockpitTab")}
        </button>

        <button
          type="button"
          onClick={() => setActiveTab("application")}
          className={`px-4 py-2 text-xs font-bold border-b-2 transition-colors ${
            activeTab === "application"
              ? "border-slate-900 text-slate-900"
              : "border-transparent text-slate-600 hover:text-slate-900"
          }`}
        >
          {t("application.title")}
        </button>

        <button
          type="button"
          onClick={() => setActiveTab("aging")}
          className={`px-4 py-2 text-xs font-bold border-b-2 transition-colors ${
            activeTab === "aging"
              ? "border-slate-900 text-slate-900"
              : "border-transparent text-slate-600 hover:text-slate-900"
          }`}
        >
          {t("aging.title")}
        </button>

        <button
          type="button"
          onClick={() => setActiveTab("statement")}
          className={`px-4 py-2 text-xs font-bold border-b-2 transition-colors ${
            activeTab === "statement"
              ? "border-slate-900 text-slate-900"
              : "border-transparent text-slate-600 hover:text-slate-900"
          }`}
        >
          {t("statement.title")}
        </button>
      </div>

      {/* Tab Content */}
      {activeTab === "cockpit" && (
        !partyId ? (
          <Surface variant="card" padding="normal" className="bg-white border border-slate-200 rounded-lg shadow-sm text-center py-12">
            <p className="text-sm font-semibold text-slate-700">{t("partyFinancial.selectPrompt")}</p>
          </Surface>
        ) : (
          <div className="space-y-6">
            {/* Dual Balance Cards: Customer AR & Supplier AP */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <SubledgerBalanceCard
                balance={customerExposureQuery.data}
                isLoading={customerExposureQuery.isLoading}
                title={t("partyFinancial.customerSide")}
              />
              <SubledgerBalanceCard
                balance={supplierExposureQuery.data}
                isLoading={supplierExposureQuery.isLoading}
                title={t("partyFinancial.supplierSide")}
              />
            </div>

            {/* Financial Profile Card */}
            <FinancialProfileCard legalEntityId={legalEntityId} partyId={partyId} />
          </div>
        )
      )}

      {activeTab === "application" && (
        <ApplicationWorkspace
          legalEntityId={legalEntityId}
          partyId={partyId}
          direction={direction}
          onCompleted={() => {
            void customerExposureQuery.refetch();
            void supplierExposureQuery.refetch();
          }}
        />
      )}

      {activeTab === "aging" && (
        <AgingReportView
          legalEntityId={legalEntityId}
          partyId={partyId}
          defaultDirection={direction}
        />
      )}

      {activeTab === "statement" && (
        <PartyStatementView
          legalEntityId={legalEntityId}
          partyId={partyId}
          defaultDirection={direction}
        />
      )}
    </div>
  );
}
