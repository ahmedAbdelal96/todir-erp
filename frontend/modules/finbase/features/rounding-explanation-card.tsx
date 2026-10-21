import React from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../components/pages/surface.tsx";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import {
  Table,
  TableHeader,
  TableBody,
  TableRow,
  TableHead,
  TableCell
} from "../../../components/primitives/table.tsx";
import type { RoundingPolicyDto } from "../types.ts";

export interface RoundingExplanationCardProps {
  readonly policies: readonly RoundingPolicyDto[];
}

export function RoundingExplanationCard({
  policies
}: RoundingExplanationCardProps): React.JSX.Element {
  const { t } = useTranslation("finbase");

  return (
    <Surface>
      <div style={{ marginBottom: "var(--space-4)" }}>
        <h3 style={{ fontSize: "var(--font-size-base)", fontWeight: 600, margin: 0 }}>
          {t("policies.roundingExplanation.title")}
        </h3>
        <p style={{ color: "var(--color-content-secondary)", fontSize: "var(--font-size-sm)", margin: "var(--space-1) 0 0 0" }}>
          {t("policies.roundingExplanation.description")}
        </p>
      </div>

      <div
        style={{
          display: "grid",
          gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))",
          gap: "var(--space-4)",
          marginBottom: "var(--space-4)"
        }}
      >
        <div
          style={{
            padding: "var(--space-3)",
            borderRadius: "var(--radius-md)",
            border: "1px solid var(--color-border-subtle)",
            backgroundColor: "var(--color-surface-subtle)"
          }}
        >
          <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: "var(--space-2)" }}>
            <span style={{ fontWeight: 600, fontSize: "var(--font-size-sm)" }}>
              {t("policies.roundingExplanation.halfUpTitle")}
            </span>
            <StatusBadge intent="info" label={t("policies.roundingExplanation.commercialStandard")} size="small" />
          </div>
          <p style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-secondary)", margin: "0 0 var(--space-2) 0" }}>
            {t("policies.roundingExplanation.halfUpDescription")}
          </p>
          <div style={{ fontSize: "var(--font-size-xs)", fontFamily: "monospace" }}>
            <div>{t("policies.roundingExplanation.halfUpExample1")}</div>
            <div>{t("policies.roundingExplanation.halfUpExample2")}</div>
          </div>
        </div>

        <div
          style={{
            padding: "var(--space-3)",
            borderRadius: "var(--radius-md)",
            border: "1px solid var(--color-border-subtle)",
            backgroundColor: "var(--color-surface-subtle)"
          }}
        >
          <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: "var(--space-2)" }}>
            <span style={{ fontWeight: 600, fontSize: "var(--font-size-sm)" }}>
              {t("policies.roundingExplanation.halfEvenTitle")}
            </span>
            <StatusBadge intent="neutral" label={t("policies.roundingExplanation.bankersRounding")} size="small" />
          </div>
          <p style={{ fontSize: "var(--font-size-xs)", color: "var(--color-content-secondary)", margin: "0 0 var(--space-2) 0" }}>
            {t("policies.roundingExplanation.halfEvenDescription")}
          </p>
          <div style={{ fontSize: "var(--font-size-xs)", fontFamily: "monospace" }}>
            <div>{t("policies.roundingExplanation.halfEvenExample1")}</div>
            <div>{t("policies.roundingExplanation.halfEvenExample2")}</div>
          </div>
        </div>
      </div>

      <div style={{ marginTop: "var(--space-4)" }}>
        <h4 style={{ fontSize: "var(--font-size-sm)", fontWeight: 600, margin: "0 0 var(--space-2) 0" }}>
          {t("policies.roundingExplanation.activePoliciesTitle")}
        </h4>
        <div style={{ overflowX: "auto", width: "100%", WebkitOverflowScrolling: "touch" }}>
          <div style={{ minWidth: "600px" }}>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>{t("policies.roundingExplanation.codeHeader")}</TableHead>
                  <TableHead>{t("policies.roundingExplanation.nameHeader")}</TableHead>
                  <TableHead>{t("policies.roundingExplanation.modeHeader")}</TableHead>
                  <TableHead>{t("policies.roundingExplanation.scaleHeader")}</TableHead>
                  <TableHead>{t("policies.roundingExplanation.cashIncrementHeader")}</TableHead>
                  <TableHead>{t("policies.roundingExplanation.strategyHeader")}</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {policies.map((p) => (
                  <TableRow key={p.id}>
                    <TableCell>
                      <span dir="ltr" style={{ fontWeight: 600 }}>{p.code}</span>
                    </TableCell>
                    <TableCell>{p.nameAr} ({p.nameEn})</TableCell>
                    <TableCell>
                      <StatusBadge
                        intent="info"
                        label={t(`policies.modes.${p.mode}`)}
                        size="small"
                      />
                    </TableCell>
                    <TableCell><span dir="ltr">{p.moneyScale}</span></TableCell>
                    <TableCell><span dir="ltr">{p.cashIncrement}</span></TableCell>
                    <TableCell>
                      {t(`policies.strategies.${p.lineDocumentStrategy}`)}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        </div>
      </div>
    </Surface>
  );
}
