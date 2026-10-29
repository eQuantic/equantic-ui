import { $eq, Box, BoxStyle, BuildContext, CodeBlock$CodeMetrics, CodeHoverCard, CodeSignature, CodeSignatureHelpSession, CodeSignatureParameter, Column, CornerRadii, EdgeInsets, Flexible, LiveRegion, Point, Rect, Row, SizeValue, Text, TextRun, VisualNode } from "../runtime-exports";

export class CodeSignatureCard {
    static build(context: BuildContext, session: CodeSignatureHelpSession, metrics: CodeBlock$CodeMetrics, viewWidth: number): [VisualNode, number, number] {
        let theme = context.theme;
        let signature = session.signature!;
        let prose = $eq.withPatch(metrics.style, { mono: false });
        let proseLine = prose.scaledLineHeight(context.typeScale);
        let codeLine = metrics.style.scaledLineHeight(context.typeScale);
        let chrome = Math.fround(2 * Math.fround(1 + 8));
        let count = $eq.collections.count((session.help!).signatures);
        let counter = count > 1 ? `${$eq.text.format(session.activeSignature + 1, null, undefined, undefined, 'int32')}/${$eq.text.format(count, null, undefined, undefined, 'int32')}` : null;
        let counterWidth = counter == null ? 0 : Math.fround(context.measureText(counter, prose) + 8);
        let active = session.activeParameter;
        let parameter = active >= 0 && active < $eq.collections.count(signature.parameters) ? $eq.collections.item(signature.parameters, active) : null;
        let about = parameter == null ? null : CodeSignatureCard.said(parameter);
        let wanted = Math.fround(CodeHoverCard.widest(context, signature.label, metrics.style) + counterWidth);
        if (!(about == null)) wanted = Math.max(wanted, CodeHoverCard.widest(context, about, prose));
        let documentation: any; 
        if ((($0) => (($0 != null && $0.length > 0) && (documentation = $0, true)))(signature.documentation)) wanted = Math.max(wanted, CodeHoverCard.widest(context, documentation, prose));
        let most = Math.fround(Math.fround(80 * metrics.columnWidth) + chrome);
        if (viewWidth > 0) most = Math.min(most, viewWidth);
        let width = Math.min(most, Math.fround(Math.ceil(wanted) + chrome));
        let room = Math.max(1, Math.fround(width - chrome));
        let column = new Column(4, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        let height = Math.fround(2 * Math.fround(1 + 4));
        let labelLines = CodeHoverCard.linesOf(context, signature.label, metrics.style, Math.fround(room - counterWidth));
        let first = new Row(8, 'start', 'start', false, null, null, { width: SizeValue.fill });
        first.add(new Flexible(new Text(signature.label, 'labelSmall', theme.textPrimary, labelLines, 'start', false, false, null, 0, { mono: true, styleOverride: metrics.style, spans: CodeSignatureCard.marked(theme, signature, active) })));
        if (!(counter == null)) first.add(new Text(counter, 'labelSmall', theme.textMuted, 1, 'start', false, false, null, 0, { styleOverride: prose }));
        column.add(first);
        height = Math.fround(height + Math.max(Math.fround(Math.fround(labelLines) * codeLine), counter == null ? 0 : proseLine));
        if (!(about == null)) {
            let lines = CodeHoverCard.linesOf(context, about, prose, room);
            column.add(new Text(about, 'labelSmall', theme.textSecondary, lines, 'start', false, false, null, 0, { styleOverride: prose, spans: CodeSignatureCard.described(theme, parameter!) }));
            height = Math.fround(height + Math.fround(4 + Math.fround(Math.fround(lines) * proseLine)));
        }
        let documented: any; 
        if ((($0) => (($0 != null && $0.length > 0) && (documented = $0, true)))(signature.documentation)) {
            let lines = CodeHoverCard.linesOf(context, documented, prose, room);
            column.add(new Text(documented, 'labelSmall', theme.textMuted, lines, 'start', false, false, null, 0, { styleOverride: prose }));
            height = Math.fround(height + Math.fround(4 + Math.fround(Math.fround(lines) * proseLine)));
        }
        let card: VisualNode = new Box(new BoxStyle({ width: width, background: theme.surface, cornerRadius: new CornerRadii(theme.shape('medium')), borderWidth: 1, borderColor: theme.border, elevation: 2, padding: EdgeInsets.symmetric(8, 4), clip: true }), column);
        return [new LiveRegion(card), width, height];
    }

    static place(caret: Rect, viewTop: number, viewBottom: number, viewLeft: number, viewWidth: number, width: number, height: number, listAbove: boolean | null) {
        let above = Math.fround(caret.y - height);
        let below = Math.fround(caret.y + caret.height);
        let roomAbove = above >= viewTop;
        let roomBelow = Math.fround(below + height) <= viewBottom;
        let y = null;
        if (listAbove == null) y = roomAbove ? above : below; else if (listAbove === false) {
            if (!roomAbove) return null;
            y = above;
        } else {
            if (!roomBelow) return null;
            y = below;
        }
        let x = caret.x;
        if (viewWidth > 0) x = Math.min(x, Math.fround(Math.fround(viewLeft + viewWidth) - width));
        x = Math.max(x, viewLeft);
        return new Point(x, y);
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
        return (($0) => (($0 != null && $0.length > 0) && (documentation = $0, true)))(parameter.documentation) ? `${parameter.label}: ${documentation}` : parameter.label;
    }

    static described(theme: any, parameter: CodeSignatureParameter) {
        let runs = [new TextRun(parameter.label, theme.textPrimary)];
        let documentation: any; 
        if ((($0) => (($0 != null && $0.length > 0) && (documentation = $0, true)))(parameter.documentation)) runs.push(new TextRun(`: ${documentation}`, theme.textSecondary));
        return runs;
    }
}

