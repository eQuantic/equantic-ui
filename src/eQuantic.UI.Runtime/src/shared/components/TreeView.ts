import { $eq, $NavigableMoveValue, Anchored, Box, BoxStyle, BuildContext, Color, ColorToken, ContextRequest, ContextTarget, ControlMetrics, CornerRadii, DateTime, EdgeInsets, Flexible, Icon, IconGlyph, KeyChord, ListTheme, MenuItem, MenuPanel, Navigable, Positioned, Pressable, Row, ScrollRequest, ScrollView, SdkStrings, Shortcut, SizeValue, Sortable, SortableItem, SortDrop, Stack, StatefulComponent, StyleDiff, Text, TextEntry, TreeMove, TreeNode, TreePlace, TreeRowState, TreeViewRow, UiComponent, VisualNode } from "../runtime-exports";

export class TreeView extends StatefulComponent {
    static $typeId = 'eQuantic.UI.Components.TreeView';
    static searchPauseMs: number = 700;
    static rowsPerPageWithoutViewport: number = 10;
    static openUnderPauseMs: number = 700;
    static dropInto: number = 1;
    static dropBefore: number = 2;
    static dropAfter: number = 3;
    _open: any = $eq.collections.hashSet();
    _loaded: any = $eq.collections.dictionary();
    _loading: any = $eq.collections.hashSet();
    _failed: any = $eq.collections.hashSet();
    _started: any = $eq.collections.hashSet();
    _stale: any = $eq.collections.hashSet();
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
    _editKey: any = null;
    _editCreates: boolean = false;
    _editIcon: any = null;
    _editText: string = '';
    _editSerial: number = 0;
    _editSeen: any = null;
    _reloadSeen: any = null;
    _working: any = $eq.collections.hashSet();
    _busy: any = $eq.collections.hashSet();
    _focusVersion: number = 0;
    _menuKey: any = null;
    _menuAt: any = null;
    _menuItems: MenuItem[] = [];
    _menuHighlight: number = 0;
    _activeParent: any = null;
    _dropKey: any = null;
    _dropKind: number = 0;
    _dropLevel: number = 0;
    _indent: number = 0;
    _pauseKey: any = null;
    _pauseSerial: number = 0;
    _scrollToActive: boolean = false;
    _offset: number = 0;
    _viewport: number = 0;
    _rowHeight: number = 0;
    _scrollVersion: number = 0;
    _scrollRequest: any = null;
    _rowBuilder: ((value: TreeRowState) => VisualNode) | null = null;
    _style: any = null;
    _onRenamed: ((arg1: TreeNode, arg2: string) => void) | null = null;
    _onCreated: ((arg1: TreeNode, arg2: string) => void) | null = null;
    _edit: any = null;
    _reload: any = null;
    _busyKeys: any = null;
    _menuBuilder: ((value: TreeNode) => MenuItem[]) | null = null;
    _onMenuSelect: ((arg1: TreeNode, arg2: number) => void) | null = null;
    _onMoved: ((value: TreeMove) => void) | null = null;
    _canMove: ((value: TreeMove) => boolean) | null = null;
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

    get rowBuilder() {
        return this._rowBuilder;
    }

    set rowBuilder(value) {
        this._rowBuilder = value;
    }

    get style() {
        return this._style;
    }

    get onRenamed() {
        return this._onRenamed;
    }

    set onRenamed(value) {
        this._onRenamed = value;
    }

    get onCreated() {
        return this._onCreated;
    }

    set onCreated(value) {
        this._onCreated = value;
    }

    get edit() {
        return this._edit;
    }

    set edit(value) {
        this._edit = value;
    }

    get reload() {
        return this._reload;
    }

    set reload(value) {
        this._reload = value;
    }

    get busyKeys() {
        return this._busyKeys;
    }

    set busyKeys(value) {
        this._busyKeys = value;
    }

    get menuBuilder() {
        return this._menuBuilder;
    }

    set menuBuilder(value) {
        this._menuBuilder = value;
    }

    get onMenuSelect() {
        return this._onMenuSelect;
    }

