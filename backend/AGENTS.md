# Backend engineering

## Absolute filename and folder-name prohibition

Never create or introduce a new backend source file, project file, or folder whose basename contains `todir` in any casing. This is absolute; use responsibility-based filesystem names while preserving already-established namespaces and assembly identifiers where required by the solution.

Apply root governance and read the approved Platform Kernel and applicable module pack before backend work. Build business correctness first using C# and ASP.NET Core only when bootstrap authorizes it. Keep the conventional module folders (`Controllers`, `Services`, `Repositories`, `Entities`, `DTOs`, `Validators`, `Data`, `Permissions`, and `Events`) responsibility-based and create only folders backed by real code; dependencies point inward through approved contracts.

The canonical backend map is `Api/`, `Worker/`, `DbMigrator/`, `Core/`,
`Modules/`, `Shared/`, and `Tests/`. Controllers are HTTP and authorization
transport only and follow `Controller → Service → Repository/Data` when
persistence is separated. Services never depend on Controllers; Entities do
not depend on transport or persistence; repositories and data code do not own
HTTP behavior. Do not add generic dumping grounds or restore parallel
`Application/`, `Infrastructure/`, `Domain/`, or `Interfaces/` project trees.

Cross-module interaction is allowed only through approved public module contracts, capabilities, events, or ports as defined by TODIR architecture. Direct access to another module's implementation, DbContext, entities, repositories, or database tables is forbidden.

A module owns its data and persistence and remains its single writer/source of truth. Use EF Core and PostgreSQL according to the approved baseline; enforce transaction, validation, authorization, tenancy/RLS, migration, messaging, and observability rules without inventing them. Design API behavior, stable error codes, retries/idempotency and database indexes as part of each feature. Instrument meaningful operations and verify performance with realistic access patterns. Test domain rules, persistence, contracts, APIs, security, tenancy, messaging, failures and migrations as applicable.
