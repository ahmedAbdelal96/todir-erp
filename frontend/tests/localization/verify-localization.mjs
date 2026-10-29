import { readdir, readFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { parse } from "@babel/parser";

const frontendRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../..");
const repositoryRoot = path.resolve(frontendRoot, "..");
const resourcePairs = [
  ["core/i18n/resources/ar/common.ts", "core/i18n/resources/en/common.ts"],
  ["core/i18n/resources/ar/navigation.ts", "core/i18n/resources/en/navigation.ts"],
  ["core/i18n/resources/ar/validation.ts", "core/i18n/resources/en/validation.ts"],
  ["core/i18n/resources/ar/workspace.ts", "core/i18n/resources/en/workspace.ts"],
  ...["accounting", "catalog", "directory", "finbase", "identity", "invoicing", "migration", "payments", "pricing", "tax", "subledger", "inventory", "sales"]
    .map((module) => [`modules/${module}/i18n/ar.ts`, `modules/${module}/i18n/en.ts`])
];
const excludedCopyPaths = /(?:\.test\.|\.stories\.|\.story\.|\/tests\/|\/stories\/|\/__fixtures__\/|\/i18n\/)/;
const technicalJsxAttributes = new Set([
  "className", "id", "type", "role", "name", "value", "href", "to", "target", "rel", "method", "action",
  "autoComplete", "inputMode", "pattern", "htmlFor", "tabIndex", "data-testid", "data-test-id", "key", "style",
  "dir", "lang", "draggable", "spellCheck", "translate", "contentEditable", "width", "height", "min", "max",
  "step", "maxLength", "minLength", "rows", "cols", "size", "colSpan", "rowSpan", "variant", "color", "tone",
  "as", "align", "justify", "position", "placement", "animation", "orientation", "status", "state", "density",
  "aria-live", "aria-hidden", "aria-current", "aria-pressed", "aria-expanded", "aria-describedby", "aria-controls", "aria-selected", "aria-invalid", "aria-required", "aria-busy", "aria-modal", "aria-haspopup"
]);
const copyAttributePattern = /^(?:aria-.*|.*(?:label|title|description|placeholder|message|hint|tooltip|empty|loading|error|success|warning|confirm|summary|caption|heading|subtitle|helper|announcement|statusText).*)$/i;
const copyPropertyPattern = /^(?:label|title|description|placeholder|message|hint|tooltip|emptyMessage|loadingMessage|errorMessage|successMessage|warningMessage|confirmText|cancelText|summary|caption|heading|subtitle|helperText|accessibilityLabel|ariaLabel|emptyTitle|errorTitle|loadingText)$/i;
const allowedBusinessValuePatterns = [
  /^شركة الأفق للتجارة(?: والمقاولات| العامة ذ\.م\.م)?$/,
  /^\+?[0-9 ()-]+$/,
  /^[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}$/i,
  /^\([A-Z]{2,4}\)$/,
  /^[a-z]+(?:[A-Z][a-zA-Z0-9]*)+$/,
  /^https?:\/\/$/
];
const nonCopyLiterals = new Set(["TODIR", "T", "✓", "✕", "⚠️", "ℹ️", "—", "|", "/", "*", "(", ")", "%", "…", "...", "0", "0.00", "name@company.com", "••••••••••••", "300123456700003", ":", "·", "#", "○", "📁", "🎉", "📄", "📥", "⚡", "⏳", "✅", "party"]);

function walk(node, visit, ancestors = []) {
  if (!node || typeof node !== "object") return;
  if (typeof node.type === "string") visit(node, ancestors);
  const next = typeof node.type === "string" ? [...ancestors, node] : ancestors;
  for (const [key, value] of Object.entries(node)) {
    if (key === "loc" || key === "start" || key === "end" || key === "comments" || key === "tokens") continue;
    if (Array.isArray(value)) value.forEach((child) => walk(child, visit, next));
    else if (value && typeof value === "object") walk(value, visit, next);
  }
}

function objectKeys(node, prefix = "") {
  if (!node || node.type !== "ObjectExpression") return [];
  const result = [];
  for (const property of node.properties) {
    if (property.type !== "ObjectProperty" || property.computed) continue;
    const key = property.key.type === "Identifier" ? property.key.name : property.key.value;
    if (typeof key !== "string") continue;
    const fullKey = prefix ? `${prefix}.${key}` : key;
    if (property.value.type === "ObjectExpression") result.push(...objectKeys(property.value, fullKey));
    else result.push(fullKey);
  }
  return result;
}

export async function checkResourceParity() {
  const issues = [];
  for (const [arPath, enPath] of resourcePairs) {
    const [arSource, enSource] = await Promise.all([
      readFile(path.join(frontendRoot, arPath), "utf8"),
      readFile(path.join(frontendRoot, enPath), "utf8")
    ]);
    const arAst = parse(arSource, { sourceType: "module", plugins: ["typescript"] });
    const enAst = parse(enSource, { sourceType: "module", plugins: ["typescript"] });
    const arObject = arAst.program.body[0]?.declaration?.declarations?.[0]?.init?.expression ?? arAst.program.body[0]?.declaration?.declarations?.[0]?.init;
    const enObject = enAst.program.body[0]?.declaration?.declarations?.[0]?.init?.expression ?? enAst.program.body[0]?.declaration?.declarations?.[0]?.init;
    const arKeys = objectKeys(arObject).sort();
    const enKeys = objectKeys(enObject).sort();
    const missingEnglish = arKeys.filter((key) => !enKeys.includes(key));
    const missingArabic = enKeys.filter((key) => !arKeys.includes(key));
    for (const key of missingEnglish) issues.push(`${arPath}: missing English key ${key}`);
    for (const key of missingArabic) issues.push(`${enPath}: missing Arabic key ${key}`);
    walk(arObject, (node) => {
      if (node.type === "StringLiteral" && !node.value.trim()) issues.push(`${arPath}:${node.loc?.start.line ?? 0}: empty Arabic translation`);
    });
    walk(enObject, (node) => {
      if (node.type === "StringLiteral" && !node.value.trim()) issues.push(`${enPath}:${node.loc?.start.line ?? 0}: empty English translation`);
    });
  }
  return issues;
}

function visibleLiteral(node, ancestors) {
  if (node.type === "JSXText") {
    const value = node.value.trim();
    if (!value || /^(?:TODIR|[T✓*%—|…✕+@()▲▼]+|\/$|\([A-Z]{2,4}\))$/u.test(value) || /^[\p{P}\p{S}\s]+$/u.test(value)) return false;
    return true;
  }
  if (node.type !== "StringLiteral" && node.type !== "TemplateLiteral") return false;
  if (node.type === "StringLiteral" && (nonCopyLiterals.has(node.value.trim()) || allowedBusinessValuePatterns.some((pattern) => pattern.test(node.value.trim())))) return false;
  if (node.type === "StringLiteral" && nonCopyLiterals.has(node.value.trim())) return false;
  if (node.type === "StringLiteral" && /^(?:actions|partyRecord|common|navigation|validation|workspace)\./.test(node.value.trim())) return false;
  if (node.type === "TemplateLiteral" && node.quasis.every((part) => /^[\s·—:|/#*()[\]{}.,…]*$/.test(part.value.cooked ?? ""))) return false;
  if (node.type === "TemplateLiteral" && node.quasis.every((part) => /^(?:0\.)?$/.test(part.value.cooked ?? "")) && node.expressions.length <= 1) return false;
  if (node.type === "TemplateLiteral" && node.quasis.some((part) => (part.value.cooked ?? "").includes("ui-datagrid__"))) return false;
  if (ancestors.some((item) => item.type === "CallExpression" &&
    ((item.callee?.type === "Identifier" && ["t", "translate"].includes(item.callee.name)) ||
      (item.callee?.type === "MemberExpression" && item.callee.property?.name === "t")))) return false;
  const expressionParent = ancestors.at(-1);
  if (expressionParent?.type === "CallExpression" &&
    ((expressionParent.callee?.type === "Identifier" && /^(?:set(?:Error|Warning|Message|Notice|DuplicateWarning)|showError|showWarning)$/.test(expressionParent.callee.name)) ||
      (expressionParent.callee?.type === "MemberExpression" && /^(?:setError|setWarning|showError|showWarning)$/.test(expressionParent.callee.property?.name ?? "")))) return true;
  const isCopyFormatter = (name) => typeof name === "string" && /(?:Label|Title|Description|Message|Text|Reason|Action|Actor|Entity)$/.test(name);
  const formatter = [...ancestors].reverse().find((item) =>
    (item.type === "FunctionDeclaration" || item.type === "FunctionExpression") && isCopyFormatter(item.id?.name) ||
    item.type === "VariableDeclarator" && item.init?.type === "ArrowFunctionExpression" && isCopyFormatter(item.id?.name)
  );
  if (formatter && ancestors.some((item) => item.type === "ReturnStatement")) return true;
  const jsxAttribute = [...ancestors].reverse().find((item) => item.type === "JSXAttribute");
  if (jsxAttribute) {
    const name = jsxAttribute.name?.name;
    if (typeof name !== "string" || technicalJsxAttributes.has(name) || !copyAttributePattern.test(name)) return false;
    if (node.type === "StringLiteral" && ancestors.at(-1)?.type === "JSXAttribute") {
      return !allowedBusinessValuePatterns.some((pattern) => pattern.test(node.value.trim())) && !nonCopyLiterals.has(node.value.trim());
    }
    const parent = ancestors.at(-1);
    return (parent?.type === "ConditionalExpression" && (parent.consequent === node || parent.alternate === node)) ||
      (parent?.type === "LogicalExpression" && parent.right === node) ||
      (node.type === "TemplateLiteral" && node.quasis.some((part) => part.value.cooked?.trim()));
  }
  if (node.type === "StringLiteral") {
    const value = node.value.trim();
    if (!value || nonCopyLiterals.has(value) || /^(?:[a-zA-Z][\w-]*(?::[\w.-]+|(?:\.[\w-]+)+))$/.test(value) || allowedBusinessValuePatterns.some((pattern) => pattern.test(value))) return false;
    const prop = [...ancestors].reverse().find((item) => item.type === "ObjectProperty");
    if (prop) {
      const key = prop.key?.name ?? prop.key?.value;
      return typeof key === "string" && copyPropertyPattern.test(key);
    }
  }
  const expressionIndex = ancestors.findLastIndex((item) => item.type === "JSXExpressionContainer");
  if (expressionIndex < 0) return false;
  const parent = ancestors.at(-1);
  if (node.type === "TemplateLiteral") return node.quasis.some((part) => part.value.cooked?.trim());
  return parent?.type === "JSXExpressionContainer" ||
    (parent?.type === "ConditionalExpression" && (parent.consequent === node || parent.alternate === node)) ||
    (parent?.type === "LogicalExpression" && parent.right === node);
}

async function sourceFiles(directory) {
  const files = [];
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const target = path.join(directory, entry.name);
    if (entry.isDirectory()) files.push(...await sourceFiles(target));
    else if (/\.(?:ts|tsx)$/.test(entry.name) && !excludedCopyPaths.test(target.replaceAll("\\", "/"))) files.push(target);
  }
  return files;
}

export async function findHardcodedCopy() {
  const issues = [];
  const roots = ["app", "core", "components", "modules"];
  for (const root of roots) {
    for (const file of await sourceFiles(path.join(frontendRoot, root))) {
      const source = await readFile(file, "utf8");
      let ast;
      try {
        ast = parse(source, { sourceType: "module", plugins: ["typescript", "jsx"] });
      } catch (error) {
        issues.push(`${path.relative(repositoryRoot, file)}: parse error: ${error.message}`);
        continue;
      }
      walk(ast, (node, ancestors) => {
        // This schema diagnostic is consumed by developer tooling only; no application surface renders it.
        if (file.endsWith(`${path.sep}theme-definition.ts`) && node.type === "StringLiteral" && node.value === "Color must be a valid 3/6-digit hex string or oklch(...) format") return;
        if (visibleLiteral(node, ancestors)) {
          const value = node.type === "JSXText" ? node.value.trim() : node.type === "StringLiteral" ? node.value : node.quasis.map((part) => part.value.cooked).join("{{value}}").trim();
          issues.push(`${path.relative(repositoryRoot, file)}:${node.loc?.start.line ?? 0}: ${JSON.stringify(value)}`);
        }
      });
    }
  }
  return issues;
}

export async function verifyLocalization() {
  return [...await checkResourceParity(), ...await findHardcodedCopy()];
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  const issues = await verifyLocalization();
  if (issues.length) {
    console.error(`Localization verification failed (${issues.length} issue(s)):`);
    for (const issue of issues) console.error(`- ${issue}`);
    process.exitCode = 1;
  } else {
    console.log("Localization verification passed: resource parity and hardcoded-copy guard are clean.");
  }
}
