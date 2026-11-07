#!/usr/bin/env python3
"""Mechanical audit for ERP frontends: catches the violations that usually come back as fix requests.

Usage:
  python audit_frontend.py <repo-root> [--changed] [--strict] [--json] [paths...]

  --changed   only files changed vs HEAD (+ untracked), via git
  --strict    treat warnings as failures (exit 1)
  --json      machine-readable output
  paths       limit to these files/dirs (relative to repo-root)

Suppress a finding on a line (or the line above it) with a comment:
  // audit-ignore: rule-id  because <reason>
Standard library only. Heuristic by design: review each finding, do not blindly "fix" it.
"""
from __future__ import annotations

import json
import os
import re
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path

IGNORE_DIRS = {"node_modules", ".git", "dist", "build", ".next", "coverage", ".turbo", "out", ".cache", "storybook-static"}
CODE_EXT = {".ts", ".tsx", ".js", ".jsx"}
JSX_EXT = {".tsx", ".jsx"}
TEST_RE = re.compile(r"(\.test\.|\.spec\.|\.stories\.|__tests__|/e2e/|/tests?/|\.d\.ts$)")
I18N_RE = re.compile(r"(^|/)(i18n|locales?|translations?|lang)(/|$)")
LIB_TABLE_RE = re.compile(r"(data-?table|datatable|/ui/table|components/table)", re.I)
CURRENCY_DATA_RE = re.compile(r"(currenc|money|format|constants?|enums?|mock|fixtures?|seed)", re.I)

ARABIC = r"\u0600-\u06FF"


@dataclass
class Rule:
    id: str
    severity: str  # error | warn
    pattern: re.Pattern
    message: str
    exts: set
    skip_path: re.Pattern | None = None


def R(id_, sev, pat, msg, exts=CODE_EXT, skip=None, flags=0):
    return Rule(id_, sev, re.compile(pat, flags), msg, exts, skip)


