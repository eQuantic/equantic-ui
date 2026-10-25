import { CodeGrid, KeyboardConventionValue, Point, PointerPhaseValue, Rect, SheetController, SheetKeymap } from "../runtime-exports";

export class SheetFormulaBar {
    constructor(sheet: SheetController) {
        this._sheet = null!;
        this._pressed = false;
        this._focusVersion = 0;
        this.grid = CodeGrid.default;
        this._sheet = sheet;
    }

    _sheet!: SheetController;
    _pressed!: boolean;
    _focusVersion!: number;
    grid!: CodeGrid;

    get holdsTheCaret(): boolean {
        return this._sheet.editing && this._sheet.entryView === 'formulaBar';
    }

    get carets(): Rect[] {
        if (!this.holdsTheCaret) return [];
        this._sheet.useView();
        return this._sheet.entry.carets;
    }

    get revealVersion(): number {
        return this._sheet.entry.revealVersion;
    }

    get focusVersion(): number {
        return this._focusVersion;
    }

    requestFocus() {
        return this._focusVersion++;
    }

    take() {
        if (!this._sheet.editing) this._sheet.beginEdit(this._sheet.document.getCell(this._sheet.activeCell), 'edit');
        this._sheet.enterEditMode();
        this._sheet.holdEntryIn('formulaBar');
    }

    handleKey(key: string, modifiers: number, convention: KeyboardConventionValue, clipboard: any) {
        if (!this._sheet.editing && (key === 'Tab' || key === 'Escape')) return false;
        if (SheetKeymap.isModifierKey(key)) return false;
        this.take();
        let claimed = SheetKeymap.handle(this._sheet, key, modifiers, convention, clipboard);
        if (!this._sheet.editing) this._sheet.requestFocus();
        return claimed;
    }

    handleText(text: string) {
        if (text.length === 0) return false;
        this.take();
        return this._sheet.entry.handleText(text);
    }

    setComposition(text: string) {
        if (!this._sheet.editing && text.length === 0) return false;
        this.take();
        return this._sheet.entry.setComposition(text);
    }

    copyText() {
        return this._sheet.editing ? this._sheet.copyText() : '';
    }

    cut() {
        return this._sheet.editing ? this._sheet.cut() : '';
    }

    paste(text: string) {
        if (text.length === 0) return false;
        this.take();
        return this._sheet.paste(text);
    }

    handlePointer(phase: PointerPhaseValue, position: Point, modifiers: number, clicks: number) {
        {
            const $s = phase;
            if ($s === 'down') {
                this.take();
                this._pressed = true;
                this._sheet.entry.handlePointer(phase, position, modifiers, clicks);
                return true;
            } else if (((($s === 'move' || $s === 'up') || $s === 'cancel') && (this._pressed))) {
                if (phase !== 'move') this._pressed = false;
                if (!this.holdsTheCaret) return false;
                this._sheet.useView();
                return this._sheet.entry.handlePointer(phase, position, modifiers, clicks);
            } else {
                return false;
            }
        }
    }

    focusChanged(focused: boolean) {
        if (!focused && this.holdsTheCaret) this._sheet.entry.focusChanged(false);
    }

    toString(): string {
        return 'eQuantic.UI.Sheet.SheetFormulaBar';
    }
}

