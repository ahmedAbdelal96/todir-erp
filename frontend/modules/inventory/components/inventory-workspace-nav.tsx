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
        display: "flex",
        alignItems: "center",
        gap: "0.25rem",
        width: "100%",
        minWidth: 0,
        marginBottom: "0.75rem",
        padding: "0.25rem",
        overflowX: "auto",
        scrollbarWidth: "none",
        msOverflowStyle: "none",
        backgroundColor: "var(--color-surface-card, #ffffff)",
        border: "1px solid var(--color-border-default)",
        borderRadius: "var(--radius-md, 0.5rem)",
        boxShadow: "0 1px 2px rgba(16, 42, 67, 0.08)"
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
            title={workspace.description}
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
              flex: "1 0 auto",
              minHeight: "2.5rem",
              padding: "0.5rem 1rem",
              textAlign: "center",
              border: "1px solid transparent",
              borderRadius: "var(--radius-sm, 0.375rem)",
              backgroundColor: isActive ? "var(--color-brand-primary, #102a43)" : "var(--color-surface-card, #ffffff)",
              color: isActive ? "#ffffff" : "var(--color-content-primary, #102a43)",
              boxShadow: isActive ? "0 1px 3px rgba(16, 42, 67, 0.18)" : "none",
              cursor: "pointer",
              whiteSpace: "nowrap",
              transition: "background-color 0.15s ease, color 0.15s ease, border-color 0.15s ease"
            }}
          >
            <span style={{ fontSize: "0.875rem", fontWeight: isActive ? 700 : 600 }}>{workspace.label}</span>
          </button>
        );
      })}
    </nav>
  );
}
