import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { QualityHeader } from "../components/quality-header.tsx";
import { QualityStatusBadge } from "../components/quality-status-badge.tsx";
import { QualityNoScopePrompt } from "../components/quality-no-scope-prompt.tsx";
import { useQualityScope } from "../hooks/use-quality-scope.ts";
import { useQualityRestrictions } from "../hooks/use-quality-queries.ts";
import { Button } from "../../../components/primitives/button.tsx";
import { Surface } from "../../../components/pages/surface.tsx";
import {
  CreateRestrictionModal,
  ReleaseRestrictionModal,
  CreateDispositionModal
} from "../components/hold-disposition-modals.tsx";

export function QualityRestrictionsPage(): React.JSX.Element {
  const { t } = useTranslation("quality");
  const { legalEntityId, isReady } = useQualityScope();

  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [selectedHoldId, setSelectedHoldId] = useState<string | null>(null);
  const [selectedHoldRemaining, setSelectedHoldRemaining] = useState<number>(0);
  const [isReleaseOpen, setIsReleaseOpen] = useState(false);
  const [isDispositionOpen, setIsDispositionOpen] = useState(false);

  const { data: pageData, isLoading } = useQualityRestrictions(legalEntityId);

  if (!isReady) {
    return <QualityNoScopePrompt />;
  }

  const restrictions = pageData?.items ?? [];

  return (
    <div data-testid="quality-restrictions-page" style={{ padding: "0 0 2rem 0" }}>
      <QualityHeader
        title={t("navigation.restrictions", "حظر واحتجاز الجودة")}
        subtitle={t("tabs.restrictions", "الحظر واحتجاز الجودة (Holds)")}
        primaryAction={
          <Button variant="primary" onPress={() => setIsCreateOpen(true)}>
            + {t("actions.createRestriction", "حظر واحتجاز الدفعة")}
          </Button>
        }
      />

      <Surface style={{ padding: "1.25rem", border: "1px solid #cbd5e1", borderRadius: "8px" }}>
        {isLoading ? (
          <div style={{ padding: "2.5rem", textAlign: "center", color: "#64748b" }}>{t("messages.loading", "جاري تحميل البيانات...")}</div>
        ) : restrictions.length === 0 ? (
          <div style={{ padding: "2.5rem", textAlign: "center", color: "#64748b" }}>{t("messages.noData", "لا توجد سجلات")}</div>
        ) : (
          <div style={{ overflowX: "auto" }}>
            <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
              <thead>
                <tr style={{ borderBottom: "2px solid #e2e8f0", textAlign: "right", color: "#475569" }}>
                  <th style={{ padding: "10px 12px" }}>{t("fields.holdReason", "كود وسبب الحظر")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("fields.initialQuantity", "الكمية المحتجزة")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("fields.releasedQuantity", "المفرج عنه")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("fields.remainingQuantity", "المتبقي بالحظر")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("fields.status", "الحالة")}</th>
                  <th style={{ padding: "10px 12px" }}>{t("actions.viewDetails", "الإجراءات")}</th>
                </tr>
              </thead>
              <tbody>
                {restrictions.map((r) => {
                  const remaining = Math.max(0, (r.quantity ?? 0) - r.releasedQuantity);
                  const canRelease = r.status === "Active" && remaining > 0;
                  return (
                    <tr key={r.id} style={{ borderBottom: "1px solid #f1f5f9" }}>
                      <td style={{ padding: "10px 12px", fontWeight: 700, color: "#102a43" }}>{r.reasonCode}</td>
                      <td style={{ padding: "10px 12px" }}>{r.quantity}</td>
                      <td style={{ padding: "10px 12px", color: "#166534" }}>{r.releasedQuantity}</td>
                      <td style={{ padding: "10px 12px", fontWeight: 700, color: remaining > 0 ? "#b91c1c" : "#64748b" }}>
                        {remaining}
                      </td>
                      <td style={{ padding: "10px 12px" }}>
                        <QualityStatusBadge status={r.status} />
                      </td>
                      <td style={{ padding: "10px 12px" }}>
                        <div style={{ display: "flex", gap: "6px" }}>
                          <Button
                            variant="primary"
                            isDisabled={!canRelease}
                            onPress={() => {
                              setSelectedHoldId(r.id);
                              setSelectedHoldRemaining(remaining);
                              setIsReleaseOpen(true);
                            }}
                          >
                            {t("actions.releaseRestriction", "إفراج مخزني")}
                          </Button>
                          <Button
                            variant="outline"
                            onPress={() => {
                              setSelectedHoldId(r.id);
                              setIsDispositionOpen(true);
                            }}
                          >
                            {t("actions.createDisposition", "قرار فني")}
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
      </Surface>

      {/* Modals */}
      <CreateRestrictionModal
        legalEntityId={legalEntityId}
        isOpen={isCreateOpen}
        onClose={() => setIsCreateOpen(false)}
      />

      {selectedHoldId && (
        <ReleaseRestrictionModal
          restrictionId={selectedHoldId}
          maxQuantity={selectedHoldRemaining}
          isOpen={isReleaseOpen}
          onClose={() => {
            setIsReleaseOpen(false);
            setSelectedHoldId(null);
          }}
        />
      )}

      {selectedHoldId && (
        <CreateDispositionModal
          legalEntityId={legalEntityId}
          isOpen={isDispositionOpen}
          onClose={() => {
            setIsDispositionOpen(false);
            setSelectedHoldId(null);
          }}
        />
      )}
    </div>
  );
}
