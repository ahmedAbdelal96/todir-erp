export type ProjectType = "CustomerProject" | "InternalProject" | "CapitalProject";

export type ProjectLifecycleState =
  | "Draft"
  | "Active"
  | "OnHold"
  | "ExecutionCompleted"
  | "Closed"
  | "Cancelled";

export type ProjectExecutionHealth = "OnTrack" | "AtRisk" | "OffTrack" | "NotAssessed";

export type ProjectBillingModel =
  | "NonBillable"
  | "TimeAndMaterials"
  | "FixedPrice"
  | "Milestone"
  | "CostPlus";

export type ProjectTaskStatus = "Planned" | "InProgress" | "Blocked" | "Completed" | "Cancelled";
export type ProjectTaskPriority = "Low" | "Normal" | "High" | "Urgent";
export type MilestoneStatus = "Planned" | "Reached" | "Accepted" | "Rejected";
export type DeliverableStatus = "Draft" | "Submitted" | "Accepted" | "Rejected";
export type TimeEntryState = "Draft" | "Submitted" | "Approved" | "Finalized" | "Rejected" | "Corrected";
export type ChargeState = "Eligible" | "Requested" | "PartiallyInvoiced" | "Invoiced" | "WrittenOff";
export type ChangeRequestStatus = "Draft" | "Submitted" | "Approved" | "Rejected" | "Cancelled";

export type MilestoneActionKind = "reach" | "accept" | "reject";
export type DeliverableActionKind = "submit" | "accept" | "reject";
export type TimeActionKind = "submit" | "approve" | "reject" | "finalize" | "correct";

export interface PagedResult<T> {
  readonly items: readonly T[];
  readonly page: number;
  readonly pageSize: number;
  readonly hasNext: boolean;
  readonly totalCount?: number;
}

export interface ProjectListItem {
  readonly id: string;
  readonly legalEntityId: string;
  readonly projectNumber: string;
  readonly name: string;
  readonly projectType: ProjectType;
  readonly customerPartyId: string | null;
  readonly lifecycleState: ProjectLifecycleState;
  readonly executionHealth: ProjectExecutionHealth;
  readonly billingModel: ProjectBillingModel;
  readonly startDate: string;
  readonly targetEndDate: string | null;
  readonly version: number;
}

export interface ProjectDetail {
  readonly id: string;
  readonly legalEntityId: string;
  readonly projectNumber: string;
  readonly name: string;
  readonly description: string | null;
  readonly projectType: ProjectType;
  readonly customerPartyId: string | null;
  readonly projectManagerEmployeeId: string | null;
  readonly startDate: string;
  readonly targetEndDate: string | null;
  readonly actualEndDate: string | null;
  readonly lifecycleState: ProjectLifecycleState;
  readonly executionHealth: ProjectExecutionHealth;
  readonly billingModel: ProjectBillingModel;
  readonly currencyCode: string | null;
  readonly version: number;
  readonly budget: number;
  readonly actualCost: number;
  readonly openCommitment: number;
  readonly etc: number;
  readonly eac: number;
  readonly billingEligible: number;
  readonly invoiced: number;
  readonly collected: number;
  readonly outstanding: number;
  readonly sourceType: string | null;
  readonly sourceRef: string | null;
}

export interface FinancialSummary {
  readonly budget: number;
  readonly actualCost: number;
  readonly openCommitment: number;
  readonly etc: number;
  readonly eac: number;
  readonly billingEligible: number;
  readonly invoiced: number;
  readonly collected: number;
  readonly outstanding: number;
  readonly expectedMargin: number;
  readonly revenueBasis: string;
  readonly revenueBasisAmount: number;
  readonly currencyCode: string | null;
}

export interface ProjectTaskView {
  readonly id: string;
  readonly projectId: string;
  readonly title: string;
  readonly description: string | null;
  readonly status: ProjectTaskStatus;
  readonly priority: ProjectTaskPriority;
  readonly assignedEmployeeId: string | null;
  readonly plannedStart: string | null;
  readonly plannedEnd: string | null;
  readonly progressPercent: number;
  readonly sequenceNo: number;
  readonly version: number;
}