    set onMenuSelect(value) {
        this._onMenuSelect = value;
    }

    get onMoved() {
        return this._onMoved;
    }

    set onMoved(value) {
        this._onMoved = value;
    }

    get canMove() {
        return this._canMove;
    }

    set canMove(value) {
        this._canMove = value;
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

    constructor(roots?: any, selectedKey: any = null, onPressed: any = null, style: any = null, props?: any) {
        super();
        if (roots !== undefined) this.roots = roots;
        if (selectedKey !== undefined) this.selectedKey = selectedKey;
        if (onPressed !== undefined) this.onPressed = onPressed;
        this.roots = roots;
        this.selectedKey = selectedKey;
        this.onPressed = onPressed;
        this._style = style;
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
        this._busy = $eq.collections.hashSet(false, this._working);
        let busyKeys: any; 
        if ((busyKeys = this.busyKeys) != null) for (const key of $eq.collections.sequenceWalk(busyKeys)) $eq.collections.setAdd(this._busy, key);
        this.adoptRequests();
        let slot = ListTheme.merge(context.theme.list, this.style);
        this._rowHeight = slot?.treeRowHeight ?? ControlMetrics.height(context, 'small');
        this._indent = slot?.treeIndent ?? 16;
        this._rows = this.flatten();
        let statusKey: any; 
        if (this._activeIsStatus && (statusKey = this._activeKey) != null && !this.hasStatusRow(statusKey)) this._activeIsStatus = false;
        let parent: any; 
        if (!(this._activeKey == null) && !this._activeIsStatus && this.activeIndex() < 0 && (parent = this._activeParent) != null && this.indexOfNode(parent) >= 0) this._activeKey = parent;
        let active = this.activeIndex();
        this._activeParent = active >= 0 ? this._rows[active].parentKey : null;
        let menuKey: any; 
        if ((menuKey = this._menuKey) != null && (this.indexOfNode(menuKey) < 0 || this._busy.has(menuKey))) this._menuKey = null;
        let editKey: any; 
        if ((editKey = this._editKey) != null && this.indexOfNode(editKey) < 0) {
            this._editKey = null;
            this._editSerial++;
        }
        let items: VisualNode[] = [];
        for (let i = 0; i < this._rows.length; i++) $eq.collections.listAdd(items, this.buildRow(context, slot, this._rows[i], i === active && this._keyboard));
        let tree: VisualNode = new Navigable($eq.delegates.bound(this, this.move), items, { role: 'tree', label: this.label, activeCell: active >= 0 ? [active, 0] : null, onTextSearch: $eq.delegates.bound(this, this.search).bind(this), focusVersion: this._focusVersion });
        let editing = !(this._editKey == null);
        if (!(this.onMoved == null)) {
            tree = new Sortable(tree, 'tree', $eq.delegates.bound(this, this.dropped), SdkStrings.sorting, null, { axis: 'vertical', nests: true, indent: this._indent, label: this.label, accepts: $eq.delegates.bound(this, this.accepts).bind(this), onHovered: $eq.delegates.bound(this, this.hovered).bind(this), enabled: !editing });
        }
        let menuOpen = !(this._menuKey == null);
        let activeNode = active >= 0 ? this._rows[active].node : null;
        let activeFree = !(activeNode == null) && !this._busy.has(activeNode.key);
        tree = new Shortcut(tree, KeyChord.enter, $eq.delegates.bound(this, this.pressActive), { focusScoped: true, enabled: active >= 0 && !editing && !menuOpen });
        tree = new Shortcut(tree, new KeyChord('F2'), $eq.delegates.bound(this, this.renameActive), { focusScoped: true, enabled: !(this.onRenamed == null) && activeFree && !editing && !menuOpen });
        tree = MenuPanel.withKeys(tree, menuOpen && $eq.collections.count(this._menuItems) > 0, () => this.setState(() => this._menuHighlight = MenuPanel.step(this._menuItems, this._menuHighlight, +1)), () => this.setState(() => this._menuHighlight = MenuPanel.step(this._menuItems, this._menuHighlight, -1)), () => this.chooseMenu(this._menuHighlight));
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
        this._rowBuilder = fresh.rowBuilder;
        this._style = fresh.style;
        this._onRenamed = fresh.onRenamed;
        this._onCreated = fresh.onCreated;
        this._edit = fresh.edit;
        this._reload = fresh.reload;
        this._busyKeys = fresh.busyKeys;
        this._menuBuilder = fresh.menuBuilder;
        this._onMenuSelect = fresh.onMenuSelect;
        this._onMoved = fresh.onMoved;
        this._canMove = fresh.canMove;
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

    buildRow(context: BuildContext, slot: any, row: TreeViewRow, ringed: boolean) {
        if (row.isField) return this.buildFieldRow(context, slot, row);
        let theme = context.theme;
        let node = row.node;
        let chevron = !(node == null) && this.hasChevron(node);
        let open = !(node == null) && this._open.has(node.key);
        let selected = !(node == null) && !(this.selectedKey == null) && node.key === this.selectedKey;
        let failed = node == null && this._failed.has(row.key);
        let busy = !(node == null) && this._busy.has(node.key);
        let renaming = !(node == null) && !this._editCreates && this._editKey === node.key;
        let iconColor = slot?.treeIconColor ?? theme.textSecondary;
        let content = TreeView.content(context, slot, chevron, open);
        let label = node?.label ?? (failed ? SdkStrings.treeLoadFailed : SdkStrings.treeLoading);
        if (renaming) {
            let editedGlyph: any; 
            if ((editedGlyph = (node!).icon) != null) content.add(new Icon(editedGlyph, 20, iconColor));
            content.add(new Flexible(this.field(context)));
        } else { let builder: any; if (!(node == null) && (builder = this.rowBuilder) != null) {
            content.add(new Flexible(builder(new TreeRowState(node, row.level, chevron ? open : null, selected, busy))));
        } else {
            let glyph: any; 
            if ((glyph = (node?.icon ?? null)) != null) content.add(new Icon(glyph, 20, iconColor)); else if (failed) content.add(new Icon(IconGlyph.fromIcons('refresh'), 20, iconColor));
            content.add(new Flexible(new Text(label, 'bodyM', node == null ? theme.textSecondary : slot?.titleColor ?? theme.textPrimary, 1)));
        } }
        let quietFill = slot?.quietFill ?? theme.surfaceSubtle;
        let quiet = selected || busy || renaming ? null : new StyleDiff({ background: quietFill });
        let mark = !(node == null) && this._dropKey === node.key ? this._dropKind : 0;
        let fill = selected ? theme.colors('primary').subtle : mark === TreeView.dropInto ? theme.selection : null;
        let plain = this.rowSurface(context, slot, row.level, content, fill, ringed && !renaming, busy, quiet, mark === TreeView.dropInto ? TreeView.dropColor(context, slot) : null);
        let surface = mark === TreeView.dropBefore || mark === TreeView.dropAfter ? this.dropLine(context, slot, this._dropLevel > 0 ? this._dropLevel : row.level, plain, mark === TreeView.dropAfter) : plain;
        let key = node == null ? `status/${row.key}` : node.key;
        let item: VisualNode = new Pressable(surface, busy || renaming ? null : () => this.press(row, false), { key: key, role: 'treeItem', label: label, selected: node == null ? null : selected, expanded: chevron ? open : null, treePlace: new TreePlace(row.level, row.position, row.count), disabled: busy });
        if (node == null) return item;
        if (!(this.onMoved == null)) item = new SortableItem(item, node.key, { key: key, enabled: !busy && this._editKey == null, depth: row.level - 1 });
        if (!(this.menuBuilder == null)) item = new ContextTarget(item, (request) => this.requestMenu(node, request), { key: key, enabled: !busy && !renaming });
        if (this._menuKey === node.key) {
            item = new Anchored(item, MenuPanel.build(context, this._menuItems, true, this._menuHighlight, $eq.delegates.bound(this, this.chooseMenu)), { key: key, at: this._menuAt, placement: 'bottomStart', open: true, onDismiss: $eq.delegates.bound(this, this.closeMenu).bind(this), panelRole: 'menu', activeIndex: this._menuHighlight });
        }
        return item;
    }

    buildFieldRow(context: BuildContext, slot: any, row: TreeViewRow) {
        let content = TreeView.content(context, slot, false, false);
        let glyph: any; 
        if ((glyph = this._editIcon) != null) content.add(new Icon(glyph, 20, slot?.treeIconColor ?? context.theme.textSecondary));
        content.add(new Flexible(this.field(context)));
        let surface = this.rowSurface(context, slot, row.level, content, null, false, false, null);
        return new Pressable(surface, null, { key: `field/${row.key}`, role: 'treeItem', label: SdkStrings.treeItemName, treePlace: new TreePlace(row.level, row.position, row.count) });
    }

    static content(context: BuildContext, slot: any, chevron: boolean, open: boolean) {
        let size = 20;
        let content = new Row(slot?.treeGap ?? 4, 'start', 'center', false, null, null, { cross: 'center', width: SizeValue.fill, height: SizeValue.fill });
        content.add(new Box(new BoxStyle({ width: SizeValue.fixed(size), height: SizeValue.fixed(size) }), chevron ? new Icon(IconGlyph.fromIcons(open ? 'chevronDown' : 'chevronRight'), size, slot?.chevronColor ?? context.theme.textSecondary) : null));
        return content;
    }

    rowSurface(context: BuildContext, slot: any, level: number, content: VisualNode, fill: any, ringed: boolean, busy: boolean, quiet: any, marked: any = null) {
        let theme = context.theme;
        let padding = slot?.treePaddingX ?? 8;
        let indent = slot?.treeIndent ?? 16;
        return new Box(new BoxStyle({ width: SizeValue.fill, height: SizeValue.fixed(this._rowHeight), padding: new EdgeInsets(Math.fround(padding + Math.fround(Math.fround(level - 1) * indent)), 0, padding, 0), cornerRadius: new CornerRadii(theme.shape(slot?.treeRowShape ?? 'small')), background: fill, borderWidth: 2, borderColor: marked ?? (ringed ? theme.focusRing : new ColorToken(Color.transparent)), opacity: busy ? theme.disabledOpacity : null, hover: quiet, pressed: quiet }), content);
    }

    field(context: BuildContext) {
        let theme = context.theme;
        let serial = this._editSerial;
        let entry = new TextEntry(this._editText, (text: string) => this._editText = text, { label: SdkStrings.treeItemName, role: 'bodyM', autofocus: true, selectOnFocus: true, onSubmit: () => this.commit(serial, true), onFocusChanged: (focused: boolean) => {
            if (!focused) this.commit(serial, false);
        } });
        let field: VisualNode = new Box(new BoxStyle({ width: SizeValue.fill, background: theme.surface, borderWidth: 1, borderColor: theme.colors('primary').base, cornerRadius: new CornerRadii(theme.shape('small')), padding: EdgeInsets.symmetric(4, 0) }), entry);
        return new Shortcut(field, KeyChord.escape, () => this.closeEdit(serial), { focusScoped: true });
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
        let field = !(parent == null) && this.isCreatingUnder(parent) ? 1 : 0;
        if (field === 1) {
            let $n0: TreeViewRow; 
            $eq.collections.listAdd(rows, ($n0 = new TreeViewRow(parent, null, parent, level, 1, $eq.collections.count(nodes) + 1), $n0.isField = true, $n0));
        }
        for (let i = 0; i < $eq.collections.count(nodes); i++) {
            let node = $eq.collections.item(nodes, i);
            $eq.collections.listAdd(rows, new TreeViewRow(node.key, node, parent, level, i + 1 + field, $eq.collections.count(nodes) + field));
            if (!this._open.has(node.key)) continue;
            let children = this.childrenOf(node);
            if (!(children == null)) {
                this.addLevel(rows, children, node.key, level + 1);
                continue;
            }
            if (!this._loading.has(node.key) && !this._failed.has(node.key)) this.startLoad(node);
            if (this.isCreatingUnder(node.key)) {
                let $n1: TreeViewRow; 
                $eq.collections.listAdd(rows, ($n1 = new TreeViewRow(node.key, null, node.key, level + 1, 1, 2), $n1.isField = true, $n1));
            }
            $eq.collections.listAdd(rows, new TreeViewRow(node.key, null, node.key, level + 1, 1, 1));
        }
    }

    isCreatingUnder(key: string) {
        return this._editCreates && this._editKey === key;
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
                if (this._stale.delete(node.key)) this.startLoad(node);
            });
        } catch {
            this.setState(() => {
                this._loading.delete(node.key);
                this._started.delete(node.key);
                $eq.collections.setAdd(this._failed, node.key);
                if (this._stale.delete(node.key)) this.startLoad(node);
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
        let node: any; 
        if (at < 0 || (node = this._rows[at].node) != null && this._busy.has(node.key)) return;
        this.press(this._rows[at], true);
    }

    adoptRequests() {
        let edit: any; 
        if ((edit = this.edit) != null && edit.version !== this._editSeen) {
            this._editSeen = edit.version;
            this.beginEdit(edit.key, edit.creates, edit.icon);
        }
        let reload: any; 
        if ((reload = this.reload) != null && reload.version !== this._reloadSeen) {
            this._reloadSeen = reload.version;
            for (const key of $eq.collections.sequenceWalk(reload.keys)) this.reloadChildren(key);
        }
    }

    beginEdit(key: string, creates: boolean, icon: any) {
        let node: any; 
        if ((creates ? this.onCreated : this.onRenamed) == null || !((node = this.findNode(key)) != null) || this._busy.has(key) || this.indexOfNode(key) < 0) return;
        if (creates) {
            if (node.isLeaf) return;
            this.toggle(node, true);
        }
        this._editKey = key;
        this._editCreates = creates;
        this._editIcon = icon;
        this._editText = creates ? '' : node.label;
        this._editSerial++;
        this._menuKey = null;
        this._activeKey = key;
        this._activeIsStatus = false;
        this._scrollToActive = true;
    }

    renameActive() {
        let at = this.activeIndex();
        let node: any; 
        if (at < 0 || !((node = this._rows[at].node) != null)) return;
        this.setState(() => this.beginEdit(node.key, false, null));
    }

    commit(serial: number, giveBack: boolean) {
        let key: any; 
        if (serial !== this._editSerial || !((key = this._editKey) != null)) return;
        let creates = this._editCreates;
        let name = $eq.text.trim(this._editText);
        let node = this.findNode(key);
        let handler = creates ? this.onCreated : this.onRenamed;
        let changed = creates ? key : this.parentOf(key);
        let runs = !(node == null) && !(handler == null) && name.length > 0 && (creates || name !== node.label);
        this.setState(() => {
            this.closeEditState(giveBack);
            if (runs) $eq.collections.setAdd(this._working, key);
        });
        if (runs) this.runEdit(handler!, node!, name, key, changed);
    }

    closeEdit(serial: number) {
        if (serial !== this._editSerial || this._editKey == null) return;
        this.setState(() => this.closeEditState(true));
    }

    closeEditState(giveBack: boolean) {
        this._editKey = null;
        this._editSerial++;
        if (!giveBack) return;
        this._focusVersion++;
        this._keyboard = true;
    }

    async runEdit(handler: (arg1: TreeNode, arg2: string) => void, node: TreeNode, name: string, busyKey: string, changedKey: any) {
        let done = false;
        try {
            await handler(node, name);
            done = true;
        } catch {
            done = false;
        }
        this.setState(() => {
            this._working.delete(busyKey);
            if (done && !(changedKey == null)) this.reloadChildren(changedKey);
        });
    }

    reloadChildren(key: string) {
        let node: any; 
        if (!this._loaded.has(key) || !((node = this.findNode(key)) != null) || !(node.children == null)) return;
        if (this._started.has(key)) {
            $eq.collections.setAdd(this._stale, key);
            return;
        }
        this.startLoad(node);
    }

    parentOf(key: string) {
        let at = this.indexOfNode(key);
        return at >= 0 ? this._rows[at].parentKey : null;
    }

    static dropColor(context: BuildContext, slot: any) {
        return slot?.treeDropColor ?? context.theme.focusRing;
    }

    dropLine(context: BuildContext, slot: any, level: number, surface: VisualNode, after: boolean) {
        let start = Math.fround((slot?.treePaddingX ?? 8) + Math.fround(Math.fround(level - 1) * (slot?.treeIndent ?? 16)));
        let line = new Box(new BoxStyle({ width: SizeValue.fill, height: SizeValue.fixed(2), background: TreeView.dropColor(context, slot) }));
        let stack = new Stack('topStart', { width: SizeValue.fill, height: SizeValue.fixed(this._rowHeight) });
        stack.add(surface);
        stack.add(after ? new Positioned(line, null, 0, 0, start) : new Positioned(line, 0, 0, null, start));
        return stack;
    }

    othersThan(carried: string) {
        let others: TreeViewRow[] = [];
        let carries = 0;
        for (const row of $eq.collections.listWalk(this._rows)) {
            if (carries > 0 && row.level > carries) continue;
            carries = 0;
            if (row.node == null) continue;
            if (row.key === carried) {
                if (this._indent > 0) carries = row.level;
                continue;
            }
            $eq.collections.listAdd(others, row);
        }
        return others;
    }

    parentAtDepth(others: TreeViewRow[], index: number, depth: number) {
        if (depth === 0 || index === 0) return null;
        let key = others[index - 1].key;
        while (this.levelOf(key) > depth) {
            let parent: any; 
            if (!((parent = this.parentOf(key)) != null)) return null;
            key = parent;
        }
        return key;
    }

    levelOf(key: string) {
        let at = this.indexOfNode(key);
        return at >= 0 ? this._rows[at].level : 0;
    }

    nodeOf(key: any) {
        if (key == null) return null;
        let at = this.indexOfNode(key);
        return at >= 0 ? this._rows[at].node : null;
    }

    siblingBefore(rows: TreeViewRow[], index: number, parent: any) {
        for (let i = index - 1; i >= 0; i--) {
            let node: any; 
            if (!((node = $eq.collections.item(rows, i).node) != null)) continue;
            if (node.key === parent) return null;
            if ($eq.collections.item(rows, i).parentKey === parent) return node;
        }
        return null;
    }

    static placeOf(drop: SortDrop, others: TreeViewRow[]): [number, number, number] {
        let into: any; 
        if ((into = drop.into) != null) {
            for (let i = 0; i < others.length; i++) if (others[i].key === into) return [TreeView.dropInto, i, others[i].level + 1];
            return [0, -1, 0];
        }
        if (drop.index < others.length) {
            let depth: any; 
            return [TreeView.dropBefore, drop.index, (depth = drop.depth) != null ? depth + 1 : others[drop.index].level];
        }
        if (others.length === 0) return [0, -1, 0];
        let last = others.length - 1;
        let after: any; 
        return [TreeView.dropAfter, last, (after = drop.depth) != null ? after + 1 : others[last].level];
    }

    moveOf(drop: SortDrop) {
        let node: any; 
        if (!((node = this.nodeOf(drop.item)) != null)) return null;
        let others = this.othersThan(drop.item);
        let [kind, at, ] = TreeView.placeOf(drop, others);
        if (kind === TreeView.dropInto) return new TreeMove(node, others[at].node, null);
        if (kind === 0) return drop.into == null ? new TreeMove(node, null, null) : null;
        let index = kind === TreeView.dropBefore ? at : others.length;
        let depth: any; 
        let parent = (depth = drop.depth) != null ? this.parentAtDepth(others, index, depth) : others[at].parentKey;
        return new TreeMove(node, this.nodeOf(parent), this.siblingBefore(others, index, parent));
    }

    accepts(drop: SortDrop) {
        let move: any; 
        if (!((move = this.moveOf(drop)) != null) || (($0) => ($0 != null && $0.isLeaf === true))(move.parent)) return false;
        for (let at = (move.parent?.key ?? null); !(at == null); at = this.parentOf(at)) if (at === move.node.key) return false;
        return this.canMove?.(move) ?? true;
    }

    hovered(drop: any) {
        let key: string | null = null;
        let kind = 0;
        let level = 0;
        if (!(drop == null)) {
            let others = this.othersThan(drop.item);
            let [placed, at, deep] = TreeView.placeOf(drop, others);
            kind = placed;
            key = at >= 0 ? others[at].key : null;
            level = deep;
        }
        let folder: any; 
        let pause = kind === TreeView.dropInto && (folder = this.nodeOf(key)) != null && !this._open.has(folder.key) && this.hasChevron(folder) ? key : null;
        this.setState(() => {
            this._dropKey = key;
            this._dropKind = kind;
            this._dropLevel = level;
        });
        if (pause === this._pauseKey) return;
        this._pauseKey = pause;
        this._pauseSerial++;
        if (!(pause == null)) this.openAfterPause(pause, this._pauseSerial);
    }

    async openAfterPause(key: string, serial: number) {
        await $eq.task.delay(TreeView.openUnderPauseMs);
        let folder: any; 
        if (serial !== this._pauseSerial || this._pauseKey !== key || !((folder = this.nodeOf(key)) != null)) return;
        this.setState(() => {
            this.toggle(folder, true);
            this._pauseKey = null;
        });
        this.onToggled?.(folder, true);
    }

    dropped(drop: SortDrop) {
        let move = this.moveOf(drop);
        this._pauseKey = null;
        this._pauseSerial++;
        this.setState(() => {
            this._dropKey = null;
            this._dropKind = 0;
            this._dropLevel = 0;
        });
        let handler: any; 
        if (move == null || !((handler = this.onMoved) != null)) return;
        let from = this.parentOf(move.node.key);
        let at = this.indexOfNode(move.node.key);
        if (from === (move.parent?.key ?? null) && at >= 0 && (this.siblingBefore(this._rows, at, from)?.key ?? null) === (move.after?.key ?? null)) return;
        this.runMove(handler, move, from);
    }

    async runMove(handler: (value: TreeMove) => void, move: TreeMove, from: any) {
        let key = move.node.key;
        let to = move.parent;
        let opens = !(to == null) && !this._open.has(to.key);
        this.setState(() => {
            $eq.collections.setAdd(this._working, key);
            if (opens) this.toggle(to!, true);
        });
        if (opens) this.onToggled?.(to!, true);
        let done = false;
        try {
            await handler(move);
            done = true;
        } catch {
            done = false;
        }
        this.setState(() => {
            this._working.delete(key);
            if (!done) return;
            if (!(from == null)) this.reloadChildren(from);
            if (!(to == null) && to.key !== from) this.reloadChildren(to.key);
        });
    }

    requestMenu(node: TreeNode, request: ContextRequest) {
        let builder: any; 
        if (!((builder = this.menuBuilder) != null) || this._busy.has(node.key) || !(this._editKey == null)) return;
        let items = builder(node);
        this.setState(() => {
            this._activeKey = node.key;
            this._activeIsStatus = false;
            this._keyboard = request.at == null;
            this._menuKey = $eq.collections.count(items) > 0 ? node.key : null;
            this._menuAt = request.at;
            this._menuItems = items;
            this._menuHighlight = MenuPanel.firstEnabled(items);
        });
    }

    chooseMenu(index: number) {
        let key: any; 
        if (!((key = this._menuKey) != null) || index < 0 || index >= $eq.collections.count(this._menuItems) || $eq.collections.item(this._menuItems, index).disabled) return;
        let node = this.findNode(key);
        this.setState(() => this._menuKey = null);
        if (!(node == null)) this.onMenuSelect?.(node, index);
    }

    closeMenu() {
        return this.setState(() => this._menuKey = null);
    }

    move(move: $NavigableMoveValue) {
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

