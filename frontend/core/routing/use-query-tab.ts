import { useCallback } from "react";
import { useSearchParams } from "react-router-dom";

interface UseQueryTabOptions<T extends string> {
  readonly defaultTab: T;
  readonly allowedTabs: readonly T[];
  readonly parameter?: string;
}

/**
 * Keeps a page-level tab addressable and restorable without polluting browser history.
 * Invalid or missing query values intentionally fall back to the page default.
 */
export function useQueryTab<T extends string>({
  defaultTab,
  allowedTabs,
  parameter = "tab"
}: UseQueryTabOptions<T>): readonly [T, (tab: T) => void] {
  const [searchParams, setSearchParams] = useSearchParams();
  const candidate = searchParams.get(parameter) as T | null;
  const activeTab = candidate && allowedTabs.includes(candidate) ? candidate : defaultTab;

  const setActiveTab = useCallback(
    (tab: T) => {
      setSearchParams((current) => {
        const next = new URLSearchParams(current);
        next.set(parameter, tab);
        return next;
      }, { replace: true });
    },
    [parameter, setSearchParams]
  );

  return [activeTab, setActiveTab] as const;
}
