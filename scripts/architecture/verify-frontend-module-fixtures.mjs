import path from "node:path";
import { validateFrontendArchitecture, validateFrontendModuleBoundaries } from "./frontend-module-boundary.mjs";

const allowedRoot = path.resolve("backend/Tests/Architecture/fixtures/frontend/allowed/modules");
const forbiddenRoot = path.resolve("backend/Tests/Architecture/fixtures/frontend/forbidden/modules");
const aliasRoot = path.resolve("backend/Tests/Architecture/fixtures/frontend/alias/modules");

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

// Check Core importing module internals
const coreFixtureRoot = path.resolve("backend/Tests/Architecture/fixtures/frontend/core-imports-module");
const coreResult = validateFrontendArchitecture({
  rootDir: coreFixtureRoot,
  modulesRoot: path.join(coreFixtureRoot, "modules")
});
if (coreResult.violations.length === 0) {
  throw new Error("Core importing module internal was not detected.");
}

// Check Circular dependencies between modules
const circularFixtureRoot = path.resolve("backend/Tests/Architecture/fixtures/frontend/circular");
const circularResult = validateFrontendArchitecture({
  rootDir: circularFixtureRoot,
  modulesRoot: path.join(circularFixtureRoot, "modules")
});
if (circularResult.cycles.length === 0) {
  throw new Error("Circular frontend module dependency was not detected.");
}

// Check Forbidden Commercial / Paid package
const paidPkgFixtureRoot = path.resolve("backend/Tests/Architecture/fixtures/frontend/forbidden-paid-package");
const paidPkgResult = validateFrontendArchitecture({
  rootDir: paidPkgFixtureRoot,
  modulesRoot: path.join(paidPkgFixtureRoot, "modules")
});
if (!paidPkgResult.violations.some(v => v.includes("Free-Only Core Dependency Rule"))) {
  throw new Error("Forbidden paid package import was not detected.");
}

// Check Shared Components importing any business-module surface
const sharedComponentsFixtureRoot = path.resolve("backend/Tests/Architecture/fixtures/frontend/shared-components-imports-module");
const sharedComponentsResult = validateFrontendArchitecture({
  rootDir: sharedComponentsFixtureRoot,
  modulesRoot: path.join(sharedComponentsFixtureRoot, "modules")
});
if (!sharedComponentsResult.violations.some(v => v.includes("Shared Components must not depend on business module"))) {
  throw new Error("Shared Components importing a business module was not detected.");
}

// Check Business Module directly importing AG Grid engine
const moduleImportsGridRoot = path.resolve("backend/Tests/Architecture/fixtures/frontend/module-imports-grid");
const moduleImportsGridResult = validateFrontendArchitecture({
  rootDir: moduleImportsGridRoot,
  modulesRoot: path.join(moduleImportsGridRoot, "modules")
});
if (!moduleImportsGridResult.violations.some(v => v.includes("Business modules must consume the shared-components DataGrid wrapper"))) {
  throw new Error("Business module directly importing AG Grid was not detected.");
}

// Check Business Module directly importing React Aria
const moduleImportsReactAriaRoot = path.resolve("backend/Tests/Architecture/fixtures/frontend/module-imports-react-aria");
const moduleImportsReactAriaResult = validateFrontendArchitecture({
  rootDir: moduleImportsReactAriaRoot,
  modulesRoot: path.join(moduleImportsReactAriaRoot, "modules")
});
if (!moduleImportsReactAriaResult.violations.some(v => v.includes("Business modules must consume approved shared-components primitives"))) {
  throw new Error("Business module directly importing React Aria was not detected.");
}

console.log("Frontend architecture fixture verification passed all tests:");
console.log("- Allowed public imports: PASSED");
console.log("- Forbidden internal imports: REJECTED (PASS)");
console.log("- Alias internal imports: REJECTED (PASS)");
console.log("- Core importing module internal: REJECTED (PASS)");
console.log("- Circular module dependency: REJECTED (PASS)");
console.log("- Forbidden paid package: REJECTED (PASS)");
console.log("- Shared Components importing business module: REJECTED (PASS)");
console.log("- Business module directly importing AG Grid: REJECTED (PASS)");
console.log("- Business module directly importing React Aria: REJECTED (PASS)");
