import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { MoneyDisplay } from "../../../../components/fields/money-display.tsx";
import { AccountPicker } from "../account-picker.tsx";
import {
  useCreateOpeningBatch,
  usePreviewOpeningBatch,
  usePostOpeningBatch
} from "../../hooks/use-ledger-queries.ts";
import { mapLedgerError } from "../../services/ledger-error-map.ts";
import type { OpeningBalanceLineRequest, OpeningBalancePreviewDto } from "../../types.ts";

export interface OpeningBalancesViewProps {
  readonly legalEntityId: string;
  readonly baseCurrency: string;
  readonly today: string;
  readonly canManageOpening: boolean;
  readonly canPostOpening: boolean;
}

interface OpeningLineItem {
  readonly id: string;
  accountId: string;
  accountCode: string;
  debit: string;
  credit: string;
  legacyCode: string;
  legacyReference: string;
}

export function OpeningBalancesView({
  legalEntityId,
  baseCurrency,
  today,
  canManageOpening,
  canPostOpening
}: OpeningBalancesViewProps): React.JSX.Element {
  const { t, i18n } = useTranslation("accounting");

  const createBatchMutation = useCreateOpeningBatch();
  const previewBatchMutation = usePreviewOpeningBatch();
  const postBatchMutation = usePostOpeningBatch();

  const [batchIdentity, setBatchIdentity] = useState(`OB-${new Date().getFullYear()}`);
  const [legacySystem, setLegacySystem] = useState("");
  const [goLiveDate, setGoLiveDate] = useState(today);

  const [lines, setLines] = useState<OpeningLineItem[]>([
    {
      id: "ob-1",
      accountId: "",
      accountCode: "",
      debit: "0.00",
      credit: "0.00",
      legacyCode: "",
      legacyReference: ""
    },
    {
      id: "ob-2",
      accountId: "",
      accountCode: "",
      debit: "0.00",
      credit: "0.00",
      legacyCode: "",
      legacyReference: ""
    }
  ]);

  const [batchId, setBatchId] = useState<string | null>(null);
  const [previewResult, setPreviewResult] = useState<OpeningBalancePreviewDto | null>(null);
  const [postedSuccess, setPostedSuccess] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const currentLang = i18n.language?.startsWith("ar") ? "ar" : "en";

  // Calculate live sum
  const sumDebit = lines.reduce((s, l) => s + (parseFloat(l.debit) || 0), 0);
  const sumCredit = lines.reduce((s, l) => s + (parseFloat(l.credit) || 0), 0);
  const difference = Math.abs(sumDebit - sumCredit);
  const isBalanced = sumDebit > 0 && difference < 0.0001;

  const handleUpdateLine = (index: number, updates: Partial<OpeningLineItem>) => {
    const next = [...lines];
    const target = next[index];
    if (!target) return;
    next[index] = { ...target, ...updates };
    setLines(next);
  };

  const handleAddLine = () => {
    setLines([
      ...lines,
      {
        id: `ob-${Date.now()}`,
        accountId: "",
        accountCode: "",
        debit: "0.00",
        credit: "0.00",
        legacyCode: "",
        legacyReference: ""
      }
    ]);
  };

  const handleRemoveLine = (index: number) => {
    if (lines.length <= 2) return;
    setLines(lines.filter((_, i) => i !== index));
  };

  const buildLines = (): OpeningBalanceLineRequest[] => {
    return lines.map((l) => {
      const d = parseFloat(l.debit) || 0;
      const c = parseFloat(l.credit) || 0;
      return {
        accountId: l.accountId,
        debit: d,
        credit: c,
        currencyCode: baseCurrency,
        baseDebit: d,
        baseCredit: c,
        exchangeRate: 1,
        legacyAccountCode: l.legacyCode.trim() || null,
        legacyReference: l.legacyReference.trim() || null
      };
    });
  };

  const handlePreview = async () => {
    setErrorMessage(null);
    if (!batchIdentity.trim() || !goLiveDate) {
      setErrorMessage(t("account.validationRequiredFields"));
      return;
    }
    if (lines.some((l) => !l.accountId)) {
      setErrorMessage(t("journal.allLinesRequireAccount"));
      return;
    }

    try {
      // 1. Create batch
      const id = await createBatchMutation.mutateAsync({
        legalEntityId,
        request: {
          batchIdentity: batchIdentity.trim(),
          fileHash: `manual-entry-${Date.now()}`,
          legacySystem: legacySystem.trim() || null,
          goLiveDate,
          lines: buildLines()
        }
      });
      setBatchId(id);

      // 2. Preview
      const preview = await previewBatchMutation.mutateAsync({
        legalEntityId,
        batchId: id
      });
      setPreviewResult(preview);
    } catch (err) {
      setErrorMessage(mapLedgerError(err, currentLang));
    }
  };

  const handlePost = async () => {
    if (!batchId) return;
    setErrorMessage(null);
    try {
      const result = await postBatchMutation.mutateAsync({
        legalEntityId,
        batchId
      });
      setPostedSuccess(result.entryNumber);
      setPreviewResult(null);
    } catch (err) {
      setErrorMessage(mapLedgerError(err, currentLang));
    }
  };

  return (
    <Surface style={{ padding: "1.25rem 1.5rem", width: "100%", maxWidth: "100%", boxSizing: "border-box", minWidth: 0 }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1.5rem", flexWrap: "wrap", gap: "1rem", minWidth: 0 }}>
        <div style={{ minWidth: 0 }}>
          <h2 style={{ margin: 0, fontSize: "1.25rem", fontWeight: 700 }}>
            {t("opening.title")}
          </h2>
          <p style={{ margin: "0.25rem 0 0", color: "var(--color-content-muted)", fontSize: "0.875rem" }}>
            {t("opening.description")}
          </p>
        </div>

        {/* Balance Status */}
        <div
          style={{
            display: "flex",
            alignItems: "center",
            gap: "1.25rem",
            padding: "0.625rem 1rem",
            backgroundColor: "var(--color-surface-sunken)",
            borderRadius: "var(--radius-md, 0.5rem)",
            border: `1px solid ${isBalanced ? "var(--color-status-success)" : "var(--color-status-danger)"}`,
            flexWrap: "wrap",
            maxWidth: "100%"
          }}
        >
          <div>
            <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{t("journal.totalDebit")}</div>
            <div style={{ fontSize: "1rem", fontWeight: 600 }}>
              <MoneyDisplay value={{ amount: sumDebit.toFixed(2), currency: baseCurrency }} />
            </div>
          </div>
          <div>
            <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{t("journal.totalCredit")}</div>
            <div style={{ fontSize: "1rem", fontWeight: 600 }}>
              <MoneyDisplay value={{ amount: sumCredit.toFixed(2), currency: baseCurrency }} />
            </div>
          </div>
          <div>
            <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{t("journal.balanceStatus")}</div>
            <div style={{ fontSize: "0.875rem", fontWeight: 700, color: isBalanced ? "var(--color-status-success)" : "var(--color-status-danger)" }}>
              <span>{isBalanced ? "✓" : "✕"}</span>{" "}
              <span>{isBalanced ? t("journal.balanced") : t("journal.unbalanced")}</span>
            </div>
          </div>
        </div>
      </div>

      {postedSuccess && (
        <div
          style={{
            padding: "1rem",
            marginBottom: "1.5rem",
            borderRadius: "var(--radius-md, 0.5rem)",
            backgroundColor: "var(--color-status-success-subtle, #ecfdf5)",
            border: "1px solid var(--color-status-success, #10b981)",
            color: "var(--color-status-success, #065f46)"
          }}
        >
          <strong>{t("opening.postSuccess")}: </strong>
          {t("journal.entryNumber")}: {postedSuccess}
        </div>
      )}

      {/* Batch Metadata */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "1rem", marginBottom: "1.5rem", minWidth: 0 }}>
        <div style={{ minWidth: 0 }}>
          <TextField
            label={t("opening.batchIdentity")}
            value={batchIdentity}
            onChange={setBatchIdentity}
            isRequired
          />
        </div>
        <div style={{ minWidth: 0 }}>
          <TextField
            label={t("opening.legacySystem")}
            value={legacySystem}
            onChange={setLegacySystem}
          />
        </div>
        <div style={{ minWidth: 0 }}>
          <TextField
            label={t("opening.goLiveDate")}
            value={goLiveDate}
            onChange={setGoLiveDate}
            isRequired
          />
        </div>
      </div>

      {/* Lines Table */}
      <div style={{ width: "100%", maxWidth: "100%", minWidth: 0, overflowX: "auto", WebkitOverflowScrolling: "touch", marginBottom: "1rem", boxSizing: "border-box" }}>
        <table style={{ width: "100%", borderCollapse: "collapse", minWidth: "800px" }}>
          <thead>
            <tr style={{ borderBottom: "1px solid var(--color-border-default)", textAlign: "start" }}>
              <th style={{ padding: "0.5rem", width: "40px" }}>#</th>
              <th style={{ padding: "0.5rem", width: "280px" }}>{t("journal.accountCodeName")}</th>
              <th style={{ padding: "0.5rem", width: "140px" }}>{t("journal.debit")}</th>
              <th style={{ padding: "0.5rem", width: "140px" }}>{t("journal.credit")}</th>
              <th style={{ padding: "0.5rem", width: "160px" }}>{t("opening.legacyCode")}</th>
              <th style={{ padding: "0.5rem" }}>{t("opening.legacyRef")}</th>
              <th style={{ padding: "0.5rem", width: "60px" }}></th>
            </tr>
          </thead>
          <tbody>
            {lines.map((line, idx) => (
              <tr key={line.id} style={{ borderBottom: "1px solid var(--color-border-default)" }}>
                <td style={{ padding: "0.5rem", color: "var(--color-content-muted)" }}>{idx + 1}</td>
                <td style={{ padding: "0.5rem" }}>
                  <AccountPicker
                    legalEntityId={legalEntityId}
                    selectedAccountId={line.accountId}
                    onSelectAccount={(acc) => {
                      handleUpdateLine(idx, {
                        accountId: acc?.id ?? "",
                        accountCode: acc?.code ?? ""
                      });
                    }}
                  />
                </td>
                <td style={{ padding: "0.5rem" }}>
                  <input
                    type="number"
                    step="0.01"
                    min="0"
                    value={line.debit}
                    onChange={(e) => {
                      const val = e.target.value;
                      handleUpdateLine(idx, {
                        debit: val,
                        credit: parseFloat(val) > 0 ? "0.00" : line.credit
                      });
                    }}
                    style={{
                      width: "100%",
                      padding: "0.5rem",
                      borderRadius: "var(--radius-sm, 0.25rem)",
                      border: "1px solid var(--color-border-default)",
                      backgroundColor: "var(--color-surface-card)",
                      textAlign: "end"
                    }}
                  />
                </td>
                <td style={{ padding: "0.5rem" }}>
                  <input
                    type="number"
                    step="0.01"
                    min="0"
                    value={line.credit}
                    onChange={(e) => {
                      const val = e.target.value;
                      handleUpdateLine(idx, {
                        credit: val,
                        debit: parseFloat(val) > 0 ? "0.00" : line.debit
                      });
                    }}
                    style={{
                      width: "100%",
                      padding: "0.5rem",
                      borderRadius: "var(--radius-sm, 0.25rem)",
                      border: "1px solid var(--color-border-default)",
                      backgroundColor: "var(--color-surface-card)",
                      textAlign: "end"
                    }}
                  />
                </td>
                <td style={{ padding: "0.5rem" }}>
                  <input
                    type="text"
                    value={line.legacyCode}
                    onChange={(e) => handleUpdateLine(idx, { legacyCode: e.target.value })}
                    style={{
                      width: "100%",
                      padding: "0.5rem",
                      borderRadius: "var(--radius-sm, 0.25rem)",
                      border: "1px solid var(--color-border-default)",
                      backgroundColor: "var(--color-surface-card)"
                    }}
                  />
                </td>
                <td style={{ padding: "0.5rem" }}>
                  <input
                    type="text"
                    value={line.legacyReference}
                    onChange={(e) => handleUpdateLine(idx, { legacyReference: e.target.value })}
                    style={{
                      width: "100%",
                      padding: "0.5rem",
                      borderRadius: "var(--radius-sm, 0.25rem)",
                      border: "1px solid var(--color-border-default)",
                      backgroundColor: "var(--color-surface-card)"
                    }}
                  />
                </td>
                <td style={{ padding: "0.5rem", textAlign: "center" }}>
                  {lines.length > 2 && (
                    <Button
                      size="compact"
                      variant="secondary"
                      onPress={() => handleRemoveLine(idx)}
                    >
                      ✕
                    </Button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Toolbar actions */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: "1rem" }}>
        <Button variant="secondary" onPress={handleAddLine}>
          + {t("journal.addLine")}
        </Button>

        <div style={{ display: "flex", gap: "0.75rem", flexWrap: "wrap" }}>
          {canManageOpening && (
            <Button
              variant="secondary"
              isLoading={createBatchMutation.isPending || previewBatchMutation.isPending}
              onPress={handlePreview}
            >
              {t("opening.previewBatch")}
            </Button>
          )}

          {canPostOpening && previewResult && previewResult.balanced && (
            <Button
              variant="primary"
              isLoading={postBatchMutation.isPending}
              onPress={handlePost}
            >
              {t("opening.commitAndPost")}
            </Button>
          )}
        </div>
      </div>

      {/* Preview Card */}
      {previewResult && (
        <div
          style={{
            marginTop: "1.5rem",
            padding: "1rem 1.25rem",
            borderRadius: "var(--radius-md, 0.5rem)",
            backgroundColor: previewResult.balanced ? "var(--color-surface-sunken)" : "var(--color-status-danger-subtle, #fee2e2)",
            border: `1px solid ${previewResult.balanced ? "var(--color-border-default)" : "var(--color-status-danger, #b91c1c)"}`
          }}
        >
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "0.5rem" }}>
            <h4 style={{ margin: 0 }}>{t("opening.previewResults")}</h4>
            <span style={{ fontWeight: 700, color: previewResult.balanced ? "var(--color-status-success)" : "var(--color-status-danger)" }}>
              <span>{previewResult.balanced ? "✓" : "✕"}</span>{" "}
              <span>{previewResult.balanced ? t("journal.balanced") : t("journal.unbalanced")}</span>
            </span>
          </div>
          <div style={{ display: "flex", gap: "2rem", fontSize: "0.875rem" }}>
            <div>
              {t("journal.totalDebit")}:{" "}
              <MoneyDisplay value={{ amount: previewResult.totalDebit.toFixed(2), currency: baseCurrency }} />
            </div>
            <div>
              {t("journal.totalCredit")}:{" "}
              <MoneyDisplay value={{ amount: previewResult.totalCredit.toFixed(2), currency: baseCurrency }} />
            </div>
          </div>
          {(previewResult.validationErrors?.length ?? 0) > 0 && (
            <ul style={{ margin: "0.5rem 0 0", paddingInlineStart: "1.25rem", color: "var(--color-status-danger)", fontSize: "0.875rem" }}>
              {previewResult.validationErrors.map((err, i) => (
                <li key={i}>{err}</li>
              ))}
            </ul>
          )}
        </div>
      )}

      {errorMessage && (
        <div
          style={{
            marginTop: "1.25rem",
            padding: "0.75rem 1rem",
            borderRadius: "var(--radius-md, 0.5rem)",
            backgroundColor: "var(--color-status-danger-subtle, #fee2e2)",
            color: "var(--color-status-danger, #b91c1c)",
            fontSize: "0.875rem"
          }}
        >
          {errorMessage}
        </div>
      )}
    </Surface>
  );
}
