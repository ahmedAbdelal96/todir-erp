import React, { useState, useEffect, useCallback, useMemo, useRef } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate, useParams, useLocation } from "react-router-dom";
import { PartyPicker } from "../../directory/public.ts";
import { Surface } from "../../../components/pages/surface.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { LocalDateField } from "../../../components/fields/local-date-field.tsx";
import { Select, SelectItem } from "../../../components/primitives/select.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { AlertCircleIcon, CheckIcon, RefreshIcon } from "../../../components/primitives/icons.tsx";
import { InvoiceLineEditor } from "../components/invoice-line-editor.tsx";
import { InvoiceTotalsCard } from "../components/invoice-totals-card.tsx";
import { InvoicePreviewModal } from "../features/invoice-preview-modal.tsx";
import { CancelDraftDialog } from "../features/cancel-draft-dialog.tsx";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import {
  useDraftAutoSave,
  useDirtyFormGuard,
  DraftRecoveryBanner
} from "../../../components/forms/index.ts";
import { useFastGridNavigation } from "../../../components/grid/index.ts";
import {
  useInvoicingScope,
  useInvoicingPermissions,
  useCustomerDraft,
  useSupplierDraft,
  useCreateCustomerDraftMutation,
  useUpdateCustomerDraftMutation,
  useCancelCustomerDraftMutation,
  useFinalizeCustomerInvoiceMutation,
  useCreateSupplierDraftMutation,
  useUpdateSupplierDraftMutation,
  useCancelSupplierDraftMutation,
  useFinalizeSupplierInvoiceMutation
} from "../hooks/invoice-queries.ts";
import { invoiceApi } from "../services/invoice-api.ts";
import type {
  InvoiceLineInput,
  CreateInvoiceDraftRequest,
  UpdateInvoiceDraftRequest,
  InvoicePreview
} from "../types.ts";
import type { CurrencyCode, DecimalString } from "../../../components/contracts/domain-values.ts";

export interface InvoiceDraftWorkspaceProps {
  readonly direction: "customer" | "supplier";
}

