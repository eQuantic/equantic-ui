/**
 * The document of an app that declared a LAYOUT (C# `UIOptions.UseLayout<TLayout>()`, #1129): one owner
 * above the page, which outlives client navigations (docs/ELEMENT-TREE-PLAN.md 3.6, 3.7).
 *
 * The layout is built ONCE, around a `PageSlot`, and the document's one `ElementRoots` builds
 * `layout → slot → page` through the transpiled engine like any other tree. A navigation hands the
 * slot the next page and renders: the layout's position keeps its instance (the same node, so the
 * engine updates it in place and it builds again, reading the new `Navigator.location`), and the
 * page's position is replaced, since the page is another node of another type, or of another key
 * (`show`). The owner's frame runs every `onUnmount` before any `onMount`, so the old page leaves
 * before the new one arrives. The page is a position like any nested component: its own `setState`
 * marks it, and the document renders what was marked.
 *
 * The server renders the same tree (`new TLayout(new PageSlot(page))`, measured from the layout at
 * the root), so hydration adopts its markup as it is and every component's server data is named by
 * the same position on both sides (#1477): the layout is `r0/Layout@`, the page stands where the
 * layout placed the slot. Nothing names the page `Type#0` here, and nothing adopts into it by that key.
 *
 * Without a layout none of this runs: each page is its own root, as before.
 */

import type { HtmlNode, RenderContext } from './types';
import type { VisualNodeValue } from '../shared/nodes';
import { RenderManager } from '../dom/renderer';
import { ElementRoots, enterPass, exitPass, lowerRoot } from '../shared/element-roots';
import { renderComponentFailure } from '../shared/component-boundary';
import { StatelessComponent, UiComponent } from './component';
import { scheduleRenderFlush } from './render-scheduler';

/**
 * Where the layout places the page: the node it is handed as its body, which builds the page. The
 * twin of the server's `eQuantic.UI.Server.Rendering.PageSlot`, whose identity it carries.
 *
 * A slot rather than the page itself, so the layout's configuration never changes: handed a new page
 * on every navigation, a stateful layout would learn it only through `adoptConfig`, and one that did
 * not override it would show its first page for ever. go_router hands a `ShellRoute` a nested
 * navigator for the same reason.
 */
export class PageSlot extends StatelessComponent {
  static readonly $typeId = 'eQuantic.UI.Server.Rendering.PageSlot';

  constructor(public page: UiComponent) {
    super();
  }

  build(_context: RenderContext): VisualNodeValue {
    return this.page as unknown as VisualNodeValue;
  }
}

/** A layout's class: a component whose one constructor parameter is the page. */
export type LayoutClass = new (body: VisualNodeValue) => UiComponent;

export class LayoutHost {
  private readonly renderManager = new RenderManager();
  /** The document's element trees: one root, the layout, which keeps every component it builds. */
  private readonly roots = new ElementRoots(() => this._scheduleRender(true));
  /** ONE invalidator for the document's life, as a page keeps one (see `StatefulComponent._invalidate`). */
  private readonly invalidate = (): void => this._scheduleRender();
  private readonly slot: PageSlot;
  /** The layout, built once: the node at the root of every render. */
  readonly layout: UiComponent;
  private mounted = false;
  private renderScheduled = false;
  private navigations = 0;

  constructor(Layout: LayoutClass, page: UiComponent) {
    this.slot = new PageSlot(page);
    this.layout = new Layout(this.slot as unknown as VisualNodeValue);
  }

  /** The page the slot shows. */
  get page(): UiComponent {
    return this.slot.page;
  }

  /**
   * A client navigation: the slot shows `page`, and the document renders. The layout stays and builds
   * again; the old page's position is replaced by the new one's.
   *
   * A page of the same type as the one it replaces is keyed apart, or the engine would keep the old
   * instance at the position and hand it the new one's configuration (`Element.canUpdate`): a
   * navigation is a new page, as it is without a layout, where each one is a fresh instance. A page
   * of another type needs no key, and keeps the DOM patched position by position.
   *
   * A document that is not drawn (never mounted, or left for one without the layout) holds the page
   * and draws it when it mounts, as `_scheduleRender` waits for the mount: it has no DOM to patch.
   */
  show(page: UiComponent): void {
    if (page.constructor === this.slot.page.constructor) page.key = `eq-navigation-${++this.navigations}`;
    this.slot.page = page;
    this.roots.requestPageBuild();
    if (this.mounted) this.renderManager.update(this.render());
  }

  /**
   * One render of the document: the layout lowered through the document's pass, every position built,
   * or only the marked ones when the document's owner asked for the render (a `setState` of the layout,
   * the page or anything they compose).
   */
  render(): HtmlNode {
    const reuse = this.roots.takeReuse();
    enterPass(this.roots, this.invalidate, !reuse);
    try {
      this.roots.remember(this.layout);
      return lowerRoot(this.layout as unknown as VisualNodeValue);
    } catch (error) {
      // The document has no parent to contain it, as a page root has none.
      this.roots.remember(undefined);
      return renderComponentFailure(this.layout.constructor.name, error);
    } finally {
      exitPass();
    }
  }

  mount(container: HTMLElement): void {
    if (this.renderManager.canHydrate(container)) this.hydrate(container);
    else {
      this.renderManager.mount(this.render(), container);
      this.mounted = true;
    }
  }

  /** Adopts the server's markup of the layout and the page, in the order the server wrote it. */
  hydrate(container: HTMLElement): void {
    this.renderManager.hydrate(this.render(), container);
    this.mounted = true;
  }

  /** Takes over a root another document drew (a page written as DOM, shown without the layout). */
  mountReconcile(container: HTMLElement, previousNode: HtmlNode | null): HtmlNode {
    const node = this.render();
    this.renderManager.adopt(node, container, previousNode);
    this.mounted = true;
    return node;
  }

  /** The virtual tree currently reflected in the DOM (the diff baseline for the next navigation). */
  getCurrentTree(): HtmlNode | null {
    return this.renderManager.getCurrentNode();
  }

  getVirtualNode(): HtmlNode {
    return this.render();
  }

  /** The document leaving for one without the layout: the layout and the page unmount with their trees. */
  disposeQuietly(): void {
    this.mounted = false;
    this.roots.unmountAll();
  }

  /**
   * Asks for a render. `onlyMarked` says the document's owner asked, because a position was marked:
   * that render builds the marked positions alone. Anything else (a theme, a culture, a density)
   * builds every position.
   */
  _scheduleRender(onlyMarked = false): void {
    if (!onlyMarked) this.roots.requestPageBuild();
    if (this.renderScheduled) return;
    this.renderScheduled = true;
    scheduleRenderFlush(() => {
      this.renderScheduled = false;
      if (!this.mounted) return;
      this.roots.beginScheduledRender();
      this.renderManager.update(this.render());
    });
  }
}
