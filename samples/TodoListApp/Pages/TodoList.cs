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
            // Example using ClassBuilder with conditional logic, state variants, and responsive classes
            var emptyStateClasses = ClassBuilder.Create()
                .Add(TW.Py(8), TW.Text.Center)
                .When(_currentFilter == TodoFilter.Active, TW.Bg.Blue50, TW.Bg.Gray50)
                .Dark(TW.Bg.Zinc900)
                .Add(TW.Rounded.Lg, TW.P(4))
                .Hover(TW.Shadow.Lg)
                .Build();

            var emptyTextClasses = ClassBuilder.Create()
                .Add(TW.Text.Gray400)
                .When(_currentFilter == TodoFilter.Completed, TW.Text.Green400)
                .Dark(TW.Text.Gray500)
                .Add(TW.Font.Medium)
                .Sm(TW.Text.Base)
                .Md(TW.Text.Lg)
                .Build();

            todoList.Children.Add(new Box {
                ClassName = emptyStateClasses,
                Children = {
                    new Text("No tasks found in this view.") {
                        Variant = Variant.Custom,
                        ClassName = emptyTextClasses
                    }
                }
            });
        }
        else
        {
            foreach (var todo in filteredTodos)
            {
                // Original: "relative flex flex-row items-center p-4 bg-gradient-to-r {gradient} backdrop-blur-sm rounded-xl transition-all duration-300 hover:scale-[1.02] hover:shadow-lg hover:shadow-blue-500/10 dark:hover:shadow-purple-500/10 group gap-[12px] border border-gray-200/50 dark:border-zinc-700/50"
                var itemClasses = ClassBuilder.Create()
                    .Add(TW.Position.Relative, TW.Display.Flex, TW.Flex.Row, TW.Flex.ItemsCenter, TW.P(4))
                    .Add(TW.Bg.GradientToR)
                    .When(todo.IsCompleted, "from-gray-50/50", "to-gray-100/50", "dark:from-zinc-800/30", "dark:to-zinc-900/30")
                    .When(!todo.IsCompleted, "from-white", "to-blue-50/30", "dark:from-zinc-800/80", "dark:to-zinc-800/50")
                    .Add(TW.Backdrop.BlurSm, TW.Rounded.Xl)
                    .Add(TW.Transition.All, TW.Duration._300)
                    .Hover("scale-[1.02]", TW.Shadow.Lg, "shadow-blue-500/10")
                    .Dark("hover:shadow-purple-500/10")
                    .Add(TW.Group, "gap-[12px]")
                    .Add(TW.Border.Base, "border-gray-200/50")
                    .Dark("border-zinc-700/50")
                    .Build();

                var checkboxClasses = ClassBuilder.Create()
                    .Add(TW.Transform.Scale125, TW.Cursor.Pointer)
                    .Add(TW.Transition.Transform)
                    .Hover(TW.Transform.Scale150)
                    .Build();

                var titleClasses = ClassBuilder.Create()
                    .Add(TW.Flex.Flex1, TW.Transition.All, TW.Text.Base)
                    .When(todo.IsCompleted, TW.Text.LineThrough, TW.Text.Gray400)
                    .When(todo.IsCompleted, TW.Dark(TW.Text.Gray600))
                    .When(!todo.IsCompleted, TW.Text.Gray900, TW.Font.Medium)
                    .When(!todo.IsCompleted, TW.Dark(TW.Text.White))
                    .Build();

                todoList.Children.Add(new Box {
                    ClassName = itemClasses,
                    Children = {
                        new Checkbox {
                            Checked = todo.IsCompleted,
                            ClassName = checkboxClasses,
                            OnChange = _ => { HandleToggle(todo.Id); }
                        },
                        new Text(todo.Title) {
                            ClassName = titleClasses,
                            Variant = Variant.Custom,
                        },
                        // Time badge (mock - você pode adicionar CreatedAt no modelo)
                        new Text(todo.IsCompleted ? "✓" : "•") {
                            ClassName = ClassBuilder.Create()
                                .Add(TW.Text.Xs, TW.Px(2), TW.Py(1), TW.Rounded.Full, TW.Font.Semibold)
                                .When(todo.IsCompleted, TW.Bg.Green100, TW.Text.Green600) // Adjusted colors slightly
                                .When(todo.IsCompleted)
                                    .Dark(TW.WithOpacity(TW.Bg.Green800, 30), TW.Text.Green400)
                                .When(!todo.IsCompleted, TW.Bg.Blue100, TW.Text.Blue600)
                                .When(!todo.IsCompleted)
                                    .Dark(TW.WithOpacity(TW.Bg.Blue800, 30), TW.Text.Blue400)
                                .Build()
                        },
                        new Box {
                            ClassName = ClassBuilder.Create()
                                .Add(TW.Display.Flex, TW.Flex.Row, "gap-[6px]", TW.Opacity.Op0)
                                .GroupHover(TW.Opacity.Op100)
                                .Add(TW.Transition.All, TW.Duration._200)
                                .Build(),
                            Children = {
                                new Button {
                                    Text = "✏️",
                                    Variant = Variant.Ghost,
                                    ClassName = ClassBuilder.Create()
                                        .Add(TW.W(8), TW.H(8), TW.P(0), TW.Rounded.Lg)
                                        .Add(TW.Bg.Blue50)
                                        .Hover(TW.Bg.Blue100)
                                        .Dark(TW.WithOpacity(TW.Bg.Blue600, 30), TW.Hover(TW.WithOpacity(TW.Bg.Blue600, 50)))
                                        .Add(TW.Transition.All)
                                        .Hover(TW.Transform.Scale110)
                                        .Build(),
                                    OnClick = () => { OpenEdit(todo); }
                                },
                                new Button {
                                    Text = "🗑️",
                                    Variant = Variant.Ghost,
                                    ClassName = ClassBuilder.Create()
                                        .Add(TW.W(8), TW.H(8), TW.P(0), TW.Rounded.Lg)
                                        .Add(TW.Bg.Red50)
                                        .Hover(TW.Bg.Red100)
                                        .Dark(TW.WithOpacity(TW.Bg.Red600, 30), TW.Hover(TW.WithOpacity(TW.Bg.Red600, 50)))
                                        .Add(TW.Transition.All)
                                        .Hover(TW.Transform.Scale110)
                                        .Build(),
                                    OnClick = () => { HandleDelete(todo.Id); }
                                }
                            }
                        }
                    }
                });
            }
        }

        var mainContainerClasses = ClassBuilder.Create()
            .Add(TW.Size.MinHScreen, TW.P(8), TW.Display.Grid, TW.Grid.PlaceItemsCenter)
            .Add(TW.Bg.GradientToBr)
            .Add("from-blue-50 via-indigo-50 to-purple-50")
            .Dark("from-zinc-950 via-zinc-900 to-zinc-950")
            .Build();

        var headerContainerClasses = ClassBuilder.Create()
            .Add(TW.Position.Absolute, TW.Position.Top(6), TW.Position.Left(6))
            .Add(TW.Display.Flex, TW.Flex.Row, TW.Flex.ItemsCenter, TW.Gap(3))
            .Build();

        var badgeClasses = ClassBuilder.Create()
            .Add(TW.Display.Flex, TW.Flex.Row, TW.Flex.ItemsCenter, TW.Gap(2))
            .Add(TW.Px(4), TW.Py(2), TW.Rounded.Full)
            .Add(TW.WithOpacity(TW.Bg.White, 80), TW.Backdrop.BlurXl, TW.Shadow.Lg)
            .Dark(TW.WithOpacity(TW.Bg.Zinc800, 80))
            .Add(TW.Border.Base, "border-white/20")
            .Dark("border-zinc-700/50")
            .Build();

        var headerTitleClasses = ClassBuilder.Create()
            .Add(TW.Font.Bold, TW.Text.Transparent, TW.Bg.ClipText)
            .Add(TW.Bg.GradientToR, "from-blue-600 to-purple-600")
            .Dark("from-blue-400 to-purple-400")
            .Build();

        var settingsContainerClasses = ClassBuilder.Create()
            .Add(TW.Position.Absolute, TW.Position.Top(6), TW.Position.Right(6))
            .Build();

        var settingsButtonClasses = ClassBuilder.Create()
            .Add(TW.W(10), TW.H(10), TW.Rounded.Full)
            .Add(TW.WithOpacity(TW.Bg.White, 80), TW.Backdrop.BlurXl, TW.Shadow.Lg)
            .Dark(TW.WithOpacity(TW.Bg.Zinc800, 80))
            .Add(TW.Border.Base, "border-white/20")
            .Dark("border-zinc-700/50")
            .Hover(TW.Transform.Scale110, TW.Transform.Rotate90)
            .Add(TW.Transition.All, TW.Duration._300)
            .Build();

        return new Box
        {
            ClassName = mainContainerClasses,
            Children = {
                // Floating Header with Badge
                new Box {
                    ClassName = headerContainerClasses,
                    Children = {
                        new Box {
                            ClassName = badgeClasses,
                            Children = {
                                new Text("✨") { ClassName = TW.Text.Xl },
                                new Text("TodoList") {
                                    ClassName = headerTitleClasses
                                }
                            }
                        }
                    }
                },

                // Settings Button
                new Box {
                    ClassName = settingsContainerClasses,
                    Children = {
                        new Button {
                            Text = "⚙️",
                            Variant = Variant.Ghost,
                            ClassName = settingsButtonClasses,
                            OnClick = () => { SetState(() => { _isDrawerOpen = true; }); }
                        }
                    }
                },

                // Main Card with Glass Effect
                new Card
                {
                    Width = "w-full max-w-2xl",
                    Shadow = Shadow.Large,
                    ClassName = ClassBuilder.Create()
                        .Add(TW.WithOpacity(TW.Bg.White, 95), TW.Backdrop.Blur2xl)
                        .Dark(TW.WithOpacity(TW.Bg.Zinc900, 95))
                        .Add(TW.Border.Base, "border-white/20")
                        .Dark("border-zinc-800/50")
                        .Add(TW.Shadow.Xl2, "shadow-blue-500/10")
                        .Dark("shadow-purple-500/10")
                        .Build(),
                    Header = new Box {
                        ClassName = ClassBuilder.Create()
                            .Add(TW.WFull, TW.Display.Flex, TW.Flex.Col, "gap-[20px]")
                            .Build(),
                        Children = {
                            new Box
                            {
                                ClassName = ClassBuilder.Create()
                                    .Add(TW.WFull, TW.Display.Flex, TW.Flex.Row, TW.Flex.ItemsCenter, TW.Flex.JustifyBetween)
                                    .Build(),
                                Children = {
                                    new Box {
                                        ClassName = ClassBuilder.Create().Add(TW.Display.Flex, TW.Flex.Col, TW.Gap(1)).Build(),
                                        Children = {
                                            new Heading("My Tasks", 1) { 
                                                ClassName = ClassBuilder.Create()
                                                    .Add(TW.Text.Xl3, TW.Font.Bold, TW.Bg.ClipText, TW.Text.Transparent)
                                                    .Add(TW.Bg.GradientToR, "from-gray-900 to-gray-700")
                                                    .Dark("from-gray-100 to-gray-400")
                                                    .Build() 
                                            },
                                            new Text(GetMotivationalMessage()) {
                                                Variant = Variant.Custom,
                                                ClassName = ClassBuilder.Create()
                                                    .Add(TW.Text.Sm, TW.Text.Gray500, TW.Font.Italic)
                                                    .Dark(TW.Text.Gray400)
                                                    .Build()
                                            }
                                        }
                                    },
                                    new Box {
                                        ClassName = ClassBuilder.Create().Add(TW.Display.Flex, TW.Flex.Col, TW.Flex.ItemsEnd).Build(),
                                        Children = {
                                            new Text($"{_todos.Count(t => !t.IsCompleted)}") {
                                                ClassName = ClassBuilder.Create()
                                                    .Add(TW.Text.Xl4, TW.Font.Black, TW.Text.Transparent, TW.Bg.ClipText)
                                                    .Add(TW.Bg.GradientToBr, "from-blue-600 to-purple-600")
                                                    .Dark("from-blue-400 to-purple-400")
                                                    .Build()
                                            },
                                            new Text("remaining") {
                                                Variant = Variant.Custom,
                                                ClassName = ClassBuilder.Create().Add(TW.Text.Xs, TW.Text.Gray400, TW.Tracking.Wider, TW.Font.Semibold, TW.Text.Uppercase).Build()
                                            }
                                        }
                                    }
                                }
                            },
                            new Box {
                                ClassName = ClassBuilder.Create()
                                    .Add(TW.Display.Flex, TW.Flex.Row, "gap-[10px]")
                                    .Add("bg-gradient-to-r from-gray-100/80 to-gray-50/50")
                                    .Dark("from-zinc-800/50 to-zinc-900/30")
                                    .Add(TW.P(2), TW.Rounded.Xl, TW.Backdrop.BlurSm)
                                    .Build(),
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
                                ClassName = ClassBuilder.Create()
                                    .Add(TW.Display.Flex, TW.Flex.Row, "gap-[12px]", TW.P(2))
                                    .Add(TW.Bg.GradientToR, "from-blue-50/50 to-purple-50/50")
                                    .Dark("from-zinc-800/50 to-zinc-900/50")
                                    .Add(TW.Rounded.Xl, TW.Border.B2, TW.Border.Dashed)
                                    .Add("border-blue-200/50")
                                    .Dark("border-zinc-700/50")
                                    .Add(TW.Transition.All)
                                    .FocusWithin("border-blue-400", TW.Shadow.Lg, "shadow-blue-500/20")
                                    .Dark(TW.FocusWithin("border-blue-500"))
                                    .Build(),
                                Children = {
                                    new Text("➕") { ClassName = ClassBuilder.Create().Add(TW.Text.Xl2, TW.SelfCenter, TW.Ml(2)).Build() },
                                    new TextInput {
                                        Value = _newTodoTitle,
                                        Placeholder = "Add a new task...",
                                        ClassName = ClassBuilder.Create()
                                            .Add(TW.Flex.Flex1, TW.Bg.Transparent, TW.Border.None)
                                            .Add("focus:ring-0", TW.Text.Base)
                                            .Add(TW.Placeholder.Gray400)
                                            .Dark(TW.Placeholder.Gray600)
                                            .Build(),
                                        OnInput = val => { SetState(() => { _newTodoTitle = val; }); }
                                    },
                                    new Button {
                                        Text = "Add Task",
                                        Variant = Variant.Primary,
                                        ClassName = ClassBuilder.Create()
                                            .Add(TW.Bg.GradientToR, "from-blue-600 to-purple-600")
                                            .Hover("from-blue-700 to-purple-700")
                                            .Add(TW.Text.White, TW.Font.Semibold, TW.Px(6))
                                            .Add(TW.Shadow.Lg, "shadow-blue-500/30")
                                            .Add(TW.Transition.All)
                                            .Hover(TW.Transform.Scale105)
                                            .Build(),
                                        OnClick = () => { HandleAdd(); }
                                    }
                                }
                            },

                            todoList
                        }
                    },
                    Footer = new Box {
                        ClassName = ClassBuilder.Create()
                            .Add(TW.Display.Flex, TW.Flex.Row, TW.Flex.ItemsCenter, TW.Flex.JustifyBetween)
                            .Add(TW.Pt(4), TW.Border.T, "border-gray-200/50")
                            .Dark("border-zinc-800/50")
                            .Build(),
                        Children = {
                            new Box {
                                ClassName = ClassBuilder.Create().Add(TW.Display.Flex, TW.Flex.Row, TW.Gap(4), TW.Text.Sm).Build(),
                                Children = {
                                    new Box {
                                        ClassName = ClassBuilder.Create().Add(TW.Display.Flex, TW.Flex.Row, TW.Flex.ItemsCenter, TW.Gap(2)).Build(),
                                        Children = {
                                            new Text("📊") { ClassName = TW.Text.Lg },
                                            new Text($"Total: {_todos.Count}") {
                                                Variant = Variant.Custom,
                                                ClassName = ClassBuilder.Create().Add(TW.Font.Semibold, TW.Text.Gray700).Dark(TW.Text.Gray300).Build()
                                            }
                                        }
                                    },
                                    new Box {
                                        ClassName = ClassBuilder.Create().Add(TW.Display.Flex, TW.Flex.Row, TW.Flex.ItemsCenter, TW.Gap(2)).Build(),
                                        Children = {
                                            new Text("✅") { ClassName = TW.Text.Lg },
                                            new Text($"Completed: {_todos.Count(t => t.IsCompleted)}") {
                                                Variant = Variant.Custom,
                                                ClassName = ClassBuilder.Create().Add(TW.Font.Semibold, TW.Text.Green600).Dark(TW.Text.Green400).Build()
                                            }
                                        }
                                    }
                                }
                            },
                            _todos.Any(t => t.IsCompleted) ? (IComponent)new Button {
                                Text = "🧹 Clear Completed",
                                Variant = Variant.Ghost,
                                ClassName = ClassBuilder.Create()
                                    .Add(TW.Text.Xs, TW.Font.Semibold, TW.Px(4), TW.Py(2))
                                    .Add(TW.Bg.Red50)
                                    .Hover(TW.Bg.Red100)
                                    .Dark(TW.WithOpacity(TW.Bg.Red600, 20), TW.Hover(TW.WithOpacity(TW.Bg.Red600, 40)))
                                    .Add(TW.Text.Red600)
                                    .Dark(TW.Text.Red400)
                                    .Add(TW.Rounded.Lg, TW.Transition.All)
                                    .Hover(TW.Transform.Scale105)
                                    .Build(),
                                OnClick = () => { HandleClearCompleted(); }
                            } : new NullComponent()
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
                        ClassName = ClassBuilder.Create().Add(TW.Display.Flex, TW.Flex.Col, "gap-[10px]").Build(),
                        Children = { 
                            new Text("Update the task title:") { Variant = Variant.Custom, ClassName = TW.Text.Gray400 },
                            new TextInput {
                                Value = _editingTitle,
                                OnInput = val => { SetState(() => { _editingTitle = val; }); }
                            }
                        }
                    },
                    Footer = new Box {
                        ClassName = ClassBuilder.Create().Add(TW.Display.Flex, TW.Flex.Row, "gap-[10px]").Build(),
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
                        ClassName = ClassBuilder.Create().Add(TW.Display.Flex, TW.Flex.Col, "gap-[20px]").Build(),
                        Children = {
                            new Heading("Settings", 3) { ClassName = TW.Mb(4) },
                            new Text("Theme preferences and other settings will go here.") { Variant = Variant.Ghost },
                            new Box { 
                                ClassName = ClassBuilder.Create()
                                    .Add(TW.P(3), TW.Rounded.Lg)
                                    .Add(TW.Bg.Green50, TW.Border.Base, TW.Border.Green200)
                                    .Dark(TW.WithOpacity(TW.Bg.Green800, 20), TW.Border.Green800)
                                    .Build(),
                                Children = {
                                    new Text("eQuantic.UI is now running with Bun + Tailwind 100%!") { 
                                        Variant = Variant.Custom, 
                                        ClassName = ClassBuilder.Create().Add(TW.Text.Sm, TW.Text.Green800, TW.Font.Medium).Dark(TW.Text.Green300).Build()
                                    }
                                }
                            },
                            new Container {
                                ClassName = ClassBuilder.Create()
                                    .Add(TW.Mt(10) /* mt-auto */, TW.Pt(8), TW.Border.T, TW.Border.Gray200)
                                    .Dark("border-zinc-800")
                                    .Build(),
                                Children = {
                                    new Button {
                                        Text = "Back to List",
                                        Variant = Variant.Secondary,
                                        ClassName = TW.WFull,
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

        var buttonClasses = ClassBuilder.Create()
            .Add(TW.Flex.Flex1, TW.Px(4), "py-2.5", TW.Text.Sm, TW.Font.Semibold, TW.Rounded.Lg, TW.Transition.All)
            .When(isActive, 
                TW.Shadow.Lg, TW.Bg.GradientToR, "from-blue-600 to-purple-600", "dark:from-blue-500", "dark:to-purple-500", TW.Text.White)
            .When(!isActive,
                TW.Hover(TW.Bg.Gray200), TW.Text.Gray600)
            .Dark(TW.Hover("bg-zinc-700/50"), TW.Text.Gray400)
            .Build();

        return new Button
        {
            Text = $"{label} ({count})",
            Variant = isActive ? Variant.Primary : Variant.Ghost,
            ClassName = buttonClasses,
            OnClick = () => { SetState(() => { _currentFilter = filter; }); }
        };
    }
}
