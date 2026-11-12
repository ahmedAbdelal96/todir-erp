import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useOutletContext, useSearchParams } from "react-router-dom";
import { useOptionalLegalEntityContext, useOptionalPermissions } from "../../../core/runtime/runtime-provider.tsx";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { useIsMobile } from "../hooks/use-is-mobile.ts";
import { FixedAssetsHeader } from "../components/fixed-assets-header.tsx";
import { FixedAssetsKpiCard } from "../components/fixed-assets-kpi-card.tsx";
import { FixedAssetsPageHelp } from "../components/fixed-assets-page-help.tsx";
import { AssetRegisterTab } from "../features/registry/asset-register-tab.tsx";
import { PhysicalCustodyTab } from "../features/custody/physical-custody-tab.tsx";
import { CapitalizationTab } from "../features/capitalization/capitalization-tab.tsx";
import { DepreciationTab } from "../features/depreciation/depreciation-tab.tsx";
import { ReportsTab } from "../features/reports/reports-tab.tsx";
import { MyAssetsTab } from "../features/my-assets/my-assets-tab.tsx";
import { AssetDetailDrawer } from "../features/registry/asset-detail-drawer.tsx";
import { NewAssetModal } from "../features/modals/new-asset-modal.tsx";
import { AssignTransferModal } from "../features/modals/assign-transfer-modal.tsx";
import { ReturnAssetModal } from "../features/modals/return-asset-modal.tsx";
import { DisposeAssetModal } from "../features/modals/dispose-asset-modal.tsx";
import { ReinstateAssetModal } from "../features/modals/reinstate-asset-modal.tsx";
import { CapitalizeCipModal, SplitCipModal, CombineCipModal } from "../features/modals/cip-modals.tsx";
import { PrepareDepreciationRunModal, ReverseDepreciationRunModal } from "../features/modals/depreciation-modals.tsx";
import { NewVerificationCampaignModal, RecordAuditFindingModal } from "../features/modals/verification-modals.tsx";
import { EstimateChangeModal, CostAdditionModal, ImpairmentModal } from "../features/modals/financial-change-modals.tsx";
import { MassTransferModal } from "../features/modals/mass-transfer-modal.tsx";
import { NewLocationModal, NewCategoryModal, NewBookModal } from "../features/modals/new-config-modals.tsx";
import { useFixedAssetsQuery, useCipProjectsQuery } from "../hooks/use-fixed-assets-queries.ts";
import type {
  FixedAssetDto,
  CipProjectDto,
  DepreciationRunDto
} from "../types.ts";
import {
  BuildingIcon,
  ShieldCheckIcon,
  LayersIcon,
  CalculatorIcon,
  FileSpreadsheetIcon,
  UserIcon
} from "../components/fixed-assets-icons.tsx";

export type FixedAssetsTabId =
  | "register"
  | "custody"
  | "capitalization"
  | "depreciation"
  | "reports"
  | "my-assets";

