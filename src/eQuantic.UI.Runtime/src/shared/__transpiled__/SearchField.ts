import { $eq, Box, BoxStyle, BuildContext, Component, ComponentContext, CornerRadii, EdgeInsets, Flexible, HtmlElement, Icon, IconSize, Pressable, Radius, Row, SizeValue, StatelessComponent, TextEntry, VisualNode } from "@equantic/runtime";

export class SearchField extends StatelessComponent {
    constructor(query?: any, onChanged: any = null, placeholder: any = 'Search…', onSubmit: any = null, props?: any) {
        super(props);
        if (query !== undefined) this.query = query;
        if (onChanged !== undefined) this.onChanged = onChanged;
        if (placeholder !== undefined) this.placeholder = placeholder;
        if (onSubmit !== undefined) this.onSubmit = onSubmit;
        this.query = query;this.onChanged = onChanged;this.placeholder = placeholder;this.onSubmit = onSubmit;
    }

    build(context: BuildContext) {
        let theme = context.theme;let row = new Row(10, { height: SizeValue.fill, cross: 'center' });row.add(new Icon('search', IconSize.dense, theme.textMuted));row.add(new Flexible(new TextEntry(this.query, this.onChanged, { placeholder: this.placeholder, onSubmit: this.onSubmit, role: 'bodyM' }), 1));if (this.query.length > 0) {row.add(new Pressable(new Icon('close', IconSize.dense, theme.textMuted), () => this.onChanged?.(''), { label: 'clear search' }));}return new Box(new BoxStyle({ width: SizeValue.fill, height: 40, background: theme.surfaceSubtle, cornerRadius: new CornerRadii(Radius.full), padding: EdgeInsets.symmetric(14, 0) }), row);
    }

}

