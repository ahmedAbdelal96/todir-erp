import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { useAddTeamMemberMutation } from "../../hooks/use-crm-queries.ts";
import { UserPlus, AlertCircle } from "../../components/crm-icons.tsx";

interface AddTeamMemberModalProps {
  isOpen: boolean;
  onClose: () => void;
  legalEntityId: string;
  teamId: string;
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
          onClose();
        },
        onError: (err: any) => {
          setErrorMessage(err?.response?.data?.detail || err?.message || t("crm.common.saveFailed", "فشل الإضافة"));
        },
      }
    );
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open: boolean) => !open && onClose()}
      title={t("crm.teams.addMemberTitle", "إضافة عضو لفريق المبيعات")}
      size="medium"
      footer={
        <>
          <Button variant="secondary" onPress={onClose} isDisabled={addMemberMutation.isPending}>
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
      <form id="add-member-form" onSubmit={handleSubmit} className="space-y-4">
        {errorMessage && (
          <div className="p-3 rounded bg-rose-50 border border-rose-200 text-rose-700 text-xs flex items-center gap-2">
            <AlertCircle className="h-4 w-4 shrink-0" />
            <span>{errorMessage}</span>
          </div>
        )}

        <div>
          <label className="block text-xs font-semibold text-slate-700 mb-1">
            {t("crm.teams.userId", "معرف المستخدم أو الموظف")} <span className="text-rose-500">*</span>
          </label>
          <input
            type="text"
            required
            placeholder="00000000-0000-0000-0000-000000000000"
            value={userId}
            onChange={(e) => setUserId(e.target.value)}
            className="w-full px-3 py-2 text-sm border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-primary-500 font-mono text-xs"
          />
        </div>

        <div>
          <label className="block text-xs font-semibold text-slate-700 mb-1">
            {t("crm.teams.memberRole", "الدور في الفريق")}
          </label>
          <select
            value={role}
            onChange={(e) => setRole(e.target.value)}
            className="w-full px-3 py-2 text-sm border border-slate-300 rounded-md focus:outline-none focus:ring-2 focus:ring-primary-500"
          >
            <option value="SalesRepresentative">{t("crm.teams.roleRep", "مسؤول مبيعات (Sales Rep)")}</option>
            <option value="TeamLeader">{t("crm.teams.roleLead", "مشرف فريق (Team Leader)")}</option>
            <option value="AccountExecutive">{t("crm.teams.roleAE", "مسؤول حسابات استراتيجية (AE)")}</option>
            <option value="SalesManager">{t("crm.teams.roleManager", "مدير مبيعات (Manager)")}</option>
          </select>
        </div>

        <div className="flex items-center gap-2">
          <input
            type="checkbox"
            id="isTeamLeaderCheck"
            checked={isLead}
            onChange={(e) => setIsLead(e.target.checked)}
            className="h-4 w-4 text-primary-600 rounded border-slate-300"
          />
          <label htmlFor="isTeamLeaderCheck" className="text-xs font-medium text-slate-700 cursor-pointer">
            {t("crm.teams.isTeamLead", "تعيين كقائد أو رئيس للفريق")}
          </label>
        </div>
      </form>
    </Dialog>
  );
};
