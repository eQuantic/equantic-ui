import { getRootServiceProvider } from '../../core/service-provider';
import { WebPhotoLibrary } from './photo-library';

/**
 * Registers what a BROWSER can do, under the same names the C# interfaces have.
 *
 * The native shells each declare their realizations through `IPhotonCapabilities`; this is the web's
 * equivalent, and it registers by INTERFACE NAME because that is what a transpiled page asks for —
 * C# resolves `IPhotoLibrary` from a constructor, and the string is what survives the crossing.
 *
 * Lazily: a page that never picks a picture never constructs one.
 */
export function registerDeviceCapabilities(): void {
  const services = getRootServiceProvider();
  services.registerSingleton('IPhotoLibrary', () => new WebPhotoLibrary());
}
