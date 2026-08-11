/**
 * Internal aggregator of every symbol eqc-transpiled modules import from "@equantic/runtime".
 * The transpiled shared-library components EMBEDDED in this runtime (shared/components/*.ts) import
 * from HERE instead of "@equantic/runtime" — importing the package name from inside its own bundle
 * would be a self-referential cycle. App-emitted modules keep the package import; the embedded
 * copies are the same pinned bytes with only the import source rewritten (see
 * SharedComponentTranspilationTests).
 */

export { $eq } from '../eq';
export { Component, HtmlElement } from '../core/types';
// The C# `UiComponent` base as it surfaces in transpiled signatures (AdoptConfig, composition).
export { Component as UiComponent } from '../core/types';
export type { RenderContext as BuildContext } from '../core/types';
export {
  StatelessComponent,
  StatefulComponent,
  SharedStatefulComponent,
  ComponentState,
} from '../core/component';
export {
  VisualNode,
  Box,
  BoxStyle,
  Row,
  Column,
  Text,
  Pressable,
  CodeSurface,
  SheetSurface,
  Hoverable,
  Flexible,
  Spacer,
  Stack,
  Positioned,
  Icon,
  IconGlyph,
  Adjustable,
  CameraPreview,
  WebFrame,
  Image,
  ScrollView,
  LoopMotion,
  LinearGradient,
  TextEntry,
  Overlay,
  Presence,
  DragDismiss,
  Link,
  Spinner,
  Anchored,
  Grid,
  GridTrack,
  StyleDiff,
  ShadowSpec,
  TextRun,
  TransitionSpec,
  StyleChannels,
  Shortcut,
  KeyChord,
  KeyModifiers,
  CuratedIcons,
  Draggable,
  SafeArea,
  Sticky,
  AdaptiveNode,
  Vector,
} from './vocabulary';
export { DynamicElement } from '../core/dynamic-element';
export { Navigator } from '../router/navigator';
export {
  Color,
  ColorToken,
  SizeValue,
  EdgeInsets,
  CornerRadii,
  TypeStyle,
  VariantColors,
} from './value-types';
export { ComponentContext } from './photon-context';
// Primitives types route to @equantic/runtime implicitly, so a page reading its route needs this
// name to resolve here or it dies at hydration on an unresolvable import.
export { RouteValues } from './route-values';
// Same promise, same failure shape: a page writing BoxStyle.Transform / .Pattern / .Glow names
// these, and eqc routes every Primitives name here.
export { Transform2D } from './value-types';
export { GridPattern, InFlow, InView, RadialGradient, Simulated } from './vocabulary';
// The Primitives VALUE types a page can name — same implicit promise, see primitive-values.ts.
export {
  GeoLocation,
  ImageData,
  MotionReading,
  MotionSpec,
  MotionVector,
  NetworkState,
  SpringSpec,
  WindowSizeClasses,
} from './primitive-values';
export {
  Space,
  Radius,
  IconSize,
  Touch,
  Motion,
  Curve,
  ButtonStyles,
  Sizing,
  photonTheme,
  PhotonTheme,
} from './design-system.generated';
export { VisualNodeComponent } from './visual-node-component';

// The shared component LIBRARY itself — embeds that compose other embeds (List → ListItem/Divider,
// EmptyState → Button) resolve those names through this aggregator too. The module cycle
// (components/X → runtime-exports → components/X) is benign: ESM live bindings only dereference at
// build()/render() time, never during module evaluation.
// The roster is the GENERATED barrel, never a hand-kept list: a list fell thirteen components
// behind, and each of them transpiled, embedded, and then failed to import.
export * from './components';

// ---- Device capabilities ---------------------------------------------------------------------
// A capability is a service on both sides: C# takes it through a constructor, and here it is
// registered under the interface's own name so a transpiled page resolves the same thing.
export { WebPhotoLibrary } from './devices/photo-library';
export { registerDeviceCapabilities } from './devices/register';
export type { PickedImage } from './devices/photo-library';
