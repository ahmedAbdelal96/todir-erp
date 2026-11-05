import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useCreateStageMutation } from "../../hooks/use-crm-queries.ts";
import { Layers, AlertCircle } from "../../components/crm-icons.tsx";

interface NewStageModalProps {
  isOpen: boolean;
  onClose: () => void;
  legalEntityId: string;
  pipelineId: string;
  currentStagesCount: number;
}

export const NewStageModal: React.FC<NewStageModalProps> = ({
  isOpen,
  onClose,
  legalEntityId,
  pipelineId,
  currentStagesCount,
}) => {
  const { t } = useTranslation("crm");
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [probability, setProbability] = useState<number>(50);
  const [isWon, setIsWon] = useState(false);
  const [isLost, setIsLost] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const createMutation = useCreateStageMutation(legalEntityId, pipelineId);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    if (!code.trim() || !name.trim()) {
      setErrorMessage(t("crm.validation.fieldsRequired", "يرجى تعبئة الحقول الإلزامية"));
      return;
    }

    createMutation.mutate(
      {
        name: name.trim(),
        sequence: currentStagesCount + 1,
        probability: Number(probability) || 0,
        isWon,
        isLost,
      },
      {
        onSuccess: () => {
          onClose();
        },
        onError: (err: any) => {
          setErrorMessage(err?.response?.data?.detail || err?.message || t("crm.common.saveFailed", "فشل الحفظ"));
        },
      }
    );
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => !open && onClose()}
      title={t("crm.stages.newTitle", "إضافة مرحلة جديدة للمسار")}
      size="medium"
      footer={
        <>
          <Button variant="secondary" onPress={onClose} isDisabled={createMutation.isPending}>
            {t("crm.common.cancel", "إلغاء")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            form="new-stage-form"
            isDisabled={createMutation.isPending}
          >
            {createMutation.isPending ? t("crm.common.saving", "جاري الحفظ...") : t("crm.common.save", "حفظ")}
          </Button>
        </>
      }
    >
      <form id="new-stage-form" onSubmit={handleSubmit} className="space-y-4">
        {errorMessage && (
          <div className="p-3 rounded bg-rose-50 border border-rose-200 text-rose-700 text-xs flex items-center gap-2">
            <AlertCircle className="h-4 w-4 shrink-0" />
            <span>{errorMessage}</span>
          </div>
        )}

        <div>
          <label className="block text-xs font-semibold text-slate-700 mb-1">
            {t("crm.stages.code", "رمز المرحلة")} <span className="text-rose-500">*</span>
          </label>
          <input
            type="text"
            required
            placeholder={t("crm.stages.codePlaceholder", "DEMO")}
            value={code}
            onChange={(e) => setCode(e.target.value.toUpperCase())}
            className="w-full px-3 py-2 text-sm border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-primary-500 font-mono uppercase"
          />
        </div>

        <div>
          <label className="block text-xs font-semibold text-slate-700 mb-1">
            {t("crm.stages.name", "اسم المرحلة")} <span className="text-rose-500">*</span>
          </label>
          <input
            type="text"
            required
            placeholder={t("crm.stages.namePlaceholder", "مثال: تقديم العرض التوضيحي")}
            value={name}
            onChange={(e) => setName(e.target.value)}
            className="w-full px-3 py-2 text-sm border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-primary-500"
          />
        </div>

        <div>
          <label className="block text-xs font-semibold text-slate-700 mb-1">
            {t("crm.stages.probability", "احتمالية الإغلاق (%)")}
          </label>
          <input
            type="number"
            min={0}
            max={100}
            value={probability}
            onChange={(e) => setProbability(Number(e.target.value))}
            className="w-full px-3 py-2 text-sm border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-primary-500"
          />
        </div>

        <div className="flex items-center gap-6 pt-1">
          <div className="flex items-center gap-2">
            <input
              type="checkbox"
              id="isWonStage"
              checked={isWon}
              onChange={(e) => {
                setIsWon(e.target.checked);
                if (e.target.checked) {
                  setIsLost(false);
                  setProbability(100);
                }
              }}
              className="h-4 w-4 text-emerald-600 rounded border-slate-300"
            />
            <label htmlFor="isWonStage" className="text-xs font-medium text-slate-700 cursor-pointer">
              {t("crm.stages.isWon", "مرحلة نجاح وفوز")}
            </label>
          </div>

          <div className="flex items-center gap-2">
            <input
              type="checkbox"
              id="isLostStage"
              checked={isLost}
              onChange={(e) => {
                setIsLost(e.target.checked);
                if (e.target.checked) {
                  setIsWon(false);
                  setProbability(0);
                }
              }}
              className="h-4 w-4 text-rose-600 rounded border-slate-300"
            />
            <label htmlFor="isLostStage" className="text-xs font-medium text-slate-700 cursor-pointer">
              {t("crm.stages.isLost", "مرحلة خسارة الصفقة")}
            </label>
          </div>
        </div>
      </form>
    </Dialog>
  );
};
