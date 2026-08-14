import * as vscode from 'vscode';

/** One node of the rendered screen, as the canvas described it. */
export interface LayerNode {
  origin: string;
  parent: string | null;
  label: string;
  depth: number;
}

/**
 * The screen as a list you can read, next to the screen as a picture you can point at.
 *
 * The canvas can only offer what is under the pointer, and on a full screen there are nodes that are
 * hard to hit and nodes that are impossible — a container filled edge to edge by its own children has
 * no pixel of its own. Every editor worth comparing to has a layers list for exactly that.
 *
 * It needs nothing new from the design host. The rendered DOM already carries an origin and a
 * component name on every node, which IS a layers list; the canvas walks it and posts it up on every
 * render, and this turns that into a tree.
 */
export class LayersProvider implements vscode.TreeDataProvider<string> {
  private readonly changed = new vscode.EventEmitter<string | undefined>();
  readonly onDidChangeTreeData = this.changed.event;

  private nodes = new Map<string, LayerNode>();
  private children = new Map<string | null, string[]>();
  private truncated = false;
  private selected: string | undefined;

  constructor(private readonly onPick: (origin: string) => void) {}

  /** A new tree, from a new frame. */
  update(nodes: LayerNode[], truncated: boolean): void {
    this.nodes = new Map(nodes.map((node) => [node.origin, node]));
    this.children = new Map();
    this.truncated = truncated;

    for (const node of nodes) {
      const siblings = this.children.get(node.parent) ?? [];
      siblings.push(node.origin);
      this.children.set(node.parent, siblings);
    }

    // A node whose parent is not in the map — a subtree the walk cut short — would otherwise be
    // unreachable from the root and simply vanish from the list.
    for (const node of nodes) {
      if (node.parent !== null && !this.nodes.has(node.parent)) {
        const roots = this.children.get(null) ?? [];
        roots.push(node.origin);
        this.children.set(null, roots);
      }
    }

    this.changed.fire(undefined);
  }

  clear(): void {
    this.nodes = new Map();
    this.children = new Map();
    this.selected = undefined;
    this.changed.fire(undefined);
  }

  /** What the canvas has selected, so the list says the same thing the picture does. */
  select(origin: string | undefined): void {
    this.selected = origin;
    this.changed.fire(undefined);
  }

  getChildren(origin?: string): string[] {
    return this.children.get(origin ?? null) ?? [];
  }

  getParent(origin: string): string | undefined {
    return this.nodes.get(origin)?.parent ?? undefined;
  }

  getTreeItem(origin: string): vscode.TreeItem {
    const node = this.nodes.get(origin);
    const has = (this.children.get(origin) ?? []).length > 0;

    const item = new vscode.TreeItem(
      node?.label ?? 'node',
      has ? vscode.TreeItemCollapsibleState.Expanded : vscode.TreeItemCollapsibleState.None,
    );

    item.id = origin;
    // The file and the line, which is the thing a layers list can say that a canvas cannot.
    item.description = LayersProvider.where(origin);
    item.tooltip = origin;
    item.iconPath = new vscode.ThemeIcon(has ? 'symbol-namespace' : 'symbol-field');
    item.contextValue = 'equanticUI.layer';

    if (origin === this.selected) {
      item.iconPath = new vscode.ThemeIcon('circle-filled', new vscode.ThemeColor('charts.blue'));
    }

    item.command = {
      command: 'equanticUI.revealLayer',
      title: 'Select',
      arguments: [origin],
    };

    return item;
  }

  /** `Screens/PaymentsPage.cs:28` — the origin's own coordinates, made readable. */
  private static where(origin: string): string {
    const [path, start] = origin.split('|');
    if (!path || !start) return '';
    const line = Number(start.split(':')[0]);
    const name = path.split(/[\\/]/).pop() ?? path;
    return Number.isFinite(line) ? `${name}:${line + 1}` : name;
  }

  /** How many nodes the list is holding — a test seam, and the only thing this exports. */
  get size(): number {
    return this.nodes.size;
  }

  get cut(): boolean {
    return this.truncated;
  }

  pick(origin: string): void {
    this.onPick(origin);
  }
}
