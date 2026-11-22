import { ApiError } from "../../../core/api/http-client.ts";

export interface MappedHrError {
  readonly code: string;
  readonly messageKey: string;
  readonly defaultMessage: string;
}

const HR_ERROR_MAP: Record<string, { messageKey: string; defaultMessage: string }> = {
  "hr.employee_arabic_name_required": {
    messageKey: "errors.employeeArabicNameRequired",
    defaultMessage: "Arabic name is required for employee creation."
  },
  "hr.employee_name_too_long": {
    messageKey: "errors.employeeNameTooLong",
    defaultMessage: "Employee name exceeds maximum length."
  },
  "hr.employment_number_invalid": {
    messageKey: "errors.employmentNumberInvalid",
    defaultMessage: "Invalid employment number."
  },
  "hr.employment_date_range_invalid": {
    messageKey: "errors.employmentDateRangeInvalid",
    defaultMessage: "Employment start date must precede end date."
  },
  "hr.leave_balance_insufficient": {
    messageKey: "errors.leaveBalanceInsufficient",
    defaultMessage: "Insufficient leave balance for this request."
  },
  "hr.leave_request_dates_overlap": {
    messageKey: "errors.leaveDatesOverlap",
    defaultMessage: "Leave request dates overlap with an existing approved or pending request."
  },
  "hr.attendance_record_payroll_locked": {
    messageKey: "errors.attendancePayrollLocked",
    defaultMessage: "Attendance record is locked by finalized payroll and cannot be modified directly."
  },
  "hr.attendance_correction_reason_required": {
    messageKey: "errors.attendanceCorrectionReasonRequired",
    defaultMessage: "A reason is mandatory for manual attendance corrections."
  },
  "hr.assignment_date_invalid": {
    messageKey: "errors.assignmentDateInvalid",
    defaultMessage: "Assignment effective date range is invalid."
  },
  "hr.contract_date_range_invalid": {
    messageKey: "errors.contractDateRangeInvalid",
    defaultMessage: "Contract end date must be after the start date."
  },
  "hr.contract_already_ended": {
    messageKey: "errors.contractAlreadyEnded",
    defaultMessage: "Contract is already ended and cannot be modified."
  },
  "hr.shift_break_exceeds_duration": {
    messageKey: "errors.shiftBreakExceedsDuration",
    defaultMessage: "Shift break duration cannot exceed total shift length."
  },
  "hr.disciplinary_amount_not_allowed": {
    messageKey: "errors.disciplinaryAmountNotAllowed",
    defaultMessage: "Monetary amount is not allowed on disciplinary actions directly; payroll rules govern deductions."
  },
  "hr.adjustment_payroll_input_invalid": {
    messageKey: "errors.adjustmentPayrollInputInvalid",
    defaultMessage: "Invalid payroll impact parameters for this HR adjustment."
  },
  "hr.insurance_already_active": {
    messageKey: "errors.insuranceAlreadyActive",
    defaultMessage: "Active insurance enrollment already exists for this scheme."
  },
  "hr.file_scan_failed": {
    messageKey: "errors.fileScanFailed",
    defaultMessage: "Document security scan failed or file rejected by antivirus scanner."
  },
  "hr.employee_stale": {
    messageKey: "errors.employeeStale",
    defaultMessage: "The record was modified by another user. Please refresh and try again."
  },
  "hr.employee_concurrency_conflict": {
    messageKey: "errors.employeeStale",
    defaultMessage: "The record was modified by another user. Please refresh and try again."
  },
  "hr.employee_profile_update_invalid": {
    messageKey: "errors.generic",
    defaultMessage: "Employee profile update is invalid."
  },
  "hr.forbidden": {
    messageKey: "errors.forbidden",
    defaultMessage: "You do not have permission to perform this HR action."
  }
};

export function mapHrError(error: unknown): MappedHrError {
  if (error instanceof ApiError) {
    const code = error.problemDetails?.code || (error.status === 403 ? "hr.forbidden" : "hr.unknown");
    const mapped = HR_ERROR_MAP[code];
    if (mapped) {
      return {
        code,
        messageKey: mapped.messageKey,
        defaultMessage: mapped.defaultMessage
      };
    }
    return {
      code,
      messageKey: "errors.generic",
      defaultMessage: error.message || "An error occurred while processing the HR request."
    };
  }

  if (error instanceof Error) {
    return {
      code: "hr.unknown",
      messageKey: "errors.generic",
      defaultMessage: error.message
    };
  }

  return {
    code: "hr.unknown",
    messageKey: "errors.generic",
    defaultMessage: "An unexpected error occurred."
  };
}
