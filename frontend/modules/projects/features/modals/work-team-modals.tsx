import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { Dialog } from "../../../../components/primitives/dialog.tsx";
import { Button } from "../../../../components/primitives/button.tsx";
import {
  useAddTask,
  useAddMilestone,
  useAddDeliverable,
  useAddTeamMember,
  useAddResourceRequirement,
  useAllocateResource
} from "../../hooks/use-projects-queries.ts";
import type { ProjectTaskPriority } from "../../types.ts";

export interface NewTaskModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly projectId: string;
}

export function NewTaskModal({ isOpen, onClose, projectId }: NewTaskModalProps): React.JSX.Element {
  const { t } = useTranslation("projects");
  const addMutation = useAddTask();

  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [priority, setPriority] = useState<ProjectTaskPriority>("Normal");
  const [plannedStart, setPlannedStart] = useState("");
  const [plannedEnd, setPlannedEnd] = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim()) return;

    try {
      await addMutation.mutateAsync({
        projectId,
        request: {
          title: title.trim(),
          description: description.trim() || null,
          priority,
          plannedStart: plannedStart || null,
          plannedEnd: plannedEnd || null
        }
      });
      onClose();
    } catch {
      // Handled by mutation
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("dialogs.newTaskTitle")} size="medium">
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.title")} *
          </label>
          <input
            type="text"
            required
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.priority")}
          </label>
          <select
            value={priority}
            onChange={(e) => setPriority(e.target.value as ProjectTaskPriority)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", background: "#ffffff", boxSizing: "border-box" }}
          >
            <option value="Low">{t("taskPriorities.Low")}</option>
            <option value="Normal">{t("taskPriorities.Normal")}</option>
            <option value="High">{t("taskPriorities.High")}</option>
            <option value="Urgent">{t("taskPriorities.Urgent")}</option>
          </select>
        </div>

        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1rem" }}>
          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("fields.plannedStart")}
            </label>
            <input
              type="date"
              value={plannedStart}
              onChange={(e) => setPlannedStart(e.target.value)}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
            />
          </div>
          <div>
            <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
              {t("fields.plannedEnd")}
            </label>
            <input
              type="date"
              value={plannedEnd}
              onChange={(e) => setPlannedEnd(e.target.value)}
              style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
            />
          </div>
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.description")}
          </label>
          <textarea
            rows={3}
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={addMutation.isPending} data-testid="btn-submit-task">
            {t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

export interface NewMilestoneModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly projectId: string;
}

