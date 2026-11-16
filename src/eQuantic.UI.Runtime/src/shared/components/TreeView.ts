import { $eq, Box, BoxStyle, BuildContext, Color, ColorToken, ControlMetrics, CornerRadii, DateTime, EdgeInsets, Flexible, Icon, IconGlyph, KeyChord, Navigable, NavigableMoveValue, Pressable, Row, ScrollRequest, ScrollView, SdkStrings, Shortcut, SizeValue, StatefulComponent, StyleDiff, Text, TreeNode, TreePlace, TreeViewRow, UiComponent, VisualNode } from "../runtime-exports";

export class TreeView extends StatefulComponent {
    static $typeId = 'eQuantic.UI.Components.TreeView';
    static searchPauseMs: number = 700;
    static rowsPerPageWithoutViewport: number = 10;
    _open: any = $eq.collections.hashSet();
    _loaded: any = $eq.collections.dictionary();
    _loading: any = $eq.collections.hashSet();
    _failed: any = $eq.collections.hashSet();
    _started: any = $eq.collections.hashSet();
    _inTree: boolean = false;
    _rows: TreeViewRow[] = [];
    _activeKey: any = null;
    _activeIsStatus: boolean = false;
    _keyboard: boolean = false;
    _search: string = '';
    _searchAt: DateTime = $eq.time.dateTime.minValue();
    _seeded: boolean = false;
    _revealed: any = null;
    _revealing: any = null;
    _scrollToActive: boolean = false;
    _offset: number = 0;
    _viewport: number = 0;
    _rowHeight: number = 0;
    _scrollVersion: number = 0;
    _scrollRequest: any = null;
    _loadChildren: ((value: TreeNode) => TreeNode[]) | null = null;
    _onToggled: ((arg1: TreeNode, arg2: boolean) => void) | null = null;
    _expandedKeys: any = null;
    _revealPath: any = null;
    _label: string = '';
    _width: SizeValue = SizeValue.fill;
    _height: any = null;
    declare roots: TreeNode[];
    declare selectedKey: any;
    declare onPressed: ((treeNode: TreeNode) => void) | null;

    get loadChildren() {
        return this._loadChildren;
    }

    set loadChildren(value) {
        this._loadChildren = value;
    }

    get onToggled() {
        return this._onToggled;
    }

    set onToggled(value) {
        this._onToggled = value;
    }

    get expandedKeys() {
        return this._expandedKeys;
    }

    set expandedKeys(value) {
        this._expandedKeys = value;
    }

    get revealPath() {
        return this._revealPath;
    }

    set revealPath(value) {
        this._revealPath = value;
    }

    get label() {
        return this._label;
    }

    set label(value) {
        this._label = value;
    }

    get width() {
        return this._width;
    }

    set width(value) {
        this._width = value;
    }

    get height() {
        return this._height;
    }

    set height(value) {
        this._height = value;
    }

    static $slots: any = null;
    static $failure: any = null;

    static $init(): any {
        if (TreeView.$slots === null) {
            if (TreeView.$failure !== null) throw TreeView.$failure;
            let $slots: any = TreeView.$slots = {};
            try {
                $slots.noChildren = null;
                $slots.noChildren = [];
            } catch ($error) {
                TreeView.$slots = null;
                throw TreeView.$failure = $eq.exceptions.typeInitialization('eQuantic.UI.Components.TreeView', $error);
            }
        }
        return TreeView.$slots;
    }

    static get noChildren(): TreeNode[] {
        return TreeView.$init().noChildren;
    }

    static set noChildren(value: TreeNode[]) {
        TreeView.$init().noChildren = value;
    }

