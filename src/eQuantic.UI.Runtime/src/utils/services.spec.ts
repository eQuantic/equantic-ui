import { afterEach, describe, expect, it } from 'vitest';
import { getRootServiceProvider, resetServiceProvider } from '../core/service-provider';
import { provideService, resolveService } from './services';

// What eqc writes at every place the browser resolves an app's own service (#1335):
// resolve(provide('IKey', () => new Impl(), lifetime)).
describe('an app service provided where the browser resolves it', () => {
  afterEach(() => resetServiceProvider());

  it('builds one singleton, whichever module provides it first', () => {
    let built = 0;
    const factory = () => ({ id: ++built });
    const first = resolveService(provideService('ILedger', factory, 'singleton'));
    const second = resolveService(provideService('ILedger', factory, 'singleton'));
    expect(first).toBe(second);
    expect(built).toBe(1);
  });

  it('keeps one scoped instance, since a browser session is one scope', () => {
    let built = 0;
    const factory = () => ({ id: ++built });
    const first = resolveService(provideService('IScoped', factory, 'scoped'));
    const second = resolveService(provideService('IScoped', factory, 'scoped'));
    expect(first).toBe(second);
    expect(built).toBe(1);
  });

  it('builds a transient every time it is resolved', () => {
    let built = 0;
    const key = provideService('ITransient', () => ({ id: ++built }), 'transient');
    expect(resolveService(key)).not.toBe(resolveService(key));
    expect(built).toBe(2);
  });

  it('leaves a service already registered as it is', () => {
    const sdk = { sdk: true };
    getRootServiceProvider().registerSingleton('IClipboard', () => sdk);
    const resolved = resolveService(provideService('IClipboard', () => ({ app: true }), 'singleton'));
    expect(resolved).toBe(sdk);
  });
});