export interface MilestoneView {
  readonly id: string;
  readonly projectId: string;
  readonly taskId: string | null;
  readonly name: string;
  readonly dueDate: string | null;
  readonly reachedAt: string | null;
  readonly acceptedAt: string | null;
  readonly acceptanceReference: string | null;
  readonly status: MilestoneStatus;
}

export interface TaskDependencyView {
  readonly id: string;
  readonly projectId: string;
  readonly predecessorTaskId: string;
  readonly successorTaskId: string;
  readonly dependencyType: string;
  readonly lagDays: number;
}

export interface ProjectDeliverableView {
  readonly id: string;
  readonly projectId: string;
  readonly taskId: string | null;
  readonly milestoneId: string | null;
  readonly name: string;
  readonly description: string | null;
  readonly status: DeliverableStatus;
  readonly submittedAt: string | null;
  readonly acceptedAt: string | null;
  readonly acceptanceReference: string | null;
}

export interface ProjectTeamMemberView {
  readonly id: string;
  readonly employeeId: string | null;
  readonly userId: string | null;
  readonly roleCode: string;
  readonly allocationPercent: number;
  readonly activeFrom: string;
  readonly activeTo: string | null;
  readonly isManager: boolean;
}

export interface ProjectResourceRequirementView {
  readonly id: string;
  readonly projectId: string;
  readonly roleCode: string;
  readonly quantity: number;
  readonly requiredFrom: string | null;
  readonly requiredTo: string | null;
  readonly status: string;
}

export interface ProjectResourceAllocationView {
  readonly id: string;
  readonly projectId: string;
  readonly requirementId: string | null;
  readonly employeeId: string | null;
  readonly resourceRef: string | null;
  readonly allocatedFrom: string;
  readonly allocatedTo: string | null;
  readonly allocationPercent: number;
  readonly status: string;
}

export interface TimeEntryView {
  readonly id: string;
  readonly projectId: string;
  readonly taskId: string | null;
  readonly employeeId: string;
  readonly workDate: string;
  readonly durationMinutes: number;
  readonly billable: boolean;
  readonly state: TimeEntryState;
  readonly costAmount: number | null;
  readonly billingAmount: number | null;
  readonly version: number;
  readonly notes?: string | null | undefined;
}

export interface RateCardView {
  readonly id: string;
  readonly legalEntityId: string;
  readonly projectId: string | null;
  readonly code: string;
  readonly name: string;
  readonly currencyCode: string;
  readonly effectiveFrom: string;
  readonly effectiveTo: string | null;
  readonly state: string;
}

export interface BudgetView {
  readonly id: string;
  readonly projectId: string;
  readonly name: string;
  readonly currencyCode: string;
  readonly controlMode: string;
  readonly originalAmount: number;
  readonly currentAmount: number;
  readonly state: string;
  readonly versionNo: number;
}

export interface BudgetVersionView {
  readonly id: string;
  readonly budgetId: string;
  readonly versionNo: number;
  readonly state: string;
  readonly originalAmount: number;
  readonly currentAmount: number;
}

export interface ForecastView {
  readonly id: string;
  readonly projectId: string;
  readonly versionNo: number;
  readonly state: string;
  readonly actualAmount: number;
  readonly openCommitmentAmount: number;
  readonly etcAmount: number;
  readonly eacAmount: number;
  readonly includesOpenCommitments: boolean;
  readonly assumptions: string | null;
}

export interface CostFactView {
  readonly id: string;
  readonly projectId: string;
  readonly sourceModule: string;
  readonly sourceType: string;
  readonly sourceId: string;
  readonly costCategory: string;
  readonly originalAmount: number;
  readonly originalCurrency: string;
  readonly baseAmount: number;
  readonly baseCurrency: string;
  readonly state: string;
  readonly correctionOfId: string | null;
}

