# Pricing Engine and Commercial Price Ownership

**Status:** Accepted for the Pricing backend mission  
**Date:** 2026-10-21

## Decision

- Pricing is the single owner of reusable commercial prices and price-list versions.
- Selling and Buying are separate directions. A resolution always supplies tenant, legal entity, direction, pricing date, currency, list or explicit selection policy, product/UOM, quantity, and party/group context.
- Catalog, Directory, and FinanceBase remain the owners of product/UOM, party, and currency/FX facts. Pricing stores stable IDs and consumes their public capabilities; it owns no duplicate masters or cross-module ORM navigation.
- The resolver uses the documented lexicographic comparator: party scope, product specificity, UOM specificity, qualifying quantity floor, then explicit priority. Same-rank contradictory outcomes are configuration conflicts. Storage order and timestamps are never ranking inputs.
- A version becomes immutable when scheduled or active. Corrections create a new draft version. Price snapshots store the resolved commercial facts and do not link historical transactions to mutable master truth.
- Price-list currency conversion is policy-controlled and delegated to FinanceBase. Pricing owns no FX data and does not round a UnitPrice to document currency scale.
- Batch resolution uses one bounded candidate load per shared context rather than one query per line.
- Activation, audit, active-version switch, and `PriceListVersionActivatedV1` outbox enqueue share one database transaction.

## Product Owner deferral

Large-scale certification at 100k variants, 500k entries, 100k rules, and 100k+ import rows is deferred to the dedicated Performance Hardening phase. This does not defer bounded candidate queries, appropriate indexes, batch equivalence, moderate realistic batch checks, cache correctness, query-plan review, or concurrency sanity.

## Scope boundary

Promotions, arbitrary formula languages, Sales/Purchasing workflows, Tax, Inventory Cost, approval engines, POS/offline pricing, Redis, and Product/Admin UX are outside this backend mission.
