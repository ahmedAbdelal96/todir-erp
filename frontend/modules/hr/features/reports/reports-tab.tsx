import React from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";

export interface ReportsTabProps {
  readonly legalEntityId: string;
}

export function ReportsTab({ legalEntityId }: ReportsTabProps): React.JSX.Element {
  const { t } = useTranslation("hr");

  const reports = [
    { id: "attendance", name: t("reports.attendanceReport"), desc: t("reports.attendanceDesc") },
    { id: "leave-balances", name: t("reports.leaveBalancesReport"), desc: t("reports.leaveBalancesDesc") },
    { id: "expiries", name: t("reports.expiriesReport"), desc: t("reports.expiriesDesc") }
  ];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      <Surface title={t("reports.title")}>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))", gap: "1.25rem" }}>
          {reports.map((rep) => (
            <div
              key={rep.id}
              style={{
                padding: "1rem",
                borderRadius: "var(--radius-md)",
                border: "1px solid var(--color-border-default)",
                background: "var(--color-surface-page)",
                display: "flex",
                flexDirection: "column",
                gap: "0.5rem"
              }}
            >
              <h4 style={{ margin: 0, fontSize: "1rem", color: "var(--color-content-primary)" }}>{rep.name}</h4>
              <p style={{ margin: 0, fontSize: "0.8125rem", color: "var(--color-content-muted)", flex: 1 }}>{rep.desc}</p>
              <div style={{ marginTop: "0.5rem" }}>
                <Button variant="secondary" size="compact" onPress={() => window.open(`/api/v1/hr/reports/${rep.id}/export?legalEntityId=${encodeURIComponent(legalEntityId)}`, "_blank")}>
                  {t("reports.exportExcel")}
                </Button>
              </div>
            </div>
          ))}
        </div>
      </Surface>
    </div>
  );
}
