import { $AdjustableRoleValue, $AlignmentValue, $CrossAlignValue, $DragAxisValue, $eq, $HitTestBehaviorValue, $IconsValue, $ImageFitValue, $LiveRegionUrgencyValue, $LoopEffectValue, $MainAlignValue, $NavigableMoveValue, $NavigableRoleValue, $PresenceMotionValue, $PressableRoleValue, $ScrollAxisValue, $SizeVariantValue, $TableCellRoleValue, $TableRoleValue, $TextAlignmentValue, $TextDirectionValue, $TextEntryKindValue, $TypeRoleValue, $VariantValue, AdaptiveNode, Adjustable, Anchored, AppBar, Avatar, Badge, Banner, BottomNavigation, Box, BoxConstraints, BoxStyle, Button, Calendar, CalendarTheme, CameraPreview, Canvas, CanvasPointer, Card, Checkbox, Chip, CodeSurface, ColorToken, Column, ConstrainedBox, ContextMenu, ContextRequest, ContextTarget, CookieConsent, CornerRadii, CultureOption, CultureSwitcher, Currency, DateOnly, DatePicker, DateTime, DateTimePicker, Dialog, DialogAction, Directionality, Divider, DragDismiss, Draggable, Drawer, Drawing, DropTarget, EdgeInsets, EmptyState, ExcludeSemantics, FileUpload, Flexible, GestureRecognizerFactory, Grid, GridTrack, Group, Hoverable, Icon, IconButton, IconGlyph, Image, InFlow, InputTheme, InView, KeyChord, LayoutBuilder, Link, ListDetail, Listener, ListItem, ListTheme, ListView, LiveRegion, LoopMotion, Markdown, MenuItem, MergeSemantics, Mermaid, MoneyInput, Navigable, NavigationRail, NavItem, NumberInput, OptionGroup, Overlay, PickedFile, Pinned, PointerEvent, Positioned, Presence, Pressable, Progress, ProgressBar, RangeValue, RawGestureDetector, Row, SafeArea, ScrollView, SdkStrings, SearchField, SegmentedControl, SegmentedTheme, Select, SheetSurface, Shortcut, Simulated, Size, SizeValue, Skeleton, Slider, Sortable, SortableItem, SortableTheme, SortAnnouncements, SortDrop, Spacer, Spinner, Stack, Stepper, Switch, TableOffset, TableRange, TableScroll, TableScrollRequest, TableSpans, TableView, TableViewCell, TableViewport, Tabs, Text, TextEntry, TextInput, TimeOnly, TimePicker, Toast, Tooltip, TreeEditRequest, TreeMove, TreeNode, TreeReloadRequest, TreeRowState, TreeView, TypeStyle, UploadedFile, UploadPolicy, Vector, VectorDrawing, VisualNode, WebContent, WebFrame } from "../runtime-exports";

export class UI {
    static column(gap: number = 0, main: $MainAlignValue = 'start', cross: $CrossAlignValue = 'stretch', wrap: boolean = false, runGap: number | null = null, padding: EdgeInsets | null = null, width?: SizeValue, height?: SizeValue, children: VisualNode[] | null = null) {
        let node = new Column(gap, main, cross, wrap, runGap, padding, { width: width, height: height });
        if (children != null) for (const child of children) node.add(child);
        return node;
    }

    static inView(child: VisualNode, onChanged: (bool: boolean) => void) {
        return new InView(child, onChanged);
    }

    static inFlow(child: VisualNode) {
        return new InFlow(child);
    }

    static directionality(direction: $TextDirectionValue, child: VisualNode) {
        return new Directionality(direction, child);
    }

    static simulated(state: number, child: VisualNode) {
        return new Simulated(state, child);
    }

    static row(gap: number = 0, main: $MainAlignValue = 'start', cross: $CrossAlignValue = 'center', wrap: boolean = false, runGap: number | null = null, padding: EdgeInsets | null = null, width?: SizeValue, height?: SizeValue, children: VisualNode[] | null = null) {
        let node = new Row(gap, main, cross, wrap, runGap, padding, { width: width, height: height });
        if (children != null) for (const child of children) node.add(child);
        return node;
    }

