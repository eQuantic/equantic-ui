import { defineConfig, devices } from '@playwright/test';

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
    baseURL: 'http://localhost:5200',
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
    command: 'dotnet run --project ../../samples/DefaultUIDashboard --urls http://localhost:5200',
    url: 'http://localhost:5200',
    reuseExistingServer: !process.env.CI,
    timeout: 120000,
    stdout: 'pipe', // Capture server output
    stderr: 'pipe',
  },
});
