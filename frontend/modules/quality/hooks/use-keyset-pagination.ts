import { useState, useCallback } from "react";

export interface KeysetPaginationState {
  cursor: string | null;
  cursorStack: string[];
  hasPrev: boolean;
  goToNext: (nextCursor: string | null) => void;
  goToPrev: () => void;
  reset: () => void;
}

export function useKeysetPagination(): KeysetPaginationState {
  const [cursor, setCursor] = useState<string | null>(null);
  const [cursorStack, setCursorStack] = useState<string[]>([]);

  const goToNext = useCallback((nextCursor: string | null) => {
    if (!nextCursor) return;
    setCursorStack((prev) => [...prev, cursor ?? ""]);
    setCursor(nextCursor);
  }, [cursor]);

  const goToPrev = useCallback(() => {
    setCursorStack((prev) => {
      if (prev.length === 0) return prev;
      const nextStack = [...prev];
      const previousCursor = nextStack.pop();
      setCursor(previousCursor === "" ? null : (previousCursor ?? null));
      return nextStack;
    });
  }, []);

  const reset = useCallback(() => {
    setCursor(null);
    setCursorStack([]);
  }, []);

  return {
    cursor,
    cursorStack,
    hasPrev: cursorStack.length > 0,
    goToNext,
    goToPrev,
    reset
  };
}
