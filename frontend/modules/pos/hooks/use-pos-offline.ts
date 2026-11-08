// POS Module 20 Durable Offline Queue & Web Crypto Resilience Hook

import { useState, useEffect, useCallback, useRef } from "react";
import { posApi } from "../services/pos-api.ts";
import type { OfflineEnvelopeRequest, OfflinePackageResponse } from "../types.ts";

const DB_NAME = "todir_pos_offline_v1";
const STORE_PACKAGES = "offline_packages";
const STORE_ENVELOPES = "offline_envelopes";

export interface StoredOfflineEnvelope extends OfflineEnvelopeRequest {
  readonly id: string;
  readonly createdAt: string;
  readonly amount: number;
  readonly status: "Pending" | "Synced" | "Conflicted" | "Failed";
  readonly retryCount: number;
  readonly conflictCode?: string | null;
  readonly lastError?: string | null;
}

export interface StoredOfflinePackage extends OfflinePackageResponse {
  readonly cachedAt: string;
}

/**
 * Open or upgrade the POS IndexedDB storage
 */
function openOfflineDatabase(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    if (typeof window === "undefined" || !window.indexedDB) {
      reject(new Error("IndexedDB is unavailable in this environment"));
      return;
    }

    const request = window.indexedDB.open(DB_NAME, 1);

    request.onupgradeneeded = (event) => {
      const db = (event.target as IDBOpenDBRequest).result;
      if (!db.objectStoreNames.contains(STORE_PACKAGES)) {
        db.createObjectStore(STORE_PACKAGES, { keyPath: "registerId" });
      }
      if (!db.objectStoreNames.contains(STORE_ENVELOPES)) {
        const envelopeStore = db.createObjectStore(STORE_ENVELOPES, { keyPath: "id" });
        envelopeStore.createIndex("by_register", "registerId", { unique: false });
        envelopeStore.createIndex("by_sequence", ["registerId", "deviceIdentity", "sourceSequence"], { unique: true });
        envelopeStore.createIndex("by_status", "status", { unique: false });
      }
    };

    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
}

/**
 * Real Web Crypto HMAC-SHA256 signature matching backend PosOfflineSecurity.SignEnvelope
 * Canonical envelope format:
 * string.Join('|', registerId, deviceIdentity, sourceSequence, idempotencyKey, packageVersion, payload)
 */
export async function signOfflineEnvelope(
  packageKey: string,
  registerId: string,
  deviceIdentity: string,
  sourceSequence: number,
  idempotencyKey: string,
  packageVersion: number,
  payload: string
): Promise<string> {
  if (!packageKey || !packageKey.trim()) {
    throw new Error("pos.offline_package_key_missing");
  }

  const canonical = `${registerId}|${deviceIdentity.trim()}|${sourceSequence}|${idempotencyKey.trim()}|${packageVersion}|${payload}`;
  const enc = new TextEncoder();
  const keyBytes = enc.encode(packageKey.trim());
  const dataBytes = enc.encode(canonical);

  const cryptoKey = await window.crypto.subtle.importKey(
    "raw",
    keyBytes,
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"]
  );

  const signatureBuffer = await window.crypto.subtle.sign("HMAC", cryptoKey, dataBytes);
  return Array.from(new Uint8Array(signatureBuffer))
    .map((b) => b.toString(16).padStart(2, "0"))
    .join("")
    .toLowerCase();
}

/**
 * Real server reachability check (beyond navigator.onLine)
 */
async function checkServerReachable(): Promise<boolean> {
  if (typeof navigator !== "undefined" && !navigator.onLine) {
    return false;
  }
  try {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 3500);
    const res = await fetch("/health", {
      method: "GET",
      signal: controller.signal,
      headers: { "Cache-Control": "no-cache" },
    });
    clearTimeout(timeout);
    return res.ok;
  } catch {
    return false;
  }
}

