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
    AppDbContext ctx,
    IServiceProvider services,
    CancellationToken ct) =>
{
    var runtime = new AppRuntime(services);
    var result = TodoServiceSimple<Eff<AppRuntime>, AppRuntime>.List()
        .Run(runtime);

    return ToResult(result, todos => Results.Ok(todos.Select(MapToResponse)));
});

// TODO: Other endpoints commented out - only List() is implemented in TodoServiceSimple
/*
// GET /todos/{id} - Get todo by id
app.MapGet("/todos/{id}", async (
    int id,
    AppDbContext ctx,
    IServiceProvider services,
    CancellationToken ct) =>
{
    var runtime = new AppRuntime(services);
    var result = await TodoServiceSimple<Eff<AppRuntime>, AppRuntime>.Get(id)
        .Run(runtime);

    return ToResult(result, todo => Results.Ok(MapToResponse(todo)));
});
*/

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
