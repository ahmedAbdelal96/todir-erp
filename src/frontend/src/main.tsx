import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { QueryClientProvider } from "@tanstack/react-query";
import { createQueryClient } from "./platform/api/query-client.ts";
import { AppShell, ShellErrorBoundary } from "./app/shell/app-shell.tsx";
import "./styles.css";

const queryClient = createQueryClient();

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <ShellErrorBoundary>
        <AppShell />
      </ShellErrorBoundary>
    </QueryClientProvider>
  </StrictMode>,
);
