import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { AppShell, ShellErrorBoundary } from "./app-shell";
import "./styles.css";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <ShellErrorBoundary><AppShell /></ShellErrorBoundary>
  </StrictMode>,
);
