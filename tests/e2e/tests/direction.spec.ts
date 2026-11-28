import { test, expect, type Locator, type Page } from '@playwright/test';

/**
 * What follows the direction (#1569), on the dashboard's `/i18n` screen, whose last section holds a calendar, a
 * switch, a swipeable row, tabs, a horizontal strip and a folded code block, written with no test of the direction
 * anywhere. Served in Arabic, the page reads right to left: the realizer turns the glyphs that point along the line,
 * the drags measure toward the end, the arrows step in reading order and the strip scrolls from its start, while the
 * code keeps reading left to right. Served in English, the same screen reads as it always did.
 *
 * What only a browser proves is the browser's own share: the cascade that resolves `--eq-inline` from the nearest
 * `[dir]`, the pointer and key events of a real input, and Chromium's negative `scrollLeft` under right to left. The
 * producers' markup is the component-parity suite's, and each half's mapping is its own spec's.
 */

async function open(page: Page, culture: 'ar' | 'en'): Promise<string[]> {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.setViewportSize({ width: 1280, height: 1600 });
  await page.goto(`/i18n?culture=${culture}`);
  await page.waitForLoadState('networkidle');
  await expect(page.getByRole('tablist')).toBeVisible();
  return errors;
}

/** The computed `scale` of the glyph inside a control: "-1 1" when it is turned, "1" or "none" when it is not. */
const glyphScale = (control: Locator): Promise<string> =>
  control.locator('svg').first().evaluate((svg) => getComputedStyle(svg).scale);

/** Drags from one point to another by the mouse, in steps, as a hand does. */
async function drag(page: Page, from: { x: number; y: number }, by: number): Promise<void> {
  await page.mouse.move(from.x, from.y);
  await page.mouse.down();
  for (let step = 1; step <= 8; step++) await page.mouse.move(from.x + (by * step) / 8, from.y);
  await page.mouse.up();
}

/** The strip's scroller, found by what it computes rather than by how it is written. */
async function scrollStrip(page: Page, scrollLeft: number): Promise<void> {
  await page.getByText('Item 1', { exact: true }).evaluate((chip, left) => {
    for (let element: Element | null = chip; element; element = element.parentElement) {
      const overflow = getComputedStyle(element).overflowX;
      if (overflow === 'auto' || overflow === 'scroll') {
        element.scrollLeft = left;
        return;
      }
    }
    throw new Error('No horizontal scroller holds the strip');
  }, scrollLeft);
}

test.describe('in Arabic, what points along the line follows the direction (#1569)', () => {
  test('the page reads right to left, and the calendar chevrons turn and trade places', async ({ page }) => {
    const errors = await open(page, 'ar');
    expect(await page.evaluate(() => document.documentElement.dir)).toBe('rtl');

    const previous = page.getByRole('button', { name: 'Previous month' });
    const next = page.getByRole('button', { name: 'Next month' });
    // The glyph is a chevron-left; turned, it points right, toward the start of an Arabic line.
    expect(await glyphScale(previous)).toBe('-1 1');
    expect(await glyphScale(next)).toBe('-1 1');
    // And the header mirrored, so "previous" stands on the right, where an Arabic reader starts.
    const p = (await previous.boundingBox())!;
    const n = (await next.boundingBox())!;
    expect(p.x).toBeGreaterThan(n.x);
    expect(errors).toEqual([]);
  });

  test('a switch dragged leftwards, toward the end, turns on', async ({ page }) => {
    const errors = await open(page, 'ar');
    const control = page.getByRole('switch');
    await expect(control).toHaveAttribute('aria-checked', 'false');
    const box = (await control.boundingBox())!;
    // Off, the knob rests at the start of the track, which is its right edge here.
    await drag(page, { x: box.x + box.width - box.height / 2, y: box.y + box.height / 2 }, -box.width);
    await expect(control).toHaveAttribute('aria-checked', 'true');
    expect(errors).toEqual([]);
  });

  test('a swipeable row swiped rightwards, toward the start, opens', async ({ page }) => {
    const errors = await open(page, 'ar');
    await expect(page.getByText('row closed', { exact: true })).toBeVisible();
    const box = (await page.getByText('Swipe to flag', { exact: true }).boundingBox())!;
    await drag(page, { x: box.x + box.width / 2, y: box.y + box.height / 2 }, 160);
    await expect(page.getByText('row open', { exact: true })).toBeVisible();
    expect(errors).toEqual([]);
  });

  test('ArrowLeft chooses the next tab and ArrowRight the previous one', async ({ page }) => {
    const errors = await open(page, 'ar');
    const tabs = page.getByRole('tablist');
    await tabs.focus();
    await page.keyboard.press('ArrowLeft');
    await expect(page.getByRole('tab', { name: 'Second' })).toHaveAttribute('aria-selected', 'true');
    await page.keyboard.press('ArrowRight');
    await expect(page.getByRole('tab', { name: 'First' })).toHaveAttribute('aria-selected', 'true');
    expect(errors).toEqual([]);
  });

  test("a horizontal strip's offset grows toward its end", async ({ page }) => {
    const errors = await open(page, 'ar');
    // Chromium counts a right-to-left scroller's position below zero; the strip reports it from its start.
    await scrollStrip(page, -120);
    await expect(page.getByText('scrolled 120', { exact: true })).toBeVisible();
    expect(errors).toEqual([]);
  });

  test('the code keeps reading left to right, and its fold chevron is not turned', async ({ page }) => {
    const errors = await open(page, 'ar');
    const header = page.getByText('void Greet(string name)', { exact: false }).first();
    expect(await header.evaluate((line) => line.closest('[dir]')?.getAttribute('dir'))).toBe('ltr');
    // Every glyph inside the code block, the folded region's chevron among them, draws as it is written.
    const scales = await header.evaluate((line) => {
      const host = line.closest('[dir="ltr"]')!;
      return Array.from(host.querySelectorAll('svg')).map((svg) => getComputedStyle(svg).scale);
    });
    expect(scales.length).toBeGreaterThan(0);
    for (const scale of scales) expect(scale.startsWith('-')).toBe(false);
    expect(errors).toEqual([]);
  });
});

test.describe('in English, the same screen reads left to right as it always did', () => {
  test('nothing turns, a rightward drag turns the switch on, and ArrowRight is next', async ({ page }) => {
    const errors = await open(page, 'en');
    expect(await page.evaluate(() => document.documentElement.dir)).toBe('ltr');
    expect((await glyphScale(page.getByRole('button', { name: 'Previous month' }))).startsWith('-')).toBe(false);

    const control = page.getByRole('switch');
    const box = (await control.boundingBox())!;
    await drag(page, { x: box.x + box.height / 2, y: box.y + box.height / 2 }, box.width);
    await expect(control).toHaveAttribute('aria-checked', 'true');

    await page.getByRole('tablist').focus();
    await page.keyboard.press('ArrowRight');
    await expect(page.getByRole('tab', { name: 'Second' })).toHaveAttribute('aria-selected', 'true');

    await scrollStrip(page, 120);
    await expect(page.getByText('scrolled 120', { exact: true })).toBeVisible();
    expect(errors).toEqual([]);
  });
});
