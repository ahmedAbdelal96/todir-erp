import type { OfflineCommandRequest, OfflineCommandView } from "../types.ts";

export type StoredCommandStatus = "queued" | "syncing" | "synced" | "conflict" | "rejected" | "failed";

export interface QueueScope {
  readonly tenantId: string;
  readonly userId: string;
  readonly legalEntityId: string;
}

export function isQueueScopeValid(scope: unknown): scope is QueueScope {
  if (!scope || typeof scope !== "object") return false;
  const s = scope as Partial<QueueScope>;
  return Boolean(
    typeof s.tenantId === "string" && s.tenantId.trim() !== "" && s.tenantId !== "default" &&
    typeof s.userId === "string" && s.userId.trim() !== "" && s.userId !== "default" &&
    typeof s.legalEntityId === "string" && s.legalEntityId.trim() !== "" && s.legalEntityId !== "default"
  );
}

export interface StoredOfflineCommand {
  readonly clientCommandId: string;
  commandType: string;
  visitId?: string | undefined;
  workOrderId?: string | undefined;
  expectedVersion?: number | undefined;
  payloadJson: string;
  queuedAt: string;
  status: StoredCommandStatus;
  errorMessage?: string | undefined;
  conflictCode?: string | undefined;
  attempts: number;
}

export function getStorageKey(scope: QueueScope): string {
  if (!isQueueScopeValid(scope)) {
    throw new Error(
      "SECURITY VIOLATION: Incomplete or unverified identity scope. Offline commands cannot be stored or replayed without verified tenant, user, and legal entity identities."
    );
  }
  return `todir_service_offline_queue_${scope.tenantId.trim()}_${scope.userId.trim()}_${scope.legalEntityId.trim()}`;
}

const activeReplayLocks = new Set<string>();

export function loadOfflineQueue(scope: QueueScope | null | undefined): StoredOfflineCommand[] {
  if (!isQueueScopeValid(scope)) return [];
  if (typeof window === "undefined" || !window.localStorage) return [];
  try {
    const raw = window.localStorage.getItem(getStorageKey(scope));
    if (!raw) return [];
    return JSON.parse(raw) as StoredOfflineCommand[];
  } catch {
    return [];
  }
}

export function saveOfflineQueue(scope: QueueScope, queue: readonly StoredOfflineCommand[]): void {
  if (!isQueueScopeValid(scope)) {
    throw new Error(
      "SECURITY VIOLATION: Refusing to persist offline commands with missing or unverified identity scope."
    );
  }
  if (typeof window === "undefined" || !window.localStorage) {
    throw new Error("Local storage is unavailable in current runtime environment.");
  }
  try {
    window.localStorage.setItem(getStorageKey(scope), JSON.stringify(queue));
  } catch (err) {
    throw new Error(
      `Failed to persist offline queue to local storage: ${err instanceof Error ? err.message : String(err)}`
    );
  }
}

export function enqueueOfflineCommand(
  scope: QueueScope,
  command: {
    readonly clientCommandId?: string | undefined;
    readonly commandType: string;
    readonly visitId?: string | undefined;
    readonly workOrderId?: string | undefined;
    readonly expectedVersion?: number | undefined;
    readonly payloadJson?: string | undefined;
  }
): StoredOfflineCommand {
  if (!isQueueScopeValid(scope)) {
    throw new Error(
      "SECURITY VIOLATION: Cannot enqueue offline command: user session, tenant, or legal entity is not authenticated."
    );
  }

  const current = loadOfflineQueue(scope);
  const stableId = command.clientCommandId || (typeof crypto !== "undefined" && crypto.randomUUID ? crypto.randomUUID() : undefined);
  if (!stableId) {
    throw new Error("ClientCommandId must be a valid UUIDv4.");
  }

  const newEntry: StoredOfflineCommand = {
    clientCommandId: stableId,
    commandType: command.commandType,
    visitId: command.visitId,
    workOrderId: command.workOrderId,
    expectedVersion: command.expectedVersion,
    payloadJson: command.payloadJson ?? "{}",
    queuedAt: new Date().toISOString(),
    status: "queued",
    attempts: 0
  };

  const updated = [...current, newEntry];
  saveOfflineQueue(scope, updated);
  return newEntry;
}

