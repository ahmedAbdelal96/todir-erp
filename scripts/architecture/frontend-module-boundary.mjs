import fs from "node:fs";
import path from "node:path";
import { API } from "typescript/unstable/sync";

const sourceExtensions = [".ts", ".tsx"];

const FORBIDDEN_COMMERCIAL_PACKAGES = [
  "ag-grid-enterprise",
  "@ag-grid-enterprise/",
  "@mui/x-data-grid-pro",
  "@mui/x-data-grid-premium",
  "handsontable",
  "@grapecity/",
  "@syncfusion/"
];

export function validateFrontendArchitecture(options = {}) {
  const rootDir = path.resolve(options.rootDir ?? "src/frontend/apps/todir-web/src");
  const modulesRoot = path.resolve(options.modulesRoot ?? path.join(rootDir, "modules"));

  const violations = [];
  const moduleGraph = new Map(); // moduleName -> Set of imported moduleNames

  if (!fs.existsSync(rootDir)) {
    return { violations, cycles: [] };
  }

  const sourcePaths = collectSourceFiles(rootDir);
  const compiler = new API({ cwd: rootDir });

  try {
    const snapshot = compiler.updateSnapshot({ openFiles: sourcePaths });
    try {
      for (const sourcePath of sourcePaths) {
        const project = snapshot.getDefaultProjectForFile(sourcePath);
        const sourceFile = project?.program.getSourceFile(sourcePath);

        if (!sourceFile) {
          continue;
        }

        const sourceModule = moduleNameFor(modulesRoot, sourcePath);
        const isPlatformOrApp = !sourceModule && isPlatformOrAppFile(rootDir, sourcePath);

        for (const importSpecifier of sourceFile.imports) {
          const specifierText = importSpecifier.text;

          // Check 1: Forbidden commercial / paid packages
          for (const banned of FORBIDDEN_COMMERCIAL_PACKAGES) {
            if (specifierText === banned || specifierText.startsWith(banned)) {
              violations.push(
                `${relative(rootDir, sourcePath)} imports forbidden commercial/paid package '${specifierText}'. TODIR Core enforces the Free-Only Core Dependency Rule.`
              );
            }
          }

          // Resolve target path for local / aliased modules
          const targetPath = resolveModuleSpecifier(sourcePath, specifierText, project, compiler);
          if (!targetPath) {
            continue;
          }

          const targetModule = moduleNameFor(modulesRoot, targetPath);

          // Check 2: Cross-module internal imports (Module A importing Module B internals)
          if (sourceModule && targetModule && sourceModule !== targetModule) {
            if (!isApprovedPublicSurface(targetPath, modulesRoot, targetModule)) {
              violations.push(
                `${relative(modulesRoot, sourcePath)} imports internal file ${relative(modulesRoot, targetPath)} from module '${targetModule}'. Only public.ts is allowed for cross-module integration.`
              );
            } else {
              // Record in dependency graph for cycle detection
              if (!moduleGraph.has(sourceModule)) {
                moduleGraph.set(sourceModule, new Set());
              }
              moduleGraph.get(sourceModule).add(targetModule);
            }
          }

          // Check 3: Platform / App importing module internals
          if (isPlatformOrApp && targetModule) {
            if (!isApprovedPublicSurface(targetPath, modulesRoot, targetModule)) {
              violations.push(
                `${relative(rootDir, sourcePath)} imports internal file ${relative(modulesRoot, targetPath)} from module '${targetModule}'. Platform must not depend on module internals.`
              );
            }
          }
        }
      }
    } finally {
      snapshot.dispose();
    }
  } finally {
    compiler.close();
  }

  // Check 4: Circular dependencies between modules
  const cycles = detectCycles(moduleGraph);
  for (const cycle of cycles) {
    violations.push(`Circular frontend module dependency detected: ${cycle.join(" -> ")}`);
  }

  return { violations, cycles };
}

// Backward compatibility alias for existing callers
export function validateFrontendModuleBoundaries(modulesRoot) {
  const result = validateFrontendArchitecture({
    modulesRoot,
    rootDir: path.dirname(modulesRoot)
  });
  return result.violations;
}

