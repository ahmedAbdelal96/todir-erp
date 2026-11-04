import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import {
  usePayrollOpeningBalances,
  useApproveOpeningBalanceMutation
} from "../../hooks/use-payroll-queries.ts";
import { PayrollStatusBadge } from "../../components/payroll-status-badge.tsx";
import { NewOpeningBalanceModal } from "./new-opening-balance-modal.tsx";
import { mapPayrollError } from "../../services/payroll-error-map.ts";
import type { PayrollOpeningBalance } from "../../types.ts";

export interface OpeningBalancesTabProps {
  readonly legalEntityId: string;
}

export function OpeningBalancesTab({
  legalEntityId
}: OpeningBalancesTabProps): React.JSX.Element {
  const { t } = useTranslation("payroll");
  const [searchTerm, setSearchTerm] = useState("");
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);

  const { data: items = [], isLoading } = usePayrollOpeningBalances(legalEntityId);
  const approveMutation = useApproveOpeningBalanceMutation(legalEntityId);

  const filtered = items.filter((item: PayrollOpeningBalance) => {
    if (!searchTerm.trim()) return true;
    const term = searchTerm.toLowerCase();
    return (
      item.employmentId.toLowerCase().includes(term) ||
      item.balanceType.toLowerCase().includes(term) ||
      item.sourceReference.toLowerCase().includes(term)
    );
  });

  const handleApprove = async (id: string) => {
    setActionError(null);
    try {
      await approveMutation.mutateAsync(id);
    } catch (err: unknown) {
      const mapped = mapPayrollError(err);
      setActionError(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Header Bar */}
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: "1rem",
          padding: "1.25rem 1.5rem",
          background: "var(--color-surface-card)",
          borderRadius: "var(--radius-lg)",
          border: "1px solid var(--color-border-default)",
          boxShadow: "var(--shadow-sm)"
        }}
      >
        <div>
          <h2 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-primary)" }}>
            {t("openingBalances.tabTitle")}
          </h2>
          <p style={{ margin: "0.25rem 0 0 0", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
            {t("openingBalances.tabSubtitle")}
          </p>
        </div>

        <Button
          variant="primary"
          onPress={() => setIsModalOpen(true)}
          style={{ display: "inline-flex", alignItems: "center", gap: "0.5rem" }}
        >
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <line x1="12" y1="5" x2="12" y2="19" />
            <line x1="5" y1="12" x2="19" y2="12" />
          </svg>
          {t("openingBalances.newBalanceAction")}
        </Button>
      </div>

      {actionError && (
        <div
          role="alert"
          style={{
            padding: "0.875rem 1rem",
            borderRadius: "var(--radius-md)",
            background: "var(--color-status-danger-subtle, #fee2e2)",
            color: "var(--color-status-danger, #b91c1c)",
            border: "1px solid var(--color-status-danger-border, #fecaca)",
            fontSize: "0.875rem"
          }}
        >
          {actionError}
        </div>
      )}

      {/* Filter and Search Bar */}
      <div
        style={{
          display: "flex",
          gap: "1rem",
          alignItems: "center",
          flexWrap: "wrap",
          padding: "1rem 1.25rem",
          background: "var(--color-surface-card)",
          borderRadius: "var(--radius-md)",
          border: "1px solid var(--color-border-default)"
        }}
      >
        <div style={{ flex: 1, minWidth: "240px" }}>
          <TextField
            label={t("common.search")}
            value={searchTerm}
            onChange={setSearchTerm}
            placeholder={t("openingBalances.searchPlaceholder")}
          />
        </div>
      </div>

      {/* Table */}
      <div
        style={{
          background: "var(--color-surface-card)",
          borderRadius: "var(--radius-lg)",
          border: "1px solid var(--color-border-default)",
          boxShadow: "var(--shadow-sm)",
          overflow: "hidden"
        }}
      >
        <div style={{ overflowX: "auto" }}>
          <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
            <thead>
              <tr
                style={{
                  background: "var(--color-surface-subtle)",
                  borderBottom: "1px solid var(--color-border-default)",
                  textAlign: "start"
                }}
              >
                <th style={{ padding: "0.75rem 1rem" }}>{t("openingBalances.employee")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("openingBalances.type")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("openingBalances.asOfDate")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("openingBalances.amount")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("openingBalances.status")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("openingBalances.reference")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("openingBalances.actions")}</th>
              </tr>
            </thead>
            <tbody>
              {isLoading ? (
                <tr>
                  <td colSpan={7} style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted)" }}>
                    {t("common.loading")}
                  </td>
                </tr>
              ) : filtered.length === 0 ? (
                <tr>
                  <td colSpan={7} style={{ padding: "3rem 2rem", textAlign: "center" }}>
                    <p style={{ margin: 0, fontWeight: 600, color: "var(--color-content-primary)" }}>
                      {t("openingBalances.emptyTitle")}
                    </p>
                    <p style={{ margin: "0.25rem 0 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                      {t("openingBalances.emptyDesc")}
                    </p>
                  </td>
                </tr>
              ) : (
                filtered.map((item: PayrollOpeningBalance) => (
                  <tr
                    key={item.id}
                    style={{
                      borderBottom: "1px solid var(--color-border-subtle)",
                      transition: "background-color 0.1s ease"
                    }}
                  >
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <div style={{ fontWeight: 600, color: "var(--color-content-primary)" }}>
                        {item.employmentId}
                      </div>
                    </td>
                    <td style={{ padding: "0.75rem 1rem", fontWeight: 600 }}>
                      {item.balanceType}
                    </td>
                    <td style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", color: "var(--color-content-secondary)" }}>
                      {item.asOfDate}
                    </td>
                    <td style={{ padding: "0.75rem 1rem", fontWeight: 600 }}>
                      <bdi>{item.amount.toLocaleString()} {item.currencyCode}</bdi>
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <PayrollStatusBadge status={item.status} />
                    </td>
                    <td style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                      {item.sourceReference || "—"}
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      {item.status === "Draft" ? (
                        <Button
                          variant="primary"
                          size="compact"
                          onPress={() => handleApprove(item.id)}
                          isDisabled={approveMutation.isPending}
                        >
                          {t("openingBalances.approveAction")}
                        </Button>
                      ) : (
                        <span style={{ fontSize: "0.75rem", color: "var(--color-status-success)", fontWeight: 600 }}>
                          {t("openingBalances.approved")}
                        </span>
                      )}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Modal */}
      <NewOpeningBalanceModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        legalEntityId={legalEntityId}
      />
    </div>
  );
}
