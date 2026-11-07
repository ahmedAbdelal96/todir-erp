// POS Module 20 API Service

import { httpClient, generateKey } from "../../../core/api/http-client.ts";
import type {
  RegisterResponse,
  CreateRegisterRequest,
  UpdateRegisterRequest,
  TerminalResponse,
  RegisterTerminalRequest,
  SessionResponse,
  OpenSessionRequest,
  CloseSessionRequest,
  CashMovementRequest,
  CartResponse,
  CartDetailsResponse,
  CreateCartRequest,
  AddCartLineRequest,
  ScanCartLineRequest,
  UpdateCartLineRequest,
  DiscountCartRequest,
  SetCartCustomerRequest,
  CheckoutRequest,
  CheckoutForCartRequest,
  CheckoutResponse,
  TenderRequest,
  CreateReturnRequest,
  ReturnLineRequest,
  RefundReturnRequest,
  ExchangeRequest,
  ExchangeResponse,
  ManagerOverrideRequest,
  OfflineEnvelopeRequest,
  OfflinePackageResponse,
  OfflineConflictResolutionRequest,
  CatalogVariantSnapshot,
  CatalogSearchResult,
  PosPaymentMixReport,
  PosDiscountSummaryReport,
} from "../types.ts";

const BASE = "/api/v1/pos";

