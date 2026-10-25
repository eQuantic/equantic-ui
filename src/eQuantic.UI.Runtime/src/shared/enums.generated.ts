/**
 * GENERATED — do not edit. One string union per non-flags enum of the C# vocabulary
 * (eQuantic.UI.Primitives, eQuantic.UI.Code and eQuantic.UI.Sheet), spelled as the
 * transpiler emits its members: camelCase strings.
 * Regenerate: EQ_UPDATE_ENUMS_TS=1 dotnet test eQuantic.UI.Web.Tests
 * (EnumUnionsTsGeneratorTests pins this file byte-for-byte against the generator).
 *
 * A [Flags] enum is absent by design: its members combine, so it crosses as a number.
 *
 * One table follows the unions: each FontWeight's number, which the runtime asks for by name.
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

export type CodeCommandValue =
  'none' | 'cursorLeft' | 'cursorLeftSelect' | 'cursorRight' | 'cursorRightSelect' | 'cursorUp'
  | 'cursorUpSelect' | 'cursorDown' | 'cursorDownSelect' | 'cursorWordLeft' | 'cursorWordLeftSelect'
  | 'cursorWordRight' | 'cursorWordRightSelect' | 'cursorLineStart' | 'cursorLineStartSelect'
  | 'cursorLineEnd' | 'cursorLineEndSelect' | 'cursorDocumentStart' | 'cursorDocumentStartSelect'
  | 'cursorDocumentEnd' | 'cursorDocumentEndSelect' | 'cursorPageUp' | 'cursorPageUpSelect'
  | 'cursorPageDown' | 'cursorPageDownSelect' | 'selectAll' | 'newLine' | 'deleteLeft'
  | 'deleteRight' | 'deleteWordLeft' | 'deleteWordRight' | 'deleteToLineStart' | 'indent'
  | 'outdent' | 'toggleLineComment' | 'undo' | 'redo' | 'copy' | 'cut' | 'paste' | 'completionShow'
  | 'completionNext' | 'completionPrevious' | 'completionNextPage' | 'completionPreviousPage'
  | 'completionAccept' | 'completionAcceptChangingText' | 'completionHide' | 'signatureHelpShow'
  | 'signatureHelpNext' | 'signatureHelpPrevious' | 'signatureHelpHide' | 'hoverShow' | 'hoverHide'
  | 'problemNext' | 'problemPrevious' | 'releaseKeyboard' | 'moveLinesUp' | 'moveLinesDown'
  | 'copyLinesUp' | 'copyLinesDown' | 'deleteLines' | 'joinLines' | 'sortLinesAscending'
  | 'sortLinesDescending' | 'insertLineAbove' | 'insertLineBelow' | 'addCursorAbove'
  | 'addCursorBelow' | 'addNextOccurrence' | 'selectAllOccurrences' | 'removeSecondaryCursors'
  | 'nextTabStop' | 'previousTabStop' | 'leaveSnippet';

export type CodeCompletionKindValue =
  'text' | 'method' | 'function' | 'constructor' | 'field' | 'variable' | 'class' | 'interface'
  | 'module' | 'property' | 'enum' | 'keyword' | 'snippet' | 'file' | 'struct' | 'enumMember'
  | 'constant' | 'event' | 'operator' | 'typeParameter' | 'value' | 'unit' | 'color' | 'reference'
  | 'folder';

export type CodeCompletionTriggerValue = 'invoked' | 'typing' | 'character' | 'incomplete';

export type CodeDecorationKindValue =
  'highlight' | 'squiggle' | 'outline' | 'strike' | 'underline' | 'line' | 'dots';

export type CodeDiagnosticSeverityValue = 'hint' | 'information' | 'warning' | 'error';

export type CodeDirectionValue = 'backward' | 'forward';

export type CodeGutterKindValue =
  'none' | 'breakpoint' | 'breakpointDisabled' | 'error' | 'warning' | 'added' | 'modified'
  | 'removed' | 'currentStatement';

export type CodeMotionValue =
  'character' | 'word' | 'line' | 'lineBoundary' | 'documentBoundary' | 'page';

export type CodePatchLineKindValue = 'context' | 'removed' | 'added';

export type CodeRowKindValue = 'line' | 'filler' | 'placeholder';

export type CodeSignatureTriggerValue = 'invoked' | 'character' | 'contentChange';

export type CodeTokenKindValue =
  'plain' | 'keyword' | 'type' | 'string' | 'number' | 'comment' | 'operator' | 'punctuation'
  | 'function' | 'attribute' | 'property' | 'constant';

export type ConsentStateValue = 'unknown' | 'granted' | 'denied';

export type CrossAlignValue = 'start' | 'center' | 'end' | 'stretch';

export type DensityValue = 'comfortable' | 'compact';

export type DeviceCapabilityValue =
  'camera' | 'microphone' | 'photoLibrary' | 'networkStatus' | 'photoLibraryAdd' | 'location'
  | 'motion' | 'biometrics' | 'contacts' | 'notifications';

export type DragAxisValue = 'vertical' | 'horizontal';

export type FontWeightValue =
  'thin' | 'extraLight' | 'light' | 'regular' | 'medium' | 'semiBold' | 'bold' | 'extraBold'
  | 'black';

export type GradientDirectionValue = 'toRight' | 'toBottom' | 'toBottomRight' | 'toBottomLeft';

export type HydratedMemberKindValue = 'field' | 'property' | 'capturedParameter' | 'backingField';

export type IconGlyphStyleValue = 'fill' | 'stroke';

export type IconsValue =
  'search' | 'close' | 'check' | 'checkCircle' | 'info' | 'warning' | 'error' | 'person'
  | 'chevronLeft' | 'chevronRight' | 'chevronUp' | 'chevronDown' | 'mail' | 'notifications'
  | 'heart' | 'heartFilled' | 'plus' | 'minus' | 'play' | 'refresh' | 'copy' | 'table' | 'menu'
  | 'calendar' | 'clock';

export type ImageFitValue = 'contain' | 'cover' | 'stretch';

export type KeyboardConventionValue = 'standard' | 'apple';

export type LiveRegionUrgencyValue = 'polite' | 'assertive';

export type LoopEffectValue = 'slideX';

export type MainAlignValue = 'start' | 'center' | 'end' | 'spaceBetween';

export type NavigableMoveValue =
  'previousItem' | 'nextItem' | 'previousRow' | 'nextRow' | 'previousPage' | 'nextPage'
  | 'previousSection' | 'nextSection' | 'rowStart' | 'rowEnd';

export type NavigableRoleValue = 'grid';

export type NetworkKindValue = 'none' | 'wifi' | 'cellular' | 'wired' | 'other';

export type PasskeyOutcomeValue =
  'unavailable' | 'succeeded' | 'cancelled' | 'alreadyRegistered' | 'failed';

export type PermissionStateValue =
  'notDetermined' | 'granted' | 'denied' | 'limited' | 'unavailable';

export type PointerCursorValue =
  'default' | 'pointer' | 'text' | 'notAllowed' | 'crosshair' | 'colResize' | 'rowResize';

export type PointerKindValue = 'mouse' | 'touch' | 'stylus';

export type PointerPhaseValue = 'down' | 'move' | 'up' | 'hover' | 'exit' | 'cancel';

export type PresenceMotionValue = 'fade' | 'slideUp';

export type PressableRoleValue =
  'button' | 'radio' | 'checkbox' | 'switch' | 'tab' | 'menuItem' | 'option' | 'destination'
  | 'gridCell';

export type ScrollAxisValue = 'vertical' | 'horizontal' | 'both';

export type SemanticCheckValue = 'off' | 'on' | 'mixed';

export type SemanticRoleValue =
  'staticText' | 'button' | 'link' | 'textField' | 'codeField' | 'slider' | 'image' | 'checkbox'
  | 'switch' | 'gridCell' | 'progressIndicator' | 'group' | 'radio' | 'tab' | 'menuItem' | 'option'
  | 'destination' | 'tabBar' | 'radioGroup' | 'comboBox' | 'dialog' | 'alertDialog';

export type ServerConnectionStateValue =
  'disconnected' | 'connecting' | 'connected' | 'reconnecting';

export type ServerTopicRefusalReasonValue = 'forbidden' | 'unknown' | 'limitReached' | 'failed';

export type ShapeScaleValue =
  'none' | 'extraSmall' | 'small' | 'medium' | 'large' | 'extraLarge' | 'full';

export type SheetAxisValue = 'rows' | 'cols';

export type SheetEditKindValue =
  'setCells' | 'insertRows' | 'deleteRows' | 'insertCols' | 'deleteCols' | 'resizeRow' | 'resizeCol';

export type SheetEditModeValue = 'enter' | 'edit';

export type SheetEntryViewValue = 'cell' | 'formulaBar';

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

/** Each weight's number, its OpenType weight class, keyed by the name the transpiler emits. */
export const FontWeightNumbers: Readonly<Record<FontWeightValue, number>> = {
    thin: 100,
    extraLight: 200,
    light: 300,
    regular: 400,
    medium: 500,
    semiBold: 600,
    bold: 700,
    extraBold: 800,
    black: 900,
};
