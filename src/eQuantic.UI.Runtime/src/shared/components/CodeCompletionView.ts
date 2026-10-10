import { $eq, Box, BoxStyle, BuildContext, CodeCompletion, CodeCompletionItem, CodeCompletionKindValue, CodeCompletionMatch, CodeLineCells, CodeMetrics, ColorToken, Column, CornerRadii, EdgeInsets, Flexible, Pressable, Rect, Row, SizeValue, Spacer, StyleDiff, Text, TextRun } from "../runtime-exports";

export class CodeCompletionView {
    static pageRows: number = 12;
    static minColumns: number = 24;
    static maxColumns: number = 60;
    static documentationLines: number = 4;
    static documentationBudget: number = 480;
    static border: number = 1;
    static pageMarkWidth: number = 3;
    static documentationChrome: number = 9;
    static labelTabSize: number = 4;

    static labelInset(metrics: CodeMetrics) {
        return Math.fround(Math.fround(Math.fround(CodeCompletionView.border + 8) + metrics.lineHeight) + 4);
    }

    static columnsOf(items: CodeCompletionMatch[]) {
        let widest = CodeCompletionView.minColumns;
        for (const match of items) {
            let columns = CodeCompletionView.entryColumns(match.item);
            if (columns > widest) widest = columns;
        }
        return Math.min(widest, CodeCompletionView.maxColumns);
    }

    static entryColumns(item: CodeCompletionItem) {
        let detail: any; 
        return Math.min(CodeCompletionView.cellsOf(item.label) + (((item.detail != null && item.detail.length > 0) && (detail = item.detail, true)) ? 2 + CodeCompletionView.cellsOf(detail) : 0), CodeCompletionView.maxColumns);
    }

    static widthOf(metrics: CodeMetrics, columns: number) {
        return Math.fround(Math.fround(Math.fround(Math.fround(Math.fround(CodeCompletionView.labelInset(metrics) + Math.fround(Math.fround(columns) * metrics.columnWidth)) + 8) + 4) + CodeCompletionView.pageMarkWidth) + CodeCompletionView.border);
    }

    static heightOf(metrics: CodeMetrics, rows: number) {
        return Math.fround(Math.fround(Math.fround(rows) * metrics.lineHeight) + Math.fround(2 * Math.fround(4 + CodeCompletionView.border)));
    }

    static documentationStyle(metrics: CodeMetrics) {
        return $eq.withPatch(metrics.style, { mono: false });
    }

    static documentationLinesOf(context: BuildContext, metrics: CodeMetrics, documentation: string, width: number) {
        let room = Math.max(1, Math.fround(width - Math.fround(2 * Math.fround(CodeCompletionView.border + 8))));
        let style = CodeCompletionView.documentationStyle(metrics);
        let lines = 0;
        for (const paragraph of CodeCompletionView.shown(documentation).split('\n')) {
            lines += Math.max(1, (Math.trunc(Math.ceil(Math.fround(context.measureText(paragraph, style) / room))) | 0));
            if (lines >= CodeCompletionView.documentationLines) return CodeCompletionView.documentationLines;
        }
        return lines;
    }

    static shown(documentation: string) {
        return documentation.length > CodeCompletionView.documentationBudget ? $eq.text.substring(documentation, 0, CodeCompletionView.documentationBudget) : documentation;
    }

    static documentationLineOf(context: BuildContext, metrics: CodeMetrics) {
        return CodeCompletionView.documentationStyle(metrics).scaledLineHeight(context.typeScale);
    }

    static documentationHeightOf(line: number, lines: number) {
        return lines === 0 ? 0 : Math.fround(CodeCompletionView.documentationChrome + Math.fround(Math.fround(lines) * line));
    }

    static place(metrics: CodeMetrics, word: Rect, viewTop: number, viewBottom: number, viewLeft: number, viewWidth: number, wanted: number, width: number, documentation: number, documentationLine: number): [number, number, number, boolean, number] {
        let below = Math.fround(viewBottom - Math.fround(word.y + word.height));
        let above = Math.fround(word.y - viewTop);
        let shown = Math.fround(word.y + word.height) > viewTop && word.y < viewBottom;
        let rows = wanted;
        let up = false;
        if (shown && CodeCompletionView.heightOf(metrics, wanted) > below) {
            if (CodeCompletionView.heightOf(metrics, wanted) <= above) up = true; else {
                up = above > below;
                let room = Math.fround((up ? above : below) - CodeCompletionView.heightOf(metrics, 0));
                rows = Math.max(1, Math.min(wanted, (Math.trunc(Math.floor(Math.fround(room / metrics.lineHeight))) | 0)));
            }
        }
        let lines = documentation;
        if (shown) {
            let spare = Math.fround(Math.fround((up ? above : below) - CodeCompletionView.heightOf(metrics, rows)) - CodeCompletionView.documentationChrome);
            lines = Math.max(0, Math.min(lines, (Math.trunc(Math.floor(Math.fround(spare / documentationLine))) | 0)));
        }
        let x = Math.fround(word.x - CodeCompletionView.labelInset(metrics));
        if (viewWidth > 0) x = Math.min(x, Math.fround(Math.fround(viewLeft + viewWidth) - width));
        x = Math.max(x, viewLeft);
        let y = up ? Math.fround(Math.fround(word.y - CodeCompletionView.heightOf(metrics, rows)) - CodeCompletionView.documentationHeightOf(documentationLine, lines)) : Math.fround(word.y + word.height);
        return [x, y, rows, up, lines];
    }

