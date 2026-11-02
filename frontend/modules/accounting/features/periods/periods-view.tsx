import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";
import { PeriodStatusBadge } from "../../components/ledger-status-badges.tsx";
import { AccountingDataTable, type AccountingTableColumn } from "../../components/accounting-data-table.tsx";
import { PeriodCloseReadinessModal } from "./period-close-readiness-modal.tsx";
import { AccountingPeriodCloseModal } from "./accounting-period-close-modal.tsx";
import { AccountingPeriodReopenModal } from "./accounting-period-reopen-modal.tsx";
import {
  useCreateFiscalYear,
  useCreatePeriod,
  useSoftClosePeriod,
  useClosePeriod,
  useReopenPeriod
} from "../../hooks/use-ledger-queries.ts";
import { mapLedgerError } from "../../services/ledger-error-map.ts";
import type { FiscalYearDto, PeriodDto } from "../../types.ts";

export interface PeriodsViewProps {
  readonly legalEntityId: string;
  readonly fiscalYears: readonly FiscalYearDto[];
  readonly periods: readonly PeriodDto[];
  readonly isLoading: boolean;
  readonly canManagePeriods: boolean;
  readonly canSoftClose: boolean;
  readonly canClose: boolean;
  readonly canReopen: boolean;
}

export function PeriodsView({
  legalEntityId,
  fiscalYears,
  periods,
  isLoading,
  canManagePeriods,
  canSoftClose,
  canClose,
  canReopen
}: PeriodsViewProps): React.JSX.Element {
  const { t, i18n } = useTranslation("accounting");
  const createYearMutation = useCreateFiscalYear();
  const createPeriodMutation = useCreatePeriod();
  const softCloseMutation = useSoftClosePeriod();
  const closeMutation = useClosePeriod();
  const reopenMutation = useReopenPeriod();

  const [selectedYearId, setSelectedYearId] = useState<string>("ALL");
  const [isYearModalOpen, setIsYearModalOpen] = useState(false);
  const [isPeriodModalOpen, setIsPeriodModalOpen] = useState(false);

  // Close Readiness Modal State
  const [readinessPeriod, setReadinessPeriod] = useState<PeriodDto | null>(null);
  const [accountingClosePeriod, setAccountingClosePeriod] = useState<PeriodDto | null>(null);
  const [accountingReopenPeriod, setAccountingReopenPeriod] = useState<PeriodDto | null>(null);

  // Year form
  const [yearCode, setYearCode] = useState("");
  const [yearStart, setYearStart] = useState("");
  const [yearEnd, setYearEnd] = useState("");

  // Period form
  const [periodTargetYearId, setPeriodTargetYearId] = useState("");
  const [periodCode, setPeriodCode] = useState("");
  const [periodStart, setPeriodStart] = useState("");
  const [periodEnd, setPeriodEnd] = useState("");
  const [periodSequence, setPeriodSequence] = useState<number>(1);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const currentLang = i18n.language?.startsWith("ar") ? "ar" : "en";

  const isSyntheticTestYear = (code: string) => /[a-f0-9]{8}/i.test(code) || code.length > 12;
  const cleanFiscalYears = fiscalYears.filter((fy) => !isSyntheticTestYear(fy.code));
  const displayFiscalYears = cleanFiscalYears.length > 0 ? cleanFiscalYears : fiscalYears;

  const formatFiscalYearLabel = (fy: FiscalYearDto) => {
    const yearNumber = fy.code.replace(/^FY-?/i, "");
    const yearTitle = /^\d{4}$/.test(yearNumber) ? `السنة المالية ${yearNumber}` : fy.code;
    const statusLabel = fy.status === "Open" ? "مفتوحة" : fy.status === "Closed" ? "مغلقة" : fy.status;
    return `${yearTitle} (${statusLabel})`;
  };

  const filteredPeriods = periods
    .filter((p) => {
      if (selectedYearId === "ALL") {
        if (cleanFiscalYears.length > 0) {
          return cleanFiscalYears.some((fy) => fy.id === p.fiscalYearId);
        }
        return true;
      }
      return p.fiscalYearId === selectedYearId;
    })
    .sort((a, b) => a.startDate.localeCompare(b.startDate));

  const handleCreateYear = async () => {
    setErrorMessage(null);
    if (!yearCode || !yearStart || !yearEnd) {
      setErrorMessage(t("account.validationRequiredFields"));
      return;
    }
    try {
      await createYearMutation.mutateAsync({
        legalEntityId,
        request: { code: yearCode, startDate: yearStart, endDate: yearEnd }
      });
      setIsYearModalOpen(false);
      setYearCode("");
      setYearStart("");
      setYearEnd("");
    } catch (err) {
      setErrorMessage(mapLedgerError(err, currentLang));
    }
  };

  const handleCreatePeriod = async () => {
    setErrorMessage(null);
    if (!periodTargetYearId || !periodCode || !periodStart || !periodEnd) {
      setErrorMessage(t("account.validationRequiredFields"));
      return;
    }
    try {
      await createPeriodMutation.mutateAsync({
        legalEntityId,
        request: {
          fiscalYearId: periodTargetYearId,
          code: periodCode,
          startDate: periodStart,
          endDate: periodEnd,
          sequence: periodSequence
        }
      });
      setIsPeriodModalOpen(false);
      setPeriodCode("");
      setPeriodStart("");
      setPeriodEnd("");
    } catch (err) {
      setErrorMessage(mapLedgerError(err, currentLang));
    }
  };

  const periodColumns: readonly AccountingTableColumn<PeriodDto>[] = [
    {
      key: "code",
      header: t("period.code"),
      width: "120px",
      render: (item) => <span style={{ fontWeight: 600 }}>{item.code}</span>
    },
    {
      key: "startDate",
      header: t("period.startDate"),
      width: "130px",
      render: (item) => <LocalDateDisplay value={item.startDate} />
    },
    {
      key: "endDate",
      header: t("period.endDate"),
      width: "130px",
      render: (item) => <LocalDateDisplay value={item.endDate} />
    },
    {
      key: "sequence",
      header: t("period.sequence"),
      width: "90px",
      align: "center",
      render: (item) => <span>{item.sequence}</span>
    },
    {
      key: "status",
      header: t("account.status"),
      width: "140px",
      render: (item) => <PeriodStatusBadge status={item.status} />
    },
    {
      key: "actions",
      header: t("common.actions"),
      width: "280px",
      align: "end",
      render: (item) => (
        <div style={{ display: "flex", gap: "0.5rem", justifyContent: "flex-end", flexWrap: "wrap" }}>
          <Button
            size="compact"
            variant="secondary"
            onPress={() => setReadinessPeriod(item)}
          >
            {t("period.checkCloseReadiness")}
          </Button>

          {canSoftClose && item.status === "Open" && (
            <Button
              size="compact"
              variant="secondary"
              isLoading={softCloseMutation.isPending}
              onPress={() => softCloseMutation.mutate({ legalEntityId, periodId: item.id })}
            >
              {t("period.softClose")}
            </Button>
          )}

          {canClose && (item.status === "Open" || item.status === "SoftClosed") && (
            <Button
              size="compact"
              variant="primary"
              data-testid="btn-open-close-period"
              onPress={() => setAccountingClosePeriod(item)}
            >
              {t("period.close")}
            </Button>
          )}

          {canReopen && (item.status === "SoftClosed" || item.status === "Closed") && (
            <Button
              size="compact"
              variant="secondary"
              onPress={() => setAccountingReopenPeriod(item)}
            >
              {t("period.reopen")}
            </Button>
          )}
        </div>
      )
    }
  ];

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
            {t("period.fiscalYearsAndPeriods")}
          </h2>
          <p style={{ margin: "0.25rem 0 0", color: "var(--color-content-muted)", fontSize: "0.875rem" }}>
            {t("period.description")}
          </p>
        </div>

        {canManagePeriods && (
          <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
            <Button variant="secondary" onPress={() => setIsYearModalOpen(true)}>
              + {t("period.newFiscalYear")}
            </Button>
            <Button variant="primary" onPress={() => setIsPeriodModalOpen(true)}>
              + {t("period.newPeriod")}
            </Button>
          </div>
        )}
      </div>

      {/* Year Filter Tabs */}
      <div style={{ display: "flex", alignItems: "center", gap: "0.5rem", marginBottom: "1.25rem", flexWrap: "wrap" }}>
        <span style={{ fontSize: "0.875rem", fontWeight: 700, color: "var(--color-content-secondary, #334155)", marginInlineEnd: "0.25rem" }}>
          السنة المالية:
        </span>
        <button
          type="button"
          onClick={() => setSelectedYearId("ALL")}
          style={{
            padding: "0.375rem 0.875rem",
            borderRadius: "var(--radius-md, 0.5rem)",
            border: "1px solid var(--color-border-default)",
            backgroundColor: selectedYearId === "ALL" ? "var(--color-brand-primary)" : "var(--color-surface-card)",
            color: selectedYearId === "ALL" ? "white" : "var(--color-content-primary)",
            fontSize: "0.875rem",
            fontWeight: 600,
            cursor: "pointer"
          }}
        >
          {t("period.allFiscalYears")} ({filteredPeriods.length})
        </button>
        {displayFiscalYears.map((fy) => (
          <button
            key={fy.id}
            type="button"
            onClick={() => setSelectedYearId(fy.id)}
            style={{
              padding: "0.375rem 0.875rem",
              borderRadius: "var(--radius-md, 0.5rem)",
              border: "1px solid var(--color-border-default)",
              backgroundColor: selectedYearId === fy.id ? "var(--color-brand-primary)" : "var(--color-surface-card)",
              color: selectedYearId === fy.id ? "white" : "var(--color-content-primary)",
              fontSize: "0.875rem",
              fontWeight: 600,
              cursor: "pointer"
            }}
          >
            {formatFiscalYearLabel(fy)}
          </button>
        ))}
      </div>

      {isLoading ? (
        <div style={{ padding: "3rem", textAlign: "center" }}>{t("common.loading")}</div>
      ) : (
        <AccountingDataTable
          data={filteredPeriods}
          columns={periodColumns}
          rowKey={(item) => item.id}
          emptyMessage={t("period.noPeriodsFound")}
        />
      )}

      {/* Close Readiness Modal */}
      <PeriodCloseReadinessModal
        isOpen={Boolean(readinessPeriod)}
        onClose={() => setReadinessPeriod(null)}
        legalEntityId={legalEntityId}
        periodId={readinessPeriod?.id ?? null}
        periodCode={readinessPeriod?.code}
      />

      {/* Accounting Period Close Modal */}
      {accountingClosePeriod && (
        <AccountingPeriodCloseModal
          isOpen={Boolean(accountingClosePeriod)}
          period={accountingClosePeriod}
          onClose={() => setAccountingClosePeriod(null)}
        />
      )}

      {/* Accounting Period Reopen Modal */}
      {accountingReopenPeriod && (
        <AccountingPeriodReopenModal
          isOpen={Boolean(accountingReopenPeriod)}
          period={accountingReopenPeriod}
          onClose={() => setAccountingReopenPeriod(null)}
        />
      )}

      {/* New Fiscal Year Dialog */}
      <Dialog
        isOpen={isYearModalOpen}
        onOpenChange={(open) => !open && setIsYearModalOpen(false)}
        title={t("period.newFiscalYear")}
        size="medium"
        footer={
          <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
            <Button variant="secondary" onPress={() => setIsYearModalOpen(false)}>
              {t("common.cancel")}
            </Button>
            <Button
              variant="primary"
              isLoading={createYearMutation.isPending}
              onPress={handleCreateYear}
            >
              {t("common.save")}
            </Button>
          </div>
        }
      >
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
          <TextField
            label={t("period.yearCode")}
            value={yearCode}
            onChange={setYearCode}
            isRequired
          />
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
            <TextField
              label={t("period.startDate")}
              value={yearStart}
              onChange={setYearStart}
              placeholder="2026-01-01"
              isRequired
            />
            <TextField
              label={t("period.endDate")}
              value={yearEnd}
              onChange={setYearEnd}
              placeholder="2026-12-31"
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

      {/* New Period Dialog */}
      <Dialog
        isOpen={isPeriodModalOpen}
        onOpenChange={(open) => !open && setIsPeriodModalOpen(false)}
        title={t("period.newPeriod")}
        size="medium"
        footer={
          <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
            <Button variant="secondary" onPress={() => setIsPeriodModalOpen(false)}>
              {t("common.cancel")}
            </Button>
            <Button
              variant="primary"
              isLoading={createPeriodMutation.isPending}
              onPress={handleCreatePeriod}
            >
              {t("common.save")}
            </Button>
          </div>
        }
      >
        <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", marginBottom: "0.25rem", fontSize: "0.875rem", fontWeight: 600 }}>
              {t("period.fiscalYear")}
            </label>
            <select
              value={periodTargetYearId}
              onChange={(e) => setPeriodTargetYearId(e.target.value)}
              style={{
                width: "100%",
                padding: "0.5rem 0.75rem",
                borderRadius: "var(--radius-md, 0.5rem)",
                border: "1px solid var(--color-border-default)",
                backgroundColor: "var(--color-surface-card)"
              }}
            >
              <option value="">{t("period.selectFiscalYear")}</option>
              {fiscalYears.map((fy) => (
                <option key={fy.id} value={fy.id}>
                  {fy.code}
                </option>
              ))}
            </select>
          </div>

          <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr", gap: "1rem" }}>
            <TextField
              label={t("period.code")}
              value={periodCode}
              onChange={setPeriodCode}
              isRequired
            />
            <TextField
              label={t("period.sequence")}
              value={periodSequence.toString()}
              onChange={(v) => setPeriodSequence(parseInt(v, 10) || 1)}
              isRequired
            />
          </div>

          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
            <TextField
              label={t("period.startDate")}
              value={periodStart}
              onChange={setPeriodStart}
              placeholder="2026-01-01"
              isRequired
            />
            <TextField
              label={t("period.endDate")}
              value={periodEnd}
              onChange={setPeriodEnd}
              placeholder="2026-01-31"
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
