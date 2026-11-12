import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import {
  useChangeEstimateMutation,
  useAddCostMutation,
  useProposeImpairmentMutation
} from "../../hooks/use-fixed-assets-queries.ts";
import type {
  ChangeEstimateRequest,
  AddCostRequest,
  ProposeImpairmentRequest
} from "../../types.ts";

export interface EstimateChangeModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly assetId: string | null;
  readonly assetName?: string | undefined;
  readonly currentLifeMonths?: number | undefined;
  readonly currentSalvageValue?: number | undefined;
}

export function EstimateChangeModal({
  isOpen,
  onClose,
  legalEntityId,
  assetId,
  assetName,
  currentLifeMonths = 60,
  currentSalvageValue = 0
}: EstimateChangeModalProps): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");
  const estimateMutation = useChangeEstimateMutation(legalEntityId);

  const [remainingLifeMonths, setRemainingLifeMonths] = useState(String(currentLifeMonths));
  const [salvageValue, setSalvageValue] = useState(String(currentSalvageValue));
  const [effectiveDate, setEffectiveDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [justification, setJustification] = useState("");
  const [error, setError] = useState<string | null>(null);

  React.useEffect(() => {
    if (currentLifeMonths) setRemainingLifeMonths(String(currentLifeMonths));
    if (currentSalvageValue !== undefined) setSalvageValue(String(currentSalvageValue));
  }, [currentLifeMonths, currentSalvageValue]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!assetId) return;
    setError(null);

    const life = parseInt(remainingLifeMonths, 10);
    if (isNaN(life) || life < 0) {
      setError(t("validation.validLifeRequired", "يجب إدخال عمر متبقي صحيح"));
      return;
    }
    if (!justification.trim()) {
      setError(t("validation.justificationRequired", "يجب كتابة المبرر المحاسبي لتغيير التقدير"));
      return;
    }

    try {
      const payload: ChangeEstimateRequest = {
        remainingLifeMonths: life,
        salvageValue: parseFloat(salvageValue) || 0,
        effectiveDate,
        justification: justification.trim()
      };

      await estimateMutation.mutateAsync({ assetId, payload });
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.unknownError", "فشل تعديل التقدير المحاسبي");
      setError(msg);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("books.estimateModalTitle", "تعديل التقدير المحاسبي (العمر الإنتاجي / الخردة)")}
      width="540px"
      data-testid="estimate-change-modal"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {assetName && (
          <div style={{ padding: "0.75rem", backgroundColor: "#f8fafc", border: "1px solid #cbd5e1", borderRadius: "0.375rem", fontSize: "0.875rem" }}>
            <strong>{t("fields.asset", "الأصل المختار")}:</strong> {assetName}
          </div>
        )}

        {error && (
          <div data-testid="estimate-error" style={{ padding: "0.75rem", backgroundColor: "#fef2f2", color: "#991b1b", borderRadius: "0.375rem" }}>
            {error}
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("fields.newRemainingLife", "العمر الإنتاجي المتبقي (أشهر)")}
            type="number"
            value={remainingLifeMonths}
            onChange={(e) => setRemainingLifeMonths(e.target.value)}
            required
            data-testid="remaining-life-input"
          />
          <TextField
            label={t("fields.newSalvageValue", "قيمة الخردة المقدرة الجديدة")}
            type="number"
            step="0.01"
            value={salvageValue}
            onChange={(e) => setSalvageValue(e.target.value)}
            data-testid="salvage-value-input"
          />
        </div>

        <TextField
          label={t("fields.effectiveDate", "تاريخ سريان التعديل")}
          type="date"
          value={effectiveDate}
          onChange={(e) => setEffectiveDate(e.target.value)}
          required
          data-testid="effective-date-input"
        />

        <TextField
          label={t("fields.justification", "المبرر المحاسبي (وفق معيار المحاسبة الدولي IAS 8)")}
          value={justification}
          onChange={(e) => setJustification(e.target.value)}
          required
          placeholder={t("fields.justificationPlaceholder", "تمديد العمر نتيجة إجراء عمرة وتحديث رئيسي...")}
          data-testid="justification-input"
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel", "إلغاء")}
          </Button>
          <Button variant="primary" type="submit" isLoading={estimateMutation.isPending} data-testid="submit-estimate-btn">
            {t("books.saveEstimateChange", "تطبيق التعديل المحاسبي")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

export interface CostAdditionModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly assetId: string | null;
  readonly assetName?: string;
}

export function CostAdditionModal({
  isOpen,
  onClose,
  legalEntityId,
  assetId,
  assetName
}: CostAdditionModalProps): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");
  const costMutation = useAddCostMutation(legalEntityId);

  const [additionAmount, setAdditionAmount] = useState("");
  const [additionDate, setAdditionDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [description, setDescription] = useState("");
  const [sourceDocumentRef, setSourceDocumentRef] = useState("");
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!assetId) return;
    setError(null);

    const amount = parseFloat(additionAmount);
    if (isNaN(amount) || amount <= 0) {
      setError(t("validation.validAmountRequired", "يجب إدخال مبلغ صحيح أكبر من الصفر"));
      return;
    }
    if (!description.trim()) {
      setError(t("validation.descRequired", "وصف التكلفة الإضافية مطلوب"));
      return;
    }

    try {
      const payload: AddCostRequest = {
        additionAmount: amount,
        additionDate,
        description: description.trim(),
        sourceDocumentRef: sourceDocumentRef.trim() || undefined
      };

      await costMutation.mutateAsync({ assetId, payload });
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.unknownError", "فشل إضافة التكلفة الرأسمالية");
      setError(msg);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("books.addCostModalTitle", "إضافة تكلفة رأسمالية لاحقة (Capital Addition)")}
      width="540px"
      data-testid="cost-addition-modal"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {assetName && (
          <div style={{ padding: "0.75rem", backgroundColor: "#f8fafc", border: "1px solid #cbd5e1", borderRadius: "0.375rem", fontSize: "0.875rem" }}>
            <strong>{t("fields.asset", "الأصل المختار")}:</strong> {assetName}
          </div>
        )}

        {error && (
          <div data-testid="cost-error" style={{ padding: "0.75rem", backgroundColor: "#fef2f2", color: "#991b1b", borderRadius: "0.375rem" }}>
            {error}
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("fields.additionAmount", "مبلغ التكلفة المضافة")}
            type="number"
            step="0.01"
            value={additionAmount}
            onChange={(e) => setAdditionAmount(e.target.value)}
            required
            data-testid="addition-amount-input"
          />
          <TextField
            label={t("fields.additionDate", "تاريخ الإضافة")}
            type="date"
            value={additionDate}
            onChange={(e) => setAdditionDate(e.target.value)}
            required
            data-testid="addition-date-input"
          />
        </div>

        <TextField
          label={t("fields.additionDesc", "وصف الإضافة الرأسمالية")}
          value={description}
          onChange={(e) => setDescription(e.target.value)}
          required
          placeholder={t("fields.additionDescPlaceholder", "إضافة وحدة تبريد إضافية / ترقية محرك...")}
          data-testid="addition-desc-input"
        />

        <TextField
          label={t("fields.sourceDocumentRef", "رقم مستند المصدر (فاتورة مورد / إذن صرف)")}
          value={sourceDocumentRef}
          onChange={(e) => setSourceDocumentRef(e.target.value)}
          placeholder="INV-2026-908"
          data-testid="source-doc-input"
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel", "إلغاء")}
          </Button>
          <Button variant="primary" type="submit" isLoading={costMutation.isPending} data-testid="submit-cost-btn">
            {t("books.saveCostAddition", "رسملة التكلفة")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

export interface ImpairmentModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly assetId: string | null;
  readonly assetName?: string;
  readonly carryingAmount?: number;
}

export function ImpairmentModal({
  isOpen,
  onClose,
  legalEntityId,
  assetId,
  assetName,
  carryingAmount = 0
}: ImpairmentModalProps): React.JSX.Element {
  const { t } = useTranslation("fixedAssets");
  const impairmentMutation = useProposeImpairmentMutation(legalEntityId);

  const [recoverableAmount, setRecoverableAmount] = useState("");
  const [impairmentDate, setImpairmentDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);

  const numericRecoverable = parseFloat(recoverableAmount) || 0;
  const calculatedLoss = Math.max(0, carryingAmount - numericRecoverable);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!assetId) return;
    setError(null);

    if (isNaN(numericRecoverable) || numericRecoverable < 0) {
      setError(t("validation.validRecoverableRequired", "يجب إدخال القيمة الاستردادية الصحيحة"));
      return;
    }
    if (calculatedLoss <= 0) {
      setError(t("validation.noImpairmentNeeded", "القيمة الاستردادية تساوي أو تفوق القيمة الدفترية، لا يوجد اضمحلال"));
      return;
    }
    if (!reason.trim()) {
      setError(t("validation.impairmentReasonRequired", "يجب إدخال سبب ودراسة الاضمحلال"));
      return;
    }

    try {
      const payload: ProposeImpairmentRequest = {
        recoverableAmount: numericRecoverable,
        impairmentLoss: calculatedLoss,
        impairmentDate,
        reason: reason.trim()
      };

      await impairmentMutation.mutateAsync({ assetId, payload });
      onClose();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : t("common.unknownError", "فشل تسجيل مقترح الاضمحلال");
      setError(msg);
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("books.impairmentModalTitle", "تسجيل خسارة هبوط في القيمة (Impairment Loss)")}
      width="540px"
      data-testid="impairment-modal"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {assetName && (
          <div style={{ padding: "0.75rem", backgroundColor: "#f8fafc", border: "1px solid #cbd5e1", borderRadius: "0.375rem", fontSize: "0.875rem" }}>
            <div><strong>{t("fields.asset", "الأصل المختار")}:</strong> {assetName}</div>
            <div style={{ marginTop: "0.25rem", color: "#64748b" }}>
              {t("fields.carryingAmount", "القيمة الدفترية الحالية")}: <strong>{carryingAmount.toLocaleString("ar-EG", { minimumFractionDigits: 2 })}</strong>
            </div>
          </div>
        )}

        {error && (
          <div data-testid="impairment-error" style={{ padding: "0.75rem", backgroundColor: "#fef2f2", color: "#991b1b", borderRadius: "0.375rem" }}>
            {error}
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <TextField
            label={t("fields.recoverableAmount", "القيمة الاستردادية المقدرة (IAS 36)")}
            type="number"
            step="0.01"
            value={recoverableAmount}
            onChange={(e) => setRecoverableAmount(e.target.value)}
            required
            data-testid="recoverable-amount-input"
          />
          <TextField
            label={t("fields.impairmentDate", "تاريخ اختبار الاضمحلال")}
            type="date"
            value={impairmentDate}
            onChange={(e) => setImpairmentDate(e.target.value)}
            required
            data-testid="impairment-date-input"
          />
        </div>

        {numericRecoverable > 0 && (
          <div style={{ padding: "0.75rem", backgroundColor: "#fff7ed", border: "1px solid #ffedd5", borderRadius: "0.375rem", fontSize: "0.875rem" }}>
            <span style={{ color: "#c2410c", fontWeight: 600 }}>
              {t("fields.calculatedLoss", "خسارة الاضمحلال المحتسبة")}: {calculatedLoss.toLocaleString("ar-EG", { minimumFractionDigits: 2 })}
            </span>
          </div>
        )}

        <TextField
          label={t("fields.impairmentReason", "سبب ودراسة اختبار الاضمحلال")}
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          required
          placeholder={t("fields.impairmentReasonPlaceholder", "تقادم تكنولوجي مفاجئ أدى لانخفاض الطاقة الإنتاجية...")}
          data-testid="impairment-reason-input"
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel", "إلغاء")}
          </Button>
          <Button variant="primary" type="submit" isLoading={impairmentMutation.isPending} data-testid="submit-impairment-btn">
            {t("books.proposeImpairment", "ترحيل خسارة الاضمحلال")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
