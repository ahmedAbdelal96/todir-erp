import path from "node:path";
import { fileURLToPath } from "node:url";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

const sourceRoot = fileURLToPath(new URL(".", import.meta.url));

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      "/api": {
        target: process.env.VITE_API_PROXY_TARGET ?? "http://127.0.0.1:5080",
        changeOrigin: true,
        secure: false
      }
    }
  },
  resolve: {
    alias: {
      "@app": path.resolve(sourceRoot, "app"),
      "@core": path.resolve(sourceRoot, "core"),
      "@ui": path.resolve(sourceRoot, "components"),
      "@modules": path.resolve(sourceRoot, "modules")
    }
  }
});
