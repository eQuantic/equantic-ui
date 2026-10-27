import { $eq, Box, BoxStyle, BuildContext, CodeBlock$CodeMetrics, CodeDecoration, CodeDecorationKindValue, CodeDocument, CodeFold, CodeGutterKindValue, CodeGutterMarker, CodeHighlighter, CodeLanguages, CodeLineCells, CodeRange, CodeRow, CodeTokenKindValue, ColorToken, Column, CornerRadii, Drawing, EdgeInsets, Flexible, Icon, IconButton, IconGlyph, Positioned, Pressable, Rect, Row, ScrollView, SdkStrings, SizeValue, SizeVariantValue, Sizing, Spacer, Stack, StatelessComponent, Text, TypeStyle, VectorDrawing, VectorPaint, VectorShape, VisualNode } from "../runtime-exports";

export class CodeBlock extends StatelessComponent {
    static $typeId = 'eQuantic.UI.Components.CodeBlock';
    static foldSlot: number = 20;
    _foldsByHeader: any = null;
    _foldedHeaders: any = null;
    _cells: any = $eq.collections.dictionary();
    _fillerCells: any = $eq.collections.dictionary();
    static linePass: number = 0;
    static highlightPass: number = 1;
    static outlinePass: number = 2;
    static waveHeight: number = 4;
    static wavePeriod: number = 4;
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
    declare activeLines: number[];
    declare selectionBands: Rect[];
    declare selections: CodeRange[];
    declare widestLine: any;
    declare rows: any;
    declare fillerDocument: any;
    declare fillerHighlighter: any;
    declare fillerDecorations: CodeDecoration[];
    declare fillerColor: any;
    declare onPlaceholderPressed: ((int: number) => void) | null;
    declare folds: CodeFold[];
    declare foldedLines: number[];
    declare onFoldPressed: ((int: number) => void) | null;
    declare caption: any;
    declare onCopy: (() => void) | null;
    declare onGutterPressed: ((int: number) => void) | null;
    declare metrics: CodeBlock$CodeMetrics | null;
    declare highlighter: any;
    declare viewportOffset: number;
    declare viewportHeight: number;
    declare viewportWidth: number;
    declare minHeight: number;
    declare onScrolled: ((float: number) => void) | null;
    declare onViewportChanged: ((float: number) => void) | null;

    get rowCount() {
        return this.rows?.rowCount ?? this.document.lineCount;
    }

    static $slots: any = null;
    static $failure: any = null;

    static $init(): any {
        if (CodeBlock.$slots === null) {
            if (CodeBlock.$failure !== null) throw CodeBlock.$failure;
            let $slots: any = CodeBlock.$slots = {};
            try {
                $slots.hintDots = null;
                $slots.codeSlab = undefined;
                $slots.codeInk = undefined;
                $slots.codeInkMuted = undefined;
                $slots.codeSlabActive = undefined;
                $slots.hintDots = new VectorDrawing(0, 0, 8, 2, [new VectorShape('M0.2 1a0.8 0.8 0 1 0 1.6 0a0.8 0.8 0 1 0 -1.6 0' + 'M3.2 1a0.8 0.8 0 1 0 1.6 0a0.8 0.8 0 1 0 -1.6 0' + 'M6.2 1a0.8 0.8 0 1 0 1.6 0a0.8 0.8 0 1 0 -1.6 0', VectorPaint.inherit)]);
                $slots.codeSlab = new ColorToken({ r: 0x10, g: 0x14, b: 0x18, a: 0xFF });
                $slots.codeInk = new ColorToken({ r: 0xC9, g: 0xD4, b: 0xDE, a: 0xFF });
                $slots.codeInkMuted = new ColorToken({ r: 0x7C, g: 0x8A, b: 0x99, a: 0xFF });
                $slots.codeSlabActive = new ColorToken({ r: 0x1B, g: 0x22, b: 0x2B, a: 0xFF });
            } catch ($error) {
                CodeBlock.$slots = null;
                throw CodeBlock.$failure = $eq.exceptions.typeInitialization('eQuantic.UI.Components.CodeBlock', $error);
            }
        }
        return CodeBlock.$slots;
    }

