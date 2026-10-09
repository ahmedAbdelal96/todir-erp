import fs from "node:fs";
import path from "node:path";

const files = [
  "ops/database/bootstrap/tenant-boundary.sql",
  "ops/database/bootstrap/organization-schema.sql",
  "ops/database/bootstrap/identity-membership-schema.sql",
  "ops/database/bootstrap/platform-kernel-schema.sql",
  "ops/database/bootstrap/sample-schema.sql",
  "ops/database/bootstrap/directory-schema.sql",
  "ops/database/bootstrap/catalog-schema.sql",
];
const errors = [];
for (const relative of files) {
  const full = path.resolve(relative);
  if (!fs.existsSync(full)) { errors.push(`${relative}: missing`); continue; }
  const sql = fs.readFileSync(full, "utf8").toLowerCase();
  if (!sql.includes("enable row level security")) errors.push(`${relative}: missing RLS enablement`);
  if (!sql.includes("force row level security")) errors.push(`${relative}: missing FORCE RLS`);
  if (!sql.includes("tenant_isolation")) errors.push(`${relative}: missing tenant isolation policy`);
}
const runtimeRoles = fs.readFileSync(path.resolve("ops/database/bootstrap/runtime-roles-and-schemas.sql"), "utf8").toLowerCase();
if (!runtimeRoles.includes("bypassrls")) errors.push("runtime roles: expected BYPASSRLS guard");
if (errors.length) { console.error(errors.join("\n")); process.exitCode = 1; } else console.log(`Validated RLS and runtime-role policy for ${files.length} bootstrap scripts.`);
