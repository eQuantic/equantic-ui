using eQuantic.UI.Core;
using eQuantic.UI.Components;
using eQuantic.UI.Components.Inputs;
using eQuantic.UI.Components.Surfaces;
using eQuantic.UI.Components.Feedback;
using eQuantic.UI.Components.Overlays;
using eQuantic.UI.Core.Theme.Types;
using eQuantic.UI.Core.Metadata;

using TodoListApp.Models;
using TodoListApp.Services;

namespace TodoListApp.Pages;

public enum TodoFilter { All, Active, Completed }

[Page("/", Title = "Todo List")]
public class TodoList : StatefulComponent, IHandleMetadata
{
    public void ConfigureMetadata(SeoBuilder seo)
    {
        seo.Title("My Tasks | TodoList App")
           .Description("Manage your daily tasks efficiently with eQuantic.UI")
           .Keywords("equantic", "ui", "framework", "dotnet", "todolist")
           .Twitter("card", "summary");
    }
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

        var todoList = new Box 
        {
            ClassName = "flex flex-col gap-[5px]"
        };
        
        if (!filteredTodos.Any())
        {
            todoList.Children.Add(new Box {
                ClassName = "py-8 text-center",
                Children = { 
                    new Text("No tasks found in this view.") { Variant = Variant.Custom, ClassName = "text-gray-400" } 
                }
            });
        }
        else
        {
            foreach (var todo in filteredTodos)
            {
                todoList.Children.Add(new Box {
                    ClassName = "flex flex-row items-center p-2 border-b border-gray-100 dark:border-zinc-700 bg-white dark:bg-zinc-800 rounded transition-all hover:bg-gray-50 dark:hover:bg-zinc-700/50 group gap-[10px]",
                    Children = {
                        new Checkbox {
                            Checked = todo.IsCompleted,
                            OnChange = _ => { HandleToggle(todo.Id); }
                        },
                        new Text(todo.Title) {
                            ClassName = "flex-1 transition-all",
                            Variant = todo.IsCompleted ? Variant.Custom : Variant.Primary,
                        },
                        new Box {
                            ClassName = "flex flex-row gap-[4px] opacity-0 group-hover:opacity-100 transition-opacity",
                            Children = {
                                new Button {
                                    Text = "Edit",
                                    Variant = Variant.Ghost,
                                    ClassName = "px-2 py-1 text-xs text-blue-600 hover:text-blue-700",
                                    OnClick = () => { OpenEdit(todo); }
                                },
                                new Button {
                                    Text = "Delete",
                                    Variant = Variant.Ghost,
                                    ClassName = "px-2 py-1 text-xs text-red-600 hover:text-red-700",
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
                            Text = $"⚙️ Settings ({DateTime.Now:HH:mm})",
                            Variant = Variant.Outline,
                            ClassName = "bg-white dark:bg-zinc-800 text-sm",
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
                                    new Heading("Tasks", 2),
                                    new Text($"{_todos.Count(t => !t.IsCompleted):N0} remaining") { Variant = Variant.Custom, ClassName = "text-gray-400" }
                                }
                            },
                            new Box {
                                ClassName = "flex flex-row gap-[8px] bg-gray-100/50 dark:bg-zinc-900/30 p-1 rounded-lg",
                                Children = {
                                    CreateFilterButton(TodoFilter.All, "All"),
                                    CreateFilterButton(TodoFilter.Active, "Active"),
                                    CreateFilterButton(TodoFilter.Completed, "Completed")
                                }
                            }
                        }
                    },
                    Body = new Box
                    {
                        ClassName = "flex flex-col gap-[20px]",
                        Children = {
                            new Box {
                                ClassName = "flex flex-row gap-[10px]",
                                Children = {
                                    new TextInput {
                                        Value = _newTodoTitle,
                                        Placeholder = "New task...",
                                        ClassName = "flex-1",
                                        OnInput = val => { SetState(() => { _newTodoTitle = val; }); }
                                    },
                                    new Button {
                                        Text = "Add",
                                        Variant = Variant.Primary,
                                        OnClick = () => { HandleAdd(); }
                                    }
                                }
                            },
                             
                            todoList
                        }
                    },
                    Footer = _todos.Any(t => t.IsCompleted) ? new Box {
                        ClassName = "flex flex-row justify-center",
                        Children = {
                            new Button {
                                Text = "Clear Completed",
                                Variant = Variant.Link,
                                ClassName = "text-xs uppercase font-semibold tracking-wider text-red-500 hover:text-red-700",
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
                    Body = new Box {
                        ClassName = "flex flex-col gap-[10px]",
                        Children = { 
                            new Text("Update the task title:") { Variant = Variant.Custom, ClassName = "text-gray-400" },
                            new TextInput {
                                Value = _editingTitle,
                                OnInput = val => { SetState(() => { _editingTitle = val; }); }
                            }
                        }
                    },
                    Footer = new Box {
                        ClassName = "flex flex-row gap-[10px]",
                        Children = {
                            new Button {
                                Text = "Cancel",
                                Variant = Variant.Ghost,
                                OnClick = () => { SetState(() => { _isEditing = false; }); }
                            },
                            new Button {
                                Text = "Save Changes",
                                Variant = Variant.Primary,
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
                    Content = new Box {
                        ClassName = "flex flex-col gap-[20px]",
                        Children = {
                            new Heading("Settings", 3) { ClassName = "mb-4" },
                            new Text("Theme preferences and other settings will go here.") { Variant = Variant.Ghost },
                            new Box { 
                                ClassName = "p-3 rounded-lg bg-green-50 dark:bg-green-900/20 border border-green-200 dark:border-green-800",
                                Children = {
                                    new Text("eQuantic.UI is now running with Bun + Tailwind 100%!") { 
                                        Variant = Variant.Custom, 
                                        ClassName = "text-sm text-green-800 dark:text-green-300 font-medium" 
                                    }
                                }
                            },
                            new Container {
                                ClassName = "mt-auto pt-8 border-t border-gray-100 dark:border-zinc-800",
                                Children = {
                                    new Button {
                                        Text = "Back to List",
                                        Variant = Variant.Secondary,
                                        ClassName = "w-full",
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
        // Simplified logic: rely on variants or minimal overrides
        
        return new Button
        {
            Text = label,
            Variant = isActive ? Variant.Primary : Variant.Ghost,
            ClassName = "flex-1 px-3 py-1.5 text-xs font-semibold rounded-md transition-all " + (isActive ? "shadow-sm" : "hover:bg-gray-200 dark:hover:bg-zinc-700/50"),
            OnClick = () => { SetState(() => { _currentFilter = filter; }); }
        };
    }
}
