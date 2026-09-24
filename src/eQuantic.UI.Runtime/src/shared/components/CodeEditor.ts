import { $eq, Box, BoxStyle, BuildContext, CodeBlock, CodeDecoration, CodeEditorController, CodeGrid, CodeGutterMarker, CodeLanguages, CodeRange, CodeSurface, CornerRadii, EdgeInsets, Flexible, Icon, IconButton, IconGlyph, KeyChord, Point, Positioned, Row, ScrollView, SdkStrings, Shortcut, Size, SizeValue, SizeVariantValue, Stack, StatefulComponent, Text, TextEntry, UiComponent, VisualNode } from "../runtime-exports";

export class CodeEditor extends StatefulComponent {
    static $typeId = 'eQuantic.UI.Components.CodeEditor';
    _editor: any = null;
    _findOpen: boolean = false;
    _findText: string = '';
    _offset: number = 0;
    _viewport: number = 0;
    _viewportWidth: number = 0;
    _toldDocument: any = null;
    _toldSelection: CodeRange = new CodeRange();
    _matches: CodeRange[] = [];
    _matchedIn: any = null;
    _matchedFor: any = null;
    _matchedCase: boolean = false;

    static get $hydration() {
        return { _offset: 'single', _viewport: 'single', _viewportWidth: 'single', height: { of: SizeValue, members: { value: 'single' } }, maxHeight: 'single' };
    }

    declare initialCode: string;
    declare languageName: any;
    declare onChanged: ((string: string) => void) | null;
    declare onSelectionChanged: any;
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
    declare onGutterPressed: any;

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
        this.initialCode = code;
        this.languageName = language;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let editor = this.editor;
        editor.readOnly = this.readOnly;
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
        let block = new CodeBlock('', null, { document: editor.document, language: highlighter.language, decorations: this.marks(editor, matches, first, last), showLineNumbers: this.showLineNumbers, firstLineNumber: this.firstLineNumber, standalone: false, size: this.size, inverse: this.inverse, gutterMarkers: this.gutterMarkers, onGutterPressed: this.onGutterPressed, highlighter: highlighter, metrics: metrics, viewportOffset: this._offset, viewportHeight: this._viewport, viewportWidth: this._viewportWidth, activeLine: editor.caret.line, selectionBands: editor.selectionBandsIn(first, last), widestLine: editor.widestLine });
        let surface: VisualNode = new CodeSurface(block, editor, { autofocus: this.autofocus, label: this.caption ?? SdkStrings.codeEditor, caretColor: CodeBlock.inkFor(this.inverse, context.theme), onChanged: () => this.setState(() => this.notify(editor)) });
        let viewport: VisualNode = new ScrollView(surface, 'horizontal', { width: SizeValue.fill, onViewportChanged: (width: number) => {
            if (Math.abs(Math.fround(width - this._viewportWidth)) < 1) return;
            this.setState(() => this._viewportWidth = width);
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
        surface = new Shortcut(surface, new KeyChord('f', 4), () => this.setState(() => this._findOpen = true));
        let capped = bounded && this.maxHeight > 0;
        let layers = new Stack('topStart', { width: SizeValue.fill, height: SizeValue.fill });
        layers.add(surface);
        let corner: any; 
        if ((corner = CodeBlock.corner(this.caption, null, this.inverse, context.theme)) != null) layers.add(corner);
        if (this._findOpen) {
            let found = this._findText.length > 0 ? matches : [];
            layers.add(new Positioned(new Shortcut(this.findBar(context, editor, found), KeyChord.escape, () => this.closeFind(editor)), 8, 8));
        }
        return new Box(new BoxStyle({ width: SizeValue.fill, height: this.height, maxHeight: capped ? SizeValue.fixed(this.maxHeight) : SizeValue.hug }), layers);
    }

    create() {
        let editor = new CodeEditorController(this.initialCode, CodeLanguages.for(this.languageName), { readOnly: this.readOnly });
        this._toldDocument = editor.document;
        this._toldSelection = editor.selection;
        return editor;
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

    marks(editor: CodeEditorController, matches: CodeRange[], first: number, last: number) {
        if (matches.length === 0 && !this.matchBrackets && editor.composition == null) return this.decorations;
        let marks: CodeDecoration[] = [...this.decorations];
        let composition: any; 
        if ((composition = editor.composition) != null) marks.push(new CodeDecoration(composition, 'underline'));
        if (matches.length > 0) {
            let current = editor.selection;
            for (let i = CodeEditor.firstEndingOnOrAfter(matches, first); i < matches.length && matches[i].start.line <= last; i++) {
                let match = matches[i];
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
        let high = matches.length;
        while (low < high) {
            let middle = Math.trunc((low + high) / 2);
            if (matches[middle].end.line < line) low = middle + 1; else high = middle;
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

    findBar(context: any, editor: CodeEditorController, matches: CodeRange[]) {
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
        let high = matches.length;
        while (low < high) {
            let middle = Math.trunc((low + high) / 2);
            if (matches[middle].start.compareTo(current) < 0) low = middle + 1; else high = middle;
        }
        if (low < matches.length && $eq.equals(matches[low].start, current)) index = low + 1;
        let row = new Row(8, 'start', 'center', false, null, null, { cross: 'center' });
        row.add(new Box(new BoxStyle({ width: 168 }), new TextEntry(this._findText, (value: string) => this.setState(() => this._findText = value), { placeholder: SdkStrings.find, label: SdkStrings.find, autofocus: true, onSubmit: () => step(true) })));
        row.add(new Text(matches.length === 0 ? this._findText.length === 0 ? '' : '0' : `${index}/${matches.length}`, 'labelSmall', theme.textMuted, 1, 'start', false, false, null, 0, { tabular: true }));
        row.add(new IconButton(new Icon(IconGlyph.fromIcons('chevronUp')), SdkStrings.previousMatch, 'standard', 'medium', null, { size: 'small', onPressed: () => step(false) }));
        row.add(new IconButton(new Icon(IconGlyph.fromIcons('chevronDown')), SdkStrings.nextMatch, 'standard', 'medium', null, { size: 'small', onPressed: () => step(true) }));
        row.add(new IconButton(new Icon(IconGlyph.fromIcons('close')), SdkStrings.closeFind, 'standard', 'medium', null, { size: 'small', onPressed: () => this.closeFind(editor) }));
        return new Box(new BoxStyle({ background: theme.surface, borderWidth: 1, borderColor: theme.border, cornerRadius: new CornerRadii(theme.shape('medium')), padding: EdgeInsets.symmetric(8, 4), shadow: theme.elevation(2) }), row);
    }
}

