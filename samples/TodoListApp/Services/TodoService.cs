using TodoListApp.Models;

namespace TodoListApp.Services;

public interface ITodoService
{
    Task<List<Todo>> GetTodosAsync();
    Task<Todo> AddTodoAsync(string title);
    Task ToggleTodoAsync(Guid id);
    Task DeleteTodoAsync(Guid id);
    Task UpdateTodoAsync(Guid id, string title);
}

public class TodoService : ITodoService
{
    private readonly List<Todo> _todos = new();

    public TodoService()
    {
        // Seeding initial data
        _todos.Add(new Todo { Title = "Learn eQuantic.UI", IsCompleted = true });
        _todos.Add(new Todo { Title = "Build TodoList App", IsCompleted = false });
    }

    public Task<List<Todo>> GetTodosAsync()
    {
        return Task.FromResult(_todos.ToList());
    }

    public Task<Todo> AddTodoAsync(string title)
    {
        var todo = new Todo { Title = title };
        _todos.Add(todo);
        return Task.FromResult(todo);
    }

    public Task ToggleTodoAsync(Guid id)
    {
        var todo = _todos.FirstOrDefault(t => t.Id == id);
        if (todo != null)
        {
            todo.IsCompleted = !todo.IsCompleted;
        }
        return Task.CompletedTask;
    }

    public Task DeleteTodoAsync(Guid id)
    {
        var todo = _todos.FirstOrDefault(t => t.Id == id);
        if (todo != null)
        {
            _todos.Remove(todo);
        }
        return Task.CompletedTask;
    }

    public Task UpdateTodoAsync(Guid id, string title)
    {
        var todo = _todos.FirstOrDefault(t => t.Id == id);
        if (todo != null)
        {
            todo.Title = title;
        }
        return Task.CompletedTask;
    }
}
