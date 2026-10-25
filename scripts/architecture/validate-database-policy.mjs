import fs from "node:fs";
import path from "node:path";

const files = [
  "backend/DbMigrator/Database/bootstrap/platform-kernel-schema.sql",
  "backend/DbMigrator/Database/bootstrap/platform-rls-finalization.sql",
  "backend/DbMigrator/Database/bootstrap/sample-schema.sql",
  "backend/DbMigrator/Database/bootstrap/directory-schema.sql",
  "backend/DbMigrator/Database/bootstrap/catalog-schema.sql",
  "backend/DbMigrator/Database/bootstrap/pricing-schema.sql",
  "backend/DbMigrator/Database/bootstrap/subledger-schema.sql",
];
const errors = [];
for (const relative of files) {
  const full = path.resolve(relative);
  if (!fs.existsSync(full)) { errors.push(`${relative}: missing`); continue; }
  const sql = fs.readFileSync(full, "utf8").toLowerCase();
  if (relative.endsWith("platform-rls-finalization.sql")) {
    if (!sql.includes("force row level security")) errors.push(`${relative}: missing FORCE RLS`);
    continue;
  }
  if (!sql.includes("enable row level security")) errors.push(`${relative}: missing RLS enablement`);
  if (!sql.includes("tenant_isolation")) errors.push(`${relative}: missing tenant isolation policy`);
}
const runtimeRoles = fs.readFileSync(path.resolve("backend/DbMigrator/Database/bootstrap/runtime-roles-and-schemas.sql"), "utf8").toLowerCase();
if (!runtimeRoles.includes("bypassrls")) errors.push("runtime roles: expected BYPASSRLS guard");
if (errors.length) { console.error(errors.join("\n")); process.exitCode = 1; } else console.log(`Validated RLS and runtime-role policy for ${files.length} bootstrap scripts.`);
