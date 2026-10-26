import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useParams, useSearchParams } from "react-router-dom";
import { PageHeader } from "../../../components/pages/page-header.tsx";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
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
    "11111111-1111-1111-1111-111111111111"; // Default to Seed Customer 1

  const initialDirection: SubledgerDirection =
    (searchParams.get("direction") as SubledgerDirection) || "Receivable";

  const [partyId, setPartyId] = useState<string>(initialParty);
  const [partyInput, setPartyInput] = useState<string>(initialParty);
  const [direction, setDirection] = useState<SubledgerDirection>(initialDirection);
  const [activeTab, setActiveTab] = useState<"cockpit" | "application" | "aging" | "statement">("cockpit");

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
    setPartyInput(newPartyId);
    setSearchParams({ partyId: newPartyId, direction });
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
          <div className="flex flex-wrap items-center gap-3">
            <label htmlFor="party-id-input" className="text-xs font-semibold text-slate-700">
              {t("partyPage.businessParty")}
            </label>
            <input
              id="party-id-input"
              type="text"
              placeholder={t("openItems.selectParty")}
              value={partyInput}
              onChange={(e) => setPartyInput(e.target.value)}
              className="w-64 px-3 py-1.5 text-xs font-mono font-medium border border-slate-300 rounded bg-white text-slate-900 shadow-sm focus:border-slate-800 focus:outline-none"
            />
            <Button
              variant="primary"
              size="compact"
              onClick={() => handleApplyParty(partyInput.trim())}
              isDisabled={!partyInput.trim()}
              className="bg-slate-900 hover:bg-slate-800 text-white"
            >
              {t("partyPage.loadParty")}
            </Button>

            {/* Quick Seed Buttons for Instant Inspection */}
            <div className="flex items-center gap-1.5">
              <span className="text-[11px] text-slate-500 font-semibold">{t("partyPage.sampleParties")}</span>
              <button
                type="button"
                onClick={() => handleApplyParty("11111111-1111-1111-1111-111111111111")}
                className="px-2 py-1 text-[11px] font-bold rounded bg-slate-100 hover:bg-slate-200 text-slate-800 border border-slate-300"
              >
                {t("partyPage.customer1")}
              </button>
              <button
                type="button"
                onClick={() => handleApplyParty("22222222-2222-2222-2222-222222222222")}
                className="px-2 py-1 text-[11px] font-bold rounded bg-slate-100 hover:bg-slate-200 text-slate-800 border border-slate-300"
              >
                {t("partyPage.supplier2")}
              </button>
            </div>
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
