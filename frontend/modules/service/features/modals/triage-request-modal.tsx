import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { ServiceSelect as Select } from "../../components/service-select.tsx";
import { TextArea } from "../../../../components/primitives/text-area.tsx";
import { useTriageServiceRequestMutation, useCreateWorkOrderMutation } from "../../hooks/use-service-queries.ts";
import type { ServiceRequestDto, ServicePriority } from "../../types.ts";

export interface TriageRequestModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly request: ServiceRequestDto | null;
}

export function TriageRequestModal({
  isOpen,
  onClose,
  legalEntityId,
  request
}: TriageRequestModalProps): React.JSX.Element {
  const { t } = useTranslation("service");
  const triageMutation = useTriageServiceRequestMutation(legalEntityId);
  const createWorkOrderMutation = useCreateWorkOrderMutation(legalEntityId);

  const [priority, setPriority] = useState<ServicePriority>((request?.priority as ServicePriority) ?? "Normal");
  const [triageNotes, setTriageNotes] = useState("");
  const [generateWorkOrder, setGenerateWorkOrder] = useState(true);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  if (!request) return <React.Fragment />;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      setErrorMsg(null);
      await (triageMutation.mutateAsync as any)({
        legalEntityId,
        requestId: request.id,
        priority,
        triageNotes: triageNotes.trim() || undefined
      });

      if (generateWorkOrder) {
        await (createWorkOrderMutation.mutateAsync as any)({
          legalEntityId,
          serviceRequestId: request.id,
          context: request.context as any,
          workType: "Corrective",
          priority,
          title: request.subject || request.title,
          description: request.description ?? undefined,
          customerPartyId: request.customerPartyId ?? undefined,
          installedBaseId: request.installedBaseId ?? undefined,
          fixedAssetId: request.fixedAssetId ?? undefined,
          customerName: request.customerName ?? undefined,
          siteLocation: request.siteLocation ?? undefined
        });
      }

      onClose();
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : t("common.genericError"));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("requests.triageTitle")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMsg && (
          <div
            style={{
              padding: "0.75rem 1rem",
              borderRadius: "var(--radius-sm, 0.375rem)",
              backgroundColor: "#fef2f2",
              border: "1px solid #fecaca",
              color: "#b91c1c",
              fontSize: "0.875rem"
            }}
          >
            {errorMsg}
          </div>
        )}

        <div
          style={{
            padding: "0.75rem 1rem",
            borderRadius: "var(--radius-md, 0.5rem)",
            backgroundColor: "var(--color-surface-subtle, #f8fafc)",
            border: "1px solid var(--color-border-subtle, #e2e8f0)",
            fontSize: "0.8125rem"
          }}
        >
          <div style={{ fontWeight: 600, color: "var(--color-content-primary, #0f172a)" }}>
            {request.requestNumber} - {request.subject}
          </div>
          <div style={{ color: "var(--color-content-muted, #64748b)", marginTop: "0.25rem" }}>
            {t("requests.context")}: {request.context === "customer" ? t("contexts.customer") : t("contexts.internal")} |{" "}
            {request.customerName ?? request.assetReference ?? ""}
          </div>
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
            {t("requests.updatedPriority")} *
          </label>
          <Select
            value={priority}
            onChange={(val) => setPriority(val as ServicePriority)}
            options={[
              { value: "low", label: t("priorities.low") },
              { value: "medium", label: t("priorities.medium") },
              { value: "high", label: t("priorities.high") },
              { value: "urgent", label: t("priorities.urgent") }
            ]}
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
            {t("requests.triageNotes")}
          </label>
          <TextArea
            value={triageNotes}
            onChange={(val: any) => setTriageNotes(typeof val === 'string' ? val : val?.target?.value ?? '')}
            placeholder={t("requests.triageNotesPlaceholder")}
            rows={3}
          />
        </div>

        <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", marginTop: "0.5rem" }}>
          <input
            type="checkbox"
            id="generate-wo-check"
            checked={generateWorkOrder}
            onChange={(e) => setGenerateWorkOrder(e.target.checked)}
            style={{ width: "1rem", height: "1rem", accentColor: "#2563eb", cursor: "pointer" }}
          />
          <label htmlFor="generate-wo-check" style={{ fontSize: "0.875rem", cursor: "pointer", fontWeight: 500 }}>
            {t("requests.generateWorkOrderNotice")}
          </label>
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isLoading={triageMutation.isPending || createWorkOrderMutation.isPending}
          >
            {t("requests.completeTriage")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
