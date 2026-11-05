import React from "react";
import type { PipelineStage } from "../types.ts";

export interface StageProgressBarProps {
  readonly stages: readonly PipelineStage[];
  readonly currentStageId: string;
  readonly onSelectStage?: ((stageId: string) => void) | undefined;
  readonly isReadOnly?: boolean | undefined;
}

export function StageProgressBar({
  stages,
  currentStageId,
  onSelectStage,
  isReadOnly = false
}: StageProgressBarProps): React.JSX.Element {
  const sortedStages = [...stages].sort((a, b) => a.sequence - b.sequence);
  const currentIndex = sortedStages.findIndex(s => s.id === currentStageId);

  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        gap: "0.25rem",
        overflowX: "auto",
        padding: "0.25rem 0",
        scrollbarWidth: "none"
      }}
    >
      {sortedStages.map((stage, idx) => {
        const isCurrent = stage.id === currentStageId;
        const isPassed = currentIndex !== -1 && idx < currentIndex;
        const isWon = stage.isWon;
        const isLost = stage.isLost;

        let bg = "var(--color-surface-subtle, #f1f5f9)";
        let text = "var(--color-content-muted, #64748b)";
        let border = "var(--color-border-default, #cbd5e1)";

        if (isCurrent) {
          if (isWon) {
            bg = "var(--color-status-success, #059669)";
            text = "#ffffff";
            border = "#047857";
          } else if (isLost) {
            bg = "var(--color-status-danger, #dc2626)";
            text = "#ffffff";
            border = "#b91c1c";
          } else {
            bg = "var(--color-action-primary, #2563eb)";
            text = "#ffffff";
            border = "#1d4ed8";
          }
        } else if (isPassed) {
          bg = "var(--color-surface-card, #ffffff)";
          text = "var(--color-content-primary, #0f172a)";
          border = "var(--color-action-primary, #2563eb)";
        }

        const canClick = !isReadOnly && !isCurrent && Boolean(onSelectStage);

        return (
          <button
            key={stage.id}
            type="button"
            disabled={!canClick}
            onClick={() => onSelectStage?.(stage.id)}
            style={{
              flex: 1,
              minWidth: "100px",
              padding: "0.5rem 0.75rem",
              borderRadius: "var(--radius-sm, 0.25rem)",
              backgroundColor: bg,
              color: text,
              border: `1px solid ${border}`,
              fontSize: "0.8125rem",
              fontWeight: isCurrent ? 700 : 500,
              cursor: canClick ? "pointer" : "default",
              display: "flex",
              flexDirection: "column",
              alignItems: "center",
              gap: "0.125rem",
              transition: "all 0.15s ease",
              textAlign: "center"
            }}
          >
            <span>{stage.name}</span>
            <span style={{ fontSize: "0.6875rem", opacity: 0.85 }}>
              {Math.round(stage.probability * 100)}%
            </span>
          </button>
        );
      })}
    </div>
  );
}
