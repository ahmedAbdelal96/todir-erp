import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useParams, useNavigate } from "react-router-dom";
import { RecordPage } from "../../../components/pages/record-page.tsx";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { LoadingState } from "../../../components/primitives/loading-state.tsx";
import { ErrorState } from "../../../components/primitives/error-state.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { LocalDateDisplay } from "../../../components/fields/local-date-display.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import {
  PaymentStatusBadge,
  ApplicationStatusBadge,
  ReconciliationStatusBadge
} from "../components/payment-status-badge.tsx";
import { PaymentReceiptModal } from "../components/payment-receipt-modal.tsx";
import { RefundDialog } from "../features/refund-dialog.tsx";
import { ReversalDialog } from "../features/reversal-dialog.tsx";
import { ApplyPaymentDialog } from "../features/apply-payment-dialog.tsx";
import {
  usePaymentsScope,
  usePaymentsPermissions,
  usePaymentRecord,
  usePaymentAccounts,
  usePaymentFees,
  useCompleteManualPayment,
  useCancelPendingPayment,
  useAddPaymentFee
} from "../hooks/payment-queries.ts";
import type { PaymentFeeType, CreatePaymentFeeRequest, PaymentMethod, PaymentDirection, PaymentType } from "../types.ts";

export function PaymentDetailPage(): React.JSX.Element {
  const { t } = useTranslation(["payments", "common"]);
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const { legalEntityId, branchId } = usePaymentsScope();
  const perms = usePaymentsPermissions();

  const paymentQuery = usePaymentRecord(legalEntityId, id || "", branchId);
  const accountsQuery = usePaymentAccounts(legalEntityId, branchId);
  const feesQuery = usePaymentFees(legalEntityId, id || "", branchId);

  const completeManualMutation = useCompleteManualPayment();
  const cancelPendingMutation = useCancelPendingPayment();
  const addFeeMutation = useAddPaymentFee();

  // Dialog controls
  const [isReceiptOpen, setIsReceiptOpen] = useState(false);
  const [isRefundOpen, setIsRefundOpen] = useState(false);
  const [isReversalOpen, setIsReversalOpen] = useState(false);
  const [isApplyOpen, setIsApplyOpen] = useState(false);
  const [isAddFeeOpen, setIsAddFeeOpen] = useState(false);
  const [isManualRecoveryOpen, setIsManualRecoveryOpen] = useState(false);
  const [manualRecoveryEvidence, setManualRecoveryEvidence] = useState("");
  const [manualRecoveryError, setManualRecoveryError] = useState("");

  // Fee state
  const [feeType, setFeeType] = useState<PaymentFeeType>("BankFee");
  const [feeAmount, setFeeAmount] = useState<string>("");
  const [feeRef, setFeeRef] = useState<string>("");
  const [actionError, setActionError] = useState<string>("");

  if (paymentQuery.isLoading || accountsQuery.isLoading) {
    return <LoadingState message={t("detail.loading")} />;
  }

  if (paymentQuery.isError || !paymentQuery.data) {
    return (
      <ErrorState
        title={t("detail.notFoundTitle")}
        message={t("detail.notFoundMessage")}
        action={
          <Button variant="primary" onPress={() => navigate("/payments")}>
            {t("detail.backToList")}
          </Button>
        }
      />
    );
  }

  const payment = paymentQuery.data;
  const account = accountsQuery.data?.find((a) => a.id === payment.paymentAccountId);
  const fees = feesQuery.data || [];

  const totalFees = fees.reduce((sum, f) => sum + Number(f.amount), 0);
  const netAmount = Number(payment.amount) - totalFees;
  const factRefText = payment.state === "Completed" ? `fact:payment:${payment.id.slice(0, 8)}` : "—";

  const getDirectionLabel = (dir: PaymentDirection) => {
    switch (dir) {
      case "Incoming": return t("direction.incoming");
      case "Outgoing": return t("direction.outgoing");
      case "InternalTransfer":
      default: return t("direction.internaltransfer");
    }
  };

  const getTypeLabel = (tp: PaymentType) => {
    switch (tp) {
      case "CustomerReceipt": return t("type.customerreceipt");
      case "SupplierPayment": return t("type.supplierpayment");
      case "CustomerAdvance": return t("type.customeradvance");
      case "SupplierAdvance": return t("type.supplieradvance");
      case "CustomerRefund": return t("type.customerrefund");
      case "SupplierRefund": return t("type.supplierrefund");
      case "ExpensePayment": return t("type.expensepayment");
      case "Standalone": return t("type.standalone");
      case "TransferLeg": return t("type.transferleg");
      case "ProviderFee":
      default: return t("type.providerfee");
    }
  };

  const getMethodLabel = (m: PaymentMethod) => {
    switch (m) {
      case "Cash": return t("method.cash");
      case "BankTransfer": return t("method.banktransfer");
      case "Cheque": return t("method.cheque");
      case "Card": return t("method.card");
      case "PaymentGateway": return t("method.paymentgateway");
      case "Wallet": return t("method.wallet");
      case "Other":
      default: return t("method.other");
    }
  };

  const getFeeTypeLabel = (ft: PaymentFeeType) => {
    switch (ft) {
      case "BankFee": return t("feeType.bankfee");
      case "ProviderFee": return t("feeType.providerfee");
      case "CardProcessingFee": return t("feeType.cardprocessingfee");
      case "FXFee": return t("feeType.fxfee");
      case "TransferFee": return t("feeType.transferfee");
      case "OtherApprovedFee":
      default: return t("feeType.otherapprovedfee");
    }
  };

  const getFeeRecognitionLabel = (state?: string) => {
    switch (state?.toLowerCase()) {
      case "recognizedimmediate":
        return t("feeRecognitionState.recognizedImmediate");
      case "recognized":
        return t("feeRecognitionState.recognized");
      case "pending":
        return t("feeRecognitionState.pending");
      case "reversed":
        return t("feeRecognitionState.reversed");
      case "deferred":
        return t("feeRecognitionState.deferred");
      case "estimated":
        return t("feeRecognitionState.estimated");
      default:
        return state ? t("feeRecognitionState.recognizedImmediate") : "—";
    }
  };

  const handleCompleteManual = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    setActionError("");
    setManualRecoveryError("");

    if (!manualRecoveryEvidence.trim()) {
      setManualRecoveryError(t("detail.manualRecoveryEvidenceRequired"));
      return;
    }

    try {
      await completeManualMutation.mutateAsync({
        legalEntityId,
        paymentId: payment.id,
        branchId
      });
      setIsManualRecoveryOpen(false);
      setManualRecoveryEvidence("");
    } catch (err: unknown) {
      const problem = err as { problemDetails?: { code?: string; detail?: string } };
      const code = problem?.problemDetails?.code;
      const msg = code ? (t as any)(`errors.${code}`) : (err as Error).message;
      setManualRecoveryError(msg);
      setActionError(msg);
    }
  };

  const handleCancelPending = async () => {
    setActionError("");
    try {
      await cancelPendingMutation.mutateAsync({
        legalEntityId,
        paymentId: payment.id,
        branchId
      });
    } catch (err: unknown) {
      const problem = err as { problemDetails?: { code?: string; detail?: string } };
      const code = problem?.problemDetails?.code;
      setActionError(code ? (t as any)(`errors.${code}`) : (err as Error).message);
    }
  };

  const handleAddFeeSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setActionError("");
    const numAmount = Number(feeAmount);
    if (!feeAmount || isNaN(numAmount) || numAmount <= 0) {
      setActionError(t("errors.amount_invalid"));
      return;
    }

    try {
      const request: CreatePaymentFeeRequest = {
        type: feeType,
        amount: Number(feeAmount).toFixed(2),
        currencyCode: payment.currencyCode,
        sourceReference: feeRef.trim() || null
      };

      await addFeeMutation.mutateAsync({
        legalEntityId,
        paymentId: payment.id,
        request
      });

      setIsAddFeeOpen(false);
      setFeeAmount("");
      setFeeRef("");
    } catch (err: unknown) {
      const problem = err as { problemDetails?: { code?: string; detail?: string } };
      const code = problem?.problemDetails?.code;
      setActionError(code ? (t as any)(`errors.${code}`) : (err as Error).message);
    }
  };

  const feeTypeOptions = [
    { id: "BankFee", label: t("feeType.bankfee") },
    { id: "ProviderFee", label: t("feeType.providerfee") },
    { id: "CardProcessingFee", label: t("feeType.cardprocessingfee") },
    { id: "FXFee", label: t("feeType.fxfee") },
    { id: "TransferFee", label: t("feeType.transferfee") },
    { id: "OtherApprovedFee", label: t("feeType.otherapprovedfee") }
  ];

  // Actions bar
  const headerActions = (
    <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap", alignItems: "center" }}>
      {payment.state === "Pending" && (
        <>
          <Button
            variant="primary"
            onPress={() => paymentQuery.refetch()}
            isDisabled={paymentQuery.isFetching}
          >
            {paymentQuery.isFetching ? t("detail.checkingStatus") : t("detail.syncStatusAction")}
          </Button>

          {perms.canCompleteManual && (
            <Button
              variant="secondary"
              onPress={() => {
                setManualRecoveryEvidence("");
                setManualRecoveryError("");
                setIsManualRecoveryOpen(true);
              }}
            >
              {t("detail.completeManualAction")}
            </Button>
          )}

          {perms.canCancelPending && (
            <Button
              variant="danger"
              onPress={handleCancelPending}
              isDisabled={cancelPendingMutation.isPending}
            >
              {t("detail.cancelPendingAction")}
            </Button>
          )}
        </>
      )}

      {payment.state === "Completed" && (
        <>
          <Button variant="secondary" onPress={() => setIsReceiptOpen(true)}>
            {t("detail.printReceiptAction")}
          </Button>

          {perms.canCreateRefund && payment.direction === "Incoming" && (
            <Button variant="secondary" onPress={() => setIsRefundOpen(true)}>
              {t("detail.refundAction")}
            </Button>
          )}

          {perms.canReverse && (
            <Button variant="danger" onPress={() => setIsReversalOpen(true)}>
              {t("detail.reverseAction")}
            </Button>
          )}

          <Button variant="primary" onPress={() => setIsApplyOpen(true)}>
            {t("detail.applyToInvoicesAction")}
          </Button>
        </>
      )}
    </div>
  );

  return (
    <>
      <RecordPage
        title={payment.number}
        breadcrumbs={[
          { label: t("actions.home"), href: "/" },
          { label: t("title"), href: "/payments" },
          { label: payment.number }
        ]}
        headerActions={headerActions}
      >
        {actionError && (
          <div
            style={{
              padding: "0.75rem 1rem",
              borderRadius: "6px",
              backgroundColor: "#fef2f2",
              border: "1px solid #fecaca",
              color: "#991b1b",
              fontSize: "0.875rem",
              marginBottom: "1rem"
            }}
          >
            {actionError}
          </div>
        )}

        <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
          {/* Section 1: Overview & Money Movement */}
          <Surface title={t("detail.sectionOverview")}>
            <div
              style={{
                display: "grid",
                gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))",
                gap: "1.25rem",
                fontSize: "0.875rem"
              }}
            >
              <div>
                <span style={{ color: "#64748b", display: "block", fontSize: "0.75rem" }}>
                  {t("detail.amount")}:
                </span>
                <span style={{ fontSize: "1.25rem", fontWeight: 800, color: "#102a43" }}>
                  <MoneyDisplay value={{ amount: payment.amount, currency: payment.currencyCode }} />
                </span>
              </div>

              <div>
                <span style={{ color: "#64748b", display: "block", fontSize: "0.75rem" }}>
                  {t("detail.state")}:
                </span>
                <div style={{ marginTop: "0.25rem" }}>
                  <PaymentStatusBadge state={payment.state} />
                </div>
              </div>

              <div>
                <span style={{ color: "#64748b", display: "block", fontSize: "0.75rem" }}>
                  {t("detail.type")}:
                </span>
                <span style={{ fontWeight: 600 }}>{getTypeLabel(payment.type)}</span>
              </div>

              <div>
                <span style={{ color: "#64748b", display: "block", fontSize: "0.75rem" }}>
                  {t("detail.method")}:
                </span>
                <span style={{ fontWeight: 600 }}>{getMethodLabel(payment.method)}</span>
              </div>

              <div>
                <span style={{ color: "#64748b", display: "block", fontSize: "0.75rem" }}>
                  {t("detail.account")}:
                </span>
                <span style={{ fontWeight: 600 }}>
                  {account?.name || account?.code || t("detail.operationalAccountDefault")}
                </span>
                {account?.maskedIdentifier && (
                  <span style={{ display: "block", fontSize: "0.75rem", color: "#64748b" }}>
                    <bdi>{account.maskedIdentifier}</bdi>
                  </span>
                )}
              </div>

              <div>
                <span style={{ color: "#64748b", display: "block", fontSize: "0.75rem" }}>
                  {t("detail.paymentDate")}:
                </span>
                <LocalDateDisplay value={payment.paymentDate} />
              </div>

              <div>
                <span style={{ color: "#64748b", display: "block", fontSize: "0.75rem" }}>
                  {t("detail.postingDate")}:
                </span>
                <LocalDateDisplay value={payment.postingDate} />
              </div>

              {payment.bankReference && (
                <div>
                  <span style={{ color: "#64748b", display: "block", fontSize: "0.75rem" }}>
                    {t("detail.bankReference")}:
                  </span>
                  <span>{payment.bankReference}</span>
                </div>
              )}

              {payment.customerReference && (
                <div>
                  <span style={{ color: "#64748b", display: "block", fontSize: "0.75rem" }}>
                    {t("detail.customerReference")}:
                  </span>
                  <span>{payment.customerReference}</span>
                </div>
              )}

              {payment.supplierReference && (
                <div>
                  <span style={{ color: "#64748b", display: "block", fontSize: "0.75rem" }}>
                    {t("detail.supplierReference")}:
                  </span>
                  <span>{payment.supplierReference}</span>
                </div>
              )}
            </div>
          </Surface>

          {/* Section 2: Subledger Settlement & Available Balance */}
          <Surface title={t("detail.sectionSubledger")}>
            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "1rem", marginBottom: "1rem" }}>
              <div style={{ backgroundColor: "#f8fafc", padding: "1rem", borderRadius: "6px", border: "1px solid #e2e8f0" }}>
                <span style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("detail.subledgerStatus")}:</span>
                <div style={{ marginTop: "0.25rem" }}>
                  <ApplicationStatusBadge status="Unapplied" />
                </div>
              </div>

              <div style={{ backgroundColor: "#f8fafc", padding: "1rem", borderRadius: "6px", border: "1px solid #e2e8f0" }}>
                <span style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("detail.appliedAmount")}:</span>
                <div style={{ fontWeight: 700, fontSize: "1.125rem", color: "#0f172a", marginTop: "0.25rem" }}>
                  <MoneyDisplay value={{ amount: "0.00", currency: payment.currencyCode }} />
                </div>
              </div>

              <div style={{ backgroundColor: "#ecfdf5", padding: "1rem", borderRadius: "6px", border: "1px solid #a7f3d0" }}>
                <span style={{ fontSize: "0.75rem", color: "#065f46" }}>{t("detail.availableCredit")}:</span>
                <div style={{ fontWeight: 800, fontSize: "1.125rem", color: "#065f46", marginTop: "0.25rem" }}>
                  <MoneyDisplay value={{ amount: payment.amount, currency: payment.currencyCode }} />
                </div>
              </div>
            </div>

            <p style={{ margin: 0, fontSize: "0.8125rem", color: "#64748b" }}>
              {t("detail.subledgerNotice")}
            </p>
          </Surface>

          {/* Section 3: Fees & Financial Economics */}
          <Surface
            title={t("detail.sectionFees")}
            headerActions={
              perms.canOverrideFee ? (
                <Button variant="secondary" onPress={() => setIsAddFeeOpen(true)}>
                  + {t("detail.addFeeAction")}
                </Button>
              ) : undefined
            }
          >
            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "1rem", marginBottom: "1rem" }}>
              <div style={{ backgroundColor: "#f8fafc", padding: "1rem", borderRadius: "6px", border: "1px solid #e2e8f0" }}>
                <span style={{ fontSize: "0.75rem", color: "#64748b" }}>{t("detail.grossAmount")}:</span>
                <div style={{ fontWeight: 700, fontSize: "1.125rem", color: "#0f172a" }}>
                  <MoneyDisplay value={{ amount: payment.amount, currency: payment.currencyCode }} />
                </div>
              </div>

              <div style={{ backgroundColor: "#fef2f2", padding: "1rem", borderRadius: "6px", border: "1px solid #fecaca" }}>
                <span style={{ fontSize: "0.75rem", color: "#991b1b" }}>{t("detail.totalFees")}:</span>
                <div style={{ fontWeight: 700, fontSize: "1.125rem", color: "#991b1b" }}>
                  <MoneyDisplay value={{ amount: String(totalFees.toFixed(2)), currency: payment.currencyCode }} />
                </div>
              </div>

              <div style={{ backgroundColor: "#f0fdf4", padding: "1rem", borderRadius: "6px", border: "1px solid #bbf7d0" }}>
                <span style={{ fontSize: "0.75rem", color: "#166534" }}>{t("detail.netAmount")}:</span>
                <div style={{ fontWeight: 800, fontSize: "1.125rem", color: "#166534" }}>
                  <MoneyDisplay value={{ amount: String(netAmount.toFixed(2)), currency: payment.currencyCode }} />
                </div>
              </div>
            </div>

            {fees.length === 0 ? (
              <div style={{ fontSize: "0.8125rem", color: "#64748b" }}>{t("detail.noFeesRecorded")}</div>
            ) : (
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.8125rem" }}>
                <thead>
                  <tr style={{ backgroundColor: "#f1f5f9", textAlign: "start" }}>
                    <th style={{ padding: "0.5rem 0.75rem" }}>{t("detail.feeType")}</th>
                    <th style={{ padding: "0.5rem 0.75rem", textAlign: "end" }}>{t("detail.feeAmount")}</th>
                    <th style={{ padding: "0.5rem 0.75rem" }}>{t("detail.feeRecognition")}</th>
                  </tr>
                </thead>
                <tbody>
                  {fees.map((f) => (
                    <tr key={f.id} style={{ borderBottom: "1px solid #e2e8f0" }}>
                      <td style={{ padding: "0.5rem 0.75rem" }}>{getFeeTypeLabel(f.type)}</td>
                      <td style={{ padding: "0.5rem 0.75rem", textAlign: "end", fontWeight: 700 }}>
                        <MoneyDisplay value={{ amount: String(f.amount), currency: (f.currencyCode || payment.currencyCode) as any }} />
                      </td>
                      <td style={{ padding: "0.5rem 0.75rem" }}>{getFeeRecognitionLabel(f.recognitionState)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </Surface>

          {/* Section 4: Accounting & Ledger Projection */}
          <Surface title={t("detail.sectionAccounting")}>
            <div style={{ fontSize: "0.875rem", lineHeight: 1.6 }}>
              <div style={{ display: "flex", justifyContent: "space-between", marginBottom: "0.5rem" }}>
                <span style={{ color: "#64748b" }}>{t("detail.ledgerStatus")}:</span>
                <span style={{ fontWeight: 600 }}>
                  {payment.state === "Completed" ? t("detail.ledgerPosted") : t("detail.ledgerPending")}
                </span>
              </div>
              <div style={{ display: "flex", justifyContent: "space-between" }}>
                <span style={{ color: "#64748b" }}>{t("detail.ledgerFactRef")}:</span>
                <span style={{ fontFamily: "monospace" }}>
                  <bdi>{factRefText}</bdi>
                </span>
              </div>
            </div>
          </Surface>

          {/* Section 5: Reconciliation Projection */}
          <Surface title={t("detail.sectionReconciliation")}>
            <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <span style={{ fontSize: "0.875rem", color: "#64748b" }}>{t("detail.bankMatchState")}:</span>
              <ReconciliationStatusBadge status="Unmatched" />
            </div>
            <p style={{ margin: "0.75rem 0 0 0", fontSize: "0.8125rem", color: "#64748b" }}>
              {t("detail.reconciliationNotice")}
            </p>
          </Surface>
        </div>
      </RecordPage>

      {/* Modals */}
      <PaymentReceiptModal
        payment={payment}
        isOpen={isReceiptOpen}
        onClose={() => setIsReceiptOpen(false)}
        accountName={account?.name}
      />

      <RefundDialog
        payment={payment}
        isOpen={isRefundOpen}
        onOpenChange={setIsRefundOpen}
        legalEntityId={legalEntityId}
        branchId={branchId}
        accounts={accountsQuery.data || []}
      />

      <ReversalDialog
        payment={payment}
        isOpen={isReversalOpen}
        onOpenChange={setIsReversalOpen}
        legalEntityId={legalEntityId}
        branchId={branchId}
      />

      <ApplyPaymentDialog
        payment={payment}
        isOpen={isApplyOpen}
        onOpenChange={setIsApplyOpen}
        legalEntityId={legalEntityId}
        branchId={branchId}
      />

      {/* Add Fee Dialog */}
      <Dialog
        isOpen={isAddFeeOpen}
        onOpenChange={setIsAddFeeOpen}
        title={t("detail.addFeeDialogTitle")}
        size="small"
      >
        <form onSubmit={handleAddFeeSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
          <Select
            label={t("detail.feeTypeLabel")}
            selectedKey={feeType}
            onSelectionChange={(val) => setFeeType(String(val ?? "") as PaymentFeeType)}
            options={feeTypeOptions}
            isRequired
          />

          <TextField
            label={t("detail.feeAmountLabel")}
            value={feeAmount}
            onChange={setFeeAmount}
            placeholder="0.00"
            isRequired
            type="number"
          />

          <TextField
            label={t("detail.feeReferenceLabel")}
            value={feeRef}
            onChange={setFeeRef}
            placeholder={t("detail.feeReferencePlaceholder")}
          />

          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem" }}>
            <Button variant="secondary" onPress={() => setIsAddFeeOpen(false)}>
              {t("actions.cancel")}
            </Button>
            <Button variant="primary" type="submit" isDisabled={addFeeMutation.isPending}>
              {addFeeMutation.isPending ? t("detail.addingFee") : t("detail.addFeeSubmit")}
            </Button>
          </div>
        </form>
      </Dialog>

      {/* Manual Recovery Dialog */}
      <Dialog
        isOpen={isManualRecoveryOpen}
        onOpenChange={(open) => {
          if (!open) {
            setIsManualRecoveryOpen(false);
            setManualRecoveryError("");
          }
        }}
        title={t("detail.manualRecoveryConfirmTitle")}
        size="medium"
      >
        <form onSubmit={handleCompleteManual} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
          {manualRecoveryError && (
            <div
              style={{
                padding: "0.75rem 1rem",
                borderRadius: "6px",
                backgroundColor: "#fef2f2",
                border: "1px solid #fecaca",
                color: "#991b1b",
                fontSize: "0.875rem"
              }}
            >
              {manualRecoveryError}
            </div>
          )}

          <div
            style={{
              padding: "0.75rem 1rem",
              borderRadius: "6px",
              backgroundColor: "#fffbeb",
              border: "1px solid #fef08a",
              color: "#854d0e",
              fontSize: "0.875rem"
            }}
          >
            {t("detail.manualRecoveryWarning")}
          </div>

          <TextField
            label={t("detail.manualRecoveryEvidenceLabel")}
            placeholder={t("detail.manualRecoveryEvidencePlaceholder")}
            value={manualRecoveryEvidence}
            onChange={setManualRecoveryEvidence}
            isRequired
          />

          <div
            style={{
              display: "flex",
              justifyContent: "flex-end",
              gap: "0.75rem",
              marginTop: "1rem",
              borderTop: "1px solid #e2e8f0",
              paddingTop: "1rem"
            }}
          >
            <Button
              variant="secondary"
              onPress={() => {
                setIsManualRecoveryOpen(false);
                setManualRecoveryError("");
              }}
              isDisabled={completeManualMutation.isPending}
            >
              {t("actions.cancel")}
            </Button>
            <Button
              variant="primary"
              type="submit"
              isDisabled={completeManualMutation.isPending || !manualRecoveryEvidence.trim()}
            >
              {completeManualMutation.isPending
                ? t("detail.completing")
                : t("detail.manualRecoverySubmit")}
            </Button>
          </div>
        </form>
      </Dialog>
    </>
  );
}
