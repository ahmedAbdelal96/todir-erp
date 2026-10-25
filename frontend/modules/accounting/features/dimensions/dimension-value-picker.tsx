import React from "react";
import { useTranslation } from "react-i18next";

export interface DimensionOption {
  readonly id: string;
  readonly code: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly category: "CostCenter" | "Project" | "Department" | "Custom";
}

export const STANDARD_DIMENSIONS: readonly DimensionOption[] = [
  {
    id: "00000000-0000-0000-0000-000000000771",
    code: "CC-FIN-01",
    nameAr: "مركز تكلفة المالية",
    nameEn: "Finance Cost Center",
    category: "CostCenter"
  },
  {
    id: "00000000-0000-0000-0000-000000000772",
    code: "CC-SALES-01",
    nameAr: "مركز تكلفة المبيعات",
    nameEn: "Sales Cost Center",
    category: "CostCenter"
  },
  {
    id: "00000000-0000-0000-0000-000000000773",
    code: "PRJ-CLOUD",
    nameAr: "مشروع السحابة",
    nameEn: "Cloud Project",
    category: "Project"
  },
  {
    id: "00000000-0000-0000-0000-000000000774",
    code: "PRJ-EXP",
    nameAr: "مشروع التوسع الإقليمي",
    nameEn: "Regional Expansion",
    category: "Project"
  },
  {
    id: "00000000-0000-0000-0000-000000000775",
    code: "DEP-ACC",
    nameAr: "إدارة المحاسبة",
    nameEn: "Accounting Dept",
    category: "Department"
  }
];

export interface DimensionValuePickerProps {
  readonly value: string;
  readonly onChange: (valueId: string) => void;
  readonly isDisabled?: boolean;
}

export function DimensionValuePicker({
  value,
  onChange,
  isDisabled = false
}: DimensionValuePickerProps): React.JSX.Element {
  const { t, i18n } = useTranslation("accounting");
  const isArabic = i18n.language?.startsWith("ar");

  const selectedDim = STANDARD_DIMENSIONS.find(
    (d) => d.id.toLowerCase() === value.toLowerCase() || d.code.toLowerCase() === value.toLowerCase()
  );

  return (
    <div style={{ position: "relative", width: "100%", minWidth: "140px" }}>
      <select
        aria-label={t("dimensions.dimensionValue")}
        value={selectedDim ? selectedDim.id : value ? value : ""}
        disabled={isDisabled}
        onChange={(e) => onChange(e.target.value)}
        style={{
          width: "100%",
          padding: "0.45rem 0.625rem",
          fontSize: "0.8125rem",
          borderRadius: "var(--radius-sm, 0.25rem)",
          border: "1px solid var(--color-border-default)",
          backgroundColor: isDisabled ? "var(--color-surface-subtle)" : "var(--color-surface-card)",
          color: "var(--color-content-primary)",
          cursor: isDisabled ? "not-allowed" : "pointer"
        }}
      >
        <option value="">{t("dimensions.noDimension")}</option>
        {STANDARD_DIMENSIONS.map((dim) => {
          const categoryLabel =
            dim.category === "CostCenter"
              ? t("dimensions.costCenter")
              : dim.category === "Project"
                ? t("dimensions.project")
                : t("dimensions.department");
          const displayName = isArabic ? dim.nameAr : dim.nameEn;
          return (
            <option key={dim.id} value={dim.id}>
              [{categoryLabel}] {dim.code} — {displayName}
            </option>
          );
        })}
      </select>
    </div>
  );
}
