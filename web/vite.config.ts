/// <reference types="vitest/config" />
import tailwindcss from "@tailwindcss/vite";
import react from "@vitejs/plugin-react";
import { fileURLToPath, URL } from "node:url";
import { defineConfig, type ProxyOptions } from "vite";

// Under the AppHost, Aspire passes each referenced service's address as services__{name}__http__0.
// The dev server proxies /api/{service} to it, so the browser talks to one origin and no service needs CORS.
function proxyTo(service: string): ProxyOptions | undefined {
  const target = process.env[`services__${service}__http__0`];
  return target ? { target, changeOrigin: true, rewrite: (path) => path.replace(`/api/${service}`, "") } : undefined;
}

const proxy = Object.fromEntries(
  ["registry", "review", "hub"].flatMap((service) => {
    const options = proxyTo(service);
    return options ? [[`/api/${service}`, options]] : [];
  }),
);

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: { alias: { "@": fileURLToPath(new URL("./src", import.meta.url)) } },
  server: {
    port: Number(process.env.PORT ?? 5173),
    strictPort: true, // the Keycloak client's redirect URI names this port
    proxy,
  },
  define: {
    // the browser reaches Keycloak directly; its issuer URL must be stable (fixed port in the AppHost)
    __OIDC_AUTHORITY__: JSON.stringify(
      `${process.env.services__keycloak__http__0 ?? "http://localhost:8080"}/realms/agentgate`,
    ),
  },
  test: {
    environment: "jsdom",
    setupFiles: ["./src/test-setup.ts"],
    include: ["src/**/*.test.{ts,tsx}"],
    pool: "vmThreads", // one jsdom per worker instead of per file
  },
});
