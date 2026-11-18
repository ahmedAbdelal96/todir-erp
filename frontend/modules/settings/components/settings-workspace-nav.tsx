import React from "react";

export interface SettingsWorkspaceItem {
  readonly id: string;
  readonly label: string;
  readonly description: string;
}

export interface SettingsWorkspaceNavProps {
  readonly ariaLabel: string;
  readonly workspaces: readonly SettingsWorkspaceItem[];
  readonly activeWorkspace: string;
  readonly onSelectWorkspace: (id: string) => void;
}

export function SettingsWorkspaceNav({
  ariaLabel,
  workspaces,
  activeWorkspace,
  onSelectWorkspace
}: SettingsWorkspaceNavProps): React.JSX.Element {
  return (
    <nav aria-label={ariaLabel} role="tablist" style={{ display: "flex", alignItems: "stretch", gap: "0.375rem", width: "100%", minWidth: 0, padding: "0.25rem", overflowX: "auto", scrollbarWidth: "none", msOverflowStyle: "none", backgroundColor: "var(--color-surface-card, #ffffff)", border: "1px solid var(--color-border-default)", borderRadius: "var(--radius-md, 0.5rem)", boxShadow: "0 1px 2px rgba(16, 42, 67, 0.08)" }}>
      {workspaces.map((workspace, index) => {
        const isActive = workspace.id === activeWorkspace;
        return (
          <button
            key={workspace.id}
            type="button"
            role="tab"
            id={workspace.id}
            aria-selected={isActive}
            aria-controls="settings-workspace-content"
            tabIndex={isActive ? 0 : -1}
            title={workspace.description}
            onClick={() => onSelectWorkspace(workspace.id)}
            onKeyDown={(event) => {
              if (event.key !== "ArrowRight" && event.key !== "ArrowLeft") return;
              event.preventDefault();
              const direction = event.key === "ArrowRight" ? 1 : -1;
              const nextIndex = (index + direction + workspaces.length) % workspaces.length;
              const nextWorkspace = workspaces[nextIndex];
              if (nextWorkspace) onSelectWorkspace(nextWorkspace.id);
            }}
            style={{ display: "flex", flex: "1 0 12rem", minHeight: "3.5rem", padding: "0.625rem 1rem", textAlign: "start", border: "1px solid transparent", borderRadius: "var(--radius-sm, 0.375rem)", backgroundColor: isActive ? "var(--color-brand-primary, #102a43)" : "var(--color-surface-card, #ffffff)", color: isActive ? "#ffffff" : "var(--color-content-primary, #102a43)", boxShadow: isActive ? "0 1px 3px rgba(16, 42, 67, 0.18)" : "none", cursor: "pointer", transition: "background-color 0.15s ease, color 0.15s ease, border-color 0.15s ease" }}
          >
            <span style={{ display: "flex", flexDirection: "column", gap: "0.2rem" }}>
              <span style={{ fontSize: "0.875rem", fontWeight: isActive ? 700 : 600 }}>{workspace.label}</span>
              <span style={{ fontSize: "0.7rem", lineHeight: 1.35, color: isActive ? "rgba(255, 255, 255, 0.78)" : "var(--color-content-muted)" }}>{workspace.description}</span>
            </span>
          </button>
        );
      })}
    </nav>
  );
}