    static grid(columns: GridTrack[], gap: number = 0, rowGap: number | null = null, width?: SizeValue, height?: SizeValue, children: VisualNode[] | null = null) {
        let node = new Grid(columns, gap, rowGap, { width: width, height: height });
        if (children != null) for (const child of children) node.add(child);
        return node;
    }

    static canvas(draw: (iCanvasPainter: any) => void, width?: SizeValue, height?: SizeValue, onPointerDown: ((canvasPointer: CanvasPointer) => void) | null = null, onPointerMove: ((canvasPointer: CanvasPointer) => void) | null = null, onPointerUp: ((canvasPointer: CanvasPointer) => void) | null = null, onPointerLeave: (() => void) | null = null, label: string | null = null) {
        return new Canvas(draw, width, height, { onPointerDown: onPointerDown, onPointerMove: onPointerMove, onPointerUp: onPointerUp, onPointerLeave: onPointerLeave, label: label });
    }

    static stack(align: $AlignmentValue = 'topStart', width?: SizeValue, height?: SizeValue, children: VisualNode[] | null = null) {
        let node = new Stack(align, { width: width, height: height });
        if (children != null) for (const child of children) node.add(child);
        return node;
    }

    static box(style?: BoxStyle, child: VisualNode | null = null) {
        return new Box(style, child);
    }

    static constrainedBox(constraints: BoxConstraints, child: VisualNode) {
        return new ConstrainedBox(constraints, child);
    }

    static text(content: string, role: $TypeRoleValue | null = null, color: ColorToken | null = null, maxLines: number | null = null, align: $TextAlignmentValue | null = null, mono: boolean = false, tabular: boolean = false, styleOverride: TypeStyle | null = null, headingLevel: number = 0) {
        return new Text(content, role, color, maxLines, align, mono, tabular, styleOverride, headingLevel);
    }

    static textEntry(value: string, onChanged: ((string: string) => void) | null = null, label: string | null = null, placeholder: string | null = null, disabled: boolean = false, obscure: boolean = false) {
        return new TextEntry(value, onChanged, { label: label, placeholder: placeholder, disabled: disabled, obscure: obscure });
    }

    static pressable(child: VisualNode, onPressed: (() => void) | null = null, label: string | null = null, selected: boolean | null = null, disabled: boolean = false, expanded: boolean | null = null, role: $PressableRoleValue = 'button', canRequestFocus: boolean = true) {
        return new Pressable(child, onPressed, { label: label, selected: selected, disabled: disabled, expanded: expanded, role: role, canRequestFocus: canRequestFocus });
    }

    static link(destination: string, child: VisualNode, label: string | null = null, current: boolean = false, opensBeside: boolean = false) {
        return new Link(destination, child, { label: label, current: current, opensBeside: opensBeside });
    }

    static icon(glyph: IconGlyph, size: number | null = null, color: ColorToken | null = null, label: string | null = null) {
        return new Icon(glyph, size, color, label);
    }

    static vector(glyph: IconGlyph, size: number, color: ColorToken | null = null, label: string | null = null, height: number = 0) {
        return new Vector(glyph, size, color, label, height);
    }

    static drawing(artwork: VectorDrawing, width: SizeValue, height: number = 0, tint: ColorToken | null = null, label: string | null = null) {
        return new Drawing(artwork, width, height, tint, label);
    }

    static image(source: string, width: number, height: number, fit: $ImageFitValue = 'cover', label: string = '') {
        return new Image(source, width, height, fit, label);
    }

    static spinner(size: number = 20, color: ColorToken | null = null) {
        return new Spinner(size, color);
    }

    static flexible(child: VisualNode, flex: number = 1, basis: number = 0, shrink: number = 1) {
        return new Flexible(child, flex, basis, shrink);
    }

    static spacer(flex: number = 1) {
        return new Spacer(flex);
    }

    static gap(dp: number) {
        return Spacer.fixed(dp);
    }

