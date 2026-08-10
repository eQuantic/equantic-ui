import { $eq, Box, BoxStyle, BuildContext, CodeDecoration, CodeDocument, CodeGutterMarker, CodeHighlighter, CodeLanguages, CodeMetrics, Color, ColorToken, Column, CornerRadii, EdgeInsets, IconButton, Positioned, Pressable, Row, ScrollView, SizeValue, Sizing, Spacer, Stack, StatelessComponent, Text, TypeStyle, VisualNode } from "../runtime-exports";

export class CodeBlock extends StatelessComponent {
    static codeSlab: ColorToken = new ColorToken(Color.fromRgba(0x10, 0x14, 0x18, 0xFF));
    static codeInk: ColorToken = new ColorToken(Color.fromRgba(0xC9, 0xD4, 0xDE, 0xFF));
    static codeInkMuted: ColorToken = new ColorToken(Color.fromRgba(0x7C, 0x8A, 0x99, 0xFF));
    static codeSlabActive: ColorToken = new ColorToken(Color.fromRgba(0x1B, 0x22, 0x2B, 0xFF));
    declare document: CodeDocument;
    declare language: any;
    declare showLineNumbers: boolean;
    declare firstLineNumber: number;
    declare maxHeight: number;
    declare size: string;
    declare inverse: boolean;
    declare gutterMarkers: CodeGutterMarker[];
    declare decorations: CodeDecoration[];
    declare activeLine: any;
    declare caption: any;
    declare onCopy: (() => void) | null;
    declare onGutterPressed: any;
    declare metrics: any;
    declare highlighter: any;
    declare viewportOffset: number;
    declare viewportHeight: number;
    declare onScrolled: any;
    declare onViewportChanged: any;
    constructor(code?: any, language: any = null, props?: any) {
        super();
        if (language !== undefined) this.language = language;
        if (this.showLineNumbers === undefined) this.showLineNumbers = true;
        if (this.firstLineNumber === undefined) this.firstLineNumber = 1;
        if (this.size === undefined) this.size = 'small';
        if (this.gutterMarkers === undefined) this.gutterMarkers = [];
        if (this.decorations === undefined) this.decorations = [];
        this.document = CodeDocument.fromText(code);this.language = CodeLanguages.for(language);
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;let highlighter = this.highlighter ?? new CodeHighlighter(this.language);let metrics = this.metrics ?? CodeBlock.metricsFor(context, this.size, this.showLineNumbers, this.firstLineNumber + this.document.lineCount - 1);let style = metrics.style;let lineHeight = metrics.lineHeight;let gutterWidth = metrics.gutterWidth;let ink = this.inverse ? CodeBlock.codeInk : theme.textPrimary;let surface = this.inverse ? CodeBlock.codeSlab : theme.surfaceSubtle;let [first, last] = this.window(lineHeight);let lines = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill });if (first > 0) lines.add(Spacer.fixed(first * lineHeight));for (let index = first; index <= last; index++) {lines.add(this.lineRow(context, highlighter, index, style, lineHeight, gutterWidth, ink, theme));}if (last < this.document.lineCount - 1) lines.add(Spacer.fixed((this.document.lineCount - 1 - last) * lineHeight));let content: VisualNode = lines;if (this.decorations.length > 0) {let decorated = new Stack('topStart', { width: SizeValue.fill });let marks = new Stack('topStart', { width: SizeValue.fill });for (const decoration of this.decorations) {for (const mark of this.marks(decoration, metrics, theme)) marks.add(mark);}decorated.add(marks);decorated.add(lines);content = decorated;}let body: VisualNode = new Box(new BoxStyle({ width: SizeValue.fill, padding: EdgeInsets.symmetric(0, 12) }), content);body = new ScrollView(body, 'horizontal', { width: SizeValue.fill });if (this.maxHeight > 0) {body = new Box(new BoxStyle({ width: SizeValue.fill, maxHeight: this.maxHeight }), new ScrollView(body, 'vertical', { width: SizeValue.fill, onScrolled: this.onScrolled, onViewportChanged: this.onViewportChanged }));}let slab = new Box(new BoxStyle({ width: SizeValue.fill, background: surface, cornerRadius: new CornerRadii(theme.shape('medium')), clip: true }), body);if (this.caption == null && this.onCopy == null) return slab;let corner = new Row(8, 'start', 'center', false, null, null, { width: SizeValue.fill, cross: 'center' });corner.add(new Spacer(1));let caption: any; if (((this.caption != null) && (caption = this.caption, true))) {corner.add(new Text(caption, 'labelSmall', this.inverse ? CodeBlock.codeInkMuted : theme.textMuted, 1, { mono: true }));}let copy: any; if (((this.onCopy != null) && (copy = this.onCopy, true))) {corner.add(new IconButton('copy', 'Copy code', 'standard', 'medium', null, { size: 'small', onPressed: copy }));}let layers = new Stack('topStart', { width: SizeValue.fill });layers.add(slab);layers.add(new Positioned(new Box(new BoxStyle({ width: SizeValue.fill, padding: EdgeInsets.symmetric(12, 8) }), corner), 0, null, null, 0));return layers;
    }

    static of(document: CodeDocument, language: any) {
        return new CodeBlock('', null, { document: document, language: language });
    }

    static metricsFor(context: any, size: string, showLineNumbers: boolean, lastLineNumber: number) {
        let style = $eq.withPatch(TypeStyle.ofSize(Sizing.labelSize(size, context.density), 'regular'), { mono: true });let gutter = showLineNumbers ? Math.ceil(context.measureText(String(lastLineNumber) + '0', style)) + 12 : 0;return new CodeMetrics(style, $eq.math.round(style.lineHeight * 1.15), context.monoAdvance(style), gutter);
    }

    lineRow(_context: any, highlighter: CodeHighlighter, index: number, style: TypeStyle, lineHeight: number, gutterWidth: number, ink: ColorToken, theme: any) {
        let row = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill, height: lineHeight, cross: 'center' });if (this.showLineNumbers) {let marker = this.markerFor(index);let numbers = new Row(4, 'start', 'center', false, null, null, { width: SizeValue.fixed(gutterWidth), height: SizeValue.fill, main: 'end', cross: 'center' });let mark: any; if (((marker != null) && (mark = marker, true))) {numbers.add(new Box(new BoxStyle({ width: 7, height: 7, background: this.gutterColor(mark.kind, theme), cornerRadius: new CornerRadii(999) })));}numbers.add(new Text(String((this.firstLineNumber + index)), 'labelSmall', this.inverse ? CodeBlock.codeInkMuted : theme.textMuted, 1, { mono: true, tabular: true, styleOverride: $eq.withPatch(style, { weight: 'regular' }) }));let pressed: any; row.add(((this.onGutterPressed != null) && (pressed = this.onGutterPressed, true)) ? new Pressable(numbers, () => pressed(index), { label: `Line ${this.firstLineNumber + index}` }) : numbers);}let code = new Row(0, 'start', 'center', false, null, null, { height: SizeValue.fill, cross: 'center' });let text = this.document.line(index);let tokens = highlighter.tokensFor(this.document, index);let at = 0;for (const token of tokens) {if (token.start > at) code.add(CodeBlock.run(text.slice(at, token.start), ink, style));code.add(CodeBlock.run(text.slice(token.start, Math.min(token.end, text.length)), this.inverse ? CodeBlock.inverseCode(token.kind, theme) : theme.code(token.kind), style));at = Math.min(token.end, text.length);}if (at < text.length) code.add(CodeBlock.run(text.slice(at), ink, style));if (text.length === 0) code.add(CodeBlock.run(' ', ink, style));row.add(new Box(new BoxStyle({ padding: EdgeInsets.symmetric(12, 0) }), code));let active = this.activeLine === index;if (!active) return row;let wash = this.inverse ? CodeBlock.codeSlabActive : theme.colors('primary').subtle;return new Box(new BoxStyle({ width: SizeValue.fill, background: wash }), row);
    }

    window(lineHeight: number) {
        if (this.viewportHeight <= 0 || lineHeight <= 0) return [0, this.document.lineCount - 1];let margin = 8;let first = Math.max(0, (Math.trunc(Math.floor(this.viewportOffset / lineHeight)) | 0) - margin);let visible = (Math.trunc(Math.ceil(this.viewportHeight / lineHeight)) | 0) + margin * 2;return [first, Math.min(this.document.lineCount - 1, first + visible)];
    }

    marks(decoration: CodeDecoration, metrics: CodeMetrics, theme: any) {
        const _seq = []; let start = this.document.clamp(decoration.range.start);let end = this.document.clamp(decoration.range.end);let color = decoration.color ?? CodeBlock.defaultColor(decoration.kind, theme);if (this.inverse) color = new ColorToken(color.dark, color.dark);for (let line = start.line; line <= end.line; line++) {let from = line === start.line ? start.column : 0;let to = line === end.line ? end.column : this.document.line(line).length;if (to <= from) continue;let left = metrics.contentLeft + from * metrics.columnWidth;let top = line * metrics.lineHeight;let width = (to - from) * metrics.columnWidth;_seq.push((() => { const _s = decoration.kind; if (_s === 'outline') return new Positioned(new Box(new BoxStyle({ width: width, height: metrics.lineHeight, borderWidth: 1, borderColor: color, cornerRadius: new CornerRadii(2) })), top, null, null, left); if (_s === 'squiggle') return new Positioned(new Box(new BoxStyle({ width: width, height: 2, background: color })), top + metrics.lineHeight - 2, null, null, left); if (_s === 'strike') return new Positioned(new Box(new BoxStyle({ width: width, height: 1, background: color })), top + metrics.lineHeight / 2, null, null, left); return new Positioned(new Box(new BoxStyle({ width: width, height: metrics.lineHeight, background: color, cornerRadius: new CornerRadii(2) })), top, null, null, left); })());} return _seq;
    }

    static defaultColor(kind: string, theme: any) {
        return (() => { const _s = kind; if (_s === 'squiggle') return theme.colors('destructive').base; if (_s === 'outline') return theme.borderStrong; if (_s === 'strike') return theme.textMuted; return theme.colors('warning').subtle; })();
    }

    static run(content: string, color: ColorToken, style: TypeStyle) {
        return new Text(content, 'labelSmall', color, 1, { mono: true, styleOverride: style });
    }

    markerFor(line: number) {
        for (const marker of this.gutterMarkers) if (marker.line === line) return marker;return null;
    }

    gutterColor(kind: string, theme: any) {
        let token = CodeBlock.gutterToken(kind, theme);return this.inverse ? new ColorToken(token.dark, token.dark) : token;
    }

    static gutterToken(kind: string, theme: any) {
        return (() => { const _s = kind; if (_s === 'breakpoint') return theme.colors('destructive').base; if (_s === 'breakpointDisabled') return theme.borderStrong; if (_s === 'error') return theme.colors('destructive').base; if (_s === 'warning') return theme.colors('warning').base; if (_s === 'added') return theme.colors('success').base; if (_s === 'modified') return theme.colors('info').base; if (_s === 'removed') return theme.colors('destructive').subtle; return theme.colors('primary').base; })();
    }

    static inkFor(inverse: boolean, theme: any) {
        return inverse ? CodeBlock.codeInk : theme.textPrimary;
    }

    static selectionFor(inverse: boolean, theme: any) {
        return inverse ? new ColorToken(theme.focusRing.dark, theme.focusRing.dark) : theme.focusRing;
    }

    static inverseCode(kind: string, theme: any) {
        let token = theme.code(kind);return new ColorToken(token.dark, token.dark);
    }

}

