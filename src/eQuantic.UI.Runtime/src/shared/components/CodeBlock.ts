import { $eq, Box, BoxStyle, BuildContext, CodeDecoration, CodeDecorationKindValue, CodeDocument, CodeGutterKindValue, CodeGutterMarker, CodeHighlighter, CodeLanguages, CodeLineCells, CodeMetrics, CodeTokenKindValue, Color, ColorToken, Column, CornerRadii, EdgeInsets, Flexible, Icon, IconButton, IconGlyph, Positioned, Pressable, Rect, Row, ScrollView, SdkStrings, SizeValue, SizeVariantValue, Sizing, Spacer, Stack, StatelessComponent, Text, TypeStyle, VisualNode } from "../runtime-exports";

export class CodeBlock extends StatelessComponent {
    static $typeId = 'eQuantic.UI.Components.CodeBlock';
    static selectionAlpha: number = Math.fround(0.28);
    _cells: Record<string, any> = {};
    static codeSlab: ColorToken = new ColorToken(Color.fromRgba(0x10, 0x14, 0x18, 0xFF));
    static codeInk: ColorToken = new ColorToken(Color.fromRgba(0xC9, 0xD4, 0xDE, 0xFF));
    static codeInkMuted: ColorToken = new ColorToken(Color.fromRgba(0x7C, 0x8A, 0x99, 0xFF));
    static codeSlabActive: ColorToken = new ColorToken(Color.fromRgba(0x1B, 0x22, 0x2B, 0xFF));

    static get $hydration() {
        return { maxHeight: 'single', selectionBands: [{ of: Rect, members: { x: 'single', y: 'single', width: 'single', height: 'single' } }], metrics: CodeMetrics, viewportOffset: 'single', viewportHeight: 'single', viewportWidth: 'single' };
    }

    declare document: CodeDocument;
    declare language: any;
    declare showLineNumbers: boolean;
    declare firstLineNumber: number;
    declare maxHeight: number;

    get tabSize() {
        return this.language.rules.indentWidth;
    }

    declare standalone: boolean;
    declare size: SizeVariantValue;
    declare inverse: boolean;
    declare gutterMarkers: CodeGutterMarker[];
    declare decorations: CodeDecoration[];
    declare activeLine: any;
    declare selectionBands: Rect[];
    declare caption: any;
    declare onCopy: (() => void) | null;
    declare onGutterPressed: any;
    declare metrics: any;
    declare highlighter: any;
    declare viewportOffset: number;
    declare viewportHeight: number;
    declare viewportWidth: number;
    declare onScrolled: any;
    declare onViewportChanged: any;