    constructor(roots?: any, selectedKey: any = null, onPressed: any = null, props?: any) {
        super();
        if (roots !== undefined) this.roots = roots;
        if (selectedKey !== undefined) this.selectedKey = selectedKey;
        if (onPressed !== undefined) this.onPressed = onPressed;
        this.roots = roots;
        this.selectedKey = selectedKey;
        this.onPressed = onPressed;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        if (!this._seeded) {
            this._seeded = true;
            let keys: any; 
            if ((keys = this.expandedKeys) != null) for (const key of $eq.collections.sequenceWalk(keys)) $eq.collections.setAdd(this._open, key);
        }
        let path: any; 
        if ((($0) => (($0 != null && $eq.collections.count($0) > 0) && (path = $0, true)))(this.revealPath) && !TreeView.samePath(path, this._revealed)) {
            this._revealed = path;
            this._revealing = path;
        }
        this.advanceReveal();
        this._rowHeight = ControlMetrics.height(context, 'small');
        this._rows = this.flatten();
        let statusKey: any; 
        if (this._activeIsStatus && (statusKey = this._activeKey) != null && !this.hasStatusRow(statusKey)) this._activeIsStatus = false;
        let active = this.activeIndex();
        let items: VisualNode[] = [];
        for (let i = 0; i < this._rows.length; i++) $eq.collections.listAdd(items, this.buildRow(context, this._rows[i], i === active && this._keyboard));
        let tree: VisualNode = new Navigable($eq.delegates.bound(this, this.move), items, { role: 'tree', label: this.label, activeCell: active >= 0 ? [active, 0] : null, onTextSearch: $eq.delegates.bound(this, this.search).bind(this) });
        tree = new Shortcut(tree, KeyChord.enter, $eq.delegates.bound(this, this.pressActive), { focusScoped: true, enabled: active >= 0 });
        let height: any; 
        if (!((height = this.height) != null)) return new Box(new BoxStyle({ width: this.width }), tree);
        if (this._scrollToActive && active >= 0 && this._viewport > 0) {
            let top = Math.fround(Math.fround(active) * this._rowHeight);
            let bottom = Math.fround(top + this._rowHeight);
            let target = top < this._offset ? top : bottom > Math.fround(this._offset + this._viewport) ? Math.fround(bottom - this._viewport) : null;
            let offset: any; 
            if ((offset = target) != null) {
                this._scrollVersion++;
                this._scrollRequest = new ScrollRequest(this._scrollVersion, { offset: offset });
            }
        }
        if (this._viewport > 0) this._scrollToActive = false;
        return new ScrollView(tree, 'vertical', { width: this.width, height: height, onScrolled: (offset: number) => this._offset = offset, onViewportChanged: (viewport: number) => this._viewport = viewport, request: this._scrollRequest });
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; 
        if (!((next instanceof TreeView && (fresh = next, true)))) return;
        this.roots = fresh.roots;
        this.selectedKey = fresh.selectedKey;
        this.onPressed = fresh.onPressed;
        this._loadChildren = fresh.loadChildren;
        this._onToggled = fresh.onToggled;
        this._revealPath = fresh.revealPath;
        this._label = fresh.label;
        this._width = fresh.width;
        this._height = fresh.height;
    }

    onMount() {
        this._inTree = true;
        let loader: any; 
        if (!((loader = this.loadChildren) != null)) return;
        for (const key of this._loading) { let node: any; if ((node = this.findNode(key)) != null) this.load(node, loader); }
    }

    onUnmount() {
        return this._inTree = false;
    }

    buildRow(context: BuildContext, row: TreeViewRow, ringed: boolean) {
        let theme = context.theme;
        let node = row.node;
        let chevron = !(node == null) && this.hasChevron(node);
        let open = !(node == null) && this._open.has(node.key);
        let selected = !(node == null) && !(this.selectedKey == null) && node.key === this.selectedKey;
        let failed = node == null && this._failed.has(row.key);
        let content = new Row(4, 'start', 'center', false, null, null, { cross: 'center', width: SizeValue.fill, height: SizeValue.fill });
        content.add(new Box(new BoxStyle({ width: SizeValue.fixed(20), height: SizeValue.fixed(20) }), chevron ? new Icon(IconGlyph.fromIcons(open ? 'chevronDown' : 'chevronRight'), 20, theme.textSecondary) : null));
        let glyph: any; 
        if ((glyph = (node?.icon ?? null)) != null) content.add(new Icon(glyph, 20, theme.textSecondary)); else if (failed) content.add(new Icon(IconGlyph.fromIcons('refresh'), 20, theme.textSecondary));
        let label = node?.label ?? (failed ? SdkStrings.treeLoadFailed : SdkStrings.treeLoading);
        content.add(new Flexible(new Text(label, 'bodyM', node == null ? theme.textSecondary : theme.textPrimary, 1)));
        let quietFill = theme.list?.quietFill ?? theme.surfaceSubtle;
        let surface = new Box(new BoxStyle({ width: SizeValue.fill, height: SizeValue.fixed(this._rowHeight), padding: new EdgeInsets(Math.fround(8 + Math.fround(Math.fround(row.level - 1) * 16)), 0, 8, 0), cornerRadius: new CornerRadii(theme.shape('small')), background: selected ? theme.colors('primary').subtle : null, borderWidth: 2, borderColor: ringed ? theme.focusRing : new ColorToken(Color.transparent), hover: selected ? null : new StyleDiff({ background: quietFill }), pressed: selected ? null : new StyleDiff({ background: quietFill }) }), content);
        return new Pressable(surface, () => this.press(row, false), { role: 'treeItem', label: label, selected: node == null ? null : selected, expanded: chevron ? open : null, treePlace: new TreePlace(row.level, row.position, row.count) });
    }