export async function replayOfflineQueue(
  scope: QueueScope,
  submitFn: (req: OfflineCommandRequest) => Promise<OfflineCommandView>,
  options: { readonly maxAttempts?: number } = {}
): Promise<{ successCount: number; failureCount: number; conflictCount: number; locked?: boolean }> {
  if (!isQueueScopeValid(scope)) {
    return { successCount: 0, failureCount: 0, conflictCount: 0, locked: false };
  }

  const key = getStorageKey(scope);
  if (activeReplayLocks.has(key)) {
    return { successCount: 0, failureCount: 0, conflictCount: 0, locked: true };
  }

  activeReplayLocks.add(key);
  try {
    const queue = loadOfflineQueue(scope);
    if (queue.length === 0) {
      return { successCount: 0, failureCount: 0, conflictCount: 0 };
    }

    const maxAttempts = options.maxAttempts ?? 5;
    let successCount = 0;
    let failureCount = 0;
    let conflictCount = 0;

    const updatedQueue: StoredOfflineCommand[] = [];

    for (const cmd of queue) {
      // Never auto-retry synced, conflicts, or permanently rejected commands
      if (cmd.status === "synced" || cmd.status === "conflict" || cmd.status === "rejected") {
        updatedQueue.push(cmd);
        continue;
      }

      // Check max attempts
      if (cmd.attempts >= maxAttempts) {
        cmd.status = "failed";
        cmd.errorMessage = `Exceeded maximum replay attempts (${maxAttempts}). Manual retry required.`;
        updatedQueue.push(cmd);
        failureCount += 1;
        continue;
      }

      try {
        cmd.status = "syncing";
        cmd.attempts += 1;

        const result = await submitFn({
          clientCommandId: cmd.clientCommandId,
          commandType: cmd.commandType,
          visitId: cmd.visitId,
          workOrderId: cmd.workOrderId,
          expectedVersion: cmd.expectedVersion,
          payloadJson: cmd.payloadJson
        });

        if (result.state === "Conflict" || result.conflictCode) {
          cmd.status = "conflict";
          cmd.conflictCode = result.conflictCode || "VersionConflict";
          cmd.errorMessage = `Conflict detected: ${result.conflictCode || "VersionConflict"}`;
          conflictCount += 1;
        } else if (result.state === "Accepted" || result.state === "Processed" || result.state === "Pending") {
          cmd.status = "synced";
          cmd.errorMessage = undefined;
          cmd.conflictCode = undefined;
          successCount += 1;
        } else if (result.state === "Rejected") {
          cmd.status = "rejected";
          cmd.errorMessage = result.conflictCode || "Rejected by domain policy";
          failureCount += 1;
        } else {
          cmd.status = "failed";
          cmd.errorMessage = `Unexpected server state: ${result.state}`;
          failureCount += 1;
        }
      } catch (err: any) {
        const errMsg = err?.message || String(err);
        if (errMsg.includes("409") || errMsg.includes("conflict")) {
          cmd.status = "conflict";
          cmd.conflictCode = "VersionConflict";
          cmd.errorMessage = errMsg;
          conflictCount += 1;
        } else if (
          errMsg.includes("400") ||
          errMsg.includes("422") ||
          errMsg.includes("validation") ||
          errMsg.includes("offline_assignment_required") ||
          errMsg.includes("offline_command_type_invalid")
        ) {
          // Permanently rejected domain error - never auto-retry
          cmd.status = "rejected";
          cmd.errorMessage = errMsg;
          failureCount += 1;
        } else {
          // Network or transient server failure
          cmd.status = "failed";
          cmd.errorMessage = errMsg;
          failureCount += 1;
        }
      }
      updatedQueue.push(cmd);
    }

    saveOfflineQueue(scope, updatedQueue);
    return { successCount, failureCount, conflictCount };
  } finally {
    activeReplayLocks.delete(key);
  }
}

export function retryOfflineCommand(scope: QueueScope, clientCommandId: string): boolean {
  if (!isQueueScopeValid(scope)) return false;
  const queue = loadOfflineQueue(scope);
  const target = queue.find((c) => c.clientCommandId === clientCommandId);
  if (!target || target.status === "synced") return false;

  if (target.status === "rejected") {
    throw new Error(
      "UNSAFE RETRY: Permanently rejected business commands cannot be retried without fixing domain validation or permissions."
    );
  }

  if (target.status === "conflict") {
    throw new Error(
      "UNSAFE RETRY: Version conflict commands must be explicitly resolved or discarded, not blindly retried."
    );
  }

  target.status = "queued";
  target.attempts = 0;
  target.errorMessage = undefined;
  saveOfflineQueue(scope, queue);
  return true;
}

export function resolveConflict(
  scope: QueueScope,
  clientCommandId: string,
  resolution: "discard" | "overrideVersion",
  newVersion?: number
): void {
  if (!isQueueScopeValid(scope)) return;
  const queue = loadOfflineQueue(scope);
  if (resolution === "discard") {
    const remaining = queue.filter((c) => c.clientCommandId !== clientCommandId);
    saveOfflineQueue(scope, remaining);
    return;
  }

  const target = queue.find((c) => c.clientCommandId === clientCommandId);
  if (target) {
    if (typeof newVersion === "number") {
      target.expectedVersion = newVersion;
    }
    target.status = "queued";
    target.attempts = 0;
    target.conflictCode = undefined;
    target.errorMessage = undefined;
    saveOfflineQueue(scope, queue);
  }
}

export function clearSyncedCommands(scope: QueueScope): void {
  if (!isQueueScopeValid(scope)) return;
  const queue = loadOfflineQueue(scope);
  const remaining = queue.filter((c) => c.status !== "synced");
  saveOfflineQueue(scope, remaining);
}

export function dismissOfflineCommand(scope: QueueScope, clientCommandId: string): void {
  if (!isQueueScopeValid(scope)) return;
  const queue = loadOfflineQueue(scope);
  const remaining = queue.filter((c) => c.clientCommandId !== clientCommandId);
  saveOfflineQueue(scope, remaining);
}
