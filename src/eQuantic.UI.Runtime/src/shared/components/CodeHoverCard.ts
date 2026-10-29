import { $eq, Box, BoxStyle, BuildContext, CodeBlock$CodeMetrics, CodeDiagnostic, CodeDiagnosticSeverityValue, CodeHoverSession, Column, CornerRadii, EdgeInsets, Flexible, LiveRegion, Rect, Row, SizeValue, Text, TextRun, TypeStyle, VisualNode } from "../runtime-exports";

export class CodeHoverCard {
    static maxColumns: number = 80;
    static maxLinesEach: number = 8;
    static border: number = 1;
    static mark: number = 8;

    static build(context: BuildContext, hover: CodeHoverSession, metrics: CodeBlock$CodeMetrics, viewWidth: number): [VisualNode, number, number] {
        let theme = context.theme;
        let prose = $eq.withPatch(metrics.style, { mono: false });
        let proseLine = prose.scaledLineHeight(context.typeScale);
        let codeLine = metrics.style.scaledLineHeight(context.typeScale);
        let chrome = Math.fround(2 * Math.fround(CodeHoverCard.border + 8));
        let marked = Math.fround(CodeHoverCard.mark + 4);
        let wanted = 0;
        for (const problem of hover.problems) wanted = Math.max(wanted, Math.fround(marked + CodeHoverCard.widest(context, CodeHoverCard.said(problem), prose)));
        for (const answer of hover.answers) {
            let signature: any; 
            if ((($0) => (($0 != null && $0.length > 0) && (signature = $0, true)))(answer.signature)) wanted = Math.max(wanted, CodeHoverCard.widest(context, signature, metrics.style));
            wanted = Math.max(wanted, CodeHoverCard.widest(context, answer.text, prose));
        }
        let most = Math.fround(Math.fround(CodeHoverCard.maxColumns * metrics.columnWidth) + chrome);
        if (viewWidth > 0) most = Math.min(most, viewWidth);
        let width = Math.min(most, Math.fround(Math.ceil(wanted) + chrome));
        let room = Math.max(1, Math.fround(width - chrome));
        let column = new Column(4, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        let height = Math.fround(2 * Math.fround(CodeHoverCard.border + 4));
        let parts = 0;
        for (const problem of hover.problems) {
            let lines = CodeHoverCard.linesOf(context, CodeHoverCard.said(problem), prose, Math.fround(room - marked));
            column.add(CodeHoverCard.problem(theme, problem, prose, proseLine, lines));
            height = Math.fround(height + Math.fround((parts++ === 0 ? 0 : 4) + Math.fround(Math.fround(lines) * proseLine)));
        }
        if ($eq.collections.count(hover.problems) > 0 && $eq.collections.count(hover.answers) > 0) {
            column.add(new Box(new BoxStyle({ width: SizeValue.fill, height: 1, background: theme.border })));
            height = Math.fround(height + Math.fround(4 + 1));
        }
        for (const answer of hover.answers) {
            let signature: any; 
            if ((($0) => (($0 != null && $0.length > 0) && (signature = $0, true)))(answer.signature)) {
                let lines = CodeHoverCard.linesOf(context, signature, metrics.style, room);
                column.add(new Text(signature, 'labelSmall', theme.textPrimary, lines, 'start', false, false, null, 0, { mono: true, styleOverride: metrics.style }));
                height = Math.fround(height + Math.fround((parts++ === 0 ? 0 : 4) + Math.fround(Math.fround(lines) * codeLine)));
            }
            if (answer.text.length > 0) {
                let lines = CodeHoverCard.linesOf(context, answer.text, prose, room);
                column.add(new Text(answer.text, 'labelSmall', theme.textSecondary, lines, 'start', false, false, null, 0, { styleOverride: prose }));
                height = Math.fround(height + Math.fround((parts++ === 0 ? 0 : 4) + Math.fround(Math.fround(lines) * proseLine)));
            }
        }
        let card: VisualNode = new Box(new BoxStyle({ width: width, background: theme.surface, cornerRadius: new CornerRadii(theme.shape('medium')), borderWidth: CodeHoverCard.border, borderColor: theme.border, elevation: 2, padding: EdgeInsets.symmetric(8, 4), clip: true }), column);
        if (hover.byKeyboard) card = new LiveRegion(card);
        return [card, width, height];
    }

    static message(context: BuildContext, metrics: CodeBlock$CodeMetrics, said: string, viewWidth: number): [VisualNode, number, number] {
        let theme = context.theme;
        let prose = $eq.withPatch(metrics.style, { mono: false });
        let proseLine = prose.scaledLineHeight(context.typeScale);
        let chrome = Math.fround(2 * Math.fround(CodeHoverCard.border + 8));
        let most = Math.fround(Math.fround(CodeHoverCard.maxColumns * metrics.columnWidth) + chrome);
        if (viewWidth > 0) most = Math.min(most, viewWidth);
        let width = Math.min(most, Math.fround(Math.ceil(CodeHoverCard.widest(context, said, prose)) + chrome));
        let lines = CodeHoverCard.linesOf(context, said, prose, Math.max(1, Math.fround(width - chrome)));
        let card = new LiveRegion(new Box(new BoxStyle({ width: width, background: theme.surface, cornerRadius: new CornerRadii(theme.shape('medium')), borderWidth: CodeHoverCard.border, borderColor: theme.border, elevation: 2, padding: EdgeInsets.symmetric(8, 4), clip: true }), new Text(said, 'labelSmall', theme.textSecondary, lines, 'start', false, false, null, 0, { styleOverride: prose })));
        return [card, width, Math.fround(Math.fround(2 * Math.fround(CodeHoverCard.border + 4)) + Math.fround(Math.fround(lines) * proseLine))];
    }

    static place(start: Rect, viewTop: number, viewLeft: number, viewWidth: number, width: number, height: number): [number, number] {
        let y = Math.fround(start.y - height) >= viewTop ? Math.fround(start.y - height) : Math.fround(start.y + start.height);
        let x = start.x;
        if (viewWidth > 0) x = Math.min(x, Math.fround(Math.fround(viewLeft + viewWidth) - width));
        x = Math.max(x, viewLeft);
        return [x, y];
    }

    static said(problem: CodeDiagnostic) {
        let origin: any; 
        return (origin = CodeHoverCard.origin(problem)) != null ? `${problem.message}  ${origin}` : problem.message;
    }

    static origin(problem: CodeDiagnostic) {
        let rule: any; 
        let code = (($0) => (($0 != null && $0.length > 0) && (rule = $0, true)))(problem.code) ? rule : null;
        let by: any; 
        let source = (($0) => (($0 != null && $0.length > 0) && (by = $0, true)))(problem.source) ? by : null;
        if (!(code == null) && !(source == null)) return `${source} ${code}`;
        return code ?? source;
    }

    static problem(theme: any, problem: CodeDiagnostic, prose: TypeStyle, proseLine: number, lines: number) {
        let runs = [new TextRun(problem.message, theme.textPrimary)];
        let origin: any; 
        if ((origin = CodeHoverCard.origin(problem)) != null) runs.push(new TextRun(`  ${origin}`, theme.textMuted));
        let row = new Row(4, 'start', 'start', false, null, null, { width: SizeValue.fill });
        row.add(new Box(new BoxStyle({ width: CodeHoverCard.mark, padding: new EdgeInsets(0, Math.max(0, Math.fround(Math.fround(proseLine - CodeHoverCard.mark) / 2)), 0, 0) }), new Box(new BoxStyle({ width: CodeHoverCard.mark, height: CodeHoverCard.mark, cornerRadius: new CornerRadii(Math.fround(CodeHoverCard.mark / 2)), background: CodeHoverCard.severityColor(theme, problem.severity) }))));
        row.add(new Flexible(new Text(CodeHoverCard.said(problem), 'labelSmall', theme.textPrimary, lines, 'start', false, false, null, 0, { styleOverride: prose, spans: runs })));
        return row;
    }

    static severityColor(theme: any, severity: CodeDiagnosticSeverityValue) {
        return (() => { const $s = severity; if ($s === 'error') return theme.colors('destructive').base; if ($s === 'warning') return theme.colors('warning').base; if ($s === 'information') return theme.colors('info').base; return theme.textMuted; })();
    }

    static widest(context: BuildContext, text: string, style: TypeStyle) {
        let widest = 0;
        for (const paragraph of $eq.text.split(text, '\n')) widest = Math.max(widest, context.measureText(paragraph, style));
        return widest;
    }

    static linesOf(context: BuildContext, text: string, style: TypeStyle, room: number) {
        let lines = 0;
        for (const paragraph of $eq.text.split(text, '\n')) {
            lines += CodeHoverCard.wrappedLines(context, paragraph, style, Math.max(1, room));
            if (lines >= CodeHoverCard.maxLinesEach) return CodeHoverCard.maxLinesEach;
        }
        return lines;
    }

    static wrappedLines(context: BuildContext, paragraph: string, style: TypeStyle, room: number) {
        let lines = 1;
        let line = '';
        for (const word of $eq.text.split(paragraph, ' ')) {
            let longer = line.length === 0 ? word : line + ' ' + word;
            if (context.measureText(longer, style) <= room) {
                line = longer;
                continue;
            }
            if (line.length > 0) lines++;
            line = word;
            let wide = context.measureText(word, style);
            if (wide > room) lines += (Math.trunc(Math.ceil(Math.fround(wide / room))) | 0) - 1;
        }
        return lines;
    }
}

