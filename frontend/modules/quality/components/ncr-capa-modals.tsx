import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import {
  useCreateIssue,
  useCreateNcr,
  useAddContainment,
  useCreateCapa,
  useAddCapaActionItem,
  useCompleteCapaActionItem,
  useAddCapaRootCause,
  useRecordEffectiveness
} from "../hooks/use-quality-queries.ts";

// --------------------------------------------------------------------------
// 1. Create Issue Modal
// --------------------------------------------------------------------------

interface CreateIssueModalProps {
  legalEntityId: string;
  isOpen: boolean;
  onClose: () => void;
}

export function CreateIssueModal({ legalEntityId, isOpen, onClose }: CreateIssueModalProps): React.JSX.Element | null {
  const { t } = useTranslation("quality");
  const createIssueMutation = useCreateIssue();

  const [issueType, setIssueType] = useState("ReceivingDefect");
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [severity, setSeverity] = useState("Major");
  const [inspectionId, setInspectionId] = useState("");
  const [supplierPartyId, setSupplierPartyId] = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim() || !description.trim()) return;

    await createIssueMutation.mutateAsync({
      legalEntityId,
      request: {
        issueType,
        title: title.trim(),
        description: description.trim(),
        severity,
        inspectionId: inspectionId.trim() || null,
        supplierPartyId: supplierPartyId.trim() || null
      }
    });
    onClose();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("issues.createTitle")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <Select
          label={t("issues.issueType")}
          selectedKey={issueType}
          onSelectionChange={(val) => val && setIssueType(String(val))}
          options={[
            { id: "ReceivingDefect", label: t("issues.typeReceiving") },
            { id: "InProcessDefect", label: t("issues.typeInProcess") },
            { id: "CustomerComplaint", label: t("issues.typeCustomerComplaint") },
            { id: "AuditFinding", label: t("issues.typeAuditFinding") }
          ]}
        />

        <TextField
          label={t("issues.title")}
          value={title}
          onChange={(val) => setTitle(val)}
          placeholder={t("issues.titlePlaceholder")}
          required
        />

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem", color: "#102a43" }}>
            {t("issues.description")}
          </label>
          <textarea
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            rows={3}
            required
            style={{
              width: "100%",
              padding: "0.5rem 0.75rem",
              borderRadius: "0.375rem",
              border: "1px solid #cbd5e1",
              fontSize: "0.875rem",
              fontFamily: "inherit",
              resize: "vertical"
            }}
          />
        </div>

        <Select
          label={t("issues.severity")}
          selectedKey={severity}
          onSelectionChange={(val) => val && setSeverity(String(val))}
          options={[
            { id: "Minor", label: t("severity.minor") },
            { id: "Major", label: t("severity.major") },
            { id: "Critical", label: t("severity.critical") }
          ]}
        />

        <TextField
          label={t("issues.inspectionId")}
          value={inspectionId}
          onChange={(val) => setInspectionId(val)}
          placeholder={t("issues.optionalReference")}
        />

        <TextField
          label={t("issues.supplierPartyId")}
          value={supplierPartyId}
          onChange={(val) => setSupplierPartyId(val)}
          placeholder={t("issues.optionalReference")}
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" disabled={createIssueMutation.isPending}>
            {createIssueMutation.isPending ? t("actions.saving") : t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// --------------------------------------------------------------------------
// 2. Create NCR Modal
// --------------------------------------------------------------------------

interface CreateNcrModalProps {
  legalEntityId: string;
  issueId?: string | null;
  isOpen: boolean;
  onClose: () => void;
}

export function CreateNcrModal({ legalEntityId, issueId, isOpen, onClose }: CreateNcrModalProps): React.JSX.Element | null {
  const { t } = useTranslation("quality");
  const createNcrMutation = useCreateNcr();

  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [severity, setSeverity] = useState("Major");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim() || !description.trim()) return;

    await createNcrMutation.mutateAsync({
      legalEntityId,
      request: {
        issueId: issueId || null,
        title: title.trim(),
        description: description.trim(),
        severity
      }
    });
    onClose();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("ncrs.createTitle")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <TextField
          label={t("ncrs.title")}
          value={title}
          onChange={(val) => setTitle(val)}
          placeholder={t("ncrs.titlePlaceholder")}
          required
        />

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem", color: "#102a43" }}>
            {t("ncrs.description")}
          </label>
          <textarea
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            rows={3}
            required
            style={{
              width: "100%",
              padding: "0.5rem 0.75rem",
              borderRadius: "0.375rem",
              border: "1px solid #cbd5e1",
              fontSize: "0.875rem",
              fontFamily: "inherit",
              resize: "vertical"
            }}
          />
        </div>

        <Select
          label={t("ncrs.severity")}
          selectedKey={severity}
          onSelectionChange={(val) => val && setSeverity(String(val))}
          options={[
            { id: "Minor", label: t("severity.minor") },
            { id: "Major", label: t("severity.major") },
            { id: "Critical", label: t("severity.critical") }
          ]}
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" disabled={createNcrMutation.isPending}>
            {createNcrMutation.isPending ? t("actions.saving") : t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// --------------------------------------------------------------------------
// 3. Create Containment Modal
// --------------------------------------------------------------------------

interface CreateContainmentModalProps {
  ncrId: string;
  isOpen: boolean;
  onClose: () => void;
}

export function CreateContainmentModal({ ncrId, isOpen, onClose }: CreateContainmentModalProps): React.JSX.Element | null {
  const { t } = useTranslation("quality");
  const addContainmentMutation = useAddContainment();

  const [actionType, setActionType] = useState("QuarantineStock");
  const [description, setDescription] = useState("");
  const [productVariantId, setProductVariantId] = useState("");
  const [lotId, setLotId] = useState("");
  const [quantity, setQuantity] = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!description.trim()) return;

    await addContainmentMutation.mutateAsync({
      ncrId,
      request: {
        actionType,
        description: description.trim(),
        productVariantId: productVariantId.trim() || null,
        lotId: lotId.trim() || null,
        quantity: quantity ? Number(quantity) : null
      }
    });
    onClose();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("ncrs.addContainmentTitle")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <Select
          label={t("ncrs.containmentType")}
          selectedKey={actionType}
          onSelectionChange={(val) => val && setActionType(String(val))}
          options={[
            { id: "QuarantineStock", label: t("ncrs.quarantineStock") },
            { id: "HaltProduction", label: t("ncrs.haltProduction") },
            { id: "CustomerNotification", label: t("ncrs.customerNotification") },
            { id: "SupplierHold", label: t("ncrs.supplierHold") }
          ]}
        />

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem", color: "#102a43" }}>
            {t("ncrs.actionDescription")}
          </label>
          <textarea
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            rows={3}
            required
            style={{
              width: "100%",
              padding: "0.5rem 0.75rem",
              borderRadius: "0.375rem",
              border: "1px solid #cbd5e1",
              fontSize: "0.875rem",
              fontFamily: "inherit",
              resize: "vertical"
            }}
          />
        </div>

        <TextField
          label={t("ncrs.productVariantId")}
          value={productVariantId}
          onChange={(val) => setProductVariantId(val)}
          placeholder={t("issues.optionalReference")}
        />

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "0.75rem" }}>
          <TextField
            label={t("ncrs.lotId")}
            value={lotId}
            onChange={(val) => setLotId(val)}
            placeholder={t("issues.optionalReference")}
          />
          <TextField
            label={t("ncrs.quantity")}
            type="number"
            value={quantity}
            onChange={(val) => setQuantity(val)}
            placeholder="0"
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" disabled={addContainmentMutation.isPending}>
            {addContainmentMutation.isPending ? t("actions.saving") : t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// --------------------------------------------------------------------------
// 4. Create CAPA Modal
// --------------------------------------------------------------------------

interface CreateCapaModalProps {
  legalEntityId: string;
  ncrId?: string | null;
  isOpen: boolean;
  onClose: () => void;
}

export function CreateCapaModal({ legalEntityId, ncrId, isOpen, onClose }: CreateCapaModalProps): React.JSX.Element | null {
  const { t } = useTranslation("quality");
  const createCapaMutation = useCreateCapa();

  const [title, setTitle] = useState("");
  const [problemStatement, setProblemStatement] = useState("");
  const [type, setType] = useState("Corrective");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim() || !problemStatement.trim()) return;

    await createCapaMutation.mutateAsync({
      legalEntityId,
      request: {
        title: title.trim(),
        problemStatement: problemStatement.trim(),
        type,
        ncrId: ncrId || null
      }
    });
    onClose();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("capas.createTitle")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <Select
          label={t("capas.type")}
          selectedKey={type}
          onSelectionChange={(val) => val && setType(String(val))}
          options={[
            { id: "Corrective", label: t("capas.typeCorrective") },
            { id: "Preventive", label: t("capas.typePreventive") }
          ]}
        />

        <TextField
          label={t("capas.title")}
          value={title}
          onChange={(val) => setTitle(val)}
          placeholder={t("capas.titlePlaceholder")}
          required
        />

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem", color: "#102a43" }}>
            {t("capas.problemStatement")}
          </label>
          <textarea
            value={problemStatement}
            onChange={(e) => setProblemStatement(e.target.value)}
            rows={3}
            required
            style={{
              width: "100%",
              padding: "0.5rem 0.75rem",
              borderRadius: "0.375rem",
              border: "1px solid #cbd5e1",
              fontSize: "0.875rem",
              fontFamily: "inherit",
              resize: "vertical"
            }}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" disabled={createCapaMutation.isPending}>
            {createCapaMutation.isPending ? t("actions.saving") : t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// --------------------------------------------------------------------------
// 5. Add CAPA Action Modal
// --------------------------------------------------------------------------

interface AddCapaActionModalProps {
  capaId: string;
  isOpen: boolean;
  onClose: () => void;
}

export function AddCapaActionModal({ capaId, isOpen, onClose }: AddCapaActionModalProps): React.JSX.Element | null {
  const { t } = useTranslation("quality");
  const addActionMutation = useAddCapaActionItem();

  const [actionType, setActionType] = useState("CorrectiveAction");
  const [description, setDescription] = useState("");
  const [ownerId, setOwnerId] = useState("");
  const [dueDate, setDueDate] = useState(() => new Date(Date.now() + 14 * 86400000).toISOString().split("T")[0]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!description.trim() || !ownerId.trim() || !dueDate) return;

    await addActionMutation.mutateAsync({
      capaId,
      request: {
        actionType,
        description: description.trim(),
        ownerId: ownerId.trim(),
        dueDate
      }
    });
    onClose();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("capas.addActionTitle")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <Select
          label={t("capas.actionType")}
          selectedKey={actionType}
          onSelectionChange={(val) => val && setActionType(String(val))}
          options={[
            { id: "CorrectiveAction", label: t("capas.actionCorrective") },
            { id: "PreventiveAction", label: t("capas.actionPreventive") },
            { id: "ProcessImprovement", label: t("capas.actionProcess") }
          ]}
        />

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem", color: "#102a43" }}>
            {t("capas.actionDescription")}
          </label>
          <textarea
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            rows={3}
            required
            style={{
              width: "100%",
              padding: "0.5rem 0.75rem",
              borderRadius: "0.375rem",
              border: "1px solid #cbd5e1",
              fontSize: "0.875rem",
              fontFamily: "inherit",
              resize: "vertical"
            }}
          />
        </div>

        <TextField
          label={t("capas.ownerId")}
          value={ownerId}
          onChange={(val) => setOwnerId(val)}
          placeholder={t("capas.ownerPlaceholder")}
          required
        />

        <TextField
          label={t("capas.dueDate")}
          type="date"
          value={dueDate}
          onChange={(val) => setDueDate(val)}
          required
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" disabled={addActionMutation.isPending}>
            {addActionMutation.isPending ? t("actions.saving") : t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// --------------------------------------------------------------------------
// 6. Complete CAPA Action Modal
// --------------------------------------------------------------------------

interface CompleteCapaActionModalProps {
  capaId: string;
  actionId: string;
  isOpen: boolean;
  onClose: () => void;
}

export function CompleteCapaActionModal({ capaId, actionId, isOpen, onClose }: CompleteCapaActionModalProps): React.JSX.Element | null {
  const { t } = useTranslation("quality");
  const completeActionMutation = useCompleteCapaActionItem();

  const [evidenceReference, setEvidenceReference] = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    await completeActionMutation.mutateAsync({
      actionId,
      request: {
        evidenceReference: evidenceReference.trim() || null
      }
    });
    onClose();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("capas.completeActionTitle")}
      size="small"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <TextField
          label={t("capas.evidenceReference")}
          value={evidenceReference}
          onChange={(val) => setEvidenceReference(val)}
          placeholder={t("capas.evidencePlaceholder")}
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" disabled={completeActionMutation.isPending}>
            {completeActionMutation.isPending ? t("actions.completing") : t("actions.complete")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// --------------------------------------------------------------------------
// 7. Add CAPA Root Cause Modal
// --------------------------------------------------------------------------

interface AddCapaRootCauseModalProps {
  capaId: string;
  isOpen: boolean;
  onClose: () => void;
}

export function AddCapaRootCauseModal({ capaId, isOpen, onClose }: AddCapaRootCauseModalProps): React.JSX.Element | null {
  const { t } = useTranslation("quality");
  const addRootCauseMutation = useAddCapaRootCause();

  const [category, setCategory] = useState("Method");
  const [description, setDescription] = useState("");
  const [verified, setVerified] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!description.trim()) return;

    await addRootCauseMutation.mutateAsync({
      capaId,
      request: {
        category,
        description: description.trim(),
        verified
      }
    });
    onClose();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("capas.addRootCauseTitle")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <Select
          label={t("capas.rootCauseCategory")}
          selectedKey={category}
          onSelectionChange={(val) => val && setCategory(String(val))}
          options={[
            { id: "Man", label: t("capas.categoryMan") },
            { id: "Machine", label: t("capas.categoryMachine") },
            { id: "Material", label: t("capas.categoryMaterial") },
            { id: "Method", label: t("capas.categoryMethod") },
            { id: "Measurement", label: t("capas.categoryMeasurement") },
            { id: "Environment", label: t("capas.categoryEnvironment") }
          ]}
        />

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem", color: "#102a43" }}>
            {t("capas.rootCauseDescription")}
          </label>
          <textarea
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            rows={3}
            required
            style={{
              width: "100%",
              padding: "0.5rem 0.75rem",
              borderRadius: "0.375rem",
              border: "1px solid #cbd5e1",
              fontSize: "0.875rem",
              fontFamily: "inherit",
              resize: "vertical"
            }}
          />
        </div>

        <label style={{ display: "flex", alignItems: "center", gap: "0.5rem", fontSize: "0.875rem", cursor: "pointer" }}>
          <input
            type="checkbox"
            checked={verified}
            onChange={(e) => setVerified(e.target.checked)}
            style={{ width: "1rem", height: "1rem" }}
          />
          <span style={{ fontWeight: 600, color: "#102a43" }}>{t("capas.rootCauseVerified")}</span>
        </label>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" disabled={addRootCauseMutation.isPending}>
            {addRootCauseMutation.isPending ? t("actions.saving") : t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// --------------------------------------------------------------------------
// 8. Record Effectiveness Modal
// --------------------------------------------------------------------------

interface RecordEffectivenessModalProps {
  capaId: string;
  isOpen: boolean;
  onClose: () => void;
}

export function RecordEffectivenessModal({ capaId, isOpen, onClose }: RecordEffectivenessModalProps): React.JSX.Element | null {
  const { t } = useTranslation("quality");
  const recordEffectivenessMutation = useRecordEffectiveness();

  const [effective, setEffective] = useState(true);
  const [evidenceReference, setEvidenceReference] = useState("");
  const [notes, setNotes] = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!evidenceReference.trim() || !notes.trim()) return;

    await recordEffectivenessMutation.mutateAsync({
      capaId,
      request: {
        effective,
        evidenceReference: evidenceReference.trim(),
        notes: notes.trim()
      }
    });
    onClose();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("capas.recordEffectivenessTitle")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <Select
          label={t("capas.effectivenessResult")}
          selectedKey={effective ? "true" : "false"}
          onSelectionChange={(val) => setEffective(val === "true")}
          options={[
            { id: "true", label: t("capas.effectiveConfirmed") },
            { id: "false", label: t("capas.ineffectiveRecurring") }
          ]}
        />

        <TextField
          label={t("capas.evidenceReference")}
          value={evidenceReference}
          onChange={(val) => setEvidenceReference(val)}
          placeholder={t("capas.evidencePlaceholder")}
          required
        />

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem", color: "#102a43" }}>
            {t("capas.evaluationNotes")}
          </label>
          <textarea
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            rows={3}
            required
            style={{
              width: "100%",
              padding: "0.5rem 0.75rem",
              borderRadius: "0.375rem",
              border: "1px solid #cbd5e1",
              fontSize: "0.875rem",
              fontFamily: "inherit",
              resize: "vertical"
            }}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" disabled={recordEffectivenessMutation.isPending}>
            {recordEffectivenessMutation.isPending ? t("actions.saving") : t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
