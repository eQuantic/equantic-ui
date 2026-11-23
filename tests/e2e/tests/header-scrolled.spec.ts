import { test, expect, type Locator, type Page } from '@playwright/test';

/**
 * The boxes INSIDE a pinned header follow its scrolled state (#1769), on the sample's `/header` page:
 * each box says what it looks like while the header is scrolled (`BoxStyle.Scrolled`), and its own
 * transition glides the swap, with nothing rebuilt.
 *
 * What only a browser proves is the rule's MATCH and its WEIGHT. The suites below this one pin the
 * rule's text, and happy-dom matches no complex selector inside `:where()`, so the nested header was
 * never really asked there. Here the cascade is Chromium's: the scrolled fill beats the resting one,
 * the hover beats the scrolled one, and a list's own pinned header follows the list, not the page.
 */

/** The box a `Group` names: its one child. */
const boxOf = (page: Page, name: string): Locator =>
  page.getByRole('group', { name, exact: true }).locator(':scope > div');

const computed = (locator: Locator, property: string): Promise<string> =>
  locator.evaluate((element, name) => getComputedStyle(element).getPropertyValue(name), property);

/** Scrolls the surface the bar pins to, the layout's scroller: the nearest ancestor that scrolls. */
async function scrollPage(page: Page, top: number): Promise<void> {
  await page.locator('[data-eq-pinned]').first().evaluate((header, y) => {
    for (let element = header.parentElement; element; element = element.parentElement) {
      const overflow = getComputedStyle(element).overflowY;
      if (overflow === 'auto' || overflow === 'scroll') {
        element.scrollTop = y;
        return;
      }
    }
    window.scrollTo(0, y);
  }, top);
}

async function open(page: Page): Promise<string[]> {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.goto('/header');
  await page.waitForLoadState('networkidle');
  return errors;
}

test.describe('A box in a pinned header', () => {
  test('the badge gives way to the mark as the page scrolls, and comes back', async ({ page }) => {
    const errors = await open(page);
    const bar = page.locator('[data-eq-pinned]').first();
    const badge = boxOf(page, 'Badge');
    const mark = boxOf(page, 'Mark');

    await expect(bar).not.toHaveAttribute('data-eq-scrolled');
    expect(await computed(badge, 'opacity')).toBe('1');
    expect(await computed(mark, 'opacity')).toBe('0');

    await scrollPage(page, 400);
    await expect(bar).toHaveAttribute('data-eq-scrolled');
    await expect.poll(() => computed(badge, 'opacity')).toBe('0');
    await expect.poll(() => computed(badge, 'transform')).toBe('matrix(0.6, 0, 0, 0.6, 0, 0)');
    await expect.poll(() => computed(mark, 'opacity')).toBe('1');

    await scrollPage(page, 0);
    await expect(bar).not.toHaveAttribute('data-eq-scrolled');
    await expect.poll(() => computed(badge, 'opacity')).toBe('1');
    await expect.poll(() => computed(badge, 'transform')).toBe('none');
    await expect.poll(() => computed(mark, 'opacity')).toBe('0');
    expect(errors).toEqual([]);
  });

  test('a link takes its scrolled fill over its resting one, and its hover over both', async ({ page }) => {
    const errors = await open(page);
    const link = page.getByRole('button', { name: 'Docs' });
    const surface = link.locator(':scope > div');
    // The pointer away from the bar, where nothing hovers.
    await page.mouse.move(5, 700);

    const resting = await computed(surface, 'background-color');
    await scrollPage(page, 400);
    await expect.poll(() => computed(surface, 'background-color')).not.toBe(resting);
    // Settled past the 100ms glide before it is read as the scrolled fill.
    await page.waitForTimeout(300);
    const scrolled = await computed(surface, 'background-color');

    await link.hover();
    await expect.poll(() => computed(surface, 'background-color')).not.toBe(scrolled);
    await page.waitForTimeout(300);
    expect(await computed(surface, 'background-color')).not.toBe(resting);

    await page.mouse.move(5, 700);
    await expect.poll(() => computed(surface, 'background-color')).toBe(scrolled);
    expect(errors).toEqual([]);
  });

  test("a list's own pinned header follows the list, not the page", async ({ page }) => {
    const errors = await open(page);
    const recent = boxOf(page, 'Recent');
    const list = boxOf(page, 'Rail');
    const badge = boxOf(page, 'Badge');

    const resting = await computed(recent, 'background-color');

    // The bar is scrolled, and the list under it is at its top: the group's header rests.
    await scrollPage(page, 400);
    await expect.poll(() => computed(badge, 'opacity')).toBe('0');
    expect(await computed(recent, 'background-color')).toBe(resting);

    // The list scrolls under its own header, and only then does the group's header turn.
    await list.evaluate((element) => {
      element.scrollTop = 60;
    });
    await expect(page.locator('[data-eq-pinned][data-eq-scrolled]')).toHaveCount(2);
    await expect.poll(() => computed(recent, 'background-color')).not.toBe(resting);
    const scrolled = await computed(recent, 'background-color');

    // And it stays turned with the page back at its top, the bar around it resting.
    await scrollPage(page, 0);
    await expect.poll(() => computed(badge, 'opacity')).toBe('1');
    expect(await computed(recent, 'background-color')).toBe(scrolled);
    expect(errors).toEqual([]);
  });
});
