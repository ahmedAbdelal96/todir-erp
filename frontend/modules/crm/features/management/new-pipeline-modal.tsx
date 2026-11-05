import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useCreatePipelineMutation } from "../../hooks/use-crm-queries.ts";
import { GitBranch, AlertCircle } from "../../components/crm-icons.tsx";

interface NewPipelineModalProps {
  isOpen: boolean;
  onClose: () => void;
  legalEntityId: string;
}

export const NewPipelineModal: React.FC<NewPipelineModalProps> = ({
  isOpen,
  onClose,
  legalEntityId,
}) => {
  const { t } = useTranslation("crm");
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [isDefault, setIsDefault] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const createMutation = useCreatePipelineMutation(legalEntityId);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    if (!code.trim()) {
      setErrorMessage(t("crm.validation.codeRequired", "رمز المسار مطلوب"));
      return;
    }
    if (!name.trim()) {
      setErrorMessage(t("crm.validation.nameRequired", "اسم المسار مطلوب"));
      return;
    }

    createMutation.mutate(
      {
        legalEntityId,
        code: code.trim().toUpperCase(),
        name: name.trim(),
        isDefault,
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
      title={t("crm.pipelines.newTitle", "إنشاء مسار بيعي جديد")}
      size="medium"
      footer={
        <>
          <Button variant="secondary" onPress={onClose} isDisabled={createMutation.isPending}>
            {t("crm.common.cancel", "إلغاء")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            form="new-pipeline-form"
            isDisabled={createMutation.isPending}
          >
            {createMutation.isPending ? t("crm.common.saving", "جاري الحفظ...") : t("crm.common.save", "حفظ")}
          </Button>
        </>
      }
    >
      <form id="new-pipeline-form" onSubmit={handleSubmit} className="space-y-4">
        {errorMessage && (
          <div className="p-3 rounded bg-rose-50 border border-rose-200 text-rose-700 text-xs flex items-center gap-2">
            <AlertCircle className="h-4 w-4 shrink-0" />
            <span>{errorMessage}</span>
          </div>
        )}

        <div>
          <label className="block text-xs font-semibold text-slate-700 mb-1">
            {t("crm.pipelines.code", "رمز المسار (Code)")} <span className="text-rose-500">*</span>
          </label>
          <input
            type="text"
            required
            placeholder={t("crm.pipelines.codePlaceholder", "ENTERPRISE")}
            value={code}
            onChange={(e) => setCode(e.target.value.toUpperCase())}
            className="w-full px-3 py-2 text-sm border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-primary-500 font-mono uppercase"
          />
        </div>

        <div>
          <label className="block text-xs font-semibold text-slate-700 mb-1">
            {t("crm.pipelines.name", "اسم المسار")} <span className="text-rose-500">*</span>
          </label>
          <input
            type="text"
            required
            placeholder={t("crm.pipelines.namePlaceholder", "مثال: مبيعات كبار العملاء")}
            value={name}
            onChange={(e) => setName(e.target.value)}
            className="w-full px-3 py-2 text-sm border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-primary-500"
          />
        </div>

        <div>
          <label className="block text-xs font-semibold text-slate-700 mb-1">
            {t("crm.pipelines.description", "الوصف")}
          </label>
          <textarea
            rows={2}
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            className="w-full px-3 py-2 text-sm border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-primary-500"
          />
        </div>

        <div className="flex items-center gap-2">
          <input
            type="checkbox"
            id="isDefaultPipeline"
            checked={isDefault}
            onChange={(e) => setIsDefault(e.target.checked)}
            className="h-4 w-4 text-primary-600 rounded border-slate-300"
          />
          <label htmlFor="isDefaultPipeline" className="text-xs font-medium text-slate-700 cursor-pointer">
            {t("crm.pipelines.isDefault", "تعيين كمسار افتراضي للمؤسسة")}
          </label>
        </div>
      </form>
    </Dialog>
  );
};
