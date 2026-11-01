export function formatCurrency(amount: number, currency: string = "EGP"): string {
  return `${Number(amount || 0).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ${currency}`;
}
