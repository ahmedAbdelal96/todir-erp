import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { MoneyDisplay } from "../../../../components/fields/money-display.tsx";
import { AccountPicker } from "../account-picker.tsx";
import { DimensionValuePicker } from "../dimensions/dimension-value-picker.tsx";
import {
  useCreateGeneralJournalDraft,
  useUpdateGeneralJournalDraft,
  useSubmitGeneralJournal,
  useApproveGeneralJournal,
  usePostGeneralJournal,
  usePostingPreview,
  useJournals
} from "../../hooks/use-ledger-queries.ts";
import { mapLedgerError } from "../../services/ledger-error-map.ts";
import { GeneralJournalStatusBadge } from "../../components/ledger-status-badges.tsx";
import type {
  GeneralJournalLineRequest,
  GeneralJournalDraftStatus,
  PostingPreviewDto,
  LedgerPostingResult
} from "../../types.ts";

export interface GeneralJournalViewProps {
  readonly legalEntityId: string;
  readonly baseCurrency: string;
  readonly today: string;
  readonly canCreate: boolean;
  readonly canPost: boolean;
  readonly onEntryPosted?: (entryNumber: string) => void;
}

interface EditorLine {
  readonly id: string;
  accountId: string;
  accountCode: string;
  accountName: string;
  debit: string;
  credit: string;
  currency: string;
  exchangeRate: string;
  dimensionValueId: string;
  description: string;
}

