import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { TextArea } from "../../../../components/primitives/text-area.tsx";
import { Checkbox } from "../../../../components/primitives/checkbox.tsx";
import { useCreatePipelineMutation } from "../../hooks/use-crm-queries.ts";

interface NewPipelineModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
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

  const reset = () => {
    setCode("");
    setName("");
    setDescription("");
    setIsDefault(false);
    setErrorMessage(null);
  };

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
      title={t("crm.pipelines.newTitle", "إنشاء مسار بيعي جديد")}
      size="medium"
      footer={
        <>
          <Button variant="secondary" onPress={() => { reset(); onClose(); }} isDisabled={createMutation.isPending}>
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
      <form id="new-pipeline-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
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
            label={t("crm.pipelines.code", "رمز المسار (Code)")}
            value={code}
            onChange={(val) => setCode(val.toUpperCase())}
            placeholder={t("crm.pipelines.codePlaceholder", "ENTERPRISE")}
            isRequired
          />
          <TextField
            label={t("crm.pipelines.name", "اسم المسار")}
            value={name}
            onChange={setName}
            placeholder={t("crm.pipelines.namePlaceholder", "مثال: مبيعات كبار العملاء")}
            isRequired
          />
        </div>

        <TextArea
          label={t("crm.pipelines.description", "الوصف")}
          value={description}
          onChange={setDescription}
          rows={2}
        />

        <div style={{ marginTop: "0.25rem" }}>
          <Checkbox
            isSelected={isDefault}
            onChange={setIsDefault}
          >
            {t("crm.pipelines.isDefault", "تعيين كمسار افتراضي للمؤسسة")}
          </Checkbox>
        </div>
      </form>
    </Dialog>
  );
};
