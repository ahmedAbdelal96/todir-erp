import { httpClient } from "../../../core/api/http-client.ts";
import type {
  AcceptAndInvoiceQuotationRequest,
  AcceptQuotationRequest,
  ConversionResult,
  ConvertQuotationRequest,
  ConvertQuotationToOrderRequest,
  CreateOrderRequest,
  CreateQuotationRequest,
  CreateReturnRequest,
  FulfillOrderRequest,
  OrderCommandRequest,
  QuotationDetail,
  QuotationListItem,
  ReturnCommandRequest,
  SalesCustomerProfileRequest,
  SalesOrderDetail,
  SalesOrderListItem,
  SalesPagedResult,
  SalesReturnExecutionResult,
  SalesReturnView,
  TransitionQuotationRequest,
  UpdateOrderRequest,
  UpdateQuotationRequest
} from "../types.ts";

const BASE = "/api/v1/sales";

function entityPath(entityId: string, path: string): string {
  return `${BASE}/legal-entities/${encodeURIComponent(entityId)}${path}`;
}

export const salesApi = {
  // Quotations
  listQuotations: (
    entityId: string,
    params?: { state?: string | null; partyId?: string | null; page?: number | null; pageSize?: number | null }
  ): Promise<SalesPagedResult<QuotationListItem>> => {
    const searchParams = new URLSearchParams();
    if (params?.state) searchParams.set("state", params.state);
    if (params?.partyId) searchParams.set("partyId", params.partyId);
    if (params?.page != null) searchParams.set("page", String(params.page));
    if (params?.pageSize != null) searchParams.set("pageSize", String(params.pageSize));
    const query = searchParams.toString();
    return httpClient<SalesPagedResult<QuotationListItem>>(entityPath(entityId, `/quotations${query ? `?${query}` : ""}`));
  },

  getQuotation: (entityId: string, id: string): Promise<QuotationDetail> =>
    httpClient<QuotationDetail>(entityPath(entityId, `/quotations/${encodeURIComponent(id)}`)),

  createQuotation: (entityId: string, request: CreateQuotationRequest): Promise<string> =>
    httpClient<string>(entityPath(entityId, "/quotations"), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  updateQuotation: (entityId: string, id: string, request: UpdateQuotationRequest): Promise<number> =>
    httpClient<number>(entityPath(entityId, `/quotations/${encodeURIComponent(id)}`), {
      method: "PUT",
      body: JSON.stringify(request)
    }),

  openQuotation: (entityId: string, id: string, request: TransitionQuotationRequest): Promise<number> =>
    httpClient<number>(entityPath(entityId, `/quotations/${encodeURIComponent(id)}/open`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  acceptQuotation: (entityId: string, id: string, request: AcceptQuotationRequest): Promise<QuotationDetail> =>
    httpClient<QuotationDetail>(entityPath(entityId, `/quotations/${encodeURIComponent(id)}/accept`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  declineQuotation: (entityId: string, id: string, request: TransitionQuotationRequest): Promise<number> =>
    httpClient<number>(entityPath(entityId, `/quotations/${encodeURIComponent(id)}/decline`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  cancelQuotation: (entityId: string, id: string, request: TransitionQuotationRequest): Promise<number> =>
    httpClient<number>(entityPath(entityId, `/quotations/${encodeURIComponent(id)}/cancel`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  reviseQuotation: (entityId: string, id: string, request: UpdateQuotationRequest): Promise<string> =>
    httpClient<string>(entityPath(entityId, `/quotations/${encodeURIComponent(id)}/revise`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  convertToInvoice: (entityId: string, id: string, request: ConvertQuotationRequest): Promise<ConversionResult> =>
    httpClient<ConversionResult>(entityPath(entityId, `/quotations/${encodeURIComponent(id)}/convert-to-invoice`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  acceptAndInvoice: (entityId: string, id: string, request: AcceptAndInvoiceQuotationRequest): Promise<QuotationDetail> =>
    httpClient<QuotationDetail>(entityPath(entityId, `/quotations/${encodeURIComponent(id)}/accept-and-invoice`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  convertToOrder: (entityId: string, id: string, request: ConvertQuotationToOrderRequest): Promise<string> =>
    httpClient<string>(entityPath(entityId, `/quotations/${encodeURIComponent(id)}/convert-to-order`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Orders
  listOrders: (
    entityId: string,
    params?: { page?: number | null; pageSize?: number | null }
  ): Promise<SalesPagedResult<SalesOrderListItem>> => {
    const searchParams = new URLSearchParams();
    if (params?.page != null) searchParams.set("page", String(params.page));
    if (params?.pageSize != null) searchParams.set("pageSize", String(params.pageSize));
    const query = searchParams.toString();
    return httpClient<SalesPagedResult<SalesOrderListItem>>(entityPath(entityId, `/orders${query ? `?${query}` : ""}`));
  },

  getOrder: (entityId: string, id: string): Promise<SalesOrderDetail> =>
    httpClient<SalesOrderDetail>(entityPath(entityId, `/orders/${encodeURIComponent(id)}`)),

  createOrder: (entityId: string, request: CreateOrderRequest): Promise<string> =>
    httpClient<string>(entityPath(entityId, "/orders"), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  updateOrder: (entityId: string, id: string, request: UpdateOrderRequest): Promise<number> =>
    httpClient<number>(entityPath(entityId, `/orders/${encodeURIComponent(id)}`), {
      method: "PUT",
      body: JSON.stringify(request)
    }),

  confirmOrder: (entityId: string, id: string, request: OrderCommandRequest): Promise<number> =>
    httpClient<number>(entityPath(entityId, `/orders/${encodeURIComponent(id)}/confirm`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  releaseReservation: (entityId: string, id: string, request: OrderCommandRequest): Promise<number> =>
    httpClient<number>(entityPath(entityId, `/orders/${encodeURIComponent(id)}/release-reservation`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  cancelOrderRemaining: (entityId: string, id: string, request: OrderCommandRequest): Promise<number> =>
    httpClient<number>(entityPath(entityId, `/orders/${encodeURIComponent(id)}/cancel-remaining`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  fulfillOrder: (entityId: string, id: string, request: FulfillOrderRequest): Promise<unknown> =>
    httpClient<unknown>(entityPath(entityId, `/orders/${encodeURIComponent(id)}/fulfill`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  createOrderInvoice: (entityId: string, id: string, request: OrderCommandRequest): Promise<ConversionResult> =>
    httpClient<ConversionResult>(entityPath(entityId, `/orders/${encodeURIComponent(id)}/create-invoice`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Returns
  createReturn: (entityId: string, request: CreateReturnRequest): Promise<string> =>
    httpClient<string>(entityPath(entityId, "/returns"), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  getReturn: (entityId: string, id: string): Promise<SalesReturnView> =>
    httpClient<SalesReturnView>(entityPath(entityId, `/returns/${encodeURIComponent(id)}`)),

  approveReturn: (entityId: string, id: string, request: ReturnCommandRequest): Promise<number> =>
    httpClient<number>(entityPath(entityId, `/returns/${encodeURIComponent(id)}/approve`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  executeReturn: (entityId: string, id: string, request: ReturnCommandRequest): Promise<SalesReturnExecutionResult> =>
    httpClient<SalesReturnExecutionResult>(entityPath(entityId, `/returns/${encodeURIComponent(id)}/execute`), {
      method: "POST",
      body: JSON.stringify(request)
    }),

  // Customer Profiles
  saveCustomerProfile: (entityId: string, partyId: string, request: SalesCustomerProfileRequest): Promise<boolean> =>
    httpClient<boolean>(entityPath(entityId, `/customer-profiles/${encodeURIComponent(partyId)}`), {
      method: "PUT",
      body: JSON.stringify(request)
    })
};
