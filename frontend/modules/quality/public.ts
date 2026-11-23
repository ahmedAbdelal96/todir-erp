/**
 * Module 27 Quality Management â€” Public Module Boundary
 */

export { qualityRoutes } from "./routes.tsx";
export { qualityAr } from "./i18n/ar.ts";
export { qualityEn } from "./i18n/en.ts";

export { QualityHeader } from "./components/quality-header.tsx";
export { QualityStatusBadge } from "./components/quality-status-badge.tsx";
export { QualityKpiRibbon } from "./components/quality-kpi-ribbon.tsx";

export { qualityApi } from "./services/quality-api.ts";
export * from "./hooks/use-quality-queries.ts";
export * from "./types.ts";
