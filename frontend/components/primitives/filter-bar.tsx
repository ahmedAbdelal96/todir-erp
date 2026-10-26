import React, { type ReactNode } from "react";
import { Button } from "./button.tsx";
import clsx from "clsx";
import { useTranslation } from "react-i18next";

export interface FilterBarProps {
  readonly searchInput?: ReactNode | undefined;
  readonly activeFilterCount?: number | undefined;
  readonly onResetFilters?: (() => void) | undefined;
  readonly onApplyFilters?: (() => void) | undefined;
  readonly isPending?: boolean | undefined;
  readonly children?: ReactNode | undefined;
  readonly className?: string | undefined;
}

export function FilterBar({
  searchInput,
  activeFilterCount = 0,
  onResetFilters,
  onApplyFilters,
  isPending = false,
  children,
  className
}: FilterBarProps): React.JSX.Element {
  const { t } = useTranslation("common");
  return (
    <div
      className={clsx("ui-filter-bar", className)}
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        flexWrap: "wrap",
        gap: "0.625rem",
        padding: "0.625rem 1rem",
        backgroundColor: "var(--color-surface-card)",
        border: "1px solid var(--color-border-default)",
        borderRadius: "var(--radius-lg, 0.75rem)",
        boxShadow: "var(--shadow-sm, 0 1px 3px 0 rgba(15, 23, 42, 0.08))",
        width: "100%",
        boxSizing: "border-box"
      }}
    >
      <div
        className="ui-filter-bar-content"
        style={{
          display: "flex",
          alignItems: "center",
          gap: "0.625rem",
          flexWrap: "wrap",
          flex: 1,
          minWidth: 0
        }}
      >
        {searchInput && (
          <div className="ui-filter-bar-search" style={{ flex: "1 1 240px", minWidth: "200px", maxWidth: "340px" }}>
            {searchInput}
          </div>
        )}
        {children && (
          <div
            className="ui-filter-bar-filters"
            style={{
              display: "flex",
              alignItems: "center",
              gap: "0.5rem",
              flexWrap: "wrap",
              flex: "2 1 auto",
              minWidth: 0
            }}
          >
            {children}
          </div>
        )}
      </div>

      <div className="ui-filter-bar-actions" style={{ display: "flex", alignItems: "center", gap: "0.5rem", flexShrink: 0 }}>
        {activeFilterCount > 0 && (
          <span
            style={{
              fontSize: "0.75rem",
              fontWeight: 600,
              padding: "0.25rem 0.5rem",
              borderRadius: "var(--radius-full, 9999px)",
              backgroundColor: "var(--color-surface-primary-subtle, #eff6ff)",
              color: "var(--color-brand-primary)"
            }}
          >
            {t("components.activeFilters", { count: activeFilterCount })}
          </span>
        )}

        {activeFilterCount > 0 && onResetFilters && (
          <Button
            variant="ghost"
            size="compact"
            onPress={onResetFilters}
            isDisabled={isPending}
          >
            {t("components.resetFilters")}
          </Button>
        )}

        {onApplyFilters && (
          <Button
            variant="secondary"
            size="compact"
            onPress={onApplyFilters}
            isDisabled={isPending}
          >
            {isPending ? t("components.applyingFilters") : t("components.applyFilters")}
          </Button>
        )}
      </div>
    </div>
  );
}
