import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { QuickPeekDrawer } from "../../../../components/primitives/quick-peek-drawer.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Table } from "../../../../components/primitives/table.tsx";
import {
  ExpenseStateBadge,
  ApprovalStateBadge,
  AccountingStateBadge,
  SettlementStateBadge,
  EvidenceStateBadge,
  PolicyStateBadge,
  PaymentSourceBadge
} from "../../components/expense-state-badge.tsx";
import {
  useExpenseDetailQuery,
  useExpenseEvidenceQuery,
  useExpenseItemizationsQuery,
  useExpenseParticipantsQuery,
  useExpenseHistoryQuery,
  useExpenseDimensionsQuery,
  useSubmitExpenseMutation,
  useFinalizeExpenseMutation,
  useReverseExpenseMutation,
  useDecideApprovalMutation,
  useAddItemizationMutation,
  useAddParticipantMutation,
  useRequestPolicyExceptionMutation
} from "../../hooks/use-expenses-queries.ts";
import { expensesApi } from "../../services/expenses-api.ts";
import { DeclareMissingReceiptModal } from "../modals/declare-missing-receipt-modal.tsx";

export interface ExpenseDetailDrawerProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly expenseId: string | null;
  readonly canApprove?: boolean;
  readonly canFinalize?: boolean;
}

