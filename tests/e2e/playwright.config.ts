import { defineConfig, devices } from '@playwright/test';

// Another port when a second checkout runs its own server beside this one's: `E2E_PORT=5210 npm test`.
const port = process.env.E2E_PORT ?? '5200';

export default defineConfig({
  testDir: './tests',
  fullyParallel: false, // Run tests sequentially for better control
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: 1, // Single worker to avoid port conflicts
  reporter: [
    ['list'],
    ['html', { open: 'never' }]
  ],
  use: {
    baseURL: `http://localhost:${port}`,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],
  webServer: {
    command: `dotnet run --project ../../samples/DefaultUIDashboard --no-launch-profile --urls http://localhost:${port}`,
    env: {
      // Hot reload's SSE stream keeps the network busy forever — e2e pins PRODUCTION behavior.
      ASPNETCORE_ENVIRONMENT: 'Production',
    },
    url: `http://localhost:${port}`,
    reuseExistingServer: !process.env.CI,
    timeout: 120000,
    stdout: 'pipe', // Capture server output
    stderr: 'pipe',
  },
});
