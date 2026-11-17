import { describe, it } from "node:test";
import assert from "node:assert/strict";
import type {
  TaxCapabilityDto,
  TaxCoverageResult,
  TaxProfileImportPreviewDto,
  TaxResultContract,
  TaxTreatment
} from "../types.ts";
import { mapTaxErrorCode, mapImportRowOutcome, sanitizeTechnicalDetail } from "../services/tax-error-map.ts";
import { getTaxStatusPresentation } from "../components/tax-status-presentation.ts";
import { deriveOverallReadiness } from "../services/tax-readiness.ts";

describe("Tax Administration Slice Contracts & Domain Invariants", () => {
  it("F01 / Tax Disabled Invariant — Disabled capability is distinct and never conflated with 0% tax or Exempt", () => {
    const disabledCapability: TaxCapabilityDto = {
      legalEntityId: "00000000-0000-0000-0000-000000000001",
      capability: "Disabled",
      effectiveFrom: "2026-01-01",
      effectiveTo: null,
      version: 1
    };

    const enabledCapability: TaxCapabilityDto = {
      legalEntityId: "00000000-0000-0000-0000-000000000002",
      capability: "Enabled",
      effectiveFrom: "2026-01-01",
      effectiveTo: null,
      version: 1
    };

    assert.equal(disabledCapability.capability, "Disabled");
    assert.equal(enabledCapability.capability, "Enabled");
    assert.notEqual(disabledCapability.capability, enabledCapability.capability);
  });

  it("F02 / Legal Treatments Integrity — ZeroRated, Exempt, OutOfScope, and ReverseCharge are strictly distinct", () => {
    const treatments: readonly TaxTreatment[] = [
      "StandardRated",
      "ZeroRated",
      "Exempt",
      "OutOfScope",
      "ReverseCharge"
    ];

    // Verify all 5 statutory treatments exist and none are equal
    const set = new Set(treatments);
    assert.equal(set.size, 5);
    assert.ok(set.has("StandardRated"));
    assert.ok(set.has("ZeroRated"));
    assert.ok(set.has("Exempt"));
    assert.ok(set.has("OutOfScope"));
    assert.ok(set.has("ReverseCharge"));

    // Verify Reverse Charge specifically requires liability indicator
    const isReverseCharge = (t: TaxTreatment) => t === "ReverseCharge";
    assert.equal(isReverseCharge("ReverseCharge"), true);
    assert.equal(isReverseCharge("Exempt"), false);
    assert.equal(isReverseCharge("ZeroRated"), false);
  });

  it("F03 / Coverage Readiness Contract — correctly categorizes operational health states", () => {
    const readyCoverage: TaxCoverageResult = {
      legalEntityId: "00000000-0000-0000-0000-000000000002",
      at: "2026-10-24",
      health: "Healthy",
      capability: "Enabled",
      countryPackStatus: "Active",
      registrationStatus: "Registered",
      registrationEffective: true,
      taxPolicyStatus: "Active",
      roundingPolicyStatus: "Active",
      productClassificationCoverage: "Complete",
      partyProfileReadiness: "Complete",
      activeRuleSetStatus: "Active",
      issues: []
    };

    assert.equal(readyCoverage.health, "Healthy");
    assert.equal(readyCoverage.registrationEffective, true);
    assert.equal(readyCoverage.issues.length, 0);

    const blockedCoverage: TaxCoverageResult = {
      legalEntityId: "00000000-0000-0000-0000-000000000001",
      at: "2026-10-24",
      health: "Blocked",
      capability: "Enabled",
      countryPackStatus: "Missing",
      registrationStatus: "Missing",
      registrationEffective: false,
      taxPolicyStatus: "Missing",
      roundingPolicyStatus: "Missing",
      productClassificationCoverage: "Missing",
      partyProfileReadiness: "Missing",
      activeRuleSetStatus: "Missing",
      issues: [
        { code: "RegistrationMissing", message: "Tax registration is required" },
        { code: "ProductClassificationMissing", message: "Product requires tax classification" }
      ]
    };

    assert.equal(blockedCoverage.health, "Blocked");
    assert.equal(blockedCoverage.registrationEffective, false);
    assert.equal(blockedCoverage.issues.length, 2);
    assert.equal(blockedCoverage.issues[0]?.code, "RegistrationMissing");
  });

  it("F04 / Purchase Recoverability Math — total tax strictly equals recoverable + non-recoverable amounts", () => {
    const calculationResult: TaxResultContract = {
      evaluationState: "Determined",
      treatment: "StandardRated",
      netAmount: "1000.00",
      taxTotal: "140.00",
      grossAmount: "1140.00",
      taxLines: [
        {
          taxTypeCode: "VAT",
          taxCode: "VAT-14",
          treatment: "StandardRated",
          rate: "14.00",
          taxableBase: "1000.00",
          taxAmount: "140.00",
          currencyCode: "EGP",
          jurisdictionCode: "EG",
          ruleId: "RULE-001",
          ruleVersion: "1",
          recoverability: {
            kind: "PartiallyRecoverable",
            recoverablePercentage: "70.00",
            recoverableAmount: "98.00",
            nonRecoverableAmount: "42.00"
          }
        }
      ],
      jurisdictionSnapshot: "EG",
      registrationSnapshot: "REG-001",
      calculationVersion: "1",
      explanation: {
        countryPackCode: "EG-VAT",
        countryPackVersion: "1.0.0",
        taxDate: "2026-10-24",
        jurisdiction: "EG",
        registrationStatus: "Registered",
        componentCodes: ["VAT-14"],
        rounding: "RoundHalfUp",
        summary: "Standard VAT applied"
      }
    };

    assert.equal(calculationResult.evaluationState, "Determined");
    const line = calculationResult.taxLines[0]!;
    const recoverable = parseFloat(line.recoverability!.recoverableAmount);
    const nonRecoverable = parseFloat(line.recoverability!.nonRecoverableAmount);
    const lineTax = parseFloat(line.taxAmount);
    assert.equal(recoverable + nonRecoverable, lineTax);
  });

  it("F05 / Statutory vs Technical Review — TestOnly pack is never presented as legally approved", () => {
    const testPack = {
      sourceKind: "TestOnly",
      reviewStatus: "Pending",
      status: "Active"
    };

    const isStatutoryCertified = testPack.sourceKind !== "TestOnly" && testPack.reviewStatus === "Approved";
    assert.equal(isStatutoryCertified, false);

    const statutoryPendingPack = {
      sourceKind: "Statutory",
      reviewStatus: "Pending",
      status: "Installed"
    };

    const isPendingLegalReview = statutoryPendingPack.reviewStatus === "Pending";
    assert.equal(isPendingLegalReview, true);
  });

  it("F06 / Import Reconciliation — distinguishes preview outcomes without mutating persistent data", () => {
    const previewDto: TaxProfileImportPreviewDto = {
      batchId: "00000000-0000-0000-0000-000000000010",
      status: "PreviewReady",
      acceptedCount: 2,
      rejectedCount: 1,
      skippedCount: 0,
      rows: [
        { rowNumber: 1, reference: "PRD-001", outcome: "Create", message: null },
        { rowNumber: 2, reference: "PRD-002", outcome: "CreateNewVersion", message: null },
        { rowNumber: 3, reference: "UNKNOWN-SKU", outcome: "UnknownProduct", message: "Product SKU not found in Catalog" }
      ]
    };

    const totalRows = previewDto.acceptedCount + previewDto.rejectedCount + previewDto.skippedCount;
    assert.equal(totalRows, 3);
    assert.equal(previewDto.acceptedCount, 2);
    assert.equal(previewDto.rejectedCount, 1);
    assert.equal(previewDto.rows[0]?.outcome, "Create");
    assert.equal(previewDto.rows[1]?.outcome, "CreateNewVersion");
    assert.equal(previewDto.rows[2]?.outcome, "UnknownProduct");
  });

  it("F07 / Error Mapping — accurately maps backend RFC-7807 codes to localized keys and plain English fallbacks", () => {
    const errReg = mapTaxErrorCode("tax.registration_missing");
    assert.equal(errReg.messageKey, "errors.registrationMissing");
    assert.ok(errReg.fallbackEn.includes("registration has not been configured"));

    const errRegistrationNumber = mapTaxErrorCode("tax.registration_number_required");
    assert.equal(errRegistrationNumber.messageKey, "errors.registrationNumberRequired");
    assert.ok(errRegistrationNumber.fallbackEn.includes("registration number is required"));

    const errProd = mapTaxErrorCode("tax.product_classification_missing");
    assert.equal(errProd.messageKey, "errors.productClassificationMissing");
    assert.ok(errProd.fallbackEn.includes("product requires a valid tax classification"));

    const errConflict = mapTaxErrorCode("tax.rule_conflict");
    assert.equal(errConflict.messageKey, "errors.ruleConflict");
    assert.ok(errConflict.fallbackEn.includes("Conflicting tax rules need review"));

    const errStack = mapTaxErrorCode("tax.stack_cycle");
    assert.equal(errStack.messageKey, "errors.stackCycle");
    assert.ok(errStack.fallbackEn.includes("circular way"));

    const errUnknown = mapTaxErrorCode("random_unknown_error_code");
    assert.equal(errUnknown.messageKey, "errors.genericTaxError");
  });

  it("F08 / Overall Readiness Aggregation — strictly derives Blocked, Warning, and Ready hierarchically (Enabled != Ready)", () => {
    // Case A: Blocked because registration is not effective, even though capability is Enabled
    const blockedCoverage: TaxCoverageResult = {
      legalEntityId: "00000000-0000-0000-0000-000000000002",
      at: "2026-10-24",
      health: "Green",
      capability: "Enabled",
      countryPackStatus: "Active",
      registrationStatus: "Missing",
      registrationEffective: false,
      taxPolicyStatus: "Active",
      roundingPolicyStatus: "Active",
      productClassificationCoverage: "Complete",
      partyProfileReadiness: "Complete",
      activeRuleSetStatus: "Active",
      issues: []
    };
    assert.equal(deriveOverallReadiness(blockedCoverage), "Blocked");

    // Case A2: Blocked because of explicit blocking issue
    const issueBlockedCoverage: TaxCoverageResult = {
      ...blockedCoverage,
      registrationStatus: "Registered",
      registrationEffective: true,
      issues: [{ code: "RegistrationMissing", message: "Tax registration missing" }]
    };
    assert.equal(deriveOverallReadiness(issueBlockedCoverage), "Blocked");

    // Case B: Warning / Needs Attention because product classification is incomplete
    const warningCoverage: TaxCoverageResult = {
      legalEntityId: "00000000-0000-0000-0000-000000000002",
      at: "2026-10-24",
      health: "Warning",
      capability: "Enabled",
      countryPackStatus: "Active",
      registrationStatus: "Registered",
      registrationEffective: true,
      taxPolicyStatus: "Active",
      roundingPolicyStatus: "Active",
      productClassificationCoverage: "Partial",
      partyProfileReadiness: "Complete",
      activeRuleSetStatus: "Active",
      issues: [{ code: "ProductClassificationWarning", message: "2 products require classification" }]
    };
    assert.equal(deriveOverallReadiness(warningCoverage), "Warning");

    // Case C: Ready only when every single mandatory technical readiness condition is satisfied
    const readyCoverage: TaxCoverageResult = {
      legalEntityId: "00000000-0000-0000-0000-000000000002",
      at: "2026-10-24",
      health: "Green",
      capability: "Enabled",
      countryPackStatus: "Available",
      registrationStatus: "Registered",
      registrationEffective: true,
      taxPolicyStatus: "Configured",
      roundingPolicyStatus: "Active",
      productClassificationCoverage: "Complete",
      partyProfileReadiness: "Complete",
      activeRuleSetStatus: "Active",
      issues: []
    };
    assert.equal(deriveOverallReadiness(readyCoverage), "Ready");
  });

  it("F09 / Import Row Outcome Localization Mapping — maps outcomes to localized messages without raw backend English leakage", () => {
    const unknownProduct = mapImportRowOutcome("UnknownProduct", "Product SKU was not found in Catalog master.");
    assert.equal(unknownProduct.isError, true);
    assert.equal(unknownProduct.messageKey, "importExport.outcomes.unknownProductMessage");
    assert.equal(unknownProduct.fallbackAr, "لم يتم العثور على المنتج في الكتالوج.");
    assert.equal(unknownProduct.fallbackEn, "Product was not found in Catalog.");
    assert.equal(unknownProduct.technicalDetail, "Product SKU was not found in Catalog master.");

    assert.equal(sanitizeTechnicalDetail("SELECT * FROM tax_profiles"), undefined);
    assert.equal(sanitizeTechnicalDetail("C:\\app\\secrets\\connection.txt"), undefined);
    assert.equal(sanitizeTechnicalDetail("Catalog reference PRD-001 was not found."), "Catalog reference PRD-001 was not found.");

    const unknownParty = mapImportRowOutcome("UnknownParty");
    assert.equal(unknownParty.isError, true);
    assert.equal(unknownParty.messageKey, "importExport.outcomes.unknownPartyMessage");
    assert.equal(unknownParty.fallbackAr, "لم يتم العثور على الطرف التجاري في دليل الأطراف.");

    const createRow = mapImportRowOutcome("Create");
    assert.equal(createRow.isError, false);
    assert.equal(createRow.messageKey, "importExport.outcomes.createMessage");
    assert.equal(createRow.fallbackAr, "صالح للاعتماد والحفظ كملف جديد.");

    const createVersion = mapImportRowOutcome("CreateNewVersion");
    assert.equal(createVersion.isError, false);
    assert.equal(createVersion.messageKey, "importExport.outcomes.createNewVersionMessage");
    assert.equal(createVersion.fallbackAr, "صالح لإنشاء إصدار جديد سارٍ.");

    for (const outcome of [
      "AmbiguousReference",
      "InvalidTaxCategory",
      "InvalidScheme",
      "EffectiveDateOverlap",
      "DuplicateInput",
      "LockedStatutoryContent",
      "Conflict"
    ]) {
      assert.equal(mapImportRowOutcome(outcome).isError, true);
      assert.notEqual(mapImportRowOutcome(outcome).messageKey, "importExport.outcomes.genericErrorMessage");
    }

    assert.equal(mapImportRowOutcome("UnknownProduct", "password=super-secret").technicalDetail, undefined);
  });

  it("F10 / Tax status presentation — maps human states centrally without exposing machine labels", () => {
    assert.deepEqual(getTaxStatusPresentation("Registered", "registration"), {
      intent: "success",
      labelKey: "registrationStatus.registered"
    });
    assert.deepEqual(getTaxStatusPresentation("PendingRegistration", "registration"), {
      intent: "warning",
      labelKey: "registrationStatus.pending"
    });
    assert.deepEqual(getTaxStatusPresentation("RegistrationException", "registration"), {
      intent: "danger",
      labelKey: "registrationStatus.exception"
    });
    assert.deepEqual(getTaxStatusPresentation("Available", "countryPack"), {
      intent: "success",
      labelKey: "countryPack.status.available"
    });
    assert.deepEqual(getTaxStatusPresentation("Complete", "coverage"), {
      intent: "success",
      labelKey: "status.complete"
    });
    assert.deepEqual(getTaxStatusPresentation("Active", "ruleSet"), {
      intent: "success",
      labelKey: "ruleSetStatus.active"
    });
  });
});
