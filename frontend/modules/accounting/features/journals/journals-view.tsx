import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { JournalTypeBadge, AccountStatusBadge } from "../../components/ledger-status-badges.tsx";
import { AccountingDataTable, type AccountingTableColumn } from "../../components/accounting-data-table.tsx";
import { JournalFormModal } from "./journal-form-modal.tsx";
import type { JournalDto } from "../../types.ts";

export interface JournalsViewProps {
  readonly legalEntityId: string;
  readonly journals: readonly JournalDto[];
  readonly isLoading: boolean;
  readonly canManageJournals: boolean;
}

export function JournalsView({
  legalEntityId,
  journals,
  isLoading,
  canManageJournals
}: JournalsViewProps): React.JSX.Element {
  const { t } = useTranslation("accounting");
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);

  const columns: readonly AccountingTableColumn<JournalDto>[] = [
    {
      key: "code",
      header: t("journals.code"),
      width: "160px",
      render: (item) => <span style={{ fontWeight: 600, fontFamily: "var(--font-mono, monospace)" }}>{item.code}</span>
    },
    {
      key: "nameAr",
      header: t("journals.nameAr"),
      render: (item) => <span style={{ fontWeight: 600 }}>{item.nameAr}</span>
    },
    {
      key: "nameEn",
      header: t("journals.nameEn"),
      render: (item) => <span style={{ color: "var(--color-content-secondary)" }}>{item.nameEn}</span>
    },
    {
      key: "type",
      header: t("journals.type"),
      width: "160px",
      render: (item) => <JournalTypeBadge type={item.type} />
    },
    {
      key: "status",
      header: t("account.status"),
      width: "120px",
      render: (item) => <AccountStatusBadge status={item.status as any} />
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
            {t("journals.title")}
          </h2>
          <p style={{ margin: "0.25rem 0 0", color: "var(--color-content-muted)", fontSize: "0.875rem" }}>
            {t("journals.description")}
          </p>
        </div>

        {canManageJournals && (
          <Button variant="primary" onPress={() => setIsCreateModalOpen(true)}>
            + {t("journals.createNew")}
          </Button>
        )}
      </div>

      {isLoading ? (
        <div style={{ padding: "3rem", textAlign: "center" }}>{t("common.loading")}</div>
      ) : (
        <AccountingDataTable
          data={journals}
          columns={columns}
          rowKey={(item) => item.id}
          emptyMessage={t("journals.noJournalsFound")}
        />
      )}

      <JournalFormModal
        isOpen={isCreateModalOpen}
        onClose={() => setIsCreateModalOpen(false)}
        legalEntityId={legalEntityId}
      />
    </Surface>
  );
}
