import { Box, BoxStyle, BuildContext, CornerRadii, EdgeInsets, Flexible, Icon, Pressable, Row, SizeValue, Sizing, StatelessComponent, TextEntry } from "../runtime-exports";

export class SearchField extends StatelessComponent {
    declare query: string;
    declare onChanged: ((string: string) => void) | null;
    declare placeholder: string;
    declare onSubmit: (() => void) | null;
    constructor(query?: any, onChanged: any = null, placeholder: any = 'Search…', onSubmit: any = null, props?: any) {
        super();
        if (query !== undefined) this.query = query;
        if (onChanged !== undefined) this.onChanged = onChanged;
        if (placeholder !== undefined) this.placeholder = placeholder;
        if (onSubmit !== undefined) this.onSubmit = onSubmit;
        this.query = query;this.onChanged = onChanged;this.placeholder = placeholder;this.onSubmit = onSubmit;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        let theme = context.theme;let row = new Row(10, 'start', 'center', false, null, null, { height: SizeValue.fill, cross: 'center' });row.add(new Icon('search', 20, theme.textMuted));row.add(new Flexible(new TextEntry(this.query, this.onChanged, { placeholder: this.placeholder, label: this.placeholder.length > 0 ? this.placeholder : null, onSubmit: this.onSubmit, role: 'bodyM' }), 1));if (this.query.length > 0) {row.add(new Pressable(new Icon('close', 20, theme.textMuted), () => this.onChanged?.(''), { label: 'clear search' }));}return new Box(new BoxStyle({ width: SizeValue.fill, height: Sizing.height('medium', context.density), background: theme.surfaceSubtle, cornerRadius: new CornerRadii(theme.shape('full')), padding: EdgeInsets.symmetric(14, 0) }), row);
    }

}

