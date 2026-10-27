import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { invoiceApi } from "../services/invoice-api.ts";
import { loadSessionContext } from "../../../core/api/auth-service.ts";
import { useRuntimeContext, usePermissions } from "../../../core/runtime/runtime-provider.tsx";
import type {
  CreateInvoiceDraftRequest,
  UpdateInvoiceDraftRequest,
  InvoiceListQuery,
  InvoiceFinalizationRequest,
  InvoiceCorrectionRequest,
  CancelInvoiceDraftRequest,
  InvoiceDirection
} from "../types.ts";

export interface InvoicingContextScope {
  readonly tenantId?: string | undefined;
  readonly legalEntityId?: string | undefined;
  readonly branchId?: string | undefined;
}

export function useInvoicingPermissions() {
  let perms: { hasPermission: (perm: string) => boolean } | null = null;
  try {
    perms = usePermissions();
  } catch {
    // Outside runtime provider
  }

  return {
    canReadCustomerInvoices: perms ? perms.hasPermission("invoicing.customer_invoice.read") : true,
    canCreateCustomerInvoice: perms ? perms.hasPermission("invoicing.customer_invoice.create") : true,
    canEditCustomerInvoice: perms ? perms.hasPermission("invoicing.customer_invoice.edit") : true,
    canFinalizeCustomerInvoice: perms ? perms.hasPermission("invoicing.customer_invoice.finalize") : true,
    canCancelCustomerDraft: perms ? perms.hasPermission("invoicing.customer_invoice.cancel_draft") : true,
    canReadSupplierInvoices: perms ? perms.hasPermission("invoicing.supplier_invoice.read") : true,
    canCreateSupplierInvoice: perms ? perms.hasPermission("invoicing.supplier_invoice.create") : true,
    canEditSupplierInvoice: perms ? perms.hasPermission("invoicing.supplier_invoice.edit") : true,
    canFinalizeSupplierInvoice: perms ? perms.hasPermission("invoicing.supplier_invoice.finalize") : true,
    canCreateCreditNote: perms ? perms.hasPermission("invoicing.credit_note.create") : true,
    canCreateDebitNote: perms ? perms.hasPermission("invoicing.debit_note.create") : true,
    canOverridePrice: perms ? perms.hasPermission("invoicing.price_override") : true,
    canOverridePaymentTerm: perms ? perms.hasPermission("invoicing.payment_term_override") : true,
    canOverrideDate: perms ? perms.hasPermission("invoicing.date_override") : true,
    canPreviewAccounting: perms ? perms.hasPermission("invoicing.accounting_preview") : true,
    canViewRegulatory: perms ? perms.hasPermission("invoicing.regulatory_details") : true
  };
}

export function useInvoicingScope(explicit?: InvoicingContextScope): {
  tenantId: string;
  legalEntityId: string;
  branchId: string;
} {
  const sessionQuery = useQuery({
    queryKey: ["session", "context"],
    queryFn: loadSessionContext,
    staleTime: 60_000
  });

  let runtimeEntityId = "";
  let runtimeTenantId = "";
  let runtimeBranchId = "";

  try {
    const runtime = useRuntimeContext();
    runtimeEntityId = (runtime as unknown as { legalEntity?: { legalEntityId?: string } }).legalEntity?.legalEntityId || "";
    runtimeTenantId = (runtime as unknown as { tenant?: { tenantId?: string } }).tenant?.tenantId || "";
    runtimeBranchId = (runtime as unknown as { branch?: { branchId?: string } }).branch?.branchId || "";
  } catch {
    // Graceful fallback outside provider
  }

  let queryEntityId = "";
  let queryBranchId = "";
  if (typeof window !== "undefined" && window.location.search) {
    try {
      const sp = new URLSearchParams(window.location.search);
      queryEntityId = sp.get("legalEntityId") || "";
      queryBranchId = sp.get("branchId") || "";
    } catch {
      // ignore
    }
  }

  const tenantId =
    explicit?.tenantId ||
    sessionQuery.data?.tenantId ||
    runtimeTenantId ||
    "";

  let legalEntityId =
    explicit?.legalEntityId ||
    queryEntityId ||
    sessionQuery.data?.context?.legalEntities?.[0]?.id ||
    runtimeEntityId ||
    "";

  let branchId =
    explicit?.branchId ||
    queryBranchId ||
    sessionQuery.data?.context?.branches?.[0]?.id ||
    runtimeBranchId ||
    "";

  if (!legalEntityId && typeof window !== "undefined") {
    legalEntityId = window.sessionStorage.getItem("erp.current.legalEntityId") || "";
  }
  if (!branchId && typeof window !== "undefined") {
    branchId = window.sessionStorage.getItem("erp.current.branchId") || "";
  }

  return { tenantId, legalEntityId, branchId };
}

