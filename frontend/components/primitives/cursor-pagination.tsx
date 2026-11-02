import React from "react";
import { useTranslation } from "react-i18next";
import { Button } from "./button.tsx";

export interface CursorPaginationProps {
  readonly page: number;
  readonly itemCount: number;
  readonly pageSize: number;
  readonly hasPrevious: boolean;
  readonly hasNext: boolean;
  readonly onPrevious: () => void;
  readonly onNext: () => void;
  readonly onPageSizeChange: (pageSize: number) => void;
  readonly isDisabled?: boolean;
  readonly pageSizeOptions?: readonly number[];
}

export function CursorPagination({
  page,
  itemCount,
  pageSize,
  hasPrevious,
  hasNext,
  onPrevious,
  onNext,
  onPageSizeChange,
  isDisabled = false,
  pageSizeOptions = [10, 20, 50, 100]
}: CursorPaginationProps): React.JSX.Element {
  const { t } = useTranslation("common");
  const start = itemCount === 0 ? 0 : (page - 1) * pageSize + 1;
  const end = itemCount === 0 ? 0 : start + itemCount - 1;
  return (
    <nav aria-label={t("pagination.navigation")} style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "0.75rem", padding: "0.75rem 0", color: "var(--color-content-muted)" }}>
      <div style={{ display: "flex", alignItems: "center", gap: "0.75rem", fontSize: "0.8125rem" }}>
        <span><bdi>{start}</bdi>–<bdi>{end}</bdi></span>
        <label style={{ display: "flex", alignItems: "center", gap: "0.375rem" }}>
          <span>{t("pagination.perPage")}</span>
          <select value={pageSize} disabled={isDisabled} onChange={(event) => onPageSizeChange(Number(event.target.value))} style={{ padding: "0.25rem 0.5rem", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-sm)", background: "var(--color-surface-card)" }}>
            {pageSizeOptions.map((option) => <option key={option} value={option}>{option}</option>)}
          </select>
        </label>
      </div>
      <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
        <Button variant="secondary" size="compact" isDisabled={isDisabled || !hasPrevious} onPress={onPrevious}>{t("pagination.previous")}</Button>
        <span aria-live="polite" style={{ minWidth: "5rem", textAlign: "center", fontSize: "0.8125rem" }}>{t("pagination.page", { page })}</span>
        <Button variant="secondary" size="compact" isDisabled={isDisabled || !hasNext} onPress={onNext}>{t("pagination.next")}</Button>
      </div>
    </nav>
  );
}
