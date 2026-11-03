import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "../../../../components/primitives/table.tsx";
import { LocalDateDisplay } from "../../../../components/fields/local-date-display.tsx";
import { HrStatusBadge } from "../../components/hr-status-badge.tsx";
import { PromoteTransferModal } from "./promote-transfer-modal.tsx";
import { NewContractModal } from "./new-contract-modal.tsx";
import { RenewContractModal } from "./renew-contract-modal.tsx";
import { TerminationRehireModal } from "./termination-rehire-modal.tsx";
import { UploadDocumentModal } from "./upload-document-modal.tsx";
import {
  useEmployeeDetail,
  useEmployments,
  useAssignments,
  useContracts,
  useCompensationAgreements,
  useInsuranceEnrollments,
  useDocuments,
  useDocumentCompleteness,
  useQualifications,
  useCredentials,
  useTimeline,
  useVerifyDocumentMutation
} from "../../hooks/use-hr-queries.ts";
import { usePermissions } from "../../../../core/runtime/runtime-provider.tsx";
import type { EmployeeDocumentView } from "../../types.ts";

export interface EmployeeWorkspaceProps {
  readonly employeeId: string;
  readonly legalEntityId: string;
  readonly onBack: () => void;
  readonly hasSalaryPermission?: boolean;
}

export type WorkspaceSection =
  | "overview"
  | "assignments"
  | "contracts"
  | "compensation"
  | "documents"
  | "qualifications"
  | "insurance"
  | "timeline";

