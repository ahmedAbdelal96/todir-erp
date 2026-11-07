// POS Module 20 Offline Queue & Resilience Hook

import { useState, useEffect, useCallback } from "react";
import { posApi } from "../services/pos-api.ts";
import type { OfflineEnvelopeRequest, OfflinePackageResponse } from "../types.ts";

const STORAGE_KEY_OFFLINE_QUEUE = "todir_pos_offline_queue_v1";
const STORAGE_KEY_OFFLINE_PACKAGE = "todir_pos_offline_package_v1";

export interface StoredOfflineEnvelope extends OfflineEnvelopeRequest {
  readonly id: string;
  readonly createdAt: string;
  readonly amount: number;
  readonly status: string;
  readonly retryCount: number;
}

export function usePosOffline(registerId?: string, deviceIdentity = "pos-client-terminal-1") {
  const [isOnline, setIsOnline] = useState<boolean>(typeof navigator !== "undefined" ? navigator.onLine : true);
  const [offlineQueue, setOfflineQueue] = useState<readonly StoredOfflineEnvelope[]>([]);
  const [offlinePackage, setOfflinePackage] = useState<OfflinePackageResponse | null>(null);
  const [isSyncing, setIsSyncing] = useState<boolean>(false);
  const [lastSyncResult, setLastSyncResult] = useState<string | null>(null);

  // Load queued envelopes from localStorage
  const loadQueue = useCallback(() => {
    try {
      const data = localStorage.getItem(STORAGE_KEY_OFFLINE_QUEUE);
      if (data) {
        setOfflineQueue(JSON.parse(data));
      } else {
        setOfflineQueue([]);
      }
    } catch {
      setOfflineQueue([]);
    }
  }, []);

  // Save queued envelopes
  const saveQueue = useCallback((queue: readonly StoredOfflineEnvelope[]) => {
    try {
      localStorage.setItem(STORAGE_KEY_OFFLINE_QUEUE, JSON.stringify(queue));
      setOfflineQueue(queue);
    } catch {
      // storage quota
    }
  }, []);

  // Monitor network online/offline events
  useEffect(() => {
    const handleOnline = () => setIsOnline(true);
    const handleOffline = () => setIsOnline(false);

    window.addEventListener("online", handleOnline);
    window.addEventListener("offline", handleOffline);
    loadQueue();

    return () => {
      window.removeEventListener("online", handleOnline);
      window.removeEventListener("offline", handleOffline);
    };
  }, [loadQueue]);

  // Download and cache offline catalog package if available
  const refreshOfflinePackage = useCallback(async () => {
    if (!registerId) return null;
    try {
      const pkg = await posApi.downloadOfflinePackage(registerId, deviceIdentity);
      localStorage.setItem(STORAGE_KEY_OFFLINE_PACKAGE, JSON.stringify(pkg));
      setOfflinePackage(pkg);
      return pkg;
    } catch {
      return null;
    }
  }, [registerId, deviceIdentity]);

  // Queue an offline cash sale envelope
  const queueOfflineSale = useCallback(
    (envelope: OfflineEnvelopeRequest, totalAmount: number): StoredOfflineEnvelope => {
      const storedItem: StoredOfflineEnvelope = {
        ...envelope,
        id: crypto.randomUUID ? crypto.randomUUID() : `${Date.now()}`,
        createdAt: new Date().toISOString(),
        amount: totalAmount,
        status: "Pending",
        retryCount: 0,
      };

      const updated = [...offlineQueue, storedItem];
      saveQueue(updated);
      return storedItem;
    },
    [offlineQueue, saveQueue]
  );

  const enqueueOfflineSale = useCallback(
    async (params: {
      registerId: string;
      deviceIdentity: string;
      sessionId: string;
      currencyCode: string;
      paymentAccountId: string;
      customerPartyId?: string | undefined;
      lines: readonly any[];
    }): Promise<StoredOfflineEnvelope> => {
      const totalAmount = params.lines.reduce((sum, l) => sum + (l.unitPrice * l.quantity), 0);
      const req: OfflineEnvelopeRequest = {
        registerId: params.registerId,
        deviceIdentity: params.deviceIdentity,
        sourceSequence: Date.now(),
        idempotencyKey: crypto.randomUUID ? crypto.randomUUID() : `${Date.now()}`,
        packageVersion: 1,
        payload: JSON.stringify(params),
        signature: "offline_signature_dummy",
      };
      return queueOfflineSale(req, totalAmount);
    },
    [queueOfflineSale]
  );

  // Sync all queued envelopes to the backend
  const syncOfflineQueue = useCallback(async (): Promise<number> => {
    if (offlineQueue.length === 0 || isSyncing) return 0;
    setIsSyncing(true);
    setLastSyncResult(null);

    let syncedCount = 0;
    const remaining: StoredOfflineEnvelope[] = [];

    for (const item of offlineQueue) {
      try {
        await posApi.syncOfflineEnvelope({
          registerId: item.registerId,
          deviceIdentity: item.deviceIdentity,
          sourceSequence: item.sourceSequence,
          idempotencyKey: item.idempotencyKey,
          packageVersion: item.packageVersion,
          payload: item.payload,
          signature: item.signature,
        });
        syncedCount++;
      } catch {
        // If conflict or network error, keep in remaining queue with incremented retry
        remaining.push({
          ...item,
          retryCount: item.retryCount + 1,
          status: "Pending",
        });
      }
    }

    saveQueue(remaining);
    setIsSyncing(false);
    setLastSyncResult(`تم مزامنة ${syncedCount} معاملة، ومتبقي ${remaining.length}`);
    return syncedCount;
  }, [offlineQueue, isSyncing, saveQueue]);

  const clearQueue = useCallback(() => {
    saveQueue([]);
  }, [saveQueue]);

  return {
    isOnline,
    setIsOnline,
    offlineQueue,
    offlinePackage,
    isSyncing,
    lastSyncResult,
    queueOfflineSale,
    enqueueOfflineSale,
    syncOfflineQueue,
    syncPendingEnvelopes: syncOfflineQueue,
    clearQueue,
    refreshOfflinePackage,
    pendingSyncCount: offlineQueue.length,
    envelopes: offlineQueue,
  };
}
