using LanguageExt;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TodoApp.Data;
using TodoApp.Domain;
using TodoApp.Features.Todos;
using TodoApp.Infrastructure;
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

// Map endpoints using CORRECTED Has<M, RT, T>.ask pattern

// GET /todos - List all todos
app.MapGet("/todos", (
    IServiceProvider services,
    CancellationToken ct) =>
{
    var runtime = new AppRuntime(services);
    var result = TodoService<Eff<AppRuntime>, AppRuntime>.List()
        .Run(runtime);

    return ToResult(result, todos => Results.Ok(todos.Select(MapToResponse)));
});

// GET /todos/{id} - Get todo by id
app.MapGet("/todos/{id}", async (
    int id,
    IServiceProvider services,
    CancellationToken ct) =>
{
    var runtime = new AppRuntime(services);
    var result = await TodoService<Eff<AppRuntime>, AppRuntime>.Get(id)
        .RunAsync(runtime, EnvIO.New(token: ct));

    return ToResult(result, todo => Results.Ok(MapToResponse(todo)));
});

// POST /todos - Create a new todo
app.MapPost("/todos", async (
    CreateTodoRequest request,
    IServiceProvider services,
    CancellationToken ct) =>
{
    var runtime = new AppRuntime(services);
    var result = await TodoService<Eff<AppRuntime>, AppRuntime>.Create(request.Title, request.Description)
        .RunAsync(runtime, EnvIO.New(token: ct));

    return ToResult(result, todo => Results.Created($"/todos/{todo.Id}", MapToResponse(todo)));
});

// PUT /todos/{id} - Update a todo
app.MapPut("/todos/{id}", async (
    int id,
    UpdateTodoRequest request,
    IServiceProvider services,
    CancellationToken ct) =>
{
    var runtime = new AppRuntime(services);
    var result = await TodoService<Eff<AppRuntime>, AppRuntime>.Update(id, request.Title, request.Description)
        .RunAsync(runtime, EnvIO.New(token: ct));

    return ToResult(result, todo => Results.Ok(MapToResponse(todo)));
});

// PUT /todos/{id}/toggle - Toggle completion status
app.MapPut("/todos/{id}/toggle", async (
    int id,
    IServiceProvider services,
    CancellationToken ct) =>
{
    var runtime = new AppRuntime(services);
    var result = await TodoService<Eff<AppRuntime>, AppRuntime>.ToggleComplete(id)
        .RunAsync(runtime, EnvIO.New(token: ct));

    return ToResult(result, todo => Results.Ok(MapToResponse(todo)));
});

// DELETE /todos/{id} - Delete a todo
app.MapDelete("/todos/{id}", async (
    int id,
    IServiceProvider services,
    CancellationToken ct) =>
{
    var runtime = new AppRuntime(services);
    var result = await TodoService<Eff<AppRuntime>, AppRuntime>.Delete(id)
        .RunAsync(runtime, EnvIO.New(token: ct));

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
