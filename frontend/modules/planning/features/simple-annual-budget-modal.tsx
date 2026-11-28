import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { LocalDateField } from "../../../components/fields/local-date-field.tsx";
import { MoneyField } from "../../../components/fields/money-field.tsx";
import { Alert } from "../../../components/primitives/alert.tsx";
import {
  useCreatePlanningCycle,
  useCreatePlanVersion,
  useCreateBaseline,
  useWriteGridBatch
} from "../hooks/use-planning-queries.ts";
import { mapPlanningError } from "../services/planning-error-map.ts";

export interface SimpleAnnualBudgetModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly currencyCode: string;
  readonly defaultModelVersionId: string;
  readonly onCreated?: ((versionId: string) => void) | undefined;
}

export function SimpleAnnualBudgetModal({
  isOpen,
  onClose,
  legalEntityId,
  currencyCode,
  defaultModelVersionId,
  onCreated
}: SimpleAnnualBudgetModalProps): React.JSX.Element {
  const { t } = useTranslation("planning");
  const currentYear = new Date().getFullYear() + 1;
  const [name, setName] = useState(`FY${currentYear} Annual Budget`);
  const [startPeriod, setStartPeriod] = useState(`${currentYear}-01-01`);
  const [endPeriod, setEndPeriod] = useState(`${currentYear}-12-01`);
  const [revenueAmount, setRevenueAmount] = useState("0");
  const [opexAmount, setOpexAmount] = useState("0");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const createCycle = useCreatePlanningCycle();
  const createVersion = useCreatePlanVersion();
  const createBaseline = useCreateBaseline();
  const writeGrid = useWriteGridBatch();

  const isPending = createCycle.isPending || createVersion.isPending || createBaseline.isPending || writeGrid.isPending;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setErrorMessage(null);
    if (!name.trim() || !defaultModelVersionId) {
      setErrorMessage(t("errors.generic"));
      return;
    }

    try {
      const cycle = await createCycle.mutateAsync({
        legalEntityId,
        name: name.trim(),
        cycleType: "AnnualBudget",
        horizon: "Monthly",
        startPeriod,
        endPeriod
      });

      const version = await createVersion.mutateAsync({
        cycleId: cycle.id,
        modelVersionId: defaultModelVersionId,
        name: `${name.trim()} - Base`,
        versionKind: "Budget"
      });

      const rev = Number(revenueAmount) || 0;
      const opex = Number(opexAmount) || 0;
      const cells = [];
      if (rev > 0) {
        cells.push({
          legalEntityId,
          periodStart: startPeriod,
          metricCode: "Revenue",
          dimensionKey: {},
          amount: rev,
          currencyCode,
          explicitZero: false,
          sourceLabel: "SME Annual Budget"
        });
      }
      if (opex > 0) {
        cells.push({
          legalEntityId,
          periodStart: startPeriod,
          metricCode: "OPEX",
          dimensionKey: {},
          amount: opex,
          currencyCode,
          explicitZero: false,
          sourceLabel: "SME Annual Budget"
        });
      }
      if (cells.length > 0) {
        await writeGrid.mutateAsync({
          planVersionId: version.id,
          cells,
          expectedVersion: version.rowVersion
        });
      }

      if (opex > 0 || rev > 0) {
        await createBaseline.mutateAsync({
          planVersionId: version.id,
          name: `${name.trim()} Baseline`,
          originalTotal: opex > 0 ? opex : rev,
          currencyCode
        });
      }

      onCreated?.(version.id);
      onClose();
    } catch (err) {
      const mapped = mapPlanningError(err);
      setErrorMessage(t(mapped.messageKey as never));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("actions.createAnnualBudget")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {errorMessage ? <Alert variant="danger">{errorMessage}</Alert> : null}

        <TextField
          label={t("fields.name")}
          value={name}
          onChange={setName}
          isRequired
        />

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <LocalDateField
            label={t("fields.startPeriod")}
            value={startPeriod}
            onChange={setStartPeriod}
          />
          <LocalDateField
            label={t("fields.endPeriod")}
            value={endPeriod}
            onChange={setEndPeriod}
          />
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <MoneyField
            label={t("labels.revenueAmount")}
            value={{ amount: revenueAmount, currency: currencyCode }}
            onChange={(v) => setRevenueAmount(v.amount)}
            defaultCurrency={currencyCode}
          />
          <MoneyField
            label={t("labels.opexAmount")}
            value={{ amount: opexAmount, currency: currencyCode }}
            onChange={(v) => setOpexAmount(v.amount)}
            defaultCurrency={currencyCode}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "0.5rem" }}>
          <Button type="button" variant="secondary" onPress={onClose}>
            {t("actions.cancel")}
          </Button>
          <Button type="submit" variant="primary" isDisabled={isPending}>
            {t("actions.createAnnualBudget")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
