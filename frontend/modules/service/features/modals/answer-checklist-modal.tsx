import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { TextArea } from "../../../../components/primitives/text-area.tsx";
import { useAnswerChecklistItemMutation } from "../../hooks/use-service-queries.ts";

export interface AnswerChecklistModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly legalEntityId: string;
  readonly workOrderId: string;
  readonly defaultItemCode?: string | undefined;
  readonly defaultPrompt?: string | undefined;
}

export function AnswerChecklistModal({
  isOpen,
  onClose,
  legalEntityId,
  workOrderId,
  defaultItemCode,
  defaultPrompt
}: AnswerChecklistModalProps): React.JSX.Element {
  const { t } = useTranslation("service");
  const answerMutation = useAnswerChecklistItemMutation(legalEntityId);

  const [itemCode, setItemCode] = useState(defaultItemCode || `CHK-${Date.now().toString().slice(-4)}`);
  const [prompt, setPrompt] = useState(defaultPrompt || "");
  const [passed, setPassed] = useState(true);
  const [value, setValue] = useState("");
  const [uom, setUom] = useState("");
  const [notes, setNotes] = useState("");
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!itemCode.trim()) {
      setErrorMsg(t("validation.itemCodeRequired"));
      return;
    }

    try {
      setErrorMsg(null);
      await answerMutation.mutateAsync({
        legalEntityId,
        workOrderId,
        itemCode: itemCode.trim(),
        prompt: prompt.trim() || undefined,
        passed,
        value: value.trim() || undefined,
        uom: uom.trim() || undefined,
        notes: notes.trim() || undefined
      });
      onClose();
    } catch (err: unknown) {
      setErrorMsg(err instanceof Error ? err.message : t("common.genericError"));
    }
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("workOrders.answerChecklistTitle")}
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

        <div style={{ display: "grid", gridTemplateColumns: "1fr 2fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("workOrders.itemCode")} *
            </label>
            <TextField
              value={itemCode}
              onChange={(val) => setItemCode(val)}
              isRequired
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("workOrders.checklistPrompt")}
            </label>
            <TextField
              value={prompt}
              onChange={(val) => setPrompt(val)}
              placeholder={t("workOrders.checklistPromptPlaceholder")}
            />
          </div>
        </div>

        <div style={{ display: "flex", alignItems: "center", gap: "1.5rem", padding: "0.5rem 0" }}>
          <label style={{ display: "flex", alignItems: "center", gap: "0.5rem", cursor: "pointer", fontWeight: 600, fontSize: "0.875rem" }}>
            <input
              type="radio"
              name="checklist-pass"
              checked={passed}
              onChange={() => setPassed(true)}
              style={{ accentColor: "#059669", width: "1.125rem", height: "1.125rem" }}
            />
            <span style={{ color: "#059669" }}>✓ {t("workOrders.pass")}</span>
          </label>

          <label style={{ display: "flex", alignItems: "center", gap: "0.5rem", cursor: "pointer", fontWeight: 600, fontSize: "0.875rem" }}>
            <input
              type="radio"
              name="checklist-pass"
              checked={!passed}
              onChange={() => setPassed(false)}
              style={{ accentColor: "#dc2626", width: "1.125rem", height: "1.125rem" }}
            />
            <span style={{ color: "#dc2626" }}>✗ {t("workOrders.fail")}</span>
          </label>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "2fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("workOrders.measurementValue")}
            </label>
            <TextField
              value={value}
              onChange={(val) => setValue(val)}
              placeholder={t("checklist.resultPlaceholder", "240, Normal, 15 psi")}
            />
          </div>

          <div>
            <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
              {t("workOrders.uom")}
            </label>
            <TextField
              value={uom}
              onChange={(val) => setUom(val)}
              placeholder={t("checklist.unitPlaceholder", "V, psi, °C, mm")}
            />
          </div>
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.8125rem", fontWeight: 600, marginBottom: "0.375rem" }}>
            {t("requests.description")}
          </label>
          <TextArea
            value={notes}
            onChange={(val: any) => setNotes(typeof val === 'string' ? val : val?.target?.value ?? '')}
            placeholder={t("workOrders.checklistNotesPlaceholder")}
            rows={2}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("common.cancel")}
          </Button>
          <Button variant="primary" type="submit" isLoading={answerMutation.isPending}>
            {t("workOrders.submitAnswer")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
