import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { ServiceSelect as Select } from "../../components/service-select.tsx";
import { useRegisterInstalledBaseMutation } from "../../hooks/use-service-queries.ts";

export interface NewInstalledBaseModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
}

export function NewInstalledBaseModal({
  isOpen,
  onClose,
  legalEntityId
}: NewInstalledBaseModalProps): React.JSX.Element {
  const { t } = useTranslation("service");
  const registerMutation = useRegisterInstalledBaseMutation(legalEntityId);

  const [serialNumber, setSerialNumber] = useState("");
  const [itemCode, setItemCode] = useState("");
  const [itemName, setItemName] = useState("");
  const [customerName, setCustomerName] = useState("");
  const [siteLocation, setSiteLocation] = useState("");
  const [fixedAssetId, setFixedAssetId] = useState("");
  const [warrantyEnd, setWarrantyEnd] = useState("");
  const [status, setStatus] = useState("operational");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!serialNumber.trim() || !itemCode.trim()) {
      setErrorMsg(t("validation.serialAndItemRequired"));
      return;
    }

    try {
      setErrorMsg(null);
      await registerMutation.mutateAsync({
        legalEntityId,
        serialNumber: serialNumber.trim(),
        itemCode: itemCode.trim(),
        itemName: itemName.trim() || undefined,
        customerName: customerName.trim() || undefined,
        siteLocation: siteLocation.trim() || undefined,
        fixedAssetId: fixedAssetId.trim() || undefined,
        warrantyEndDate: warrantyEnd ? new Date(warrantyEnd).toISOString() : undefined,
        status: status.trim() || "operational"
      });
      onClose();
      // Reset form
      setSerialNumber("");
      setItemCode("");
      setItemName("");
      setCustomerName("");
      setSiteLocation("");
      setFixedAssetId("");
      setWarrantyEnd("");
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : t("common.genericError"));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("installedBase.newEquipmentTitle")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMsg && (
          <div
            style={{
              padding: "0.75rem 1rem",
              borderRadius: "var(--radius-sm, 0.375rem)",
              backgroundColor: "#fef2f2",
              border: "1px solid #fecaca",
              color: "#b91c1c",
              fontSize: "0.875rem"
            }}
          >
            {errorMsg}
          </div>
        )}

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("installedBase.serialNumber")} *
            </label>
            <TextField
              value={serialNumber}
              onChange={(val) => setSerialNumber(val)}
              placeholder={t("installedBase.serialPlaceholder", "مثال: SN-GEN-88301")}
              isRequired
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("parts.itemId")} *
            </label>
            <TextField
              value={itemCode}
              onChange={(val) => setItemCode(val)}
              placeholder={t("installedBase.modelPlaceholder", "مثال: CAT-C18")}
              isRequired
            />
          </div>
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
            {t("installedBase.modelName")}
          </label>
          <TextField
            value={itemName}
            onChange={(val) => setItemName(val)}
            placeholder={t("installedBase.assetNamePlaceholder", "مثال: مولد ديزل كاتر بيلر")}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("requests.customerName")}
            </label>
            <TextField
              value={customerName}
              onChange={(val) => setCustomerName(val)}
              placeholder={t("requests.customerPlaceholder")}
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("requests.siteLocation")}
            </label>
            <TextField
              value={siteLocation}
              onChange={(val) => setSiteLocation(val)}
              placeholder={t("requests.sitePlaceholder")}
            />
          </div>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("workOrders.fixedAssetId")} ({t("contexts.internal")})
            </label>
            <TextField
              value={fixedAssetId}
              onChange={(val) => setFixedAssetId(val)}
              placeholder={t("installedBase.fixedAssetPlaceholder", "FA-00192")}
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("installedBase.operationalState")}
            </label>
            <Select
              value={status}
              onChange={(val) => setStatus(val)}
              options={[
                { value: "operational", label: t("installedBase.operational") },
                { value: "degraded", label: t("installedBase.degraded") },
                { value: "down", label: t("installedBase.down") },
                { value: "decommissioned", label: t("installedBase.decommissioned") }
              ]}
            />
          </div>
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
            {t("installedBase.warrantyEnd")}
          </label>
          <input
            type="date"
            value={warrantyEnd}
            onChange={(e) => setWarrantyEnd(e.target.value)}
            style={{
              width: "100%",
              padding: "0.5rem 0.75rem",
              borderRadius: "var(--radius-md, 0.375rem)",
              border: "1px solid var(--color-border-default, #cbd5e1)",
              backgroundColor: "#ffffff",
              fontSize: "0.875rem",
              boxSizing: "border-box"
            }}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isLoading={registerMutation.isPending}>
            {t("installedBase.registerEquipment")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
