# Repository scripts

`scripts/` is for repository automation and developer-operation scripts, never arbitrary backend/frontend scripts. Scripts must be focused, documented, safe, and idempotent where practical; follow project cross-platform policy when defined. Destructive behavior requires explicit targets and clear safeguards.

Use concise kebab-case action names such as `validate-architecture.mjs` or `check-module-boundaries.mjs`. Do not use product, phase, date, version or temporary-history prefixes in new script filenames.
