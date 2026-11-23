import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import {
  useCreateRestrictionForInspection,
  useReleaseRestriction,
  useCreateDisposition,
  useApproveDisposition
} from "../hooks/use-quality-queries.ts";

// --------------------------------------------------------------------------
// 1. Create Restriction (Quality Hold) Modal
// --------------------------------------------------------------------------

interface CreateRestrictionModalProps {
  inspectionId?: string | null;
  defaultInspectionId?: string | null;
  legalEntityId: string;
  isOpen: boolean;
  onClose: () => void;
}

export function CreateRestrictionModal({
  inspectionId,
  defaultInspectionId,
  legalEntityId,
  isOpen,
  onClose
}: CreateRestrictionModalProps): React.JSX.Element | null {
  const { t } = useTranslation("quality");
  const createRestrictionMutation = useCreateRestrictionForInspection();

  const [targetInspectionId, setTargetInspectionId] = useState(inspectionId || defaultInspectionId || "");
  const [restrictionType, setRestrictionType] = useState("QualityHold");
  const [reasonCode, setReasonCode] = useState("RECEIVING_INSPECTION_HOLD");
  const [quantity, setQuantity] = useState("100");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const finalInspectionId = inspectionId || defaultInspectionId || targetInspectionId.trim();
    const qty = Number(quantity);
    if (!finalInspectionId || !qty || qty <= 0 || !reasonCode.trim()) return;

    await createRestrictionMutation.mutateAsync({
      inspectionId: finalInspectionId,
      legalEntityId,
      request: {
        restrictionType,
        reasonCode: reasonCode.trim(),
        quantity: qty,
        inspectionId: finalInspectionId
      }
    });
    onClose();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("actions.createRestriction", "حظر واحتجاز الدفعة (Quality Hold)")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
        <Select
          label={t("fields.restrictionType", "نوع الحظر والاحتجاز")}
          selectedKey={restrictionType}
          onSelectionChange={(key) => setRestrictionType(String(key))}
          options={[
            { id: "QualityHold", label: "احتجاز جودة (QualityHold)" },
            { id: "Quarantine", label: "حجر صناعي وصحي (Quarantine)" },
            { id: "Conditional", label: "حظر مشروط (Conditional)" }
          ]}
        />
        <TextField
          label={t("fields.reasonCode", "كود وسبب الحظر")}
          value={reasonCode}
          onChange={setReasonCode}
          isRequired
          autoFocus
        />
        <TextField
          label={t("fields.quantity", "الكمية المحتجزة")}
          type="number"
          value={quantity}
          onChange={setQuantity}
          isRequired
        />
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "8px", marginTop: "12px" }}>
          <Button variant="outline" onPress={onClose} type="button">
            {t("actions.cancel", "إلغاء")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isDisabled={createRestrictionMutation.isPending || !reasonCode.trim() || !quantity || Number(quantity) <= 0}
          >
            {createRestrictionMutation.isPending ? "..." : t("actions.save", "تطبيق الحظر")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// --------------------------------------------------------------------------
// 2. Release Restriction (Partial or Full) Modal
// --------------------------------------------------------------------------

interface ReleaseRestrictionModalProps {
  restrictionId: string;
  totalQuantity?: number;
  remainingQuantity?: number;
  maxQuantity?: number;
  isOpen: boolean;
  onClose: () => void;
}

export function ReleaseRestrictionModal({
  restrictionId,
  remainingQuantity,
  maxQuantity,
  isOpen,
  onClose
}: ReleaseRestrictionModalProps): React.JSX.Element | null {
  const { t } = useTranslation("quality");
  const releaseMutation = useReleaseRestriction();

  const effectiveRemaining = remainingQuantity ?? maxQuantity ?? 10;
  const [quantity, setQuantity] = useState(String(effectiveRemaining));
  const [reason, setReason] = useState("");
  const [operationalDate, setOperationalDate] = useState(new Date().toISOString().split("T")[0]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const qty = Number(quantity);
    if (!qty || qty <= 0 || !reason.trim()) return;

    await releaseMutation.mutateAsync({
      restrictionId,
      request: {
        quantity: qty,
        reason: reason.trim(),
        operationalDate: operationalDate || null,
        postingDate: operationalDate || null
      }
    });
    onClose();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("actions.releaseRestriction", "إفراج مخزني عن الدفعة المحتجزة")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
        <div style={{ padding: "8px 12px", backgroundColor: "#f8fafc", border: "1px solid #e2e8f0", borderRadius: "6px", fontSize: "13px" }}>
          <div>{t("fields.remainingQuantity", "الكمية المتبقية بالحظر")}: <strong>{remainingQuantity}</strong></div>
        </div>
        <TextField
          label={t("fields.quantity", "كمية الإفراج (جزئي أو كلي)")}
          type="number"
          value={quantity}
          onChange={setQuantity}
          isRequired
          autoFocus
        />
        <TextField
          label={t("fields.reason", "مبرر وأسباب الإفراج الفني")}
          value={reason}
          onChange={setReason}
          isRequired
        />
        <TextField
          label={t("fields.effectiveFrom", "تاريخ العملية")}
          type="date"
          value={operationalDate}
          onChange={setOperationalDate}
          isRequired
        />
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "8px", marginTop: "12px" }}>
          <Button variant="outline" onPress={onClose} type="button">
            {t("actions.cancel", "إلغاء")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isDisabled={releaseMutation.isPending || !reason.trim() || !quantity || Number(quantity) <= 0}
          >
            {releaseMutation.isPending ? "..." : t("actions.confirm", "تأكيد الإفراج")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// --------------------------------------------------------------------------
// 3. Create Disposition Modal
// --------------------------------------------------------------------------

interface CreateDispositionModalProps {
  legalEntityId: string;
  inspectionId?: string | null;
  defaultInspectionId?: string | null;
  isOpen: boolean;
  onClose: () => void;
}

export function CreateDispositionModal({
  legalEntityId,
  inspectionId,
  defaultInspectionId,
  isOpen,
  onClose
}: CreateDispositionModalProps): React.JSX.Element | null {
  const { t } = useTranslation("quality");
  const createDispositionMutation = useCreateDisposition();

  const [decision, setDecision] = useState("Accept");
  const [reason, setReason] = useState("");
  const [quantity, setQuantity] = useState("100");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!reason.trim()) return;

    await createDispositionMutation.mutateAsync({
      legalEntityId,
      request: {
        decision,
        reason: reason.trim(),
        quantity: quantity ? Number(quantity) : null,
        inspectionId: inspectionId || defaultInspectionId || null
      }
    });
    onClose();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("actions.createDisposition", "إصدار قرار فني وتصرف (Disposition)")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
        <Select
          label={t("fields.decision", "نوع القرار الفني")}
          selectedKey={decision}
          onSelectionChange={(key) => setDecision(String(key))}
          options={[
            { id: "Accept", label: t("decisions.Accept", "قبول تام وإفراج") },
            { id: "Reject", label: t("decisions.Reject", "رفض وحظر نهائي") },
            { id: "UseAsIs", label: t("decisions.UseAsIs", "استخدام بالحالة الراهنة (Use-As-Is)") },
            { id: "Concession", label: t("decisions.Concession", "سماح وتجاوز فني (Concession)") },
            { id: "SupplierReturn", label: t("decisions.SupplierReturn", "إرجاع للمورد (Supplier Return)") },
            { id: "Scrap", label: t("decisions.Scrap", "تخريد وإتلاف (Scrap)") },
            { id: "Rework", label: t("decisions.Rework", "إعادة تشغيل ومعالجة (Rework)") }
          ]}
        />
        {decision === "SupplierReturn" && (
          <div style={{ padding: "8px 12px", backgroundColor: "#fffbeb", border: "1px solid #fde68a", borderRadius: "6px", fontSize: "12px", color: "#92400e" }}>
            {t("messages.supplierReturnSafetyNotice", "أمان المخزون: إرجاع المورد يُصرَف مباشرة من رصيد احتجاز الجودة (QualityHold) ولا يمر إطلاقاً برصيد المتاح للبيع.")}
          </div>
        )}
        <TextField
          label={t("fields.quantity", "الكمية الخاضعة للقرار")}
          type="number"
          value={quantity}
          onChange={setQuantity}
        />
        <TextField
          label={t("fields.reason", "المبرر والأسباب الفنية")}
          value={reason}
          onChange={setReason}
          isRequired
          autoFocus
        />
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "8px", marginTop: "12px" }}>
          <Button variant="outline" onPress={onClose} type="button">
            {t("actions.cancel", "إلغاء")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isDisabled={createDispositionMutation.isPending || !reason.trim()}
          >
            {createDispositionMutation.isPending ? "..." : t("actions.save", "حفظ القرار كمسودة")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// --------------------------------------------------------------------------
// 4. Approve Disposition Modal
// --------------------------------------------------------------------------

interface ApproveDispositionModalProps {
  dispositionId: string;
  isOpen: boolean;
  onClose: () => void;
}

export function ApproveDispositionModal({ dispositionId, isOpen, onClose }: ApproveDispositionModalProps): React.JSX.Element | null {
  const { t } = useTranslation("quality");
  const approveMutation = useApproveDisposition();
  const [notes, setNotes] = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    await approveMutation.mutateAsync({
      dispositionId,
      request: { notes: notes.trim() || null }
    });
    onClose();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("actions.approveDisposition", "اعتماد القرار الفني")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "14px" }}>
        <p style={{ fontSize: "13px", color: "#475569", margin: 0 }}>
          {t("messages.irreversibleWarning", "اعتماد القرار يتيح تنفيذه مباشرة عبر الموديول المالك (المخزون / المشتريات / التصنيع).")}
        </p>
        <TextField
          label={t("fields.notes", "ملاحظات الاعتماد")}
          value={notes}
          onChange={setNotes}
          autoFocus
        />
        <div style={{ display: "flex", justifyContent: "flex-end", gap: "8px", marginTop: "12px" }}>
          <Button variant="outline" onPress={onClose} type="button">
            {t("actions.cancel", "إلغاء")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isDisabled={approveMutation.isPending}
          >
            {approveMutation.isPending ? "..." : t("actions.confirm", "تأكيد الاعتماد")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
