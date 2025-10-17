# Functional Todo API

A simple Todo web application built with ASP.NET Core and SQLite using functional programming concepts with language-ext.

## Features

- Create, read, update, and delete todos
- Toggle completion status
- Validation with error accumulation
- Functional composition using the Db monad
- Effect extensions (logging, metrics)
- Type-safe error handling
- SQLite database with Entity Framework Core

## Architecture

This application follows functional programming patterns:

- **Db Monad**: Composable effects for database operations, logging, and cancellation
- **Pure Functions**: All business logic is pure and composable
- **Validation**: Error accumulation using Validation applicative
- **Effect Extensions**: WithLogging, WithMetrics for cross-cutting concerns
- **Type Safety**: Compiler-enforced correctness

## Project Structure

```
TodoApp/
├── Domain/
│   └── Todo.cs                 # Domain entity
├── Data/
│   └── AppDbContext.cs         # EF Core context
├── Infrastructure/
│   ├── DbEnv.cs                # Environment record
│   ├── DbMonad.cs              # Db monad implementation
│   ├── ApiResultHelpers.cs     # HTTP result helpers
│   └── Extensions/
│       ├── LoggingExtensions.cs
│       └── MetricsExtensions.cs
├── Features/
│   └── Todos/
│       ├── TodoRepository.cs   # Repository with functional patterns
│       ├── TodoValidation.cs   # Validation logic
│       └── TodoDtos.cs         # Request/Response DTOs
└── Program.cs                  # API endpoints
```

## Getting Started

### Prerequisites

- .NET 8.0 SDK
- Any IDE (Visual Studio, VS Code, Rider)

### Installation

1. Restore packages:
```bash
cd TodoApp
dotnet restore
```

2. Run the application:
```bash
dotnet run
```

The API will be available at `http://localhost:5000` (or the port shown in console).

### Swagger UI

When running in development mode, open your browser to:
```
http://localhost:5000/swagger
```

## API Endpoints

### List all todos
```http
GET /todos
```

### Get a todo by ID
```http
GET /todos/{id}
```

### Create a new todo
```http
POST /todos
Content-Type: application/json

{
  "title": "Buy groceries",
  "description": "Milk, eggs, bread"
}
```

### Update a todo
```http
PUT /todos/{id}
Content-Type: application/json

{
  "title": "Buy groceries",
  "description": "Milk, eggs, bread, butter"
}
```

### Toggle completion status
```http
PATCH /todos/{id}/toggle
```

### Delete a todo
```http
DELETE /todos/{id}
```

## Example Usage

### Create a todo
```bash
curl -X POST http://localhost:5000/todos \
  -H "Content-Type: application/json" \
  -d '{"title":"Learn functional programming","description":"Study language-ext"}'
```

### List all todos
```bash
curl http://localhost:5000/todos
```

### Toggle completion
```bash
curl -X PATCH http://localhost:5000/todos/1/toggle
```

## Functional Programming Concepts Used

### 1. Db Monad for Effect Composition
All database operations return `K<Db, A>` which represents a computation that:
- Has access to DbContext, Logger, and CancellationToken
- Can fail with typed errors
- Can be composed with other operations

### 2. LINQ Query Syntax for Composition
```csharp
public static K<Db, Todo> Get(int id) =>
    from ctx in Db.Ctx<AppDbContext>()
    from ct in Db.CancellationToken()
    from todo in Db.LiftIO(ctx.Todos.FindAsync(new object[] { id }, ct).AsTask())
    from _ in guard(notnull(todo), Error.New(404, $"Todo with id {id} not found"))
    select todo;
```

### 3. Validation with Error Accumulation
```csharp
public static Validation<Error, Todo> Validate(Todo todo) =>
    (ValidateTitle(todo), ValidateDescription(todo))
        .Apply((_, _) => todo);
```

### 4. Effect Extensions
Operations can be decorated with effects:
```csharp
TodoRepository.Get(id)
    .WithLogging($"Fetching todo {id}", todo => $"Retrieved: {todo.Title}")
    .WithMetrics($"GetTodo_{id}")
```

### 5. Type-Safe Error Handling
```csharp
var result = await operation.RunAsync(env);
return result.Match(
    Succ: value => Results.Ok(value),
    Fail: error => HandleError(error)
);
```

## Database

The application uses SQLite with a file named `todos.db` created in the application directory. The database is automatically created on first run.

## License

MIT
