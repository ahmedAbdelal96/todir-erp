import { lazy, type ComponentType, type LazyExoticComponent } from "react";

type ComponentModule = Record<string, unknown>;

/**
 * Keeps route registration synchronous while deferring page code until the route is visited.
 * The runtime check turns a broken export into the normal route error boundary instead of a
 * silent blank screen.
 */
export function lazyNamed<T extends ComponentModule>(
  loader: () => Promise<T>,
  exportName: keyof T & string
): LazyExoticComponent<ComponentType<Record<string, unknown>>> {
  return lazy(async () => {
    const module = await loader();
    const component = module[exportName];
    if (typeof component !== "function") {
      throw new Error(`Route component export "${exportName}" is not callable.`);
    }
    return { default: component as ComponentType<Record<string, unknown>> };
  });
}
