import { defineConfig, devices } from '@playwright/test'

// The stack (web, API, worker, database, mail catcher and the target site) is started before this runs: with
// `docker compose -f docker-compose.yml -f docker-compose.e2e.yml up --wait` in CI, or by hand while developing.
// PW_CHANNEL=chrome uses an installed Chrome instead of the downloaded Chromium.
export default defineConfig({
  testDir: './tests',
  timeout: 240_000,
  expect: { timeout: 15_000 },
  fullyParallel: false,
  workers: 1,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['github'], ['html', { open: 'never' }]] : [['list']],
  use: {
    baseURL: process.env.PULSE_URL ?? 'http://localhost:8080',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'], channel: process.env.PW_CHANNEL || undefined },
    },
  ],
})
