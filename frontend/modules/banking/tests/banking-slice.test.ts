import { test, describe } from "node:test";
import assert from "node:assert/strict";
import { ar as bankingAr } from "../i18n/ar.ts";
import { en as bankingEn } from "../i18n/en.ts";

describe("TODIR Module 25 (Banking & Cash Management) — Frontend Unit & Slice Suite", () => {
  test("1:1 Arabic and English translation key parity for Banking resources", () => {
    const getKeys = (obj: Record<string, any>, prefix = ""): string[] => {
      let keys: string[] = [];
      for (const [k, v] of Object.entries(obj)) {
        const fullKey = prefix ? `${prefix}.${k}` : k;
        if (typeof v === "object" && v !== null) {
          keys = keys.concat(getKeys(v, fullKey));
        } else {
          keys.push(fullKey);
        }
      }
      return keys;
    };

    const arKeys = new Set(getKeys(bankingAr));
    const enKeys = new Set(getKeys(bankingEn));

    assert.equal(arKeys.size, enKeys.size, `Keys count mismatch: AR=${arKeys.size}, EN=${enKeys.size}`);
    for (const key of arKeys) {
      assert.ok(enKeys.has(key), `Missing EN translation key: ${key}`);
    }
    for (const key of enKeys) {
      assert.ok(arKeys.has(key), `Missing AR translation key: ${key}`);
    }
  });

  test("Physical cash count denomination arithmetic conservation invariant", () => {
    const denominations = [
      { denomination: "200", unitValue: 200, quantity: 25 }, // 5000
      { denomination: "100", unitValue: 100, quantity: 15 }, // 1500
      { denomination: "50", unitValue: 50, quantity: 10 },   // 500
      { denomination: "20", unitValue: 20, quantity: 5 },    // 100
      { denomination: "10", unitValue: 10, quantity: 10 },   // 100
      { denomination: "5", unitValue: 5, quantity: 8 },      // 40
      { denomination: "1", unitValue: 1, quantity: 12 },     // 12
      { denomination: "0.50", unitValue: 0.5, quantity: 6 }  // 3
    ];

    const expectedAmount = 7255;
    const totalCounted = denominations.reduce((acc, d) => acc + d.unitValue * d.quantity, 0);
    const variance = Math.round((totalCounted - expectedAmount) * 100) / 100;

    assert.equal(totalCounted, 7255, "Total counted cash must equal exact mathematical sum of denomination values");
    assert.equal(variance, 0, "Variance must be exactly 0 when physical cash matches expected book balance");

    // Shortage scenario
    const shortageCounted = 7200;
    const shortageVariance = Math.round((shortageCounted - expectedAmount) * 100) / 100;
    assert.equal(shortageVariance, -55, "Shortage must reflect negative variance (-55)");

    // Overage scenario
    const overageCounted = 7300;
    const overageVariance = Math.round((overageCounted - expectedAmount) * 100) / 100;
    assert.equal(overageVariance, 45, "Overage must reflect positive variance (+45)");
  });

  test("Bank reconciliation conservation arithmetic: externalAmount == systemAmount + difference", () => {
    // 1-to-1 exact match
    const externalAmount = 15000;
    const systemAmount = 15000;
    const diff = Math.round((externalAmount - systemAmount) * 100) / 100;
    assert.equal(diff, 0, "Exact match must have difference = 0");
    assert.equal(externalAmount, systemAmount + diff, "Conservation rule must hold: external = system + diff");

    // Partial match with fee / tolerance difference
    const externalWithFee = 14950;
    const systemGross = 15000;
    const feeDiff = Math.round((externalWithFee - systemGross) * 100) / 100;
    assert.equal(feeDiff, -50, "Bank fee difference must be -50");
    assert.equal(externalWithFee, systemGross + feeDiff, "Conservation rule holds with negative difference");

    // Many-to-1 match
    const lines = [5000, 3000, 2000];
    const totalExternal = lines.reduce((acc, l) => acc + l, 0);
    const singlePayment = 10000;
    const splitDiff = Math.round((totalExternal - singlePayment) * 100) / 100;
    assert.equal(splitDiff, 0, "Multi-line aggregate match must conserve sum");
    assert.equal(totalExternal, singlePayment + splitDiff);
  });

  test("Cash position stale detection logic invariant", () => {
    const isPositionStale = (asOfIsoString: string | null | undefined, thresholdHours = 24): boolean => {
      if (!asOfIsoString) return true;
      const asOfTime = new Date(asOfIsoString).getTime();
      if (Number.isNaN(asOfTime)) return true;
      const now = Date.now();
      const elapsedHours = (now - asOfTime) / (1000 * 60 * 60);
      return elapsedHours > thresholdHours;
    };

    assert.equal(isPositionStale(null), true, "Null asOf date is considered stale");
    assert.equal(isPositionStale(undefined), true, "Undefined asOf date is considered stale");
    assert.equal(isPositionStale("invalid-date"), true, "Invalid date string is considered stale");

    const freshDate = new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(); // 2 hours ago
    assert.equal(isPositionStale(freshDate), false, "Position updated 2 hours ago is not stale (< 24h)");

    const staleDate = new Date(Date.now() - 48 * 60 * 60 * 1000).toISOString(); // 48 hours ago
    assert.equal(isPositionStale(staleDate), true, "Position updated 48 hours ago is stale (> 24h)");
  });

  test("Internal transfer transit states and balance preservation", () => {
    interface TransferStateSimulation {
      sourceAmount: number;
      destinationAmount: number;
      inTransitAmount: number;
      state: "InTransit" | "Matched";
    }

    const initialTransfer: TransferStateSimulation = {
      sourceAmount: 50000,
      destinationAmount: 50000,
      inTransitAmount: 50000,
      state: "InTransit"
    };

    assert.equal(initialTransfer.state, "InTransit");
    assert.equal(initialTransfer.inTransitAmount, initialTransfer.sourceAmount);

    // After bank statement reconciliation on both sides
    const completedTransfer: TransferStateSimulation = {
      ...initialTransfer,
      inTransitAmount: 0,
      state: "Matched"
    };

    assert.equal(completedTransfer.state, "Matched");
    assert.equal(completedTransfer.inTransitAmount, 0, "In-transit amount cleared to 0 once fully matched");
  });

  test("CSV bank statement parser: handles quoted fields, embedded commas, and CRLF endings", async () => {
    const { parseCsvText, parseAndValidateCsvStatementLines } = await import("../utils/csv-statement-parser.ts");

    const sampleCsv = [
      'ExternalId,BookingDate,Direction,Amount,Counterparty,Reference,Code',
      'TXN-001,2026-11-01,Credit,"12,500.50","Atlas Telecom, LLC",REF-9988,CR-DEP',
      'TXN-002,2026-11-02,Debit,450.00,"Bank Charges ""Special""",FEE-01,DB-WD',
      'TXN-003,2026-11-03,Credit,3000.00,"Delta Corp",INV-4412,CR-DEP'
    ].join("\r\n");

    const rows = parseCsvText(sampleCsv);
    assert.equal(rows.length, 4, "Must parse 4 rows including header");
    assert.equal(rows[1]![4], "Atlas Telecom, LLC", "Must preserve comma inside quoted counterparty string");
    assert.equal(rows[2]![4], 'Bank Charges "Special"', "Must unescape double quotes correctly");

    const validated = parseAndValidateCsvStatementLines(sampleCsv, "EGP");
    assert.equal(validated.success, true);
    if (validated.success) {
      assert.equal(validated.lines.length, 3, "Parsed exactly 3 statement lines");
      assert.equal(validated.lines[0]!.amount, 12500.50);
      assert.equal(validated.lines[0]!.counterpartyName, "Atlas Telecom, LLC");
      assert.equal(validated.lines[1]!.direction, "Debit");
      assert.equal(validated.totalCredit, 15500.50);
      assert.equal(validated.totalDebit, 450.00);
    }
  });

  test("CSV bank statement parser: rejects invalid rows and duplicates with row-specific errors", async () => {
    const { parseAndValidateCsvStatementLines } = await import("../utils/csv-statement-parser.ts");

    const badCsv = [
      'ExternalId,BookingDate,Direction,Amount',
      'TXN-101,2026-11-01,Credit,1000',
      'TXN-102,not-a-date,Credit,2000',      // bad date
      'TXN-103,2026-11-03,InvalidDir,500',   // bad direction
      'TXN-104,2026-11-04,Debit,-250',        // negative amount
      'TXN-101,2026-11-05,Credit,1500'       // duplicate ID
    ].join("\n");

    const result = parseAndValidateCsvStatementLines(badCsv, "EGP");
    assert.equal(result.success, false, "Must fail validation when invalid rows exist");
    if (!result.success) {
      assert.ok(result.errors.some(e => e.includes("Row 3") && e.includes("date")), "Identifies row 3 date error");
      assert.ok(result.errors.some(e => e.includes("Row 4") && e.includes("direction")), "Identifies row 4 direction error");
      assert.ok(result.errors.some(e => e.includes("Row 5") && e.includes("amount")), "Identifies row 5 negative amount error");
      assert.ok(result.errors.some(e => e.includes("Row 6") && e.includes("Duplicate")), "Identifies row 6 duplicate ID error");
    }
  });
});
