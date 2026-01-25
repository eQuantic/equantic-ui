using eQuantic.UI.Core;
using eQuantic.UI.Components;
using eQuantic.UI.Components.Inputs;
using eQuantic.UI.Components.Surfaces;
using eQuantic.UI.Components.Feedback;
using eQuantic.UI.Components.Overlays;

using TodoListApp.Models;
using TodoListApp.Services;

namespace TodoListApp.Pages;

public enum TodoFilter { All, Active, Completed }

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

    [ServerAction]
    public async Task UpdateTodo(Guid id, string title)
    {
        await _todoService.UpdateTodoAsync(id, title);
    }

    [ServerAction]
    public async Task ClearCompleted()
    {
        var todos = await _todoService.GetTodosAsync();
        foreach (var todo in todos)
        {
            if (todo.IsCompleted)
            {
                await _todoService.DeleteTodoAsync(todo.Id);
            }
        }
    }

    public override ComponentState CreateState() => new TodoListState();
}

public class TodoListState : ComponentState<TodoList>
{
    private List<Todo> _todos = new();
    private string _newTodoTitle = "";
    private TodoFilter _currentFilter = TodoFilter.All;
    
    // Modal State
    private bool _isEditing = false;
    private Guid _editingId;
    private string _editingTitle = "";

