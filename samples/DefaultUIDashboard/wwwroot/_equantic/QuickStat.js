import{Box,Text}from"./Counter-1v2evy8r.js";import{StatelessComponent}from"@equantic/runtime";class QuickStat extends StatelessComponent{constructor(title,value,colorClass,props){super(props);if(title!==void 0)this.title=title;if(value!==void 0)this.value=value;if(colorClass!==void 0)this.colorClass=colorClass}build(context){return new Box({className:"eq-surface-hover eq-border eq-p-6 eq-rounded-xl eq-shadow-sm",children:[new Text(this._title,{className:"eq-text-secondary eq-text-sm eq-font-medium eq-mb-2 eq-block"}),new Box({className:"flex items-end justify-between",children:[new Text(this._value,{className:"eq-text-3xl eq-font-black"}),new Box({className:`eq-px-3 eq-py-1 eq-rounded-full eq-text-xs eq-font-bold ${this._colorClass}`,children:[new Text("LIVE")]})]})]})}}export{QuickStat};
export{QuickStat};

//# debugId=15D0A6C17646EFDC64756E2164756E21
//# sourceMappingURL=QuickStat.js.map
