import React from "react";
import { useTranslation } from "react-i18next";
import { CashRegisterIcon } from "./pos-icons.tsx";
import type { RegisterResponse, SessionResponse } from "../types.ts";

export interface PosWorkspaceHeaderProps {
  readonly title?: string;
  readonly subtitle?: string;
  readonly currentRegister?: RegisterResponse | undefined;
  readonly registers?: readonly RegisterResponse[] | undefined;
  readonly session?: SessionResponse | null | undefined;
  readonly onSelectRegister?: ((registerId: string) => void) | undefined;
}

export function PosWorkspaceHeader({
  title,
  subtitle,
  currentRegister,
  registers = [],
  session,
  onSelectRegister
}: PosWorkspaceHeaderProps): React.JSX.Element {
  const { t } = useTranslation("pos");

  const effectiveTitle = title ?? t("page.title");
  const effectiveSubtitle = subtitle ?? t("page.subtitle");

  return (
    <header
      style={{
        display: "flex",
        justifyContent: "space-between",
        alignItems: "center",
        flexWrap: "wrap",
        gap: "1rem",
        backgroundColor: "#102a43",
        borderRadius: "var(--radius-lg, 0.75rem)",
        padding: "1rem 1.5rem",
        color: "#ffffff",
        boxShadow: "0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -2px rgba(0, 0, 0, 0.1)",
      }}
    >
      <div style={{ display: "flex", alignItems: "center", gap: "1rem" }}>
        <div
          style={{
            padding: "0.625rem",
            backgroundColor: "rgba(255, 255, 255, 0.12)",
            borderRadius: "var(--radius-md, 0.5rem)",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            border: "1px solid rgba(255, 255, 255, 0.2)",
          }}
        >
          <CashRegisterIcon size={24} />
        </div>
        <div>
          <h1 style={{ margin: 0, fontSize: "1.25rem", fontWeight: 800, color: "#ffffff", letterSpacing: "-0.01em" }}>
            {effectiveTitle}
          </h1>
          <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "#94a3b8" }}>
            {effectiveSubtitle}
          </p>
        </div>
      </div>

      {currentRegister && (
        <div
          style={{
            display: "inline-flex",
            alignItems: "center",
            gap: "0.75rem",
            fontSize: "0.8125rem",
            color: "#e2e8f0",
            backgroundColor: "rgba(255, 255, 255, 0.08)",
            padding: "0.4rem 0.85rem",
            borderRadius: "var(--radius-md, 0.5rem)",
            border: "1px solid rgba(255, 255, 255, 0.15)",
            flexWrap: "wrap",
          }}
        >
          {registers.length > 1 && onSelectRegister ? (
            <select
              value={currentRegister.id}
              onChange={(e) => onSelectRegister(e.target.value)}
              aria-label={t("nav.registers")}
              style={{
                background: "rgba(0, 0, 0, 0.2)",
                color: "#ffffff",
                border: "1px solid rgba(255, 255, 255, 0.2)",
                borderRadius: "var(--radius-sm, 0.25rem)",
                padding: "0.2rem 0.5rem",
                fontSize: "0.8125rem",
              }}
            >
              {registers.map((r) => (
                <option key={r.id} value={r.id} style={{ color: "#000000" }}>
                  {r.name} ({r.code})
                </option>
              ))}
            </select>
          ) : (
            <span>{currentRegister.name} ({currentRegister.code})</span>
          )}
          <span style={{ color: "#94a3b8" }}>•</span>
          <span style={{ color: session?.status === "Open" ? "#4ade80" : "#fbbf24", fontWeight: 600 }}>
            {session?.status === "Open" ? t("workspace.online") : t("page.noActiveSessionTitle")}
          </span>
        </div>
      )}
    </header>
  );
}
