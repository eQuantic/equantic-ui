import type { CancellationToken } from '../../utils/cancellation';
import type { PasskeyOutcomeValue } from '../enums.generated';
import { PasskeyResult } from '../primitive-values';

/**
 * The browser's IPasskeys: Web Authentication over `navigator.credentials`, in a secure context.
 *
 * The relying party writes the options and verifies the answer, both as the W3C Web Authentication
 * JSON, and this only relays them: it turns the options' base64url members into the bytes the API
 * takes and the credential's bytes back into base64url. Where the browser carries the standard's own
 * conversions (`PublicKeyCredential.parseCreationOptionsFromJSON`, `parseRequestOptionsFromJSON` and
 * `toJSON()`), it uses them, so an extension or a member this file has never heard of crosses as the
 * browser understands it. Where it does not, the members every ceremony needs are converted here,
 * and those alone: an extension whose members are bytes (`prf`, `largeBlob`) needs a browser that
 * carries the conversions, since its salts would reach the API as text and its results come back as
 * empty objects.
 *
 * Like every realization, it resolves and never rejects: each way a ceremony ends is an outcome, and
 * the error names Web Authentication gives are mapped once, below.
 */

/** Which of the two ceremonies: `create` makes a passkey, `get` signs with one. */
type Ceremony = 'create' | 'get';

/** The relying party's options as JSON: an object whose binary members are base64url strings. */
type Json = Record<string, unknown>;

/** The standard's conversions, which recent browsers carry and older DOM typings do not name. */
interface JsonConversions {
  parseCreationOptionsFromJSON?(options: Json): PublicKeyCredentialCreationOptions;
  parseRequestOptionsFromJSON?(options: Json): PublicKeyCredentialRequestOptions;
}

export class WebPasskeys {
  /** A secure context with Web Authentication: an `https` page, or `localhost` while developing. */
  get isAvailable(): boolean {
    return (
      typeof window !== 'undefined' &&
      window.isSecureContext === true &&
      typeof PublicKeyCredential !== 'undefined' &&
      typeof navigator !== 'undefined' &&
      !!navigator.credentials
    );
  }

  createAsync(options: string, cancellationToken?: CancellationToken | null): Promise<PasskeyResult> {
    return this.ceremony('create', options, cancellationToken);
  }

  authenticateAsync(options: string, cancellationToken?: CancellationToken | null): Promise<PasskeyResult> {
    return this.ceremony('get', options, cancellationToken);
  }

  private async ceremony(
    ceremony: Ceremony,
    options: string,
    token: CancellationToken | null | undefined,
  ): Promise<PasskeyResult> {
    if (!this.isAvailable) return new PasskeyResult('unavailable');

    let publicKey: PublicKeyCredentialCreationOptions | PublicKeyCredentialRequestOptions;
    try {
      publicKey = ceremony === 'create' ? creationOptions(options) : requestOptions(options);
    } catch {
      // Not the relying party's JSON: nothing is asked of the person for options the browser would refuse.
      return new PasskeyResult('failed');
    }
    if (token?.isCancellationRequested) return new PasskeyResult('cancelled');

    // A token the call omitted is undefined on this side and cancels nothing.
    const abort = new AbortController();
    const registration = token?.register(() => abort.abort());
    try {
      const credential = (
        ceremony === 'create'
          ? await navigator.credentials.create({
              publicKey: publicKey as PublicKeyCredentialCreationOptions,
              signal: abort.signal,
            })
          : await navigator.credentials.get({
              publicKey: publicKey as PublicKeyCredentialRequestOptions,
              signal: abort.signal,
            })
      ) as PublicKeyCredential | null;
      if (!credential) return new PasskeyResult('failed');
      return new PasskeyResult('succeeded', JSON.stringify(answer(credential)));
    } catch (error) {
      return new PasskeyResult(outcomeOf(ceremony, error));
    } finally {
      registration?.dispose();
    }
  }
}

/**
 * The outcome of a refusal, by the name Web Authentication gives it. `NotAllowedError` is how the
 * standard reports a person who declined and a ceremony that lapsed, alike on purpose, and the
 * `AbortError` an abort raises is the page's own cancellation, so both are `cancelled`. A creation
 * refused with `InvalidStateError` is one the relying party excluded: the device already holds a
 * passkey for the account.
 */
function outcomeOf(ceremony: Ceremony, error: unknown): PasskeyOutcomeValue {
  const name = typeof error === 'object' && error !== null ? (error as { name?: unknown }).name : undefined;
  if (name === 'NotAllowedError' || name === 'AbortError') return 'cancelled';
  if (name === 'InvalidStateError' && ceremony === 'create') return 'alreadyRegistered';
  return 'failed';
}

