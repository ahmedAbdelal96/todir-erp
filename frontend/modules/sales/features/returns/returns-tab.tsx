import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { SalesDataTable } from "../../components/sales-data-table.tsx";
import { SalesStatusBadge } from "../../components/sales-status-badge.tsx";
import { SalesCustomerPicker } from "../../components/sales-customer-picker.tsx";
import { SalesCustomerDisplay } from "../../components/sales-customer-display.tsx";
import { SalesProductPicker } from "../../components/sales-product-picker.tsx";
import { SalesProductDisplay } from "../../components/sales-product-display.tsx";
import {
  useApproveReturnMutation,
  useCreateReturnMutation,
  useExecuteReturnMutation,
  useSalesReturn
} from "../../hooks/use-sales-queries.ts";
import { mapSalesError } from "../../services/sales-error-map.ts";
import type { ReturnLineRequest, SalesReturnView } from "../../types.ts";

export interface ReturnsTabProps {
  readonly legalEntityId: string;
}

export function ReturnsTab({ legalEntityId }: ReturnsTabProps): React.JSX.Element {
  const { t } = useTranslation("sales");

  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [inspectedReturnId, setInspectedReturnId] = useState<string | null>(null);

  // Return creation form
  const createMutation = useCreateReturnMutation(legalEntityId);
  const [partyId, setPartyId] = useState("");
  const [sourceType, setSourceType] = useState<"SalesOrder" | "Invoice">("SalesOrder");
  const [sourceDocumentId, setSourceDocumentId] = useState("");
  const [reason, setReason] = useState("");
  const [returnLines, setReturnLines] = useState<readonly ReturnLineRequest[]>([
    {
      productVariantId: "a6315416-cffe-bf52-bcf0-d5e590d3bf94",
      quantity: 1,
      description: "",
      stockStatus: "Available"
    }
  ]);
  const [formError, setFormError] = useState<string | null>(null);

  // Active returns list tracking
  const [activeReturns, setActiveReturns] = useState<readonly SalesReturnView[]>([
    {
      id: "RET-2026-0001",
      legalEntityId,
      partyId: "0b15b508-3aa3-4f93-b67f-c1f010375997",
      state: "Requested",
      sourceDocumentType: "SalesOrder",
      sourceDocumentId: "SO-2026-0001",
      reason: t("returns.defaultReturnReason"),
      version: 1,
      lines: [
        {
          productVariantId: "a6315416-cffe-bf52-bcf0-d5e590d3bf94",
          quantity: 2,
          description: "",
          stockStatus: "Available"
        }
      ]
    },
    {
      id: "RET-2026-0002",
      legalEntityId,
      partyId: "0b15b508-3aa3-4f93-b67f-c1f010375998",
      state: "Approved",
      sourceDocumentType: "SalesOrder",
      sourceDocumentId: "SO-2026-0002",
      reason: t("returns.specMismatchReturnReason"),
      version: 1,
      lines: [
        {
          productVariantId: "b8524107-dfef-4fa1-a647-ea6541f92a34",
          quantity: 5,
          description: "",
          stockStatus: "Available"
        }
      ]
    }
  ]);

  const handleCreateReturn = async (e: React.FormEvent) => {
    e.preventDefault();
    setFormError(null);

    if (!partyId) {
      setFormError(t("returns.customerRequired"));
      return;
    }

    if (!reason.trim()) {
      setFormError(t("returns.reasonRequired"));
      return;
    }

    try {
      const returnId = await createMutation.mutateAsync({
        legalEntityId,
        partyId,
        sourceDocumentType: sourceType,
        sourceDocumentId: sourceDocumentId || null,
        reason,
        lines: returnLines.map((l) => ({
          ...l,
          description: l.description || t("returns.item")
        }))
      });

      const newReturn: SalesReturnView = {
        id: returnId,
        legalEntityId,
        partyId,
        state: "Requested",
        sourceDocumentType: sourceType,
        sourceDocumentId: sourceDocumentId || null,
        reason,
        version: 1,
        lines: returnLines
      };

      setActiveReturns((prev) => [newReturn, ...prev]);
      setIsCreateOpen(false);
      setReason("");
    } catch (err) {
      const mapped = mapSalesError(err);
      setFormError(t(mapped.messageKey, { defaultValue: mapped.code }));
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
      {/* Header and Create Button */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
        <div>
          <h3 style={{ margin: 0, fontSize: "1.125rem", fontWeight: 700 }}>
            {t("returns.title")}
          </h3>
          <p style={{ margin: "0.25rem 0 0", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
            {t("returns.subtitle")}
          </p>
        </div>

        <Button
          variant="primary"
          onPress={() => setIsCreateOpen(true)}
          type="button"
        >
          {t("returns.newReturn")}
        </Button>
      </div>

      {/* Returns List Table */}
      <div
        style={{
          backgroundColor: "var(--color-surface-card, #ffffff)",
          border: "1px solid var(--color-border-default, #cbd5e1)",
          borderRadius: "var(--radius-lg, 0.75rem)",
          padding: "1.25rem 1.5rem",
          display: "flex",
          flexDirection: "column",
          gap: "1rem",
          boxShadow: "0 1px 3px 0 rgba(0, 0, 0, 0.05)"
        }}
      >
        <SalesDataTable minWidth="850px">
          <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start" }}>
            <thead>
              <tr style={{ borderBottom: "1px solid var(--color-border-default)", backgroundColor: "var(--color-surface-subtle)" }}>
                <th style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", fontWeight: 600 }}>{t("returns.returnId")}</th>
                <th style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", fontWeight: 600 }}>{t("returns.customer")}</th>
                <th style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", fontWeight: 600 }}>{t("returns.sourceDoc")}</th>
                <th style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", fontWeight: 600 }}>{t("returns.reason")}</th>
                <th style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", fontWeight: 600 }}>{t("returns.status")}</th>
                <th style={{ padding: "0.75rem 1rem", fontSize: "0.8125rem", fontWeight: 600, textAlign: "center" }}>{t("common.actions")}</th>
              </tr>
            </thead>
            <tbody>
              {activeReturns.map((ret) => (
                <tr key={ret.id} style={{ borderBottom: "1px solid var(--color-border-subtle)" }}>
                  <td style={{ padding: "0.75rem 1rem", fontFamily: "monospace", fontSize: "0.875rem", fontWeight: 600, color: "var(--color-brand-primary)", whiteSpace: "nowrap" }}>
                    {ret.id.length > 20 ? ret.id.slice(0, 12) : ret.id}
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <SalesCustomerDisplay partyId={ret.partyId} />
                  </td>
                  <td style={{ padding: "0.75rem 1rem", fontSize: "0.875rem", whiteSpace: "nowrap" }}>
                    {ret.sourceDocumentType === "Invoice" ? t("returns.sourceInvoice") : t("returns.sourceSalesOrder")}
                    {ret.sourceDocumentId && ret.sourceDocumentId.length <= 16 ? ` (${ret.sourceDocumentId})` : ""}
                  </td>
                  <td style={{ padding: "0.75rem 1rem", fontSize: "0.875rem", color: "var(--color-content-muted)" }}>
                    {ret.reason}
                  </td>
                  <td style={{ padding: "0.75rem 1rem" }}>
                    <SalesStatusBadge kind="returnState" status={ret.state} />
                  </td>
                  <td style={{ padding: "0.75rem 1rem", textAlign: "center" }}>
                    <Button
                      variant="secondary"
                      onPress={() => setInspectedReturnId(ret.id)}
                      type="button"
                    >
                      {t("common.manage")}
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </SalesDataTable>
      </div>

      {/* Create Return Dialog */}
      {isCreateOpen && (
        <Dialog
          isOpen={isCreateOpen}
          onOpenChange={(open) => { if (!open) setIsCreateOpen(false); }}
          title={t("returns.newReturn")}
          isDismissable
        >
          <form onSubmit={handleCreateReturn} style={{ display: "flex", flexDirection: "column", gap: "1rem", minWidth: "500px" }}>
            {formError && (
              <div
                role="alert"
                style={{
                  padding: "0.75rem 1rem",
                  backgroundColor: "var(--color-status-danger-subtle)",
                  border: "1px solid var(--color-status-danger)",
                  borderRadius: "var(--radius-md)",
                  color: "var(--color-status-danger)",
                  fontSize: "0.875rem"
                }}
              >
                {formError}
              </div>
            )}

            <div>
              <SalesCustomerPicker
                value={partyId}
                onChange={setPartyId}
                isRequired
                label={t("returns.customer")}
              />
            </div>

            <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
              <div>
                <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.25rem" }}>
                  {t("returns.sourceDocType")}
                </label>
                <select
                  value={sourceType}
                  onChange={(e) => setSourceType(e.target.value as "SalesOrder" | "Invoice")}
                  style={{
                    width: "100%",
                    padding: "0.5rem",
                    borderRadius: "var(--radius-sm)",
                    border: "1px solid var(--color-border-default)"
                  }}
                >
                  <option value="SalesOrder">{t("returns.sourceSalesOrder")}</option>
                  <option value="Invoice">{t("returns.sourceInvoice")}</option>
                </select>
              </div>

              <TextField
                label={t("returns.sourceDocId")}
                value={sourceDocumentId}
                onChange={setSourceDocumentId}
                placeholder={t("returns.sourceDocIdPlaceholder")}
              />
            </div>

            <TextField
              label={t("returns.reason")}
              value={reason}
              onChange={setReason}
              isRequired
              placeholder={t("returns.reasonPlaceholder")}
            />

            {/* Line item selection */}
            <div>
              <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
                {t("returns.itemToReturn")}
              </label>
              <SalesProductPicker
                value={returnLines[0]?.productVariantId ?? undefined}
                onChange={(id, p) => {
                  setReturnLines([
                    {
                      productVariantId: id,
                      quantity: 1,
                      description: p.name,
                      stockStatus: "Available"
                    }
                  ]);
                }}
              />
            </div>

            <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", borderTop: "1px solid var(--color-border-default)", paddingTop: "1rem" }}>
              <Button variant="secondary" onPress={() => setIsCreateOpen(false)} type="button">
                {t("common.cancel")}
              </Button>
              <Button
                variant="primary"
                type="submit"
                isDisabled={createMutation.isPending}
              >
                {createMutation.isPending ? t("common.saving") : t("returns.submitReturn")}
              </Button>
            </div>
          </form>
        </Dialog>
      )}

      {/* Inspect Return Sub-Dialog */}
      {inspectedReturnId && (
        <ReturnDetailModal
          legalEntityId={legalEntityId}
          returnId={inspectedReturnId}
          initialReturn={activeReturns.find((r) => r.id === inspectedReturnId)}
          onClose={() => setInspectedReturnId(null)}
          onStateUpdated={(newState) => {
            setActiveReturns((prev) =>
              prev.map((r) => (r.id === inspectedReturnId ? { ...r, state: newState } : r))
            );
          }}
        />
      )}
    </div>
  );
}

interface ReturnDetailModalProps {
  readonly legalEntityId: string;
  readonly returnId: string;
  readonly initialReturn?: SalesReturnView | undefined;
  readonly onClose: () => void;
  readonly onStateUpdated: (state: any) => void;
}

function ReturnDetailModal({
  legalEntityId,
  returnId,
  initialReturn,
  onClose,
  onStateUpdated
}: ReturnDetailModalProps): React.JSX.Element {
  const { t } = useTranslation("sales");
  const { data: serverReturn, isLoading } = useSalesReturn(legalEntityId, returnId);
  const currentReturn = serverReturn ?? initialReturn;

  const approveMutation = useApproveReturnMutation(legalEntityId, returnId);
  const executeMutation = useExecuteReturnMutation(legalEntityId, returnId);

  const [actionError, setActionError] = useState<string | null>(null);
  const [actionSuccess, setActionSuccess] = useState<string | null>(null);

  const handleApprove = async () => {
    setActionError(null);
    try {
      await approveMutation.mutateAsync({
        version: currentReturn!.version,
        idempotencyKey: `ret-app-${currentReturn!.id}-${Date.now()}`
      });
      setActionSuccess(t("returns.approvedSuccess"));
      onStateUpdated("Approved");
    } catch (err) {
      const mapped = mapSalesError(err);
      setActionError(t(mapped.messageKey, { defaultValue: mapped.code }));
    }
  };

  const handleExecute = async () => {
    setActionError(null);
    try {
      await executeMutation.mutateAsync({
        version: currentReturn!.version,
        idempotencyKey: `ret-exe-${currentReturn!.id}-${Date.now()}`
      });
      setActionSuccess(t("returns.executedSuccess"));
      onStateUpdated("Executed");
    } catch (err) {
      const mapped = mapSalesError(err);
      setActionError(t(mapped.messageKey, { defaultValue: mapped.code }));
    }
  };

  return (
    <Dialog
      isOpen
      onOpenChange={(open) => { if (!open) onClose(); }}
      title={`${t("returns.detailTitle")} — ${returnId.slice(0, 12)}`}
      isDismissable
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", minWidth: "550px" }}>
        {isLoading && (
          <div style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted)" }}>
            {t("common.loading")}
          </div>
        )}

        {actionError && (
          <div
            role="alert"
            style={{
              padding: "0.75rem 1rem",
              backgroundColor: "var(--color-status-danger-subtle)",
              border: "1px solid var(--color-status-danger)",
              borderRadius: "var(--radius-md)",
              color: "var(--color-status-danger)",
              fontSize: "0.875rem"
            }}
          >
            {actionError}
          </div>
        )}

        {actionSuccess && (
          <div
            role="status"
            style={{
              padding: "0.75rem 1rem",
              backgroundColor: "var(--color-status-success-subtle)",
              border: "1px solid var(--color-status-success)",
              borderRadius: "var(--radius-md)",
              color: "var(--color-status-success)",
              fontSize: "0.875rem"
            }}
          >
            {actionSuccess}
          </div>
        )}

        {currentReturn && (
          <>
            <div style={{ display: "flex", justifyContent: "space-between", backgroundColor: "var(--color-surface-subtle)", padding: "1rem", borderRadius: "var(--radius-md)" }}>
              <div>
                <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>{t("returns.customer")}</span>
                <SalesCustomerDisplay partyId={currentReturn.partyId} />
              </div>

              <div>
                <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>{t("returns.status")}</span>
                <SalesStatusBadge kind="returnState" status={currentReturn.state} />
              </div>
            </div>

            <div style={{ fontSize: "0.875rem" }}>
              <strong>{t("returns.reason")}: </strong>
              <span>{currentReturn.reason}</span>
            </div>

            {/* Actions Toolbar */}
            <div style={{ display: "flex", gap: "0.5rem" }}>
              {currentReturn.state === "Requested" && (
                <Button
                  variant="primary"
                  onPress={handleApprove}
                  type="button"
                  isDisabled={approveMutation.isPending}
                >
                  {t("returns.approveReturn")}
                </Button>
              )}

              {currentReturn.state === "Approved" && (
                <Button
                  variant="primary"
                  onPress={handleExecute}
                  type="button"
                  isDisabled={executeMutation.isPending}
                >
                  {t("returns.executeReturn")}
                </Button>
              )}
            </div>

            {/* Return items */}
            <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
              <h4 style={{ margin: 0, fontSize: "0.875rem" }}>{t("returns.items")}</h4>
              <SalesDataTable minWidth="500px">
                <table style={{ width: "100%", borderCollapse: "collapse", textAlign: "start" }}>
                  <thead>
                    <tr style={{ borderBottom: "1px solid var(--color-border-default)", backgroundColor: "var(--color-surface-subtle)" }}>
                      <th style={{ padding: "0.5rem 0.75rem", fontSize: "0.75rem" }}>{t("returns.item")}</th>
                      <th style={{ padding: "0.5rem 0.75rem", fontSize: "0.75rem", textAlign: "end" }}>{t("returns.quantity")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {currentReturn.lines.map((l, idx) => (
                      <tr key={idx} style={{ borderBottom: "1px solid var(--color-border-subtle)" }}>
                        <td style={{ padding: "0.5rem 0.75rem" }}>
                          <SalesProductDisplay productVariantId={l.productVariantId ?? undefined} />
                        </td>
                        <td style={{ padding: "0.5rem 0.75rem", textAlign: "end", fontSize: "0.875rem" }}>
                          {l.quantity}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </SalesDataTable>
            </div>
          </>
        )}

        <div style={{ display: "flex", justifyContent: "flex-end", borderTop: "1px solid var(--color-border-default)", paddingTop: "1rem" }}>
          <Button variant="secondary" onPress={onClose} type="button">
            {t("common.close")}
          </Button>
        </div>
      </div>
    </Dialog>
  );
}
