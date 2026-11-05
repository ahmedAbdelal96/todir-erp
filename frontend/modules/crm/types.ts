export interface CrmPage<T> {
  readonly items: readonly T[];
  readonly totalCount: number;
  readonly page: number;
  readonly pageSize: number;
}

export type LeadStatus = "New" | "Working" | "Qualified" | "Disqualified" | "Converted";
export type LeadPriority = "Low" | "Normal" | "High";

export interface Lead {
  readonly id: string;
  readonly legalEntityId: string;
  readonly displayName: string;
  readonly companyName?: string | null;
  readonly email?: string | null;
  readonly phone?: string | null;
  readonly source: string;
  readonly status: LeadStatus;
  readonly ownerId?: string | null;
  readonly teamId?: string | null;
  readonly priority: LeadPriority;
  readonly score: number;
  readonly partyId?: string | null;
  readonly opportunityId?: string | null;
  readonly nextActionAt?: string | null;
  readonly version: number;
  readonly createdAt: string;
  readonly duplicateCandidateIds: readonly string[];
}

export interface CreateLeadRequest {
  readonly legalEntityId: string;
  readonly displayName: string;
  readonly companyName?: string | null;
  readonly email?: string | null;
  readonly phone?: string | null;
  readonly source: string;
  readonly ownerId?: string | null;
  readonly teamId?: string | null;
  readonly priority?: LeadPriority;
  readonly interest?: string | null;
  readonly notes?: string | null;
}

export interface UpdateLeadRequest {
  readonly displayName: string;
  readonly companyName?: string | null;
  readonly email?: string | null;
  readonly phone?: string | null;
  readonly source: string;
  readonly priority: LeadPriority;
  readonly version: number;
}

export interface AssignLeadRequest {
  readonly ownerId?: string | null;
  readonly teamId?: string | null;
  readonly version: number;
}

export interface QualifyLeadRequest {
  readonly partyId?: string | null;
  readonly need?: string | null;
  readonly budget?: string | null;
  readonly timing?: string | null;
  readonly decisionRole?: string | null;
  readonly version: number;
}

export interface DisqualifyLeadRequest {
  readonly reason: string;
  readonly version: number;
}

export interface ConvertLeadRequest {
  readonly existingPartyId?: string | null;
  readonly newPartyType?: string | null;
  readonly newPartyName?: string | null;
  readonly pipelineId: string;
  readonly stageId: string;
  readonly opportunityName: string;
  readonly estimatedAmount: number;
  readonly currencyCode: string;
  readonly expectedCloseDate?: string | null;
  readonly idempotencyKey: string;
  readonly version: number;
}

export interface MergeLeadsRequest {
  readonly sourceLeadId: string;
  readonly targetLeadId: string;
  readonly sourceVersion: number;
  readonly targetVersion: number;
}

export interface ImportLeadsRequest {
  readonly csv: string;
}

export interface ImportLeadsResult {
  readonly imported: number;
  readonly duplicateCandidates: number;
  readonly errors: readonly string[];
}

export type OpportunityStatus = "Open" | "Won" | "Lost";
export type ForecastCategory = "Pipeline" | "BestCase" | "Commit" | "Closed";
export type DealHealthStatus = "Healthy" | "Good" | "Fair" | "AtRisk" | "Stale";

export interface Opportunity {
  readonly id: string;
  readonly legalEntityId: string;
  readonly partyId?: string | null;
  readonly leadId?: string | null;
  readonly pipelineId: string;
  readonly stageId: string;
  readonly pipelineName: string;
  readonly stageName: string;
  readonly name: string;
  readonly ownerId?: string | null;
  readonly teamId?: string | null;
  readonly estimatedAmount: number;
  readonly currencyCode: string;
  readonly expectedCloseDate?: string | null;
  readonly forecastCategory: ForecastCategory;
  readonly status: OpportunityStatus;
  readonly lossReason?: string | null;
  readonly competitor?: string | null;
  readonly nextActionAt?: string | null;
  readonly healthScore: number;
  readonly healthStatus: DealHealthStatus;
  readonly version: number;
  readonly createdAt: string;
}

