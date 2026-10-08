import path from "node:path";
import { validateFrontendArchitecture, validateFrontendModuleBoundaries } from "./frontend-module-boundary.mjs";

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

// Check Platform importing module internals
const platformFixtureRoot = path.resolve("tests/architecture/fixtures/frontend/platform-imports-module");
const platformResult = validateFrontendArchitecture({
  rootDir: platformFixtureRoot,
  modulesRoot: path.join(platformFixtureRoot, "modules")
});
if (platformResult.violations.length === 0) {
  throw new Error("Platform importing module internal was not detected.");
}

// Check Circular dependencies between modules
const circularFixtureRoot = path.resolve("tests/architecture/fixtures/frontend/circular");
const circularResult = validateFrontendArchitecture({
  rootDir: circularFixtureRoot,
  modulesRoot: path.join(circularFixtureRoot, "modules")
});
if (circularResult.cycles.length === 0) {
  throw new Error("Circular frontend module dependency was not detected.");
}

// Check Forbidden Commercial / Paid package
const paidPkgFixtureRoot = path.resolve("tests/architecture/fixtures/frontend/forbidden-paid-package");
const paidPkgResult = validateFrontendArchitecture({
  rootDir: paidPkgFixtureRoot,
  modulesRoot: path.join(paidPkgFixtureRoot, "modules")
});
if (!paidPkgResult.violations.some(v => v.includes("Free-Only Core Dependency Rule"))) {
  throw new Error("Forbidden paid package import was not detected.");
}

console.log("Frontend architecture fixture verification passed all tests:");
console.log("- Allowed public imports: PASSED");
console.log("- Forbidden internal imports: REJECTED (PASS)");
console.log("- Alias internal imports: REJECTED (PASS)");
console.log("- Platform importing module internal: REJECTED (PASS)");
console.log("- Circular module dependency: REJECTED (PASS)");
console.log("- Forbidden paid package: REJECTED (PASS)");
