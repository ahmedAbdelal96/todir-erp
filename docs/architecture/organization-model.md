# Organization model

`platform` owns legal entities and branches. A legal entity belongs to one
tenant; a branch belongs to one legal entity and carries the tenant key for
composite-key and RLS enforcement.

Codes are unique within their tenant scope, referenced organization records are
archived rather than deleted, and historical business ownership does not change
when organization metadata changes. Country-specific tax behavior belongs to
country packs, not the global organization model.
