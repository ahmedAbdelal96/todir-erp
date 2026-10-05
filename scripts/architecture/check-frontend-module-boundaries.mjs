import path from "node:path";
import { validateFrontendModuleBoundaries } from "./frontend-module-boundary.mjs";

const modulesRoot = path.resolve(process.argv[2] ?? "src/frontend/apps/todir-web/src/modules");
const violations = validateFrontendModuleBoundaries(modulesRoot);

if (violations.length > 0) {
  console.error(`Frontend module boundary violations in ${modulesRoot}:`);
  for (const violation of violations) {
    console.error(`- ${violation}`);
  }

  process.exitCode = 1;
} else {
  console.log(`Frontend module boundaries are valid in ${modulesRoot}.`);
}
