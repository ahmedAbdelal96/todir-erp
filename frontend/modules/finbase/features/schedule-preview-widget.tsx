import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../components/pages/surface.tsx";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import { StatusBadge } from "../../../components/fields/status-badge.tsx";
import {
  Table,
  TableHeader,
  TableBody,
  TableRow,
  TableHead,
  TableCell
} from "../../../components/primitives/table.tsx";
import { normalizeDecimalInput } from "../../../components/formatting/decimal-parser.ts";
import { useSchedulePreviewMutation } from "../hooks/finbase-queries.ts";
import type { PaymentScheduleLineDto, PaymentTermRuleRequest } from "../types.ts";

export interface SchedulePreviewWidgetProps {
  readonly defaultAmount?: string;
  readonly defaultTermCode?: string;
}

type PresetKey = "CASH" | "NET15" | "NET30" | "SPLIT50" | "INSTALL3";

const PRESETS: Record<PresetKey, readonly PaymentTermRuleRequest[]> = {
  CASH: [
    {
      sequence: 1,
      allocationType: "Balance",
      dueDateBasis: "Immediate",
      days: 0,
      monthOffset: 0
    }
  ],
  NET15: [
    {
      sequence: 1,
      allocationType: "Balance",
      dueDateBasis: "DaysAfterDate",
      days: 15,
      monthOffset: 0
    }
  ],
  NET30: [
    {
      sequence: 1,
      allocationType: "Balance",
      dueDateBasis: "DaysAfterDate",
      days: 30,
      monthOffset: 0
    }
  ],
  SPLIT50: [
    {
      sequence: 1,
      allocationType: "Percentage",
      percentage: "0.5",
      dueDateBasis: "Immediate",
      days: 0,
      monthOffset: 0
    },
    {
      sequence: 2,
      allocationType: "Balance",
      dueDateBasis: "DaysAfterDate",
      days: 30,
      monthOffset: 0
    }
  ],
  INSTALL3: [
    {
      sequence: 1,
      allocationType: "Percentage",
      percentage: "0.3333",
      dueDateBasis: "Immediate",
      days: 0,
      monthOffset: 0
    },
    {
      sequence: 2,
      allocationType: "Percentage",
      percentage: "0.3333",
      dueDateBasis: "DaysAfterDate",
      days: 30,
      monthOffset: 0
    },
    {
      sequence: 3,
      allocationType: "Balance",
      dueDateBasis: "DaysAfterDate",
      days: 60,
      monthOffset: 0
    }
  ]
};

