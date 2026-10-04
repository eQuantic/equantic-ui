import { $eq, CSharpLanguage, JsonLanguage, PlainTextLanguage, PythonLanguage, TypeScriptLanguage, XmlLanguage } from "../runtime-exports";

export class CodeLanguages {
    static $slots: any = null;

    static $init(): any {
        if (CodeLanguages.$slots === null) {
            const slots: any = CodeLanguages.$slots = { cSharp: null, typeScript: null, python: null, json: null, xml: null, plainText: null, known: null };
            slots.cSharp = new CSharpLanguage();
            slots.typeScript = new TypeScriptLanguage();
            slots.python = new PythonLanguage();
            slots.json = new JsonLanguage();
            slots.xml = new XmlLanguage();
            slots.plainText = new PlainTextLanguage();
            slots.known = $eq.collections.dictionary().set('c#', CodeLanguages.cSharp).set('csharp', CodeLanguages.cSharp).set('cs', CodeLanguages.cSharp).set('typescript', CodeLanguages.typeScript).set('ts', CodeLanguages.typeScript).set('javascript', CodeLanguages.typeScript).set('js', CodeLanguages.typeScript).set('tsx', CodeLanguages.typeScript).set('jsx', CodeLanguages.typeScript).set('python', CodeLanguages.python).set('py', CodeLanguages.python).set('json', CodeLanguages.json).set('xml', CodeLanguages.xml).set('csproj', CodeLanguages.xml).set('html', CodeLanguages.xml).set('plist', CodeLanguages.xml).set('text', CodeLanguages.plainText).set('txt', CodeLanguages.plainText).set('plain', CodeLanguages.plainText);
        }
        return CodeLanguages.$slots;
    }

    static get cSharp(): any {
        return CodeLanguages.$init().cSharp;
    }

    static set cSharp(value: any) {
        CodeLanguages.$init().cSharp = value;
    }

    static get typeScript(): any {
        return CodeLanguages.$init().typeScript;
    }

    static set typeScript(value: any) {
        CodeLanguages.$init().typeScript = value;
    }

    static get python(): any {
        return CodeLanguages.$init().python;
    }

    static set python(value: any) {
        CodeLanguages.$init().python = value;
    }

    static get json(): any {
        return CodeLanguages.$init().json;
    }

    static set json(value: any) {
        CodeLanguages.$init().json = value;
    }

    static get xml(): any {
        return CodeLanguages.$init().xml;
    }

    static set xml(value: any) {
        CodeLanguages.$init().xml = value;
    }

    static get plainText(): any {
        return CodeLanguages.$init().plainText;
    }

    static set plainText(value: any) {
        CodeLanguages.$init().plainText = value;
    }

    static get known(): any {
        return CodeLanguages.$init().known;
    }

    static set known(value: any) {
        CodeLanguages.$init().known = value;
    }

    static register(name: string, language: any) {
        return $eq.mapSet(CodeLanguages.known, CodeLanguages.keyOf(name), language);
    }

    static for(name: string | null) {
        if ((!$eq.text.hasNonWhiteSpace(name))) return CodeLanguages.plainText;
        let language: any; 
        return (($0: any, $1: any) => ($0.has($1) ? ((language = $0.get($1)), true) : ((language = null), false)))(CodeLanguages.known, CodeLanguages.keyOf(name)) ? language : CodeLanguages.plainText;
    }

    static keyOf(name: string) {
        return (_s => { const _c = '.'; let _i = 0; while (_i < _s.length && _c.includes(_s[_i])) _i++; return _s.slice(_i); })(name).toLowerCase();
    }
}

