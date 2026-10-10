import { httpClient } from "../../../core/api/http-client.ts";
import type { ImportJob, FieldMapping } from "../types.ts";

export const importApi = {
  createJob: async (
    fileName: string,
    entityType: string,
    tenantId?: string
  ): Promise<ImportJob> => {
    return httpClient<ImportJob>("/api/v1/imports/jobs", {
      method: "POST",
      body: JSON.stringify({ fileName, entityType }),
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  },

  dryRun: async (
    jobId: string,
    mappings: FieldMapping,
    tenantId?: string
  ): Promise<ImportJob> => {
    return httpClient<ImportJob>(`/api/v1/imports/jobs/${jobId}/dry-run`, {
      method: "POST",
      body: JSON.stringify({ mappings }),
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  },

  execute: async (
    jobId: string,
    mappings: FieldMapping,
    tenantId?: string
  ): Promise<ImportJob> => {
    return httpClient<ImportJob>(`/api/v1/imports/jobs/${jobId}/execute`, {
      method: "POST",
      body: JSON.stringify({ mappings }),
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  },

  getJobStatus: async (
    jobId: string,
    tenantId?: string
  ): Promise<ImportJob> => {
    return httpClient<ImportJob>(`/api/v1/imports/jobs/${jobId}`, {
      ...(tenantId !== undefined ? { tenantId } : {})
    });
  }
};
