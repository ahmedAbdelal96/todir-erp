import fs from "node:fs";
const files = ["docs/api/platform-openapi.json", "docs/api/ledger-openapi.json", "docs/api/subledger-openapi.json"];
for (const file of files) {
  const document = JSON.parse(fs.readFileSync(file, "utf8"));
  if (document.openapi !== "3.1.0" || !document.info?.title || !document.paths || Object.keys(document.paths).length === 0) {
    throw new Error(`${file}: invalid OpenAPI contract`);
  }
  for (const [path, item] of Object.entries(document.paths)) {
    for (const [method, operation] of Object.entries(item)) {
      if (!/^(get|post|put|patch|delete|options|head|trace)$/i.test(method)) continue;
      if (operation.requestBody && !operation.requestBody.$ref && !operation.requestBody.content) throw new Error(`${file}: ${method.toUpperCase()} ${path} has a request body without content`);
      if (operation.requestBody?.$ref && !operation.requestBody.$ref.startsWith("#/components/requestBodies/")) throw new Error(`${file}: ${method.toUpperCase()} ${path} has an unsupported request body reference`);
    }
  }
  console.log(`Validated OpenAPI ${document.openapi} contract in ${file} with ${Object.keys(document.paths).length} paths.`);
}
