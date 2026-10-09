import React from "react";
import type { EntityReference } from "../contracts/domain-values.ts";
import clsx from "clsx";

export interface EntityRefLinkProps {
  readonly entity: EntityReference | null | undefined;
  readonly href?: string;
  readonly onClick?: () => void;
  readonly showCode?: boolean;
  readonly className?: string;
}

export function EntityRefLink({
  entity,
  href,
  onClick,
  showCode = true,
  className
}: EntityRefLinkProps): React.JSX.Element {
  if (!entity) return <span>—</span>;

  const displayText = showCode && entity.code ? `${entity.code} · ${entity.name}` : entity.name;

  if (href || onClick) {
    return (
      <a
        href={href ?? "#"}
        onClick={(e) => {
          if (onClick) {
            e.preventDefault();
            onClick();
          }
        }}
        className={clsx("ui-entity-ref-link", className)}
        style={{
          color: "var(--color-action-primary)",
          textDecoration: "none",
          fontWeight: 500,
          cursor: "pointer"
        }}
      >
        {displayText}
      </a>
    );
  }

  return (
    <span
      className={clsx("ui-entity-ref-text", className)}
      style={{
        color: "var(--color-content-primary)",
        fontWeight: 500
      }}
    >
      {displayText}
    </span>
  );
}
