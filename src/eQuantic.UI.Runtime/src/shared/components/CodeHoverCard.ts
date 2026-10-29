import { $eq, Box, BoxStyle, BuildContext, CodeBlock$CodeMetrics, CodeDiagnostic, CodeDiagnosticSeverityValue, CodeHoverSession, CodeNoteBuilder, CornerRadii, EdgeInsets, Flexible, Row, SizeValue, Text, TextRun, TypeStyle, VisualNode } from "../runtime-exports";

export class CodeHoverCard {
    static mark: number = 8;

    static build(context: BuildContext, hover: CodeHoverSession, metrics: CodeBlock$CodeMetrics, viewWidth: number): [VisualNode, number, number] {
        let theme = context.theme;
        let note = new CodeNoteBuilder(context, metrics, viewWidth);
        let marked = Math.fround(CodeHoverCard.mark + 4);
        for (const problem of hover.problems) note.want(Math.fround(marked + note.widest(CodeHoverCard.said(problem), note.prose)));
        for (const answer of hover.answers) {
            let signature: any; 
            if (((answer.signature != null && answer.signature.length > 0) && (signature = answer.signature, true))) note.want(note.widest(signature, note.code));
            note.want(note.widest(answer.text, note.prose));
        }
        let room = note.fit();
        for (const problem of hover.problems) {
            let lines = note.linesOf(CodeHoverCard.said(problem), note.prose, Math.fround(room - marked));
            note.add(CodeHoverCard.problem(theme, problem, note.prose, note.proseLine, lines), Math.fround(Math.fround(lines) * note.proseLine));
        }
        if ($eq.collections.count(hover.problems) > 0 && $eq.collections.count(hover.answers) > 0) note.add(new Box(new BoxStyle({ width: SizeValue.fill, height: 1, background: theme.border })), 1);
        for (const answer of hover.answers) {
            let signature: any; 
            if (((answer.signature != null && answer.signature.length > 0) && (signature = answer.signature, true))) {
                let lines = note.linesOf(signature, note.code, room);
                note.add(new Text(signature, 'labelSmall', theme.textPrimary, lines, 'start', false, false, null, 0, { mono: true, styleOverride: note.code }), Math.fround(Math.fround(lines) * note.codeLine));
            }
            if (answer.text.length > 0) {
                let lines = note.linesOf(answer.text, note.prose, room);
                note.add(new Text(answer.text, 'labelSmall', theme.textSecondary, lines, 'start', false, false, null, 0, { styleOverride: note.prose }), Math.fround(Math.fround(lines) * note.proseLine));
            }
        }
        return note.build(hover.byKeyboard);
    }

    static message(context: BuildContext, metrics: CodeBlock$CodeMetrics, said: string, viewWidth: number): [VisualNode, number, number] {
        let note = new CodeNoteBuilder(context, metrics, viewWidth);
        note.want(note.widest(said, note.prose));
        let lines = note.linesOf(said, note.prose, note.fit());
        return note.frame(new Text(said, 'labelSmall', context.theme.textSecondary, lines, 'start', false, false, null, 0, { styleOverride: note.prose }), Math.fround(Math.fround(lines) * note.proseLine), true);
    }

    static said(problem: CodeDiagnostic) {
        let origin: any; 
        return (origin = CodeHoverCard.origin(problem)) != null ? `${problem.message}  ${origin}` : problem.message;
    }

    static origin(problem: CodeDiagnostic) {
        let rule: any; 
        let code = ((problem.code != null && problem.code.length > 0) && (rule = problem.code, true)) ? rule : null;
        let by: any; 
        let source = ((problem.source != null && problem.source.length > 0) && (by = problem.source, true)) ? by : null;
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
}

