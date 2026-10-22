import { defineConfig } from "@playwright/test";

// The console against mocked APIs (e2e/fixtures.ts): no services, no Keycloak. Proves the screens render the
// governance story and keeps screenshots for the docs. Uses a pre-installed Chromium when one is provided.
export default defineConfig({
  testDir: "./e2e",
  timeout: 30_000,
  use: {
    baseURL: "http://localhost:5199",
    viewport: { width: 1440, height: 900 },
    colorScheme: "dark",
    launchOptions: process.env.CHROMIUM_PATH ? { executablePath: process.env.CHROMIUM_PATH } : {},
  },
  webServer: { command: "npx vite --port 5199", url: "http://localhost:5199", reuseExistingServer: false },
});
