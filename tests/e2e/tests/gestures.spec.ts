import { test, expect, type CDPSession, type Page } from '@playwright/test';

/**
 * A gesture of the app's own (#1563), on the dashboard's `/gestures` screen: cards in a scrolling list, each inside a
 * `RawGestureDetector` hosting the sample's `HoldThenDrag`, each holding a button. The recognizer competes for the
 * pointer with the button's press and the list's scroll in one arena.
 *
 * What only a browser proves is the browser's own scroll, the one member the runtime cannot hold: it starts a pan by
 * its own slop and announces it with `pointercancel`, and the runtime keeps it from starting only by preventing a
 * `touchmove` once a recognizer has won. So the finger here is real touch input, `Input.dispatchTouchEvent` on a
 * context with touch, which Chromium turns into its own gestures and scrolls, as a phone's finger is; the arena's
 * cases are `gesture-arena.spec.ts`'s, and its rule is `gesture-arena.fixture.json`'s on both targets.
 */

/** What the page saw of a finger: every `touchmove` (cancelable, prevented), every `pointercancel`. */
interface Seen {
  touchmoves: { cancelable: boolean; prevented: boolean }[];
  cancels: { x: number; y: number }[];
}

async function open(page: Page): Promise<string[]> {
  const errors: string[] = [];
  page.on('pageerror', (error) => errors.push(error.message));
  await page.setViewportSize({ width: 1280, height: 900 });
  await page.goto('/gestures');
  await page.waitForLoadState('networkidle');
  await expect(page.getByRole('group', { name: 'Cards', exact: true })).toBeVisible();
  // Watched from the window, after every listener of the document ran: whether the runtime prevented each move.
  await page.evaluate(() => {
    const seen: Seen = { touchmoves: [], cancels: [] };
    (window as unknown as { __seen: Seen }).__seen = seen;
    window.addEventListener('touchmove', (e) => seen.touchmoves.push({ cancelable: e.cancelable, prevented: e.defaultPrevented }), {
      passive: true,
    });
    window.addEventListener('pointercancel', (e) => seen.cancels.push({ x: e.clientX, y: e.clientY }));
  });
  return errors;
}

const seen = (page: Page): Promise<Seen> => page.evaluate(() => (window as unknown as { __seen: Seen }).__seen);

/** The list's scroller: the element inside the `Cards` group that scrolls. */
async function listTop(page: Page): Promise<number> {
  return page.getByRole('group', { name: 'Cards', exact: true }).evaluate((group) => {
    for (const element of [group, ...Array.from(group.querySelectorAll('*'))]) {
      const overflow = getComputedStyle(element).overflowY;
      if (overflow === 'auto' || overflow === 'scroll') return element.scrollTop;
    }
    throw new Error('The Cards group holds no scroller');
  });
}

/** The point on a card's title a finger or a mouse presses, away from its button: one the list shows, not clips. */
async function titleOf(page: Page, card: string): Promise<{ x: number; y: number }> {
  const group = page.getByRole('group', { name: card, exact: true });
  const box = (await group.getByText(card, { exact: true }).boundingBox())!;
  const at = { x: Math.round(box.x + 8), y: Math.round(box.y + box.height / 2) };
  expect(await group.evaluate((element, [x, y]) => element.contains(document.elementFromPoint(x, y)), [at.x, at.y])).toBe(true);
  return at;
}

/** The card's own transform, which only a lift writes. */
const transformOf = (page: Page, card: string): Promise<string> =>
  page
    .getByRole('group', { name: card, exact: true })
    .locator(':scope > div')
    .first()
    .evaluate((face) => getComputedStyle(face).transform);

const report = (page: Page, name: 'Moved' | 'Opened') => page.getByRole('group', { name, exact: true });

/** A finger, as Chromium's input pipeline receives one. */
class Finger {
  private constructor(private readonly cdp: CDPSession) {}

  static async on(page: Page): Promise<Finger> {
    return new Finger(await page.context().newCDPSession(page));
  }

  down(x: number, y: number): Promise<unknown> {
    return this.cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x, y, id: 1 }] });
  }

  /** Travels from (x, y) by (dx, dy) in `steps` moves, `everyMs` apart. */
  async travel(x: number, y: number, dx: number, dy: number, steps: number, everyMs: number): Promise<void> {
    for (let i = 1; i <= steps; i++) {
      await this.cdp.send('Input.dispatchTouchEvent', {
        type: 'touchMove',
        touchPoints: [{ x: x + (dx * i) / steps, y: y + (dy * i) / steps, id: 1 }],
      });
      await new Promise((resolve) => setTimeout(resolve, everyMs));
    }
  }

  up(): Promise<unknown> {
    return this.cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
  }
}