    // Drawer State
    private bool _isDrawerOpen = false;

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
        await LoadTodos(); 
    }

    private async Task HandleDelete(Guid id)
    {
        await Component.DeleteTodo(id);
        await LoadTodos();
    }

    private async Task HandleClearCompleted()
    {
        await Component.ClearCompleted();
        await LoadTodos();
    }

    private void OpenEdit(Todo todo)
    {
        SetState(() => {
            _isEditing = true;
            _editingId = todo.Id;
            _editingTitle = todo.Title;
        });
    }

    private async Task SaveEdit()
    {
        if (string.IsNullOrWhiteSpace(_editingTitle)) return;
        await Component.UpdateTodo(_editingId, _editingTitle);
        SetState(() => {
            _isEditing = false;
        });
        await LoadTodos();
    }

    public override IComponent Build(RenderContext context)
    {
        var filteredTodos = _currentFilter switch
        {
            TodoFilter.Active => _todos.Where(t => !t.IsCompleted).ToList(),
            TodoFilter.Completed => _todos.Where(t => t.IsCompleted).ToList(),
            _ => _todos
        };

        var todoList = new Column 
        {
            Gap = "5px"
        };
        
        if (!filteredTodos.Any())
        {
            todoList.Children.Add(new Box {
                ClassName = "py-8 text-center text-gray-500 italic",
                Children = { new Text("No tasks found in this view.") }
            });
        }
        else
        {
            foreach (var todo in filteredTodos)
            {
                todoList.Children.Add(new Row {
                    ClassName = "items-center p-2 border-b border-gray-100 dark:border-zinc-700 bg-white dark:bg-zinc-800 rounded transition-all hover:bg-gray-50 dark:hover:bg-zinc-700/50 group",
                    Gap = "10px",
                    Children = {
                        new Checkbox {
                            Checked = todo.IsCompleted,
                            OnChange = _ => { HandleToggle(todo.Id); }
                        },
                        new Text(todo.Title) {
                            ClassName = $"flex-1 { (todo.IsCompleted ? "line-through text-gray-400" : "text-gray-900 dark:text-gray-100") }"
                        },
                        new Row {
                            Gap = "4px",
                            ClassName = "opacity-0 group-hover:opacity-100 transition-opacity",
                            Children = {
                                new Button {
                                    Text = "Edit",
                                    ClassName = "px-2 py-1 text-xs text-blue-600 hover:bg-blue-50 dark:hover:bg-blue-900/30 rounded",
                                    OnClick = () => { OpenEdit(todo); }
                                },
                                new Button {
                                    Text = "Delete",
                                    ClassName = "px-2 py-1 text-xs text-red-600 hover:bg-red-50 dark:hover:bg-red-900/30 rounded",
                                    OnClick = () => { HandleDelete(todo.Id); }
                                }
                            }
                        }
                    }
                });
            }
        }

        return new Box
        {
            ClassName = "min-h-screen bg-gray-50 dark:bg-zinc-900 p-8 grid place-items-center",
            Children = {
                // Header Actions
                new Box {
                    ClassName = "absolute top-4 right-4",
                    Children = {
                        new Button {
                            Text = "⚙️ Settings",
                            ClassName = "px-3 py-2 bg-white dark:bg-zinc-800 border border-gray-200 dark:border-zinc-700 rounded-lg shadow-sm hover:shadow-md text-sm",
                            OnClick = () => { SetState(() => { _isDrawerOpen = true; }); }
                        }
                    }
                },

                // Main Card
                new Card
                {
                    Width = "w-full max-w-md",
                    Shadow = Shadow.Large,
                    Header = new Box {
                        ClassName = "w-full flex flex-col gap-[15px]",
                        Children = {
                            new Box 
                            { 
                                ClassName = "w-full flex flex-row items-center justify-between",
                                Children = { 
                                    new Heading("Tasks", 2) { ClassName = "text-xl font-bold text-gray-800 dark:text-white" },
                                    new Text($"{_todos.Count(t => !t.IsCompleted)} remaining") { ClassName = "text-sm text-gray-500" }
                                }
                            },
                            new Row {
                                Gap = "8px",
                                ClassName = "bg-gray-100/50 dark:bg-zinc-900/30 p-1 rounded-lg",
                                Children = {
                                    CreateFilterButton(TodoFilter.All, "All"),
                                    CreateFilterButton(TodoFilter.Active, "Active"),
                                    CreateFilterButton(TodoFilter.Completed, "Completed")
                                }
                            }
                        }
                    },
                    Body = new Column
                    {
                        Gap = "20px",
                        Children = {
                            new Row {
                                Gap = "10px",
                                Children = {
                                    new TextInput {
                                        Value = _newTodoTitle,
                                        Placeholder = "New task...",
                                        ClassName = "flex-1 px-3 py-2 border rounded border-gray-300 dark:border-zinc-600 dark:bg-zinc-800 dark:text-white focus:ring-2 focus:ring-blue-500 outline-none transition-all",
                                        OnInput = val => { SetState(() => { _newTodoTitle = val; }); }
                                    },
                                    new Button {
                                        Text = "Add",
                                        ClassName = "px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 font-medium shadow-sm transition-colors",
                                        OnClick = () => { HandleAdd(); }
                                    }
                                }
                            },
                             
                            todoList
                        }
                    },
                    Footer = _todos.Any(t => t.IsCompleted) ? new Row {
                        ClassName = "justify-center",
                        Children = {
                            new Button {
                                Text = "Clear Completed",
                                ClassName = "text-sm text-red-500 hover:text-red-700 transition-colors uppercase font-semibold tracking-wider",
                                OnClick = () => { HandleClearCompleted(); }
                            }
                        }
                    } : null
                },

                // Edit Modal
                new Modal
                {
                    IsOpen = _isEditing,
                    Title = "Edit Task",
                    OnClose = () => { SetState(() => { _isEditing = false; }); },
                    Body = new Column {
                        Gap = "10px",
                        Children = {
                            new Text("Update the task title:") { ClassName = "text-sm text-gray-500 dark:text-gray-400" },
                            new TextInput {
                                Value = _editingTitle,
                                ClassName = "w-full px-3 py-2 border rounded border-gray-300 dark:border-zinc-600 dark:bg-zinc-800 dark:text-white focus:ring-2 focus:ring-blue-500 outline-none",
                                OnInput = val => { SetState(() => { _editingTitle = val; }); }
                            }
                        }
                    },
                    Footer = new Row {
                        Gap = "10px",
                        Children = {
                            new Button {
                                Text = "Cancel",
                                ClassName = "px-4 py-2 text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-zinc-700 rounded transition-colors",
                                OnClick = () => { SetState(() => { _isEditing = false; }); }
                            },
                            new Button {
                                Text = "Save Changes",
                                ClassName = "px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 transition-colors font-medium",
                                OnClick = () => { SaveEdit(); }
                            }
                        }
                    }
                },

                // Settings Drawer
                new Drawer
                {
                    IsOpen = _isDrawerOpen,
                    Side = DrawerSide.Right,
                    OnClose = () => { SetState(() => { _isDrawerOpen = false; }); },
                    Content = new Column {
                        Gap = "20px",
                        Children = {
                            new Heading("Settings", 3) { ClassName = "text-xl font-bold mb-4 dark:text-white" },
                            new Text("Theme preferences and other settings will go here.") { ClassName = "text-gray-600 dark:text-gray-400" },
                            new Alert { Type = AlertType.Success, Message = "eQuantic.UI is now running with Bun + Tailwind 4!" },
                            new Container {
                                ClassName = "mt-auto pt-8 border-t border-gray-100 dark:border-zinc-800",
                                Children = {
                                    new Button {
                                        Text = "Back to List",
                                        ClassName = "w-full py-2 bg-gray-100 dark:bg-zinc-800 text-gray-700 dark:text-gray-300 rounded hover:bg-gray-200 dark:hover:bg-zinc-700 transition-colors",
                                        OnClick = () => { SetState(() => { _isDrawerOpen = false; }); }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };
    }

    private Button CreateFilterButton(TodoFilter filter, string label)
    {
        var isActive = _currentFilter == filter;
        var activeClass = isActive 
            ? "bg-white dark:bg-zinc-800 text-blue-600 shadow-sm" 
            : "text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200";

        return new Button
        {
            Text = label,
            ClassName = "flex-1 px-3 py-1.5 text-xs font-semibold rounded-md transition-all " + activeClass,
            OnClick = () => { SetState(() => { _currentFilter = filter; }); }
        };
    }
}