    static get hintDots(): VectorDrawing {
        return CodeBlock.$init().hintDots;
    }

    static set hintDots(value: VectorDrawing) {
        CodeBlock.$init().hintDots = value;
    }

    static get codeSlab(): ColorToken {
        return CodeBlock.$init().codeSlab;
    }

    static set codeSlab(value: ColorToken) {
        CodeBlock.$init().codeSlab = value;
    }

    static get codeInk(): ColorToken {
        return CodeBlock.$init().codeInk;
    }

    static set codeInk(value: ColorToken) {
        CodeBlock.$init().codeInk = value;
    }

    static get codeInkMuted(): ColorToken {
        return CodeBlock.$init().codeInkMuted;
    }

    static set codeInkMuted(value: ColorToken) {
        CodeBlock.$init().codeInkMuted = value;
    }

    static get codeSlabActive(): ColorToken {
        return CodeBlock.$init().codeSlabActive;
    }

    static set codeSlabActive(value: ColorToken) {
        CodeBlock.$init().codeSlabActive = value;
    }

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
        if (this.activeLines === undefined) this.activeLines = [];
        if (this.selectionBands === undefined) this.selectionBands = [];
        if (this.selections === undefined) this.selections = [];
        if (this.fillerDecorations === undefined) this.fillerDecorations = [];
        if (this.folds === undefined) this.folds = [];
        if (this.foldedLines === undefined) this.foldedLines = [];
        if (this.viewportOffset === undefined) this.viewportOffset = 0;
        if (this.viewportHeight === undefined) this.viewportHeight = 0;
        if (this.viewportWidth === undefined) this.viewportWidth = 0;
        if (this.minHeight === undefined) this.minHeight = 0;
        this.document = CodeDocument.fromText(code);
        this.language = CodeLanguages.for(language);
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let highlighter = this.highlighter ?? new CodeHighlighter(this.language);
        let metrics = this.metrics ?? CodeBlock.metricsFor(context, this.size, this.showLineNumbers, this.firstLineNumber + this.document.lineCount - 1);
        let lineHeight = metrics.lineHeight;
        let ink = this.inverse ? CodeBlock.codeInk : theme.textPrimary;
        let surface = this.inverse ? CodeBlock.codeSlab : theme.surfaceSubtle;
        let [first, last] = this.window(lineHeight);
        let [firstLine, lastLine] = this.linesIn(first, last);
        let widest = 0;
        let known: any; 
        if ((known = this.widestLine) != null) widest = known; else {
            for (let index = 0; index < this.document.lineCount; index++) widest = Math.max(widest, CodeLineCells.widthOf(this.document.line(index), this.tabSize));
            let fillers: any; 
            if ((fillers = this.fillerDocument) != null) {
                for (let index = 0; index < fillers.lineCount; index++) widest = Math.max(widest, CodeLineCells.widthOf(fillers.line(index), this.tabSize));
            }
        }
        let codeWidth = Math.fround(Math.fround(Math.fround(widest) * metrics.columnWidth) + metrics.columnWidth);
        let fillerHighlighter = this.fillerDocument == null ? null : this.fillerHighlighter ?? new CodeHighlighter(this.language);
        let lines = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        if (first > 0) lines.add(Spacer.fixed(Math.fround(Math.fround(first) * lineHeight)));
        for (let row = first; row <= last; row++) lines.add(this.rowView(this.rowAt(row), highlighter, fillerHighlighter, metrics, ink, theme));
        if (last < this.rowCount - 1) lines.add(Spacer.fixed(Math.fround(Math.fround(this.rowCount - 1 - last) * lineHeight)));
        let content: VisualNode = new Box(new BoxStyle({ width: SizeValue.fill, padding: EdgeInsets.symmetric(0, 12) }), lines);
        let width = Math.max(codeWidth, this.viewportWidth);
        let marks = new Stack('topStart', { width: SizeValue.fill });
        this.addMarks(marks, CodeBlock.linePass, metrics, theme, first, last, firstLine, lastLine, width);
        for (const activeLine of this.activeLines) {
            if (activeLine < 0 || activeLine >= this.document.lineCount || !this.shows(activeLine)) continue;
            marks.add(new Positioned(new Box(new BoxStyle({ width: width, height: lineHeight, background: this.inverse ? CodeBlock.codeSlabActive : theme.colors('primary').subtle })), Math.fround(metrics.contentTop + Math.fround(Math.fround(this.rowOf(activeLine)) * lineHeight)), null, null, 0));
        }
        this.addMarks(marks, CodeBlock.highlightPass, metrics, theme, first, last, firstLine, lastLine, width);
        if ($eq.collections.count(this.selectionBands) > 0) {
            let band = CodeBlock.selectionFor(this.inverse, theme);
            let windowTop = Math.fround(metrics.contentTop + Math.fround(Math.fround(first) * lineHeight));
            let windowBottom = Math.fround(metrics.contentTop + Math.fround(Math.fround(last + 1) * lineHeight));
            for (const rect of this.selectionBands) {
                if (Math.fround(rect.y + rect.height) <= windowTop || rect.y >= windowBottom) continue;
                marks.add(new Positioned(new Box(new BoxStyle({ width: rect.width, height: rect.height, background: band, cornerRadius: new CornerRadii(1) })), rect.y, null, null, rect.x));
            }
        }
        this.addMarks(marks, CodeBlock.outlinePass, metrics, theme, first, last, firstLine, lastLine, width);
        if (marks.children.length > 0) {
            let layered = new Stack('topStart', { width: SizeValue.fill });
            layered.add(marks);
            layered.add(content);
            content = layered;
        }
        let body: VisualNode = new Box(new BoxStyle({ width: SizeValue.fixed(width), minHeight: this.minHeight }), content);
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