    static build(context: BuildContext, completion: CodeCompletion, metrics: CodeMetrics, top: number, rows: number, width: number, above: boolean, documentation: string | null, documentationLines: number, pick: (int: number) => void) {
        let theme = context.theme;
        let items = completion.items;
        let columns = (Math.trunc(Math.floor(Math.fround(Math.fround(width - CodeCompletionView.widthOf(metrics, 0)) / metrics.columnWidth))) | 0);
        let page = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        for (let i = top; i < top + rows && i < items.length; i++) {
            let index = i;
            page.add(CodeCompletionView.option(theme, metrics, items[i], i === completion.selected, columns, () => pick(index)));
        }
        let paged = new Row(4, 'start', 'start', false, null, null, { width: SizeValue.fill });
        paged.add(new Flexible(page));
        paged.add(CodeCompletionView.pageMark(theme, metrics, top, rows, items.length));
        let list = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        let documented = CodeCompletionView.documentation(context, metrics, documentation, documentationLines, above);
        if (above && !(documented == null)) list.add(documented);
        list.add(paged);
        if (!above && !(documented == null)) list.add(documented);
        return new Box(new BoxStyle({ width: width, background: theme.surface, cornerRadius: new CornerRadii(theme.shape('medium')), borderWidth: CodeCompletionView.border, borderColor: theme.border, elevation: 2, padding: EdgeInsets.symmetric(0, 4), clip: true }), list);
    }

    static option(theme: any, metrics: CodeMetrics, match: CodeCompletionMatch, selected: boolean, columns: number, pressed: () => void) {
        let item = match.item;
        let ink = selected ? theme.colors('primary').onSubtle : theme.textPrimary;
        let label = CodeCompletionView.fit(item.label, columns);
        let row = new Row(4, 'start', 'center', false, null, null, { width: SizeValue.fill, height: SizeValue.fill });
        row.add(CodeCompletionView.glyph(theme, metrics, item.kind));
        row.add(new Text(label, 'labelSmall', ink, 1, 'start', false, false, null, 0, { mono: true, styleOverride: metrics.style, spans: CodeCompletionView.marked(label, match.highlights, ink, theme.colors('primary').base) }));
        let room = columns - CodeCompletionView.cellsOf(label) - 2;
        let said: any; 
        let detail = ((item.detail != null && item.detail.length > 0) && (said = item.detail, true)) && room >= 2 ? CodeCompletionView.fit(said, room) : null;
        if (!(detail == null)) {
            row.add(new Flexible(new Spacer()));
            row.add(new Text(detail, 'labelSmall', theme.textMuted, 1, 'start', false, false, null, 0, { mono: true, styleOverride: metrics.style }));
        }
        let whole: any; 
        return new Pressable(new Box(new BoxStyle({ width: SizeValue.fill, height: metrics.lineHeight, padding: EdgeInsets.symmetric(8, 0), background: selected ? theme.colors('primary').subtle : null, hover: selected ? null : new StyleDiff({ background: theme.surfaceSubtle }) }), row), pressed, { role: 'option', selected: selected, canRequestFocus: false, label: ((item.detail != null && item.detail.length > 0) && (whole = item.detail, true)) ? item.label + ', ' + whole : item.label });
    }

    static cellsOf(text: string) {
        return CodeLineCells.widthOf(text, CodeCompletionView.labelTabSize);
    }

    static fit(text: string, columns: number) {
        if (CodeCompletionView.cellsOf(text) <= columns) return text;
        if (columns <= 1) return '…';
        let cells = new CodeLineCells(text, CodeCompletionView.labelTabSize);
        let end = 0;
        for (let i = 0; i < cells.count; i++) {
            let element = cells.elementAt(i);
            if (element.cell + element.width > columns - 1) break;
            end = element.end;
        }
        return $eq.text.substring(text, 0, end) + '…';
    }

