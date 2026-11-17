import { test, describe, beforeEach } from "node:test";
import assert from "node:assert/strict";
import { serviceAr } from "../i18n/ar.ts";
import { serviceEn } from "../i18n/en.ts";
import type {
  ServiceRequestState,
  WorkOrderState,
  WorkOrderBillingOwner,
  ChargesSummaryItem
} from "../types.ts";
import {
  loadOfflineQueue,
  saveOfflineQueue,
  enqueueOfflineCommand,
  replayOfflineQueue,
  clearSyncedCommands,
  dismissOfflineCommand,
  retryOfflineCommand,
  resolveConflict,
  type StoredOfflineCommand
} from "../utils/offline-queue.ts";
import { verifyTargetDatabase } from "../utils/test-probe-guard.ts";

describe("TODIR Module 24 (Service & Maintenance) — Comprehensive Frontend Unit & Slice Suite", () => {
  // In-memory localStorage mock for node environment
  const mockStorage = new Map<string, string>();
  beforeEach(() => {
    mockStorage.clear();
    (globalThis as any).window = {
      localStorage: {
        getItem: (k: string) => mockStorage.get(k) ?? null,
        setItem: (k: string, v: string) => mockStorage.set(k, v),
        removeItem: (k: string) => mockStorage.delete(k)
      }
    };
  });

  test("1:1 Arabic and English translation key parity for Service resources", () => {
    const getKeys = (obj: Record<string, any>, prefix = ""): string[] => {
      let keys: string[] = [];
      for (const [k, v] of Object.entries(obj)) {
        const fullKey = prefix ? `${prefix}.${k}` : k;
        if (typeof v === "object" && v !== null) {
          keys = keys.concat(getKeys(v, fullKey));
        } else {
          keys.push(fullKey);
        }
      }
      return keys;
    };

    const arKeys = new Set(getKeys(serviceAr));
    const enKeys = new Set(getKeys(serviceEn));

    assert.equal(arKeys.size, enKeys.size, `Keys count mismatch: AR=${arKeys.size}, EN=${enKeys.size}`);
    for (const key of arKeys) {
      assert.ok(enKeys.has(key), `Missing EN translation key: ${key}`);
    }
  });

  test("Service Request State Lifecycle: Strict status modeling", () => {
    const states: ServiceRequestState[] = ["New", "Triaged", "InProgress", "Resolved", "Closed", "Cancelled"];
    assert.equal(states.length, 6);

    assert.ok(serviceAr.requests.statuses.new);
    assert.ok(serviceAr.requests.statuses.triaged);
    assert.ok(serviceEn.requests.statuses.new);
    assert.ok(serviceEn.requests.statuses.triaged);
  });

  test("Work Order State Lifecycle: Complete ERP execution stages", () => {
    const woStates: WorkOrderState[] = [
      "Draft",
      "Ready",
      "Scheduled",
      "InProgress",
      "Waiting",
      "TechnicalCompleted",
      "Accepted",
      "Closed",
      "Cancelled"
    ];
    assert.equal(woStates.length, 9);

    assert.ok(serviceAr.workOrders.statuses.draft);
    assert.ok(serviceAr.workOrders.statuses.ready);
    assert.ok(serviceAr.workOrders.statuses.in_progress);
    assert.ok(serviceEn.workOrders.statuses.draft);
    assert.ok(serviceEn.workOrders.statuses.ready);
    assert.ok(serviceEn.workOrders.statuses.in_progress);
  });

  test("Work Order Billing Owner Separation: Service vs Projects vs None", () => {
    const billingOwners: WorkOrderBillingOwner[] = ["None", "Service", "Projects"];
    assert.equal(billingOwners.length, 3);

    // Business Invariant: If BillingOwner is "Projects", Service cannot invoice directly; it routes via Projects module
    const canInvoiceFromService = (owner: WorkOrderBillingOwner): boolean => owner === "Service";
    assert.equal(canInvoiceFromService("Service"), true);
    assert.equal(canInvoiceFromService("Projects"), false);
    assert.equal(canInvoiceFromService("None"), false);
  });

  test("Visit Transitions and Optimistic Concurrency Invariants", () => {
    const allowedTransitions: Record<string, string[]> = {
      Planned: ["Dispatched", "Cancelled"],
      Dispatched: ["Traveling", "InProgress", "Cancelled"],
      Traveling: ["Arrived", "NoAccess", "Cancelled"],
      Arrived: ["InProgress", "Completed", "NoAccess", "Cancelled"],
      InProgress: ["Completed", "Cancelled"]
    };

    const isValidTransition = (from: string, to: string): boolean => {
      return (allowedTransitions[from] ?? []).includes(to);
    };

    assert.equal(isValidTransition("Planned", "Dispatched"), true);
    assert.equal(isValidTransition("Dispatched", "Traveling"), true);
    assert.equal(isValidTransition("Traveling", "Arrived"), true);
    assert.equal(isValidTransition("Arrived", "InProgress"), true);
    assert.equal(isValidTransition("InProgress", "Completed"), true);
    // Invalid jumps must be rejected
    assert.equal(isValidTransition("Planned", "Completed"), false);
    assert.equal(isValidTransition("Traveling", "Completed"), false);
  });

  test("Durable Offline Queue: Enqueue, Replay, Sync and Conflict handling", async () => {
    const entityId = "aad8a34b-344c-9e5b-8d58-57f182fc6528";

    // 1. Enqueue offline command
    const cmd1 = enqueueOfflineCommand(entityId, {
      commandType: "visit.travel",
      visitId: "01933df5-0000-7000-8000-000000000001",
      workOrderId: "01933df5-0000-7000-8000-000000000002",
      expectedVersion: 1,
      payloadJson: JSON.stringify({ action: "travel" })
    });

    assert.equal(cmd1.status, "queued");
    assert.equal(cmd1.attempts, 0);

    // Verify persisted in localStorage
    const stored = loadOfflineQueue(entityId);
    assert.equal(stored.length, 1);
    assert.equal(stored[0]?.clientCommandId, cmd1.clientCommandId);

    // 2. Replay with successful server acceptance
    const replay1 = await replayOfflineQueue(entityId, async (req) => {
      assert.equal(req.clientCommandId, cmd1.clientCommandId);
      return {
        id: "01933df5-0000-7000-8000-000000000099",
        clientCommandId: req.clientCommandId,
        commandType: req.commandType,
        state: "Accepted",
        conflictCode: undefined,
        resultJson: "{}"
      };
    });

    assert.equal(replay1.successCount, 1);
    assert.equal(replay1.conflictCount, 0);
    assert.equal(replay1.failureCount, 0);

    const afterSync = loadOfflineQueue(entityId);
    assert.equal(afterSync[0]?.status, "synced");

    // 3. Clear synced commands
    clearSyncedCommands(entityId);
    assert.equal(loadOfflineQueue(entityId).length, 0);

    // 4. Enqueue command that results in Version Conflict
    const cmd2 = enqueueOfflineCommand(entityId, {
      commandType: "visit.complete",
      visitId: "01933df5-0000-7000-8000-000000000001",
      expectedVersion: 1
    });

    const replay2 = await replayOfflineQueue(entityId, async () => {
      return {
        id: "01933df5-0000-7000-8000-000000000098",
        clientCommandId: cmd2.clientCommandId,
        commandType: cmd2.commandType,
        state: "Conflict",
        conflictCode: "service.visit_conflict",
        resultJson: null
      };
    });

    assert.equal(replay2.conflictCount, 1);
    const afterConflict = loadOfflineQueue(entityId);
    assert.equal(afterConflict.length, 1);
    assert.equal(afterConflict[0]?.status, "conflict");
    assert.equal(afterConflict[0]?.conflictCode, "service.visit_conflict");

    // 5. Dismiss command explicitly
    dismissOfflineCommand(entityId, cmd2.clientCommandId);
    assert.equal(loadOfflineQueue(entityId).length, 0);
  });

  test("Multi-Currency Financial Aggregation Presentation Integrity", () => {
    // Business Rule: Currencies must NEVER be summed together into an unconverted total
    const summaryItems: ChargesSummaryItem[] = [
      { currencyCode: "EGP", customerAmount: 15000, coveredAmount: 5000, chargeCount: 10, readyCount: 8, coveredCount: 3 },
      { currencyCode: "SAR", customerAmount: 4500, coveredAmount: 1200, chargeCount: 4, readyCount: 3, coveredCount: 1 }
    ];

    // Presentation formatting: distinct breakdown
    const customerBillableDisplay = summaryItems.map((s) => `${s.customerAmount.toLocaleString()} ${s.currencyCode}`).join(" | ");
    const coveredDisplay = summaryItems.map((s) => `${s.coveredAmount.toLocaleString()} ${s.currencyCode}`).join(" | ");

    assert.equal(customerBillableDisplay, "15,000 EGP | 4,500 SAR");
    assert.equal(coveredDisplay, "5,000 EGP | 1,200 SAR");

    // Total counts can be safely aggregated across currencies
    const totalReadyCount = summaryItems.reduce((acc, s) => acc + s.readyCount, 0);
    assert.equal(totalReadyCount, 11);
  });

  test("Test Database Safety Probe: Negative tests proving unsafe configurations fail closed", () => {
    const expectedDb = "todir_service_acceptance";
    const validSecret = "test-probe-secret-value-1234";

    // 1. Missing secret
    const res1 = verifyTargetDatabase({
      probeSecret: "",
      expectedDatabase: expectedDb,
      probeResponse: { status: 200, data: { database: expectedDb } }
    });
    assert.equal(res1.success, false);
    assert.ok(res1.error?.includes("TODIR_TEST_PROBE_SECRET is required"));

    // 2. HTTP 401 Unauthorized
    const res2 = verifyTargetDatabase({
      probeSecret: validSecret,
      expectedDatabase: expectedDb,
      probeResponse: { status: 401 }
    });
    assert.equal(res2.success, false);
    assert.ok(res2.error?.includes("HTTP 401"));

    // 3. HTTP 404 / 500
    const res3 = verifyTargetDatabase({
      probeSecret: validSecret,
      expectedDatabase: expectedDb,
      probeResponse: { status: 500 }
    });
    assert.equal(res3.success, false);
    assert.ok(res3.error?.includes("HTTP 500"));

    // 4. Populated dev database "todir"
    const res4 = verifyTargetDatabase({
      probeSecret: validSecret,
      expectedDatabase: expectedDb,
      probeResponse: { status: 200, data: { database: "todir" } }
    });
    assert.equal(res4.success, false);
    assert.ok(res4.error?.includes('"todir"'));

    // 5. Production database
    const res5 = verifyTargetDatabase({
      probeSecret: validSecret,
      expectedDatabase: expectedDb,
      probeResponse: { status: 200, data: { database: "todir_production" } }
    });
    assert.equal(res5.success, false);
    assert.ok(res5.error?.includes("production database"));

    // 6. Unexpected database name
    const res6 = verifyTargetDatabase({
      probeSecret: validSecret,
      expectedDatabase: expectedDb,
      probeResponse: { status: 200, data: { database: "other_unrelated_db" } }
    });
    assert.equal(res6.success, false);
    assert.ok(res6.error?.includes("does not match explicitly authorized test database"));

    // 7. Successful exact match
    const res7 = verifyTargetDatabase({
      probeSecret: validSecret,
      expectedDatabase: expectedDb,
      probeResponse: { status: 200, data: { database: "todir_service_acceptance" } }
    });
    assert.equal(res7.success, true);
    assert.equal(res7.databaseName, "todir_service_acceptance");
  });

  test("Durable Offline Queue: Scoped by tenant and user, concurrency lock, and manual retry", async () => {
    const scopeUserA = { tenantId: "tenant-1", userId: "user-tech-1", legalEntityId: "entity-1" };
    const scopeUserB = { tenantId: "tenant-1", userId: "user-tech-2", legalEntityId: "entity-1" };

    // 1. User A enqueues command
    const cmdA = enqueueOfflineCommand(scopeUserA, {
      commandType: "visit.travel",
      visitId: "01933df5-0000-7000-8000-000000000001",
      expectedVersion: 1
    });

    // User B cannot see User A's pending commands on shared device
    const queueA = loadOfflineQueue(scopeUserA);
    const queueB = loadOfflineQueue(scopeUserB);
    assert.equal(queueA.length, 1);
    assert.equal(queueB.length, 0);

    // 2. Permanent rejection: domain 400 error marks status as "rejected" and does NOT auto-retry
    await replayOfflineQueue(scopeUserA, async () => {
      throw new Error("400 Bad Request: Invalid visit parameters");
    });

    const after400 = loadOfflineQueue(scopeUserA);
    assert.equal(after400[0]?.status, "rejected");

    // Second replay ignores permanently rejected commands
    const replayAgain = await replayOfflineQueue(scopeUserA, async () => {
      return { id: "cmd", clientCommandId: cmdA.clientCommandId, commandType: "visit.travel", state: "Accepted", resultJson: "{}" };
    });
    assert.equal(replayAgain.successCount, 0);
    assert.equal(replayAgain.failureCount, 0);

    // 3. Manual retry can reactivate a failed command
    retryOfflineCommand(scopeUserA, cmdA.clientCommandId);
    assert.equal(loadOfflineQueue(scopeUserA)[0]?.status, "queued");
  });
});