export function NewMilestoneModal({ isOpen, onClose, projectId }: NewMilestoneModalProps): React.JSX.Element {
  const { t } = useTranslation("projects");
  const addMutation = useAddMilestone();

  const [name, setName] = useState("");
  const [dueDate, setDueDate] = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) return;

    try {
      await addMutation.mutateAsync({
        projectId,
        request: {
          name: name.trim(),
          dueDate: dueDate || null
        }
      });
      onClose();
    } catch {
      // Handled by mutation
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("dialogs.newMilestoneTitle")} size="small">
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.milestoneName")} *
          </label>
          <input
            type="text"
            required
            value={name}
            onChange={(e) => setName(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.dueDate")}
          </label>
          <input
            type="date"
            value={dueDate}
            onChange={(e) => setDueDate(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={addMutation.isPending} data-testid="btn-submit-milestone">
            {t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

export interface NewDeliverableModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly projectId: string;
}

export function NewDeliverableModal({ isOpen, onClose, projectId }: NewDeliverableModalProps): React.JSX.Element {
  const { t } = useTranslation("projects");
  const addMutation = useAddDeliverable();

  const [name, setName] = useState("");
  const [description, setDescription] = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) return;

    try {
      await addMutation.mutateAsync({
        projectId,
        request: {
          name: name.trim(),
          description: description.trim() || null
        }
      });
      onClose();
    } catch {
      // Handled by mutation
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("dialogs.newDeliverableTitle")} size="small">
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.title")} *
          </label>
          <input
            type="text"
            required
            value={name}
            onChange={(e) => setName(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.description")}
          </label>
          <textarea
            rows={2}
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={addMutation.isPending} data-testid="btn-submit-deliverable">
            {t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

export interface NewTeamMemberModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly projectId: string;
}

export function NewTeamMemberModal({ isOpen, onClose, projectId }: NewTeamMemberModalProps): React.JSX.Element {
  const { t } = useTranslation("projects");
  const addMutation = useAddTeamMember();

  const [employeeId, setEmployeeId] = useState("83c2eada-5068-1059-8ae4-5ec1dc818ae1");
  const [roleCode, setRoleCode] = useState("PROJECT_MANAGER");
  const [allocationPercent, setAllocationPercent] = useState(100);
  const [activeFrom, setActiveFrom] = useState(new Date().toISOString().slice(0, 10));
  const [isManager, setIsManager] = useState(true);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!roleCode.trim() || !employeeId.trim()) return;

    try {
      await addMutation.mutateAsync({
        projectId,
        request: {
          employeeId: employeeId.trim(),
          roleCode: roleCode.trim(),
          allocationPercent: Number(allocationPercent),
          activeFrom,
          isManager
        }
      });
      onClose();
    } catch {
      // Handled by mutation
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("dialogs.newTeamMemberTitle")} size="small">
      <form id="new-team-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.employee")} *
          </label>
          <input
            type="text"
            required
            value={employeeId}
            onChange={(e) => setEmployeeId(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.roleCode")} *
          </label>
          <input
            type="text"
            required
            value={roleCode}
            onChange={(e) => setRoleCode(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.allocationPercent")}
          </label>
          <input
            type="number"
            min="1"
            max="100"
            required
            value={allocationPercent}
            onChange={(e) => setAllocationPercent(Number(e.target.value))}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("tables.period")} *
          </label>
          <input
            type="date"
            required
            value={activeFrom}
            onChange={(e) => setActiveFrom(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
          <input
            type="checkbox"
            id="isManagerCheckbox"
            checked={isManager}
            onChange={(e) => setIsManager(e.target.checked)}
          />
          <label htmlFor="isManagerCheckbox" style={{ fontSize: "0.875rem", fontWeight: 600 }}>
            {t("fields.isManager")}
          </label>
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isDisabled={addMutation.isPending}
            data-testid="btn-submit-team"
          >
            {t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

export interface NewResourceReqModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly projectId: string;
}

export function NewResourceReqModal({ isOpen, onClose, projectId }: NewResourceReqModalProps): React.JSX.Element {
  const { t } = useTranslation("projects");
  const addMutation = useAddResourceRequirement();

  const [roleCode, setRoleCode] = useState("");
  const [quantity, setQuantity] = useState(1);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!roleCode.trim()) return;

    try {
      await addMutation.mutateAsync({
        projectId,
        request: {
          roleCode: roleCode.trim(),
          quantity: Number(quantity)
        }
      });
      onClose();
    } catch {
      // Handled by mutation
    }
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("dialogs.newResourceReqTitle")} size="small">
      <form id="new-req-form" onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.roleCode")} *
          </label>
          <input
            type="text"
            required
            value={roleCode}
            onChange={(e) => setRoleCode(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("tables.amount")} *
          </label>
          <input
            type="number"
            min="1"
            required
            value={quantity}
            onChange={(e) => setQuantity(Number(e.target.value))}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button
            variant="primary"
            type="submit"
            isDisabled={addMutation.isPending}
            data-testid="btn-submit-req"
          >
            {t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}

export interface AllocateResourceModalProps {
  readonly isOpen: boolean;
  readonly onClose: () => void;
  readonly projectId: string;
}

export function AllocateResourceModal({ isOpen, onClose, projectId }: AllocateResourceModalProps): React.JSX.Element {
  const { t } = useTranslation("projects");
  const allocateMutation = useAllocateResource();

  const [resourceRef, setResourceRef] = useState("");
  const [allocationPercent, setAllocationPercent] = useState(100);
  const [allocatedFrom, setAllocatedFrom] = useState(new Date().toISOString().slice(0, 10));

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!resourceRef.trim()) return;

    allocateMutation.mutate(
      {
        projectId,
        request: {
          resourceRef: resourceRef.trim(),
          allocationPercent: Number(allocationPercent),
          allocatedFrom
        }
      },
      {
        onSuccess: () => {
          onClose();
        }
      }
    );
  };

  return (
    <Dialog isOpen={isOpen} onClose={onClose} title={t("dialogs.allocateResourceTitle")} size="small">
      <form onSubmit={handleSubmit} style={{ display: "flex", flexDirection: "column", gap: "1rem" }}>
        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("tables.resource")} *
          </label>
          <input
            type="text"
            required
            value={resourceRef}
            onChange={(e) => setResourceRef(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("fields.allocationPercent")}
          </label>
          <input
            type="number"
            min="1"
            max="100"
            required
            value={allocationPercent}
            onChange={(e) => setAllocationPercent(Number(e.target.value))}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div>
          <label style={{ display: "block", fontSize: "0.875rem", fontWeight: 600, marginBottom: "0.25rem" }}>
            {t("tables.period")} *
          </label>
          <input
            type="date"
            required
            value={allocatedFrom}
            onChange={(e) => setAllocatedFrom(e.target.value)}
            style={{ width: "100%", padding: "0.5rem 0.75rem", borderRadius: "6px", border: "1px solid #cbd5e1", boxSizing: "border-box" }}
          />
        </div>

        <div style={{ display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" }}>
          <Button variant="secondary" onClick={onClose} type="button">
            {t("actions.cancel")}
          </Button>
          <Button variant="primary" type="submit" isDisabled={allocateMutation.isPending} data-testid="btn-submit-allocation">
            {t("actions.save")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
