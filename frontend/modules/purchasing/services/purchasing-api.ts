import { httpClient } from "../../../core/api/http-client.ts";
import type {
  CreatePurchaseOrderRequest,
  UpdatePurchaseOrderRequest,
  PurchaseOrderCommandRequest,
  CreateSupplierInvoiceRequest,
  SupplierPurchaseProfileRequest,
  SupplierPurchaseProfileView,
  PurchaseSettingsRequest,
  PurchaseSettingsView,
  ReceivePurchaseOrderRequest,
  PurchaseReceiptResult,
  MatchPurchaseOrderRequest,
  PurchaseMatchView,
  CreateSupplierReturnRequest,
  SupplierReturnView,
  PurchaseOrderView,
  PurchaseOrderPage,
  ApproveAndBillRequest,
  ReceiveAndBillRequest
} from "../types.ts";

const BASE = "/api/v1/purchasing";

function entityPath(entityId: string, path: string): string {
  return `${BASE}/legal-entities/${encodeURIComponent(entityId)}${path}`;
}

export const purchasingApi = {
  listPurchaseOrders: (
    entityId: string,
    params?: { state?: string | null; page?: number | null; pageSize?: number | null }
  ): Promise<PurchaseOrderPage> => {
    const searchParams = new URLSearchParams();
    if (params?.state) searchParams.set("state", params.state);
    if (params?.page != null) searchParams.set("page", String(params.page));
    if (params?.pageSize != null) searchParams.set("pageSize", String(params.pageSize));
    const query = searchParams.toString();
    return httpClient<PurchaseOrderPage>(entityPath(entityId, `/purchase-orders${query ? `?${query}` : ""}`));
  },

  getPurchaseOrder: (entityId: string, id: string): Promise<PurchaseOrderView> =>
    httpClient<PurchaseOrderView>(entityPath(entityId, `/purchase-orders/${encodeURIComponent(id)}`)),

  createPurchaseOrder: (entityId: string, request: CreatePurchaseOrderRequest): Promise<string> =>
    httpClient<string>(entityPath(entityId, "/purchase-orders"), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  updatePurchaseOrder: (entityId: string, id: string, request: UpdatePurchaseOrderRequest): Promise<number> =>
    httpClient<number>(entityPath(entityId, `/purchase-orders/${encodeURIComponent(id)}`), {
      method: "PUT",
      body: JSON.stringify(request)
    }),

  amendPurchaseOrder: (entityId: string, id: string, request: UpdatePurchaseOrderRequest): Promise<number> =>
    httpClient<number>(entityPath(entityId, `/purchase-orders/${encodeURIComponent(id)}/amend`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  approvePurchaseOrder: (entityId: string, id: string, request: PurchaseOrderCommandRequest): Promise<number> =>
    httpClient<number>(entityPath(entityId, `/purchase-orders/${encodeURIComponent(id)}/approve`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  rejectPurchaseOrder: (entityId: string, id: string, request: PurchaseOrderCommandRequest): Promise<number> =>
    httpClient<number>(entityPath(entityId, `/purchase-orders/${encodeURIComponent(id)}/reject`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  cancelRemainingPurchaseOrder: (entityId: string, id: string, request: PurchaseOrderCommandRequest): Promise<number> =>
    httpClient<number>(entityPath(entityId, `/purchase-orders/${encodeURIComponent(id)}/cancel-remaining`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  closePurchaseOrder: (entityId: string, id: string, request: PurchaseOrderCommandRequest): Promise<number> =>
    httpClient<number>(entityPath(entityId, `/purchase-orders/${encodeURIComponent(id)}/close`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  closeShortPurchaseOrder: (entityId: string, id: string, request: PurchaseOrderCommandRequest): Promise<number> =>
    httpClient<number>(entityPath(entityId, `/purchase-orders/${encodeURIComponent(id)}/close-short`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  receivePurchaseOrder: (entityId: string, id: string, request: ReceivePurchaseOrderRequest): Promise<PurchaseReceiptResult> =>
    httpClient<PurchaseReceiptResult>(entityPath(entityId, `/purchase-orders/${encodeURIComponent(id)}/receive`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  createSupplierInvoice: (entityId: string, id: string, request: CreateSupplierInvoiceRequest): Promise<string> =>
    httpClient<string>(entityPath(entityId, `/purchase-orders/${encodeURIComponent(id)}/create-supplier-invoice`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  approveAndBill: (entityId: string, id: string, request: ApproveAndBillRequest): Promise<string> =>
    httpClient<string>(entityPath(entityId, `/purchase-orders/${encodeURIComponent(id)}/approve-and-bill`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  receiveAndBill: (entityId: string, id: string, request: ReceiveAndBillRequest): Promise<string> =>
    httpClient<string>(entityPath(entityId, `/purchase-orders/${encodeURIComponent(id)}/receive-and-bill`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  matchPurchaseOrder: (entityId: string, id: string, request: MatchPurchaseOrderRequest): Promise<PurchaseMatchView> =>
    httpClient<PurchaseMatchView>(entityPath(entityId, `/purchase-orders/${encodeURIComponent(id)}/match`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  getSupplierProfile: (entityId: string, supplierId: string): Promise<SupplierPurchaseProfileView | null> =>
    httpClient<SupplierPurchaseProfileView | null>(entityPath(entityId, `/suppliers/${encodeURIComponent(supplierId)}/profile`)),

  saveSupplierProfile: (entityId: string, supplierId: string, request: SupplierPurchaseProfileRequest): Promise<number> =>
    httpClient<number>(entityPath(entityId, `/suppliers/${encodeURIComponent(supplierId)}/profile`), {
      method: "PUT",
      body: JSON.stringify(request)
    }),

  getSettings: (entityId: string): Promise<PurchaseSettingsView> =>
    httpClient<PurchaseSettingsView>(entityPath(entityId, "/settings")),

  saveSettings: (entityId: string, request: PurchaseSettingsRequest): Promise<PurchaseSettingsView> =>
    httpClient<PurchaseSettingsView>(entityPath(entityId, "/settings"), {
      method: "PUT",
      body: JSON.stringify(request)
    }),

  createReturn: (entityId: string, orderId: string, request: CreateSupplierReturnRequest): Promise<string> =>
    httpClient<string>(entityPath(entityId, `/purchase-orders/${encodeURIComponent(orderId)}/returns`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  getReturn: (entityId: string, id: string): Promise<SupplierReturnView | null> =>
    httpClient<SupplierReturnView | null>(entityPath(entityId, `/returns/${encodeURIComponent(id)}`)),

  approveReturn: (entityId: string, id: string, request: PurchaseOrderCommandRequest): Promise<number> =>
    httpClient<number>(entityPath(entityId, `/returns/${encodeURIComponent(id)}/approve`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  executeReturn: (entityId: string, id: string): Promise<SupplierReturnView> =>
    httpClient<SupplierReturnView>(entityPath(entityId, `/returns/${encodeURIComponent(id)}/execute`), {
      method: "POST"
    })
};
