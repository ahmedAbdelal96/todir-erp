import fs from "node:fs";
const files = ["docs/api/platform-openapi.json", "docs/api/ledger-openapi.json", "docs/api/subledger-openapi.json", "docs/api/invoicing-openapi.json"];
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
  const validateReferences = (value, location = "#") => {
    if (!value || typeof value !== "object") return;
    for (const [key, nested] of Object.entries(value)) {
      if (key === "$ref" && typeof nested === "string" && nested.startsWith("#/")) {
        const resolved = nested.slice(2).split("/").map(part => part.replaceAll("~1", "/").replaceAll("~0", "~"))
          .reduce((target, part) => target?.[part], document);
        if (resolved === undefined) throw new Error(`${file}: unresolved local reference '${nested}' at ${location}`);
      } else {
        validateReferences(nested, `${location}/${key}`);
      }
    }
  };
  validateReferences(document);
  console.log(`Validated OpenAPI ${document.openapi} contract in ${file} with ${Object.keys(document.paths).length} paths.`);
}
