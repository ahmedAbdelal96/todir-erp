import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "../../../../components/primitives/button.tsx";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Switch } from "../../../../components/primitives/switch.tsx";
import { BidiText } from "../../../../components/primitives/bidi-text.tsx";
import {
  useCreateRegisterMutation,
  useActivateRegisterMutation,
  useDeactivateRegisterMutation,
} from "../../hooks/use-pos-queries.ts";
import type { RegisterResponse } from "../../types.ts";

export interface PosRegistersTabProps {
  readonly legalEntityId: string;
  readonly registers: readonly RegisterResponse[];
  readonly onRefetch: () => void;
}

export function PosRegistersTab({
  legalEntityId,
  registers,
  onRefetch,
}: PosRegistersTabProps): React.JSX.Element {
  const { t } = useTranslation("pos");
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [currencyCode, setCurrencyCode] = useState("EGP");
  const [offlineEnabled, setOfflineEnabled] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const createMutation = useCreateRegisterMutation(legalEntityId);
  const activateMutation = useActivateRegisterMutation();
  const deactivateMutation = useDeactivateRegisterMutation();

  const handleCreateRegister = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!code.trim() || !name.trim()) {
      setError(t("registers.fieldsRequired"));
      return;
    }
    setError(null);
    try {
      await createMutation.mutateAsync({
        legalEntityId,
        branchId: "00000000-0000-0000-0000-000000000000",
        code: code.trim(),
        name: name.trim(),
        currencyCode: currencyCode.trim() || "EGP",
        offlineEnabled,
      });
      setIsCreateModalOpen(false);
      setCode("");
      setName("");
      onRefetch();
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : t("registers.createFailed"));
    }
  };

  const handleToggleStatus = async (register: RegisterResponse) => {
    try {
      if (register.status === "Active") {
        await deactivateMutation.mutateAsync(register.id);
      } else {
        await activateMutation.mutateAsync(register.id);
      }
      onRefetch();
    } catch {
      // error handled in mutation
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      {/* Header Bar */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
        <div>
          <h2 style={{ fontSize: "1.25rem", fontWeight: 700, margin: 0, color: "var(--color-content-default)" }}>
            {t("registers.tabTitle")}
          </h2>
          <p style={{ margin: "0.25rem 0 0", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
            {t("registers.tabSubtitle")}
          </p>
        </div>

        <Button variant="primary" onClick={() => setIsCreateModalOpen(true)}>
          + {t("registers.newRegisterAction")}
        </Button>
      </div>

      {/* Registers Table */}
      <div
        style={{
          backgroundColor: "#ffffff",
          borderRadius: "var(--radius-lg)",
          border: "1px solid #cbd5e1",
          overflowX: "auto",
          boxShadow: "0 1px 3px 0 rgba(0, 0, 0, 0.05)",
        }}
      >
        <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem", textAlign: "start" }}>
          <thead>
            <tr
              style={{
                backgroundColor: "var(--color-surface-hover)",
                borderBottom: "1px solid #cbd5e1",
                color: "var(--color-content-muted)",
                fontWeight: 600,
              }}
            >
              <th style={{ padding: "0.75rem 1rem" }}>{t("registers.colCode")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("registers.colName")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("registers.colCurrency")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("registers.colOffline")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("registers.colStatus")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("common.actions")}</th>
            </tr>
          </thead>
          <tbody>
            {registers.length > 0 ? (
              registers.map((r, idx) => (
                <tr key={r.id} style={{ borderBottom: idx === registers.length - 1 ? "none" : "1px solid #e2e8f0" }}>
                  <td style={{ padding: "0.75rem 1rem", fontWeight: 700 }}>
                    <BidiText>{r.code}</BidiText>
                  </td>
                  <td style={{ padding: "0.75rem 1rem", fontWeight: 600 }}>
                    <BidiText>{r.name}</BidiText>
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <BidiText>{r.currencyCode}</BidiText>
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <span
                      style={{
                        padding: "0.2rem 0.5rem",
                        borderRadius: "var(--radius-full)",
                        fontSize: "0.75rem",
                        fontWeight: 600,
                        backgroundColor: r.offlineEnabled ? "rgba(16, 185, 129, 0.1)" : "rgba(100, 116, 139, 0.1)",
                        color: r.offlineEnabled ? "var(--color-status-success)" : "var(--color-content-muted)",
                      }}
                    >
                      {r.offlineEnabled ? t("registers.enabled") : t("registers.disabled")}
                    </span>
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <span
                      style={{
                        padding: "0.2rem 0.5rem",
                        borderRadius: "var(--radius-full)",
                        fontSize: "0.75rem",
                        fontWeight: 600,
                        backgroundColor: r.status === "Active" ? "rgba(16, 185, 129, 0.1)" : "rgba(239, 68, 68, 0.1)",
                        color: r.status === "Active" ? "var(--color-status-success)" : "var(--color-status-danger)",
                      }}
                    >
                      {r.status}
                    </span>
                  </td>
                  <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                    <Button
                      size="compact"
                      variant={r.status === "Active" ? "secondary" : "primary"}
                      onClick={() => handleToggleStatus(r)}
                    >
                      {r.status === "Active" ? t("registers.deactivate") : t("registers.activate")}
                    </Button>
                  </td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan={6} style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted)" }}>
                  {t("registers.empty")}
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {/* Create Register Modal */}
      <Dialog
        isOpen={isCreateModalOpen}
        onOpenChange={(open) => !open && setIsCreateModalOpen(false)}
        title={t("registers.createModalTitle")}
        description={t("registers.createModalSubtitle")}
        size="small"
      >
        <form onSubmit={handleCreateRegister} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
          <TextField
            label={t("registers.codeLabel")}
            placeholder={t("registers.codePlaceholder")}
            value={code}
            onChange={(v) => {
              setCode(v);
              setError(null);
            }}
            autoFocus
          />

          <TextField
            label={t("registers.nameLabel")}
            placeholder={t("registers.namePlaceholder")}
            value={name}
            onChange={(v) => {
              setName(v);
              setError(null);
            }}
          />

          <TextField
            label={t("registers.currencyLabel")}
            value={currencyCode}
            onChange={(v) => setCurrencyCode(v)}
          />

          <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between" }}>
            <span style={{ fontSize: "0.875rem", fontWeight: 600 }}>{t("registers.offlineEnabledLabel")}</span>
            <Switch
              isSelected={offlineEnabled}
              onChange={(checked) => setOfflineEnabled(checked)}
            />
          </div>

          {error && (
            <div style={{ color: "var(--color-status-danger)", fontSize: "0.875rem" }} role="alert">
              {error}
            </div>
          )}

          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "0.5rem" }}>
            <Button variant="secondary" type="button" onClick={() => setIsCreateModalOpen(false)}>
              {t("common.cancel")}
            </Button>
            <Button variant="primary" type="submit" isDisabled={createMutation.isPending}>
              {createMutation.isPending ? t("common.submitting") : t("registers.createAction")}
            </Button>
          </div>
        </form>
      </Dialog>
    </div>
  );
}
