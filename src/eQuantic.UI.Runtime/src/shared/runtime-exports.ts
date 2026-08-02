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
  Hoverable,
  Flexible,
  Spacer,
  Stack,
  Positioned,
  Icon,
  IconGlyph,
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
export { Button } from './components/Button';
export { Card } from './components/Card';
export { Divider } from './components/Divider';
export { Badge } from './components/Badge';
export { Chip } from './components/Chip';
export { ProgressBar } from './components/ProgressBar';
export { Avatar } from './components/Avatar';
export { Banner } from './components/Banner';
export { IconButton } from './components/IconButton';
export { Checkbox } from './components/Checkbox';
export { Switch } from './components/Switch';
export { RadioGroup } from './components/RadioGroup';
export { ListItem } from './components/ListItem';
export { List } from './components/List';
export { Tabs } from './components/Tabs';
export { EmptyState } from './components/EmptyState';
export { Skeleton } from './components/Skeleton';
export { TextInput } from './components/TextInput';
export { Dialog } from './components/Dialog';
export { DialogAction } from './components/DialogAction';
export { SearchField } from './components/SearchField';
export { AppBar } from './components/AppBar';
export { BottomNavigation } from './components/BottomNavigation';
export { NavItem } from './components/NavItem';
export { Breadcrumb } from './components/Breadcrumb';
export { Crumb } from './components/Crumb';
export { DataColumn } from './components/DataColumn';
export { DataRow } from './components/DataRow';
export { DataTable } from './components/DataTable';
export { PageIndicator } from './components/PageIndicator';
export { Pagination } from './components/Pagination';
export { SegmentedControl } from './components/SegmentedControl';
export { Slider } from './components/Slider';
export { Stepper } from './components/Stepper';
