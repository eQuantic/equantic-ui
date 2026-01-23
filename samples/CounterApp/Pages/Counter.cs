// Counter.cs - Sample eQuantic.UI Page Component
using eQuantic.UI.Core;
using eQuantic.UI.Components;

namespace CounterApp.Pages;

[Page("/", Title = "Counter Demo")]
[Page("/counter")]
public class Counter : StatefulComponent
{
    /// <summary>
    /// Example Server Action - processes on the server and returns result.
    /// </summary>
    [ServerAction]
    public async Task<int> IncrementOnServer(int current)
    {
        // Simulate server-side processing
        await Task.Delay(100);
        return current + 1;
    }
    
    public override ComponentState CreateState() => new CounterState();
}

public class CounterState : ComponentState<Counter>
{
    private int _count = 0;
    private string _message = "";

    private void _increment() => SetState(() => _count++);
    private void _decrement() => SetState(() => _count--);

    public override IComponent Build(RenderContext context)
    {
        return new Container
        {
            Id = "counter-container",
            ClassName = "counter",
            DataAttributes = new() { ["testid"] = "counter" },
            Children =
            {
                new Heading("eQuantic.UI Counter", 1)
                {
                    ClassName = "title"
                },
                
                new TextInput
                {
                    Id = "message-input",
                    Value = _message,
                    Placeholder = "Type something...",
                    OnChange = (value) => SetState(() => _message = value),
                    AriaAttributes = new() { ["label"] = "Message input" }
                },
                
                new Row
                {
                    Gap = "8px",
                    Justify = JustifyContent.Center,
                    Children =
                    {
                        new Button
                        {
                            Id = "decrement-btn",
                            ClassName = "btn btn-secondary",
                            OnClick = _decrement,
                            Text = "-"
                        },
                        
                        new Text($"{_count}")
                        {
                            ClassName = "count-display"
                        },
                        
                        new Button
                        {
                            Id = "increment-btn",
                            ClassName = "btn btn-primary",
                            OnClick = _increment,
                            Text = "+"
                        }
                    }
                },
                
                _count > 0
                    ? new Text($"Message: {_message}")
                        { ClassName = "message-display" }
                    : null
            }
        };
    }
}