    static positioned(child: VisualNode, top: number | null = null, end: number | null = null, bottom: number | null = null, start: number | null = null, topFraction: number | null = null, endFraction: number | null = null, bottomFraction: number | null = null, startFraction: number | null = null, shiftX: number = 0, shiftY: number = 0) {
        return new Positioned(child, top, end, bottom, start, { topFraction: topFraction, endFraction: endFraction, bottomFraction: bottomFraction, startFraction: startFraction, shiftX: shiftX, shiftY: shiftY });
    }

    static scrollView(child: VisualNode, axis: $ScrollAxisValue = 'vertical', width?: SizeValue, height?: SizeValue) {
        return new ScrollView(child, axis, { width: width, height: height });
    }

    static tableViewport(rows: TableSpans, columns: TableSpans, scroll: any, pinnedRows: number = 0, pinnedColumns: number = 0, width?: SizeValue, height?: SizeValue, request: TableScrollRequest | null = null, onScrolled: ((tableOffset: TableOffset) => void) | null = null, onViewportChanged: ((size: Size) => void) | null = null, label: string | null = null, canRequestFocus: boolean = true, role: $TableRoleValue = 'group', children: VisualNode[] | null = null) {
        let node = new TableViewport(rows, columns, scroll, { pinnedRows: pinnedRows, pinnedColumns: pinnedColumns, width: width, height: height, request: request, onScrolled: onScrolled, onViewportChanged: onViewportChanged, label: label, canRequestFocus: canRequestFocus, role: role });
        if (children != null) for (const child of children) node.add((($as: any) => $as instanceof TableViewCell ? $as : null)(child) ?? $eq.exceptions.raise($eq.exceptions.create(['System.ArgumentException', 'System.SystemException', 'System.Exception'], 'A table viewport places TableViewCells, and nothing else.', { paramName: 'children' })));
        return node;
    }

    static tableViewCell(row: number, column: number, child: VisualNode, mark: boolean = false, rows: number = 1, columns: number = 1, role: $TableCellRoleValue = 'cell', selected: boolean = false, current: boolean = false) {
        return new TableViewCell(row, column, child, mark, { rows: rows, columns: columns, role: role, selected: selected, current: current });
    }

    static safeArea(child: VisualNode, edges: number = 15) {
        return new SafeArea(child, edges);
    }

    static pinned(child: VisualNode, offset: number = 0) {
        return new Pinned(child, offset);
    }

    static overlay(child: VisualNode) {
        return new Overlay(child);
    }

    static anchored(anchor: VisualNode, panel: VisualNode) {
        return new Anchored(anchor, panel);
    }

    static hoverable(child: VisualNode, onChanged: (bool: boolean) => void) {
        return new Hoverable(child, onChanged);
    }

    static presence(child: VisualNode, enter: $PresenceMotionValue = 'fade') {
        return new Presence(child, enter);
    }

    static draggable(child: VisualNode, onReleased: ((float: number) => void) | null = null) {
        return new Draggable(child, onReleased);
    }

    static dragDismiss(child: VisualNode, onDismiss: (() => void) | null = null) {
        return new DragDismiss(child, onDismiss);
    }

    static progress(child: VisualNode, label: string = '', value: RangeValue | null = null, valueText: string | null = null) {
        return new Progress(child, { label: label, value: value, valueText: valueText });
    }

    static liveRegion(child: VisualNode, urgency: $LiveRegionUrgencyValue = 'polite', label: string = '') {
        return new LiveRegion(child, { urgency: urgency, label: label });
    }

    static group(child: VisualNode, label: string = '') {
        return new Group(child, { label: label });
    }

    static mergeSemantics(child: VisualNode) {
        return new MergeSemantics(child);
    }

    static excludeSemantics(child: VisualNode, excluding: boolean = true) {
        return new ExcludeSemantics(child, { excluding: excluding });
    }

    static adjustable(child: VisualNode, onAdjust: (int: number) => void, value: RangeValue | null = null, role: $AdjustableRoleValue = 'slider', valueText: string | null = null, axis: $DragAxisValue = 'horizontal') {
        return new Adjustable(child, onAdjust, { value: value, role: role, valueText: valueText, axis: axis });
    }

