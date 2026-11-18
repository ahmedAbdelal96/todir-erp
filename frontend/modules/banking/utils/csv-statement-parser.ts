import type { ImportStatementLineRequest } from "../types.ts";

export interface CsvParseValidationSuccess {
  readonly success: true;
  readonly lines: readonly ImportStatementLineRequest[];
  readonly totalDebit: number;
  readonly totalCredit: number;
  readonly rowCount: number;
}

export interface CsvParseValidationFailure {
  readonly success: false;
  readonly errors: readonly string[];
}

export type CsvParseResult = CsvParseValidationSuccess | CsvParseValidationFailure;

/**
 * Robust RFC 4180 compliant CSV parser supporting quoted strings,
 * escaped quotes (""), embedded commas, CRLF and LF.
 */
export function parseCsvText(raw: string): string[][] {
  const result: string[][] = [];
  let row: string[] = [];
  let current = "";
  let insideQuotes = false;
  let i = 0;

  while (i < raw.length) {
    const char = raw[i];
    const nextChar = i + 1 < raw.length ? raw[i + 1] : "";

    if (char === '"') {
      if (insideQuotes && nextChar === '"') {
        // Escaped double quote: "" -> "
        current += '"';
        i += 2;
        continue;
      }
      insideQuotes = !insideQuotes;
      i++;
      continue;
    }

    if (char === "," && !insideQuotes) {
      row.push(current.trim());
      current = "";
      i++;
      continue;
    }

    if ((char === "\r" || char === "\n") && !insideQuotes) {
      if (char === "\r" && nextChar === "\n") {
        i++; // skip \n in \r\n
      }
      row.push(current.trim());
      if (row.some((c) => c.length > 0)) {
        result.push(row);
      }
      row = [];
      current = "";
      i++;
      continue;
    }

    current += char;
    i++;
  }

  if (current.length > 0 || row.length > 0) {
    row.push(current.trim());
    if (row.some((c) => c.length > 0)) {
      result.push(row);
    }
  }

  return result;
}

/**
 * Validates parsed CSV rows against banking statement line domain invariants.
 * Rejects with row-specific errors on invalid dates, negative amounts, duplicate external IDs,
 * or unrecognized directions. Never silently drops invalid financial lines.
 */
export function parseAndValidateCsvStatementLines(
  csvText: string,
  currencyCode: string,
  defaultDate?: string
): CsvParseResult {
  if (!csvText.trim()) {
    return { success: false, errors: ["CSV file content is empty."] };
  }

  const rawRows = parseCsvText(csvText);
  if (rawRows.length === 0) {
    return { success: false, errors: ["No valid rows found in CSV content."] };
  }

  const errors: string[] = [];
  const lines: ImportStatementLineRequest[] = [];
  const seenExternalIds = new Set<string>();

  let totalDebit = 0;
  let totalCredit = 0;
  const today = defaultDate || new Date().toISOString().slice(0, 10);

  // Check if first row is a header
  let startIndex = 0;
  const firstRow = rawRows[0]!;
  const firstCol = firstRow[0]?.toLowerCase() ?? "";
  if (
    firstCol.includes("id") ||
    firstCol.includes("external") ||
    firstCol.includes("line") ||
    firstCol.includes("مرجع") ||
    firstCol.includes("كود")
  ) {
    startIndex = 1;
  }

  for (let r = startIndex; r < rawRows.length; r++) {
    const row = rawRows[r]!;
    const rowNum = r + 1;

    // Must have at least 4 columns: [externalLineId, bookingDate, direction, amount]
    if (row.length < 4) {
      errors.push(`Row ${rowNum}: Insufficient columns (${row.length} found, minimum 4 required: ID, Date, Direction, Amount).`);
      continue;
    }

    const [extId, date, dir, amt, counterparty, ref, code] = row;

    if (!extId || !extId.trim()) {
      errors.push(`Row ${rowNum}: External Line ID is required and cannot be empty.`);
      continue;
    }

    const cleanExtId = extId.trim();
    if (seenExternalIds.has(cleanExtId)) {
      errors.push(`Row ${rowNum}: Duplicate External Line ID '${cleanExtId}'.`);
      continue;
    }
    seenExternalIds.add(cleanExtId);

    // Validate Date (YYYY-MM-DD)
    const cleanDate = date?.trim() || today;
    if (!/^\d{4}-\d{2}-\d{2}$/.test(cleanDate) && isNaN(Date.parse(cleanDate))) {
      errors.push(`Row ${rowNum}: Invalid booking date '${date}'. Expected ISO format YYYY-MM-DD.`);
      continue;
    }

    // Validate Direction
    const dirLower = dir?.trim().toLowerCase() ?? "";
    let normalizedDirection: "Credit" | "Debit" | null = null;
    if (dirLower === "credit" || dirLower === "cr" || dirLower === "دائن" || dirLower === "إيداع") {
      normalizedDirection = "Credit";
    } else if (dirLower === "debit" || dirLower === "dr" || dirLower === "مدين" || dirLower === "سحب") {
      normalizedDirection = "Debit";
    } else {
      errors.push(`Row ${rowNum}: Invalid direction '${dir}'. Must be Credit or Debit.`);
      continue;
    }

    // Validate Amount
    const cleanAmt = amt?.trim().replace(/,/g, "") ?? "";
    const parsedAmount = parseFloat(cleanAmt);
    if (isNaN(parsedAmount) || parsedAmount <= 0) {
      errors.push(`Row ${rowNum}: Invalid amount '${amt}'. Amount must be a positive number greater than zero.`);
      continue;
    }

    if (normalizedDirection === "Credit") {
      totalCredit += parsedAmount;
    } else {
      totalDebit += parsedAmount;
    }

    lines.push({
      externalLineId: cleanExtId,
      bookingDate: cleanDate,
      valueDate: cleanDate,
      direction: normalizedDirection,
      amount: parsedAmount,
      currencyCode: currencyCode.trim().toUpperCase(),
      reference: ref?.trim() || null,
      transactionCode: code?.trim() || (normalizedDirection === "Credit" ? "CR-DEP" : "DB-WD"),
      counterpartyName: counterparty?.trim() || null,
      rawPayload: JSON.stringify({ csvRow: row, rowNumber: rowNum })
    });
  }

  if (errors.length > 0) {
    return { success: false, errors };
  }

  if (lines.length === 0) {
    return { success: false, errors: ["No valid transaction lines could be parsed from the CSV."] };
  }

  return {
    success: true,
    lines,
    totalDebit: Math.round(totalDebit * 100) / 100,
    totalCredit: Math.round(totalCredit * 100) / 100,
    rowCount: lines.length
  };
}
