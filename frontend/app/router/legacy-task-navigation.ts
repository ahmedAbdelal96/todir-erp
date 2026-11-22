import type { ModuleNavigationRegistration, NavDestination } from "./route-contract.ts";

function normalizeTaskToken(value: string): string {
  return value.trim().toLowerCase().replace(/[^a-z0-9]+/g, "");
}

function lastPathToken(path: string): string {
  const tokens = path.split("/").filter(Boolean);
  return tokens[tokens.length - 1] ?? "";
}

/**
 * Resolves the old `?tab=` vocabulary to the registered canonical destination.
 * The resolver is intentionally based on the destination registry; it never
 * invents a route and therefore cannot grant access to an unregistered task.
 */
export function resolveLegacyTaskDestination(
  module: ModuleNavigationRegistration,
  tab: string | null | undefined
): NavDestination | null {
  return resolveLegacyTaskDestinationFromDestinations(module.destinations ?? [], module.path, tab);
}

export function resolveLegacyTaskDestinationFromDestinations(
  destinations: readonly NavDestination[],
  modulePath: string,
  tab: string | null | undefined
): NavDestination | null {
  if (!tab) return null;

  const token = normalizeTaskToken(tab);
  if (!token) return null;

  const root = destinations.find(destination => destination.path === modulePath);
  if (root && ["home", "overview", "all", "default", "portfolio"].includes(token)) {
    return root;
  }

  const candidates = destinations.filter(destination => destination.path !== modulePath);
  return candidates.find(destination => {
    const pathToken = normalizeTaskToken(lastPathToken(destination.path));
    const idToken = normalizeTaskToken(destination.id);
    const moduleToken = normalizeTaskToken(destination.moduleId);
    const shortIdToken = idToken.startsWith(moduleToken) ? idToken.slice(moduleToken.length) : idToken;
    return token === pathToken || token === idToken || token === shortIdToken;
  }) ?? null;
}

export function removeLegacyTabQuery(pathname: string, search: string): string {
  const params = new URLSearchParams(search);
  params.delete("tab");
  const query = params.toString();
  return query ? `${pathname}?${query}` : pathname;
}

export function buildCanonicalTaskPath(
  destinations: readonly NavDestination[],
  modulePath: string,
  tab: string,
  search: string
): string | null {
  const destination = resolveLegacyTaskDestinationFromDestinations(destinations, modulePath, tab);
  if (!destination) return null;
  return removeLegacyTabQuery(destination.path, search);
}