    static navigable(onMove: (navigableMove: $NavigableMoveValue) => void, rows: VisualNode[], label: string = '', role: $NavigableRoleValue = 'grid', hasHeaderRow: boolean = false, activeCell: [number, number] | null = null) {
        return new Navigable(onMove, rows, { label: label, role: role, hasHeaderRow: hasHeaderRow, activeCell: activeCell });
    }

    static shortcut(child: VisualNode, chord: KeyChord, onPressed: () => void) {
        return new Shortcut(child, chord, onPressed);
    }

    static adaptiveNode(compact: VisualNode, medium: VisualNode | null = null, expanded: VisualNode | null = null) {
        return new AdaptiveNode(compact, medium, expanded);
    }

    static layoutBuilder(builder: (value: BoxConstraints) => VisualNode) {
        return new LayoutBuilder(builder);
    }

    static cameraPreview(session: any, width: number, height: number, cornerRadius?: CornerRadii, label: string = '') {
        return new CameraPreview(session, width, height, { cornerRadius: cornerRadius, label: label });
    }

    static loopMotion(child: VisualNode, effect: $LoopEffectValue, fromX: number, toX: number, durationMs: number, hideAtRest: boolean = false) {
        return new LoopMotion(child, effect, fromX, toX, durationMs, { hideAtRest: hideAtRest });
    }

    static codeSurface(child: VisualNode, model: any, onChanged: (() => void) | null = null, label: string | null = null, autofocus: boolean = false) {
        return new CodeSurface(child, model, { onChanged: onChanged, label: label, autofocus: autofocus });
    }

    static sheetSurface(child: VisualNode, model: any, onChanged: (() => void) | null = null, label: string | null = null) {
        return new SheetSurface(child, model, { onChanged: onChanged, label: label });
    }

    static webFrame(content: WebContent, title: string, sandbox: number = 1, cornerRadius?: CornerRadii) {
        return new WebFrame(content, title, { sandbox: sandbox, cornerRadius: cornerRadius });
    }

    static button(label: string, variant: $VariantValue = 'primary', size: $SizeVariantValue = 'medium', onPressed: (() => void) | null = null) {
        return new Button(label, variant, size, onPressed);
    }

    static cookieConsent(policyHref: string | null = null) {
        return new CookieConsent(policyHref);
    }

    static card(child: VisualNode, kind: string = 'elevated') {
        return new Card(child, kind);
    }

    static chip(label: string, kind: string = 'filter', selected: boolean = false, onPressed: (() => void) | null = null, onRemove: (() => void) | null = null) {
        return new Chip(label, kind, selected, onPressed, onRemove);
    }

    static badge(count: number = 0, max: number = 99, variant: $VariantValue = 'destructive') {
        return new Badge(count, max, variant);
    }

    static dotBadge(variant: $VariantValue = 'destructive') {
        return Badge.asDot(variant);
    }

    static avatar(initials: string, size: $SizeVariantValue = 'medium', name: string | null = null) {
        return new Avatar(initials, size, name);
    }

    static banner(status: $VariantValue, title: string, body: string | null = null) {
        return new Banner(status, title, body);
    }

    static checkbox(checked: boolean, onChanged: (() => void) | null = null, label: string | null = null, disabled: boolean = false) {
        return new Checkbox(checked, onChanged, label, { disabled: disabled });
    }

    static switch(on: boolean, onChanged: (() => void) | null = null, label: string | null = null, disabled: boolean = false) {
        return new Switch(on, onChanged, { label: label, disabled: disabled });
    }

    static slider(value: number, onChanged: ((float: number) => void) | null = null, min: number = 0, max: number = 1, step: number = 0, label: string = '', valueText: string | null = null, disabled: boolean = false, variant: $VariantValue = 'primary') {
        return new Slider(value, onChanged, { min: min, max: max, step: step, label: label, valueText: valueText, disabled: disabled, variant: variant });
    }

    static stepper(value: number, onChanged: ((int: number) => void) | null = null, style: SegmentedTheme | null = null) {
        return new Stepper(value, onChanged, style);
    }

    static moneyInput(amount: bigint | null, onChanged: ((long: bigint | null) => void) | null, label: string, currency: Currency, min: bigint | null = null, max: bigint | null = null, placeholder: string | null = null, helper: string | null = null, error: string | null = null, size: $SizeVariantValue = 'large') {
        return new MoneyInput(amount, onChanged, label, currency, min, max, placeholder, helper, error, size);
    }

