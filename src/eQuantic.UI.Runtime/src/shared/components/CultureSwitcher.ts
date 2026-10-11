import { $eq, Box, BuildContext, Button, CultureOption, Menu, MenuItem, SegmentedControl, SizeVariantValue, StatelessComponent } from "../runtime-exports";

export class CultureSwitcher extends StatelessComponent {
    static $typeId = 'eQuantic.UI.Components.CultureSwitcher';
    declare options: CultureOption[];
    declare size: SizeVariantValue;
    declare shape: string;
    declare icon: any;
    declare onChanged: ((string: string) => void) | null;

    constructor(options?: any, props?: any) {
        super();
        if (options !== undefined) this.options = options;
        if (this.size === undefined) this.size = 'small';
        if (this.shape === undefined) this.shape = 'auto';
        this.options = options;
        if (props && typeof props === 'object') Object.assign(this, props);
    }

    build(context: BuildContext) {
        if ($eq.collections.count(this.options) === 0) return new Box();
        let controller = context.getService('ICultureController');
        let current = controller?.uICulture ?? '';
        let selected = 0;
        for (let i = 0; i < $eq.collections.count(this.options); i++) {
            if ($eq.collections.item(this.options, i).name === current) {
                selected = i;
                break;
            }
            if (CultureSwitcher.languageOf($eq.collections.item(this.options, i).name) === CultureSwitcher.languageOf(current)) selected = i;
        }
        if (this.shape !== 'menu' && (this.shape === 'segments' || $eq.collections.count(this.options) <= 3)) {
            let labels: string[] = [];
            for (const option of this.options) labels.push(option.label);
            return new SegmentedControl(labels, selected, (index: number) => this.switch(controller, index), { size: this.size, stretch: false });
        }
        let items: MenuItem[] = [];
        for (const option of this.options) {
            let flag: any; 
            items.push(new MenuItem(((option.flag != null && option.flag.length > 0) && (flag = option.flag, true)) ? `${flag}  ${option.label}` : option.label));
        }
        let chosen = $eq.collections.item(this.options, selected);
        let code: any; 
        return new Menu(new Button(((chosen.short != null && chosen.short.length > 0) && (code = chosen.short, true)) ? code : chosen.label, 'ghost', this.size, null, { leading: this.icon }), items, (index: number) => this.switch(controller, index));
    }

    switch(controller: any, index: number) {
        if (index < 0 || index >= $eq.collections.count(this.options)) return;
        let option = $eq.collections.item(this.options, index);
        controller?.apply(option.name, option.name);
        this.onChanged?.(option.name);
    }

    static languageOf(name: string) {
        let cut = name.indexOf('-');
        return cut < 0 ? name : name.slice(0, cut);
    }
}

