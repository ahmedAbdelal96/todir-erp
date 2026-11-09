import React from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { HrStatusBadge } from "../../components/hr-status-badge.tsx";
import { useMyProfile, useEmployments } from "../../hooks/use-hr-queries.ts";

export interface SelfServiceTabProps {
  readonly legalEntityId: string;
}

export function SelfServiceTab({ legalEntityId }: SelfServiceTabProps): React.JSX.Element {
  const { t } = useTranslation("hr");
  const profileQuery = useMyProfile(legalEntityId);
  const profile = profileQuery.data;

  const employmentsQuery = useEmployments(profile?.id ?? "", legalEntityId);
  const currentEmployment = employmentsQuery.data?.[0];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      <Surface title={t("selfservice.title")}>
        {profileQuery.isLoading ? (
          <p>{t("actions.loading")}</p>
        ) : profileQuery.isError ? (
          <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem", alignItems: "flex-start" }}>
            <p style={{ color: "var(--color-content-danger)" }}>{t("errors.generic")}</p>
            <Button variant="secondary" onPress={() => void profileQuery.refetch()}>
              {t("actions.retry")}
            </Button>
          </div>
        ) : !profile ? (
          <p style={{ color: "var(--color-content-muted)" }}>{t("selfservice.noProfileLinked")}</p>
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            <div style={{ display: "flex", alignItems: "center", gap: "1rem" }}>
              <h3 style={{ margin: 0, fontSize: "1.25rem" }}>{profile.arabicName}</h3>
              <HrStatusBadge status={profile.status} />
            </div>
            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "1rem", fontSize: "0.875rem" }}>
              <div><strong>{t("selfservice.employmentNumber")}:</strong> {currentEmployment?.employmentNumber || "—"}</div>
              <div><strong>{t("selfservice.employmentType")}:</strong> {currentEmployment?.type || "—"}</div>
              <div><strong>{t("selfservice.status")}:</strong> {currentEmployment?.status || "—"}</div>
            </div>
          </div>
        )}
      </Surface>
    </div>
  );
}
