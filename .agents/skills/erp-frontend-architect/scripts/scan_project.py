#!/usr/bin/env python3
"""Scan a frontend repo and print a factual map to seed .erp-frontend/profile.md.

Usage:  python scan_project.py <repo-root> [--json]

Standard library only. Reads no secrets (skips .env*). Everything it cannot detect
is reported as UNKNOWN so the agent asks the owner instead of guessing.
"""
from __future__ import annotations

import json
import os
import re
import sys
from pathlib import Path

IGNORE_DIRS = {
    "node_modules", ".git", "dist", "build", ".next", ".nuxt", "coverage", ".turbo",
    ".cache", "out", ".vercel", ".output", "__pycache__", ".venv", "venv", "storybook-static",
}
SRC_EXT = {".ts", ".tsx", ".js", ".jsx", ".vue", ".svelte"}
MAX_FILE_BYTES = 400_000

COMPONENT_KEYWORDS = {
    "Data table": r"datatable|data-table|datagrid|table",
    "Pagination": r"pagination|pager",
    "Card / section wrapper": r"componentcard|card|panel|section",
    "Metric / stat": r"statcard|metric|kpi|stat",
    "Inputs": r"inputfield|textfield|input|textarea",
    "Select": r"select|dropdown",
    "Date picker": r"datepicker|date-picker|calendar",
    "Entity combobox": r"combobox|autocomplete|entitypicker|lookup",
    "Modal / dialog": r"modal|dialog",
    "Drawer / sheet": r"drawer|sheet|sidepanel",
    "Confirm dialog": r"confirm",
    "Loading / empty / error": r"asyncstate|loadingstate|emptystate|errorstate|skeleton|spinner",
    "Button": r"^button",
    "Badge / status": r"badge|status|tag|chip",
    "Action menu": r"actionmenu|dropdownmenu|menu",
    "Toast / notification": r"toast|notification|snackbar",
    "Tabs": r"tabs",
    "Tooltip": r"tooltip",
    "Stepper": r"stepper|wizard",
    "File upload": r"upload|dropzone|attachment",
}

PKG_SIGNALS = {
    "framework": {
        "next": "Next.js", "vite": "Vite", "react-scripts": "Create React App",
        "@remix-run/react": "Remix", "nuxt": "Nuxt", "@angular/core": "Angular", "vue": "Vue",
        "svelte": "Svelte", "react": "React",
    },
    "router": {
        "react-router-dom": "React Router", "react-router": "React Router",
        "@tanstack/react-router": "TanStack Router", "vue-router": "Vue Router",
    },
    "styling": {
        "tailwindcss": "Tailwind", "styled-components": "styled-components", "@emotion/react": "Emotion",
        "sass": "Sass", "@mui/material": "MUI", "antd": "Ant Design", "@chakra-ui/react": "Chakra UI",
        "@mantine/core": "Mantine", "class-variance-authority": "cva (shadcn-style)",
        "@radix-ui/react-dialog": "Radix UI", "@headlessui/react": "Headless UI",
    },
    "server_state": {
        "@tanstack/react-query": "TanStack Query", "swr": "SWR", "@reduxjs/toolkit": "Redux Toolkit (RTK Query?)",
        "@apollo/client": "Apollo", "axios": "axios (http client)", "ky": "ky (http client)",
    },
    "client_state": {"zustand": "Zustand", "redux": "Redux", "jotai": "Jotai", "recoil": "Recoil", "mobx": "MobX"},
    "forms": {
        "react-hook-form": "react-hook-form", "formik": "Formik", "zod": "zod", "yup": "yup",
        "@hookform/resolvers": "hookform resolvers", "valibot": "valibot",
    },
    "tables": {
        "@tanstack/react-table": "TanStack Table", "ag-grid-react": "AG Grid", "react-virtuoso": "react-virtuoso",
        "@tanstack/react-virtual": "TanStack Virtual", "react-window": "react-window",
    },
    "i18n": {
        "i18next": "i18next", "react-i18next": "react-i18next", "next-intl": "next-intl",
        "react-intl": "react-intl", "@formatjs/intl": "FormatJS", "vue-i18n": "vue-i18n",
    },
    "charts": {"recharts": "Recharts", "chart.js": "Chart.js", "echarts": "ECharts", "apexcharts": "ApexCharts", "@nivo/core": "Nivo"},
    "dates_money": {
        "dayjs": "dayjs", "date-fns": "date-fns", "luxon": "Luxon", "moment": "moment",
        "decimal.js": "decimal.js", "big.js": "big.js", "bignumber.js": "bignumber.js", "dinero.js": "dinero.js",
        "moment-hijri": "moment-hijri", "@internationalized/date": "@internationalized/date",
    },
    "testing": {
        "vitest": "Vitest", "jest": "Jest", "@playwright/test": "Playwright", "cypress": "Cypress",
        "@testing-library/react": "Testing Library", "msw": "MSW", "storybook": "Storybook",
    },
}


