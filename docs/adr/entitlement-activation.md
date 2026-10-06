# ADR-009 — Entitlement versus Activation

Status: Accepted

## Decision

Signed tenant entitlement snapshots answer whether a capability is commercially available; module lifecycle state answers whether the module is operationally activated. Authorization and endpoint guards evaluate both.

## Consequences

Commercial entitlement changes do not silently mutate operational state, and deactivation preserves history according to the module policy.

## Evidence

`src/backend/platform/platform-kernel/Entitlements`, `src/backend/platform/platform-kernel/Modules`, and platform service tests.
