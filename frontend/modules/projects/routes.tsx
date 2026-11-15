import React from "react";
import type { ModuleRouteRegistration } from "../../app/router/route-contract.ts";
import { ProjectsPage } from "./pages/projects-page.tsx";
import { ProjectWorkspacePage } from "./pages/project-workspace-page.tsx";

export const projectsRoutes: ModuleRouteRegistration = {
  navigation: {
    id: "projects",
    moduleIds: ["projects"],
    path: "/projects",
    navigationKey: "projects",
    permission: "projects.read",
    icon: "projects"
  },
  routes: [
    {
      path: "projects",
      element: <ProjectsPage />
    },
    {
      path: "projects/:id",
      element: <ProjectWorkspacePage />
    }
  ]
};
