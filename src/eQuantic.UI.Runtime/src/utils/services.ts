import { getRootServiceProvider } from '../core/service-provider';

/**
 * The service behind an interface NAME, or null when nothing satisfies it here.
 *
 * A transpiled page takes its dependencies through its constructor, exactly as it does natively;
 * on the native side ActivatorUtilities resolves them, and this is the same step in the browser.
 * The key is the interface's name because that is what survives the crossing — a C# type does not
 * exist at run time here, but `IPhotoLibrary` as a string does, and both sides agree on it.
 *
 * NULL rather than a throw for something unregistered: a capability a browser does not have is not
 * an error, it is an absence, and a page that declared the parameter nullable already said what to
 * do about it. A page that did not gets a null and fails where it uses it — which names the real
 * problem far better than a failure to construct.
 */
export function resolveService(interfaceName: string): unknown {
  return getRootServiceProvider().getService(interfaceName) ?? null;
}

/** How long one instance of a service lives, as the app registered it in its container. */
export type ServiceLifetimeName = 'singleton' | 'scoped' | 'transient';

/**
 * Registers an app's own service when nothing has yet, and answers its key.
 *
 * The app registers its services once, in C#: `builder.Services.AddSingleton<IWalletLedger,
 * WalletLedger>()` in `Program.cs`. The server's container hands them out from that line, Photon's
 * from the same one, and eqc writes it into every place the browser resolves the service:
 * `resolve(provide('IWalletLedger', () => new WalletLedger(), 'singleton'))`. Every place, so the
 * module that happens to run first registers it and the others find the same singleton, whichever
 * page the browser opened.
 *
 * What is registered already wins: the SDK's own capabilities, and a registration an earlier
 * resolution made. One browser session is one scope, so a scoped service lives as long as a
 * singleton does, which is what a scope means wherever there is one user and no request.
 */
export function provideService(
  interfaceName: string,
  factory: () => unknown,
  lifetime: ServiceLifetimeName
): string {
  const services = getRootServiceProvider();
  if (services.hasService(interfaceName)) return interfaceName;
  switch (lifetime) {
    case 'transient':
      services.registerTransient(interfaceName, factory);
      break;
    case 'scoped':
      services.registerScoped(interfaceName, factory);
      break;
    default:
      services.registerSingleton(interfaceName, factory);
      break;
  }
  return interfaceName;
}
