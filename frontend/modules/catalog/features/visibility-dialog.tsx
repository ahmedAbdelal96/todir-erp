import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Select, SelectItem } from "../../../components/primitives/select.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { useSetProductVisibility } from "../hooks/catalog-queries.ts";
import { useRuntimeContext } from "../../../core/runtime/runtime-provider.tsx";
import type { ProductDetails, VisibilityScope } from "../types.ts";

export interface VisibilityDialogProps {
  readonly isOpen: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly product: ProductDetails;
  readonly onUpdated?: () => void;
}

export function VisibilityDialog({
  isOpen,
  onOpenChange,
  product,
  onUpdated
}: VisibilityDialogProps): React.JSX.Element {
  const { t } = useTranslation("catalog");
  let legalEntityId = "";
  let legalEntityName = "";
  try {
    const runtime = useRuntimeContext();
    legalEntityId = runtime.legalEntity.legalEntityId || "";
    legalEntityName = runtime.legalEntity.name || "";
  } catch {
    // fallback
  }

  const [scopeType, setScopeType] = useState<VisibilityScope>("TenantWide");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const setVisibility = useSetProductVisibility();

  const handleClose = (open: boolean) => {
    if (!open) {
      setScopeType("TenantWide");
      setErrorMsg(null);
    }
    onOpenChange(open);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (scopeType === "LegalEntity" && !legalEntityId) {
      setErrorMsg(t("visibilityDialog.errorLegalEntityMissing"));
      return;
    }

    try {
      await setVisibility.mutateAsync({
        id: product.id,
        input: {
          scopeType,
          legalEntityId: scopeType === "LegalEntity" ? legalEntityId : null
        }
      });
      handleClose(false);
      onUpdated?.();
    } catch {
      setErrorMsg(t("visibilityDialog.genericError"));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={handleClose}
      title={t("visibilityDialog.title")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMsg ? (
          <Alert variant="danger" title={t("common.error")}>{errorMsg}</Alert>
        ) : null}

        <p style={{ color: "var(--color-content-secondary)", fontSize: "0.875rem", margin: 0 }}>
          {t("visibilityDialog.description", { name: product.displayName })}
        </p>

        <Select
          label={t("visibilityDialog.scopeLabel")}
          selectedKey={scopeType}
          onSelectionChange={(key) => setScopeType(String(key) as VisibilityScope)}
        >
          <SelectItem id="TenantWide">{t("visibilityDialog.scopeTenantWide")}</SelectItem>
          <SelectItem id="LegalEntity">{t("visibilityDialog.scopeLegalEntity")}</SelectItem>
        </Select>

        {scopeType === "LegalEntity" ? (
          <div style={{ padding: "0.75rem", background: "var(--color-surface-subtle)", borderRadius: "var(--radius-sm)", fontSize: "0.875rem" }}>
            <strong>{t("visibilityDialog.selectedEntityLabel")}:</strong>{" "}
            <span>{legalEntityName || legalEntityId || t("visibilityDialog.currentEntity")}</span>
          </div>
        ) : null}

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "0.5rem" }}>
          <Button variant="secondary" type="button" onPress={() => handleClose(false)}>
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isLoading={setVisibility.isPending}>
            {t("visibilityDialog.submit")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
