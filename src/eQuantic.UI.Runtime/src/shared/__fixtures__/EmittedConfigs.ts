import { BoxStyle, Color, ColorToken, Group, Spacer, VisualNode } from "@equantic/runtime";

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
}

