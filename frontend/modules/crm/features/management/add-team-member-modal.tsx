import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Select } from "../../../../components/primitives/select.tsx";
import { Checkbox } from "../../../../components/primitives/checkbox.tsx";
import { useAddTeamMemberMutation } from "../../hooks/use-crm-queries.ts";

interface AddTeamMemberModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly teamId: string;
}

export const AddTeamMemberModal: React.FC<AddTeamMemberModalProps> = ({
  isOpen,
  onClose,
  legalEntityId,
  teamId,
}) => {
  const { t } = useTranslation("crm");
  const [userId, setUserId] = useState("");
  const [role, setRole] = useState("SalesRepresentative");
  const [isLead, setIsLead] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const addMemberMutation = useAddTeamMemberMutation(legalEntityId, teamId);

  const reset = () => {
    setUserId("");
    setRole("SalesRepresentative");
    setIsLead(false);
    setErrorMessage(null);
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);

    if (!userId.trim()) {
      setErrorMessage(t("crm.validation.userRequired", "يرجى تحديد معرف المستخدم"));
      return;
    }

    addMemberMutation.mutate(
      {
        userId: userId.trim(),
        role: isLead ? "Lead" : role,
      },
      {
        onSuccess: () => {
          reset();
          onClose();
        },
        onError: (err: any) => {
          setErrorMessage(err?.response?.data?.detail || err?.message || t("crm.common.saveFailed", "فشل الإضافة"));
        },
      }
    );
  };

  const roleOptions = [
    { id: "SalesRepresentative", label: t("crm.teams.roleRep", "مسؤول مبيعات (Sales Rep)") },
    { id: "TeamLeader", label: t("crm.teams.roleLead", "مشرف فريق (Team Leader)") },
    { id: "AccountExecutive", label: t("crm.teams.roleAE", "مسؤول حسابات استراتيجية (AE)") },
    { id: "SalesManager", label: t("crm.teams.roleManager", "مدير مبيعات (Manager)") },
  ];

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => {
        if (!open) {
          reset();
          onClose();
        }
      }}
      title={t("crm.teams.addMemberTitle", "إضافة عضو لفريق المبيعات")}
      size="medium"
      footer={
        <>
          <Button variant="secondary" onPress={() => { reset(); onClose(); }} isDisabled={addMemberMutation.isPending}>
            {t("crm.common.cancel", "إلغاء")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            form="add-member-form"
            isDisabled={addMemberMutation.isPending}
          >
            {addMemberMutation.isPending ? t("crm.common.saving", "جاري الإضافة...") : t("crm.common.add", "إضافة")}
          </Button>
        </>
      }
    >
      <form id="add-member-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
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

        <TextField
          label={t("crm.teams.userId", "معرف المستخدم أو الموظف")}
          value={userId}
          onChange={setUserId}
          placeholder="00000000-0000-0000-0000-000000000000"
          isRequired
        />

        <Select
          label={t("crm.teams.memberRole", "الدور في الفريق")}
          selectedKey={role}
          onSelectionChange={(val) => {
            if (val) setRole(String(val));
          }}
          options={roleOptions}
        />

        <div style={{ marginTop: "0.25rem" }}>
          <Checkbox
            isSelected={isLead}
            onChange={setIsLead}
          >
            {t("crm.teams.isTeamLead", "تعيين كقائد أو رئيس للفريق")}
          </Checkbox>
        </div>
      </form>
    </Dialog>
  );
};
