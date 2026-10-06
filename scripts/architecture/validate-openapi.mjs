import fs from "node:fs";
const file = "docs/api/platform-openapi.json";
const document = JSON.parse(fs.readFileSync(file, "utf8"));
if (document.openapi !== "3.1.0" || !document.info?.title || !document.paths || Object.keys(document.paths).length === 0) {
  throw new Error(`${file}: invalid OpenAPI contract`);
}
console.log(`Validated OpenAPI ${document.openapi} contract with ${Object.keys(document.paths).length} paths.`);
