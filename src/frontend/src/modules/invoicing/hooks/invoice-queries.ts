import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { invoiceApi } from "../api/invoice-api.ts";
import type { Invoice, InvoicePreviewRequest, InvoicePreviewResponse } from "../types.ts";
import { queryKeyFactory } from "../../../platform/api/query-keys.ts";

export interface UseInvoiceContext {
  readonly tenantId?: string | undefined;
  readonly entityId?: string | undefined;
}

const DEFAULT_TENANT = "tenant-default";
const DEFAULT_ENTITY = "entity-default";

export function useInvoiceRecord(id: string, context: UseInvoiceContext = {}) {
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useQuery<Invoice>({
    queryKey: queryKeyFactory.detail(tenantId, entityId, "invoicing", "invoices", id),
    queryFn: () => invoiceApi.getById(id, tenantId),
    enabled: Boolean(id),
    staleTime: 60_000,
  });
}

export function useInvoicePreview(context: UseInvoiceContext = {}) {
  const tenantId = context.tenantId || DEFAULT_TENANT;

  return useMutation<InvoicePreviewResponse, Error, { request: InvoicePreviewRequest; signal?: AbortSignal }>({
    mutationFn: ({ request, signal }) => invoiceApi.preview(request, tenantId, signal),
  });
}

export function usePostInvoice(context: UseInvoiceContext = {}) {
  const queryClient = useQueryClient();
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useMutation<Invoice, Error, { id: string; etag: string }>({
    mutationFn: ({ id, etag }) => invoiceApi.post(id, etag, tenantId),
    onSuccess: (_data, { id }) => {
      void queryClient.invalidateQueries({
        queryKey: queryKeyFactory.detail(tenantId, entityId, "invoicing", "invoices", id),
      });
      void queryClient.invalidateQueries({
        queryKey: queryKeyFactory.resource(tenantId, entityId, "invoicing", "invoices"),
      });
    },
  });
}
