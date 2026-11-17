import type { OfflineCommandRequest, OfflineCommandView } from "../types.ts";

export type StoredCommandStatus = "queued" | "syncing" | "synced" | "conflict" | "rejected" | "failed";

export interface QueueScope {
  readonly tenantId?: string | undefined;
  readonly userId?: string | undefined;
  readonly legalEntityId: string;
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

export function getStorageKey(scope: QueueScope | string): string {
  if (typeof scope === "string") {
    return `todir_service_offline_queue_default_default_${scope.trim()}`;
  }
  const t = scope.tenantId?.trim() || "default";
  const u = scope.userId?.trim() || "default";
  const e = scope.legalEntityId?.trim() || "default";
  return `todir_service_offline_queue_${t}_${u}_${e}`;
}

const activeReplayLocks = new Set<string>();

export function loadOfflineQueue(scope: QueueScope | string): StoredOfflineCommand[] {
  if (typeof window === "undefined" || !window.localStorage) return [];
  try {
    const raw = window.localStorage.getItem(getStorageKey(scope));
    if (!raw) return [];
    return JSON.parse(raw) as StoredOfflineCommand[];
  } catch {
    return [];
  }
}

export function saveOfflineQueue(scope: QueueScope | string, queue: readonly StoredOfflineCommand[]): void {
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
  scope: QueueScope | string,
  command: {
    readonly clientCommandId?: string | undefined;
    readonly commandType: string;
    readonly visitId?: string | undefined;
    readonly workOrderId?: string | undefined;
    readonly expectedVersion?: number | undefined;
    readonly payloadJson?: string | undefined;
  }
): StoredOfflineCommand {
  const current = loadOfflineQueue(scope);
  const stableId = command.clientCommandId || (typeof crypto !== "undefined" && crypto.randomUUID ? crypto.randomUUID() : `cmd-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`);

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
  scope: QueueScope | string,
  submitFn: (req: OfflineCommandRequest) => Promise<OfflineCommandView>,
  options: { readonly maxAttempts?: number } = {}
): Promise<{ successCount: number; failureCount: number; conflictCount: number; locked?: boolean }> {
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
      // Never auto-retry conflicts or permanently rejected commands
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
        } else if (result.state === "Accepted" || result.state === "Processed") {
          cmd.status = "synced";
          cmd.errorMessage = undefined;
          cmd.conflictCode = undefined;
          successCount += 1;
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
        } else if (errMsg.includes("400") || errMsg.includes("422") || errMsg.includes("validation")) {
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

export function retryOfflineCommand(scope: QueueScope | string, clientCommandId: string): boolean {
  const queue = loadOfflineQueue(scope);
  const target = queue.find((c) => c.clientCommandId === clientCommandId);
  if (!target || target.status === "synced") return false;

  target.status = "queued";
  target.attempts = 0;
  target.errorMessage = undefined;
  saveOfflineQueue(scope, queue);
  return true;
}

export function resolveConflict(
  scope: QueueScope | string,
  clientCommandId: string,
  resolution: "discard" | "overrideVersion",
  newVersion?: number
): void {
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

export function clearSyncedCommands(scope: QueueScope | string): void {
  const queue = loadOfflineQueue(scope);
  const remaining = queue.filter((c) => c.status !== "synced");
  saveOfflineQueue(scope, remaining);
}

export function dismissOfflineCommand(scope: QueueScope | string, clientCommandId: string): void {
  const queue = loadOfflineQueue(scope);
  const remaining = queue.filter((c) => c.clientCommandId !== clientCommandId);
  saveOfflineQueue(scope, remaining);
}
