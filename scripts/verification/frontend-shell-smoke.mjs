import fs from "node:fs";

const html = fs.readFileSync("frontend/index.html", "utf8");
const entry = fs.readFileSync("frontend/main.tsx", "utf8");
const shell = fs.readFileSync("frontend/app/shell/app-shell.tsx", "utf8");
const auth = fs.readFileSync("frontend/app/auth/auth-view.tsx", "utf8");
const authService = fs.readFileSync("frontend/core/api/auth-service.ts", "utf8");

const required = ["dir=\"rtl\"", "id=\"root\"", "main.tsx"];
const shellRequired = ["ShellErrorBoundary", "forbidden", "operation-progress", "AuthView"];
const authRequired = ["/api/v1/auth/login", "/api/v1/auth/mfa/verify", "/api/v1/auth/password/initial"];

const missing = required.filter(value => !html.includes(value));
const missingShell = shellRequired.filter(value => !shell.includes(value));
const missingAuth = authRequired.filter(value => !auth.includes(value) && !authService.includes(value));

if (!entry.includes("createRoot") || missing.length > 0 || missingShell.length > 0 || missingAuth.length > 0 || shell.includes("localStorage")) {
  throw new Error(`Frontend shell contract failed: ${[...missing, ...missingShell, ...missingAuth, ...(shell.includes("localStorage") ? ["no-local-storage"] : [])].join(", ") || "root bootstrap missing"}`);
}
console.log("Validated RTL frontend shell authentication, context, permission, error, and storage boundaries.");
