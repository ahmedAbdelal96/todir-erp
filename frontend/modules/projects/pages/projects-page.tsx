import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate, useSearchParams, useOutletContext } from "react-router-dom";
import type { WorkspaceOutletContext } from "../../../app/router/route-contract.ts";
import { useOptionalLegalEntityContext } from "../../../core/runtime/runtime-provider.tsx";
import { hasAnyPermissionInAccess } from "../../../core/runtime/effective-access.ts";
import { PROJECTS_DESTINATIONS, isDestinationPermitted } from "../../../app/router/destination-registry.ts";
import { buildCanonicalTaskPath } from "../../../app/router/legacy-task-navigation.ts";
import { useIsMobile } from "../hooks/use-is-mobile.ts";
import { Surface } from "../../../components/pages/surface.tsx";
import { Button } from "../../../components/primitives/button.tsx";
import {
  useProjectsList,
  useProjectPortfolio,
  useResourceLoad,
  useProjectTemplates
} from "../hooks/use-projects-queries.ts";
import { ProjectPortfolioTab } from "../features/portfolio/project-portfolio-tab.tsx";
import { ResourceLoadTab } from "../features/portfolio/resource-load-tab.tsx";
import { ProjectTemplatesTab } from "../features/templates/project-templates-tab.tsx";
import { NewProjectModal, FromSalesSourceModal } from "../features/modals/project-modals.tsx";
import { NewTemplateModal } from "../features/modals/financial-billing-modals.tsx";
import {
  ProjectBriefcaseIcon,
  TeamUsersIcon,
  DocumentFolderIcon,
  PlusCircleIcon,
  ArrowPathIcon
} from "../components/projects-icons.tsx";

export type ProjectsRootTabId = "portfolio" | "resourceLoad" | "templates";

export interface ProjectsPageProps {
  readonly initialTab?: ProjectsRootTabId | undefined;
}

