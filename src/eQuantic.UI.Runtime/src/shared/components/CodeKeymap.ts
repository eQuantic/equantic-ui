import { $eq, CodeCommandValue, CodeEditorController, CodeKeyBinding, KeyboardConventionValue, KeyChord } from "../runtime-exports";

export class CodeKeymap {
    static $slots: any = null;
    static $failure: any = null;

    static $init(): any {
        if (CodeKeymap.$slots === null) {
            if (CodeKeymap.$failure !== null) throw CodeKeymap.$failure;
            let $slots: any = CodeKeymap.$slots = {};
            try {
                $slots.appleBindings = null;
                $slots.standardBindings = null;
                $slots.appleBindings = CodeKeymap.build(true);
                $slots.standardBindings = CodeKeymap.build(false);
            } catch ($error) {
                CodeKeymap.$slots = null;
                throw CodeKeymap.$failure = $eq.exceptions.typeInitialization('eQuantic.UI.Code.CodeKeymap', $error);
            }
        }
        return CodeKeymap.$slots;
    }

    static get appleBindings(): CodeKeyBinding[] {
        return CodeKeymap.$init().appleBindings;
    }

    static set appleBindings(value: CodeKeyBinding[]) {
        CodeKeymap.$init().appleBindings = value;
    }

    static get standardBindings(): CodeKeyBinding[] {
        return CodeKeymap.$init().standardBindings;
    }

    static set standardBindings(value: CodeKeyBinding[]) {
        CodeKeymap.$init().standardBindings = value;
    }

    static defaults(convention: KeyboardConventionValue) {
        return [...CodeKeymap.table(convention)];
    }

    static table(convention: KeyboardConventionValue) {
        return convention === 'apple' ? CodeKeymap.appleBindings : CodeKeymap.standardBindings;
    }

    static handle(editor: CodeEditorController, key: string, modifiers: number, convention: KeyboardConventionValue, clipboard: any = null) {
        if (key !== 'Tab' && key !== 'Escape' && !CodeKeymap.isModifierKey(key)) editor.tabMovesFocus = false;
        if (CodeKeymap.isModifierKey(key)) return false;
        let first = editor.pendingChord;
        editor.pendingChord = null;
        let chord = new KeyChord(key, modifiers);
        let bindings = CodeKeymap.bindings(editor, convention);
        let waited: any; 
        if ((waited = first) != null) {
            let completes = false;
            for (const binding of bindings) {
                let starts: any; 
                if (!((starts = binding.first) != null) || !CodeKeymap.matches(starts, waited) || !CodeKeymap.matches(binding.chord, chord)) continue;
                completes = true;
                if (editor.execute(binding.command, clipboard)) return true;
            }
            if (completes) return true;
        }
        for (const binding of bindings) {
            let starts: any; 
            if ((starts = binding.first) != null) {
                if (!CodeKeymap.matches(starts, chord) || !editor.canExecute(binding.command)) continue;
                editor.pendingChord = chord;
                return true;
            }
            if (CodeKeymap.matches(binding.chord, chord) && editor.execute(binding.command, clipboard)) return true;
        }
        return false;
    }

    static bindings(editor: CodeEditorController, convention: KeyboardConventionValue) {
        let defaults = CodeKeymap.table(convention);
        let own = editor.keyBindings;
        if ($eq.collections.count(own) === 0) return defaults;
        let bindings: CodeKeyBinding[] = $eq.collections.listCopy(own);
        for (const binding of defaults) {
            let named = false;
            for (const mine of own) {
                let sameChord = CodeKeymap.matches(mine.chord, binding.chord) && CodeKeymap.sameFirst(mine.first, binding.first);
                let starts: any; 
                let startsOnMine = mine.first == null && (starts = binding.first) != null && CodeKeymap.matches(mine.chord, starts);
                if (!sameChord && !startsOnMine) continue;
                named = true;
                break;
            }
            if (!named) bindings.push(binding);
        }
        return bindings;
    }

    static matches(bound: KeyChord, pressed: KeyChord) {
        if (bound.modifiers !== pressed.modifiers) return false;
        if (bound.key === pressed.key) return true;
        return bound.key.length === 1 && pressed.key.length === 1 && $eq.text.toLower(bound.key[0], true) === $eq.text.toLower(pressed.key[0], true);
    }

    static sameFirst(bound: KeyChord | null, pressed: KeyChord | null) {
        let one: any; 
        if (!((one = bound) != null)) return pressed == null;
        let other: any; 
        return (other = pressed) != null && CodeKeymap.matches(one, other);
    }

    static isModifierKey(key: string) {
        return (((((((key === 'Shift' || key === 'Control') || key === 'Alt') || key === 'Meta') || key === 'AltGraph') || key === 'CapsLock') || key === 'Fn') || key === 'OS');
    }

    static bind(key: string, modifiers: number, command: CodeCommandValue) {
        return new CodeKeyBinding(new KeyChord(key, modifiers), command);
    }