RULES = [
    R("forbidden-tailwind", "error", r"""className=["'][^"']*\b(?:grid-cols-\d|bg-white|rounded-lg|rounded-md|p-4|p-6|gap-4|shadow-md|border-slate-)\b""",
      "Tailwind utility class detected: TODIR ERP strictly prohibits Tailwind utility classes. Use CSS variables, style objects with logical properties, or shared components.", JSX_EXT),
    R("raw-table", "error", r"<table\b", "Raw <table>: use the shared DataTable (consistent sorting, pagination, RTL, empty states).", JSX_EXT, LIB_TABLE_RE),
    R("physical-direction", "warn",
      r"(?<![\w-])(?<!rtl:)(?<!ltr:)-?(?:(?:m|p)[lr]-(?:\d|\[|px|auto)|(?:left|right)-(?:\d|\[|px|full|auto|1/2)|text-(?:left|right)\b|rounded-(?:l|r|tl|tr|bl|br)(?:-|\b)|border-(?:l|r)(?:-|\b)|float-(?:left|right)\b)",
      "Physical direction class: use logical ones (ms-/me-/ps-/pe-/start-/end-/text-start/rounded-s/border-s) so RTL works natively.", JSX_EXT),
    R("inline-style-direction", "warn", r"style=\{\{[^}]*(marginLeft|marginRight|paddingLeft|paddingRight|\bleft\s*:|\bright\s*:|textAlign\s*:\s*['\"](?:left|right))",
      "Inline style with physical direction: use logical CSS properties (marginInlineStart, insetInlineEnd, textAlign:'start').", JSX_EXT),
    R("hardcoded-currency-code", "warn", r"""['"](?:SAR|EGP|AED|USD|EUR|KWD|QAR|BHD|OMR|JOD)['"]""",
      "Hardcoded currency code: currency must come from the backend (entity currencyCode or company base currency).", CODE_EXT, CURRENCY_DATA_RE),
    R("currency-default-param", "error", r"""currency(?:Code)?\s*(?:=|\?\?|\|\|)\s*['"][A-Z]{3}['"]""",
      "Silent default currency: never assume one. Use the entity or company currency and show a neutral state if missing."),
    R("hardcoded-currency-symbol", "warn", r"(?:ر\.\s?س|ج\.\s?م|د\.\s?إ|ريال|جنيه|درهم)\s*(?=[<{'\"`])|[}\d]\s*(?:ر\.\s?س|ج\.\s?م)",
      "Hardcoded currency symbol in UI: use the central money formatter with the backend currency code.", JSX_EXT, I18N_RE),
    R("float-money", "warn", r"\.toFixed\(|parseFloat\([^)]*(?:amount|price|total|cost|balance|rate)", "Floating-point money/format: use the central formatter and decimal-safe math.", CODE_EXT),
    R("arabic-literal-in-jsx", "warn", rf">\s*[^<>{{}}\n]*[{ARABIC}][^<>{{}}\n]*<", "Arabic text hardcoded in JSX: move it to the feature's i18n file.", JSX_EXT, I18N_RE),
    R("literal-ui-attribute", "warn", rf"""\b(?:placeholder|title|aria-label|alt|label)=["'][^"'{{}}]*[{ARABIC}A-Za-z]{{2,}}[^"'{{}}]*["']""",
      "Literal UI text in attribute: use t('...') (placeholders, titles, aria-labels are user-facing).", JSX_EXT, I18N_RE),
    R("raw-enum-render", "warn", r">\s*\{\s*[\w.?]+\.(?:status|type|state|kind|stage)\s*\}\s*<", "Raw status/type rendered: map enum codes to translated labels (and Badge).", JSX_EXT),
    R("entity-as-text-input", "warn", r"""<(?:input|InputField|TextField)\b[^>]*\bname=["'](?:project|supplier|vendor|customer|client|warehouse|contract|costCode|account)(?:_?[iI]d|Name|Code)?["']""",
      "Entity chosen via text input: use the entity combobox that binds the id.", JSX_EXT),
    R("date-shift", "warn", r"""new Date\(\s*['"]\d{4}-\d{2}-\d{2}['"]\s*\)|toISOString\(\)\s*\.\s*(?:split|slice|substring)""",
      "Date-only value through Date/toISOString can shift a day by time zone: use the date helper or parse the parts.", CODE_EXT),
    R("window-dialog", "warn", r"(?<![\w.])(?:window\.)?(?:alert|confirm|prompt)\(", "Browser alert/confirm: use ConfirmDialog / toast from the shared library.", CODE_EXT),
    R("dangerous-html", "error", r"dangerouslySetInnerHTML", "dangerouslySetInnerHTML: render text, or sanitize with an approved sanitizer and justify.", JSX_EXT),
    R("token-in-storage", "error", r"""localStorage\.(?:set|get)Item\(\s*['"][^'"]*(?:token|jwt|auth|password|secret)""",
      "Auth secret in localStorage: follow the project's token strategy (prefer httpOnly cookies).", CODE_EXT),
    R("console-debug", "warn", r"console\.(?:log|debug)\(", "Leftover console output.", CODE_EXT),
    R("ts-any", "warn", r":\s*any\b|\bas any\b|<any>", "`any` type: use a real type or `unknown` with narrowing.", {".ts", ".tsx"}),
    R("ts-suppress", "warn", r"@ts-ignore|@ts-expect-error", "TS suppression: needs a reason comment and ideally a fix.", {".ts", ".tsx"}),
    R("ts-nocheck", "error", r"@ts-nocheck", "@ts-nocheck disables type safety for the whole file.", {".ts", ".tsx"}),
    R("index-as-key", "warn", r"key=\{\s*(?:i|idx|index)\s*\}", "Array index as React key: use a stable id.", JSX_EXT),
    R("click-on-div", "warn", r"<(?:div|span)\b[^>]*\bonClick=", "Click handler on non-interactive element: use button/link (keyboard + a11y).", JSX_EXT),
    R("img-no-alt", "warn", r"<img\b(?![^>]*\balt=)[^>]*>", "Image without alt text.", JSX_EXT),
    R("blank-no-rel", "warn", r"""target=["']_blank["'](?![^>]*\brel=)""", "target=_blank without rel=\"noopener noreferrer\".", JSX_EXT),
    R("fetch-in-effect", "warn", r"useEffect\(\s*\(\)\s*=>\s*\{?\s*(?:fetch|axios)\b", "Data fetching inside useEffect: use the project's query layer (cache, cancel, retry).", JSX_EXT),
]

IGNORE_TOKEN = re.compile(r"audit-ignore(?::\s*([\w,\- ]+))?")


def git_changed(root: Path) -> list[Path] | None:
    try:
        a = subprocess.run(["git", "-C", str(root), "diff", "--name-only", "HEAD"], capture_output=True, text=True, timeout=20)
        b = subprocess.run(["git", "-C", str(root), "ls-files", "--others", "--exclude-standard"], capture_output=True, text=True, timeout=20)
        if a.returncode != 0:
            a = subprocess.run(["git", "-C", str(root), "diff", "--name-only"], capture_output=True, text=True, timeout=20)
        names = set(a.stdout.split()) | set(b.stdout.split())
        return [root / n for n in sorted(names)]
    except (OSError, subprocess.SubprocessError):
        return None


