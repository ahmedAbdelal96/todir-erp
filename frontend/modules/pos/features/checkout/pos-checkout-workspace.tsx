import React, { useState, useEffect, useCallback } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { BarcodeIcon, CashRegisterIcon, DrawerIcon, UserWalkInIcon, OfflineIcon, SyncIcon } from "../../components/pos-icons.tsx";
import { BidiText } from "../../../../components/primitives/bidi-text.tsx";
import { PosCartLinesTable } from "./pos-cart-lines-table.tsx";
import { PosCartTotalsPanel } from "./pos-cart-totals-panel.tsx";
import { CustomerSelectModal } from "../../components/customer-select-modal.tsx";
import { DiscountModal } from "../../components/discount-modal.tsx";
import { TenderModal } from "../../components/tender-modal.tsx";
import { ReceiptModal } from "../../components/receipt-modal.tsx";
import { CashMovementModal } from "../../components/cash-movement-modal.tsx";
import { usePosScanner } from "../../hooks/use-pos-scanner.ts";
import { usePosOffline } from "../../hooks/use-pos-offline.ts";
import {
  useCartDetailsQuery,
  useCreateCartMutation,
  useScanCartLineMutation,
  useAddCartLineMutation,
  useUpdateCartLineMutation,
  useRemoveCartLineMutation,
  useSetCartCustomerMutation,
  useDiscountCartLineMutation,
  useHoldCartMutation,
  useResumeCartMutation,
  useCancelCartMutation,
  useCheckoutMutation,
  useAddCashMovementMutation,
  useCatalogSearchQuery,
} from "../../hooks/use-pos-queries.ts";
import type {
  CartLineResponse,
  CheckoutResponse,
  TenderRequest,
  CashMovementType,
  SessionResponse,
  RegisterResponse,
} from "../../types.ts";

export interface PosCheckoutWorkspaceProps {
  readonly legalEntityId: string;
  readonly register: RegisterResponse;
  readonly session: SessionResponse;
  readonly onOpenSessionRequired?: (() => void) | undefined;
  readonly onManageSessions?: (() => void) | undefined;
}