    static numberInput(value: number | null, onChanged: ((int: number | null) => void) | null, label: string, unit: string = '', min: number | null = null, max: number | null = null, placeholder: string | null = null, helper: string | null = null, error: string | null = null, size: $SizeVariantValue = 'large') {
        return new NumberInput(value, onChanged, label, unit, min, max, placeholder, helper, error, size);
    }

    static datePicker(selected: DateOnly | null = null, onChanged: ((dateOnly: DateOnly) => void) | null = null, min: DateOnly | null = null, max: DateOnly | null = null, label: string = '', style: InputTheme | null = null, calendarStyle: CalendarTheme | null = null, labelHidden: boolean = false) {
        return new DatePicker(selected, onChanged, min, max, label, style, calendarStyle, { labelHidden: labelHidden });
    }

    static timePicker(selected: TimeOnly | null = null, onChanged: ((timeOnly: TimeOnly) => void) | null = null, stepMinutes: number = 30, min: TimeOnly | null = null, max: TimeOnly | null = null, label: string = '', style: InputTheme | null = null) {
        return new TimePicker(selected, onChanged, stepMinutes, min, max, label, style);
    }

    static dateTimePicker(selected: DateTime | null = null, onChanged: ((dateTime: DateTime) => void) | null = null, min: DateTime | null = null, max: DateTime | null = null, stepMinutes: number = 30, dateLabel: string = '', timeLabel: string = '', style: InputTheme | null = null, calendarStyle: CalendarTheme | null = null, labelHidden: boolean = false) {
        return new DateTimePicker(selected, onChanged, min, max, stepMinutes, dateLabel, timeLabel, style, calendarStyle, { labelHidden: labelHidden });
    }

    static calendar(selected: DateOnly | null = null, onChanged: ((dateOnly: DateOnly) => void) | null = null, min: DateOnly | null = null, max: DateOnly | null = null, style: CalendarTheme | null = null) {
        return new Calendar(selected, onChanged, min, max, style);
    }

    static select(options: string[] | null = null, selectedIndex: number = -1, onChanged: ((int: number) => void) | null = null, placeholder: string | null = null, groups: OptionGroup[] | null = null, style: InputTheme | null = null, optionBuilder: ((value: number) => VisualNode) | null = null, label: string = '') {
        return new Select(options, selectedIndex, onChanged, placeholder, groups, style, optionBuilder, label);
    }

    static textInput(value: string, onChanged: ((string: string) => void) | null = null, label: string = '', placeholder: string | null = null, helper: string | null = null, error: string | null = null, leading: $IconsValue | null = null, size: $SizeVariantValue = 'large', trailing: VisualNode | null = null, kind: $TextEntryKindValue = 'text', prefix: string | null = null, suffix: string | null = null, style: InputTheme | null = null, labelHidden: boolean = false) {
        return new TextInput(value, onChanged, label, placeholder, helper, error, leading, size, trailing, kind, prefix, suffix, style, { labelHidden: labelHidden });
    }

    static searchField(query: string, onChanged: ((string: string) => void) | null = null, placeholder: string | null = null, onSubmit: (() => void) | null = null, style: InputTheme | null = null) {
        return new SearchField(query, onChanged, placeholder, onSubmit, style);
    }

    static progressBar(value: number | null = null, variant: $VariantValue = 'primary', label: string = '', valueText: string | null = null, prominent: boolean = false) {
        return new ProgressBar(value, variant, { label: label, valueText: valueText, prominent: prominent });
    }

    static divider(inset: string = 'none', axis: string = 'horizontal') {
        return new Divider(inset, axis);
    }

    static markdown(source: string) {
        return new Markdown(source);
    }

    static mermaid(source: string) {
        return new Mermaid(source);
    }

    static cultureSwitcher(options: CultureOption[], style: SegmentedTheme | null = null) {
        return new CultureSwitcher(options, style);
    }

