/**
 * GENERATED — do not edit. Every wire kind the C# vocabulary declares
 * (eQuantic.UI.Primitives, VisualNode.NodeKind), plus the expansion seam.
 * Regenerate: EQ_UPDATE_NODE_KINDS_TS=1 dotnet test eQuantic.UI.Web.Tests
 * (NodeKindTsGeneratorTests pins this file byte-for-byte against the generator).
 *
 * This union is what makes the browser's dispatch exhaustive: `lowerNodeKind`
 * ends in assertNever, so a kind added here with no case is a build error.
 */

export type NodeKind =
  'adaptive' | 'adjustable' | 'anchored' | 'box' | 'cameraPreview' | 'canvas' | 'codeSurface'
  | 'column' | 'dragDismiss' | 'draggable' | 'drawing' | 'flexible' | 'grid' | 'hoverable' | 'icon'
  | 'image' | 'inFlow' | 'inView' | 'link' | 'loopMotion' | 'navigable' | 'overlay' | 'pinned'
  | 'positioned' | 'presence' | 'pressable' | 'progress' | 'row' | 'safeArea' | 'scrollView'
  | 'sheetSurface' | 'shortcut' | 'simulated' | 'spacer' | 'spinner' | 'stack' | 'text'
  | 'textEntry' | 'vector' | 'webFrame' | 'component';
