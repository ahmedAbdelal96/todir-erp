import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { importApi } from "../api/import-api.ts";
import type { ImportJob, FieldMapping } from "../types.ts";
import { queryKeyFactory } from "../../../platform/api/query-keys.ts";

export interface UseImportContext {
  readonly tenantId?: string | undefined;
  readonly entityId?: string | undefined;
}

const DEFAULT_TENANT = "tenant-default";
const DEFAULT_ENTITY = "entity-default";

export function useImportJob(jobId: string | null, context: UseImportContext = {}) {
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useQuery<ImportJob>({
    queryKey: queryKeyFactory.detail(tenantId, entityId, "migration", "import-jobs", jobId ?? ""),
    queryFn: () => importApi.getJobStatus(jobId!, tenantId),
    enabled: Boolean(jobId),
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      if (status === "Validating" || status === "Executing") {
        return 2_000;
      }
      return false;
    },
  });
}

export function useCreateImportJob(context: UseImportContext = {}) {
  const queryClient = useQueryClient();
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useMutation<ImportJob, Error, { fileName: string; entityType: string }>({
    mutationFn: ({ fileName, entityType }) =>
      importApi.createJob(fileName, entityType, tenantId),
    onSuccess: (job) => {
      void queryClient.setQueryData(
        queryKeyFactory.detail(tenantId, entityId, "migration", "import-jobs", job.id),
        job
      );
    },
  });
}

export function useDryRunImport(context: UseImportContext = {}) {
  const queryClient = useQueryClient();
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useMutation<ImportJob, Error, { jobId: string; mappings: FieldMapping }>({
    mutationFn: ({ jobId, mappings }) =>
      importApi.dryRun(jobId, mappings, tenantId),
    onSuccess: (job) => {
      void queryClient.setQueryData(
        queryKeyFactory.detail(tenantId, entityId, "migration", "import-jobs", job.id),
        job
      );
    },
  });
}

export function useExecuteImport(context: UseImportContext = {}) {
  const queryClient = useQueryClient();
  const tenantId = context.tenantId || DEFAULT_TENANT;
  const entityId = context.entityId || DEFAULT_ENTITY;

  return useMutation<ImportJob, Error, { jobId: string; mappings: FieldMapping }>({
    mutationFn: ({ jobId, mappings }) =>
      importApi.execute(jobId, mappings, tenantId),
    onSuccess: (job) => {
      void queryClient.setQueryData(
        queryKeyFactory.detail(tenantId, entityId, "migration", "import-jobs", job.id),
        job
      );
    },
  });
}
