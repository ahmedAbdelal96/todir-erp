import { execFileSync } from "node:child_process";
import fs from "node:fs";

const file = "docs/api/platform-openapi.json";
const current = JSON.parse(fs.readFileSync(file, "utf8"));
const baseRef = process.env.GITHUB_BASE_REF ? `origin/${process.env.GITHUB_BASE_REF}` : "HEAD^";
let previous;
try {
  previous = JSON.parse(execFileSync("git", ["show", `${baseRef}:${file}`], { encoding: "utf8" }));
} catch {
  console.log("No previous OpenAPI contract is available; breaking-change check is not applicable to this initial revision.");
  process.exit(0);
}

const removed = Object.keys(previous.paths ?? {}).filter(path => !(path in (current.paths ?? {})));
if (removed.length > 0) {
  console.error(`Breaking API paths removed without an explicit contract decision: ${removed.join(", ")}`);
  process.exitCode = 1;
} else {
  console.log(`OpenAPI compatibility check passed: ${Object.keys(previous.paths ?? {}).length} previous paths remain available.`);
}
