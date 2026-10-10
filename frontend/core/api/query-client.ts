import { QueryClient } from "@tanstack/react-query";

/**
 * Creates a configured QueryClient with robust ERP defaults.
 */
export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 1000 * 60 * 2, // 2 minutes
        gcTime: 1000 * 60 * 10, // 10 minutes
        retry: (failureCount, error) => {
          // Do not retry on 4xx client errors
          if (error && typeof error === "object" && "status" in error) {
            const status = (error as { status: number }).status;
            if (status >= 400 && status < 500) {
              return false;
            }
          }
          return failureCount < 2;
        },
        refetchOnWindowFocus: false
      },
      mutations: {
        retry: false
      }
    }
  });
}
