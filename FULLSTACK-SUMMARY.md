# Full Stack Functional Programming Todo App

A complete full-stack application demonstrating functional programming principles with **language-ext** (backend) and **fp-ts** (frontend).

## Overview

This project showcases how functional programming patterns can be applied consistently across both backend and frontend, providing type safety, composability, and maintainability throughout the entire stack.

### Architecture

```
┌─────────────────────────────────────────────┐
│          React + TypeScript + fp-ts          │
│                  (Frontend)                  │
│                                             │
│  • App monad (Reader + TaskEither)          │
│  • RemoteData pattern                       │
│  • Applicative validation                   │
│  • Effect composition                       │
└──────────────┬──────────────────────────────┘
               │ HTTP (JSON)
               │
┌──────────────┴──────────────────────────────┐
│    ASP.NET Core + C# + language-ext         │
│                 (Backend)                    │
│                                             │
│  • Db monad (ReaderT + IO)                  │
│  • Validation with error accumulation       │
│  • Effect extensions                        │
│  • Minimal APIs                             │
└──────────────┬──────────────────────────────┘
               │
        ┌──────┴───────┐
        │    SQLite    │
        └──────────────┘
```

## Project Structure

```
fp-concepts/
├── TodoApp/                          # Backend (ASP.NET Core)
│   ├── Domain/
│   │   └── Todo.cs                   # Todo entity
│   ├── Data/
│   │   └── AppDbContext.cs           # EF Core context
│   ├── Infrastructure/
│   │   ├── DbEnv.cs                  # Environment
│   │   ├── DbMonad.cs                # Db monad
│   │   ├── ApiResultHelpers.cs       # HTTP helpers
│   │   └── Extensions/
│   │       ├── LoggingExtensions.cs  # Logging effects
│   │       └── MetricsExtensions.cs  # Metrics effects
│   ├── Features/
│   │   └── Todos/
│   │       ├── TodoRepository.cs     # Repository
│   │       ├── TodoValidation.cs     # Validation
│   │       └── TodoDtos.cs           # DTOs
│   ├── Program.cs                    # API endpoints
│   └── todos.db                      # SQLite database
│
└── todo-app-frontend/                # Frontend (React + TypeScript)
    ├── src/
    │   ├── lib/
    │   │   ├── AppEnv.ts             # Environment
    │   │   ├── AppMonad.ts           # App monad
    │   │   ├── HttpClient.ts         # HTTP client
    │   │   └── effects/
    │   │       └── logging.ts        # Logging effect
    │   ├── features/
    │   │   └── todos/
    │   │       ├── types.ts          # Todo types
    │   │       ├── api.ts            # API calls
    │   │       ├── validation.ts     # Validation
    │   │       ├── hooks.ts          # React hooks
    │   │       └── components/
    │   │           ├── TodoList.tsx  # List component
    │   │           └── TodoForm.tsx  # Form component
    │   ├── App.tsx                   # Main app
    │   └── main.tsx                  # Entry point
    └── package.json
```

## Getting Started

### Prerequisites

- **.NET 8 SDK** for backend
- **Node.js 18+** and npm for frontend

### Backend Setup

```bash
cd TodoApp
dotnet restore
dotnet run
```

Backend will run on `http://localhost:5000`

### Frontend Setup

```bash
cd todo-app-frontend
npm install
npm run dev
```

Frontend will run on `http://localhost:3000`

### Using the App

1. Start the backend first
2. Start the frontend
3. Open `http://localhost:3000` in your browser
4. Create, update, toggle, and delete todos!

## Functional Programming Concepts

### Backend (C# + language-ext)

#### 1. Db Monad

```csharp
public record Db<A>(ReaderT<DbEnv, IO, A> RunDb) : K<Db, A>
```

Combines:
- **Reader** for dependency injection
- **IO** for side effects
- **Error handling** with Fin<A>

#### 2. Repository Pattern

```csharp
public static K<Db, List<Todo>> List() =>
    from ctx in Db.Ctx<AppDbContext>()
    from ct in Db.CancellationToken()
    from todos in Db.LiftIO(ctx.Todos.ToListAsync(ct))
    select todos;
```

#### 3. Effect Extensions

```csharp
TodoRepository.Get(id)
    .WithLogging($"Fetching todo {id}", todo => $"Found: {todo.Title}")
    .WithMetrics($"GetTodo_{id}")
    .RunAsync(env)
```

#### 4. Validation

```csharp
public static Validation<Error, Todo> Validate(Todo todo) =>
    (ValidateTitle(todo), ValidateDescription(todo))
        .Apply((_, _) => todo).As();
```

### Frontend (TypeScript + fp-ts)

#### 1. App Monad

```typescript
type App<A> = Reader<AppEnv, TaskEither<Error, A>>
```

Combines:
- **Reader** for dependency injection
- **TaskEither** for async operations with error handling

#### 2. API Calls

```typescript
export const listTodos = (): App.App<Todo[]> =>
  pipe(
    App.httpClient(),
    App.chain(client => App.fromTaskEither(client.get<Todo[]>('/todos'))),
    withLogging('Fetching all todos', todos => `Fetched ${todos.length} todos`)
  );
```

#### 3. RemoteData Pattern

```typescript
type RemoteData<E, A> =
  | { _tag: 'NotAsked' }
  | { _tag: 'Loading' }
  | { _tag: 'Failure'; error: E }
  | { _tag: 'Success'; data: A }
```

#### 4. Validation

```typescript
export const validateTodo = (input: CreateTodoRequest): ValidationResult<CreateTodoRequest> =>
  pipe(
    sequenceS(validationApplicative)({
      title: validateTitle(input.title),
      description: validateDescription(input.description),
    }),
    map(() => input)
  );
```

## Parallel Concepts