export function ProjectsPage({ initialTab }: ProjectsPageProps = {}): React.JSX.Element {
  const { t } = useTranslation("projects");
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const isMobile = useIsMobile();

  const activeTab = initialTab ?? (searchParams.get("tab") as ProjectsRootTabId | null) ?? "portfolio";

  const handleTabChange = (tab: ProjectsRootTabId) => {
    const target = buildCanonicalTaskPath(PROJECTS_DESTINATIONS, "/projects", tab, searchParams.toString());
    if (target) navigate(target);
  };

  const outletCtx = useOutletContext<WorkspaceOutletContext | undefined>();
  const runtimeContext = useOptionalLegalEntityContext();
  const effectiveAccess = outletCtx?.effectiveAccess;

  const legalEntityId = outletCtx?.legalEntityId || runtimeContext?.legalEntityId || "";
  const legalEntityName = runtimeContext?.name;

  // Authorization checks use the backend-authoritative effective access.
  const canRead = hasAnyPermissionInAccess(effectiveAccess, "projects.read", "projects.budget.read", "projects.cost.read");
  const canCreate = hasAnyPermissionInAccess(effectiveAccess, "projects.create");
  const canViewPortfolio = Boolean(effectiveAccess && isDestinationPermitted(PROJECTS_DESTINATIONS[0]!, effectiveAccess));
  const canViewResourceLoad = Boolean(effectiveAccess && isDestinationPermitted(PROJECTS_DESTINATIONS[1]!, effectiveAccess));
  const canViewTemplates = Boolean(effectiveAccess && isDestinationPermitted(PROJECTS_DESTINATIONS[2]!, effectiveAccess));

  // Search & filter state
  const [search, setSearch] = useState("");
  const [stateFilter, setStateFilter] = useState("ALL");
  const [typeFilter, setTypeFilter] = useState("ALL");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(20);

  const handleSearchChange = (value: string) => {
    setSearch(value);
    setPage(1);
  };

  const handleStateFilterChange = (value: string) => {
    setStateFilter(value);
    setPage(1);
  };

  const handleTypeFilterChange = (value: string) => {
    setTypeFilter(value);
    setPage(1);
  };

  // Queries
  const queryParams: {
    search?: string;
    state?: string;
    projectType?: string;
    page?: number;
    pageSize?: number;
  } = {
    page,
    pageSize
  };
  if (search) queryParams.search = search;
  if (stateFilter !== "ALL") queryParams.state = stateFilter;
  if (typeFilter !== "ALL") queryParams.projectType = typeFilter;

  const projectsQuery = useProjectsList(legalEntityId, queryParams);
  const portfolioQuery = useProjectPortfolio(legalEntityId);
  const resourceLoadQuery = useResourceLoad(legalEntityId);
  const templatesQuery = useProjectTemplates(legalEntityId);

  // Modals state
  const [isNewProjectOpen, setIsNewProjectOpen] = useState(false);
  const [isFromSalesOpen, setIsFromSalesOpen] = useState(false);
  const [isNewTemplateOpen, setIsNewTemplateOpen] = useState(false);

  // Fail closed when the backend-authoritative access does not permit the module.
  if (!canRead) {
    return (
      <Surface variant="card" padding="normal" style={{ textAlign: "center", margin: "2rem" }}>
        <h2 style={{ color: "var(--color-danger, #ef4444)" }}>{t("messages.unauthorized")}</h2>
      </Surface>
    );
  }

  const projects = projectsQuery.data?.items ?? [];

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "1.25rem", padding: isMobile ? "0.75rem" : "1.5rem" }}>
      {/* Top Banner / Navigation Bar */}
      <Surface
        variant="card"
        padding="compact"
        style={{
          background: "var(--color-surface-card, #ffffff)",
          border: "1px solid var(--color-border-default, #cbd5e1)",
          borderRadius: "var(--radius-lg, 12px)",
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          flexWrap: "wrap",
          gap: "1rem"
        }}
      >
        <div style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}>
          <div
            style={{
              width: "42px",
              height: "42px",
              borderRadius: "var(--radius-md, 8px)",
              background: "var(--color-brand-primary, #102a43)",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              color: "#ffffff"
            }}
          >
            <ProjectBriefcaseIcon size={24} color="#ffffff" />
          </div>
          <div>
            <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
              <h1
                style={{
                  margin: 0,
                  fontSize: isMobile ? "1.25rem" : "1.5rem",
                  fontWeight: 800,
                  color: "var(--color-content-primary, #0f172a)",
                  fontFamily: "var(--font-brand, inherit)"
                }}
              >
                {t("title")}
              </h1>
              {legalEntityName && (
                <span
                  style={{
                    padding: "0.125rem 0.625rem",
                    fontSize: "0.75rem",
                    fontWeight: 600,
                    borderRadius: "9999px",
                    background: "var(--color-surface-subtle, #f1f5f9)",
                    color: "var(--color-content-secondary, #475569)",
                    border: "1px solid var(--color-border-default, #cbd5e1)"
                  }}
                >
                  {legalEntityName}
                </span>
              )}
            </div>
            <p
              style={{
                margin: "0.25rem 0 0 0",
                fontSize: "0.875rem",
                color: "var(--color-content-secondary, #64748b)"
              }}
            >
              {t("subtitle")}
            </p>
          </div>
        </div>

        {/* Global Actions */}
        <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
          <Button
            variant="secondary"
            size="compact"
            onClick={() => {
              projectsQuery.refetch();
              portfolioQuery.refetch();
              resourceLoadQuery.refetch();
              templatesQuery.refetch();
            }}
            data-testid="btn-refresh-projects"
          >
            <ArrowPathIcon size={14} />
            {t("actions.refresh")}
          </Button>

          {canCreate && (
            <>
              <Button
                variant="secondary"
                size="compact"
                onClick={() => setIsFromSalesOpen(true)}
                data-testid="btn-from-sales"
              >
                <PlusCircleIcon size={14} />
                {t("actions.fromSalesSource")}
              </Button>

              <Button
                variant="primary"
                size="compact"
                onClick={() => setIsNewProjectOpen(true)}
                data-testid="btn-new-project"
              >
                <PlusCircleIcon size={14} />
                {t("actions.newProject")}
              </Button>
            </>
          )}
        </div>
      </Surface>

      {/* Primary Module Tabs */}
      <Surface
        variant="card"
        padding="none"
        style={{
          display: "flex",
          gap: "0.25rem",
          padding: "0.375rem",
          background: "var(--color-surface-card, #ffffff)",
          border: "1px solid var(--color-border-default, #cbd5e1)",
          borderRadius: "var(--radius-lg, 12px)",
          overflowX: "auto"
        }}
      >
        {canViewPortfolio && <button
          type="button"
          onClick={() => handleTabChange("portfolio")}
          data-testid="tab-portfolio"
          style={{
            display: "inline-flex",
            alignItems: "center",
            gap: "0.5rem",
            padding: "0.625rem 1rem",
            borderRadius: "var(--radius-md, 8px)",
            border: "none",
            background: activeTab === "portfolio" ? "var(--color-brand-primary, #102a43)" : "transparent",
            color: activeTab === "portfolio" ? "#ffffff" : "var(--color-content-secondary, #475569)",
            fontWeight: activeTab === "portfolio" ? 600 : 500,
            fontSize: "0.875rem",
            cursor: "pointer",
            whiteSpace: "nowrap",
            transition: "all 0.15s ease"
          }}
        >
          <ProjectBriefcaseIcon size={16} />
          {t("tabs.portfolio")}
          </button>}

        {canViewResourceLoad && <button
          type="button"
          onClick={() => handleTabChange("resourceLoad")}
          data-testid="tab-resource-load"
          style={{
            display: "inline-flex",
            alignItems: "center",
            gap: "0.5rem",
            padding: "0.625rem 1rem",
            borderRadius: "var(--radius-md, 8px)",
            border: "none",
            background: activeTab === "resourceLoad" ? "var(--color-brand-primary, #102a43)" : "transparent",
            color: activeTab === "resourceLoad" ? "#ffffff" : "var(--color-content-secondary, #475569)",
            fontWeight: activeTab === "resourceLoad" ? 600 : 500,
            fontSize: "0.875rem",
            cursor: "pointer",
            whiteSpace: "nowrap",
            transition: "all 0.15s ease"
          }}
        >
          <TeamUsersIcon size={16} />
          {t("tabs.resourceLoad")}
          </button>}

        {canViewTemplates && <button
          type="button"
          onClick={() => handleTabChange("templates")}
          data-testid="tab-templates"
          style={{
            display: "inline-flex",
            alignItems: "center",
            gap: "0.5rem",
            padding: "0.625rem 1rem",
            borderRadius: "var(--radius-md, 8px)",
            border: "none",
            background: activeTab === "templates" ? "var(--color-brand-primary, #102a43)" : "transparent",
            color: activeTab === "templates" ? "#ffffff" : "var(--color-content-secondary, #475569)",
            fontWeight: activeTab === "templates" ? 600 : 500,
            fontSize: "0.875rem",
            cursor: "pointer",
            whiteSpace: "nowrap",
            transition: "all 0.15s ease"
          }}
        >
          <DocumentFolderIcon size={16} />
          {t("tabs.templates")}
        </button>}
      </Surface>

      {/* Active Tab Panel */}
      {activeTab === "portfolio" && (
        <ProjectPortfolioTab
          projects={projects}
          portfolioItems={portfolioQuery.data?.items}
          isLoading={projectsQuery.isLoading || portfolioQuery.isLoading}
          onSelectProject={(projectId) => navigate(`/projects/${projectId}`)}
          onNewProject={() => setIsNewProjectOpen(true)}
          onFromSalesSource={() => setIsFromSalesOpen(true)}
          search={search}
          onSearchChange={handleSearchChange}
          stateFilter={stateFilter}
          onStateFilterChange={handleStateFilterChange}
          typeFilter={typeFilter}
          onTypeFilterChange={handleTypeFilterChange}
          page={page}
          pageSize={pageSize}
          totalCount={projectsQuery.data?.totalCount}
          hasNext={projectsQuery.data?.hasNext}
          onPageChange={setPage}
          onPageSizeChange={(newSize) => {
            setPageSize(newSize);
            setPage(1);
          }}
        />
      )}

      {activeTab === "resourceLoad" && (
        <ResourceLoadTab
          resourceLoad={Array.isArray(resourceLoadQuery.data) ? resourceLoadQuery.data : []}
          isLoading={resourceLoadQuery.isLoading}
        />
      )}

      {activeTab === "templates" && (
        <ProjectTemplatesTab
          templates={templatesQuery.data?.items ?? []}
          isLoading={templatesQuery.isLoading}
          onNewTemplate={() => setIsNewTemplateOpen(true)}
        />
      )}

      {/* Creation Modals */}
      <NewProjectModal
        isOpen={isNewProjectOpen}
        legalEntityId={legalEntityId}
        onClose={() => setIsNewProjectOpen(false)}
      />

      <FromSalesSourceModal
        isOpen={isFromSalesOpen}
        legalEntityId={legalEntityId}
        onClose={() => setIsFromSalesOpen(false)}
      />

      <NewTemplateModal
        isOpen={isNewTemplateOpen}
        legalEntityId={legalEntityId}
        onClose={() => setIsNewTemplateOpen(false)}
      />
    </div>
  );
}
