import { $eq, Anchored, Box, BoxStyle, BuildContext, CodeBlock$CodeMetrics, CodeCompletionView, CodeEditorController, CodeGoTo, CodeGoToEntry, CodeGoToModeValue, CodeGoToNotFound, CodeLineCells, CodeSymbolKindValue, Column, CornerRadii, EdgeInsets, LiveRegion, Pressable, Row, SdkStrings, SizeValue, StyleDiff, Text, TextEntry, VisualNode } from "../runtime-exports";

export class CodeGoToView {
    static maxWidth: number = 560;
    static pageRows: number = 10;

    static build(context: BuildContext, editor: CodeEditorController, metrics: CodeBlock$CodeMetrics, width: number, top: number, typed: (string: string) => void, accept: () => void, dismiss: () => void, pick: (int: number) => void) {
        let goTo = editor.goTo;
        let theme = context.theme;
        let places = CodeGoToView.lists(goTo.mode);
        let listed = (goTo.mode === 'symbol' || places) && $eq.collections.count(goTo.entries) > 0;
        let field = new TextEntry(goTo.query, typed, { label: places ? CodeGoToView.title(goTo, $eq.collections.count(goTo.places)) : SdkStrings.goTo, placeholder: places ? SdkStrings.filterPlaces : SdkStrings.goToStart, autofocus: true, onSubmit: accept, onFocusChanged: (focused: boolean) => {
            if (!focused) dismiss();
        } });
        let panel = listed ? CodeGoToView.list(theme, metrics, goTo, width, top, pick) : CodeGoToView.said(theme, metrics, CodeGoToView.saying(editor));
        let coated = new Box(new BoxStyle({ width: SizeValue.fill, background: theme.surface, borderWidth: 1, borderColor: theme.border, cornerRadius: new CornerRadii(theme.shape('medium')), padding: EdgeInsets.symmetric(8, 4), shadow: theme.elevation(2) }), field);
        return new Box(new BoxStyle({ width: width }), new Anchored(coated, panel, { open: true, panelRole: listed ? 'listbox' : 'none', activeIndex: listed ? goTo.selected - top : -1, matchAnchorWidth: true }));
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
        if (CodeGoToView.lists(goTo.mode)) return SdkStrings.noMatchingPlaces;
        return SdkStrings.goToStart;
    }

    static lists(mode: CodeGoToModeValue) {
        return mode === 'definitions' || mode === 'references';
    }

    static title(goTo: CodeGoTo, count: number) {
        return goTo.mode === 'references' ? SdkStrings.referencesCount(count) : (() => { const $s = goTo.definitionKind; if ($s === 'declaration') return SdkStrings.declarationsCount(count); if ($s === 'typeDefinition') return SdkStrings.typeDefinitionsCount(count); if ($s === 'implementation') return SdkStrings.implementationsCount(count); return SdkStrings.definitionsCount(count); })();
    }

    static notFoundText(nothing: CodeGoToNotFound) {
        let word = nothing.word;
        let named = word.length > 0;
        if (nothing.mode === 'references') return named ? SdkStrings.noReferencesFound(word) : SdkStrings.noReferences;
        return (() => { const $s = nothing.kind; if ($s === 'declaration') return named ? SdkStrings.noDeclarationFound(word) : SdkStrings.noDeclaration; if ($s === 'typeDefinition') return named ? SdkStrings.noTypeDefinitionFound(word) : SdkStrings.noTypeDefinition; if ($s === 'implementation') return named ? SdkStrings.noImplementationFound(word) : SdkStrings.noImplementation; return named ? SdkStrings.noDefinitionFound(word) : SdkStrings.noDefinition; })();
    }

    static said(theme: any, metrics: CodeBlock$CodeMetrics, saying: string) {
        return new LiveRegion(CodeGoToView.panel(theme, new Box(new BoxStyle({ width: SizeValue.fill, padding: EdgeInsets.symmetric(8, 4) }), new Text(saying, 'labelSmall', theme.textMuted, 0, 'start', false, false, null, 0, { styleOverride: $eq.withPatch(metrics.style, { mono: false }) }))));
    }

    static list(theme: any, metrics: CodeBlock$CodeMetrics, goTo: CodeGoTo, width: number, top: number, pick: (int: number) => void) {
        let entries = goTo.entries;
        let columns = $eq.num.saturate(Math.floor(Math.fround(Math.fround(Math.fround(Math.fround(width - Math.fround(2 * 8)) - metrics.lineHeight) - 4) / metrics.columnWidth)));
        let page = new Column(0, 'start', 'stretch', false, null, null, { width: SizeValue.fill });
        let heading = CodeGoToView.lists(goTo.mode) ? CodeGoToView.title(goTo, $eq.collections.count(entries)) : SdkStrings.symbolsCount($eq.collections.count(entries));
        page.add(new Box(new BoxStyle({ width: SizeValue.fill, padding: EdgeInsets.symmetric(8, 4) }), new Text(heading, 'labelSmall', theme.textMuted, 1)));
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
        let kind: any; 
        if ((kind = entry.kind) != null) row.add(CodeCompletionView.glyph(theme, metrics, CodeGoToView.kindOf(kind)));
        row.add(new Text(name, 'labelSmall', ink, 1, 'start', false, false, null, 0, { mono: true, styleOverride: metrics.style, spans: CodeCompletionView.marked(name, entry.matches, ink, accent) }));
        let room = columns - CodeLineCells.widthOf(name, 4) - 2;
        let held: any; 
        if ((($0) => (($0 != null && $0.length > 0) && (held = $0, true)))(entry.container) && room >= 2) {
            let container = CodeCompletionView.fit(held, room);
            row.add(new Text(container, 'labelSmall', theme.textMuted, 1, 'start', false, false, null, 0, { mono: true, styleOverride: metrics.style, spans: CodeCompletionView.marked(container, entry.containerMatches, theme.textMuted, accent) }));
        }
        let container2: any; 
        return new Pressable(new Box(new BoxStyle({ width: SizeValue.fill, height: metrics.lineHeight, padding: EdgeInsets.symmetric(8, 0), background: selected ? theme.colors('primary').subtle : null, hover: selected ? null : new StyleDiff({ background: theme.surfaceSubtle }) }), row), pressed, { role: 'option', selected: selected, canRequestFocus: false, label: (($0) => (($0 != null && $0.length > 0) && (container2 = $0, true)))(entry.container) ? entry.name + ', ' + container2 : entry.name });
    }

    static kindOf(kind: CodeSymbolKindValue) {
        return (() => { const $s = kind; if ($s === 'file') return 'file'; if ((($s === 'module' || $s === 'namespace') || $s === 'package')) return 'module'; if ($s === 'class') return 'class'; if ($s === 'method') return 'method'; if ($s === 'property') return 'property'; if ($s === 'field') return 'field'; if ($s === 'constructor') return 'constructor'; if ($s === 'enum') return 'enum'; if ($s === 'interface') return 'interface'; if ($s === 'function') return 'function'; if ($s === 'variable') return 'variable'; if ($s === 'constant') return 'constant'; if ($s === 'enumMember') return 'enumMember'; if ($s === 'struct') return 'struct'; if ($s === 'event') return 'event'; if ($s === 'operator') return 'operator'; if ($s === 'typeParameter') return 'typeParameter'; return 'value'; })();
    }
}