/** The options' object, unwrapped from the `publicKey` member some relying parties write it under. */
function relyingPartyJson(text: string): Json {
  const parsed: unknown = JSON.parse(text);
  if (!isObject(parsed)) throw new TypeError('the options are not a JSON object');
  return isObject(parsed.publicKey) ? parsed.publicKey : parsed;
}

function creationOptions(text: string): PublicKeyCredentialCreationOptions {
  const json = relyingPartyJson(text);
  const conversions = PublicKeyCredential as unknown as JsonConversions;
  if (typeof conversions.parseCreationOptionsFromJSON === 'function')
    return conversions.parseCreationOptionsFromJSON(json);
  if (!isObject(json.user)) throw new TypeError('the creation options name no user');
  return {
    ...json,
    challenge: bytes(json.challenge),
    user: { ...json.user, id: bytes(json.user.id) },
    excludeCredentials: descriptors(json.excludeCredentials),
  } as unknown as PublicKeyCredentialCreationOptions;
}

function requestOptions(text: string): PublicKeyCredentialRequestOptions {
  const json = relyingPartyJson(text);
  const conversions = PublicKeyCredential as unknown as JsonConversions;
  if (typeof conversions.parseRequestOptionsFromJSON === 'function')
    return conversions.parseRequestOptionsFromJSON(json);
  return {
    ...json,
    challenge: bytes(json.challenge),
    allowCredentials: descriptors(json.allowCredentials),
  } as unknown as PublicKeyCredentialRequestOptions;
}

/** A list of credentials to exclude or allow, each `id` turned into bytes. */
function descriptors(value: unknown): PublicKeyCredentialDescriptor[] | undefined {
  if (value === undefined || value === null) return undefined;
  if (!Array.isArray(value)) throw new TypeError('a credential list is not an array');
  return value.map((descriptor: unknown) => {
    if (!isObject(descriptor)) throw new TypeError('a credential descriptor is not an object');
    return { ...descriptor, id: bytes(descriptor.id) } as unknown as PublicKeyCredentialDescriptor;
  });
}

/**
 * The credential as the relying party reads it: `RegistrationResponseJSON` from a creation,
 * `AuthenticationResponseJSON` from a signature, every binary member in base64url.
 */
function answer(credential: PublicKeyCredential): unknown {
  const own = (credential as unknown as { toJSON?: () => unknown }).toJSON;
  if (typeof own === 'function') return own.call(credential);

  const response = credential.response;
  const common = {
    id: credential.id,
    rawId: base64url(credential.rawId),
    type: credential.type,
    authenticatorAttachment:
      (credential as unknown as { authenticatorAttachment?: string | null }).authenticatorAttachment ?? undefined,
    clientExtensionResults: credential.getClientExtensionResults?.() ?? {},
  };
  if ('attestationObject' in response) {
    const created = response as AuthenticatorAttestationResponse;
    const publicKey = created.getPublicKey?.();
    return {
      ...common,
      response: {
        clientDataJSON: base64url(created.clientDataJSON),
        attestationObject: base64url(created.attestationObject),
        authenticatorData: optional(created.getAuthenticatorData?.()),
        transports: created.getTransports?.() ?? [],
        publicKey: optional(publicKey),
        publicKeyAlgorithm: created.getPublicKeyAlgorithm?.(),
      },
    };
  }
  const signed = response as AuthenticatorAssertionResponse;
  return {
    ...common,
    response: {
      clientDataJSON: base64url(signed.clientDataJSON),
      authenticatorData: base64url(signed.authenticatorData),
      signature: base64url(signed.signature),
      userHandle: optional(signed.userHandle),
    },
  };
}

function optional(value: ArrayBuffer | null | undefined): string | undefined {
  return value ? base64url(value) : undefined;
}

function bytes(value: unknown): Uint8Array {
  if (typeof value !== 'string') throw new TypeError('a binary member is not a base64url string');
  const base64 = value.replace(/-/g, '+').replace(/_/g, '/');
  const binary = atob(base64 + '='.repeat((4 - (base64.length % 4)) % 4));
  const out = new Uint8Array(binary.length);
  for (let i = 0; i < binary.length; i++) out[i] = binary.charCodeAt(i);
  return out;
}

function base64url(value: ArrayBuffer | ArrayBufferView): string {
  const view =
    value instanceof ArrayBuffer ? new Uint8Array(value) : new Uint8Array(value.buffer, value.byteOffset, value.byteLength);
  let binary = '';
  for (let i = 0; i < view.length; i++) binary += String.fromCharCode(view[i]);
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

function isObject(value: unknown): value is Json {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}
