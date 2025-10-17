using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Traits;
using Microsoft.EntityFrameworkCore;
using TodoApp.Data;
using TodoApp.Domain;
using TodoApp.Infrastructure;
using static LanguageExt.Prelude;

namespace TodoApp.Features.Todos;

public static class TodoRepository
{
    // List all todos
    public static K<Db, List<Todo>> List() =>
        from ctx in Db.Ctx<AppDbContext>()
        from ct in Db.CancellationToken()
        from todos in Db.LiftIO(ctx.Todos.OrderByDescending(t => t.CreatedAt).ToListAsync(ct))
        select todos;

    // Get todo by id
    public static K<Db, Todo> Get(int id) =>
        from ctx in Db.Ctx<AppDbContext>()
        from ct in Db.CancellationToken()
        from todo in Db.LiftIO(ctx.Todos.FindAsync(new object[] { id }, ct).AsTask())
        from _ in guard(notnull(todo), Error.New(404, $"Todo with id {id} not found"))
        select todo;

    // Create new todo
    public static K<Db, Todo> Create(string title, string? description) =>
        from todo in CreateTodoEntity(title, description)
        from validated in Db.Lift(TodoValidation.Validate(todo))
        from ctx in Db.Ctx<AppDbContext>()
        from ct in Db.CancellationToken()
        from _ in Db.LiftIO(ctx.AddAsync(validated, ct).AsTask())
        from __ in Db.Save()
        select validated;

    // Update todo
    public static K<Db, Todo> Update(int id, string title, string? description) =>
        from existing in Get(id)
        from updated in UpdateTodoEntity(existing, title, description)
        from validated in Db.Lift(TodoValidation.Validate(updated))
        from ctx in Db.Ctx<AppDbContext>()
        from _ in Db.LiftIO(() =>
        {
            ctx.Entry(validated).State = EntityState.Modified;
            return Task.FromResult(unit);
        })
        from __ in Db.Save()
        select validated;

    // Toggle completion status
    public static K<Db, Todo> ToggleComplete(int id) =>
        from existing in Get(id)
        from updated in ToggleTodoComplete(existing)
        from ctx in Db.Ctx<AppDbContext>()
        from _ in Db.LiftIO(() =>
        {
            ctx.Entry(updated).State = EntityState.Modified;
            return Task.FromResult(unit);
        })
        from __ in Db.Save()
        select updated;

    // Delete todo
    public static K<Db, Unit> Delete(int id) =>
        from existing in Get(id)
        from ctx in Db.Ctx<AppDbContext>()
        from _ in Db.LiftIO(() =>
        {
            ctx.Remove(existing);
            return Task.FromResult(unit);
        })
        from __ in Db.Save()
        select unit;

    // Helper methods
    private static K<Db, Todo> CreateTodoEntity(string title, string? description) =>
        Db.Pure(new Todo
        {
            Title = title,
            Description = description,
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow
        });

    private static K<Db, Todo> UpdateTodoEntity(Todo existing, string title, string? description) =>
        Db.Pure(existing with
        {
            Title = title,
            Description = description
        });

    private static K<Db, Todo> ToggleTodoComplete(Todo existing) =>
        Db.Pure(existing with
        {
            IsCompleted = !existing.IsCompleted,
            CompletedAt = !existing.IsCompleted ? DateTime.UtcNow : null
        });
}
