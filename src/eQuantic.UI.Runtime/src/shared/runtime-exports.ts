/**
 * Internal aggregator of every symbol eqc-transpiled modules import from "@equantic/runtime".
 * The transpiled shared-library components EMBEDDED in this runtime (shared/components/*.ts) import
 * from HERE instead of "@equantic/runtime" — importing the package name from inside its own bundle
 * would be a self-referential cycle. App-emitted modules keep the package import; the embedded
 * copies are the same pinned bytes with only the import source rewritten (see
 * SharedComponentTranspilationTests).
 */

export { $eq } from '../eq';
export {
  Component,
  HtmlElement,
} from '../core/types';
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
  Flexible,
  Spacer,
  Stack,
  Positioned,
  Icon,
} from './vocabulary';
export {
  ColorToken,
  SizeValue,
  EdgeInsets,
  CornerRadii,
  TypeStyle,
  VariantColors,
} from './value-types';
export { ComponentContext } from './photon-context';
export {
  Space,
  Radius,
  IconSize,
  Touch,
  Motion,
  ButtonStyles,
  photonTheme,
  PhotonTheme,
} from './design-system.generated';
export { VisualNodeComponent } from './visual-node-component';
