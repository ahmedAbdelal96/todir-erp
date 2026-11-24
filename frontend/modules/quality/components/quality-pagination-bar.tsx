import React from "react";
import { useTranslation } from "react-i18next";
import { Button } from "../../../components/primitives/button.tsx";

export interface QualityPaginationBarProps {
  readonly hasPrev: boolean;
  readonly hasNext: boolean;
  readonly onPrev: () => void;
  readonly onNext: () => void;
  readonly isLoading?: boolean;
}

export function QualityPaginationBar({
  hasPrev,
  hasNext,
  onPrev,
  onNext,
  isLoading
}: QualityPaginationBarProps): React.JSX.Element {
  const { t } = useTranslation("quality");

  return (
    <div
      aria-label={t("actions.pagination", "تصفح الصفحات")}
      role="navigation"
      style={{
        display: "flex",
        justifyContent: "space-between",
        alignItems: "center",
        padding: "0.75rem 1rem",
        borderTop: "1px solid #e2e8f0",
        marginTop: "0.75rem",
        backgroundColor: "#f8fafc",
        borderRadius: "0 0 6px 6px"
      }}
    >
      <Button
        variant="outline"
        size="small"
        isDisabled={!hasPrev || isLoading}
        onPress={onPrev}
      >
        {t("actions.previous", "السابق")}
      </Button>
      <span style={{ fontSize: "0.8125rem", color: "#64748b" }}>
        {t("messages.keysetNav", "تصفح الخادم (Keyset Navigation)")}
      </span>
      <Button
        variant="outline"
        size="small"
        isDisabled={!hasNext || isLoading}
        onPress={onNext}
      >
        {t("actions.next", "التالي")}
      </Button>
    </div>
  );
}
