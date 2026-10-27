import { httpClient } from "../../../core/api/http-client.ts";
import type {
  CreateInvoiceDraftRequest,
  UpdateInvoiceDraftRequest,
  InvoiceListQuery,
  InvoicePage,
  InvoicePreview,
  InvoiceFinalizationRequest,
  InvoiceFinalizedResult,
  InvoiceDetail,
  InvoiceRenderModel,
  InvoiceDraftResponse,
  InvoiceCorrectionRequest,
  CancelInvoiceDraftRequest,
  InvoiceComplianceSnapshotContract,
  LegacyInvoice
} from "../types.ts";

function buildQueryString(params: Record<string, unknown>): string {
  const searchParams = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== "") {
      searchParams.set(key, String(value));
    }
  }
  const query = searchParams.toString();
  return query ? `?${query}` : "";
}

export const invoiceApi = {
  // 1. Customer Invoices (Receivable)
  async listCustomerInvoices(query: InvoiceListQuery): Promise<InvoicePage> {
    const qs = buildQueryString({
      legalEntityId: query.legalEntityId,
      branchId: query.branchId,
      partyId: query.partyId,
      fromDate: query.fromDate,
      toDate: query.toDate,
      currencyCode: query.currencyCode,
      lifecycle: query.lifecycle,
      search: query.search,
      page: query.page ?? 1,
      pageSize: query.pageSize ?? 50
    });
    return httpClient<InvoicePage>(`/api/v1/invoicing/customer-invoices${qs}`);
  },

  async createCustomerDraft(request: CreateInvoiceDraftRequest): Promise<{ id: string; lifecycle: string; version: number }> {
    return httpClient<{ id: string; lifecycle: string; version: number }>("/api/v1/invoicing/customer-invoices", {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  async getCustomerDraft(id: string, legalEntityId: string): Promise<InvoiceDraftResponse> {
    return httpClient<InvoiceDraftResponse>(`/api/v1/invoicing/customer-invoices/${encodeURIComponent(id)}?legalEntityId=${encodeURIComponent(legalEntityId)}`);
  },

  async updateCustomerDraft(id: string, legalEntityId: string, request: UpdateInvoiceDraftRequest): Promise<{ id: string; version: number }> {
    return httpClient<{ id: string; version: number }>(`/api/v1/invoicing/customer-invoices/${encodeURIComponent(id)}?legalEntityId=${encodeURIComponent(legalEntityId)}`, {
      method: "PUT",
      body: JSON.stringify(request)
    });
  },

  async cancelCustomerDraft(id: string, legalEntityId: string, request: CancelInvoiceDraftRequest): Promise<void> {
    await httpClient<void>(`/api/v1/invoicing/customer-invoices/${encodeURIComponent(id)}/cancel-draft?legalEntityId=${encodeURIComponent(legalEntityId)}`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  async previewCustomerDraftStateless(request: CreateInvoiceDraftRequest): Promise<InvoicePreview> {
    return httpClient<InvoicePreview>("/api/v1/invoicing/customer-invoices/preview", {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  async previewCustomerDraft(id: string, legalEntityId: string): Promise<InvoicePreview> {
    return httpClient<InvoicePreview>(`/api/v1/invoicing/customer-invoices/${encodeURIComponent(id)}/preview?legalEntityId=${encodeURIComponent(legalEntityId)}`, {
      method: "POST"
    });
  },

  async finalizeCustomerInvoice(id: string, legalEntityId: string, request: InvoiceFinalizationRequest): Promise<InvoiceFinalizedResult> {
    return httpClient<InvoiceFinalizedResult>(`/api/v1/invoicing/customer-invoices/${encodeURIComponent(id)}/finalize?legalEntityId=${encodeURIComponent(legalEntityId)}`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  async getCustomerInvoiceDetail(id: string, legalEntityId: string): Promise<InvoiceDetail> {
    return httpClient<InvoiceDetail>(`/api/v1/invoicing/customer-invoices/${encodeURIComponent(id)}/detail?legalEntityId=${encodeURIComponent(legalEntityId)}`);
  },

  async createCustomerCreditNote(id: string, legalEntityId: string, request: InvoiceCorrectionRequest): Promise<{ id: string; lifecycle: string; documentType: string; version: number }> {
    return httpClient<{ id: string; lifecycle: string; documentType: string; version: number }>(`/api/v1/invoicing/customer-invoices/${encodeURIComponent(id)}/credit-notes?legalEntityId=${encodeURIComponent(legalEntityId)}`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  async createCustomerDebitNote(id: string, legalEntityId: string, request: InvoiceCorrectionRequest): Promise<{ id: string; lifecycle: string; documentType: string; version: number }> {
    return httpClient<{ id: string; lifecycle: string; documentType: string; version: number }>(`/api/v1/invoicing/customer-invoices/${encodeURIComponent(id)}/debit-notes?legalEntityId=${encodeURIComponent(legalEntityId)}`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  async getCustomerRenderModel(id: string, legalEntityId: string): Promise<InvoiceRenderModel> {
    return httpClient<InvoiceRenderModel>(`/api/v1/invoicing/customer-invoices/${encodeURIComponent(id)}/render-model?legalEntityId=${encodeURIComponent(legalEntityId)}`);
  },

  async getCustomerComplianceSnapshot(id: string, legalEntityId: string): Promise<InvoiceComplianceSnapshotContract> {
    return httpClient<InvoiceComplianceSnapshotContract>(`/api/v1/invoicing/customer-invoices/${encodeURIComponent(id)}/compliance-snapshot?legalEntityId=${encodeURIComponent(legalEntityId)}`);
  },

  // 2. Supplier Invoices (Payable)
  async listSupplierInvoices(query: InvoiceListQuery): Promise<InvoicePage> {
    const qs = buildQueryString({
      legalEntityId: query.legalEntityId,
      branchId: query.branchId,
      partyId: query.partyId,
      fromDate: query.fromDate,
      toDate: query.toDate,
      currencyCode: query.currencyCode,
      lifecycle: query.lifecycle,
      search: query.search,
      page: query.page ?? 1,
      pageSize: query.pageSize ?? 50
    });
    return httpClient<InvoicePage>(`/api/v1/invoicing/supplier-invoices${qs}`);
  },

  async createSupplierDraft(request: CreateInvoiceDraftRequest): Promise<{ id: string; lifecycle: string; version: number }> {
    return httpClient<{ id: string; lifecycle: string; version: number }>("/api/v1/invoicing/supplier-invoices", {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  async getSupplierDraft(id: string, legalEntityId: string): Promise<InvoiceDraftResponse> {
    return httpClient<InvoiceDraftResponse>(`/api/v1/invoicing/supplier-invoices/${encodeURIComponent(id)}?legalEntityId=${encodeURIComponent(legalEntityId)}`);
  },

  async updateSupplierDraft(id: string, legalEntityId: string, request: UpdateInvoiceDraftRequest): Promise<{ id: string; version: number }> {
    return httpClient<{ id: string; version: number }>(`/api/v1/invoicing/supplier-invoices/${encodeURIComponent(id)}?legalEntityId=${encodeURIComponent(legalEntityId)}`, {
      method: "PUT",
      body: JSON.stringify(request)
    });
  },

  async cancelSupplierDraft(id: string, legalEntityId: string, request: CancelInvoiceDraftRequest): Promise<void> {
    await httpClient<void>(`/api/v1/invoicing/supplier-invoices/${encodeURIComponent(id)}/cancel-draft?legalEntityId=${encodeURIComponent(legalEntityId)}`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  async previewSupplierDraftStateless(request: CreateInvoiceDraftRequest): Promise<InvoicePreview> {
    return httpClient<InvoicePreview>("/api/v1/invoicing/supplier-invoices/preview", {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  async previewSupplierDraft(id: string, legalEntityId: string): Promise<InvoicePreview> {
    return httpClient<InvoicePreview>(`/api/v1/invoicing/supplier-invoices/${encodeURIComponent(id)}/preview?legalEntityId=${encodeURIComponent(legalEntityId)}`, {
      method: "POST"
    });
  },

  async finalizeSupplierInvoice(id: string, legalEntityId: string, request: InvoiceFinalizationRequest): Promise<InvoiceFinalizedResult> {
    return httpClient<InvoiceFinalizedResult>(`/api/v1/invoicing/supplier-invoices/${encodeURIComponent(id)}/finalize?legalEntityId=${encodeURIComponent(legalEntityId)}`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  async getSupplierInvoiceDetail(id: string, legalEntityId: string): Promise<InvoiceDetail> {
    return httpClient<InvoiceDetail>(`/api/v1/invoicing/supplier-invoices/${encodeURIComponent(id)}/detail?legalEntityId=${encodeURIComponent(legalEntityId)}`);
  },

  async createSupplierCreditNote(id: string, legalEntityId: string, request: InvoiceCorrectionRequest): Promise<{ id: string; lifecycle: string; documentType: string; version: number }> {
    return httpClient<{ id: string; lifecycle: string; documentType: string; version: number }>(`/api/v1/invoicing/supplier-invoices/${encodeURIComponent(id)}/credit-notes?legalEntityId=${encodeURIComponent(legalEntityId)}`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  async createSupplierDebitNote(id: string, legalEntityId: string, request: InvoiceCorrectionRequest): Promise<{ id: string; lifecycle: string; documentType: string; version: number }> {
    return httpClient<{ id: string; lifecycle: string; documentType: string; version: number }>(`/api/v1/invoicing/supplier-invoices/${encodeURIComponent(id)}/debit-notes?legalEntityId=${encodeURIComponent(legalEntityId)}`, {
      method: "POST",
      body: JSON.stringify(request)
    });
  },

  async getSupplierRenderModel(id: string, legalEntityId: string): Promise<InvoiceRenderModel> {
    return httpClient<InvoiceRenderModel>(`/api/v1/invoicing/supplier-invoices/${encodeURIComponent(id)}/render-model?legalEntityId=${encodeURIComponent(legalEntityId)}`);
  },

  async getSupplierComplianceSnapshot(id: string, legalEntityId: string): Promise<InvoiceComplianceSnapshotContract> {
    return httpClient<InvoiceComplianceSnapshotContract>(`/api/v1/invoicing/supplier-invoices/${encodeURIComponent(id)}/compliance-snapshot?legalEntityId=${encodeURIComponent(legalEntityId)}`);
  },

  // 3. Legacy reference endpoint (for backward-compatible tests)
  async getLegacyInvoice(id: string): Promise<LegacyInvoice> {
    return httpClient<LegacyInvoice>(`/api/v1/invoices/${encodeURIComponent(id)}`);
  }
};
