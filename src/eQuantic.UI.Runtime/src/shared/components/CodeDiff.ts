import { $eq, Box, BoxStyle, BuildContext, Button, CodeBlock, CodeDecoration, CodeDiffLayout, CodeDiffSource, CodeEditorController, CodeGrid, CodeLanguages, CodePatchFile, CodePosition, CodeRange, CodeRow, CodeRows, CodeSurface, ColorToken, Column, CornerRadii, Flexible, Icon, IconButton, IconGlyph, KeyChord, Point, Row, ScrollView, SdkStrings, Shortcut, Size, SizeValue, SizeVariantValue, StatefulComponent, Text, UiComponent, VisualNode } from "../runtime-exports";

export class CodeDiff extends StatefulComponent {
    static $typeId = 'eQuantic.UI.Components.CodeDiff';
    static wordAlpha: number = Math.fround(0.3);
    _original: any = null;
    _modified: any = null;
    _openedOriginal: any = null;
    _openedModified: any = null;
    _openedPatch: any = null;
    _source: any = null;
    _comparedOriginal: any = null;
    _comparedModified: any = null;
    _layout: any = null;
    _toldDocument: any = null;
    _expanded: any = new Set();
    _inlineChosen: boolean = false;
    _inlineChoice: boolean = false;
    _offset: number = 0;
    _viewport: number = 0;
    _originalWidth: number = 0;
    _modifiedWidth: number = 0;

    static get $hydration() {
        return { _offset: 'single', _viewport: 'single', _originalWidth: 'single', _modifiedWidth: 'single', height: { of: SizeValue, members: { value: 'single' } }, maxHeight: 'single' };
    }

    declare original: string;
    declare modified: string;
    declare patch: any;
    declare languageName: any;
    declare inline: boolean;
    declare context: number;
    declare readOnly: boolean;
    declare onChanged: ((string: string) => void) | null;
    declare height: SizeValue;
    declare maxHeight: number;
    declare size: SizeVariantValue;
    declare inverse: boolean;
    declare originalCaption: any;
    declare modifiedCaption: any;
    declare showToolbar: boolean;

    get editor() {
        this.openWhatChanged();
        return this._modified!;
    }

    get editable() {
        return this.patch == null && !this.readOnly;
    }

    get showsInline() {
        return this._inlineChosen ? this._inlineChoice : this.inline;
    }

