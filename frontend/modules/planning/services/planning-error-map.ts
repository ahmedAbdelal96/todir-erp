import { ApiError } from "../../../core/api/http-client.ts";

export interface MappedPlanningError {
  readonly code: string;
  readonly messageKey: string;
  readonly status?: number;
}

const ERROR_MAP: Record<string, string> = {
  "planning.cycle_not_found": "errors.cycleNotFound",
  "planning.version_not_found": "errors.versionNotFound",
  "planning.version_locked": "errors.versionLocked",
  "planning.version_stale": "errors.versionStale",
  "planning.published_immutable": "errors.publishedImmutable",
  "planning.submission_locked": "errors.submissionLocked",
  "planning.invalid_transition": "errors.invalidTransition",
  "planning.budget_exceeded": "errors.budgetExceeded",
  "planning.budget_approval_required": "errors.budgetApprovalRequired",
  "planning.transfer_not_balanced": "errors.transferNotBalanced",
  "planning.formula_invalid": "errors.formulaInvalid",
  "planning.circular_dependency": "errors.circularDependency",
  "planning.fx_rate_missing": "errors.fxRateMissing",
  "planning.calculation_running": "errors.calculationRunning",
  "planning.calculation_not_retryable": "errors.calculationNotRetryable",
  "planning.import_validation_failed": "errors.importValidationFailed",
  "planning.sod_violation": "errors.sodViolation",
  "planning.permission_denied": "errors.permissionDenied",
  "module.lifecycle_readonly": "errors.moduleReadOnly",
  "module.not_entitled": "errors.moduleNotEntitled"
};

export function mapPlanningError(error: unknown): MappedPlanningError {
  if (error instanceof ApiError) {
    const rawCode = (error.problemDetails as { code?: string; detail?: string } | undefined)?.code ?? "";
    if (rawCode && ERROR_MAP[rawCode]) {
      return { code: rawCode, messageKey: ERROR_MAP[rawCode]!, status: error.status };
    }
    if (error.status === 403) {
      return { code: rawCode || "planning.forbidden", messageKey: "errors.permissionDenied", status: 403 };
    }
    if (error.status === 404) {
      return { code: rawCode || "planning.not_found", messageKey: "errors.notFound", status: 404 };
    }
    if (error.status === 409) {
      return { code: rawCode || "planning.conflict", messageKey: "errors.versionStale", status: 409 };
    }
    return {
      code: rawCode || `planning.http_${error.status}`,
      messageKey: "errors.generic",
      status: error.status
    };
  }

  if (error instanceof Error && error.message && ERROR_MAP[error.message]) {
    return { code: error.message, messageKey: ERROR_MAP[error.message]! };
  }

  return { code: "planning.unknown", messageKey: "errors.generic" };
}
