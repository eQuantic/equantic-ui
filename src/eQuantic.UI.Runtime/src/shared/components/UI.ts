import { AdaptiveNode, Adjustable, Anchored, Avatar, Badge, Banner, Box, BoxStyle, Button, Card, Checkbox, Chip, ColorToken, Column, Dialog, DialogAction, Divider, DragDismiss, Draggable, EmptyState, Flexible, Grid, GridTrack, Hoverable, Icon, IconButton, IconGlyph, Image, KeyChord, Link, Overlay, Positioned, Presence, Pressable, ProgressBar, Row, SafeArea, ScrollView, SearchField, Select, Shortcut, Skeleton, Slider, Spacer, Spinner, Stack, Stepper, Sticky, Switch, Tabs, Text, TextEntry, TextInput, Toast, Tooltip, Vector, VisualNode } from "../runtime-exports";
export class UI {
    static column(gap: number = 0, children: VisualNode[] | null = null) { let node = new Column(gap);if (children != null) for (const child of children) node.add(child);return node; }
    static row(gap: number = 0, children: VisualNode[] | null = null) { let node = new Row(gap);if (children != null) for (const child of children) node.add(child);return node; }
    static grid(columns: GridTrack[], gap: number = 0, rowGap: number | null = null, children: VisualNode[] | null = null) { let node = new Grid(columns, gap, rowGap);if (children != null) for (const child of children) node.add(child);return node; }
    static stack(align: string = 'topStart', children: VisualNode[] | null = null) { let node = new Stack(align);if (children != null) for (const child of children) node.add(child);return node; }
    static box(style?: BoxStyle, child: VisualNode | null = null) { return new Box(style, child); }
    static text(content: string, role: string = 'bodyL', color: ColorToken | null = null, maxLines: number = 0) { return new Text(content, role, color, maxLines); }
    static textEntry(value: string, onChanged: ((string: string) => void) | null = null) { return new TextEntry(value, onChanged); }
    static pressable(child: VisualNode, onPressed: (() => void) | null = null) { return new Pressable(child, onPressed); }
    static link(href: string, child: VisualNode) { return new Link(href, child); }
    static glyph(glyph: IconGlyph, size: number = 24, color: ColorToken | null = null, label: string | null = null) { return new Icon(glyph, size, color, label); }
    static icon(glyph: string, size: number = 24, color: ColorToken | null = null, label: string | null = null) { return new Icon(glyph, size, color, label); }
    static vector(glyph: IconGlyph, size: number, color: ColorToken | null = null, label: string | null = null) { return new Vector(glyph, size, color, label); }
    static image(source: string, width: number, height: number, fit: string = 'cover', alt: string = '') { return new Image(source, width, height, fit, alt); }
    static spinner(size: number = 20, color: ColorToken | null = null) { return new Spinner(size, color); }
    static flexible(child: VisualNode, flex: number = 1, basis: number = 0, shrink: number = 1) { return new Flexible(child, flex, basis, shrink); }
    static spacer(flex: number = 1) { return new Spacer(flex); }
    static gap(dp: number) { return Spacer.fixed(dp); }
    static positioned(child: VisualNode, top: number | null = null, end: number | null = null, bottom: number | null = null, start: number | null = null) { return new Positioned(child, top, end, bottom, start); }
    static scrollView(child: VisualNode, axis: string = 'vertical') { return new ScrollView(child, axis); }
    static safeArea(child: VisualNode, edges: number = 15) { return new SafeArea(child, edges); }
    static sticky(child: VisualNode, offset: number = 0) { return new Sticky(child, offset); }
    static overlay(child: VisualNode) { return new Overlay(child); }
    static anchored(anchor: VisualNode, panel: VisualNode) { return new Anchored(anchor, panel); }
    static hoverable(child: VisualNode, onChanged: (bool: boolean) => void) { return new Hoverable(child, onChanged); }
    static presence(child: VisualNode, enter: string = 'fade') { return new Presence(child, enter); }
    static draggable(child: VisualNode, onReleased: ((float: number) => void) | null = null) { return new Draggable(child, onReleased); }
    static dragDismiss(child: VisualNode, onDismiss: (() => void) | null = null) { return new DragDismiss(child, onDismiss); }
    static adjustable(child: VisualNode, onAdjust: (int: number) => void) { return new Adjustable(child, onAdjust); }
    static shortcut(child: VisualNode, chord: KeyChord, onPressed: () => void) { return new Shortcut(child, chord, onPressed); }
    static adaptiveNode(compact: VisualNode, medium: VisualNode | null = null, expanded: VisualNode | null = null) { return new AdaptiveNode(compact, medium, expanded); }
    static button(label: string, variant: string = 'primary', size: string = 'medium', onPressed: (() => void) | null = null) { return new Button(label, variant, size, onPressed); }
    static card(child: VisualNode, kind: string = 'elevated') { return new Card(child, kind); }
    static chip(label: string, kind: string = 'filter', selected: boolean = false, onPressed: (() => void) | null = null, onRemove: (() => void) | null = null) { return new Chip(label, kind, selected, onPressed, onRemove); }
    static badge(count: number = 0, max: number = 99, variant: string = 'destructive') { return new Badge(count, max, variant); }
    static dotBadge(variant: string = 'destructive') { return Badge.asDot(variant); }
    static avatar(initials: string, size: string = 'medium', name: string | null = null) { return new Avatar(initials, size, name); }
    static banner(status: string, title: string, body: string | null = null) { return new Banner(status, title, body); }
    static checkbox(checked: boolean, onChanged: (() => void) | null = null, label: string | null = null) { return new Checkbox(checked, onChanged, label); }
    static switch(on: boolean, onChanged: (() => void) | null = null) { return new Switch(on, onChanged); }
    static slider(value: number, onChanged: ((float: number) => void) | null = null) { return new Slider(value, onChanged); }
    static stepper(value: number, onChanged: ((int: number) => void) | null = null) { return new Stepper(value, onChanged); }
    static select(options: string[], selectedIndex: number = -1, onChanged: ((int: number) => void) | null = null, placeholder: string | null = null) { return new Select(options, selectedIndex, onChanged, placeholder); }
    static textInput(value: string, onChanged: ((string: string) => void) | null = null, label: string = '', placeholder: string | null = null, helper: string | null = null, error: string | null = null, leading: string | null = null, size: string = 'large') { return new TextInput(value, onChanged, label, placeholder, helper, error, leading, size); }
    static searchField(query: string, onChanged: ((string: string) => void) | null = null, placeholder: string = 'Search…', onSubmit: (() => void) | null = null) { return new SearchField(query, onChanged, placeholder, onSubmit); }
    static progressBar(value: number | null = null, variant: string = 'primary') { return new ProgressBar(value, variant); }
    static divider(inset: string = 'none', axis: string = 'horizontal') { return new Divider(inset, axis); }
    static iconButton(glyph: string, label: string, kind: string = 'standard', size: string = 'medium', onPressed: (() => void) | null = null) { return new IconButton(glyph, label, kind, size, onPressed); }
    static emptyState(icon: string, title: string, body: string | null = null) { return new EmptyState(icon, title, body); }
    static tabs(labels: string[], selected: number, onSelect: ((int: number) => void) | null = null) { return new Tabs(labels, selected, onSelect); }
    static dialog(title: string, body: string, actions: DialogAction[], dismissible: boolean = false, onDismiss: (() => void) | null = null) { return new Dialog(title, body, actions, dismissible, onDismiss); }
    static toast(message: string, status: string = 'info', actionLabel: string | null = null, onAction: (() => void) | null = null) { return new Toast(message, status, actionLabel, onAction); }
    static tooltip(child: VisualNode, text: string) { return new Tooltip(child, text); }
    static skeleton(shape: string, width: number, height: number = 0) { return new Skeleton(shape, width, height); }
}