    static iconButton(glyph: Icon, label: string, kind: string = 'standard', size: $SizeVariantValue = 'medium', onPressed: (() => void) | null = null) {
        return new IconButton(glyph, label, kind, size, onPressed);
    }

    static emptyState(icon: Icon, title: string, body: string | null = null) {
        return new EmptyState(icon, title, body);
    }

    static segmentedControl(segments: string[], selectedIndex: number, onChanged: ((int: number) => void) | null = null, style: SegmentedTheme | null = null, segmentBuilder: ((value: number) => VisualNode) | null = null, stretch: boolean = true) {
        return new SegmentedControl(segments, selectedIndex, onChanged, style, segmentBuilder, { stretch: stretch });
    }

    static tabs(labels: string[], selected: number, onSelect: ((int: number) => void) | null = null) {
        return new Tabs(labels, selected, onSelect);
    }

    static bottomNavigation(items: NavItem[], selected: number, onSelect: ((int: number) => void) | null = null) {
        return new BottomNavigation(items, selected, onSelect);
    }

    static navigationRail(items: NavItem[], selected: number, onSelect: ((int: number) => void) | null = null) {
        return new NavigationRail(items, selected, onSelect);
    }

    static drawer(content: VisualNode, open: boolean, onDismiss: (() => void) | null = null) {
        return new Drawer(content, open, onDismiss);
    }

    static appBar(title: string) {
        return new AppBar(title);
    }

    static listItem(title: string, subtitle: string | null = null, onPressed: (() => void) | null = null) {
        return new ListItem(title, subtitle, onPressed);
    }

    static listView(count: number, itemExtent: number, itemBuilder: (value: number) => VisualNode, width?: SizeValue, height?: SizeValue) {
        return new ListView(count, itemExtent, itemBuilder, { width: width, height: height });
    }

    static treeView(roots: TreeNode[], selectedKey: string | null = null, onPressed: ((treeNode: TreeNode) => void) | null = null, style: ListTheme | null = null, loadChildren: ((value: TreeNode) => TreeNode[]) | null = null, onToggled: ((arg1: TreeNode, arg2: boolean) => void) | null = null, expandedKeys: string[] | null = null, revealPath: string[] | null = null, label: string = '', height: SizeValue | null = null, rowBuilder: ((value: TreeRowState) => VisualNode) | null = null, onRenamed: ((arg1: TreeNode, arg2: string) => void) | null = null, onCreated: ((arg1: TreeNode, arg2: string) => void) | null = null, edit: TreeEditRequest | null = null, reload: TreeReloadRequest | null = null, busyKeys: string[] | null = null, menuBuilder: ((value: TreeNode) => MenuItem[]) | null = null, onMenuSelect: ((arg1: TreeNode, arg2: number) => void) | null = null, onMoved: ((value: TreeMove) => void) | null = null, canMove: ((value: TreeMove) => boolean) | null = null) {
        return new TreeView(roots, selectedKey, onPressed, style, { loadChildren: loadChildren, onToggled: onToggled, expandedKeys: expandedKeys, revealPath: revealPath, label: label, height: height, rowBuilder: rowBuilder, onRenamed: onRenamed, onCreated: onCreated, edit: edit, reload: reload, busyKeys: busyKeys, menuBuilder: menuBuilder, onMenuSelect: onMenuSelect, onMoved: onMoved, canMove: canMove });
    }

    static listDetail(list: () => VisualNode, detail: (() => VisualNode) | null = null, onBack: (() => void) | null = null) {
        return new ListDetail(list, detail, onBack);
    }

    static dialog(title: string, body: string, actions: DialogAction[], dismissible: boolean = false, onDismiss: (() => void) | null = null) {
        return new Dialog(title, body, actions, dismissible, onDismiss);
    }

    static toast(message: string, status: $VariantValue = 'info', actionLabel: string | null = null, onAction: (() => void) | null = null) {
        return new Toast(message, status, actionLabel, onAction);
    }

    static tooltip(child: VisualNode, text: string) {
        return new Tooltip(child, text);
    }

    static skeleton(shape: string, width: number, height: number = 0) {
        return new Skeleton(shape, width, height);
    }

