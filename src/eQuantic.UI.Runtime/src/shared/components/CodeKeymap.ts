import { $eq, CodeEditorController, KeyboardConventionValue } from "../runtime-exports";

export class CodeKeymap {
    static handle(editor: CodeEditorController, key: string, modifiers: number, convention: KeyboardConventionValue, clipboard: any = null) {
        let shift = (modifiers & 1) !== 0;
        let command = (modifiers & 4) !== 0;
        let control = (modifiers & 8) !== 0;
        let alt = (modifiers & 2) !== 0;
        let apple = convention === 'apple';
        if (key !== 'Tab' && key !== 'Escape' && !CodeKeymap.isModifierKey(key)) editor.tabMovesFocus = false;
        let chord = editor.pendingChord;
        if (!CodeKeymap.isModifierKey(key)) editor.pendingChord = null;
        let completion = editor.completion;
        if (completion.isOpen && !command && !control && !alt) {
            if (key === 'ArrowDown' && !shift && completion.move(1)) return true;
            if (key === 'ArrowUp' && !shift && completion.move(-1)) return true;
            if (key === 'PageDown' && !shift && completion.movePage(1)) return true;
            if (key === 'PageUp' && !shift && completion.movePage(-1)) return true;
            if (key === 'Tab' && completion.accept()) return true;
            if (key === 'Enter' && completion.acceptChangesText && completion.accept()) return true;
            if (key === 'Escape') return completion.dismiss();
        }
        if (key === ' ' && (command || control) && !shift && !alt) return completion.invoke();
        if (key === ' ' && (command || control) && shift && !alt) return editor.signatureHelp.invoke();
        if (!command && !control && !shift && (key === 'ArrowUp' || key === 'ArrowDown') && !editor.hover.isOpen && editor.signatureHelp.move(key === 'ArrowUp' ? -1 : 1)) return true;
        if (!(chord == null) && command && (key === 'i' || key === 'I')) {
            editor.hover.showAt(editor.caret);
            return true;
        }
        if (command && !shift && !alt && (key === 'k' || key === 'K') && ($eq.collections.count(editor.hover.providers) > 0 || $eq.collections.count(editor.diagnostics.items) > 0)) {
            editor.pendingChord = 'K';
            return true;
        }
        if (key === 'F8' && !command && !control && !alt) return !(editor.diagnostics.next(shift) == null);
        if (key === 'Escape' && editor.hover.dismiss()) return true;
        if (key === 'Escape' && editor.signatureHelp.dismiss()) return true;
        if (key === 'Escape') {
            completion.dismiss();
            editor.tabMovesFocus = true;
            return false;
        }
        if (command && key.length === 1) {
            {
                let pasted: any;
                switch ($eq.text.toLower(key[0], true)) {
                    case 'a':
                        editor.selectAll();
                        return true;
                    case 'z':
                        return shift ? editor.redo() : editor.undo();
                    case 'y':
                        return editor.redo();
                    case 'c':
                        if (clipboard == null) return false;
                        clipboard.write(editor.copyText());
                        return true;
                    case 'x':
                        if (clipboard == null) return false;
                        clipboard.write(editor.copyText());
                        editor.cut();
                        return true;
                    case 'v':
                        if (clipboard == null) return false;
                        if ((($v9409) => (($v9409 != null && $v9409.length > 0) && (pasted = $v9409, true)))(clipboard.read())) editor.paste(pasted);
                        return true;
                    case '/':
                        return editor.toggleLineComment();
                    default:
                        return false;
                }
            }
        }
        if (command && (key === '/' || key === 'Slash')) return editor.toggleLineComment();
        let byWord = apple ? alt : command;
        switch (key) {
            case 'ArrowLeft':
                editor.move(apple && command ? 'lineBoundary' : byWord ? 'word' : 'character', 'backward', shift);
                return true;
            case 'ArrowRight':
                editor.move(apple && command ? 'lineBoundary' : byWord ? 'word' : 'character', 'forward', shift);
                return true;
            case 'ArrowUp':
                if (!apple && command) return false;
                editor.move(apple && command ? 'documentBoundary' : 'line', 'backward', shift);
                return true;
            case 'ArrowDown':
                if (!apple && command) return false;
                editor.move(apple && command ? 'documentBoundary' : 'line', 'forward', shift);
                return true;
            case 'Home':
                editor.move(command ? 'documentBoundary' : 'lineBoundary', 'backward', shift);
                return true;
            case 'End':
                editor.move(command ? 'documentBoundary' : 'lineBoundary', 'forward', shift);
                return true;
            case 'PageUp':
                editor.move('page', 'backward', shift);
                return true;
            case 'PageDown':
                editor.move('page', 'forward', shift);
                return true;
        }
        switch (key) {
            case 'Enter':
                return editor.insertNewLine();
            case 'Backspace':
                return editor.deleteBackward(apple && command ? 'lineBoundary' : byWord ? 'word' : 'character');
            case 'Delete':
                return editor.deleteForward(byWord ? 'word' : 'character');
            case 'Tab':
                if (editor.readOnly || editor.tabMovesFocus) return false;
                return shift ? editor.outdent() : editor.indent();
            default:
                return false;
        }
    }

    static isModifierKey(key: string) {
        return (((((((key === 'Shift' || key === 'Control') || key === 'Alt') || key === 'Meta') || key === 'AltGraph') || key === 'CapsLock') || key === 'Fn') || key === 'OS');
    }
}

