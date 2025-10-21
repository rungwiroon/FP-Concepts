using LanguageExt;
using Microsoft.EntityFrameworkCore;
using TodoApp.Data;
using TodoApp.Domain;
using TodoApp.Infrastructure.Traits;
using static LanguageExt.Prelude;

namespace TodoApp.Infrastructure.Live;

/// <summary>
/// Production implementation of DatabaseIO using EF Core.
/// Implements high-level domain operations using AppDbContext.
/// </summary>
public class LiveDatabaseIO(AppDbContext context) : DatabaseIO
{
    public async Task<List<Todo>> GetAllTodosAsync(CancellationToken cancellationToken)
    {
        return await context.Todos
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<Option<Todo>> GetTodoByIdAsync(int id, CancellationToken cancellationToken)
    {
        var todo = await context.Todos
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        return Optional(todo);
    }

    public async Task<Todo> AddTodoAsync(Todo todo, CancellationToken cancellationToken)
    {
        context.Todos.Add(todo);
        await context.SaveChangesAsync(cancellationToken);
        return todo;
    }

    public async Task<Todo> UpdateTodoAsync(Todo todo, CancellationToken cancellationToken)
    {
        context.Entry(todo).State = EntityState.Modified;
        await context.SaveChangesAsync(cancellationToken);
        return todo;
    }

    public async Task<Unit> DeleteTodoAsync(Todo todo, CancellationToken cancellationToken)
    {
        context.Todos.Remove(todo);
        await context.SaveChangesAsync(cancellationToken);
        return Unit.Default;
    }
}