export function InvoiceDraftWorkspace({ direction }: InvoiceDraftWorkspaceProps): React.JSX.Element {
  const { t } = useTranslation("invoicing");
  const navigate = useNavigate();
  const location = useLocation();
  const { id } = useParams<{ id: string }>();
  const isEditing = Boolean(id);
  const isCustomer = direction === "customer";

  const { legalEntityId, branchId } = useInvoicingScope();
  const perms = useInvoicingPermissions();

  // Queries for existing draft
  const customerDraftQuery = useCustomerDraft(id ?? "", legalEntityId, isCustomer && isEditing);
  const supplierDraftQuery = useSupplierDraft(id ?? "", legalEntityId, !isCustomer && isEditing);
  const existingDraft = isCustomer ? customerDraftQuery.data : supplierDraftQuery.data;

  // Mutations
  const createCustomerDraft = useCreateCustomerDraftMutation();
  const updateCustomerDraft = useUpdateCustomerDraftMutation();
  const cancelCustomerDraft = useCancelCustomerDraftMutation();
  const finalizeCustomer = useFinalizeCustomerInvoiceMutation();

  const createSupplierDraft = useCreateSupplierDraftMutation();
  const updateSupplierDraft = useUpdateSupplierDraftMutation();
  const cancelSupplierDraft = useCancelSupplierDraftMutation();
  const finalizeSupplier = useFinalizeSupplierInvoiceMutation();

  // Form State
  const [version, setVersion] = useState<number>(1);
  const [partyId, setPartyId] = useState<string>("");
  const [supplierInvoiceNumber, setSupplierInvoiceNumber] = useState<string>("");
  const [documentDate, setDocumentDate] = useState<string>(() => new Date().toISOString().slice(0, 10));
  const [postingDate, setPostingDate] = useState<string>(() => new Date().toISOString().slice(0, 10));
  const [taxDate, setTaxDate] = useState<string>(() => new Date().toISOString().slice(0, 10));
  const [supplyDate, setSupplyDate] = useState<string>("");
  const [currencyCode, setCurrencyCode] = useState<CurrencyCode>("SAR");
  const [paymentTermId, setPaymentTermId] = useState<string>("");
  const [documentDiscount, setDocumentDiscount] = useState<DecimalString>("0.00");
  const [lines, setLines] = useState<InvoiceLineInput[]>([
    {
      description: "",
      quantity: "1.000",
      unitCode: "EA",
      unitPrice: "0.00",
      discountAmount: "0.00"
    }
  ]);

  // UI state
  const [showAdvancedDates, setShowAdvancedDates] = useState(false);
  const [concurrencyConflict, setConcurrencyConflict] = useState(false);
  const [duplicateSupplierError, setDuplicateSupplierError] = useState(false);
  const [saveSuccessMessage, setSaveSuccessMessage] = useState(false);
  const [isPreviewOpen, setIsPreviewOpen] = useState(false);
  const [previewData, setPreviewData] = useState<InvoicePreview | null>(null);
  const [isPreviewLoading, setIsPreviewLoading] = useState(false);
  const [isCancelDialogOpen, setIsCancelDialogOpen] = useState(false);
  const [whyPriceIndex, setWhyPriceIndex] = useState<number | null>(null);

  const linesContainerRef = useRef<HTMLDivElement>(null);

  const draftStorageKey = isEditing && id ? `inv-draft-${direction}-${id}` : `inv-draft-new-${direction}`;
  const isFormDirty = Boolean(
    partyId ||
    lines.some((l) => l.description || (l.unitPrice && l.unitPrice !== "0.00") || l.quantity !== "1.000") ||
    lines.length > 1 ||
    supplierInvoiceNumber
  );

  useDirtyFormGuard({ isDirty: isFormDirty });

  const {
    existingDraft: localDraft,
    restoreDraft: restoreLocalDraft,
    discardDraft: discardLocalDraft,
    clearDraft: clearLocalDraft
  } = useDraftAutoSave({
    draftKey: draftStorageKey,
    data: {
      partyId,
      supplierInvoiceNumber,
      documentDate,
      postingDate,
      taxDate,
      supplyDate,
      currencyCode,
      paymentTermId,
      documentDiscount,
      lines
    },
    isDirty: isFormDirty
  });

  const handleRestoreDraft = () => {
    const restored = restoreLocalDraft();
    if (restored) {
      if (restored.partyId) setPartyId(restored.partyId);
      if (restored.supplierInvoiceNumber) setSupplierInvoiceNumber(restored.supplierInvoiceNumber);
      if (restored.documentDate) setDocumentDate(restored.documentDate);
      if (restored.postingDate) setPostingDate(restored.postingDate);
      if (restored.taxDate) setTaxDate(restored.taxDate);
      if (restored.supplyDate) setSupplyDate(restored.supplyDate);
      if (restored.currencyCode) setCurrencyCode(restored.currencyCode);
      if (restored.paymentTermId) setPaymentTermId(restored.paymentTermId);
      if (restored.documentDiscount) setDocumentDiscount(restored.documentDiscount);
      if (restored.lines && restored.lines.length > 0) setLines(restored.lines);
    }
  };

  useEffect(() => {
    if ((location.state as { saved?: boolean })?.saved) {
      setSaveSuccessMessage(true);
      const timer = setTimeout(() => setSaveSuccessMessage(false), 3000);
      return () => clearTimeout(timer);
    }
  }, [location.state]);

  // Sync loaded draft into local state
  useEffect(() => {
    if (existingDraft) {
      setVersion(existingDraft.version);
      setPartyId(existingDraft.partyId);
      setSupplierInvoiceNumber(existingDraft.supplierInvoiceNumber ?? "");
      setDocumentDate(existingDraft.documentDate);
      setPostingDate(existingDraft.postingDate);
      setTaxDate(existingDraft.taxDate);
      setSupplyDate(existingDraft.supplyDate ?? "");
      setCurrencyCode(existingDraft.currencyCode);
      setPaymentTermId(existingDraft.paymentTermId ?? "");
      setDocumentDiscount(existingDraft.documentDiscountAmount || "0.00");
      if (existingDraft.lines && existingDraft.lines.length > 0) {
        setLines([...existingDraft.lines]);
      }
    }
  }, [existingDraft]);

  // Line change handlers
  const handleLineChange = useCallback((index: number, updated: InvoiceLineInput) => {
    setLines((prev) => {
      const copy = [...prev];
      copy[index] = updated;
      return copy;
    });
  }, []);

  const handleLineRemove = useCallback((index: number) => {
    setLines((prev) => (prev.length > 1 ? prev.filter((_, i) => i !== index) : prev));
  }, []);

  const handleAddLine = () => {
    setLines((prev) => [
      ...prev,
      {
        description: "",
        quantity: "1.000",
        unitCode: "EA",
        unitPrice: "0.00",
        discountAmount: "0.00"
      }
    ]);
  };

  // Approximate totals for immediate responsiveness
  const { localSubtotal, localGrandTotal } = useMemo(() => {
    let sub = 0;
    for (const l of lines) {
      const q = parseFloat(l.quantity) || 0;
      const p = parseFloat(l.unitPrice || "0") || 0;
      const d = parseFloat(l.discountAmount || "0") || 0;
      sub += Math.max(0, q * p - d);
    }
    const docDisc = parseFloat(documentDiscount) || 0;
    const finalNet = Math.max(0, sub - docDisc);
    const estGross = finalNet;
    return {
      localSubtotal: sub.toFixed(2),
      localGrandTotal: estGross.toFixed(2)
    };
  }, [lines, documentDiscount]);

  // Save Draft Handler
  const handleSaveDraft = async () => {
    setConcurrencyConflict(false);
    setSaveSuccessMessage(false);

    if (isEditing && id) {
      const updateReq: UpdateInvoiceDraftRequest = {
        version,
        partyId,
        supplierInvoiceNumber: isCustomer ? undefined : supplierInvoiceNumber || undefined,
        documentDate,
        postingDate,
        taxDate,
        supplyDate: supplyDate || undefined,
        currencyCode,
        paymentTermId: paymentTermId || undefined,
        documentDiscountAmount: documentDiscount,
        lines
      };

      try {
        if (isCustomer) {
          const res = await updateCustomerDraft.mutateAsync({ id, legalEntityId, request: updateReq });
          setVersion(res.version);
        } else {
          const res = await updateSupplierDraft.mutateAsync({ id, legalEntityId, request: updateReq });
          setVersion(res.version);
        }
        clearLocalDraft();
        setSaveSuccessMessage(true);
        setTimeout(() => setSaveSuccessMessage(false), 3000);
      } catch (err: unknown) {
        const status = (err as { status?: number })?.status;
        if (status === 409) {
          setConcurrencyConflict(true);
        }
      }
    } else {
      const createReq: CreateInvoiceDraftRequest = {
        legalEntityId,
        branchId: branchId || undefined,
        partyId,
        supplierInvoiceNumber: isCustomer ? undefined : supplierInvoiceNumber || undefined,
        documentDate,
        postingDate,
        taxDate,
        supplyDate: supplyDate || undefined,
        currencyCode,
        paymentTermId: paymentTermId || undefined,
        documentDiscountAmount: documentDiscount,
        lines
      };

      try {
        setDuplicateSupplierError(false);
        const res = isCustomer
          ? await createCustomerDraft.mutateAsync(createReq)
          : await createSupplierDraft.mutateAsync(createReq);
        clearLocalDraft();
        const routePrefix = isCustomer ? "/invoicing/customer-invoices" : "/invoicing/supplier-invoices";
        navigate(`${routePrefix}/${encodeURIComponent(res.id)}/edit`, { state: { saved: true } });
      } catch (err: unknown) {
        const error = err as { status?: number; problemDetails?: { extensions?: { code?: string } } };
        if (error?.status === 409 || error?.problemDetails?.extensions?.code === "invoicing.duplicate_supplier_invoice") {
          setDuplicateSupplierError(true);
        }
      }
    }
  };

  // Preview & Finalize Handler
  const handleOpenPreview = async () => {
    setIsPreviewLoading(true);
    setIsPreviewOpen(true);

    try {
      if (isEditing && id) {
        const preview = isCustomer
          ? await invoiceApi.previewCustomerDraft(id, legalEntityId)
          : await invoiceApi.previewSupplierDraft(id, legalEntityId);
        setPreviewData(preview);
      } else {
        const statelessReq: CreateInvoiceDraftRequest = {
          legalEntityId,
          branchId: branchId || undefined,
          partyId,
          supplierInvoiceNumber: isCustomer ? undefined : supplierInvoiceNumber || undefined,
          documentDate,
          postingDate,
          taxDate,
          supplyDate: supplyDate || undefined,
          currencyCode,
          paymentTermId: paymentTermId || undefined,
          documentDiscountAmount: documentDiscount,
          lines
        };
        const preview = isCustomer
          ? await invoiceApi.previewCustomerDraftStateless(statelessReq)
          : await invoiceApi.previewSupplierDraftStateless(statelessReq);
        setPreviewData(preview);
      }
    } catch {
      // preview error handled in modal
    } finally {
      setIsPreviewLoading(false);
    }
  };

  // Confirm Finalize Handler
  const handleConfirmFinalize = async () => {
    let targetDraftId = id;
    let targetVersion = version;

    // If draft was not saved yet, save it first
    if (!targetDraftId) {
      const createReq: CreateInvoiceDraftRequest = {
        legalEntityId,
        branchId: branchId || undefined,
        partyId,
        supplierInvoiceNumber: isCustomer ? undefined : supplierInvoiceNumber || undefined,
        documentDate,
        postingDate,
        taxDate,
        supplyDate: supplyDate || undefined,
        currencyCode,
        paymentTermId: paymentTermId || undefined,
        documentDiscountAmount: documentDiscount,
        lines
      };
      const created = isCustomer
        ? await createCustomerDraft.mutateAsync(createReq)
        : await createSupplierDraft.mutateAsync(createReq);
      targetDraftId = created.id;
      targetVersion = created.version;
    }

    const finReq = {
      version: targetVersion,
      idempotencyKey: `fin-${targetDraftId}-${Date.now()}`
    };

    clearLocalDraft();
    if (isCustomer) {
      const res = await finalizeCustomer.mutateAsync({ id: targetDraftId, legalEntityId, request: finReq });
      setIsPreviewOpen(false);
      const finalId = res?.invoiceId || (res as unknown as { id?: string })?.id || targetDraftId;
      navigate(`/invoicing/customer-invoices/${encodeURIComponent(finalId)}`);
    } else {
      const res = await finalizeSupplier.mutateAsync({ id: targetDraftId, legalEntityId, request: finReq });
      setIsPreviewOpen(false);
      const finalId = res?.invoiceId || (res as unknown as { id?: string })?.id || targetDraftId;
      navigate(`/invoicing/supplier-invoices/${encodeURIComponent(finalId)}`);
    }
  };

  // Cancel Draft Handler
  const handleCancelDraft = async (reason: string) => {
    clearLocalDraft();
    if (!id) {
      const routePrefix = isCustomer ? "/invoicing/customer-invoices" : "/invoicing/supplier-invoices";
      navigate(routePrefix);
      return;
    }

    if (isCustomer) {
      await cancelCustomerDraft.mutateAsync({ id, legalEntityId, request: { version, reason } });
      navigate("/invoicing/customer-invoices");
    } else {
      await cancelSupplierDraft.mutateAsync({ id, legalEntityId, request: { version, reason } });
      navigate("/invoicing/supplier-invoices");
    }
  };

  const isSaving =
    createCustomerDraft.isPending ||
    updateCustomerDraft.isPending ||
    createSupplierDraft.isPending ||
    updateSupplierDraft.isPending;

  const isFinalizing = finalizeCustomer.isPending || finalizeSupplier.isPending;

  useFastGridNavigation({
    containerRef: linesContainerRef,
    onAddRow: handleAddLine,
    onSave: handleSaveDraft
  });

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", padding: "1.25rem", maxWidth: "1280px", margin: "0 auto" }}>
      {/* Page Title & Breadcrumbs Header */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: "0.75rem" }}>
        <div>
          <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", fontSize: "0.8125rem", color: "var(--color-content-muted, #64748b)", marginBottom: "0.25rem" }}>
            <span style={{ cursor: "pointer" }} onClick={() => navigate("/invoicing")}>{t("title")}</span>
            <span>/</span>
            <span>{isCustomer ? t("tabs.customerInvoices") : t("tabs.supplierInvoices")}</span>
          </div>
          <h1 style={{ margin: 0, fontSize: "1.375rem", fontWeight: 800, color: "#102a43" }}>
            {isCustomer
              ? (isEditing ? t("draft.editCustomerTitle") : t("draft.newCustomerTitle"))
              : (isEditing ? t("draft.editSupplierTitle") : t("draft.newSupplierTitle"))}
          </h1>
          <p style={{ margin: "0.25rem 0 0", fontSize: "0.8125rem", color: "var(--color-content-secondary, #475569)" }}>
            {isCustomer
              ? (isEditing ? t("draft.editCustomerSubtitle") : t("draft.newCustomerSubtitle"))
              : (isEditing ? t("draft.editSupplierSubtitle") : t("draft.newSupplierSubtitle"))}
          </p>
        </div>

        {/* Top Actions */}
        <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", flexWrap: "wrap" }}>
          <Button variant="secondary" size="compact" onPress={() => navigate("/invoicing")}>
            {t("draft.backToList")}
          </Button>
          {isEditing && (
            <Button
              variant="danger"
              size="compact"
              onPress={() => setIsCancelDialogOpen(true)}
            >
              {t("draft.cancelDraft")}
            </Button>
          )}
          <Button
            variant="secondary"
            size="compact"
            onPress={handleSaveDraft}
            isDisabled={isSaving}
          >
            {isSaving ? t("draft.savingDraft") : t("draft.saveDraft")}
          </Button>
          <Button
            variant="primary"
            size="compact"
            onPress={handleOpenPreview}
            isDisabled={lines.length === 0 || !partyId}
          >
            {t("draft.previewAndFinalize")}
          </Button>
        </div>
      </div>

      {/* 0. Local Draft Auto-Recovery Banner */}
      {localDraft && !isEditing && (
        <DraftRecoveryBanner
          onRestore={handleRestoreDraft}
          onDiscard={discardLocalDraft}
          savedTimestamp={localDraft.timestamp}
        />
      )}

      {/* Concurrency Conflict Banner */}
      {concurrencyConflict && (
        <div
          style={{
            padding: "0.875rem 1.25rem",
            borderRadius: "var(--radius-md)",
            backgroundColor: "#fffbeb",
            border: "1px solid #fde68a",
            color: "#92400e",
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            gap: "1rem"
          }}
        >
          <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", fontSize: "0.875rem", fontWeight: 600 }}>
            <AlertCircleIcon size={18} />
            <span>{t("draft.concurrencyError")}</span>
          </div>
          <Button
            variant="secondary"
            size="compact"
            onPress={() => {
              if (isCustomer) void customerDraftQuery.refetch();
              else void supplierDraftQuery.refetch();
            }}
          >
            <RefreshIcon size={14} /> {t("draft.reloadDraft")}
          </Button>
        </div>
      )}

      {/* Duplicate Supplier Invoice Error Banner */}
      {duplicateSupplierError && (
        <div
          style={{
            padding: "0.875rem 1.25rem",
            borderRadius: "var(--radius-md)",
            backgroundColor: "#fef2f2",
            border: "1px solid #fecaca",
            color: "#b91c1c",
            display: "flex",
            alignItems: "center",
            gap: "0.75rem",
            fontSize: "0.875rem",
            fontWeight: 600
          }}
        >
          <AlertCircleIcon size={18} />
          <span>{t("errors.duplicateSupplierInvoice")}</span>
        </div>
      )}

      {/* Save Success Indicator */}
      {saveSuccessMessage && (
        <div
          style={{
            padding: "0.625rem 1rem",
            borderRadius: "var(--radius-md)",
            backgroundColor: "#ecfdf5",
            border: "1px solid #a7f3d0",
            color: "#065f46",
            fontSize: "0.8125rem",
            fontWeight: 600,
            display: "flex",
            alignItems: "center",
            gap: "0.375rem"
          }}
        >
          <CheckIcon size={16} />
          <span>{t("draft.savedStatus")}</span>
        </div>
      )}

      {/* 1. Header & Party Surface */}
      <Surface title={isCustomer ? t("draft.customerParty") : t("draft.supplierParty")}>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "1rem" }}>
          {/* Party Selection */}
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 700, color: "#334155", marginBottom: "0.25rem" }}>
              {isCustomer ? t("draft.customerParty") : t("draft.supplierParty")} <span style={{ color: "#ef4444" }}>*</span>
            </label>
            <PartyPicker
              label={isCustomer ? t("draft.customerParty") : t("draft.supplierParty")}
              value={partyId || undefined}
              onChange={(id) => setPartyId(id)}
              roleFilter={isCustomer ? "Customer" : "Supplier"}
              placeholder={isCustomer ? t("draft.selectCustomer") : t("draft.selectSupplier")}
              enableQuickCreate={false}
              isRequired
            />
          </div>

          {/* Supplier External Invoice Number (For AP only) */}
          {!isCustomer && (
            <div>
              <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 700, color: "#334155", marginBottom: "0.25rem" }}>
                {t("draft.supplierInvoiceNumber")} <span style={{ color: "#ef4444" }}>*</span>
              </label>
              <TextField
                size="compact"
                value={supplierInvoiceNumber}
                onChange={setSupplierInvoiceNumber}
                placeholder={t("draft.supplierInvoiceNumberPlaceholder")}
                aria-label={t("draft.supplierInvoiceNumber")}
                isRequired
              />
            </div>
          )}

          {/* Document Date */}
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 700, color: "#334155", marginBottom: "0.25rem" }}>
              {t("draft.documentDate")} <span style={{ color: "#ef4444" }}>*</span>
            </label>
            <LocalDateField
              size="compact"
              value={documentDate}
              onChange={setDocumentDate}
              aria-label={t("draft.documentDate")}
              isRequired
            />
          </div>

          {/* Currency */}
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 700, color: "#334155", marginBottom: "0.25rem" }}>
              {t("draft.currency")}
            </label>
            <Select
              size="compact"
              aria-label={t("draft.currency")}
              selectedKey={currencyCode}
              onSelectionChange={(key) => setCurrencyCode(String(key ?? "SAR") as CurrencyCode)}
            >
              <SelectItem id="SAR">{t("draft.currencySar")}</SelectItem>
              <SelectItem id="EGP">{t("draft.currencyEgp")}</SelectItem>
              <SelectItem id="AED">{t("draft.currencyAed")}</SelectItem>
              <SelectItem id="USD">{t("draft.currencyUsd")}</SelectItem>
            </Select>
          </div>
        </div>

        {/* Collapsible Advanced Dates Toggle */}
        <div style={{ marginTop: "1rem", borderTop: "1px solid #f1f5f9", paddingTop: "0.75rem" }}>
          <button
            type="button"
            onClick={() => setShowAdvancedDates(!showAdvancedDates)}
            style={{
              background: "none",
              border: "none",
              color: "#3b82f6",
              fontSize: "0.8125rem",
              fontWeight: 600,
              cursor: "pointer",
              padding: 0
            }}
          >
            {showAdvancedDates ? t("draft.hideAdvancedDates") : t("draft.showAdvancedDates")}
          </button>

          {showAdvancedDates && (
            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "1rem", marginTop: "0.75rem" }}>
              <div>
                <label style={{ display: "block", fontSize: "0.75rem", fontWeight: 700, color: "#475569", marginBottom: "0.25rem" }}>
                  {t("draft.postingDate")}
                </label>
                <LocalDateField
                  size="compact"
                  value={postingDate}
                  onChange={setPostingDate}
                  isDisabled={!perms.canOverrideDate}
                  aria-label={t("draft.postingDate")}
                />
              </div>
              <div>
                <label style={{ display: "block", fontSize: "0.75rem", fontWeight: 700, color: "#475569", marginBottom: "0.25rem" }}>
                  {t("draft.taxDate")}
                </label>
                <LocalDateField
                  size="compact"
                  value={taxDate}
                  onChange={setTaxDate}
                  isDisabled={!perms.canOverrideDate}
                  aria-label={t("draft.taxDate")}
                />
              </div>
              <div>
                <label style={{ display: "block", fontSize: "0.75rem", fontWeight: 700, color: "#475569", marginBottom: "0.25rem" }}>
                  {t("draft.supplyDate")}
                </label>
                <LocalDateField
                  size="compact"
                  value={supplyDate}
                  onChange={setSupplyDate}
                  aria-label={t("draft.supplyDate")}
                />
              </div>
            </div>
          )}
        </div>
      </Surface>

      {/* 2. Line Items Surface */}
      <Surface
        title={t("draft.linesTitle")}
        headerActions={
          <Button variant="secondary" size="compact" onPress={handleAddLine}>
            {t("draft.addLine")}
          </Button>
        }
      >
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "0.75rem" }}>
          <p style={{ margin: 0, fontSize: "0.8125rem", color: "var(--color-content-secondary, #475569)" }}>
            {t("draft.linesSubtitle")}
          </p>
          <span className="ui-scroll-hint" style={{ fontSize: "0.75rem", color: "#64748b" }}>
            ⟵ {t("draft.scrollHint")} ⟶
          </span>
        </div>

        {/* Lines Header & List with Responsive Horizontal Scroll */}
        <div style={{ overflowX: "auto", width: "100%" }}>
          <div ref={linesContainerRef} style={{ minWidth: "720px" }}>
            {/* Lines Header */}
            <div
              style={{
                display: "grid",
                gridTemplateColumns: "minmax(240px, 2fr) 90px 80px 130px 110px 120px 40px",
                gap: "0.5rem",
                padding: "0.5rem 0.75rem",
                backgroundColor: "#102a43",
                color: "#ffffff",
                borderRadius: "var(--radius-sm, 0.25rem) var(--radius-sm, 0.25rem) 0 0",
                fontSize: "0.75rem",
                fontWeight: 700
              }}
            >
              <div>{t("draft.description")}</div>
              <div>{t("draft.quantity")}</div>
              <div style={{ textAlign: "center" }}>{t("draft.unit")}</div>
              <div>{t("draft.unitPrice")}</div>
              <div>{t("draft.discount")}</div>
              <div style={{ textAlign: "end" }}>{t("draft.totalAmount")}</div>
              <div></div>
            </div>

            {/* Lines List */}
            <div style={{ border: "1px solid #cbd5e1", borderTop: "none", borderRadius: "0 0 var(--radius-md) var(--radius-md)", overflow: "hidden" }}>
              {lines.map((line, index) => (
                <InvoiceLineEditor
                  key={index}
                  line={line}
                  index={index}
                  currencyCode={currencyCode}
                  canOverridePrice={perms.canOverridePrice}
                  onChange={handleLineChange}
                  onRemove={handleLineRemove}
                  onShowWhyPrice={(idx) => setWhyPriceIndex(idx)}
                />
              ))}
            </div>
          </div>
        </div>

        {/* Add Line Action Footer */}
        <div style={{ marginTop: "0.75rem" }}>
          <Button variant="ghost" size="compact" onPress={handleAddLine}>
            {t("draft.addLine")}
          </Button>
        </div>
      </Surface>

      {/* 3. Document-Level Discount & Summary */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(300px, 1fr))", gap: "1.25rem", alignItems: "start" }}>
        {/* Document Discount Box */}
        <Surface title={t("draft.chargesAndDiscountsTitle")}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 700, color: "#334155", marginBottom: "0.25rem" }}>
              {t("draft.documentDiscount")}
            </label>
            <TextField
              size="compact"
              value={documentDiscount}
              onChange={setDocumentDiscount}
              placeholder="0.00"
              aria-label={t("draft.documentDiscount")}
            />
            <p style={{ margin: "0.375rem 0 0", fontSize: "0.75rem", color: "var(--color-content-muted, #64748b)" }}>
              {t("draft.documentDiscountNotice")}
            </p>
          </div>
        </Surface>

        {/* Authoritative Financial Totals Card */}
        <InvoiceTotalsCard
          totals={previewData?.totals}
          fallbackCurrency={currencyCode}
          localSubtotal={localSubtotal}
          localGrandTotal={localGrandTotal}
          localDocumentDiscount={documentDiscount}
        />
      </div>

      {/* 4. Bottom Actions Bar */}
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          padding: "1rem",
          backgroundColor: "#ffffff",
          border: "1px solid #cbd5e1",
          borderRadius: "var(--radius-lg, 0.75rem)",
          boxShadow: "0 1px 3px 0 rgba(0, 0, 0, 0.05)"
        }}
      >
        <Button variant="secondary" onPress={() => navigate("/invoicing")}>
          {t("draft.backToList")}
        </Button>

        <div style={{ display: "flex", gap: "0.75rem" }}>
          <Button
            variant="secondary"
            onPress={handleSaveDraft}
            isDisabled={isSaving}
          >
            {isSaving ? t("draft.savingDraft") : t("draft.saveDraft")}
          </Button>
          <Button
            variant="primary"
            onPress={handleOpenPreview}
            isDisabled={lines.length === 0 || !partyId}
          >
            {t("draft.previewAndFinalize")}
          </Button>
        </div>
      </div>

      {/* Preview & Finalization Modal */}
      <InvoicePreviewModal
        isOpen={isPreviewOpen}
        onClose={() => setIsPreviewOpen(false)}
        preview={previewData}
        isLoading={isPreviewLoading}
        isFinalizing={isFinalizing}
        onConfirmFinalize={handleConfirmFinalize}
        currencyCode={currencyCode}
        hasAccountingPreviewPermission={perms.canPreviewAccounting}
      />

      {/* Cancel Draft Confirmation Dialog */}
      <CancelDraftDialog
        isOpen={isCancelDialogOpen}
        onClose={() => setIsCancelDialogOpen(false)}
        onConfirm={handleCancelDraft}
      />

      {/* Why This Price Modal */}
      {whyPriceIndex !== null && (
        <Dialog
          isOpen={true}
          onOpenChange={(open) => { if (!open) setWhyPriceIndex(null); }}
          title={t("draft.priceExplanationTitle")}
          size="medium"
        >
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem", padding: "0.5rem 0" }}>
            <p style={{ margin: 0, fontSize: "0.875rem", color: "#334155" }}>
              {previewData?.lines?.[whyPriceIndex]?.priceExplanation || t("draft.noPriceExplanation")}
            </p>
            <div style={{ display: "flex", justifyContent: "flex-end" }}>
              <Button variant="secondary" size="compact" onPress={() => setWhyPriceIndex(null)}>
                {t("draft.priceExplanationClose")}
              </Button>
            </div>
          </div>
        </Dialog>
      )}
    </div>
  );
}
