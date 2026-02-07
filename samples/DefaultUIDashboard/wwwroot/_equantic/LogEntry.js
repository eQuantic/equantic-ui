import{Box,Text}from"./Counter-1v2evy8r.js";import{StatelessComponent}from"@equantic/runtime";class LogEntry extends StatelessComponent{constructor(msg,time,props){super(props);if(msg!==void 0)this.msg=msg;if(time!==void 0)this.time=time}build(context){return new Box({className:"flex items-start gap-3",children:[new Box({className:"eq-w-1.5 eq-h-1.5 eq-rounded-full eq-surface-active eq-mt-2"}),new Box({children:[new Text(this._msg,{className:"eq-text-sm eq-text-primary eq-block"}),new Text(this._time,{className:"eq-text-xs eq-text-secondary"})]})]})}}export{LogEntry};
export{LogEntry};

//# debugId=CA1959CE5D7F2AA664756E2164756E21
//# sourceMappingURL=LogEntry.js.map
