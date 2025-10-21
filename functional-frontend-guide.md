# Functional Frontend with Effect

A comprehensive guide to building TypeScript frontend applications using functional programming patterns with Effect.

## Table of Contents

- [Overview](#overview)
- [Setup](#setup)
- [Core Architecture](#core-architecture)
- [The App Effect](#the-app-effect)
- [Effect Composition](#effect-composition)
- [React Integration](#react-integration)
- [Form Validation](#form-validation)
- [API Client Patterns](#api-client-patterns)
- [State Management](#state-management)
- [Common Patterns](#common-patterns)

---

## Overview

Just like the backend with language-ext, we can compose multiple effects in a type-safe way using Effect:

- **API calls** via fetch/axios
- **Logging** to console or services
- **Error handling** with automatic recovery
- **Validation** with error accumulation
- **State management** with predictable updates
- **Caching** for performance
- **Loading states** automatically
- **Dependency injection** with Context
- **Resource management** with automatic cleanup

### Key Benefits

✅ **Type-safe** - compiler catches errors<br/>
✅ **Composable** - stack effects naturally<br/>
✅ **Testable** - easy to mock dependencies<br/>
✅ **Declarative** - clear intent<br/>
✅ **Error handling** - automatic short-circuiting<br/>
✅ **Generator syntax** - readable imperative-style code<br/>
✅ **Unified API** - one Effect type for all operations<br/>
✅ **Rich ecosystem** - scheduling, resources, metrics, tracing<br/>

---

## Setup

### Install Dependencies

```bash
npm install effect
# or
yarn add effect
```

### Project Structure

```
src/
├── lib/
│   ├── AppMonad.ts           # App effect type & utilities
│   ├── AppEnv.ts             # Context tags for DI
│   ├── HttpClient.ts         # HTTP client implementation
│   └── effects/
│       ├── logging.ts        # Logging effect combinator
│       ├── cache.ts          # Caching effect (optional)
│       └── retry.ts          # Retry effect (optional)
├── features/
│   └── todos/
│       ├── api.ts            # Todo API calls
│       ├── validation.ts     # Todo validation
│       ├── hooks.ts          # React hooks
│       ├── types.ts          # Domain types
│       └── components/
│           ├── TodoList.tsx
│           └── TodoForm.tsx
└── App.tsx
```

---

## Core Architecture

### The AppEnv Type and Context Tags

Effect uses Context.Tag for type-safe dependency injection:

```typescript
import { Context, Effect } from 'effect';

export interface Logger {
  info: (message: string) => void;
  warn: (message: string) => void;
  error: (message: string, err?: unknown) => void;
}

export interface HttpClient {
  get: <A>(url: string) => Effect.Effect<A, Error>;
  post: <A, B>(url: string, body: A) => Effect.Effect<B, Error>;
  put: <A, B>(url: string, body: A) => Effect.Effect<B, Error>;
  patch: <A>(url: string) => Effect.Effect<A, Error>;
  delete: (url: string) => Effect.Effect<void, Error>;
}

// Define Context Tags for dependency injection
export class LoggerService extends Context.Tag('LoggerService')<
  LoggerService,
  Logger
>() {}

export class HttpClientService extends Context.Tag('HttpClientService')<
  HttpClientService,
  HttpClient
>() {}

export class BaseUrl extends Context.Tag('BaseUrl')<BaseUrl, string>() {}

// Combined environment for convenience
export interface AppEnv {
  httpClient: HttpClient;
  logger: Logger;
  baseUrl: string;
}
```

**Key Points:**
- `Context.Tag` creates type-safe service identifiers
- Services are provided at runtime using `Effect.provideService`
- No manual environment passing needed

### The App Effect

Instead of manually composing `Reader` + `TaskEither`, Effect provides a unified type:

```typescript
import { Effect } from 'effect';
import { HttpClientService, LoggerService, type AppEnv } from './AppEnv';

// App<A> = Effect that requires HttpClient and Logger services,
//          may fail with Error, and succeeds with A
export type App<A> = Effect.Effect<A, Error, HttpClientService | LoggerService>;

// Basic constructors
export const succeed = <A>(a: A): App<A> =>
  Effect.succeed(a);

export const fail = <A = never>(error: Error): App<A> =>
  Effect.fail(error);

// Lift a promise into App
export const fromPromise = <A>(f: () => Promise<A>): App<A> =>
  Effect.tryPromise({
    try: f,
    catch: (reason) => reason instanceof Error ? reason : new Error(String(reason))
  });

// Access the logger service
export const logger = (): App<AppEnv['logger']> =>
  LoggerService;

// Access the http client service
export const httpClient = (): App<AppEnv['httpClient']> =>
  HttpClientService;

// Logging operations
export const logInfo = (message: string): App<void> =>
  Effect.gen(function* (_) {
    const log = yield* _(LoggerService);
    log.info(message);
  });

export const logError = (message: string, err?: unknown): App<void> =>
  Effect.gen(function* (_) {
    const log = yield* _(LoggerService);
    log.error(message, err);
  });

export const logWarn = (message: string): App<void> =>
  Effect.gen(function* (_) {
    const log = yield* _(LoggerService);
    log.warn(message);
  });

// Re-export common Effect utilities for convenience
export const map = Effect.map;
export const flatMap = Effect.flatMap;
export const gen = Effect.gen;
export const all = Effect.all;
export const catchAll = Effect.catchAll;
export const tap = Effect.tap;
```

**Key Benefits:**
- `Effect.Effect<A, E, R>` combines async operations, error handling, and dependencies
- `R` (requirements) tracks which services are needed
- No manual Reader/TaskEither composition
- Generator syntax (`Effect.gen`) provides imperative-style code with full type safety

---

## Effect Composition

### Logging Effect

```typescript
// lib/effects/logging.ts
import { Effect } from 'effect';
import * as App from '../AppMonad';

export const withLogging = <A>(
  startMessage: string,
  successMessage: (a: A) => string
) =>
  (operation: App.App<A>): App.App<A> =>
    Effect.gen(function* (_) {
      yield* _(App.logInfo(startMessage));
      const result = yield* _(operation);
      yield* _(App.logInfo(successMessage(result)));
      return result;
    });

// Usage
const getTodo = (id: number) =>
  Effect.gen(function* (_) {
    const client = yield* _(App.httpClient());
    return yield* _(client.get<Todo>(`/todos/${id}`));
  }).pipe(
    withLogging(
      `Fetching todo ${id}`,
      todo => `Successfully fetched: ${todo.title}`
    )
  );
```

### Retry Effect

```typescript
// lib/effects/retry.ts
import { Effect, Schedule } from 'effect';
import * as App from '../AppMonad';

export const withRetry = <A>(
  operation: App.App<A>,
  maxAttempts: number = 3,
  delayMs: number = 1000
): App.App<A> =>
  operation.pipe(
    Effect.retry({
      times: maxAttempts - 1,
      schedule: Schedule.exponential(delayMs)
    })
  );

// Usage
const getTodo = (id: number) =>
  Effect.gen(function* (_) {
    const client = yield* _(App.httpClient());
    return yield* _(client.get<Todo>(`/todos/${id}`));
  }).pipe(
    withRetry(3, 1000) // Retry up to 3 times with exponential backoff
  );
```

### Timeout Effect

```typescript
// lib/effects/timeout.ts
import { Effect, Duration } from 'effect';

export const withTimeout = <A>(
  operation: App.App<A>,
  timeoutMs: number
): App.App<A> =>
  operation.pipe(
    Effect.timeout(Duration.millis(timeoutMs))
  );
```

---

## API Client Patterns

### HTTP Client Implementation

```typescript
// lib/HttpClient.ts
import { Effect } from 'effect';
import type { HttpClient } from './AppEnv';

export const createHttpClient = (baseURL: string): HttpClient => ({
  get: <A>(url: string) =>
    Effect.tryPromise({
      try: async () => {
        const response = await fetch(`${baseURL}${url}`);
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        return response.json() as Promise<A>;
      },
      catch: (reason) => reason instanceof Error ? reason : new Error(String(reason))
    }),

  post: <A, B>(url: string, body: A) =>
    Effect.tryPromise({
      try: async () => {
        const response = await fetch(`${baseURL}${url}`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(body),
        });
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        return response.json() as Promise<B>;
      },
      catch: (reason) => reason instanceof Error ? reason : new Error(String(reason))
    }),

  put: <A, B>(url: string, body: A) =>
    Effect.tryPromise({
      try: async () => {
        const response = await fetch(`${baseURL}${url}`, {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(body),
        });
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        return response.json() as Promise<B>;
      },
      catch: (reason) => reason instanceof Error ? reason : new Error(String(reason))
    }),

  patch: <A>(url: string) =>
    Effect.tryPromise({
      try: async () => {
        const response = await fetch(`${baseURL}${url}`, {
          method: 'PATCH',
        });
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        return response.json() as Promise<A>;
      },
      catch: (reason) => reason instanceof Error ? reason : new Error(String(reason))
    }),

  delete: (url: string) =>
    Effect.tryPromise({
      try: async () => {
        const response = await fetch(`${baseURL}${url}`, {
          method: 'DELETE',
        });
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
      },
      catch: (reason) => reason instanceof Error ? reason : new Error(String(reason))
    }),
});
```

### Todo API Example

```typescript
// features/todos/api.ts
import { Effect } from 'effect';
import * as App from '../../lib/AppMonad';
import { withLogging } from '../../lib/effects/logging';
import type { Todo, CreateTodoRequest, UpdateTodoRequest } from './types';

// Get all todos
export const listTodos = (): App.App<Todo[]> =>
  Effect.gen(function* (_) {
    const client = yield* _(App.httpClient());
    return yield* _(client.get<Todo[]>('/todos'));
  }).pipe(
    withLogging(
      'Fetching all todos',
      todos => `Fetched ${todos.length} todos`
    )
  );

// Get single todo
export const getTodo = (id: number): App.App<Todo> =>
  Effect.gen(function* (_) {
    const client = yield* _(App.httpClient());
    return yield* _(client.get<Todo>(`/todos/${id}`));
  }).pipe(
    withLogging(
      `Fetching todo ${id}`,
      todo => `Fetched todo: ${todo.title}`
    )
  );

// Create todo
export const createTodo = (
  request: CreateTodoRequest
): App.App<Todo> =>
  Effect.gen(function* (_) {
    const client = yield* _(App.httpClient());
    return yield* _(client.post<CreateTodoRequest, Todo>(
      '/todos',
      request
    ));
  }).pipe(
    withLogging(
      `Creating todo: ${request.title}`,
      todo => `Created todo with ID: ${todo.id}`
    )
  );

// Update todo
export const updateTodo = (
  id: number,
  request: UpdateTodoRequest
): App.App<Todo> =>
  Effect.gen(function* (_) {
    const client = yield* _(App.httpClient());
    return yield* _(client.put<UpdateTodoRequest, Todo>(
      `/todos/${id}`,
      request
    ));
  }).pipe(
    withLogging(
      `Updating todo ${id}`,
      todo => `Updated todo: ${todo.title}`
    )
  );

// Toggle completion
export const toggleTodo = (id: number): App.App<Todo> =>
  Effect.gen(function* (_) {
    const client = yield* _(App.httpClient());
    return yield* _(client.patch<Todo>(`/todos/${id}/toggle`));
  }).pipe(
    withLogging(
      `Toggling todo ${id}`,
      todo => `Todo ${id} is now ${todo.isCompleted ? 'completed' : 'incomplete'}`
    )
  );

// Delete todo
export const deleteTodo = (id: number): App.App<void> =>
  Effect.gen(function* (_) {
    const client = yield* _(App.httpClient());
    return yield* _(client.delete(`/todos/${id}`));
  }).pipe(
    withLogging(
      `Deleting todo ${id}`,
      () => `Deleted todo ${id}`
    )
  );
```

**Key Points:**
- `Effect.gen` provides imperative-style syntax
- `yield* _()` unwraps Effect values
- `.pipe()` allows composing effects like logging
- All operations are lazy - nothing executes until run

---

## Form Validation

Using Effect's Either for validation:

```typescript
// features/todos/validation.ts
import { Either } from 'effect';
import type { CreateTodoRequest } from './types';

export interface ValidationError {
  field: string;
  message: string;
}

export type ValidationResult<A> = Either.Either<A, ValidationError[]>;

const validateTitle = (title: string): ValidationResult<string> =>
  title.trim().length > 0 && title.length <= 200
    ? Either.right(title)
    : Either.left([{
        field: 'title',
        message: 'Title is required and must be less than 200 characters'
      }]);

const validateDescription = (description: string | null): ValidationResult<string | null> =>
  description === null || description.length <= 1000
    ? Either.right(description)
    : Either.left([{
        field: 'description',
        message: 'Description must be less than 1000 characters'
      }]);

export const validateTodo = (
  input: CreateTodoRequest
): ValidationResult<CreateTodoRequest> => {
  const titleResult = validateTitle(input.title);
  const descResult = validateDescription(input.description);

  // Combine validation results - collect all errors
  if (Either.isLeft(titleResult) || Either.isLeft(descResult)) {
    const errors: ValidationError[] = [
      ...(Either.isLeft(titleResult) ? titleResult.left : []),
      ...(Either.isLeft(descResult) ? descResult.left : []),
    ];
    return Either.left(errors);
  }

  return Either.right(input);
};
```

**Important:** Effect's Either has **swapped type parameters** compared to fp-ts:
- Effect: `Either<A, E>` (right value, left error)
- fp-ts: `Either<E, A>` (left error, right value)

---

## React Integration

### Custom Hooks

```typescript
// features/todos/hooks.ts
import { useState, useEffect, useCallback } from 'react';
import { Effect, Exit } from 'effect';
import type { App } from '../../lib/AppMonad';
import { HttpClientService, LoggerService, type AppEnv } from '../../lib/AppEnv';

// RemoteData pattern for loading states
export type RemoteData<E, A> =
  | { _tag: 'NotAsked' }
  | { _tag: 'Loading' }
  | { _tag: 'Failure'; error: E }
  | { _tag: 'Success'; data: A };

export const notAsked = <E, A>(): RemoteData<E, A> => ({ _tag: 'NotAsked' });
export const loading = <E, A>(): RemoteData<E, A> => ({ _tag: 'Loading' });
export const failure = <E, A>(error: E): RemoteData<E, A> => ({
  _tag: 'Failure',
  error
});
export const success = <E, A>(data: A): RemoteData<E, A> => ({
  _tag: 'Success',
  data
});

// Helper to provide services to an Effect
const provideServices = <A>(
  effect: App<A>,
  env: AppEnv
): Effect.Effect<A, Error> =>
  effect.pipe(
    Effect.provideService(HttpClientService, env.httpClient),
    Effect.provideService(LoggerService, env.logger)
  ) as Effect.Effect<A, Error>;

// Hook to run an App operation
export const useApp = <A>(env: AppEnv) => {
  const [state, setState] = useState<RemoteData<Error, A>>(notAsked());

  const execute = useCallback(
    async (app: App<A>) => {
      setState(loading());

      const result = await Effect.runPromiseExit(
        provideServices(app, env)
      );

      if (Exit.isSuccess(result)) {
        setState(success(result.value));
      } else {
        const error = result.cause._tag === 'Fail'
          ? result.cause.error
          : new Error('Unknown error');
        setState(failure(error));
      }
    },
    [env]
  );

  return { state, execute };
};

// Hook for fetching data on mount
export const useAppQuery = <A>(
  env: AppEnv,
  app: App<A>,
  deps: React.DependencyList = []
) => {
  const [state, setState] = useState<RemoteData<Error, A>>(loading());

  useEffect(() => {
    let cancelled = false;

    const fetchData = async () => {
      const result = await Effect.runPromiseExit(
        provideServices(app, env)
      );

      if (!cancelled) {
        if (Exit.isSuccess(result)) {
          setState(success(result.value));
        } else {
          const error = result.cause._tag === 'Fail'
            ? result.cause.error
            : new Error('Unknown error');
          setState(failure(error));
        }
      }
    };

    fetchData();

    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps);

  const refetch = useCallback(async () => {
    setState(loading());
    const result = await Effect.runPromiseExit(
      provideServices(app, env)
    );

    if (Exit.isSuccess(result)) {
      setState(success(result.value));
    } else {
      const error = result.cause._tag === 'Fail'
        ? result.cause.error
        : new Error('Unknown error');
      setState(failure(error));
    }
  }, [app, env]);

  return { state, refetch };
};
```

**Key Points:**
- `Effect.runPromiseExit` runs the effect and returns an Exit type
- `Exit.isSuccess` checks if the effect succeeded
- Services are provided using `Effect.provideService`
- RemoteData pattern tracks loading states explicitly

### React Components

```typescript
// features/todos/components/TodoList.tsx
import React from 'react';
import { useAppQuery, type RemoteData } from '../hooks';
import { listTodos, type Todo } from '../api';
import type { AppEnv } from '../../../lib/AppEnv';

interface Props {
  env: AppEnv;
}

export const TodoList: React.FC<Props> = ({ env }) => {
  const { state, refetch } = useAppQuery(env, listTodos(), []);

  const renderContent = (state: RemoteData<Error, Todo[]>) => {
    switch (state._tag) {
      case 'NotAsked':
        return <div>Not loaded yet</div>;

      case 'Loading':
        return <div>Loading todos...</div>;

      case 'Failure':
        return (
          <div>
            <p>Error: {state.error.message}</p>
            <button onClick={refetch}>Retry</button>
          </div>
        );

      case 'Success':
        return (
          <div>
            <h2>Todos</h2>
            <button onClick={refetch}>Refresh</button>
            <ul>
              {state.data.map(todo => (
                <li key={todo.id}>
                  <input
                    type="checkbox"
                    checked={todo.isCompleted}
                    readOnly
                  />
                  {todo.title}
                  {todo.description && <p>{todo.description}</p>}
                </li>
              ))}
            </ul>
          </div>
        );
    }
  };

  return <div className="todo-list">{renderContent(state)}</div>;
};
```

```typescript
// features/todos/components/TodoForm.tsx
import React, { useState } from 'react';
import { Either } from 'effect';
import { useApp } from '../hooks';
import { createTodo, type CreateTodoRequest } from '../api';
import { validateTodo, type ValidationError } from '../validation';
import type { AppEnv } from '../../../lib/AppEnv';
import type { Todo } from '../types';

interface Props {
  env: AppEnv;
  onSuccess?: () => void;
}

export const TodoForm: React.FC<Props> = ({ env, onSuccess }) => {
  const [formData, setFormData] = useState<CreateTodoRequest>({
    title: '',
    description: null,
  });
  const [validationErrors, setValidationErrors] = useState<ValidationError[]>([]);
  const { state, execute } = useApp<Todo>(env);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    // Validate
    const validationResult = validateTodo(formData);

    if (Either.isLeft(validationResult)) {
      // Validation failed
      setValidationErrors(validationResult.left);
    } else {
      // Validation passed
      setValidationErrors([]);

      // Execute the API call
      await execute(createTodo(validationResult.right));

      if (state._tag === 'Success' || state._tag === 'NotAsked') {
        setFormData({ title: '', description: null });
        onSuccess?.();
      }
    }
  };

  const getFieldError = (field: string) =>
    validationErrors.find(e => e.field === field)?.message;

  return (
    <form onSubmit={handleSubmit} className="todo-form">
      <div className="form-group">
        <label htmlFor="title">Title *</label>
        <input
          id="title"
          type="text"
          value={formData.title}
          onChange={(e) => setFormData({ ...formData, title: e.target.value })}
          className={getFieldError('title') ? 'error-input' : ''}
          placeholder="Enter todo title"
        />
        {getFieldError('title') && (
          <span className="error-message">{getFieldError('title')}</span>
        )}
      </div>

      <div className="form-group">
        <label htmlFor="description">Description</label>
        <textarea
          id="description"
          value={formData.description || ''}
          onChange={(e) => setFormData({ ...formData, description: e.target.value || null })}
          className={getFieldError('description') ? 'error-input' : ''}
          placeholder="Enter todo description (optional)"
          rows={3}
        />
        {getFieldError('description') && (
          <span className="error-message">{getFieldError('description')}</span>
        )}
      </div>

      <button type="submit" disabled={state._tag === 'Loading'} className="btn btn-primary">
        {state._tag === 'Loading' ? 'Creating...' : 'Create Todo'}
      </button>

      {state._tag === 'Failure' && (
        <div className="error-message">Error: {state.error.message}</div>
      )}

      {state._tag === 'Success' && (
        <div className="success-message">Todo created successfully!</div>
      )}
    </form>
  );
};
```

### App Setup

```typescript
// App.tsx
import React from 'react';
import { createHttpClient } from './lib/HttpClient';
import type { AppEnv } from './lib/AppEnv';
import { TodoList } from './features/todos/components/TodoList';
import { TodoForm } from './features/todos/components/TodoForm';

// Create the environment
const env: AppEnv = {
  httpClient: createHttpClient('http://localhost:5000'),
  logger: {
    info: (message) => console.info(message),
    warn: (message) => console.warn(message),
    error: (message, err) => console.error(message, err),
  },
  baseUrl: 'http://localhost:5000',
};

export const App: React.FC = () => {
  return (
    <div className="app">
      <h1>Todo Management</h1>

      <section>
        <h2>Create Todo</h2>
        <TodoForm env={env} />
      </section>

      <section>
        <h2>All Todos</h2>
        <TodoList env={env} />
      </section>
    </div>
  );
};
```

---

## State Management

### With Context (Recommended for Medium Apps)

```typescript
// lib/AppContext.tsx
import React, { createContext, useContext } from 'react';
import type { AppEnv } from './AppEnv';

const AppContext = createContext<AppEnv | null>(null);

export const AppProvider: React.FC<{ env: AppEnv; children: React.ReactNode }> = ({
  env,
  children,
}) => {
  return <AppContext.Provider value={env}>{children}</AppContext.Provider>;
};

export const useAppEnv = (): AppEnv => {
  const env = useContext(AppContext);
  if (!env) {
    throw new Error('useAppEnv must be used within AppProvider');
  }
  return env;
};

// Updated hooks that use context
export const useAppQuery = <A>(
  app: App<A>,
  deps: React.DependencyList = []
) => {
  const env = useAppEnv();
  // ... same implementation as before
};
```

---

## Common Patterns

### Pattern 1: Sequential Operations

```typescript
// Fetch todo, then fetch related metadata
const getTodoWithMetadata = (id: number): App.App<TodoWithMetadata> =>
  Effect.gen(function* (_) {
    const todo = yield* _(getTodo(id));
    const metadata = yield* _(getMetadata(todo.id));
    return { todo, metadata };
  });
```

### Pattern 2: Parallel Operations

```typescript
import { Effect } from 'effect';

// Fetch multiple todos in parallel
const getMultipleTodos = (ids: number[]): App.App<Todo[]> =>
  Effect.all(ids.map(id => getTodo(id)));

// Fetch different types in parallel
const getDashboardData = (): App.App<DashboardData> =>
  Effect.gen(function* (_) {
    const [todos, stats, user] = yield* _(
      Effect.all([
        listTodos(),
        getStats(),
        getCurrentUser()
      ])
    );
    return { todos, stats, user };
  });
```

### Pattern 3: Conditional Operations

```typescript
const getTodoOrCreate = (id: number): App.App<Todo> =>
  getTodo(id).pipe(
    Effect.catchAll(err =>
      Effect.gen(function* (_) {
        yield* _(App.logInfo(`Todo ${id} not found, creating default`));
        return yield* _(createTodo({ title: 'Default', description: null }));
      })
    )
  );
```

### Pattern 4: Error Recovery

```typescript
// Try primary, fallback to secondary
const getTodoResilient = (id: number): App.App<Todo> =>
  getTodo(id).pipe(
    Effect.catchAll(() =>
      Effect.gen(function* (_) {
        yield* _(App.logWarn(`Primary failed, trying cache`));
        return yield* _(getTodoFromCache(id));
      })
    ),
    Effect.catchAll(() =>
      Effect.gen(function* (_) {
        yield* _(App.logWarn(`Cache failed, returning default`));
        return getDefaultTodo(id);
      })
    )
  );
```

### Pattern 5: Resource Management

```typescript
import { Effect } from 'effect';

// Automatically cleanup resources
const withDatabase = <A>(
  operation: (db: Database) => App.App<A>
): App.App<A> =>
  Effect.gen(function* (_) {
    const db = yield* _(openDatabase());
    try {
      return yield* _(operation(db));
    } finally {
      yield* _(closeDatabase(db));
    }
  });
```

### Pattern 6: Timeout and Retry

```typescript
import { Effect, Schedule, Duration } from 'effect';

const getTodoWithRetry = (id: number): App.App<Todo> =>
  getTodo(id).pipe(
    Effect.timeout(Duration.seconds(5)),
    Effect.retry({
      times: 3,
      schedule: Schedule.exponential(Duration.seconds(1))
    })
  );
```

---

## Best Practices

### 1. Use Effect.gen for Readability

```typescript
// ❌ Less readable - deeply nested
const createAndFetch = (request: CreateTodoRequest) =>
  createTodo(request).pipe(
    Effect.flatMap(todo => getTodo(todo.id))
  );

// ✅ More readable - imperative style
const createAndFetch = (request: CreateTodoRequest) =>
  Effect.gen(function* (_) {
    const todo = yield* _(createTodo(request));
    return yield* _(getTodo(todo.id));
  });
```

### 2. Use Type Guards

```typescript
// Type-safe state matching
const renderTodoState = (state: RemoteData<Error, Todo>) => {
  switch (state._tag) {
    case 'NotAsked':
      return <div>Not loaded</div>;
    case 'Loading':
      return <div>Loading...</div>;
    case 'Failure':
      return <div>Error: {state.error.message}</div>;
    case 'Success':
      return <div>{state.data.title}</div>;
  }
};
```

### 3. Compose Effects Consistently

```typescript
// Always compose in the same order
const apiCallWithEffects = (url: string) =>
  fetchFromApi(url).pipe(
    withRetry(3),        // 1. Retry on failure
    withCache(url),      // 2. Cache results
    withLogging('API')   // 3. Log everything
  );
```

### 4. Handle All Error Cases

```typescript
// ❌ Bad - ignoring errors
useEffect(() => {
  Effect.runPromise(provideServices(getTodos(), env));
}, []);

// ✅ Good - handling errors
useEffect(() => {
  Effect.runPromiseExit(provideServices(getTodos(), env)).then(
    exit => {
      if (Exit.isSuccess(exit)) {
        console.log('Loaded todos:', exit.value);
      } else {
        console.error('Failed to load todos:', exit.cause);
      }
    }
  );
}, []);
```

### 5. Extract Reusable Operations

```typescript
// Create reusable composed operations
const createApiCall = <A>(
  name: string,
  operation: App.App<A>
): App.App<A> =>
  operation.pipe(
    Effect.retry({ times: 3 }),
    Effect.timeout(Duration.seconds(30)),
    withLogging(`API: ${name}`, () => `${name} completed`)
  );

// Use it
const getTodo = (id: number) =>
  createApiCall(
    `get-todo-${id}`,
    Effect.gen(function* (_) {
      const client = yield* _(App.httpClient());
      return yield* _(client.get<Todo>(`/todos/${id}`));
    })
  );
```

---

## Resources

- [Effect Documentation](https://effect.website/)
- [Effect GitHub](https://github.com/Effect-TS/effect)
- [Effect Discord Community](https://discord.gg/effect-ts)
- [Effect Tutorial](https://effect.website/docs/introduction)

---

## Conclusion

The functional patterns from backend (language-ext) translate beautifully to frontend (Effect):

| Backend (C#/language-ext) | Frontend (TS/Effect) |
|---------------------------|---------------------|
| `Db<A>` | `App<A>` |
| `DbEnv` | `AppEnv` / Context.Tag |
| `ReaderT<DbEnv, IO, A>` | `Effect<A, Error, R>` |
| `WithTransaction()` | `withRetry()`, `withCache()` |
| `ProductRepository` | Product API module |
| ASP.NET endpoints | React hooks |

The benefits are the same: **type safety, composability, testability, and maintainability**!

Effect provides even more:
- **Generator syntax** for readable code
- **Unified effect type** (no manual Reader + TaskEither)
- **Built-in scheduling, resources, and tracing**
- **Better type inference**
- **Smaller bundle size** (tree-shakeable)

🚀
