import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import {
  usePayrollInputs,
  useApprovePayrollInputMutation,
  useCancelPayrollInputMutation
} from "../../hooks/use-payroll-queries.ts";
import { PayrollStatusBadge } from "../../components/payroll-status-badge.tsx";
import { NewPayrollInputModal } from "./new-payroll-input-modal.tsx";
import { PayrollImportDialog } from "./payroll-import-dialog.tsx";
import { mapPayrollError } from "../../services/payroll-error-map.ts";
import type { PayrollInput } from "../../types.ts";
import { ApiError } from "../../../../core/api/http-client.ts";

export interface PayrollInputsTabProps {
  readonly legalEntityId: string;
}

export function PayrollInputsTab({
  legalEntityId
}: PayrollInputsTabProps): React.JSX.Element {
  const { t } = useTranslation("payroll");
  const [searchTerm, setSearchTerm] = useState("");
  const [statusFilter, setStatusFilter] = useState<string>("all");
  const [isNewModalOpen, setIsNewModalOpen] = useState(false);
  const [isImportModalOpen, setIsImportModalOpen] = useState(false);
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const [actionError, setActionError] = useState<string | null>(null);

  const inputsQuery = usePayrollInputs(legalEntityId, {
    ...(searchTerm ? { search: searchTerm } : {}),
    ...(statusFilter !== "all" ? { status: statusFilter } : {})
  });
  const { data: inputs = [], isLoading } = inputsQuery;
  const approveMutation = useApprovePayrollInputMutation(legalEntityId);
  const cancelMutation = useCancelPayrollInputMutation(legalEntityId);

  const filtered = inputs.filter((item: PayrollInput) => {
    if (statusFilter !== "all" && item.status !== statusFilter) return false;
    if (!searchTerm.trim()) return true;
    const term = searchTerm.toLowerCase();
    return (
      (item.employeeName && item.employeeName.toLowerCase().includes(term)) ||
      (item.employmentNumber && item.employmentNumber.toLowerCase().includes(term)) ||
      (item.componentName && item.componentName.toLowerCase().includes(term)) ||
      (item.componentCode && item.componentCode.toLowerCase().includes(term)) ||
      item.employmentId.toLowerCase().includes(term) ||
      item.componentId.toLowerCase().includes(term) ||
      (item.notes && item.notes.toLowerCase().includes(term))
    );
  });

  const handleSelectAll = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.checked) {
      const draftIds = filtered.filter((i: PayrollInput) => i.status === "Draft").map((i: PayrollInput) => i.id);
      setSelectedIds(draftIds);
    } else {
      setSelectedIds([]);
    }
  };

  const handleToggleSelect = (id: string) => {
    setSelectedIds((prev) =>
      prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]
    );
  };

  const handleApproveSelected = async () => {
    if (selectedIds.length === 0) return;
    setActionError(null);
    try {
      for (const inputId of selectedIds) {
        await approveMutation.mutateAsync(inputId);
      }
      setSelectedIds([]);
    } catch (err: unknown) {
      const mapped = mapPayrollError(err);
      setActionError(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const statusOptions = [
    { id: "all", label: t("common.allStatuses") },
    { id: "Draft", label: t("statuses.draft") },
    { id: "Approved", label: t("statuses.approved") },
    { id: "Consumed", label: t("statuses.consumed") }
  ];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Header and Actions Bar */}
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
            {t("inputs.tabTitle")}
          </h2>
          <p style={{ margin: "0.25rem 0 0 0", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
            {t("inputs.tabSubtitle")}
          </p>
        </div>

        <div style={{ display: "flex", alignItems: "center", gap: "0.75rem", flexWrap: "wrap" }}>
          {selectedIds.length > 0 && (
            <Button
              variant="primary"
              onPress={handleApproveSelected}
              isDisabled={approveMutation.isPending}
              style={{ background: "var(--color-status-success)", borderColor: "var(--color-status-success)" }}
            >
              {approveMutation.isPending
                ? t("actions.approving")
                : t("inputs.approveSelected", { count: selectedIds.length })}
            </Button>
          )}

          <Button
            variant="secondary"
            onPress={() => setIsImportModalOpen(true)}
            style={{ display: "inline-flex", alignItems: "center", gap: "0.5rem" }}
          >
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
              <polyline points="7 10 12 15 17 10" />
              <line x1="12" y1="15" x2="12" y2="3" />
            </svg>
            {t("inputs.importAction")}
          </Button>

          <Button
            variant="primary"
            onPress={() => setIsNewModalOpen(true)}
            style={{ display: "inline-flex", alignItems: "center", gap: "0.5rem" }}
          >
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <line x1="12" y1="5" x2="12" y2="19" />
              <line x1="5" y1="12" x2="19" y2="12" />
            </svg>
            {t("inputs.newInputAction")}
          </Button>
        </div>
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
            placeholder={t("inputs.searchPlaceholder")}
          />
        </div>

        <div style={{ width: "180px" }}>
          <Select
            label={t("common.status")}
            selectedKey={statusFilter}
            onSelectionChange={(val) => {
              if (val) setStatusFilter(String(val));
            }}
            options={statusOptions}
          />
        </div>
      </div>

      {/* Inputs List Table */}
      <div
        style={{
          background: "var(--color-surface-card)",
          borderRadius: "var(--radius-lg)",
          border: "1px solid var(--color-border-default)",
          boxShadow: "var(--shadow-sm)",
          overflow: "hidden"
        }}
      >
        {inputsQuery.isError ? (
          <div role="alert" style={{ padding: "3rem 2rem", textAlign: "center", color: "var(--color-status-danger)" }}>
            <p style={{ margin: 0, fontWeight: 700, color: "var(--color-content-primary)" }}>
              {t("common.dataLoadError")}
            </p>
            <Button
              variant="secondary"
              size="compact"
              onPress={() => void inputsQuery.refetch()}
              style={{ marginTop: "0.75rem" }}
            >
              {t("common.retry", "إعادة المحاولة")}
            </Button>
          </div>
        ) : (
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
                <th style={{ padding: "0.75rem 1rem", width: "40px" }}>
                  <input
                    type="checkbox"
                    onChange={handleSelectAll}
                    checked={
                      filtered.length > 0 &&
                      filtered.filter((i: PayrollInput) => i.status === "Draft").length > 0 &&
                      selectedIds.length === filtered.filter((i: PayrollInput) => i.status === "Draft").length
                    }
                    title={t("inputs.selectAllDrafts")}
                  />
                </th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("inputs.employee")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("inputs.component")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("inputs.amount")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("inputs.date")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("inputs.status")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("inputs.notes")}</th>
                <th style={{ padding: "0.75rem 1rem" }}>{t("runs.actionsTitle", "الإجراءات")}</th>
              </tr>
            </thead>
            <tbody>
              {isLoading ? (
                <tr>
                  <td colSpan={8} style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted)" }}>
                    {t("common.loading")}
                  </td>
                </tr>
              ) : filtered.length === 0 ? (
                <tr>
                  <td colSpan={8} style={{ padding: "3rem 2rem", textAlign: "center" }}>
                    <p style={{ margin: 0, fontWeight: 600, color: "var(--color-content-primary)" }}>
                      {t("inputs.emptyTitle")}
                    </p>
                    <p style={{ margin: "0.25rem 0 0 0", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                      {t("inputs.emptyDesc")}
                    </p>
                  </td>
                </tr>
              ) : (
                filtered.map((item: PayrollInput) => (
                  <tr
                    key={item.id}
                    style={{
                      borderBottom: "1px solid var(--color-border-subtle)",
                      transition: "background-color 0.1s ease"
                    }}
                  >
                    <td style={{ padding: "0.75rem 1rem" }}>
                      {item.status === "Draft" ? (
                        <input
                          type="checkbox"
                          checked={selectedIds.includes(item.id)}
                          onChange={() => handleToggleSelect(item.id)}
                        />
                      ) : (
                        <span style={{ color: "var(--color-content-muted)" }}>—</span>
                      )}
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <div style={{ fontWeight: 600, color: "var(--color-content-primary)" }}>
                        {item.employeeName || item.employmentId}
                      </div>
                      {item.employmentNumber && (
                        <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", fontFamily: "var(--font-mono)" }}>
                          {item.employmentNumber}
                        </div>
                      )}
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <div style={{ fontWeight: 600 }}>{item.componentName || item.componentCode || item.componentId}</div>
                      {item.componentCode && item.componentName && (
                        <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", fontFamily: "var(--font-mono)" }}>
                          {item.componentCode}
                        </div>
                      )}
                    </td>
                    <td style={{ padding: "0.75rem 1rem", fontWeight: 600 }}>
                      <bdi>{item.amount.toLocaleString()} {item.currencyCode}</bdi>
                    </td>
                    <td style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", color: "var(--color-content-secondary)" }}>
                      {item.inputDate}
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      <PayrollStatusBadge status={item.status} />
                    </td>
                    <td style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                      {item.notes || "—"}
                    </td>
                    <td style={{ padding: "0.75rem 1rem" }}>
                      {item.status === "Draft" ? (
                        <div style={{ display: "flex", gap: "0.375rem" }}>
                          <Button
                            variant="secondary"
                            size="compact"
                            onPress={async () => {
                              setActionError(null);
                              try {
                                await approveMutation.mutateAsync(item.id);
                              } catch (err: unknown) {
                                const mapped = mapPayrollError(err);
                                setActionError(t(mapped.messageKey, mapped.defaultMessage));
                              }
                            }}
                            isDisabled={approveMutation.isPending || cancelMutation.isPending}
                          >
                            {t("actions.approve", "اعتماد")}
                          </Button>
                          <Button
                            variant="secondary"
                            size="compact"
                            onPress={async () => {
                              setActionError(null);
                              try {
                                await cancelMutation.mutateAsync(item.id);
                              } catch (err: unknown) {
                                const mapped = mapPayrollError(err);
                                setActionError(t(mapped.messageKey, mapped.defaultMessage));
                              }
                            }}
                            isDisabled={approveMutation.isPending || cancelMutation.isPending}
                            style={{ color: "var(--color-status-danger)" }}
                          >
                            {t("actions.cancel", "إلغاء")}
                          </Button>
                        </div>
                      ) : (
                        <span style={{ color: "var(--color-content-muted)" }}>—</span>
                      )}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
        )}
      </div>

      {/* Creation Modal */}
      <NewPayrollInputModal
        isOpen={isNewModalOpen}
        onClose={() => setIsNewModalOpen(false)}
        legalEntityId={legalEntityId}
      />

      {/* Import Dialog */}
      <PayrollImportDialog
        isOpen={isImportModalOpen}
        onClose={() => setIsImportModalOpen(false)}
        legalEntityId={legalEntityId}
      />
    </div>
  );
}
