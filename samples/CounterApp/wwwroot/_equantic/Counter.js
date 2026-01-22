import { StatefulComponent, StatelessComponent, Container, Text, Button, TextInput, Row, Column, Flex } from '@equantic/ui-runtime';

export class Counter extends StatefulComponent {
  createState() {
    return new CounterState(this);
  }
}

class CounterState {
  _count = 0;
  _message = "";

  constructor(component) {
    this._component = component;
    this._needsRender = false;
  }

  setState(fn) {
    fn();
    this._needsRender = true;
    this._component._scheduleRender();
  }

  _increment() { this.setState(() => { this._count++; }); }

  _decrement() { this.setState(() => { this._count--; }); }

  build(context) {
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
