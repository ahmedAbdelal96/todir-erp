import React from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { LedgerReadinessBadge } from "../components/ledger-status-badges.tsx";
import { useManagedLedgerSetup } from "../hooks/use-ledger-queries.ts";
import type { LedgerReadinessDto } from "../types.ts";

export interface AccountingReadinessProps {
  readonly legalEntityId: string;
  readonly readiness?: LedgerReadinessDto | undefined;
  readonly isLoading?: boolean | undefined;
  readonly canManageConfig: boolean;
}

export function AccountingReadiness({
  legalEntityId,
  readiness,
  isLoading,
  canManageConfig
}: AccountingReadinessProps): React.JSX.Element {
  const { t } = useTranslation("accounting");
  const managedSetupMutation = useManagedLedgerSetup();

  if (isLoading) {
    return (
      <Surface style={{ padding: "1.25rem", marginBottom: "1.5rem" }}>
        <p style={{ margin: 0, color: "var(--color-content-muted)" }}>{t("readiness.loading")}</p>
      </Surface>
    );
  }

  if (!readiness) {
    return (
      <Surface
        style={{
          padding: "1.5rem",
          marginBottom: "1.5rem",
          backgroundColor: "var(--color-status-warning-subtle, #fef3c7)",
          border: "1px solid var(--color-status-warning, #f59e0b)"
        }}
      >
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
          <div>
            <h3 style={{ margin: "0 0 0.5rem", color: "var(--color-content-primary)" }}>
              {t("setup.uninitializedTitle")}
            </h3>
            <p style={{ margin: 0, color: "var(--color-content-secondary)", fontSize: "0.875rem" }}>
              {t("setup.uninitializedDescription")}
            </p>
          </div>
          {canManageConfig && (
            <Button
              variant="primary"
              isLoading={managedSetupMutation.isPending}
              onPress={() => managedSetupMutation.mutate({ legalEntityId })}
            >
              {t("setup.initializeButton")}
            </Button>
          )}
        </div>
      </Surface>
    );
  }

  const allReasons = readiness.capabilities.flatMap((c) => c.reasons);
  const isHealthy = readiness.state === "Ready";

  return (
    <Surface
      style={{
        padding: "1.25rem 1.5rem",
        marginBottom: "1.5rem",
        width: "100%",
        maxWidth: "100%",
        boxSizing: "border-box",
        minWidth: 0,
        borderLeft: isHealthy ? "4px solid var(--color-status-success, #10b981)" : "4px solid var(--color-status-warning, #f59e0b)"
      }}
    >
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: "1rem", minWidth: 0 }}>
        <div style={{ minWidth: 0, maxWidth: "600px" }}>
          <div style={{ display: "flex", alignItems: "center", gap: "0.75rem", marginBottom: "0.5rem", flexWrap: "wrap" }}>
            <span style={{ fontWeight: 600, fontSize: "1rem", color: "var(--color-content-primary)" }}>
              {t("readiness.title")}
            </span>
            <LedgerReadinessBadge state={readiness.state} />
          </div>
          <p style={{ margin: 0, fontSize: "0.875rem", color: "var(--color-content-secondary)" }}>
            {isHealthy ? t("readiness.allSystemsOperational") : t("readiness.actionRequired")}
          </p>
          <p style={{ margin: "0.25rem 0 0", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
            {t("readiness.configOnlyNotice")}
          </p>
        </div>

        {/* Capabilities Grid */}
        <div style={{ display: "flex", gap: "0.75rem", flexWrap: "wrap" }}>
          {readiness.capabilities.map((cap) => (
            <div
              key={cap.capability}
              style={{
                display: "flex",
                flexDirection: "column",
                gap: "0.25rem",
                padding: "0.5rem 0.75rem",
                backgroundColor: "var(--color-surface-sunken)",
                borderRadius: "var(--radius-md, 0.5rem)",
                fontSize: "0.75rem"
              }}
            >
              <span style={{ fontWeight: 600, color: "var(--color-content-secondary)" }}>
                {t(`capabilities.${cap.capability}`, { defaultValue: cap.capability })}
              </span>
              <LedgerReadinessBadge state={cap.state} />
            </div>
          ))}
        </div>
      </div>

      {allReasons.length > 0 && (
        <div style={{ marginTop: "1rem", paddingTop: "1rem", borderTop: "1px solid var(--color-border-default)" }}>
          <span style={{ fontSize: "0.875rem", fontWeight: 600, color: "var(--color-content-primary)" }}>
            {t("readiness.details")}:
          </span>
          <ul style={{ margin: "0.5rem 0 0", paddingInlineStart: "1.25rem", fontSize: "0.875rem", color: "var(--color-content-secondary)" }}>
            {allReasons.map((r, i) => (
              <li key={`${r.code}-${i}`}>
                <strong>{r.capability ? `${t(`capabilities.${r.capability}`, { defaultValue: r.capability })}: ` : ""}</strong>
                {r.message}
              </li>
            ))}
          </ul>
        </div>
      )}
    </Surface>
  );
}