    static metricsFor(context: BuildContext, size: SizeVariantValue, showLineNumbers: boolean, lastLineNumber: number) {
        let style = $eq.withPatch(TypeStyle.ofSize(Sizing.labelSize(size, context.density), 'regular'), { mono: true });
        let gutter = showLineNumbers ? Math.fround(Math.ceil(context.measureText($eq.text.format(lastLineNumber, null, undefined, undefined, 'int32') + '0', style)) + 12) : 0;
        return new CodeBlock$CodeMetrics(style, $eq.math.roundSingle(Math.fround(style.lineHeight * Math.fround(1.15))), context.monoAdvance(style), gutter);
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

    gutter(context: BuildContext, numberOf: ((value: CodeRow) => string | null) | null = null) {
        let theme = context.theme;
        let metrics = this.metrics ?? CodeBlock.metricsFor(context, this.size, this.showLineNumbers, this.firstLineNumber + this.document.lineCount - 1);
        let lineHeight = metrics.lineHeight;
        let [first, last] = this.window(lineHeight);
        let column = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fixed(metrics.gutterWidth) });
        column.add(Spacer.fixed(12));
        if (first > 0) column.add(Spacer.fixed(Math.fround(Math.fround(first) * lineHeight)));
        for (let row = first; row <= last; row++) column.add(this.gutterCell(this.rowAt(row), numberOf, metrics, theme));
        if (last < this.rowCount - 1) column.add(Spacer.fixed(Math.fround(Math.fround(this.rowCount - 1 - last) * lineHeight)));
        return column;
    }

    gutterCell(shown: CodeRow, numberOf: ((value: CodeRow) => string | null) | null, metrics: CodeBlock$CodeMetrics, theme: any) {
        let isLine = shown.kind === 'line';
        let index = shown.line;
        let number: any; 
        let label = (number = numberOf) != null ? number(shown) : isLine ? $eq.text.format((this.firstLineNumber + index), null, undefined, undefined, 'int32') : null;
        let numbers = new Row(4, 'start', 'center', false, null, null, { width: SizeValue.fill, height: SizeValue.fill, main: 'end', cross: 'center' });
        let mark: any; 
        if (isLine && (mark = this.markerFor(index)) != null) {
            numbers.add(new Box(new BoxStyle({ width: 7, height: 7, background: this.gutterColor(mark.kind, theme), cornerRadius: new CornerRadii(999) })));
        }
        let text: any; 
        if ((text = label) != null) {
            numbers.add(new Text(text, 'labelSmall', this.inverse ? CodeBlock.codeInkMuted : theme.textMuted, 1, 'start', false, false, null, 0, { mono: true, tabular: true, styleOverride: $eq.withPatch(metrics.style, { weight: 'regular' }) }));
        }
        let cell = new Box(new BoxStyle({ width: SizeValue.fixed(metrics.gutterWidth), height: SizeValue.fixed(metrics.lineHeight), padding: new EdgeInsets(0, 0, 12, 0), background: !isLine ? this.fillerColor : this.activeLines.includes(index) ? this.inverse ? CodeBlock.codeSlabActive : theme.colors('primary').subtle : null }), numbers);
        if ($eq.collections.count(this.folds) === 0 && this.onFoldPressed == null) {
            let pressed: any; 
            return isLine && (pressed = this.onGutterPressed) != null ? new Pressable(cell, () => pressed(index), { label: SdkStrings.lineNumbered(this.firstLineNumber + index) }) : cell;
        }
        let slot = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fixed(metrics.gutterWidth), height: SizeValue.fixed(metrics.lineHeight), cross: 'center' });
        let numbered: VisualNode = new Box(new BoxStyle({ width: SizeValue.fixed(Math.fround(Math.fround(metrics.gutterWidth - CodeBlock.foldSlot) - 12)), height: SizeValue.fill }), numbers);
        let press: any; 
        if (isLine && (press = this.onGutterPressed) != null) numbered = new Pressable(numbered, () => press(index), { label: SdkStrings.lineNumbered(this.firstLineNumber + index) });
        slot.add(numbered);
        slot.add(this.foldChevron(isLine ? this.foldAt(index) : null, metrics, theme));
        return new Box(new BoxStyle({ width: SizeValue.fixed(metrics.gutterWidth), height: SizeValue.fixed(metrics.lineHeight), background: !isLine ? this.fillerColor : this.activeLines.includes(index) ? this.inverse ? CodeBlock.codeSlabActive : theme.colors('primary').subtle : null }), slot);
    }

    foldAt(line: number) {
        if (this._foldsByHeader == null) {
            this._foldsByHeader = $eq.collections.dictionary();
            for (const fold of this.folds) $eq.mapSet(this._foldsByHeader, fold.startLine, fold);
        }
        let found: any; 
        return (($0) => ($0.has(line) ? ((found = $0.get(line)), true) : ((found = null), false)))(this._foldsByHeader) ? found : null;
    }

    isFolded(line: number) {
        this._foldedHeaders ?? (this._foldedHeaders = $eq.collections.hashSetOf([...this.foldedLines]));
        return this._foldedHeaders.has(line);
    }

    foldChevron(fold: any, metrics: CodeBlock$CodeMetrics, theme: any) {
        let slot = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fixed(CodeBlock.foldSlot), height: SizeValue.fill, main: 'end', cross: 'center' });
        if (fold == null) return slot;
        let folded = this.isFolded(fold.startLine);
        let from = this.firstLineNumber + fold.startLine;
        let to = this.firstLineNumber + fold.endLine;
        slot.add(new Icon(IconGlyph.fromIcons(folded ? 'chevronRight' : 'chevronDown'), Math.min(16, metrics.lineHeight), this.inverse ? CodeBlock.codeInkMuted : theme.textMuted));
        let box: VisualNode = slot;
        let pressed: any; 
        return (pressed = this.onFoldPressed) != null ? new Pressable(box, () => pressed(fold.startLine), { label: folded ? SdkStrings.unfoldLines(from, to) : SdkStrings.foldLines(from, to), expanded: !folded, canRequestFocus: false }) : box;
    }

    rowView(shown: CodeRow, highlighter: CodeHighlighter, fillerHighlighter: any, metrics: CodeBlock$CodeMetrics, ink: ColorToken, theme: any) {
        if (shown.kind === 'line') return this.lineRow(this.document, highlighter, this.cellsOf(shown.line), shown.line, metrics, ink, theme, true);
        if (shown.kind === 'placeholder') return this.placeholderRow(shown, metrics, theme);
        let fillers: any; let colours: any; 
        if (shown.sourceLine >= 0 && (fillers = this.fillerDocument) != null && (colours = fillerHighlighter) != null && shown.sourceLine < fillers.lineCount) return this.lineRow(fillers, colours, this.fillerCellsOf(shown.sourceLine), shown.sourceLine, metrics, ink, theme, false);
        return this.fillerRow(shown, metrics, theme);
    }

    fillerRow(shown: CodeRow, metrics: CodeBlock$CodeMetrics, theme: any) {
        let row = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill, height: metrics.lineHeight, cross: 'center' });
        let label: any; 
        if ((label = shown.label) != null) row.add(new Box(new BoxStyle({ padding: EdgeInsets.symmetric(12, 0) }), this.muted(label, metrics, theme)));
        return new Box(new BoxStyle({ width: SizeValue.fill, height: metrics.lineHeight, background: this.fillerColor }), row);
    }

    placeholderRow(shown: CodeRow, metrics: CodeBlock$CodeMetrics, theme: any) {
        let label = shown.label ?? SdkStrings.hiddenLines(shown.count);
        let row = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill, height: metrics.lineHeight, cross: 'center' });
        row.add(new Box(new BoxStyle({ padding: EdgeInsets.symmetric(12, 0) }), this.muted(label, metrics, theme)));
        let box: VisualNode = new Box(new BoxStyle({ width: SizeValue.fill, height: metrics.lineHeight, background: this.fillerColor }), row);
        let pressed: any; 
        return (pressed = this.onPlaceholderPressed) != null ? new Pressable(box, () => pressed(shown.line), { label: label }) : box;
    }

    foldMarker(fold: CodeFold, metrics: CodeBlock$CodeMetrics, theme: any) {
        let spaced = new Row(0, 'start', 'center', false, null, null, { height: SizeValue.fill, cross: 'center' });
        spaced.add(Spacer.fixed(4));
        spaced.add(new Box(new BoxStyle({ padding: EdgeInsets.symmetric(4, 0), background: theme.colors('primary').subtle, cornerRadius: new CornerRadii(theme.shape('small')) }), this.muted(fold.placeholder ?? '⋯', metrics, theme)));
        let marker: VisualNode = spaced;
        let pressed: any; 
        return (pressed = this.onFoldPressed) != null ? new Pressable(marker, () => pressed(fold.startLine), { label: SdkStrings.unfoldLines(this.firstLineNumber + fold.startLine, this.firstLineNumber + fold.endLine), expanded: false, canRequestFocus: false }) : marker;
    }

    muted(text: string, metrics: CodeBlock$CodeMetrics, theme: any) {
        return new Text(text, 'labelSmall', this.inverse ? CodeBlock.codeInkMuted : theme.textMuted, 1, 'start', false, false, null, 0, { mono: true, styleOverride: metrics.style });
    }

    lineRow(document: CodeDocument, highlighter: CodeHighlighter, cells: CodeLineCells, index: number, metrics: CodeBlock$CodeMetrics, ink: ColorToken, theme: any, selectable: boolean) {
        let style = metrics.style;
        let lineHeight = metrics.lineHeight;
        let columnWidth = metrics.columnWidth;
        let row = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill, height: lineHeight, cross: 'center' });
        let code = new Row(0, 'start', 'center', false, null, null, { height: SizeValue.fill, cross: 'center' });
        let text = document.line(index);
        let tokens = highlighter.tokensFor(document, index);
        let [selected, selectedInk] = this.selectedOn(selectable, index, text.length, ink, theme);
        let at = 0;
        for (const token of tokens) {
            let start = Math.min(Math.max(token.start, at), text.length);
            let end = Math.min(Math.max(token.end, start), text.length);
            if (start > at) CodeBlock.addSpans(code, cells, at, start, ink, style, columnWidth, selected, selectedInk);
            CodeBlock.addSpans(code, cells, start, end, this.inverse ? CodeBlock.inverseCode(token.kind, theme) : theme.code(token.kind), style, columnWidth, selected, selectedInk);
            at = end;
        }
        if (at < text.length) CodeBlock.addSpans(code, cells, at, text.length, ink, style, columnWidth, selected, selectedInk);
        if (text.length === 0) code.add(CodeBlock.run(' ', ink, style));
        let fold: any; 
        if (selectable && this.isFolded(index) && (fold = this.foldAt(index)) != null) code.add(this.foldMarker(fold, metrics, theme));
        row.add(new Box(new BoxStyle({ padding: EdgeInsets.symmetric(12, 0) }), code));
        return row;
    }

    cellsOf(line: number) {
        let cells: any; 
        if ((($0) => ($0.has(line) ? ((cells = $0.get(line)), true) : ((cells = null), false)))(this._cells)) return cells;
        cells = new CodeLineCells(this.document.line(line), this.tabSize);
        $eq.mapSet(this._cells, line, cells);
        return cells;
    }

    fillerCellsOf(line: number) {
        let cells: any; 
        if ((($0) => ($0.has(line) ? ((cells = $0.get(line)), true) : ((cells = null), false)))(this._fillerCells)) return cells;
        cells = new CodeLineCells((this.fillerDocument!).line(line), this.tabSize);
        $eq.mapSet(this._fillerCells, line, cells);
        return cells;
    }

    rowAt(row: number) {
        let rows: any; 
        return (rows = this.rows) != null ? rows.rowAt(row) : new CodeRow('line', row);
    }

    rowOf(line: number) {
        return this.rows?.rowOf(line) ?? line;
    }

    shows(line: number) {
        return this.rows?.isVisible(line) ?? true;
    }

    linesIn(first: number, last: number): [number, number] {
        return this.rows?.linesIn(first, last) ?? [first, last];
    }

    window(lineHeight: number): [number, number] {
        return CodeBlock.windowOf(this.rowCount, lineHeight, this.viewportOffset, this.viewportHeight);
    }

    static windowOf(lineCount: number, lineHeight: number, offset: number, viewportHeight: number): [number, number] {
        if (viewportHeight <= 0 || lineHeight <= 0) return [0, lineCount - 1];
        let margin = 8;
        let first = Math.max(0, (Math.trunc(Math.floor(Math.fround(offset / lineHeight))) | 0) - margin);
        let visible = (Math.trunc(Math.ceil(Math.fround(viewportHeight / lineHeight))) | 0) + margin * 2;
        return [first, Math.min(lineCount - 1, first + visible)];
    }

    static passOf(kind: CodeDecorationKindValue) {
        return (() => { const $s = kind; if ($s === 'line') return CodeBlock.linePass; if ($s === 'highlight') return CodeBlock.highlightPass; return CodeBlock.outlinePass; })();
    }

    addMarks(marks: Stack, pass: number, metrics: CodeBlock$CodeMetrics, theme: any, first: number, last: number, firstLine: number, lastLine: number, width: number) {
        for (const decoration of this.decorations) {
            if (CodeBlock.passOf(decoration.kind) !== pass) continue;
            for (const mark of this.marks(decoration, this.document, (line: number) => this.cellsOf(line), (line: number) => this.shows(line) ? this.rowOf(line) : -1, firstLine, lastLine, metrics, theme, width)) marks.add(mark);
        }
        let fillers: any; 
        if (!((fillers = this.fillerDocument) != null) || $eq.collections.count(this.fillerDecorations) === 0) return;
        let sources: number[] = [];
        let rows: number[] = [];
        let lowest = 2147483647;
        let highest = -1;
        for (let row = first; row <= last; row++) {
            let shown = this.rowAt(row);
            if (shown.kind !== 'filler' || shown.sourceLine < 0) continue;
            sources.push(shown.sourceLine);
            rows.push(row);
            lowest = Math.min(lowest, shown.sourceLine);
            highest = Math.max(highest, shown.sourceLine);
        }
        if (sources.length === 0) return;
        for (const decoration of this.fillerDecorations) {
            if (CodeBlock.passOf(decoration.kind) !== pass) continue;
            for (const mark of this.marks(decoration, fillers, (line: number) => this.fillerCellsOf(line), (line: number) => CodeBlock.rowOfSource(sources, rows, line), lowest, highest, metrics, theme, width)) marks.add(mark);
        }
    }

    static rowOfSource(sources: number[], rows: number[], line: number) {
        for (let i = 0; i < sources.length; i++) if (sources[i] === line) return rows[i];
        return -1;
    }

    marks(decoration: CodeDecoration, document: CodeDocument, cellsOf: (value: number) => CodeLineCells, rowOf: (value: number) => number, first: number, last: number, metrics: CodeBlock$CodeMetrics, theme: any, rowWidth: number) {
        const _seq = [];
        let start = document.clamp(decoration.range.start);
        let end = document.clamp(decoration.range.end);
        let color = decoration.color ?? this.defaultColor(decoration.kind, theme);
        if (this.inverse) color = new ColorToken(color.dark, color.dark);
        let whole = decoration.kind === 'line';
        let lastTouched = whole && end.line > start.line && end.column === 0 ? end.line - 1 : end.line;
        for (let line = Math.max(start.line, first); line <= Math.min(lastTouched, last); line++) {
            let row = rowOf(line);
            if (row < 0) continue;
            let top = Math.fround(metrics.contentTop + Math.fround(Math.fround(row) * metrics.lineHeight));
            if (whole) {
                _seq.push(new Positioned(new Box(new BoxStyle({ width: rowWidth, height: metrics.lineHeight, background: color })), top, null, null, 0));
                continue;
            }
            let from = line === start.line ? start.column : 0;
            let to = line === end.line ? end.column : document.line(line).length;
            let point = $eq.equals(start, end) && (decoration.kind === 'squiggle' || decoration.kind === 'dots');
            if (to <= from && !point) continue;
            let cells = cellsOf(line);
            let left = Math.fround(metrics.contentLeft + Math.fround(Math.fround(cells.cellOf(from)) * metrics.columnWidth));
            let width = point ? Math.fround(metrics.columnWidth * Math.fround(Math.max(1, cells.cellOf(Math.min(from + 1, document.line(line).length)) - cells.cellOf(from)))) : Math.fround(Math.fround(cells.cellOf(to) - cells.cellOf(from)) * metrics.columnWidth);
            _seq.push((() => { const $s = decoration.kind; if ($s === 'outline') return new Positioned(new Box(new BoxStyle({ width: width, height: metrics.lineHeight, borderWidth: 1, borderColor: color, cornerRadius: new CornerRadii(2) })), top, null, null, left); if ($s === 'squiggle') return new Positioned(CodeBlock.wave(width, color), Math.fround(Math.fround(top + metrics.lineHeight) - CodeBlock.waveHeight), null, null, left); if ($s === 'dots') return new Positioned(new Drawing(CodeBlock.hintDots, CodeBlock.hintDots.width, CodeBlock.hintDots.height, color), Math.fround(Math.fround(top + metrics.lineHeight) - CodeBlock.hintDots.height), null, null, left); if ($s === 'strike') return new Positioned(new Box(new BoxStyle({ width: width, height: 1, background: color })), Math.fround(top + Math.fround(metrics.lineHeight / 2)), null, null, left); if ($s === 'underline') return new Positioned(new Box(new BoxStyle({ width: width, height: 1, background: color })), Math.fround(Math.fround(top + metrics.lineHeight) - 2), null, null, left); return new Positioned(new Box(new BoxStyle({ width: width, height: metrics.lineHeight, background: color, cornerRadius: new CornerRadii(2) })), top, null, null, left); })());
        }
        return _seq;
    }

    defaultColor(kind: CodeDecorationKindValue, theme: any) {
        return (() => { const $s = kind; if ($s === 'squiggle') return theme.colors('destructive').base; if ($s === 'outline') return theme.borderStrong; if ($s === 'strike') return theme.textMuted; if ($s === 'underline') return CodeBlock.inkFor(this.inverse, theme); if ($s === 'line') return theme.colors('primary').subtle; if ($s === 'dots') return theme.textMuted; return theme.colors('warning').subtle; })();
    }

    static wave(width: number, color: ColorToken) {
        let path = $eq.text.stringBuilder('M0 2');
        for (let x = 0; Math.fround(x) < width; x += CodeBlock.wavePeriod) path.append(' L').append($eq.text.format(x + Math.trunc(CodeBlock.wavePeriod / 2), null, undefined, undefined, 'int32')).append(' 0 L').append($eq.text.format(x + CodeBlock.wavePeriod, null, undefined, undefined, 'int32')).append(' 2');
        let wave = new VectorDrawing(0, -1, width, CodeBlock.waveHeight, [new VectorShape(path.toString(), VectorPaint.none, VectorPaint.inherit, 1)]);
        return new Drawing(wave, width, CodeBlock.waveHeight, color);
    }

    static addSpan(code: Row, cells: CodeLineCells, from: number, to: number, color: ColorToken, style: TypeStyle, columnWidth: number) {
        if (to <= from) return;
        let run = '';
        for (let i = cells.indexOf(from); i < cells.count; i++) {
            let element = cells.elementAt(i);
            if (element.start >= to) break;
            if (element.start < from) continue;
            let text = $eq.text.substring(cells.text, element.start, element.end - element.start);
            if (text === '\t') {
                for (let space = 0; space < element.width; space++) run += ' ';
            } else if (element.width === 2) {
                if (run.length > 0) code.add(CodeBlock.run(run, color, style));
                run = '';
                code.add(new Box(new BoxStyle({ width: Math.fround(2 * columnWidth) }), CodeBlock.run(text, color, style)));
            } else run += text;
        }
        if (run.length > 0) code.add(CodeBlock.run(run, color, style));
    }

    selectedOn(selectable: boolean, line: number, length: number, ink: ColorToken, theme: any): [[number, number][], ColorToken] {
        let stretches: [number, number][] = [];
        let on: any; 
        if (!selectable || !((on = theme.onSelection) != null)) return [stretches, ink];
        for (const selection of this.selections) {
            if (selection.isEmpty) continue;
            let start = selection.start;
            let end = selection.end;
            if (line < start.line || line > end.line) continue;
            stretches.push([line === start.line ? start.column : 0, line === end.line ? end.column : length]);
        }
        return [stretches, this.inverse ? new ColorToken(on.dark, on.dark) : on];
    }

    static addSpans(code: Row, cells: CodeLineCells, from: number, to: number, color: ColorToken, style: TypeStyle, columnWidth: number, selected: any[], selectedInk: ColorToken) {
        let at = from;
        for (const stretch of selected) {
            let inFrom = Math.min(Math.max(stretch[0], at), to);
            let inTo = Math.min(Math.max(stretch[1], inFrom), to);
            if (inTo <= inFrom) continue;
            CodeBlock.addSpan(code, cells, at, inFrom, color, style, columnWidth);
            CodeBlock.addSpan(code, cells, inFrom, inTo, selectedInk, style, columnWidth);
            at = inTo;
        }
        CodeBlock.addSpan(code, cells, at, to, color, style, columnWidth);
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
        return (() => { const $s = kind; if ($s === 'breakpoint') return theme.colors('destructive').base; if ($s === 'breakpointDisabled') return theme.borderStrong; if ($s === 'error') return theme.colors('destructive').base; if ($s === 'warning') return theme.colors('warning').base; if ($s === 'added') return theme.colors('success').base; if ($s === 'modified') return theme.colors('info').base; if ($s === 'removed') return theme.colors('destructive').subtle; return theme.colors('primary').base; })();
    }

    static inkFor(inverse: boolean, theme: any) {
        return inverse ? CodeBlock.codeInk : theme.textPrimary;
    }

    static selectionFor(inverse: boolean, theme: any) {
        return inverse ? new ColorToken(theme.selection.dark, theme.selection.dark) : theme.selection;
    }

    static surfaceFor(inverse: boolean, theme: any) {
        return inverse ? CodeBlock.codeSlab : theme.surfaceSubtle;
    }

    static inverseCode(kind: CodeTokenKindValue, theme: any) {
        let token = theme.code(kind);
        return new ColorToken(token.dark, token.dark);
    }
}

