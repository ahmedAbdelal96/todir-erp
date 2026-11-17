import type { OfflineCommandRequest, OfflineCommandView } from "../types.ts";

export type StoredCommandStatus = "queued" | "syncing" | "synced" | "conflict" | "failed";

export interface StoredOfflineCommand {
  clientCommandId: string;
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

function getStorageKey(legalEntityId: string): string {
  return `todir_service_offline_queue_${legalEntityId}`;
}

export function loadOfflineQueue(legalEntityId: string): StoredOfflineCommand[] {
  if (typeof window === "undefined" || !window.localStorage) return [];
  try {
    const raw = window.localStorage.getItem(getStorageKey(legalEntityId));
    if (!raw) return [];
    return JSON.parse(raw) as StoredOfflineCommand[];
  } catch {
    return [];
  }
}

export function saveOfflineQueue(legalEntityId: string, queue: readonly StoredOfflineCommand[]): void {
  if (typeof window === "undefined" || !window.localStorage) return;
  try {
    window.localStorage.setItem(getStorageKey(legalEntityId), JSON.stringify(queue));
  } catch {
    // Quota exceeded or private browsing restricted
  }
}

export function enqueueOfflineCommand(
  legalEntityId: string,
  command: {
    clientCommandId?: string | undefined;
    commandType: string;
    visitId?: string | undefined;
    workOrderId?: string | undefined;
    expectedVersion?: number | undefined;
    payloadJson?: string | undefined;
  }
): StoredOfflineCommand {
  const current = loadOfflineQueue(legalEntityId);
  const newEntry: StoredOfflineCommand = {
    clientCommandId: command.clientCommandId || (typeof crypto !== "undefined" ? crypto.randomUUID() : `cmd-${Date.now()}`),
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
  saveOfflineQueue(legalEntityId, updated);
  return newEntry;
}

export async function replayOfflineQueue(
  legalEntityId: string,
  submitFn: (req: OfflineCommandRequest) => Promise<OfflineCommandView>
): Promise<{ successCount: number; failureCount: number; conflictCount: number }> {
  const queue = loadOfflineQueue(legalEntityId);
  if (queue.length === 0) {
    return { successCount: 0, failureCount: 0, conflictCount: 0 };
  }

  let successCount = 0;
  let failureCount = 0;
  let conflictCount = 0;

  const updatedQueue: StoredOfflineCommand[] = [];

  for (const cmd of queue) {
    // Only replay queued or retryable failed commands
    if (cmd.status === "synced") {
      // Keep or skip synced commands
      updatedQueue.push(cmd);
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
        cmd.errorMessage = `Conflict: ${result.conflictCode || "VersionConflict"}`;
        conflictCount += 1;
        updatedQueue.push(cmd);
      } else {
        cmd.status = "synced";
        cmd.errorMessage = undefined;
        successCount += 1;
        updatedQueue.push(cmd);
      }
    } catch (err: any) {
      const errMsg = err?.message || String(err);
      if (errMsg.includes("409") || errMsg.includes("conflict")) {
        cmd.status = "conflict";
        cmd.conflictCode = "VersionConflict";
        cmd.errorMessage = errMsg;
        conflictCount += 1;
      } else {
        cmd.status = "failed";
        cmd.errorMessage = errMsg;
        failureCount += 1;
      }
      updatedQueue.push(cmd);
    }
  }

  saveOfflineQueue(legalEntityId, updatedQueue);
  return { successCount, failureCount, conflictCount };
}

export function clearSyncedCommands(legalEntityId: string): void {
  const queue = loadOfflineQueue(legalEntityId);
  const remaining = queue.filter((c) => c.status !== "synced");
  saveOfflineQueue(legalEntityId, remaining);
}

export function dismissOfflineCommand(legalEntityId: string, clientCommandId: string): void {
  const queue = loadOfflineQueue(legalEntityId);
  const remaining = queue.filter((c) => c.clientCommandId !== clientCommandId);
  saveOfflineQueue(legalEntityId, remaining);
}
