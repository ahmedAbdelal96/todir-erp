import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { useCreateDimension } from "../../hooks/use-ledger-queries.ts";
import { mapLedgerError } from "../../services/ledger-error-map.ts";

export interface DimensionsViewProps {
  readonly legalEntityId: string;
  readonly canManageDimensions: boolean;
}

export function DimensionsView({
  legalEntityId,
  canManageDimensions
}: DimensionsViewProps): React.JSX.Element {
  const { t, i18n } = useTranslation("accounting");
  const createDimMutation = useCreateDimension();

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [code, setCode] = useState("");
  const [nameAr, setNameAr] = useState("");
  const [nameEn, setNameEn] = useState("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const currentLang = i18n.language?.startsWith("ar") ? "ar" : "en";

  const handleCreate = async () => {
    setErrorMessage(null);
    if (!code.trim() || !nameAr.trim() || !nameEn.trim()) {
      setErrorMessage(t("account.validationRequiredFields"));
      return;
    }
    try {
      await createDimMutation.mutateAsync({
        legalEntityId,
        request: {
          code: code.trim().toUpperCase(),
          nameAr: nameAr.trim(),
          nameEn: nameEn.trim(),
          valueOwnerModule: "ledger"
        }
      });
      setIsModalOpen(false);
      setCode("");
      setNameAr("");
      setNameEn("");
    } catch (err) {
      setErrorMessage(mapLedgerError(err, currentLang));
    }
  };

  return (
    <Surface style={{ padding: "1.25rem 1.5rem", width: "100%", maxWidth: "100%", boxSizing: "border-box", minWidth: 0 }}>
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: "1rem",
          marginBottom: "1.25rem"
        }}
      >
        <div>
          <h2 style={{ margin: 0, fontSize: "1.25rem", fontWeight: 700 }}>
            {t("dimensions.title")}
          </h2>
          <p style={{ margin: "0.25rem 0 0", color: "var(--color-content-muted)", fontSize: "0.875rem" }}>
            {t("dimensions.description")}
          </p>
        </div>

        {canManageDimensions && (
          <Button variant="primary" onPress={() => setIsModalOpen(true)}>
            + {t("dimensions.newDimension")}
          </Button>
        )}
      </div>

      <div
        style={{
          padding: "1.5rem",
          borderRadius: "var(--radius-md, 0.5rem)",
          backgroundColor: "var(--color-surface-sunken)",
          border: "1px solid var(--color-border-default)",
          fontSize: "0.875rem",
          color: "var(--color-content-secondary)"
        }}
      >
        <p style={{ margin: "0 0 0.5rem", fontWeight: 600 }}>{t("dimensions.infoTitle")}</p>
        <p style={{ margin: 0 }}>{t("dimensions.infoBody")}</p>
      </div>

      <Dialog
        isOpen={isModalOpen}
        onOpenChange={(open) => !open && setIsModalOpen(false)}
        title={t("dimensions.newDimension")}
        size="medium"
        footer={
          <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
            <Button variant="secondary" onPress={() => setIsModalOpen(false)}>
              {t("common.cancel")}
            </Button>
            <Button
              variant="primary"
              isLoading={createDimMutation.isPending}
              onPress={handleCreate}
            >
              {t("common.save")}
            </Button>
          </div>
        }
      >
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
          <TextField
            label={t("dimensions.code")}
            value={code}
            onChange={setCode}
            isRequired
          />
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
            <TextField
              label={t("dimensions.nameAr")}
              value={nameAr}
              onChange={setNameAr}
              isRequired
            />
            <TextField
              label={t("dimensions.nameEn")}
              value={nameEn}
              onChange={setNameEn}
              isRequired
            />
          </div>

          {errorMessage && (
            <div style={{ padding: "0.75rem", color: "var(--color-status-danger)", backgroundColor: "#fee2e2", borderRadius: "0.5rem" }}>
              {errorMessage}
            </div>
          )}
        </div>
      </Dialog>
    </Surface>
  );
}