def walk(root: Path, max_files: int = 20000):
    count = 0
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if d not in IGNORE_DIRS and not d.startswith(".git")]
        for f in filenames:
            count += 1
            if count > max_files:
                return
            yield Path(dirpath) / f


def read_text(p: Path) -> str:
    try:
        if p.stat().st_size > MAX_FILE_BYTES:
            return ""
        return p.read_text(encoding="utf-8", errors="ignore")
    except OSError:
        return ""


def find_package_jsons(root: Path):
    found = []
    for p in walk(root):
        if p.name == "package.json":
            found.append(p)
    return found


def detect_deps(pkg: dict) -> dict[str, list[str]]:
    deps = {}
    deps.update(pkg.get("dependencies", {}))
    deps.update(pkg.get("devDependencies", {}))
    out: dict[str, list[str]] = {}
    for cat, signals in PKG_SIGNALS.items():
        hits = []
        for name, label in signals.items():
            if name in deps:
                hits.append(f"{label} {deps[name]}")
        if hits:
            out[cat] = hits
    return out


def pick_frontend_package(pkgs: list[Path]):
    best, best_score = None, -1
    for p in pkgs:
        try:
            data = json.loads(read_text(p) or "{}")
        except json.JSONDecodeError:
            continue
        deps = {**data.get("dependencies", {}), **data.get("devDependencies", {})}
        score = sum(k in deps for k in ("react", "next", "vue", "vite", "tailwindcss", "@tanstack/react-query"))
        if score > best_score:
            best, best_score = (p, data), score
    return best


def tree(root: Path, base: Path, depth: int = 3, limit: int = 60) -> list[str]:
    lines: list[str] = []

    def rec(d: Path, level: int):
        if level > depth or len(lines) >= limit:
            return
        try:
            entries = sorted([e for e in d.iterdir() if e.is_dir() and e.name not in IGNORE_DIRS and not e.name.startswith(".")])
        except OSError:
            return
        for e in entries:
            if len(lines) >= limit:
                return
            lines.append("  " * level + e.name + "/")
            rec(e, level + 1)

    rec(base, 0)
    return lines


def lockfile_pm(base: Path, root: Path) -> str:
    for d in (base, root):
        for lock, pm in (("pnpm-lock.yaml", "pnpm"), ("yarn.lock", "yarn"), ("package-lock.json", "npm"), ("bun.lockb", "bun")):
            if (d / lock).exists():
                return pm
    return "UNKNOWN"