    constructor(code?: any, language: any = null, props?: any) {
        super();
        if (language !== undefined) this.language = language;
        if (this.showLineNumbers === undefined) this.showLineNumbers = true;
        if (this.firstLineNumber === undefined) this.firstLineNumber = 1;
        if (this.maxHeight === undefined) this.maxHeight = 0;
        if (this.standalone === undefined) this.standalone = true;
        if (this.size === undefined) this.size = 'small';
        if (this.inverse === undefined) this.inverse = false;
        if (this.gutterMarkers === undefined) this.gutterMarkers = [];
        if (this.decorations === undefined) this.decorations = [];
        if (this.selectionBands === undefined) this.selectionBands = [];
        if (this.viewportOffset === undefined) this.viewportOffset = 0;
        if (this.viewportHeight === undefined) this.viewportHeight = 0;
        if (this.viewportWidth === undefined) this.viewportWidth = 0;
        this.document = CodeDocument.fromText(code);
        this.language = CodeLanguages.for(language);
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let highlighter = this.highlighter ?? new CodeHighlighter(this.language);
        let metrics = this.metrics ?? CodeBlock.metricsFor(context, this.size, this.showLineNumbers, this.firstLineNumber + this.document.lineCount - 1);
        let style = metrics.style;
        let lineHeight = metrics.lineHeight;
        let ink = this.inverse ? CodeBlock.codeInk : theme.textPrimary;
        let surface = this.inverse ? CodeBlock.codeSlab : theme.surfaceSubtle;
        let [first, last] = this.window(lineHeight);
        let widest = 0;
        for (let index = 0; index < this.document.lineCount; index++) widest = Math.max(widest, CodeLineCells.widthOf(this.document.line(index), this.tabSize));
        let codeWidth = Math.fround(Math.fround(Math.fround(widest) * metrics.columnWidth) + metrics.columnWidth);
        let lines = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        if (first > 0) lines.add(Spacer.fixed(Math.fround(Math.fround(first) * lineHeight)));
        for (let index = first; index <= last; index++) {
            lines.add(this.lineRow(highlighter, index, style, lineHeight, metrics.columnWidth, ink, theme));
        }
        if (last < this.document.lineCount - 1) lines.add(Spacer.fixed(Math.fround(Math.fround(this.document.lineCount - 1 - last) * lineHeight)));
        let content: VisualNode = new Box(new BoxStyle({ width: SizeValue.fill, padding: EdgeInsets.symmetric(0, 12) }), lines);
        let width = Math.max(codeWidth, this.viewportWidth);
        let marks = new Stack('topStart', { width: SizeValue.fill });
        let activeLine: any; 
        if ((activeLine = this.activeLine) != null && activeLine >= 0 && activeLine < this.document.lineCount) {
            marks.add(new Positioned(new Box(new BoxStyle({ width: width, height: lineHeight, background: this.inverse ? CodeBlock.codeSlabActive : theme.colors('primary').subtle })), Math.fround(metrics.contentTop + Math.fround(Math.fround(activeLine) * lineHeight)), null, null, 0));
        }
        for (const decoration of this.decorations) {
            if (decoration.kind !== 'highlight') continue;
            for (const mark of this.marks(decoration, metrics, theme, first, last)) marks.add(mark);
        }
        if (this.selectionBands.length > 0) {
            let band = CodeBlock.selectionFor(this.inverse, theme).withOpacity(CodeBlock.selectionAlpha);
            let windowTop = Math.fround(metrics.contentTop + Math.fround(Math.fround(first) * lineHeight));
            let windowBottom = Math.fround(metrics.contentTop + Math.fround(Math.fround(last + 1) * lineHeight));
            for (const rect of this.selectionBands) {
                if (Math.fround(rect.y + rect.height) <= windowTop || rect.y >= windowBottom) continue;
                marks.add(new Positioned(new Box(new BoxStyle({ width: rect.width, height: rect.height, background: band, cornerRadius: new CornerRadii(1) })), rect.y, null, null, rect.x));
            }
        }
        for (const decoration of this.decorations) {
            if (decoration.kind === 'highlight') continue;
            for (const mark of this.marks(decoration, metrics, theme, first, last)) marks.add(mark);
        }
        if (marks.children.length > 0) {
            let layered = new Stack('topStart', { width: SizeValue.fill });
            layered.add(marks);
            layered.add(content);
            content = layered;
        }
        let body: VisualNode = new Box(new BoxStyle({ width: SizeValue.fixed(width) }), content);
        if (!this.standalone) return body;
        body = new ScrollView(body, 'horizontal', { width: SizeValue.fill });
        if (this.showLineNumbers) {
            let withGutter = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill, cross: 'start' });
            withGutter.add(this.gutter(context));
            withGutter.add(new Flexible(body));
            body = withGutter;
        }
        if (this.maxHeight > 0) {
            body = new Box(new BoxStyle({ width: SizeValue.fill, maxHeight: this.maxHeight }), new ScrollView(body, 'vertical', { width: SizeValue.fill, onScrolled: this.onScrolled, onViewportChanged: this.onViewportChanged }));
        }
        let slab = new Box(new BoxStyle({ width: SizeValue.fill, background: surface, cornerRadius: new CornerRadii(theme.shape('medium')), clip: true }), body);
        let corner: any; 
        if (!((corner = CodeBlock.corner(this.caption, this.onCopy, this.inverse, theme)) != null)) return slab;
        let layers = new Stack('topStart', { width: SizeValue.fill });
        layers.add(slab);
        layers.add(corner);
        return layers;
    }

    static of(document: CodeDocument, language: any) {
        return new CodeBlock('', null, { document: document, language: language });
    }

    static metricsFor(context: any, size: SizeVariantValue, showLineNumbers: boolean, lastLineNumber: number) {
        let style = $eq.withPatch(TypeStyle.ofSize(Sizing.labelSize(size, context.density), 'regular'), { mono: true });
        let gutter = showLineNumbers ? Math.fround(Math.ceil(context.measureText(String(lastLineNumber) + '0', style)) + 12) : 0;
        return new CodeMetrics(style, $eq.math.roundSingle(Math.fround(style.lineHeight * Math.fround(1.15))), context.monoAdvance(style), gutter);
    }

    static corner(caption: any, onCopy: (() => void) | null, inverse: boolean, theme: any) {
        if (caption == null && onCopy == null) return null;
        let corner = new Row(8, 'start', 'center', false, null, null, { cross: 'center' });
        let text: any; 
        if ((text = caption) != null) {
            corner.add(new Text(text, 'labelSmall', inverse ? CodeBlock.codeInkMuted : theme.textMuted, 1, 'start', false, false, null, 0, { mono: true }));
        }
        let copy: any; 
        if ((copy = onCopy) != null) {
            corner.add(new IconButton(new Icon(IconGlyph.fromIcons('copy')), SdkStrings.copyCode, 'standard', 'medium', null, { size: 'small', onPressed: copy }));
        }
        return new Positioned(new Box(new BoxStyle({ padding: EdgeInsets.symmetric(12, 8) }), corner), 0, 0);
    }

    gutter(context: any) {
        let theme = context.theme;
        let metrics = this.metrics ?? CodeBlock.metricsFor(context, this.size, this.showLineNumbers, this.firstLineNumber + this.document.lineCount - 1);
        let lineHeight = metrics.lineHeight;
        let [first, last] = this.window(lineHeight);
        let column = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fixed(metrics.gutterWidth) });
        column.add(Spacer.fixed(12));
        if (first > 0) column.add(Spacer.fixed(Math.fround(Math.fround(first) * lineHeight)));
        for (let index = first; index <= last; index++) column.add(this.gutterCell(index, metrics, theme));
        if (last < this.document.lineCount - 1) column.add(Spacer.fixed(Math.fround(Math.fround(this.document.lineCount - 1 - last) * lineHeight)));
        return column;
    }

    gutterCell(index: number, metrics: CodeMetrics, theme: any) {
        let numbers = new Row(4, 'start', 'center', false, null, null, { width: SizeValue.fill, height: SizeValue.fill, main: 'end', cross: 'center' });
        let mark: any; 
        if ((mark = this.markerFor(index)) != null) {
            numbers.add(new Box(new BoxStyle({ width: 7, height: 7, background: this.gutterColor(mark.kind, theme), cornerRadius: new CornerRadii(999) })));
        }
        numbers.add(new Text(String((this.firstLineNumber + index)), 'labelSmall', this.inverse ? CodeBlock.codeInkMuted : theme.textMuted, 1, 'start', false, false, null, 0, { mono: true, tabular: true, styleOverride: $eq.withPatch(metrics.style, { weight: 'regular' }) }));
        let cell = new Box(new BoxStyle({ width: SizeValue.fixed(metrics.gutterWidth), height: SizeValue.fixed(metrics.lineHeight), padding: new EdgeInsets(0, 0, 12, 0), background: this.activeLine === index ? this.inverse ? CodeBlock.codeSlabActive : theme.colors('primary').subtle : null }), numbers);
        let pressed: any; 
        return (pressed = this.onGutterPressed) != null ? new Pressable(cell, () => pressed(index), { label: `Line ${this.firstLineNumber + index}` }) : cell;
    }

    lineRow(highlighter: CodeHighlighter, index: number, style: TypeStyle, lineHeight: number, columnWidth: number, ink: ColorToken, theme: any) {
        let row = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill, height: lineHeight, cross: 'center' });
        let code = new Row(0, 'start', 'center', false, null, null, { height: SizeValue.fill, cross: 'center' });
        let text = this.document.line(index);
        let cells = this.cellsOf(index);
        let tokens = highlighter.tokensFor(this.document, index);
        let at = 0;
        for (const token of tokens) {
            let start = Math.min(Math.max(token.start, at), text.length);
            let end = Math.min(Math.max(token.end, start), text.length);
            if (start > at) CodeBlock.addSpan(code, cells, at, start, ink, style, columnWidth);
            CodeBlock.addSpan(code, cells, start, end, this.inverse ? CodeBlock.inverseCode(token.kind, theme) : theme.code(token.kind), style, columnWidth);
            at = end;
        }
        if (at < text.length) CodeBlock.addSpan(code, cells, at, text.length, ink, style, columnWidth);
        if (text.length === 0) code.add(CodeBlock.run(' ', ink, style));
        row.add(new Box(new BoxStyle({ padding: EdgeInsets.symmetric(12, 0) }), code));
        return row;
    }

    cellsOf(line: number) {
        let cells: any; if ((Object.prototype.hasOwnProperty.call(this._cells, line) ? ((cells = this._cells[line]), true) : false)) return cells;
        cells = new CodeLineCells(this.document.line(line), this.tabSize);
        this._cells[line] = cells;
        return cells;
    }

    window(lineHeight: number) {
        if (this.viewportHeight <= 0 || lineHeight <= 0) return [0, this.document.lineCount - 1];
        let margin = 8;
        let first = Math.max(0, (Math.trunc(Math.floor(Math.fround(this.viewportOffset / lineHeight))) | 0) - margin);
        let visible = (Math.trunc(Math.ceil(Math.fround(this.viewportHeight / lineHeight))) | 0) + margin * 2;
        return [first, Math.min(this.document.lineCount - 1, first + visible)];
    }

    marks(decoration: CodeDecoration, metrics: CodeMetrics, theme: any, first: number, last: number) {
        const _seq = [];
        let start = this.document.clamp(decoration.range.start);
        let end = this.document.clamp(decoration.range.end);
        let color = decoration.color ?? this.defaultColor(decoration.kind, theme);
        if (this.inverse) color = new ColorToken(color.dark, color.dark);
        for (let line = Math.max(start.line, first); line <= Math.min(end.line, last); line++) {
            let from = line === start.line ? start.column : 0;
            let to = line === end.line ? end.column : this.document.line(line).length;
            if (to <= from) continue;
            let cells = this.cellsOf(line);
            let left = Math.fround(metrics.contentLeft + Math.fround(Math.fround(cells.cellOf(from)) * metrics.columnWidth));
            let top = Math.fround(metrics.contentTop + Math.fround(Math.fround(line) * metrics.lineHeight));
            let width = Math.fround(Math.fround(cells.cellOf(to) - cells.cellOf(from)) * metrics.columnWidth);
            _seq.push((() => { const _s = decoration.kind; if (_s === 'outline') return new Positioned(new Box(new BoxStyle({ width: width, height: metrics.lineHeight, borderWidth: 1, borderColor: color, cornerRadius: new CornerRadii(2) })), top, null, null, left); if (_s === 'squiggle') return new Positioned(new Box(new BoxStyle({ width: width, height: 2, background: color })), Math.fround(Math.fround(top + metrics.lineHeight) - 2), null, null, left); if (_s === 'strike') return new Positioned(new Box(new BoxStyle({ width: width, height: 1, background: color })), Math.fround(top + Math.fround(metrics.lineHeight / 2)), null, null, left); if (_s === 'underline') return new Positioned(new Box(new BoxStyle({ width: width, height: 1, background: color })), Math.fround(Math.fround(top + metrics.lineHeight) - 2), null, null, left); return new Positioned(new Box(new BoxStyle({ width: width, height: metrics.lineHeight, background: color, cornerRadius: new CornerRadii(2) })), top, null, null, left); })());
        }
        return _seq;
    }

    defaultColor(kind: CodeDecorationKindValue, theme: any) {
        return (() => { const _s = kind; if (_s === 'squiggle') return theme.colors('destructive').base; if (_s === 'outline') return theme.borderStrong; if (_s === 'strike') return theme.textMuted; if (_s === 'underline') return CodeBlock.inkFor(this.inverse, theme); return theme.colors('warning').subtle; })();
    }

    static addSpan(code: Row, cells: CodeLineCells, from: number, to: number, color: ColorToken, style: TypeStyle, columnWidth: number) {
        if (to <= from) return;
        let run = '';
        for (let i = cells.indexOf(from); i < cells.count; i++) {
            let element = cells.elementAt(i);
            if (element.start >= to) break;
            if (element.start < from) continue;
            let text = $eq.text.substring(cells.text, element.start, element.end - element.start);
            if (text === '	') {
                for (let space = 0; space < element.width; space++) run += ' ';
            } else if (element.width === 2) {
                if (run.length > 0) code.add(CodeBlock.run(run, color, style));
                run = '';
                code.add(new Box(new BoxStyle({ width: Math.fround(2 * columnWidth) }), CodeBlock.run(text, color, style)));
            } else run += text;
        }
        if (run.length > 0) code.add(CodeBlock.run(run, color, style));
    }

    static run(content: string, color: ColorToken, style: TypeStyle) {
        return new Text(content, 'labelSmall', color, 1, 'start', false, false, null, 0, { mono: true, styleOverride: style });
    }

    markerFor(line: number) {
        for (const marker of this.gutterMarkers) if (marker.line === line) return marker;
        return null;
    }

    gutterColor(kind: CodeGutterKindValue, theme: any) {
        let token = CodeBlock.gutterToken(kind, theme);
        return this.inverse ? new ColorToken(token.dark, token.dark) : token;
    }

    static gutterToken(kind: CodeGutterKindValue, theme: any) {
        return (() => { const _s = kind; if (_s === 'breakpoint') return theme.colors('destructive').base; if (_s === 'breakpointDisabled') return theme.borderStrong; if (_s === 'error') return theme.colors('destructive').base; if (_s === 'warning') return theme.colors('warning').base; if (_s === 'added') return theme.colors('success').base; if (_s === 'modified') return theme.colors('info').base; if (_s === 'removed') return theme.colors('destructive').subtle; return theme.colors('primary').base; })();
    }

    static inkFor(inverse: boolean, theme: any) {
        return inverse ? CodeBlock.codeInk : theme.textPrimary;
    }

    static selectionFor(inverse: boolean, theme: any) {
        return inverse ? new ColorToken(theme.focusRing.dark, theme.focusRing.dark) : theme.focusRing;
    }

    static surfaceFor(inverse: boolean, theme: any) {
        return inverse ? CodeBlock.codeSlab : theme.surfaceSubtle;
    }

    static inverseCode(kind: CodeTokenKindValue, theme: any) {
        let token = theme.code(kind);
        return new ColorToken(token.dark, token.dark);
    }
}

