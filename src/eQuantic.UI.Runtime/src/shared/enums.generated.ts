/**
 * GENERATED — do not edit. One string union per non-flags enum of the C# vocabulary
 * (eQuantic.UI.Primitives), spelled as the transpiler emits its members: camelCase strings.
 * Regenerate: EQ_UPDATE_ENUMS_TS=1 dotnet test eQuantic.UI.Web.Tests
 * (EnumUnionsTsGeneratorTests pins this file byte-for-byte against the generator).
 *
 * A [Flags] enum is absent by design: its members combine, so it crosses as a number.
 */

export type AdjustableRoleValue = 'slider' | 'tablist' | 'radiogroup';

export type AlignmentValue =
  'topStart' | 'topCenter' | 'topEnd' | 'centerStart' | 'center' | 'centerEnd' | 'bottomStart'
  | 'bottomCenter' | 'bottomEnd';

export type AnchorPanelRoleValue = 'none' | 'menu' | 'listbox' | 'dialog';

export type AnchorPlacementValue =
  'bottomStart' | 'bottomEnd' | 'topStart' | 'topEnd' | 'bottomCenter' | 'topCenter';

export type BiometricResultValue =
  'succeeded' | 'failed' | 'cancelled' | 'fallbackRequested' | 'notEnrolled' | 'unavailable';

export type CodeCompletionKindValue =
  'text' | 'method' | 'function' | 'constructor' | 'field' | 'variable' | 'class' | 'interface'
  | 'module' | 'property' | 'enum' | 'keyword' | 'snippet' | 'file';

export type CodeDecorationKindValue = 'highlight' | 'squiggle' | 'outline' | 'strike';

export type CodeDiagnosticSeverityValue = 'hint' | 'information' | 'warning' | 'error';

export type CodeDirectionValue = 'backward' | 'forward';

export type CodeGutterKindValue =
  'none' | 'breakpoint' | 'breakpointDisabled' | 'error' | 'warning' | 'added' | 'modified'
  | 'removed' | 'currentStatement';

export type CodeMotionValue =
  'character' | 'word' | 'line' | 'lineBoundary' | 'documentBoundary' | 'page';

export type CodeTokenKindValue =
  'plain' | 'keyword' | 'type' | 'string' | 'number' | 'comment' | 'operator' | 'punctuation'
  | 'function' | 'attribute' | 'property' | 'constant';

export type CrossAlignValue = 'start' | 'center' | 'end' | 'stretch';

export type DensityValue = 'comfortable' | 'compact';

export type DeviceCapabilityValue =
  'camera' | 'microphone' | 'photoLibrary' | 'networkStatus' | 'photoLibraryAdd' | 'location'
  | 'motion' | 'biometrics' | 'contacts' | 'notifications';

export type DragAxisValue = 'vertical' | 'horizontal';

export type FontWeightValue = 'regular' | 'medium' | 'semiBold' | 'bold' | 'extraBold';

export type GradientDirectionValue = 'toRight' | 'toBottom' | 'toBottomRight' | 'toBottomLeft';

export type IconGlyphStyleValue = 'fill' | 'stroke';

export type IconsValue =
  'search' | 'close' | 'check' | 'checkCircle' | 'info' | 'warning' | 'error' | 'person'
  | 'chevronLeft' | 'chevronRight' | 'chevronUp' | 'chevronDown' | 'mail' | 'notifications'
  | 'heart' | 'heartFilled' | 'plus' | 'minus' | 'play' | 'refresh' | 'copy' | 'table' | 'menu'
  | 'calendar' | 'clock';

export type ImageFitValue = 'contain' | 'cover' | 'stretch';

export type LoopEffectValue = 'slideX';

export type MainAlignValue = 'start' | 'center' | 'end' | 'spaceBetween';

export type NavigableMoveValue =
  'previousItem' | 'nextItem' | 'previousRow' | 'nextRow' | 'previousPage' | 'nextPage'
  | 'previousSection' | 'nextSection' | 'rowStart' | 'rowEnd';

export type NavigableRoleValue = 'grid';

export type NetworkKindValue = 'none' | 'wifi' | 'cellular' | 'wired' | 'other';

export type PermissionStateValue =
  'notDetermined' | 'granted' | 'denied' | 'limited' | 'unavailable';

export type PointerCursorValue =
  'default' | 'pointer' | 'text' | 'notAllowed' | 'crosshair' | 'colResize' | 'rowResize';

export type PresenceMotionValue = 'fade' | 'slideUp';

export type PressableRoleValue =
  'button' | 'radio' | 'checkbox' | 'switch' | 'tab' | 'menuItem' | 'option' | 'destination'
  | 'gridCell';

export type ScrollAxisValue = 'vertical' | 'horizontal' | 'both';

export type ShapeScaleValue =
  'none' | 'extraSmall' | 'small' | 'medium' | 'large' | 'extraLarge' | 'full';

export type SheetAxisValue = 'rows' | 'cols';

export type SheetEditKindValue =
  'setCells' | 'insertRows' | 'deleteRows' | 'insertCols' | 'deleteCols' | 'resizeRow' | 'resizeCol';

export type SheetMotionValue = 'cell' | 'dataEdge' | 'rowBoundary' | 'sheetBoundary';

export type SizeKindValue = 'hug' | 'fill' | 'fixed' | 'windowMinus';

export type SizeVariantValue = 'small' | 'medium' | 'large' | 'xLarge';

export type TextAlignmentValue = 'start' | 'center' | 'end';

export type ThemeModeValue = 'light' | 'dark';

export type TypeRoleValue =
  'display' | 'heading' | 'title' | 'bodyL' | 'bodyM' | 'label' | 'caption' | 'titleSmall'
  | 'labelSmall' | 'overline';

export type VariantValue =
  'primary' | 'secondary' | 'destructive' | 'outline' | 'ghost' | 'link' | 'success' | 'warning'
  | 'info' | 'tertiary';

export type VectorPaintKindValue =
  'none' | 'inherit' | 'solid' | 'linearGradient' | 'radialGradient';

export type VectorVerbValue = 'move' | 'line' | 'cubic' | 'close';

export type WindowChromeValue = 'standard' | 'unified';

export type WindowSizeClassValue = 'compact' | 'medium' | 'expanded';
