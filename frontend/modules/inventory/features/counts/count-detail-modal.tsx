import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { QuantityDisplay } from "../../../../components/fields/quantity-display.tsx";
import { InventoryProductDisplay } from "../../components/inventory-product-display.tsx";
import { InventoryStatusBadge } from "../../components/inventory-status-badge.tsx";
import { usePhysicalCountLines, useInventoryMutations } from "../../hooks/use-inventory-queries.ts";
import { mapInventoryError } from "../../services/inventory-error-map.ts";
import { toQty } from "../../utils.ts";

export interface CountDetailModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly countId: string;
  readonly countNumber: string;
  readonly countMode: string;
  readonly countStatus: string;
  readonly onCountPosted?: () => void;
}

export function CountDetailModal({
  isOpen,
  onClose,
  legalEntityId,
  countId,
  countNumber,
  countMode,
  countStatus,
  onCountPosted
}: CountDetailModalProps): React.JSX.Element | null {
  const { t } = useTranslation("inventory");

  const isBlind = countMode === "Blind";
  const isPosted = countStatus === "Posted";

  const { data: lines = [], refetch } = usePhysicalCountLines(legalEntityId, countId);
  const { enterPhysicalCountLine, postPhysicalCount } = useInventoryMutations(legalEntityId);

  const [editingLineId, setEditingLineId] = useState<string | null>(null);
  const [countedQtyInput, setCountedQtyInput] = useState<string>("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const handleStartEnter = (lineId: string, currentCounted?: number | null, expected?: number) => {
    setEditingLineId(lineId);
    setCountedQtyInput(String(currentCounted ?? (isBlind ? 0 : (expected ?? 0))));
  };

  const handleSaveLine = async (lineId: string) => {
    setErrorMessage(null);
    const num = parseFloat(countedQtyInput);
    if (isNaN(num) || num < 0) {
      setErrorMessage(t("errors.quantityInvalid"));
      return;
    }

    try {
      await enterPhysicalCountLine.mutateAsync({
        countId,
        lineId,
        request: {
          countedBaseQuantity: num
        }
      });
      setEditingLineId(null);
      refetch();
    } catch (err) {
      const mapped = mapInventoryError(err);
      setErrorMessage(t(mapped.messageKey as any, { defaultValue: mapped.fallbackAr }));
    }
  };

  const handlePost = async () => {
    setErrorMessage(null);
    try {
      const idempotencyKey = `cnt-post-${Date.now()}`;
      await postPhysicalCount.mutateAsync({
        countId,
        idempotencyKey
      });
      if (onCountPosted) onCountPosted();
      onClose();
    } catch (err) {
      const mapped = mapInventoryError(err);
      setErrorMessage(t(mapped.messageKey as any, { defaultValue: mapped.fallbackAr }));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={t("counts.title")}
      size="large"
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMessage && (
          <div role="alert" style={{ padding: "0.75rem", backgroundColor: "#fee2e2", border: "1px solid #ef4444", borderRadius: "var(--radius-sm)", color: "#b91c1c", fontSize: "0.875rem" }}>
            {errorMessage}
          </div>
        )}

        {/* Info header */}
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: "0.75rem", backgroundColor: "var(--color-surface-subtle, #f8fafc)", borderRadius: "var(--radius-md)", border: "1px solid var(--color-border-default)" }}>
          <div style={{ display: "flex", gap: "1rem", alignItems: "center" }}>
            <span style={{ fontSize: "0.875rem", fontWeight: 600 }}>{countNumber}</span>
            <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>{t(`counts.modes.${countMode}` as any, { defaultValue: countMode })}</span>
            <InventoryStatusBadge status={countStatus} type="count" />
          </div>

          {!isPosted && (
            <Button
              variant="primary"
              onPress={handlePost}
              isLoading={postPhysicalCount.isPending}
            >
              {postPhysicalCount.isPending ? t("common.posting") : t("counts.postCount")}
            </Button>
          )}
        </div>

        {/* Blind notice or Post note */}
        {isBlind && (
          <div style={{ padding: "0.625rem", backgroundColor: "#fffbeb", border: "1px solid #fde68a", borderRadius: "var(--radius-sm)", fontSize: "0.8125rem", color: "#92400e" }}>
            {t("counts.blindCountNote")}
          </div>
        )}

        {!isPosted && (
          <div style={{ padding: "0.625rem", backgroundColor: "#f0fdf4", border: "1px solid #bbf7d0", borderRadius: "var(--radius-sm)", fontSize: "0.8125rem", color: "#166534" }}>
            {t("counts.postConfirmNotice")}
          </div>
        )}

        {/* Lines Table */}
        <div style={{ overflowX: "auto", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)" }}>
          <table style={{ width: "100%", minWidth: "680px", borderCollapse: "collapse", fontSize: "0.875rem" }}>
            <thead>
              <tr style={{ backgroundColor: "var(--color-surface-subtle, #f8fafc)", borderBottom: "1px solid var(--color-border-default)" }}>
                <th style={{ padding: "0.625rem 0.75rem", textAlign: "start" }}>{t("operations.product")}</th>
                <th style={{ padding: "0.625rem 0.75rem", textAlign: "start" }}>{t("stock.status")}</th>
                {!isBlind && (
                  <th style={{ padding: "0.625rem 0.75rem", textAlign: "end" }}>{t("counts.expectedQty")}</th>
                )}
                <th style={{ padding: "0.625rem 0.75rem", textAlign: "end" }}>{t("counts.countedQty")}</th>
                {!isBlind && (
                  <th style={{ padding: "0.625rem 0.75rem", textAlign: "end" }}>{t("counts.differenceQty")}</th>
                )}
                {!isPosted && (
                  <th style={{ padding: "0.625rem 0.75rem", textAlign: "center" }}>{t("stock.actions")}</th>
                )}
              </tr>
            </thead>
            <tbody>
              {lines.length === 0 ? (
                <tr>
                  <td colSpan={isBlind ? 4 : 6} style={{ padding: "1.5rem", textAlign: "center", color: "var(--color-content-muted)" }}>
                    {t("common.empty")}
                  </td>
                </tr>
              ) : (
                lines.map((ln) => {
                  const isEditing = editingLineId === ln.id;
                  const diff = ln.difference ?? (ln.counted !== null && ln.counted !== undefined ? ln.counted - ln.expected : null);

                  return (
                    <tr key={ln.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                      <td style={{ padding: "0.625rem 0.75rem", fontWeight: 600 }}>
                        <InventoryProductDisplay productVariantId={ln.productVariantId} />
                      </td>
                      <td style={{ padding: "0.625rem 0.75rem" }}>
                        <InventoryStatusBadge status={ln.stockStatus} />
                      </td>
                      {!isBlind && (
                        <td style={{ padding: "0.625rem 0.75rem", textAlign: "end" }}>
                          <QuantityDisplay value={toQty(ln.expected)} />
                        </td>
                      )}
                      <td style={{ padding: "0.625rem 0.75rem", textAlign: "end", fontWeight: 700 }}>
                        {isEditing ? (
                          <div style={{ display: "flex", gap: "0.25rem", justifyContent: "flex-end" }}>
                            <input
                              type="number"
                              value={countedQtyInput}
                              onChange={(e) => setCountedQtyInput(e.target.value)}
                              min="0"
                              step="any"
                              style={{ width: "90px", padding: "0.25rem 0.5rem", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-default)" }}
                            />
                            <Button variant="primary" onPress={() => handleSaveLine(ln.id)}>
                              {t("common.save")}
                            </Button>
                          </div>
                        ) : ln.counted !== null && ln.counted !== undefined ? (
                          <QuantityDisplay value={toQty(ln.counted)} />
                        ) : (
                          <span style={{ color: "var(--color-content-subtle)" }}>—</span>
                        )}
                      </td>
                      {!isBlind && (
                        <td style={{ padding: "0.625rem 0.75rem", textAlign: "end", fontWeight: 700, color: (diff ?? 0) < 0 ? "var(--color-status-danger)" : (diff ?? 0) > 0 ? "var(--color-status-success)" : "var(--color-content-muted)" }}>
                          {diff !== null ? <QuantityDisplay value={toQty(diff)} /> : "—"}
                        </td>
                      )}
                      {!isPosted && (
                        <td style={{ padding: "0.625rem 0.75rem", textAlign: "center" }}>
                          {!isEditing && (
                            <Button
                              variant="secondary"
                              onPress={() => handleStartEnter(ln.id, ln.counted, ln.expected)}
                            >
                              {t("counts.enterCount")}
                            </Button>
                          )}
                        </td>
                      )}
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", marginTop: "0.5rem" }}>
          <Button variant="secondary" onPress={onClose}>
            {t("common.close")}
          </Button>
        </div>
      </div>
    </Dialog>
  );
}
