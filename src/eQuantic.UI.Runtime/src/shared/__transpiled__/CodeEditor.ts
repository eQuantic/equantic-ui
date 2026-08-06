import { BuildContext, CodeBlock, CodeDecoration, CodeEditorController, CodeGutterMarker, CodeLanguages, CodeSurface, SharedStatefulComponent } from "@equantic/runtime";

export class CodeEditor extends SharedStatefulComponent {
    _editor: any;
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
    declare onGutterPressed: any;
    get editor() { return this._editor ?? (this._editor = this.create()); }
    constructor(code: any = '', language: any = null, props?: any) {
        super();
        if (this.showLineNumbers === undefined) this.showLineNumbers = true;
        if (this.firstLineNumber === undefined) this.firstLineNumber = 1;
        if (this.size === undefined) this.size = 'small';
        if (this.gutterMarkers === undefined) this.gutterMarkers = [];
        if (this.decorations === undefined) this.decorations = [];
        this.initialCode = code;this.languageName = language;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let editor = this.editor;editor.readOnly = this.readOnly;let highlighter = editor.highlighter;let metrics = CodeBlock.metricsFor(context, this.size, this.showLineNumbers, this.firstLineNumber + editor.document.lineCount - 1);let block = new CodeBlock(editor.document, highlighter.language, { showLineNumbers: this.showLineNumbers, firstLineNumber: this.firstLineNumber, maxHeight: this.maxHeight, size: this.size, inverse: this.inverse, caption: this.caption, gutterMarkers: this.gutterMarkers, decorations: this.decorations, onGutterPressed: this.onGutterPressed, highlighter: highlighter, activeLine: editor.caret.line });return new CodeSurface(block, editor, { contentTop: metrics.contentTop, lineHeight: metrics.lineHeight, contentLeft: metrics.contentLeft, columnWidth: metrics.columnWidth, autofocus: this.autofocus, label: this.caption ?? 'Code editor', onChanged: () => this.setState(() => {this.onChanged?.(editor.document.text);this.onSelectionChanged?.(editor.selection);}) });
    }

    create() {
        let editor = new CodeEditorController(this.initialCode, CodeLanguages.for(this.languageName), { readOnly: this.readOnly });return editor;
    }

}

