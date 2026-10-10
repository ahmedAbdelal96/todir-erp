# Catalog module rules

- `foundation_catalog` is the single writer for product templates, variants, identifiers, categories, brands and units of measure.
- Product identity is separate from SKU, barcode and future pricing, tax, inventory and accounting profiles.
- Every product has a default variant; downstream modules reference `ProductVariantId` through contracts and never read Catalog tables directly.
- Product and child data are tenant-owned, tenant-safe and protected by enabled and forced PostgreSQL RLS.
- Products and master data are archived/restored, not hard-deleted.
- Product UOM conversions are product-specific, decimal-only and effective-dated; historical consumers must snapshot conversion factors.
- Controllers only transport requests and authorization; domain invariants belong in Domain and application services orchestrate persistence, audit and outbox.
- Search is bounded, projected and indexed. Do not add pricing, tax, inventory, purchasing or accounting fields.
- Arabic/English names and RTL workflows remain supported. Local verification is required before the single Catalog commit.