export function ExpenseDetailDrawer({
  isOpen,
  onClose,
  legalEntityId,
  expenseId,
  canApprove = false,
  canFinalize = false
}: ExpenseDetailDrawerProps): React.JSX.Element {
  const { t } = useTranslation("expenses");

  const [activeTab, setActiveTab] = useState<
    "overview" | "evidence" | "policy" | "itemizations" | "participants" | "dimensions" | "history"
  >("overview");

  const [isMissingReceiptOpen, setIsMissingReceiptOpen] = useState(false);
  const [itemDesc, setItemDesc] = useState("");
  const [itemAmount, setItemAmount] = useState("");
  const [partName, setPartName] = useState("");
  const [partRole, setPartRole] = useState("MealParticipant");
  const [exceptionReason, setExceptionReason] = useState("");
  const [reversalReason, setReversalReason] = useState("");
  const [showReversalInput, setShowReversalInput] = useState(false);

  // Evidence upload state
  const [uploadFile, setUploadFile] = useState<File | null>(null);
  const [uploadType, setUploadType] = useState<string>("TaxInvoice");
  const [isUploading, setIsUploading] = useState<boolean>(false);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [ocrStatus, setOcrStatus] = useState<Record<string, string>>({});

  const detailQuery = useExpenseDetailQuery(legalEntityId, expenseId ?? "");
  const evidenceQuery = useExpenseEvidenceQuery(legalEntityId, expenseId ?? "");
  const itemizationsQuery = useExpenseItemizationsQuery(legalEntityId, expenseId ?? "");
  const participantsQuery = useExpenseParticipantsQuery(legalEntityId, expenseId ?? "");
  const historyQuery = useExpenseHistoryQuery(legalEntityId, expenseId ?? "");
  const dimensionsQuery = useExpenseDimensionsQuery(legalEntityId, expenseId ?? "");

  const submitMutation = useSubmitExpenseMutation(legalEntityId, expenseId ?? "");
  const finalizeMutation = useFinalizeExpenseMutation(legalEntityId, expenseId ?? "");
  const reverseMutation = useReverseExpenseMutation(legalEntityId, expenseId ?? "");
  const decideApprovalMutation = useDecideApprovalMutation(legalEntityId, expenseId ?? "");
  const addItemMutation = useAddItemizationMutation(legalEntityId, expenseId ?? "");
  const addParticipantMutation = useAddParticipantMutation(legalEntityId, expenseId ?? "");
  const requestExceptionMutation = useRequestPolicyExceptionMutation(legalEntityId, expenseId ?? "");

  const expense = detailQuery.data;

  if (!isOpen || !expenseId) {
    return <></>;
  }

  const handleSubmit = async () => {
    if (!expense) return;
    try {
      await submitMutation.mutateAsync({ version: expense.version });
      try {
        await expensesApi.requestApproval(legalEntityId, expense.id, {
          reason: "طلب اعتماد المصروف"
        });
      } catch (appErr) {
        console.warn("Approval request notification:", appErr);
      }
      await detailQuery.refetch();
    } catch (err) {
      console.error("Failed to submit expense", err);
    }
  };

  const handleApprove = async () => {
    if (!expense) return;
    try {
      let reqId = expense.approvalRequestId;
      if (!reqId) {
        try {
          const approval = await expensesApi.requestApproval(legalEntityId, expense.id, {
            reason: "طلب اعتماد من منصة مراجعة المصروفات"
          });
          reqId = approval.requestId;
        } catch {
          // If requestApproval cannot be called by approver, continue
        }
      }
      if (reqId) {
        await decideApprovalMutation.mutateAsync({
          requestId: reqId,
          decision: "Approved",
          reason: "Approved from review workbench"
        });
        await detailQuery.refetch();
      }
    } catch (err) {
      console.error("Failed to approve expense", err);
    }
  };

  const handleReject = async () => {
    if (!expense) return;
    try {
      let reqId = expense.approvalRequestId;
      if (!reqId) {
        try {
          const approval = await expensesApi.requestApproval(legalEntityId, expense.id, {
            reason: "طلب اعتماد من منصة مراجعة المصروفات"
          });
          reqId = approval.requestId;
        } catch {
          // ignore
        }
      }
      if (reqId) {
        await decideApprovalMutation.mutateAsync({
          requestId: reqId,
          decision: "Rejected",
          reason: "Rejected from review workbench"
        });
        await detailQuery.refetch();
      }
    } catch (err) {
      console.error("Failed to reject expense", err);
    }
  };

  const handleFinalize = async () => {
    if (!expense) return;
    await finalizeMutation.mutateAsync({ version: expense.version });
  };

  const handleReverse = async () => {
    if (!expense || !reversalReason.trim()) return;
    await reverseMutation.mutateAsync({ version: expense.version, reason: reversalReason.trim() });
    setShowReversalInput(false);
    setReversalReason("");
  };

  const handleAddItem = async (e: React.FormEvent) => {
    e.preventDefault();
    const parsed = parseFloat(itemAmount);
    if (!itemDesc.trim() || isNaN(parsed) || parsed <= 0) return;
    await addItemMutation.mutateAsync({ description: itemDesc.trim(), amount: parsed });
    setItemDesc("");
    setItemAmount("");
  };

  const handleAddParticipant = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!partName.trim()) return;
    await addParticipantMutation.mutateAsync({ displayName: partName.trim(), role: partRole });
    setPartName("");
  };

  const handleRequestException = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!expense || !exceptionReason.trim()) return;
    await requestExceptionMutation.mutateAsync({
      findingCode: "POLICY_LIMIT_EXCEEDED",
      reason: exceptionReason.trim(),
      version: expense.version
    });
    setExceptionReason("");
  };

  const handleUploadEvidence = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!uploadFile || !expenseId) return;
    setIsUploading(true);
    setUploadError(null);
    try {
      const intent = await expensesApi.createEvidenceUploadIntent(legalEntityId, expenseId, {
        fileName: uploadFile.name,
        contentType: uploadFile.type || "application/octet-stream",
        sizeBytes: uploadFile.size,
        classification: "Confidential"
      });

      const formData = new FormData();
      if (intent.fields) {
        for (const [key, val] of Object.entries(intent.fields)) {
          formData.append(key, val);
        }
      }
      formData.append("file", uploadFile);

      const uploadRes = await fetch(intent.url, {
        method: "POST",
        body: formData
      });

      if (!uploadRes.ok) {
        throw new Error(`Upload storage failed: ${uploadRes.status}`);
      }

      // Retry loop for backend scan readiness
      let completed = false;
      let attempts = 0;
      while (!completed && attempts < 5) {
        attempts++;
        try {
          await expensesApi.completeEvidenceUpload(legalEntityId, expenseId, {
            fileRef: intent.fileRef,
            evidenceType: uploadType
          });
          completed = true;
        } catch (err: any) {
          if (attempts < 5 && (err?.message?.includes("scan") || err?.message?.includes("not_ready"))) {
            await new Promise((r) => setTimeout(r, 1000));
          } else {
            throw err;
          }
        }
      }

      setUploadFile(null);
      await evidenceQuery.refetch();
      await detailQuery.refetch();
    } catch (err: any) {
      setUploadError(err?.message || t("evidenceUpload.uploadError"));
    } finally {
      setIsUploading(false);
    }
  };

  const handleDownloadEvidence = async (evId: string) => {
    try {
      const res = await expensesApi.getEvidenceDownloadUrl(legalEntityId, expenseId, evId);
      if (res?.url) {
        window.open(res.url, "_blank");
      }
    } catch (err) {
      console.error("Failed to get download URL", err);
    }
  };

  const handleRequestOcr = async (evId: string) => {
    try {
      setOcrStatus((prev) => ({ ...prev, [evId]: "Pending" }));
      const job = await expensesApi.requestOcr(legalEntityId, expenseId, evId);
      setOcrStatus((prev) => ({ ...prev, [evId]: job.state }));
    } catch (err) {
      setOcrStatus((prev) => ({ ...prev, [evId]: "Failed" }));
    }
  };

  return (
    <>
      <QuickPeekDrawer
        isOpen={isOpen}
        onClose={onClose}
        title={t("drawer.title")}
        subtitle={t("drawer.subtitle")}
        width="640px"
      >
        {detailQuery.isLoading ? (
          <div style={{ padding: "2rem", textAlign: "center" }}>{t("common.loading")}</div>
        ) : !expense ? (
          <div style={{ padding: "2rem", textAlign: "center" }}>{t("common.errorTitle")}</div>
        ) : (
          <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", padding: "1rem" }}>
            {/* Header info card */}
            <div
              style={{
                padding: "1rem",
                borderRadius: "var(--radius-md, 0.5rem)",
                border: "1px solid var(--color-border-default, #cbd5e1)",
                backgroundColor: "var(--color-surface-subtle, #f8fafc)",
                display: "flex",
                flexDirection: "column",
                gap: "0.5rem"
              }}
            >
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "0.5rem" }}>
                <div>
                  <h3 style={{ margin: 0, fontSize: "1.125rem", color: "var(--color-content-primary, #0f172a)" }}>
                    {expense.merchantName}
                  </h3>
                  <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)" }}>
                    {expense.expenseDate}
                  </span>
                </div>
                <div style={{ display: "flex", alignItems: "baseline", gap: "0.25rem" }}>
                  <span style={{ fontSize: "1.5rem", fontWeight: 700, color: "var(--color-content-primary, #0f172a)" }}>
                    {expense.amount.toFixed(2)}
                  </span>
                  <span style={{ fontSize: "0.875rem", fontWeight: 600, color: "var(--color-content-muted, #64748b)" }}>
                    {expense.currencyCode}
                  </span>
                </div>
              </div>

              {/* Status Badges Matrix */}
              <div style={{ display: "flex", flexWrap: "wrap", gap: "0.5rem", marginTop: "0.25rem" }}>
                <ExpenseStateBadge state={expense.expenseState} />
                <ApprovalStateBadge state={expense.approvalState} />
                <AccountingStateBadge state={expense.accountingState} />
                <SettlementStateBadge state={expense.settlementState} />
                <EvidenceStateBadge state={expense.evidenceState} />
                <PolicyStateBadge state={expense.policyState} />
                <PaymentSourceBadge source={expense.paymentSource} />
              </div>
            </div>

            {/* Sub-Navigation Tabs */}
            <div
              style={{
                display: "flex",
                gap: "0.5rem",
                borderBottom: "1px solid var(--color-border-default, #cbd5e1)",
                paddingBottom: "0.25rem",
                overflowX: "auto"
              }}
            >
              {[
                { id: "overview", label: t("drawer.overview") },
                { id: "evidence", label: t("drawer.evidenceTab") },
                { id: "policy", label: t("drawer.policyTab") },
                { id: "itemizations", label: t("drawer.itemizationTab") },
                { id: "participants", label: t("drawer.participantsTab") },
                { id: "dimensions", label: t("drawer.dimensionsTab") },
                { id: "history", label: t("drawer.historyTab") }
              ].map((tab) => (
                <button
                  key={tab.id}
                  type="button"
                  onClick={() => setActiveTab(tab.id as any)}
                  style={{
                    padding: "0.5rem 0.75rem",
                    borderRadius: "var(--radius-sm, 0.375rem)",
                    border: "none",
                    background: activeTab === tab.id ? "var(--color-brand-primary, #102a43)" : "transparent",
                    color: activeTab === tab.id ? "#ffffff" : "var(--color-content-primary, #0f172a)",
                    fontSize: "0.8125rem",
                    fontWeight: 600,
                    cursor: "pointer",
                    whiteSpace: "nowrap"
                  }}
                >
                  {tab.label}
                </button>
              ))}
            </div>

            {/* Tab Contents */}
            {activeTab === "overview" && (
              <div style={{ display: "flex", flexDirection: "column", gap: "0.75rem", fontSize: "0.875rem" }}>
                <div>
                  <strong style={{ display: "block", color: "var(--color-content-muted, #64748b)" }}>{t("common.purpose")}:</strong>
                  <span>{expense.businessPurpose || t("common.notAvailable")}</span>
                </div>
                <div>
                  <strong style={{ display: "block", color: "var(--color-content-muted, #64748b)" }}>{t("common.description")}:</strong>
                  <span>{expense.description || t("common.notAvailable")}</span>
                </div>
                {expense.baseAmount && (
                  <div>
                    <strong style={{ display: "block", color: "var(--color-content-muted, #64748b)" }}>{t("drawer.baseAmount")}:</strong>
                    <span>
                      {expense.baseAmount.toFixed(2)} {expense.baseCurrencyCode ?? expense.currencyCode}
                    </span>
                  </div>
                )}
                {expense.taxAmount && (
                  <div>
                    <strong style={{ display: "block", color: "var(--color-content-muted, #64748b)" }}>{t("drawer.taxAmount")}:</strong>
                    <span>
                      {expense.taxAmount.toFixed(2)} {expense.currencyCode}
                    </span>
                  </div>
                )}
              </div>
            )}

            {activeTab === "evidence" && (
              <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
                <div
                  style={{
                    padding: "0.75rem",
                    borderRadius: "var(--radius-md, 0.5rem)",
                    backgroundColor: "var(--color-surface-warning, #fffbeb)",
                    border: "1px solid var(--color-status-warning, #d97706)",
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center"
                  }}
                >
                  <span style={{ fontSize: "0.8125rem", color: "var(--color-status-warning, #d97706)", fontWeight: 600 }}>
                    {t("drawer.missingReceiptDeclared")}
                  </span>
                  <Button type="button" variant="secondary" size="compact" onClick={() => setIsMissingReceiptOpen(true)}>
                    {t("common.declareMissingReceipt")}
                  </Button>
                </div>

                {/* Evidence Upload Form */}
                <form
                  onSubmit={handleUploadEvidence}
                  style={{
                    display: "flex",
                    flexDirection: "column",
                    gap: "0.75rem",
                    padding: "1rem",
                    backgroundColor: "#f8fafc",
                    border: "1px dashed var(--color-border-default, #cbd5e1)",
                    borderRadius: "var(--radius-md, 0.5rem)"
                  }}
                >
                  <strong style={{ fontSize: "0.875rem", color: "#102a43" }}>{t("evidenceUpload.uploadTitle")}</strong>
                  <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap", alignItems: "center" }}>
                    <input
                      type="file"
                      accept=".pdf,.png,.jpg,.jpeg"
                      onChange={(e) => setUploadFile(e.target.files?.[0] || null)}
                      style={{ fontSize: "0.8125rem" }}
                    />
                    <select
                      value={uploadType}
                      onChange={(e) => setUploadType(e.target.value)}
                      style={{
                        padding: "0.375rem 0.5rem",
                        borderRadius: "var(--radius-sm, 0.375rem)",
                        border: "1px solid #cbd5e1",
                        fontSize: "0.8125rem"
                      }}
                    >
                      <option value="TaxInvoice">{t("evidenceTypes.TaxInvoice")}</option>
                      <option value="Receipt">{t("evidenceTypes.Receipt")}</option>
                      <option value="Ticket">{t("evidenceTypes.Ticket")}</option>
                      <option value="Contract">{t("evidenceTypes.Contract")}</option>
                      <option value="Other">{t("evidenceTypes.Other")}</option>
                    </select>
                    <Button
                      type="submit"
                      variant="primary"
                      size="compact"
                      isDisabled={!uploadFile || isUploading}
                    >
                      {isUploading ? t("evidenceUpload.uploading") : t("evidenceUpload.submitButton")}
                    </Button>
                  </div>
                  {uploadError && (
                    <span style={{ fontSize: "0.75rem", color: "#ef4444" }}>{uploadError}</span>
                  )}
                </form>

                {evidenceQuery.data && evidenceQuery.data.length > 0 ? (
                  <div style={{ overflowX: "auto" }}>
                    <Table style={{ minWidth: "550px" }}>
                      <thead>
                        <tr>
                          <th>{t("evidenceUpload.typeLabel")}</th>
                          <th>{t("common.file")}</th>
                          <th>{t("common.status")}</th>
                          <th>{t("common.date")}</th>
                          <th>{t("common.actions")}</th>
                        </tr>
                      </thead>
                      <tbody>
                        {evidenceQuery.data.map((ev) => (
                          <tr key={ev.id}>
                            <td>{t(`evidenceTypes.${ev.evidenceType}` as any, ev.evidenceType)}</td>
                            <td style={{ maxWidth: "120px", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                              {ev.fileReference}
                            </td>
                            <td>
                              <EvidenceStateBadge state={ev.status} />
                            </td>
                            <td>{ev.createdAt.slice(0, 10)}</td>
                            <td>
                              <div style={{ display: "flex", gap: "0.375rem" }}>
                                <Button
                                  type="button"
                                  variant="secondary"
                                  size="compact"
                                  onClick={() => handleDownloadEvidence(ev.id)}
                                >
                                  {t("evidenceUpload.downloadAction")}
                                </Button>
                                <Button
                                  type="button"
                                  variant="secondary"
                                  size="compact"
                                  onClick={() => handleRequestOcr(ev.id)}
                                  isDisabled={ocrStatus[ev.id] === "Pending"}
                                >
                                  {ocrStatus[ev.id] === "Pending"
                                    ? t("evidenceUpload.ocrJobPending")
                                    : t("evidenceUpload.startOcrAction")}
                                </Button>
                              </div>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </Table>
                  </div>
                ) : (
                  <div style={{ textAlign: "center", padding: "1.5rem", color: "var(--color-content-muted, #64748b)" }}>
                    {t("drawer.noEvidence")}
                  </div>
                )}
              </div>
            )}

            {activeTab === "policy" && (
              <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
                <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
                  <span>{t("drawer.policyTab")}:</span>
                  <PolicyStateBadge state={expense.policyState} />
                </div>

                {expense.policyState !== "Passed" && (
                  <form onSubmit={handleRequestException} style={{ display: "flex", flexDirection: "column", gap: "0.75rem" }}>
                    <TextField
                      label={t("drawer.requestException")}
                      placeholder={t("drawer.exceptionReasonPlaceholder")}
                      value={exceptionReason}
                      onChange={(val) => setExceptionReason(val)}
                      isRequired
                    />
                    <Button type="submit" variant="secondary" isDisabled={requestExceptionMutation.isPending}>
                      {t("drawer.requestException")}
                    </Button>
                  </form>
                )}
              </div>
            )}

            {activeTab === "itemizations" && (
              <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
                {itemizationsQuery.data && itemizationsQuery.data.length > 0 ? (
                  <Table>
                    <thead>
                      <tr>
                        <th>{t("common.description")}</th>
                        <th>{t("common.amount")}</th>
                        <th>{t("common.currency")}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {itemizationsQuery.data.map((item) => (
                        <tr key={item.id}>
                          <td>{item.description}</td>
                          <td>{item.amount.toFixed(2)}</td>
                          <td>{item.currencyCode}</td>
                        </tr>
                      ))}
                    </tbody>
                  </Table>
                ) : (
                  <div style={{ textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
                    {t("drawer.noItemizations")}
                  </div>
                )}

                {expense.expenseState === "Draft" && (
                  <form onSubmit={handleAddItem} style={{ display: "flex", gap: "0.5rem", alignItems: "flex-end" }}>
                    <TextField
                      label={t("drawer.addItemization")}
                      placeholder={t("drawer.itemDescriptionPlaceholder")}
                      value={itemDesc}
                      onChange={(val) => setItemDesc(val)}
                      isRequired
                    />
                    <TextField
                      label={t("common.amount")}
                      type="number"
                      placeholder="0.00"
                      value={itemAmount}
                      onChange={(val) => setItemAmount(val)}
                      isRequired
                    />
                    <Button type="submit" variant="primary" isDisabled={addItemMutation.isPending}>
                      +
                    </Button>
                  </form>
                )}
              </div>
            )}

            {activeTab === "participants" && (
              <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
                {participantsQuery.data && participantsQuery.data.length > 0 ? (
                  <Table>
                    <thead>
                      <tr>
                        <th>{t("drawer.participantsTab")}</th>
                        <th>{t("drawer.participantRole")}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {participantsQuery.data.map((p) => (
                        <tr key={p.id}>
                          <td>{p.displayName}</td>
                          <td>{t(`participantRoles.${p.role}` as any, p.role)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </Table>
                ) : (
                  <div style={{ textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
                    {t("drawer.noParticipants")}
                  </div>
                )}

                {expense.expenseState === "Draft" && (
                  <form onSubmit={handleAddParticipant} style={{ display: "flex", gap: "0.5rem", alignItems: "flex-end" }}>
                    <TextField
                      label={t("drawer.addParticipant")}
                      placeholder={t("drawer.participantNamePlaceholder")}
                      value={partName}
                      onChange={(val) => setPartName(val)}
                      isRequired
                    />
                    <Button type="submit" variant="primary" isDisabled={addParticipantMutation.isPending}>
                      +
                    </Button>
                  </form>
                )}
              </div>
            )}

            {activeTab === "dimensions" && (
              <div>
                {dimensionsQuery.data && dimensionsQuery.data.length > 0 ? (
                  <Table>
                    <thead>
                      <tr>
                        <th>{t("drawer.dimensionCode")}</th>
                        <th>{t("drawer.dimensionValue")}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {dimensionsQuery.data.map((dim) => (
                        <tr key={dim.id}>
                          <td>{dim.dimensionCode}</td>
                          <td>{dim.valueCode}</td>
                        </tr>
                      ))}
                    </tbody>
                  </Table>
                ) : (
                  <div style={{ textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
                    {t("drawer.noDimensions")}
                  </div>
                )}
              </div>
            )}

            {activeTab === "history" && (
              <div>
                {historyQuery.data && historyQuery.data.length > 0 ? (
                  <Table>
                    <thead>
                      <tr>
                        <th>{t("common.actions")}</th>
                        <th>{t("common.date")}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {historyQuery.data.map((h) => (
                        <tr key={h.id}>
                          <td>{t(`historyActions.${h.action}` as any, h.action)}</td>
                          <td>{h.occurredAt}</td>
                        </tr>
                      ))}
                    </tbody>
                  </Table>
                ) : (
                  <div style={{ textAlign: "center", color: "var(--color-content-muted, #64748b)" }}>
                    {t("common.notAvailable")}
                  </div>
                )}
              </div>
            )}

            {/* Action Bar Footer */}
            <div
              style={{
                display: "flex",
                justifyContent: "flex-end",
                gap: "0.75rem",
                marginTop: "1rem",
                paddingTop: "1rem",
                borderTop: "1px solid var(--color-border-default, #cbd5e1)",
                flexWrap: "wrap"
              }}
            >
              {expense.expenseState === "Draft" && (
                <Button type="button" variant="primary" onClick={handleSubmit} isDisabled={submitMutation.isPending}>
                  {t("common.submit")}
                </Button>
              )}

              {expense.expenseState === "Submitted" && expense.approvalState !== "Approved" && canApprove && (
                <>
                  <Button type="button" variant="secondary" onClick={handleReject} isDisabled={decideApprovalMutation.isPending}>
                    {t("review.rejectAction")}
                  </Button>
                  <Button type="button" variant="primary" onClick={handleApprove} isDisabled={decideApprovalMutation.isPending}>
                    {t("review.approveAction")}
                  </Button>
                </>
              )}

              {(expense.expenseState === "Approved" || (expense.expenseState === "Submitted" && expense.approvalState === "Approved")) && canFinalize && (
                <Button type="button" variant="primary" onClick={handleFinalize} isDisabled={finalizeMutation.isPending}>
                  {t("review.finalizeAction")}
                </Button>
              )}

              {expense.expenseState === "Finalized" && canFinalize && (
                <>
                  {showReversalInput ? (
                    <div style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
                      <TextField
                        placeholder={t("review.reversalReasonPlaceholder")}
                        aria-label={t("review.reversalReasonLabel")}
                        value={reversalReason}
                        onChange={(val) => setReversalReason(val)}
                      />
                      <Button type="button" variant="danger" onClick={handleReverse} isDisabled={reverseMutation.isPending || !reversalReason.trim()}>
                        {t("review.confirmReverseAction")}
                      </Button>
                      <Button type="button" variant="secondary" onClick={() => setShowReversalInput(false)}>
                        {t("common.cancel")}
                      </Button>
                    </div>
                  ) : (
                    <Button type="button" variant="danger" onClick={() => setShowReversalInput(true)}>
                      {t("review.reverseAction")}
                    </Button>
                  )}
                </>
              )}

              <Button type="button" variant="secondary" onClick={onClose}>
                {t("common.close")}
              </Button>
            </div>
          </div>
        )}
      </QuickPeekDrawer>

      <DeclareMissingReceiptModal
        isOpen={isMissingReceiptOpen}
        onClose={() => setIsMissingReceiptOpen(false)}
        legalEntityId={legalEntityId}
        expenseId={expenseId}
      />
    </>
  );
}
