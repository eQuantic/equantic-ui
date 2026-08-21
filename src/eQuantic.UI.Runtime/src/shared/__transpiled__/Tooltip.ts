import { Anchored, AnchorPlacementValue, Box, BoxStyle, BuildContext, CornerRadii, EdgeInsets, StatelessComponent, Text, VisualNode } from "@equantic/runtime";

export class Tooltip extends StatelessComponent {
    declare child: VisualNode;
    declare text: string;
    declare placement: AnchorPlacementValue;
    constructor(child?: any, text?: any, props?: any) {
        super();
        if (child !== undefined) this.child = child;
        if (text !== undefined) this.text = text;
        if (this.placement === undefined) this.placement = 'topCenter';
        this.child = child;
        this.text = text;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;
        let pill = new Box(new BoxStyle({ background: theme.textPrimary, cornerRadius: new CornerRadii(theme.shape('small')), padding: EdgeInsets.symmetric(8, 4) }), new Text(this.text, 'caption', theme.textInverse, 1));
        return new Anchored(this.child, pill, { placement: this.placement, openOnHover: true, describesAnchor: true });
    }

}

