using eQuantic.UI.Core;
using eQuantic.UI.Components;
using TodoListApp.Components;
using TodoListApp.Models;
using TodoListApp.Services;

namespace TodoListApp.Pages;

[Page("/", Title = "Todo List")]
public class TodoList : StatefulComponent
{
    private readonly ITodoService _todoService;

    public TodoList(ITodoService todoService)
    {
        _todoService = todoService;
    }

    [ServerAction]
    public async Task<List<Todo>> GetTodos()
    {
        return await _todoService.GetTodosAsync();
    }

    [ServerAction]
    public async Task<Todo> AddTodo(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title cannot be empty");
        return await _todoService.AddTodoAsync(title);
    }

    [ServerAction]
    public async Task ToggleTodo(Guid id)
    {
        await _todoService.ToggleTodoAsync(id);
    }

    [ServerAction]
    public async Task DeleteTodo(Guid id)
    {
        await _todoService.DeleteTodoAsync(id);
    }

    public override ComponentState CreateState() => new TodoListState();
}

public class TodoListState : ComponentState<TodoList>
{
    private List<Todo> _todos = new();
    private string _newTodoTitle = "";

    public override void OnMount()
    {
        _ = LoadTodos();
    }

    private async Task LoadTodos()
    {
        _todos = await Component.GetTodos();
        SetState(() => { });
    }

    private async Task HandleAdd()
    {
        if (string.IsNullOrWhiteSpace(_newTodoTitle)) return;
        
        await Component.AddTodo(_newTodoTitle);
        _newTodoTitle = "";
        await LoadTodos();
    }

    private async Task HandleToggle(Guid id)
    {
        await Component.ToggleTodo(id);
        await LoadTodos(); // Refresh to ensure sync
    }

    private async Task HandleDelete(Guid id)
    {
        await Component.DeleteTodo(id);
        await LoadTodos();
    }

    public override IComponent Build(RenderContext context)
    {
        var todoList = new Column 
        {
            Gap = "5px"
        };
        
        foreach (var todo in _todos)
        {
            todoList.Children.Add(new Row {
                ClassName = "todo-item",
                Gap = "10px",
                Children = {
                    new Checkbox {
                        Checked = todo.IsCompleted,
                        OnChange = _ => { HandleToggle(todo.Id); }
                    },
                    new Text(todo.Title) {
                        ClassName = todo.IsCompleted ? "completed" : ""
                    },
                    new Button {
                        Text = "X",
                        ClassName = "btn btn-danger btn-sm",
                        OnClick = () => { HandleDelete(todo.Id); }
                    }
                }
            });
        }

        return new Container
        {
            ClassName = "todo-container",
            Children = {
                new Heading("eQuantic Todo List", 1),
                
                new Row {
                    Gap = "10px",
                    Children = {
                        new TextInput {
                            Value = _newTodoTitle,
                            Placeholder = "What needs to be done?",
                            OnChange = val => SetState(() => _newTodoTitle = val)
                        },
                        new Button {
                            Text = "Add",
                            ClassName = "btn btn-primary",
                            OnClick = () => { HandleAdd(); }
                        }
                    }
                },
                
                todoList
            }
        };
    }
}
