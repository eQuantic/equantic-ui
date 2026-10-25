import { CodeDocument, CodeEditorController, CodeGrid, CodePosition, CodeRange, KeyboardConventionValue, Point, PointerPhaseValue, Rect, SheetController, SheetEntryLanguage } from "../runtime-exports";

export class SheetNameBox {
    constructor(sheet: SheetController) {
        this._sheet = null!;
        this._box = (() => {
        const $o = new CodeEditorController('', new SheetEntryLanguage());
        $o.singleLine = true;
        return $o;
    })();
        this._focusVersion = 0;
        this._placed = 0;
        this._focusingPress = false;
        this._justFocused = false;
        this.typing = false;
        this.invalid = false;
        this._sheet = sheet;
    }

    _sheet!: SheetController;
    _box!: CodeEditorController;
    _focusVersion!: number;
    _placed!: number;
    _focusingPress!: boolean;
    _justFocused!: boolean;
    typing!: boolean;
    invalid!: boolean;

    get grid(): CodeGrid {
        return this._box.grid;
    }

    set grid(value: CodeGrid) {
        this._box.grid = value;
    }

    get entry(): CodeEditorController {
        return this._box;
    }

    get isPlaced(): boolean {
        return this._placed > 0;
    }

    get carets(): Rect[] {
        return this.typing ? this._box.carets : [];
    }

    get revealVersion(): number {
        return this._box.revealVersion;
    }

    get focusVersion(): number {
        return this._focusVersion;
    }

    placed() {
        return this._placed++;
    }

    removed() {
        return this._placed = Math.max(0, this._placed - 1);
    }

    requestFocus() {
        return this._focusVersion++;
    }

    handleKey(key: string, modifiers: number, convention: KeyboardConventionValue, clipboard: any) {
        if (!this.typing) return false;
        this._justFocused = false;
        switch (key) {
            case 'Enter':
                if (this._sheet.goTo(this._box.document.text)) {
                    this.invalid = false;
                    this.typing = false;
                } else this.invalid = true;
                return true;
            case 'Escape':
                this.invalid = false;
                this.typing = false;
                this._sheet.requestFocus();
                return true;
            case 'Tab':
                return false;
        }
        let claimed = this._box.handleKey(key, modifiers, convention, clipboard);
        if (claimed) this.invalid = false;
        return claimed;
    }

    handleText(text: string) {
        if (!this.typing || text.length === 0) return false;
        this._justFocused = false;
        this.invalid = false;
        return this._box.handleText(text);
    }

    setComposition(text: string) {
        return this.typing && this._box.setComposition(text);
    }

    copyText() {
        return this.typing ? this._box.document.textIn(this._box.selection) : '';
    }

    cut() {
        return this.typing && !this._box.selection.isEmpty ? this._box.cut() : '';
    }

    paste(text: string) {
        return this.typing && this._box.paste(text);
    }

    handlePointer(phase: PointerPhaseValue, position: Point, modifiers: number, clicks: number) {
        if (!this.typing) return false;
        if (phase === 'down' && this._justFocused) {
            this._justFocused = false;
            this._focusingPress = true;
            return true;
        }
        if (this._focusingPress) {
            if ((phase === 'up' || phase === 'cancel')) this._focusingPress = false;
            return phase !== 'hover';
        }
        return this._box.handlePointer(phase, position, modifiers, clicks);
    }

    focusChanged(focused: boolean) {
        this._box.focusChanged(focused);
        if (focused === this.typing) return;
        this.typing = focused;
        this.invalid = false;
        if (!focused) return;
        this._box.document = CodeDocument.fromText(this._sheet.activeCell.toA1());
        this._box.selection = new CodeRange(CodePosition.start, this._box.document.end);
        this.invalid = false;
        this._justFocused = true;
    }

    toString(): string {
        return 'eQuantic.UI.Sheet.SheetNameBox';
    }
}