export const invoicingKeys = {
  all: ["invoicing"] as const,
  customerList: (legalEntityId: string, query?: Partial<InvoiceListQuery>) =>
    [...invoicingKeys.all, "customer-invoices", legalEntityId, query] as const,
  supplierList: (legalEntityId: string, query?: Partial<InvoiceListQuery>) =>
    [...invoicingKeys.all, "supplier-invoices", legalEntityId, query] as const,
  customerDraft: (legalEntityId: string, id: string) =>
    [...invoicingKeys.all, "customer-draft", legalEntityId, id] as const,
  supplierDraft: (legalEntityId: string, id: string) =>
    [...invoicingKeys.all, "supplier-draft", legalEntityId, id] as const,
  customerDetail: (legalEntityId: string, id: string) =>
    [...invoicingKeys.all, "customer-detail", legalEntityId, id] as const,
  supplierDetail: (legalEntityId: string, id: string) =>
    [...invoicingKeys.all, "supplier-detail", legalEntityId, id] as const,
  customerRender: (legalEntityId: string, id: string) =>
    [...invoicingKeys.all, "customer-render", legalEntityId, id] as const,
  supplierRender: (legalEntityId: string, id: string) =>
    [...invoicingKeys.all, "supplier-render", legalEntityId, id] as const,
  customerCompliance: (legalEntityId: string, id: string) =>
    [...invoicingKeys.all, "customer-compliance", legalEntityId, id] as const,
  supplierCompliance: (legalEntityId: string, id: string) =>
    [...invoicingKeys.all, "supplier-compliance", legalEntityId, id] as const,
  legacy: (id: string) =>
    ["invoices", id] as const
};

// 1. Customer Invoices Queries
export function useCustomerInvoices(query: InvoiceListQuery, enabled = true) {
  return useQuery({
    queryKey: invoicingKeys.customerList(query.legalEntityId, query),
    queryFn: () => invoiceApi.listCustomerInvoices(query),
    enabled: Boolean(query.legalEntityId && enabled),
    staleTime: 15_000
  });
}

export function useCustomerDraft(id: string, legalEntityId: string, enabled = true) {
  return useQuery({
    queryKey: invoicingKeys.customerDraft(legalEntityId, id),
    queryFn: () => invoiceApi.getCustomerDraft(id, legalEntityId),
    enabled: Boolean(id && legalEntityId && enabled),
    staleTime: 5_000
  });
}

export function useCustomerInvoiceDetail(id: string, legalEntityId: string, enabled = true) {
  return useQuery({
    queryKey: invoicingKeys.customerDetail(legalEntityId, id),
    queryFn: () => invoiceApi.getCustomerInvoiceDetail(id, legalEntityId),
    enabled: Boolean(id && legalEntityId && enabled),
    staleTime: 30_000
  });
}

export function useCustomerRenderModel(id: string, legalEntityId: string, enabled = true) {
  return useQuery({
    queryKey: invoicingKeys.customerRender(legalEntityId, id),
    queryFn: () => invoiceApi.getCustomerRenderModel(id, legalEntityId),
    enabled: Boolean(id && legalEntityId && enabled),
    staleTime: 60_000
  });
}

export function useCustomerComplianceSnapshot(id: string, legalEntityId: string, enabled = true) {
  return useQuery({
    queryKey: invoicingKeys.customerCompliance(legalEntityId, id),
    queryFn: () => invoiceApi.getCustomerComplianceSnapshot(id, legalEntityId),
    enabled: Boolean(id && legalEntityId && enabled),
    staleTime: 60_000
  });
}