    static build(apple: boolean) {
        let none = 0;
        let shift = 1;
        let command = 4;
        let control = 8;
        let alt = 2;
        let word = apple ? alt : command;
        let $n13: any; let $n14: any; let $n15: any; let $n16: any; let $n17: any; let $n18: any; 
        let bindings = [CodeKeymap.bind('ArrowDown', none, 'completionNext'), CodeKeymap.bind('ArrowUp', none, 'completionPrevious'), CodeKeymap.bind('PageDown', none, 'completionNextPage'), CodeKeymap.bind('PageUp', none, 'completionPreviousPage'), CodeKeymap.bind('Tab', none, 'completionAccept'), CodeKeymap.bind('Enter', none, 'completionAcceptChangingText'), CodeKeymap.bind(' ', command, 'completionShow'), CodeKeymap.bind(' ', control, 'completionShow'), CodeKeymap.bind(' ', shift | command, 'signatureHelpShow'), CodeKeymap.bind(' ', shift | control, 'signatureHelpShow'), CodeKeymap.bind('ArrowDown', none, 'signatureHelpNext'), CodeKeymap.bind('ArrowUp', none, 'signatureHelpPrevious'), CodeKeymap.bind('ArrowDown', alt, 'signatureHelpNext'), CodeKeymap.bind('ArrowUp', alt, 'signatureHelpPrevious'), ($n13 = new CodeKeyBinding(new KeyChord('i', command), 'hoverShow'), $n13.first = new KeyChord('k', command), $n13), CodeKeymap.bind('F8', none, 'problemNext'), CodeKeymap.bind('F8', shift, 'problemPrevious'), CodeKeymap.bind('Escape', none, 'completionHide'), CodeKeymap.bind('Escape', none, 'hoverHide'), CodeKeymap.bind('Escape', none, 'signatureHelpHide'), CodeKeymap.bind('Escape', none, 'leaveSnippet'), CodeKeymap.bind('Escape', none, 'removeSecondaryCursors'), CodeKeymap.bind('Escape', none, 'releaseKeyboard'), CodeKeymap.bind('Escape', shift, 'completionHide'), CodeKeymap.bind('Escape', shift, 'hoverHide'), CodeKeymap.bind('Escape', shift, 'signatureHelpHide'), CodeKeymap.bind('Escape', shift, 'leaveSnippet'), CodeKeymap.bind('Escape', shift, 'removeSecondaryCursors'), CodeKeymap.bind('Escape', shift, 'releaseKeyboard'), CodeKeymap.bind('a', command, 'selectAll'), CodeKeymap.bind('z', command, 'undo'), CodeKeymap.bind('z', shift | command, 'redo'), CodeKeymap.bind('y', command, 'redo'), CodeKeymap.bind('c', command, 'copy'), CodeKeymap.bind('x', command, 'cut'), CodeKeymap.bind('v', command, 'paste'), CodeKeymap.bind('/', command, 'toggleLineComment'), CodeKeymap.bind('/', shift | command, 'toggleLineComment'), CodeKeymap.bind('Slash', command, 'toggleLineComment'), CodeKeymap.bind('Slash', shift | command, 'toggleLineComment'), CodeKeymap.bind('ArrowLeft', none, 'cursorLeft'), CodeKeymap.bind('ArrowLeft', shift, 'cursorLeftSelect'), CodeKeymap.bind('ArrowRight', none, 'cursorRight'), CodeKeymap.bind('ArrowRight', shift, 'cursorRightSelect'), CodeKeymap.bind('ArrowUp', none, 'cursorUp'), CodeKeymap.bind('ArrowUp', shift, 'cursorUpSelect'), CodeKeymap.bind('ArrowDown', none, 'cursorDown'), CodeKeymap.bind('ArrowDown', shift, 'cursorDownSelect'), CodeKeymap.bind('ArrowLeft', word, 'cursorWordLeft'), CodeKeymap.bind('ArrowLeft', shift | word, 'cursorWordLeftSelect'), CodeKeymap.bind('ArrowRight', word, 'cursorWordRight'), CodeKeymap.bind('ArrowRight', shift | word, 'cursorWordRightSelect'), CodeKeymap.bind('Home', none, 'cursorLineStart'), CodeKeymap.bind('Home', shift, 'cursorLineStartSelect'), CodeKeymap.bind('End', none, 'cursorLineEnd'), CodeKeymap.bind('End', shift, 'cursorLineEndSelect'), CodeKeymap.bind('Home', command, 'cursorDocumentStart'), CodeKeymap.bind('Home', shift | command, 'cursorDocumentStartSelect'), CodeKeymap.bind('End', command, 'cursorDocumentEnd'), CodeKeymap.bind('End', shift | command, 'cursorDocumentEndSelect'), CodeKeymap.bind('PageUp', none, 'cursorPageUp'), CodeKeymap.bind('PageUp', shift, 'cursorPageUpSelect'), CodeKeymap.bind('PageDown', none, 'cursorPageDown'), CodeKeymap.bind('PageDown', shift, 'cursorPageDownSelect'), CodeKeymap.bind('Enter', none, 'newLine'), CodeKeymap.bind('Enter', shift, 'newLine'), CodeKeymap.bind('Backspace', none, 'deleteLeft'), CodeKeymap.bind('Backspace', shift, 'deleteLeft'), CodeKeymap.bind('Backspace', word, 'deleteWordLeft'), CodeKeymap.bind('Delete', none, 'deleteRight'), CodeKeymap.bind('Delete', word, 'deleteWordRight'), CodeKeymap.bind('Tab', none, 'nextTabStop'), CodeKeymap.bind('Tab', shift, 'previousTabStop'), CodeKeymap.bind('Tab', none, 'indent'), CodeKeymap.bind('Tab', shift, 'outdent'), CodeKeymap.bind('ArrowUp', alt, 'moveLinesUp'), CodeKeymap.bind('ArrowDown', alt, 'moveLinesDown'), CodeKeymap.bind('ArrowUp', shift | alt, 'copyLinesUp'), CodeKeymap.bind('ArrowDown', shift | alt, 'copyLinesDown'), CodeKeymap.bind('k', shift | command, 'deleteLines'), CodeKeymap.bind('Enter', command, 'insertLineBelow'), CodeKeymap.bind('Enter', shift | command, 'insertLineAbove'), CodeKeymap.bind('ArrowUp', alt | command, 'addCursorAbove'), CodeKeymap.bind('ArrowDown', alt | command, 'addCursorBelow'), CodeKeymap.bind('d', command, 'addNextOccurrence'), CodeKeymap.bind('l', shift | command, 'selectAllOccurrences'), CodeKeymap.bind('o', shift | command, 'goToSymbol'), CodeKeymap.bind('F12', none, 'goToDefinition'), CodeKeymap.bind('F12', command, 'goToImplementation'), CodeKeymap.bind('F12', shift, 'findReferences'), CodeKeymap.bind('F4', none, 'nextReference'), CodeKeymap.bind('F4', shift, 'previousReference'), ($n14 = new CodeKeyBinding(new KeyChord('[', command), 'foldRecursively'), $n14.first = new KeyChord('k', command), $n14), ($n15 = new CodeKeyBinding(new KeyChord(']', command), 'unfoldRecursively'), $n15.first = new KeyChord('k', command), $n15), ($n16 = new CodeKeyBinding(new KeyChord('l', command), 'toggleFold'), $n16.first = new KeyChord('k', command), $n16), ($n17 = new CodeKeyBinding(new KeyChord('0', command), 'foldAll'), $n17.first = new KeyChord('k', command), $n17), ($n18 = new CodeKeyBinding(new KeyChord('j', command), 'unfoldAll'), $n18.first = new KeyChord('k', command), $n18)];
        if (apple) {
            bindings.push(CodeKeymap.bind('g', control, 'goToLine'));
            bindings.push(CodeKeymap.bind('-', control, 'navigateBack'));
            bindings.push(CodeKeymap.bind('Minus', control, 'navigateBack'));
            bindings.push(CodeKeymap.bind('-', shift | control, 'navigateForward'));
            bindings.push(CodeKeymap.bind('_', shift | control, 'navigateForward'));
            bindings.push(CodeKeymap.bind('Minus', shift | control, 'navigateForward'));
            bindings.push(CodeKeymap.bind('ArrowLeft', command, 'cursorLineStart'));
            bindings.push(CodeKeymap.bind('ArrowLeft', shift | command, 'cursorLineStartSelect'));
            bindings.push(CodeKeymap.bind('ArrowRight', command, 'cursorLineEnd'));
            bindings.push(CodeKeymap.bind('ArrowRight', shift | command, 'cursorLineEndSelect'));
            bindings.push(CodeKeymap.bind('ArrowUp', command, 'cursorDocumentStart'));
            bindings.push(CodeKeymap.bind('ArrowUp', shift | command, 'cursorDocumentStartSelect'));
            bindings.push(CodeKeymap.bind('ArrowDown', command, 'cursorDocumentEnd'));
            bindings.push(CodeKeymap.bind('ArrowDown', shift | command, 'cursorDocumentEndSelect'));
            bindings.push(CodeKeymap.bind('Backspace', command, 'deleteToLineStart'));
            bindings.push(CodeKeymap.bind('j', control, 'joinLines'));
            bindings.push(CodeKeymap.bind('j', command, 'joinLines'));
            bindings.push(CodeKeymap.bind('[', alt | command, 'fold'));
            bindings.push(CodeKeymap.bind('“', alt | command, 'fold'));
            bindings.push(CodeKeymap.bind(']', alt | command, 'unfold'));
            bindings.push(CodeKeymap.bind('‘', alt | command, 'unfold'));
        } else {
            bindings.push(CodeKeymap.bind('g', command, 'goToLine'));
            bindings.push(CodeKeymap.bind('ArrowLeft', alt, 'navigateBack'));
            bindings.push(CodeKeymap.bind('ArrowRight', alt, 'navigateForward'));
            bindings.push(CodeKeymap.bind('[', shift | command, 'fold'));
            bindings.push(CodeKeymap.bind('{', shift | command, 'fold'));
            bindings.push(CodeKeymap.bind(']', shift | command, 'unfold'));
            bindings.push(CodeKeymap.bind('}', shift | command, 'unfold'));
        }
        return bindings;
    }
}

