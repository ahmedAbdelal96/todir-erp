import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "react-router-dom";
import { createQueryClient } from "./core/api/query-client.ts";
import { appRouter } from "./app/router/app-router.tsx";
import { ShellErrorBoundary } from "./app/shell/app-shell.tsx";
import { ToastProvider } from "./components/primitives/toast.tsx";
import "./styles.css";

const queryClient = createQueryClient();

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <ShellErrorBoundary>
        <ToastProvider>
          <RouterProvider router={appRouter} />
        </ToastProvider>
      </ShellErrorBoundary>
    </QueryClientProvider>
  </StrictMode>,
);
