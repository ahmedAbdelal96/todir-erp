import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import {
  Table,
  TableHeader,
  TableBody,
  TableRow,
  TableHead,
  TableCell
} from "../../../components/primitives/table.tsx";
import {
  usePaymentTermHistory,
  useArchivePaymentTermMutation,
  useActivatePaymentTermVersionMutation
} from "../hooks/finbase-queries.ts";
import type { PaymentTermRuleRequest } from "../types.ts";

export interface PaymentTermDrawerProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly termId?: string | null | undefined;
}

export function PaymentTermDrawer({
  isOpen,
  onClose,
  termId
}: PaymentTermDrawerProps): React.JSX.Element | null {
  const { t } = useTranslation("finbase");
  const { data: termHistory, isLoading, error } = usePaymentTermHistory(termId ?? undefined);
  const archiveMutation = useArchivePaymentTermMutation();
  const activateMutation = useActivatePaymentTermVersionMutation();

  const [actionError, setActionError] = useState<string>();

  if (!isOpen || !termId) return null;

  const handleArchive = async () => {
    if (!termHistory) return;
    setActionError(undefined);
    try {
      await archiveMutation.mutateAsync({
        termId: termHistory.id,
        request: { expectedTermVersion: termHistory.version }
      });
      onClose();
    } catch {
      setActionError(t("paymentTerms.drawer.archiveError"));
    }
  };

  const handleActivate = async (versionId: string) => {
    if (!termHistory) return;
    setActionError(undefined);
    try {
      await activateMutation.mutateAsync({
        termId: termHistory.id,
        versionId,
        request: { expectedTermVersion: termHistory.version }
      });
    } catch {
      setActionError(t("paymentTerms.drawer.activateError"));
    }
  };

  const renderRuleOffset = (rule: PaymentTermRuleRequest) => {
    const parts: string[] = [];
    if (rule.days > 0) {
      parts.push(t("paymentTerms.rules.daysFormat", { days: rule.days }));
    }
    if (rule.dayOfMonth) {
      parts.push(t("paymentTerms.rules.dayOfMonthFormat", { day: rule.dayOfMonth }));
    }
    if (rule.monthOffset > 0) {
      parts.push(t("paymentTerms.rules.monthOffsetFormat", { months: rule.monthOffset }));
    }
    return parts.length > 0 ? parts.join(" · ") : "—";
  };

  return (
    <Dialog
      isOpen={isOpen}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
      title={
        termHistory
          ? `${termHistory.code} — ${termHistory.displayNameAr}`
          : t("paymentTerms.drawer.loadingTitle")
      }
      description={termHistory?.displayNameEn ?? undefined}
      size="large"
    >
      <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
        {actionError ? (
          <Alert variant="danger" title={t("paymentTerms.drawer.errorTitle")}>
            {actionError}
          </Alert>
        ) : null}

        {isLoading ? (
          <p style={{ color: "var(--color-content-muted)" }}>
            {t("paymentTerms.drawer.loading")}
          </p>
        ) : error || !termHistory ? (
          <Alert variant="danger" title={t("paymentTerms.drawer.errorTitle")}>
            {t("paymentTerms.drawer.loadError")}
          </Alert>
        ) : (
          <>
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                padding: "var(--space-3)",
                backgroundColor: "var(--color-surface-subtle)",
                borderRadius: "var(--radius-md)",
                border: "1px solid var(--color-border-subtle)"
              }}
            >
              <div style={{ display: "flex", alignItems: "center", gap: "var(--space-3)" }}>
                <span style={{ fontSize: "var(--font-size-sm)", fontWeight: 600 }}>
                  {t("paymentTerms.drawer.termStatus")}:
                </span>
                <StatusBadge
                  intent={termHistory.status === "Active" ? "success" : "neutral"}
                  label={
                    termHistory.status === "Active"
                      ? t("paymentTerms.status.Active")
                      : t("paymentTerms.status.Archived")
                  }
                />
              </div>

              {termHistory.status === "Active" ? (
                <Button
                  variant="secondary"
                  type="button"
                  onPress={() => void handleArchive()}
                  isDisabled={archiveMutation.isPending}
                >
                  {archiveMutation.isPending
                    ? t("paymentTerms.drawer.archiving")
                    : t("paymentTerms.drawer.archiveButton")}
                </Button>
              ) : null}
            </div>

            <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
              <h4 style={{ fontSize: "var(--font-size-sm)", fontWeight: 700, margin: 0 }}>
                {t("paymentTerms.drawer.versionsTitle")}
              </h4>

              {termHistory.versions.map((ver) => (
                <div
                  key={ver.id}
                  style={{
                    border: "1px solid var(--color-border-subtle)",
                    borderRadius: "var(--radius-md)",
                    padding: "var(--space-3)",
                    display: "flex",
                    flexDirection: "column",
                    gap: "var(--space-3)"
                  }}
                >
                  <div
                    style={{
                      display: "flex",
                      justifyContent: "space-between",
                      alignItems: "center",
                      flexWrap: "wrap",
                      gap: "var(--space-2)"
                    }}
                  >
                    <div style={{ display: "flex", alignItems: "center", gap: "var(--space-2)" }}>
                      <span style={{ fontWeight: 600, fontSize: "var(--font-size-sm)" }}>
                        {t("paymentTerms.drawer.versionNumber", { number: ver.versionNumber })}
                      </span>
                      <StatusBadge
                        intent={
                          ver.status === "Active"
                            ? "success"
                            : ver.status === "Draft"
                            ? "warning"
                            : "neutral"
                        }
                        label={
                          ver.status === "Active"
                            ? t("paymentTerms.versionStatus.Active")
                            : ver.status === "Draft"
                            ? t("paymentTerms.versionStatus.Draft")
                            : ver.status === "Superseded"
                            ? t("paymentTerms.versionStatus.Superseded")
                            : t("paymentTerms.versionStatus.Retired")
                        }
                        size="small"
                      />
                    </div>

                    <div style={{ display: "flex", alignItems: "center", gap: "var(--space-2)", fontSize: "var(--font-size-xs)", color: "var(--color-content-muted)" }}>
                      <span>
                        {t("paymentTerms.drawer.effectiveFrom")}: <strong dir="ltr">{ver.effectiveFrom}</strong>
                      </span>
                      {ver.effectiveTo ? (
                        <span>
                          {t("paymentTerms.drawer.effectiveTo")}: <strong dir="ltr">{ver.effectiveTo}</strong>
                        </span>
                      ) : null}
                    </div>
                  </div>

                  {ver.description && !ver.description.toLowerCase().startsWith("canonical development") ? (
                    <p style={{ margin: 0, fontSize: "var(--font-size-xs)", color: "var(--color-content-secondary)" }}>
                      {ver.description}
                    </p>
                  ) : null}

                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead>{t("paymentTerms.rules.seq")}</TableHead>
                        <TableHead>{t("paymentTerms.rules.allocation")}</TableHead>
                        <TableHead>{t("paymentTerms.rules.value")}</TableHead>
                        <TableHead>{t("paymentTerms.rules.dueBasis")}</TableHead>
                        <TableHead>{t("paymentTerms.rules.offset")}</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {ver.rules.map((rule) => (
                        <TableRow key={rule.sequence}>
                          <TableCell>{rule.sequence}</TableCell>
                          <TableCell>
                            <StatusBadge
                              intent={rule.allocationType === "Balance" ? "info" : "neutral"}
                              label={t(`paymentTerms.allocations.${rule.allocationType}`)}
                              size="small"
                            />
                          </TableCell>
                          <TableCell>
                            <span dir="ltr">
                              {rule.allocationType === "Percentage"
                                ? `${rule.percentage ?? ""}`
                                : rule.allocationType === "Fixed"
                                ? rule.fixedAmount ?? ""
                                : t("paymentTerms.rules.balanceRemaining")}
                            </span>
                          </TableCell>
                          <TableCell>
                            {t(`paymentTerms.dueBases.${rule.dueDateBasis}`)}
                          </TableCell>
                          <TableCell>
                            <span dir="ltr">
                              {renderRuleOffset(rule)}
                            </span>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>

                  {ver.status === "Draft" ? (
                    <div style={{ display: "flex", justifyContent: "flex-end", marginTop: "var(--space-2)" }}>
                      <Button
                        variant="primary"
                        type="button"
                        onPress={() => void handleActivate(ver.id)}
                        isDisabled={activateMutation.isPending}
                      >
                        {activateMutation.isPending
                          ? t("paymentTerms.drawer.activating")
                          : t("paymentTerms.drawer.activateButton")}
                      </Button>
                    </div>
                  ) : null}
                </div>
              ))}
            </div>

            <div style={{ display: "flex", justifyContent: "flex-end", marginTop: "var(--space-2)" }}>
              <Button variant="secondary" type="button" onPress={onClose}>
                {t("paymentTerms.drawer.close")}
              </Button>
            </div>
          </>
        )}
      </div>
    </Dialog>
  );
}
