import { Box, BoxStyle, BuildContext, CodeBlock, Column, CornerRadii, Divider, EdgeInsets, Grid, GridTrack, MarkdownBlock, MarkdownParser, MarkdownRun, MarkdownTheme, Mermaid, Row, SizeValue, StatelessComponent, Text, TextRun, TypeRoleValue, TypeStyle } from "../runtime-exports";

export class Markdown extends StatelessComponent {
    static $typeId = 'eQuantic.UI.Components.Markdown';
    declare source: string;
    declare style: any;

    constructor(source?: any, props?: any) {
        super();
        if (source !== undefined) this.source = source;
        this.source = source;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let style = Markdown.merge(this.style, theme.markdown);
        let blocks = MarkdownParser.parse(this.source);
        let column = new Column(style.blockGap ?? 14, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        for (const block of blocks) column.add(Markdown.blockView(block, theme, style, context));
        return column;
    }

    static merge(own: any, slot: any) {
        if (own == null) return slot ?? new MarkdownTheme();
        if (slot == null) return own;
        return new MarkdownTheme({ heading1: own.heading1 ?? slot.heading1, heading2: own.heading2 ?? slot.heading2, heading3: own.heading3 ?? slot.heading3, heading4: own.heading4 ?? slot.heading4, body: own.body ?? slot.body, blockGap: own.blockGap ?? slot.blockGap, codeLineNumbers: own.codeLineNumbers ?? slot.codeLineNumbers, codeInverse: own.codeInverse ?? slot.codeInverse, headingSpace: own.headingSpace ?? slot.headingSpace, subheadingSpace: own.subheadingSpace ?? slot.subheadingSpace, headingColor: own.headingColor ?? slot.headingColor, bodyColor: own.bodyColor ?? slot.bodyColor, inlineCodeMaxSize: own.inlineCodeMaxSize ?? slot.inlineCodeMaxSize, inlineCodeColor: own.inlineCodeColor ?? slot.inlineCodeColor, quoteIndent: own.quoteIndent ?? slot.quoteIndent, quotePaddingY: own.quotePaddingY ?? slot.quotePaddingY, quoteRuleWidth: own.quoteRuleWidth ?? slot.quoteRuleWidth, quoteRuleColor: own.quoteRuleColor ?? slot.quoteRuleColor, listGap: own.listGap ?? slot.listGap, markerGap: own.markerGap ?? slot.markerGap, listIndent: own.listIndent ?? slot.listIndent, markerColor: own.markerColor ?? slot.markerColor, tableRadius: own.tableRadius ?? slot.tableRadius, tableBorderWidth: own.tableBorderWidth ?? slot.tableBorderWidth, tableBorderColor: own.tableBorderColor ?? slot.tableBorderColor, tableHeaderFill: own.tableHeaderFill ?? slot.tableHeaderFill, tableCellPaddingX: own.tableCellPaddingX ?? slot.tableCellPaddingX, tableHeaderPaddingY: own.tableHeaderPaddingY ?? slot.tableHeaderPaddingY, tableCellPaddingY: own.tableCellPaddingY ?? slot.tableCellPaddingY });
    }

    static bodyRole(style: MarkdownTheme) {
        return style.body ?? 'bodyM';
    }

    static blockView(block: MarkdownBlock, theme: any, style: MarkdownTheme, context: BuildContext) {
        switch (block.kind) {
            case 'heading':
                {
                    let role: TypeRoleValue = style.heading4 ?? 'label';
                    if (block.level === 1) role = style.heading1 ?? 'heading'; else if (block.level === 2) role = style.heading2 ?? 'title'; else if (block.level === 3) role = style.heading3 ?? 'titleSmall';
                    return new Box(new BoxStyle({ width: SizeValue.fill, padding: new EdgeInsets(0, block.level <= 2 ? style.headingSpace ?? 10 : style.subheadingSpace ?? 6, 0, 0) }), new Text(block.text, role, style.headingColor ?? theme.textPrimary, 0, 'start', false, false, null, block.level, { key: block.id }));
                }
            case 'list':
                return Markdown.listView(block, theme, style);
            case 'code':
                if (block.lang === 'mermaid') return new Mermaid(block.raw);
                return new CodeBlock(block.raw, block.lang, { showLineNumbers: style.codeLineNumbers ?? false, inverse: style.codeInverse ?? false, onCopy: () => context.getService('ITextClipboard')?.write(block.raw) });
            case 'quote':
                return new Box(new BoxStyle({ width: SizeValue.fill, padding: new EdgeInsets(style.quoteIndent ?? 16, style.quotePaddingY ?? 2, 0, style.quotePaddingY ?? 2), borderColor: style.quoteRuleColor ?? theme.borderStrong, borderWidth: style.quoteRuleWidth ?? 3, borderSides: 8 }), Markdown.paragraph(block.runs, theme, style));
            case 'table':
                return Markdown.tableView(block, theme, style);
            case 'rule':
                return new Divider();
            default:
                return Markdown.paragraph(block.runs, theme, style);
        }
    }

    static paragraph(runs: MarkdownRun[], theme: any, style: MarkdownTheme) {
        let body: TypeRoleValue = Markdown.bodyRole(style);
        let prose = theme.type(body);
        let codeMax = style.inlineCodeMaxSize ?? 13.5;
        let codeSize = prose.size < codeMax ? prose.size : codeMax;
        let code = new TypeStyle(codeSize, prose.lineHeight, prose.weight, 0, prose.maxScale, true);
        let spans: TextRun[] = [];
        for (const run of runs) {
            spans.push(new TextRun(run.text, null, false, { mono: run.code, styleOverride: run.code ? code : null, color: run.code ? style.inlineCodeColor ?? theme.textPrimary : null, weight: run.bold ? 'bold' : null, italic: run.italic, destination: run.isLink ? run.href : null }));
        }
        return new Text('', body, style.bodyColor ?? theme.textSecondary, 0, 'start', false, false, null, 0, { spans: spans });
    }

    static listView(block: MarkdownBlock, theme: any, style: MarkdownTheme) {
        let indent = style.listIndent ?? 20;
        let list = new Column(style.listGap ?? 8, 'start', 'stretch', false, null, null, { width: SizeValue.fill, cross: 'start' });
        for (const item of block.items) {
            let row = new Row(style.markerGap ?? 10, 'start', 'center', false, null, null, { width: SizeValue.fill, cross: 'start' });
            row.add(new Box(new BoxStyle({ padding: new EdgeInsets(Math.fround(Math.fround(item.depth) * indent), 0, 0, 0) }), new Text(item.marker, Markdown.bodyRole(style), style.markerColor ?? theme.textMuted, 1)));
            row.add(new Box(new BoxStyle({ width: SizeValue.fill }), Markdown.paragraph(item.runs, theme, style)));
            list.add(row);
        }
        return list;
    }

    static tableView(block: MarkdownBlock, theme: any, style: MarkdownTheme) {
        let paddingX = style.tableCellPaddingX ?? 14;
        let headerY = style.tableHeaderPaddingY ?? 10;
        let cellY = style.tableCellPaddingY ?? 11;
        let tracks: GridTrack[] = [];
        for (let i = 0; i < block.head.length; i++) tracks.push(GridTrack.flex(1));
        let rows = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        let head = new Grid(tracks, 0, null, { width: SizeValue.fill });
        for (const cell of block.head) head.add(new Box(new BoxStyle({ padding: new EdgeInsets(paddingX, headerY, paddingX, headerY) }), Markdown.paragraph(cell.runs, theme, style)));
        rows.add(new Box(new BoxStyle({ width: SizeValue.fill, background: style.tableHeaderFill ?? theme.surfaceSubtle }), head));
        for (const row of block.rows) {
            rows.add(new Divider());
            let grid = new Grid(tracks, 0, null, { width: SizeValue.fill });
            for (const cell of row.cells) grid.add(new Box(new BoxStyle({ padding: new EdgeInsets(paddingX, cellY, paddingX, cellY) }), Markdown.paragraph(cell.runs, theme, style)));
            rows.add(grid);
        }
        return new Box(new BoxStyle({ width: SizeValue.fill, cornerRadius: new CornerRadii(style.tableRadius ?? 12), borderColor: style.tableBorderColor ?? theme.border, borderWidth: style.tableBorderWidth ?? 1, clip: true }), rows);
    }
}

