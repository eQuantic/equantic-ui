import{DynamicElement,Text}from"./Counter-1v2evy8r.js";import{StatelessComponent,StyleBuilder}from"@equantic/runtime";class Button extends StatelessComponent{build(context){let buttonTheme=context.getService("IAppTheme")?.button,classValue=StyleBuilder.create(buttonTheme?.base).add(buttonTheme?.getVariant(this.variant)).add(buttonTheme?.getSize(this.size)).add(this.className).build(),attrs={["type"]:this.type,["class"]:classValue};if(this.disabled||this.loading)attrs.disabled="true";if(this.loading)attrs["data-loading"]="true";let element=new DynamicElement({tagName:"button",customAttributes:attrs,customEvents:this.buildEvents()});if(this.loading)element.children.push(this.createSpinner());if(this.children.length>0)for(let child of this.children)element.children.push(child);else if(this.text!=null)element.children.push(new Text(this.text));return element}}
export{Button};

//# debugId=3704C0E327FFE1DB64756E2164756E21
//# sourceMappingURL=Counter-my6wqdxf.js.map