export interface CommitmentView {
  readonly id: string;
  readonly projectId: string;
  readonly sourceModule: string;
  readonly sourceId: string;
  readonly committedAmount: number;
  readonly actualizedAmount: number;
  readonly openAmount: number;
  readonly state: string;
  readonly currencyCode: string;
}

export interface BillingRuleView {
  readonly id: string;
  readonly projectId: string;
  readonly versionNo: number;
  readonly model: string;
  readonly state: string;
  readonly contractValue: number | null;
  readonly currencyCode: string | null;
  readonly markupPercent: number;
  readonly billingCap: number | null;
}

export interface BillingPlanLineView {
  readonly id: string;
  readonly billingRuleId: string;
  readonly milestoneId: string | null;
  readonly dueDate: string | null;
  readonly amount: number;
  readonly status: string;
}

export interface BillableChargeView {
  readonly id: string;
  readonly projectId: string;
  readonly sourceType: string;
  readonly sourceId: string;
  readonly costAmount: number;
  readonly billableAmount: number;
  readonly currencyCode: string;
  readonly state: ChargeState;
  readonly invoiceRequestId: string | null;
}

export interface InvoiceRequestView {
  readonly id: string;
  readonly projectId: string;
  readonly requestedAmount: number;
  readonly currencyCode: string;
  readonly state: string;
  readonly invoiceId: string | null;
}

export interface ChangeRequestView {
  readonly id: string;
  readonly projectId: string;
  readonly requestNumber: string;
  readonly title: string;
  readonly status: ChangeRequestStatus;
  readonly contractDelta: number;
  readonly budgetDelta: number;
  readonly scheduleDeltaDays: number;
}

export interface CapitalHandoffView {
  readonly id: string;
  readonly projectId: string;
  readonly costFactId: string;
  readonly amount: number;
  readonly currencyCode: string;
  readonly state: string;
  readonly externalReference: string | null;
}

export interface ProjectTemplateView {
  readonly id: string;
  readonly legalEntityId: string | null;
  readonly code: string;
  readonly name: string;
  readonly projectType: string;
  readonly status: string;
  readonly version: number;
  readonly effectiveFrom: string;
  readonly effectiveTo: string | null;
  readonly definitionJson: string;
}

export interface ProjectBaselineView {
  readonly id: string;
  readonly projectId: string;
  readonly baselineType: string;
  readonly versionNo: number;
  readonly snapshotJson: string;
  readonly createdBy: string;
  readonly createdAt: string;
}

export interface ProjectDocumentView {
  readonly id: string;
  readonly projectId: string;
  readonly documentRef: string;
  readonly documentType: string;
  readonly sensitivity: string;
  readonly title: string | null;
  readonly createdBy: string;
  readonly createdAt: string;
}

export interface ProjectTimelineEventView {
  readonly id: string;
  readonly projectId: string;
  readonly action: string;
  readonly actorId: string | null;
  readonly occurredAt: string;
  readonly beforeJson: string | null;
  readonly afterJson: string | null;
}

export interface CloseView {
  readonly projectId: string;
  readonly state: string;
  readonly blockers: readonly string[];
}

export interface ProjectCloseExceptionView {
  readonly id: string;
  readonly projectId: string;
  readonly projectCloseId: string;
  readonly exceptionType: string;
  readonly referenceId: string | null;
  readonly message: string;
  readonly resolvedAt: string | null;
}

export interface ProjectPortfolioItem {
  readonly id: string;
  readonly legalEntityId: string;
  readonly projectNumber: string;
  readonly name: string;
  readonly lifecycleState: ProjectLifecycleState;
  readonly executionHealth: ProjectExecutionHealth;
  readonly budget: number;
  readonly actualCost: number;
  readonly openCommitment: number;
  readonly eac: number;
  readonly billingEligible: number;
  readonly outstanding: number;
}

export interface ProjectResourceLoadView {
  readonly resourceKey: string;
  readonly allocationPercent: number;
  readonly projectCount: number;
}

