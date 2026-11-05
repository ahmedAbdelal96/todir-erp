import React from "react";
import { useTranslation } from "react-i18next";
import type { Opportunity, PipelineStage } from "../types.ts";
import { DealHealthBadge } from "./deal-health-badge.tsx";
import { ForecastCategoryBadge } from "./forecast-category-badge.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { LocalDateField } from "../../../components/fields/local-date-field.tsx";
import { Button } from "../../../components/primitives/button.tsx";

export interface CrmPipelineBoardProps {
  readonly stages: readonly PipelineStage[];
  readonly opportunities: readonly Opportunity[];
  readonly onSelectOpportunity: (opportunity: Opportunity) => void;
  readonly onMoveStage?: (opportunityId: string, stageId: string) => void;
  readonly onWin?: (opportunity: Opportunity) => void;
  readonly onLose?: (opportunity: Opportunity) => void;
}

const DEFAULT_CURRENCY = "EGP";

export function CrmPipelineBoard({
  stages,
  opportunities,
  onSelectOpportunity,
  onMoveStage,
  onWin,
  onLose
}: CrmPipelineBoardProps): React.JSX.Element {
  const { t } = useTranslation("crm");

  const sortedStages = [...stages].sort((a, b) => a.sequence - b.sequence);

  return (
    <div
      style={{
        display: "flex",
        gap: "1rem",
        overflowX: "auto",
        paddingBottom: "1rem",
        minHeight: "500px",
        alignItems: "flex-start"
      }}
    >
      {sortedStages.map((stage) => {
        const stageOpps = opportunities.filter((o) => o.stageId === stage.id);
        const stageTotal = stageOpps.reduce((sum, o) => sum + Number(o.estimatedAmount || 0), 0);
        const currency = stageOpps[0]?.currencyCode || DEFAULT_CURRENCY;

        return (
          <div
            key={stage.id}
            data-testid={`pipeline-column-${stage.id}`}
            style={{
              flex: "0 0 300px",
              minWidth: "280px",
              backgroundColor: "var(--color-surface-subtle, #f8fafc)",
              borderRadius: "var(--radius-lg, 0.5rem)",
              border: "1px solid var(--color-border-default, #cbd5e1)",
              display: "flex",
              flexDirection: "column",
              maxHeight: "calc(100vh - 16rem)",
              boxShadow: "0 1px 3px 0 rgba(0, 0, 0, 0.05)"
            }}
          >
            {/* Column Header */}
            <div
              style={{
                padding: "0.875rem 1rem",
                borderBottom: "1px solid var(--color-border-default, #cbd5e1)",
                backgroundColor: "var(--color-surface-card, #ffffff)",
                borderTopLeftRadius: "var(--radius-lg, 0.5rem)",
                borderTopRightRadius: "var(--radius-lg, 0.5rem)",
                display: "flex",
                flexDirection: "column",
                gap: "0.25rem"
              }}
            >
              <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
                <span style={{ fontWeight: 700, fontSize: "0.9375rem", color: "var(--color-content-primary, #0f172a)" }}>
                  {stage.name}
                </span>
                <span
                  style={{
                    backgroundColor: "var(--color-surface-subtle, #f1f5f9)",
                    padding: "0.125rem 0.5rem",
                    borderRadius: "9999px",
                    fontSize: "0.75rem",
                    fontWeight: 600,
                    color: "var(--color-content-muted, #64748b)"
                  }}
                >
                  {stageOpps.length}
                </span>
              </div>
              <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
                <span>{Math.round(stage.probability * 100)}% {t("opportunities.probability")}</span>
                <MoneyDisplay value={{ amount: String(stageTotal), currency }} size="small" />
              </div>
            </div>

            {/* Opportunity Cards List */}
            <div
              style={{
                padding: "0.75rem",
                display: "flex",
                flexDirection: "column",
                gap: "0.75rem",
                overflowY: "auto",
                flex: 1
              }}
            >
              {stageOpps.length === 0 ? (
                <div
                  style={{
                    padding: "2rem 1rem",
                    textAlign: "center",
                    fontSize: "0.8125rem",
                    color: "var(--color-content-muted, #94a3b8)",
                    border: "1px dashed var(--color-border-default, #cbd5e1)",
                    borderRadius: "var(--radius-md, 0.375rem)"
                  }}
                >
                  {t("opportunities.emptyStage")}
                </div>
              ) : (
                stageOpps.map((opp) => (
                  <div
                    key={opp.id}
                    data-testid={`opportunity-card-${opp.id}`}
                    onClick={() => onSelectOpportunity(opp)}
                    style={{
                      backgroundColor: "var(--color-surface-card, #ffffff)",
                      border: "1px solid var(--color-border-default, #cbd5e1)",
                      borderRadius: "var(--radius-md, 0.375rem)",
                      padding: "0.875rem",
                      cursor: "pointer",
                      display: "flex",
                      flexDirection: "column",
                      gap: "0.625rem",
                      boxShadow: "0 1px 2px 0 rgba(0, 0, 0, 0.05)",
                      transition: "border-color 0.15s ease, box-shadow 0.15s ease"
                    }}
                  >
                    <div style={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between", gap: "0.5rem" }}>
                      <span
                        style={{
                          fontWeight: 600,
                          fontSize: "0.875rem",
                          color: "var(--color-content-primary, #0f172a)",
                          lineHeight: 1.3
                        }}
                      >
                        {opp.name}
                      </span>
                      <DealHealthBadge status={opp.healthStatus} score={opp.healthScore} />
                    </div>

                    <div style={{ display: "flex", alignItems: "baseline", justifyContent: "space-between" }}>
                      <div style={{ fontWeight: 700, color: "var(--color-brand-primary, #102a43)" }}>
                        <MoneyDisplay
                          value={{ amount: String(opp.estimatedAmount), currency: opp.currencyCode || DEFAULT_CURRENCY }}
                          size="small"
                        />
                      </div>
                      <ForecastCategoryBadge category={opp.forecastCategory} />
                    </div>

                    {opp.expectedCloseDate && (
                      <div style={{ display: "flex", alignItems: "center", gap: "0.375rem", fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
                        <span>📅</span>
                        <span>{t("opportunities.expectedClose")}:</span>
                        <LocalDateField value={opp.expectedCloseDate} />
                      </div>
                    )}

                    {/* Quick Move / Status Actions */}
                    <div
                      style={{
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "space-between",
                        paddingTop: "0.375rem",
                        borderTop: "1px solid var(--color-surface-subtle, #f1f5f9)"
                      }}
                      onClick={(e) => e.stopPropagation()}
                    >
                      <Button
                        size="compact"
                        variant="secondary"
                        onPress={() => onSelectOpportunity(opp)}
                        style={{ fontSize: "0.75rem", padding: "0.125rem 0.5rem" }}
                      >
                        {t("actions.view")}
                      </Button>

                      <div style={{ display: "flex", gap: "0.25rem" }}>
                        {opp.status === "Open" && onWin && (
                          <button
                            type="button"
                            title={t("opportunities.markWon")}
                            onClick={() => onWin(opp)}
                            style={{
                              background: "transparent",
                              border: "none",
                              cursor: "pointer",
                              color: "var(--color-status-success, #059669)",
                              fontSize: "0.875rem",
                              padding: "0.125rem"
                            }}
                          >
                            🏆
                          </button>
                        )}
                        {opp.status === "Open" && onLose && (
                          <button
                            type="button"
                            title={t("opportunities.markLost")}
                            onClick={() => onLose(opp)}
                            style={{
                              background: "transparent",
                              border: "none",
                              cursor: "pointer",
                              color: "var(--color-status-danger, #dc2626)",
                              fontSize: "0.875rem",
                              padding: "0.125rem"
                            }}
                          >
                            ✕
                          </button>
                        )}
                      </div>
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
}
