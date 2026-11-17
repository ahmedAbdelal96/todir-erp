import type { StatusIntent } from "../../../components/fields/status-badge.tsx";

export type TaxStatusPresentationType =
  | "capability"
  | "health"
  | "ruleSet"
  | "registration"
  | "review"
  | "countryPack"
  | "coverage"
  | "generic";

export interface TaxStatusPresentation {
  readonly intent: StatusIntent;
  readonly labelKey: string;
}

export function getTaxStatusPresentation(
  status: string,
  type: TaxStatusPresentationType = "health"
): TaxStatusPresentation {
  let intent: StatusIntent = "neutral";
  let labelKey = `status.${status.toLowerCase()}`;

  switch (type) {
    case "capability":
      if (status === "Enabled") {
        intent = "success";
        labelKey = "capability.enabled";
      } else {
        labelKey = "capability.disabled";
      }
      break;

    case "countryPack":
      if (status === "Active" || status === "Available") {
        intent = "success";
      } else if (status === "Installed") {
        intent = "info";
      } else if (status === "Incompatible" || status === "Missing") {
        intent = "danger";
      }
      labelKey = `countryPack.status.${status.toLowerCase()}`;
      break;

    case "coverage":
      if (status === "Complete") {
        intent = "success";
      } else if (status === "Pending" || status === "Warning") {
        intent = "warning";
      } else if (status === "Blocked") {
        intent = "danger";
      }
      break;

    case "generic":
      if (["Enabled", "Active", "Complete", "Ready", "Validated", "Approved", "Available", "Configured"].includes(status)) {
        intent = "success";
      } else if (["Pending", "Draft", "Scheduled", "Warning"].includes(status)) {
        intent = "warning";
      } else if (["Blocked", "Disabled", "Invalid"].includes(status)) {
        intent = "danger";
      }
      break;

    case "health":
      if (status === "Healthy" || status === "Ready" || status === "Green") {
        intent = "success";
        labelKey = "health.healthy";
      } else if (status === "Warning" || status === "NeedsAttention") {
        intent = "warning";
        labelKey = "health.warning";
      } else if (status === "Blocked") {
        intent = "danger";
        labelKey = "health.blocked";
      }
      break;

    case "ruleSet":
      if (status === "Active") {
        intent = "success";
        labelKey = "ruleSetStatus.active";
      } else if (status === "Draft") {
        intent = "info";
        labelKey = "ruleSetStatus.draft";
      } else if (status === "Approved") {
        intent = "info";
        labelKey = "ruleSetStatus.approved";
      } else if (status === "Scheduled") {
        intent = "warning";
        labelKey = "ruleSetStatus.scheduled";
      } else if (status === "Superseded" || status === "Retired") {
        labelKey = `ruleSetStatus.${status.toLowerCase()}`;
      }
      break;

    case "registration":
      if (status === "Registered") {
        intent = "success";
        labelKey = "registrationStatus.registered";
      } else if (status === "Pending" || status === "PendingRegistration") {
        intent = "warning";
        labelKey = "registrationStatus.pending";
      } else if (status === "Suspended" || status === "RegistrationException") {
        intent = "danger";
        labelKey = "registrationStatus.exception";
      } else if (status === "Deregistered") {
        labelKey = "registrationStatus.deregistered";
      }
      break;

    case "review":
      if (status === "Approved" || status === "Reviewed") {
        intent = "success";
        labelKey = "reviewStatus.approved";
      } else if (status === "Pending" || status === "Unreviewed") {
        intent = "warning";
        labelKey = "reviewStatus.pending";
      }
      break;
  }

  return { intent, labelKey };
}
