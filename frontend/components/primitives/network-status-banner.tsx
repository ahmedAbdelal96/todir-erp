import React, { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { WifiIcon, WifiOffIcon } from "./icons.tsx";

export function NetworkStatusBanner(): React.JSX.Element | null {
  const { t } = useTranslation("common");
  const [isOnline, setIsOnline] = useState<boolean>(() => (typeof navigator !== "undefined" ? navigator.onLine : true));
  const [wasOffline, setWasOffline] = useState(false);
  const [showReconnected, setShowReconnected] = useState(false);

  useEffect(() => {
    function handleOnline() {
      setIsOnline(true);
      if (wasOffline) {
        setShowReconnected(true);
        const timer = setTimeout(() => {
          setShowReconnected(false);
          setWasOffline(false);
        }, 3500);
        return () => clearTimeout(timer);
      }
    }

    function handleOffline() {
      setIsOnline(false);
      setWasOffline(true);
      setShowReconnected(false);
    }

    window.addEventListener("online", handleOnline);
    window.addEventListener("offline", handleOffline);

    return () => {
      window.removeEventListener("online", handleOnline);
      window.removeEventListener("offline", handleOffline);
    };
  }, [wasOffline]);

  if (isOnline && !showReconnected) {
    return null;
  }

  if (!isOnline) {
    return (
      <aside
        role="status"
        aria-live="assertive"
        className="network-status-offline"
        style={{
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          gap: "0.625rem",
          padding: "0.5rem 1.25rem",
          backgroundColor: "#fffbeb",
          borderBottom: "1px solid #fde68a",
          color: "#92400e",
          fontSize: "0.8125rem",
          fontWeight: 600,
          boxShadow: "0 2px 4px rgba(0, 0, 0, 0.05)",
          zIndex: "var(--z-sticky, 1100)" as unknown as number
        }}
      >
        <WifiOffIcon size={16} style={{ color: "#d97706", flexShrink: 0 }} />
        <span>
          <strong>{t("networkStatus.offlineTitle")}: </strong>
          {t("networkStatus.offlineMessage")}
        </span>
      </aside>
    );
  }

  return (
    <aside
      role="status"
      aria-live="polite"
      className="network-status-reconnected"
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        gap: "0.625rem",
        padding: "0.5rem 1.25rem",
        backgroundColor: "#ecfdf5",
        borderBottom: "1px solid #a7f3d0",
        color: "#065f46",
        fontSize: "0.8125rem",
        fontWeight: 600,
        boxShadow: "0 2px 4px rgba(0, 0, 0, 0.05)",
        zIndex: "var(--z-sticky, 1100)" as unknown as number,
        animation: "fade-in 200ms ease"
      }}
    >
      <WifiIcon size={16} style={{ color: "#059669", flexShrink: 0 }} />
      <span>
        <strong>{t("networkStatus.onlineTitle")}: </strong>
        {t("networkStatus.onlineMessage")}
      </span>
    </aside>
  );
}
