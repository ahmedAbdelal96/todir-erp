import React from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Spinner } from "../../../components/primitives/spinner.tsx";
import { TaxStatusBadge } from "../components/tax-status-badge.tsx";
import { TaxDataTable, type TaxTableColumn } from "../components/tax-data-table.tsx";
import { useTaxRegistrationHistory } from "../hooks/tax-queries.ts";
import type { TaxRegistrationDto } from "../types.ts";

export interface RegistrationHistoryModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly registrationId: string;
  readonly legalEntityId: string;
}

export function RegistrationHistoryModal({
  isOpen,
  onClose,
  registrationId,
  legalEntityId
}: RegistrationHistoryModalProps): React.JSX.Element | null {
  const { t } = useTranslation("tax");
  const { data: history, isLoading } = useTaxRegistrationHistory(registrationId, legalEntityId);

  const columns: readonly TaxTableColumn<TaxRegistrationDto>[] = [
    {
      key: "version",
      header: t("registrations.version"),
      width: "90px",
      render: (item) => <span style={{ fontWeight: 600 }}>{t("registrations.versionTag", { version: item.version })}</span>
    },
    {
      key: "status",
      header: t("registrations.status"),
      width: "140px",
      render: (item) => <TaxStatusBadge status={item.status} type="registration" />
    },
    {
      key: "registrationNumber",
      header: t("registrations.registrationNumber"),
      render: (item) => item.registrationNumber ? <code>{item.registrationNumber}</code> : <span>—</span>
    },
    {
      key: "effectiveFrom",
      header: t("registrations.effectiveFrom"),
      render: (item) => <span>{item.effectiveFrom}</span>
    },
    {
      key: "effectiveTo",
      header: t("registrations.effectiveTo"),
      render: (item) => (
        <span style={{ color: item.effectiveTo ? "var(--color-content-muted)" : "var(--color-status-success-text)" }}>
          {item.effectiveTo || t("registrations.currentEffective")}
        </span>
      )
    },
    {
      key: "authority",
      header: t("registrations.authority"),
      render: (item) => <span>{item.authority || "—"}</span>
    }
  ];

  const footer = (
    <div style={{ display: "flex", justifyContent: "flex-end" }}>
      <Button variant="secondary" onPress={onClose}>
        {t("actions.close")}
      </Button>
    </div>
  );

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
      size="large"
      title={t("registrations.historyTitle")}
      description={t("registrations.historyDescription")}
      footer={footer}
    >
      {isLoading ? (
        <div style={{ display: "flex", justifyContent: "center", padding: "2rem" }}>
          <Spinner size="large" />
        </div>
      ) : !history || history.length === 0 ? (
        <div style={{ padding: "1.5rem", textAlign: "center", color: "var(--color-content-muted)" }}>
          {t("registrations.noHistory")}
        </div>
      ) : (
        <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
          <TaxDataTable
            data={history}
            columns={columns}
            rowKey={(item) => item.id}
          />
          <p style={{ margin: "0.5rem 0 0", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
            {t("registrations.immutableHistoryNotice")}
          </p>
        </div>
      )}
    </Dialog>
  );
}
