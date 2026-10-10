import fs from "node:fs";
import path from "node:path";

const roots = process.argv.slice(2).length > 0 ? process.argv.slice(2) : ["backend"];
const manifests = roots.flatMap(root => collect(path.resolve(root)));
const errors = [];
const modules = new Map();
const schema = JSON.parse(fs.readFileSync(path.resolve("docs/platform/module-manifest-schema.json"), "utf8"));
for (const file of manifests) {
  let manifest;
  try { manifest = JSON.parse(fs.readFileSync(file, "utf8")); } catch (error) { errors.push(`${file}: invalid JSON`); continue; }
  for (const required of schema.required ?? []) if (!(required in manifest)) errors.push(`${file}: missing required property '${required}'`);
  const allowed = new Set(["$schema", ...Object.keys(schema.properties ?? {})]);
  for (const property of Object.keys(manifest)) if (!allowed.has(property)) errors.push(`${file}: unknown property '${property}'`);
  if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(manifest.id ?? "")) errors.push(`${file}: invalid id`);
  if (!/^\d+\.\d+\.\d+$/.test(manifest.version ?? "")) errors.push(`${file}: invalid version`);
  if (!Number.isInteger(manifest.contractsVersion) || manifest.contractsVersion < 1) errors.push(`${file}: invalid contractsVersion`);
  if (!/^L[0-6]$/.test(manifest.layer ?? "")) errors.push(`${file}: invalid layer`);
  for (const [pair, firstKey, secondKey] of [["dependencies", "hard", "optionalCapabilities"], ["capabilities", "provides", "consumes"], ["messaging", "publishes", "consumes"]]) {
    if (!manifest[pair] || !Array.isArray(manifest[pair][firstKey]) || !Array.isArray(manifest[pair][secondKey])) errors.push(`${file}: invalid ${pair}`);
  }
  if (modules.has(manifest.id)) errors.push(`${file}: duplicate module id '${manifest.id}'`);
  modules.set(manifest.id, manifest);
}
for (const [id, manifest] of modules) for (const dependency of manifest.dependencies?.hard ?? []) if (!modules.has(dependency)) errors.push(`${id}: missing dependency '${dependency}'`);
const state = new Map();
function visit(id, trail = []) { if (state.get(id) === "active") { errors.push(`dependency cycle: ${[...trail, id].join(" -> ")}`); return; } if (state.get(id) === "done") return; state.set(id, "active"); for (const dep of modules.get(id)?.dependencies?.hard ?? []) visit(dep, [...trail, id]); state.set(id, "done"); }
for (const id of modules.keys()) visit(id);
if (errors.length) { console.error(errors.join("\n")); process.exitCode = 1; } else console.log(`Validated ${manifests.length} module manifests and their hard dependency graph.`);

function collect(directory) { if (!fs.existsSync(directory)) return []; return fs.readdirSync(directory, { withFileTypes: true }).flatMap(entry => { const full = path.join(directory, entry.name); return entry.isDirectory() ? collect(full) : entry.name === "module.manifest.json" ? [full] : []; }); }
