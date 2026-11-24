import React from "react";
import type { ModuleRouteRegistration, PermissionCondition } from "../../app/router/route-contract.ts";
import { lazyNamed } from "../../app/router/lazy-route-component.ts";
import { PermissionGuard } from "../../core/runtime/permission-guard.tsx";
import { ModuleRootRoute } from "../../app/router/module-root-route.tsx";
import { QUALITY_DESTINATIONS } from "../../app/router/destination-registry.ts";

const QualityOverviewPage = lazyNamed(() => import("./pages/quality-overview-page.tsx"), "QualityOverviewPage");
const QualityInspectionsPage = lazyNamed(() => import("./pages/quality-inspections-page.tsx"), "QualityInspectionsPage");
const InspectionWorkspacePage = lazyNamed(() => import("./pages/inspection-workspace-page.tsx"), "InspectionWorkspacePage");
const QualityPlansPage = lazyNamed(() => import("./pages/quality-plans-page.tsx"), "QualityPlansPage");
const QualityRestrictionsPage = lazyNamed(() => import("./pages/quality-restrictions-page.tsx"), "QualityRestrictionsPage");
const QualityDispositionsPage = lazyNamed(() => import("./pages/quality-dispositions-page.tsx"), "QualityDispositionsPage");
const QualityNcrsPage = lazyNamed(() => import("./pages/quality-ncrs-page.tsx"), "QualityNcrsPage");
const QualityCapasPage = lazyNamed(() => import("./pages/quality-capas-page.tsx"), "QualityCapasPage");
const SupplierQualityPage = lazyNamed(() => import("./pages/supplier-quality-page.tsx"), "SupplierQualityPage");
const QualityCertificatesPage = lazyNamed(() => import("./pages/quality-certificates-page.tsx"), "QualityCertificatesPage");

const qualityModulePermissions: PermissionCondition = {
  anyOf: [
    "quality.read",
    "quality.plan.manage",
    "quality.characteristic.manage",
    "quality.inspection.create",
    "quality.inspection.execute",
    "quality.inspection.review",
    "quality.hold.manage",
    "quality.disposition.manage",
    "quality.issue.manage",
    "quality.ncr.manage",
    "quality.capa.manage",
    "quality.supplier.manage",
    "quality.certificate.manage",
    "quality.cost.read",
    "quality.audit.read",
    "quality.export"
  ]
};

const qualityNavigation = {
  id: "quality" as const,
  moduleIds: ["quality"] as const,
  path: "/quality",
  navigationKey: "quality" as const,
  category: "inventory" as const,
  destinations: QUALITY_DESTINATIONS,
  permissions: qualityModulePermissions,
  icon: "verified"
};

export const qualityRoutes: ModuleRouteRegistration = {
  navigation: qualityNavigation,
  routes: [
    {
      path: "quality",
      element: (
        <ModuleRootRoute
          module={qualityNavigation}
          overviewPermission="quality.read"
          defaultElement={<QualityOverviewPage />}
        />
      )
    },
    {
      path: "quality/overview",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["quality.read"] }}
          moduleId="quality"
          mode="read"
        >
          <QualityOverviewPage />
        </PermissionGuard>
      )
    },
    {
      path: "quality/inspections",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["quality.inspection.create", "quality.inspection.execute", "quality.inspection.review", "quality.read"] }}
          moduleId="quality"
          mode="mixed"
        >
          <QualityInspectionsPage />
        </PermissionGuard>
      )
    },
    {
      path: "quality/inspections/:id",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["quality.inspection.execute", "quality.inspection.review", "quality.read"] }}
          moduleId="quality"
          mode="mixed"
        >
          <InspectionWorkspacePage />
        </PermissionGuard>
      )
    },
    {
      path: "quality/plans",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["quality.plan.manage", "quality.characteristic.manage", "quality.read"] }}
          moduleId="quality"
          mode="mixed"
        >
          <QualityPlansPage />
        </PermissionGuard>
      )
    },
    {
      path: "quality/restrictions",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["quality.hold.manage", "quality.read"] }}
          moduleId="quality"
          mode="mixed"
        >
          <QualityRestrictionsPage />
        </PermissionGuard>
      )
    },
    {
      path: "quality/dispositions",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["quality.disposition.manage", "quality.read"] }}
          moduleId="quality"
          mode="mixed"
        >
          <QualityDispositionsPage />
        </PermissionGuard>
      )
    },
    {
      path: "quality/ncrs",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["quality.ncr.manage", "quality.issue.manage", "quality.read"] }}
          moduleId="quality"
          mode="mixed"
        >
          <QualityNcrsPage />
        </PermissionGuard>
      )
    },
    {
      path: "quality/capas",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["quality.capa.manage", "quality.read"] }}
          moduleId="quality"
          mode="mixed"
        >
          <QualityCapasPage />
        </PermissionGuard>
      )
    },
    {
      path: "quality/suppliers",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["quality.supplier.manage", "quality.read"] }}
          moduleId="quality"
          mode="mixed"
        >
          <SupplierQualityPage />
        </PermissionGuard>
      )
    },
    {
      path: "quality/supplier-quality",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["quality.supplier.manage", "quality.read"] }}
          moduleId="quality"
          mode="mixed"
        >
          <SupplierQualityPage />
        </PermissionGuard>
      )
    },
    {
      path: "quality/certificates",
      element: (
        <PermissionGuard
          permission={{ anyOf: ["quality.certificate.manage", "quality.read"] }}
          moduleId="quality"
          mode="mixed"
        >
          <QualityCertificatesPage />
        </PermissionGuard>
      )
    }
  ]
};
