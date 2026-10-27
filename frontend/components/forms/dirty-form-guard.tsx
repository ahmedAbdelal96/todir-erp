import { useEffect, useRef, useState, useCallback } from "react";
import { useTranslation } from "react-i18next";
import {
  saveFormDraft,
  loadFormDraft,
  clearFormDraft,
  type StoredDraft
} from "./draft-storage.ts";

export interface UseDirtyFormGuardOptions {
  readonly isDirty: boolean;
  readonly message?: string | undefined;
}

/**
 * Guards against accidental tab close, page refresh, or navigation when form has unsaved edits.
 */
export function useDirtyFormGuard({ isDirty, message }: UseDirtyFormGuardOptions): void {
  const { t } = useTranslation("common");
  const defaultWarning = t("draftRecovery.unsavedWarning", "توجد تعديلات غير محفوظة. هل أنت متأكد من مغادرة هذه الصفحة؟");
  const warningMessage = message ?? defaultWarning;

  useEffect(() => {
    if (!isDirty) return;

    function handleBeforeUnload(e: BeforeUnloadEvent) {
      e.preventDefault();
      e.returnValue = warningMessage;
      return warningMessage;
    }

    window.addEventListener("beforeunload", handleBeforeUnload);
    return () => window.removeEventListener("beforeunload", handleBeforeUnload);
  }, [isDirty, warningMessage]);
}

export interface UseDraftAutoSaveOptions<T> {
  readonly draftKey: string;
  readonly data: T;
  readonly isDirty: boolean;
  readonly debounceMs?: number | undefined;
}

export interface UseDraftAutoSaveResult<T> {
  readonly existingDraft: StoredDraft<T> | null;
  readonly restoreDraft: () => T | null;
  readonly discardDraft: () => void;
  readonly clearDraft: () => void;
}

/**
 * Automatically persists active form data to client draft storage when modified,
 * allowing instant 1-click recovery if interrupted.
 */
export function useDraftAutoSave<T>({
  draftKey,
  data,
  isDirty,
  debounceMs = 1000
}: UseDraftAutoSaveOptions<T>): UseDraftAutoSaveResult<T> {
  const [existingDraft, setExistingDraft] = useState<StoredDraft<T> | null>(() => {
    const loaded = loadFormDraft<T>(draftKey);
    return loaded instanceof Promise ? null : loaded;
  });

  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Check draft on mount if async adapter is used
  useEffect(() => {
    const loaded = loadFormDraft<T>(draftKey);
    if (loaded instanceof Promise) {
      void loaded.then((result) => setExistingDraft(result));
    }
  }, [draftKey]);

  // Auto-save debounce effect
  useEffect(() => {
    if (!isDirty) return;

    if (timerRef.current) {
      clearTimeout(timerRef.current);
    }

    timerRef.current = setTimeout(() => {
      void saveFormDraft(draftKey, data);
    }, debounceMs);

    return () => {
      if (timerRef.current) {
        clearTimeout(timerRef.current);
      }
    };
  }, [draftKey, data, isDirty, debounceMs]);

  const restoreDraft = useCallback((): T | null => {
    if (!existingDraft) return null;
    const restored = existingDraft.data;
    setExistingDraft(null);
    return restored;
  }, [existingDraft]);

  const discardDraft = useCallback((): void => {
    void clearFormDraft(draftKey);
    setExistingDraft(null);
  }, [draftKey]);

  const clearDraft = useCallback((): void => {
    void clearFormDraft(draftKey);
    setExistingDraft(null);
  }, [draftKey]);

  return {
    existingDraft,
    restoreDraft,
    discardDraft,
    clearDraft
  };
}

export { DraftRecoveryBanner } from "./draft-recovery-banner.tsx";
export {
  saveFormDraft,
  loadFormDraft,
  clearFormDraft,
  setDraftStorageAdapter,
  type DraftStorageAdapter,
  type StoredDraft
} from "./draft-storage.ts";
