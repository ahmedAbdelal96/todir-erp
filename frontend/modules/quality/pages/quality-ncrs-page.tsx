import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { QualityHeader } from "../components/quality-header.tsx";
import { QualityStatusBadge } from "../components/quality-status-badge.tsx";
import { QualityNoScopePrompt } from "../components/quality-no-scope-prompt.tsx";
import { useQualityScope } from "../hooks/use-quality-scope.ts";
import { useQualityNcrs, useQualityIssues } from "../hooks/use-quality-queries.ts";
import { Button } from "../../../components/primitives/button.tsx";
import { Surface } from "../../../components/pages/surface.tsx";
import {
  CreateNcrModal,
  CreateIssueModal,
  CreateContainmentModal,
  CreateCapaModal
} from "../components/ncr-capa-modals.tsx";

export function QualityNcrsPage(): React.JSX.Element {
  const { t } = useTranslation("quality");
  const { legalEntityId, isReady } = useQualityScope();

  const [activeTab, setActiveTab] = useState<"ncrs" | "issues">("ncrs");
  const [isCreateNcrOpen, setIsCreateNcrOpen] = useState(false);
  const [isCreateIssueOpen, setIsCreateIssueOpen] = useState(false);
  const [selectedNcrId, setSelectedNcrId] = useState<string | null>(null);
  const [isContainmentOpen, setIsContainmentOpen] = useState(false);
  const [isCapaOpen, setIsCapaOpen] = useState(false);

  const { data: ncrsPage, isLoading: ncrsLoading } = useQualityNcrs(legalEntityId);
  const { data: issuesPage, isLoading: issuesLoading } = useQualityIssues(legalEntityId);

  if (!isReady) {
    return <QualityNoScopePrompt />;
  }

  const ncrs = ncrsPage?.items ?? [];
  const issues = issuesPage?.items ?? [];

  return (
    <div data-testid="quality-ncrs-page" style={{ padding: "0 0 2rem 0" }}>
      <QualityHeader
        title={t("navigation.ncrs", "تقارير عدم المطابقة (NCR)")}
        subtitle={t("tabs.ncrs", "حالات عدم المطابقة (NCR)")}
        primaryAction={
          activeTab === "ncrs" ? (
            <Button variant="primary" onPress={() => setIsCreateNcrOpen(true)}>
              + {t("actions.createNcr", "فتح تقرير عدم مطابقة (NCR)")}
            </Button>
          ) : (
            <Button variant="primary" onPress={() => setIsCreateIssueOpen(true)}>
              + {t("actions.createIssue", "تسجيل مشكلة جودة")}
            </Button>
          )
        }
      />

      {/* Sub-view Switcher */}
      <div style={{ display: "flex", gap: "8px", marginBottom: "1.25rem" }}>
        <Button
          variant={activeTab === "ncrs" ? "primary" : "outline"}
          onPress={() => setActiveTab("ncrs")}
        >
          {t("tabs.ncrs", "تقارير عدم المطابقة")} ({ncrs.length})
        </Button>
        <Button
          variant={activeTab === "issues" ? "primary" : "outline"}
          onPress={() => setActiveTab("issues")}
        >
          {t("tabs.issues", "سجل المشاكل")} ({issues.length})
        </Button>
      </div>

      {/* Tab 1: NCRs Table */}
      {activeTab === "ncrs" && (
        <Surface style={{ padding: "1.25rem", border: "1px solid #cbd5e1", borderRadius: "8px" }}>
          {ncrsLoading ? (
            <div style={{ padding: "2.5rem", textAlign: "center", color: "#64748b" }}>{t("messages.loading", "جاري تحميل البيانات...")}</div>
          ) : ncrs.length === 0 ? (
            <div style={{ padding: "2.5rem", textAlign: "center", color: "#64748b" }}>{t("messages.noData", "لا توجد سجلات")}</div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
                <thead>
                  <tr style={{ borderBottom: "2px solid #e2e8f0", textAlign: "right", color: "#475569" }}>
                    <th style={{ padding: "10px 12px" }}>{t("fields.code", "رقم التقرير")}</th>
                    <th style={{ padding: "10px 12px" }}>{t("ncrs.title", "العنوان")}</th>
                    <th style={{ padding: "10px 12px" }}>{t("ncrs.severity", "الخطورة")}</th>
                    <th style={{ padding: "10px 12px" }}>{t("fields.status", "الحالة")}</th>
                    <th style={{ padding: "10px 12px" }}>{t("actions.viewDetails", "الإجراءات")}</th>
                  </tr>
                </thead>
                <tbody>
                  {ncrs.map((n) => (
                    <tr key={n.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                      <td style={{ padding: "10px 12px", fontWeight: 700, color: "#102a43" }}>{n.number}</td>
                      <td style={{ padding: "10px 12px", color: "#1e293b" }}>{n.title}</td>
                      <td style={{ padding: "10px 12px" }}>
                        <QualityStatusBadge status={n.severity} />
                      </td>
                      <td style={{ padding: "10px 12px" }}>
                        <QualityStatusBadge status={n.state} />
                      </td>
                      <td style={{ padding: "10px 12px" }}>
                        <div style={{ display: "flex", gap: "6px" }}>
                          <Button
                            variant="primary"
                            onPress={() => {
                              setSelectedNcrId(n.id);
                              setIsContainmentOpen(true);
                            }}
                          >
                            {t("actions.addContainment", "إجراء احتواء")}
                          </Button>
                          <Button
                            variant="outline"
                            onPress={() => {
                              setSelectedNcrId(n.id);
                              setIsCapaOpen(true);
                            }}
                          >
                            {t("actions.createCapa", "فتح CAPA")}
                          </Button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Surface>
      )}

      {/* Tab 2: Issues Log Table */}
      {activeTab === "issues" && (
        <Surface style={{ padding: "1.25rem", border: "1px solid #cbd5e1", borderRadius: "8px" }}>
          {issuesLoading ? (
            <div style={{ padding: "2.5rem", textAlign: "center", color: "#64748b" }}>{t("messages.loading", "جاري تحميل البيانات...")}</div>
          ) : issues.length === 0 ? (
            <div style={{ padding: "2.5rem", textAlign: "center", color: "#64748b" }}>{t("messages.noData", "لا توجد سجلات")}</div>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
                <thead>
                  <tr style={{ borderBottom: "2px solid #e2e8f0", textAlign: "right", color: "#475569" }}>
                    <th style={{ padding: "10px 12px" }}>{t("issues.issueType", "نوع المشكلة")}</th>
                    <th style={{ padding: "10px 12px" }}>{t("issues.title", "العنوان")}</th>
                    <th style={{ padding: "10px 12px" }}>{t("issues.severity", "الخطورة")}</th>
                    <th style={{ padding: "10px 12px" }}>{t("fields.status", "الحالة")}</th>
                  </tr>
                </thead>
                <tbody>
                  {issues.map((iss) => (
                    <tr key={iss.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                      <td style={{ padding: "10px 12px", fontWeight: 700, color: "#102a43" }}>{iss.issueType}</td>
                      <td style={{ padding: "10px 12px", color: "#1e293b" }}>{iss.title}</td>
                      <td style={{ padding: "10px 12px" }}>
                        <QualityStatusBadge status={iss.severity} />
                      </td>
                      <td style={{ padding: "10px 12px" }}>
                        <QualityStatusBadge status={iss.state} />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Surface>
      )}

      {/* Modals */}
      <CreateNcrModal
        legalEntityId={legalEntityId}
        isOpen={isCreateNcrOpen}
        onClose={() => setIsCreateNcrOpen(false)}
      />

      <CreateIssueModal
        legalEntityId={legalEntityId}
        isOpen={isCreateIssueOpen}
        onClose={() => setIsCreateIssueOpen(false)}
      />

      {selectedNcrId && (
        <CreateContainmentModal
          ncrId={selectedNcrId}
          isOpen={isContainmentOpen}
          onClose={() => {
            setIsContainmentOpen(false);
            setSelectedNcrId(null);
          }}
        />
      )}

      {selectedNcrId && (
        <CreateCapaModal
          legalEntityId={legalEntityId}
          ncrId={selectedNcrId}
          isOpen={isCapaOpen}
          onClose={() => {
            setIsCapaOpen(false);
            setSelectedNcrId(null);
          }}
        />
      )}
    </div>
  );
}
