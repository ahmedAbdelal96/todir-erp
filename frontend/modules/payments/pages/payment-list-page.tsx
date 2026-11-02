import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate, useSearchParams } from "react-router-dom";
import { ListPage } from "../../../components/pages/list-page.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { Pagination } from "../../../components/primitives/pagination.tsx";
import { LoadingState } from "../../../components/primitives/loading-state.tsx";
import { EmptyState } from "../../../components/primitives/empty-state.tsx";
import { ErrorState } from "../../../components/primitives/error-state.tsx";
import { MoneyDisplay } from "../../../components/fields/money-display.tsx";
import { LocalDateDisplay } from "../../../components/fields/local-date-display.tsx";
import { FloatingBatchBar } from "../../../components/pages/floating-batch-bar.tsx";
import {
  PaymentStatusBadge,
  ApplicationStatusBadge,
  ProviderStatusBadge,
  ReconciliationStatusBadge
} from "../components/payment-status-badge.tsx";
import { PaymentDetailDrawer } from "../components/payment-detail-drawer.tsx";
import { PaymentReceiptModal } from "../components/payment-receipt-modal.tsx";
import { ReceivePaymentDialog } from "../features/receive-payment-dialog.tsx";
import { PaySupplierDialog } from "../features/pay-supplier-dialog.tsx";
import { InternalTransferDialog } from "../features/internal-transfer-dialog.tsx";
import { CreateAccountDialog } from "../features/create-account-dialog.tsx";
import { CreatePaymentRequestDialog } from "../features/create-payment-request-dialog.tsx";
import {
  usePaymentsScope,
  usePaymentsPermissions,
  usePaymentList,
  usePaymentAccounts,
  usePaymentRequests,
  useUpdatePaymentAccountStatus
} from "../hooks/payment-queries.ts";
import type {
  PaymentRecord,
  PaymentDirection,
  PaymentBusinessState,
  PaymentAccount,
  PaymentAccountType,
  PaymentAccountStatus,
  PaymentMethod,
  PaymentType
} from "../types.ts";