test.describe('A gesture of the app\'s own, with a finger', () => {
  test.use({ hasTouch: true });

  test('a hold then a drag moves the card, and the list does not scroll', async ({ page }) => {
    const errors = await open(page);
    const finger = await Finger.on(page);
    const at = await titleOf(page, 'Review the copy');
    const before = await listTop(page);

    await finger.down(at.x, at.y);
    await page.waitForTimeout(600);
    // Halfway, the lifted card follows the finger sideways.
    await finger.travel(at.x, at.y, 30, 40, 4, 16);
    await expect.poll(() => transformOf(page, 'Review the copy')).toBe('matrix(1, 0, 0, 1, 30, 0)');
    await finger.travel(at.x + 30, at.y + 40, 30, 40, 4, 16);
    await finger.up();

    await expect(report(page, 'Moved')).toHaveText('Moved Review the copy by (60, 80)');
    expect(await listTop(page)).toBe(before);
    const what = await seen(page);
    console.log(`hold then drag: ${JSON.stringify(what)}, scrollTop ${before} -> ${await listTop(page)}`);
    expect(what.cancels).toEqual([]);
    expect(what.touchmoves.length).toBeGreaterThan(0);
    expect(what.touchmoves.every((move) => move.cancelable && move.prevented)).toBe(true);
    await expect(report(page, 'Opened')).toHaveText('No card opened yet.');
    expect(errors).toEqual([]);
  });

  test('a swipe at once scrolls the list, and the card does not move', async ({ page }) => {
    const errors = await open(page);
    const finger = await Finger.on(page);
    const at = await titleOf(page, 'Draft the brief');
    const before = await listTop(page);

    await finger.down(at.x, at.y);
    await finger.travel(at.x, at.y, 0, -80, 5, 10);
    await finger.up();

    await expect.poll(() => listTop(page)).toBeGreaterThan(before);
    const what = await seen(page);
    console.log(`swipe at once: ${JSON.stringify(what)}, scrollTop ${before} -> ${await listTop(page)}`);
    expect(what.cancels.length).toBe(1);
    expect(what.touchmoves.some((move) => move.prevented)).toBe(false);
    await page.waitForTimeout(600);
    await expect(report(page, 'Moved')).toHaveText('No card moved yet.');
    expect(await transformOf(page, 'Draft the brief')).toBe('none');
    expect(errors).toEqual([]);
  });

  test("a tap presses the card's button", async ({ page }) => {
    const errors = await open(page);
    const finger = await Finger.on(page);
    const button = (await page
      .getByRole('group', { name: 'Book the venue', exact: true })
      .getByRole('button', { name: 'Open' })
      .boundingBox())!;
    const x = Math.round(button.x + button.width / 2);
    const y = Math.round(button.y + button.height / 2);

    await finger.down(x, y);
    await page.waitForTimeout(50);
    await finger.up();

    await expect(report(page, 'Opened')).toHaveText('Opened: Book the venue');
    await page.waitForTimeout(600);
    await expect(report(page, 'Moved')).toHaveText('No card moved yet.');
    expect(errors).toEqual([]);
  });
});

test.describe('A gesture of the app\'s own, with a mouse', () => {
  test('a hold then a drag moves the card, and the document holds no selection', async ({ page }) => {
    const errors = await open(page);
    const at = await titleOf(page, 'Order the prints');
    const before = await listTop(page);

    await page.mouse.move(at.x, at.y);
    await page.mouse.down();
    await page.waitForTimeout(600);
    await page.mouse.move(at.x + 100, at.y, { steps: 10 });
    await expect.poll(() => transformOf(page, 'Order the prints')).toBe('matrix(1, 0, 0, 1, 100, 0)');
    await page.mouse.up();

    await expect(report(page, 'Moved')).toHaveText('Moved Order the prints by (100, 0)');
    expect(await page.evaluate(() => getSelection()?.toString() ?? '')).toBe('');
    expect(await listTop(page)).toBe(before);
    expect(errors).toEqual([]);
  });

  test("a click presses the card's button, and moves nothing", async ({ page }) => {
    const errors = await open(page);
    await page.getByRole('group', { name: 'Send the invites', exact: true }).getByRole('button', { name: 'Open' }).click();

    await expect(report(page, 'Opened')).toHaveText('Opened: Send the invites');
    await expect(report(page, 'Moved')).toHaveText('No card moved yet.');
    expect(errors).toEqual([]);
  });
});
