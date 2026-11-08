import React, { useState, useEffect, useCallback } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import {
  BarcodeIcon,
  CashRegisterIcon,
  DrawerIcon,
  UserWalkInIcon,
  OfflineIcon,
  SyncIcon,
} from "../../components/pos-icons.tsx";
import { BidiText } from "../../../../components/primitives/bidi-text.tsx";
import { PosCartLinesTable } from "./pos-cart-lines-table.tsx";
import { PosCartTotalsPanel } from "./pos-cart-totals-panel.tsx";
import { CustomerSelectModal } from "../../components/customer-select-modal.tsx";
import { DiscountModal } from "../../components/discount-modal.tsx";
import { TenderModal } from "../../components/tender-modal.tsx";
import { ReceiptModal } from "../../components/receipt-modal.tsx";
import { CashMovementModal } from "../../components/cash-movement-modal.tsx";
import { ManagerOverrideModal } from "../../components/manager-override-modal.tsx";
import { useOptionalUserContext } from "../../../../core/runtime/index.ts";
import { posApi } from "../../services/pos-api.ts";
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
  usePaymentAccountsQuery,
  usePriceListsQuery,
  usePaymentRequestsQuery,
  useCreateManagerOverrideMutation,
  useRecoverCheckoutMutation,
  useRegisterTerminalsQuery,
} from "../../hooks/use-pos-queries.ts";
import type {
  CartLineResponse,
  CheckoutResponse,
  PosSaleLineDetail,
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

  // Stable idempotency key per checkout session
  const [checkoutIdempotencyKey, setCheckoutIdempotencyKey] = useState<string>(() =>
    window.crypto?.randomUUID ? window.crypto.randomUUID() : `checkout-${Date.now()}`
  );

  // Barcode / Search input
  const [barcodeInput, setBarcodeInput] = useState<string>("");
  const [searchQuery, setSearchQuery] = useState<string>("");
  const [statusMessage, setStatusMessage] = useState<string | null>(null);

  // Modals state
  const [isCustomerModalOpen, setIsCustomerModalOpen] = useState(false);
  const [isTenderModalOpen, setIsTenderModalOpen] = useState(false);
  const [isReceiptModalOpen, setIsReceiptModalOpen] = useState(false);
  const [isMovementModalOpen, setIsMovementModalOpen] = useState(false);
  const [isOverrideModalOpen, setIsOverrideModalOpen] = useState(false);
  const [discountLine, setDiscountLine] = useState<CartLineResponse | null>(null);
  const [pendingDiscountAmount, setPendingDiscountAmount] = useState<number | null>(null);

  // Completed checkout details for receipt
  const [completedCheckout, setCompletedCheckout] = useState<CheckoutResponse | null>(null);
  const [frozenReceiptLines, setFrozenReceiptLines] = useState<readonly CartLineResponse[]>([]);
  const [authoritativeReceiptNumber, setAuthoritativeReceiptNumber] = useState<string | null>(null);
  const [authoritativeBusinessDate, setAuthoritativeBusinessDate] = useState<string | null>(null);
  const [authoritativeSale, setAuthoritativeSale] = useState<{
    readonly subtotal: number;
    readonly taxTotal: number;
    readonly discountTotal: number;
    readonly grandTotal: number;
  } | null>(null);
  const [authoritativeLines, setAuthoritativeLines] = useState<readonly PosSaleLineDetail[] | null>(null);

  // Authenticated user & human cashier name
  const user = useOptionalUserContext();
  const cashierDisplayName =
    user?.displayName ||
    user?.email ||
    t("workspace.cashierOnDuty", "أمين الصندوق المناوب");

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
  const createOverrideMutation = useCreateManagerOverrideMutation();
  const recoverCheckoutMutation = useRecoverCheckoutMutation();

  // Queries
  const { data: cartDetails, refetch: refetchCart } = useCartDetailsQuery(cartId);
  const { data: catalogResults } = useCatalogSearchQuery(legalEntityId, searchQuery, 1, 10);
  const { data: paymentAccountsData } = usePaymentAccountsQuery(legalEntityId);
  const { data: priceListsData } = usePriceListsQuery(legalEntityId);
  const { data: paymentRequestsData } = usePaymentRequestsQuery(legalEntityId);
  const { data: terminalsData } = useRegisterTerminalsQuery(register.id);

  const activeTerminal = terminalsData?.find((t) => t.status === "Ready") || terminalsData?.[0];
  const dynamicDeviceIdentity = activeTerminal?.deviceIdentity;

  React.useEffect(() => {
    if (dynamicDeviceIdentity && typeof window !== "undefined") {
      window.sessionStorage.setItem("erp.pos.deviceIdentity", dynamicDeviceIdentity);
    }
  }, [dynamicDeviceIdentity]);

  // Offline Hook with dynamic device identity
  const {
    isOnline,
    pendingSyncCount,
    offlinePackage,
    isOfflinePackageValid,
    deviceIdentity,
    enqueueOfflineSale,
    syncOfflineQueue,
  } = usePosOffline(register.id, dynamicDeviceIdentity);

  // Dynamic configuration resolution
  const paymentAccounts = paymentAccountsData || [];
  const activePriceList =
    (priceListsData || []).find(
      (p) =>
        (p.direction === "Selling" || p.direction?.toLowerCase() === "selling") &&
        (p.status === "Active" || !p.status)
    ) || (priceListsData || [])[0];
  const dynamicPriceListId =
    activePriceList?.id || offlinePackage?.parsedPayload?.priceListId || null;

  const resolvedProviderConfigId =
    (paymentRequestsData?.items || paymentRequestsData || [])[0]?.providerConfigId || null;

  // Resolve authoritative receipt details when checkout is completed
  useEffect(() => {
    const saleId = completedCheckout?.saleId;
    if (saleId && completedCheckout.state === "Completed") {
      posApi
        .getSale(saleId)
        .then((sale) => {
          if (sale?.receiptNumber) {
            setAuthoritativeReceiptNumber(sale.receiptNumber);
            setAuthoritativeBusinessDate(sale.businessDate);
            setAuthoritativeSale({
              subtotal: sale.subtotal ?? 0,
              taxTotal: sale.taxTotal ?? 0,
              discountTotal: sale.discountTotal ?? 0,
              grandTotal: sale.total,
            });
          } else {
            setAuthoritativeReceiptNumber(null);
            setAuthoritativeSale(null);
          }
        })
        .catch(() => {
          setAuthoritativeReceiptNumber(null);
          setAuthoritativeSale(null);
        });

      posApi
        .getSaleLines(saleId)
        .then((lines) => {
          if (lines && lines.length > 0) {
            setAuthoritativeLines(lines);
          }
        })
        .catch(() => {
          setAuthoritativeLines(null);
        });
    } else {
      setAuthoritativeReceiptNumber(null);
      setAuthoritativeBusinessDate(null);
      setAuthoritativeSale(null);
      setAuthoritativeLines(null);
    }
  }, [completedCheckout?.saleId, completedCheckout?.state]);

  // Initialize Cart if none exists
  useEffect(() => {
    if (!cartId && session?.id) {
      createCartMutation
        .mutateAsync({
          sessionId: session.id,
          currencyCode: register.currencyCode || "EGP",
          customerPartyId: customerPartyId ?? null,
        })
        .then((newCart) => {
          setCartId(newCart.id);
        })
        .catch((err) => {
          setStatusMessage(err instanceof Error ? err.message : t("workspace.cartCreateFailed", "فشل إنشاء سلة مشتريات"));
        });
    }
  }, [cartId, session?.id, register.currencyCode, customerPartyId]);

  // Handle barcode scanned from physical scanner or manual input
  const handleBarcodeScanned = useCallback(
    async (code: string) => {
      if (!cartId || !code.trim()) return;
      const effectivePriceList = dynamicPriceListId || activePriceList?.id || offlinePackage?.parsedPayload?.priceListId;
      if (!effectivePriceList) {
        setStatusMessage(t("workspace.noPriceListForOffline", "لا توجد قائمة أسعار صالحة للعمل"));
        return;
      }
      setStatusMessage(null);
      try {
        await scanMutation.mutateAsync({
          cartId,
          request: {
            skuOrBarcode: code.trim(),
            quantity: 1,
            legalEntityId,
            priceListId: effectivePriceList,
          },
        });
        setBarcodeInput("");
        setStatusMessage(`${t("workspace.scanned")}: ${code.trim()}`);
      } catch (err: unknown) {
        const errorMsg = err instanceof Error ? err.message : t("workspace.scanFailed");
        if (errorMsg.includes("cart_version_conflict")) {
          await refetchCart();
          setStatusMessage(t("workspace.cartVersionRefreshed", "تم تحديث السلة لتطابق أحدث إصدار."));
        } else {
          setStatusMessage(errorMsg);
        }
      }
    },
    [cartId, legalEntityId, dynamicPriceListId, scanMutation, refetchCart, t]
  );

  // Keyboard barcode scanner hook listener
  usePosScanner({
    onScan: handleBarcodeScanned,
    enabled: Boolean(cartId) && !isTenderModalOpen && !isReceiptModalOpen && !isOverrideModalOpen,
  });

  // Quantity updates with version conflict resilience
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
      const errorMsg = err instanceof Error ? err.message : "Update failed";
      if (errorMsg.includes("cart_version_conflict")) {
        await refetchCart();
        setStatusMessage(t("workspace.cartVersionRefreshed", "تم تحديث السلة لتطابق أحدث إصدار."));
      } else {
        setStatusMessage(errorMsg);
      }
    }
  };

  // Remove line with version conflict resilience
  const handleRemoveLine = async (lineId: string, version: number) => {
    if (!cartId) return;
    try {
      await removeLineMutation.mutateAsync({
        cartId,
        lineId,
        expectedVersion: version,
      });
    } catch (err: unknown) {
      const errorMsg = err instanceof Error ? err.message : "Remove failed";
      if (errorMsg.includes("cart_version_conflict")) {
        await refetchCart();
        setStatusMessage(t("workspace.cartVersionRefreshed", "تم تحديث السلة لتطابق أحدث إصدار."));
      } else {
        setStatusMessage(errorMsg);
      }
    }
  };

  // Line Discount with Manager Override prompt
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
      setStatusMessage(t("workspace.discountApplied", "تم تطبيق الخصم على البند بنجاح"));
    } catch (err: unknown) {
      const errorMsg = err instanceof Error ? err.message : "Discount failed";
      if (errorMsg.includes("discount_override_required")) {
        setPendingDiscountAmount(amount);
        setIsOverrideModalOpen(true);
      } else if (errorMsg.includes("cart_version_conflict")) {
        await refetchCart();
        setStatusMessage(t("workspace.cartVersionRefreshed", "تم تحديث السلة لتطابق أحدث إصدار."));
      } else {
        setStatusMessage(errorMsg);
      }
    }
  };

  // Handle Manager Override Approval for Discount
  const handleManagerAuthorize = async (reason: string) => {
    if (!cartId || pendingDiscountAmount === null || !discountLine) return;
    try {
      const expiresAt = new Date(Date.now() + 15 * 60 * 1000).toISOString();
      await createOverrideMutation.mutateAsync({
        scope: "Discount",
        targetId: cartId,
        reason,
        expiresAt,
      });

      // Refetch cart to get latest version before re-applying
      const latestCart = await refetchCart();
      const currentVersion = latestCart.data?.cart?.version || cartDetails?.cart?.version || 1;

      await discountMutation.mutateAsync({
        cartId,
        request: {
          lineId: discountLine.id,
          amount: pendingDiscountAmount,
          expectedVersion: currentVersion,
        },
      });

      setDiscountLine(null);
      setPendingDiscountAmount(null);
      setIsOverrideModalOpen(false);
      setStatusMessage(t("workspace.discountApproved", "تم اعتماد وتطبيق الخصم بنجاح"));
    } catch (err: unknown) {
      setStatusMessage(err instanceof Error ? err.message : t("override.authFailed"));
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

  // Checkout with tenders and financial state machine handling
  const handleCheckout = async (tenders: readonly TenderRequest[]) => {
    if (!cartId) return;
    setStatusMessage(null);
    try {
      const res = await checkoutMutation.mutateAsync({
        cartId,
        request: {
          idempotencyKey: checkoutIdempotencyKey,
          tenders,
        },
      });

      setCompletedCheckout(res);

      if (res.state === "Completed") {
        // Freeze cart lines for immutable receipt printing
        setFrozenReceiptLines(cartDetails?.lines ? [...cartDetails.lines] : []);
        if (res.saleId) {
          try {
            const sale = await posApi.getSale(res.saleId);
            if (sale?.receiptNumber) {
              setAuthoritativeReceiptNumber(sale.receiptNumber);
              setAuthoritativeBusinessDate(sale.businessDate);
              setAuthoritativeSale({
                subtotal: sale.subtotal ?? 0,
                taxTotal: sale.taxTotal ?? 0,
                discountTotal: sale.discountTotal ?? 0,
                grandTotal: sale.total,
              });
            }
            const saleLines = await posApi.getSaleLines(res.saleId);
            if (saleLines && saleLines.length > 0) {
              setAuthoritativeLines(saleLines);
            }
          } catch {
            setAuthoritativeReceiptNumber(null);
            setAuthoritativeSale(null);
            setAuthoritativeLines(null);
          }
        }
        setIsTenderModalOpen(false);
        setIsReceiptModalOpen(true);
      } else if (
        res.state === "RecoveryRequired" ||
        res.paymentState === "Pending" ||
        res.paymentState === "Unknown"
      ) {
        setIsTenderModalOpen(false);
        setStatusMessage(
          t(
            "workspace.checkoutPendingRecovery",
            "الدفع قيد المعالجة أو يتطلب استرداد الحالة من مزود الدفع. يرجى الضغط على زر استرداد الحالة للتحقق."
          )
        );
      } else if (res.paymentState === "Failed") {
        setStatusMessage(
          t("workspace.paymentFailed", "فشلت عملية التحصيل من مزود الدفع. يرجى اختيار وسيلة دفع أخرى أو إعادة المحاولة.")
        );
      } else {
        setIsTenderModalOpen(false);
        setStatusMessage(
          `${t("workspace.currentStatus", "الحالة الحالية")}: ${res.paymentState} (${res.state})`
        );
      }
    } catch (err: unknown) {
      const errorMsg = err instanceof Error ? err.message : String(err);
      if (errorMsg.includes("cart_version_conflict")) {
        await refetchCart();
        setStatusMessage(t("workspace.cartVersionRefreshed", "تم تحديث السلة لتطابق أحدث إصدار."));
      } else {
        setStatusMessage(errorMsg);
      }
    }
  };

  // Recover Checkout Status
  const handleRecoverCheckout = async (checkoutId: string) => {
    try {
      const res = await recoverCheckoutMutation.mutateAsync(checkoutId);
      setCompletedCheckout(res);
      if (res.state === "Completed") {
        if (cartDetails?.lines && cartDetails.lines.length > 0) {
          setFrozenReceiptLines([...cartDetails.lines]);
        }
        if (res.saleId) {
          try {
            const sale = await posApi.getSale(res.saleId);
            if (sale) {
              setAuthoritativeReceiptNumber(sale.receiptNumber);
              setAuthoritativeBusinessDate(sale.businessDate);
            }
          } catch {
            // Fallback
          }
        }
        setIsReceiptModalOpen(true);
        setStatusMessage(t("workspace.checkoutRecoveredSuccess", "تم استرداد وتأكيد اكتمال العملية بنجاح"));
      } else {
        setStatusMessage(
          `${t("workspace.currentStatus", "الحالة الحالية")}: ${res.paymentState} (${res.state})`
        );
      }
    } catch (err: unknown) {
      setStatusMessage(err instanceof Error ? err.message : "Recovery check failed");
    }
  };

  // Explicit Offline Cash Sale Queue
  const handleQueueOfflineSale = async () => {
    if (!isOfflinePackageValid) {
      setStatusMessage(
        t(
          "workspace.offlinePackageInvalidError",
          "لا يمكن إتمام البيع بدون اتصال لعدم وجود حزمة غير متصلة صالحة. يرجى الاتصال بالإنترنت وتحديث الحزمة."
        )
      );
      return;
    }

    const defaultCashAccount =
      paymentAccounts.find(
        (a) =>
          (a.type?.toLowerCase() === "cashbox" || a.type === "Cashbox") &&
          (a.status === "Active" || !a.status)
      );

    if (!defaultCashAccount) {
      setStatusMessage(t("tender.noCashAccountAvailable", "لا يوجد حساب خزينة متاح لإتمام البيع النقدي"));
      return;
    }

    const cartLines = cartDetails?.lines || [];
    if (cartLines.length === 0) {
      setStatusMessage(t("workspace.cartEmpty", "السلة فارغة"));
      return;
    }

    const packageItems = offlinePackage?.parsedPayload?.items || [];
    const packagePriceListId = offlinePackage?.parsedPayload?.priceListId;
    const effectivePriceListId = dynamicPriceListId || packagePriceListId;

    if (!effectivePriceListId) {
      setStatusMessage(
        t("workspace.noPriceListForOffline", "لا توجد قائمة أسعار صالحة للعمل بدون اتصال")
      );
      return;
    }

    try {
      const lines = cartLines.map((l) => {
        const matched = packageItems.find(
          (pi) => pi.variantId === l.variantId || (pi.barcode && pi.barcode === l.description)
        );
        return {
          variantId: l.variantId,
          description: l.description,
          uomCode: l.uomCode,
          quantity: l.quantity,
          unitPrice: l.unitPrice,
          taxAmount: l.taxAmount,
          discountAmount: l.discountAmount,
          legalEntityId,
          productTemplateId: matched?.productTemplateId || null,
          unitOfMeasureId: matched?.unitOfMeasureId || null,
          priceListId: effectivePriceListId,
          pricingDate: new Date().toISOString().slice(0, 10),
        };
      });

      await enqueueOfflineSale({
        registerId: register.id,
        deviceIdentity,
        sessionId: session.id,
        currencyCode: register.currencyCode || "EGP",
        paymentAccountId: defaultCashAccount.id,
        customerPartyId: customerPartyId ?? undefined,
        lines,
      });

      setStatusMessage(t("workspace.offlineQueuedSuccess", "تم حفظ العملية بنجاح في قاعدة البيانات المحلية (IndexedDB) للمزامنة لاحقاً"));
      setCartId(null);
    } catch (err: unknown) {
      setStatusMessage(err instanceof Error ? err.message : "Offline queue error");
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

  // Reset after completed receipt for fresh new sale
  const handleNewSale = () => {
    setCartId(null);
    setCompletedCheckout(null);
    setFrozenReceiptLines([]);
    setAuthoritativeReceiptNumber(null);
    setAuthoritativeBusinessDate(null);
    setCustomerPartyId(null);
    setCustomerName(null);
    setCheckoutIdempotencyKey(
      window.crypto?.randomUUID ? window.crypto.randomUUID() : `checkout-${Date.now()}`
    );
  };

  const lines = cartDetails?.lines || [];
  const subtotal = cartDetails?.cart?.subtotal || 0;
  const taxTotal = cartDetails?.cart?.taxTotal || 0;
  const discountTotal = cartDetails?.cart?.discountTotal || 0;
  const grandTotal = cartDetails?.cart?.grandTotal || 0;
  const cartVersion = cartDetails?.cart?.version || 1;
  const isHeld = cartDetails?.cart?.state === "Held";

  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        gap: "1rem",
        minHeight: "80vh",
        maxWidth: "100%",
        overflowX: "hidden",
        boxSizing: "border-box",
      }}
    >
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
          boxSizing: "border-box",
          maxWidth: "100%",
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: "0.75rem", flexWrap: "wrap" }}>
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
        <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", flexWrap: "wrap" }}>
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

          {/* If offline and has items, offer explicit offline cash sale button */}
          {!isOnline && lines.length > 0 && (
            <Button
              size="compact"
              variant="primary"
              onClick={handleQueueOfflineSale}
              isDisabled={!isOfflinePackageValid}
              style={{ fontSize: "0.75rem", backgroundColor: "#d97706" }}
            >
              ⚡ {t("workspace.queueOfflineCashSale", "تسجيل بيع غير متصل")}
            </Button>
          )}

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
          flexWrap: "wrap",
          backgroundColor: "#ffffff",
          padding: "0.875rem 1.25rem",
          borderRadius: "var(--radius-lg)",
          border: "1px solid #cbd5e1",
          boxShadow: "0 1px 3px 0 rgba(0, 0, 0, 0.05)",
          boxSizing: "border-box",
          maxWidth: "100%",
        }}
      >
        <div style={{ flex: "1 1 18rem", display: "flex", gap: "0.5rem", maxWidth: "100%" }}>
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

        {/* Quick Product Search input */}
        <div style={{ flex: "0 1 18rem", maxWidth: "100%" }}>
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
          onClick={() => handleBarcodeScanned("EG-2-0001")}
          style={{ fontSize: "0.8125rem" }}
        >
          {t("workspace.quickItemPaper")}
        </Button>
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
          onClick={() => handleBarcodeScanned("EG-2-0003")}
          style={{ fontSize: "0.8125rem" }}
        >
          {t("workspace.quickItemTape")}
        </Button>
        <Button
          size="compact"
          variant="secondary"
          onClick={() => handleBarcodeScanned("EG-1-0004")}
          style={{ fontSize: "0.8125rem" }}
        >
          {t("workspace.quickItemMultimeter")}
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
              role="button"
              tabIndex={0}
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                padding: "0.5rem 0.75rem",
                cursor: "pointer",
                borderRadius: "var(--radius-sm)",
                borderBottom: "1px solid #f1f5f9",
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
                <strong style={{ color: "var(--color-content-default)" }}>
                  <BidiText>{item.displayName}</BidiText>
                </strong>
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
            flexWrap: "wrap",
            gap: "0.5rem",
          }}
        >
          <span>{statusMessage}</span>
          <div style={{ display: "flex", gap: "0.5rem", alignItems: "center" }}>
            {completedCheckout && completedCheckout.state !== "Completed" && (
              <Button
                size="compact"
                variant="primary"
                onClick={() => handleRecoverCheckout(completedCheckout.checkoutId)}
              >
                {t("workspace.recoverPaymentStatus", "استرداد حالة الدفع")}
              </Button>
            )}
            <button
              type="button"
              onClick={() => setStatusMessage(null)}
              style={{ background: "none", border: "none", cursor: "pointer", fontSize: "1rem" }}
            >
              ×
            </button>
          </div>
        </div>
      )}

      {/* Main Responsive Layout: Cart Table + Totals Sidebar */}
      <div
        style={{
          display: "flex",
          flexWrap: "wrap",
          gap: "1.25rem",
          alignItems: "start",
          maxWidth: "100%",
          boxSizing: "border-box",
        }}
      >
        {/* Cart Lines Table Panel: expansive primary workspace */}
        <div style={{ flex: "1 1 36rem", minWidth: 0, maxWidth: "100%", overflowX: "auto" }}>
          <PosCartLinesTable
            lines={lines}
            currencyCode={register.currencyCode}
            cartVersion={cartVersion}
            onUpdateQuantity={handleUpdateQuantity}
            onOpenDiscount={(line) => setDiscountLine(line)}
            onRemoveLine={handleRemoveLine}
          />
        </div>

        {/* Cart Totals & Checkout Panel: compact dedicated sidebar */}
        <div style={{ flex: "0 0 21rem", minWidth: "18rem", maxWidth: "100%" }}>
          <PosCartTotalsPanel
            subtotal={subtotal}
            taxTotal={taxTotal}
            discountTotal={discountTotal}
            grandTotal={grandTotal}
            currencyCode={register.currencyCode}
            isHeld={isHeld}
            lineCount={lines.length}
            onHoldCart={handleHoldCart}
            onCancelCart={handleCancelCart}
            onOpenTender={() => setIsTenderModalOpen(true)}
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

      <DiscountModal
        isOpen={Boolean(discountLine)}
        onClose={() => setDiscountLine(null)}
        lineDescription={discountLine?.description}
        currencyCode={register.currencyCode}
        lineTotal={discountLine?.lineTotal || 0}
        onApply={(amount) => handleApplyDiscount(amount)}
      />

      <ManagerOverrideModal
        isOpen={isOverrideModalOpen}
        onClose={() => {
          setIsOverrideModalOpen(false);
          setPendingDiscountAmount(null);
        }}
        scope="Discount"
        targetId={cartId || ""}
        onAuthorize={handleManagerAuthorize}
        isSubmitting={createOverrideMutation.isPending || discountMutation.isPending}
      />

      <TenderModal
        isOpen={isTenderModalOpen}
        onClose={() => setIsTenderModalOpen(false)}
        totalAmount={grandTotal}
        currencyCode={register.currencyCode}
        paymentAccounts={paymentAccounts}
        providerConfigId={resolvedProviderConfigId}
        onCheckout={handleCheckout}
        isSubmitting={checkoutMutation.isPending}
      />

      <ReceiptModal
        isOpen={isReceiptModalOpen}
        onClose={() => setIsReceiptModalOpen(false)}
        checkout={completedCheckout}
        lines={frozenReceiptLines.length > 0 ? frozenReceiptLines : lines}
        subtotal={subtotal}
        taxTotal={taxTotal}
        discountTotal={discountTotal}
        grandTotal={grandTotal}
        currencyCode={register.currencyCode}
        registerCode={register.code}
        cashierName={cashierDisplayName}
        authoritativeReceiptNumber={authoritativeReceiptNumber ?? undefined}
        authoritativeBusinessDate={authoritativeBusinessDate ?? undefined}
        authoritativeSale={authoritativeSale}
        authoritativeLines={authoritativeLines}
        onNewSale={handleNewSale}
      />

      <CashMovementModal
        isOpen={isMovementModalOpen}
        onClose={() => setIsMovementModalOpen(false)}
        currencyCode={register.currencyCode}
        onConfirm={handleCashMovement}
        isSubmitting={cashMovementMutation.isPending}
      />
    </div>
  );
}
