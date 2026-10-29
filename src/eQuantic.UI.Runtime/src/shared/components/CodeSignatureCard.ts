import { $eq, BuildContext, CodeBlock$CodeMetrics, CodeNoteBuilder, CodeSignature, CodeSignatureHelpSession, CodeSignatureParameter, Flexible, Row, SizeValue, Text, TextRun, VisualNode } from "../runtime-exports";

export class CodeSignatureCard {
    static build(context: BuildContext, session: CodeSignatureHelpSession, metrics: CodeBlock$CodeMetrics, viewWidth: number): [VisualNode, number, number] {
        let theme = context.theme;
        let signature = session.signature!;
        let note = new CodeNoteBuilder(context, metrics, viewWidth);
        let count = $eq.collections.count((session.help!).signatures);
        let counter = count > 1 ? `${$eq.text.format(session.activeSignature + 1, null, undefined, undefined, 'int32')}/${$eq.text.format(count, null, undefined, undefined, 'int32')}` : null;
        let counterWidth = counter == null ? 0 : Math.fround(context.measureText(counter, note.prose) + 8);
        let active = session.activeParameter;
        let parameter = active >= 0 && active < $eq.collections.count(signature.parameters) ? $eq.collections.item(signature.parameters, active) : null;
        let about = parameter == null ? null : CodeSignatureCard.said(parameter);
        note.want(Math.fround(note.widest(signature.label, note.code) + counterWidth));
        if (!(about == null)) note.want(note.widest(about, note.prose));
        let documentation: any; 
        if (((signature.documentation != null && signature.documentation.length > 0) && (documentation = signature.documentation, true))) note.want(note.widest(documentation, note.prose));
        let room = note.fit();
        let labelLines = note.linesOf(signature.label, note.code, Math.fround(room - counterWidth));
        let first = new Row(8, 'start', 'start', false, null, null, { width: SizeValue.fill });
        first.add(new Flexible(new Text(signature.label, 'labelSmall', theme.textPrimary, labelLines, 'start', false, false, null, 0, { mono: true, styleOverride: note.code, spans: CodeSignatureCard.marked(theme, signature, active) })));
        if (!(counter == null)) first.add(new Text(counter, 'labelSmall', theme.textMuted, 1, 'start', false, false, null, 0, { styleOverride: note.prose }));
        note.add(first, Math.max(Math.fround(Math.fround(labelLines) * note.codeLine), counter == null ? 0 : note.proseLine));
        if (!(about == null)) {
            let lines = note.linesOf(about, note.prose, room);
            note.add(new Text(about, 'labelSmall', theme.textSecondary, lines, 'start', false, false, null, 0, { styleOverride: note.prose, spans: CodeSignatureCard.described(theme, parameter!) }), Math.fround(Math.fround(lines) * note.proseLine));
        }
        let documented: any; 
        if (((signature.documentation != null && signature.documentation.length > 0) && (documented = signature.documentation, true))) {
            let lines = note.linesOf(documented, note.prose, room);
            note.add(new Text(documented, 'labelSmall', theme.textMuted, lines, 'start', false, false, null, 0, { styleOverride: note.prose }), Math.fround(Math.fround(lines) * note.proseLine));
        }
        return note.build(true);
    }

    static marked(theme: any, signature: CodeSignature, active: number) {
        let start = signature.parameterStart(active);
        if (start < 0) return [new TextRun(signature.label, theme.textPrimary)];
        let end = start + $eq.collections.item(signature.parameters, active).label.length;
        let runs: TextRun[] = [];
        if (start > 0) runs.push(new TextRun($eq.text.substring(signature.label, 0, start), theme.textPrimary));
        runs.push(new TextRun($eq.text.substring(signature.label, start, end - start), theme.colors('primary').base));
        if (end < signature.label.length) runs.push(new TextRun($eq.text.substring(signature.label, end), theme.textPrimary));
        return runs;
    }

    static said(parameter: CodeSignatureParameter) {
        let documentation: any; 
        return ((parameter.documentation != null && parameter.documentation.length > 0) && (documentation = parameter.documentation, true)) ? `${parameter.label}: ${documentation}` : parameter.label;
    }

    static described(theme: any, parameter: CodeSignatureParameter) {
        let runs = [new TextRun(parameter.label, theme.textPrimary)];
        let documentation: any; 
        if (((parameter.documentation != null && parameter.documentation.length > 0) && (documentation = parameter.documentation, true))) runs.push(new TextRun(`: ${documentation}`, theme.textSecondary));
        return runs;
    }
}