export function EmployeeWorkspace({
  employeeId,
  legalEntityId,
  onBack,
  hasSalaryPermission = false
}: EmployeeWorkspaceProps): React.JSX.Element {
  const { t } = useTranslation("hr");
  let userPerms;
  try {
    userPerms = usePermissions();
  } catch {
    // Outside runtime provider
  }
  const effectiveSalaryPermission = userPerms ? userPerms.hasPermission("hr.compensation.read") : hasSalaryPermission;

  const [activeSection, setActiveSection] = useState<WorkspaceSection>("overview");

  // Modals
  const [isPromoteOpen, setIsPromoteOpen] = useState(false);
  const [isNewContractOpen, setIsNewContractOpen] = useState(false);
  const [renewContractId, setRenewContractId] = useState<string | null>(null);
  const [terminateMode, setTerminateMode] = useState<"terminate" | "rehire" | null>(null);
  const [isUploadDocOpen, setIsUploadDocOpen] = useState(false);

  // Queries
  const employeeQuery = useEmployeeDetail(employeeId, legalEntityId);
  const employmentsQuery = useEmployments(employeeId, legalEntityId);
  const employee = employeeQuery.data;
  const employments = employmentsQuery.data ?? [];
  const currentEmployment = employments[0] ?? null;
  const currentEmploymentId = currentEmployment?.id ?? "";

  const assignmentsQuery = useAssignments(currentEmploymentId, legalEntityId);
  const contractsQuery = useContracts(currentEmploymentId, legalEntityId);
  const compensationQuery = useCompensationAgreements(employeeId, currentEmploymentId, legalEntityId, effectiveSalaryPermission);
  const insuranceQuery = useInsuranceEnrollments(employeeId, currentEmploymentId, legalEntityId);
  const documentsQuery = useDocuments(employeeId, legalEntityId);
  const completenessQuery = useDocumentCompleteness(employeeId, legalEntityId);
  const qualificationsQuery = useQualifications(employeeId, legalEntityId);
  const credentialsQuery = useCredentials(employeeId, legalEntityId);
  const timelineQuery = useTimeline(employeeId, legalEntityId);
  const verifyDocMutation = useVerifyDocumentMutation(employeeId, legalEntityId);

  const handleVerifyDoc = async (doc: EmployeeDocumentView) => {
    try {
      await verifyDocMutation.mutateAsync({
        documentId: doc.id,
        expectedVersion: doc.version,
        verified: true,
        reason: "Document verified by HR"
      });
    } catch {
      // Handled by react query
    }
  };

  const assignments = assignmentsQuery.data ?? [];
  const contracts = contractsQuery.data ?? [];
  const compensation = compensationQuery.data ?? [];
  const insurance = insuranceQuery.data ?? [];
  const documents = documentsQuery.data ?? [];
  const completeness = completenessQuery.data;
  const qualifications = qualificationsQuery.data ?? [];
  const credentials = credentialsQuery.data ?? [];
  const timeline = timelineQuery.data ?? [];

  if (employeeQuery.isLoading) {
    return (
      <div style={{ padding: "3rem", textAlign: "center" }}>
        {t("actions.loading")}
      </div>
    );
  }

  if (!employee) {
    return (
      <div style={{ padding: "3rem", textAlign: "center" }}>
        <p>{t("workspace.employeeNotFound")}</p>
        <Button variant="secondary" onPress={onBack}>
          {t("workspace.backToDirectory")}
        </Button>
      </div>
    );
  }

  const navItems: { id: WorkspaceSection; label: string; count?: number }[] = [
    { id: "overview", label: t("workspace.tabs.overview") },
    { id: "assignments", label: t("workspace.tabs.assignments"), count: assignments.length },
    { id: "contracts", label: t("workspace.tabs.contracts"), count: contracts.length },
    ...(effectiveSalaryPermission ? [{ id: "compensation" as WorkspaceSection, label: t("workspace.tabs.compensation"), count: compensation.length }] : []),
    { id: "documents", label: t("workspace.tabs.documents"), count: documents.length },
    { id: "qualifications", label: t("workspace.tabs.qualifications"), count: qualifications.length + credentials.length },
    { id: "insurance", label: t("workspace.tabs.insurance"), count: insurance.length },
    { id: "timeline", label: t("workspace.tabs.timeline"), count: timeline.length }
  ];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.5rem" }}>
      {/* 360-Degree Header Surface */}
      <Surface>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", flexWrap: "wrap", gap: "1rem" }}>
          <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
            <Button variant="secondary" size="compact" onPress={onBack} style={{ alignSelf: "flex-start" }}>
              ← {t("workspace.backToDirectory")}
            </Button>
            <div style={{ display: "flex", alignItems: "center", gap: "0.75rem", flexWrap: "wrap" }}>
              <h1 style={{ margin: 0, fontSize: "1.5rem", color: "var(--color-content-primary)" }}>
                {employee.arabicName}
              </h1>
              <HrStatusBadge status={employee.status} label={t(`employeeStatus.${employee.status}`, { defaultValue: employee.status })} />
            </div>

            <div style={{ display: "flex", gap: "1.5rem", marginTop: "0.25rem", fontSize: "0.875rem", color: "var(--color-content-muted)", flexWrap: "wrap" }}>
              {employee.englishName && (
                <span>
                  <strong>{t("workspace.header.englishName")}:</strong> {employee.englishName}
                </span>
              )}
              <span>
                <strong>{t("workspace.header.employmentNumber")}:</strong> {currentEmployment?.employmentNumber || "—"}
              </span>
              <span>
                <strong>{t("workspace.header.startDate")}:</strong>{" "}
                {currentEmployment?.startDate ? <LocalDateDisplay value={currentEmployment.startDate} /> : "—"}
              </span>
              <span>
                <strong>{t("workspace.header.employmentStatus")}:</strong>{" "}
                <HrStatusBadge status={currentEmployment?.status ?? "Active"} label={t(`employmentStatus.${currentEmployment?.status ?? "Active"}`)} />
              </span>
            </div>
          </div>

          {/* Quick Lifecycle Action Buttons */}
          <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
            <Button data-testid="workspace-action-promote" variant="primary" onPress={() => setIsPromoteOpen(true)}>
              {t("workspace.actions.promoteTransfer")}
            </Button>
            <Button data-testid="workspace-action-contract" variant="secondary" onPress={() => setIsNewContractOpen(true)}>
              {t("workspace.actions.newContract")}
            </Button>
            <Button data-testid="workspace-action-upload" variant="secondary" onPress={() => setIsUploadDocOpen(true)}>
              {t("workspace.actions.uploadDoc")}
            </Button>
            {currentEmployment?.status === "Terminated" || currentEmployment?.status === "Ended" ? (
              <Button data-testid="workspace-action-rehire" variant="primary" onPress={() => setTerminateMode("rehire")}>
                {t("workspace.actions.rehire")}
              </Button>
            ) : (
              <Button data-testid="workspace-action-terminate" variant="danger" onPress={() => setTerminateMode("terminate")}>
                {t("workspace.actions.terminate")}
              </Button>
            )}
          </div>
        </div>
      </Surface>

      {/* Workspace Inner Navigation Tabs */}
      <div style={{ display: "flex", gap: "0.5rem", borderBottom: "1px solid var(--color-border-default)", overflowX: "auto" }}>
        {navItems.map((item) => {
          const isActive = activeSection === item.id;
          return (
            <button
              key={item.id}
              type="button"
              data-testid={`workspace-tab-${item.id}`}
              onClick={() => setActiveSection(item.id)}
              style={{
                display: "inline-flex",
                alignItems: "center",
                gap: "0.5rem",
                padding: "0.625rem 1rem",
                background: isActive ? "var(--color-surface-card)" : "transparent",
                color: isActive ? "var(--color-brand-primary)" : "var(--color-content-muted)",
                fontWeight: isActive ? 600 : 500,
                fontSize: "0.875rem",
                borderStyle: "solid",
                borderWidth: "1px 1px 0 1px",
                borderColor: isActive ? "var(--color-border-default)" : "transparent",
                borderRadius: "var(--radius-md) var(--radius-md) 0 0",
                cursor: "pointer",
                whiteSpace: "nowrap"
              }}
            >
              <span>{item.label}</span>
              {item.count !== undefined && item.count > 0 && (
                <span style={{ fontSize: "0.75rem", padding: "0.125rem 0.375rem", background: "var(--color-border-default)", borderRadius: "9999px" }}>
                  {item.count}
                </span>
              )}
            </button>
          );
        })}
      </div>

      {/* Section 1: Overview */}
      {activeSection === "overview" && (
        <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem" }}>
          <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(300px, 1fr))", gap: "1rem" }}>
            <Surface title={t("workspace.overview.currentAssignmentTitle")}>
              {assignments[0] ? (
                <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem", fontSize: "0.875rem" }}>
                  <div><strong>{t("workspace.assignment.effectiveFrom")}:</strong> <LocalDateDisplay value={assignments[0].effectiveFrom} /></div>
                  <div><strong>{t("workspace.assignment.reasonCode")}:</strong> {t(`workspace.assignment.reasons.${assignments[0].reasonCode}`, { defaultValue: assignments[0].reasonCode })}</div>
                </div>
              ) : (
                <p style={{ margin: 0, color: "var(--color-content-muted)", fontSize: "0.875rem" }}>
                  {t("workspace.overview.noCurrentAssignment")}
                </p>
              )}
            </Surface>

            <Surface title={t("workspace.overview.documentCompletenessTitle")}>
              <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem", fontSize: "0.875rem" }}>
                <div><strong>{t("workspace.overview.verifiedCount")}:</strong> {completeness?.verifiedCount ?? 0} / {completeness?.totalMandatory ?? 0}</div>
                <div>
                  <strong>{t("workspace.overview.isComplete")}:</strong>{" "}
                  <HrStatusBadge status={completeness?.isComplete ? "Active" : "Pending"} label={completeness?.isComplete ? t("workspace.overview.complete") : t("workspace.overview.incomplete")} />
                </div>
              </div>
            </Surface>
          </div>
        </div>
      )}

      {/* Section 2: Assignments & History */}
      {activeSection === "assignments" && (
        <Surface title={t("workspace.assignment.historyTitle")}>
          <Table aria-label={t("workspace.assignment.historyTitle")}>
            <TableHeader>
              <TableRow>
                <TableHead>{t("workspace.assignment.effectiveFrom")}</TableHead>
                <TableHead>{t("workspace.assignment.effectiveTo")}</TableHead>
                <TableHead>{t("workspace.assignment.reasonCode")}</TableHead>
                <TableHead>{t("workspace.assignment.version")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {assignments.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={4} style={{ textAlign: "center", padding: "1.5rem", color: "var(--color-content-muted)" }}>
                    {t("workspace.assignment.emptyHistory")}
                  </TableCell>
                </TableRow>
              ) : (
                assignments.map((asg) => (
                  <TableRow key={asg.id}>
                    <TableCell><LocalDateDisplay value={asg.effectiveFrom} /></TableCell>
                    <TableCell>{asg.effectiveTo ? <LocalDateDisplay value={asg.effectiveTo} /> : t("workspace.assignment.current")}</TableCell>
                    <TableCell style={{ fontWeight: 600 }}>{t(`workspace.assignment.reasons.${asg.reasonCode}`, { defaultValue: asg.reasonCode })}</TableCell>
                    <TableCell>{asg.version}</TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </Surface>
      )}

      {/* Section 3: Contracts & Amendments */}
      {activeSection === "contracts" && (
        <Surface title={t("workspace.contracts.title")}>
          <Table aria-label={t("workspace.contracts.title")}>
            <TableHeader>
              <TableRow>
                <TableHead>{t("workspace.contracts.type")}</TableHead>
                <TableHead>{t("workspace.contracts.startDate")}</TableHead>
                <TableHead>{t("workspace.contracts.endDate")}</TableHead>
                <TableHead>{t("workspace.contracts.status")}</TableHead>
                <TableHead style={{ textAlign: "end" }}>{t("workspace.contracts.actions")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {contracts.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={5} style={{ textAlign: "center", padding: "1.5rem", color: "var(--color-content-muted)" }}>
                    {t("workspace.contracts.empty")}
                  </TableCell>
                </TableRow>
              ) : (
                contracts.map((ct) => (
                  <TableRow key={ct.id}>
                    <TableCell style={{ fontWeight: 600 }}>
                      {t(`workspace.contracts.types.${ct.contractType}`, { defaultValue: ct.contractType })}
                    </TableCell>
                    <TableCell>{ct.startDate ? <LocalDateDisplay value={ct.startDate} /> : "—"}</TableCell>
                    <TableCell>{ct.endDate ? <LocalDateDisplay value={ct.endDate} /> : t("workspace.contracts.indefinite")}</TableCell>
                    <TableCell>
                      <HrStatusBadge status={ct.status} label={t(`statuses.${ct.status}`, { defaultValue: ct.status })} />
                    </TableCell>
                    <TableCell style={{ textAlign: "end" }}>
                      <Button variant="secondary" size="compact" onPress={() => setRenewContractId(ct.id)}>
                        {t("workspace.contracts.renewAction")}
                      </Button>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </Surface>
      )}

      {/* Section 4: Compensation */}
      {activeSection === "compensation" && effectiveSalaryPermission && (
        <Surface title={t("workspace.compensation.title")}>
          <Table aria-label={t("workspace.compensation.title")}>
            <TableHeader>
              <TableRow>
                <TableHead>{t("workspace.compensation.amount")}</TableHead>
                <TableHead>{t("workspace.compensation.currency")}</TableHead>
                <TableHead>{t("workspace.compensation.frequency")}</TableHead>
                <TableHead>{t("workspace.compensation.effectiveFrom")}</TableHead>
                <TableHead>{t("workspace.compensation.status")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {compensation.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={5} style={{ textAlign: "center", padding: "1.5rem", color: "var(--color-content-muted)" }}>
                    {t("workspace.compensation.empty")}
                  </TableCell>
                </TableRow>
              ) : (
                compensation.map((comp) => (
                  <TableRow key={comp.id}>
                    <TableCell style={{ fontWeight: 700, fontSize: "1rem" }}>{comp.amount}</TableCell>
                    <TableCell>{comp.currencyCode}</TableCell>
                    <TableCell>{t(`frequencies.${comp.payFrequency}`, { defaultValue: comp.payFrequency })}</TableCell>
                    <TableCell><LocalDateDisplay value={comp.effectiveFrom} /></TableCell>
                    <TableCell>
                      <HrStatusBadge status={comp.status} label={t(`statuses.${comp.status}`, { defaultValue: comp.status })} />
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </Surface>
      )}

      {/* Section 5: Documents */}
      {activeSection === "documents" && (
        <Surface title={t("workspace.documents.title")}>
          <Table aria-label={t("workspace.documents.title")}>
            <TableHeader>
              <TableRow>
                <TableHead>{t("workspace.documents.category")}</TableHead>
                <TableHead>{t("workspace.documents.titleLabel")}</TableHead>
                <TableHead>{t("workspace.documents.expiryDate")}</TableHead>
                <TableHead>{t("workspace.documents.verified")}</TableHead>
                <TableHead style={{ textAlign: "end" }}>{t("actions.action", "الإجراء")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {documents.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={5} style={{ textAlign: "center", padding: "1.5rem", color: "var(--color-content-muted)" }}>
                    {t("workspace.documents.empty")}
                  </TableCell>
                </TableRow>
              ) : (
                documents.map((doc) => {
                  const isDocVerified = doc.verificationStatus === "Verified" || doc.isVerified === true;
                  return (
                    <TableRow key={doc.id}>
                      <TableCell>{t(`workspace.documents.categories.${doc.category}`, doc.category)}</TableCell>
                      <TableCell style={{ fontWeight: 600 }}>{doc.titleAr || doc.category}</TableCell>
                      <TableCell>{doc.expiryDate ? <LocalDateDisplay value={doc.expiryDate} /> : "—"}</TableCell>
                      <TableCell><HrStatusBadge status={isDocVerified ? "Verified" : "Pending"} /></TableCell>
                      <TableCell style={{ textAlign: "end" }}>
                        {!isDocVerified && (
                          <Button
                            size="compact"
                            variant="secondary"
                            onPress={() => handleVerifyDoc(doc)}
                            isDisabled={verifyDocMutation.isPending}
                          >
                            {t("workspace.documents.verify", "تحقق")}
                          </Button>
                        )}
                      </TableCell>
                    </TableRow>
                  );
                })
              )}
            </TableBody>
          </Table>
        </Surface>
      )}

      {/* Section 6: Qualifications & Credentials */}
      {activeSection === "qualifications" && (
        <Surface title={t("workspace.qualifications.title")}>
          <Table aria-label={t("workspace.qualifications.title")}>
            <TableHeader>
              <TableRow>
                <TableHead>{t("workspace.qualifications.level")}</TableHead>
                <TableHead>{t("workspace.qualifications.title")}</TableHead>
                <TableHead>{t("workspace.qualifications.institution")}</TableHead>
                <TableHead>{t("workspace.qualifications.year")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {qualifications.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={4} style={{ textAlign: "center", padding: "1.5rem", color: "var(--color-content-muted)" }}>
                    {t("workspace.qualifications.empty")}
                  </TableCell>
                </TableRow>
              ) : (
                qualifications.map((q) => (
                  <TableRow key={q.id}>
                    <TableCell>{q.level}</TableCell>
                    <TableCell style={{ fontWeight: 600 }}>{q.titleAr}</TableCell>
                    <TableCell>{q.institution}</TableCell>
                    <TableCell>{q.graduationYear ?? "—"}</TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </Surface>
      )}

      {/* Section 7: Insurance */}
      {activeSection === "insurance" && (
        <Surface title={t("workspace.insurance.title")}>
          <Table aria-label={t("workspace.insurance.title")}>
            <TableHeader>
              <TableRow>
                <TableHead>{t("workspace.insurance.scheme")}</TableHead>
                <TableHead>{t("workspace.insurance.memberNumber")}</TableHead>
                <TableHead>{t("workspace.insurance.effectiveFrom")}</TableHead>
                <TableHead>{t("workspace.insurance.status")}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {insurance.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={4} style={{ textAlign: "center", padding: "1.5rem", color: "var(--color-content-muted)" }}>
                    {t("workspace.insurance.empty")}
                  </TableCell>
                </TableRow>
              ) : (
                insurance.map((ins) => (
                  <TableRow key={ins.id}>
                    <TableCell style={{ fontWeight: 600 }}>{ins.schemeCode}</TableCell>
                    <TableCell>{ins.maskedMemberNumber ?? "—"}</TableCell>
                    <TableCell><LocalDateDisplay value={ins.effectiveFrom} /></TableCell>
                    <TableCell><HrStatusBadge status={ins.status} /></TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </Surface>
      )}

      {/* Section 8: Timeline */}
      {activeSection === "timeline" && (
        <Surface title={t("workspace.timeline.title")}>
          {timeline.length === 0 ? (
            <p style={{ margin: 0, color: "var(--color-content-muted)", fontSize: "0.875rem" }}>
              {t("workspace.timeline.empty")}
            </p>
          ) : (
            <div style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
              {timeline.map((evt) => (
                <div key={evt.id} style={{ display: "flex", gap: "1rem", borderLeft: "2px solid var(--color-brand-primary)", paddingLeft: "1rem" }}>
                  <div>
                    <span style={{ fontSize: "0.75rem", color: "var(--color-content-muted)", display: "block" }}>
                      <LocalDateDisplay value={evt.effectiveOn} />
                    </span>
                    <strong style={{ fontSize: "0.875rem", color: "var(--color-content-primary)" }}>
                      {evt.eventType}
                    </strong>
                  </div>
                </div>
              ))}
            </div>
          )}
        </Surface>
      )}

      {/* Modals */}
      <PromoteTransferModal
        isOpen={isPromoteOpen}
        onClose={() => setIsPromoteOpen(false)}
        employmentId={currentEmploymentId}
        legalEntityId={legalEntityId}
      />

      <NewContractModal
        isOpen={isNewContractOpen}
        onClose={() => setIsNewContractOpen(false)}
        employmentId={currentEmploymentId}
        legalEntityId={legalEntityId}
      />

      {renewContractId && (
        <RenewContractModal
          isOpen={Boolean(renewContractId)}
          onClose={() => setRenewContractId(null)}
          employmentId={currentEmploymentId}
          contractId={renewContractId}
          legalEntityId={legalEntityId}
        />
      )}

      {terminateMode && (
        <TerminationRehireModal
          isOpen={Boolean(terminateMode)}
          onClose={() => setTerminateMode(null)}
          employeeId={employeeId}
          legalEntityId={legalEntityId}
          currentEmployment={currentEmployment}
          mode={terminateMode}
        />
      )}

      <UploadDocumentModal
        isOpen={isUploadDocOpen}
        onClose={() => setIsUploadDocOpen(false)}
        employeeId={employeeId}
        legalEntityId={legalEntityId}
        employmentId={currentEmploymentId}
      />
    </div>
  );
}