function detectCycles(graph) {
  const cycles = [];
  const visited = new Set();
  const recursionStack = [];

  function dfs(node) {
    visited.add(node);
    recursionStack.push(node);

    const neighbors = graph.get(node) || new Set();
    for (const neighbor of neighbors) {
      if (!visited.has(neighbor)) {
        dfs(neighbor);
      } else if (recursionStack.includes(neighbor)) {
        const cycleStartIndex = recursionStack.indexOf(neighbor);
        const cycle = [...recursionStack.slice(cycleStartIndex), neighbor];
        cycles.push(cycle);
      }
    }

    recursionStack.pop();
  }

  for (const node of graph.keys()) {
    if (!visited.has(node)) {
      dfs(node);
    }
  }

  return cycles;
}

function collectSourceFiles(directory) {
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
    const entryPath = path.join(directory, entry.name);
    if (entry.isDirectory()) {
      return entry.name === "node_modules" || entry.name === "dist" ? [] : collectSourceFiles(entryPath);
    }

    return sourceExtensions.includes(path.extname(entry.name)) && !entry.name.endsWith(".d.ts") ? [entryPath] : [];
  });
}

function resolveModuleSpecifier(sourcePath, moduleSpecifier, project, compiler) {
  if (moduleSpecifier.startsWith(".")) {
    return resolveCandidate(path.resolve(path.dirname(sourcePath), moduleSpecifier));
  }

  const options = project?.configFileName && fs.existsSync(project.configFileName)
    ? compiler.parseConfigFile(project.configFileName).options
    : undefined;
  const configDirectory = options?.configFilePath ? path.dirname(options.configFilePath) : path.dirname(sourcePath);
  const baseUrl = options?.baseUrl ? path.resolve(configDirectory, options.baseUrl) : configDirectory;
  const resolvedPath = resolvePathMappedSpecifier(moduleSpecifier, options?.paths, baseUrl);
  return resolvedPath ?? resolveCandidate(path.resolve(baseUrl, moduleSpecifier));
}

function resolvePathMappedSpecifier(moduleSpecifier, paths, baseUrl) {
  if (!paths || typeof paths !== "object") {
    return undefined;
  }

  for (const [pattern, substitutions] of Object.entries(paths)) {
    const wildcardIndex = pattern.indexOf("*");
    const prefix = wildcardIndex < 0 ? pattern : pattern.slice(0, wildcardIndex);
    const suffix = wildcardIndex < 0 ? "" : pattern.slice(wildcardIndex + 1);
    if (!moduleSpecifier.startsWith(prefix) || !moduleSpecifier.endsWith(suffix)) {
      continue;
    }

    const wildcard = moduleSpecifier.slice(prefix.length, moduleSpecifier.length - suffix.length);
    for (const substitution of substitutions) {
      const candidate = path.resolve(baseUrl, substitution.replace("*", wildcard));
      const resolved = resolveCandidate(candidate);
      if (resolved) {
        return resolved;
      }
    }
  }

  return undefined;
}

function resolveCandidate(candidate) {
  const candidates = [
    candidate,
    ...sourceExtensions.map(extension => `${candidate}${extension}`),
    ...sourceExtensions.map(extension => path.join(candidate, `index${extension}`))
  ];
  return candidates.find(fs.existsSync);
}

function moduleNameFor(modulesRoot, filePath) {
  const relativePath = path.relative(modulesRoot, filePath);
  if (relativePath.startsWith("..") || path.isAbsolute(relativePath)) {
    return undefined;
  }

  return relativePath.split(path.sep)[0];
}

function isPlatformOrAppFile(rootDir, filePath) {
  const relativePath = path.relative(rootDir, filePath);
  return !relativePath.startsWith("..") && !path.isAbsolute(relativePath) && !relativePath.startsWith("modules");
}

function isApprovedPublicSurface(targetPath, modulesRoot, targetModule) {
  const expectedPublicPath = path.join(modulesRoot, targetModule, "public");
  return sourceExtensions.some(extension => path.resolve(targetPath) === path.resolve(`${expectedPublicPath}${extension}`));
}

function relative(root, target) {
  return path.relative(root, target).split(path.sep).join("/");
}
