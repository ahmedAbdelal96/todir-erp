import React from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../components/pages/surface.tsx";

export function NoLegalEntityPrompt(): React.JSX.Element {
  const { t } = useTranslation("manufacturing");

  return (
    <div data-testid="manufacturing-no-legal-entity-prompt">
      <Surface
        style={{
          padding: "2.5rem 2rem",
          textAlign: "center",
          maxWidth: "600px",
          margin: "3rem auto"
        }}
      >
        <div style={{ fontSize: "2rem", marginBottom: "0.75rem" }}>🏢</div>
        <h3 style={{ margin: "0 0 0.5rem", fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary, #102a43)" }}>
          {t("scope.noLegalEntityTitle")}
        </h3>
        <p style={{ margin: "0 0 1.25rem", fontSize: "0.875rem", color: "var(--color-content-secondary, #475569)", lineHeight: 1.5 }}>
          {t("scope.noLegalEntityDescription")}
        </p>
      </Surface>
    </div>
  );
}
