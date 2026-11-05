import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Checkbox } from "../../../../components/primitives/checkbox.tsx";
import { useCreateStageMutation } from "../../hooks/use-crm-queries.ts";

interface NewStageModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly pipelineId: string;
  readonly currentStagesCount: number;
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
  const [probability, setProbability] = useState<string>("50");
  const [isWon, setIsWon] = useState(false);
  const [isLost, setIsLost] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const createMutation = useCreateStageMutation(legalEntityId, pipelineId);

  const reset = () => {
    setCode("");
    setName("");
    setProbability("50");
    setIsWon(false);
    setIsLost(false);
    setErrorMessage(null);
  };

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
          reset();
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
      onOpenChange={(open: boolean) => {
        if (!open) {
          reset();
          onClose();
        }
      }}
      title={t("crm.stages.newTitle", "إضافة مرحلة جديدة للمسار")}
      size="medium"
      footer={
        <>
          <Button variant="secondary" onPress={() => { reset(); onClose(); }} isDisabled={createMutation.isPending}>
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
      <form id="new-stage-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMessage && (
          <div
            role="alert"
            style={{
              padding: "0.625rem 0.875rem",
              borderRadius: "var(--radius-sm)",
              backgroundColor: "var(--color-status-danger-subtle, #fee2e2)",
              color: "var(--color-status-danger, #b91c1c)",
              fontSize: "0.8125rem"
            }}
          >
            {errorMessage}
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))", gap: "0.875rem" }}>
          <TextField
            label={t("crm.stages.code", "رمز المرحلة")}
            value={code}
            onChange={(val) => setCode(val.toUpperCase())}
            placeholder={t("crm.stages.codePlaceholder", "DEMO")}
            isRequired
          />
          <TextField
            label={t("crm.stages.name", "اسم المرحلة")}
            value={name}
            onChange={setName}
            placeholder={t("crm.stages.namePlaceholder", "مثال: تقديم العرض التوضيحي")}
            isRequired
          />
        </div>

        <TextField
          label={t("crm.stages.probability", "احتمالية الإغلاق (%)")}
          type="number"
          value={probability}
          onChange={setProbability}
          placeholder="50"
        />

        <div style={{ display: "flex", gap: "1.5rem", flexWrap: "wrap", marginTop: "0.25rem" }}>
          <Checkbox
            isSelected={isWon}
            onChange={(checked) => {
              setIsWon(checked);
              if (checked) setIsLost(false);
            }}
          >
            {t("crm.stages.isWon", "مرحلة نجاح وفوز")}
          </Checkbox>

          <Checkbox
            isSelected={isLost}
            onChange={(checked) => {
              setIsLost(checked);
              if (checked) setIsWon(false);
            }}
          >
            {t("crm.stages.isLost", "مرحلة خسارة الصفقة")}
          </Checkbox>
        </div>
      </form>
    </Dialog>
  );
};
