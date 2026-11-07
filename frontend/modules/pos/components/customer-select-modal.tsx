import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { UserWalkInIcon } from "./pos-icons.tsx";
import { BidiText } from "../../../components/primitives/bidi-text.tsx";

export interface CustomerSelectModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly onSelect: (partyId: string | null, partyName: string | null) => void;
  readonly currentPartyId?: string | null | undefined;
  readonly currentPartyName?: string | null | undefined;
}

interface RetailCustomer {
  readonly id: string;
  readonly nameAr: string;
  readonly nameEn: string;
  readonly code: string;
}

const RETAIL_CUSTOMERS: readonly RetailCustomer[] = [
  {
    id: "party-atlas-trading",
    nameAr: "أطلس للتجارة العامة",
    nameEn: "Atlas Trading General",
    code: "CUST-001",
  },
  {
    id: "party-nour-supplies",
    nameAr: "النور للتوريدات",
    nameEn: "Al Nour Supplies",
    code: "CUST-002",
  },
  {
    id: "party-pioneers-equipment",
    nameAr: "شركة الرواد للتجهيزات",
    nameEn: "Pioneers Equipment Co.",
    code: "CUST-003",
  },
  {
    id: "party-future-electrical",
    nameAr: "شركة المستقبل للأدوات الكهربائية",
    nameEn: "Future Electrical Tools",
    code: "CUST-004",
  },
];

export function CustomerSelectModal({
  isOpen,
  onClose,
  onSelect,
  currentPartyId,
}: CustomerSelectModalProps): React.JSX.Element | null {
  const { t, i18n } = useTranslation("pos");
  const isAr = i18n.language !== "en";
  const [searchTerm, setSearchTerm] = useState("");

  const filteredCustomers = searchTerm.trim()
    ? RETAIL_CUSTOMERS.filter((c) =>
        c.nameAr.includes(searchTerm.trim()) ||
        c.nameEn.toLowerCase().includes(searchTerm.toLowerCase().trim()) ||
        c.code.toLowerCase().includes(searchTerm.toLowerCase().trim())
      )
    : RETAIL_CUSTOMERS;

  const handleSelectWalkIn = () => {
    onSelect(null, null);
    onClose();
  };

  const handleSelectCustomer = (party: { id: string; name: string }) => {
    onSelect(party.id, party.name);
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
        <div style={{ display: "flex", gap: "0.5rem" }}>
          <Button
            variant={currentPartyId ? "secondary" : "primary"}
            onClick={handleSelectWalkIn}
            style={{ width: "100%", justifyContent: "center", display: "flex", alignItems: "center", gap: "0.5rem" }}
          >
            <UserWalkInIcon size={18} />
            <span>{t("customerModal.anonymousCustomer")}</span>
          </Button>
        </div>

        <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
          <TextField
            label={t("customerModal.searchLabel")}
            placeholder={t("customerModal.searchPlaceholder")}
            value={searchTerm}
            onChange={(val) => setSearchTerm(val)}
            autoFocus
          />
        </div>

        <div
          style={{
            maxHeight: "18rem",
            overflowY: "auto",
            border: "1px solid var(--color-border-default)",
            borderRadius: "var(--radius-md)",
            background: "var(--color-surface-card)",
          }}
        >
          {filteredCustomers.length > 0 ? (
            <ul style={{ listStyle: "none", margin: 0, padding: 0 }}>
              {filteredCustomers.map((party) => {
                const isSelected = party.id === currentPartyId;
                const name = isAr ? party.nameAr : party.nameEn;
                return (
                  <li
                    key={party.id}
                    style={{
                      borderBottom: "1px solid var(--color-border-subtle)",
                      padding: "0.75rem 1rem",
                      display: "flex",
                      justifyContent: "space-between",
                      alignItems: "center",
                      backgroundColor: isSelected ? "var(--color-surface-hover)" : "transparent",
                    }}
                  >
                    <div>
                      <div style={{ fontWeight: 600, color: "var(--color-content-default)" }}>
                        <BidiText>{name}</BidiText>
                      </div>
                      <div style={{ fontSize: "0.8125rem", color: "var(--color-content-muted)" }}>
                        <BidiText>{party.code}</BidiText>
                      </div>
                    </div>
                    <Button
                      variant={isSelected ? "secondary" : "primary"}
                      size="compact"
                      onClick={() => handleSelectCustomer({ id: party.id, name })}
                    >
                      {isSelected ? t("customerModal.selected") : t("customerModal.select")}
                    </Button>
                  </li>
                );
              })}
            </ul>
          ) : (
            <div style={{ padding: "1.5rem", textAlign: "center", color: "var(--color-content-muted)" }}>
              {searchTerm ? t("customerModal.noResults") : t("customerModal.emptyPrompt")}
            </div>
          )}
        </div>
      </div>
    </Dialog>
  );
}
