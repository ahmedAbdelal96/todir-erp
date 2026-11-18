import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import {
  useAccountConnections,
  useCreateAccountChangeRequest
} from "../../hooks/use-banking-queries.ts";
import { bankingApi } from "../../services/banking-api.ts";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { StatusBadge } from "../../../../components/fields/status-badge.tsx";
import { BdiText } from "../../../../components/formatting/bidi-text.tsx";
import { formatDate } from "../../../../components/formatting/date-formatter.ts";
import type { TreasuryAccountDto } from "../../types.ts";

interface Props {
  readonly legalEntityId: string;
  readonly account: TreasuryAccountDto | null;
  readonly isOpen: boolean;
  readonly onClose: () => void;
}

export function TreasuryAccountDetailDrawer({ legalEntityId, account, isOpen, onClose }: Props): React.JSX.Element | null {
  const { t } = useTranslation("banking");
  const [activeSubTab, setActiveSubTab] = useState<"info" | "connections" | "changes">("info");

  // Connection creation state
  const [isAddingConnection, setIsAddingConnection] = useState(false);
  const [providerCode, setProviderCode] = useState("");
  const [connectionRef, setConnectionRef] = useState("");

  // Change request state
  const [isRequestingChange, setIsRequestingChange] = useState(false);
  const [changeName, setChangeName] = useState("");
  const [changePurpose, setChangePurpose] = useState("");
  const [changeLiquidityClass, setChangeLiquidityClass] = useState("");
  const [changeReason, setChangeReason] = useState("");

  const { data: connections, refetch: refetchConnections } = useAccountConnections(legalEntityId, account?.id ?? "");
  const createChangeRequestMutation = useCreateAccountChangeRequest(legalEntityId, account?.id ?? "");

  if (!isOpen || !account) return null;

  const handleAddConnectionSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!providerCode.trim() || !connectionRef.trim()) return;
    try {
      await bankingApi.createConnection(legalEntityId, account.id, {
        providerCode: providerCode.trim(),
        connectionRef: connectionRef.trim()
      });
      setIsAddingConnection(false);
      setProviderCode("");
      setConnectionRef("");
      void refetchConnections();
    } catch {
      // ignore
    }
  };

  const handleRefreshConnection = async (connId: string, version: number) => {
    try {
      await bankingApi.refreshConnection(legalEntityId, connId, version);
      void refetchConnections();
    } catch {
      // ignore
    }
  };

  const handleDisconnectConnection = async (connId: string, version: number) => {
    try {
      await bankingApi.disconnectConnection(legalEntityId, connId, version);
      void refetchConnections();
    } catch {
      // ignore
    }
  };

  const handleChangeRequestSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!changeReason.trim()) return;
    try {
      await createChangeRequestMutation.mutateAsync({
        reason: changeReason.trim(),
        version: account.version,
        payload: {
          name: changeName.trim() || account.name,
          purpose: changePurpose.trim() || account.purpose,
          liquidityClass: changeLiquidityClass.trim() || account.liquidityClass
        }
      });
      setIsRequestingChange(false);
      setChangeReason("");
    } catch {
      // ignore
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("accounts.drawerTitle")}>
      <div style={{ minWidth: "340px", maxWidth: "560px", display: "flex", flexDirection: "column", gap: "1rem" }}>
        {/* Header Summary */}
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", borderBottom: "1px solid var(--color-border-subtle)", paddingBottom: "1rem" }}>
          <div>
            <div style={{ fontSize: "1.125rem", fontWeight: 700, color: "var(--color-brand-primary)" }}>{account.name}</div>
            <div style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
              <BdiText>{account.code}</BdiText> &bull; {t(`accountTypes.${account.type}` as const)}
            </div>
          </div>
          <StatusBadge
            intent={account.status === "Active" ? "success" : account.status === "Draft" ? "neutral" : "warning"}
            label={t(`accountStatuses.${account.status}` as const)}
          />
        </div>

        {/* Sub-tab navigation */}
        <div style={{ display: "flex", gap: "0.5rem", borderBottom: "1px solid var(--color-border-subtle)", paddingBottom: "0.5rem" }}>
          <Button
            variant={activeSubTab === "info" ? "primary" : "ghost"}
            size="compact"
            onClick={() => setActiveSubTab("info")}
          >
            {t("accounts.inspect")}
          </Button>
          <Button
            variant={activeSubTab === "connections" ? "primary" : "ghost"}
            size="compact"
            onClick={() => setActiveSubTab("connections")}
          >
            {t("accounts.connectionsTab")}
          </Button>
          <Button
            variant={activeSubTab === "changes" ? "primary" : "ghost"}
            size="compact"
            onClick={() => setActiveSubTab("changes")}
          >
            {t("accounts.changeRequestsTab")}
          </Button>
        </div>

        {/* Content based on sub-tab */}
        {activeSubTab === "info" && (
          <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem", fontSize: "0.875rem" }}>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
              <div>
                <span style={{ color: "var(--color-content-muted)", display: "block" }}>{t("accounts.currency")}:</span>
                <span style={{ fontWeight: 600 }}>{account.currencyCode || t("currencyPolicies.MultiCurrency")}</span>
              </div>
              <div>
                <span style={{ color: "var(--color-content-muted)", display: "block" }}>{t("accounts.policy")}:</span>
                <span style={{ fontWeight: 600 }}>{t(`currencyPolicies.${account.currencyPolicy}` as const)}</span>
              </div>
              <div>
                <span style={{ color: "var(--color-content-muted)", display: "block" }}>{t("accounts.purpose")}:</span>
                <span style={{ fontWeight: 600 }}>{account.purpose}</span>
              </div>
              <div>
                <span style={{ color: "var(--color-content-muted)", display: "block" }}>{t("accounts.liquidityClass")}:</span>
                <span style={{ fontWeight: 600 }}>{account.liquidityClass}</span>
              </div>
              <div>
                <span style={{ color: "var(--color-content-muted)", display: "block" }}>{t("accounts.maskedIdentifier")}:</span>
                <span style={{ fontWeight: 600 }}><BdiText>{account.maskedIdentifier || "—"}</BdiText></span>
              </div>
              <div>
                <span style={{ color: "var(--color-content-muted)", display: "block" }}>{t("common.version")}:</span>
                <span style={{ fontWeight: 600 }}>{account.version}</span>
              </div>
            </div>

            <div style={{ borderTop: "1px solid var(--color-border-subtle)", paddingTop: "0.75rem", fontSize: "0.75rem", color: "var(--color-content-muted)" }}>
              <span>{t("common.createdAt")}: <BdiText>{formatDate(account.createdAt)}</BdiText></span>
            </div>
          </div>
        )}

        {activeSubTab === "connections" && (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <span style={{ fontSize: "0.875rem", fontWeight: 600 }}>{t("accounts.connectionsTab")}</span>
              <Button size="compact" variant="secondary" onClick={() => setIsAddingConnection(!isAddingConnection)}>
                {t("accounts.addConnection")}
              </Button>
            </div>

            {isAddingConnection && (
              <form onSubmit={handleAddConnectionSubmit} style={{ padding: "0.875rem", background: "var(--color-surface-page)", borderRadius: "var(--radius-sm)", display: "flex", flexDirection: "column", gap: "0.75rem" }}>
                <TextField label={t("accounts.providerCode")} value={providerCode} onChange={(val) => setProviderCode(val)} isRequired />
                <TextField label={t("accounts.connectionRef")} value={connectionRef} onChange={(val) => setConnectionRef(val)} isRequired />
                <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
                  <Button type="button" size="compact" variant="ghost" onClick={() => setIsAddingConnection(false)}>{t("accounts.cancel")}</Button>
                  <Button type="submit" size="compact" variant="primary">{t("common.confirm")}</Button>
                </div>
              </form>
            )}

            <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
              {(connections ?? []).length === 0 ? (
                <div style={{ color: "var(--color-content-muted)", textAlign: "center", padding: "1rem", fontSize: "0.875rem" }}>
                  {t("common.noData")}
                </div>
              ) : (
                connections?.map((conn) => (
                  <div key={conn.id} style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: "0.75rem", border: "1px solid var(--color-border-subtle)", borderRadius: "var(--radius-sm)" }}>
                    <div>
                      <div style={{ fontWeight: 600, fontSize: "0.875rem" }}>{conn.providerCode}</div>
                      <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}><BdiText>{conn.connectionRef}</BdiText></div>
                    </div>
                    <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
                      <StatusBadge
                        intent={conn.status === "Connected" ? "success" : "warning"}
                        label={conn.status}
                      />
                      <Button size="compact" variant="ghost" onClick={() => handleRefreshConnection(conn.id, conn.version)}>
                        {t("accounts.refreshConnection")}
                      </Button>
                      <Button size="compact" variant="ghost" onClick={() => handleDisconnectConnection(conn.id, conn.version)}>
                        {t("accounts.disconnectConnection")}
                      </Button>
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>
        )}

        {activeSubTab === "changes" && (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <span style={{ fontSize: "0.875rem", fontWeight: 600 }}>{t("accounts.changeRequestsTab")}</span>
              <Button size="compact" variant="secondary" onClick={() => setIsRequestingChange(!isRequestingChange)}>
                {t("accounts.changeRequestTitle")}
              </Button>
            </div>

            {isRequestingChange && (
              <form onSubmit={handleChangeRequestSubmit} style={{ padding: "0.875rem", background: "var(--color-surface-page)", borderRadius: "var(--radius-sm)", display: "flex", flexDirection: "column", gap: "0.75rem" }}>
                <TextField label={t("accounts.nameLabel")} value={changeName} onChange={(val) => setChangeName(val)} placeholder={account.name} />
                <TextField label={t("accounts.purposeLabel")} value={changePurpose} onChange={(val) => setChangePurpose(val)} placeholder={account.purpose} />
                <TextField label={t("accounts.liquidityClassLabel")} value={changeLiquidityClass} onChange={(val) => setChangeLiquidityClass(val)} placeholder={account.liquidityClass} />
                <TextField label={t("accounts.changeReasonLabel")} value={changeReason} onChange={(val) => setChangeReason(val)} isRequired />
                <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem" }}>
                  <Button type="button" size="compact" variant="ghost" onClick={() => setIsRequestingChange(false)}>{t("accounts.cancel")}</Button>
                  <Button type="submit" size="compact" variant="primary">{t("accounts.changeSubmit")}</Button>
                </div>
              </form>
            )}
          </div>
        )}

        <div style={{ display: "flex", justifyContent: "flex-end", borderTop: "1px solid var(--color-border-subtle)", paddingTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose}>{t("common.close")}</Button>
        </div>
      </div>
    </Dialog>
  );
}