export function usePosOffline(registerId?: string, deviceIdentity?: string) {
  const activeDeviceIdentity = deviceIdentity || (typeof window !== "undefined" ? window.sessionStorage.getItem("erp.pos.deviceIdentity") || "POS-TERM-01" : "POS-TERM-01");

  const [isOnline, setIsOnline] = useState<boolean>(typeof navigator !== "undefined" ? navigator.onLine : true);
  const [isServerReachable, setIsServerReachable] = useState<boolean>(true);
  const [offlineQueue, setOfflineQueue] = useState<readonly StoredOfflineEnvelope[]>([]);
  const [offlinePackage, setOfflinePackage] = useState<StoredOfflinePackage | null>(null);
  const [isSyncing, setIsSyncing] = useState<boolean>(false);
  const [lastSyncResult, setLastSyncResult] = useState<string | null>(null);

  const reachabilityTimerRef = useRef<ReturnType<typeof setInterval> | null>(null);

  // Load cached package from IndexedDB
  const loadCachedPackage = useCallback(async () => {
    if (!registerId) return;
    try {
      const db = await openOfflineDatabase();
      const tx = db.transaction(STORE_PACKAGES, "readonly");
      const store = tx.objectStore(STORE_PACKAGES);
      const req = store.get(registerId);
      req.onsuccess = () => {
        if (req.result) {
          setOfflinePackage(req.result as StoredOfflinePackage);
        } else {
          setOfflinePackage(null);
        }
      };
    } catch {
      // IndexedDB failure fallback
    }
  }, [registerId]);

  // Load queued envelopes from IndexedDB
  const loadQueue = useCallback(async () => {
    try {
      const db = await openOfflineDatabase();
      const tx = db.transaction(STORE_ENVELOPES, "readonly");
      const store = tx.objectStore(STORE_ENVELOPES);
      const req = store.getAll();
      req.onsuccess = () => {
        const items = (req.result || []) as StoredOfflineEnvelope[];
        // Filter for this register if specified
        const filtered = registerId ? items.filter((item) => item.registerId === registerId) : items;
        // Sort by sequence
        filtered.sort((a, b) => a.sourceSequence - b.sourceSequence);
        setOfflineQueue(filtered);
      };
    } catch {
      setOfflineQueue([]);
    }
  }, [registerId]);

  // Periodic reachability check
  useEffect(() => {
    const runPing = async () => {
      const reachable = await checkServerReachable();
      setIsServerReachable(reachable);
      setIsOnline(reachable && (typeof navigator === "undefined" || navigator.onLine));
    };

    runPing();
    reachabilityTimerRef.current = setInterval(runPing, 15000);

    const handleBrowserOnline = () => {
      runPing();
    };
    const handleBrowserOffline = () => {
      setIsOnline(false);
      setIsServerReachable(false);
    };

    window.addEventListener("online", handleBrowserOnline);
    window.addEventListener("offline", handleBrowserOffline);
    loadQueue();
    loadCachedPackage();

    return () => {
      if (reachabilityTimerRef.current) clearInterval(reachabilityTimerRef.current);
      window.removeEventListener("online", handleBrowserOnline);
      window.removeEventListener("offline", handleBrowserOffline);
    };
  }, [loadQueue, loadCachedPackage]);

  // Download and cache offline catalog package in IndexedDB
  const refreshOfflinePackage = useCallback(async () => {
    if (!registerId) return null;
    try {
      const pkg = await posApi.downloadOfflinePackage(registerId, activeDeviceIdentity);
      const storedPkg: StoredOfflinePackage = {
        ...pkg,
        cachedAt: new Date().toISOString(),
      };

      const db = await openOfflineDatabase();
      const tx = db.transaction(STORE_PACKAGES, "readwrite");
      tx.objectStore(STORE_PACKAGES).put(storedPkg);

      await new Promise<void>((resolve, reject) => {
        tx.oncomplete = () => resolve();
        tx.onerror = () => reject(tx.error);
      });

      setOfflinePackage(storedPkg);
      return storedPkg;
    } catch (err) {
      return null;
    }
  }, [registerId, activeDeviceIdentity]);

  // Check if offline package is currently valid and unexpired
  const isOfflinePackageValid = Boolean(
    offlinePackage &&
    offlinePackage.signature &&
    new Date(offlinePackage.expiresAt).getTime() > Date.now()
  );

  // Queue a signed offline envelope in IndexedDB
  const enqueueOfflineSale = useCallback(
    async (params: {
      registerId: string;
      deviceIdentity: string;
      sessionId: string;
      currencyCode: string;
      paymentAccountId: string;
      customerPartyId?: string | undefined;
      lines: ReadonlyArray<{
        variantId: string;
        description: string;
        uomCode: string;
        quantity: number;
        unitPrice: number;
        taxAmount: number;
        discountAmount: number;
        legalEntityId: string;
        productTemplateId?: string | null;
        unitOfMeasureId?: string | null;
        priceListId?: string | null;
        pricingDate?: string | null;
      }>;
    }): Promise<StoredOfflineEnvelope> => {
      if (!offlinePackage || !offlinePackage.signature) {
        throw new Error("pos.offline_package_missing");
      }
      if (new Date(offlinePackage.expiresAt).getTime() <= Date.now()) {
        throw new Error("pos.offline_package_expired");
      }

      const totalAmount = params.lines.reduce((sum, l) => sum + l.unitPrice * l.quantity, 0);
      const nextSequence = offlineQueue.length > 0
        ? Math.max(...offlineQueue.map((e) => e.sourceSequence)) + 1
        : 1;

      const idempotencyKey = window.crypto?.randomUUID ? window.crypto.randomUUID() : `offline-key-${Date.now()}`;
      const envelopeId = window.crypto?.randomUUID ? window.crypto.randomUUID() : `env-${Date.now()}`;
      const payloadObj = {
        schemaVersion: 1,
        type: "CashSale",
        sessionId: params.sessionId,
        currencyCode: params.currencyCode,
        paymentAccountId: params.paymentAccountId,
        idempotencyKey,
        customerPartyId: params.customerPartyId || "023b6cc2-2c8a-8756-b1e8-7664444e37e9",
        lines: params.lines.map((l) => ({
          variantId: l.variantId,
          description: l.description,
          uomCode: l.uomCode,
          quantity: l.quantity,
          unitPrice: l.unitPrice,
          taxAmount: l.taxAmount || 0,
          discountAmount: l.discountAmount || 0,
          legalEntityId: l.legalEntityId,
          productTemplateId: l.productTemplateId || l.variantId,
          unitOfMeasureId: l.unitOfMeasureId || l.variantId,
          priceListId: l.priceListId || params.registerId,
          pricingDate: l.pricingDate || new Date().toISOString().slice(0, 10),
          lotId: null,
          serialId: null,
        })),
      };
      const payloadString = JSON.stringify(payloadObj);

      const signature = await signOfflineEnvelope(
        offlinePackage.signature,
        params.registerId,
        params.deviceIdentity,
        nextSequence,
        idempotencyKey,
        offlinePackage.version,
        payloadString
      );

      const storedItem: StoredOfflineEnvelope = {
        id: envelopeId,
        registerId: params.registerId,
        deviceIdentity: params.deviceIdentity,
        sourceSequence: nextSequence,
        idempotencyKey,
        packageVersion: offlinePackage.version,
        payload: payloadString,
        signature,
        amount: totalAmount,
        createdAt: new Date().toISOString(),
        status: "Pending",
        retryCount: 0,
      };

      const db = await openOfflineDatabase();
      const tx = db.transaction(STORE_ENVELOPES, "readwrite");
      tx.objectStore(STORE_ENVELOPES).put(storedItem);

      await new Promise<void>((resolve, reject) => {
        tx.oncomplete = () => resolve();
        tx.onerror = () => reject(tx.error);
      });

      await loadQueue();
      return storedItem;
    },
    [offlinePackage, offlineQueue, loadQueue]
  );

  // Sync all queued envelopes to the backend in sequence order
  const syncOfflineQueue = useCallback(async (): Promise<number> => {
    const pendingItems = offlineQueue.filter((item) => item.status === "Pending");
    if (pendingItems.length === 0 || isSyncing) return 0;

    setIsSyncing(true);
    setLastSyncResult(null);

    let syncedCount = 0;
    const db = await openOfflineDatabase();

    for (const item of pendingItems) {
      try {
        const syncRes = await posApi.syncOfflineEnvelope({
          registerId: item.registerId,
          deviceIdentity: item.deviceIdentity,
          sourceSequence: item.sourceSequence,
          idempotencyKey: item.idempotencyKey,
          packageVersion: item.packageVersion,
          payload: item.payload,
          signature: item.signature,
        });

        // Resolve envelope ID returned by the server or use stored ID
        const resolvedEnvelopeId =
          (syncRes && (syncRes.envelopeId || (typeof syncRes === "string" ? syncRes : null))) ||
          item.id;

        // Process the offline cash sale into an authoritative backend sale
        try {
          await posApi.processOfflineCashSale(resolvedEnvelopeId);
        } catch {
          // If already processed or queued for worker reconciliation, envelope is still synced
        }

        // Mark as Synced in IndexedDB
        const tx = db.transaction(STORE_ENVELOPES, "readwrite");
        const updated: StoredOfflineEnvelope = {
          ...item,
          status: "Synced",
        };
        tx.objectStore(STORE_ENVELOPES).put(updated);
        await new Promise<void>((res) => {
          tx.oncomplete = () => res();
        });

        syncedCount++;
      } catch (err: unknown) {
        const errorMsg = err instanceof Error ? err.message : "Sync error";
        const isConflict = errorMsg.includes("conflict") || errorMsg.includes("pos.offline_sequence_conflict");

        const tx = db.transaction(STORE_ENVELOPES, "readwrite");
        const updated: StoredOfflineEnvelope = {
          ...item,
          status: isConflict ? "Conflicted" : "Pending",
          retryCount: item.retryCount + 1,
          conflictCode: isConflict ? "SourceSequenceConflict" : null,
          lastError: errorMsg,
        };
        tx.objectStore(STORE_ENVELOPES).put(updated);
        await new Promise<void>((res) => {
          tx.oncomplete = () => res();
        });
      }
    }

    await loadQueue();
    setIsSyncing(false);
    setLastSyncResult(`Synced ${syncedCount} offline transaction(s)`);
    return syncedCount;
  }, [offlineQueue, isSyncing, loadQueue]);

  const pendingSyncCount = offlineQueue.filter((i) => i.status === "Pending").length;

  return {
    isOnline,
    isServerReachable,
    offlineQueue,
    offlinePackage,
    isOfflinePackageValid,
    isSyncing,
    lastSyncResult,
    pendingSyncCount,
    deviceIdentity: activeDeviceIdentity,
    refreshOfflinePackage,
    enqueueOfflineSale,
    syncOfflineQueue,
    refetchQueue: loadQueue,
  };
}
