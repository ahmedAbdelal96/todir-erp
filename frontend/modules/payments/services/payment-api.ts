import { httpClient, generateKey } from "../../../core/api/http-client.ts";
import type {
  PaymentRecord,
  PaymentPage,
  PaymentListQuery,
  PaymentAccount,
  CreatePaymentRequest,
  CreateInternalTransferRequest,
  InternalTransferResult,
  CreateRefundRequest,
  ProviderRefundResult,
  ReversePaymentRequest,
  CreatePaymentFeeRequest,
  PaymentFeeResult,
  ApplyPaymentRequest,
  CreatePaymentIntentRequest,
  PaymentIntentResult,
  PaymentRequestDetails,
  PaymentRequestListQuery,
  PaymentRequestPage,
  CreatePaymentLinkRequest,
  PaymentLinkResult,
  PublicPaymentLinkDetails,
  CreatePaymentProviderRequest,
  PaymentProviderResult,
  CreatePaymentAccountRequest,
  UpdatePaymentAccountStatusRequest,
  PaymentCommand,
  PaymentResult,
  PaymentStatusCheck
} from "../types.ts";

export const paymentApi = {
  // Accounts
  listAccounts: async (legalEntityId: string, branchId?: string): Promise<readonly PaymentAccount[]> => {
    const searchParams = new URLSearchParams();
    if (branchId) searchParams.set("branchId", branchId);
    const query = searchParams.toString() ? `?${searchParams.toString()}` : "";
    return httpClient<readonly PaymentAccount[]>(`/api/v1/payments/legal-entities/${legalEntityId}/accounts${query}`);
  },

  createAccount: async (legalEntityId: string, request: CreatePaymentAccountRequest): Promise<PaymentAccount> => {
    return httpClient<PaymentAccount>(`/api/v1/payments/legal-entities/${legalEntityId}/accounts`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  updateAccountStatus: async (
    legalEntityId: string,
    accountId: string,
    request: UpdatePaymentAccountStatusRequest
  ): Promise<void> => {
    await httpClient<void>(`/api/v1/payments/legal-entities/${legalEntityId}/accounts/${accountId}/status`, {
      method: "PUT",
      body: JSON.stringify(request)
    });
  },

  // Payments List & Read
  listPayments: async (params: PaymentListQuery): Promise<PaymentPage> => {
    const searchParams = new URLSearchParams();
    if (params.page !== undefined) searchParams.set("page", String(params.page));
    if (params.pageSize !== undefined) searchParams.set("pageSize", String(params.pageSize));
    if (params.search) searchParams.set("search", params.search);
    if (params.direction) searchParams.set("direction", params.direction);
    if (params.state) searchParams.set("state", params.state);
    if (params.fromDate) searchParams.set("fromDate", params.fromDate);
    if (params.toDate) searchParams.set("toDate", params.toDate);
    if (params.branchId) searchParams.set("branchId", params.branchId);
    if (params.paymentAccountId) searchParams.set("paymentAccountId", params.paymentAccountId);
    if (params.currencyCode) searchParams.set("currencyCode", params.currencyCode);

    const query = searchParams.toString() ? `?${searchParams.toString()}` : "";
    return httpClient<PaymentPage>(`/api/v1/payments/legal-entities/${params.legalEntityId}${query}`);
  },

  getPayment: async (legalEntityId: string, paymentId: string, branchId?: string): Promise<PaymentRecord> => {
    const searchParams = new URLSearchParams();
    if (branchId) searchParams.set("branchId", branchId);
    const query = searchParams.toString() ? `?${searchParams.toString()}` : "";
    return httpClient<PaymentRecord>(`/api/v1/payments/legal-entities/${legalEntityId}/${paymentId}${query}`);
  },

  // Create Incoming Payment
  createIncomingPayment: async (
    legalEntityId: string,
    request: CreatePaymentRequest,
    idempotencyKey?: string
  ): Promise<PaymentRecord> => {
    const key = idempotencyKey ?? generateKey();
    return httpClient<PaymentRecord>(`/api/v1/payments/legal-entities/${legalEntityId}/incoming`, {
      method: "POST",
      headers: { "Idempotency-Key": key },
      idempotencyKey: key,
      body: JSON.stringify(request)
    });
  },

  // Create Outgoing Payment
  createOutgoingPayment: async (
    legalEntityId: string,
    request: CreatePaymentRequest,
    idempotencyKey?: string
  ): Promise<PaymentRecord> => {
    const key = idempotencyKey ?? generateKey();
    return httpClient<PaymentRecord>(`/api/v1/payments/legal-entities/${legalEntityId}/outgoing`, {
      method: "POST",
      headers: { "Idempotency-Key": key },
      idempotencyKey: key,
      body: JSON.stringify(request)
    });
  },

  // Complete Manual Payment
  completeManualPayment: async (
    legalEntityId: string,
    paymentId: string,
    branchId?: string,
    idempotencyKey?: string
  ): Promise<PaymentRecord> => {
    const key = idempotencyKey ?? generateKey();
    const searchParams = new URLSearchParams();
    if (branchId) searchParams.set("branchId", branchId);
    const query = searchParams.toString() ? `?${searchParams.toString()}` : "";

    return httpClient<PaymentRecord>(`/api/v1/payments/legal-entities/${legalEntityId}/${paymentId}/complete${query}`, {
      method: "POST",
      headers: { "Idempotency-Key": key },
      idempotencyKey: key
    });
  },

  // Cancel Pending Payment
  cancelPendingPayment: async (
    legalEntityId: string,
    paymentId: string,
    branchId?: string,
    idempotencyKey?: string
  ): Promise<PaymentRecord> => {
    const key = idempotencyKey ?? generateKey();
    const searchParams = new URLSearchParams();
    if (branchId) searchParams.set("branchId", branchId);
    const query = searchParams.toString() ? `?${searchParams.toString()}` : "";

    return httpClient<PaymentRecord>(`/api/v1/payments/legal-entities/${legalEntityId}/${paymentId}/cancel${query}`, {
      method: "POST",
      headers: { "Idempotency-Key": key },
      idempotencyKey: key
    });
  },

  // Refunds
  createRefund: async (
    legalEntityId: string,
    paymentId: string,
    request: CreateRefundRequest,
    branchId?: string,
    idempotencyKey?: string
  ): Promise<PaymentRecord> => {
    const key = idempotencyKey ?? generateKey();
    const searchParams = new URLSearchParams();
    if (branchId) searchParams.set("branchId", branchId);
    const query = searchParams.toString() ? `?${searchParams.toString()}` : "";

    return httpClient<PaymentRecord>(`/api/v1/payments/legal-entities/${legalEntityId}/${paymentId}/refunds${query}`, {
      method: "POST",
      headers: { "Idempotency-Key": key },
      idempotencyKey: key,
      body: JSON.stringify(request)
    });
  },

  createProviderRefund: async (
    legalEntityId: string,
    paymentId: string,
    request: CreateRefundRequest,
    branchId?: string,
    idempotencyKey?: string
  ): Promise<ProviderRefundResult> => {
    const key = idempotencyKey ?? generateKey();
    const searchParams = new URLSearchParams();
    if (branchId) searchParams.set("branchId", branchId);
    const query = searchParams.toString() ? `?${searchParams.toString()}` : "";

    return httpClient<ProviderRefundResult>(`/api/v1/payments/legal-entities/${legalEntityId}/${paymentId}/provider-refunds${query}`, {
      method: "POST",
      headers: { "Idempotency-Key": key },
      idempotencyKey: key,
      body: JSON.stringify(request)
    });
  },

  syncProviderRefundStatus: async (
    legalEntityId: string,
    refundPaymentId: string
  ): Promise<ProviderRefundResult> => {
    return httpClient<ProviderRefundResult>(`/api/v1/payments/legal-entities/${legalEntityId}/${refundPaymentId}/provider-refund-status`, {
      method: "POST"
    });
  },

  // Reversal
  reversePayment: async (
    legalEntityId: string,
    paymentId: string,
    request: ReversePaymentRequest,
    branchId?: string,
    idempotencyKey?: string
  ): Promise<PaymentRecord> => {
    const key = idempotencyKey ?? generateKey();
    const searchParams = new URLSearchParams();
    if (branchId) searchParams.set("branchId", branchId);
    const query = searchParams.toString() ? `?${searchParams.toString()}` : "";

    return httpClient<PaymentRecord>(`/api/v1/payments/legal-entities/${legalEntityId}/${paymentId}/reverse${query}`, {
      method: "POST",
      headers: { "Idempotency-Key": key },
      idempotencyKey: key,
      body: JSON.stringify(request)
    });
  },

  // Fees
  listFees: async (legalEntityId: string, paymentId: string, branchId?: string): Promise<readonly PaymentFeeResult[]> => {
    const searchParams = new URLSearchParams();
    if (branchId) searchParams.set("branchId", branchId);
    const query = searchParams.toString() ? `?${searchParams.toString()}` : "";
    return httpClient<readonly PaymentFeeResult[]>(`/api/v1/payments/legal-entities/${legalEntityId}/${paymentId}/fees${query}`);
  },

  addFee: async (
    legalEntityId: string,
    paymentId: string,
    request: CreatePaymentFeeRequest,
    idempotencyKey?: string
  ): Promise<PaymentFeeResult> => {
    const key = idempotencyKey ?? generateKey();
    return httpClient<PaymentFeeResult>(`/api/v1/payments/legal-entities/${legalEntityId}/${paymentId}/fees`, {
      method: "POST",
      headers: { "Idempotency-Key": key },
      idempotencyKey: key,
      body: JSON.stringify(request)
    });
  },

  // Subledger Application
  applyPayment: async (
    legalEntityId: string,
    paymentId: string,
    request: ApplyPaymentRequest,
    branchId?: string,
    idempotencyKey?: string
  ): Promise<unknown> => {
    const key = idempotencyKey ?? generateKey();
    const searchParams = new URLSearchParams();
    if (branchId) searchParams.set("branchId", branchId);
    const query = searchParams.toString() ? `?${searchParams.toString()}` : "";

    return httpClient<unknown>(`/api/v1/payments/legal-entities/${legalEntityId}/${paymentId}/applications${query}`, {
      method: "POST",
      headers: { "Idempotency-Key": key },
      idempotencyKey: key,
      body: JSON.stringify(request)
    });
  },

  // Internal Transfers
  createInternalTransfer: async (
    legalEntityId: string,
    request: CreateInternalTransferRequest,
    idempotencyKey?: string
  ): Promise<InternalTransferResult> => {
    const key = idempotencyKey ?? generateKey();
    return httpClient<InternalTransferResult>(`/api/v1/payments/legal-entities/${legalEntityId}/internal-transfers`, {
      method: "POST",
      headers: { "Idempotency-Key": key },
      idempotencyKey: key,
      body: JSON.stringify(request)
    });
  },

  // Payment Requests / Intents
  createPaymentRequest: async (
    legalEntityId: string,
    request: CreatePaymentIntentRequest,
    idempotencyKey?: string
  ): Promise<PaymentIntentResult> => {
    const key = idempotencyKey ?? generateKey();
    return httpClient<PaymentIntentResult>(`/api/v1/payments/legal-entities/${legalEntityId}/payment-requests`, {
      method: "POST",
      headers: { "Idempotency-Key": key },
      idempotencyKey: key,
      body: JSON.stringify(request)
    });
  },

  listPaymentRequests: async (params: PaymentRequestListQuery): Promise<PaymentRequestPage> => {
    const searchParams = new URLSearchParams();
    if (params.page !== undefined) searchParams.set("page", String(params.page));
    if (params.pageSize !== undefined) searchParams.set("pageSize", String(params.pageSize));
    if (params.search) searchParams.set("search", params.search);
    if (params.state) searchParams.set("state", params.state);
    const query = searchParams.toString() ? `?${searchParams.toString()}` : "";
    return httpClient<PaymentRequestPage>(`/api/v1/payments/legal-entities/${params.legalEntityId}/payment-requests${query}`);
  },

  getPaymentRequestDetails: async (legalEntityId: string, requestId: string): Promise<PaymentRequestDetails> => {
    return httpClient<PaymentRequestDetails>(`/api/v1/payments/legal-entities/${legalEntityId}/payment-requests/${requestId}`);
  },

  retryPaymentRequest: async (
    legalEntityId: string,
    requestId: string,
    idempotencyKey?: string
  ): Promise<PaymentIntentResult> => {
    const key = idempotencyKey ?? generateKey();
    return httpClient<PaymentIntentResult>(`/api/v1/payments/legal-entities/${legalEntityId}/payment-requests/${requestId}/attempts`, {
      method: "POST",
      headers: { "Idempotency-Key": key },
      idempotencyKey: key
    });
  },

  syncPaymentRequestStatus: async (legalEntityId: string, requestId: string): Promise<PaymentIntentResult> => {
    return httpClient<PaymentIntentResult>(`/api/v1/payments/legal-entities/${legalEntityId}/payment-requests/${requestId}/status-sync`, {
      method: "POST"
    });
  },

  cancelPaymentRequest: async (legalEntityId: string, requestId: string): Promise<PaymentIntentResult> => {
    return httpClient<PaymentIntentResult>(`/api/v1/payments/legal-entities/${legalEntityId}/payment-requests/${requestId}/cancel`, {
      method: "POST"
    });
  },

  // Payment Links
  createPaymentLink: async (
    legalEntityId: string,
    requestId: string,
    request: CreatePaymentLinkRequest
  ): Promise<PaymentLinkResult> => {
    return httpClient<PaymentLinkResult>(`/api/v1/payments/legal-entities/${legalEntityId}/payment-requests/${requestId}/links`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  cancelPaymentLink: async (legalEntityId: string, requestId: string, linkId: string): Promise<void> => {
    await httpClient<void>(`/api/v1/payments/legal-entities/${legalEntityId}/payment-requests/${requestId}/links/${linkId}`, {
      method: "DELETE"
    });
  },

  getPublicPaymentLink: async (token: string): Promise<PublicPaymentLinkDetails> => {
    return httpClient<PublicPaymentLinkDetails>(`/api/v1/payments/public-links/${token}`);
  },

  // Provider Config
  createProviderConfig: async (
    legalEntityId: string,
    request: CreatePaymentProviderRequest
  ): Promise<PaymentProviderResult> => {
    return httpClient<PaymentProviderResult>(`/api/v1/payments/legal-entities/${legalEntityId}/providers`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  // Backward-compatible spike methods
  submitPayment: async (
    command: PaymentCommand,
    idempotencyKey: string,
    tenantId?: string
  ): Promise<PaymentResult> => {
    return httpClient<PaymentResult>("/api/v1/payments/dispatch", {
      method: "POST",
      idempotencyKey,
      headers: { "Idempotency-Key": idempotencyKey },
      body: JSON.stringify(command),
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  },

  checkStatus: async (
    paymentId: string,
    tenantId?: string
  ): Promise<PaymentStatusCheck> => {
    return httpClient<PaymentStatusCheck>(`/api/v1/payments/${paymentId}/status`, {
      method: "GET",
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  }
};