export interface CreateOpportunityRequest {
  readonly legalEntityId: string;
  readonly partyId?: string | null;
  readonly leadId?: string | null;
  readonly pipelineId: string;
  readonly stageId: string;
  readonly name: string;
  readonly estimatedAmount: number;
  readonly currencyCode: string;
  readonly expectedCloseDate?: string | null;
  readonly ownerId?: string | null;
  readonly teamId?: string | null;
  readonly forecastCategory?: ForecastCategory;
}

export interface UpdateOpportunityRequest {
  readonly name: string;
  readonly estimatedAmount: number;
  readonly expectedCloseDate?: string | null;
  readonly forecastCategory: ForecastCategory;
  readonly version: number;
}

export interface AssignOpportunityRequest {
  readonly ownerId?: string | null;
  readonly teamId?: string | null;
  readonly version: number;
}

export interface MoveOpportunityStageRequest {
  readonly stageId: string;
  readonly version: number;
  readonly note?: string | null;
}

export interface CloseOpportunityRequest {
  readonly version: number;
  readonly reason?: string | null;
  readonly competitor?: string | null;
}

export interface ForecastCategoryRequest {
  readonly category: ForecastCategory;
  readonly version: number;
}

export interface OpportunityInterest {
  readonly id: string;
  readonly opportunityId: string;
  readonly productVariantId?: string | null;
  readonly description: string;
  readonly quantity: number;
}

export interface UpsertOpportunityInterestRequest {
  readonly productVariantId?: string | null;
  readonly description: string;
  readonly quantity: number;
}

export interface OpportunityContactRole {
  readonly id: string;
  readonly opportunityId: string;
  readonly contactPersonId: string;
  readonly role: string;
  readonly isPrimary: boolean;
}

export interface UpsertOpportunityContactRoleRequest {
  readonly contactPersonId: string;
  readonly role: string;
  readonly isPrimary?: boolean;
}

export type ActivityType = "Call" | "Meeting" | "Task" | "FollowUp" | "Note";
export type ActivityState = "Pending" | "Scheduled" | "Completed" | "Cancelled";

export interface Activity {
  readonly id: string;
  readonly legalEntityId: string;
  readonly leadId?: string | null;
  readonly opportunityId?: string | null;
  readonly partyId?: string | null;
  readonly type: ActivityType;
  readonly subject: string;
  readonly state: ActivityState;
  readonly dueAt: string;
  readonly completedAt?: string | null;
  readonly outcome?: string | null;
  readonly notes?: string | null;
  readonly ownerId?: string | null;
  readonly version: number;
}

export interface CreateActivityRequest {
  readonly legalEntityId: string;
  readonly leadId?: string | null;
  readonly opportunityId?: string | null;
  readonly partyId?: string | null;
  readonly type: ActivityType;
  readonly subject: string;
  readonly dueAt: string;
  readonly notes?: string | null;
  readonly ownerId?: string | null;
  readonly idempotencyKey?: string | null;
}

export interface UpdateActivityRequest {
  readonly subject: string;
  readonly dueAt: string;
  readonly notes?: string | null;
  readonly version: number;
}

export interface CompleteActivityRequest {
  readonly version: number;
  readonly outcome?: string | null;
  readonly notes?: string | null;
  readonly createNextAction?: boolean;
  readonly nextDueAt?: string | null;
  readonly nextSubject?: string | null;
}

export interface PipelineStage {
  readonly id: string;
  readonly pipelineId: string;
  readonly name: string;
  readonly sequence: number;
  readonly probability: number;
  readonly staleAfterDays: number;
  readonly isWon: boolean;
  readonly isLost: boolean;
  readonly status: string;
}

export interface Pipeline {
  readonly id: string;
  readonly legalEntityId: string;
  readonly code: string;
  readonly name: string;
  readonly status: string;
  readonly isDefault: boolean;
  readonly version: number;
  readonly stages: readonly PipelineStage[];
}

export interface CreatePipelineRequest {
  readonly legalEntityId: string;
  readonly code: string;
  readonly name: string;
  readonly isDefault?: boolean;
}

