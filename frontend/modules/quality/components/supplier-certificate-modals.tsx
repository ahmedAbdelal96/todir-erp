import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../components/primitives/dialog.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import { TextField } from "../../../components/primitives/text-field.tsx";
import { Select } from "../../../components/primitives/select.tsx";
import {
  useSaveSupplierProfile,
  useCreateScar,
  useSubmitScarResponse,
  useReviewScar,
  useIssueCertificate,
  useQualityCertificate,
  useQualityInspections
} from "../hooks/use-quality-queries.ts";
import { QualityStatusBadge } from "./quality-status-badge.tsx";

// --------------------------------------------------------------------------
// 1. Save Supplier Profile Modal
// --------------------------------------------------------------------------

interface SaveSupplierProfileModalProps {
  legalEntityId: string;
  isOpen: boolean;
  onClose: () => void;
  initialData?: {
    supplierPartyId: string;
    riskLevel?: string;
    targetPpm?: number;
    receivingInspectionRequired?: boolean;
  } | null;
}

export function SaveSupplierProfileModal({
  legalEntityId,
  isOpen,
  onClose,
  initialData
}: SaveSupplierProfileModalProps): React.JSX.Element | null {
  const { t } = useTranslation("quality");
  const saveProfileMutation = useSaveSupplierProfile();

  const [supplierPartyId, setSupplierPartyId] = useState(initialData?.supplierPartyId || "");
  const [riskLevel, setRiskLevel] = useState(initialData?.riskLevel || "Medium");
  const [targetPpm, setTargetPpm] = useState(String(initialData?.targetPpm ?? 50));
  const [receivingInspectionRequired, setReceivingInspectionRequired] = useState(
    initialData?.receivingInspectionRequired ?? true
  );

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!supplierPartyId.trim()) return;

    await saveProfileMutation.mutateAsync({
      legalEntityId,
      supplierPartyId: supplierPartyId.trim(),
      riskLevel,
      targetPpm: Number(targetPpm) || 0,
      receivingInspectionRequired
    });
    onClose();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={initialData ? t("suppliers.editProfile") : t("suppliers.createProfile")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <TextField
          label={t("suppliers.partyId")}
          value={supplierPartyId}
          onChange={(val) => setSupplierPartyId(val)}
          placeholder={t("suppliers.partyIdPlaceholder")}
          required
          disabled={Boolean(initialData)}
        />

        <Select
          label={t("suppliers.riskLevel")}
          selectedKey={riskLevel}
          onSelectionChange={(val) => val && setRiskLevel(String(val))}
          options={[
            { id: "Low", label: t("risk.low") },
            { id: "Medium", label: t("risk.medium") },
            { id: "High", label: t("risk.high") },
            { id: "Critical", label: t("risk.critical") }
          ]}
        />

        <TextField
          label={t("suppliers.targetPpm")}
          type="number"
          value={targetPpm}
          onChange={(val) => setTargetPpm(val)}
          placeholder="50"
          required
        />

        <label style={{ display: "flex", alignItems: "center", gap: "0.5rem", fontSize: "0.875rem", cursor: "pointer" }}>
          <input
            type="checkbox"
            checked={receivingInspectionRequired}
            onChange={(e) => setReceivingInspectionRequired(e.target.checked)}
            style={{ width: "1rem", height: "1rem" }}
          />
          <span style={{ fontWeight: 600, color: "#102a43" }}>{t("suppliers.receivingInspectionRequired")}</span>
        </label>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" disabled={saveProfileMutation.isPending}>
            {saveProfileMutation.isPending ? t("actions.saving") : t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// --------------------------------------------------------------------------
// 2. Create SCAR Modal
// --------------------------------------------------------------------------

interface CreateScarModalProps {
  legalEntityId: string;
  isOpen: boolean;
  onClose: () => void;
  defaultSupplierId?: string;
  defaultNcrId?: string;
}

export function CreateScarModal({
  legalEntityId,
  isOpen,
  onClose,
  defaultSupplierId = "",
  defaultNcrId = ""
}: CreateScarModalProps): React.JSX.Element | null {
  const { t } = useTranslation("quality");
  const createScarMutation = useCreateScar();

  const [supplierPartyId, setSupplierPartyId] = useState(defaultSupplierId);
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [ncrId, setNcrId] = useState(defaultNcrId);
  const [dueDate, setDueDate] = useState(() => new Date(Date.now() + 14 * 86400000).toISOString().split("T")[0]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!supplierPartyId.trim() || !title.trim() || !description.trim()) return;

    await createScarMutation.mutateAsync({
      legalEntityId,
      supplierPartyId: supplierPartyId.trim(),
      title: title.trim(),
      description: description.trim(),
      ncrId: ncrId.trim() || null,
      dueDate: dueDate || null
    });
    onClose();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("scars.createTitle")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <TextField
          label={t("scars.supplierPartyId")}
          value={supplierPartyId}
          onChange={(val) => setSupplierPartyId(val)}
          placeholder={t("scars.supplierPlaceholder")}
          required
        />

        <TextField
          label={t("scars.title")}
          value={title}
          onChange={(val) => setTitle(val)}
          placeholder={t("scars.titlePlaceholder")}
          required
        />

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem", color: "#102a43" }}>
            {t("scars.description")}
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
          label={t("scars.ncrId")}
          value={ncrId}
          onChange={(val) => setNcrId(val)}
          placeholder={t("issues.optionalReference")}
        />

        <TextField
          label={t("scars.dueDate")}
          type="date"
          value={dueDate}
          onChange={(val) => setDueDate(val)}
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" disabled={createScarMutation.isPending}>
            {createScarMutation.isPending ? t("actions.saving") : t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// --------------------------------------------------------------------------
// 3. Submit SCAR Response Modal
// --------------------------------------------------------------------------

interface SubmitScarResponseModalProps {
  scarId: string;
  isOpen: boolean;
  onClose: () => void;
}

export function SubmitScarResponseModal({ scarId, isOpen, onClose }: SubmitScarResponseModalProps): React.JSX.Element | null {
  const { t } = useTranslation("quality");
  const submitResponseMutation = useSubmitScarResponse();

  const [response, setResponse] = useState("");
  const [evidenceReference, setEvidenceReference] = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!response.trim() || !evidenceReference.trim()) return;

    await submitResponseMutation.mutateAsync({
      scarId,
      request: {
        response: response.trim(),
        evidenceReference: evidenceReference.trim()
      }
    });
    onClose();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("scars.submitResponseTitle")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem", color: "#102a43" }}>
            {t("scars.responseExplanation")}
          </label>
          <textarea
            value={response}
            onChange={(e) => setResponse(e.target.value)}
            rows={4}
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
          label={t("scars.evidenceReference")}
          value={evidenceReference}
          onChange={(val) => setEvidenceReference(val)}
          placeholder={t("capas.evidencePlaceholder")}
          required
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" disabled={submitResponseMutation.isPending}>
            {submitResponseMutation.isPending ? t("actions.saving") : t("actions.submit")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// --------------------------------------------------------------------------
// 4. Review SCAR Modal
// --------------------------------------------------------------------------

interface ReviewScarModalProps {
  scarId: string;
  isOpen: boolean;
  onClose: () => void;
}

export function ReviewScarModal({ scarId, isOpen, onClose }: ReviewScarModalProps): React.JSX.Element | null {
  const { t } = useTranslation("quality");
  const reviewMutation = useReviewScar();

  const [decision, setDecision] = useState<"Accepted" | "Rejected">("Accepted");
  const [notes, setNotes] = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!notes.trim()) return;

    await reviewMutation.mutateAsync({
      scarId,
      request: {
        decision,
        notes: notes.trim()
      }
    });
    onClose();
  };

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("scars.reviewTitle")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <Select
          label={t("scars.reviewDecision")}
          selectedKey={decision}
          onSelectionChange={(val) => val && setDecision(String(val) as "Accepted" | "Rejected")}
          options={[
            { id: "Accepted", label: t("scars.decisionAccepted") },
            { id: "Rejected", label: t("scars.decisionRejected") }
          ]}
        />

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem", color: "#102a43" }}>
            {t("scars.reviewNotes")}
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
          <Button
            variant={decision === "Accepted" ? "primary" : "destructive"}
            type="submit"
            disabled={reviewMutation.isPending}
          >
            {reviewMutation.isPending ? t("actions.saving") : t("actions.confirm")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// --------------------------------------------------------------------------
// 5. Issue Certificate Modal
// --------------------------------------------------------------------------

interface IssueCertificateModalProps {
  legalEntityId: string;
  inspectionId: string;
  isOpen: boolean;
  onClose: () => void;
}

export function IssueCertificateModal({ legalEntityId, inspectionId, isOpen, onClose }: IssueCertificateModalProps): React.JSX.Element | null {
  const { t } = useTranslation("quality");
  const issueMutation = useIssueCertificate();
  const { data: inspectionsData } = useQualityInspections(legalEntityId);

  const [targetInspectionId, setTargetInspectionId] = useState(inspectionId || "");
  const [certificateType, setCertificateType] = useState("COA");
  const [customerReference, setCustomerReference] = useState("");

  React.useEffect(() => {
    if (inspectionId) {
      setTargetInspectionId(inspectionId);
    } else if (inspectionsData?.items && inspectionsData.items.length > 0 && !targetInspectionId) {
      const first = inspectionsData.items[0];
      if (first) setTargetInspectionId(first.id);
    }
  }, [inspectionId, inspectionsData, targetInspectionId]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const finalInspectionId = inspectionId || targetInspectionId.trim();
    if (!finalInspectionId) return;

    await issueMutation.mutateAsync({
      legalEntityId,
      request: {
        inspectionId: finalInspectionId,
        certificateType,
        customerReference: customerReference.trim() || null
      }
    });
    onClose();
  };

  const inspectionOptions = (inspectionsData?.items ?? []).map((ins) => ({
    id: ins.id,
    label: `${ins.number} - ${ins.eventType} (${ins.lotQuantity})`
  }));

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={t("certificates.issueTitle")}
      size="medium"
    >
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        {!inspectionId && inspectionOptions.length > 0 ? (
          <Select
            label={t("certificates.inspectionId", "رقم أمر الفحص المرجعي")}
            selectedKey={targetInspectionId}
            onSelectionChange={(val) => val && setTargetInspectionId(String(val))}
            options={inspectionOptions}
          />
        ) : !inspectionId ? (
          <TextField
            label={t("certificates.inspectionId", "رقم أمر الفحص المرجعي")}
            value={targetInspectionId}
            onChange={(val) => setTargetInspectionId(val)}
            required
          />
        ) : null}

        <Select
          label={t("certificates.certificateType")}
          selectedKey={certificateType}
          onSelectionChange={(val) => val && setCertificateType(String(val))}
          options={[
            { id: "COA", label: t("certificates.typeCoa") },
            { id: "COC", label: t("certificates.typeCoc") }
          ]}
        />

        <TextField
          label={t("certificates.customerReference")}
          value={customerReference}
          onChange={(val) => setCustomerReference(val)}
          placeholder={t("certificates.customerPlaceholder")}
        />

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" disabled={issueMutation.isPending}>
            {issueMutation.isPending ? t("actions.issuing") : t("actions.issueCertificate")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

// --------------------------------------------------------------------------
// 6. View Certificate Modal (Customer-safe print/view)
// --------------------------------------------------------------------------

interface CertificateViewModalProps {
  certificateId: string | null;
  isOpen: boolean;
  onClose: () => void;
}

export function CertificateViewModal({ certificateId, isOpen, onClose }: CertificateViewModalProps): React.JSX.Element | null {
  const { t } = useTranslation("quality");
  const { data: cert, isLoading } = useQualityCertificate(certificateId);

  if (!isOpen) return null;

  return (
    <Dialog
      isOpen={isOpen}
      onClose={onClose}
      title={cert?.number ? `${t("certificates.documentTitle")} - ${cert.number}` : t("certificates.documentTitle")}
      size="large"
    >
      {isLoading ? (
        <div style={{ padding: "2rem", textAlign: "center", color: "#627d98" }}>{t("messages.loading")}</div>
      ) : cert ? (
        <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", padding: "0.5rem" }}>
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              paddingBottom: "1rem",
              borderBottom: "2px solid #cbd5e1"
            }}
          >
            <div>
              <h2 style={{ margin: 0, fontSize: "1.25rem", color: "#102a43", fontWeight: 700 }}>
                {cert.certificateType === "COA" ? t("certificates.coaFullName") : t("certificates.cocFullName")}
              </h2>
              <div style={{ fontSize: "0.875rem", color: "#627d98", marginTop: "0.25rem" }}>
                {t("certificates.number")}: <strong style={{ color: "#102a43" }}>{cert.number}</strong>
              </div>
            </div>
            <QualityStatusBadge status={cert.state} />
          </div>

          <div
            style={{
              display: "grid",
              gridTemplateColumns: "1fr 1fr",
              gap: "1rem",
              backgroundColor: "#f8fafc",
              padding: "1rem",
              borderRadius: "0.375rem",
              border: "1px solid #e2e8f0"
            }}
          >
            <div>
              <span style={{ fontSize: "0.75rem", color: "#627d98", display: "block" }}>{t("certificates.inspectionId")}</span>
              <strong style={{ fontSize: "0.875rem", color: "#102a43" }}>{cert.inspectionId}</strong>
            </div>
            <div>
              <span style={{ fontSize: "0.75rem", color: "#627d98", display: "block" }}>{t("certificates.customerReference")}</span>
              <strong style={{ fontSize: "0.875rem", color: "#102a43" }}>{cert.customerReference || "-"}</strong>
            </div>
            <div>
              <span style={{ fontSize: "0.75rem", color: "#627d98", display: "block" }}>{t("certificates.issuedAt")}</span>
              <span style={{ fontSize: "0.875rem", color: "#102a43" }}>{new Date(cert.createdAt).toLocaleDateString()}</span>
            </div>
            <div>
              <span style={{ fontSize: "0.75rem", color: "#627d98", display: "block" }}>{t("certificates.verificationStatus")}</span>
              <span style={{ fontSize: "0.875rem", color: "#166534", fontWeight: 600 }}>{t("certificates.tamperProofVerified")}</span>
            </div>
          </div>

          <div
            style={{
              border: "1px dashed #94a3b8",
              borderRadius: "0.375rem",
              padding: "1rem",
              backgroundColor: "#ffffff",
              textAlign: "center"
            }}
          >
            <p style={{ margin: 0, fontSize: "0.875rem", color: "#334e68", lineHeight: 1.6 }}>
              {t("certificates.certificationStatement")}
            </p>
          </div>

          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginTop: "0.5rem" }}>
            <Button variant="secondary" onClick={() => window.print()} type="button">
              {t("actions.printCertificate")}
            </Button>
            <Button variant="primary" onClick={onClose} type="button">
              {t("actions.close")}
            </Button>
          </div>
        </div>
      ) : (
        <div style={{ padding: "2rem", textAlign: "center", color: "#dc2626" }}>{t("messages.notFound")}</div>
      )}
    </Dialog>
  );
}
