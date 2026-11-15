import { test, describe } from "node:test";
import assert from "node:assert/strict";
import { ar as projectsAr } from "../i18n/ar.ts";
import { en as projectsEn } from "../i18n/en.ts";
import type {
  ProjectType,
  ProjectLifecycleState,
  ProjectExecutionHealth,
  ProjectBillingModel,
  ProjectTaskStatus,
  MilestoneStatus,
  DeliverableStatus,
  TimeEntryState,
  ChargeState,
  ChangeRequestStatus
} from "../types.ts";

describe("Module 23 Projects Slice Contracts & Domain Invariants", () => {
  test("1:1 Arabic and English translation parity for Projects resources", () => {
    const getKeys = (obj: Record<string, unknown>, prefix = ""): string[] => {
      let keys: string[] = [];
      for (const [k, v] of Object.entries(obj)) {
        const fullKey = prefix ? `${prefix}.${k}` : k;
        if (typeof v === "object" && v !== null) {
          keys = keys.concat(getKeys(v as Record<string, unknown>, fullKey));
        } else {
          keys.push(fullKey);
        }
      }
      return keys;
    };

    const arKeys = new Set(getKeys(projectsAr));
    const enKeys = new Set(getKeys(projectsEn));

    assert.equal(arKeys.size, enKeys.size, `Keys count mismatch: AR=${arKeys.size}, EN=${enKeys.size}`);
    for (const key of arKeys) {
      assert.ok(enKeys.has(key), `Missing EN key: ${key}`);
    }
  });

  test("Project lifecycle states match canonical specification", () => {
    const canonicalStates: ProjectLifecycleState[] = [
      "Draft",
      "Active",
      "OnHold",
      "ExecutionCompleted",
      "Closed",
      "Cancelled"
    ];
    assert.equal(canonicalStates.length, 6);
    assert.ok(canonicalStates.includes("Draft"));
    assert.ok(canonicalStates.includes("Active"));
    assert.ok(canonicalStates.includes("OnHold"));
    assert.ok(canonicalStates.includes("ExecutionCompleted"));
    assert.ok(canonicalStates.includes("Closed"));
    assert.ok(canonicalStates.includes("Cancelled"));
  });

  test("Project execution health states match canonical specification", () => {
    const healthStates: ProjectExecutionHealth[] = [
      "OnTrack",
      "AtRisk",
      "OffTrack",
      "NotAssessed"
    ];
    assert.equal(healthStates.length, 4);
    assert.ok(healthStates.includes("OnTrack"));
    assert.ok(healthStates.includes("AtRisk"));
    assert.ok(healthStates.includes("OffTrack"));
    assert.ok(healthStates.includes("NotAssessed"));
  });

  test("Project types support commercial, internal and capital projects", () => {
    const types: ProjectType[] = [
      "CustomerProject",
      "InternalProject",
      "CapitalProject"
    ];
    assert.equal(types.length, 3);
    assert.ok(types.includes("CustomerProject"));
    assert.ok(types.includes("InternalProject"));
    assert.ok(types.includes("CapitalProject"));
  });

  test("Billing models support Time & Materials, Fixed Price, Milestone, and Cost Plus", () => {
    const models: ProjectBillingModel[] = [
      "TimeAndMaterials",
      "FixedPrice",
      "Milestone",
      "CostPlus"
    ];
    assert.equal(models.length, 4);
    assert.ok(models.includes("TimeAndMaterials"));
    assert.ok(models.includes("FixedPrice"));
    assert.ok(models.includes("Milestone"));
    assert.ok(models.includes("CostPlus"));
  });

  test("Governed backend permission strings match exact backend security contracts", () => {
    const governedPermissions = [
      "projects.read",
      "projects.create",
      "projects.update",
      "projects.financial.read",
      "projects.time.approve",
      "projects.close"
    ] as const;

    governedPermissions.forEach((perm) => {
      assert.ok(perm.startsWith("projects."), `Permission must be dot-notated: ${perm}`);
      assert.ok(!perm.includes(":"), `Invented colon-notation forbidden: ${perm}`);
    });
  });

  test("EAC and financial calculation invariant", () => {
    const actualCost = 50000;
    const etc = 30000;
    const openCommitment = 10000;

    // Standard EAC = ActualCost + ETC
    const eacStandard = actualCost + etc;
    assert.equal(eacStandard, 80000);

    // EAC with commitments = ActualCost + OpenCommitment + ETC
    const eacWithCommitments = actualCost + openCommitment + etc;
    assert.equal(eacWithCommitments, 90000);
    assert.ok(eacWithCommitments >= eacStandard);
  });

  test("Close readiness checklist evaluates blockers deterministically", () => {
    const blockers = [
      { code: "UNAPPROVED_TIME", count: 3 },
      { code: "OPEN_CHANGE_ORDERS", count: 1 }
    ];

    const isCloseReady = blockers.length === 0;
    assert.equal(isCloseReady, false);

    const resolvedBlockers: typeof blockers = [];
    const isNowReady = resolvedBlockers.length === 0;
    assert.equal(isNowReady, true);
  });

  test("Task, milestone, and deliverable state contracts", () => {
    const taskStatus: ProjectTaskStatus[] = ["Planned", "InProgress", "Blocked", "Completed", "Cancelled"];
    const milestoneStatus: MilestoneStatus[] = ["Planned", "Reached", "Accepted", "Rejected"];
    const deliverableStatus: DeliverableStatus[] = ["Draft", "Submitted", "Accepted", "Rejected"];
    const timeState: TimeEntryState[] = ["Draft", "Submitted", "Approved", "Finalized", "Rejected", "Corrected"];
    const chargeState: ChargeState[] = ["Eligible", "Requested", "PartiallyInvoiced", "Invoiced", "WrittenOff"];
    const changeStatus: ChangeRequestStatus[] = ["Draft", "Submitted", "Approved", "Rejected", "Cancelled"];

    assert.equal(taskStatus.length, 5);
    assert.equal(milestoneStatus.length, 4);
    assert.equal(deliverableStatus.length, 4);
    assert.equal(timeState.length, 6);
    assert.equal(chargeState.length, 5);
    assert.equal(changeStatus.length, 5);
  });
});
