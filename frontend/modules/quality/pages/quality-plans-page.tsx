import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { QualityHeader } from "../components/quality-header.tsx";
import { QualityStatusBadge } from "../components/quality-status-badge.tsx";
import { QualityNoScopePrompt } from "../components/quality-no-scope-prompt.tsx";
import { QualityPaginationBar } from "../components/quality-pagination-bar.tsx";
import { useQualityScope } from "../hooks/use-quality-scope.ts";
import { useQualityAccess } from "../hooks/use-quality-access.ts";
import {
  useQualityPlans,
  useQualityCharacteristics,
  useQualitySamplingPlans,
  useActivateQualityPlanVersion
} from "../hooks/use-quality-queries.ts";
import { useKeysetPagination } from "../hooks/use-keyset-pagination.ts";
import { Button } from "../../../components/primitives/button.tsx";
import { Surface } from "../../../components/pages/surface.tsx";
import {
  CreatePlanModal,
  CreateVersionModal,
  CreateCharacteristicModal,
  CreateSamplingPlanModal
} from "../components/plan-modals.tsx";

export function QualityPlansPage(): React.JSX.Element {
  const { t } = useTranslation("quality");
  const { legalEntityId, isReady } = useQualityScope();
  const { isReadOnly, canManagePlans, canManageCharacteristics } = useQualityAccess();

  const [activeTab, setActiveTab] = useState<"plans" | "characteristics" | "sampling">("plans");
  const [isCreatePlanOpen, setIsCreatePlanOpen] = useState(false);
  const [selectedPlanId, setSelectedPlanId] = useState<string | null>(null);
  const [isCreateVersionOpen, setIsCreateVersionOpen] = useState(false);
  const [isCreateCharacteristicOpen, setIsCreateCharacteristicOpen] = useState(false);
  const [isCreateSamplingOpen, setIsCreateSamplingOpen] = useState(false);

  const plansPagination = useKeysetPagination();
  const charsPagination = useKeysetPagination();
  const samplingPagination = useKeysetPagination();

  useEffect(() => {
    plansPagination.reset();
    charsPagination.reset();
    samplingPagination.reset();
  }, [legalEntityId, activeTab]);

  const { data: plansPage, isLoading: plansLoading } = useQualityPlans(
    legalEntityId,
    null,
    50,
    plansPagination.cursor
  );
  const { data: characteristicsPage, isLoading: charsLoading } = useQualityCharacteristics(
    legalEntityId,
    null,
    50,
    charsPagination.cursor
  );
  const { data: samplingPage, isLoading: samplingLoading } = useQualitySamplingPlans(
    legalEntityId,
    null,
    50,
    samplingPagination.cursor
  );
  const activateVersionMutation = useActivateQualityPlanVersion();

  if (!isReady) {
    return <QualityNoScopePrompt />;
  }

  const plans = plansPage?.items ?? [];
  const characteristics = characteristicsPage?.items ?? [];
  const samplingPlans = samplingPage?.items ?? [];

  return (
    <div data-testid="quality-plans-page" style={{ padding: "0 0 2rem 0" }}>
      <QualityHeader
        title={t("navigation.plans", "خطط ومواصفات الجودة")}
        subtitle={t("tabs.plans", "خطط ومعايير الجودة")}
        primaryAction={
          activeTab === "plans" ? (
            <Button
              variant="primary"
              isDisabled={!canManagePlans || isReadOnly}
              onPress={() => setIsCreatePlanOpen(true)}
            >
              + {t("actions.createPlan", "إنشاء خطة جودة جديدة")}
            </Button>
          ) : activeTab === "characteristics" ? (
            <Button
              variant="primary"
              isDisabled={!canManageCharacteristics || isReadOnly}
              onPress={() => setIsCreateCharacteristicOpen(true)}
            >
              + {t("actions.createCharacteristic", "إضافة خاصية فحص")}
            </Button>
          ) : (
            <Button
              variant="primary"
              isDisabled={!canManagePlans || isReadOnly}
              onPress={() => setIsCreateSamplingOpen(true)}
            >
              + {t("actions.createSamplingPlan", "إنشاء خطة عينات")}
            </Button>
          )
        }
      />

      {/* Sub-view Switcher */}
      <div style={{ display: "flex", gap: "8px", marginBottom: "1.25rem" }}>
        <Button
          variant={activeTab === "plans" ? "primary" : "outline"}
          onPress={() => setActiveTab("plans")}
        >
          {t("tabs.plans", "خطط الجودة")} ({plans.length})
        </Button>
        <Button
          variant={activeTab === "characteristics" ? "primary" : "outline"}
          onPress={() => setActiveTab("characteristics")}
        >
          {t("tabs.characteristics", "خصائص الفحص")} ({characteristics.length})
        </Button>
        <Button
          variant={activeTab === "sampling" ? "primary" : "outline"}
          onPress={() => setActiveTab("sampling")}
        >
          {t("tabs.samplingPlans", "خطط العينات")} ({samplingPlans.length})
        </Button>
      </div>

      {/* Tab 1: Quality Plans List */}
      {activeTab === "plans" && (
        <Surface style={{ padding: "1.25rem", border: "1px solid #cbd5e1", borderRadius: "8px" }}>
          {plansLoading ? (
            <div style={{ padding: "2rem", textAlign: "center", color: "#64748b" }}>{t("messages.loading", "جاري تحميل البيانات...")}</div>
          ) : plans.length === 0 ? (
            <div style={{ padding: "2rem", textAlign: "center", color: "#64748b" }}>{t("messages.noData", "لا توجد سجلات")}</div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
                <thead>
                  <tr style={{ borderBottom: "2px solid #e2e8f0", textAlign: "right", color: "#475569" }}>
                    <th style={{ padding: "10px 12px" }}>{t("fields.code", "الكود")}</th>
                    <th style={{ padding: "10px 12px" }}>{t("fields.name", "الاسم")}</th>
                    <th style={{ padding: "10px 12px" }}>{t("fields.eventType", "نوع الحدث")}</th>
                    <th style={{ padding: "10px 12px" }}>{t("fields.status", "الحالة")}</th>
                    <th style={{ padding: "10px 12px" }}>{t("actions.viewDetails", "الإجراءات")}</th>
                  </tr>
                </thead>
                <tbody>
                  {plans.map((p) => (
                    <tr key={p.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                      <td style={{ padding: "10px 12px", fontWeight: 700, color: "#102a43" }}>{p.code}</td>
                      <td style={{ padding: "10px 12px", color: "#1e293b" }}>{p.name}</td>
                      <td style={{ padding: "10px 12px", color: "#64748b" }}>{p.eventType}</td>
                      <td style={{ padding: "10px 12px" }}>
                        <QualityStatusBadge status={p.status} />
                      </td>
                      <td style={{ padding: "10px 12px" }}>
                        <div style={{ display: "flex", gap: "6px" }}>
                          <Button
                            variant="outline"
                            isDisabled={!canManagePlans || isReadOnly}
                            onPress={() => {
                              setSelectedPlanId(p.id);
                              setIsCreateVersionOpen(true);
                            }}
                          >
                            + {t("actions.createVersion", "إصدار نسخة")}
                          </Button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <QualityPaginationBar
            hasPrev={plansPagination.hasPrev}
            hasNext={Boolean(plansPage?.nextCursor)}
            onPrev={plansPagination.goToPrev}
            onNext={() => plansPagination.goToNext(plansPage?.nextCursor ?? null)}
            isLoading={plansLoading}
          />
        </Surface>
      )}

      {/* Tab 2: Characteristics Catalog */}
      {activeTab === "characteristics" && (
        <Surface style={{ padding: "1.25rem", border: "1px solid #cbd5e1", borderRadius: "8px" }}>
          {charsLoading ? (
            <div style={{ padding: "2rem", textAlign: "center", color: "#64748b" }}>{t("messages.loading", "جاري تحميل البيانات...")}</div>
          ) : characteristics.length === 0 ? (
            <div style={{ padding: "2rem", textAlign: "center", color: "#64748b" }}>{t("messages.noData", "لا توجد سجلات")}</div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
                <thead>
                  <tr style={{ borderBottom: "2px solid #e2e8f0", textAlign: "right", color: "#475569" }}>
                    <th style={{ padding: "10px 12px" }}>{t("fields.code", "الكود")}</th>
                    <th style={{ padding: "10px 12px" }}>{t("fields.name", "الاسم")}</th>
                    <th style={{ padding: "10px 12px" }}>{t("fields.dataType", "نوع البيانات")}</th>
                    <th style={{ padding: "10px 12px" }}>{t("fields.uom", "وحدة القياس")}</th>
                  </tr>
                </thead>
                <tbody>
                  {characteristics.map((c) => (
                    <tr key={c.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                      <td style={{ padding: "10px 12px", fontWeight: 700, color: "#102a43" }}>{c.code}</td>
                      <td style={{ padding: "10px 12px", color: "#1e293b" }}>{c.name}</td>
                      <td style={{ padding: "10px 12px", color: "#64748b" }}>{c.dataType}</td>
                      <td style={{ padding: "10px 12px", color: "#64748b" }}>{c.baseUomCode || "-"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <QualityPaginationBar
            hasPrev={charsPagination.hasPrev}
            hasNext={Boolean(characteristicsPage?.nextCursor)}
            onPrev={charsPagination.goToPrev}
            onNext={() => charsPagination.goToNext(characteristicsPage?.nextCursor ?? null)}
            isLoading={charsLoading}
          />
        </Surface>
      )}

      {/* Tab 3: Sampling Plans */}
      {activeTab === "sampling" && (
        <Surface style={{ padding: "1.25rem", border: "1px solid #cbd5e1", borderRadius: "8px" }}>
          {samplingLoading ? (
            <div style={{ padding: "2rem", textAlign: "center", color: "#64748b" }}>{t("messages.loading", "جاري تحميل البيانات...")}</div>
          ) : samplingPlans.length === 0 ? (
            <div style={{ padding: "2rem", textAlign: "center", color: "#64748b" }}>{t("messages.noData", "لا توجد سجلات")}</div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
                <thead>
                  <tr style={{ borderBottom: "2px solid #e2e8f0", textAlign: "right", color: "#475569" }}>
                    <th style={{ padding: "10px 12px" }}>{t("fields.code", "الكود")}</th>
                    <th style={{ padding: "10px 12px" }}>{t("fields.name", "الاسم")}</th>
                    <th style={{ padding: "10px 12px" }}>{t("fields.method", "طريقة العينة")}</th>
                    <th style={{ padding: "10px 12px" }}>{t("fields.sampleSize", "حجم أو نسبة العينة")}</th>
                  </tr>
                </thead>
                <tbody>
                  {samplingPlans.map((s) => (
                    <tr key={s.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                      <td style={{ padding: "10px 12px", fontWeight: 700, color: "#102a43" }}>{s.code}</td>
                      <td style={{ padding: "10px 12px", color: "#1e293b" }}>{s.name}</td>
                      <td style={{ padding: "10px 12px", color: "#64748b" }}>
                        {t(`samplingMethods.${s.method}`, s.method)}
                      </td>
                      <td style={{ padding: "10px 12px", color: "#64748b" }}>
                        {s.fixedQuantity != null
                          ? `${s.fixedQuantity} ${t("fields.units", "وحدة")}`
                          : s.percentage != null
                            ? t("fields.percentageFormat", { value: s.percentage })
                            : "-"}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <QualityPaginationBar
            hasPrev={samplingPagination.hasPrev}
            hasNext={Boolean(samplingPage?.nextCursor)}
            onPrev={samplingPagination.goToPrev}
            onNext={() => samplingPagination.goToNext(samplingPage?.nextCursor ?? null)}
            isLoading={samplingLoading}
          />
        </Surface>
      )}

      {/* Modals */}
      <CreatePlanModal
        legalEntityId={legalEntityId}
        isOpen={isCreatePlanOpen}
        onClose={() => setIsCreatePlanOpen(false)}
      />

      {selectedPlanId && (
        <CreateVersionModal
          planId={selectedPlanId}
          isOpen={isCreateVersionOpen}
          onClose={() => {
            setIsCreateVersionOpen(false);
            setSelectedPlanId(null);
          }}
        />
      )}

      <CreateCharacteristicModal
        legalEntityId={legalEntityId}
        isOpen={isCreateCharacteristicOpen}
        onClose={() => setIsCreateCharacteristicOpen(false)}
      />

      <CreateSamplingPlanModal
        legalEntityId={legalEntityId}
        isOpen={isCreateSamplingOpen}
        onClose={() => setIsCreateSamplingOpen(false)}
      />
    </div>
  );
}
