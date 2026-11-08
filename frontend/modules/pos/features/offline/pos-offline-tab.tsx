import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "../../../../components/primitives/button.tsx";
import { BidiText } from "../../../../components/primitives/bidi-text.tsx";
import { OfflineIcon, SyncIcon } from "../../components/pos-icons.tsx";
import { usePosOffline, type StoredOfflineEnvelope } from "../../hooks/use-pos-offline.ts";
import { useRegisterTerminalsQuery } from "../../hooks/use-pos-queries.ts";

export interface PosOfflineTabProps {
  readonly registerId: string;
}

export function PosOfflineTab({ registerId }: PosOfflineTabProps): React.JSX.Element {
  const { t } = useTranslation("pos");
  const { data: terminalsData } = useRegisterTerminalsQuery(registerId);
  const activeTerminal = terminalsData?.find((t) => t.status === "Ready") || terminalsData?.[0];
  const dynamicDeviceIdentity = activeTerminal?.deviceIdentity;

  const {
    isOnline,
    pendingSyncCount,
    offlineQueue,
    offlinePackage,
    isOfflinePackageValid,
    deviceIdentity,
    refreshOfflinePackage,
    syncOfflineQueue,
  } = usePosOffline(registerId, dynamicDeviceIdentity);

  const [isSyncing, setIsSyncing] = useState(false);
  const [isDownloading, setIsDownloading] = useState(false);
  const [statusMessage, setStatusMessage] = useState<string | null>(null);

  const handleSyncNow = async () => {
    setIsSyncing(true);
    setStatusMessage(null);
    try {
      const syncedCount = await syncOfflineQueue();
      setStatusMessage(`${t("offline.syncedCount")}: ${syncedCount}`);
    } catch (err: unknown) {
      setStatusMessage(err instanceof Error ? err.message : t("offline.syncFailed"));
    } finally {
      setIsSyncing(false);
    }
  };

  const handleDownload = async () => {
    setIsDownloading(true);
    try {
      const pkg = await refreshOfflinePackage();
      if (pkg) {
        setStatusMessage(
          `${t("offline.packageDownloaded")}: v${pkg.version} (${t("offline.validUntil", "صالحة حتى")}: ${new Date(pkg.expiresAt).toLocaleTimeString()})`
        );
      } else {
        setStatusMessage(t("offline.downloadFailed", "فشل تحميل حزمة العمل بدون اتصال"));
      }
    } catch (err: unknown) {
      setStatusMessage(err instanceof Error ? err.message : "Download failed");
    } finally {
      setIsDownloading(false);
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      {/* Header Bar */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
        <div>
          <h2 style={{ fontSize: "1.25rem", fontWeight: 700, margin: 0, color: "var(--color-content-default)" }}>
            {t("offline.tabTitle")}
          </h2>
          <p style={{ margin: "0.25rem 0 0", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
            {t("offline.tabSubtitle")}
          </p>
        </div>

        <div style={{ display: "flex", gap: "0.5rem" }}>
          <Button
            variant="secondary"
            onClick={handleDownload}
            isDisabled={isDownloading || !isOnline}
          >
            {isDownloading ? t("common.downloading") : t("offline.downloadPackage")}
          </Button>

          <Button
            variant="primary"
            onClick={handleSyncNow}
            isDisabled={isSyncing || pendingSyncCount === 0 || !isOnline}
            style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}
          >
            <SyncIcon size={18} />
            <span>{isSyncing ? t("offline.syncing") : t("offline.syncNow")}</span>
          </Button>
        </div>
      </div>

      {statusMessage && (
        <div
          role="status"
          style={{
            padding: "0.75rem 1rem",
            borderRadius: "var(--radius-md)",
            backgroundColor: "rgba(16, 185, 129, 0.1)",
            border: "1px solid rgba(16, 185, 129, 0.3)",
            color: "var(--color-status-success)",
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
          }}
        >
          <span>{statusMessage}</span>
          <button
            type="button"
            onClick={() => setStatusMessage(null)}
            style={{ background: "none", border: "none", cursor: "pointer", fontSize: "1rem" }}
          >
            ×
          </button>
        </div>
      )}

      {/* Network, Device & Package Health Cards */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(16rem, 1fr))", gap: "1rem" }}>
        {/* Network Status Card */}
        <div
          style={{
            backgroundColor: "#ffffff",
            padding: "1.25rem",
            borderRadius: "var(--radius-lg)",
            border: "1px solid #cbd5e1",
            boxShadow: "0 1px 3px 0 rgba(0, 0, 0, 0.05)",
            display: "flex",
            alignItems: "center",
            gap: "1rem",
          }}
        >
          <div
            style={{
              padding: "0.75rem",
              borderRadius: "var(--radius-md)",
              backgroundColor: isOnline ? "rgba(16, 185, 129, 0.1)" : "rgba(239, 68, 68, 0.1)",
              color: isOnline ? "var(--color-status-success)" : "var(--color-status-danger)",
            }}
          >
            {isOnline ? <SyncIcon size={24} /> : <OfflineIcon size={24} />}
          </div>
          <div>
            <div style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
              {t("offline.networkStatus")}
            </div>
            <div style={{ fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-default)" }}>
              {isOnline ? t("workspace.online") : t("workspace.offline")}
            </div>
          </div>
        </div>

        {/* Offline Package Validity Card */}
        <div
          style={{
            backgroundColor: "#ffffff",
            padding: "1.25rem",
            borderRadius: "var(--radius-lg)",
            border: "1px solid #cbd5e1",
            boxShadow: "0 1px 3px 0 rgba(0, 0, 0, 0.05)",
            display: "flex",
            alignItems: "center",
            gap: "1rem",
          }}
        >
          <div
            style={{
              padding: "0.75rem",
              borderRadius: "var(--radius-md)",
              backgroundColor: isOfflinePackageValid ? "rgba(16, 185, 129, 0.1)" : "rgba(245, 158, 11, 0.1)",
              color: isOfflinePackageValid ? "var(--color-status-success)" : "var(--color-status-warning)",
            }}
          >
            <OfflineIcon size={24} />
          </div>
          <div>
            <div style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
              {t("offline.packageStatus", "حزمة البيانات غير المتصلة")}
            </div>
            <div style={{ fontSize: "1rem", fontWeight: 700, color: "var(--color-content-default)" }}>
              {isOfflinePackageValid
                ? t("offline.packageActiveVersion", {
                    version: offlinePackage?.version ?? 1,
                    defaultValue: "Version v{{version}} (Active & Authorized)",
                  })
                : t("offline.packageMissingOrExpired", "غير متوفرة / منتهية")}
            </div>
          </div>
        </div>

        {/* Pending Queue Card */}
        <div
          style={{
            backgroundColor: "#ffffff",
            padding: "1.25rem",
            borderRadius: "var(--radius-lg)",
            border: "1px solid #cbd5e1",
            boxShadow: "0 1px 3px 0 rgba(0, 0, 0, 0.05)",
            display: "flex",
            alignItems: "center",
            gap: "1rem",
          }}
        >
          <div
            style={{
              padding: "0.75rem",
              borderRadius: "var(--radius-md)",
              backgroundColor: pendingSyncCount > 0 ? "rgba(245, 158, 11, 0.1)" : "rgba(16, 185, 129, 0.1)",
              color: pendingSyncCount > 0 ? "var(--color-status-warning)" : "var(--color-status-success)",
            }}
          >
            <SyncIcon size={24} />
          </div>
          <div>
            <div style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
              {t("offline.pendingQueue")}
            </div>
            <div style={{ fontSize: "1.125rem", fontWeight: 700, color: "var(--color-content-default)" }}>
              <BidiText>{pendingSyncCount}</BidiText> {t("offline.envelopes")}
            </div>
          </div>
        </div>
      </div>

      {/* Offline Envelopes Table (Durable IndexedDB storage) */}
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
              <th style={{ padding: "0.75rem 1rem" }}>{t("offline.colEnvelopeId")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("offline.colSequence")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("offline.colTimestamp")}</th>
              <th style={{ padding: "0.75rem 1rem" }}>{t("offline.colStatus")}</th>
              <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("offline.colRetries")}</th>
            </tr>
          </thead>
          <tbody>
            {offlineQueue.length > 0 ? (
              offlineQueue.map((env: StoredOfflineEnvelope, idx: number) => (
                <tr key={env.id} style={{ borderBottom: idx === offlineQueue.length - 1 ? "none" : "1px solid #e2e8f0" }}>
                  <td style={{ padding: "0.75rem 1rem", fontWeight: 700 }}>
                    <BidiText>{env.id.slice(0, 8)}</BidiText>
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <BidiText>{env.sourceSequence}</BidiText>
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <BidiText>{new Date(env.createdAt).toLocaleTimeString()}</BidiText>
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <span
                      style={{
                        padding: "0.2rem 0.5rem",
                        borderRadius: "var(--radius-full)",
                        fontSize: "0.75rem",
                        fontWeight: 600,
                        backgroundColor:
                          env.status === "Synced"
                            ? "rgba(16, 185, 129, 0.1)"
                            : env.status === "Failed" || env.status === "Conflicted"
                            ? "rgba(239, 68, 68, 0.1)"
                            : "rgba(245, 158, 11, 0.1)",
                        color:
                          env.status === "Synced"
                            ? "var(--color-status-success)"
                            : env.status === "Failed" || env.status === "Conflicted"
                            ? "var(--color-status-danger)"
                            : "var(--color-status-warning)",
                      }}
                    >
                      {env.status}
                    </span>
                  </td>
                  <td style={{ padding: "0.75rem 1rem", textAlign: "end" }}>
                    <BidiText>{env.retryCount}</BidiText>
                  </td>
                </tr>
              ))
            ) : (
              <tr>
                <td colSpan={5} style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted)" }}>
                  {t("offline.emptyQueue")}
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
