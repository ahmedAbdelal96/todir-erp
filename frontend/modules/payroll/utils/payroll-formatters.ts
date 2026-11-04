/**
 * Payroll domain formatters and presentation helpers.
 * Guarantees financial truth, avoids negative-zero artifacts, and resolves employee labels safely.
 */

export function formatMoneyAmount(
  amount: number | null | undefined,
  currency: string = "EGP",
  options?: { isDeduction?: boolean }
): string {
  if (amount == null || isNaN(amount)) {
    return `0 ${currency}`;
  }

  // Prevent IEEE 754 negative zero (-0) or tiny floating point noise
  if (Math.abs(amount) < 0.0001) {
    return `0 ${currency}`;
  }

  const prefix = options?.isDeduction && amount > 0 ? "-" : "";
  return `${prefix}${amount.toLocaleString(undefined, { minimumFractionDigits: 0, maximumFractionDigits: 2 })} ${currency}`;
}

export function formatMoneyValue(amount: number | null | undefined): string {
  if (amount == null || isNaN(amount) || Math.abs(amount) < 0.0001) {
    return "0";
  }
  return amount.toLocaleString(undefined, { minimumFractionDigits: 0, maximumFractionDigits: 2 });
}

export function resolveEmployeeDisplay(
  employmentId: string,
  employeesOrMap?: any
): string {
  if (!employmentId) return "—";
  if (!employeesOrMap) return employmentId;

  // If array of employee objects
  if (Array.isArray(employeesOrMap)) {
    const found = employeesOrMap.find(
      (e: any) => e.id === employmentId || e.employmentId === employmentId
    );
    if (found) {
      const name = found.displayName || `${found.firstName || ""} ${found.lastName || ""}`.trim() || employmentId;
      const num = found.employmentNumber || found.employeeNumber;
      return num ? `${name} (${num})` : name;
    }
    return employmentId;
  }

  // If map
  const foundInMap = employeesOrMap[employmentId];
  if (foundInMap) {
    return `${foundInMap.displayName}${foundInMap.employmentNumber ? ` (${foundInMap.employmentNumber})` : ""}`;
  }

  return employmentId;
}

export const resolveEmployeeLabel = resolveEmployeeDisplay;
