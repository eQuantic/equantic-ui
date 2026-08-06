import { $eq, Box, BoxStyle, BuildContext, CodeDecoration, CodeDocument, CodeGutterMarker, CodeHighlighter, CodeLanguages, CodeMetrics, Color, ColorToken, Column, CornerRadii, EdgeInsets, IconButton, Positioned, Pressable, Row, ScrollView, SizeValue, Sizing, Spacer, Stack, StatelessComponent, Text, TypeStyle, VisualNode } from "../runtime-exports";

export class CodeBlock extends StatelessComponent {
    static codeSlab: ColorToken = new ColorToken(Color.fromRgba(0x10, 0x14, 0x18, 0xFF));
    static codeInk: ColorToken = new ColorToken(Color.fromRgba(0xC9, 0xD4, 0xDE, 0xFF));
    static codeInkMuted: ColorToken = new ColorToken(Color.fromRgba(0x7C, 0x8A, 0x99, 0xFF));
    static codeSlabActive: ColorToken = new ColorToken(Color.fromRgba(0x1B, 0x22, 0x2B, 0xFF));
    static codeSlabMarked: ColorToken = new ColorToken(Color.fromRgba(0x16, 0x1C, 0x24, 0xFF));
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
    declare highlighter: any;
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
        let theme = context.theme;let highlighter = this.highlighter ?? new CodeHighlighter(this.language);let metrics = CodeBlock.metricsFor(context, this.size, this.showLineNumbers, this.firstLineNumber + this.document.lineCount - 1);let style = metrics.style;let lineHeight = metrics.lineHeight;let gutterWidth = metrics.gutterWidth;let ink = this.inverse ? CodeBlock.codeInk : theme.textPrimary;let surface = this.inverse ? CodeBlock.codeSlab : theme.surfaceSubtle;let lines = new Column(0, { width: SizeValue.fill });for (let index = 0; index < this.document.lineCount; index++) {lines.add(this.lineRow(context, highlighter, index, style, lineHeight, gutterWidth, ink, theme));}let body: VisualNode = new Box(new BoxStyle({ width: SizeValue.fill, padding: EdgeInsets.symmetric(0, 12) }), lines);body = new ScrollView(body, 'horizontal', { width: SizeValue.fill });if (this.maxHeight > 0) {body = new Box(new BoxStyle({ width: SizeValue.fill, maxHeight: this.maxHeight }), new ScrollView(body, 'vertical', { width: SizeValue.fill }));}let slab = new Box(new BoxStyle({ width: SizeValue.fill, background: surface, cornerRadius: new CornerRadii(theme.shape('medium')), clip: true }), body);if (this.caption == null && this.onCopy == null) return slab;let corner = new Row(8, { width: SizeValue.fill, cross: 'center' });corner.add(new Spacer(1));let caption; if (((this.caption != null) && (caption = this.caption, true))) {corner.add(new Text(caption, 'labelSmall', this.inverse ? CodeBlock.codeInkMuted : theme.textMuted, 1, { mono: true }));}let copy; if (((this.onCopy != null) && (copy = this.onCopy, true))) {corner.add(new IconButton('copy', 'Copy code', 'standard', 'medium', null, { size: 'small', onPressed: copy }));}let layers = new Stack('topStart', { width: SizeValue.fill });layers.add(slab);layers.add(new Positioned(new Box(new BoxStyle({ width: SizeValue.fill, padding: EdgeInsets.symmetric(12, 8) }), corner), 0, null, null, 0));return layers;
    }

    static metricsFor(context: any, size: string, showLineNumbers: boolean, lastLineNumber: number) {
        let style = $eq.withPatch(TypeStyle.ofSize(Sizing.labelSize(size, context.density), 'regular'), { mono: true });let gutter = showLineNumbers ? Math.ceil(context.measureText(String(lastLineNumber) + '0', style)) + 12 : 0;return new CodeMetrics(style, $eq.math.round(style.lineHeight * 1.15), context.monoAdvance(style), gutter);
    }

    lineRow(_context: any, highlighter: CodeHighlighter, index: number, style: TypeStyle, lineHeight: number, gutterWidth: number, ink: ColorToken, theme: any) {
        let row = new Row(0, { width: SizeValue.fill, height: lineHeight, cross: 'center' });if (this.showLineNumbers) {let marker = this.markerFor(index);let numbers = new Row(4, { width: SizeValue.fixed(gutterWidth), height: SizeValue.fill, main: 'end', cross: 'center' });let mark; if (((marker != null) && (mark = marker, true))) {numbers.add(new Box(new BoxStyle({ width: 7, height: 7, background: this.gutterColor(mark.kind, theme), cornerRadius: new CornerRadii(999) })));}numbers.add(new Text(String((this.firstLineNumber + index)), 'labelSmall', this.inverse ? CodeBlock.codeInkMuted : theme.textMuted, 1, { mono: true, tabular: true, styleOverride: $eq.withPatch(style, { weight: 'regular' }) }));let pressed: any; row.add(((this.onGutterPressed != null) && (pressed = this.onGutterPressed, true)) ? new Pressable(numbers, () => pressed(index), { label: `Line ${this.firstLineNumber + index}` }) : numbers);}let code = new Row(0, { height: SizeValue.fill, cross: 'center' });let text = this.document.line(index);let tokens = highlighter.tokensFor(this.document, index);let at = 0;for (const token of tokens) {if (token.start > at) code.add(CodeBlock.run(text.slice(at, token.start), ink, style));code.add(CodeBlock.run(text.slice(token.start, Math.min(token.end, text.length)), this.inverse ? CodeBlock.inverseCode(token.kind, theme) : theme.code(token.kind), style));at = Math.min(token.end, text.length);}if (at < text.length) code.add(CodeBlock.run(text.slice(at), ink, style));if (text.length === 0) code.add(CodeBlock.run(' ', ink, style));row.add(new Box(new BoxStyle({ padding: EdgeInsets.symmetric(12, 0) }), code));let active = this.activeLine === index;let decoration = this.decorationFor(index);if (!active && decoration == null) return row;let marked: any; let wash = ((decoration?.color != null) && (marked = decoration?.color, true)) ? (this.inverse ? new ColorToken(marked.dark, marked.dark) : marked) : this.inverse ? (active ? CodeBlock.codeSlabActive : CodeBlock.codeSlabMarked) : (active ? theme.colors('primary').subtle : theme.surfaceHighlight);return new Box(new BoxStyle({ width: SizeValue.fill, background: wash }), row);
    }

    static run(content: string, color: ColorToken, style: TypeStyle) {
        return new Text(content, 'labelSmall', color, 1, { mono: true, styleOverride: style });
    }

    markerFor(line: number) {
        for (const marker of this.gutterMarkers) if (marker.line === line) return marker;return null;
    }

    decorationFor(line: number) {
        for (const decoration of this.decorations) if (decoration.range.start.line <= line && decoration.range.end.line >= line) return decoration;return null;
    }

    gutterColor(kind: string, theme: any) {
        let token = CodeBlock.gutterToken(kind, theme);return this.inverse ? new ColorToken(token.dark, token.dark) : token;
    }

    static gutterToken(kind: string, theme: any) {
        return (() => { const _s = kind; if (_s === 'breakpoint') return theme.colors('destructive').base; if (_s === 'breakpointDisabled') return theme.borderStrong; if (_s === 'error') return theme.colors('destructive').base; if (_s === 'warning') return theme.colors('warning').base; if (_s === 'added') return theme.colors('success').base; if (_s === 'modified') return theme.colors('info').base; if (_s === 'removed') return theme.colors('destructive').subtle; return theme.colors('primary').base; })();
    }

    static inverseCode(kind: string, theme: any) {
        let token = theme.code(kind);return new ColorToken(token.dark, token.dark);
    }

}

