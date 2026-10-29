import { $eq, Box, BoxStyle, BuildContext, CodeBlock$CodeMetrics, Column, CornerRadii, EdgeInsets, LiveRegion, Point, Rect, SizeValue, TypeStyle, VisualNode } from "../runtime-exports";

export class CodeNoteBuilder {
    constructor(context: BuildContext, metrics: CodeBlock$CodeMetrics, viewWidth: number) {
        this._context = null!;
        this._most = 0;
        this._stack = null!;
        this._wanted = 0;
        this._width = 0;
        this._height = 0;
        this._parts = 0;
        this._fitted = false;
        this.code = undefined!;
        this.prose = undefined!;
        this.codeLine = 0;
        this.proseLine = 0;
        this._context = context;
        this.code = metrics.style;
        this.prose = $eq.withPatch(metrics.style, { mono: false });
        this.codeLine = this.code.scaledLineHeight(context.typeScale);
        this.proseLine = this.prose.scaledLineHeight(context.typeScale);
        this._most = Math.fround(Math.fround(CodeNoteBuilder.maxColumns * metrics.columnWidth) + CodeNoteBuilder.across);
        if (viewWidth > 0) this._most = Math.min(this._most, viewWidth);
        this._stack = new Column(CodeNoteBuilder.gap, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        this._height = CodeNoteBuilder.down;
    }

    _context!: BuildContext;
    _most!: number;
    _stack!: Column;
    _wanted!: number;
    _width!: number;
    _height!: number;
    _parts!: number;
    _fitted!: boolean;
    code!: TypeStyle;
    prose!: TypeStyle;
    codeLine!: number;
    proseLine!: number;
    static maxColumns: number = 80;
    static maxLinesEach: number = 8;
    static border: number = 1;
    static paddingAcross: number = 8;
    static paddingDown: number = 4;
    static gap: number = 4;
    static across: number = 18;
    static down: number = 10;

    want(wide: number) {
        if (this._fitted) throw $eq.exceptions.create(['System.InvalidOperationException', 'System.SystemException', 'System.Exception'], 'A note is asked for its lines before it fits: its width is settled.');
        this._wanted = Math.max(this._wanted, wide);
    }

    fit() {
        this._fitted = true;
        this._width = Math.min(this._most, Math.fround(Math.ceil(this._wanted) + CodeNoteBuilder.across));
        return Math.max(1, Math.fround(this._width - CodeNoteBuilder.across));
    }

    add(part: VisualNode, tall: number) {
        this.fitted();
        this._stack.add(part);
        this._height = Math.fround(this._height + Math.fround((this._parts++ === 0 ? 0 : CodeNoteBuilder.gap) + tall));
    }

    build(announced: boolean): [VisualNode, number, number] {
        return this.framed(this._stack, this._height, announced);
    }

    frame(part: VisualNode, tall: number, announced: boolean): [VisualNode, number, number] {
        return this.framed(part, Math.fround(CodeNoteBuilder.down + tall), announced);
    }

    widest(text: string, style: TypeStyle) {
        let widest = 0;
        for (const paragraph of $eq.text.split(text, '\n')) widest = Math.max(widest, this._context.measureText(paragraph, style));
        return widest;
    }

    linesOf(text: string, style: TypeStyle, room: number) {
        let lines = 0;
        for (const paragraph of $eq.text.split(text, '\n')) {
            lines += this.wrappedLines(paragraph, style, Math.max(1, room));
            if (lines >= CodeNoteBuilder.maxLinesEach) return CodeNoteBuilder.maxLinesEach;
        }
        return lines;
    }

    static place(line: Rect, viewTop: number, viewBottom: number, viewLeft: number, viewWidth: number, width: number, height: number, aboveTaken: boolean | null) {
        let above = Math.fround(line.y - height);
        let below = Math.fround(line.y + line.height);
        let roomAbove = above >= viewTop;
        let roomBelow = Math.fround(below + height) <= viewBottom;
        let y = null;
        if (aboveTaken == null) y = roomAbove ? above : below; else if (aboveTaken === false) {
            if (!roomAbove) return null;
            y = above;
        } else {
            if (!roomBelow) return null;
            y = below;
        }
        let x = line.x;
        if (viewWidth > 0) x = Math.min(x, Math.fround(Math.fround(viewLeft + viewWidth) - width));
        x = Math.max(x, viewLeft);
        return new Point(x, y);
    }

    framed(content: VisualNode, height: number, announced: boolean): [VisualNode, number, number] {
        this.fitted();
        let theme = this._context.theme;
        let card: VisualNode = new Box(new BoxStyle({ width: this._width, background: theme.surface, cornerRadius: new CornerRadii(theme.shape('medium')), borderWidth: CodeNoteBuilder.border, borderColor: theme.border, elevation: 2, padding: EdgeInsets.symmetric(CodeNoteBuilder.paddingAcross, CodeNoteBuilder.paddingDown), clip: true }), content);
        if (announced) card = new LiveRegion(card);
        return [card, this._width, height];
    }

    fitted() {
        if (!this._fitted) throw $eq.exceptions.create(['System.InvalidOperationException', 'System.SystemException', 'System.Exception'], 'A note fits before its parts are added: its width is not settled.');
    }

    wrappedLines(paragraph: string, style: TypeStyle, room: number) {
        let lines = 1;
        let line = '';
        for (const word of $eq.text.split(paragraph, ' ')) {
            let longer = line.length === 0 ? word : line + ' ' + word;
            if (this._context.measureText(longer, style) <= room) {
                line = longer;
                continue;
            }
            if (line.length > 0) lines++;
            line = word;
            let wide = this._context.measureText(word, style);
            if (wide > room) lines += (Math.trunc(Math.ceil(Math.fround(wide / room))) | 0) - 1;
        }
        return lines;
    }

    toString(): string {
        return 'eQuantic.UI.Components.CodeNoteBuilder';
    }
}

