import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Surface } from "../../../../components/pages/surface.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import { TextField } from "../../../../components/primitives/text-field.tsx";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "../../../../components/primitives/table.tsx";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { HrStatusBadge } from "../../components/hr-status-badge.tsx";
import { useDepartments, usePositions, useCreateDepartmentMutation, useCreatePositionMutation } from "../../hooks/use-hr-queries.ts";
import { mapHrError } from "../../services/hr-error-map.ts";

export interface OrganizationTabProps {
  readonly legalEntityId: string;
}

export function OrganizationTab({ legalEntityId }: OrganizationTabProps): React.JSX.Element {
  const { t } = useTranslation("hr");
  const departmentsQuery = useDepartments(legalEntityId);
  const positionsQuery = usePositions(legalEntityId);

  const createDeptMutation = useCreateDepartmentMutation(legalEntityId);
  const createPosMutation = useCreatePositionMutation(legalEntityId);

  const departments = departmentsQuery.data ?? [];
  const positions = positionsQuery.data ?? [];

  // Modal States
  const [isDeptModalOpen, setIsDeptModalOpen] = useState(false);
  const [deptCode, setDeptCode] = useState("");
  const [deptNameAr, setDeptNameAr] = useState("");
  const [deptNameEn, setDeptNameEn] = useState("");
  const [deptError, setDeptError] = useState<string | null>(null);

  const [isPosModalOpen, setIsPosModalOpen] = useState(false);
  const [posCode, setPosCode] = useState("");
  const [posTitleAr, setPosTitleAr] = useState("");
  const [posTitleEn, setPosTitleEn] = useState("");
  const [posError, setPosError] = useState<string | null>(null);

  const handleCreateDept = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!deptCode.trim() || !deptNameAr.trim()) return;

    try {
      await createDeptMutation.mutateAsync({
        legalEntityId,
        code: deptCode.trim(),
        nameAr: deptNameAr.trim(),
        nameEn: deptNameEn.trim() || null
      });
      setDeptCode("");
      setDeptNameAr("");
      setDeptNameEn("");
      setIsDeptModalOpen(false);
    } catch (err: unknown) {
      const mapped = mapHrError(err);
      setDeptError(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  const handleCreatePos = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!posCode.trim() || !posTitleAr.trim()) return;

    try {
      await createPosMutation.mutateAsync({
        legalEntityId,
        code: posCode.trim(),
        titleAr: posTitleAr.trim(),
        titleEn: posTitleEn.trim() || null
      });
      setPosCode("");
      setPosTitleAr("");
      setPosTitleEn("");
      setIsPosModalOpen(false);
    } catch (err: unknown) {
      const mapped = mapHrError(err);
      setPosError(t(mapped.messageKey, mapped.defaultMessage));
    }
  };

  return (
    <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(400px, 1fr))", gap: "1.5rem" }}>
      {/* Departments Section */}
      <Surface
        title={t("organization.departmentsTitle")}
        headerActions={
          <Button variant="primary" size="compact" onPress={() => setIsDeptModalOpen(true)}>
            + {t("organization.newDepartment")}
          </Button>
        }
      >
        <Table aria-label={t("organization.departmentsTitle")}>
          <TableHeader>
            <TableRow>
              <TableHead>{t("organization.code")}</TableHead>
              <TableHead>{t("organization.nameAr")}</TableHead>
              <TableHead>{t("organization.nameEn")}</TableHead>
              <TableHead>{t("organization.status")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {departments.length === 0 ? (
              <TableRow>
                <TableCell colSpan={4} style={{ textAlign: "center", color: "var(--color-content-muted)" }}>
                  {t("organization.noDepartments")}
                </TableCell>
              </TableRow>
            ) : (
              departments.map((dept) => (
                <TableRow key={dept.id}>
                  <TableCell style={{ fontFamily: "monospace", fontWeight: 600 }}>{dept.code}</TableCell>
                  <TableCell style={{ fontWeight: 600 }}>{dept.nameAr}</TableCell>
                  <TableCell style={{ color: "var(--color-content-muted)" }}>{dept.nameEn || "—"}</TableCell>
                  <TableCell>
                    <HrStatusBadge status={dept.status} label={t(`statuses.${dept.status}`, { defaultValue: dept.status })} />
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </Surface>

      {/* Positions Section */}
      <Surface
        title={t("organization.positionsTitle")}
        headerActions={
          <Button variant="primary" size="compact" onPress={() => setIsPosModalOpen(true)}>
            + {t("organization.newPosition")}
          </Button>
        }
      >
        <Table aria-label={t("organization.positionsTitle")}>
          <TableHeader>
            <TableRow>
              <TableHead>{t("organization.code")}</TableHead>
              <TableHead>{t("organization.nameAr")}</TableHead>
              <TableHead>{t("organization.nameEn")}</TableHead>
              <TableHead>{t("organization.status")}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {positions.length === 0 ? (
              <TableRow>
                <TableCell colSpan={4} style={{ textAlign: "center", color: "var(--color-content-muted)" }}>
                  {t("organization.noPositions")}
                </TableCell>
              </TableRow>
            ) : (
              positions.map((pos) => (
                <TableRow key={pos.id}>
                  <TableCell style={{ fontFamily: "monospace", fontWeight: 600 }}>{pos.code}</TableCell>
                  <TableCell style={{ fontWeight: 600 }}>{pos.nameAr}</TableCell>
                  <TableCell style={{ color: "var(--color-content-muted)" }}>{pos.nameEn || "—"}</TableCell>
                  <TableCell>
                    <HrStatusBadge status={pos.status} label={t(`statuses.${pos.status}`, { defaultValue: pos.status })} />
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </Surface>

      {/* Department Modal */}
      <Dialog isOpen={isDeptModalOpen} onOpenChange={setIsDeptModalOpen} title={t("organization.newDepartment")} size="medium">
        <form onSubmit={handleCreateDept} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
          {deptError && <div role="alert" style={{ color: "red" }}>{deptError}</div>}
          <TextField label={t("organization.code")} value={deptCode} onChange={setDeptCode} isRequired />
          <TextField label={t("organization.nameAr")} value={deptNameAr} onChange={setDeptNameAr} isRequired />
          <TextField label={t("organization.nameEn")} value={deptNameEn} onChange={setDeptNameEn} />
          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem", borderTop: "1px solid #e2e8f0", paddingTop: "1rem" }}>
            <Button variant="secondary" onPress={() => setIsDeptModalOpen(false)}>{t("actions.cancel")}</Button>
            <Button variant="primary" type="submit">{t("actions.confirm")}</Button>
          </div>
        </form>
      </Dialog>

      {/* Position Modal */}
      <Dialog isOpen={isPosModalOpen} onOpenChange={setIsPosModalOpen} title={t("organization.newPosition")} size="medium">
        <form onSubmit={handleCreatePos} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
          {posError && <div role="alert" style={{ color: "red" }}>{posError}</div>}
          <TextField label={t("organization.code")} value={posCode} onChange={setPosCode} isRequired />
          <TextField label={t("organization.nameAr")} value={posTitleAr} onChange={setPosTitleAr} isRequired />
          <TextField label={t("organization.nameEn")} value={posTitleEn} onChange={setPosTitleEn} />
          <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.75rem", marginTop: "1rem", borderTop: "1px solid #e2e8f0", paddingTop: "1rem" }}>
            <Button variant="secondary" onPress={() => setIsPosModalOpen(false)}>{t("actions.cancel")}</Button>
            <Button variant="primary" type="submit">{t("actions.confirm")}</Button>
          </div>
        </form>
      </Dialog>
    </div>
  );
}
