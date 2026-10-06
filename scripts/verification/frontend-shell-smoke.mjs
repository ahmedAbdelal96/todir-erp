import fs from "node:fs";

const html = fs.readFileSync("src/frontend/apps/todir-web/index.html", "utf8");
const entry = fs.readFileSync("src/frontend/apps/todir-web/src/main.tsx", "utf8");
const required = ["dir=\"rtl\"", "id=\"root\"", "src/main.tsx"];
const missing = required.filter(value => !html.includes(value));
if (!entry.includes("createRoot") || missing.length > 0) {
  throw new Error(`Frontend shell contract failed: ${missing.join(", ") || "root bootstrap missing"}`);
}
console.log("Validated RTL frontend shell bootstrap.");