    static glyph(theme: any, metrics: CodeMetrics, kind: CodeCompletionKindValue) {
        let cell = new Row(0, 'center', 'center', false, null, null, { width: SizeValue.fixed(metrics.lineHeight), height: SizeValue.fixed(metrics.lineHeight) });
        cell.add(new Text(CodeCompletionView.glyphOf(kind), 'labelSmall', theme.code(CodeCompletionView.inkOf(kind)), 1, 'start', false, false, null, 0, { mono: true, styleOverride: $eq.withPatch(metrics.style, { weight: 'bold' }) }));
        return cell;
    }

    static glyphOf(kind: CodeCompletionKindValue) {
        return (() => { const $s = kind; if ((($s === 'method' || $s === 'function') || $s === 'constructor')) return 'm'; if (($s === 'field' || $s === 'variable')) return 'v'; if ($s === 'property') return 'p'; if ($s === 'event') return 'e'; if ($s === 'class') return 'C'; if ($s === 'struct') return 'S'; if ($s === 'interface') return 'I'; if ($s === 'enum') return 'E'; if ($s === 'typeParameter') return 'T'; if ($s === 'module') return 'N'; if (($s === 'enumMember' || $s === 'constant')) return 'c'; if ((($s === 'value' || $s === 'unit') || $s === 'color')) return '#'; if ($s === 'keyword') return 'k'; if ($s === 'snippet') return 's'; if ($s === 'operator') return 'o'; if ($s === 'reference') return 'r'; if ($s === 'file') return 'f'; if ($s === 'folder') return 'd'; return 'a'; })();
    }

    static inkOf(kind: CodeCompletionKindValue) {
        return (() => { const $s = kind; if (((($s === 'method' || $s === 'function') || $s === 'constructor') || $s === 'event')) return 'function'; if (((((($s === 'class' || $s === 'struct') || $s === 'interface') || $s === 'enum') || $s === 'typeParameter') || $s === 'module')) return 'type'; if ($s === 'property') return 'property'; if (($s === 'enumMember' || $s === 'constant')) return 'constant'; if ((($s === 'value' || $s === 'unit') || $s === 'color')) return 'number'; if (($s === 'keyword' || $s === 'snippet')) return 'keyword'; if ($s === 'operator') return 'operator'; return 'plain'; })();
    }

    static marked(label: string, marks: number[], ink: ColorToken, accent: ColorToken) {
        let marked = new Array(label.length).fill(false);
        for (const at of marks) {
            if (at >= 0 && at < label.length) marked[at] = true;
        }
        let runs: TextRun[] = [];
        let from = 0;
        for (let i = 1; i <= label.length; i++) {
            if (i < label.length && marked[i] === marked[from]) continue;
            runs.push(new TextRun($eq.text.substring(label, from, i - from), marked[from] ? accent : ink, true));
            from = i;
        }
        return runs;
    }

    static pageMark(theme: any, metrics: CodeMetrics, top: number, rows: number, count: number) {
        let track = Math.fround(Math.fround(rows) * metrics.lineHeight);
        let mark = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fixed(CodeCompletionView.pageMarkWidth), height: SizeValue.fixed(track) });
        if (count <= rows) return mark;
        let thumb = Math.max(8, Math.fround(Math.fround(track * Math.fround(rows)) / Math.fround(count)));
        mark.add(Spacer.fixed(Math.fround(Math.fround(Math.fround(track - thumb) * Math.fround(top)) / Math.fround(count - rows))));
        mark.add(new Box(new BoxStyle({ width: SizeValue.fill, height: thumb, background: theme.borderStrong, cornerRadius: new CornerRadii(Math.fround(CodeCompletionView.pageMarkWidth / 2)) })));
        return mark;
    }

    static documentation(context: BuildContext, metrics: CodeMetrics, documentation: string | null, lines: number, above: boolean) {
        let text: any; 
        if (lines === 0 || !(((documentation != null && documentation.length > 0) && (text = documentation, true)))) return null;
        let theme = context.theme;
        let rule = new Box(new BoxStyle({ width: SizeValue.fill, height: 1, background: theme.border }));
        let body = new Box(new BoxStyle({ width: SizeValue.fill, height: Math.fround(CodeCompletionView.documentationHeightOf(CodeCompletionView.documentationLineOf(context, metrics), lines) - 1), padding: EdgeInsets.symmetric(8, 4), clip: true }), new Text(CodeCompletionView.shown(text), 'labelSmall', theme.textSecondary, lines, 'start', false, false, null, 0, { styleOverride: CodeCompletionView.documentationStyle(metrics) }));
        let column = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        if (above) {
            column.add(body);
            column.add(rule);
        } else {
            column.add(rule);
            column.add(body);
        }
        return column;
    }
}

