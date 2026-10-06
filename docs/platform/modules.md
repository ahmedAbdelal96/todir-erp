# Module registry

Manifests are machine-readable contracts. IDs are stable lowercase identifiers, hard dependencies form a DAG, and implementation assemblies never reference another implementation assembly. `ModuleRegistry` validates IDs, dependencies, cycles, and owned schema uniqueness before the host starts.
