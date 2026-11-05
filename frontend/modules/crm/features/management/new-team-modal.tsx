import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { useCreateTeamMutation } from "../../hooks/use-crm-queries.ts";

interface NewTeamModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export const NewTeamModal: React.FC<NewTeamModalProps> = ({
  isOpen,
  onClose,
  legalEntityId,
}) => {
  const { t } = useTranslation("crm");
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const createMutation = useCreateTeamMutation(legalEntityId);

  const reset = () => {
    setCode("");
    setName("");
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
        legalEntityId,
        code: code.trim().toUpperCase(),
        name: name.trim(),
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
      title={t("crm.teams.newTitle", "إنشاء فريق مبيعات جديد")}
      size="medium"
      footer={
        <>
          <Button variant="secondary" onPress={() => { reset(); onClose(); }} isDisabled={createMutation.isPending}>
            {t("crm.common.cancel", "إلغاء")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            form="new-team-form"
            isDisabled={createMutation.isPending}
          >
            {createMutation.isPending ? t("crm.common.saving", "جاري الحفظ...") : t("crm.common.save", "حفظ")}
          </Button>
        </>
      }
    >
      <form id="new-team-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
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
            label={t("crm.teams.code", "رمز الفريق")}
            value={code}
            onChange={(val) => setCode(val.toUpperCase())}
            placeholder={t("crm.teams.codePlaceholder", "TEAM-CAIRO")}
            isRequired
          />
          <TextField
            label={t("crm.teams.name", "اسم الفريق")}
            value={name}
            onChange={setName}
            placeholder={t("crm.teams.namePlaceholder", "مثال: فريق مبيعات القاهرة الكبرى")}
            isRequired
          />
        </div>
      </form>
    </Dialog>
  );
};
