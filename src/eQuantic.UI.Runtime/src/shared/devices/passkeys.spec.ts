import { afterEach, describe, expect, it, vi } from 'vitest';
import { CancellationTokenSource } from '../../utils/cancellation';
import { WebPasskeys } from './passkeys';

/**
 * The browser realization of IPasskeys over a stubbed `navigator.credentials`: every way a ceremony
 * ends is an outcome, never a rejection, and the relying party's JSON crosses both ways, through the
 * browser's own conversions where it has them and through this file's where it does not.
 */
describe('WebPasskeys', () => {
  afterEach(() => vi.unstubAllGlobals());

  const encoder = new TextEncoder();

  function b64url(bytes: Uint8Array): string {
    let binary = '';
    for (const byte of bytes) binary += String.fromCharCode(byte);
    return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  }

  function buffer(text: string): ArrayBuffer {
    return encoder.encode(text).buffer as ArrayBuffer;
  }

  // Bytes that need every base64url substitution: 0xfb 0xff encode as '-_8' in base64url.
  const challenge = new Uint8Array([0xfb, 0xff, 0x01, 0x02, 0x03]);
  const userId = new Uint8Array([0x75, 0x73, 0x65, 0x72]);
  const excluded = new Uint8Array([0x0a, 0x0b, 0x0c]);

  const creation = {
    challenge: b64url(challenge),
    rp: { name: 'eQuantic Console' },
    user: { id: b64url(userId), name: 'ana@equantic.tech', displayName: 'Ana' },
    pubKeyCredParams: [{ type: 'public-key', alg: -7 }],
    excludeCredentials: [{ type: 'public-key', id: b64url(excluded) }],
    timeout: 60_000,
  };

  const request = {
    challenge: b64url(challenge),
    allowCredentials: [{ type: 'public-key', id: b64url(excluded) }],
    userVerification: 'preferred',
  };

  interface Stubs {
    secure?: boolean;
    webAuthn?: boolean;
    conversions?: boolean;
    create?: (options: CredentialCreationOptions) => Promise<unknown>;
    get?: (options: CredentialRequestOptions) => Promise<unknown>;
  }

  /** A browser as the test describes it, with a spy on each ceremony to say what it was asked. */
  function browser(stubs: Stubs = {}) {
    const create = vi.fn(stubs.create ?? (() => Promise.resolve(null)));
    const get = vi.fn(stubs.get ?? (() => Promise.resolve(null)));
    const parseCreation = vi.fn((json: unknown) => ({ converted: 'creation', json }));
    const parseRequest = vi.fn((json: unknown) => ({ converted: 'request', json }));
    vi.stubGlobal('window', { isSecureContext: stubs.secure ?? true });
    vi.stubGlobal('navigator', { credentials: { create, get } });
    if (stubs.webAuthn === false) vi.stubGlobal('PublicKeyCredential', undefined);
    else
      vi.stubGlobal(
        'PublicKeyCredential',
        stubs.conversions
          ? { parseCreationOptionsFromJSON: parseCreation, parseRequestOptionsFromJSON: parseRequest }
          : function PublicKeyCredential() {},
      );
    return { create, get, parseCreation, parseRequest };
  }

  /** A credential from a creation, as a browser without `toJSON()` hands it over. */
  function created() {
    return {
      id: b64url(excluded),
      rawId: excluded.buffer,
      type: 'public-key',
      authenticatorAttachment: 'platform',
      getClientExtensionResults: () => ({ credProps: { rk: true } }),
      response: {
        clientDataJSON: buffer('{"type":"webauthn.create"}'),
        attestationObject: buffer('attestation'),
        getAuthenticatorData: () => buffer('authenticator data'),
        getTransports: () => ['internal', 'hybrid'],
        getPublicKey: () => buffer('public key'),
        getPublicKeyAlgorithm: () => -7,
      },
    };
  }

  /** A credential from a signature, as a browser without `toJSON()` hands it over. */
  function signed(userHandle: ArrayBuffer | null) {
    return {
      id: b64url(excluded),
      rawId: excluded.buffer,
      type: 'public-key',
      authenticatorAttachment: 'cross-platform',
      getClientExtensionResults: () => ({}),
      response: {
        clientDataJSON: buffer('{"type":"webauthn.get"}'),
        authenticatorData: buffer('authenticator data'),
        signature: buffer('signature'),
        userHandle,
      },
    };
  }

  function bytesOf(value: unknown): number[] {
    return Array.from(value as Uint8Array);
  }

  const rejection = (name: string) => () => Promise.reject(new DOMException('refused', name));

  describe('a host without passkeys', () => {
    it('outside a secure context is unavailable, and asks nothing of the person', async () => {
      const { create, get } = browser({ secure: false });
      const passkeys = new WebPasskeys();

      expect(passkeys.isAvailable).toBe(false);
      expect(await passkeys.createAsync(JSON.stringify(creation))).toEqual(
        expect.objectContaining({ outcome: 'unavailable', credential: null }),
      );
      expect(await passkeys.authenticateAsync(JSON.stringify(request))).toEqual(
        expect.objectContaining({ outcome: 'unavailable', credential: null }),
      );
      expect(create).not.toHaveBeenCalled();
      expect(get).not.toHaveBeenCalled();
    });

    it('without Web Authentication is unavailable', async () => {
      const { create } = browser({ webAuthn: false });
      const passkeys = new WebPasskeys();

      expect(passkeys.isAvailable).toBe(false);
      expect((await passkeys.createAsync(JSON.stringify(creation))).outcome).toBe('unavailable');
      expect(create).not.toHaveBeenCalled();
    });
  });

  describe('creating a passkey', () => {
    it("goes through the browser's own conversions where it has them, and answers its JSON", async () => {
      const registration = { id: 'abc', rawId: 'abc', type: 'public-key', response: { clientDataJSON: 'e30' } };
      const { create, parseCreation } = browser({
        conversions: true,
        create: () => Promise.resolve({ toJSON: () => registration }),
      });

      const result = await new WebPasskeys().createAsync(JSON.stringify(creation));

      expect(result.outcome).toBe('succeeded');
      expect(JSON.parse(result.credential!)).toEqual(registration);
      expect(parseCreation).toHaveBeenCalledWith(creation);
      expect(create.mock.calls[0][0].publicKey).toEqual({ converted: 'creation', json: creation });
      expect(create.mock.calls[0][0].signal).toBeInstanceOf(AbortSignal);
    });

    it('converts the members itself where the browser cannot, and answers base64url', async () => {
      const { create } = browser({ create: () => Promise.resolve(created()) });

      const result = await new WebPasskeys().createAsync(JSON.stringify(creation));

      const asked = create.mock.calls[0][0].publicKey as unknown as Record<string, any>;
      expect(bytesOf(asked.challenge)).toEqual(Array.from(challenge));
      expect(bytesOf(asked.user.id)).toEqual(Array.from(userId));
      expect(asked.user.name).toBe('ana@equantic.tech');
      expect(bytesOf(asked.excludeCredentials[0].id)).toEqual(Array.from(excluded));
      expect(asked.rp).toEqual({ name: 'eQuantic Console' });
      expect(asked.pubKeyCredParams).toEqual([{ type: 'public-key', alg: -7 }]);

      expect(result.outcome).toBe('succeeded');
      const json = JSON.parse(result.credential!);
      expect(json).toEqual({
        id: b64url(excluded),
        rawId: b64url(excluded),
        type: 'public-key',
        authenticatorAttachment: 'platform',
        clientExtensionResults: { credProps: { rk: true } },
        response: {
          clientDataJSON: b64url(encoder.encode('{"type":"webauthn.create"}')),
          attestationObject: b64url(encoder.encode('attestation')),
          authenticatorData: b64url(encoder.encode('authenticator data')),
          transports: ['internal', 'hybrid'],
          publicKey: b64url(encoder.encode('public key')),
          publicKeyAlgorithm: -7,
        },
      });
    });

    it('takes the options wrapped in publicKey as the same options', async () => {
      const plain = browser({ create: () => Promise.resolve(created()) });
      await new WebPasskeys().createAsync(JSON.stringify(creation));
      const unwrapped = plain.create.mock.calls[0][0].publicKey;
      vi.unstubAllGlobals();

      const wrapped = browser({ create: () => Promise.resolve(created()) });
      const result = await new WebPasskeys().createAsync(JSON.stringify({ publicKey: creation }));

      expect(result.outcome).toBe('succeeded');
      expect(wrapped.create.mock.calls[0][0].publicKey).toEqual(unwrapped);
    });

    it('a device that already holds a passkey for the account answers alreadyRegistered', async () => {
      browser({ create: rejection('InvalidStateError') });

      const result = await new WebPasskeys().createAsync(JSON.stringify(creation));

      expect(result).toEqual(expect.objectContaining({ outcome: 'alreadyRegistered', credential: null }));
    });

    it('a person who backs out answers cancelled', async () => {
      browser({ create: rejection('NotAllowedError') });

      expect((await new WebPasskeys().createAsync(JSON.stringify(creation))).outcome).toBe('cancelled');
    });

    it('any other refusal, or no credential at all, answers failed', async () => {
      browser({ create: rejection('SecurityError') });
      expect((await new WebPasskeys().createAsync(JSON.stringify(creation))).outcome).toBe('failed');

      browser({ create: () => Promise.resolve(null) });
      expect((await new WebPasskeys().createAsync(JSON.stringify(creation))).outcome).toBe('failed');
    });

    it('options that are not the JSON answer failed, and nothing is asked of the person', async () => {
      const { create } = browser();
      const passkeys = new WebPasskeys();

      expect((await passkeys.createAsync('not json')).outcome).toBe('failed');
      expect((await passkeys.createAsync('[]')).outcome).toBe('failed');
      expect((await passkeys.createAsync(JSON.stringify({ ...creation, user: undefined }))).outcome).toBe('failed');
      expect((await passkeys.createAsync(JSON.stringify({ ...creation, challenge: 7 }))).outcome).toBe('failed');
      expect(create).not.toHaveBeenCalled();
    });
  });

  describe('signing with a passkey', () => {
    it("goes through the browser's own conversions where it has them", async () => {
      const assertion = { id: 'abc', type: 'public-key', response: { signature: 'c2ln' } };
      const { get, parseRequest } = browser({
        conversions: true,
        get: () => Promise.resolve({ toJSON: () => assertion }),
      });

      const result = await new WebPasskeys().authenticateAsync(JSON.stringify({ publicKey: request }));

      expect(result.outcome).toBe('succeeded');
      expect(JSON.parse(result.credential!)).toEqual(assertion);
      expect(parseRequest).toHaveBeenCalledWith(request);
      expect(get.mock.calls[0][0].publicKey).toEqual({ converted: 'request', json: request });
    });

    it('converts the members itself where the browser cannot: signature, authenticator data and user handle', async () => {
      const { get } = browser({ get: () => Promise.resolve(signed(userId.buffer as ArrayBuffer)) });

      const result = await new WebPasskeys().authenticateAsync(JSON.stringify(request));

      const asked = get.mock.calls[0][0].publicKey as unknown as Record<string, any>;
      expect(bytesOf(asked.challenge)).toEqual(Array.from(challenge));
      expect(bytesOf(asked.allowCredentials[0].id)).toEqual(Array.from(excluded));
      expect(asked.userVerification).toBe('preferred');

      expect(result.outcome).toBe('succeeded');
      expect(JSON.parse(result.credential!)).toEqual({
        id: b64url(excluded),
        rawId: b64url(excluded),
        type: 'public-key',
        authenticatorAttachment: 'cross-platform',
        clientExtensionResults: {},
        response: {
          clientDataJSON: b64url(encoder.encode('{"type":"webauthn.get"}')),
          authenticatorData: b64url(encoder.encode('authenticator data')),
          signature: b64url(encoder.encode('signature')),
          userHandle: b64url(userId),
        },
      });
    });

    it('leaves the user handle out when the authenticator gives none', async () => {
      browser({ get: () => Promise.resolve(signed(null)) });

      const result = await new WebPasskeys().authenticateAsync(JSON.stringify(request));

      expect(JSON.parse(result.credential!).response).not.toHaveProperty('userHandle');
    });

    it('a refusal while signing is never alreadyRegistered', async () => {
      browser({ get: rejection('InvalidStateError') });

      expect((await new WebPasskeys().authenticateAsync(JSON.stringify(request))).outcome).toBe('failed');
    });
  });

  describe('the cancellation token', () => {
    it('cancelled while the prompt is open aborts the ceremony, which answers cancelled', async () => {
      let signal: AbortSignal | undefined;
      browser({
        create: (options) =>
          new Promise((_resolve, reject) => {
            signal = options.signal!;
            signal.addEventListener('abort', () => reject(new DOMException('aborted', 'AbortError')));
          }),
      });
      const source = new CancellationTokenSource();

      const pending = new WebPasskeys().createAsync(JSON.stringify(creation), source.token);
      await Promise.resolve();
      source.cancel();

      expect((await pending).outcome).toBe('cancelled');
      expect(signal!.aborted).toBe(true);
    });

    it('cancelled before the call asks nothing of the person', async () => {
      const { get } = browser();
      const source = new CancellationTokenSource();
      source.cancel();

      const result = await new WebPasskeys().authenticateAsync(JSON.stringify(request), source.token);

      expect(result.outcome).toBe('cancelled');
      expect(get).not.toHaveBeenCalled();
    });

    it('is let go once the ceremony ends, so a later cancel aborts nothing', async () => {
      let signal: AbortSignal | undefined;
      browser({
        create: (options) => {
          signal = options.signal!;
          return Promise.resolve(created());
        },
      });
      const source = new CancellationTokenSource();

      expect((await new WebPasskeys().createAsync(JSON.stringify(creation), source.token)).outcome).toBe('succeeded');
      source.cancel();

      expect(signal!.aborted).toBe(false);
    });
  });
});