export function GeneralJournalView({
  legalEntityId,
  baseCurrency,
  today,
  canCreate,
  canPost,
  onEntryPosted
}: GeneralJournalViewProps): React.JSX.Element {
  const { t, i18n } = useTranslation("accounting");
  const { data: journals = [] } = useJournals(legalEntityId);

  const createDraftMutation = useCreateGeneralJournalDraft();
  const updateDraftMutation = useUpdateGeneralJournalDraft();
  const submitDraftMutation = useSubmitGeneralJournal();
  const approveDraftMutation = useApproveGeneralJournal();
  const postJournalMutation = usePostGeneralJournal();
  const previewMutation = usePostingPreview();

  const [activeDraftId, setActiveDraftId] = useState<string | null>(null);
  const [draftStatus, setDraftStatus] = useState<GeneralJournalDraftStatus | null>(null);

  const [journalId, setJournalId] = useState<string>("");
  const [postingDate, setPostingDate] = useState<string>(today);
  const [description, setDescription] = useState<string>("");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [successResult, setSuccessResult] = useState<LedgerPostingResult | null>(null);
  const [infoMessage, setInfoMessage] = useState<string | null>(null);

  // Preview dialog state
  const [previewData, setPreviewData] = useState<PostingPreviewDto | null>(null);
  const [isPreviewOpen, setIsPreviewOpen] = useState(false);

  // Default to general journal
  const generalJournal = journals.find((j) => j.type === "General") || journals[0];
  const activeJournalId = journalId || generalJournal?.id || "";

  // 2 initial empty lines
  const [lines, setLines] = useState<EditorLine[]>([
    {
      id: "l-1",
      accountId: "",
      accountCode: "",
      accountName: "",
      debit: "0.00",
      credit: "0.00",
      currency: baseCurrency,
      exchangeRate: "1.0",
      dimensionValueId: "",
      description: ""
    },
    {
      id: "l-2",
      accountId: "",
      accountCode: "",
      accountName: "",
      debit: "0.00",
      credit: "0.00",
      currency: baseCurrency,
      exchangeRate: "1.0",
      dimensionValueId: "",
      description: ""
    }
  ]);

  const currentLang = i18n.language?.startsWith("ar") ? "ar" : "en";
  const isReadOnly = draftStatus === "Posted";

  // Calculate live debit, credit, difference
  const totals = lines.reduce(
    (acc, line) => {
      const d = parseFloat(line.debit) || 0;
      const c = parseFloat(line.credit) || 0;
      const rate = parseFloat(line.exchangeRate) || 1;
      const baseD = d * rate;
      const baseC = c * rate;
      return {
        totalDebit: acc.totalDebit + baseD,
        totalCredit: acc.totalCredit + baseC
      };
    },
    { totalDebit: 0, totalCredit: 0 }
  );

  const difference = Math.abs(totals.totalDebit - totals.totalCredit);
  const isBalanced = totals.totalDebit > 0 && Math.abs(difference) < 0.0001;

  const handleUpdateLine = (index: number, updates: Partial<EditorLine>) => {
    const next = [...lines];
    const target = next[index];
    if (!target) return;
    next[index] = { ...target, ...updates };
    setLines(next);
  };

  const handleAddLine = () => {
    if (isReadOnly) return;
    setLines([
      ...lines,
      {
        id: `l-${Date.now()}`,
        accountId: "",
        accountCode: "",
        accountName: "",
        debit: "0.00",
        credit: "0.00",
        currency: baseCurrency,
        exchangeRate: "1.0",
        dimensionValueId: "",
        description: ""
      }
    ]);
  };

  const handleRemoveLine = (index: number) => {
    if (isReadOnly || lines.length <= 2) return;
    setLines(lines.filter((_, i) => i !== index));
  };

  const buildLineRequests = (): GeneralJournalLineRequest[] => {
    return lines.map((l) => {
      const d = parseFloat(l.debit) || 0;
      const c = parseFloat(l.credit) || 0;
      const rate = parseFloat(l.exchangeRate) || 1;
      const dimIds = l.dimensionValueId.trim() ? [l.dimensionValueId.trim()] : undefined;
      return {
        accountId: l.accountId,
        debit: d,
        credit: c,
        transactionCurrencyCode: l.currency,
        transactionDebit: d,
        transactionCredit: c,
        baseDebit: d * rate,
        baseCredit: c * rate,
        exchangeRate: rate,
        ...(dimIds ? { dimensionValueIds: dimIds } : {}),
        description: l.description.trim() || null
      };
    });
  };

  const handleSaveDraft = async (): Promise<string | null> => {
    setErrorMessage(null);
    setInfoMessage(null);
    if (!activeJournalId || !description.trim()) {
      setErrorMessage(t("account.validationRequiredFields"));
      return null;
    }
    if (lines.some((l) => !l.accountId)) {
      setErrorMessage(t("journal.allLinesRequireAccount"));
      return null;
    }

    try {
      const req = {
        journalId: activeJournalId,
        postingDate,
        description: description.trim(),
        baseCurrencyCode: baseCurrency,
        idempotencyKey: activeDraftId ? `gj-${activeDraftId}` : `gj-${Date.now()}`,
        lines: buildLineRequests()
      };

      if (activeDraftId) {
        const updated = await updateDraftMutation.mutateAsync({
          legalEntityId,
          draftId: activeDraftId,
          request: req
        });
        setActiveDraftId(updated.id);
        setDraftStatus(updated.status);
        setInfoMessage(t("journal.draftSaved"));
        return updated.id;
      } else {
        const created = await createDraftMutation.mutateAsync({
          legalEntityId,
          request: req
        });
        setActiveDraftId(created.id);
        setDraftStatus(created.status);
        setInfoMessage(t("journal.draftSaved"));
        return created.id;
      }
    } catch (err) {
      setErrorMessage(mapLedgerError(err, currentLang));
      return null;
    }
  };

  const handleSubmitDraft = async () => {
    setErrorMessage(null);
    setInfoMessage(null);
    const targetDraftId = activeDraftId ?? (await handleSaveDraft());
    if (!targetDraftId) return;

    try {
      const result = await submitDraftMutation.mutateAsync({
        legalEntityId,
        draftId: targetDraftId
      });
      setDraftStatus(result.status);
      setInfoMessage(t("journal.draftSubmitted"));
    } catch (err) {
      setErrorMessage(mapLedgerError(err, currentLang));
    }
  };

  const handleApproveDraft = async () => {
    setErrorMessage(null);
    setInfoMessage(null);
    if (!activeDraftId) return;

    try {
      const result = await approveDraftMutation.mutateAsync({
        legalEntityId,
        draftId: activeDraftId
      });
      setDraftStatus(result.status);
      setInfoMessage(t("journal.draftApproved"));
    } catch (err) {
      setErrorMessage(mapLedgerError(err, currentLang));
    }
  };

  const handlePreview = async () => {
    setErrorMessage(null);
    setInfoMessage(null);
    if (!activeJournalId || !description.trim()) {
      setErrorMessage(t("account.validationRequiredFields"));
      return;
    }
    if (lines.some((l) => !l.accountId)) {
      setErrorMessage(t("journal.allLinesRequireAccount"));
      return;
    }
    if (!isBalanced) {
      setErrorMessage(t("journal.unbalanced"));
      return;
    }

    try {
      const result = await previewMutation.mutateAsync({
        legalEntityId,
        request: {
          journalId: activeJournalId,
          postingDate,
          description: description.trim(),
          baseCurrencyCode: baseCurrency,
          lines: buildLineRequests()
        }
      });
      setPreviewData(result);
      setIsPreviewOpen(true);
    } catch (err) {
      setErrorMessage(mapLedgerError(err, currentLang));
    }
  };

  const handlePost = async () => {
    setErrorMessage(null);
    setInfoMessage(null);
    if (!activeJournalId || !description.trim()) {
      setErrorMessage(t("account.validationRequiredFields"));
      return;
    }
    if (lines.some((l) => !l.accountId)) {
      setErrorMessage(t("journal.allLinesRequireAccount"));
      return;
    }
    if (!isBalanced) {
      setErrorMessage(t("journal.unbalanced"));
      return;
    }

    try {
      let targetDraftId = activeDraftId;
      if (!targetDraftId) {
        targetDraftId = await handleSaveDraft();
        if (!targetDraftId) return;
      }

      // If draft is still in Draft state, backend requires Submit then Approve if workflow is enabled
      // Let's attempt to post directly; if backend says draft must be submitted/approved, handle transition
      let postResult: LedgerPostingResult;
      try {
        postResult = await postJournalMutation.mutateAsync({
          legalEntityId,
          draftId: targetDraftId
        });
      } catch (postErr) {
        const code = (postErr as any)?.extensions?.code;
        if (code === "ledger.general_journal_transition_invalid" || code === "ledger.draft_not_approved") {
          // Transition through Submit -> Approve -> Post
          await submitDraftMutation.mutateAsync({ legalEntityId, draftId: targetDraftId });
          await approveDraftMutation.mutateAsync({ legalEntityId, draftId: targetDraftId });
          postResult = await postJournalMutation.mutateAsync({ legalEntityId, draftId: targetDraftId });
        } else {
          throw postErr;
        }
      }

      setDraftStatus("Posted");
      setSuccessResult(postResult);
      setIsPreviewOpen(false);
      onEntryPosted?.(postResult.entryNumber);
    } catch (err) {
      setErrorMessage(mapLedgerError(err, currentLang));
    }
  };

  return (
    <Surface style={{ padding: "1.25rem 1.5rem", width: "100%", maxWidth: "100%", boxSizing: "border-box", minWidth: 0 }}>
      {/* Header */}
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1.5rem", flexWrap: "wrap", gap: "1rem", minWidth: 0 }}>
        <div style={{ minWidth: 0 }}>
          <div style={{ display: "flex", alignItems: "center", gap: "0.75rem", flexWrap: "wrap" }}>
            <h2 style={{ margin: 0, fontSize: "1.25rem", fontWeight: 700 }}>
              {t("journal.documentType")}
            </h2>
            {draftStatus && <GeneralJournalStatusBadge status={draftStatus} />}
          </div>
          <p style={{ margin: "0.25rem 0 0", color: "var(--color-content-muted)", fontSize: "0.875rem" }}>
            {t("journal.generalJournalDescription")}
          </p>
        </div>

        {/* Live Balance Card */}
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
              <MoneyDisplay value={{ amount: totals.totalDebit.toFixed(2), currency: baseCurrency }} />
            </div>
          </div>
          <div>
            <div style={{ fontSize: "0.75rem", color: "var(--color-content-muted)" }}>{t("journal.totalCredit")}</div>
            <div style={{ fontSize: "1rem", fontWeight: 600 }}>
              <MoneyDisplay value={{ amount: totals.totalCredit.toFixed(2), currency: baseCurrency }} />
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

      {infoMessage && (
        <div
          style={{
            padding: "0.75rem 1rem",
            marginBottom: "1rem",
            borderRadius: "var(--radius-md, 0.5rem)",
            backgroundColor: "var(--color-status-info-subtle, #eff6ff)",
            border: "1px solid var(--color-status-info, #3b82f6)",
            color: "var(--color-status-info, #1d4ed8)",
            fontSize: "0.875rem"
          }}
        >
          {infoMessage}
        </div>
      )}

      {successResult && (
        <div
          style={{
            padding: "1rem",
            marginBottom: "1.5rem",
            borderRadius: "var(--radius-md, 0.5rem)",
            backgroundColor: "var(--color-status-success-subtle, #ecfdf5)",
            border: "1px solid var(--color-status-success, #10b981)",
            color: "var(--color-status-success, #065f46)",
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            flexWrap: "wrap",
            gap: "1rem"
          }}
        >
          <div>
            <strong>{t("journal.postSuccess")}: </strong>
            {t("journal.entryNumber")}: {successResult.entryNumber}
            <span style={{ marginInlineStart: "0.5rem", fontSize: "0.875rem", opacity: 0.85 }}>
              ({t("journal.postedNonEditable")})
            </span>
          </div>
          {onEntryPosted && (
            <Button
              variant="secondary"
              size="compact"
              onPress={() => onEntryPosted(successResult.entryNumber)}
            >
              {t("journal.viewPostedEntry")}
            </Button>
          )}
        </div>
      )}

      {/* Main Metadata Form */}
      <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(200px, 1fr))", gap: "1rem", marginBottom: "1.5rem", minWidth: 0 }}>
        <div style={{ minWidth: 0 }}>
          <label style={{ display: "block", marginBottom: "0.25rem", fontSize: "0.875rem", fontWeight: 600 }}>
            {t("journals.journal")}
          </label>
          <select
            value={activeJournalId}
            disabled={isReadOnly}
            onChange={(e) => setJournalId(e.target.value)}
            style={{
              width: "100%",
              padding: "0.5rem 0.75rem",
              borderRadius: "var(--radius-md, 0.5rem)",
              border: "1px solid var(--color-border-default)",
              backgroundColor: "var(--color-surface-card)"
            }}
          >
            {journals.map((j) => (
              <option key={j.id} value={j.id}>
                {j.code} — {j.nameAr} ({j.nameEn})
              </option>
            ))}
          </select>
        </div>

        <div style={{ minWidth: 0 }}>
          <TextField
            label={t("journal.entryDate")}
            value={postingDate}
            isDisabled={isReadOnly}
            onChange={setPostingDate}
            placeholder="2026-10-24"
            isRequired
          />
        </div>

        <div style={{ gridColumn: "span 2", minWidth: 0 }}>
          <TextField
            label={t("journal.description")}
            value={description}
            isDisabled={isReadOnly}
            onChange={setDescription}
            placeholder={t("journal.descriptionPlaceholder")}
            isRequired
          />
        </div>
      </div>

      {/* Multi-line Editor Table */}
      <div style={{ width: "100%", maxWidth: "100%", minWidth: 0, overflowX: "auto", WebkitOverflowScrolling: "touch", marginBottom: "1rem", boxSizing: "border-box" }}>
        <table style={{ width: "100%", borderCollapse: "collapse", minWidth: "900px" }}>
          <thead>
            <tr style={{ borderBottom: "1px solid var(--color-border-default)", textAlign: "start" }}>
              <th style={{ padding: "0.5rem", width: "40px" }}>#</th>
              <th style={{ padding: "0.5rem", width: "260px" }}>{t("journal.accountCodeName")}</th>
              <th style={{ padding: "0.5rem", width: "130px" }}>{t("journal.debit")}</th>
              <th style={{ padding: "0.5rem", width: "130px" }}>{t("journal.credit")}</th>
              <th style={{ padding: "0.5rem", width: "180px" }}>{t("dimensions.dimensionValue")}</th>
              <th style={{ padding: "0.5rem" }}>{t("journal.memo")}</th>
              <th style={{ padding: "0.5rem", width: "50px" }}></th>
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
                    isDisabled={isReadOnly}
                    onSelectAccount={(acc) => {
                      handleUpdateLine(idx, {
                        accountId: acc?.id ?? "",
                        accountCode: acc?.code ?? "",
                        accountName: acc?.nameAr ?? ""
                      });
                    }}
                  />
                </td>
                <td style={{ padding: "0.5rem" }}>
                  <input
                    type="number"
                    step="0.01"
                    min="0"
                    disabled={isReadOnly}
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
                    disabled={isReadOnly}
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
                  <DimensionValuePicker
                    value={line.dimensionValueId}
                    isDisabled={isReadOnly}
                    onChange={(val) => handleUpdateLine(idx, { dimensionValueId: val })}
                  />
                </td>
                <td style={{ padding: "0.5rem" }}>
                  <input
                    type="text"
                    disabled={isReadOnly}
                    value={line.description}
                    onChange={(e) => handleUpdateLine(idx, { description: e.target.value })}
                    placeholder={t("journal.memoPlaceholder")}
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
                  {!isReadOnly && lines.length > 2 && (
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
        {!isReadOnly && (
          <Button variant="secondary" onPress={handleAddLine}>
            + {t("journal.addLine")}
          </Button>
        )}

        <div style={{ display: "flex", gap: "0.75rem", flexWrap: "wrap", marginInlineStart: "auto" }}>
          {canCreate && !isReadOnly && (
            <Button
              variant="secondary"
              isLoading={createDraftMutation.isPending || updateDraftMutation.isPending}
              onPress={handleSaveDraft}
            >
              {t("journal.saveDraft")}
            </Button>
          )}

          {canCreate && activeDraftId && (draftStatus === "Draft" || !draftStatus) && !isReadOnly && (
            <Button
              variant="secondary"
              isLoading={submitDraftMutation.isPending}
              onPress={handleSubmitDraft}
            >
              {t("journal.submitDraft")}
            </Button>
          )}

          {activeDraftId && draftStatus === "Submitted" && !isReadOnly && (
            <Button
              variant="secondary"
              isLoading={approveDraftMutation.isPending}
              onPress={handleApproveDraft}
            >
              {t("journal.approveDraft")}
            </Button>
          )}

          {!isReadOnly && (
            <Button
              variant="secondary"
              isLoading={previewMutation.isPending}
              isDisabled={!isBalanced}
              onPress={handlePreview}
            >
              {t("journal.postingPreview")}
            </Button>
          )}

          {canPost && !isReadOnly && (
            <Button
              variant="primary"
              isLoading={createDraftMutation.isPending || postJournalMutation.isPending}
              isDisabled={!isBalanced}
              onPress={handlePost}
            >
              {t("journal.post")}
            </Button>
          )}
        </div>
      </div>

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

      {/* Posting Preview Dialog */}
      <Dialog
        isOpen={isPreviewOpen}
        onOpenChange={(open) => !open && setIsPreviewOpen(false)}
        title={t("journal.postingPreview")}
        size="large"
        footer={
          <div style={{ display: "flex", gap: "0.75rem", justifyContent: "flex-end" }}>
            <Button variant="secondary" onPress={() => setIsPreviewOpen(false)}>
              {t("common.close")}
            </Button>
            {canPost && (
              <Button
                variant="primary"
                isLoading={postJournalMutation.isPending}
                onPress={handlePost}
              >
                {t("journal.confirmAndPost")}
              </Button>
            )}
          </div>
        }
      >
        {previewData && (
          <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
            <div style={{ display: "flex", gap: "1.5rem", fontSize: "0.875rem" }}>
              <div>
                <strong>{t("journal.entryDate")}: </strong>
                {previewData.postingDate}
              </div>
              <div>
                <strong>{t("journal.totalDebit")}: </strong>
                <MoneyDisplay value={{ amount: previewData.totalDebit.toFixed(2), currency: previewData.baseCurrencyCode }} />
              </div>
              <div>
                <strong>{t("journal.totalCredit")}: </strong>
                <MoneyDisplay value={{ amount: previewData.totalCredit.toFixed(2), currency: previewData.baseCurrencyCode }} />
              </div>
            </div>

            <table style={{ width: "100%", borderCollapse: "collapse", fontSize: "0.875rem" }}>
              <thead>
                <tr style={{ borderBottom: "1px solid var(--color-border-default)", textAlign: "start" }}>
                  <th style={{ padding: "0.5rem" }}>{t("explanation.accountRole")}</th>
                  <th style={{ padding: "0.5rem" }}>{t("journal.debit")}</th>
                  <th style={{ padding: "0.5rem" }}>{t("journal.credit")}</th>
                  <th style={{ padding: "0.5rem" }}>{t("journal.memo")}</th>
                </tr>
              </thead>
              <tbody>
                {previewData.lines.map((l, i) => (
                  <tr key={i} style={{ borderBottom: "1px solid var(--color-border-default)" }}>
                    <td style={{ padding: "0.5rem" }}>{l.accountRole}</td>
                    <td style={{ padding: "0.5rem" }}>
                      <MoneyDisplay value={{ amount: l.baseDebit.toFixed(2), currency: l.currencyCode }} />
                    </td>
                    <td style={{ padding: "0.5rem" }}>
                      <MoneyDisplay value={{ amount: l.baseCredit.toFixed(2), currency: l.currencyCode }} />
                    </td>
                    <td style={{ padding: "0.5rem" }}>{l.description || "—"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Dialog>
    </Surface>
  );
}
