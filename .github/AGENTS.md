# GitHub automation

Place CI workflows, pull-request automation, repository automation, future security scanning and dependency automation in this directory. Do not put application logic here. Any workflow must follow repository governance and be introduced only when toolchain and operational architecture authorize it.

Workflow filenames must be short responsibility-based kebab-case names such as `validation.yml`, `security-scan.yml` or `deploy-staging.yml`. Do not use brand or phase prefixes. Keep a small number of responsibility-based workflows instead of one workflow per phase.