def main() -> int:
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    as_json = "--json" in sys.argv
    root = Path(args[0] if args else ".").resolve()
    if not root.exists():
        print(f"Path not found: {root}", file=sys.stderr)
        return 2

    result: dict = {"root": str(root)}
    pkgs = find_package_jsons(root)
    picked = pick_frontend_package(pkgs)
    if not picked:
        result["error"] = "No package.json found. Not a JS/TS frontend, or wrong root."
        print(json.dumps(result, ensure_ascii=False, indent=2) if as_json else result["error"])
        return 1

    pkg_path, pkg = picked
    base = pkg_path.parent
    result["frontend_dir"] = str(base.relative_to(root)) or "."
    result["package_name"] = pkg.get("name", "UNKNOWN")
    result["all_packages"] = [str(p.parent.relative_to(root)) or "." for p in pkgs][:15]
    result["package_manager"] = lockfile_pm(base, root)
    result["scripts"] = pkg.get("scripts", {})
    result["stack"] = detect_deps(pkg)

    # tsconfig
    ts = {}
    for name in ("tsconfig.json", "tsconfig.app.json"):
        t = base / name
        if t.exists():
            raw = re.sub(r"//.*|/\*.*?\*/", "", read_text(t), flags=re.S)
            ts["file"] = name
            ts["strict"] = bool(re.search(r'"strict"\s*:\s*true', raw))
            m = re.search(r'"paths"\s*:\s*\{(.*?)\}', raw, flags=re.S)
            if m:
                ts["paths"] = re.sub(r"\s+", " ", m.group(1)).strip()
            break
    result["typescript"] = ts or "UNKNOWN (no tsconfig)"

    # Tailwind / tokens
    tw = [n for n in ("tailwind.config.ts", "tailwind.config.js", "tailwind.config.cjs", "postcss.config.js") if (base / n).exists()]
    result["tailwind_config"] = tw or "NONE found (may be Tailwind v4 CSS-first: check global css for @theme)"

    # Source scan
    src_candidates = [base / "src", base / "app", base]
    src = next((c for c in src_candidates if c.exists()), base)
    result["src_dir"] = str(src.relative_to(root)) or "."
    result["tree"] = tree(root, src)

    comp_hits: dict[str, list[str]] = {k: [] for k in COMPONENT_KEYWORDS}
    i18n_files: list[str] = []
    perm_files: dict[str, int] = {}
    currency_files: dict[str, int] = {}
    rtl_signals: list[str] = []
    docs: list[str] = []
    feature_dirs: set[str] = set()
    ar_files = 0
    en_files = 0
    stats = {"files": 0, "tsx": 0, "jsx": 0}

    perm_re = re.compile(r"hasPermission|usePermission|useCan\b|can\(|permissions?\.(?:includes|has)|PermissionGate|RouteGuard", re.I)
    cur_re = re.compile(r"formatCurrency|formatMoney|formatPlanningMoney|useCompanyCurrency|currencyCode|baseCurrency|Intl\.NumberFormat", re.I)
    rtl_re = re.compile(r"""dir\s*=\s*["'{]\s*(rtl|isRtl|dir)|document\.dir|documentElement\.dir|direction\s*:\s*["']rtl""", re.I)

    for p in walk(root):
        rel = str(p.relative_to(root)).replace("\\", "/")
        name = p.name
        lower = name.lower()
        if lower in ("agents.md", "claude.md", "readme.md", "contributing.md") and p.parent in (root, base):
            docs.append(rel)
        if rel.startswith("docs/") and lower.endswith(".md") and len(docs) < 25:
            docs.append(rel)
        if rel.endswith(".erp-frontend/profile.md") or rel == ".erp-frontend/profile.md":
            result["existing_profile"] = rel
        if p.suffix not in SRC_EXT and not lower.endswith((".json", ".css")):
            continue
        if base not in p.parents and p.parent != base:
            continue

        if p.suffix in SRC_EXT:
            stats["files"] += 1
            if p.suffix == ".tsx":
                stats["tsx"] += 1
            if p.suffix == ".jsx":
                stats["jsx"] += 1
            stem = p.stem.lower()
            for label, pat in COMPONENT_KEYWORDS.items():
                if re.search(pat, stem) and any(k in rel.lower() for k in ("component", "shared", "ui", "common", "lib")):
                    if len(comp_hits[label]) < 4:
                        comp_hits[label].append(rel)
            m = re.search(r"features?/([^/]+)/", rel)
            if m:
                feature_dirs.add(m.group(1))
            text = read_text(p)
            if text:
                if perm_re.search(text):
                    perm_files[rel] = len(perm_re.findall(text))
                if cur_re.search(text):
                    currency_files[rel] = len(cur_re.findall(text))
                if rtl_re.search(text) and len(rtl_signals) < 5:
                    rtl_signals.append(rel)

        low_rel = rel.lower()
        if re.search(r"(^|/)(i18n|locales?|translations?|lang)(/|$)", low_rel):
            if re.search(r"(^|[/._-])ar([._-]|\.|/)", low_rel) or "/ar/" in low_rel:
                ar_files += 1
                if len(i18n_files) < 12:
                    i18n_files.append(rel)
            elif re.search(r"(^|[/._-])en([._-]|\.|/)", low_rel) or "/en/" in low_rel:
                en_files += 1

    result["shared_component_candidates"] = {k: v for k, v in comp_hits.items() if v}
    result["i18n"] = {"arabic_files_found": ar_files, "english_files_found": en_files, "samples": i18n_files}
    result["feature_folders"] = sorted(feature_dirs)[:40]
    result["permission_helpers_seen_in"] = [k for k, _ in sorted(perm_files.items(), key=lambda kv: -kv[1])[:6]]
    result["currency_or_format_helpers_seen_in"] = [k for k, _ in sorted(currency_files.items(), key=lambda kv: -kv[1])[:6]]
    result["rtl_direction_set_in"] = rtl_signals or "UNKNOWN (no dir=rtl found)"
    result["docs_to_read"] = sorted(set(docs))[:25]
    result["source_stats"] = stats
    result["unknown_so_ask_owner"] = [
        k for k, v in {
            "arabic_register": True,
            "digits (latn vs arab)": True,
            "calendar (gregorian/hijri)": True,
            "drawer_side": True,
            "regression/e2e registry": "testing" not in result["stack"],
            "idempotency + version fields in API": True,
        }.items() if v
    ]

    if as_json:
        print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0

    out = []
    out.append(f"# Project scan: {result['package_name']}")
    out.append(f"- root: {root}")
    out.append(f"- frontend dir: {result['frontend_dir']}   src: {result['src_dir']}   package manager: {result['package_manager']}")
    if len(result["all_packages"]) > 1:
        out.append(f"- other packages (monorepo?): {', '.join(result['all_packages'])}")
    if "existing_profile" in result:
        out.append(f"- EXISTING PROFILE FOUND: {result['existing_profile']} (read it first, then verify)")
    out.append("\n## Stack (from package.json)")
    for cat, hits in result["stack"].items():
        out.append(f"- {cat}: {', '.join(hits)}")
    out.append(f"- typescript: {result['typescript']}")
    out.append(f"- tailwind config: {result['tailwind_config']}")
    out.append("\n## Scripts")
    for k, v in result["scripts"].items():
        out.append(f"- {k}: `{v}`")
    out.append("\n## Folder tree (dirs only)")
    out.extend(result["tree"] or ["(none)"])
    out.append("\n## Feature folders")
    out.append(", ".join(result["feature_folders"]) or "none detected")
    out.append("\n## Shared component candidates (verify by opening)")
    for k, v in result["shared_component_candidates"].items():
        out.append(f"- {k}: {', '.join(v)}")
    if not result["shared_component_candidates"]:
        out.append("- none detected: the project may not have a shared library yet")
    out.append("\n## i18n")
    out.append(f"- Arabic files: {ar_files}, English files: {en_files}")
    for s in i18n_files:
        out.append(f"  - {s}")
    out.append("\n## Permissions / currency / direction signals")
    out.append(f"- permission logic seen in: {', '.join(result['permission_helpers_seen_in']) or 'none'}")
    out.append(f"- currency/format helpers seen in: {', '.join(result['currency_or_format_helpers_seen_in']) or 'none'}")
    out.append(f"- RTL set in: {result['rtl_direction_set_in'] if isinstance(result['rtl_direction_set_in'], str) else ', '.join(result['rtl_direction_set_in'])}")
    out.append("\n## Docs to read before coding")
    out.extend(f"- {d}" for d in result["docs_to_read"] or ["none found"])
    out.append("\n## UNKNOWN: ask the owner (max 3 questions) or confirm from code")
    out.extend(f"- {u}" for u in result["unknown_so_ask_owner"])
    print("\n".join(out))
    return 0


if __name__ == "__main__":
    sys.exit(main())
