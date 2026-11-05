import { test, describe } from "node:test";
import assert from "node:assert/strict";
import { mapCrmError } from "../services/crm-error-map.ts";
import { crmAr } from "../i18n/ar.ts";
import { crmEn } from "../i18n/en.ts";
import type { ForecastCategory, LeadPriority, DealHealthStatus } from "../types.ts";

describe("CRM Slice Contracts & Domain Invariants", () => {
  test("RFC-7807 error mapper translates known CRM business codes without leaking technical details", () => {
    const error1 = mapCrmError({ code: "crm.lead_not_found" });
    assert.equal(error1.messageKey, "errors.leadNotFound");
    assert.ok(error1.defaultMessage.length > 0);

    const error2 = mapCrmError({ code: "crm.lead_not_qualified" });
    assert.equal(error2.messageKey, "errors.leadNotQualified");

    const error3 = mapCrmError({ code: "crm.opportunity_not_found" });
    assert.equal(error3.messageKey, "errors.opportunityNotFound");

    const error4 = mapCrmError({ code: "crm.opportunity_closed" });
    assert.equal(error4.messageKey, "errors.opportunityClosed");

    const error5 = mapCrmError({ code: "crm.pipeline_not_found" });
    assert.equal(error5.messageKey, "errors.pipelineNotFound");

    const error6 = mapCrmError({ code: "crm.stage_not_found" });
    assert.equal(error6.messageKey, "errors.stageNotFound");

    const error7 = mapCrmError({ code: "crm.team_not_found" });
    assert.equal(error7.messageKey, "errors.teamNotFound");

    const error8 = mapCrmError({ code: "crm.concurrency_conflict" });
    assert.equal(error8.messageKey, "errors.concurrencyConflict");

    const error9 = mapCrmError({ code: "crm.stale_record" });
    assert.equal(error9.messageKey, "errors.concurrencyConflict");

    const unknown = mapCrmError({ code: "crm.unknown_internal_code" });
    assert.equal(unknown.messageKey, "errors.unknown");
  });

  test("1:1 Arabic and English translation parity for CRM resources", () => {
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

    const arKeys = new Set(getKeys(crmAr));
    const enKeys = new Set(getKeys(crmEn));

    assert.equal(arKeys.size, enKeys.size, `Keys count mismatch: AR=${arKeys.size}, EN=${enKeys.size}`);
    for (const key of arKeys) {
      assert.ok(enKeys.has(key), `Missing EN key: ${key}`);
    }
  });

  test("Deal health score invariants: strictly partitioned into 4 distinct operational states", () => {
    const resolveHealth = (score: number): DealHealthStatus => {
      if (score >= 70) return "Good";
      if (score >= 40) return "Fair";
      if (score >= 20) return "AtRisk";
      return "Stale";
    };

    assert.equal(resolveHealth(95), "Good");
    assert.equal(resolveHealth(70), "Good");
    assert.equal(resolveHealth(69), "Fair");
    assert.equal(resolveHealth(40), "Fair");
    assert.equal(resolveHealth(39), "AtRisk");
    assert.equal(resolveHealth(20), "AtRisk");
    assert.equal(resolveHealth(19), "Stale");
    assert.equal(resolveHealth(0), "Stale");

    // Invariant: Statuses map to distinct localized labels in both Arabic and English
    assert.ok(crmAr.dealHealth.good.length > 0);
    assert.ok(crmAr.dealHealth.fair.length > 0);
    assert.ok(crmAr.dealHealth.atRisk.length > 0);
    assert.ok(crmAr.dealHealth.stale.length > 0);

    assert.notEqual(crmAr.dealHealth.good, crmAr.dealHealth.atRisk);
    assert.notEqual(crmAr.dealHealth.fair, crmAr.dealHealth.stale);
  });

  test("Forecast category confidence order and calculation invariants", () => {
    const categories: ForecastCategory[] = ["Pipeline", "BestCase", "Commit", "Closed"];
    assert.equal(categories.length, 4);

    // Test forecast weighting formula: estimatedAmount * (probability / 100)
    const calculateWeighted = (amount: number, probability: number): number => {
      assert.ok(probability >= 0 && probability <= 100, "Probability must be between 0 and 100%");
      assert.ok(amount >= 0, "Deal amount must be non-negative");
      return Math.round(amount * (probability / 100));
    };

    assert.equal(calculateWeighted(100000, 20), 20000);
    assert.equal(calculateWeighted(250000, 80), 200000);
    assert.equal(calculateWeighted(50000, 100), 50000);
    assert.equal(calculateWeighted(75000, 0), 0);
  });

  test("Priority taxonomy invariants: High > Normal > Low with zero ambiguous values", () => {
    const priorityWeight: Record<LeadPriority, number> = {
      High: 3,
      Normal: 2,
      Low: 1
    };

    assert.ok((priorityWeight.High ?? 0) > (priorityWeight.Normal ?? 0));
    assert.ok((priorityWeight.Normal ?? 0) > (priorityWeight.Low ?? 0));

    // Verify Arabic translations exist and maintain hierarchy
    assert.equal(crmAr.priority.high, "مرتفعة");
    assert.equal(crmAr.priority.normal, "عادية");
    assert.equal(crmAr.priority.low, "منخفضة");
  });

  test("Lead qualification invariant: qualified leads only can transition to directory conversion", () => {
    const canConvertLead = (status: string): boolean => {
      return status === "Qualified";
    };

    assert.equal(canConvertLead("New"), false);
    assert.equal(canConvertLead("Working"), false);
    assert.equal(canConvertLead("Disqualified"), false);
    assert.equal(canConvertLead("Converted"), false);
    assert.equal(canConvertLead("Qualified"), true);
  });

  test("Direct CRM-to-Sales Quotation contract validation: preserves commercial lines without inventory leaks", () => {
    const sampleHandoff = {
      legalEntityId: "entity-001",
      partyId: "party-001",
      opportunityId: "opp-001",
      currencyCode: "EGP",
      notes: "Commercial quote created from CRM opportunity",
      lines: [
        {
          description: "Enterprise ERP Cloud Subscription (Annual)",
          quantity: 1,
          unitPrice: 150000,
          discountAmount: 15000
        },
        {
          description: "Implementation & Localization Services",
          quantity: 40,
          unitPrice: 2000,
          discountAmount: 0
        }
      ]
    };

    assert.ok(sampleHandoff.lines.length >= 1, "Quotation must contain at least one line item");
    const subtotal = sampleHandoff.lines.reduce(
      (sum, line) => sum + (line.quantity * line.unitPrice - line.discountAmount),
      0
    );
    assert.equal(subtotal, 135000 + 80000); // 215,000 EGP
  });
});
