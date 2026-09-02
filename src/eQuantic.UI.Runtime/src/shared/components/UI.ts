import { AdaptiveNode, Adjustable, AlignmentValue, Anchored, AppBar, Avatar, Badge, Banner, BottomNavigation, Box, BoxStyle, Button, Calendar, Canvas, CanvasPointer, Card, Checkbox, Chip, ColorToken, Column, CookieConsent, CrossAlignValue, CultureOption, CultureSwitcher, DateOnly, DatePicker, DateTime, DateTimePicker, Dialog, DialogAction, Divider, DragDismiss, Draggable, Drawer, Drawing, EdgeInsets, EmptyState, Flexible, Grid, GridTrack, Hoverable, Icon, IconButton, IconGlyph, IconsValue, Image, ImageFitValue, InFlow, InView, KeyChord, Link, ListDetail, ListItem, ListView, MainAlignValue, Markdown, Mermaid, NavigationRail, NavItem, Overlay, Positioned, Presence, PresenceMotionValue, Pressable, PressableRoleValue, ProgressBar, Row, SafeArea, ScrollAxisValue, ScrollView, SearchField, SegmentedControl, Select, Shortcut, Simulated, SizeValue, SizeVariantValue, Skeleton, Slider, Spacer, Spinner, Stack, Stepper, Sticky, Switch, Tabs, Text, TextAlignmentValue, TextEntry, TextInput, TimeOnly, TimePicker, Toast, Tooltip, TypeRoleValue, TypeStyle, VariantValue, Vector, VectorDrawing, VisualNode } from "../runtime-exports";

