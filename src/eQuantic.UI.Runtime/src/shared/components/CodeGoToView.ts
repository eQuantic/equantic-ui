import { $eq, Anchored, Box, BoxStyle, BuildContext, CodeBlock$CodeMetrics, CodeCompletionView, CodeEditorController, CodeGoTo, CodeGoToEntry, CodeLineCells, CodeSymbolKindValue, Column, CornerRadii, EdgeInsets, LiveRegion, Pressable, Row, SdkStrings, SizeValue, StyleDiff, Text, TextEntry, VisualNode } from "../runtime-exports";

export class CodeGoToView {
    static maxWidth: number = 560;
    static pageRows: number = 10;

    static build(context: BuildContext, editor: CodeEditorController, metrics: CodeBlock$CodeMetrics, width: number, top: number, typed: (string: string) => void, accept: () => void, close: () => void, pick: (int: number) => void) {
        let goTo = editor.goTo;
        let theme = context.theme;
        let listed = goTo.mode === 'symbol' && $eq.collections.count(goTo.entries) > 0;
        let field = new TextEntry(goTo.query, typed, { label: SdkStrings.goTo, placeholder: SdkStrings.goToStart, autofocus: true, onSubmit: accept });
        let panel = listed ? CodeGoToView.list(theme, metrics, goTo, width, top, pick) : CodeGoToView.said(theme, metrics, CodeGoToView.saying(editor));
        let coated = new Box(new BoxStyle({ width: SizeValue.fill, background: theme.surface, borderWidth: 1, borderColor: theme.border, cornerRadius: new CornerRadii(theme.shape('medium')), padding: EdgeInsets.symmetric(8, 4), shadow: theme.elevation(2) }), field);
        return new Box(new BoxStyle({ width: width }), new Anchored(coated, panel, { open: true, panelRole: listed ? 'listbox' : 'none', activeIndex: listed ? goTo.selected - top : -1, matchAnchorWidth: true, onDismiss: close }));
    }

    static saying(editor: CodeEditorController) {
        let goTo = editor.goTo;
        if (goTo.mode === 'line') {
            let line: any; 
            if (!((line = goTo.line) != null)) return SdkStrings.goToLinePrompt(editor.document.lineCount);
            let column: any; 
            if ((column = line.column) != null) return SdkStrings.goToPosition(line.line, column);
            return line.columnStarted ? SdkStrings.goToColumnPrompt(line.line, line.columns) : SdkStrings.goToLineOrColumn(line.line);
        }
        if (goTo.mode === 'symbol') {
            if ($eq.collections.count(editor.symbols.providers) === 0) return SdkStrings.noSymbolProvider;
            if (goTo.isAsking) return SdkStrings.readingSymbols;
            return $eq.text.trim($eq.text.substring(goTo.query, 1)).length === 0 ? SdkStrings.noSymbols : SdkStrings.noMatchingSymbols;
        }
        return SdkStrings.goToStart;
    }

    static said(theme: any, metrics: CodeBlock$CodeMetrics, saying: string) {
        return new LiveRegion(CodeGoToView.panel(theme, new Box(new BoxStyle({ width: SizeValue.fill, padding: EdgeInsets.symmetric(8, 4) }), new Text(saying, 'labelSmall', theme.textMuted, 0, 'start', false, false, null, 0, { styleOverride: $eq.withPatch(metrics.style, { mono: false }) }))));
    }

    static list(theme: any, metrics: CodeBlock$CodeMetrics, goTo: CodeGoTo, width: number, top: number, pick: (int: number) => void) {
        let entries = goTo.entries;
        let columns = (Math.trunc(Math.floor(Math.fround(Math.fround(Math.fround(Math.fround(width - Math.fround(2 * 8)) - metrics.lineHeight) - 4) / metrics.columnWidth))) | 0);
        let page = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        page.add(new Box(new BoxStyle({ width: SizeValue.fill, padding: EdgeInsets.symmetric(8, 4) }), new Text(SdkStrings.symbolsCount($eq.collections.count(entries)), 'labelSmall', theme.textMuted, 1)));
        for (let i = top; i < top + CodeGoToView.pageRows && i < $eq.collections.count(entries); i++) {
            let index = i;
            page.add(CodeGoToView.entry(theme, metrics, $eq.collections.item(entries, i), i === goTo.selected, columns, () => pick(index)));
        }
        return CodeGoToView.panel(theme, page);
    }

    static panel(theme: any, content: VisualNode) {
        return new Box(new BoxStyle({ width: SizeValue.fill, background: theme.surface, cornerRadius: new CornerRadii(theme.shape('medium')), borderWidth: 1, borderColor: theme.border, elevation: 2, padding: EdgeInsets.symmetric(0, 4), clip: true }), content);
    }

    static entry(theme: any, metrics: CodeBlock$CodeMetrics, entry: CodeGoToEntry, selected: boolean, columns: number, pressed: () => void) {
        let ink = entry.deprecated ? theme.textMuted : selected ? theme.colors('primary').onSubtle : theme.textPrimary;
        let accent = theme.colors('primary').base;
        let name = CodeCompletionView.fit(entry.name, columns);
        let row = new Row(4, 'start', 'center', false, null, null, { width: SizeValue.fill, height: SizeValue.fill });
        row.add(CodeCompletionView.glyph(theme, metrics, CodeGoToView.kindOf(entry.kind)));
        row.add(new Text(name, 'labelSmall', ink, 1, 'start', false, false, null, 0, { mono: true, styleOverride: metrics.style, spans: CodeCompletionView.marked(name, entry.matches, ink, accent) }));
        let room = columns - CodeLineCells.widthOf(name, 4) - 2;
        let held: any; 
        if (((entry.container != null && entry.container.length > 0) && (held = entry.container, true)) && room >= 2) {
            let container = CodeCompletionView.fit(held, room);
            row.add(new Text(container, 'labelSmall', theme.textMuted, 1, 'start', false, false, null, 0, { mono: true, styleOverride: metrics.style, spans: CodeCompletionView.marked(container, entry.containerMatches, theme.textMuted, accent) }));
        }
        let container2: any; 
        return new Pressable(new Box(new BoxStyle({ width: SizeValue.fill, height: metrics.lineHeight, padding: EdgeInsets.symmetric(8, 0), background: selected ? theme.colors('primary').subtle : null, hover: selected ? null : new StyleDiff({ background: theme.surfaceSubtle }) }), row), pressed, { role: 'option', selected: selected, canRequestFocus: false, label: ((entry.container != null && entry.container.length > 0) && (container2 = entry.container, true)) ? entry.name + ', ' + container2 : entry.name });
    }

    static kindOf(kind: CodeSymbolKindValue) {
        return (() => { const $s = kind; if ($s === 'file') return 'file'; if ((($s === 'module' || $s === 'namespace') || $s === 'package')) return 'module'; if ($s === 'class') return 'class'; if ($s === 'method') return 'method'; if ($s === 'property') return 'property'; if ($s === 'field') return 'field'; if ($s === 'constructor') return 'constructor'; if ($s === 'enum') return 'enum'; if ($s === 'interface') return 'interface'; if ($s === 'function') return 'function'; if ($s === 'variable') return 'variable'; if ($s === 'constant') return 'constant'; if ($s === 'enumMember') return 'enumMember'; if ($s === 'struct') return 'struct'; if ($s === 'event') return 'event'; if ($s === 'operator') return 'operator'; if ($s === 'typeParameter') return 'typeParameter'; return 'value'; })();
    }
}