| Concept | Backend (language-ext) | Frontend (fp-ts) |
|---------|------------------------|------------------|
| **Monad** | `Db<A>` | `App<A>` |
| **Environment** | `DbEnv` | `AppEnv` |
| **Base Structure** | `ReaderT<DbEnv, IO, A>` | `Reader<AppEnv, TaskEither<Error, A>>` |
| **Effect Extension** | `WithLogging()`, `WithMetrics()` | `withLogging()` |
| **Repository/API** | `TodoRepository` | Todo API module |
| **Validation** | `Validation<Error, A>` | `Either<ValidationError[], A>` |
| **Error Handling** | `Fin<A>` | `Either<Error, A>` |
| **Dependency Access** | `Db.Ask()`, `Db.Ctx()` | `App.ask()`, `App.httpClient()` |
| **Composition** | LINQ query syntax | `pipe()` function |

## Key Benefits

### Type Safety
- **Backend**: Compiler enforces all effects are handled
- **Frontend**: TypeScript catches errors at compile time
- **Full Stack**: End-to-end type safety from database to UI

### Composability
- **Backend**: LINQ query syntax for natural composition
- **Frontend**: `pipe()` for functional composition
- **Both**: Effects stack like LEGO blocks

### Testability
- **Backend**: Easy to mock DbContext and dependencies
- **Frontend**: Easy to mock HttpClient and AppEnv
- **Both**: Pure functions are trivial to test

### Maintainability
- **Backend**: Clear separation of concerns
- **Frontend**: Predictable state management
- **Both**: Declarative code that's easy to reason about

### Error Handling
- **Backend**: Automatic short-circuiting with `Fin<A>`
- **Frontend**: Type-safe error handling with `Either<E, A>`
- **Both**: Errors are values, not exceptions

## API Endpoints

All endpoints follow RESTful conventions:

- `GET /todos` - List all todos
- `GET /todos/{id}` - Get todo by ID
- `POST /todos` - Create new todo
- `PUT /todos/{id}` - Update todo
- `PATCH /todos/{id}/toggle` - Toggle completion status
- `DELETE /todos/{id}` - Delete todo

## Technology Stack

### Backend
- **ASP.NET Core 8.0** - Web framework
- **language-ext 5.0** - Functional programming library
- **Entity Framework Core 8.0** - ORM
- **SQLite** - Database

### Frontend
- **React 18** - UI library
- **TypeScript 5.2** - Type system
- **fp-ts 2.16** - Functional programming library
- **Vite 5** - Build tool

## Testing the Full Stack

### 1. Start Backend

```bash
cd TodoApp
dotnet run
```

Output:
```
Now listening on: http://localhost:5000
```

### 2. Start Frontend

```bash
cd todo-app-frontend
npm run dev
```

Output:
```
Local: http://localhost:3000
```

### 3. Use the App

Open `http://localhost:3000` and:
- Create todos with title and description
- Toggle completion status
- Delete todos
- See validation errors
- Watch console logs for functional effect tracking

## Extending the Application

### Backend

Add new effects:

```csharp
// Infrastructure/Extensions/CacheExtensions.cs
public static class CacheExtensions
{
    public static K<Db, A> WithCache<A>(
        this K<Db, A> operation,
        string key,
        TimeSpan ttl) => /* implementation */;
}
```

### Frontend

Add new effects:

```typescript
// lib/effects/retry.ts
export const withRetry = <A>(
  maxAttempts: number
) => (operation: App.App<A>): App.App<A> => /* implementation */;
```

## Comparison: Imperative vs Functional

### Imperative (Traditional)

**Backend:**
```csharp
public async Task<Todo> GetTodo(int id)
{
    logger.LogInfo($"Getting todo {id}");
    var todo = await ctx.Todos.FindAsync(id);
    if (todo == null) throw new NotFoundException();
    logger.LogInfo($"Found todo {id}");
    return todo;
}
```

**Frontend:**
```typescript
const [loading, setLoading] = useState(false);
const [error, setError] = useState(null);
const [data, setData] = useState(null);

const fetchTodos = async () => {
  setLoading(true);
  try {
    const response = await fetch('/todos');
    const data = await response.json();
    setData(data);
  } catch (err) {
    setError(err);
  } finally {
    setLoading(false);
  }
};
```

### Functional (This App)

**Backend:**
```csharp
public static K<Db, Todo> Get(int id) =>
    from ctx in Db.Ctx<AppDbContext>()
    from ct in Db.CancellationToken()
    from todo in Db.LiftIO(ctx.Todos.FindAsync(id, ct).AsTask())
    from _ in guard(notnull(todo), Error.New(404, $"Todo {id} not found"))
    select todo;
```

**Frontend:**
```typescript
export const listTodos = (): App.App<Todo[]> =>
  pipe(
    App.httpClient(),
    App.chain(client => App.fromTaskEither(client.get<Todo[]>('/todos'))),
    withLogging('Fetching todos', todos => `Fetched ${todos.length} todos`)
  );

// In component
const { state, refetch } = useAppQuery(env, listTodos(), []);
```

### Benefits of Functional Approach

1. **Separation of Concerns** - Effects are separate from business logic
2. **Composability** - Easy to add logging, caching, retry, etc.
3. **Type Safety** - Compiler enforces correctness
4. **Testability** - Pure functions are easy to test
5. **Reusability** - Effects can be reused across operations

## Learn More

- [functional-guide.md](functional-guide.md) - Backend patterns with language-ext
- [functional-frontend-guide.md](functional-frontend-guide.md) - Frontend patterns with fp-ts
- [TodoApp/README.md](TodoApp/README.md) - Backend documentation
- [todo-app-frontend/README.md](todo-app-frontend/README.md) - Frontend documentation

## License

MIT
