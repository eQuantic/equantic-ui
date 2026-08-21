import { Box, BoxStyle, BuildContext, CodeBlock, Column, CornerRadii, Divider, EdgeInsets, Grid, GridTrack, MarkdownBlock, MarkdownParser, MarkdownRun, MarkdownStyle, Mermaid, Row, SizeValue, StatelessComponent, Text, TextRun, TypeRoleValue, TypeStyle } from "../runtime-exports";

export class Markdown extends StatelessComponent {
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
        let style = this.style ?? new MarkdownStyle();
        let blocks = MarkdownParser.parse(this.source);
        let column = new Column(style.blockGap, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        for (const block of blocks) column.add(Markdown.blockView(block, theme, style, context));
        return column;
    }

    static blockView(block: MarkdownBlock, theme: any, style: MarkdownStyle, context: any) {
        switch (block.kind) {
            case 'heading':
                {
                    let role: TypeRoleValue = style.heading4;
                    if (block.level === 1) role = style.heading1; else if (block.level === 2) role = style.heading2; else if (block.level === 3) role = style.heading3;
                    return new Box(new BoxStyle({ width: SizeValue.fill, padding: new EdgeInsets(0, block.level <= 2 ? 10 : 6, 0, 0) }), new Text(block.text, role, theme.textPrimary, 0, 'start', false, false, null, { key: block.id }));
                }
            case 'list':
                return Markdown.listView(block, theme, style);
            case 'code':
                if (block.lang === 'mermaid') return new Mermaid(block.raw);
                return new CodeBlock(block.raw, block.lang, { showLineNumbers: style.codeLineNumbers, inverse: style.codeInverse, onCopy: () => context.getService('ITextClipboard')?.write(block.raw) });
            case 'quote':
                return new Box(new BoxStyle({ width: SizeValue.fill, padding: new EdgeInsets(16, 2, 0, 2), borderColor: theme.borderStrong, borderWidth: 3, borderSides: 8 }), Markdown.paragraph(block.runs, theme, style));
            case 'table':
                return Markdown.tableView(block, theme, style);
            case 'rule':
                return new Divider();
            default:
                return Markdown.paragraph(block.runs, theme, style);
        }
    }

    static paragraph(runs: MarkdownRun[], theme: any, style: MarkdownStyle) {
        let prose = theme.type(style.body);
        let codeSize = prose.size < 13.5 ? prose.size : 13.5;
        let code = new TypeStyle(codeSize, prose.lineHeight, prose.weight, 0, prose.maxScale, true);
        let spans: TextRun[] = [];
        for (const run of runs) {
            spans.push(new TextRun(run.text, null, false, { mono: run.code, styleOverride: run.code ? code : null, color: run.code ? theme.textPrimary : null, weight: run.bold ? 'bold' : null, italic: run.italic, destination: run.isLink ? run.href : null }));
        }
        return new Text('', style.body, theme.textSecondary, 0, 'start', false, false, null, { spans: spans });
    }

    static listView(block: MarkdownBlock, theme: any, style: MarkdownStyle) {
        let list = new Column(8, 'start', 'stretch', false, null, null, { width: SizeValue.fill, cross: 'start' });
        for (const item of block.items) {
            let row = new Row(10, 'start', 'center', false, null, null, { width: SizeValue.fill, cross: 'start' });
            row.add(new Box(new BoxStyle({ padding: new EdgeInsets(item.depth * 20, 0, 0, 0) }), new Text(item.marker, style.body, theme.textMuted, 1)));
            row.add(new Box(new BoxStyle({ width: SizeValue.fill }), Markdown.paragraph(item.runs, theme, style)));
            list.add(row);
        }
        return list;
    }

    static tableView(block: MarkdownBlock, theme: any, style: MarkdownStyle) {
        let tracks: GridTrack[] = [];
        for (let i = 0; i < block.head.length; i++) tracks.push(GridTrack.flex(1));
        let rows = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        let head = new Grid(tracks, 0, null, { width: SizeValue.fill });
        for (const cell of block.head) head.add(new Box(new BoxStyle({ padding: new EdgeInsets(14, 10, 14, 10) }), Markdown.paragraph(cell.runs, theme, style)));
        rows.add(new Box(new BoxStyle({ width: SizeValue.fill, background: theme.surfaceSubtle }), head));
        for (const row of block.rows) {
            rows.add(new Divider());
            let grid = new Grid(tracks, 0, null, { width: SizeValue.fill });
            for (const cell of row.cells) grid.add(new Box(new BoxStyle({ padding: new EdgeInsets(14, 11, 14, 11) }), Markdown.paragraph(cell.runs, theme, style)));
            rows.add(grid);
        }
        return new Box(new BoxStyle({ width: SizeValue.fill, cornerRadius: new CornerRadii(12), borderColor: theme.border, borderWidth: 1, clip: true }), rows);
    }

}

