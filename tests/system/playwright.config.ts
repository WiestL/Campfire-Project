import { defineConfig } from '@playwright/test'

// System tests run against the live stack (real API, real Postgres, real
// Vite dev server) per the V&V plan: "Playwright against the live stack,
// one major workflow per week." They are not started/stopped by this config;
// see tests/system/README.md for what needs to already be running.
export default defineConfig({
  testDir: './tests',
  timeout: 30_000,
  retries: 0,
  reporter: [['list']],
  use: {
    baseURL: 'http://localhost:5173',
    ignoreHTTPSErrors: true,
    screenshot: 'only-on-failure',
  },
})