def iter_files(root: Path, targets: list[Path]):
    for t in targets:
        if t.is_file():
            yield t
            continue
        for dirpath, dirnames, filenames in os.walk(t):
            dirnames[:] = [d for d in dirnames if d not in IGNORE_DIRS]
            for f in filenames:
                yield Path(dirpath) / f


def is_ignored(lines: list[str], idx: int, rule_id: str) -> bool:
    for j in (idx, idx - 1):
        if 0 <= j < len(lines):
            m = IGNORE_TOKEN.search(lines[j])
            if m:
                ids = m.group(1)
                if not ids:
                    return True
                first = re.split(r"\s+because\b", ids)[0]
                if rule_id in [x.strip() for x in re.split(r"[,\s]+", first) if x.strip()]:
                    return True
    return False


def audit_file(root: Path, path: Path):
    rel = str(path.relative_to(root)).replace("\\", "/")
    if path.suffix not in CODE_EXT or TEST_RE.search(rel):
        return []
    try:
        text = path.read_text(encoding="utf-8", errors="ignore")
    except OSError:
        return []
    if len(text) > 600_000:
        return []
    lines = text.splitlines()
    findings = []
    for rule in RULES:
        if path.suffix not in rule.exts:
            continue
        if rule.skip_path and rule.skip_path.search(rel):
            continue
        # single-line scan, except patterns meant for tags that can span lines
        for i, line in enumerate(lines):
            stripped = line.lstrip()
            if stripped.startswith(("//", "*", "/*")):
                continue
            if rule.pattern.search(line):
                if is_ignored(lines, i, rule.id):
                    continue
                findings.append({"file": rel, "line": i + 1, "rule": rule.id, "severity": rule.severity, "message": rule.message, "code": line.strip()[:140]})
    # raw i18n keys leaking as visible strings, e.g. >planning.budget.actions.submit<
    if path.suffix in JSX_EXT and not I18N_RE.search(rel):
        for i, line in enumerate(lines):
            if re.search(r">\s*[a-z]+(?:\.[a-zA-Z]+){2,}\s*<", line) and not is_ignored(lines, i, "raw-key"):
                findings.append({"file": rel, "line": i + 1, "rule": "raw-key", "severity": "error", "message": "Looks like a raw translation key rendered as text.", "code": line.strip()[:140]})
    return findings


def main() -> int:
    argv = sys.argv[1:]
    flags = {a for a in argv if a.startswith("--")}
    pos = [a for a in argv if not a.startswith("--")]
    root = Path(pos[0] if pos else ".").resolve()
    rest = pos[1:]
    if not root.exists():
        print(f"Path not found: {root}", file=sys.stderr)
        return 2

    if rest:
        targets = [(root / r).resolve() for r in rest]
    elif "--changed" in flags:
        changed = git_changed(root)
        if changed is None:
            print("git unavailable: falling back to full scan", file=sys.stderr)
            targets = [root]
        else:
            targets = [c for c in changed if c.exists()]
            if not targets:
                print("No changed files to audit.")
                return 0
    else:
        targets = [root]

    all_findings = []
    scanned = 0
    for f in iter_files(root, targets):
        if f.suffix in CODE_EXT:
            scanned += 1
        all_findings.extend(audit_file(root, f))

    errors = [x for x in all_findings if x["severity"] == "error"]
    warns = [x for x in all_findings if x["severity"] == "warn"]

    if "--json" in flags:
        print(json.dumps({"scanned": scanned, "errors": len(errors), "warnings": len(warns), "findings": all_findings}, ensure_ascii=False, indent=2))
    else:
        by_rule: dict[str, list] = {}
        for f in all_findings:
            by_rule.setdefault(f["rule"], []).append(f)
        for rule_id, items in sorted(by_rule.items(), key=lambda kv: (kv[1][0]["severity"] != "error", kv[0])):
            print(f"\n[{items[0]['severity'].upper()}] {rule_id} ({len(items)}): {items[0]['message']}")
            for it in items[:12]:
                print(f"  {it['file']}:{it['line']}  {it['code']}")
            if len(items) > 12:
                print(f"  ... and {len(items) - 12} more")
        print(f"\nScanned {scanned} code files: {len(errors)} errors, {len(warns)} warnings.")
        if all_findings:
            print("Heuristic audit: fix real issues, or add `// audit-ignore: <rule-id> because <reason>` for justified exceptions.")

    if errors or ("--strict" in flags and warns):
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
