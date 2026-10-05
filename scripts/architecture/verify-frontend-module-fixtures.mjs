import path from "node:path";
import { validateFrontendModuleBoundaries } from "./frontend-module-boundary.mjs";

const allowedRoot = path.resolve("tests/architecture/fixtures/frontend/allowed/modules");
const forbiddenRoot = path.resolve("tests/architecture/fixtures/frontend/forbidden/modules");
const allowedViolations = validateFrontendModuleBoundaries(allowedRoot);
const forbiddenViolations = validateFrontendModuleBoundaries(forbiddenRoot);

if (allowedViolations.length > 0) {
  throw new Error(`Allowed public import fixture was rejected: ${allowedViolations.join("; ")}`);
}

if (forbiddenViolations.length === 0) {
  throw new Error("Forbidden cross-module internal import fixture was not detected.");
}

console.log("Frontend fixture validation detected the forbidden internal import and accepted the public import.");
