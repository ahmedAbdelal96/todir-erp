# Backend engineering

Apply root governance and read the approved Platform Kernel and applicable module pack before backend work. Build business correctness first using C# and ASP.NET Core only when bootstrap authorizes it. Preserve module boundaries and domain/application/infrastructure separation; dependencies point inward.

Cross-module interaction is allowed only through approved public module contracts, capabilities, events, or ports as defined by TODIR architecture. Direct access to another module's implementation, DbContext, entities, repositories, or database tables is forbidden.

A module owns its data and persistence and remains its single writer/source of truth. Use EF Core and PostgreSQL according to the approved baseline; enforce transaction, validation, authorization, tenancy/RLS, migration, messaging, and observability rules without inventing them. Design API behavior, stable error codes, retries/idempotency and database indexes as part of each feature. Instrument meaningful operations and verify performance with realistic access patterns. Test domain rules, persistence, contracts, APIs, security, tenancy, messaging, failures and migrations as applicable.
