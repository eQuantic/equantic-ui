import { test, expect, type Page } from '@playwright/test';

/**
 * Contrast levels on the web (#340): the system's setting is the input, read by the browser as
 * `prefers-contrast: more`, and the page steps up with nothing the app wrote. The theme's stylesheet
 * declares its colour variables again under that query, and every atomic class reads a theme colour
 * through its variable, so the proof is the painted colour of the elements, not the variable alone.
 */

const rgb = (hex: string): string => {
  const n = parseInt(hex.slice(1), 16);
  return `rgb(${(n >> 16) & 255}, ${(n >> 8) & 255}, ${n & 255})`;
};

/** Muted text, Standard and More, in each mode: tokens.json's `color.text.muted` and `color.more`. */
const MUTED = {
  light: { standard: rgb('#5F6B7A'), more: rgb('#475261') },
  dark: { standard: rgb('#ACB7C5'), more: rgb('#C5D1DF') },
};

/** A selector for every element painted in `colour`, numbered in document order. */
async function markPainted(page: Page, colour: string): Promise<number> {
  return page.evaluate((wanted) => {
    let count = 0;
    for (const element of Array.from(document.body.querySelectorAll('*'))) {
      if (getComputedStyle(element).color === wanted) element.setAttribute('data-muted-probe', String(count++));
    }
    return count;
  }, colour);
}

async function colourOfMarked(page: Page): Promise<string[]> {
  return page.evaluate(() =>
    Array.from(document.querySelectorAll('[data-muted-probe]')).map((element) => getComputedStyle(element).color));
}

for (const scheme of ['light', 'dark'] as const) {
  test(`muted text steps up when the system asks for more contrast (${scheme})`, async ({ page }) => {
    await page.emulateMedia({ colorScheme: scheme, contrast: 'no-preference' });
    await page.goto('/');
    await page.waitForLoadState('load');

    const marked = await markPainted(page, MUTED[scheme].standard);
    expect(marked, 'the dashboard paints muted text').toBeGreaterThan(0);

    await page.emulateMedia({ colorScheme: scheme, contrast: 'more' });
    expect(new Set(await colourOfMarked(page)), 'every muted element now paints the More leg')
      .toEqual(new Set([MUTED[scheme].more]));

    await page.emulateMedia({ colorScheme: scheme, contrast: 'no-preference' });
    expect(new Set(await colourOfMarked(page)), 'and steps back down with the setting')
      .toEqual(new Set([MUTED[scheme].standard]));
  });
}

test('a screen the runtime builds after a navigation reads the same level', async ({ page }) => {
  await page.emulateMedia({ colorScheme: 'light', contrast: 'more' });
  await page.goto('/');
  const link = page.locator('a[href="/form"]').first();
  await expect(link).toHaveCount(1);
  await link.click();
  await page.waitForURL((url) => url.pathname === '/form');

  expect(await markPainted(page, MUTED.light.standard), 'no muted text paints its Standard leg at More').toBe(0);
  expect(await markPainted(page, MUTED.light.more), 'the screen the runtime built paints the More leg').toBeGreaterThan(0);
});
