import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { useCreatePlantMutation } from "../../hooks/use-manufacturing.ts";

export interface CreatePlantDialogProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function CreatePlantDialog({ isOpen, onClose, legalEntityId }: CreatePlantDialogProps): React.JSX.Element {
  const { t } = useTranslation("manufacturing");
  const plantMutation = useCreatePlantMutation();

  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [timeZone, setTimeZone] = useState("UTC");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!code.trim() || !name.trim()) return;

    plantMutation.mutate(
      {
        legalEntityId,
        code: code.trim().toUpperCase(),
        name: name.trim(),
        timeZone
      },
      {
        onSuccess: () => {
          onClose();
        }
      }
    );
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("definitions.addPlant")}
      size="small"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {plantMutation.isError ? (
          <div role="alert" style={{ padding: "0.75rem", background: "#fef2f2", border: "1px solid #ef4444", borderRadius: "var(--radius-sm)", color: "#b91c1c", fontSize: "0.875rem" }}>
            {t("errors.generalFailure")}
          </div>
        ) : null}

        <TextField
          label={t("definitions.plantCode")}
          value={code}
          onChange={(val) => setCode(val)}
          required
        />

        <TextField
          label={t("definitions.plantName")}
          value={name}
          onChange={(val) => setName(val)}
          required
        />

        <TextField
          label={t("common.timeZone")}
          value={timeZone}
          onChange={(val) => setTimeZone(val)}
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button type="button" variant="outline" onPress={onClose}>
            {t("common.cancel")}
          </Button>
          <Button type="submit" variant="primary" isDisabled={plantMutation.isPending || !code.trim() || !name.trim()}>
            {plantMutation.isPending ? t("common.loading") : t("common.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
