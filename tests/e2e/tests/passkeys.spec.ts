import { test, expect, type Page } from '@playwright/test';

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

/** Presses a button once it is enabled: the page enables it when the options for its ceremony arrived. */
async function press(page: Page, name: string) {
  const button = page.getByRole('button', { name, exact: true });
  await expect(button).toBeEnabled({ timeout: 20_000 });
  await button.click();
}

test.describe('Passkeys', () => {
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
