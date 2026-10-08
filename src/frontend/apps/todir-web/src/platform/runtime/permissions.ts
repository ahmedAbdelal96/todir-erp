export class PermissionSet {
  private readonly permissions: ReadonlySet<string>;

  constructor(permissions: Iterable<string> = []) {
    this.permissions = new Set(permissions);
  }

  hasPermission(permission: string): boolean {
    if (this.permissions.has("*") || this.permissions.has("admin")) {
      return true;
    }
    if (this.permissions.has(permission)) {
      return true;
    }
    // Check wildcard scope (e.g. 'platform.*' matches 'platform.settings.read')
    const dotIndex = permission.indexOf(".");
    if (dotIndex > 0) {
      const scopeWildcard = `${permission.slice(0, dotIndex)}.*`;
      if (this.permissions.has(scopeWildcard)) {
        return true;
      }
    }
    return false;
  }

  hasAnyPermission(...permissions: readonly string[]): boolean {
    if (permissions.length === 0) {
      return true;
    }
    return permissions.some(perm => this.hasPermission(perm));
  }

  hasAllPermissions(...permissions: readonly string[]): boolean {
    return permissions.every(perm => this.hasPermission(perm));
  }

  toArray(): string[] {
    return Array.from(this.permissions);
  }
}
