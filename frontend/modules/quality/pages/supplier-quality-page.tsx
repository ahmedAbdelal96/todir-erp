import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { QualityHeader } from "../components/quality-header.tsx";
import { QualityStatusBadge } from "../components/quality-status-badge.tsx";
import { QualityNoScopePrompt } from "../components/quality-no-scope-prompt.tsx";
import { useQualityScope } from "../hooks/use-quality-scope.ts";
import {
  useQualitySupplierProfiles,
  useQualityScars
} from "../hooks/use-quality-queries.ts";
import { Button } from "../../../components/primitives/button.tsx";
import { Surface } from "../../../components/pages/surface.tsx";
import {
  SaveSupplierProfileModal,
  CreateScarModal,
  SubmitScarResponseModal,
  ReviewScarModal
} from "../components/supplier-certificate-modals.tsx";

export function SupplierQualityPage(): React.JSX.Element {
  const { t } = useTranslation("quality");
  const { legalEntityId, isReady } = useQualityScope();

  const [activeTab, setActiveTab] = useState<"profiles" | "scars">("profiles");
  const [isCreateProfileOpen, setIsCreateProfileOpen] = useState(false);
  const [isCreateScarOpen, setIsCreateScarOpen] = useState(false);
  const [selectedScarId, setSelectedScarId] = useState<string | null>(null);
  const [isSubmitResponseOpen, setIsSubmitResponseOpen] = useState(false);
  const [isReviewScarOpen, setIsReviewScarOpen] = useState(false);

  const { data: profilesPage, isLoading: profilesLoading } = useQualitySupplierProfiles(legalEntityId);
  const { data: scarsPage, isLoading: scarsLoading } = useQualityScars(legalEntityId);

  if (!isReady) {
    return <QualityNoScopePrompt />;
  }

  const profiles = profilesPage?.items ?? [];
  const scars = scarsPage?.items ?? [];

  return (
    <div data-testid="supplier-quality-page" style={{ padding: "0 0 2rem 0" }}>
      <QualityHeader
        title={t("navigation.supplierQuality", "جودة الموردين و(SCAR)")}
        subtitle={t("tabs.supplierQuality", "جودة الموردين و(SCAR)")}
        primaryAction={
          activeTab === "profiles" ? (
            <Button variant="primary" onPress={() => setIsCreateProfileOpen(true)}>
              + {t("actions.saveSupplierProfile", "حفظ سياسة جودة المورد")}
            </Button>
          ) : (
            <Button variant="primary" onPress={() => setIsCreateScarOpen(true)}>
              + {t("actions.createScar", "إصدار طلب SCAR للمورد")}
            </Button>
          )
        }
      />

      {/* Sub-view Switcher */}
      <div style={{ display: "flex", gap: "8px", marginBottom: "1.25rem" }}>
        <Button
          variant={activeTab === "profiles" ? "primary" : "outline"}
          onPress={() => setActiveTab("profiles")}
        >
          {t("tabs.supplierQuality", "سياسات جودة الموردين")} ({profiles.length})
        </Button>
        <Button
          variant={activeTab === "scars" ? "primary" : "outline"}
          onPress={() => setActiveTab("scars")}
        >
          {t("scars.title", "مطالبات الـ SCAR")} ({scars.length})
        </Button>
      </div>

      {/* Tab 1: Profiles List */}
      {activeTab === "profiles" && (
        <Surface style={{ padding: "1.25rem", border: "1px solid #cbd5e1", borderRadius: "8px" }}>
          {profilesLoading ? (
            <div style={{ padding: "2.5rem", textAlign: "center", color: "#64748b" }}>{t("messages.loading", "جاري تحميل البيانات...")}</div>
          ) : profiles.length === 0 ? (
            <div style={{ padding: "2.5rem", textAlign: "center", color: "#64748b" }}>{t("messages.noData", "لا توجد سجلات")}</div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
                <thead>
                  <tr style={{ borderBottom: "2px solid #e2e8f0", textAlign: "right", color: "#475569" }}>
                    <th style={{ padding: "10px 12px" }}>{t("suppliers.partyId", "كود المورد")}</th>
                    <th style={{ padding: "10px 12px" }}>{t("suppliers.riskLevel", "مستوى المخاطر")}</th>
                    <th style={{ padding: "10px 12px" }}>{t("suppliers.targetPpm", "معدل العيوب (PPM)")}</th>
                    <th style={{ padding: "10px 12px" }}>{t("suppliers.receivingInspectionRequired", "إلزامية الفحص")}</th>
                    <th style={{ padding: "10px 12px" }}>{t("fields.status", "الحالة")}</th>
                  </tr>
                </thead>
                <tbody>
                  {profiles.map((p) => (
                    <tr key={p.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                      <td style={{ padding: "10px 12px", fontWeight: 700, color: "#102a43" }}>{p.supplierPartyId}</td>
                      <td style={{ padding: "10px 12px" }}>
                        <QualityStatusBadge status={p.riskLevel} />
                      </td>
                      <td style={{ padding: "10px 12px" }}>{p.targetPpm} PPM</td>
                      <td style={{ padding: "10px 12px", color: p.receivingInspectionRequired ? "#166534" : "#64748b", fontWeight: 700 }}>
                        {p.receivingInspectionRequired ? "✓" : "-"}
                      </td>
                      <td style={{ padding: "10px 12px" }}>
                        <QualityStatusBadge status={p.status} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Surface>
      )}

      {/* Tab 2: SCAR Queue */}
      {activeTab === "scars" && (
        <Surface style={{ padding: "1.25rem", border: "1px solid #cbd5e1", borderRadius: "8px" }}>
          {scarsLoading ? (
            <div style={{ padding: "2.5rem", textAlign: "center", color: "#64748b" }}>{t("messages.loading", "جاري تحميل البيانات...")}</div>
          ) : scars.length === 0 ? (
            <div style={{ padding: "2.5rem", textAlign: "center", color: "#64748b" }}>{t("messages.noData", "لا توجد سجلات")}</div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
                <thead>
                  <tr style={{ borderBottom: "2px solid #e2e8f0", textAlign: "right", color: "#475569" }}>
                    <th style={{ padding: "10px 12px" }}>{t("fields.code", "رقم الطلب")}</th>
                    <th style={{ padding: "10px 12px" }}>{t("scars.supplierPartyId", "المورد")}</th>
                    <th style={{ padding: "10px 12px" }}>{t("scars.title", "العنوان")}</th>
                    <th style={{ padding: "10px 12px" }}>{t("scars.dueDate", "تاريخ الاستحقاق")}</th>
                    <th style={{ padding: "10px 12px" }}>{t("fields.status", "الحالة")}</th>
                    <th style={{ padding: "10px 12px" }}>{t("actions.viewDetails", "الإجراءات")}</th>
                  </tr>
                </thead>
                <tbody>
                  {scars.map((s) => {
                    const canRespond = s.state === "Issued" || s.state === "ResponsePending";
                    const canReview = s.state === "UnderReview" || s.state === "ResponsePending";

                    return (
                      <tr key={s.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                        <td style={{ padding: "10px 12px", fontWeight: 700, color: "#102a43" }}>{s.number}</td>
                        <td style={{ padding: "10px 12px", color: "#475569" }}>{s.supplierPartyId}</td>
                        <td style={{ padding: "10px 12px", color: "#1e293b" }}>{s.title}</td>
                        <td style={{ padding: "10px 12px", color: "#64748b" }}>{s.dueDate || "-"}</td>
                        <td style={{ padding: "10px 12px" }}>
                          <QualityStatusBadge status={s.state} />
                        </td>
                        <td style={{ padding: "10px 12px" }}>
                          <div style={{ display: "flex", gap: "6px" }}>
                            {canRespond && (
                              <Button
                                variant="primary"
                                onPress={() => {
                                  setSelectedScarId(s.id);
                                  setIsSubmitResponseOpen(true);
                                }}
                              >
                                📝 {t("actions.submitScarResponse", "تسجيل رد المورد")}
                              </Button>
                            )}
                            {canReview && (
                              <Button
                                variant="outline"
                                onPress={() => {
                                  setSelectedScarId(s.id);
                                  setIsReviewScarOpen(true);
                                }}
                              >
                                ✓ {t("actions.reviewScar", "مراجعة الرد")}
                              </Button>
                            )}
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </Surface>
      )}

      {/* Modals */}
      <SaveSupplierProfileModal
        legalEntityId={legalEntityId}
        isOpen={isCreateProfileOpen}
        onClose={() => setIsCreateProfileOpen(false)}
      />

      <CreateScarModal
        legalEntityId={legalEntityId}
        isOpen={isCreateScarOpen}
        onClose={() => setIsCreateScarOpen(false)}
      />

      {selectedScarId && (
        <SubmitScarResponseModal
          scarId={selectedScarId}
          isOpen={isSubmitResponseOpen}
          onClose={() => {
            setIsSubmitResponseOpen(false);
            setSelectedScarId(null);
          }}
        />
      )}

      {selectedScarId && (
        <ReviewScarModal
          scarId={selectedScarId}
          isOpen={isReviewScarOpen}
          onClose={() => {
            setIsReviewScarOpen(false);
            setSelectedScarId(null);
          }}
        />
      )}
    </div>
  );
}
