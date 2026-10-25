import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";
import { StatusBadge } from "../../../../components/fields/status-badge.tsx";
import { AccountingDataTable, type AccountingTableColumn } from "../../components/accounting-data-table.tsx";
import { useCreateLock } from "../../hooks/use-ledger-queries.ts";
import { mapLedgerError } from "../../services/ledger-error-map.ts";
import type { FinancialLockDto, FinancialLockType } from "../../types.ts";

export interface FinancialLocksViewProps {
  readonly legalEntityId: string;
  readonly locks: readonly FinancialLockDto[];
  readonly isLoading: boolean;
  readonly canManageLocks: boolean;
}

export function FinancialLocksView({
  legalEntityId,
  locks,
  isLoading,
  canManageLocks
}: FinancialLocksViewProps): React.JSX.Element {
  const { t, i18n } = useTranslation("accounting");
  const createLockMutation = useCreateLock();

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [lockType, setLockType] = useState<FinancialLockType>("General");
  const [startDate, setStartDate] = useState("");
  const [endDate, setEndDate] = useState("");
  const [reason, setReason] = useState("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const currentLang = i18n.language?.startsWith("ar") ? "ar" : "en";

  const handleCreate = async () => {
    setErrorMessage(null);
    if (!startDate || !reason.trim()) {
      setErrorMessage(t("account.validationRequiredFields"));
      return;
    }
    try {
      await createLockMutation.mutateAsync({
        legalEntityId,
        request: {
          lockType,
          startDate,
          endDate: endDate ? endDate : null,
          reason: reason.trim()
        }
      });
      setIsModalOpen(false);
      setStartDate("");
      setEndDate("");
      setReason("");
    } catch (err) {
      setErrorMessage(mapLedgerError(err, currentLang));
    }
  };

  const columns: readonly AccountingTableColumn<FinancialLockDto>[] = [
    {
      key: "lockType",
      header: t("locks.lockType"),
      width: "140px",
      render: (item) => <span style={{ fontWeight: 600 }}>{t(`lockTypes.${item.lockType}`)}</span>
    },
    {
      key: "startDate",
      header: t("locks.startDate"),
      width: "130px",
      render: (item) => <LocalDateDisplay value={item.startDate} />
    },
    {
      key: "endDate",
      header: t("locks.endDate"),
      width: "130px",
      render: (item) => item.endDate ? <LocalDateDisplay value={item.endDate} /> : <span>{t("locks.openEnded")}</span>
    },
    {
      key: "reason",
      header: t("locks.reason"),
      render: (item) => <span>{item.reason}</span>
    },
    {
      key: "status",
      header: t("account.status"),
      width: "120px",
      render: (item) => (
        <StatusBadge
          status={item.status === "Active" ? "warning" : "neutral"}
          label={item.status === "Active" ? t("locks.statusActive") : t("locks.statusReleased")}
        />
      )
    }
  ];

  return (
    <Surface style={{ padding: "1.25rem 1.5rem", width: "100%", maxWidth: "100%", boxSizing: "border-box", minWidth: 0 }}>
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: "1rem",
          marginBottom: "1.25rem"
        }}
      >
        <div>
          <h2 style={{ margin: 0, fontSize: "1.25rem", fontWeight: 700 }}>
            {t("locks.title")}
          </h2>
          <p style={{ margin: "0.25rem 0 0", color: "var(--color-content-muted)", fontSize: "0.875rem" }}>
            {t("locks.description")}
          </p>
        </div>

        {canManageLocks && (
          <Button variant="primary" onPress={() => setIsModalOpen(true)}>
            + {t("locks.newLock")}
          </Button>
        )}
      </div>

      {isLoading ? (
        <div style={{ padding: "3rem", textAlign: "center" }}>{t("common.loading")}</div>
      ) : (
        <AccountingDataTable
          data={locks}
          columns={columns}
          rowKey={(item) => item.id}
          emptyMessage={t("locks.noLocksFound")}
        />
      )}

      {/* New Lock Dialog */}
      <Dialog
        isOpen={isModalOpen}
        onOpenChange={(open) => !open && setIsModalOpen(false)}
        title={t("locks.newLock")}
        size="medium"
        footer={
          <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
            <Button variant="secondary" onPress={() => setIsModalOpen(false)}>
              {t("common.cancel")}
            </Button>
            <Button
              variant="primary"
              isLoading={createLockMutation.isPending}
              onPress={handleCreate}
            >
              {t("common.save")}
            </Button>
          </div>
        }
      >
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", marginBottom: "0.25rem", fontSize: "0.875rem", fontWeight: 600 }}>
              {t("locks.lockType")}
            </label>
            <select
              value={lockType}
              onChange={(e) => setLockType(e.target.value as FinancialLockType)}
              style={{
                width: "100%",
                padding: "0.5rem 0.75rem",
                borderRadius: "var(--radius-md, 0.5rem)",
                border: "1px solid var(--color-border-default)",
                backgroundColor: "var(--color-surface-card)"
              }}
            >
              <option value="General">{t("lockTypes.General")}</option>
              <option value="Tax">{t("lockTypes.Tax")}</option>
              <option value="Advisor">{t("lockTypes.Advisor")}</option>
              <option value="Module">{t("lockTypes.Module")}</option>
            </select>
          </div>

          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
            <TextField
              label={t("locks.startDate")}
              value={startDate}
              onChange={setStartDate}
              placeholder="2026-01-01"
              isRequired
            />
            <TextField
              label={t("locks.endDateOptional")}
              value={endDate}
              onChange={setEndDate}
              placeholder="2026-01-31"
            />
          </div>

          <TextField
            label={t("locks.reason")}
            value={reason}
            onChange={setReason}
            placeholder={t("locks.reasonPlaceholder")}
            isRequired
          />

          {errorMessage && (
            <div style={{ padding: "0.75rem", color: "var(--color-status-danger)", backgroundColor: "#fee2e2", borderRadius: "0.5rem" }}>
              {errorMessage}
            </div>
          )}
        </div>
      </Dialog>
    </Surface>
  );
}
