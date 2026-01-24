using eQuantic.UI.Core;
using eQuantic.UI.Components;
using eQuantic.UI.Components.Inputs;
using eQuantic.UI.Components.Surfaces;
using eQuantic.UI.Components.Feedback;

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
                ClassName = "items-center p-2 border-b border-gray-100 dark:border-zinc-700",
                Gap = "10px",
                Children = {
                    new Checkbox {
                        Checked = todo.IsCompleted,
                        OnChange = _ => { HandleToggle(todo.Id); }
                    },
                    new Text(todo.Title) {
                        ClassName = todo.IsCompleted ? "line-through text-gray-400" : "flex-1"
                    },
                    new Button {
                        Text = "Delete",
                        ClassName = "px-2 py-1 text-xs text-red-600 hover:bg-red-50 rounded",
                        OnClick = () => { HandleDelete(todo.Id); }
                    }
                }
            });
        }

        return new Container
        {
            ClassName = "min-h-screen bg-gray-50 dark:bg-zinc-900 p-8 flex justify-center",
            Children = {
                new Card
                {
                    Width = "w-full max-w-md",
                    Shadow = Shadow.Large,
                    Header = new Row 
                    { 
                        ClassName = "items-center justify-between",
                        Children = { 
                            new Heading("Tasks", 2) { ClassName = "text-xl font-bold text-gray-800 dark:text-white" },
                            new Text($"{_todos.Count(t => !t.IsCompleted)} remaining") { ClassName = "text-sm text-gray-500" }
                        }
                    },
                    Body = new Column
                    {
                        Gap = "20px",
                        Children = {
                            new Alert 
                            { 
                                Type = AlertType.Info, 
                                Message = "Welcome to eQuantic.UI with Tailwind!" 
                            },
                            
                            new Row {
                                Gap = "10px",
                                Children = {
                                    new TextInput {
                                        Value = _newTodoTitle,
                                        Placeholder = "New task...",
                                        ClassName = "flex-1 px-3 py-2 border rounded border-gray-300 focus:ring-2 focus:ring-blue-500",
                                        OnInput = val => SetState(() => _newTodoTitle = val)
                                    },
                                    new Button {
                                        Text = "Add",
                                        ClassName = "px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 font-medium",
                                        OnClick = () => { HandleAdd(); }
                                    }
                                }
                            },
                             
                            todoList
                        }
                    }
                }
            }
        };
    }
}
