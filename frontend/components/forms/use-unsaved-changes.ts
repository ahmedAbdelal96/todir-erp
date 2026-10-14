import { useEffect, useCallback } from "react";
import { useTranslation } from "react-i18next";

export interface UseUnsavedChangesOptions {
  readonly isDirty: boolean;
  readonly message?: string | undefined;
}

export function useUnsavedChanges({ isDirty, message }: UseUnsavedChangesOptions) {
  const { t } = useTranslation("common");
  const confirmationMessage = message ?? t("components.unsavedChanges");
  useEffect(() => {
    if (!isDirty) return;

    const handleBeforeUnload = (e: BeforeUnloadEvent) => {
      e.preventDefault();
      e.returnValue = confirmationMessage;
      return confirmationMessage;
    };

    window.addEventListener("beforeunload", handleBeforeUnload);
    return () => window.removeEventListener("beforeunload", handleBeforeUnload);
  }, [isDirty, confirmationMessage]);

  const confirmNavigation = useCallback(
    (onProceed: () => void, customMessage?: string) => {
      if (!isDirty) {
        onProceed();
        return;
      }

      const confirmed = window.confirm(customMessage ?? confirmationMessage);
      if (confirmed) {
        onProceed();
      }
    },
    [isDirty, confirmationMessage]
  );

  return { confirmNavigation };
}