export function PosCheckoutWorkspace({
  legalEntityId,
  register,
  session,
  onManageSessions,
}: PosCheckoutWorkspaceProps): React.JSX.Element {
  const { t } = useTranslation("pos");

  // Cart state
  const [cartId, setCartId] = useState<string | null>(null);
  const [customerPartyId, setCustomerPartyId] = useState<string | null>(null);
  const [customerName, setCustomerName] = useState<string | null>(null);

  // Barcode / Search input
  const [barcodeInput, setBarcodeInput] = useState<string>("");
  const [searchQuery, setSearchQuery] = useState<string>("");
  const [statusMessage, setStatusMessage] = useState<string | null>(null);

  // Modals state
  const [isCustomerModalOpen, setIsCustomerModalOpen] = useState(false);
  const [isTenderModalOpen, setIsTenderModalOpen] = useState(false);
  const [isReceiptModalOpen, setIsReceiptModalOpen] = useState(false);
  const [isMovementModalOpen, setIsMovementModalOpen] = useState(false);
  const [discountLine, setDiscountLine] = useState<CartLineResponse | null>(null);

  // Completed checkout details for receipt
  const [completedCheckout, setCompletedCheckout] = useState<CheckoutResponse | null>(null);

  // Mutations
  const createCartMutation = useCreateCartMutation();
  const scanMutation = useScanCartLineMutation();
  const addLineMutation = useAddCartLineMutation();
  const updateLineMutation = useUpdateCartLineMutation();
  const removeLineMutation = useRemoveCartLineMutation();
  const setCustomerMutation = useSetCartCustomerMutation();
  const discountMutation = useDiscountCartLineMutation();
  const holdMutation = useHoldCartMutation();
  const resumeMutation = useResumeCartMutation();
  const cancelMutation = useCancelCartMutation();
  const checkoutMutation = useCheckoutMutation();
  const cashMovementMutation = useAddCashMovementMutation();

  // Queries
  const { data: cartDetails } = useCartDetailsQuery(cartId);
  const { data: catalogResults } = useCatalogSearchQuery(legalEntityId, searchQuery, 1, 10);

  // Offline Hook
  const { isOnline, pendingSyncCount, enqueueOfflineSale } = usePosOffline();

  // Initialize Cart if none exists
  useEffect(() => {
    if (!cartId && session?.id) {
      createCartMutation.mutateAsync({
        sessionId: session.id,
        currencyCode: register.currencyCode || "EGP",
        customerPartyId: customerPartyId ?? null,
      }).then((newCart) => {
        setCartId(newCart.id);
      }).catch((err) => {
        setStatusMessage(err instanceof Error ? err.message : "Failed to create cart");
      });
    }
  }, [cartId, session?.id, register.currencyCode, customerPartyId]);

  // Handle barcode scanned from physical scanner or manual input
  const handleBarcodeScanned = useCallback(async (code: string) => {
    if (!cartId || !code.trim()) return;
    setStatusMessage(null);
    try {
      await scanMutation.mutateAsync({
        cartId,
        request: {
          skuOrBarcode: code.trim(),
          quantity: 1,
          legalEntityId,
          priceListId: "4537199e-5f67-f251-8e46-8525a8f06144",
        },
      });
      setBarcodeInput("");
      setStatusMessage(`${t("workspace.scanned")}: ${code.trim()}`);
    } catch (err: unknown) {
      setStatusMessage(err instanceof Error ? err.message : t("workspace.scanFailed"));
    }
  }, [cartId, legalEntityId, scanMutation, t]);

  // Keyboard barcode scanner hook listener
  usePosScanner({
    onScan: handleBarcodeScanned,
    enabled: Boolean(cartId) && !isTenderModalOpen && !isReceiptModalOpen,
  });

  // Quantity updates
  const handleUpdateQuantity = async (lineId: string, currentQty: number, delta: number, version: number) => {
    if (!cartId) return;
    const newQty = currentQty + delta;
    if (newQty <= 0) return;
    try {
      await updateLineMutation.mutateAsync({
        cartId,
        lineId,
        request: {
          quantity: newQty,
          discountAmount: 0,
          expectedVersion: version,
        },
      });
    } catch (err: unknown) {
      setStatusMessage(err instanceof Error ? err.message : "Update failed");
    }
  };

  // Remove line
  const handleRemoveLine = async (lineId: string, version: number) => {
    if (!cartId) return;
    try {
      await removeLineMutation.mutateAsync({
        cartId,
        lineId,
        expectedVersion: version,
      });
    } catch (err: unknown) {
      setStatusMessage(err instanceof Error ? err.message : "Remove failed");
    }
  };

  // Line Discount
  const handleApplyDiscount = async (amount: number) => {
    if (!cartId || !discountLine || !cartDetails) return;
    try {
      await discountMutation.mutateAsync({
        cartId,
        request: {
          lineId: discountLine.id,
          amount,
          expectedVersion: cartDetails.cart.version,
        },
      });
      setDiscountLine(null);
    } catch (err: unknown) {
      setStatusMessage(err instanceof Error ? err.message : "Discount failed");
    }
  };

  // Customer Selection
  const handleSelectCustomer = async (partyId: string | null, partyName: string | null) => {
    setCustomerPartyId(partyId);
    setCustomerName(partyName);
    if (cartId) {
      try {
        await setCustomerMutation.mutateAsync({
          cartId,
          request: { customerPartyId: partyId },
        });
      } catch (err: unknown) {
        setStatusMessage(err instanceof Error ? err.message : "Customer update failed");
      }
    }
  };

  // Hold & Resume
  const handleHoldCart = async () => {
    if (!cartId || !cartDetails) return;
    if (cartDetails.cart.state === "Held") {
      await resumeMutation.mutateAsync(cartId);
    } else {
      await holdMutation.mutateAsync(cartId);
    }
  };

  // Cancel Cart
  const handleCancelCart = async () => {
    if (!cartId) return;
    await cancelMutation.mutateAsync(cartId);
    setCartId(null);
  };

  // Checkout with tenders
  const handleCheckout = async (tenders: readonly TenderRequest[]) => {
    if (!cartId) return;
    try {
      const res = await checkoutMutation.mutateAsync({
        cartId,
        request: {
          idempotencyKey: window.crypto?.randomUUID ? window.crypto.randomUUID() : String(Date.now()),
          tenders,
        },
      });
      setCompletedCheckout(res);
      setIsTenderModalOpen(false);
      setIsReceiptModalOpen(true);
    } catch (err: unknown) {
      // If offline or network failure, queue offline sale
      if (!isOnline && tenders.every((t) => t.method === "Cash")) {
        const lines = (cartDetails?.lines || []).map((l) => ({
          variantId: l.variantId,
          description: l.description,
          uomCode: l.uomCode,
          quantity: l.quantity,
          unitPrice: l.unitPrice,
          taxAmount: l.taxAmount,
          discountAmount: l.discountAmount,
          legalEntityId,
          productTemplateId: "00000000-0000-0000-0000-000000000000",
          unitOfMeasureId: "00000000-0000-0000-0000-000000000000",
          priceListId: "00000000-0000-0000-0000-000000000000",
          pricingDate: new Date().toISOString().slice(0, 10),
        }));

        await enqueueOfflineSale({
          registerId: register.id,
          deviceIdentity: "POS-WEB-COUNTER",
          sessionId: session.id,
          currencyCode: register.currencyCode || "EGP",
          paymentAccountId: "00000000-0000-0000-0000-000000000000",
          customerPartyId: customerPartyId ?? undefined,
          lines,
        });

        setIsTenderModalOpen(false);
        setStatusMessage(t("workspace.offlineQueued"));
        setCartId(null);
      } else {
        throw err;
      }
    }
  };

  // Cash Movement
  const handleCashMovement = async (type: CashMovementType, amount: number, reason: string) => {
    if (!session?.id) return;
    await cashMovementMutation.mutateAsync({
      sessionId: session.id,
      request: {
        sessionId: session.id,
        movementType: type,
        amount,
        currencyCode: register.currencyCode || "EGP",
        reason,
      },
    });
    setStatusMessage(t("workspace.movementRecorded"));
  };

  // Open Drawer trigger
  const handleOpenDrawer = () => {
    setStatusMessage(t("workspace.drawerOpened"));
  };

  // Reset after completed receipt
  const handleNewSale = () => {
    setCartId(null);
    setCompletedCheckout(null);
    setCustomerPartyId(null);
    setCustomerName(null);
  };

  const lines = cartDetails?.lines || [];
  const subtotal = cartDetails?.cart?.subtotal || 0;
  const taxTotal = cartDetails?.cart?.taxTotal || 0;
  const discountTotal = cartDetails?.cart?.discountTotal || 0;
  const grandTotal = cartDetails?.cart?.grandTotal || 0;
  const cartVersion = cartDetails?.cart?.version || 1;
  const isHeld = cartDetails?.cart?.state === "Held";

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1rem", minHeight: "80vh" }}>
      {/* Top Workspace Header Bar */}
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: "0.75rem",
          padding: "0.875rem 1.25rem",
          backgroundColor: "#102a43",
          color: "#ffffff",
          borderRadius: "var(--radius-lg)",
          boxShadow: "0 2px 4px 0 rgba(0, 0, 0, 0.1)",
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: "1rem" }}>
          <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
            <CashRegisterIcon size={24} style={{ color: "#60a5fa" }} />
            <div>
              <div style={{ fontWeight: 800, fontSize: "1rem", lineHeight: 1.2 }}>
                <BidiText>{register.name}</BidiText>
              </div>
              <div style={{ fontSize: "0.75rem", color: "#94a3b8" }}>
                {t("workspace.code")}: <BidiText>{register.code}</BidiText>
              </div>
            </div>
          </div>

          <div style={{ height: "1.75rem", width: "1px", backgroundColor: "#334e68" }} />

          {/* Customer selector pill */}
          <button
            type="button"
            onClick={() => setIsCustomerModalOpen(true)}
            style={{
              display: "flex",
              alignItems: "center",
              gap: "0.5rem",
              background: "rgba(255, 255, 255, 0.1)",
              border: "1px solid rgba(255, 255, 255, 0.2)",
              color: "#ffffff",
              padding: "0.35rem 0.75rem",
              borderRadius: "var(--radius-full)",
              cursor: "pointer",
              fontSize: "0.8125rem",
              fontWeight: 600,
            }}
          >
            <UserWalkInIcon size={16} />
            <span>{customerName ? <BidiText>{customerName}</BidiText> : t("workspace.walkInCustomer")}</span>
          </button>
        </div>

        {/* Right side operational actions */}
        <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
          {/* Offline / Online Status */}
          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: "0.35rem",
              fontSize: "0.75rem",
              padding: "0.35rem 0.6rem",
              borderRadius: "var(--radius-sm)",
              backgroundColor: isOnline ? "rgba(16, 185, 129, 0.15)" : "rgba(239, 68, 68, 0.2)",
              color: isOnline ? "#34d399" : "#f87171",
            }}
          >
            {isOnline ? <SyncIcon size={14} /> : <OfflineIcon size={14} />}
            <span>{isOnline ? t("workspace.online") : t("workspace.offline")}</span>
            {pendingSyncCount > 0 && (
              <span style={{ fontWeight: 700, marginInlineStart: "0.25rem" }}>
                (<BidiText>{pendingSyncCount}</BidiText>)
              </span>
            )}
          </div>

          <Button
            size="compact"
            variant="secondary"
            onClick={handleOpenDrawer}
            style={{ color: "#ffffff", borderColor: "#334e68", display: "flex", gap: "0.35rem", alignItems: "center" }}
          >
            <DrawerIcon size={16} />
            <span>{t("workspace.openDrawer")}</span>
          </Button>

          <Button
            size="compact"
            variant="secondary"
            onClick={() => setIsMovementModalOpen(true)}
            style={{ color: "#ffffff", borderColor: "#334e68" }}
          >
            {t("workspace.cashMovement")}
          </Button>

          {onManageSessions && (
            <Button
              size="compact"
              variant="secondary"
              onClick={onManageSessions}
              style={{ color: "#ffffff", borderColor: "#334e68" }}
            >
              {t("workspace.sessionOps")}
            </Button>
          )}
        </div>
      </div>

      {/* Barcode & Manual Product Lookup Bar */}
      <div
        style={{
          display: "flex",
          gap: "0.75rem",
          alignItems: "center",
          backgroundColor: "#ffffff",
          padding: "0.875rem 1.25rem",
          borderRadius: "var(--radius-lg)",
          border: "1px solid #cbd5e1",
          boxShadow: "0 1px 3px 0 rgba(0, 0, 0, 0.05)",
        }}
      >
        <div style={{ flex: 1, display: "flex", gap: "0.5rem" }}>
          <div style={{ flex: 1 }}>
            <TextField
              placeholder={t("workspace.scanOrTypeBarcode")}
              value={barcodeInput}
              onChange={(val) => setBarcodeInput(val)}
              onKeyDown={(e) => {
                if (e.key === "Enter" && barcodeInput.trim()) {
                  e.preventDefault();
                  handleBarcodeScanned(barcodeInput);
                }
              }}
              autoFocus
            />
          </div>

          <Button
            variant="primary"
            isDisabled={!barcodeInput.trim()}
            onClick={() => handleBarcodeScanned(barcodeInput)}
            style={{ display: "flex", gap: "0.35rem", alignItems: "center" }}
          >
            <BarcodeIcon size={18} />
            <span>{t("workspace.add")}</span>
          </Button>
        </div>

        {/* Quick Product Search dropdown input */}
        <div style={{ width: "16rem" }}>
          <TextField
            placeholder={t("workspace.searchCatalog")}
            value={searchQuery}
            onChange={(val) => setSearchQuery(val)}
          />
        </div>
      </div>

      {/* Quick fast-moving retail items bar */}
      <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap", alignItems: "center" }}>
        <span style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)", fontWeight: 600 }}>
          {t("workspace.quickItems")}:
        </span>
        <Button
          size="compact"
          variant="secondary"
          onClick={() => handleBarcodeScanned("EG-1-0002")}
          style={{ fontSize: "0.8125rem" }}
        >
          {t("workspace.quickItemCable")}
        </Button>
        <Button
          size="compact"
          variant="secondary"
          onClick={() => handleBarcodeScanned("EG-2-0001")}
          style={{ fontSize: "0.8125rem" }}
        >
          {t("workspace.quickItemBreaker")}
        </Button>
        <Button
          size="compact"
          variant="secondary"
          onClick={() => handleBarcodeScanned("EG-1-0004")}
          style={{ fontSize: "0.8125rem" }}
        >
          {t("workspace.quickItemTransformer")}
        </Button>
      </div>

      {/* Quick Catalog Results Preview if query typed */}
      {searchQuery.trim().length > 1 && catalogResults?.items && (
        <div
          style={{
            backgroundColor: "#ffffff",
            borderRadius: "var(--radius-md)",
            border: "1px solid #cbd5e1",
            padding: "0.5rem",
            maxHeight: "12rem",
            overflowY: "auto",
            boxShadow: "0 4px 6px -1px rgba(0, 0, 0, 0.1)",
          }}
        >
          {catalogResults.items.map((item) => (
            <div
              key={item.variantId}
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                padding: "0.5rem 0.75rem",
                borderBottom: "1px solid #f1f5f9",
                cursor: "pointer",
              }}
              onClick={async () => {
                if (!cartId) return;
                await addLineMutation.mutateAsync({
                  cartId,
                  request: {
                    variantId: item.variantId,
                    description: item.displayName,
                    uomCode: item.uomCode,
                    quantity: 1,
                    unitPrice: item.unitPrice,
                  },
                });
                setSearchQuery("");
              }}
            >
              <div>
                <strong style={{ color: "var(--color-content-default)" }}><BidiText>{item.displayName}</BidiText></strong>
                <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", marginInlineStart: "0.5rem" }}>
                  <BidiText>{item.sku}</BidiText>
                </span>
              </div>
              <strong style={{ color: "var(--color-brand-primary)" }}>
                <BidiText>{item.unitPrice.toFixed(2)} {register.currencyCode}</BidiText>
              </strong>
            </div>
          ))}
        </div>
      )}

      {/* Notification / Status Message */}
      {statusMessage && (
        <div
          role="status"
          style={{
            padding: "0.6rem 1rem",
            borderRadius: "var(--radius-md)",
            backgroundColor: "rgba(59, 130, 246, 0.1)",
            border: "1px solid rgba(59, 130, 246, 0.3)",
            color: "var(--color-brand-primary)",
            fontSize: "0.875rem",
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
          }}
        >
          <span>{statusMessage}</span>
          <button
            type="button"
            onClick={() => setStatusMessage(null)}
            style={{ background: "none", border: "none", cursor: "pointer", fontSize: "1rem" }}
          >
            ×
          </button>
        </div>
      )}

      {/* Main Dual-Column Grid */}
      <div
        style={{
          display: "grid",
          gridTemplateColumns: "1fr 22rem",
          gap: "1.25rem",
          alignItems: "start",
        }}
      >
        {/* Left Column: Cart Lines Table */}
        <div>
          <PosCartLinesTable
            lines={lines}
            currencyCode={register.currencyCode}
            onUpdateQuantity={handleUpdateQuantity}
            onOpenDiscount={(line) => setDiscountLine(line)}
            onRemoveLine={handleRemoveLine}
            cartVersion={cartVersion}
            disabled={isHeld}
          />
        </div>

        {/* Right Column: Totals & Checkout Panel */}
        <div>
          <PosCartTotalsPanel
            subtotal={subtotal}
            taxTotal={taxTotal}
            discountTotal={discountTotal}
            grandTotal={grandTotal}
            currencyCode={register.currencyCode}
            onOpenTender={() => setIsTenderModalOpen(true)}
            onHoldCart={handleHoldCart}
            onCancelCart={handleCancelCart}
            isHeld={isHeld}
            lineCount={lines.length}
          />
        </div>
      </div>

      {/* Modals */}
      <CustomerSelectModal
        isOpen={isCustomerModalOpen}
        onClose={() => setIsCustomerModalOpen(false)}
        onSelect={handleSelectCustomer}
        currentPartyId={customerPartyId}
        currentPartyName={customerName}
      />

      {discountLine && (
        <DiscountModal
          isOpen={Boolean(discountLine)}
          onClose={() => setDiscountLine(null)}
          onApply={handleApplyDiscount}
          lineDescription={discountLine.description}
          lineTotal={discountLine.lineTotal}
          currentDiscount={discountLine.discountAmount}
          currencyCode={register.currencyCode}
        />
      )}

      <TenderModal
        isOpen={isTenderModalOpen}
        onClose={() => setIsTenderModalOpen(false)}
        totalAmount={grandTotal}
        currencyCode={register.currencyCode}
        onCheckout={handleCheckout}
        isSubmitting={checkoutMutation.isPending}
      />

      <ReceiptModal
        isOpen={isReceiptModalOpen}
        onClose={() => setIsReceiptModalOpen(false)}
        checkout={completedCheckout}
        lines={lines}
        subtotal={subtotal}
        taxTotal={taxTotal}
        discountTotal={discountTotal}
        grandTotal={grandTotal}
        currencyCode={register.currencyCode}
        registerCode={register.code}
        onNewSale={handleNewSale}
      />

      <CashMovementModal
        isOpen={isMovementModalOpen}
        onClose={() => setIsMovementModalOpen(false)}
        onConfirm={handleCashMovement}
        currencyCode={register.currencyCode}
        isSubmitting={cashMovementMutation.isPending}
      />
    </div>
  );
}
