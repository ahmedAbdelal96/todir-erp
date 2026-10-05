# Documentation governance

Canonical placement is: architecture in `architecture/`; ADRs in `adr/`; Platform Kernel documentation in `platform/`; module documentation in `modules/`; API material in `api/`; operational procedures in `runbooks/`; and security documentation in `security/`.

Do not place random Markdown at repository root or bury canonical architecture inside source folders. Update the existing canonical document rather than creating `*-new`, `*-final`, or version-chaos copies. Preserve established numbered plan ordering. Significant architecture changes require an ADR; code and docs must evolve together when behavior or contracts change.
