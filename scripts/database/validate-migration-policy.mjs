import fs from "node:fs";
import path from "node:path";

const root = path.resolve("ops/database");
const destructive = /\b(?:drop\s+(?:table|column|schema)|truncate(?:\s+table)?|alter\s+type)\b/gi;
const findings = [];

for (const file of collectSql(root)) {
  const text = fs.readFileSync(file, "utf8");
  for (const match of text.matchAll(destructive)) {
    const line = text.slice(0, match.index).split("\n").length;
    findings.push(`${path.relative(process.cwd(), file)}:${line}: destructive schema change requires an expand/contract forward-fix`);
  }
}

if (findings.length > 0) {
  console.error(findings.join("\n"));
  process.exitCode = 1;
} else {
  console.log(`Validated ${collectSql(root).length} SQL files for expand/contract migration safety.`);
}

function collectSql(directory) {
  if (!fs.existsSync(directory)) return [];
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
    const file = path.join(directory, entry.name);
    return entry.isDirectory() ? collectSql(file) : entry.name.endsWith(".sql") ? [file] : [];
  });
}
