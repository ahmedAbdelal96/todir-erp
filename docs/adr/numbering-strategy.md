# ADR-015 — Business Numbering Strategy

Status: Accepted

## Decision

Entity IDs and legal document numbers are separate. Sequence definitions are tenant and scope bound, allocated in the finalization transaction, and support standard, gap-controlled and regulatory-provider strategies without embedding country law in the kernel.

## Consequences

Numbering contention is isolated to the relevant sequence scope and legal numbering policy can evolve through providers.

## Evidence

`src/backend/platform/platform-kernel/Services/PlatformServices.cs`, numbering schema constraints, and numbering tests.
