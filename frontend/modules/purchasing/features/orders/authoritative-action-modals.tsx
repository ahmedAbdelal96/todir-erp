import React from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { usePurchaseOrderDetail } from "../../hooks/use-purchasing-queries.ts";
import { ReceiveOrderModal } from "../receipts/receive-order-modal.tsx";
import { ReceiveAndBillModal } from "../receipts/receive-and-bill-modal.tsx";
import { MatchOrderModal } from "../matching/match-order-modal.tsx";
import { NewSupplierReturnModal } from "../returns/new-supplier-return-modal.tsx";

interface AuthoritativeModalBaseProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly orderId: string | null;
}

export function AuthoritativeReceiveModal({
  isOpen,
  onClose,
  legalEntityId,
  orderId,
  onReceived
}: AuthoritativeModalBaseProps & {
  readonly onReceived?: (receiptIds: string[]) => void;
}): React.JSX.Element | null {
  const { t } = useTranslation("purchasing");
  const { data: order, isLoading } = usePurchaseOrderDetail(legalEntityId, orderId ?? "");

  if (!isOpen || !orderId) return null;

  if (isLoading || !order) {
    return (
      <Dialog isOpen={isOpen} onClose={onClose} title={t("receipts.title")}>
        <div style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted)" }}>
          {t("common.loading", "جاري التحميل...")}
        </div>
      </Dialog>
    );
  }

  return <ReceiveOrderModal isOpen={isOpen} onClose={onClose} order={order} onReceived={onReceived} />;
}

export function AuthoritativeReceiveAndBillModal({
  isOpen,
  onClose,
  legalEntityId,
  orderId,
  onCompleted
}: AuthoritativeModalBaseProps & {
  readonly onCompleted?: (receiptId: string, invoiceId: string) => void;
}): React.JSX.Element | null {
  const { t } = useTranslation("purchasing");
  const { data: order, isLoading } = usePurchaseOrderDetail(legalEntityId, orderId ?? "");

  if (!isOpen || !orderId) return null;

  if (isLoading || !order) {
    return (
      <Dialog isOpen={isOpen} onClose={onClose} title={t("receipts.receiveAndBillTitle")}>
        <div style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted)" }}>
          {t("common.loading", "جاري التحميل...")}
        </div>
      </Dialog>
    );
  }

  return <ReceiveAndBillModal isOpen={isOpen} onClose={onClose} order={order} onCompleted={onCompleted} />;
}

export function AuthoritativeMatchModal({
  isOpen,
  onClose,
  legalEntityId,
  orderId,
  onMatched,
  hasMatchOverridePermission
}: AuthoritativeModalBaseProps & {
  readonly onMatched?: () => void;
  readonly hasMatchOverridePermission?: boolean;
}): React.JSX.Element | null {
  const { t } = useTranslation("purchasing");
  const { data: order, isLoading } = usePurchaseOrderDetail(legalEntityId, orderId ?? "");

  if (!isOpen || !orderId) return null;

  if (isLoading || !order) {
    return (
      <Dialog isOpen={isOpen} onClose={onClose} title={t("matching.title")}>
        <div style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted)" }}>
          {t("common.loading", "جاري التحميل...")}
        </div>
      </Dialog>
    );
  }

  return (
    <MatchOrderModal
      isOpen={isOpen}
      onClose={onClose}
      order={order}
      onMatched={onMatched}
      hasMatchOverridePermission={hasMatchOverridePermission}
    />
  );
}

export function AuthoritativeReturnModal({
  isOpen,
  onClose,
  legalEntityId,
  orderId,
  onCreated
}: AuthoritativeModalBaseProps & {
  readonly onCreated?: () => void;
}): React.JSX.Element | null {
  const { t } = useTranslation("purchasing");
  const { data: order, isLoading } = usePurchaseOrderDetail(legalEntityId, orderId ?? "");

  if (!isOpen || !orderId) return null;

  if (isLoading || !order) {
    return (
      <Dialog isOpen={isOpen} onClose={onClose} title={t("returns.title")}>
        <div style={{ padding: "2rem", textAlign: "center", color: "var(--color-content-muted)" }}>
          {t("common.loading", "جاري التحميل...")}
        </div>
      </Dialog>
    );
  }

  return <NewSupplierReturnModal isOpen={isOpen} onClose={onClose} order={order} onCreated={onCreated} />;
}