export function SchedulePreviewWidget({
  defaultAmount = "100.00",
  defaultTermCode = "INSTALL3"
}: SchedulePreviewWidgetProps): React.JSX.Element {
  const { t } = useTranslation("finbase");
  const scheduleMutation = useSchedulePreviewMutation();

  const [amount, setAmount] = useState(defaultAmount);
  const [basisDate, setBasisDate] = useState(() =>
    new Date().toISOString().slice(0, 10)
  );
  const [selectedPreset, setSelectedPreset] = useState<PresetKey>(
    (defaultTermCode as PresetKey) in PRESETS ? (defaultTermCode as PresetKey) : "INSTALL3"
  );
  const [schedule, setSchedule] = useState<readonly PaymentScheduleLineDto[]>([]);
  const [errorMessage, setErrorMessage] = useState<string>();
  const [isOpen, setIsOpen] = useState(false);

  const handleCalculate = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    setErrorMessage(undefined);

    const normalizedAmount = normalizeDecimalInput(amount);
    if (!normalizedAmount) return;

    try {
      const rules = PRESETS[selectedPreset];
      const lines = await scheduleMutation.mutateAsync({
        totalAmount: normalizedAmount,
        basisDate,
        scale: 2,
        rules
      });
      setSchedule(lines);
    } catch {
      setErrorMessage(t("paymentTerms.scheduleCalculator.error"));
    }
  };

  const presetOptions = (Object.keys(PRESETS) as PresetKey[]).map((key) => ({
    id: key,
    label: t(`paymentTerms.presets.${key}`)
  }));

  return (
    <>
      <Surface>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", gap: "var(--space-4)", flexWrap: "wrap" }}>
          <div>
            <h3 style={{ fontSize: "var(--font-size-base)", fontWeight: 600, margin: 0 }}>
              {t("paymentTerms.scheduleCalculator.title")}
            </h3>
            <p style={{ color: "var(--color-content-secondary)", fontSize: "var(--font-size-sm)", margin: "var(--space-1) 0 0 0" }}>
              {t("paymentTerms.scheduleCalculator.description")}
            </p>
          </div>
          <Button variant="primary" type="button" onPress={() => setIsOpen(true)}>
            {t("paymentTerms.scheduleCalculator.openButton")}
          </Button>
        </div>
      </Surface>

      <Dialog
        isOpen={isOpen}
        onOpenChange={(open) => {
          if (!open) setIsOpen(false);
        }}
        title={t("paymentTerms.scheduleCalculator.title")}
        description={t("paymentTerms.scheduleCalculator.description")}
        size="large"
      >
        <form onSubmit={handleCalculate} style={{ display: "flex", flexDirection: "column", gap: "var(--space-3)" }}>
        <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(140px, 1fr))", gap: "var(--space-3)" }}>
          <TextField
            label={t("paymentTerms.scheduleCalculator.amountLabel")}
            value={amount}
            onChange={setAmount}
            inputDir="ltr"
            placeholder={t("paymentTerms.scheduleCalculator.amountPlaceholder")}
            isRequired
          />
          <TextField
            label={t("paymentTerms.scheduleCalculator.basisDateLabel")}
            value={basisDate}
            onChange={setBasisDate}
            inputDir="ltr"
            isRequired
          />
          <Select
            label={t("paymentTerms.scheduleCalculator.presetLabel")}
            selectedKey={selectedPreset}
            onSelectionChange={(k) => setSelectedPreset(k as PresetKey)}
            options={presetOptions}
          />
        </div>

        <div>
          <Button
            variant="primary"
            type="submit"
            isDisabled={scheduleMutation.isPending || !amount}
          >
            {scheduleMutation.isPending
              ? t("paymentTerms.scheduleCalculator.calculating")
              : t("paymentTerms.scheduleCalculator.calculateButton")}
          </Button>
        </div>
        </form>

        {errorMessage ? (
        <div style={{ marginTop: "var(--space-4)" }}>
          <Alert variant="danger" title={t("paymentTerms.scheduleCalculator.errorTitle")}>
            {errorMessage}
          </Alert>
        </div>
        ) : null}

        {schedule.length > 0 ? (
        <div data-testid="schedule-result" style={{ marginTop: "var(--space-4)" }}>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t("paymentTerms.scheduleCalculator.sequenceHeader")}</TableHead>
                <TableHead align="end">{t("paymentTerms.scheduleCalculator.amountHeader")}</TableHead>
                <TableHead>{t("paymentTerms.scheduleCalculator.dueDateHeader")}</TableHead>
                <TableHead>{t("paymentTerms.scheduleCalculator.allocationHeader")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {schedule.map((line) => (
                <TableRow key={line.sequence}>
                  <TableCell>
                    {t("paymentTerms.scheduleCalculator.installmentNumber", { number: line.sequence })}
                  </TableCell>
                  <TableCell align="end">
                    <span dir="ltr" style={{ fontWeight: 600 }}>
                      {line.amount}
                    </span>
                  </TableCell>
                  <TableCell>
                    <span dir="ltr">{line.dueDate}</span>
                  </TableCell>
                  <TableCell>
                    <StatusBadge
                      intent={line.allocation === "Balance" ? "info" : "neutral"}
                      label={t(`paymentTerms.allocations.${line.allocation}`)}
                      size="small"
                    />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>

          {selectedPreset === "INSTALL3" ? (
            <div
              style={{
                marginTop: "var(--space-3)",
                padding: "var(--space-2) var(--space-3)",
                borderRadius: "var(--radius-md)",
                backgroundColor: "var(--color-surface-subtle)",
                border: "1px solid var(--color-border-subtle)",
                fontSize: "var(--font-size-xs)",
                color: "var(--color-content-secondary)"
              }}
            >
              <strong>{t("paymentTerms.scheduleCalculator.residualNoticeTitle")}: </strong>
              {t("paymentTerms.scheduleCalculator.residualNoticeDescription")}
            </div>
          ) : null}
        </div>
        ) : null}
      </Dialog>
    </>
  );
}
