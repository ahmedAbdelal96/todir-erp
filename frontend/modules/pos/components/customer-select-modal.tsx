import React from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { UserWalkInIcon } from "./pos-icons.tsx";
import { PartyPicker } from "../../directory/public.ts";

export interface CustomerSelectModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly onSelect: (partyId: string | null, partyName: string | null) => void;
  readonly currentPartyId?: string | null | undefined;
  readonly currentPartyName?: string | null | undefined;
}

/** POS customers come from Directory; anonymous walk-in remains the explicit POS exception. */
export function CustomerSelectModal({
  isOpen,
  onClose,
  onSelect,
  currentPartyId
}: CustomerSelectModalProps): React.JSX.Element | null {
  const { t } = useTranslation("pos");

  const handleSelectWalkIn = () => {
    onSelect(null, null);
    onClose();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => !open && onClose()}
      title={t("customerModal.title")}
      description={t("customerModal.subtitle")}
      size="medium"
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <Button
          variant={currentPartyId ? "secondary" : "primary"}
          onClick={handleSelectWalkIn}
          style={{ width: "100%", justifyContent: "center", display: "flex", alignItems: "center", gap: "0.5rem" }}
        >
          <UserWalkInIcon size={18} />
          <span>{t("customerModal.anonymousCustomer")}</span>
        </Button>

        <PartyPicker
          label={t("customerModal.searchLabel")}
          value={currentPartyId ?? undefined}
          onChange={(partyId, party) => {
            onSelect(partyId, party?.displayName ?? null);
            onClose();
          }}
          roleFilter="Customer"
          enableQuickCreate={false}
          placeholder={t("customerModal.searchPlaceholder")}
        />
      </div>
    </Dialog>
  );
}
