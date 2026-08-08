/**
 * TWIN of `eQuantic.UI.Primitives.ComponentBoundary` — the client half of the seam every target
 * expands a component through.
 *
 * A component whose `build` threw used to end the whole render: the mount threw, nothing was ever
 * written to the root, and the page stayed white. One null reference in a card cost every other
 * thing on the screen. So the throw is caught where the component is expanded, its subtree is
 * replaced by a surface that says so, and its siblings render.
 *
 * The surface is built from the VOCABULARY (plain node values, lowered by the same rules as any
 * other node) so it is the same panel the C# realizer draws — which also means an SSR-contained
 * failure and a client-contained failure hydrate against each other instead of fighting.
 */

import type { HtmlNode } from '../core/types';
import { iconPaths } from './icons.generated';
import type {
  BoxNode,
  ColorTokenValue,
  FlexNodeValue,
  IconNode,
  TextNode,
  VisualNodeValue,
} from './nodes';
import type { AppTheme } from './value-types';

/**
 * Whether a DEVELOPER is watching. The boot stamps `window.__EQ_DEV__`, the same flag the hot
 * reload client reads — an exception message is written for whoever wrote the code, and it can name
 * an id, a path or a query that someone's user should never read.
 */
export function boundaryDiagnostics(): boolean {
  return typeof window !== 'undefined' && (window as { __EQ_DEV__?: boolean }).__EQ_DEV__ === true;
}

/**
 * Where a contained failure goes so it is not merely swallowed. A boundary without this trades a
 * loud crash for a silent one, which is worse: nobody sees it and the bug lives forever behind a
 * small red box.
 */
export function reportComponentFailure(componentName: string, error: unknown): void {
  // eslint-disable-next-line no-console
  console.error(`[eQuantic.UI] ${componentName} failed to render and was contained`, error);
}

/**
 * The panel as DOM, for the seams that own a whole tree (a PAGE has no parent to contain it).
 *
 * Registered by the lowering rather than imported from it, and for a concrete reason: the module
 * that lowers nodes reaches the shared components, which reach the runtime's exports, which reach
 * the component base class — importing it from there closes a cycle, and a cycle in ESM means a
 * base class that is `undefined` when a subclass is being defined. So the dependency points one
 * way only, and this leaf module holds the seam.
 */
type FailureRenderer = (componentName: string, error: unknown) => HtmlNode;
let failureRenderer: FailureRenderer | null = null;

export function setFailureRenderer(renderer: FailureRenderer): void {
  failureRenderer = renderer;
}

export function renderComponentFailure(componentName: string, error: unknown): HtmlNode {
  if (failureRenderer) return failureRenderer(componentName, error);
  // Nothing lowered yet (a host that renders before the vocabulary loads): still say it, plainly.
  reportComponentFailure(componentName, error);
  return {
    tag: 'div',
    attributes: { role: 'alert' },
    events: {},
    children: [],
    textContent: boundaryDiagnostics()
      ? `${componentName} failed to render`
      : 'This section could not be displayed.',
  };
}

/** What the C# side prints: the exception's type and message, nothing deeper. */
function describeError(error: unknown): string {
  if (error instanceof Error) return `${error.name}: ${error.message}`;
  return String(error);
}

/** The surface that takes the failed subtree's place — C# `ComponentBoundary.Describe`, twin for twin. */
export function describeComponentFailure(
  componentName: string,
  error: unknown,
  theme: AppTheme,
): VisualNodeValue {
  const tint = theme.colors('destructive');
  const diagnostics = boundaryDiagnostics();

  const lines: TextNode[] = [
    {
      nodeKind: 'text',
      content: diagnostics
        ? `${componentName} failed to render`
        : 'This section could not be displayed.',
      role: 'caption',
      color: tint.onSubtle as ColorTokenValue,
      maxLines: 2,
      styleOverride: { size: 13, lineHeight: 18, weight: 'semiBold', tracking: 0, maxScale: 1.3 },
    },
  ];

  if (diagnostics) {
    lines.push({
      nodeKind: 'text',
      content: describeError(error),
      role: 'caption',
      color: tint.onSubtle as ColorTokenValue,
      maxLines: 6,
      mono: true,
      styleOverride: { size: 12, lineHeight: 17, weight: 'regular', tracking: 0, maxScale: 1.3 },
    });
  }

  const glyph: IconNode = {
    nodeKind: 'icon',
    glyph: { name: 'error', path: iconPaths.error ?? '' },
    size: 20,
    color: tint.onSubtle as ColorTokenValue,
  };

  const column: FlexNodeValue = {
    nodeKind: 'column',
    gap: 4,
    main: 'start',
    cross: 'start',
    children: lines,
  } as FlexNodeValue;

  const row: FlexNodeValue = {
    nodeKind: 'row',
    gap: 10,
    main: 'start',
    cross: 'start',
    children: [glyph, { nodeKind: 'flexible', flex: 1, child: column } as unknown as VisualNodeValue],
  } as FlexNodeValue;

  const panel: BoxNode = {
    nodeKind: 'box',
    style: {
      width: { kind: 'fill', value: 0 },
      padding: { start: 14, top: 12, end: 14, bottom: 12 },
      background: tint.subtle as ColorTokenValue,
      borderWidth: 1,
      borderColor: tint.base as ColorTokenValue,
      cornerRadius: {
        topLeft: theme.shape('large'),
        topRight: theme.shape('large'),
        bottomRight: theme.shape('large'),
        bottomLeft: theme.shape('large'),
      },
    },
    child: row,
  };

  return panel;
}
