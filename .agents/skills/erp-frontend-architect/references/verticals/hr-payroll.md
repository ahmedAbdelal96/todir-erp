# TODIR HR, Personnel & Payroll Domain Pack

Canonical specifications:
- Modules: `hr`, `payroll`

---

## Core Entities & Workflows

1. **Employee Management (`سجل الموظف`)**:
   - 360 Employee Profile: Personal info, employment contract, department, position, identity documents, credentials.
   - Privacy Invariant: Sensitive documents and compensation data guarded by dedicated permissions (`hr.compensation.read`, `hr.employee.document.read_sensitive`).
2. **Attendance & Leaves (`الحضور والإجازات`)**:
   - Shift assignments, attendance logs, overtime approvals, leave requests and balance tracking.
3. **Payroll Runs (`مسير الرواتب`)**:
   - Monthly payroll calculation cycle: **Inputs -> Calculate -> Review -> Approve -> Finalize -> Bank File / Accounting Preview**.
   - Finalized payroll lock prevents retroactive modifications.
   - Payslip export with complete earnings and deductions breakdown.
