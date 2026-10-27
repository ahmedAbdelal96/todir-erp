export interface StoredDraft<T> {
  readonly data: T;
  readonly timestamp: number;
}

export interface DraftStorageAdapter {
  saveDraft<T>(key: string, data: T): Promise<void> | void;
  loadDraft<T>(key: string): Promise<StoredDraft<T> | null> | StoredDraft<T> | null;
  clearDraft(key: string): Promise<void> | void;
}

class SessionStorageDraftAdapter implements DraftStorageAdapter {
  private readonly memoryFallback = new Map<string, string>();

  saveDraft<T>(key: string, data: T): void {
    const payload: StoredDraft<T> = {
      data,
      timestamp: Date.now()
    };
    const serialized = JSON.stringify(payload);
    try {
      window.sessionStorage.setItem(`erp.draft.${key}`, serialized);
    } catch {
      this.memoryFallback.set(key, serialized);
    }
  }

  loadDraft<T>(key: string): StoredDraft<T> | null {
    try {
      const raw = window.sessionStorage.getItem(`erp.draft.${key}`);
      if (raw) {
        return JSON.parse(raw) as StoredDraft<T>;
      }
    } catch {
      // Fall through to memory
    }

    const fallback = this.memoryFallback.get(key);
    return fallback ? (JSON.parse(fallback) as StoredDraft<T>) : null;
  }

  clearDraft(key: string): void {
    try {
      window.sessionStorage.removeItem(`erp.draft.${key}`);
    } catch {
      // Ignore
    }
    this.memoryFallback.delete(key);
  }
}

/**
 * Global default draft storage adapter.
 * Can be swapped with an HTTP / backend adapter in future releases.
 */
let currentDraftAdapter: DraftStorageAdapter = new SessionStorageDraftAdapter();

export function setDraftStorageAdapter(adapter: DraftStorageAdapter): void {
  currentDraftAdapter = adapter;
}

export function saveFormDraft<T>(key: string, data: T): Promise<void> | void {
  return currentDraftAdapter.saveDraft(key, data);
}

export function loadFormDraft<T>(key: string): Promise<StoredDraft<T> | null> | StoredDraft<T> | null {
  return currentDraftAdapter.loadDraft(key);
}

export function clearFormDraft(key: string): Promise<void> | void {
  return currentDraftAdapter.clearDraft(key);
}