    static tableView(rows: TableSpans, columns: TableSpans, cellBuilder: (arg1: number, arg2: number) => VisualNode | null, pinnedRows: number = 0, pinnedColumns: number = 0, overscan: number = 3, width?: SizeValue, height?: SizeValue, marks: TableViewCell[] | null = null, request: TableScrollRequest | null = null, onScrolled: ((tableOffset: TableOffset) => void) | null = null, onViewportChanged: ((size: Size) => void) | null = null, label: string | null = null, canRequestFocus: boolean = true, scroll: TableScroll | null = null, role: $TableRoleValue = 'group', cellRole: ((arg1: number, arg2: number) => $TableCellRoleValue) | null = null, selected: ((arg1: number, arg2: number) => boolean) | null = null, current: TableRange | null = null) {
        return new TableView(rows, columns, cellBuilder, { pinnedRows: pinnedRows, pinnedColumns: pinnedColumns, overscan: overscan, width: width, height: height, marks: marks, request: request, onScrolled: onScrolled, onViewportChanged: onViewportChanged, label: label, canRequestFocus: canRequestFocus, scroll: scroll, role: role, cellRole: cellRole, selected: selected, current: current });
    }

    static sortable(child: VisualNode, id: string, onDropped: (sortDrop: SortDrop) => void, announcements: SortAnnouncements | null = null, style: SortableTheme | null = null, group: string = '', axis: $DragAxisValue = 'vertical', nests: boolean = false, onHovered: ((sortDrop: SortDrop | null) => void) | null = null, accepts: ((value: SortDrop) => boolean) | null = null, enabled: boolean = true, label: string = '', indent: number = 0) {
        return new Sortable(child, id, onDropped, announcements ?? SdkStrings.sorting, style, { label: label, indent: indent, group: group, axis: axis, nests: nests, onHovered: onHovered, accepts: accepts, enabled: enabled });
    }

    static sortableItem(child: VisualNode, id: string, enabled: boolean = true, name: string = '', depth: number = 0) {
        return new SortableItem(child, id, { enabled: enabled, name: name, depth: depth });
    }

    static dropTarget(child: VisualNode, onDropped: (value: PickedFile[]) => void, onDragOverChanged: ((bool: boolean) => void) | null = null, enabled: boolean = true) {
        return new DropTarget(child, onDropped, { onDragOverChanged: onDragOverChanged, enabled: enabled });
    }

    static contextTarget(child: VisualNode, onRequested: (contextRequest: ContextRequest) => void, enabled: boolean = true) {
        return new ContextTarget(child, onRequested, { enabled: enabled });
    }

    static listener(child: VisualNode, onPointerDown: ((pointerEvent: PointerEvent) => void) | null = null, onPointerMove: ((pointerEvent: PointerEvent) => void) | null = null, onPointerUp: ((pointerEvent: PointerEvent) => void) | null = null, onPointerCancel: ((pointerEvent: PointerEvent) => void) | null = null, onPointerHover: ((pointerEvent: PointerEvent) => void) | null = null, onPointerScroll: ((pointerEvent: PointerEvent) => void) | null = null, behavior: $HitTestBehaviorValue = 'deferToChild') {
        return new Listener(child, { onPointerDown: onPointerDown, onPointerMove: onPointerMove, onPointerUp: onPointerUp, onPointerCancel: onPointerCancel, onPointerHover: onPointerHover, onPointerScroll: onPointerScroll, behavior: behavior });
    }

    static rawGestureDetector(child: VisualNode, gestures: GestureRecognizerFactory[], behavior: $HitTestBehaviorValue = 'deferToChild') {
        return Object.assign(new RawGestureDetector(child, gestures), { behavior: behavior });
    }

    static contextMenu(child: VisualNode, items: MenuItem[], onSelect: ((int: number) => void) | null = null, enabled: boolean = true) {
        return new ContextMenu(child, items, onSelect, { enabled: enabled });
    }

    static fileUpload(policy: UploadPolicy, onCompleted: (uploadedFile: UploadedFile) => void, multiple: boolean = true, label: string | null = null, hint: string | null = null, style: InputTheme | null = null) {
        return new FileUpload(policy, onCompleted, multiple, label, hint, style);
    }
}

