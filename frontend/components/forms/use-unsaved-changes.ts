import { useEffect, useCallback } from "react";

export interface UseUnsavedChangesOptions {
  readonly isDirty: boolean;
  readonly message?: string | undefined;
}

export function useUnsavedChanges({
  isDirty,
  message = "توجد تعديلات غير محفوظة. هل أنت متأكد من رغبتك في مغادرة الصفحة؟"
}: UseUnsavedChangesOptions) {
  useEffect(() => {
    if (!isDirty) return;

    const handleBeforeUnload = (e: BeforeUnloadEvent) => {
      e.preventDefault();
      e.returnValue = message;
      return message;
    };

    window.addEventListener("beforeunload", handleBeforeUnload);
    return () => window.removeEventListener("beforeunload", handleBeforeUnload);
  }, [isDirty, message]);

  const confirmNavigation = useCallback(
    (onProceed: () => void, customMessage?: string) => {
      if (!isDirty) {
        onProceed();
        return;
      }

      const confirmed = window.confirm(customMessage ?? message);
      if (confirmed) {
        onProceed();
      }
    },
    [isDirty, message]
  );

  return { confirmNavigation };
}
