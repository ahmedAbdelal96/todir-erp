import React, { useState, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { QualityHeader } from "../components/quality-header.tsx";
import { QualityStatusBadge } from "../components/quality-status-badge.tsx";
import { QualityNoScopePrompt } from "../components/quality-no-scope-prompt.tsx";
import { QualityPaginationBar } from "../components/quality-pagination-bar.tsx";
import { useQualityScope } from "../hooks/use-quality-scope.ts";
import { useQualityRestrictions } from "../hooks/use-quality-queries.ts";
import { useKeysetPagination } from "../hooks/use-keyset-pagination.ts";
import { useQualityAccess } from "../hooks/use-quality-access.ts";
import type { QualityRestrictionListItem } from "../types.ts";
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
  const { isReadOnly, canManageHolds, canManageDispositions } = useQualityAccess();

  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [selectedHold, setSelectedHold] = useState<QualityRestrictionListItem | null>(null);
  const [isReleaseOpen, setIsReleaseOpen] = useState(false);
  const [isDispositionOpen, setIsDispositionOpen] = useState(false);

  const pagination = useKeysetPagination();

  useEffect(() => {
    pagination.reset();
  }, [legalEntityId]);

  const { data: pageData, isLoading } = useQualityRestrictions(
    legalEntityId,
    null,
    null,
    null,
    50,
    pagination.cursor
  );

  if (!isReady) {
    return <QualityNoScopePrompt />;
  }

  const restrictions = pageData?.items ?? [];

  return (
    <div data-testid="quality-restrictions-page" style={{ padding: "0 0 2rem 0" }}>
      <QualityHeader
        title={t("navigation.restrictions", "حظر واحتجاز الجودة")}
        subtitle={t("tabs.restrictions", "الحظر واحتجاز الجودة (Holds)")}
        showHoldBanner={true}
        primaryAction={
          <Button
            variant="primary"
            isDisabled={!canManageHolds || isReadOnly}
            onPress={() => setIsCreateOpen(true)}
          >
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
                            isDisabled={!canRelease || !canManageHolds || isReadOnly}
                            onPress={() => {
                              setSelectedHold(r);
                              setIsReleaseOpen(true);
                            }}
                          >
                            {t("actions.releaseRestriction", "إفراج مخزني")}
                          </Button>
                          <Button
                            variant="outline"
                            isDisabled={!canManageDispositions || isReadOnly}
                            onPress={() => {
                              setSelectedHold(r);
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
        <QualityPaginationBar
          hasPrev={pagination.hasPrev}
          hasNext={Boolean(pageData?.nextCursor)}
          onPrev={pagination.goToPrev}
          onNext={() => pagination.goToNext(pageData?.nextCursor ?? null)}
          isLoading={isLoading}
        />
      </Surface>

      {/* Modals */}
      <CreateRestrictionModal
        legalEntityId={legalEntityId}
        isOpen={isCreateOpen}
        onClose={() => setIsCreateOpen(false)}
      />

      {selectedHold && (
        <ReleaseRestrictionModal
          restrictionId={selectedHold.id}
          remainingQuantity={Math.max(0, (selectedHold.quantity ?? 0) - selectedHold.releasedQuantity)}
          maxQuantity={Math.max(0, (selectedHold.quantity ?? 0) - selectedHold.releasedQuantity)}
          isOpen={isReleaseOpen}
          onClose={() => {
            setIsReleaseOpen(false);
            setSelectedHold(null);
          }}
        />
      )}

      {selectedHold && (
        <CreateDispositionModal
          legalEntityId={legalEntityId}
          holdId={selectedHold.id}
          holdRemainingQuantity={Math.max(0, (selectedHold.quantity ?? 0) - selectedHold.releasedQuantity)}
          productVariantId={selectedHold.productVariantId}
          isOpen={isDispositionOpen}
          onClose={() => {
            setIsDispositionOpen(false);
            setSelectedHold(null);
          }}
        />
      )}
    </div>
  );
}
