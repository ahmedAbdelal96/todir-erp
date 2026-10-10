import fs from "node:fs";

const html = fs.readFileSync("frontend/index.html", "utf8");
const entry = fs.readFileSync("frontend/main.tsx", "utf8");
const shell = fs.readFileSync("frontend/app/shell/app-shell.tsx", "utf8");
const required = ["dir=\"rtl\"", "id=\"root\"", "main.tsx"];
const shellRequired = ["/api/v1/auth/login", "/api/v1/context/tenant", "ShellErrorBoundary", "forbidden", "operation-progress", "mfaCode"];
const missing = required.filter(value => !html.includes(value));
const missingShell = shellRequired.filter(value => !shell.includes(value));
if (!entry.includes("createRoot") || missing.length > 0 || missingShell.length > 0 || shell.includes("localStorage")) {
  throw new Error(`Frontend shell contract failed: ${[...missing, ...missingShell, ...(shell.includes("localStorage") ? ["no-local-storage"] : [])].join(", ") || "root bootstrap missing"}`);
}
console.log("Validated RTL frontend shell authentication, context, permission, error, and storage boundaries.");
