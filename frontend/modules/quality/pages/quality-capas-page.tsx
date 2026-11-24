import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { QualityHeader } from "../components/quality-header.tsx";
import { QualityStatusBadge } from "../components/quality-status-badge.tsx";
import { QualityNoScopePrompt } from "../components/quality-no-scope-prompt.tsx";
import { QualityPaginationBar } from "../components/quality-pagination-bar.tsx";
import { useQualityScope } from "../hooks/use-quality-scope.ts";
import { useQualityCapas } from "../hooks/use-quality-queries.ts";
import { useKeysetPagination } from "../hooks/use-keyset-pagination.ts";
import { useQualityAccess } from "../hooks/use-quality-access.ts";
import { Button } from "../../../components/primitives/button.tsx";
import { Surface } from "../../../components/pages/surface.tsx";
import {
  CreateCapaModal,
  AddCapaActionModal,
  AddCapaRootCauseModal,
  RecordEffectivenessModal
} from "../components/ncr-capa-modals.tsx";

export function QualityCapasPage(): React.JSX.Element {
  const { t } = useTranslation("quality");
  const { legalEntityId, isReady } = useQualityScope();
  const { isReadOnly, canManageCapas } = useQualityAccess();

  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [selectedCapaId, setSelectedCapaId] = useState<string | null>(null);
  const [isAddActionOpen, setIsAddActionOpen] = useState(false);
  const [isAddRootCauseOpen, setIsAddRootCauseOpen] = useState(false);
  const [isRecordEffectivenessOpen, setIsRecordEffectivenessOpen] = useState(false);

  const pagination = useKeysetPagination();

  useEffect(() => {
    pagination.reset();
  }, [legalEntityId]);

  const { data: pageData, isLoading } = useQualityCapas(
    legalEntityId,
    null,
    undefined,
    null,
    50,
    pagination.cursor
  );

  if (!isReady) {
    return <QualityNoScopePrompt />;
  }

  const capas = pageData?.items ?? [];

  return (
    <div data-testid="quality-capas-page" style={{ padding: "0 0 2rem 0" }}>
      <QualityHeader
        title={t("navigation.capas", "الإجراءات التصحيحية والوقائية (CAPA)")}
        subtitle={t("tabs.capas", "الإجراءات التصحيحية والوقائية (CAPA)")}
        primaryAction={
          <Button
            variant="primary"
            isDisabled={!canManageCapas || isReadOnly}
            onPress={() => setIsCreateOpen(true)}
          >
            + {t("actions.createCapa", "فتح طلب CAPA جديد")}
          </Button>
        }
      />

      <Surface style={{ padding: "1.25rem", border: "1px solid #cbd5e1", borderRadius: "8px" }}>
        {isLoading ? (
          <div style={{ padding: "2.5rem", textAlign: "center", color: "#64748b" }}>{t("messages.loading", "جاري تحميل البيانات...")}</div>
        ) : capas.length === 0 ? (
          <div style={{ padding: "2.5rem", textAlign: "center", color: "#64748b" }}>{t("messages.noData", "لا توجد سجلات")}</div>
        ) : (
          <div style={{ overflowX: "auto" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
              <thead>
                <tr style={{ borderBottom: "2px solid #e2e8f0", textAlign: "right", color: "#475569" }}>
                  <th style={{ padding: "10px 12px" }}>{t("fields.code", "رقم الـ CAPA")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("capas.type", "النوع")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("capas.title", "العنوان")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("fields.status", "الحالة")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("capas.addActionTitle", "الإجراءات المعلقة")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("actions.viewDetails", "إدارة الملف")}</th>
                </tr>
              </thead>
              <tbody>
                {capas.map((c) => {
                  const isClosed = c.state === "Closed";
                  return (
                    <tr key={c.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                      <td style={{ padding: "10px 12px", fontWeight: 700, color: "#102a43" }}>{c.number}</td>
                      <td style={{ padding: "10px 12px", color: "#475569" }}>{c.type}</td>
                      <td style={{ padding: "10px 12px", color: "#1e293b" }}>{c.title}</td>
                      <td style={{ padding: "10px 12px" }}>
                        <QualityStatusBadge status={c.state} />
                      </td>
                      <td style={{ padding: "10px 12px", fontWeight: 600 }}>{c.openActionCount}</td>
                      <td style={{ padding: "10px 12px" }}>
                        <div style={{ display: "flex", gap: "6px" }}>
                          <Button
                            variant="primary"
                            isDisabled={isClosed || !canManageCapas || isReadOnly}
                            onPress={() => {
                              setSelectedCapaId(c.id);
                              setIsAddActionOpen(true);
                            }}
                          >
                            + {t("actions.addAction", "تكليف بإجراء")}
                          </Button>
                          <Button
                            variant="outline"
                            isDisabled={isClosed || !canManageCapas || isReadOnly}
                            onPress={() => {
                              setSelectedCapaId(c.id);
                              setIsAddRootCauseOpen(true);
                            }}
                          >
                            🔍 {t("actions.addRootCause", "تحليل السبب (6M)")}
                          </Button>
                          <Button
                            variant="outline"
                            isDisabled={isClosed || !canManageCapas || isReadOnly}
                            onPress={() => {
                              setSelectedCapaId(c.id);
                              setIsRecordEffectivenessOpen(true);
                            }}
                          >
                            ✓ {t("actions.recordEffectiveness", "تقييم الفعالية")}
                          </Button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
        <QualityPaginationBar
          hasPrev={pagination.hasPrev}
          hasNext={Boolean(pageData?.nextCursor)}
          onPrev={pagination.goToPrev}
          onNext={() => pagination.goToNext(pageData?.nextCursor ?? null)}
          isLoading={isLoading}
        />
      </Surface>

      {/* Modals */}
      <CreateCapaModal
        legalEntityId={legalEntityId}
        isOpen={isCreateOpen}
        onClose={() => setIsCreateOpen(false)}
      />

      {selectedCapaId && (
        <AddCapaActionModal
          capaId={selectedCapaId}
          isOpen={isAddActionOpen}
          onClose={() => {
            setIsAddActionOpen(false);
            setSelectedCapaId(null);
          }}
        />
      )}

      {selectedCapaId && (
        <AddCapaRootCauseModal
          capaId={selectedCapaId}
          isOpen={isAddRootCauseOpen}
          onClose={() => {
            setIsAddRootCauseOpen(false);
            setSelectedCapaId(null);
          }}
        />
      )}

      {selectedCapaId && (
        <RecordEffectivenessModal
          capaId={selectedCapaId}
          isOpen={isRecordEffectivenessOpen}
          onClose={() => {
            setIsRecordEffectivenessOpen(false);
            setSelectedCapaId(null);
          }}
        />
      )}
    </div>
  );
}
