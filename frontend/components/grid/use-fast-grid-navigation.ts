import { useEffect, useCallback, type RefObject } from "react";

export interface FastGridNavigationOptions {
  readonly containerRef: RefObject<HTMLElement | null>;
  readonly onAddRow?: (() => void) | undefined;
  readonly onSave?: (() => void) | undefined;
  readonly isEnabled?: boolean | undefined;
}

/**
 * Enterprise Numpad & Keyboard-first Ergonomics:
 * - Enter: Advance focus to next cell/input in the line.
 * - Enter on last cell of the last line: Triggers onAddRow() and moves focus to the first cell of the new line.
 * - Ctrl + S / Cmd + S: Triggers document save (prevents browser default HTML download).
 * - Alt + N: Quickly adds a new blank line.
 */
export function useFastGridNavigation({
  containerRef,
  onAddRow,
  onSave,
  isEnabled = true
}: FastGridNavigationOptions): void {
  const getEditableInputs = useCallback((): HTMLElement[] => {
    if (!containerRef.current) return [];
    const selector = 'input:not([disabled]):not([readonly]):not([type="hidden"]), select:not([disabled]):not([readonly]), textarea:not([disabled]):not([readonly]), button[data-grid-action]:not([disabled])';
    return Array.from(containerRef.current.querySelectorAll<HTMLElement>(selector)).filter(
      (el) => el.offsetParent !== null
    );
  }, [containerRef]);

  const handleKeyDown = useCallback(
    (e: KeyboardEvent) => {
      if (!isEnabled) return;

      // 1. Quick Save (Ctrl+S / Cmd+S)
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "s") {
        if (onSave) {
          e.preventDefault();
          e.stopPropagation();
          onSave();
          return;
        }
      }

      // 2. Add Line Shortcut (Alt+N)
      if (e.altKey && e.key.toLowerCase() === "n") {
        if (onAddRow) {
          e.preventDefault();
          e.stopPropagation();
          onAddRow();
          return;
        }
      }

      // 3. Fast Enter progression through cells
      if (e.key === "Enter" && !e.shiftKey) {
        const activeEl = document.activeElement as HTMLElement | null;
        if (!activeEl || !containerRef.current?.contains(activeEl)) return;

        // Skip Enter in multiline textareas
        if (activeEl.tagName.toLowerCase() === "textarea") return;

        const inputs = getEditableInputs();
        const currentIndex = inputs.indexOf(activeEl);

        if (currentIndex !== -1) {
          e.preventDefault();
          if (currentIndex < inputs.length - 1) {
            // Jump to next cell
            inputs[currentIndex + 1]?.focus();
          } else {
            // Last cell of last row: auto-create new line if callback provided
            if (onAddRow) {
              onAddRow();
              // Focus new row after DOM renders
              setTimeout(() => {
                const refreshedInputs = getEditableInputs();
                const nextTarget = refreshedInputs[currentIndex + 1] ?? refreshedInputs[refreshedInputs.length - 1];
                nextTarget?.focus();
              }, 60);
            }
          }
        }
      }
    },
    [isEnabled, onSave, onAddRow, containerRef, getEditableInputs]
  );

  useEffect(() => {
    const root = containerRef.current;
    if (!root || !isEnabled) return;

    root.addEventListener("keydown", handleKeyDown);
    return () => {
      root.removeEventListener("keydown", handleKeyDown);
    };
  }, [containerRef, isEnabled, handleKeyDown]);
}
