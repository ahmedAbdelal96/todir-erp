import React, { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { QuantityDisplay } from "../../../../components/fields/quantity-display.tsx";
import { SearchField } from "../../../../components/primitives/search-field.tsx";
import { Pagination } from "../../../../components/primitives/pagination.tsx";
import { LoadingState } from "../../../../components/primitives/loading-state.tsx";
import { ErrorState } from "../../../../components/primitives/error-state.tsx";
import { EmptyState } from "../../../../components/primitives/empty-state.tsx";
import { InventoryProductDisplay } from "../../components/inventory-product-display.tsx";
import { InventoryStatusBadge } from "../../components/inventory-status-badge.tsx";
import { usePhysicalCountLines, useInventoryMutations } from "../../hooks/use-inventory-queries.ts";
import { mapInventoryError } from "../../services/inventory-error-map.ts";
import { toQty } from "../../utils.ts";
import { CountLineEntryDialog } from "./count-line-entry-dialog.tsx";

export interface CountDetailModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly countId: string;
  readonly countNumber: string;
  readonly countMode: string;
  readonly countStatus: string;
  readonly totalLines: number;
  readonly countedLines: number;
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
  totalLines,
  countedLines,
  onCountPosted
}: CountDetailModalProps): React.JSX.Element | null {
  const { t } = useTranslation("inventory");
  const isBlind = countMode === "Blind";
  const [page, setPage] = useState(1);
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [editingLineId, setEditingLineId] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [progress, setProgress] = useState({ totalLines, countedLines, status: countStatus });

  useEffect(() => {
    setProgress({ totalLines, countedLines, status: countStatus });
    setPage(1);
  }, [countId, totalLines, countedLines, countStatus]);

  // A count session can contain thousands of items. Keep the interaction bounded
  // while giving the counter a dense, useful working set on every page.
  const pageSize = 50;
  const { data, isLoading, isFetching, isError, refetch } = usePhysicalCountLines(legalEntityId, countId, { page, pageSize, search });
  const { enterPhysicalCountLine, postPhysicalCount } = useInventoryMutations(legalEntityId);
  const lines = data?.items ?? [];
  const totalPages = Math.max(1, Math.ceil((data?.totalCount ?? 0) / pageSize));
  const remainingLines = Math.max(0, progress.totalLines - progress.countedLines);
  const isPosted = progress.status === "Posted";
  const canPost = !isPosted && progress.totalLines > 0 && remainingLines === 0;

  const handlePost = async () => {
    setErrorMessage(null);
    if (!canPost) {
      setErrorMessage(t("counts.enterAllLinesBeforePosting"));
      return;
    }
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

  const handleSavedLine = async (lineId: string, countedBaseQuantity: number) => {
    setErrorMessage(null);
    try {
      const updated = await enterPhysicalCountLine.mutateAsync({ countId, lineId, request: { countedBaseQuantity } });
      setProgress({ totalLines: updated.lines, countedLines: updated.countedLines, status: updated.status });
      setEditingLineId(null);
      await refetch();
    } catch (err) {
      const mapped = mapInventoryError(err);
      setErrorMessage(t(mapped.messageKey as any, { defaultValue: mapped.fallbackAr }));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={t("counts.detailTitle")}
      description={t("counts.detailSubtitle")}
      size="wide"
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
            <bdi style={{ fontSize: "0.875rem", fontWeight: 700 }}>{countNumber}</bdi>
            <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>{t(`counts.modes.${countMode}` as any, { defaultValue: countMode })}</span>
            <InventoryStatusBadge status={progress.status} type="count" />
          </div>

          {!isPosted && (
            <Button
              variant="primary"
              onPress={handlePost}
              isDisabled={!canPost}
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

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(10rem, 1fr))", gap: "0.75rem" }}>
          <div style={{ padding: "0.75rem", backgroundColor: "var(--color-surface-subtle, #f8fafc)", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)" }}>
            <span style={{ display: "block", color: "var(--color-content-muted)", fontSize: "0.75rem" }}>{t("counts.countProgress")}</span>
            <strong style={{ fontSize: "1.125rem" }}>{progress.countedLines.toLocaleString()} / {progress.totalLines.toLocaleString()}</strong>
          </div>
          <div style={{ padding: "0.75rem", backgroundColor: remainingLines === 0 ? "#ecfdf5" : "#fffbeb", border: `1px solid ${remainingLines === 0 ? "#a7f3d0" : "#fde68a"}`, borderRadius: "var(--radius-md)" }}>
            <span style={{ display: "block", color: "var(--color-content-muted)", fontSize: "0.75rem" }}>{t("counts.remainingLines")}</span>
            <strong style={{ fontSize: "1.125rem" }}>{remainingLines.toLocaleString()}</strong>
          </div>
          <div style={{ padding: "0.75rem", backgroundColor: "#f0fdf4", border: "1px solid #bbf7d0", borderRadius: "var(--radius-md)", fontSize: "0.8125rem", color: "#166534" }}>
            {t("counts.postConfirmNotice")}
          </div>
        </div>

        {!isPosted && remainingLines > 0 && (
          <div style={{ padding: "0.625rem", backgroundColor: "#fffbeb", border: "1px solid #fde68a", borderRadius: "var(--radius-sm)", fontSize: "0.8125rem", color: "#92400e" }}>
            {t("counts.enterAllLinesBeforePosting")}
          </div>
        )}

        <SearchField
          value={searchInput}
          onChange={setSearchInput}
          onSearch={(value) => { setPage(1); setSearch(value.trim()); }}
          placeholder={t("counts.searchLinesPlaceholder")}
          isPending={isFetching}
          ariaLabel={t("counts.searchLinesPlaceholder")}
        />

        {/* Lines Table */}
        {isLoading ? <LoadingState variant="skeleton" skeletonRows={6} /> : isError ? (
          <ErrorState message={t("counts.linesLoadError")} onRetry={() => void refetch()} isRetrying={isFetching} />
        ) : lines.length === 0 ? (
          <EmptyState title={t("counts.noMatchingLines")} />
        ) : <div style={{ overflow: "auto", maxHeight: "min(58vh, 42rem)", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md)" }}>
          <table style={{ width: "100%", minWidth: "900px", borderCollapse: "collapse", fontSize: "0.875rem" }}>
            <thead style={{ position: "sticky", top: 0, zIndex: 1 }}>
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
              {lines.map((ln) => {
                  const diff = ln.difference ?? (ln.counted !== null && ln.counted !== undefined ? ln.counted - ln.expected : null);

                  return (
                    <tr key={ln.id} style={{ borderBottom: "1px solid var(--color-border-subtle, #e2e8f0)" }}>
                      <td style={{ padding: "0.625rem 0.75rem", fontWeight: 600 }}>
                        <InventoryProductDisplay productVariantId={ln.productVariantId} displayName={ln.productDisplayName} sku={ln.productSku} />
                      </td>
                      <td style={{ padding: "0.625rem 0.75rem" }}>
                        <InventoryStatusBadge status={ln.stockStatus} />
                      </td>
                      {!isBlind && (
                        <td style={{ padding: "0.625rem 0.75rem", textAlign: "end" }}>
                          <QuantityDisplay value={toQty(ln.expected, ln.baseUomCode ?? "EA")} />
                        </td>
                      )}
                      <td style={{ padding: "0.625rem 0.75rem", textAlign: "end", fontWeight: 700 }}>
                        {ln.counted !== null && ln.counted !== undefined ? (
                          <QuantityDisplay value={toQty(ln.counted, ln.baseUomCode ?? "EA")} />
                        ) : (
                          <span style={{ color: "var(--color-content-subtle)" }}>—</span>
                        )}
                      </td>
                      {!isBlind && (
                        <td style={{ padding: "0.625rem 0.75rem", textAlign: "end", fontWeight: 700, color: (diff ?? 0) < 0 ? "var(--color-status-danger)" : (diff ?? 0) > 0 ? "var(--color-status-success)" : "var(--color-content-muted)" }}>
                          {diff !== null ? <QuantityDisplay value={toQty(diff, ln.baseUomCode ?? "EA")} /> : "—"}
                        </td>
                      )}
                      {!isPosted && (
                        <td style={{ padding: "0.625rem 0.75rem", textAlign: "center" }}>
                          <Button variant="secondary" onPress={() => setEditingLineId(ln.id)}>{t("counts.enterCount")}</Button>
                        </td>
                      )}
                    </tr>
                  );
                })}
            </tbody>
          </table>
        </div>}

        {data && data.totalCount > pageSize && (
          <Pagination page={page} totalPages={totalPages} totalItems={data.totalCount} pageSize={pageSize} onPageChange={setPage} isDisabled={isFetching} size="compact" />
        )}

        <div style={{ display: "flex", justifyContent: "flex-end", marginTop: "0.5rem" }}>
          <Button variant="secondary" onPress={onClose}>
            {t("common.close")}
          </Button>
        </div>
      </div>
      {editingLineId && (() => {
        const line = lines.find((item) => item.id === editingLineId);
        return line ? <CountLineEntryDialog isOpen onClose={() => setEditingLineId(null)} line={line} isBlind={isBlind} onSave={(quantity) => handleSavedLine(line.id, quantity)} isSaving={enterPhysicalCountLine.isPending} /> : null;
      })()}
    </Dialog>
  );
}
