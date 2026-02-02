using eQuantic.UI.Core;
using eQuantic.UI.Components;
using eQuantic.UI.Components.Inputs;
using eQuantic.UI.Components.Surfaces;
using eQuantic.UI.Components.Feedback;
using eQuantic.UI.Components.Overlays;
using eQuantic.UI.Core.Theme.Types;
using eQuantic.UI.Core.Metadata;
using eQuantic.UI.Tailwind;

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

    public override void OnInit()
    {
        // OnInit is called during SSR on the server AND during client initialization
        // Load data ONLY if we're on the server (not hydrating on client)
        // We detect this by checking if _todos is empty - the client will hydrate from __INITIAL_STATE__
        if (_todos.Count == 0)
        {
            try
            {
                _todos = Component.GetTodos().GetAwaiter().GetResult();
            }
            catch
            {
                // Ignore errors during client-side init - data will load in OnMount
            }
        }
    }

    public override void OnMount()
    {
        // OnMount is called ONLY on the client after DOM is ready
        // Only load if we don't have data (means we're not hydrating from SSR)
        if (_todos.Count == 0)
        {
            _ = LoadTodos();
        }
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
                    ClassName = $"relative flex flex-row items-center p-4 bg-gradient-to-r {(todo.IsCompleted ? "from-gray-50/50 to-gray-100/50 dark:from-zinc-800/30 dark:to-zinc-900/30" : "from-white to-blue-50/30 dark:from-zinc-800/80 dark:to-zinc-800/50")} backdrop-blur-sm rounded-xl transition-all duration-300 hover:scale-[1.02] hover:shadow-lg hover:shadow-blue-500/10 dark:hover:shadow-purple-500/10 group gap-[12px] border border-gray-200/50 dark:border-zinc-700/50",
                    Children = {
                        new Checkbox {
                            Checked = todo.IsCompleted,
                            ClassName = "scale-125 cursor-pointer transition-transform hover:scale-150",
                            OnChange = _ => { HandleToggle(todo.Id); }
                        },
                        new Text(todo.Title) {
                            ClassName = $"flex-1 transition-all text-base {(todo.IsCompleted ? "line-through text-gray-400 dark:text-gray-600" : "text-gray-900 dark:text-white font-medium")}",
                            Variant = Variant.Custom,
                        },
                        // Time badge (mock - você pode adicionar CreatedAt no modelo)
                        new Text(todo.IsCompleted ? "✓" : "•") {
                            ClassName = $"text-xs px-2 py-1 rounded-full {(todo.IsCompleted ? "bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400" : "bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400")} font-semibold"
                        },
                        new Box {
                            ClassName = "flex flex-row gap-[6px] opacity-0 group-hover:opacity-100 transition-all duration-200",
                            Children = {
                                new Button {
                                    Text = "✏️",
                                    Variant = Variant.Ghost,
                                    ClassName = "w-8 h-8 p-0 rounded-lg bg-blue-50 hover:bg-blue-100 dark:bg-blue-900/30 dark:hover:bg-blue-900/50 transition-all hover:scale-110",
                                    OnClick = () => { OpenEdit(todo); }
                                },
                                new Button {
                                    Text = "🗑️",
                                    Variant = Variant.Ghost,
                                    ClassName = "w-8 h-8 p-0 rounded-lg bg-red-50 hover:bg-red-100 dark:bg-red-900/30 dark:hover:bg-red-900/50 transition-all hover:scale-110",
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
            ClassName = "min-h-screen bg-gradient-to-br from-blue-50 via-indigo-50 to-purple-50 dark:from-zinc-950 dark:via-zinc-900 dark:to-zinc-950 p-8 grid place-items-center",
            Children = {
                // Floating Header with Badge
                new Box {
                    ClassName = "absolute top-6 left-6 flex items-center gap-3",
                    Children = {
                        new Box {
                            ClassName = "flex items-center gap-2 px-4 py-2 bg-white/80 dark:bg-zinc-800/80 backdrop-blur-xl rounded-full shadow-lg border border-white/20 dark:border-zinc-700/50",
                            Children = {
                                new Text("✨") { ClassName = "text-xl" },
                                new Text("TodoList") {
                                    ClassName = "font-bold text-transparent bg-clip-text bg-gradient-to-r from-blue-600 to-purple-600 dark:from-blue-400 dark:to-purple-400"
                                }
                            }
                        }
                    }
                },

                // Settings Button
                new Box {
                    ClassName = "absolute top-6 right-6",
                    Children = {
                        new Button {
                            Text = "⚙️",
                            Variant = Variant.Ghost,
                            ClassName = "w-10 h-10 rounded-full bg-white/80 dark:bg-zinc-800/80 backdrop-blur-xl shadow-lg border border-white/20 dark:border-zinc-700/50 hover:scale-110 hover:rotate-90 transition-all duration-300",
                            OnClick = () => { SetState(() => { _isDrawerOpen = true; }); }
                        }
                    }
                },

                // Main Card with Glass Effect
                new Card
                {
                    Width = "w-full max-w-2xl",
                    Shadow = Shadow.Large,
                    ClassName = "bg-white/95 dark:bg-zinc-900/95 backdrop-blur-2xl border border-white/20 dark:border-zinc-800/50 shadow-2xl shadow-blue-500/10 dark:shadow-purple-500/10",
                    Header = new Box {
                        ClassName = "w-full flex flex-col gap-[20px]",
                        Children = {
                            new Box
                            {
                                ClassName = "w-full flex flex-row items-center justify-between",
                                Children = {
                                    new Box {
                                        ClassName = "flex flex-col gap-1",
                                        Children = {
                                            new Heading("My Tasks", 1) { ClassName = "text-3xl font-bold bg-gradient-to-r from-gray-900 to-gray-700 dark:from-white dark:to-gray-300 bg-clip-text text-transparent" },
                                            new Text(GetMotivationalMessage()) {
                                                Variant = Variant.Custom,
                                                ClassName = "text-sm text-gray-500 dark:text-gray-400 italic"
                                            }
                                        }
                                    },
                                    new Box {
                                        ClassName = "flex flex-col items-end",
                                        Children = {
                                            new Text($"{_todos.Count(t => !t.IsCompleted)}") {
                                                ClassName = "text-4xl font-black text-transparent bg-clip-text bg-gradient-to-br from-blue-600 to-purple-600 dark:from-blue-400 dark:to-purple-400"
                                            },
                                            new Text("remaining") {
                                                Variant = Variant.Custom,
                                                ClassName = "text-xs text-gray-400 uppercase tracking-wider font-semibold"
                                            }
                                        }
                                    }
                                }
                            },
                            new Box {
                                ClassName = "flex flex-row gap-[10px] bg-gradient-to-r from-gray-100/80 to-gray-50/50 dark:from-zinc-800/50 dark:to-zinc-900/30 p-1.5 rounded-xl backdrop-blur-sm",
                                Children = {
                                    CreateFilterButton(TodoFilter.All, "📋 All", _todos.Count),
                                    CreateFilterButton(TodoFilter.Active, "⚡ Active", _todos.Count(t => !t.IsCompleted)),
                                    CreateFilterButton(TodoFilter.Completed, "✅ Done", _todos.Count(t => t.IsCompleted))
                                }
                            }
                        }
                    },
                    Body = new Box
                    {
                        ClassName = "flex flex-col gap-[24px]",
                        Children = {
                            new Box {
                                ClassName = "flex flex-row gap-[12px] p-2 bg-gradient-to-r from-blue-50/50 to-purple-50/50 dark:from-zinc-800/50 dark:to-zinc-900/50 rounded-xl border-2 border-dashed border-blue-200/50 dark:border-zinc-700/50 transition-all focus-within:border-blue-400 dark:focus-within:border-blue-500 focus-within:shadow-lg focus-within:shadow-blue-500/20",
                                Children = {
                                    new Text("➕") { ClassName = "text-2xl self-center ml-2" },
                                    new TextInput {
                                        Value = _newTodoTitle,
                                        Placeholder = "Add a new task...",
                                        ClassName = "flex-1 bg-transparent border-none focus:ring-0 text-base placeholder:text-gray-400 dark:placeholder:text-gray-600",
                                        OnInput = val => { SetState(() => { _newTodoTitle = val; }); }
                                    },
                                    new Button {
                                        Text = "Add Task",
                                        Variant = Variant.Primary,
                                        ClassName = "bg-gradient-to-r from-blue-600 to-purple-600 hover:from-blue-700 hover:to-purple-700 text-white font-semibold px-6 shadow-lg shadow-blue-500/30 transition-all hover:scale-105",
                                        OnClick = () => { HandleAdd(); }
                                    }
                                }
                            },

                            todoList
                        }
                    },
                    Footer = new Box {
                        ClassName = "flex flex-row items-center justify-between pt-4 border-t border-gray-200/50 dark:border-zinc-800/50",
                        Children = {
                            new Box {
                                ClassName = "flex gap-4 text-sm",
                                Children = {
                                    new Box {
                                        ClassName = "flex items-center gap-2",
                                        Children = {
                                            new Text("📊") { ClassName = "text-lg" },
                                            new Text($"Total: {_todos.Count}") {
                                                Variant = Variant.Custom,
                                                ClassName = "font-semibold text-gray-700 dark:text-gray-300"
                                            }
                                        }
                                    },
                                    new Box {
                                        ClassName = "flex items-center gap-2",
                                        Children = {
                                            new Text("✅") { ClassName = "text-lg" },
                                            new Text($"Completed: {_todos.Count(t => t.IsCompleted)}") {
                                                Variant = Variant.Custom,
                                                ClassName = "font-semibold text-green-600 dark:text-green-400"
                                            }
                                        }
                                    }
                                }
                            },
                            _todos.Any(t => t.IsCompleted) ? new Button {
                                Text = "🧹 Clear Completed",
                                Variant = Variant.Ghost,
                                ClassName = "text-xs font-semibold px-4 py-2 bg-red-50 hover:bg-red-100 dark:bg-red-900/20 dark:hover:bg-red-900/40 text-red-600 dark:text-red-400 rounded-lg transition-all hover:scale-105",
                                OnClick = () => { HandleClearCompleted(); }
                            } : null
                        }
                    }
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

    private string GetMotivationalMessage()
    {
        var activeCount = _todos.Count(t => !t.IsCompleted);
        if (activeCount == 0 && _todos.Count > 0) return "All done! Time to celebrate! 🎉";
        if (activeCount == 0) return "Ready to start your day? ✨";
        if (activeCount == 1) return "Almost there! One more to go! 💪";
        if (activeCount <= 3) return "You're doing great! Keep it up! 🌟";
        return "Let's tackle these tasks! 🚀";
    }

    private Button CreateFilterButton(TodoFilter filter, string label, int count)
    {
        var isActive = _currentFilter == filter;

        return new Button
        {
            Text = $"{label} ({count})",
            Variant = isActive ? Variant.Primary : Variant.Ghost,
            ClassName = "flex-1 px-4 py-2.5 text-sm font-semibold rounded-lg transition-all " +
                        (isActive ? "shadow-lg bg-gradient-to-r from-blue-600 to-purple-600 text-white" :
                                   "hover:bg-gray-200 dark:hover:bg-zinc-700/50 text-gray-600 dark:text-gray-400"),
            OnClick = () => { SetState(() => { _currentFilter = filter; }); }
        };
    }
}
