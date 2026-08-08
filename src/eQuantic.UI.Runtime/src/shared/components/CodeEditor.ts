import { $eq, Box, BoxStyle, BuildContext, CodeBlock, CodeDecoration, CodeEditorController, CodeGutterMarker, CodeLanguages, CodeRange, CodeSurface, CornerRadii, EdgeInsets, IconButton, KeyChord, Positioned, Row, SharedStatefulComponent, Shortcut, SizeValue, Stack, Text, TextEntry, VisualNode } from "../runtime-exports";

export class CodeEditor extends SharedStatefulComponent {
    _editor: any;
    _findOpen: boolean = false;
    _findText: string = '';
    _offset: number = 0;
    _viewport: number = 0;
    declare initialCode: string;
    declare languageName: any;
    declare onChanged: ((string: string) => void) | null;
    declare onSelectionChanged: any;
    declare showLineNumbers: boolean;
    declare firstLineNumber: number;
    declare maxHeight: number;
    declare size: string;
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
    get editor() { return this._editor ?? (this._editor = this.create()); }
    constructor(code: any = '', language: any = null, props?: any) {
        super();
        if (this.showLineNumbers === undefined) this.showLineNumbers = true;
        if (this.firstLineNumber === undefined) this.firstLineNumber = 1;
        if (this.size === undefined) this.size = 'small';
        if (this.gutterMarkers === undefined) this.gutterMarkers = [];
        if (this.decorations === undefined) this.decorations = [];
        if (this.matchBrackets === undefined) this.matchBrackets = true;
        this.initialCode = code;this.languageName = language;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let editor = this.editor;editor.readOnly = this.readOnly;let highlighter = editor.highlighter;let metrics = CodeBlock.metricsFor(context, this.size, this.showLineNumbers, this.firstLineNumber + editor.document.lineCount - 1);let block = new CodeBlock('', null, { document: editor.document, language: highlighter.language, decorations: this.marks(editor), showLineNumbers: this.showLineNumbers, firstLineNumber: this.firstLineNumber, maxHeight: this.maxHeight, size: this.size, inverse: this.inverse, caption: this.caption, gutterMarkers: this.gutterMarkers, onGutterPressed: this.onGutterPressed, highlighter: highlighter, viewportOffset: this._offset, viewportHeight: this._viewport, onScrolled: (offset: number) => {if (Math.abs(offset - this._offset) < 1) return;this.setState(() => this._offset = offset);}, onViewportChanged: (height: number) => {if (Math.abs(height - this._viewport) < 1) return;this.setState(() => this._viewport = height);}, activeLine: editor.caret.line });let surface: VisualNode = Object.assign(new CodeSurface(block, editor), { contentTop: metrics.contentTop, lineHeight: metrics.lineHeight, contentLeft: metrics.contentLeft, columnWidth: metrics.columnWidth, autofocus: this.autofocus, label: this.caption ?? 'Code editor', onChanged: () => this.setState(() => {this.onChanged?.(editor.document.text);this.onSelectionChanged?.(editor.selection);}) });surface = new Shortcut(surface, new KeyChord('f', 4), () => this.setState(() => this._findOpen = true));if (!this._findOpen) return surface;let layers = new Stack('topStart', { width: SizeValue.fill });layers.add(surface);layers.add(new Positioned(this.findBar(context, editor), 8, 8));return layers;
    }

    create() {
        let editor = new CodeEditorController(this.initialCode, CodeLanguages.for(this.languageName), { readOnly: this.readOnly });return editor;
    }

    marks(editor: CodeEditorController) {
        let needle = this._findOpen && this._findText.length > 0 ? this._findText : this.search;if (!((needle != null && needle.length > 0)) && !this.matchBrackets) return this.decorations;let marks: CodeDecoration[] = [...this.decorations];let search: any; if (((needle != null && needle.length > 0) && (search = needle, true))) {let current = editor.selection;for (const match of editor.findAll(search, this.searchMatchCase)) {marks.push(new CodeDecoration(match, $eq.equals(match.start, current.start) && $eq.equals(match.end, current.end) ? 'outline' : 'highlight'));}}let pair: any; if (this.matchBrackets && ((editor.bracketAtCaret() != null) && (pair = editor.bracketAtCaret(), true))) {marks.push(new CodeDecoration(new CodeRange(pair[0], $eq.withPatch(pair[0], { column: pair[0].column + 1 })), 'outline'));marks.push(new CodeDecoration(new CodeRange(pair[1], $eq.withPatch(pair[1], { column: pair[1].column + 1 })), 'outline'));}return marks;
    }

    findBar(context: any, editor: CodeEditorController) {
        let theme = context.theme;let matches = this._findText.length > 0 ? editor.findAll(this._findText, this.searchMatchCase).length : 0;let index = 0;if (matches > 0) {let current = editor.selection;let all = editor.findAll(this._findText, this.searchMatchCase);for (let i = 0; i < all.length; i++) if ($eq.equals(all[i].start, current.start)) {index = i + 1;break;}}const step = (forward: boolean) => {if (this._findText.length === 0) return;let match = editor.findNext(this._findText, this.searchMatchCase, !forward);let found: any; if (((match != null) && (found = match, true))) this.setState(() => editor.selection = found);};let row = new Row(8, { cross: 'center' });row.add(new Box(new BoxStyle({ width: 168 }), new TextEntry(this._findText, (value: string) => this.setState(() => this._findText = value), { placeholder: 'Find', autofocus: true, onSubmit: () => step(true) })));row.add(new Text(matches === 0 ? (this._findText.length === 0 ? '' : '0') : `${index}/${matches}`, 'labelSmall', theme.textMuted, 1, { tabular: true }));row.add(new IconButton('chevronUp', 'Previous match', 'standard', 'medium', null, { size: 'small', onPressed: () => step(false) }));row.add(new IconButton('chevronDown', 'Next match', 'standard', 'medium', null, { size: 'small', onPressed: () => step(true) }));row.add(new IconButton('close', 'Close find', 'standard', 'medium', null, { size: 'small', onPressed: () => this.setState(() => {this._findOpen = false;this._findText = '';}) }));return new Box(new BoxStyle({ background: theme.surface, borderWidth: 1, borderColor: theme.border, cornerRadius: new CornerRadii(theme.shape('medium')), padding: EdgeInsets.symmetric(8, 4), shadow: theme.elevation(2) }), row);
    }

}

