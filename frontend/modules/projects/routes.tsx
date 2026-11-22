import React from "react";
import type { ModuleRouteRegistration } from "../../app/router/route-contract.ts";
import { lazyNamed } from "../../app/router/lazy-route-component.ts";
import { ModuleRootRoute } from "../../app/router/module-root-route.tsx";
import { PermissionGuard } from "../../core/runtime/permission-guard.tsx";
import { PROJECTS_DESTINATIONS } from "../../app/router/destination-registry.ts";
const ProjectsPage = lazyNamed(() => import("./pages/projects-page.tsx"), "ProjectsPage");
const ProjectWorkspacePage = lazyNamed(() => import("./pages/project-workspace-page.tsx"), "ProjectWorkspacePage");

const projectsNavigation = {
  id: "projects",
  moduleIds: ["projects"],
  path: "/projects",
  navigationKey: "projects",
  permissions: { anyOf: ["projects.read", "projects.create", "projects.budget.read", "projects.cost.read"] },
  destinations: PROJECTS_DESTINATIONS,
  icon: "projects"
} as const;

export const projectsRoutes: ModuleRouteRegistration = {
  navigation: projectsNavigation,
  routes: [
    { path: "projects", element: <ModuleRootRoute module={projectsNavigation} overviewPermission={{ anyOf: ["projects.read", "projects.create", "projects.budget.read", "projects.cost.read"] }} defaultElement={<ProjectsPage />} /> },
    { path: "projects/resource-load", element: <PermissionGuard permission={{ anyOf: ["projects.read", "projects.manage_team"] }} moduleId="projects" mode="read"><ProjectsPage initialTab="resourceLoad" /></PermissionGuard> },
    { path: "projects/templates", element: <PermissionGuard permission={{ anyOf: ["projects.read", "projects.create", "projects.edit"] }} moduleId="projects" mode="mixed"><ProjectsPage initialTab="templates" /></PermissionGuard> },
    { path: "projects/:id", element: <PermissionGuard permission="projects.read" moduleId="projects" mode="mixed"><ProjectWorkspacePage /></PermissionGuard> }
  ]
};
