import { getRootServiceProvider } from '../../core/service-provider';
import { WebAppStorage } from './app-storage';
import { WebBiometrics } from './biometrics';
import { WebCamera } from './camera';
import { WebLocation } from './location';
import { WebMotionSensor } from './motion-sensor';
import { WebNetworkStatus } from './network-status';
import { WebPhotoLibrary } from './photo-library';
import { WebTextClipboard } from './text-clipboard';
import { WebCultureController } from './culture-controller';
import { WebAnalytics } from './analytics';
import { WebThemeController } from './theme-controller';

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
  services.registerSingleton('ICamera', () => new WebCamera());
  // The app's own durable key/value store. NOT ISecretStore beside it: the browser has no vault,
  // and registering a store any script on the origin can read under that name would be the
  // framework helping an app make the mistake the two types exist to prevent.
  services.registerSingleton('IAppStorage', () => new WebAppStorage());
  // Not a device, but the same contract: a component asks for the light/dark hand by interface
  // name and never learns that on this target it is one color-scheme declaration.
  services.registerSingleton('IThemeController', () => new WebThemeController());
  // The same contract for the LANGUAGE: switching culture re-renders this page and tells the
  // server through its own cookie, and the component asking never learns either half.
  services.registerSingleton('ICultureController', () => new WebCultureController());
  services.registerSingleton('ITextClipboard', () => new WebTextClipboard());
  // Analytics: registered even without an installer — a page that tracks must RESOLVE something,
  // and the realization itself goes quiet when no collector declared itself (__EQ_ANALYTICS__).
  services.registerSingleton('IAnalytics', () => new WebAnalytics());
}
