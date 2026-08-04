import { getRootServiceProvider } from '../../core/service-provider';
import { WebBiometrics } from './biometrics';
import { WebLocation } from './location';
import { WebMotionSensor } from './motion-sensor';
import { WebNetworkStatus } from './network-status';
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
  // Registered even though it reports itself unavailable: a page that takes one must RECEIVE one,
  // or it fails to construct instead of showing the fallback it already knows how to show.
  services.registerSingleton('IBiometrics', () => new WebBiometrics());
  services.registerSingleton('INetworkStatus', () => new WebNetworkStatus());
  services.registerSingleton('IMotionSensor', () => new WebMotionSensor());
  services.registerSingleton('ILocation', () => new WebLocation());
}