export function FixedAssetsPage(): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");
  const isMobile = useIsMobile(768);

  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const runtimeContext = useOptionalLegalEntityContext();
  const permissions = outletCtx?.permissions ?? useOptionalPermissions();

  const [searchParams, setSearchParams] = useSearchParams();
  const activeTab = (searchParams.get("tab") as FixedAssetsTabId | null) ?? "register";

  const legalEntityId = outletCtx?.legalEntityId || runtimeContext?.legalEntityId || "";
  const legalEntityName = runtimeContext?.name;

  // Authorization checks (Fail closed)
  const canManage = Boolean(
    permissions &&
      (permissions.hasPermission("assets:view") ||
        permissions.hasPermission("assets:create") ||
        permissions.hasPermission("assets:depreciate") ||
        permissions.hasPermission("admin") ||
        permissions.hasPermission("*"))
  );

  const canCreate = Boolean(
    permissions &&
      (permissions.hasPermission("assets:create") ||
        permissions.hasPermission("admin") ||
        permissions.hasPermission("*"))
  );

  // Queries for KPI metrics
  const assetsQuery = useFixedAssetsQuery(legalEntityId, { pageSize: 100 });
  const cipQuery = useCipProjectsQuery(legalEntityId);

  const allAssets = assetsQuery.data?.items ?? [];
  const totalAssetsCount = assetsQuery.data?.totalCount ?? allAssets.length;
  const totalHistoricalCost = allAssets.reduce((sum: number, a: FixedAssetDto) => sum + (a.acquisitionCost ?? 0), 0);
  const totalAccDep = allAssets.reduce((sum: number, a: FixedAssetDto) => sum + (a.accumulatedDepreciation ?? 0), 0);
  const totalNetBookValue = allAssets.reduce((sum: number, a: FixedAssetDto) => sum + (a.netBookValue ?? 0), 0);

  // Modal / Drawer States
  const [isHelpOpen, setIsHelpOpen] = useState(false);
  const [isNewAssetOpen, setIsNewAssetOpen] = useState(false);
  const [selectedAsset, setSelectedAsset] = useState<FixedAssetDto | null>(null);
  const [isDetailOpen, setIsDetailOpen] = useState(false);

  // Specific Action Modals
  const [actionAsset, setActionAsset] = useState<FixedAssetDto | null>(null);
  const [isTransferOpen, setIsTransferOpen] = useState(false);
  const [isReturnOpen, setIsReturnOpen] = useState(false);
  const [isDisposeOpen, setIsDisposeOpen] = useState(false);
  const [isReinstateOpen, setIsReinstateOpen] = useState(false);
  const [isEstimateOpen, setIsEstimateOpen] = useState(false);
  const [isAddCostOpen, setIsAddCostOpen] = useState(false);
  const [isImpairmentOpen, setIsImpairmentOpen] = useState(false);

  // Custody Modals
  const [isNewLocationOpen, setIsNewLocationOpen] = useState(false);
  const [isNewCampaignOpen, setIsNewCampaignOpen] = useState(false);
  const [isMassTransferOpen, setIsMassTransferOpen] = useState(false);
  const [findingCampaignId, setFindingCampaignId] = useState<string | null>(null);

  // CIP Modals
  const [selectedCipProject, setSelectedCipProject] = useState<CipProjectDto | null>(null);
  const [isCapitalizeCipOpen, setIsCapitalizeCipOpen] = useState(false);
  const [isSplitCipOpen, setIsSplitCipOpen] = useState(false);
  const [isCombineCipOpen, setIsCombineCipOpen] = useState(false);

  // Depreciation Modals
  const [isPrepareRunOpen, setIsPrepareRunOpen] = useState(false);
  const [selectedRunForReversal, setSelectedRunForReversal] = useState<DepreciationRunDto | null>(null);
  const [isReverseRunOpen, setIsReverseRunOpen] = useState(false);
  const [isNewBookOpen, setIsNewBookOpen] = useState(false);
  const [isNewCategoryOpen, setIsNewCategoryOpen] = useState(false);

  const handleTabChange = (newTab: FixedAssetsTabId) => {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.set("tab", newTab);
      return next;
    });
  };

  const tabs: readonly {
    id: FixedAssetsTabId;
    label: string;
    icon: React.ReactNode;
    requiresElevated?: boolean;
  }[] = [
    { id: "register", label: t("tabs.register", "سجل الأصول (Register)"), icon: <BuildingIcon size={16} /> },
    { id: "custody", label: t("tabs.custody", "العمليات الميدانية والعهد"), icon: <ShieldCheckIcon size={16} /> },
    { id: "capitalization", label: t("tabs.capitalization", "الرسملة والمشاريع الرأسمالية (CIP)"), icon: <LayersIcon size={16} /> },
    { id: "depreciation", label: t("tabs.depreciation", "الدفاتر والاستهلاك"), icon: <CalculatorIcon size={16} />, requiresElevated: true },
    { id: "reports", label: t("tabs.reports", "المطابقة والتقارير"), icon: <FileSpreadsheetIcon size={16} /> },
    { id: "my-assets", label: t("tabs.myAssets", "أصولي وعهد العمل"), icon: <UserIcon size={16} /> }
  ];

  if (!legalEntityId) {
    return (
      <div
        data-testid="fixed-assets-page"
        style={{
          display: "flex",
          flexDirection: "column",
          minHeight: "100%",
          backgroundColor: "#f0f4f8"
        }}
      >
        <header
          style={{
            backgroundColor: "#102a43",
            color: "#ffffff",
            padding: "1.25rem 2rem",
            borderBottom: "1px solid rgba(255, 255, 255, 0.1)"
          }}
        >
          <h1 style={{ margin: 0, fontSize: "1.5rem", fontWeight: 700, color: "#ffffff" }}>
            {t("title", "منظومة الأصول الثابتة")}
          </h1>
        </header>
        <main style={{ padding: "2rem", textAlign: "center", color: "#64748b" }}>
          <div
            style={{
              maxWidth: "480px",
              margin: "2rem auto",
              padding: "2rem",
              borderRadius: "0.75rem",
              backgroundColor: "#ffffff",
              border: "1px solid #cbd5e1"
            }}
          >
            <h3 style={{ margin: "0 0 0.5rem", color: "#0f172a" }}>
              {t("common.noLegalEntitySelected", "يرجى اختيار الكيان القانوني أولاً")}
            </h3>
            <p style={{ margin: 0, fontSize: "0.875rem", color: "#64748b" }}>
              {t("common.selectLegalEntityDesc", "تتطلب عمليات الأصول الثابتة وحسابات الإهلاك تحديد كيان قانوني مسجل.")}
            </p>
          </div>
        </main>
      </div>
    );
  }

  return (
    <div
      data-testid="fixed-assets-page"
      style={{
        display: "flex",
        flexDirection: "column",
        minHeight: "100%",
        backgroundColor: "#f0f4f8"
      }}
    >
      {/* 1. Dark Oxford Navy Header */}
      <FixedAssetsHeader
        onOpenNewAssetModal={() => setIsNewAssetOpen(true)}
        onOpenHelpDrawer={() => setIsHelpOpen(false)}
        legalEntityName={legalEntityName}
        canCreate={canCreate}
      />

      {/* Main Content Area */}
      <main style={{ flex: 1, padding: isMobile ? "1rem" : "1.5rem 2rem", display: "flex", flexDirection: "column", gap: "1.25rem" }}>
        {/* 2. KPI Metrics Bar */}
        <div
          data-testid="fixed-assets-kpi-bar"
          style={{
            display: "grid",
            gridTemplateColumns: isMobile ? "1fr 1fr" : "repeat(4, 1fr)",
            gap: "1rem"
          }}
        >
          <FixedAssetsKpiCard
            title={t("kpi.totalAssets", "إجمالي الأصول المسجلة")}
            value={totalAssetsCount}
            subtitle={t("kpi.activeAndUnderConstruction", "نشط وقيد الإنشاء")}
            tone="default"
          />
          <FixedAssetsKpiCard
            title={t("kpi.historicalCost", "التكلفة التاريخية")}
            value={`${totalHistoricalCost.toLocaleString("ar-EG")} EGP`}
            subtitle={t("kpi.totalAcquisitionCost", "إجمالي تكلفة الاقتناء")}
            tone="default"
          />
          <FixedAssetsKpiCard
            title={t("kpi.accumulatedDepreciation", "مجمع الإهلاك")}
            value={`${totalAccDep.toLocaleString("ar-EG")} EGP`}
            subtitle={t("kpi.depreciatedPortion", "الأقساط المستهلكة حتى الآن")}
            tone="warning"
          />
          <FixedAssetsKpiCard
            title={t("kpi.netBookValue", "صافي القيمة الدفترية (NBV)")}
            value={`${totalNetBookValue.toLocaleString("ar-EG")} EGP`}
            subtitle={t("kpi.carryingAmount", "القيمة الدفترية في الميزانية")}
            tone="success"
          />
        </div>

        {/* 3. High-Contrast Tab Navigation Bar */}
        <div
          style={{
            display: "flex",
            gap: "0.5rem",
            backgroundColor: "#ffffff",
            padding: "0.5rem",
            borderRadius: "0.5rem",
            border: "1px solid #cbd5e1",
            overflowX: "auto"
          }}
          data-testid="fixed-assets-tabs"
        >
          {tabs.map((tab) => {
            const isActive = activeTab === tab.id;
            return (
              <button
                key={tab.id}
                type="button"
                onClick={() => handleTabChange(tab.id)}
                data-testid={`tab-${tab.id}`}
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: "0.5rem",
                  padding: "0.625rem 1rem",
                  fontSize: "0.875rem",
                  fontWeight: isActive ? 700 : 500,
                  color: isActive ? "#ffffff" : "#475569",
                  backgroundColor: isActive ? "#102a43" : "transparent",
                  border: "none",
                  borderRadius: "0.375rem",
                  cursor: "pointer",
                  whiteSpace: "nowrap",
                  transition: "all 0.15s ease"
                }}
              >
                {tab.icon}
                <span>{tab.label}</span>
              </button>
            );
          })}
        </div>

        {/* 4. Active Tab Workspace */}
        {activeTab === "register" && (
          <AssetRegisterTab
            legalEntityId={legalEntityId}
            onOpenNewAssetModal={() => setIsNewAssetOpen(true)}
            onSelectAsset={(asset) => {
              setSelectedAsset(asset);
              setIsDetailOpen(true);
            }}
            onTransferAsset={(asset) => {
              setActionAsset(asset);
              setIsTransferOpen(true);
            }}
            onReturnAsset={(asset) => {
              setActionAsset(asset);
              setIsReturnOpen(true);
            }}
            onDisposeAsset={(asset) => {
              setActionAsset(asset);
              setIsDisposeOpen(true);
            }}
            onReinstateAsset={(asset) => {
              setActionAsset(asset);
              setIsReinstateOpen(true);
            }}
          />
        )}

        {activeTab === "custody" && (
          <PhysicalCustodyTab
            legalEntityId={legalEntityId}
            onOpenNewLocationModal={() => setIsNewLocationOpen(true)}
            onOpenNewCampaignModal={() => setIsNewCampaignOpen(true)}
            onOpenMassTransferModal={() => setIsMassTransferOpen(true)}
            onOpenRecordFindingModal={(campaignId) => {
              setFindingCampaignId(campaignId);
            }}
            onSelectAsset={(asset) => {
              setSelectedAsset(asset);
              setIsDetailOpen(true);
            }}
          />
        )}

        {activeTab === "capitalization" && (
          <CapitalizationTab
            legalEntityId={legalEntityId}
            onOpenCapitalizeCipModal={(project) => {
              setSelectedCipProject(project);
              setIsCapitalizeCipOpen(true);
            }}
            onOpenSplitCipModal={(project) => {
              setSelectedCipProject(project);
              setIsSplitCipOpen(true);
            }}
            onOpenCombineCipModal={() => setIsCombineCipOpen(true)}
            onCapitalizeCandidateAsAsset={(_candidate) => {
              setIsNewAssetOpen(true);
            }}
          />
        )}

        {activeTab === "depreciation" && (
          <DepreciationTab
            legalEntityId={legalEntityId}
            onOpenPrepareRunModal={() => setIsPrepareRunOpen(true)}
            onOpenReverseRunModal={(run) => {
              setSelectedRunForReversal(run);
              setIsReverseRunOpen(true);
            }}
            onOpenNewBookModal={() => setIsNewBookOpen(true)}
          />
        )}

        {activeTab === "reports" && (
          <ReportsTab legalEntityId={legalEntityId} />
        )}

        {activeTab === "my-assets" && (
          <MyAssetsTab
            legalEntityId={legalEntityId}
            onSelectAsset={(asset) => {
              setSelectedAsset(asset);
              setIsDetailOpen(true);
            }}
            onReturnAsset={(asset) => {
              setActionAsset(asset);
              setIsReturnOpen(true);
            }}
          />
        )}
      </main>

      {/* 5. Modals & Drawers Mountings */}
      <FixedAssetsPageHelp
        isOpen={isHelpOpen}
        onClose={() => setIsHelpOpen(false)}
      />

      <NewAssetModal
        isOpen={isNewAssetOpen}
        onClose={() => setIsNewAssetOpen(false)}
        legalEntityId={legalEntityId}
      />

      <AssetDetailDrawer
        isOpen={isDetailOpen}
        onClose={() => setIsDetailOpen(false)}
        legalEntityId={legalEntityId}
        assetId={selectedAsset?.id ?? null}
        onTransferClick={() => {
          setActionAsset(selectedAsset);
          setIsTransferOpen(true);
        }}
        onReturnClick={() => {
          setActionAsset(selectedAsset);
          setIsReturnOpen(true);
        }}
        onDisposeClick={() => {
          setActionAsset(selectedAsset);
          setIsDisposeOpen(true);
        }}
        onReinstateClick={() => {
          setActionAsset(selectedAsset);
          setIsReinstateOpen(true);
        }}
        onEstimateChangeClick={() => {
          setActionAsset(selectedAsset);
          setIsEstimateOpen(true);
        }}
        onAddCostClick={() => {
          setActionAsset(selectedAsset);
          setIsAddCostOpen(true);
        }}
        onImpairmentClick={() => {
          setActionAsset(selectedAsset);
          setIsImpairmentOpen(true);
        }}
      />

      <AssignTransferModal
        isOpen={isTransferOpen}
        onClose={() => setIsTransferOpen(false)}
        legalEntityId={legalEntityId}
        assetId={actionAsset?.id ?? null}
        assetName={actionAsset?.assetName}
      />

      <ReturnAssetModal
        isOpen={isReturnOpen}
        onClose={() => setIsReturnOpen(false)}
        legalEntityId={legalEntityId}
        assetId={actionAsset?.id ?? null}
        assetName={actionAsset?.assetName}
      />

      <DisposeAssetModal
        isOpen={isDisposeOpen}
        onClose={() => setIsDisposeOpen(false)}
        legalEntityId={legalEntityId}
        assetId={actionAsset?.id ?? null}
        assetName={actionAsset?.assetName}
        currentNetBookValue={actionAsset?.netBookValue ?? 0}
      />

      <ReinstateAssetModal
        isOpen={isReinstateOpen}
        onClose={() => setIsReinstateOpen(false)}
        legalEntityId={legalEntityId}
        assetId={actionAsset?.id ?? null}
        assetName={actionAsset?.assetName}
      />

      <CapitalizeCipModal
        isOpen={isCapitalizeCipOpen}
        onClose={() => setIsCapitalizeCipOpen(false)}
        legalEntityId={legalEntityId}
        project={selectedCipProject}
      />

      <SplitCipModal
        isOpen={isSplitCipOpen}
        onClose={() => setIsSplitCipOpen(false)}
        legalEntityId={legalEntityId}
        project={selectedCipProject}
      />

      <CombineCipModal
        isOpen={isCombineCipOpen}
        onClose={() => setIsCombineCipOpen(false)}
        legalEntityId={legalEntityId}
        availableProjects={cipQuery.data?.items ?? []}
      />

      <PrepareDepreciationRunModal
        isOpen={isPrepareRunOpen}
        onClose={() => setIsPrepareRunOpen(false)}
        legalEntityId={legalEntityId}
      />

      <ReverseDepreciationRunModal
        isOpen={isReverseRunOpen}
        onClose={() => setIsReverseRunOpen(false)}
        legalEntityId={legalEntityId}
        runId={selectedRunForReversal?.id ?? null}
        periodLabel={selectedRunForReversal ? `${selectedRunForReversal.periodYear}/${selectedRunForReversal.periodMonth}` : ""}
      />

      <NewVerificationCampaignModal
        isOpen={isNewCampaignOpen}
        onClose={() => setIsNewCampaignOpen(false)}
        legalEntityId={legalEntityId}
      />

      {findingCampaignId && (
        <RecordAuditFindingModal
          isOpen={Boolean(findingCampaignId)}
          onClose={() => setFindingCampaignId(null)}
          legalEntityId={legalEntityId}
          campaignId={findingCampaignId}
        />
      )}

      <EstimateChangeModal
        isOpen={isEstimateOpen}
        onClose={() => setIsEstimateOpen(false)}
        legalEntityId={legalEntityId}
        assetId={actionAsset?.id ?? null}
        assetName={actionAsset?.assetName}
        currentLifeMonths={actionAsset?.usefulLifeMonths ?? 60}
        currentSalvageValue={actionAsset?.salvageValue ?? 0}
      />

      <CostAdditionModal
        isOpen={isAddCostOpen}
        onClose={() => setIsAddCostOpen(false)}
        legalEntityId={legalEntityId}
        assetId={actionAsset?.id ?? null}
        assetName={actionAsset?.assetName}
      />

      <ImpairmentModal
        isOpen={isImpairmentOpen}
        onClose={() => setIsImpairmentOpen(false)}
        legalEntityId={legalEntityId}
        assetId={actionAsset?.id ?? null}
        assetName={actionAsset?.assetName}
        carryingAmount={actionAsset?.netBookValue ?? 0}
      />

      <MassTransferModal
        isOpen={isMassTransferOpen}
        onClose={() => setIsMassTransferOpen(false)}
        legalEntityId={legalEntityId}
        selectedAssetIds={allAssets.slice(0, 3).map((a: FixedAssetDto) => a.id)}
        availableAssets={allAssets}
      />

      <NewLocationModal
        isOpen={isNewLocationOpen}
        onClose={() => setIsNewLocationOpen(false)}
        legalEntityId={legalEntityId}
      />

      <NewCategoryModal
        isOpen={isNewCategoryOpen}
        onClose={() => setIsNewCategoryOpen(false)}
        legalEntityId={legalEntityId}
      />

      <NewBookModal
        isOpen={isNewBookOpen}
        onClose={() => setIsNewBookOpen(false)}
        legalEntityId={legalEntityId}
      />
    </div>
  );
}
