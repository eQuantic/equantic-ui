import { Anchored, Box, BoxStyle, BuildContext, CornerRadii, EdgeInsets, Space, StatelessComponent, Text, VisualNode } from "@equantic/runtime";

export class Tooltip extends StatelessComponent {
    declare child: VisualNode;
    declare text: string;
    declare placement: string;
    constructor(child?: any, text?: any, props?: any) {
        super(props);
        if (child !== undefined) this.child = child;
        if (text !== undefined) this.text = text;
        if (this.placement === undefined) this.placement = 'topCenter';
        this.child = child;this.text = text;
    }

    build(context: BuildContext) {
        let theme = context.theme;let pill = new Box(new BoxStyle({ background: theme.textPrimary, cornerRadius: new CornerRadii(theme.shape('small')), padding: EdgeInsets.symmetric(Space.s2, Space.s1) }), new Text(this.text, 'caption', theme.textInverse, 1));return new Anchored(this.child, pill, { placement: this.placement, openOnHover: true });
    }

}

