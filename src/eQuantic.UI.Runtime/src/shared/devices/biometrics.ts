/**
 * The browser's answer about the fingerprint reader: it does not have one.
 *
 * Not an oversight — a decision worth stating. WebAuthn can verify a user with a platform
 * authenticator, and it looks similar from a distance, but it is a different contract: it needs a
 * credential registered against a server, a challenge, and a relying party. `IBiometrics` asks the
 * device "is this the person who owns you?" and gets a local yes or no with nothing to store.
 * Pretending WebAuthn satisfies that would give apps a method that fails in ways the interface
 * cannot describe.
 *
 * So the honest answer is `unavailable`, which the contract already has a place for — and a page
 * written once shows its fallback in a browser and its prompt on a phone, from the same code.
 */
export class WebBiometrics {
  get isAvailable(): boolean {
    return false;
  }

  async authenticateAsync(_reason: string): Promise<string> {
    return 'unavailable';
  }
}