export class UI {
    static column(gap: number = 0, main: MainAlignValue = 'start', cross: CrossAlignValue = 'stretch', wrap: boolean = false, runGap: number | null = null, padding: EdgeInsets | null = null, width?: SizeValue, height?: SizeValue, children: VisualNode[] | null = null) {
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

    static simulated(state: number, child: VisualNode) {
        return new Simulated(state, child);
    }

    static row(gap: number = 0, main: MainAlignValue = 'start', cross: CrossAlignValue = 'center', wrap: boolean = false, runGap: number | null = null, padding: EdgeInsets | null = null, width?: SizeValue, height?: SizeValue, children: VisualNode[] | null = null) {
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

    static stack(align: AlignmentValue = 'topStart', width?: SizeValue, height?: SizeValue, children: VisualNode[] | null = null) {
        let node = new Stack(align, { width: width, height: height });
        if (children != null) for (const child of children) node.add(child);
        return node;
    }

    static box(style?: BoxStyle, child: VisualNode | null = null) {
        return new Box(style, child);
    }

    static text(content: string, role: TypeRoleValue = 'bodyL', color: ColorToken | null = null, maxLines: number = 0, align: TextAlignmentValue = 'start', mono: boolean = false, tabular: boolean = false, styleOverride: TypeStyle | null = null, headingLevel: number = 0) {
        return new Text(content, role, color, maxLines, align, mono, tabular, styleOverride, headingLevel);
    }

    static textEntry(value: string, onChanged: ((string: string) => void) | null = null, label: string | null = null, placeholder: string | null = null, disabled: boolean = false, obscure: boolean = false) {
        return new TextEntry(value, onChanged, { label: label, placeholder: placeholder, disabled: disabled, obscure: obscure });
    }

    static pressable(child: VisualNode, onPressed: (() => void) | null = null, label: string | null = null, selected: boolean | null = null, disabled: boolean = false, pressedBackground: ColorToken | null = null, expanded: boolean | null = null, role: PressableRoleValue = 'button') {
        return new Pressable(child, onPressed, { label: label, selected: selected, disabled: disabled, pressedBackground: pressedBackground, expanded: expanded, role: role });
    }

    static link(destination: string, child: VisualNode, label: string | null = null, current: boolean = false) {
        return new Link(destination, child, { label: label, current: current });
    }

    static glyph(glyph: IconGlyph, size: number = 24, color: ColorToken | null = null, label: string | null = null) {
        return new Icon(glyph, size, color, label);
    }

    static icon(glyph: IconsValue, size: number = 24, color: ColorToken | null = null, label: string | null = null) {
        return new Icon(glyph, size, color, label);
    }

    static vector(glyph: IconGlyph, size: number, color: ColorToken | null = null, label: string | null = null, height: number = 0) {
        return new Vector(glyph, size, color, label, height);
    }

    static drawing(artwork: VectorDrawing, width: number, height: number = 0, tint: ColorToken | null = null, label: string | null = null) {
        return new Drawing(artwork, width, height, tint, label);
    }

    static image(source: string, width: number, height: number, fit: ImageFitValue = 'cover', alt: string = '') {
        return new Image(source, width, height, fit, alt);
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

    static positioned(child: VisualNode, top: number | null = null, end: number | null = null, bottom: number | null = null, start: number | null = null) {
        return new Positioned(child, top, end, bottom, start);
    }

    static scrollView(child: VisualNode, axis: ScrollAxisValue = 'vertical', width?: SizeValue, height?: SizeValue) {
        return new ScrollView(child, axis, { width: width, height: height });
    }

    static safeArea(child: VisualNode, edges: number = 15) {
        return new SafeArea(child, edges);
    }

    static sticky(child: VisualNode, offset: number = 0) {
        return new Sticky(child, offset);
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

    static presence(child: VisualNode, enter: PresenceMotionValue = 'fade') {
        return new Presence(child, enter);
    }

    static draggable(child: VisualNode, onReleased: ((float: number) => void) | null = null) {
        return new Draggable(child, onReleased);
    }

    static dragDismiss(child: VisualNode, onDismiss: (() => void) | null = null) {
        return new DragDismiss(child, onDismiss);
    }

    static adjustable(child: VisualNode, onAdjust: (int: number) => void) {
        return new Adjustable(child, onAdjust);
    }

    static shortcut(child: VisualNode, chord: KeyChord, onPressed: () => void) {
        return new Shortcut(child, chord, onPressed);
    }

    static adaptiveNode(compact: VisualNode, medium: VisualNode | null = null, expanded: VisualNode | null = null) {
        return new AdaptiveNode(compact, medium, expanded);
    }

    static button(label: string, variant: VariantValue = 'primary', size: SizeVariantValue = 'medium', onPressed: (() => void) | null = null) {
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

    static badge(count: number = 0, max: number = 99, variant: VariantValue = 'destructive') {
        return new Badge(count, max, variant);
    }

    static dotBadge(variant: VariantValue = 'destructive') {
        return Badge.asDot(variant);
    }

    static avatar(initials: string, size: SizeVariantValue = 'medium', name: string | null = null) {
        return new Avatar(initials, size, name);
    }

    static banner(status: VariantValue, title: string, body: string | null = null) {
        return new Banner(status, title, body);
    }

    static checkbox(checked: boolean, onChanged: (() => void) | null = null, label: string | null = null, disabled: boolean = false) {
        return new Checkbox(checked, onChanged, label, { disabled: disabled });
    }

    static switch(on: boolean, onChanged: (() => void) | null = null, label: string | null = null, disabled: boolean = false) {
        return new Switch(on, onChanged, { label: label, disabled: disabled });
    }

    static slider(value: number, onChanged: ((float: number) => void) | null = null) {
        return new Slider(value, onChanged);
    }

    static stepper(value: number, onChanged: ((int: number) => void) | null = null) {
        return new Stepper(value, onChanged);
    }

    static datePicker(selected: DateOnly | null = null, onChanged: ((dateOnly: DateOnly) => void) | null = null, min: DateOnly | null = null, max: DateOnly | null = null, label: string = '') {
        return new DatePicker(selected, onChanged, min, max, label);
    }

    static timePicker(selected: TimeOnly | null = null, onChanged: ((timeOnly: TimeOnly) => void) | null = null, stepMinutes: number = 30, min: TimeOnly | null = null, max: TimeOnly | null = null, label: string = '') {
        return new TimePicker(selected, onChanged, stepMinutes, min, max, label);
    }

    static dateTimePicker(selected: DateTime | null = null, onChanged: ((dateTime: DateTime) => void) | null = null, min: DateTime | null = null, max: DateTime | null = null, stepMinutes: number = 30, dateLabel: string = '', timeLabel: string = '') {
        return new DateTimePicker(selected, onChanged, min, max, stepMinutes, dateLabel, timeLabel);
    }

    static calendar(selected: DateOnly | null = null, onChanged: ((dateOnly: DateOnly) => void) | null = null, min: DateOnly | null = null, max: DateOnly | null = null) {
        return new Calendar(selected, onChanged, min, max);
    }

    static select(options: string[], selectedIndex: number = -1, onChanged: ((int: number) => void) | null = null, placeholder: string | null = null) {
        return new Select(options, selectedIndex, onChanged, placeholder);
    }

    static textInput(value: string, onChanged: ((string: string) => void) | null = null, label: string = '', placeholder: string | null = null, helper: string | null = null, error: string | null = null, leading: IconsValue | null = null, size: SizeVariantValue = 'large', trailing: VisualNode | null = null) {
        return new TextInput(value, onChanged, label, placeholder, helper, error, leading, size, trailing);
    }

    static searchField(query: string, onChanged: ((string: string) => void) | null = null, placeholder: string | null = null, onSubmit: (() => void) | null = null) {
        return new SearchField(query, onChanged, placeholder, onSubmit);
    }

    static progressBar(value: number | null = null, variant: VariantValue = 'primary') {
        return new ProgressBar(value, variant);
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

    static cultureSwitcher(options: CultureOption[]) {
        return new CultureSwitcher(options);
    }

    static iconButton(glyph: Icon, label: string, kind: string = 'standard', size: SizeVariantValue = 'medium', onPressed: (() => void) | null = null) {
        return new IconButton(glyph, label, kind, size, onPressed);
    }

    static emptyState(icon: Icon, title: string, body: string | null = null) {
        return new EmptyState(icon, title, body);
    }

    static segmentedControl(segments: string[], selectedIndex: number, onChanged: ((int: number) => void) | null = null, stretch: boolean = true) {
        return new SegmentedControl(segments, selectedIndex, onChanged, { stretch: stretch });
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

    static listDetail(list: VisualNode, detail: VisualNode | null = null, onBack: (() => void) | null = null) {
        return new ListDetail(list, detail, onBack);
    }

    static dialog(title: string, body: string, actions: DialogAction[], dismissible: boolean = false, onDismiss: (() => void) | null = null) {
        return new Dialog(title, body, actions, dismissible, onDismiss);
    }

    static toast(message: string, status: VariantValue = 'info', actionLabel: string | null = null, onAction: (() => void) | null = null) {
        return new Toast(message, status, actionLabel, onAction);
    }

    static tooltip(child: VisualNode, text: string) {
        return new Tooltip(child, text);
    }

    static skeleton(shape: string, width: number, height: number = 0) {
        return new Skeleton(shape, width, height);
    }
}