    childrenOf(node: TreeNode) {
        if (node.isLeaf) return TreeView.noChildren;
        let given: any; 
        if ((given = node.children) != null) return given;
        let loaded: any; 
        if ((($0, $1) => ($0.has($1) ? ((loaded = $0.get($1)), true) : ((loaded = null), false)))(this._loaded, node.key)) return loaded;
        return this.loadChildren == null ? TreeView.noChildren : null;
    }

    hasChevron(node: TreeNode) {
        return (($0) => !(($0 != null && $eq.collections.count($0) === 0)))(this.childrenOf(node));
    }

    flatten() {
        let rows: TreeViewRow[] = [];
        this.addLevel(rows, this.roots, null, 1);
        return rows;
    }

    addLevel(rows: TreeViewRow[], nodes: TreeNode[], parent: any, level: number) {
        for (let i = 0; i < $eq.collections.count(nodes); i++) {
            let node = $eq.collections.item(nodes, i);
            $eq.collections.listAdd(rows, new TreeViewRow(node.key, node, parent, level, i + 1, $eq.collections.count(nodes)));
            if (!this._open.has(node.key)) continue;
            let children = this.childrenOf(node);
            if (!(children == null)) {
                this.addLevel(rows, children, node.key, level + 1);
                continue;
            }
            if (!this._loading.has(node.key) && !this._failed.has(node.key)) this.startLoad(node);
            $eq.collections.listAdd(rows, new TreeViewRow(node.key, null, node.key, level + 1, 1, 1));
        }
    }

    findNode(key: string) {
        return this.findIn(this.roots, key, true);
    }

    findIn(nodes: TreeNode[], key: string, deep: boolean) {
        for (const node of $eq.collections.sequenceWalk(nodes)) {
            if (node.key === key) return node;
            if (!deep || node.isLeaf) continue;
            let loaded: any; 
            let children = node.children ?? ((($0, $1) => ($0.has($1) ? ((loaded = $0.get($1)), true) : ((loaded = null), false)))(this._loaded, node.key) ? loaded : null);
            let found: any; 
            if (!(children == null) && (found = this.findIn(children, key, true)) != null) return found;
        }
        return null;
    }

    activeIndex() {
        if (this._activeKey == null) return -1;
        for (let i = 0; i < this._rows.length; i++) if (this._rows[i].key === this._activeKey && this._rows[i].isStatus === this._activeIsStatus) return i;
        return -1;
    }

    hasStatusRow(key: string) {
        for (const row of $eq.collections.listWalk(this._rows)) if (row.isStatus && row.key === key) return true;
        return false;
    }

    indexOfNode(key: string) {
        for (let i = 0; i < this._rows.length; i++) if (this._rows[i].key === key && !this._rows[i].isStatus) return i;
        return -1;
    }

    static samePath(path: string[], other: any) {
        if (other == null || $eq.collections.count(other) !== $eq.collections.count(path)) return false;
        for (let i = 0; i < $eq.collections.count(path); i++) if ($eq.collections.item(path, i) !== $eq.collections.item(other, i)) return false;
        return true;
    }

    toggle(node: TreeNode, open: boolean) {
        if (!open) {
            this._open.delete(node.key);
            return;
        }
        $eq.collections.setAdd(this._open, node.key);
        if (this.childrenOf(node) == null && !this._loading.has(node.key)) this.startLoad(node);
    }

    startLoad(node: TreeNode) {
        let loader: any; 
        if (!((loader = this.loadChildren) != null)) return;
        $eq.collections.setAdd(this._loading, node.key);
        this._failed.delete(node.key);
        this.load(node, loader);
    }

    async load(node: TreeNode, loader: (value: TreeNode) => TreeNode[]) {
        await new Promise(($resolve) => setTimeout($resolve, 0));
        if (!this._inTree || !this._loading.has(node.key) || !$eq.collections.setAdd(this._started, node.key)) return;
        try {
            let children = await loader(node);
            this.setState(() => {
                this._loading.delete(node.key);
                this._started.delete(node.key);
                $eq.mapSet(this._loaded, node.key, children);
            });
        } catch {
            this.setState(() => {
                this._loading.delete(node.key);
                this._started.delete(node.key);
                $eq.collections.setAdd(this._failed, node.key);
            });
        }
    }

    advanceReveal() {
        let path: any; 
        if (!((path = this._revealing) != null)) return;
        let level = this.roots;
        for (let i = 0; i < $eq.collections.count(path) - 1; i++) {
            let node: any; 
            if (!((node = this.findIn(level, $eq.collections.item(path, i), false)) != null)) return;
            $eq.collections.setAdd(this._open, node.key);
            let children: any; 
            if (!((children = this.childrenOf(node)) != null)) {
                if (this._failed.has(node.key)) this._revealing = null; else if (!this._loading.has(node.key)) this.startLoad(node);
                return;
            }
            level = children;
        }
        let last = $eq.collections.item(path, $eq.collections.count(path) - 1);
        if (this.findIn(level, last, false) == null) return;
        this._activeKey = last;
        this._activeIsStatus = false;
        this._scrollToActive = true;
        this._revealing = null;
    }

