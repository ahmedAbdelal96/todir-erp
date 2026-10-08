import path from "node:path";
import { validateFrontendArchitecture } from "./frontend-module-boundary.mjs";

const rootDir = path.resolve(process.argv[2] ?? "src/frontend/apps/todir-web/src");
const result = validateFrontendArchitecture({ rootDir });

if (result.violations.length > 0) {
  console.error(`Frontend architecture violations in ${rootDir}:`);
  for (const violation of result.violations) {
    console.error(`- ${violation}`);
  }

  process.exitCode = 1;
} else {
  console.log(`Frontend architecture boundaries are valid in ${rootDir}.`);
}
