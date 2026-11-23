import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { QualityHeader } from "../components/quality-header.tsx";
import { QualityStatusBadge } from "../components/quality-status-badge.tsx";
import { QualityNoScopePrompt } from "../components/quality-no-scope-prompt.tsx";
import { useQualityScope } from "../hooks/use-quality-scope.ts";
import {
  useQualityDispositions,
  useExecuteDisposition,
  useReconcileDisposition
} from "../hooks/use-quality-queries.ts";
import { Button } from "../../../components/primitives/button.tsx";
import { Surface } from "../../../components/pages/surface.tsx";
import {
  CreateDispositionModal,
  ApproveDispositionModal
} from "../components/hold-disposition-modals.tsx";

export function QualityDispositionsPage(): React.JSX.Element {
  const { t } = useTranslation("quality");
  const { legalEntityId, isReady } = useQualityScope();

  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [selectedDispositionId, setSelectedDispositionId] = useState<string | null>(null);
  const [isApproveOpen, setIsApproveOpen] = useState(false);

  const { data: pageData, isLoading } = useQualityDispositions(legalEntityId);
  const executeMutation = useExecuteDisposition();
  const reconcileMutation = useReconcileDisposition();

  if (!isReady) {
    return <QualityNoScopePrompt />;
  }

  const dispositions = pageData?.items ?? [];

  const handleExecute = async (id: string) => {
    await executeMutation.mutateAsync(id);
  };

  const handleReconcile = async (id: string) => {
    await reconcileMutation.mutateAsync(id);
  };

  return (
    <div data-testid="quality-dispositions-page" style={{ padding: "0 0 2rem 0" }}>
      <QualityHeader
        title={t("navigation.dispositions", "القرارات الفنية والتصرف")}
        subtitle={t("tabs.dispositions", "القرارات الفنية (MRB)")}
        primaryAction={
          <Button variant="primary" onPress={() => setIsCreateOpen(true)}>
            + {t("actions.createDisposition", "إصدار قرار فني جديد")}
          </Button>
        }
      />

      <Surface style={{ padding: "1.25rem", border: "1px solid #cbd5e1", borderRadius: "8px" }}>
        {isLoading ? (
          <div style={{ padding: "2.5rem", textAlign: "center", color: "#64748b" }}>{t("messages.loading", "جاري تحميل البيانات...")}</div>
        ) : dispositions.length === 0 ? (
          <div style={{ padding: "2.5rem", textAlign: "center", color: "#64748b" }}>{t("messages.noData", "لا توجد سجلات")}</div>
        ) : (
          <div style={{ overflowX: "auto" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
              <thead>
                <tr style={{ borderBottom: "2px solid #e2e8f0", textAlign: "right", color: "#475569" }}>
                  <th style={{ padding: "10px 12px" }}>{t("fields.evaluation", "نوع القرار (Decision)")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("fields.initialQuantity", "الكمية الخاضعة")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("fields.status", "حالة القرار")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("fields.targetWarehouse", "المستودع / الأثر")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("actions.viewDetails", "الإجراءات")}</th>
                </tr>
              </thead>
              <tbody>
                {dispositions.map((d) => {
                  const canApprove = d.state === "Draft";
                  const canExecute = d.state === "Approved" && d.ownerExecutionStatus !== "Confirmed";
                  const canReconcile = d.ownerExecutionStatus === "Unknown";

                  return (
                    <tr key={d.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                      <td style={{ padding: "10px 12px", fontWeight: 700, color: "#102a43" }}>{d.decision}</td>
                      <td style={{ padding: "10px 12px" }}>{d.quantity}</td>
                      <td style={{ padding: "10px 12px" }}>
                        <QualityStatusBadge status={d.state} />
                      </td>
                      <td style={{ padding: "10px 12px" }}>
                        <QualityStatusBadge status={d.ownerExecutionStatus} />
                      </td>
                      <td style={{ padding: "10px 12px" }}>
                        <div style={{ display: "flex", gap: "6px" }}>
                          {canApprove && (
                            <Button
                              variant="primary"
                              onPress={() => {
                                setSelectedDispositionId(d.id);
                                setIsApproveOpen(true);
                              }}
                            >
                              {t("actions.approveDisposition", "اعتماد")}
                            </Button>
                          )}
                          {canExecute && (
                            <Button
                              variant="primary"
                              isDisabled={executeMutation.isPending}
                              onPress={() => handleExecute(d.id)}
                            >
                              {t("actions.executeDisposition", "تنفيذ الأثر")}
                            </Button>
                          )}
                          {canReconcile && (
                            <Button
                              variant="outline"
                              isDisabled={reconcileMutation.isPending}
                              onPress={() => handleReconcile(d.id)}
                            >
                              {t("actions.reconcileDisposition", "مطابقة الأثر")}
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

      {/* Modals */}
      <CreateDispositionModal
        legalEntityId={legalEntityId}
        isOpen={isCreateOpen}
        onClose={() => setIsCreateOpen(false)}
      />

      {selectedDispositionId && (
        <ApproveDispositionModal
          dispositionId={selectedDispositionId}
          isOpen={isApproveOpen}
          onClose={() => {
            setIsApproveOpen(false);
            setSelectedDispositionId(null);
          }}
        />
      )}
    </div>
  );
}