    press(row: TreeViewRow, fromKeyboard: boolean) {
        let node: any; 
        if (!((node = row.node) != null)) {
            this.setState(() => {
                this._activeKey = row.key;
                this._activeIsStatus = true;
                this._keyboard = fromKeyboard;
                let failed: any; 
                if (this._failed.has(row.key) && (failed = this.findNode(row.key)) != null) this.startLoad(failed);
            });
            return;
        }
        let toggled = this.hasChevron(node) ? !this._open.has(node.key) : null;
        this.setState(() => {
            this._activeKey = node.key;
            this._activeIsStatus = false;
            this._keyboard = fromKeyboard;
            let open: any; 
            if ((open = toggled) != null) this.toggle(node, open);
        });
        this.onPressed?.(node);
        let opened: any; 
        if ((opened = toggled) != null) this.onToggled?.(node, opened);
    }

    pressActive() {
        let at = this.activeIndex();
        if (at >= 0) this.press(this._rows[at], true);
    }

    move(move: NavigableMoveValue) {
        if (this._rows.length === 0) return;
        let at = this.activeIndex();
        if (at < 0) {
            let selected = this.selectedKey == null ? -1 : this.indexOfNode(this.selectedKey);
            this.activate(selected >= 0 ? selected : 0, null, false);
            return;
        }
        let row = this._rows[at];
        {
            let closed: any; let opened: any;
            switch (move) {
                case 'nextRow':
                    this.activate(Math.min(at + 1, this._rows.length - 1), null, false);
                    break;
                case 'previousRow':
                    this.activate(Math.max(at - 1, 0), null, false);
                    break;
                case 'nextItem':
                    if ((closed = row.node) != null && this.hasChevron(closed)) {
                        if (!this._open.has(closed.key)) this.activate(at, closed, true); else if (at + 1 < this._rows.length && this._rows[at + 1].parentKey === closed.key) this.activate(at + 1, null, false);
                    }
                    break;
                case 'previousItem':
                    if ((opened = row.node) != null && this.hasChevron(opened) && this._open.has(opened.key)) this.activate(at, opened, false); else { let parent: any; if ((parent = row.parentKey) != null) this.activate(this.indexOfNode(parent), null, false); }
                    break;
                case 'rowStart':
                    this.activate(0, null, false);
                    break;
                case 'rowEnd':
                    this.activate(this._rows.length - 1, null, false);
                    break;
                case 'nextPage':
                case 'nextSection':
                    this.activate(Math.min(at + this.rowsPerPage(), this._rows.length - 1), null, false);
                    break;
                case 'previousPage':
                case 'previousSection':
                    this.activate(Math.max(at - this.rowsPerPage(), 0), null, false);
                    break;
            }
        }
    }

    activate(index: number, toggle: any, open: boolean) {
        if (index < 0 || index >= this._rows.length) return;
        let target = this._rows[index];
        this.setState(() => {
            this._activeKey = target.key;
            this._activeIsStatus = target.isStatus;
            this._keyboard = true;
            this._scrollToActive = true;
            if (!(toggle == null)) this.toggle(toggle, open);
        });
        if (!(toggle == null)) this.onToggled?.(toggle, open);
    }

    rowsPerPage() {
        return this._viewport > 0 && this._rowHeight > 0 ? Math.max(1, $eq.num.saturate(Math.floor(Math.fround(this._viewport / this._rowHeight)))) : TreeView.rowsPerPageWithoutViewport;
    }

    search(typed: string) {
        if (this._rows.length === 0) return;
        let now = $eq.time.dateTime.utcNow();
        this._search = (now.diff(this._searchAt)).totalMilliseconds <= TreeView.searchPauseMs ? this._search + typed : typed;
        this._searchAt = now;
        let prefix = this._search;
        let repeated = true;
        for (let i = 1; i < prefix.length; i++) if ($eq.text.toLower(prefix[i], true) !== $eq.text.toLower(prefix[0], true)) repeated = false;
        if (repeated) prefix = $eq.text.substring(prefix, 0, 1);
        let at = this.activeIndex();
        let start = prefix.length > 1 ? Math.max(at, 0) : at + 1;
        for (let step = 0; step < this._rows.length; step++) {
            let index = $eq.num.intRem(start + step, this._rows.length);
            let node: any; 
            if ((node = this._rows[index].node) != null && $eq.text.startsWith(node.label, prefix, 'ordinalIgnoreCase')) {
                this.activate(index, null, false);
                return;
            }
        }
    }
}

