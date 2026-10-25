import { $eq, Box, BoxStyle, BuildContext, CodeBlock, CodeCompletion, CodeCompletionView, CodeDecoration, CodeDiagnostic, CodeEditorController, CodeGrid, CodeGutterMarker, CodeHoverCard, CodeKeyBinding, CodeKeywordCompletionProvider, CodeLanguages, CodeRange, CodeSignatureCard, CodeSurface, CodeWordCompletionProvider, CornerRadii, EdgeInsets, Flexible, Icon, IconButton, IconGlyph, KeyChord, Point, Positioned, Row, ScrollView, SdkStrings, Shortcut, Size, SizeValue, SizeVariantValue, Stack, StatefulComponent, Text, TextEntry, UiComponent, VisualNode } from "../runtime-exports";

export class CodeEditor extends StatefulComponent {
    static $typeId = 'eQuantic.UI.Components.CodeEditor';
    _editor: any = null;
    _findOpen: boolean = false;
    _findText: string = '';
    _offset: number = 0;
    _viewport: number = 0;
    _viewportWidth: number = 0;
    _scrollX: number = 0;
    _listTop: number = 0;
    _listColumns: number = 0;
    _measured: any = null;
    _handed: any = null;
    _handedAny: boolean = false;
    _put: any[] = [];
    _reported: any = null;
    _diagnosers: any[] = [];
    _explainers: any[] = [];
    _signers: any[] = [];
    _bindings: CodeKeyBinding[] = [];
    _toldDocument: any = null;
    _toldSelection: CodeRange = CodeRange.$zero();
    _matches: CodeRange[] = [];
    _matchedIn: any = null;
    _matchedFor: any = null;
    _matchedCase: boolean = false;
    declare initialCode: string;
    declare languageName: any;
    declare onChanged: ((string: string) => void) | null;
    declare onSelectionChanged: ((codeRange: CodeRange) => void) | null;
    declare showLineNumbers: boolean;
    declare firstLineNumber: number;
    declare height: SizeValue;
    declare maxHeight: number;
    declare size: SizeVariantValue;
    declare inverse: boolean;
    declare readOnly: boolean;
    declare autofocus: boolean;
    declare caption: any;
    declare gutterMarkers: CodeGutterMarker[];
    declare decorations: CodeDecoration[];
    declare matchBrackets: boolean;
    declare search: any;
    declare searchMatchCase: boolean;
    declare onGutterPressed: ((int: number) => void) | null;
    declare completions: any;
    declare diagnostics: CodeDiagnostic[];
    declare diagnosticProviders: any[];
    declare hoverProviders: any[];
    declare signatureProviders: any[];
    declare keyBindings: CodeKeyBinding[];

    get editor() {
        return this._editor ?? (this._editor = this.create());
    }

    get needle() {
        return this._findOpen && this._findText.length > 0 ? this._findText : this.search;
    }