    constructor(original: any = '', modified: any = '', language: any = null, props?: any) {
        super();
        if (original !== undefined) this.original = original;
        if (modified !== undefined) this.modified = modified;
        if (this.inline === undefined) this.inline = false;
        if (this.context === undefined) this.context = 3;
        if (this.readOnly === undefined) this.readOnly = false;
        if (this.height === undefined) this.height = SizeValue.hug;
        if (this.maxHeight === undefined) this.maxHeight = 0;
        if (this.size === undefined) this.size = 'small';
        if (this.inverse === undefined) this.inverse = false;
        if (this.showToolbar === undefined) this.showToolbar = true;
        this.original = original;
        this.modified = modified;
        this.languageName = language;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        this.openWhatChanged();
        let theme = context.theme;
        let original = this._original!;
        let modified = this._modified!;
        let source = this.sourceOf();
        let inline = this.showsInline;
        let layout = this.layoutOf(source, inline);
        let opened = false;
        let holding: any; 
        if ((holding = layout.foldOfModified(modified.caret.line)) != null) {
            $eq.collections.setAdd(this._expanded, holding.originalLine);
            opened = true;
        }
        let held: any; 
        if (!inline && (held = layout.foldOfOriginal(original.caret.line)) != null) {
            $eq.collections.setAdd(this._expanded, held.originalLine);
            opened = true;
        }
        if (opened) layout = this.layoutOf(source, inline);
        this._layout = layout;
        let lastNumber = Math.max(source.originalLineCount > 0 ? source.originalNumber(source.originalLineCount - 1) : 1, source.modifiedLineCount > 0 ? source.modifiedNumber(source.modifiedLineCount - 1) : 1);
        let metrics = CodeBlock.metricsFor(context, this.size, true, lastNumber);
        let origin = new Point(metrics.contentLeft, metrics.contentTop);
        let cell = new Size(metrics.columnWidth, metrics.lineHeight);
        modified.grid = new CodeGrid(origin, cell, layout.modified);
        original.grid = new CodeGrid(origin, cell, layout.original);
        let bounded = this.height.kind !== 'hug';
        let windowed = bounded || this.maxHeight > 0;
        if (!windowed) {
            this._offset = 0;
            this._viewport = 0;
        }
        let [first, last] = CodeBlock.windowOf(layout.modified.rowCount, metrics.lineHeight, this._offset, this._viewport);
        let added = theme.colors('success');
        let removed = theme.colors('destructive');
        let filler = theme.border.withOpacity(Math.fround(0.35));
        let modifiedLines = CodeDiff.linesOf(layout.modified, first, last);
        let fillerLines = inline ? CodeDiff.sourceLinesOf(layout.modified, first, last) : [0, -1];
        let modifiedBlock = new CodeBlock('', null, { document: modified.document, language: modified.highlighter.language, highlighter: modified.highlighter, rows: layout.modified, decorations: CodeDiff.marksOf(source, true, modifiedLines[0], modifiedLines[1], added.subtle, added.base.withOpacity(CodeDiff.wordAlpha), modified.composition), fillerDocument: inline ? original.document : null, fillerHighlighter: inline ? original.highlighter : null, fillerDecorations: inline ? CodeDiff.marksOf(source, false, fillerLines[0], fillerLines[1], removed.subtle, removed.base.withOpacity(CodeDiff.wordAlpha), null) : [], fillerColor: filler, onPlaceholderPressed: (line: number) => this.setState(() => this.openFoldOf(line, true)), standalone: false, size: this.size, inverse: this.inverse, metrics: metrics, viewportOffset: this._offset, viewportHeight: this._viewport, viewportWidth: this._modifiedWidth, selectionBands: modified.selectionBandsIn(modifiedLines[0], modifiedLines[1]), widestLine: inline ? Math.max(modified.widestLine, original.widestLine) : modified.widestLine });
        let ink = CodeBlock.inkFor(this.inverse, theme);
        let modifiedSurface: VisualNode = new CodeSurface(modifiedBlock, modified, { label: this.modifiedCaption ?? SdkStrings.diffModified, caretColor: ink, onChanged: () => this.setState(() => this.notify()) });
        let modifiedScroll = new ScrollView(modifiedSurface, 'horizontal', { width: SizeValue.fill, onViewportChanged: (width: number) => {
            if (Math.abs(Math.fround(width - this._modifiedWidth)) < 1) return;
            this.setState(() => this._modifiedWidth = width);
        } });
        let body = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill, cross: 'stretch' });
        if (inline) {
            body.add(modifiedBlock.gutter(context, (row) => CodeDiff.originalNumberOf(source, row)));
            body.add(modifiedBlock.gutter(context, (row) => row.kind === 'line' ? String(source.modifiedNumber(row.line)) : null));
            body.add(new Flexible(modifiedScroll));
        } else {
            let originalLines = CodeDiff.linesOf(layout.original, first, last);
            let originalBlock = new CodeBlock('', null, { document: original.document, language: original.highlighter.language, highlighter: original.highlighter, rows: layout.original, decorations: CodeDiff.marksOf(source, false, originalLines[0], originalLines[1], removed.subtle, removed.base.withOpacity(CodeDiff.wordAlpha), null), fillerColor: filler, onPlaceholderPressed: (line: number) => this.setState(() => this.openFoldOf(line, false)), standalone: false, size: this.size, inverse: this.inverse, metrics: metrics, viewportOffset: this._offset, viewportHeight: this._viewport, viewportWidth: this._originalWidth, selectionBands: original.selectionBandsIn(originalLines[0], originalLines[1]), widestLine: original.widestLine });
            let originalSurface: VisualNode = new CodeSurface(originalBlock, original, { label: this.originalCaption ?? SdkStrings.diffOriginal, caretColor: ink, onChanged: () => this.setState(() => {}) });
            let originalScroll = new ScrollView(originalSurface, 'horizontal', { width: SizeValue.fill, onViewportChanged: (width: number) => {
                if (Math.abs(Math.fround(width - this._originalWidth)) < 1) return;
                this.setState(() => this._originalWidth = width);
            } });
            let originalSide = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill, cross: 'start' });
            originalSide.add(originalBlock.gutter(context, (row) => row.kind === 'line' ? String(source.originalNumber(row.line)) : null));
            originalSide.add(new Flexible(originalScroll));
            let modifiedSide = new Row(0, 'start', 'center', false, null, null, { width: SizeValue.fill, cross: 'start' });
            modifiedSide.add(modifiedBlock.gutter(context, (row) => row.kind === 'line' ? String(source.modifiedNumber(row.line)) : null));
            modifiedSide.add(new Flexible(modifiedScroll));
            body.add(new Flexible(originalSide));
            body.add(new Box(new BoxStyle({ width: 1, background: theme.border })));
            body.add(new Flexible(modifiedSide));
        }
        let viewport: VisualNode = body;
        if (windowed) {
            viewport = new Box(new BoxStyle({ width: SizeValue.fill, height: bounded ? SizeValue.fill : SizeValue.hug, maxHeight: !bounded && this.maxHeight > 0 ? SizeValue.fixed(this.maxHeight) : SizeValue.hug }), new ScrollView(body, 'vertical', { width: SizeValue.fill, height: bounded ? SizeValue.fill : SizeValue.hug, onScrolled: (offset: number) => {
                if (Math.abs(Math.fround(offset - this._offset)) < 1) return;
                this.setState(() => this._offset = offset);
            }, onViewportChanged: (height: number) => {
                if (Math.abs(Math.fround(height - this._viewport)) < 1) return;
                this.setState(() => this._viewport = height);
            } }));
        }
        let frame: VisualNode = new Box(new BoxStyle({ width: SizeValue.fill, height: bounded ? SizeValue.fill : SizeValue.hug, background: CodeBlock.surfaceFor(this.inverse, theme), cornerRadius: new CornerRadii(theme.shape('medium')), clip: true }), viewport);
        let whole = new Column(8, 'start', 'stretch', false, null, null, { width: SizeValue.fill, height: bounded ? SizeValue.fill : SizeValue.hug });
        if (this.showToolbar) whole.add(this.toolbar(theme, source, inline));
        whole.add(bounded ? new Flexible(frame) : frame);
        let shortcuts: VisualNode = new Shortcut(whole, new KeyChord('F7'), () => this.stepTo(true), { focusScoped: true });
        shortcuts = new Shortcut(shortcuts, new KeyChord('F7', 1), () => this.stepTo(false), { focusScoped: true });
        shortcuts = new Shortcut(shortcuts, new KeyChord('F5', 2), () => this.stepTo(true), { focusScoped: true });
        shortcuts = new Shortcut(shortcuts, new KeyChord('F5', 2 | 1), () => this.stepTo(false), { focusScoped: true });
        let capped = bounded && this.maxHeight > 0;
        return new Box(new BoxStyle({ width: SizeValue.fill, height: this.height, maxHeight: capped ? SizeValue.fixed(this.maxHeight) : SizeValue.hug }), shortcuts);
    }

    static ofPatch(file: CodePatchFile, language: any = null) {
        return new CodeDiff('', '', language, { patch: file });
    }

    adoptConfig(next: UiComponent) {
        let fresh: any; 
        if (!((next instanceof CodeDiff && (fresh = next, true)))) return;
        if (fresh.inline !== this.inline) this._inlineChosen = false;
        this.original = fresh.original;
        this.modified = fresh.modified;
        this.patch = fresh.patch;
        this.languageName = fresh.languageName;
        this.inline = fresh.inline;
        this.context = fresh.context;
        this.readOnly = fresh.readOnly;
        this.onChanged = fresh.onChanged;
        this.height = fresh.height;
        this.maxHeight = fresh.maxHeight;
        this.size = fresh.size;
        this.inverse = fresh.inverse;
        this.originalCaption = fresh.originalCaption;
        this.modifiedCaption = fresh.modifiedCaption;
        this.showToolbar = fresh.showToolbar;
    }

    openWhatChanged() {
        let language = CodeLanguages.for(this.languageName);
        let patch: any; 
        if ((patch = this.patch) != null) {
            if (!(this._original == null) && (patch === this._openedPatch)) return;
            let source = CodeDiffSource.fromPatch(patch);
            this._original = new CodeEditorController(source.original.text, language, { readOnly: true });
            this._modified = new CodeEditorController(source.modified.text, language, { readOnly: true });
            this._openedPatch = patch;
            this._source = source;
            this._comparedOriginal = null;
            this._comparedModified = null;
            this._expanded.clear();
            this.startAtTheFirstChange(source);
            return;
        }
        let opened = false;
        if (this._original == null || !(this._openedPatch == null) || this.original !== this._openedOriginal) {
            this._original = new CodeEditorController(this.original, language, { readOnly: true });
            this._openedOriginal = this.original;
            this._expanded.clear();
            opened = true;
        }
        if (this._modified == null || !(this._openedPatch == null) || !this.editable && this.modified !== this._openedModified) {
            this._modified = new CodeEditorController(this.modified, language);
            this._openedModified = this.modified;
            this._toldDocument = this._modified.document;
            opened = true;
        }
        this._openedPatch = null;
        this._modified.readOnly = !this.editable;
        if (opened) this.startAtTheFirstChange(this.sourceOf());
    }

    sourceOf() {
        if (!(this._openedPatch == null) && !(this._source == null)) return this._source;
        let original = (this._original!).document;
        let modified = (this._modified!).document;
        if (this._source == null || original !== this._comparedOriginal || modified !== this._comparedModified) {
            this._source = CodeDiffSource.fromDocuments(original, modified);
            this._comparedOriginal = original;
            this._comparedModified = modified;
        }
        return this._source;
    }

    startAtTheFirstChange(source: CodeDiffSource) {
        if (source.changes.length === 0) return;
        let change = source.changes[0];
        (this._modified!).selection = new CodeRange(new CodePosition(Math.min(change.modifiedStart, this._modified.document.lineCount - 1), 0));
        (this._original!).selection = new CodeRange(new CodePosition(Math.min(change.originalStart, this._original.document.lineCount - 1), 0));
    }

    layoutOf(source: CodeDiffSource, inline: boolean) {
        return inline ? CodeDiffLayout.inline(source.changes, source.originalLineCount, source.modifiedLineCount, this.context, this._expanded, source.gaps, (count: number) => SdkStrings.unchangedLines(count)) : CodeDiffLayout.sideBySide(source.changes, source.originalLineCount, source.modifiedLineCount, this.context, this._expanded, source.gaps, (count: number) => SdkStrings.unchangedLines(count));
    }

    notify() {
        let document = (this._modified!).document;
        if (document === this._toldDocument) return;
        this._toldDocument = document;
        this.onChanged?.(document.text);
    }

    toolbar(theme: any, source: CodeDiffSource, inline: boolean) {
        let addedLines = 0;
        let removedLines = 0;
        for (const change of source.changes) {
            addedLines += change.modifiedCount;
            removedLines += change.originalCount;
        }
        let bar = new Row(8, 'start', 'center', false, null, null, { width: SizeValue.fill, cross: 'center' });
        let title = this.modifiedCaption ?? this.originalCaption;
        bar.add(new Flexible(new Text(title ?? '', 'label', theme.textSecondary, 1, 'start', false, false, null, 0, { mono: true })));
        bar.add(new Text(`+${addedLines}`, 'labelSmall', theme.colors('success').base, 1, 'start', false, false, null, 0, { tabular: true }));
        bar.add(new Text(`−${removedLines}`, 'labelSmall', theme.colors('destructive').base, 1, 'start', false, false, null, 0, { tabular: true }));
        let none = source.changes.length === 0;
        bar.add(new IconButton(new Icon(IconGlyph.fromIcons('chevronUp')), SdkStrings.previousChange, 'standard', 'medium', null, { size: 'small', disabled: none, onPressed: () => this.stepTo(false) }));
        bar.add(new IconButton(new Icon(IconGlyph.fromIcons('chevronDown')), SdkStrings.nextChange, 'standard', 'medium', null, { size: 'small', disabled: none, onPressed: () => this.stepTo(true) }));
        bar.add(new Button(inline ? SdkStrings.showSideBySide : SdkStrings.showInline, 'ghost', 'small', null, { onPressed: () => this.setState(() => {
            this._inlineChosen = true;
            this._inlineChoice = !inline;
        }) }));
        return bar;
    }

    stepTo(forward: boolean) {
        let source: any; let modified: any; let original: any; 
        if (!(((this._source != null && this._source.changes != null && this._source.changes.length > 0) && (source = this._source, true))) || !((modified = this._modified) != null) || !((original = this._original) != null)) return;
        let caret = modified.caret.line;
        let target = forward ? source.changes[0] : source.changes[source.changes.length - 1];
        if (forward) {
            for (const change of source.changes) {
                if (change.modifiedStart <= caret) continue;
                target = change;
                break;
            }
        } else {
            for (let i = source.changes.length - 1; i >= 0; i--) {
                if (source.changes[i].modifiedStart >= caret) continue;
                target = source.changes[i];
                break;
            }
        }
        this.setState(() => {
            modified.selection = new CodeRange(new CodePosition(Math.min(target.modifiedStart, modified.document.lineCount - 1), 0));
            original.selection = new CodeRange(new CodePosition(Math.min(target.originalStart, original.document.lineCount - 1), 0));
        });
    }

    openFoldOf(line: number, modifiedSide: boolean) {
        let layout: any; 
        if (!((layout = this._layout) != null)) return;
        let fold = modifiedSide ? layout.foldOfModified(line) : layout.foldOfOriginal(line);
        let found: any; 
        if ((found = fold) != null) $eq.collections.setAdd(this._expanded, found.originalLine);
    }

    static linesOf(rows: CodeRows, first: number, last: number): [number, number] {
        return last < first ? [0, -1] : [rows.lineAtRow(first), rows.lineAtRow(last)];
    }

    static sourceLinesOf(rows: CodeRows, first: number, last: number): [number, number] {
        let lowest = 2147483647;
        let highest = -1;
        for (let row = first; row <= last; row++) {
            let shown = rows.rowAt(row);
            if (shown.kind !== 'filler' || shown.sourceLine < 0) continue;
            lowest = Math.min(lowest, shown.sourceLine);
            highest = Math.max(highest, shown.sourceLine);
        }
        return highest < 0 ? [0, -1] : [lowest, highest];
    }

    static marksOf(source: CodeDiffSource, modifiedSide: boolean, first: number, last: number, line: ColorToken, word: ColorToken, composition: any) {
        let marks: CodeDecoration[] = [];
        let document = modifiedSide ? source.modified : source.original;
        for (const change of source.changes) {
            let start = modifiedSide ? change.modifiedStart : change.originalStart;
            let count = modifiedSide ? change.modifiedCount : change.originalCount;
            if (count === 0 || start + count - 1 < first || start > last) continue;
            let end = Math.min(start + count - 1, document.lineCount - 1);
            marks.push(new CodeDecoration(new CodeRange(new CodePosition(start, 0), new CodePosition(end, document.line(end).length)), 'line', line));
            for (const inner of change.inner) marks.push(new CodeDecoration(modifiedSide ? inner.modified : inner.original, undefined, word));
        }
        let composing: any; 
        if ((composing = composition) != null) marks.push(new CodeDecoration(composing, 'underline'));
        return marks;
    }

    static originalNumberOf(source: CodeDiffSource, row: CodeRow) {
        if (row.kind === 'filler' && row.sourceLine >= 0) return String(source.originalNumber(row.sourceLine));
        if (row.kind !== 'line') return null;
        let line = source.originalLineOf(row.line);
        return line < 0 ? null : String(source.originalNumber(line));
    }
}

