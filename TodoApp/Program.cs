using Microsoft.EntityFrameworkCore;
using TodoApp.Data;
using TodoApp.Domain;
using TodoApp.Features.Todos;
using TodoApp.Infrastructure;
using TodoApp.Infrastructure.Extensions;
using static TodoApp.Infrastructure.ApiResultHelpers;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=todos.db"));

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:3001")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// Enable CORS
app.UseCors("AllowFrontend");

// Map endpoints

// GET /todos - List all todos
app.MapGet("/todos", async (
    AppDbContext ctx,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    var env = DbEnvExtensions.Create(ctx, ct, logger);
    var result = await TodoRepository.List()
        .WithLogging(
            "Fetching all todos",
            todos => $"Retrieved {todos.Count} todos")
        .WithMetrics("ListTodos")
        .RunAsync(env);

    return ToResult(result, todos => Results.Ok(todos.Select(MapToResponse)));
});

// GET /todos/{id} - Get todo by id
app.MapGet("/todos/{id}", async (
    int id,
    AppDbContext ctx,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    var env = DbEnvExtensions.Create(ctx, ct, logger);
    var result = await TodoRepository.Get(id)
        .WithLogging(
            $"Fetching todo {id}",
            todo => $"Retrieved todo: {todo.Title}")
        .WithMetrics($"GetTodo_{id}")
        .RunAsync(env);

    return ToResult(result, todo => Results.Ok(MapToResponse(todo)));
});

// POST /todos - Create new todo
app.MapPost("/todos", async (
    CreateTodoRequest request,
    AppDbContext ctx,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    var env = DbEnvExtensions.Create(ctx, ct, logger);
    var result = await TodoRepository.Create(request.Title, request.Description)
        .WithLogging(
            $"Creating todo: {request.Title}",
            todo => $"Created todo with id {todo.Id}")
        .WithMetrics("CreateTodo")
        .RunAsync(env);

    return ToResult(result, todo => Results.Created($"/todos/{todo.Id}", MapToResponse(todo)));
});

// PUT /todos/{id} - Update todo
app.MapPut("/todos/{id}", async (
    int id,
    UpdateTodoRequest request,
    AppDbContext ctx,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    var env = DbEnvExtensions.Create(ctx, ct, logger);
    var result = await TodoRepository.Update(id, request.Title, request.Description)
        .WithLogging(
            $"Updating todo {id}",
            todo => $"Updated todo: {todo.Title}")
        .WithMetrics($"UpdateTodo_{id}")
        .RunAsync(env);

    return ToResult(result, todo => Results.Ok(MapToResponse(todo)));
});

// PATCH /todos/{id}/toggle - Toggle completion status
app.MapPatch("/todos/{id}/toggle", async (
    int id,
    AppDbContext ctx,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    var env = DbEnvExtensions.Create(ctx, ct, logger);
    var result = await TodoRepository.ToggleComplete(id)
        .WithLogging(
            $"Toggling completion for todo {id}",
            todo => $"Todo {id} is now {(todo.IsCompleted ? "completed" : "incomplete")}")
        .WithMetrics($"ToggleTodo_{id}")
        .RunAsync(env);

    return ToResult(result, todo => Results.Ok(MapToResponse(todo)));
});

// DELETE /todos/{id} - Delete todo
app.MapDelete("/todos/{id}", async (
    int id,
    AppDbContext ctx,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    var env = DbEnvExtensions.Create(ctx, ct, logger);
    var result = await TodoRepository.Delete(id)
        .WithLogging(
            $"Deleting todo {id}",
            _ => $"Deleted todo {id}")
        .WithMetrics($"DeleteTodo_{id}")
        .RunAsync(env);

    return ToResult(result, _ => Results.NoContent());
});

app.Run();

// Helper method to map domain entity to response DTO
static TodoResponse MapToResponse(Todo todo) =>
    new TodoResponse(
        todo.Id,
        todo.Title,
        todo.Description,
        todo.IsCompleted,
        todo.CreatedAt,
        todo.CompletedAt
    );