export function PaymentListPage(): React.JSX.Element {
  const { t } = useTranslation(["payments", "common"]);
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();

  const { legalEntityId, branchId } = usePaymentsScope();
  const perms = usePaymentsPermissions();

  // Tab State: "all" | "incoming" | "outgoing" | "transfers" | "accounts" | "requests"
  const activeTab = searchParams.get("tab") || "all";
  const setActiveTab = (tab: string) => {
    const next = new URLSearchParams(searchParams);
    next.set("tab", tab);
    next.set("page", "1");
    setSearchParams(next);
  };

  // Filter State
  const search = searchParams.get("search") || "";
  const directionFilter = (searchParams.get("direction") as PaymentDirection) || undefined;
  const stateFilter = (searchParams.get("state") as PaymentBusinessState) || undefined;
  const page = Number(searchParams.get("page") || "1");
  const pageSize = 25;

  // Dialog states
  const [isReceiveOpen, setIsReceiveOpen] = useState(false);
  const [isPayOpen, setIsPayOpen] = useState(false);
  const [isTransferOpen, setIsTransferOpen] = useState(false);
  const [isAccountCreateOpen, setIsAccountCreateOpen] = useState(false);
  const [isRequestCreateOpen, setIsRequestCreateOpen] = useState(false);
  const [receiptPayment, setReceiptPayment] = useState<PaymentRecord | null>(null);
  const [peekingPayment, setPeekingPayment] = useState<PaymentRecord | null>(null);

  // Selection state
  const [selectedPaymentIds, setSelectedPaymentIds] = useState<Set<string>>(new Set());

  // Determine direction from active tab if applicable
  const resolvedDirection: PaymentDirection | undefined =
    activeTab === "incoming"
      ? "Incoming"
      : activeTab === "outgoing"
      ? "Outgoing"
      : activeTab === "transfers"
      ? "InternalTransfer"
      : directionFilter;

  // Query payments
  const paymentsQuery = usePaymentList(
    {
      legalEntityId,
      branchId: branchId || undefined,
      page,
      pageSize,
      search: search || undefined,
      direction: resolvedDirection,
      state: stateFilter
    },
    activeTab !== "accounts" && activeTab !== "requests"
  );

  // Query accounts
  const accountsQuery = usePaymentAccounts(legalEntityId, branchId || undefined);

  // Query payment requests (provider intents)
  const requestsQuery = usePaymentRequests(
    {
      legalEntityId,
      page,
      pageSize,
      search: search || undefined
    },
    activeTab === "requests"
  );

  const updateAccountStatusMutation = useUpdatePaymentAccountStatus();

  const payments = paymentsQuery.data?.items || [];
  const accounts = accountsQuery.data || [];
  const requests = requestsQuery.data?.items || [];

  const handleSearchChange = (val: string) => {
    const next = new URLSearchParams(searchParams);
    if (val) next.set("search", val);
    else next.delete("search");
    next.set("page", "1");
    setSearchParams(next);
  };

  const handleStateFilterChange = (val: string) => {
    const next = new URLSearchParams(searchParams);
    if (val) next.set("state", val);
    else next.delete("state");
    next.set("page", "1");
    setSearchParams(next);
  };

  const handleBatchExport = () => {
    const selected = payments.filter((p) => selectedPaymentIds.has(p.id));
    const header = "Number,Direction,Type,Amount,Currency,Method,Account,Date,State\n";
    const rows = selected
      .map(
        (p) =>
          `"${p.number}","${p.direction}","${p.type}","${p.amount}","${p.currencyCode}","${p.method}","${p.paymentAccountId}","${p.paymentDate}","${p.state}"`
      )
      .join("\n");
    const blob = new Blob([header + rows], { type: "text/csv;charset=utf-8;" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.setAttribute("download", `payments-export-${new Date().toISOString().slice(0, 10)}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  const getAccountTypeLabel = (tpe: PaymentAccountType) => {
    switch (tpe) {
      case "Bank": return t("accountType.bank");
      case "Cashbox": return t("accountType.cashbox");
      case "ProviderClearing": return t("accountType.providerclearing");
      case "Wallet": return t("accountType.wallet");
      case "InternalClearing":
      default: return t("accountType.internalclearing");
    }
  };

  const getAccountStatusLabel = (st: PaymentAccountStatus) => {
    switch (st) {
      case "Active": return t("accountStatus.active");
      case "Suspended": return t("accountStatus.suspended");
      case "Archived":
      default: return t("accountStatus.archived");
    }
  };

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

  const stateOptions = [
    { id: "", label: t("list.filterAllStates") },
    { id: "Completed", label: t("state.completed") },
    { id: "Pending", label: t("state.pending") },
    { id: "Draft", label: t("state.draft") },
    { id: "Failed", label: t("state.failed") },
    { id: "Cancelled", label: t("state.cancelled") },
    { id: "Reversed", label: t("state.reversed") }
  ];

  const tabs = [
    { id: "all", label: t("tabs.allPayments") },
    { id: "incoming", label: t("tabs.incoming") },
    { id: "outgoing", label: t("tabs.outgoing") },
    { id: "transfers", label: t("tabs.transfers") },
    { id: "accounts", label: t("tabs.accounts") },
    { id: "requests", label: t("tabs.requests") }
  ];

  const helpContent = {
    title: t("help.title"),
    summary: t("help.description"),
    keyConcepts: [
      { term: t("help.separationTitle"), explanation: t("help.separationContent") },
      { term: t("help.receiveTitle"), explanation: t("help.receiveContent") },
      { term: t("help.refundReversalTitle"), explanation: t("help.refundReversalContent") }
    ]
  };

  const primaryActions = (
    <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap", alignItems: "center" }}>
      {activeTab === "accounts" ? (
        perms.canManageAccounts && (
          <Button variant="primary" onPress={() => setIsAccountCreateOpen(true)}>
            + {t("accounts.newAccountAction")}
          </Button>
        )
      ) : activeTab === "requests" ? (
        perms.canCreateIncoming && (
          <Button variant="primary" onPress={() => setIsRequestCreateOpen(true)}>
            + {t("requests.newRequestAction")}
          </Button>
        )
      ) : (
        <>
          {perms.canCreateIncoming && (
            <Button variant="primary" onPress={() => setIsReceiveOpen(true)}>
              + {t("actions.receivePayment")}
            </Button>
          )}
          {perms.canCreateOutgoing && (
            <Button variant="secondary" onPress={() => setIsPayOpen(true)}>
              + {t("actions.paySupplier")}
            </Button>
          )}
          {perms.canCreateTransfer && (
            <Button variant="secondary" onPress={() => setIsTransferOpen(true)}>
              + {t("actions.internalTransfer")}
            </Button>
          )}
        </>
      )}
    </div>
  );

  return (
    <>
      <ListPage
        title={t("title")}
        subtitle={t("subtitle")}
        breadcrumbs={[
          { label: t("actions.home"), href: "/" },
          { label: t("title") }
        ]}
        secondaryActions={primaryActions}
        help={helpContent}
        pagination={
          activeTab === "requests" ? (
            <Pagination
              page={page}
              totalItems={requestsQuery.data?.totalCount ?? 0}
              totalPages={Math.max(1, Math.ceil((requestsQuery.data?.totalCount ?? 0) / pageSize))}
              pageSize={pageSize}
              onPageChange={(nextPage) => {
                const next = new URLSearchParams(searchParams);
                next.set("page", String(nextPage));
                setSearchParams(next);
              }}
              isDisabled={requestsQuery.isLoading}
            />
          ) : activeTab !== "accounts" ? (
            <Pagination
              page={page}
              totalItems={paymentsQuery.data?.totalCount ?? 0}
              totalPages={Math.max(1, Math.ceil((paymentsQuery.data?.totalCount ?? 0) / pageSize))}
              pageSize={pageSize}
              onPageChange={(nextPage) => {
                const next = new URLSearchParams(searchParams);
                next.set("page", String(nextPage));
                setSearchParams(next);
              }}
              isDisabled={paymentsQuery.isLoading}
            />
          ) : undefined
        }
      >
        {/* Navigation Tabs */}
        <div
          style={{
            display: "flex",
            gap: "0.5rem",
            borderBottom: "1px solid #cbd5e1",
            paddingBottom: "0.5rem",
            marginBottom: "1rem",
            overflowX: "auto"
          }}
        >
          {tabs.map((tab) => {
            const isActive = activeTab === tab.id;
            return (
              <button
                key={tab.id}
                type="button"
                onClick={() => setActiveTab(tab.id)}
                style={{
                  padding: "0.5rem 1rem",
                  fontSize: "0.875rem",
                  fontWeight: isActive ? 700 : 500,
                  color: isActive ? "#102a43" : "#64748b",
                  backgroundColor: isActive ? "#ffffff" : "transparent",
                  border: isActive ? "1px solid #cbd5e1" : "1px solid transparent",
                  borderBottom: isActive ? "2px solid #2563eb" : "1px solid transparent",
                  borderRadius: "6px 6px 0 0",
                  cursor: "pointer",
                  whiteSpace: "nowrap"
                }}
              >
                {tab.label}
              </button>
            );
          })}
        </div>

        {/* Filter Bar */}
        <div
          style={{
            display: "flex",
            gap: "0.75rem",
            alignItems: "center",
            flexWrap: "wrap",
            marginBottom: "1rem",
            backgroundColor: "#f8fafc",
            border: "1px solid #e2e8f0",
            borderRadius: "6px",
            padding: "0.75rem"
          }}
        >
          <div style={{ flex: 1, minWidth: "200px" }}>
            <TextField
              aria-label={t("list.searchPlaceholder")}
              value={search}
              onChange={handleSearchChange}
              placeholder={t("list.searchPlaceholder")}
            />
          </div>

          {activeTab !== "accounts" && activeTab !== "requests" && (
            <div style={{ minWidth: "160px" }}>
              <Select
                aria-label={t("list.filterStateLabel")}
                selectedKey={stateFilter || ""}
                onSelectionChange={(val) => handleStateFilterChange(String(val ?? ""))}
                options={stateOptions}
              />
            </div>
          )}
        </div>

        {/* Content by active tab */}
        {activeTab === "accounts" ? (
          /* Payment Accounts Surface */
          accountsQuery.isLoading ? (
            <LoadingState message={t("accounts.loading")} />
          ) : accountsQuery.isError ? (
            <ErrorState title={t("accounts.errorTitle")} message={t("accounts.errorMessage")} />
          ) : accounts.length === 0 ? (
            <EmptyState
              title={t("accounts.emptyTitle")}
              description={t("accounts.emptyDesc")}
              action={
                perms.canManageAccounts ? (
                  <Button variant="primary" onPress={() => setIsAccountCreateOpen(true)}>
                    + {t("accounts.newAccountAction")}
                  </Button>
                ) : undefined
              }
            />
          ) : (
            <div style={{ overflowX: "auto", border: "1px solid #cbd5e1", borderRadius: "6px" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.8125rem", textAlign: "start" }}>
                <thead>
                  <tr style={{ backgroundColor: "#102a43", color: "#ffffff", borderBottom: "1px solid #0c1f31" }}>
                    <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("accounts.columnName")}</th>
                    <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("accounts.columnCode")}</th>
                    <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("accounts.columnType")}</th>
                    <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("accounts.columnCurrency")}</th>
                    <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("accounts.columnIdentifier")}</th>
                    <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("accounts.columnStatus")}</th>
                    <th style={{ padding: "0.75rem 1rem", textAlign: "center" }}>{t("accounts.columnActions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {accounts.map((acc, index) => (
                    <tr
                      key={acc.id}
                      style={{
                        backgroundColor: index % 2 === 0 ? "#ffffff" : "#f8fafc",
                        borderBottom: "1px solid #e2e8f0"
                      }}
                    >
                      <td style={{ padding: "0.75rem 1rem", fontWeight: 700, color: "#102a43" }}>{acc.name}</td>
                      <td style={{ padding: "0.75rem 1rem" }}><bdi>{acc.code}</bdi></td>
                      <td style={{ padding: "0.75rem 1rem" }}>{getAccountTypeLabel(acc.type)}</td>
                      <td style={{ padding: "0.75rem 1rem" }}>{acc.currencyCode || acc.currencyPolicy}</td>
                      <td style={{ padding: "0.75rem 1rem" }}>{acc.maskedIdentifier || "—"}</td>
                      <td style={{ padding: "0.75rem 1rem" }}>
                        <span
                          style={{
                            padding: "0.125rem 0.5rem",
                            borderRadius: "4px",
                            fontSize: "0.75rem",
                            fontWeight: 600,
                            backgroundColor: acc.status === "Active" ? "#ecfdf5" : "#fef2f2",
                            color: acc.status === "Active" ? "#065f46" : "#991b1b"
                          }}
                        >
                          {getAccountStatusLabel(acc.status)}
                        </span>
                      </td>
                      <td style={{ padding: "0.75rem 1rem", textAlign: "center" }}>
                        {perms.canManageAccounts && (
                          <Button
                            variant="secondary"
                            onPress={() => {
                              const newStatus = acc.status === "Active" ? "Suspended" : "Active";
                              updateAccountStatusMutation.mutate({
                                legalEntityId,
                                accountId: acc.id,
                                request: { status: newStatus }
                              });
                            }}
                          >
                            {acc.status === "Active" ? t("accounts.suspendAction") : t("accounts.activateAction")}
                          </Button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )
        ) : activeTab === "requests" ? (
          /* Payment Requests Surface */
          requestsQuery.isLoading ? (
            <LoadingState message={t("requests.loading")} />
          ) : requestsQuery.isError ? (
            <ErrorState title={t("requests.errorTitle")} message={t("requests.errorMessage")} />
          ) : requests.length === 0 ? (
            <EmptyState
              title={t("requests.emptyTitle")}
              description={t("requests.emptyDesc")}
              action={
                perms.canCreateIncoming ? (
                  <Button variant="primary" onPress={() => setIsRequestCreateOpen(true)}>
                    + {t("requests.newRequestAction")}
                  </Button>
                ) : undefined
              }
            />
          ) : (
            <div style={{ overflowX: "auto", border: "1px solid #cbd5e1", borderRadius: "6px" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.8125rem", textAlign: "start" }}>
                <thead>
                  <tr style={{ backgroundColor: "#102a43", color: "#ffffff", borderBottom: "1px solid #0c1f31" }}>
                    <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("requests.columnReference")}</th>
                    <th style={{ padding: "0.75rem 1rem", textAlign: "end" }}>{t("requests.columnAmount")}</th>
                    <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("requests.columnCurrency")}</th>
                    <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("requests.columnState")}</th>
                    <th style={{ padding: "0.75rem 1rem", textAlign: "start" }}>{t("requests.columnAttempt")}</th>
                  </tr>
                </thead>
                <tbody>
                  {requests.map((req, index) => (
                    <tr
                      key={req.id}
                      style={{
                        backgroundColor: index % 2 === 0 ? "#ffffff" : "#f8fafc",
                        borderBottom: "1px solid #e2e8f0"
                      }}
                    >
                      <td style={{ padding: "0.75rem 1rem", fontWeight: 700 }}><bdi>{req.merchantReference}</bdi></td>
                      <td style={{ padding: "0.75rem 1rem", textAlign: "end", fontWeight: 700 }}>
                        <MoneyDisplay value={{ amount: String(req.amount), currency: req.currencyCode as any }} />
                      </td>
                      <td style={{ padding: "0.75rem 1rem" }}>{req.currencyCode}</td>
                      <td style={{ padding: "0.75rem 1rem" }}>
                        <ProviderStatusBadge state={req.state} />
                      </td>
                      <td style={{ padding: "0.75rem 1rem" }}>#{req.attemptNumber}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )
        ) : (
          /* Normal Payments List */
          paymentsQuery.isLoading ? (
            <LoadingState message={t("list.loading")} />
          ) : paymentsQuery.isError ? (
            <ErrorState title={t("list.errorTitle")} message={t("list.errorMessage")} />
          ) : payments.length === 0 ? (
            <EmptyState
              title={t("list.emptyTitle")}
              description={t("list.emptyDesc")}
              action={
                perms.canCreateIncoming ? (
                  <Button variant="primary" onPress={() => setIsReceiveOpen(true)}>
                    + {t("actions.receivePayment")}
                  </Button>
                ) : undefined
              }
            />
          ) : (
            <div style={{ overflowX: "auto", border: "1px solid #cbd5e1", borderRadius: "6px", width: "100%" }}>
              <table style={{ width: "100%", minWidth: "960px", borderCollapse: "collapse", fontSize: "0.8125rem", textAlign: "start" }}>
                <thead>
                  <tr style={{ backgroundColor: "#102a43", color: "#ffffff", borderBottom: "1px solid #0c1f31" }}>
                    <th style={{ width: "36px", padding: "0.625rem 0.5rem", textAlign: "center" }}>
                      <input
                        type="checkbox"
                        checked={payments.length > 0 && selectedPaymentIds.size === payments.length}
                        onChange={(e) => {
                          if (e.target.checked) setSelectedPaymentIds(new Set(payments.map((p) => p.id)));
                          else setSelectedPaymentIds(new Set());
                        }}
                        aria-label={(t as any)("common:grid.selectAll")}
                      />
                    </th>
                    <th style={{ padding: "0.625rem 0.75rem", textAlign: "start" }}>{t("list.columnNumber")}</th>
                    <th style={{ padding: "0.625rem 0.75rem", textAlign: "start" }}>{t("list.columnDirection")}</th>
                    <th style={{ padding: "0.625rem 0.75rem", textAlign: "end" }}>{t("list.columnAmount")}</th>
                    <th style={{ padding: "0.625rem 0.75rem", textAlign: "start" }}>{t("list.columnMethod")}</th>
                    <th style={{ padding: "0.625rem 0.75rem", textAlign: "start" }}>{t("list.columnAccount")}</th>
                    <th style={{ padding: "0.625rem 0.75rem", textAlign: "start" }}>{t("list.columnDate")}</th>
                    <th style={{ padding: "0.625rem 0.75rem", textAlign: "start" }}>{t("list.columnState")}</th>
                    <th style={{ padding: "0.625rem 0.75rem", textAlign: "start" }}>{t("list.columnApplication")}</th>
                    <th style={{ padding: "0.625rem 0.75rem", textAlign: "start" }}>{t("list.columnReconciliation")}</th>
                    <th
                      style={{
                        padding: "0.625rem 0.75rem",
                        textAlign: "center",
                        position: "sticky",
                        left: 0,
                        zIndex: 3,
                        backgroundColor: "#102a43"
                      }}
                    >
                      {t("list.columnActions")}
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {payments.map((payment, index) => {
                    const account = accounts.find((a) => a.id === payment.paymentAccountId);
                    return (
                      <tr
                        key={payment.id}
                        style={{
                          backgroundColor: index % 2 === 0 ? "#ffffff" : "#f8fafc",
                          borderBottom: "1px solid #e2e8f0",
                          cursor: "pointer"
                        }}
                        onClick={() => navigate(`/payments/${payment.id}`)}
                      >
                        {/* Checkbox */}
                        <td style={{ width: "36px", padding: "0.625rem 0.5rem", textAlign: "center" }} onClick={(e) => e.stopPropagation()}>
                          <input
                            type="checkbox"
                            checked={selectedPaymentIds.has(payment.id)}
                            onChange={() => {
                              setSelectedPaymentIds((prev) => {
                                const next = new Set(prev);
                                if (next.has(payment.id)) next.delete(payment.id);
                                else next.add(payment.id);
                                return next;
                              });
                            }}
                            aria-label={payment.number}
                          />
                        </td>

                        {/* Payment Number */}
                        <td style={{ padding: "0.625rem 0.75rem", fontWeight: 700, color: "#2563eb", whiteSpace: "nowrap" }}>
                          <bdi>{payment.number}</bdi>
                        </td>

                        {/* Direction / Type */}
                        <td style={{ padding: "0.625rem 0.75rem", whiteSpace: "nowrap" }}>
                          <span style={{ fontWeight: 600 }}>{getDirectionLabel(payment.direction)}</span>
                          <span style={{ display: "block", fontSize: "0.6875rem", color: "#64748b" }}>
                            {getTypeLabel(payment.type)}
                          </span>
                        </td>

                        {/* Amount */}
                        <td style={{ padding: "0.625rem 0.75rem", textAlign: "end", fontWeight: 700, whiteSpace: "nowrap" }}>
                          <MoneyDisplay value={{ amount: payment.amount, currency: payment.currencyCode }} />
                        </td>

                        {/* Method */}
                        <td style={{ padding: "0.625rem 0.75rem", whiteSpace: "nowrap" }}>{getMethodLabel(payment.method)}</td>

                        {/* Account */}
                        <td style={{ padding: "0.625rem 0.75rem", whiteSpace: "nowrap" }}>
                          {account?.name || account?.code || t("detail.operationalAccountDefault")}
                        </td>

                        {/* Date */}
                        <td style={{ padding: "0.625rem 0.75rem", whiteSpace: "nowrap" }}>
                          <LocalDateDisplay value={payment.paymentDate} />
                        </td>

                        {/* Business State */}
                        <td style={{ padding: "0.625rem 0.75rem", whiteSpace: "nowrap" }}>
                          <PaymentStatusBadge state={payment.state} />
                        </td>

                        {/* Subledger Application */}
                        <td style={{ padding: "0.625rem 0.75rem", whiteSpace: "nowrap" }}>
                          <ApplicationStatusBadge status="Unapplied" />
                        </td>

                        {/* Reconciliation */}
                        <td style={{ padding: "0.625rem 0.75rem", whiteSpace: "nowrap" }}>
                          <ReconciliationStatusBadge status="Unmatched" />
                        </td>

                        {/* Actions */}
                        <td
                          style={{
                            padding: "0.625rem 0.75rem",
                            textAlign: "center",
                            position: "sticky",
                            left: 0,
                            zIndex: 2,
                            backgroundColor: index % 2 === 0 ? "#ffffff" : "#f8fafc",
                            boxShadow: "2px 0 4px -2px rgba(0,0,0,0.1)",
                            whiteSpace: "nowrap"
                          }}
                          onClick={(e) => e.stopPropagation()}
                        >
                          <div style={{ display: "flex", gap: "0.25rem", justifyContent: "center" }}>
                            <Button
                              variant="secondary"
                              onPress={() => setPeekingPayment(payment)}
                            >
                              {t("list.quickPeekAction")}
                            </Button>
                            {payment.state === "Completed" && (
                              <Button
                                variant="secondary"
                                onPress={() => setReceiptPayment(payment)}
                              >
                                {t("list.printVoucherAction")}
                              </Button>
                            )}
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )
        )}
      </ListPage>

      {/* Floating Batch Bar */}
      <FloatingBatchBar
        selectedCount={selectedPaymentIds.size}
        onClearSelection={() => setSelectedPaymentIds(new Set())}
        actions={[
          {
            id: "export",
            label: t("list.exportBatch"),
            variant: "primary",
            onExecute: handleBatchExport
          }
        ]}
      />

      {/* Quick Peek Drawer */}
      <PaymentDetailDrawer
        payment={peekingPayment}
        isOpen={Boolean(peekingPayment)}
        onClose={() => setPeekingPayment(null)}
        onOpenFull={(p) => navigate(`/payments/${p.id}`)}
      />

      {/* Printable Receipt Modal */}
      <PaymentReceiptModal
        payment={receiptPayment}
        isOpen={Boolean(receiptPayment)}
        onClose={() => setReceiptPayment(null)}
        accountName={accounts.find((a) => a.id === receiptPayment?.paymentAccountId)?.name}
      />

      {/* Creation Workflows */}
      <ReceivePaymentDialog
        isOpen={isReceiveOpen}
        onOpenChange={setIsReceiveOpen}
        legalEntityId={legalEntityId}
        branchId={branchId}
        accounts={accounts}
      />

      <PaySupplierDialog
        isOpen={isPayOpen}
        onOpenChange={setIsPayOpen}
        legalEntityId={legalEntityId}
        branchId={branchId}
        accounts={accounts}
      />

      <InternalTransferDialog
        isOpen={isTransferOpen}
        onOpenChange={setIsTransferOpen}
        legalEntityId={legalEntityId}
        accounts={accounts}
      />

      <CreateAccountDialog
        isOpen={isAccountCreateOpen}
        onOpenChange={setIsAccountCreateOpen}
        legalEntityId={legalEntityId}
        branchId={branchId}
      />

      <CreatePaymentRequestDialog
        isOpen={isRequestCreateOpen}
        onOpenChange={setIsRequestCreateOpen}
        legalEntityId={legalEntityId}
        branchId={branchId}
      />
    </>
  );
}
