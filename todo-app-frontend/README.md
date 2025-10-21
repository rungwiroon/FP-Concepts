# Functional Todo App - Frontend

A React TypeScript frontend built with **Effect-TS** following functional programming principles. This app connects to the ASP.NET Core backend built with **language-ext v5**.

## Features

- **Functional Programming with Effect-TS**
  - Effect system with Context.Tag for dependency injection
  - Type-safe HTTP client with typed errors
  - RemoteData pattern for loading states
  - Effect validation
  - Composable effects (logging, error handling, etc.)
  - Generator syntax for effect composition

- **Modern React with TypeScript**
  - React 18 with hooks
  - TypeScript strict mode
  - Vite for fast development
  - Clean component architecture

## Project Structure

```
src/
├── lib/
│   ├── AppEnv.ts              # Environment type definition
│   ├── AppMonad.ts            # App monad implementation
│   ├── HttpClient.ts          # HTTP client with fp-ts
│   └── effects/
│       └── logging.ts         # Logging effect
├── features/
│   └── todos/
│       ├── types.ts           # Todo types
│       ├── api.ts             # Todo API calls
│       ├── validation.ts      # Todo validation
│       ├── hooks.ts           # React hooks
│       └── components/
│           ├── TodoList.tsx   # Todo list component
│           └── TodoForm.tsx   # Todo form component
├── App.tsx                    # Main app component
├── main.tsx                   # Entry point
├── App.css                    # Styles
└── index.css                  # Global styles
```

## Getting Started

### Prerequisites

- Node.js 18+ and npm
- Backend API running on `http://localhost:5000`

### Installation

```bash
cd todo-app-frontend
npm install
```

### Development

```bash
npm run dev
```

The app will be available at `http://localhost:3000`

### Build for Production

```bash
npm run build
npm run preview
```

## Functional Programming Concepts

### 1. Effect System with Context

The `App<A>` type is an Effect that requires services:

```typescript
// App<A> = Effect that requires HttpClient and Logger services
type App<A> = Effect.Effect<A, Error, HttpClientService | LoggerService>;
```

This provides:
- **Dependency injection** via `Context.Tag` services
- **Async operations** built-in
- **Typed errors** with `Error`
- **Service requirements** tracked in the type system

### 2. Context.Tag for Services

Services are defined using `Context.Tag`:

```typescript
export class HttpClientService extends Context.Tag('HttpClientService')<
  HttpClientService,
  HttpClient
>() {}

export class LoggerService extends Context.Tag('LoggerService')<
  LoggerService,
  Logger
>() {}
```

### 3. Effect.gen for Composition

Effects are composed using generator functions:

```typescript
export const listTodos = (): App<Todo[]> =>
  Effect.gen(function* (_) {
    const client = yield* _(App.httpClient());
    return yield* _(client.get<Todo[]>('/todos'));
  }).pipe(
    withLogging(
      'Fetching all todos',
      todos => `Fetched ${todos.length} todos`
    )
  );
```

### 4. RemoteData Pattern

Loading states are modeled as a discriminated union:

```typescript
type RemoteData<E, A> =
  | { _tag: 'NotAsked' }
  | { _tag: 'Loading' }
  | { _tag: 'Failure'; error: E }
  | { _tag: 'Success'; data: A }
```

### 5. Validation with Effect

Validation uses Effect's validation capabilities for error accumulation.

### 6. Type-Safe HTTP Client

All API calls return `Effect<A, Error>`:

```typescript
export interface HttpClient {
  get: <A>(url: string) => Effect.Effect<A, Error>;
  post: <A, B>(url: string, body: A) => Effect.Effect<B, Error>;
  put: <A, B>(url: string, body: A) => Effect.Effect<B, Error>;
  patch: <A>(url: string) => Effect.Effect<A, Error>;
  delete: (url: string) => Effect.Effect<void, Error>;
}
```

## API Integration

The app expects the backend API to be running at `http://localhost:5000` with the following endpoints:

- `GET /todos` - List all todos
- `GET /todos/:id` - Get todo by ID
- `POST /todos` - Create new todo
- `PUT /todos/:id` - Update todo
- `PATCH /todos/:id/toggle` - Toggle completion
- `DELETE /todos/:id` - Delete todo

## Key Dependencies

- **react** - UI library
- **effect** - Modern effect system for TypeScript (v3.10.0)
- **vite** - Build tool
- **typescript** - Type safety

## Comparison to Backend

Both the backend (C#/language-ext v5) and frontend (TypeScript/Effect-TS) use similar functional patterns with modern effect systems:

| Concept | Backend (C#/language-ext v5) | Frontend (TS/Effect-TS) |
|---------|------------------------------|-------------------------|
| **Effect Type** | `K<M, A>` (Higher-Kinded Type) | `Effect<A, Error, R>` |
| **Monad** | `Eff<RT>` (built-in) | `Effect` (built-in) |
| **Environment/Context** | `AppRuntime` (trait implementations) | `Context.Tag` services |
| **Capabilities/Services** | Traits: `DatabaseIO`, `LoggerIO`, `TimeIO` | Services: `HttpClientService`, `LoggerService` |
| **Dependency Injection** | `Has<M, RT, T>.ask` pattern | `Context.Tag` with service access |
| **Service Layer** | `TodoService<M, RT>` (generic) | `listTodos()`, `createTodo()` (concrete) |
| **Effect Composition** | LINQ query syntax | `Effect.gen` with generator functions |
| **Logging** | `Logger<M, RT>.logInfo()` | `LoggerService` + `withLogging()` |
| **Validation** | `Validation<Error, A>` | Effect validation |
| **Error Handling** | `Fin<A>` with `Fail/Succ` | `Effect<A, Error, R>` with typed errors |
| **Async** | `RunAsync()` returns `Task<Fin<A>>` | `Effect.runPromise()` returns `Promise<A>` |

### Architectural Similarities

**Backend (language-ext v5 - Trait-Based):**
```csharp
// Generic over M and RT with trait constraints
public static K<M, List<Todo>> List()
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, DatabaseIO>, Has<M, LoggerIO>
    =>
    from _ in Logger<M, RT>.logInfo("Listing all todos")
    from todos in Database<M, RT>.liftIO((ctx, ct) =>
        ctx.Todos.OrderByDescending(t => t.CreatedAt).ToListAsync(ct))
    from __ in Logger<M, RT>.logInfo("Found {TodosCount} todos", todos.Count)
    select todos;
```

**Frontend (Effect-TS - Context-Based):**
```typescript
// Effect with Context.Tag service dependencies
export const listTodos = (): App<Todo[]> =>
  Effect.gen(function* (_) {
    const client = yield* _(App.httpClient());
    return yield* _(client.get<Todo[]>('/todos'));
  }).pipe(
    withLogging(
      'Fetching all todos',
      todos => `Fetched ${todos.length} todos`
    )
  );

// App<A> = Effect requiring HttpClient and Logger services
type App<A> = Effect.Effect<A, Error, HttpClientService | LoggerService>;
```

### Key Shared Patterns

Both architectures demonstrate the same functional principles:

1. **Effect Systems** - Both use modern effect systems (Eff<RT> and Effect) instead of custom monads
2. **Context/Dependency Injection** - Backend uses `Has<M, RT, T>.ask`, Frontend uses `Context.Tag`
3. **Type-Safe Service Access** - Compiler ensures required services are provided
4. **Generator Syntax** - Backend uses LINQ query syntax, Frontend uses `Effect.gen`
5. **Composable Effects** - Both support logging, error handling, and other cross-cutting concerns
6. **Typed Errors** - Backend uses `Fin<A>`, Frontend uses `Effect<A, Error, R>`
7. **Pure Business Logic** - Side effects are pushed to service implementations
8. **Service-Based Architecture** - Both organize capabilities as services/traits

## License

MIT
