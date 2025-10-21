# Functional Frontend with fp-ts

A comprehensive guide to building TypeScript frontend applications using functional programming patterns with fp-ts.

## Table of Contents

- [Overview](#overview)
- [Setup](#setup)
- [Core Architecture](#core-architecture)
- [The App Monad](#the-app-monad)
- [Effect Composition](#effect-composition)
- [React Integration](#react-integration)
- [Form Validation](#form-validation)
- [API Client Patterns](#api-client-patterns)
- [State Management](#state-management)
- [Common Patterns](#common-patterns)

---

## Overview

Just like the backend with language-ext, we can compose multiple effects in a type-safe way:

- **API calls** via fetch/axios
- **Logging** to console or services
- **Error handling** with automatic recovery
- **Validation** with error accumulation
- **State management** with predictable updates
- **Caching** for performance
- **Loading states** automatically

### Key Benefits

✅ **Type-safe** - compiler catches errors  
✅ **Composable** - stack effects naturally  
✅ **Testable** - easy to mock dependencies  
✅ **Declarative** - clear intent  
✅ **Error handling** - automatic short-circuiting  
✅ **No callback hell** - flat composition  

---

## Setup

### Install Dependencies

```bash
npm install fp-ts
# or
yarn add fp-ts

# Optional but recommended for RemoteData pattern
npm install @devexperts/remote-data-ts
```

### Project Structure

```
src/
├── lib/
│   ├── AppMonad.ts           # App monad implementation
│   ├── AppEnv.ts             # Environment type
│   ├── HttpClient.ts         # HTTP client implementation
│   └── effects/
│       └── logging.ts        # Logging effect
├── features/
│   └── todos/
│       ├── api.ts            # Todo API calls
│       ├── types.ts          # Todo type definitions
│       ├── validation.ts     # Todo validation
│       ├── hooks.ts          # React hooks
│       └── components/
│           ├── TodoList.tsx
│           └── TodoForm.tsx
└── App.tsx
```

---

## Core Architecture

### The AppEnv Type

The environment holds all dependencies:

```typescript
import { TaskEither } from 'fp-ts/TaskEither';
import * as TE from 'fp-ts/TaskEither';
import * as E from 'fp-ts/Either';

interface Logger {
  info: (message: string) => void;
  warn: (message: string) => void;
  error: (message: string, err?: unknown) => void;
}

interface HttpClient {
  get: <A>(url: string) => TaskEither<Error, A>;
  post: <A, B>(url: string, body: A) => TaskEither<Error, B>;
  put: <A, B>(url: string, body: A) => TaskEither<Error, B>;
  patch: <A>(url: string) => TaskEither<Error, A>;
  delete: (url: string) => TaskEither<Error, void>;
}

interface Cache {
  get: <A>(key: string) => TaskEither<Error, A | null>;
  set: <A>(key: string, value: A, ttl?: number) => TaskEither<Error, void>;
  invalidate: (key: string) => TaskEither<Error, void>;
}

export interface AppEnv {
  httpClient: HttpClient;
  logger: Logger;
  baseUrl: string;
}
```

### The App Monad

```typescript
import * as R from 'fp-ts/Reader';
import * as TE from 'fp-ts/TaskEither';
import { pipe } from 'fp-ts/function';

// App<A> = Reader<AppEnv, TaskEither<Error, A>>
// Same structure as: ReaderT<AppEnv, IO, A> in C#
export type App<A> = R.Reader<AppEnv, TE.TaskEither<Error, A>>;

// Basic constructors
export const of = <A>(a: A): App<A> => 
  R.of(TE.of(a));

export const fail = <A>(error: Error): App<A> => 
  R.of(TE.left(error));

// Lift a TaskEither into App
export const fromTaskEither = <A>(te: TE.TaskEither<Error, A>): App<A> =>
  R.of(te);

// Access the environment
export const ask = (): App<AppEnv> =>
  R.asks((env: AppEnv) => TE.of(env));

// Lift an async operation
export const fromAsync = <A>(f: () => Promise<A>): App<A> =>
  R.of(TE.tryCatch(
    f,
    (reason) => reason instanceof Error ? reason : new Error(String(reason))
  ));

// Map over the result
export const map = <A, B>(f: (a: A) => B) =>
  (fa: App<A>): App<B> =>
    pipe(fa, R.map(TE.map(f)));

// FlatMap for chaining
export const chain = <A, B>(f: (a: A) => App<B>) =>
  (fa: App<A>): App<B> =>
    (env: AppEnv) =>
      pipe(
        fa(env),
        TE.chain(a => f(a)(env))
      );

// Run the App with an environment
export const run = <A>(env: AppEnv) => 
  (app: App<A>): TE.TaskEither<Error, A> =>
    app(env);
```

### Helper Functions

```typescript
// Accessing dependencies
export const logger = (): App<Logger> =>
  pipe(
    ask(),
    map(env => env.logger)
  );

export const httpClient = (): App<HttpClient> =>
  pipe(
    ask(),
    map(env => env.httpClient)
  );

// Logging operations
export const logInfo = (message: string): App<void> =>
  pipe(
    logger(),
    chain(log => fromAsync(() => Promise.resolve(log.info(message))))
  );

export const logError = (message: string, err?: unknown): App<void> =>
  pipe(
    logger(),
    chain(log => fromAsync(() => Promise.resolve(log.error(message, err))))
  );

export const logWarn = (message: string): App<void> =>
  pipe(
    logger(),
    chain(log => fromAsync(() => Promise.resolve(log.warn(message))))
  );
```

---

## Effect Composition

### Logging Effect

```typescript
// lib/effects/logging.ts
import * as App from '../AppMonad';
import { pipe } from 'fp-ts/function';

export const withLogging = <A>(
  startMessage: string,
  successMessage: (a: A) => string
) => 
  (operation: App.App<A>): App.App<A> =>
    pipe(
      App.logInfo(startMessage),
      App.chain(() => operation),
      App.chain(result =>
        pipe(
          App.logInfo(successMessage(result)),
          App.map(() => result)
        )
      )
    );

// Usage
const getTodo = (id: number) =>
  pipe(
    fetchTodoFromApi(id),
    withLogging(
      `Fetching todo ${id}`,
      todo => `Successfully fetched: ${todo.title}`
    )
  );
```

### Cache Effect

```typescript
// lib/effects/cache.ts
import * as App from '../AppMonad';
import * as TE from 'fp-ts/TaskEither';
import * as O from 'fp-ts/Option';
import { pipe } from 'fp-ts/function';

export const withCache = <A>(
  key: string,
  ttl?: number
) => 
  (operation: App.App<A>): App.App<A> =>
    pipe(
      App.cache(),
      App.chain(cacheOpt =>
        O.fromNullable(cacheOpt).fold(
          // No cache available, just run operation
          () => operation,
          // Cache available
          cache => pipe(
            App.fromTaskEither(cache.get<A>(key)),
            App.chain(cached =>
              cached !== null
                ? pipe(
                    App.logInfo(`Cache hit: ${key}`),
                    App.map(() => cached)
                  )
                : pipe(
                    App.logInfo(`Cache miss: ${key}`),
                    App.chain(() => operation),
                    App.chain(result =>
                      pipe(
                        App.fromTaskEither(cache.set(key, result, ttl)),
                        App.map(() => result)
                      )
                    )
                  )
            )
          )
        )
      )
    );

// Usage
const getTodo = (id: number) =>
  pipe(
    fetchTodoFromApi(id),
    withCache(`todo:${id}`, 5 * 60 * 1000) // 5 minutes
  );
```

### Retry Effect

```typescript
// lib/effects/retry.ts
import * as App from '../AppMonad';
import * as TE from 'fp-ts/TaskEither';
import { pipe } from 'fp-ts/function';

export const withRetry = (
  maxAttempts: number,
  delayMs: number = 1000
) => 
  <A>(operation: App.App<A>): App.App<A> => {
    const tryExecute = (attempt: number): App.App<A> =>
      attempt > maxAttempts
        ? App.fail(new Error(`Failed after ${maxAttempts} attempts`))
        : pipe(
            operation,
            App.chain(result => App.of(result)),
            // On error, retry
            env => pipe(
              env(operation),
              TE.orElse(err =>
                attempt < maxAttempts
                  ? pipe(
                      App.logInfo(`Retry attempt ${attempt + 1}/${maxAttempts}`),
                      App.chain(() => 
                        App.fromAsync(() => 
                          new Promise(resolve => setTimeout(resolve, delayMs))
                        )
                      ),
                      App.chain(() => tryExecute(attempt + 1))
                    )(env)
                  : TE.left(err)
              )
            )
          );
    
    return tryExecute(1);
  };
```

### Loading State Effect

```typescript
// lib/effects/loading.ts
import * as App from '../AppMonad';
import { pipe } from 'fp-ts/function';

export const withLoadingState = <A>(
  setLoading: (loading: boolean) => void
) => 
  (operation: App.App<A>): App.App<A> =>
    pipe(
      App.fromAsync(() => Promise.resolve(setLoading(true))),
      App.chain(() => operation),
      App.chain(result =>
        pipe(
          App.fromAsync(() => Promise.resolve(setLoading(false))),
          App.map(() => result)
        )
      ),
      // Ensure loading is set to false even on error
      env => pipe(
        env(operation),
        TE.fold(
          err => pipe(
            App.fromAsync(() => Promise.resolve(setLoading(false))),
            App.chain(() => App.fail<A>(err))
          )(env),
          result => pipe(
            App.fromAsync(() => Promise.resolve(setLoading(false))),
            App.map(() => result)
          )(env)
        )
      )
    );
```

---

## API Client Patterns

### Basic HTTP Client

```typescript
// lib/HttpClient.ts
import * as TE from 'fp-ts/TaskEither';

export const createHttpClient = (baseURL: string): HttpClient => ({
  get: <A>(url: string) =>
    TE.tryCatch(
      async () => {
        const response = await fetch(`${baseURL}${url}`);
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        return response.json() as Promise<A>;
      },
      (reason) => reason instanceof Error ? reason : new Error(String(reason))
    ),

  post: <A, B>(url: string, body: A) =>
    TE.tryCatch(
      async () => {
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
      (reason) => reason instanceof Error ? reason : new Error(String(reason))
    ),

  put: <A, B>(url: string, body: A) =>
    TE.tryCatch(
      async () => {
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
      (reason) => reason instanceof Error ? reason : new Error(String(reason))
    ),

  patch: <A>(url: string) =>
    TE.tryCatch(
      async () => {
        const response = await fetch(`${baseURL}${url}`, {
          method: 'PATCH',
        });
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
        return response.json() as Promise<A>;
      },
      (reason) => reason instanceof Error ? reason : new Error(String(reason))
    ),

  delete: (url: string) =>
    TE.tryCatch(
      async () => {
        const response = await fetch(`${baseURL}${url}`, {
          method: 'DELETE',
        });
        if (!response.ok) {
          throw new Error(`HTTP ${response.status}: ${response.statusText}`);
        }
      },
      (reason) => reason instanceof Error ? reason : new Error(String(reason))
    ),
});
```

### Todo API Example

```typescript
// features/todos/types.ts
export interface Todo {
  id: number;
  title: string;
  description: string | null;
  isCompleted: boolean;
  createdAt: string;
  completedAt: string | null;
}

export interface CreateTodoRequest {
  title: string;
  description: string | null;
}

export interface UpdateTodoRequest {
  title: string;
  description: string | null;
}
```

```typescript
// features/todos/api.ts
import * as App from '../../lib/AppMonad';
import { pipe } from 'fp-ts/function';
import { withLogging } from '../../lib/effects/logging';
import type { Todo, CreateTodoRequest, UpdateTodoRequest } from './types';

// Re-export types
export type { Todo, CreateTodoRequest, UpdateTodoRequest } from './types';

// Get all todos
export const listTodos = (): App.App<Todo[]> =>
  pipe(
    App.httpClient(),
    App.chain(client => App.fromTaskEither(client.get<Todo[]>('/todos'))),
    withLogging(
      'Fetching all todos',
      todos => `Fetched ${todos.length} todos`
    )
  );

// Get single todo
export const getTodo = (id: number): App.App<Todo> =>
  pipe(
    App.httpClient(),
    App.chain(client =>
      App.fromTaskEither(client.get<Todo>(`/todos/${id}`))
    ),
    withLogging(
      `Fetching todo ${id}`,
      todo => `Fetched todo: ${todo.title}`
    )
  );

// Create todo
export const createTodo = (
  request: CreateTodoRequest
): App.App<Todo> =>
  pipe(
    App.httpClient(),
    App.chain(client =>
      App.fromTaskEither(client.post<CreateTodoRequest, Todo>(
        '/todos',
        request
      ))
    ),
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
  pipe(
    App.httpClient(),
    App.chain(client =>
      App.fromTaskEither(client.put<UpdateTodoRequest, Todo>(
        `/todos/${id}`,
        request
      ))
    ),
    withLogging(
      `Updating todo ${id}`,
      todo => `Updated todo: ${todo.title}`
    )
  );

// Toggle completion
export const toggleTodo = (id: number): App.App<Todo> =>
  pipe(
    App.httpClient(),
    App.chain(client =>
      App.fromTaskEither(client.patch<Todo>(`/todos/${id}/toggle`))
    ),
    withLogging(
      `Toggling todo ${id}`,
      todo => `Todo ${id} is now ${todo.isCompleted ? 'completed' : 'incomplete'}`
    )
  );

// Delete todo
export const deleteTodo = (id: number): App.App<void> =>
  pipe(
    App.httpClient(),
    App.chain(client =>
      App.fromTaskEither(client.delete(`/todos/${id}`))
    ),
    withLogging(
      `Deleting todo ${id}`,
      () => `Deleted todo ${id}`
    )
  );
```

---

## Form Validation

Using `fp-ts` for validation logic with error accumulation:

```typescript
// features/todos/validation.ts
import * as E from 'fp-ts/Either';
import * as A from 'fp-ts/Apply';
import { pipe } from 'fp-ts/function';
import { getValidation } from 'fp-ts/Either';
import { getSemigroup } from 'fp-ts/Array';
import type { CreateTodoRequest } from './types';

export interface ValidationError {
  field: string;
  message: string;
}

export type ValidationResult<A> = E.Either<ValidationError[], A>;

// Individual field validators
const validateTitle = (title: string): ValidationResult<string> =>
  title.trim().length > 0 && title.length <= 200
    ? E.right(title)
    : E.left([{
        field: 'title',
        message: 'Title is required and must be less than 200 characters'
      }]);

const validateDescription = (description: string | null): ValidationResult<string | null> =>
  description === null || description.length <= 1000
    ? E.right(description)
    : E.left([{
        field: 'description',
        message: 'Description must be less than 1000 characters'
      }]);

// Combine validators with applicative to accumulate ALL errors
const validationApplicative = getValidation(getSemigroup<ValidationError>());

export const validateTodo = (
  input: CreateTodoRequest
): ValidationResult<CreateTodoRequest> =>
  pipe(
    A.sequenceS(validationApplicative)({
      title: validateTitle(input.title),
      description: validateDescription(input.description),
    }),
    E.map(() => input)
  );
```

---

## React Integration

### Custom Hooks

```typescript
// features/products/hooks.ts
import { useState, useEffect, useCallback } from 'react';
import * as App from '../../lib/AppMonad';
import * as E from 'fp-ts/Either';
import { pipe } from 'fp-ts/function';
import type { AppEnv } from '../../lib/AppEnv';

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

// Hook to run an App operation
export const useApp = <A>(env: AppEnv) => {
  const [state, setState] = useState<RemoteData<Error, A>>(notAsked());

  const execute = useCallback(
    async (app: App.App<A>) => {
      setState(loading());
      
      const result = await pipe(
        app,
        App.run(env)
      )();

      pipe(
        result,
        E.fold(
          (error) => setState(failure(error)),
          (data) => setState(success(data))
        )
      );
    },
    [env]
  );

  return { state, execute };
};

// Hook for fetching data on mount
export const useAppQuery = <A>(
  env: AppEnv,
  app: App.App<A>,
  deps: React.DependencyList = []
) => {
  const [state, setState] = useState<RemoteData<Error, A>>(loading());

  useEffect(() => {
    let cancelled = false;

    const fetchData = async () => {
      const result = await pipe(app, App.run(env))();

      if (!cancelled) {
        pipe(
          result,
          E.fold(
            (error) => setState(failure(error)),
            (data) => setState(success(data))
          )
        );
      }
    };

    fetchData();

    return () => {
      cancelled = true;
    };
  }, deps);

  const refetch = useCallback(async () => {
    setState(loading());
    const result = await pipe(app, App.run(env))();
    
    pipe(
      result,
      E.fold(
        (error) => setState(failure(error)),
        (data) => setState(success(data))
      )
    );
  }, [app, env]);

  return { state, refetch };
};
```

### React Components

```typescript
// features/todos/components/TodoList.tsx
import React from 'react';
import { useAppQuery, type RemoteData } from '../hooks';
import { listTodos, toggleTodo, deleteTodo, type Todo } from '../api';
import { useApp } from '../hooks';
import type { AppEnv } from '../../../lib/AppEnv';

interface Props {
  env: AppEnv;
  onRefresh?: () => void;
}

export const TodoList: React.FC<Props> = ({ env, onRefresh }) => {
  const { state, refetch } = useAppQuery(env, listTodos(), []);
  const { execute: executeToggle } = useApp<Todo>(env);
  const { execute: executeDelete } = useApp<void>(env);

  const handleToggle = async (id: number) => {
    await executeToggle(toggleTodo(id));
    refetch();
    onRefresh?.();
  };

  const handleDelete = async (id: number) => {
    if (window.confirm('Are you sure you want to delete this todo?')) {
      await executeDelete(deleteTodo(id));
      refetch();
      onRefresh?.();
    }
  };

  const renderContent = (state: RemoteData<Error, Todo[]>) => {
    switch (state._tag) {
      case 'NotAsked':
        return <div className="info">Not loaded yet</div>;

      case 'Loading':
        return <div className="info">Loading todos...</div>;

      case 'Failure':
        return (
          <div className="error-container">
            <p className="error">Error: {state.error.message}</p>
            <button onClick={refetch} className="btn btn-secondary">
              Retry
            </button>
          </div>
        );

      case 'Success':
        if (state.data.length === 0) {
          return (
            <div className="empty-state">
              <p>No todos yet. Create one to get started!</p>
              <button onClick={refetch} className="btn btn-secondary">
                Refresh
              </button>
            </div>
          );
        }

        return (
          <div>
            <div className="list-header">
              <h2>Your Todos</h2>
              <button onClick={refetch} className="btn btn-secondary">
                Refresh
              </button>
            </div>
            <ul className="todo-list">
              {state.data.map(todo => (
                <li key={todo.id} className={`todo-item ${todo.isCompleted ? 'completed' : ''}`}>
                  <div className="todo-content">
                    <div className="todo-info">
                      <h3>{todo.title}</h3>
                      {todo.description && <p>{todo.description}</p>}
                      <small>
                        Created: {new Date(todo.createdAt).toLocaleString()}
                        {todo.completedAt && ` | Completed: ${new Date(todo.completedAt).toLocaleString()}`}
                      </small>
                    </div>
                    <div className="todo-actions">
                      <button
                        onClick={() => handleToggle(todo.id)}
                        className={`btn ${todo.isCompleted ? 'btn-warning' : 'btn-success'}`}
                      >
                        {todo.isCompleted ? 'Undo' : 'Complete'}
                      </button>
                      <button
                        onClick={() => handleDelete(todo.id)}
                        className="btn btn-danger"
                      >
                        Delete
                      </button>
                    </div>
                  </div>
                </li>
              ))}
            </ul>
          </div>
        );
    }
  };

  return <div className="todo-list-container">{renderContent(state)}</div>;
};
```

```typescript
// features/todos/components/TodoForm.tsx
import React, { useState } from 'react';
import { pipe } from 'fp-ts/function';
import * as E from 'fp-ts/Either';
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

    pipe(
      validationResult,
      E.fold(
        // Validation failed
        (errors) => {
          setValidationErrors(errors);
        },
        // Validation passed
        async (validData) => {
          setValidationErrors([]);

          // Execute the API call
          await execute(createTodo(validData));

          if (state._tag === 'Success' || state._tag === 'NotAsked') {
            setFormData({ title: '', description: null });
            onSuccess?.();
          }
        }
      )
    );
  };

  const getFieldError = (field: string) =>
    validationErrors.find(e => e.field === field)?.message;

  return (
    <form onSubmit={handleSubmit} className="todo-form">
      <div className="form-group">
        <label htmlFor="title">
          Title *
        </label>
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
        <label htmlFor="description">
          Description
        </label>
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
import React, { useState } from 'react';
import { createHttpClient } from './lib/HttpClient';
import type { AppEnv } from './lib/AppEnv';
import { TodoList } from './features/todos/components/TodoList';
import { TodoForm } from './features/todos/components/TodoForm';
import './App.css';

// Create the environment
const env: AppEnv = {
  httpClient: createHttpClient('http://localhost:5000'),
  logger: {
    info: (message) => console.info('[INFO]', message),
    warn: (message) => console.warn('[WARN]', message),
    error: (message, err) => console.error('[ERROR]', message, err),
  },
  baseUrl: 'http://localhost:5000',
};

export const App: React.FC = () => {
  const [refreshKey, setRefreshKey] = useState(0);

  const handleSuccess = () => {
    // Trigger refresh of the list
    setRefreshKey(prev => prev + 1);
  };

  return (
    <div className="app">
      <header className="app-header">
        <h1>Functional Todo App</h1>
        <p className="subtitle">Built with fp-ts & React</p>
      </header>

      <div className="container">
        <section className="section">
          <h2>Create New Todo</h2>
          <TodoForm env={env} onSuccess={handleSuccess} />
        </section>

        <section className="section">
          <TodoList key={refreshKey} env={env} onRefresh={handleSuccess} />
        </section>
      </div>

      <footer className="app-footer">
        <p>
          Functional Programming with <strong>fp-ts</strong> |
          Backend: ASP.NET Core with <strong>language-ext</strong>
        </p>
      </footer>
    </div>
  );
};

export default App;
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
  app: App.App<A>,
  deps: React.DependencyList = []
) => {
  const env = useAppEnv();
  // ... same implementation as before
};
```

### With Redux Toolkit (For Large Apps)

```typescript
// store/todosSlice.ts
import { createSlice, createAsyncThunk } from '@reduxjs/toolkit';
import * as E from 'fp-ts/Either';
import { pipe } from 'fp-ts/function';
import * as App from '../lib/AppMonad';
import { listTodos, type Todo } from '../features/todos/api';
import type { AppEnv } from '../lib/AppEnv';

interface TodosState {
  items: Todo[];
  loading: boolean;
  error: string | null;
}

const initialState: TodosState = {
  items: [],
  loading: false,
  error: null,
};

// Thunk that runs an App operation
export const fetchTodos = createAsyncThunk<
  Todo[],
  AppEnv,
  { rejectValue: string }
>(
  'todos/fetchTodos',
  async (env, { rejectWithValue }) => {
    const result = await pipe(listTodos(), App.run(env))();

    return pipe(
      result,
      E.fold(
        (error) => rejectWithValue(error.message),
        (todos) => todos
      )
    );
  }
);

export const todosSlice = createSlice({
  name: 'todos',
  initialState,
  reducers: {},
  extraReducers: (builder) => {
    builder
      .addCase(fetchTodos.pending, (state) => {
        state.loading = true;
        state.error = null;
      })
      .addCase(fetchTodos.fulfilled, (state, action) => {
        state.loading = false;
        state.items = action.payload;
      })
      .addCase(fetchTodos.rejected, (state, action) => {
        state.loading = false;
        state.error = action.payload ?? 'Unknown error';
      });
  },
});

export default todosSlice.reducer;
```

---

## Common Patterns

### Pattern 1: Sequential Operations

```typescript
// Fetch todo, then fetch related items (e.g., subtasks or comments)
const getTodoWithDetails = (id: number): App.App<TodoWithDetails> =>
  pipe(
    getTodo(id),
    App.chain(todo =>
      pipe(
        getTodoComments(todo.id),
        App.map(comments => ({ todo, comments }))
      )
    )
  );
```

### Pattern 2: Parallel Operations

```typescript
import * as A from 'fp-ts/Array';
import { sequenceT } from 'fp-ts/Apply';

// Fetch multiple todos in parallel
const getMultipleTodos = (ids: number[]): App.App<Todo[]> =>
  pipe(
    ids,
    A.map(id => getTodo(id)),
    A.sequence(App.Applicative) // Requires defining Applicative instance
  );

// Or using sequenceT for different types
const getDashboardData = (): App.App<DashboardData> =>
  pipe(
    sequenceT(App.Applicative)(
      listTodos(),
      getCompletedCount(),
      getRecentActivity()
    ),
    App.map(([todos, completed, activity]) => ({
      todos,
      completed,
      activity
    }))
  );
```

### Pattern 3: Conditional Operations

```typescript
const getTodoOrCreate = (id: number): App.App<Todo> =>
  pipe(
    getTodo(id),
    // On error, create a default todo
    env => pipe(
      env(getTodo(id)),
      TE.orElse(err =>
        pipe(
          App.logInfo(`Todo ${id} not found, creating default`),
          App.chain(() => createTodo({ title: 'Default', description: null }))
        )(env)
      )
    )
  );
```

### Pattern 4: Error Recovery

```typescript
// Try primary, fallback to secondary
const getTodoResilient = (id: number): App.App<Todo> =>
  pipe(
    getTodo(id),
    env => pipe(
      env(getTodo(id)),
      TE.orElse(() =>
        pipe(
          App.logWarn(`Primary failed, trying cache`),
          App.chain(() => getTodoFromCache(id))
        )(env)
      ),
      TE.orElse(() =>
        pipe(
          App.logWarn(`Cache failed, returning default`),
          App.map(() => getDefaultTodo(id))
        )(env)
      )
    )
  );
```

### Pattern 5: Batch Operations with Error Handling

```typescript
// Process array with individual error handling
const createMultipleTodos = (
  requests: CreateTodoRequest[]
): App.App<Array<E.Either<Error, Todo>>> =>
  pipe(
    requests,
    A.traverse(App.Applicative)(request =>
      pipe(
        createTodo(request),
        App.chain(todo => App.of(E.right(todo))),
        env => pipe(
          env(createTodo(request)),
          TE.fold(
            err => TE.of(E.left(err)),
            todo => TE.of(E.right(todo))
          )
        )
      )
    )
  );
```

---

## Best Practices

### 1. Keep Operations Pure

```typescript
// ❌ Bad - side effect hidden
const getTodo = (id: number): App.App<Todo> => {
  console.log(`Fetching ${id}`); // Hidden side effect
  return fetchFromApi(id);
};

// ✅ Good - side effect explicit
const getTodo = (id: number): App.App<Todo> =>
  pipe(
    App.logInfo(`Fetching todo ${id}`),
    App.chain(() => fetchFromApi(id))
  );
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
  pipe(
    fetchFromApi(url),
    withRetry(3),        // 1. Retry on failure
    withCache(url),      // 2. Cache results
    withLogging('API')   // 3. Log everything
  );
```

### 4. Handle All Error Cases

```typescript
// ❌ Bad - ignoring errors
useEffect(() => {
  App.run(env)(listTodos())();
}, []);

// ✅ Good - handling errors
useEffect(() => {
  App.run(env)(listTodos())().then(
    E.fold(
      err => console.error('Failed to load todos:', err),
      todos => console.log('Loaded todos:', todos)
    )
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
  pipe(
    operation,
    withRetry(3),
    withCache(name, 60000),
    withLogging(`API: ${name}`, () => `${name} completed`)
  );

// Use it
const getTodo = (id: number) =>
  createApiCall(
    `get-todo-${id}`,
    pipe(
      App.httpClient(),
      App.chain(client =>
        App.fromTaskEither(client.get<Todo>(`/todos/${id}`))
      )
    )
  );
```

---

## Comparison: Ramda vs fp-ts

### Ramda Approach

Ramda is great for **data transformation** but lacks type safety for effects:

```typescript
import * as R from 'ramda';

// Ramda - good for transformations
const processTodos = R.pipe(
  R.filter(R.propEq('isCompleted', false)),
  R.map(R.pick(['id', 'title', 'createdAt'])),
  R.sortBy(R.prop('createdAt'))
);

// But handling async/effects is not as elegant
const fetchAndProcess = async (ids: number[]) => {
  const todos = await Promise.all(ids.map(fetchTodo));
  return processTodos(todos);
};
```

### fp-ts Approach

fp-ts provides **type-safe effect composition**:

```typescript
import { pipe } from 'fp-ts/function';
import * as A from 'fp-ts/Array';

// fp-ts - type-safe effects
const fetchAndProcess = (ids: number[]): App.App<Todo[]> =>
  pipe(
    ids,
    A.traverse(App.Applicative)(fetchTodo),
    App.map(todos =>
      pipe(
        todos,
        A.filter(t => !t.isCompleted),
        A.map(t => ({ id: t.id, title: t.title, createdAt: t.createdAt })),
        A.sortBy([Ord.contramap((t: Todo) => t.createdAt)(S.Ord)])
      )
    )
  );
```

**Recommendation:** Use both!
- **Ramda** for pure data transformations
- **fp-ts** for effect composition and type safety

---

## Resources

- [fp-ts Documentation](https://gcanti.github.io/fp-ts/)
- [Functional Programming in TypeScript](https://github.com/enricopolanski/functional-programming)
- [Remote Data Pattern](https://github.com/devexperts/remote-data-ts)
- [language-ext for C#](https://github.com/louthy/language-ext) - Backend equivalent

---

## Conclusion

The functional patterns from backend (language-ext) translate beautifully to frontend (fp-ts):

| Backend (C#/language-ext) | Frontend (TS/fp-ts) |
|---------------------------|---------------------|
| `Db<A>` | `App<A>` |
| `DbEnv` | `AppEnv` |
| `ReaderT<DbEnv, IO, A>` | `Reader<AppEnv, TaskEither<Error, A>>` |
| `WithTransaction()` | `withRetry()`, `withCache()` |
| `TodoRepository` | Todo API module |
| ASP.NET endpoints | React hooks |

The benefits are the same: **type safety, composability, testability, and maintainability**! 🚀