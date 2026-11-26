import { test, expect } from '@playwright/test';

/**
 * A LayoutBuilder in a real browser (#1555): the dashboard's /layout page holds three cards whose content
 * is built for the room each box has. The two fixed cards are told the same room Photon tells them
 * (LayoutBuilderTests lays out the same tree), and the filling one is built again when the window narrows.
 */

test('a builder is told the room its box has, as Photon tells it', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto('/layout');

  // A fixed box decides both axes: the builder is told 600 by 200, and lays its parts side by side.
  await expect(page.getByText('fixed: 600 × 200')).toBeVisible();
  // A narrower one is told 360, and stacks them.
  await expect(page.getByText('narrow: 360 × 200')).toBeVisible();
  // A box that fills the width and sizes its height from what it holds: an unbounded height.
  await expect(page.getByText(/^fill: \d+(\.\d+)? × unbounded$/)).toBeVisible();
  await expect(page.getByText('side by side')).toHaveCount(2);
  await expect(page.getByText('stacked')).toHaveCount(1);
});

test('a narrower window builds the filling card again', async ({ page }) => {
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto('/layout');
  const fill = page.getByText(/^fill: /);
  await expect(fill).toBeVisible();
  const wide = Number(/fill: ([\d.]+)/.exec((await fill.textContent()) ?? '')![1]);
  expect(wide).toBeGreaterThanOrEqual(480);

  await page.setViewportSize({ width: 420, height: 900 });

  // The room the filling card is told follows the window, and below 480 its parts stack.
  await expect(fill).not.toHaveText(`fill: ${wide} × unbounded`);
  const narrow = Number(/fill: ([\d.]+)/.exec((await fill.textContent()) ?? '')![1]);
  expect(narrow).toBeLessThan(480);
  await expect(page.getByText('stacked')).toHaveCount(2);
});
