import { BoxStyle, Color, ColorToken, Group, Image, LinearGradient, SafeArea, Spacer, Text, VisualNode } from "@equantic/runtime";

export class EmittedConfigs {
    static inked() {
        return new BoxStyle({ foreground: new ColorToken(Color.black) });
    }

    static grouped(child: VisualNode) {
        return new Group(child, { label: 'Totals', bookmark: 'totals', alignSelf: 'center', gridSpan: 2 });
    }

    static rigid() {
        return new Spacer(1, { fixedLength: 8 });
    }

    static labelled() {
        return new Image('a.png', 1, 2, 'cover', '', { label: 'Chart' });
    }

    static titled() {
        return new Text('Totals', null, null, null, null, false, false, null, 0, { role: 'title', maxLines: 2 });
    }

    static reparented(first: VisualNode, second: VisualNode) {
        return new Group(first, { child: second });
    }

    static inset(child: VisualNode) {
        return new SafeArea(child, 15, { edges: 3 });
    }

    static downward() {
        return new LinearGradient(new ColorToken(Color.black), new ColorToken(Color.white), 'toRight', { direction: 'toBottom' });
    }
}

