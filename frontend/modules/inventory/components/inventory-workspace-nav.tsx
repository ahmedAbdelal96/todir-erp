import React from "react";

export interface InventoryWorkspaceItem {
  readonly id: string;
  readonly label: string;
  readonly description: string;
}

export interface InventoryWorkspaceNavProps {
  readonly ariaLabel: string;
  readonly workspaces: readonly InventoryWorkspaceItem[];
  readonly activeWorkspace: string;
  readonly onSelectWorkspace: (id: string) => void;
}

export function InventoryWorkspaceNav({
  ariaLabel,
  workspaces,
  activeWorkspace,
  onSelectWorkspace
}: InventoryWorkspaceNavProps): React.JSX.Element {
  return (
    <nav
      aria-label={ariaLabel}
      role="tablist"
      style={{
        display: "grid",
        gridTemplateColumns: "repeat(auto-fit, minmax(180px, 1fr))",
        gap: "0.5rem",
        width: "100%",
        marginBottom: "0.75rem"
      }}
    >
      {workspaces.map((workspace, index) => {
        const isActive = workspace.id === activeWorkspace;
        return (
          <button
            key={workspace.id}
            type="button"
            role="tab"
            aria-selected={isActive}
            aria-controls="inventory-tab-content"
            tabIndex={isActive ? 0 : -1}
            onClick={() => onSelectWorkspace(workspace.id)}
            onKeyDown={(event) => {
              if (event.key !== "ArrowRight" && event.key !== "ArrowLeft") return;
              event.preventDefault();
              const direction = event.key === "ArrowRight" ? 1 : -1;
              const nextIndex = (index + direction + workspaces.length) % workspaces.length;
              const nextWorkspace = workspaces[nextIndex];
              if (nextWorkspace) {
                onSelectWorkspace(nextWorkspace.id);
              }
            }}
            style={{
              display: "flex",
              flexDirection: "column",
              alignItems: "flex-start",
              gap: "0.2rem",
              minHeight: "4.25rem",
              padding: "0.7rem 0.875rem",
              textAlign: "start",
              border: `1px solid ${isActive ? "var(--color-brand-primary, #102a43)" : "var(--color-border-default)"}`,
              borderRadius: "var(--radius-md, 0.5rem)",
              backgroundColor: isActive ? "var(--color-brand-primary, #102a43)" : "var(--color-surface-card, #ffffff)",
              color: isActive ? "#ffffff" : "var(--color-content-primary, #102a43)",
              boxShadow: isActive
                ? "0 2px 4px rgba(16, 42, 67, 0.18), 0 6px 14px rgba(16, 42, 67, 0.08)"
                : "0 1px 2px rgba(16, 42, 67, 0.08)",
              cursor: "pointer",
              transition: "background-color 0.15s ease, color 0.15s ease, border-color 0.15s ease"
            }}
          >
            <span style={{ fontSize: "0.9375rem", fontWeight: 700 }}>{workspace.label}</span>
            <span
              style={{
                fontSize: "0.75rem",
                lineHeight: 1.4,
                color: isActive ? "rgba(255, 255, 255, 0.82)" : "var(--color-content-muted)"
              }}
            >
              {workspace.description}
            </span>
          </button>
        );
      })}
    </nav>
  );
}
