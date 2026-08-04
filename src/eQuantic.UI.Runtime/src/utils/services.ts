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
