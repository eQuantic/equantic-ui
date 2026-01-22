import { Component, BuildContext, StatefulComponent } from '@equantic/runtime';
import { Button, Container, Heading, Row, Text, TextInput } from '@equantic/runtime';

export class Counter extends StatefulComponent {
  createState(): CounterState {
    return new CounterState(this);
  }
}

class CounterState {
  private _component: Counter;
  private _needsRender: boolean = false;

  private _count: number = 0;
  private _message: string = "";

  constructor(component: Counter) {
    this._component = component;
  }

  setState(fn: () => void): void {
    fn();
    this._needsRender = true;
    this._component._scheduleRender();
  }

  _increment(): void { this.setState(() => { this._count++; }); }

  _decrement(): void { this.setState(() => { this._count--; }); }

  build(context: BuildContext): Component {
return new Container({
      id: 'counter-container',
      className: 'counter',
      dataAttributes: { testid: 'counter' },
children: [
new Heading({
          content: 'eQuantic.UI Counter',
          className: 'title',
})        ,
new TextInput({
          id: 'message-input',
          value: this._message,
          placeholder: 'Type something...',
          onChange: (value) => this.setState(() => this._message = value),
          ariaAttributes: { label: 'Message input' },
})        ,
new Row({
          gap: '8px',
          justify: 'center',
children: [
new Button({
              id: 'decrement-btn',
              className: 'btn btn-secondary',
              onClick: this._decrement,
              text: '-',
})            ,
new Text({
              content: `${this._count}`,
              className: 'count-display',
})            ,
new Button({
              id: 'increment-btn',
              className: 'btn btn-primary',
              onClick: this._increment,
              text: '+',
})            ,
]
})        ,
]
})    ;
  }
}
