import fs from "node:fs";
import path from "node:path";

const root = process.cwd();
const contractPath = path.join(root, "docs/api/platform-openapi.json");
const outputPath = path.join(root, "src/frontend/src/platform/api/platform-client.ts");
const contract = JSON.parse(fs.readFileSync(contractPath, "utf8"));
const paths = Object.keys(contract.paths ?? {}).sort();
if (paths.length === 0) throw new Error("OpenAPI contract has no paths");

const content = `// Generated from docs/api/platform-openapi.json. Do not edit manually.\nexport const platformApiPaths = ${JSON.stringify(paths, null, 2)} as const;\n\nexport type PlatformApiPath = (typeof platformApiPaths)[number];\n`;
const checkOnly = process.argv.includes("--check");
if (checkOnly) {
  const current = fs.existsSync(outputPath) ? fs.readFileSync(outputPath, "utf8") : "";
  if (current !== content) {
    console.error(`${path.relative(root, outputPath)} is stale; run pnpm api:generate`);
    process.exitCode = 1;
  } else {
    console.log(`Validated generated client for ${paths.length} API paths.`);
  }
} else {
  fs.mkdirSync(path.dirname(outputPath), { recursive: true });
  fs.writeFileSync(outputPath, content);
  console.log(`Generated client for ${paths.length} API paths.`);
}
