import { execFileSync } from "node:child_process";
import fs from "node:fs";

const files = ["docs/api/platform-openapi.json", "docs/api/ledger-openapi.json", "docs/api/subledger-openapi.json", "docs/api/invoicing-openapi.json", "docs/api/payments-openapi.json"];
const baseRef = process.env.GITHUB_BASE_REF ? `origin/${process.env.GITHUB_BASE_REF}` : "HEAD^";
let checkedContracts = 0;
for (const file of files) {
  const current = JSON.parse(fs.readFileSync(file, "utf8"));
  let previous;
  try {
    execFileSync("git", ["cat-file", "-e", `${baseRef}:${file}`], { stdio: "ignore" });
    previous = JSON.parse(execFileSync("git", ["show", `${baseRef}:${file}`], { encoding: "utf8" }));
  } catch {
    console.log(`${file}: no previous contract; initial contract check is not applicable.`);
    continue;
  }

  const removed = Object.keys(previous.paths ?? {}).filter(path => !(path in (current.paths ?? {})));
  if (removed.length > 0) {
    console.error(`${file}: API paths removed without an explicit contract decision: ${removed.join(", ")}`);
    process.exitCode = 1;
  } else {
    checkedContracts++;
    console.log(`${file}: compatibility check passed; all ${Object.keys(previous.paths ?? {}).length} previous paths remain available.`);
  }
}

if (checkedContracts === 0 && !process.exitCode)
  console.log("No previous OpenAPI contract is available; breaking-change check is not applicable to this initial revision.");
