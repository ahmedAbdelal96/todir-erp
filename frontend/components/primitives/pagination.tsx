import React from "react";
import { Button } from "./button.tsx";
import clsx from "clsx";
import { useTranslation } from "react-i18next";
import { currentIntlLocale } from "../../core/i18n/i18n.ts";

export interface PaginationProps {
  readonly page: number;
  readonly totalPages?: number | undefined;
  readonly totalItems?: number | undefined;
  readonly pageSize?: number | undefined;
  readonly onPageChange?: ((page: number) => void) | undefined;
  readonly onPageSizeChange?: ((pageSize: number) => void) | undefined;
  readonly pageSizeOptions?: readonly number[] | undefined;
  readonly isDisabled?: boolean | undefined;
  readonly size?: "comfortable" | "compact" | undefined;
  readonly className?: string | undefined;
}

export function Pagination({
  page,
  totalPages = 1,
  totalItems,
  pageSize = 20,
  onPageChange,
  onPageSizeChange,
  pageSizeOptions = [10, 20, 50, 100],
  isDisabled = false,
  size = "comfortable",
  className
}: PaginationProps): React.JSX.Element {
  const { t } = useTranslation("common");
  const safePage = Math.max(1, Math.min(page, Math.max(1, totalPages)));
  const canGoPrev = safePage > 1 && !isDisabled;
  const canGoNext = safePage < totalPages && !isDisabled;

  const labels = {
    nav: t("pagination.navigation"),
    total: t("pagination.total"),
    perPage: t("pagination.perPage"),
    prev: t("pagination.previous"),
    next: t("pagination.next"),
    pageLabel: (p: number) => t("pagination.page", { page: p })
  };

  const getPageNumbers = (): (number | "ellipsis")[] => {
    if (totalPages <= 7) return Array.from({ length: totalPages }, (_, i) => i + 1);
    if (safePage <= 4) return [1, 2, 3, 4, 5, "ellipsis", totalPages];
    if (safePage >= totalPages - 3) return [1, "ellipsis", totalPages - 4, totalPages - 3, totalPages - 2, totalPages - 1, totalPages];
    return [1, "ellipsis", safePage - 1, safePage, safePage + 1, "ellipsis", totalPages];
  };

  return (
    <nav className={clsx("ui-pagination", className)} aria-label={labels.nav} style={{ display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: "0.75rem", padding: size === "compact" ? "0.375rem 0" : "0.75rem 0", fontSize: size === "compact" ? "0.8125rem" : "0.875rem", color: "var(--color-content-primary)" }}>
      <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", color: "var(--color-content-muted)" }}>
        {totalItems !== undefined ? (
          <span>{labels.total} <strong style={{ color: "var(--color-content-primary)" }}><bdi>{totalItems.toLocaleString(currentIntlLocale())}</bdi></strong></span>
        ) : null}
        {onPageSizeChange && (
          <label style={{ display: "flex", alignItems: "center", gap: "0.375rem", marginInlineStart: "0.75rem" }}>
            <span>{labels.perPage}</span>
            <select value={pageSize} onChange={(e) => onPageSizeChange(Number(e.target.value))} disabled={isDisabled} style={{ padding: "0.25rem 0.5rem", borderRadius: "var(--radius-sm)", border: "1px solid var(--color-border-default)", backgroundColor: "var(--color-surface-card)", color: "var(--color-content-primary)", fontSize: "inherit", cursor: "pointer" }}>
              {pageSizeOptions.map((opt) => <option key={opt} value={opt}>{opt}</option>)}
            </select>
          </label>
        )}
      </div>

      <div style={{ display: "flex", alignItems: "center", gap: "0.25rem" }}>
        <Button variant="secondary" size={size} isDisabled={!canGoPrev} onPress={() => onPageChange?.(safePage - 1)} aria-label={labels.prev}>{labels.prev}</Button>
        {getPageNumbers().map((item, index) => item === "ellipsis" ? (
          <span key={`ellipsis-${index}`} style={{ padding: "0 0.375rem", color: "var(--color-content-muted)" }} aria-hidden="true">…</span>
        ) : (
          <Button key={item} variant={item === safePage ? "primary" : "secondary"} size={size} isDisabled={isDisabled} onPress={() => onPageChange?.(item)} {...(item === safePage ? { "aria-current": "page" as const } : {})} aria-label={labels.pageLabel(item)} style={{ minWidth: size === "compact" ? "32px" : "36px", padding: "0 0.5rem" }}>{item}</Button>
        ))}
        <Button variant="secondary" size={size} isDisabled={!canGoNext} onPress={() => onPageChange?.(safePage + 1)} aria-label={labels.next}>{labels.next}</Button>
      </div>
    </nav>
  );
}
