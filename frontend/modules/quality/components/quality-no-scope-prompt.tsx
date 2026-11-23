import React from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../components/pages/surface.tsx";

export function QualityNoScopePrompt(): React.JSX.Element {
  const { t } = useTranslation("quality");

  return (
    <div data-testid="quality-no-legal-entity-prompt">
      <Surface
        style={{
          padding: "2.5rem 2rem",
          textAlign: "center",
          maxWidth: "600px",
          margin: "3rem auto"
        }}
      >
        <h3 style={{ margin: "0 0 0.5rem", fontSize: "1.125rem", fontWeight: 700, color: "#102a43" }}>
          {t("fields.legalEntity")}
        </h3>
        <p style={{ margin: "0 0 1.25rem", fontSize: "0.875rem", color: "#475569", lineHeight: 1.5 }}>
          {t("messages.noData")}
        </p>
      </Surface>
    </div>
  );
}
