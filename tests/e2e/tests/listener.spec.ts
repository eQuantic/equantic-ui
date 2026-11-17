import { test, expect, type Page } from '@playwright/test';

/**
 * Any subtree hears the raw pointer (#1562). The dashboard's `/files` screen resizes its tree by the gap beside it:
 * a `Listener` over an empty box, `Opaque` because the gap draws nothing, so a deferring listener would hear
 * nothing there.
 *
 * What only a browser can prove is the browser's own hit test (`elementsFromPoint`, which the runtime's controller
 * reads in paint order) and a press followed past the box it went down on: the gap is 16 px wide and the drag runs
 * a hundred past it, so every move but the first lands outside it. The hit rule itself runs the same cases on both
 * targets (`listener-hits.fixture.json`), and the controller's cases are `pointer-listener.spec.ts`'s.
 */

/** The tree's box, the gap's listener and its centre, as the page lays them out now. */
async function layout(page: Page) {
  const gap = page.locator('[data-eq-listener="opaque"]').first();
  const tree = page.getByRole('tree', { name: 'Repository' });
  const gapBox = (await gap.boundingBox())!;
  const treeWidth = await tree.evaluate((el) => {
    // The box the tree is drawn in: the gap's previous sibling holds it.
    const holder = el.closest('[data-eq-listener]')?.previousElementSibling ?? el.parentElement!;
    return holder.getBoundingClientRect().width;
  });
  return { gap, gapBox, treeWidth };
}

test.describe('A listener in the browser', () => {
  test('the gap beside the tree, which draws nothing, follows a drag that runs past it', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/files');
    await page.waitForLoadState('networkidle');
    await expect(page.getByRole('tree', { name: 'Repository' })).toBeVisible();

    const before = await layout(page);
    const x = before.gapBox.x + before.gapBox.width / 2;
    const y = before.gapBox.y + before.gapBox.height / 2;

    await page.mouse.move(x, y);
    await page.mouse.down();
    await page.mouse.move(x + 100, y, { steps: 10 });
    await page.mouse.up();

    await expect.poll(async () => (await layout(page)).treeWidth).toBeCloseTo(before.treeWidth + 100, -1);
    const after = await layout(page);
    expect(after.gapBox.x).toBeCloseTo(before.gapBox.x + 100, -1);

    // Released, the gap no longer follows the pointer.
    await page.mouse.move(x + 300, y, { steps: 5 });
    expect((await layout(page)).treeWidth).toBeCloseTo(after.treeWidth, 0);
  });

  test('the same gap, deferring to a child that draws nothing, hears nothing', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/files');
    await page.waitForLoadState('networkidle');
    const before = await layout(page);
    // The behaviour is read from the mark at every event, so the gap deferring is the same page with one word changed:
    // the browser's hit test, with the real computed styles, finds nothing drawn in the gap for the listener to defer to.
    await before.gap.evaluate((el) => el.setAttribute('data-eq-listener', 'defer'));
    const x = before.gapBox.x + before.gapBox.width / 2;
    const y = before.gapBox.y + before.gapBox.height / 2;

    await page.mouse.move(x, y);
    await page.mouse.down();
    await page.mouse.move(x + 100, y, { steps: 10 });
    await page.mouse.up();

    const tree = page.getByRole('tree', { name: 'Repository' });
    const width = await tree.evaluate(
      (el) => (el.closest('[data-eq-listener]')?.previousElementSibling ?? el.parentElement!).getBoundingClientRect().width,
    );
    expect(width).toBeCloseTo(before.treeWidth, 0);
  });

  test('a press elsewhere moves nothing', async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto('/files');
    await page.waitForLoadState('networkidle');
    const before = await layout(page);

    // Down on the screen's description, then dragged across the gap: the gap heard no down, so it follows nothing.
    const words = (await page.getByText('Folders load from the server', { exact: false }).boundingBox())!;
    await page.mouse.move(words.x + 5, words.y + 5);
    await page.mouse.down();
    await page.mouse.move(before.gapBox.x + 200, before.gapBox.y + 40, { steps: 10 });
    await page.mouse.up();

    expect((await layout(page)).treeWidth).toBeCloseTo(before.treeWidth, 0);
  });
});