export const posApi = {
  // Registers
  async listRegisters(entityId: string): Promise<readonly RegisterResponse[]> {
    return httpClient<readonly RegisterResponse[]>(`${BASE}/legal-entities/${entityId}/registers`, {
      legalEntityId: entityId,
    });
  },

  async createRegister(entityId: string, request: CreateRegisterRequest): Promise<RegisterResponse> {
    return httpClient<RegisterResponse>(`${BASE}/legal-entities/${entityId}/registers`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId: entityId,
      idempotencyKey: generateKey(),
    });
  },

  async getRegister(registerId: string): Promise<RegisterResponse> {
    return httpClient<RegisterResponse>(`${BASE}/registers/${registerId}`);
  },

  async updateRegister(registerId: string, request: UpdateRegisterRequest): Promise<RegisterResponse> {
    return httpClient<RegisterResponse>(`${BASE}/registers/${registerId}`, {
      method: "PUT",
      body: JSON.stringify(request),
    });
  },

  async activateRegister(registerId: string): Promise<RegisterResponse> {
    return httpClient<RegisterResponse>(`${BASE}/registers/${registerId}/activate`, {
      method: "POST",
    });
  },

  async deactivateRegister(registerId: string): Promise<RegisterResponse> {
    return httpClient<RegisterResponse>(`${BASE}/registers/${registerId}/deactivate`, {
      method: "POST",
    });
  },

  async registerTerminal(registerId: string, request: RegisterTerminalRequest): Promise<TerminalResponse> {
    return httpClient<TerminalResponse>(`${BASE}/registers/${registerId}/terminals`, {
      method: "POST",
      body: JSON.stringify(request),
      idempotencyKey: generateKey(),
    });
  },

  // Terminals
  async rotateTerminalCredential(terminalId: string): Promise<TerminalResponse> {
    return httpClient<TerminalResponse>(`${BASE}/terminals/${terminalId}/rotate-credential`, {
      method: "POST",
    });
  },

  async revokeTerminal(terminalId: string): Promise<TerminalResponse> {
    return httpClient<TerminalResponse>(`${BASE}/terminals/${terminalId}/revoke`, {
      method: "POST",
    });
  },

  async terminalHeartbeat(terminalId: string): Promise<TerminalResponse> {
    return httpClient<TerminalResponse>(`${BASE}/terminals/${terminalId}/heartbeat`, {
      method: "POST",
    });
  },

  async heartbeatTerminal(terminalId: string): Promise<TerminalResponse> {
    return this.terminalHeartbeat(terminalId);
  },

  // Sessions
  async openSession(entityId: string, request: OpenSessionRequest): Promise<SessionResponse> {
    return httpClient<SessionResponse>(`${BASE}/legal-entities/${entityId}/sessions`, {
      method: "POST",
      body: JSON.stringify(request),
      legalEntityId: entityId,
      idempotencyKey: generateKey(),
    });
  },

  async getSession(sessionId: string): Promise<SessionResponse> {
    return httpClient<SessionResponse>(`${BASE}/sessions/${sessionId}`);
  },

  async getCurrentSession(registerId: string): Promise<SessionResponse | null> {
    try {
      return await httpClient<SessionResponse>(`${BASE}/sessions/current?registerId=${registerId}`);
    } catch {
      return null;
    }
  },

  async startSessionClose(sessionId: string): Promise<SessionResponse> {
    return httpClient<SessionResponse>(`${BASE}/sessions/${sessionId}/start-close`, {
      method: "POST",
    });
  },

  async recordCashCount(sessionId: string, request: CloseSessionRequest): Promise<SessionResponse> {
    return httpClient<SessionResponse>(`${BASE}/sessions/${sessionId}/cash-count`, {
      method: "POST",
      body: JSON.stringify(request),
    });
  },

  async addCashMovement(sessionId: string, request: CashMovementRequest): Promise<{ id: string }> {
    return httpClient<{ id: string }>(`${BASE}/sessions/${sessionId}/cash-movements`, {
      method: "POST",
      body: JSON.stringify(request),
      idempotencyKey: generateKey(),
    });
  },

  async closeSession(sessionId: string, request: CloseSessionRequest): Promise<SessionResponse> {
    return httpClient<SessionResponse>(`${BASE}/sessions/${sessionId}/close`, {
      method: "POST",
      body: JSON.stringify(request),
      idempotencyKey: generateKey(),
    });
  },

  async approveSessionVariance(sessionId: string): Promise<SessionResponse> {
    return httpClient<SessionResponse>(`${BASE}/sessions/${sessionId}/approve-variance`, {
      method: "POST",
      idempotencyKey: generateKey(),
    });
  },

  // Carts & Cashier Operations
  async createCart(request: CreateCartRequest): Promise<CartResponse> {
    return httpClient<CartResponse>(`${BASE}/carts`, {
      method: "POST",
      body: JSON.stringify(request),
      idempotencyKey: generateKey(),
    });
  },

  async getCart(cartId: string): Promise<CartResponse> {
    return httpClient<CartResponse>(`${BASE}/carts/${cartId}`);
  },

  async getCartDetails(cartId: string): Promise<CartDetailsResponse> {
    return httpClient<CartDetailsResponse>(`${BASE}/carts/${cartId}`);
  },

  async scanCartLine(cartId: string, request: ScanCartLineRequest): Promise<CartResponse> {
    return httpClient<CartResponse>(`${BASE}/carts/${cartId}/scan`, {
      method: "POST",
      body: JSON.stringify(request),
      idempotencyKey: generateKey(),
    });
  },

  async addCartLine(cartId: string, request: AddCartLineRequest): Promise<CartResponse> {
    return httpClient<CartResponse>(`${BASE}/carts/${cartId}/lines`, {
      method: "POST",
      body: JSON.stringify(request),
      idempotencyKey: generateKey(),
    });
  },

  async updateCartLine(cartId: string, lineId: string, request: UpdateCartLineRequest): Promise<CartResponse> {
    return httpClient<CartResponse>(`${BASE}/carts/${cartId}/lines/${lineId}`, {
      method: "PUT",
      body: JSON.stringify(request),
    });
  },

  async removeCartLine(cartId: string, lineId: string, expectedVersion: number): Promise<CartResponse> {
    return httpClient<CartResponse>(`${BASE}/carts/${cartId}/lines/${lineId}?expectedVersion=${expectedVersion}`, {
      method: "DELETE",
    });
  },

  async setCartCustomer(cartId: string, customerPartyId: string | null): Promise<CartResponse> {
    return httpClient<CartResponse>(`${BASE}/carts/${cartId}/customer`, {
      method: "POST",
      body: JSON.stringify({ customerPartyId }),
    });
  },

  async applyCartLineDiscount(cartId: string, request: DiscountCartRequest): Promise<CartResponse> {
    return httpClient<CartResponse>(`${BASE}/carts/${cartId}/discount`, {
      method: "POST",
      body: JSON.stringify(request),
    });
  },

  async holdCart(cartId: string): Promise<CartResponse> {
    return httpClient<CartResponse>(`${BASE}/carts/${cartId}/hold`, {
      method: "POST",
    });
  },

  async resumeCart(cartId: string): Promise<CartResponse> {
    return httpClient<CartResponse>(`${BASE}/carts/${cartId}/resume`, {
      method: "POST",
    });
  },

  async cancelCart(cartId: string): Promise<CartResponse> {
    return httpClient<CartResponse>(`${BASE}/carts/${cartId}/cancel`, {
      method: "POST",
    });
  },

  // Checkout & Payments
  async checkout(request: CheckoutRequest): Promise<CheckoutResponse> {
    return httpClient<CheckoutResponse>(`${BASE}/checkout`, {
      method: "POST",
      body: JSON.stringify(request),
      idempotencyKey: request.idempotencyKey || generateKey(),
    });
  },

  async checkoutForCart(cartId: string, request: CheckoutForCartRequest): Promise<CheckoutResponse> {
    return httpClient<CheckoutResponse>(`${BASE}/carts/${cartId}/checkout`, {
      method: "POST",
      body: JSON.stringify(request),
      idempotencyKey: request.idempotencyKey || generateKey(),
    });
  },

  async getCheckout(checkoutId: string): Promise<CheckoutResponse> {
    return httpClient<CheckoutResponse>(`${BASE}/checkouts/${checkoutId}`);
  },

  async refreshPaymentStatus(checkoutId: string): Promise<CheckoutResponse> {
    return httpClient<CheckoutResponse>(`${BASE}/checkouts/${checkoutId}/payment-status/refresh`, {
      method: "POST",
    });
  },

  async completeCheckout(checkoutId: string): Promise<CheckoutResponse> {
    return httpClient<CheckoutResponse>(`${BASE}/checkouts/${checkoutId}/complete`, {
      method: "POST",
    });
  },

  async addCheckoutTender(checkoutId: string, tender: TenderRequest): Promise<CheckoutResponse> {
    return httpClient<CheckoutResponse>(`${BASE}/checkouts/${checkoutId}/tenders`, {
      method: "POST",
      body: JSON.stringify(tender),
      idempotencyKey: generateKey(),
    });
  },

  async recoverCheckout(checkoutId: string): Promise<CheckoutResponse> {
    return httpClient<CheckoutResponse>(`${BASE}/checkouts/${checkoutId}/recover`, {
      method: "POST",
    });
  },

  // Returns & Exchanges
  async createReturn(request: CreateReturnRequest): Promise<{ id: string }> {
    return httpClient<{ id: string }>(`${BASE}/returns`, {
      method: "POST",
      body: JSON.stringify(request),
      idempotencyKey: generateKey(),
    });
  },

  async addReturnLine(returnId: string, line: ReturnLineRequest): Promise<{ id: string }> {
    return httpClient<{ id: string }>(`${BASE}/returns/${returnId}/lines`, {
      method: "POST",
      body: JSON.stringify(line),
      idempotencyKey: generateKey(),
    });
  },

  async completeReturn(returnId: string): Promise<{ success: boolean }> {
    return httpClient<{ success: boolean }>(`${BASE}/returns/${returnId}/complete`, {
      method: "POST",
    });
  },

  async refundReturn(returnId: string, request: RefundReturnRequest): Promise<{ success: boolean; paymentId?: string }> {
    return httpClient<{ success: boolean; paymentId?: string }>(`${BASE}/returns/${returnId}/refund`, {
      method: "POST",
      body: JSON.stringify(request),
      idempotencyKey: generateKey(),
    });
  },

  async createReturnCreditNote(returnId: string): Promise<{ invoiceId: string; documentNumber: string }> {
    return httpClient<{ invoiceId: string; documentNumber: string }>(`${BASE}/returns/${returnId}/credit-note`, {
      method: "POST",
      idempotencyKey: generateKey(),
    });
  },

  async createExchange(request: ExchangeRequest): Promise<ExchangeResponse> {
    return httpClient<ExchangeResponse>(`${BASE}/exchanges`, {
      method: "POST",
      body: JSON.stringify(request),
      idempotencyKey: generateKey(),
    });
  },

  // Manager Overrides
  async createManagerOverride(request: ManagerOverrideRequest): Promise<string> {
    return httpClient<string>(`${BASE}/overrides`, {
      method: "POST",
      body: JSON.stringify(request),
      idempotencyKey: generateKey(),
    });
  },

  // Offline Mode & Sync
  async downloadOfflinePackage(registerId: string, deviceIdentity: string): Promise<OfflinePackageResponse> {
    return httpClient<OfflinePackageResponse>(`${BASE}/offline/package?registerId=${registerId}&deviceIdentity=${deviceIdentity}`);
  },

  async syncOfflineEnvelope(request: OfflineEnvelopeRequest): Promise<{ envelopeId: string }> {
    return httpClient<{ envelopeId: string }>(`${BASE}/offline/sync`, {
      method: "POST",
      body: JSON.stringify(request),
      idempotencyKey: request.idempotencyKey || generateKey(),
    });
  },

  async resolveOfflineConflict(conflictId: string, request: OfflineConflictResolutionRequest): Promise<{ resolved: boolean }> {
    return httpClient<{ resolved: boolean }>(`${BASE}/offline/conflicts/${conflictId}/resolve`, {
      method: "POST",
      body: JSON.stringify(request),
    });
  },

  // Store Manager Reports
  async listSales(entityId: string, from?: string, to?: string, page = 1, pageSize = 25): Promise<any> {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (from) params.set("from", from);
    if (to) params.set("to", to);
    return httpClient<any>(`${BASE}/legal-entities/${entityId}/reports/sales?${params.toString()}`);
  },

  async listReturns(entityId: string, from?: string, to?: string, page = 1, pageSize = 25): Promise<any> {
    const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
    if (from) params.set("from", from);
    if (to) params.set("to", to);
    return httpClient<any>(`${BASE}/legal-entities/${entityId}/reports/returns?${params.toString()}`);
  },

  async getPaymentMix(entityId: string, from?: string, to?: string): Promise<PosPaymentMixReport> {
    const params = new URLSearchParams();
    if (from) params.set("from", from);
    if (to) params.set("to", to);
    return httpClient<PosPaymentMixReport>(`${BASE}/legal-entities/${entityId}/reports/payment-mix?${params.toString()}`);
  },

  async getSessionSummary(entityId: string, sessionId: string): Promise<any> {
    return httpClient<any>(`${BASE}/legal-entities/${entityId}/reports/session-summary/${sessionId}`);
  },

  async listCashDifferences(entityId: string, page = 1, pageSize = 25): Promise<any> {
    return httpClient<any>(`${BASE}/legal-entities/${entityId}/reports/cash-differences?page=${page}&pageSize=${pageSize}`);
  },

  async getDiscountSummary(entityId: string, from?: string, to?: string): Promise<PosDiscountSummaryReport> {
    const params = new URLSearchParams();
    if (from) params.set("from", from);
    if (to) params.set("to", to);
    return httpClient<PosDiscountSummaryReport>(`${BASE}/legal-entities/${entityId}/reports/discounts?${params.toString()}`);
  },

  // Catalog Lookup
  async lookupCatalog(query: string, legalEntityId: string, page = 1, pageSize = 25): Promise<CatalogSearchResult> {
    return httpClient<CatalogSearchResult>(`${BASE}/catalog/search?query=${encodeURIComponent(query)}&legalEntityId=${legalEntityId}&page=${page}&pageSize=${pageSize}`);
  },

  async searchCatalog(query: string, legalEntityId: string, page = 1, pageSize = 25): Promise<CatalogSearchResult> {
    return this.lookupCatalog(query, legalEntityId, page, pageSize);
  },

  async lookupBarcode(barcode: string, legalEntityId: string): Promise<CatalogVariantSnapshot> {
    return httpClient<CatalogVariantSnapshot>(`${BASE}/catalog/barcode/${encodeURIComponent(barcode)}?legalEntityId=${legalEntityId}`);
  },
};
