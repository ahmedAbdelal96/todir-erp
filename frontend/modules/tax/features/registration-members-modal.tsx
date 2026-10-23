import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { Spinner } from "../../../components/primitives/spinner.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { LocalDateField } from "../../../components/fields/local-date-field.tsx";
import { TaxDataTable, type TaxTableColumn } from "../components/tax-data-table.tsx";
import { useTaxRegistrationMembers } from "../hooks/tax-queries.ts";
import { taxApi } from "../services/tax-api.ts";
import { extractTaxError } from "../services/tax-error-map.ts";
import type { TaxRegistrationMemberDto } from "../types.ts";

export interface RegistrationMembersModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly registrationId: string;
  readonly legalEntityId: string;
  readonly defaultEffectiveDate: string;
  readonly canManage: boolean;
}

export function RegistrationMembersModal({
  isOpen,
  onClose,
  registrationId,
  legalEntityId,
  defaultEffectiveDate,
  canManage
}: RegistrationMembersModalProps): React.JSX.Element | null {
  const { t } = useTranslation("tax");
  const { data: members, isLoading, refetch } = useTaxRegistrationMembers(registrationId, legalEntityId);

  const [isAdding, setIsAdding] = useState(false);
  const [memberEntityId, setMemberEntityId] = useState("");
  const [effectiveFrom, setEffectiveFrom] = useState(defaultEffectiveDate);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [actionPending, setActionPending] = useState(false);

  const handleAddMember = async () => {
    if (!memberEntityId) return;
    setErrorMessage(null);
    setActionPending(true);
    try {
      await taxApi.addRegistrationMember(registrationId, {
        legalEntityId: memberEntityId,
        effectiveFrom
      });
      setIsAdding(false);
      setMemberEntityId("");
      await refetch();
    } catch (err) {
      const mapped = extractTaxError(err);
      setErrorMessage(t(mapped.messageKey, { defaultValue: mapped.fallbackEn }));
    } finally {
      setActionPending(false);
    }
  };

  const handleEndMember = async (memberId: string) => {
    setErrorMessage(null);
    setActionPending(true);
    try {
      await taxApi.endRegistrationMember(memberId, legalEntityId, defaultEffectiveDate);
      await refetch();
    } catch (err) {
      const mapped = extractTaxError(err);
      setErrorMessage(t(mapped.messageKey, { defaultValue: mapped.fallbackEn }));
    } finally {
      setActionPending(false);
    }
  };

  const columns: readonly TaxTableColumn<TaxRegistrationMemberDto>[] = [
    {
      key: "legalEntityId",
      header: t("registrations.memberEntity"),
      render: (item) => <code>{item.legalEntityId}</code>
    },
    {
      key: "effectiveFrom",
      header: t("registrations.effectiveFrom"),
      render: (item) => <span>{item.effectiveFrom}</span>
    },
    {
      key: "effectiveTo",
      header: t("registrations.effectiveTo"),
      render: (item) => (
        <span style={{ color: item.effectiveTo ? "var(--color-content-muted)" : "var(--color-status-success-text)" }}>
          {item.effectiveTo || t("registrations.currentEffective")}
        </span>
      )
    },
    {
      key: "actions",
      header: "",
      width: "130px",
      render: (item) =>
        canManage && !item.effectiveTo ? (
          <Button
            variant="ghost"
            size="compact"
            onPress={() => void handleEndMember(item.id)}
            isDisabled={actionPending}
          >
            {t("registrations.endMembership")}
          </Button>
        ) : null
    }
  ];

  const footer = (
    <div style={{ display: "flex", justifyContent: "flex-end" }}>
      <Button variant="secondary" onPress={onClose}>
        {t("actions.close")}
      </Button>
    </div>
  );

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
      size="large"
      title={t("registrations.membersTitle")}
      description={t("registrations.membersDescription")}
      footer={footer}
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMessage && (
          <div
            role="alert"
            style={{
              padding: "0.75rem",
              borderRadius: "var(--radius-md)",
              backgroundColor: "var(--color-status-danger-subtle, #fee2e2)",
              color: "var(--color-status-danger-text, #991b1b)",
              fontSize: "0.875rem"
            }}
          >
            {errorMessage}
          </div>
        )}

        {isLoading ? (
          <div style={{ display: "flex", justifyContent: "center", padding: "2rem" }}>
            <Spinner size="large" />
          </div>
        ) : !members || members.length === 0 ? (
          <div style={{ padding: "1rem", textAlign: "center", color: "var(--color-content-muted)" }}>
            {t("registrations.noMembers")}
          </div>
        ) : (
          <TaxDataTable
            data={members}
            columns={columns}
            rowKey={(item) => item.id}
          />
        )}

        {canManage && !isAdding && (
          <div>
            <Button variant="secondary" size="compact" onPress={() => setIsAdding(true)}>
              + {t("registrations.addMember")}
            </Button>
          </div>
        )}

        {canManage && isAdding && (
          <div
            style={{
              padding: "1rem",
              borderRadius: "var(--radius-md)",
              border: "1px solid var(--color-border-subtle, #e5e7eb)",
              backgroundColor: "var(--color-surface-subtle, #f9fafb)",
              display: "flex",
              flexDirection: "column",
              gap: "0.75rem"
            }}
          >
            <TextField
              label={t("registrations.memberEntityIdLabel")}
              value={memberEntityId}
              onChange={setMemberEntityId}
              isRequired
            />
            <LocalDateField
              label={t("registrations.effectiveFrom")}
              value={effectiveFrom}
              onChange={setEffectiveFrom}
              isRequired
            />
            <div style={{ display: "flex", gap: "0.5rem" }}>
              <Button variant="primary" size="compact" onPress={() => void handleAddMember()} isLoading={actionPending}>
                {t("actions.save")}
              </Button>
              <Button variant="secondary" size="compact" onPress={() => setIsAdding(false)}>
                {t("actions.cancel")}
              </Button>
            </div>
          </div>
        )}
      </div>
    </Dialog>
  );
}
