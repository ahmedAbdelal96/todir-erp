# Platform Kernel

## Absolute filename and folder-name prohibition

Never create or introduce a new Platform Kernel source file, project file, or folder whose basename contains `todir` in any casing. This is absolute; use responsibility-based paths even when the code retains an existing `Todir.*` namespace or assembly identity.

Platform Kernel work is phase-by-phase under the current approved Platform Kernel pack. Do not skip gates or begin real foundation/business modules before the required Kernel gates are complete.

Platform code must remain reusable platform capability, never acquire business-domain semantics. Changes to tenancy, identity, authorization, module lifecycle, transaction coordination, messaging, audit, settings, files, custom fields, scheduling, or observability must follow their approved phase, ownership, security and contract rules.
