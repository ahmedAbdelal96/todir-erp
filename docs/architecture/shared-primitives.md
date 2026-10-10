# Shared primitives

The shared primitives lock cross-module technical semantics that must not drift.

## Identifiers and references

`TenantId`, `LegalEntityId`, `BranchId`, `IdentityId`, `MembershipId`, `UserId`, `ModuleId`, `CorrelationId`, `CausationId`, and `MessageId` are immutable strongly typed wrappers around application-generated UUIDv7 values. They are distributed identifiers, not database sequences and never replace business document numbers.

`EntityRef` and `ActorRef` carry an opaque type and non-empty identifier across boundaries. They deliberately do not expose module implementations or identity persistence details.

## Time

Backend time uses NodaTime. `Instant` denotes a real UTC moment; `LocalDate` denotes a business date. `BusinessTime` converts using the owning entity's explicit IANA `DateTimeZone`, never the server-local timezone. The start-of-day operation delegates DST edge handling to NodaTime's zone rules.

## Errors

`DomainError` and its validation, conflict, forbidden, not-found, and concurrency variants carry stable machine codes such as `tenant.not_found` and `concurrency.conflict`. `Shared.Primitives` stays framework-neutral. The technical `ProblemDetailsMapper` lives in `Shared.BuildingBlocks` and maps errors to RFC 7807-compatible HTTP responses while preserving the `code` extension; it does not implement exception middleware or request-pipeline behavior.