// Request Types
export interface CreateProjectRequest {
  readonly legalEntityId: string;
  readonly name: string;
  readonly projectType: ProjectType;
  readonly customerPartyId?: string | null;
  readonly projectManagerEmployeeId?: string | null;
  readonly startDate: string;
  readonly targetEndDate?: string | null;
  readonly billingModel?: ProjectBillingModel;
  readonly currencyCode?: string | null;
  readonly description?: string | null;
  readonly sourceType?: string | null;
  readonly sourceRef?: string | null;
  readonly initialBudget?: number | null;
  readonly templateVersionId?: string | null;
}

export interface CreateProjectFromSalesSourceRequest {
  readonly legalEntityId: string;
  readonly quotationId: string;
  readonly acceptedRevision: number;
  readonly name: string;
  readonly projectType?: ProjectType;
  readonly projectManagerEmployeeId?: string | null;
  readonly startDate?: string | null;
  readonly targetEndDate?: string | null;
  readonly billingModel?: ProjectBillingModel;
  readonly initialBudget?: number | null;
  readonly templateVersionId?: string | null;
  readonly description?: string | null;
  readonly idempotencyKey: string;
}

export interface UpdateProjectRequest {
  readonly name: string;
  readonly description?: string | null;
  readonly targetEndDate?: string | null;
  readonly executionHealth?: ProjectExecutionHealth | null;
  readonly version: number;
}

export interface TimeActionRequest {
  readonly version: number;
  readonly reason?: string | null;
}

export interface CreateProjectTaskRequest {
  readonly title: string;
  readonly description?: string | null;
  readonly priority?: ProjectTaskPriority;
  readonly assignedEmployeeId?: string | null;
  readonly plannedStart?: string | null;
  readonly plannedEnd?: string | null;
  readonly workNodeId?: string | null;
}

export interface CreateTaskDependencyRequest {
  readonly predecessorTaskId: string;
  readonly successorTaskId: string;
  readonly dependencyType?: string;
  readonly lagDays?: number;
}

export interface CreateMilestoneRequest {
  readonly name: string;
  readonly dueDate?: string | null;
  readonly taskId?: string | null;
}

export interface MilestoneActionRequest {
  readonly acceptanceReference?: string | null;
  readonly reason?: string | null;
}

export interface AddProjectTeamMemberRequest {
  readonly employeeId?: string | null;
  readonly userId?: string | null;
  readonly roleCode: string;
  readonly allocationPercent?: number;
  readonly activeFrom?: string | null;
  readonly activeTo?: string | null;
  readonly isManager?: boolean;
}

export interface CreateTimeEntryRequest {
  readonly projectId: string;
  readonly taskId?: string | null;
  readonly employeeId: string;
  readonly workDate: string;
  readonly durationMinutes: number;
  readonly description?: string | null;
  readonly billable: boolean;
  readonly idempotencyKey?: string | null;
}

export interface CreateRateCardRequest {
  readonly legalEntityId: string;
  readonly code: string;
  readonly name: string;
  readonly currencyCode: string;
  readonly effectiveFrom: string;
  readonly effectiveTo?: string | null;
  readonly lines: readonly RateCardLineRequest[];
}

export interface RateCardLineRequest {
  readonly roleCode: string;
  readonly employeeId?: string | null;
  readonly costRate?: number | null;
  readonly billingRate?: number | null;
}

export interface BudgetLineRequest {
  readonly categoryCode: string;
  readonly amount: number;
  readonly description?: string | null;
}

export interface CreateBudgetRequest {
  readonly name: string;
  readonly currencyCode: string;
  readonly originalAmount: number;
  readonly controlMode?: string;
  readonly lines?: readonly BudgetLineRequest[] | null;
}

export interface CreateBudgetRevisionRequest {
  readonly currentAmount: number;
  readonly lines?: readonly BudgetLineRequest[] | null;
  readonly reason?: string | null;
}

export interface CreateForecastRequest {
  readonly etcAmount: number;
  readonly includesOpenCommitments?: boolean;
  readonly assumptions?: string | null;
}