export interface UpdatePipelineRequest {
  readonly name: string;
  readonly isDefault: boolean;
  readonly version: number;
}

export interface CreateStageRequest {
  readonly name: string;
  readonly sequence: number;
  readonly probability?: number;
  readonly staleAfterDays?: number;
  readonly isWon?: boolean;
  readonly isLost?: boolean;
}

export interface ReorderStagesRequest {
  readonly stageIds: readonly string[];
  readonly version: number;
}

export interface TeamMember {
  readonly teamId: string;
  readonly userId: string;
  readonly role: string;
  readonly activeFrom: string;
  readonly activeTo?: string | null;
}

export interface Team {
  readonly id: string;
  readonly legalEntityId: string;
  readonly code: string;
  readonly name: string;
  readonly managerId?: string | null;
  readonly status: string;
}

export interface CreateTeamRequest {
  readonly legalEntityId: string;
  readonly code: string;
  readonly name: string;
  readonly managerId?: string | null;
}

export interface AddTeamMemberRequest {
  readonly userId: string;
  readonly role?: string;
  readonly activeFrom?: string | null;
}

export interface ForecastDto {
  readonly pipelineAmount: number;
  readonly weightedAmount: number;
  readonly bestCaseAmount: number;
  readonly commitAmount: number;
  readonly wonAmount: number;
  readonly openOpportunityCount: number;
  readonly staleOpportunityCount: number;
  readonly overdueCloseCount: number;
  readonly currencyCode: string;
}

export interface CreateForecastSnapshotRequest {
  readonly asOfDate: string;
  readonly currencyCode: string;
}

export interface CrmReportDto {
  readonly leadCount: number;
  readonly qualifiedLeadCount: number;
  readonly convertedLeadCount: number;
  readonly disqualifiedLeadCount: number;
  readonly leadConversionRate: number;
  readonly openOpportunityCount: number;
  readonly wonOpportunityCount: number;
  readonly lostOpportunityCount: number;
  readonly wonAmount: number;
  readonly lostAmount: number;
  readonly staleOpportunityCount: number;
  readonly currencyCode: string;
}

export interface Customer360Dto {
  readonly partyId: string;
  readonly legalEntityId: string;
  readonly displayName: string;
  readonly openOpportunityCount: number;
  readonly openPipelineAmount: number;
  readonly lastContactAt?: string | null;
  readonly nextActionAt?: string | null;
  readonly relationshipHealth: string;
  readonly doNotContact: boolean;
}

export interface CustomFieldDefinition {
  readonly id: string;
  readonly legalEntityId: string;
  readonly entityType: string;
  readonly fieldKey: string;
  readonly label: string;
  readonly dataType: string;
  readonly requiredStageId?: string | null;
  readonly status: string;
  readonly version: number;
}

export interface CreateCustomFieldDefinitionRequest {
  readonly legalEntityId: string;
  readonly entityType: string;
  readonly fieldKey: string;
  readonly label: string;
  readonly dataType: string;
  readonly requiredStageId?: string | null;
}

export interface CustomFieldValue {
  readonly fieldKey: string;
  readonly dataType: string;
  readonly value: unknown;
  readonly version: number;
}

export interface SetCustomFieldValueRequest {
  readonly entityType: string;
  readonly entityId: string;
  readonly values: Record<string, unknown>;
  readonly version?: number | null;
}

export interface QuoteHandoffLine {
  readonly productVariantId?: string | null;
  readonly unitOfMeasureId?: string | null;
  readonly description: string;
  readonly quantity: number;
  readonly unitPrice: number;
  readonly discountAmount?: number;
  readonly uomCode?: string | null;
}

export interface QuoteHandoffRequest {
  readonly idempotencyKey: string;
  readonly quoteDate: string;
  readonly validUntil?: string | null;
  readonly currencyCode: string;
  readonly lines: readonly QuoteHandoffLine[];
  readonly customerReference?: string | null;
}

export interface CrmExternalEvent {
  readonly messageId: string;
  readonly legalEntityId?: string | null;
  readonly messageType: string;
  readonly entityId?: string | null;
  readonly occurredAt: string;
}
