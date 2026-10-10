import { httpClient } from "../../../core/api/http-client.ts";
import type { Invoice, InvoicePreviewRequest, InvoicePreviewResponse } from "../types.ts";

export const invoiceApi = {
  getById: async (id: string, tenantId?: string): Promise<Invoice> => {
    return httpClient<Invoice>(`/api/v1/invoices/${id}`, {
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  },

  preview: async (
    request: InvoicePreviewRequest,
    tenantId?: string,
    signal?: AbortSignal
  ): Promise<InvoicePreviewResponse> => {
    return httpClient<InvoicePreviewResponse>("/api/v1/invoices/preview", {
      method: "POST",
      body: JSON.stringify(request),
      ...(signal !== undefined ? { signal } : {}),
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  },

  post: async (id: string, etag: string, tenantId?: string): Promise<Invoice> => {
    return httpClient<Invoice>(`/api/v1/invoices/${id}/post`, {
      method: "POST",
      etag,
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  }
};