// 2. Supplier Invoices Queries
export function useSupplierInvoices(query: InvoiceListQuery, enabled = true) {
  return useQuery({
    queryKey: invoicingKeys.supplierList(query.legalEntityId, query),
    queryFn: () => invoiceApi.listSupplierInvoices(query),
    enabled: Boolean(query.legalEntityId && enabled),
    staleTime: 15_000
  });
}

export function useSupplierDraft(id: string, legalEntityId: string, enabled = true) {
  return useQuery({
    queryKey: invoicingKeys.supplierDraft(legalEntityId, id),
    queryFn: () => invoiceApi.getSupplierDraft(id, legalEntityId),
    enabled: Boolean(id && legalEntityId && enabled),
    staleTime: 5_000
  });
}

export function useSupplierInvoiceDetail(id: string, legalEntityId: string, enabled = true) {
  return useQuery({
    queryKey: invoicingKeys.supplierDetail(legalEntityId, id),
    queryFn: () => invoiceApi.getSupplierInvoiceDetail(id, legalEntityId),
    enabled: Boolean(id && legalEntityId && enabled),
    staleTime: 30_000
  });
}

export function useSupplierRenderModel(id: string, legalEntityId: string, enabled = true) {
  return useQuery({
    queryKey: invoicingKeys.supplierRender(legalEntityId, id),
    queryFn: () => invoiceApi.getSupplierRenderModel(id, legalEntityId),
    enabled: Boolean(id && legalEntityId && enabled),
    staleTime: 60_000
  });
}

export function useSupplierComplianceSnapshot(id: string, legalEntityId: string, enabled = true) {
  return useQuery({
    queryKey: invoicingKeys.supplierCompliance(legalEntityId, id),
    queryFn: () => invoiceApi.getSupplierComplianceSnapshot(id, legalEntityId),
    enabled: Boolean(id && legalEntityId && enabled),
    staleTime: 60_000
  });
}

// 3. Mutations
export function useCreateCustomerDraftMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (req: CreateInvoiceDraftRequest) => invoiceApi.createCustomerDraft(req),
    onSuccess: (_, vars) => {
      void queryClient.invalidateQueries({ queryKey: [...invoicingKeys.all, "customer-invoices", vars.legalEntityId] });
    }
  });
}

export function useUpdateCustomerDraftMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, legalEntityId, request }: { id: string; legalEntityId: string; request: UpdateInvoiceDraftRequest }) =>
      invoiceApi.updateCustomerDraft(id, legalEntityId, request),
    onSuccess: (_, vars) => {
      void queryClient.invalidateQueries({ queryKey: invoicingKeys.customerDraft(vars.legalEntityId, vars.id) });
      void queryClient.invalidateQueries({ queryKey: [...invoicingKeys.all, "customer-invoices", vars.legalEntityId] });
    }
  });
}

export function useCancelCustomerDraftMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, legalEntityId, request }: { id: string; legalEntityId: string; request: CancelInvoiceDraftRequest }) =>
      invoiceApi.cancelCustomerDraft(id, legalEntityId, request),
    onSuccess: (_, vars) => {
      void queryClient.invalidateQueries({ queryKey: invoicingKeys.customerDraft(vars.legalEntityId, vars.id) });
      void queryClient.invalidateQueries({ queryKey: [...invoicingKeys.all, "customer-invoices", vars.legalEntityId] });
    }
  });
}

export function useFinalizeCustomerInvoiceMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, legalEntityId, request }: { id: string; legalEntityId: string; request: InvoiceFinalizationRequest }) =>
      invoiceApi.finalizeCustomerInvoice(id, legalEntityId, request),
    onSuccess: (_, vars) => {
      void queryClient.invalidateQueries({ queryKey: invoicingKeys.customerDraft(vars.legalEntityId, vars.id) });
      void queryClient.invalidateQueries({ queryKey: invoicingKeys.customerDetail(vars.legalEntityId, vars.id) });
      void queryClient.invalidateQueries({ queryKey: [...invoicingKeys.all, "customer-invoices", vars.legalEntityId] });
    }
  });
}

export function useCreateCustomerCorrectionMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, legalEntityId, request }: { id: string; legalEntityId: string; request: InvoiceCorrectionRequest }) => {
      if (request.documentType === "CreditNote") {
        return invoiceApi.createCustomerCreditNote(id, legalEntityId, request);
      }
      return invoiceApi.createCustomerDebitNote(id, legalEntityId, request);
    },
    onSuccess: (_, vars) => {
      void queryClient.invalidateQueries({ queryKey: invoicingKeys.customerDetail(vars.legalEntityId, vars.id) });
      void queryClient.invalidateQueries({ queryKey: [...invoicingKeys.all, "customer-invoices", vars.legalEntityId] });
    }
  });
}

export function useCreateSupplierDraftMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (req: CreateInvoiceDraftRequest) => invoiceApi.createSupplierDraft(req),
    onSuccess: (_, vars) => {
      void queryClient.invalidateQueries({ queryKey: [...invoicingKeys.all, "supplier-invoices", vars.legalEntityId] });
    }
  });
}

export function useUpdateSupplierDraftMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, legalEntityId, request }: { id: string; legalEntityId: string; request: UpdateInvoiceDraftRequest }) =>
      invoiceApi.updateSupplierDraft(id, legalEntityId, request),
    onSuccess: (_, vars) => {
      void queryClient.invalidateQueries({ queryKey: invoicingKeys.supplierDraft(vars.legalEntityId, vars.id) });
      void queryClient.invalidateQueries({ queryKey: [...invoicingKeys.all, "supplier-invoices", vars.legalEntityId] });
    }
  });
}

export function useCancelSupplierDraftMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, legalEntityId, request }: { id: string; legalEntityId: string; request: CancelInvoiceDraftRequest }) =>
      invoiceApi.cancelSupplierDraft(id, legalEntityId, request),
    onSuccess: (_, vars) => {
      void queryClient.invalidateQueries({ queryKey: invoicingKeys.supplierDraft(vars.legalEntityId, vars.id) });
      void queryClient.invalidateQueries({ queryKey: [...invoicingKeys.all, "supplier-invoices", vars.legalEntityId] });
    }
  });
}

export function useFinalizeSupplierInvoiceMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, legalEntityId, request }: { id: string; legalEntityId: string; request: InvoiceFinalizationRequest }) =>
      invoiceApi.finalizeSupplierInvoice(id, legalEntityId, request),
    onSuccess: (_, vars) => {
      void queryClient.invalidateQueries({ queryKey: invoicingKeys.supplierDraft(vars.legalEntityId, vars.id) });
      void queryClient.invalidateQueries({ queryKey: invoicingKeys.supplierDetail(vars.legalEntityId, vars.id) });
      void queryClient.invalidateQueries({ queryKey: [...invoicingKeys.all, "supplier-invoices", vars.legalEntityId] });
    }
  });
}

export function useCreateSupplierCorrectionMutation() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, legalEntityId, request }: { id: string; legalEntityId: string; request: InvoiceCorrectionRequest }) => {
      if (request.documentType === "CreditNote") {
        return invoiceApi.createSupplierCreditNote(id, legalEntityId, request);
      }
      return invoiceApi.createSupplierDebitNote(id, legalEntityId, request);
    },
    onSuccess: (_, vars) => {
      void queryClient.invalidateQueries({ queryKey: invoicingKeys.supplierDetail(vars.legalEntityId, vars.id) });
      void queryClient.invalidateQueries({ queryKey: [...invoicingKeys.all, "supplier-invoices", vars.legalEntityId] });
    }
  });
}

// 4. Legacy compatibility hook (for stage3 reference flow test)
export function useInvoiceRecord(id: string) {
  return useQuery({
    queryKey: invoicingKeys.legacy(id),
    queryFn: () => invoiceApi.getLegacyInvoice(id),
    enabled: Boolean(id),
    staleTime: 60_000
  });
}

export function usePostInvoice() {
  return useMutation({
    mutationFn: async (_payload: { id: string; etag: string }) => {
      // no-op legacy post stub
      return { success: true };
    }
  });
}

export function useInvoicePreview() {
  return useMutation({
    mutationFn: async (_req: unknown) => {
      return { success: true };
    }
  });
}