    constructor(code: any = '', language: any = null, props?: any) {
        super();
        if (this.showLineNumbers === undefined) this.showLineNumbers = true;
        if (this.firstLineNumber === undefined) this.firstLineNumber = 1;
        if (this.height === undefined) this.height = SizeValue.hug;
        if (this.maxHeight === undefined) this.maxHeight = 0;
        if (this.size === undefined) this.size = 'small';
        if (this.inverse === undefined) this.inverse = false;
        if (this.readOnly === undefined) this.readOnly = false;
        if (this.autofocus === undefined) this.autofocus = false;
        if (this.gutterMarkers === undefined) this.gutterMarkers = [];
        if (this.decorations === undefined) this.decorations = [];
        if (this.matchBrackets === undefined) this.matchBrackets = true;
        if (this.searchMatchCase === undefined) this.searchMatchCase = false;
        if (this.diagnostics === undefined) this.diagnostics = [];
        if (this.diagnosticProviders === undefined) this.diagnosticProviders = [];
        if (this.hoverProviders === undefined) this.hoverProviders = [];
        if (this.signatureProviders === undefined) this.signatureProviders = [];
        if (this.keyBindings === undefined) this.keyBindings = [];
        this.initialCode = code;
        this.languageName = language;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let editor = this.editor;
        editor.readOnly = this.readOnly;
        this.handProviders(editor.completion);
        this.handIntelligence(editor);
        this.handKeyBindings(editor);
        let highlighter = editor.highlighter;
        let metrics = CodeBlock.metricsFor(context, this.size, this.showLineNumbers, this.firstLineNumber + editor.document.lineCount - 1);
        editor.grid = new CodeGrid(new Point(metrics.contentLeft, metrics.contentTop), new Size(metrics.columnWidth, metrics.lineHeight));
        let bounded = this.height.kind !== 'hug';
        let windowed = bounded || this.maxHeight > 0;
        if (!windowed) {
            this._offset = 0;
            this._viewport = 0;
        }
        let [first, last] = CodeBlock.windowOf(editor.document.lineCount, metrics.lineHeight, this._offset, this._viewport);
        let matches = this.matchesOf(editor, this.needle);
        let block = new CodeBlock('', null, { document: editor.document, language: highlighter.language, decorations: this.marks(editor, matches, first, last, context.theme), showLineNumbers: this.showLineNumbers, firstLineNumber: this.firstLineNumber, standalone: false, size: this.size, inverse: this.inverse, gutterMarkers: this.gutter(editor, first, last), onGutterPressed: this.onGutterPressed, highlighter: highlighter, metrics: metrics, viewportOffset: this._offset, viewportHeight: this._viewport, viewportWidth: this._viewportWidth, minHeight: bounded ? this._viewport : 0, activeLines: editor.caretLinesIn(first, last), selectionBands: editor.selectionBandsIn(first, last), selections: editor.selectionsIn(first, last), widestLine: editor.widestLine });
        let completion = editor.completion;
        let offered: VisualNode | null = null;
        let offeredAt = Point.zero;
        let highlighted = -1;
        let listAbove: boolean | null = null;
        let codeHeight = Math.fround(Math.fround(2 * metrics.contentTop) + Math.fround(Math.fround(editor.document.lineCount) * metrics.lineHeight));
        let surfaceHeight = bounded ? Math.max(codeHeight, this._viewport) : codeHeight;
        let viewTop = windowed ? this._offset : 0;
        let viewBottom = windowed && this._viewport > 0 ? Math.min(Math.fround(this._offset + this._viewport), surfaceHeight) : surfaceHeight;
        if (completion.isOpen && completion.selected >= 0) {
            let items = completion.items;
            if (!(items === this._measured)) {
                this._measured = items;
                this._listColumns = Math.max(this._listColumns, CodeCompletionView.columnsOf(items));
            }
            this._listColumns = Math.max(this._listColumns, CodeCompletionView.entryColumns($eq.collections.item(items, completion.selected).item));
            let width = CodeCompletionView.widthOf(metrics, this._listColumns);
            if (this._viewportWidth > 0) width = Math.min(width, this._viewportWidth);
            let documentation = $eq.collections.item(items, completion.selected).item.documentation;
            let documentationLines = (documentation != null && documentation.length > 0) ? CodeCompletionView.documentationLinesOf(context, metrics, documentation, width) : 0;
            let [x, y, rows, above, documentationShown] = CodeCompletionView.place(metrics, editor.caretRect(completion.start), viewTop, viewBottom, this._scrollX, this._viewportWidth, Math.min(12, $eq.collections.count(items)), width, documentationLines, CodeCompletionView.documentationLineOf(context, metrics));
            let selected = completion.selected;
            if (selected < this._listTop) this._listTop = selected;
            if (selected >= this._listTop + rows) this._listTop = selected - rows + 1;
            this._listTop = Math.max(0, Math.min(this._listTop, $eq.collections.count(items) - rows));
            completion.pageSize = rows;
            offered = CodeCompletionView.build(context, completion, metrics, this._listTop, rows, width, above, documentation, documentationShown, (index: number) => this.pick(editor, index));
            offeredAt = new Point(x, y);
            highlighted = selected - this._listTop;
            listAbove = above;
        } else {
            this._listTop = 0;
            this._listColumns = 0;
            this._measured = null;
        }
        let notes: Positioned[] = [];
        let hover = editor.hover;
        if (hover.isOpen && offered == null) {
            let [card, cardWidth, cardHeight] = CodeHoverCard.build(context, hover, metrics, this._viewportWidth);
            let [x, y] = CodeHoverCard.place(editor.caretRect(hover.range.start), viewTop, this._scrollX, this._viewportWidth, cardWidth, cardHeight);
            notes.push(new Positioned(card, y, null, null, x));
        }
        let signatures = editor.signatureHelp;
        if (signatures.isOpen && !(signatures.signature == null) && !hover.isOpen) {
            let [panel, panelWidth, panelHeight] = CodeSignatureCard.build(context, signatures, metrics, this._viewportWidth);
            let at: any; 
            if ((at = CodeSignatureCard.place(editor.caretRect(editor.caret), viewTop, viewBottom, this._scrollX, this._viewportWidth, panelWidth, panelHeight, listAbove)) != null) notes.push(new Positioned(panel, at.y, null, null, at.x));
        }
        let surface: VisualNode = new CodeSurface(block, editor, { autofocus: this.autofocus, label: this.caption ?? SdkStrings.codeEditor, caretColor: CodeBlock.inkFor(this.inverse, context.theme), onChanged: () => this.setState(() => this.notify(editor)), options: offered, optionsOrigin: offeredAt, highlightedOption: highlighted, notes: notes });
        let viewport: VisualNode = new ScrollView(surface, 'horizontal', { width: SizeValue.fill, onViewportChanged: (width: number) => {
            if (Math.abs(Math.fround(width - this._viewportWidth)) < 1) return;
            this.setState(() => this._viewportWidth = width);
        }, onScrolled: (offset: number) => {
            if (Math.abs(Math.fround(offset - this._scrollX)) < 1) return;
            if (editor.completion.isOpen || editor.hover.isOpen || editor.signatureHelp.isOpen) this.setState(() => this._scrollX = offset); else this._scrollX = offset;
        } });
        if (this.showLineNumbers) {
            let withGutter = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill, cross: 'start' });
            withGutter.add(block.gutter(context));
            withGutter.add(new Flexible(viewport));
            viewport = withGutter;
        }
        if (windowed) {
            viewport = new Box(new BoxStyle({ width: SizeValue.fill, height: bounded ? SizeValue.fill : SizeValue.hug, maxHeight: !bounded && this.maxHeight > 0 ? SizeValue.fixed(this.maxHeight) : SizeValue.hug }), new ScrollView(viewport, 'vertical', { width: SizeValue.fill, height: bounded ? SizeValue.fill : SizeValue.hug, onScrolled: (offset: number) => {
                if (Math.abs(Math.fround(offset - this._offset)) < 1) return;
                this.setState(() => this._offset = offset);
            }, onViewportChanged: (height: number) => {
                if (Math.abs(Math.fround(height - this._viewport)) < 1) return;
                this.setState(() => this._viewport = height);
            } }));
        }
        surface = new Box(new BoxStyle({ width: SizeValue.fill, height: bounded ? SizeValue.fill : SizeValue.hug, background: CodeBlock.surfaceFor(this.inverse, context.theme), cornerRadius: new CornerRadii(context.theme.shape('medium')), clip: true }), viewport);
        let capped = bounded && this.maxHeight > 0;
        let layers = new Stack('topStart', { width: SizeValue.fill, height: SizeValue.fill });
        layers.add(surface);
        let corner: any; 
        if ((corner = CodeBlock.corner(this.caption, null, this.inverse, context.theme)) != null) layers.add(corner);
        if (this._findOpen) {
            let found = this._findText.length > 0 ? matches : [];
            layers.add(new Positioned(this.findBar(context, editor, found), 8, 8));
        }
        let closable = new Shortcut(layers, KeyChord.escape, () => this.closeFind(editor), { focusScoped: true, enabled: this._findOpen });
        let findable = new Shortcut(closable, new KeyChord('f', 4), () => this.setState(() => this._findOpen = true), { focusScoped: true });
        return new Box(new BoxStyle({ width: SizeValue.fill, height: this.height, maxHeight: capped ? SizeValue.fixed(this.maxHeight) : SizeValue.hug }), findable);
    }

    create() {
        let $n0: any; 
        let editor = ($n0 = new CodeEditorController(this.initialCode, CodeLanguages.for(this.languageName)), $n0.readOnly = this.readOnly, $n0);
        this._toldDocument = editor.document;
        this._toldSelection = editor.selection;
        editor.completion.changed = $eq.delegates.combine(editor.completion.changed, () => this.setState(() => {}));
        editor.diagnostics.changed = $eq.delegates.combine(editor.diagnostics.changed, () => this.setState(() => {}));
        editor.hover.changed = $eq.delegates.combine(editor.hover.changed, () => this.setState(() => {}));
        editor.signatureHelp.changed = $eq.delegates.combine(editor.signatureHelp.changed, () => this.setState(() => {}));
        editor.snippet.changed = $eq.delegates.combine(editor.snippet.changed, () => this.setState(() => {}));
        return editor;
    }

    handIntelligence(editor: CodeEditorController) {
        if (this._reported == null || !CodeEditor.sameProblems(this.diagnostics, this._reported)) {
            this._reported = [...this.diagnostics];
            editor.diagnostics.report(this._reported);
        }
        if (!CodeEditor.sameProviders(this.diagnosticProviders, this._diagnosers)) {
            for (const provider of this._diagnosers) {
                let at = CodeEditor.indexOfInstance(editor.diagnostics.providers, provider);
                if (at >= 0) editor.diagnostics.providers.splice(at, 1);
            }
            this._diagnosers = [...this.diagnosticProviders];
            for (const provider of this._diagnosers) $eq.collections.add(editor.diagnostics.providers, provider);
            editor.diagnostics.check();
        }
        if (!CodeEditor.sameProviders(this.hoverProviders, this._explainers)) {
            for (const provider of this._explainers) {
                let at = CodeEditor.indexOfInstance(editor.hover.providers, provider);
                if (at >= 0) editor.hover.providers.splice(at, 1);
            }
            this._explainers = [...this.hoverProviders];
            for (const provider of this._explainers) $eq.collections.add(editor.hover.providers, provider);
            editor.hover.dismiss();
        }
        if (!CodeEditor.sameProviders(this.signatureProviders, this._signers)) {
            for (const provider of this._signers) {
                let at = CodeEditor.indexOfInstance(editor.signatureHelp.providers, provider);
                if (at >= 0) editor.signatureHelp.providers.splice(at, 1);
            }
            this._signers = [...this.signatureProviders];
            for (const provider of this._signers) $eq.collections.add(editor.signatureHelp.providers, provider);
            editor.signatureHelp.dismiss();
        }
    }

    handKeyBindings(editor: CodeEditorController) {
        if (CodeEditor.sameBindings(this.keyBindings, this._bindings)) return;
        let list = editor.keyBindings;
        let at = CodeEditor.lastBlockAt(list, this._bindings);
        if (at >= 0) {
            for (let i = 0; i < this._bindings.length; i++) list.splice(at, 1);
        } else {
            for (let i = this._bindings.length - 1; i >= 0; i--) {
                let last = CodeEditor.lastIndexOf(list, this._bindings[i]);
                if (last >= 0) list.splice(last, 1);
            }
        }
        this._bindings = [...this.keyBindings];
        for (const binding of this._bindings) $eq.collections.add(list, binding);
    }

    static lastBlockAt(list: CodeKeyBinding[], block: CodeKeyBinding[]) {
        if (block.length === 0) return -1;
        for (let at = $eq.collections.count(list) - block.length; at >= 0; at--) {
            if (CodeEditor.holds(list, block, at)) return at;
        }
        return -1;
    }

    static holds(list: CodeKeyBinding[], block: CodeKeyBinding[], at: number) {
        if (at < 0 || at + block.length > $eq.collections.count(list)) return false;
        for (let i = 0; i < block.length; i++) {
            if (!$eq.equals($eq.collections.item(list, at + i), block[i])) return false;
        }
        return true;
    }

    static lastIndexOf(list: CodeKeyBinding[], binding: CodeKeyBinding) {
        for (let i = $eq.collections.count(list) - 1; i >= 0; i--) {
            if ($eq.equals($eq.collections.item(list, i), binding)) return i;
        }
        return -1;
    }

    static sameBindings(one: CodeKeyBinding[], other: CodeKeyBinding[]) {
        if ($eq.collections.count(one) !== $eq.collections.count(other)) return false;
        for (let i = 0; i < $eq.collections.count(one); i++) if (!$eq.equals($eq.collections.item(one, i), $eq.collections.item(other, i))) return false;
        return true;
    }

    static sameProblems(one: CodeDiagnostic[], other: CodeDiagnostic[]) {
        if ($eq.collections.count(one) !== $eq.collections.count(other)) return false;
        for (let i = 0; i < $eq.collections.count(one); i++) if (!$eq.equalsInstance($eq.collections.item(one, i), $eq.collections.item(other, i))) return false;
        return true;
    }

    handProviders(completion: CodeCompletion) {
        if (this._handedAny && CodeEditor.sameProviders(this.completions, this._handed)) return;
        this._handedAny = true;
        let providers = completion.providers;
        for (const provider of this._put) {
            let at = CodeEditor.indexOfInstance(providers, provider);
            if (at >= 0) providers.splice(at, 1);
        }
        this._put = this.completions == null ? [new CodeKeywordCompletionProvider(), new CodeWordCompletionProvider()] : [...this.completions];
        this._handed = this.completions == null ? null : this._put;
        for (const provider of this._put) $eq.collections.add(providers, provider);
    }

    static indexOfInstance(providers: any[], provider: any) {
        let at = 0;
        for (const held of providers) {
            if ((held === provider)) return at;
            at++;
        }
        return -1;
    }

    static sameProviders(one: any, other: any) {
        if (one == null || other == null) return one == null && other == null;
        if ($eq.collections.count(one) !== $eq.collections.count(other)) return false;
        for (let i = 0; i < $eq.collections.count(one); i++) {
            if (!($eq.collections.item(one, i) === $eq.collections.item(other, i))) return false;
        }
        return true;
    }

    pick(editor: CodeEditorController, index: number) {
        return this.setState(() => {
            if (editor.completion.select(index)) editor.completion.accept();
            this.notify(editor);
        });
    }

    notify(editor: CodeEditorController) {
        if (editor.document !== this._toldDocument) {
            this._toldDocument = editor.document;
            this.onChanged?.(editor.document.text);
        }
        if (!$eq.equals(editor.selection, this._toldSelection)) {
            this._toldSelection = editor.selection;
            this.onSelectionChanged?.(editor.selection);
        }
    }

    gutter(editor: CodeEditorController, first: number, last: number) {
        let problems = editor.diagnostics.items;
        if ($eq.collections.count(problems) === 0) return this.gutterMarkers;
        let worst: any = $eq.collections.dictionary();
        for (const problem of problems) {
            let severity = (($0) => (({ 'hint': 0, 'information': 1, 'warning': 2, 'error': 3 })[$0] ?? $0))(problem.severity);
            if (severity < 2) continue;
            let line = problem.range.start.line;
            if (line < first || line > last) continue;
            let known: any; 
            if (!(worst.has(line) ? ((known = worst.get(line)), true) : ((known = 0), false)) || severity > known) $eq.mapSet(worst, line, severity);
        }
        if (worst.size === 0) return this.gutterMarkers;
        let markers: CodeGutterMarker[] = $eq.collections.listCopy(this.gutterMarkers);
        for (const entry of worst) {
            if (markers.some((marker) => marker.line === entry.key)) continue;
            markers.push(new CodeGutterMarker(entry.key, entry.value === 3 ? 'error' : 'warning'));
        }
        return markers;
    }

    marks(editor: CodeEditorController, matches: CodeRange[], first: number, last: number, theme: any) {
        let problems = editor.diagnostics.items;
        if ($eq.collections.count(matches) === 0 && !this.matchBrackets && editor.composition == null && $eq.collections.count(problems) === 0 && !editor.snippet.isActive) return this.decorations;
        let marks: CodeDecoration[] = $eq.collections.listCopy(this.decorations);
        for (const placeholder of editor.snippet.placeholdersIn(first, last)) {
            let $n3: any; 
            marks.push(($n3 = new CodeDecoration(placeholder, 'highlight'), $n3.color = theme.colors('info').subtle, $n3));
        }
        for (const problem of problems) {
            if (problem.range.end.line < first || problem.range.start.line > last) continue;
            let $n4: any; 
            marks.push(($n4 = new CodeDecoration(problem.range, problem.severity === 'hint' ? 'dots' : 'squiggle'), $n4.color = CodeHoverCard.severityColor(theme, problem.severity), $n4));
        }
        let composition: any; 
        if ((composition = editor.composition) != null) marks.push(new CodeDecoration(composition, 'underline'));
        if ($eq.collections.count(matches) > 0) {
            let current = editor.selection;
            for (let i = CodeEditor.firstEndingOnOrAfter(matches, first); i < $eq.collections.count(matches) && $eq.collections.item(matches, i).start.line <= last; i++) {
                let match = $eq.collections.item(matches, i);
                marks.push(new CodeDecoration(match, $eq.equals(match.start, current.start) && $eq.equals(match.end, current.end) ? 'outline' : 'highlight'));
            }
        }
        let pair: any; 
        if (this.matchBrackets && (pair = editor.bracketAtCaret()) != null) {
            marks.push(new CodeDecoration(new CodeRange(pair[0], $eq.withPatch(pair[0], { column: pair[0].column + 1 })), 'outline'));
            marks.push(new CodeDecoration(new CodeRange(pair[1], $eq.withPatch(pair[1], { column: pair[1].column + 1 })), 'outline'));
        }
        return marks;
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; 
        if (!((next instanceof CodeEditor && (fresh = next, true)))) return;
        this.languageName = fresh.languageName;
        this.onChanged = fresh.onChanged;
        this.onSelectionChanged = fresh.onSelectionChanged;
        this.showLineNumbers = fresh.showLineNumbers;
        this.firstLineNumber = fresh.firstLineNumber;
        this.height = fresh.height;
        this.maxHeight = fresh.maxHeight;
        this.size = fresh.size;
        this.inverse = fresh.inverse;
        this.readOnly = fresh.readOnly;
        this.autofocus = fresh.autofocus;
        this.caption = fresh.caption;
        this.gutterMarkers = fresh.gutterMarkers;
        this.decorations = fresh.decorations;
        this.matchBrackets = fresh.matchBrackets;
        this.search = fresh.search;
        this.searchMatchCase = fresh.searchMatchCase;
        this.onGutterPressed = fresh.onGutterPressed;
        this.completions = fresh.completions;
        this.diagnostics = fresh.diagnostics;
        this.diagnosticProviders = fresh.diagnosticProviders;
        this.hoverProviders = fresh.hoverProviders;
        this.signatureProviders = fresh.signatureProviders;
        this.keyBindings = fresh.keyBindings;
    }

    matchesOf(editor: CodeEditorController, needle: any) {
        if (!((needle != null && needle.length > 0))) return [];
        if (editor.document !== this._matchedIn || needle !== this._matchedFor || this.searchMatchCase !== this._matchedCase) {
            this._matches = editor.findAll(needle, this.searchMatchCase);
            this._matchedIn = editor.document;
            this._matchedFor = needle;
            this._matchedCase = this.searchMatchCase;
        }
        return this._matches;
    }

    static firstEndingOnOrAfter(matches: CodeRange[], line: number) {
        let low = 0;
        let high = $eq.collections.count(matches);
        while (low < high) {
            let middle = Math.trunc((low + high) / 2);
            if ($eq.collections.item(matches, middle).end.line < line) low = middle + 1; else high = middle;
        }
        return low;
    }

    closeFind(editor: CodeEditorController) {
        return this.setState(() => {
            this._findOpen = false;
            this._findText = '';
            editor.requestFocus();
        });
    }

    findBar(context: BuildContext, editor: CodeEditorController, matches: CodeRange[]) {
        const step = (forward: boolean) => {
            if (this._findText.length === 0) return;
            let found: any; 
            if (!((found = editor.nextOf(this.matchesOf(editor, this._findText), !forward)) != null)) return;
            this.setState(() => {
                editor.selection = found;
                this.notify(editor);
            });
        };
        let theme = context.theme;
        let index = 0;
        let current = editor.selection.start;
        let low = 0;
        let high = $eq.collections.count(matches);
        while (low < high) {
            let middle = Math.trunc((low + high) / 2);
            if ($eq.collections.item(matches, middle).start.compareTo(current) < 0) low = middle + 1; else high = middle;
        }
        if (low < $eq.collections.count(matches) && $eq.equals($eq.collections.item(matches, low).start, current)) index = low + 1;
        let row = new Row(8, 'start', 'center', false, null, null, { cross: 'center' });
        row.add(new Box(new BoxStyle({ width: 168 }), new TextEntry(this._findText, (value: string) => this.setState(() => this._findText = value), { placeholder: SdkStrings.find, label: SdkStrings.find, autofocus: true, onSubmit: () => step(true) })));
        row.add(new Text($eq.collections.count(matches) === 0 ? this._findText.length === 0 ? '' : '0' : `${$eq.text.format(index, null, undefined, undefined, 'int32')}/${$eq.text.format($eq.collections.count(matches), null, undefined, undefined, 'int32')}`, 'labelSmall', theme.textMuted, 1, 'start', false, false, null, 0, { tabular: true }));
        row.add(new IconButton(new Icon(IconGlyph.fromIcons('chevronUp')), SdkStrings.previousMatch, 'standard', 'medium', null, { size: 'small', onPressed: () => step(false) }));
        row.add(new IconButton(new Icon(IconGlyph.fromIcons('chevronDown')), SdkStrings.nextMatch, 'standard', 'medium', null, { size: 'small', onPressed: () => step(true) }));
        row.add(new IconButton(new Icon(IconGlyph.fromIcons('close')), SdkStrings.closeFind, 'standard', 'medium', null, { size: 'small', onPressed: () => this.closeFind(editor) }));
        return new Box(new BoxStyle({ background: theme.surface, borderWidth: 1, borderColor: theme.border, cornerRadius: new CornerRadii(theme.shape('medium')), padding: EdgeInsets.symmetric(8, 4), shadow: theme.elevation(2) }), row);
    }
}

