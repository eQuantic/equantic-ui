import { test, expect, type Page, type Request } from '@playwright/test';

/**
 * Passkeys from one C# contract (#1017), on the sample's /passkeys page. Chrome's virtual authenticator,
 * driven over the DevTools protocol, creates and signs with no hardware, so the page's IPasskeys runs a
 * real Web Authentication ceremony: the options its server action wrote go in, and the credential goes
 * back to the sample's relying party, which checks that the challenge it issued came back in the client
 * data. It verifies no signature, which a relying party does with a Web Authentication library.
 *
 * `passkeys.test` is mapped to this machine, so the same server answers a name that is not localhost over
 * plain http: a page that is not a secure context, which has no passkey to offer.
 */
test.use({ launchOptions: { args: ['--host-resolver-rules=MAP passkeys.test 127.0.0.1'] } });

async function withAuthenticator(page: Page) {
  const cdp = await page.context().newCDPSession(page);
  await cdp.send('WebAuthn.enable', { enableUI: false });
  const { authenticatorId } = await cdp.send('WebAuthn.addVirtualAuthenticator', {
    options: {
      protocol: 'ctap2',
      ctap2Version: 'ctap2_1',
      transport: 'internal',
      hasResidentKey: true,
      hasUserVerification: true,
      isUserVerified: true,
      automaticPresenceSimulation: true,
    },
  });
  return { cdp, authenticatorId };
}

/** Every server action the page calls goes to this one endpoint, named in the body. */
const actions = '**/api/_equantic/actions';

function actionOf(request: Request): string {
  const body = request.postDataJSON() as { actionId?: string } | null;
  return body?.actionId ?? '';
}

/** Presses a button once it is enabled: the page enables it when the options for its ceremony arrived. */
async function press(page: Page, name: string) {
  const button = page.getByRole('button', { name, exact: true });
  await expect(button).toBeEnabled({ timeout: 20_000 });
  await button.click();
}

test.describe('Passkeys', () => {
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

  test('a passkey is created, signs in, and is not created twice for the account', async ({ page }) => {
    const { cdp, authenticatorId } = await withAuthenticator(page);
    await page.goto('/passkeys');

    await press(page, 'Create a passkey');
    await expect(
      page.getByText(/^Registered passkey \S+: its client data carried the challenge this server issued\./),
    ).toBeVisible({ timeout: 20_000 });
    const { credentials } = await cdp.send('WebAuthn.getCredentials', { authenticatorId });
    expect(credentials).toHaveLength(1);
    expect(credentials[0].isResidentCredential).toBe(true);

    await press(page, 'Sign in with a passkey');
    await expect(
      page.getByText(/^Signed in with passkey \S+: its client data carried the challenge this server issued\./),
    ).toBeVisible({ timeout: 20_000 });

    // The relying party now excludes the passkey it registered, and the device refuses a second one.
    await press(page, 'Create a passkey');
    await expect(
      page.getByText('This device already holds a passkey for this account. Sign in with it instead.'),
    ).toBeVisible({ timeout: 20_000 });
  });

  test('a server that does not take the answer leaves the page ready to try again', async ({ page }) => {
    await withAuthenticator(page);
    // The first answer the page hands its server meets an outage.
    let refused = false;
    await page.route(actions, async (route) => {
      if (!refused && actionOf(route.request()) === 'PasskeyScreen/FinishRegistration') {
        refused = true;
        await route.fulfill({ status: 503, body: 'down for a moment' });
        return;
      }
      await route.continue();
    });
    await page.goto('/passkeys');

    await press(page, 'Create a passkey');
    await expect(page.getByText('The server did not take the answer: press again.')).toBeVisible({
      timeout: 20_000,
    });

    await press(page, 'Create a passkey');
    await expect(page.getByText(/^Registered passkey \S+:/)).toBeVisible({ timeout: 20_000 });
  });

  test('options the server cannot renew are asked for again outside the press', async ({ page }) => {
    await page.clock.install();
    await page.goto('/passkeys');
    const create = page.getByRole('button', { name: 'Create a passkey', exact: true });
    await expect(create).toBeEnabled({ timeout: 20_000 });

    // A minute idle with the server down: the renewal fails, and the page keeps no options it cannot trust.
    await page.route(actions, (route) => route.fulfill({ status: 503, body: 'down for a moment' }));
    await page.clock.fastForward('01:01');
    await expect(
      page.getByText('The server did not answer, so the page holds no options: it asks again in a minute.'),
    ).toBeVisible();
    await expect(create).toBeDisabled();

    // The server is back, and the next renewal finds it with nobody pressing anything.
    await page.unroute(actions);
    await page.clock.fastForward('01:01');
    await expect(create).toBeEnabled();
  });

  test('options older than the page uses start no ceremony', async ({ page }) => {
    const { cdp, authenticatorId } = await withAuthenticator(page);
    await page.clock.install();
    await page.goto('/passkeys');
    const create = page.getByRole('button', { name: 'Create a passkey', exact: true });
    await expect(create).toBeEnabled({ timeout: 20_000 });

    // The renewal never came: a device that slept, a tab the browser held back. The page's clock moves on
    // without firing a timer, so the options it holds are older than their challenge stays good.
    await page.clock.setSystemTime(new Date(Date.now() + 10 * 60_000));
    await create.click();
    await expect(page.getByText('These options lapsed while the page was away: press again.')).toBeVisible({
      timeout: 20_000,
    });
    const { credentials } = await cdp.send('WebAuthn.getCredentials', { authenticatorId });
    expect(credentials).toHaveLength(0);

    // The press asked for new options, and the next one uses them.
    await press(page, 'Create a passkey');
    await expect(page.getByText(/^Registered passkey \S+:/)).toBeVisible({ timeout: 20_000 });
  });

  test('a page outside a secure context has no passkey to offer', async ({ page, baseURL }) => {
    // The server the configuration starts, under the mapped name instead of localhost.
    const outsider = new URL('/passkeys', baseURL);
    outsider.hostname = 'passkeys.test';
    await page.goto(outsider.href);

    // A string, so the test runner's transform leaves the module import for the browser to resolve
    // through the page's own import map.
    const outside = await page.evaluate(`(async () => {
      const { $eq } = await import('@equantic/runtime');
      const passkeys = $eq.services.resolve('IPasskeys');
      const result = await passkeys.createAsync('{"challenge":"AAAA","rp":{"name":"x"},'
        + '"user":{"id":"AAAA","name":"a","displayName":"a"},"pubKeyCredParams":[]}');
      return { secure: window.isSecureContext, available: passkeys.isAvailable,
        outcome: result.outcome, credential: result.credential };
    })()`);

    expect(outside).toEqual({ secure: false, available: false, outcome: 'unavailable', credential: null });
    await expect(page.getByText(/^A passkey needs a secure context/)).toBeVisible();
  });
});
