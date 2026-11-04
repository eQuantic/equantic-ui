import { createHash } from 'node:crypto';
import { test, expect, type Page } from '@playwright/test';

/**
 * Files sent to the server in parts (#1215), on the sample's /uploads page: the FileUpload component
 * asks the browser's picker for a file, sends it in parts of 8 MB over the upload endpoint, and hands
 * what arrived to the page's Server Action, which reads it as a stream and answers its length and its
 * SHA-256. The answer is the proof that the server holds the bytes that were chosen, every part in its
 * place.
 */

/** Bytes no two parts share, so a part sent twice or out of place changes the hash. */
function model(length: number): Buffer {
  const bytes = Buffer.alloc(length);
  let state = 0x2545f491;
  for (let i = 0; i < length; i++) {
    state ^= state << 13;
    state ^= state >>> 17;
    state ^= state << 5;
    bytes[i] = state & 0xff;
  }
  return bytes;
}

/** Presses the area's button and answers its picker with these files. */
async function choose(page: Page, files: { name: string; mimeType: string; buffer: Buffer }[]) {
  const button = page.getByRole('button', { name: 'Choose files', exact: true });
  await expect(button).toBeEnabled({ timeout: 20_000 });
  const chooser = page.waitForEvent('filechooser');
  await button.click();
  await (await chooser).setFiles(files);
}

test.describe('Uploads', () => {
  // An exception or a rejected promise nothing handled fails the test that left it, whatever the page
  // showed: in development the runtime puts each one in its error overlay.
  const uncaught = new WeakMap<Page, string[]>();
  test.beforeEach(({ page }) => {
    const errors: string[] = [];
    uncaught.set(page, errors);
    page.on('pageerror', (error) => errors.push(error.message));
  });
  test.afterEach(({ page }) => {
    expect(uncaught.get(page), 'errors nothing in the page handled').toEqual([]);
  });

  test('a file of three parts reaches the Server Action whole', async ({ page }) => {
    const parts: string[] = [];
    page.on('request', (request) => {
      if (request.url().includes('/api/_equantic/uploads')) parts.push(`${request.method()} ${request.headers()['upload-offset'] ?? ''}`.trim());
    });
    await page.goto('/uploads');
    const bytes = model(20 * 1024 * 1024 + 123);
    const sha256 = createHash('sha256').update(bytes).digest('hex');

    await choose(page, [{ name: 'bracket.stl', mimeType: 'model/stl', buffer: bytes }]);

    await expect(page.getByText(`bracket.stl: ${bytes.length} bytes, SHA-256 ${sha256}`)).toBeVisible({
      timeout: 60_000,
    });
    await expect(page.getByText('Uploaded', { exact: true })).toBeVisible();
    await expect(page.getByText('bracket.stl uploaded.')).toBeVisible();
    // Created once, then three parts at the offsets each one ended at.
    expect(parts).toEqual(['POST', 'PATCH 0', `PATCH ${8 * 1024 * 1024}`, `PATCH ${16 * 1024 * 1024}`]);
    // The page forgets an upload that completed, so the same file starts afresh next time.
    expect(await page.evaluate(() => Object.keys(localStorage).filter((key) => key.startsWith('eq-upload:')))).toEqual([]);
  });

  test('a file the policy does not accept is refused in its row, before anything is sent', async ({ page }) => {
    const sent: string[] = [];
    page.on('request', (request) => {
      if (request.url().includes('/api/_equantic/uploads')) sent.push(request.method());
    });
    await page.goto('/uploads');

    await choose(page, [{ name: 'setup.exe', mimeType: 'application/octet-stream', buffer: Buffer.from([1, 2, 3]) }]);

    await expect(page.getByText('Only .stl, .obj, .3mf, .txt files are accepted here.', { exact: true })).toBeVisible();
    await expect(
      page.getByText('setup.exe was not uploaded: Only .stl, .obj, .3mf, .txt files are accepted here.'),
    ).toBeVisible();
    expect(sent).toEqual([]);
  });

  test("the server's own refusal reaches the row in its words", async ({ page }) => {
    // A browser that skipped the page's own check: the server refuses the same file, in the same words.
    await page.route('**/api/_equantic/uploads', (route) =>
      route.fulfill({ status: 413, body: 'The file is larger than the 67108864 bytes accepted here.' }),
    );
    await page.goto('/uploads');

    await choose(page, [{ name: 'notes.txt', mimeType: 'text/plain', buffer: Buffer.from('hello') }]);

    await expect(page.getByText('The file is larger than the 67108864 bytes accepted here.', { exact: true })).toBeVisible({
      timeout: 20_000,
    });
  });
});
