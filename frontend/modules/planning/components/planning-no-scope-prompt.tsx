import React from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../components/pages/surface.tsx";
import { EmptyState } from "../../../components/primitives/empty-state.tsx";

export function PlanningNoScopePrompt(): React.JSX.Element {
  const { t } = useTranslation("planning");
  return (
    <Surface style={{ padding: "2.5rem", margin: "1rem 0" }}>
      <EmptyState
        title={t("title")}
        description={t("messages.noEntity")}
      />
    </Surface>
  );
}
