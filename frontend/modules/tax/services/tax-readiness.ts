import type { TaxCoverageResult } from "../types.ts";

export type TaxOverallReadiness = "Blocked" | "Warning" | "Ready";

/**
 * Derives overall Tax engine readiness strictly and hierarchically:
 * 1. If ANY blocking issue, disabled capability, or missing/inactive registration exists -> "Blocked".
 * 2. Else if ANY warning, unclassified product, inactive rule set, or open issue exists -> "Warning".
 * 3. Only when ALL mandatory technical readiness requirements are satisfied -> "Ready".
 *
 * NOTE: Tax Capability "Enabled" != "Ready". Enabled simply permits calculation; true readiness
 * requires active registration, valid country pack, active rule set, and clean coverage.
 */
export function deriveOverallReadiness(coverage: TaxCoverageResult | null | undefined): TaxOverallReadiness {
  if (!coverage) {
    return "Blocked";
  }

  // 1. Explicit capability disabled is strictly Blocked
  if (coverage.capability === "Disabled") {
    return "Blocked";
  }

  // 2. Inactive or missing tax registration is strictly Blocked
  if (
    !coverage.registrationEffective ||
    coverage.registrationStatus === "Missing" ||
    coverage.registrationStatus === "Suspended" ||
    coverage.registrationStatus === "Deregistered"
  ) {
    return "Blocked";
  }

  // 3. Explicit backend health Blocked
  if (coverage.health === "Blocked") {
    return "Blocked";
  }

  // 4. Any blocking issue reported by coverage
  const issues = coverage.issues ?? [];
  const hasBlockingIssue = issues.some((issue) => {
    const code = (issue.code ?? "").toLowerCase();
    return (
      code.includes("block") ||
      code.includes("disabled") ||
      code === "registrationmissing" ||
      code === "registrationnoteffective" ||
      code === "countrypackmissing" ||
      code === "countrypackincompatible" ||
      code === "rulesetmissing" ||
      code === "missingrule" ||
      code === "ruleconflict" ||
      code === "roundingpolicymissing" ||
      code === "productclassificationmissing" ||
      code === "productclassificationexpired" ||
      code === "partytaxdatamissing"
    );
  });

  if (hasBlockingIssue) {
    return "Blocked";
  }

  // 5. Warning / Needs Attention checks
  const isHealthWarning = coverage.health === "Warning" || coverage.health === "NeedsAttention";
  const isProductIncomplete =
    coverage.productClassificationCoverage !== "Complete" &&
    coverage.productClassificationCoverage !== "Classified";
  const isRuleSetNotActive = coverage.activeRuleSetStatus !== "Active";
  const isPartyIncomplete =
    coverage.partyProfileReadiness !== "Complete" &&
    coverage.partyProfileReadiness !== "Configured";
  const isCountryPackNotReady =
    coverage.countryPackStatus === "Missing" ||
    coverage.countryPackStatus === "Incompatible" ||
    coverage.countryPackStatus === "Pending";
  const hasIssues = issues.length > 0;

  if (
    isHealthWarning ||
    isProductIncomplete ||
    isRuleSetNotActive ||
    isPartyIncomplete ||
    isCountryPackNotReady ||
    hasIssues
  ) {
    return "Warning";
  }

  // 6. Only when all mandatory requirements are fully met
  return "Ready";
}
