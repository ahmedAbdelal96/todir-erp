import path from "node:path";
import { validateFrontendModuleBoundaries } from "./frontend-module-boundary.mjs";

const allowedRoot = path.resolve("tests/architecture/fixtures/frontend/allowed/modules");
const forbiddenRoot = path.resolve("tests/architecture/fixtures/frontend/forbidden/modules");
const aliasRoot = path.resolve("tests/architecture/fixtures/frontend/alias/modules");
const allowedViolations = validateFrontendModuleBoundaries(allowedRoot);
const forbiddenViolations = validateFrontendModuleBoundaries(forbiddenRoot);
const aliasViolations = validateFrontendModuleBoundaries(aliasRoot);

if (allowedViolations.length > 0) {
  throw new Error(`Allowed public import fixture was rejected: ${allowedViolations.join("; ")}`);
}

if (forbiddenViolations.length === 0) {
  throw new Error("Forbidden cross-module internal import fixture was not detected.");
}

if (aliasViolations.length === 0) {
  throw new Error("Alias-based cross-module internal import fixture was not detected.");
}

console.log("Frontend fixture validation detected relative and alias internal imports and accepted the public import.");
