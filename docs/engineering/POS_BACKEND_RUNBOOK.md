# POS Backend Recovery Runbook

This runbook is for the authorized POS permissions and the existing backend API. It never instructs operators to edit POS tables directly.

## Unknown or delayed payment

Use `GET /api/v1/pos/checkouts/{checkoutId}`. For a provider checkout still in `PaymentPending`, use the provider webhook or `POST /api/v1/pos/checkouts/{checkoutId}/recover`. Do not create a second checkout with a new idempotency key. If the provider reports failure, the checkout is `PaymentFailed` and the sale remains non-completed.

## Payment success with internal completion failure

Keep the original checkout idempotency key. Call the recovery endpoint until inventory, document, and accounting states converge to `Completed`. The recovery operation is idempotent and records any cash movement once per checkout.

## Inventory conflict or stuck checkout

Inspect the checkout response states and retry the same recovery endpoint after the stock or integration condition is corrected. A checkout is not manually marked completed. An unresolved state blocks session close.

## Fiscal rejection

The country provider owns the payload and statutory response. Keep commercial/payment/inventory truth intact, preserve the regulatory failure state, and retry through the provider recovery operation. POS core does not contain country conditionals.

## Printer, scanner, drawer, or terminal failure

Hardware adapters return device observations only. Retry the adapter operation after device health is restored; never replay checkout or mutate sale state to repair a device issue.

## Duplicate scan, source, or offline replay

Reuse the original idempotency key. Offline envelopes are signed against the package credential, device, sequence, key, and payload. A valid replay returns the original envelope; a sequence collision creates a visible conflict for manager resolution.

## Offline package expired or device revoked

Download a new package only after the register is active, offline-enabled, and the terminal is `Ready`. A revoked/expired package or non-ready terminal is rejected. Resolve an accepted conflict with `POST /api/v1/pos/offline/conflicts/{conflictId}/resolve` using an authorized manager decision and an explanatory note.

## Offline cash sale

Submit the signed envelope, then call `POST /api/v1/pos/offline/envelopes/{envelopeId}/process-cash-sale`. The server revalidates the session and normal Catalog/Pricing/Tax/Inventory/Payments/Invoicing path. Retry the same endpoint for a `NeedsReview` envelope; the payload idempotency key prevents duplicate checkout, cash movement, or invoice effects.

## Cash difference or session cannot close

Use the session summary and cash-differences report. Resolve held carts, pending checkouts, and required variance reasons first. A reconciling variance requires the authorized variance-approval endpoint; direct database edits are prohibited.

## Return, refund, and exchange disagreement

Returns validate remaining quantity under row locking. Refunds use the original captured payment and idempotency key. Credit notes are requested from the return endpoint. Exchanges persist a group linking the return and replacement checkout; retry the underlying return/checkout recovery path rather than creating unrelated documents.

## Projection rebuild and disaster recovery

Use the platform's approved migration, outbox, and projection-rebuild operations. Validate restored tenant RLS, runtime role grants, migration checksum state, outbox delivery, and POS report reconciliation in a disposable verification database before reopening a store.
