# Documentation governance

Canonical placement is: architecture in `architecture/`; ADRs in `adr/`; Platform Kernel documentation in `platform/`; module documentation in `modules/`; API material in `api/`; operational procedures in `runbooks/`; and security documentation in `security/`.

Do not place random Markdown at repository root or bury canonical architecture inside source folders. Update the existing canonical document rather than creating `*-new`, `*-final`, or version-chaos copies. Preserve established numbered plan ordering. Significant architecture changes require an ADR; code and docs must evolve together when behavior or contracts change.

New durable documentation filenames must be short purpose-based kebab-case names such as `solution-boundaries.md` or `tenant-isolation.md`. Do not add brand names, phase numbers, dates, versions or history prefixes. Existing numbered architecture/module packs are legacy canonical sources and must not be mass-renamed as part of ordinary implementation.