export interface RecordCostFactRequest {
  readonly projectId: string;
  readonly taskId?: string | null;
  readonly sourceModule: string;
  readonly sourceType: string;
  readonly sourceId: string;
  readonly sourceLineRef?: string | null;
  readonly costCategory: string;
  readonly originalCurrency: string;
  readonly originalAmount: number;
  readonly baseCurrency: string;
  readonly baseAmount: number;
  readonly exchangeRate?: number | null;
  readonly factDate: string;
  readonly idempotencyKey: string;
  readonly correctionOfId?: string | null;
  readonly legalEntityId?: string | null;
}

export interface RecordCommitmentRequest {
  readonly projectId: string;
  readonly sourceModule: string;
  readonly sourceId: string;
  readonly commitmentDate: string;
  readonly currencyCode: string;
  readonly committedAmount: number;
  readonly actualizedAmount: number;
  readonly idempotencyKey: string;
  readonly legalEntityId?: string | null;
}

export interface CreateBillingRuleRequest {
  readonly model: string;
  readonly currencyCode: string;
  readonly contractValue?: number | null;
  readonly markupPercent?: number;
  readonly billingCap?: number | null;
  readonly effectiveFrom?: string | null;
}

export interface CreateBillingPlanLineRequest {
  readonly billingRuleId: string;
  readonly milestoneId?: string | null;
  readonly dueDate?: string | null;
  readonly amount: number;
}

export interface CreateBillableChargeRequest {
  readonly projectId: string;
  readonly sourceType: string;
  readonly sourceId: string;
  readonly chargeDate: string;
  readonly description: string;
  readonly currencyCode: string;
  readonly costAmount: number;
  readonly billableAmount: number;
  readonly idempotencyKey: string;
}

export interface RequestInvoiceRequest {
  readonly projectId: string;
  readonly chargeIds: readonly string[];
  readonly currencyCode: string;
  readonly requestedAmount: number;
  readonly idempotencyKey: string;
}

export interface CreateChangeRequest {
  readonly title: string;
  readonly reason?: string | null;
  readonly contractDelta: number;
  readonly budgetDelta: number;
  readonly scheduleDeltaDays: number;
}

export interface ChangeDecisionRequest {
  readonly decision: "approve" | "reject";
  readonly reason?: string | null;
}

export interface CapitalHandoffRequest {
  readonly costFactId: string;
  readonly amount: number;
  readonly currencyCode: string;
  readonly idempotencyKey: string;
}

export interface CreateProjectTemplateRequest {
  readonly legalEntityId?: string | null;
  readonly code: string;
  readonly name: string;
  readonly projectType: string;
  readonly definitionJson?: string;
}

export interface CreateResourceRequirementRequest {
  readonly roleCode: string;
  readonly quantity?: number;
  readonly requiredFrom?: string | null;
  readonly requiredTo?: string | null;
}

export interface AllocateProjectResourceRequest {
  readonly requirementId?: string | null;
  readonly employeeId?: string | null;
  readonly resourceRef?: string | null;
  readonly allocatedFrom: string;
  readonly allocatedTo?: string | null;
  readonly allocationPercent: number;
  readonly status?: string;
}

export interface CreateProjectDeliverableRequest {
  readonly name: string;
  readonly description?: string | null;
  readonly taskId?: string | null;
  readonly milestoneId?: string | null;
}

export interface DeliverableActionRequest {
  readonly acceptanceReference?: string | null;
  readonly reason?: string | null;
}

export interface CreateProjectDocumentRequest {
  readonly documentRef: string;
  readonly documentType: string;
  readonly title?: string | null;
  readonly sensitivity?: string;
}

export interface CreateProjectBaselineRequest {
  readonly baselineType: string;
  readonly snapshotJson?: string;
}

export interface CreateCloseExceptionRequest {
  readonly exceptionType: string;
  readonly referenceId?: string | null;
  readonly message: string;
}
