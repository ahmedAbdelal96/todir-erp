import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { QuickPeekDrawer } from "../../../../components/primitives/quick-peek-drawer.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { ServiceLifecycleBadge } from "../../components/service-lifecycle-badge.tsx";
import {
  useServiceRequestQuery,
  useResolveServiceRequestMutation,
  useCloseServiceRequestMutation,
  useCancelServiceRequestMutation
} from "../../hooks/use-service-queries.ts";
import { TriageRequestModal } from "../modals/triage-request-modal.tsx";
import {
  AlertCircleIcon,
  CheckCircleIcon,
  ClockIcon,
  WrenchIcon
} from "../../components/service-icons.tsx";

export interface RequestDetailDrawerProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly requestId: string | null;
}

export function RequestDetailDrawer({
  isOpen,
  onClose,
  legalEntityId,
  requestId
}: RequestDetailDrawerProps): React.JSX.Element {
  const { t } = useTranslation("service");
  const [isTriageOpen, setIsTriageOpen] = useState(false);

  const requestQuery = useServiceRequestQuery(legalEntityId, requestId ?? undefined);
  const resolveMutation = useResolveServiceRequestMutation(legalEntityId);
  const closeMutation = useCloseServiceRequestMutation(legalEntityId);
  const cancelMutation = useCancelServiceRequestMutation(legalEntityId);

  const request = requestQuery.data;

  if (!request) {
    return (
      <QuickPeekDrawer isOpen={isOpen} onClose={onClose} title={t("requests.requestDetail")}>
        <div style={{ padding: "2rem", textAlign: "center", color: "#64748b" }}>
          {requestQuery.isLoading ? t("common.loading") : t("requests.noRequestSelected")}
        </div>
      </QuickPeekDrawer>
    );
  }

  const handleResolve = async () => {
    try {
      await resolveMutation.mutateAsync({
        legalEntityId,
        requestId: request.id,
        resolutionSummary: "Resolved by service agent"
      });
    } catch {
      // handled by TanStack
    }
  };

  const handleClose = async () => {
    try {
      await closeMutation.mutateAsync({
        legalEntityId,
        requestId: request.id
      });
    } catch {
      // handled by TanStack
    }
  };

  const handleCancel = async () => {
    try {
      await cancelMutation.mutateAsync({
        legalEntityId,
        requestId: request.id,
        reason: "Canceled by user"
      });
    } catch {
      // handled by TanStack
    }
  };

  return (
    <>
      <QuickPeekDrawer
        isOpen={isOpen}
        onClose={onClose}
        title={`${request.requestNumber} - ${request.subject || request.title}`}
        subtitle={`${t("requests.context")}: ${request.context === "customer" ? t("contexts.customer") : t("contexts.internal")}`}
        width="540px"
      >
        <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", padding: "1rem 0" }}>
          {/* Header Status & Priority Card */}
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              padding: "1rem",
              borderRadius: "var(--radius-md, 0.5rem)",
              backgroundColor: "var(--color-surface-subtle, #f8fafc)",
              border: "1px solid var(--color-border-subtle, #e2e8f0)"
            }}
          >
            <div>
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
                {t("common.status")}:
              </span>
              <div style={{ marginTop: "0.25rem" }}>
                <ServiceLifecycleBadge status={request.status} />
              </div>
            </div>

            <div>
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
                {t("common.priority")}:
              </span>
              <div style={{ marginTop: "0.25rem" }}>
                <ServiceLifecycleBadge status={request.priority} />
              </div>
            </div>

            <div>
              <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
                {t("requests.reportedAt")}:
              </span>
              <div style={{ marginTop: "0.25rem", fontSize: "0.8125rem", fontWeight: 600 }}>
                {request.createdAtUtc ? new Date(request.createdAtUtc).toLocaleDateString() : "—"}
              </div>
            </div>
          </div>

          {/* Customer / Asset Details */}
          <div
            style={{
              padding: "1rem",
              borderRadius: "var(--radius-md, 0.5rem)",
              backgroundColor: "#ffffff",
              border: "1px solid var(--color-border-default, #cbd5e1)"
            }}
          >
            <h4 style={{ margin: "0 0 0.75rem", fontSize: "0.875rem", fontWeight: 700 }}>
              {t("requests.entityDetails")}
            </h4>
            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem", fontSize: "0.8125rem" }}>
              {request.context === "customer" ? (
                <>
                  <div>
                    <span style={{ color: "#64748b" }}>{t("requests.customerName")}:</span>
                    <strong style={{ display: "block", color: "#0f172a", marginTop: "0.125rem" }}>
                      {request.customerName || "—"}
                    </strong>
                  </div>
                  <div>
                    <span style={{ color: "#64748b" }}>{t("requests.siteLocation")}:</span>
                    <strong style={{ display: "block", color: "#0f172a", marginTop: "0.125rem" }}>
                      {request.siteLocation || "—"}
                    </strong>
                  </div>
                </>
              ) : (
                <div>
                  <span style={{ color: "#64748b" }}>{t("requests.assetReference")}:</span>
                  <strong style={{ display: "block", color: "#0f172a", marginTop: "0.125rem" }}>
                    {request.assetReference || "—"}
                  </strong>
                </div>
              )}
            </div>
          </div>

          {/* Description */}
          <div
            style={{
              padding: "1rem",
              borderRadius: "var(--radius-md, 0.5rem)",
              backgroundColor: "#ffffff",
              border: "1px solid var(--color-border-default, #cbd5e1)"
            }}
          >
            <h4 style={{ margin: "0 0 0.5rem", fontSize: "0.875rem", fontWeight: 700 }}>
              {t("requests.description")}
            </h4>
            <p style={{ margin: 0, fontSize: "0.8125rem", color: "#334155", lineHeight: 1.6 }}>
              {request.description || t("requests.noDescription")}
            </p>
          </div>

          {/* Action Toolbar */}
          <div style={{ display: "flex", flexWrap: "wrap", gap: "0.75rem", marginTop: "0.5rem" }}>
            {request.status === "new" && (
              <Button
                variant="primary"
                onClick={() => setIsTriageOpen(true)}
                startIcon={<AlertCircleIcon size={16} />}
              >
                {t("requests.triageTitle")}
              </Button>
            )}

            {request.status !== "resolved" && request.status !== "closed" && request.status !== "canceled" && (
              <Button
                variant="secondary"
                onClick={handleResolve}
                isLoading={resolveMutation.isPending}
                startIcon={<CheckCircleIcon size={16} />}
              >
                {t("requests.resolve")}
              </Button>
            )}

            {request.status === "resolved" && (
              <Button
                variant="primary"
                onClick={handleClose}
                isLoading={closeMutation.isPending}
                startIcon={<CheckCircleIcon size={16} />}
              >
                {t("requests.close")}
              </Button>
            )}

            {request.status !== "closed" && request.status !== "canceled" && (
              <Button
                variant="danger"
                onClick={handleCancel}
                isLoading={cancelMutation.isPending}
              >
                {t("requests.cancel")}
              </Button>
            )}
          </div>
        </div>
      </QuickPeekDrawer>

      {isTriageOpen && (
        <TriageRequestModal
          isOpen={isTriageOpen}
          onClose={() => setIsTriageOpen(false)}
          legalEntityId={legalEntityId}
          request={request}
        />
      )}
    </>
  );
}
