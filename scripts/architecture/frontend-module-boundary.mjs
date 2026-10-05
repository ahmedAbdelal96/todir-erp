import fs from "node:fs";
import path from "node:path";
import { API } from "typescript/unstable/sync";

const sourceExtensions = [".ts", ".tsx"];

export function validateFrontendModuleBoundaries(modulesRoot) {
  const root = path.resolve(modulesRoot);
  if (!fs.existsSync(root)) {
    return [];
  }

  const sourcePaths = collectSourceFiles(root);
  const violations = [];
  const compiler = new API({ cwd: root });

  try {
    // TypeScript 7 exposes parsed import specifiers through its compiler API.
    // The rule deliberately inspects syntax metadata, never source-text patterns.
    const snapshot = compiler.updateSnapshot({ openFiles: sourcePaths });
    try {
      for (const sourcePath of sourcePaths) {
        const sourceModule = moduleNameFor(root, sourcePath);
        const project = snapshot.getDefaultProjectForFile(sourcePath);
        const sourceFile = project?.program.getSourceFile(sourcePath);

        if (!sourceModule || !sourceFile) {
          continue;
        }

        for (const importSpecifier of sourceFile.imports) {
          validateImport(importSpecifier.text, sourcePath, sourceModule, root, project, compiler, violations);
        }
      }
    } finally {
      snapshot.dispose();
    }
  } finally {
    compiler.close();
  }

  return violations;
}

function validateImport(moduleSpecifier, sourcePath, sourceModule, modulesRoot, project, compiler, violations) {
  const targetPath = resolveModuleSpecifier(sourcePath, moduleSpecifier, project, compiler);
  const targetModule = targetPath ? moduleNameFor(modulesRoot, targetPath) : undefined;

  if (targetModule && targetModule !== sourceModule && !isApprovedPublicSurface(targetPath, modulesRoot, targetModule)) {
    violations.push(`${relative(modulesRoot, sourcePath)} imports internal file ${relative(modulesRoot, targetPath)} from module '${targetModule}'.`);
  }
}

function collectSourceFiles(directory) {
  return fs.readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
    const entryPath = path.join(directory, entry.name);
    if (entry.isDirectory()) {
      return entry.name === "node_modules" ? [] : collectSourceFiles(entryPath);
    }

    return sourceExtensions.includes(path.extname(entry.name)) && !entry.name.endsWith(".d.ts") ? [entryPath] : [];
  });
}

function resolveModuleSpecifier(sourcePath, moduleSpecifier, project, compiler) {
  if (moduleSpecifier.startsWith(".")) {
    return resolveCandidate(path.resolve(path.dirname(sourcePath), moduleSpecifier));
  }

  // Read the active compiler configuration through TypeScript's compiler API.
  // This applies the same baseUrl/paths contract used by the source project.
  const options = project?.configFileName ? compiler.parseConfigFile(project.configFileName).options : undefined;
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
  const candidates = [candidate, ...sourceExtensions.map(extension => `${candidate}${extension}`), ...sourceExtensions.map(extension => path.join(candidate, `index${extension}`))];
  return candidates.find(fs.existsSync);
}

function moduleNameFor(modulesRoot, filePath) {
  const relativePath = path.relative(modulesRoot, filePath);
  if (relativePath.startsWith("..") || path.isAbsolute(relativePath)) {
    return undefined;
  }

  return relativePath.split(path.sep)[0];
}

function isApprovedPublicSurface(targetPath, modulesRoot, targetModule) {
  const expectedPublicPath = path.join(modulesRoot, targetModule, "public");
  return sourceExtensions.some(extension => path.resolve(targetPath) === path.resolve(`${expectedPublicPath}${extension}`));
}

function relative(root, target) {
  return path.relative(root, target).split(path.sep).join("/");
}
