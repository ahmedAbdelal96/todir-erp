import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { useCreateSubstitutionGroupMutation, useAddSubstituteMutation } from "../../hooks/use-manufacturing.ts";

export interface CreateSubstitutionGroupDialogProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
}

export function CreateSubstitutionGroupDialog({ isOpen, onClose }: CreateSubstitutionGroupDialogProps): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const createGroupMutation = useCreateSubstitutionGroupMutation();
  const addSubstituteMutation = useAddSubstituteMutation();

  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [policy, setPolicy] = useState<"Suggest" | "Automatic" | "Forbidden">("Suggest");

  // Optional first substitute item
  const [substituteVariantId, setSubstituteVariantId] = useState("");
  const [replacementRatio, setReplacementRatio] = useState("1.0");
  const [uomCode, setUomCode] = useState("KG");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!code.trim() || !name.trim()) return;

    createGroupMutation.mutate(
      {
        code: code.trim().toUpperCase(),
        name: name.trim(),
        policy
      },
      {
        onSuccess: (group) => {
          if (substituteVariantId.trim()) {
            addSubstituteMutation.mutate({
              groupId: group.id,
              request: {
                productVariantId: substituteVariantId.trim(),
                priority: 1,
                validFrom: new Date().toISOString().slice(0, 10),
                replacementRatio: parseFloat(replacementRatio) || 1.0,
                uomCode: uomCode.trim().toUpperCase(),
                autoApplyAllowed: policy === "Automatic"
              }
            });
          }
          onClose();
        }
      }
    );
  };

  const isLoading = createGroupMutation.isPending || addSubstituteMutation.isPending;

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("definitions.addSubstituteGroup")}
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "16px" }}>
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "12px" }}>
          <TextField
            label={t("definitions.substituteGroupCode")}
            value={code}
            onChange={(e) => setCode(e.target.value)}
            required
          />
          <TextField
            label={t("definitions.substituteGroupName")}
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
          />
        </div>

        <div style={{ display: "flex", flexDirection: "column", gap: "6px" }}>
          <label style={{ fontSize: "13px", fontWeight: 600, color: "#1e293b" }}>
            {t("definitions.substitutionPolicy")}
          </label>
          <select
            value={policy}
            onChange={(e) => setPolicy(e.target.value as "Suggest" | "Automatic" | "Forbidden")}
            style={{
              padding: "8px 12px",
              borderRadius: "6px",
              border: "1px solid #cbd5e1",
              fontSize: "14px",
              background: "#ffffff"
            }}
          >
            <option value="Suggest">{t("definitions.policySuggest")}</option>
            <option value="Automatic">{t("definitions.policyAuto")}</option>
            <option value="Forbidden">{t("definitions.policyForbidden")}</option>
          </select>
        </div>

        <div style={{ borderTop: "1px solid #cbd5e1", paddingTop: "12px" }}>
          <div style={{ fontSize: "13px", fontWeight: 600, color: "#1e293b", marginBottom: "8px" }}>
            {t("definitions.firstSubstitute")}
          </div>
          <TextField
            label={t("definitions.substituteVariantId")}
            value={substituteVariantId}
            onChange={(e) => setSubstituteVariantId(e.target.value)}
          />
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "12px", marginTop: "8px" }}>
            <TextField
              label={t("definitions.replacementRatio")}
              type="number"
              value={replacementRatio}
              onChange={(e) => setReplacementRatio(e.target.value)}
            />
            <TextField
              label={t("orders.uom")}
              value={uomCode}
              onChange={(e) => setUomCode(e.target.value)}
            />
          </div>
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "8px", marginTop: "12px" }}>
          <Button type="button" variant="secondary" onClick={onClose} disabled={isLoading}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" variant="primary" disabled={isLoading || !code || !name}>
            {isLoading ? t("common.loading") : t("common.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
