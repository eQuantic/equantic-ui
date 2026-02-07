using eQuantic.UI.Core;
using eQuantic.UI.Components;
using eQuantic.UI.Components.Inputs;
using eQuantic.UI.Components.Surfaces;
using eQuantic.UI.Components.Feedback;
using eQuantic.UI.Components.Overlays;
using eQuantic.UI.Core.Theme.Types;
using eQuantic.UI.Core.Metadata;
using eQuantic.UI.Core.Styling;
using eQuantic.UI.Tailwind;

using TailwindDashboard.Models;
using TailwindDashboard.Services;
using TailwindDashboard.Components;

namespace TailwindDashboard.Pages;

public enum TodoFilter { All, Active, Completed }

[Page("/tasks", Title = "Tasks")]
public class TodoList : StatefulComponent, IHandleMetadata
{
    public void ConfigureMetadata(SeoBuilder seo)
    {
        seo.Title("My Tasks | TailwindDashboard")
           .Description("Manage your daily tasks efficiently with eQuantic.UI Dashboard")
           .Keywords("equantic", "ui", "framework", "dotnet", "dashboard")
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

    public override void OnInit()
    {
        if (_todos.Count == 0)
        {
            try
            {
                _todos = Component.GetTodos().GetAwaiter().GetResult();
            }
            catch
            {
            }
        }
    }

    public override void OnMount()
    {
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
            ClassName = "flex flex-col gap-2"
        };
        
        if (!filteredTodos.Any())
        {
            todoList.Children.Add(new Box {
                ClassName = "py-8 text-center text-zinc-500 bg-zinc-50 dark:bg-zinc-900 rounded-lg border-2 border-dashed",
                Children = { new Text("No tasks found in this view.") }
            });
        }
        else
        {
            foreach (var todo in filteredTodos)
            {
                var todoId = todo.Id;
                var todoTitle = todo.Title;
                var isCompleted = todo.IsCompleted;
                
                todoList.Children.Add(new Box {
                    ClassName = "flex items-center justify-between p-4 bg-white dark:bg-zinc-800 rounded-lg border shadow-sm",
                    Children = {
                        new Box {
                            ClassName = "flex items-center gap-3",
                            Children = {
                                new Checkbox {
                                    Checked = isCompleted,
                                    IsNative = true,
                                    OnChange = (bool val) => { _ = HandleToggle(todoId); }
                                },
                                new Text(todoTitle) {
                                    ClassName = isCompleted ? "line-through text-zinc-400" : "font-medium"
                                }
                            }
                        },
                        new Box {
                            ClassName = "flex items-center gap-2",
                            Children = {
                                new Button {
                                    Text = "✏️",
                                    Variant = Variant.Ghost,
                                    OnClick = () => { OpenEdit(todo); }
                                },
                                new Button {
                                    Text = "🗑️",
                                    Variant = Variant.Ghost,
                                    OnClick = () => { _ = HandleDelete(todoId); }
                                }
                            }
                        }
                    }
                });
            }
        }

        var mainContent = new Box
        {
            ClassName = "flex flex-col gap-6",
            Children = {
                new Box {
                    ClassName = "flex items-center justify-between",
                    Children = {
                        new Box {
                            ClassName = "flex items-center gap-2",
                            Children = {
                                CreateFilterButton(TodoFilter.All, "All", _todos.Count),
                                CreateFilterButton(TodoFilter.Active, "Active", _todos.Count(t => !t.IsCompleted)),
                                CreateFilterButton(TodoFilter.Completed, "Done", _todos.Count(t => t.IsCompleted))
                            }
                        }
                    }
                },
                new Card {
                    ClassName = "bg-white dark:bg-zinc-900 border",
                    Header = new Box {
                        ClassName = "p-4 border-b flex gap-3",
                        Children = {
                            new TextInput {
                                Value = _newTodoTitle,
                                Placeholder = "Add a new task...",
                                ClassName = "flex-1",
                                OnInput = val => { SetState(() => { _newTodoTitle = val; }); }
                            },
                            new Button {
                                Text = "Add Task",
                                Variant = Variant.Primary,
                                OnClick = () => { _ = HandleAdd(); }
                            }
                        }
                    },
                    Body = new Box {
                        ClassName = "p-4",
                        Children = { todoList }
                    },
                    Footer = _todos.Any(t => t.IsCompleted) ? new Box {
                        ClassName = "p-4 border-t flex justify-end",
                        Children = {
                            new Button {
                                Text = "Clear Completed",
                                Variant = Variant.Ghost,
                                ClassName = "text-red-500",
                                OnClick = () => { _ = HandleClearCompleted(); }
                            }
                        }
                    } : null
                }
            }
        };

        return new DashboardShell
        {
            PageTitle = "Tasks Management",
            ActivePath = "/tasks",
            Children = {
                mainContent,
                new Modal
                {
                    IsOpen = _isEditing,
                    Title = "Edit Task",
                    OnClose = () => { SetState(() => { _isEditing = false; }); },
                    Body = new Box {
                        ClassName = "p-4",
                        Children = { 
                            new TextInput {
                                Value = _editingTitle,
                                OnInput = val => { SetState(() => { _editingTitle = val; }); }
                            }
                        }
                    },
                    Footer = new Box {
                        ClassName = "p-4 border-t flex justify-end gap-2",
                        Children = {
                            new Button {
                                Text = "Cancel",
                                Variant = Variant.Ghost,
                                OnClick = () => { SetState(() => { _isEditing = false; }); }
                            },
                            new Button {
                                Text = "Save",
                                Variant = Variant.Primary,
                                OnClick = () => { _ = SaveEdit(); }
                            }
                        }
                    }
                }
            }
        };
    }

    private Button CreateFilterButton(TodoFilter filter, string label, int count)
    {
        var isActive = _currentFilter == filter;
        return new Button
        {
            Text = $"{label} ({count})",
            Variant = isActive ? Variant.Primary : Variant.Ghost,
            ClassName = isActive ? "bg-zinc-900 text-zinc-50 dark:bg-zinc-50 dark:text-zinc-900" : "",
            OnClick = () => { SetState(() => { _currentFilter = filter; }); }
        };
    }
}
