// Local only, never committed: this machine's cached headless shell is a newer build than the one
// this Playwright version names, so the run points at it instead of downloading another.
import base from './playwright.config';
import { defineConfig } from '@playwright/test';

export default defineConfig({
  ...base,
  use: {
    ...base.use,
    launchOptions: {
      executablePath: `${process.env.HOME}/Library/Caches/ms-playwright/chromium_headless_shell-1248/chrome-headless-shell-mac-arm64/chrome-headless-shell`,
    },
  },
});
