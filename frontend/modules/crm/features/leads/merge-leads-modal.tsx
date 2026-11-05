import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useMergeLeadsMutation, useLeads } from "../../hooks/use-crm-queries.ts";
import { mapCrmError } from "../../services/crm-error-map.ts";
import type { Lead } from "../../types.ts";

export interface MergeLeadsModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly lead: Lead | null;
  readonly onMerged?: () => void;
}

export function MergeLeadsModal({
  isOpen,
  onClose,
  legalEntityId,
  lead,
  onMerged
}: MergeLeadsModalProps): React.JSX.Element {
  const { t } = useTranslation("crm");
  const mergeMutation = useMergeLeadsMutation(legalEntityId);
  const { data: leadsPage } = useLeads(legalEntityId, { pageSize: 50 });
  const allLeads = leadsPage?.items || [];

  // Filter possible target leads excluding this lead
  const candidates = allLeads.filter((l) => l.id !== lead?.id);
  const [selectedTargetId, setSelectedTargetId] = useState<string>("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const selectedTarget = candidates.find((c) => c.id === selectedTargetId);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!lead || !selectedTarget) return;

    try {
      await mergeMutation.mutateAsync({
        sourceLeadId: lead.id,
        targetLeadId: selectedTarget.id,
        sourceVersion: lead.version,
        targetVersion: selectedTarget.version
      });

      setSelectedTargetId("");
      onClose();
      onMerged?.();
    } catch (err: unknown) {
      const mapped = mapCrmError(err);
      setErrorMessage(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => {
        if (!open) onClose();
      }}
      title={t("leads.mergeModal.title")}
      size="medium"
      footer={
        <>
          <Button
            variant="secondary"
            onPress={onClose}
            isDisabled={mergeMutation.isPending}
          >
            {t("actions.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            form="merge-leads-form"
            isDisabled={mergeMutation.isPending || !selectedTargetId}
          >
            {mergeMutation.isPending ? t("actions.saving") : t("leads.mergeModal.submit")}
          </Button>
        </>
      }
    >
      <form id="merge-leads-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMessage && (
          <div
            role="alert"
            style={{
              padding: "0.625rem 0.875rem",
              borderRadius: "var(--radius-sm)",
              background: "var(--color-status-danger-subtle, #fee2e2)",
              color: "var(--color-status-danger, #b91c1c)",
              fontSize: "0.8125rem"
            }}
          >
            {errorMessage}
          </div>
        )}

        <div style={{ backgroundColor: "var(--color-surface-subtle)", padding: "0.75rem", borderRadius: "var(--radius-sm)" }}>
          <p style={{ margin: 0, fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
            {t("leads.mergeModal.sourceNotice")}
          </p>
          <div style={{ marginTop: "0.5rem", fontWeight: 600, fontSize: "0.875rem" }}>
            {lead?.displayName} ({lead?.companyName || t("leads.mergeModal.noCompany")})
          </div>
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.5rem" }}>
            {t("leads.mergeModal.selectTarget")}
          </label>
          <div style={{ maxHeight: "200px", overflowY: "auto", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-sm)" }}>
            {candidates.length === 0 ? (
              <div style={{ padding: "1rem", textAlign: "center", color: "var(--color-content-muted)" }}>
                {t("leads.mergeModal.noCandidates")}
              </div>
            ) : (
              candidates.map((cand) => (
                <div
                  key={cand.id}
                  onClick={() => setSelectedTargetId(cand.id)}
                  style={{
                    padding: "0.625rem 0.875rem",
                    borderBottom: "1px solid var(--color-surface-subtle)",
                    backgroundColor: selectedTargetId === cand.id ? "var(--color-surface-subtle)" : "var(--color-surface-card)",
                    cursor: "pointer",
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center"
                  }}
                >
                  <div>
                    <div style={{ fontWeight: 600, fontSize: "0.875rem" }}>{cand.displayName}</div>
                    <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{cand.companyName || cand.email || cand.phone}</div>
                  </div>
                  {selectedTargetId === cand.id && <span style={{ color: "var(--color-action-primary)" }}>✓</span>}
                </div>
              ))
            )}
          </div>
        </div>
      </form>
    </Dialog>
  );
}
