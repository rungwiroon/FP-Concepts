# บทที่ 9: สร้าง Frontend Architecture

> สร้าง Frontend แบบ Type-Safe, Testable และ Maintainable ด้วย Effect-TS

---

## เนื้อหาในบทนี้

- ภาพรวม Frontend Architecture
- Project Setup with React + Vite + Effect-TS
- Services Layer - Service Interfaces
- Layers - Live & Mock Implementations
- Effects - Business Logic
- React Integration - Custom Hooks
- State Management with Effect
- Error Handling & Loading States
- Testing Strategy
- Best Practices

---

## 9.1 ภาพรวม Frontend Architecture

เราจะสร้าง **Todo Frontend Application** ที่ integrate กับ Backend API (จากบทที่ 5) โดยใช้ **Effect-TS** เพื่อให้มี:

### คุณสมบัติหลัก

1. **Type-Safe Services** - API calls ที่มี type ชัดเจน
2. **Dependency Injection** - Mock ได้ง่ายสำหรับ testing
3. **Composable Effects** - ต่อ business logic แบบ readable
4. **Error Handling** - จัดการ errors อย่างเป็นระบบ
5. **Loading States** - ติดตาม async operations
6. **Optimistic Updates** - UI responsive

### Architecture Overview

```
frontend/
├── src/
│   ├── domain/
│   │   └── Todo.ts                # Domain types
│   ├── services/
│   │   ├── TodoApi.ts             # API service interface
│   │   ├── Logger.ts              # Logger service interface
│   │   └── Storage.ts             # LocalStorage service interface
│   ├── layers/
│   │   ├── TodoApiLive.ts         # Real API implementation
│   │   ├── TodoApiMock.ts         # Mock for testing
│   │   ├── LoggerLive.ts
│   │   └── index.ts               # Combined layers
│   ├── effects/
│   │   ├── todos.ts               # Todo business logic
│   │   └── index.ts
│   ├── hooks/
│   │   ├── useRunEffect.ts        # Run effects in React
│   │   ├── useTodos.ts            # Todo-specific hook
│   │   └── index.ts
│   ├── components/
│   │   ├── TodoList.tsx
│   │   ├── TodoItem.tsx
│   │   ├── AddTodoForm.tsx
│   │   └── TodoFilters.tsx
│   └── App.tsx
├── package.json
└── vite.config.ts
```

### ภาพรวมการทำงาน

```
User Action
   ↓
React Component (TodoList.tsx)
   ↓
Custom Hook (useTodos)
   ↓
Business Logic Effect (effects/todos.ts)
   ↓
Service Layer (TodoApi)
   ↓
Implementation Layer (TodoApiLive or TodoApiMock)
   ↓
Backend API / Mock Data
```

**สิ่งที่แตกต่างจาก Backend:**
- **Backend (language-ext)**: `Eff<RT, A>` + `Has<M, RT, T>` pattern
- **Frontend (Effect-TS)**: `Effect<A, E, R>` + `Context.Tag<T>` pattern
- **การ run effects**: Backend run ใน controller, Frontend run ใน React hooks

---

## 9.2 Project Setup

### 9.2.1 สร้าง Project

```bash
# Create Vite project with React + TypeScript
npm create vite@latest todo-frontend -- --template react-ts
cd todo-frontend

# Install dependencies
npm install effect
npm install -D @types/node

# Install additional libraries
npm install date-fns      # Date utilities
npm install clsx          # Conditional classes
```

### 9.2.2 Configure TypeScript

**tsconfig.json:**

```json
{
  "compilerOptions": {
    "target": "ES2020",
    "useDefineForClassFields": true,
    "lib": ["ES2020", "DOM", "DOM.Iterable"],
    "module": "ESNext",
    "skipLibCheck": true,

    /* Bundler mode */
    "moduleResolution": "bundler",
    "allowImportingTsExtensions": true,
    "resolveJsonModule": true,
    "isolatedModules": true,
    "noEmit": true,
    "jsx": "react-jsx",

    /* Linting */
    "strict": true,
    "noUnusedLocals": true,
    "noUnusedParameters": true,
    "noFallthroughCasesInSwitch": true,

    /* Path mapping */
    "baseUrl": ".",
    "paths": {
      "@/*": ["./src/*"]
    }
  },
  "include": ["src"],
  "references": [{ "path": "./tsconfig.node.json" }]
}
```

### 9.2.3 Configure Vite

**vite.config.ts:**

```typescript
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src')
    }
  },
  server: {
    proxy: {
      // Proxy API calls to backend
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true
      }
    }
  }
})
```

---

## 9.3 Domain Layer

### 9.3.1 Domain Types

**src/domain/Todo.ts:**

```typescript
// Domain entity
export interface Todo {
  readonly id: string;
  readonly title: string;
  readonly completed: boolean;
  readonly createdAt: Date;
  readonly updatedAt?: Date;
}

// Create Todo Request
export interface CreateTodoRequest {
  readonly title: string;
}

// Update Todo Request
export interface UpdateTodoRequest {
  readonly title?: string;
  readonly completed?: boolean;
}

// View models
export type TodoFilter = 'all' | 'active' | 'completed';

export interface TodoStats {
  readonly total: number;
  readonly active: number;
  readonly completed: number;
}
```

---

## 9.4 Services Layer

### 9.4.1 Error Types

**src/services/errors.ts:**

```typescript
// Base error class
export abstract class AppError {
  abstract readonly _tag: string;
  abstract readonly message: string;
}

// API Errors
export class NetworkError extends AppError {
  readonly _tag = 'NetworkError';
  constructor(
    readonly message: string,
    readonly cause?: unknown
  ) {
    super();
  }
}

export class NotFoundError extends AppError {
  readonly _tag = 'NotFoundError';
  constructor(
    readonly resource: string,
    readonly id: string
  ) {
    super();
  }

  get message(): string {
    return `${this.resource} with id ${this.id} not found`;
  }
}

export class ValidationError extends AppError {
  readonly _tag = 'ValidationError';
  constructor(
    readonly field: string,
    readonly reason: string
  ) {
    super();
  }

  get message(): string {
    return `${this.field}: ${this.reason}`;
  }
}

export class UnauthorizedError extends AppError {
  readonly _tag = 'UnauthorizedError';
  readonly message = 'Unauthorized access';
}

// Union type
export type TodoError =
  | NetworkError
  | NotFoundError
  | ValidationError
  | UnauthorizedError;
```

### 9.4.2 TodoApi Service

**src/services/TodoApi.ts:**

```typescript
import { Context, Effect } from 'effect';
import type { Todo, CreateTodoRequest, UpdateTodoRequest } from '@/domain/Todo';
import type { TodoError } from './errors';

// Service interface
export interface TodoApi {
  /**
   * Fetch all todos
   */
  readonly fetchAll: () => Effect.Effect<Todo[], TodoError, never>;

  /**
   * Get todo by ID
   */
  readonly getById: (id: string) => Effect.Effect<Todo, TodoError, never>;

  /**
   * Create new todo
   */
  readonly create: (
    request: CreateTodoRequest
  ) => Effect.Effect<Todo, TodoError, never>;

  /**
   * Update existing todo
   */
  readonly update: (
    id: string,
    request: UpdateTodoRequest
  ) => Effect.Effect<Todo, TodoError, never>;

  /**
   * Toggle todo completion
   */
  readonly toggle: (id: string) => Effect.Effect<Todo, TodoError, never>;

  /**
   * Delete todo
   */
  readonly delete: (id: string) => Effect.Effect<void, TodoError, never>;
}

// Context Tag for DI
export const TodoApi = Context.GenericTag<TodoApi>('@services/TodoApi');
```

### 9.4.3 Logger Service

**src/services/Logger.ts:**

```typescript
import { Context, Effect } from 'effect';

export interface Logger {
  readonly debug: (
    message: string,
    data?: unknown
  ) => Effect.Effect<void, never, never>;

  readonly info: (
    message: string,
    data?: unknown
  ) => Effect.Effect<void, never, never>;

  readonly warn: (
    message: string,
    data?: unknown
  ) => Effect.Effect<void, never, never>;

  readonly error: (
    message: string,
    error?: unknown
  ) => Effect.Effect<void, never, never>;
}

export const Logger = Context.GenericTag<Logger>('@services/Logger');
```

### 9.4.4 Storage Service

**src/services/Storage.ts:**

```typescript
import { Context, Effect } from 'effect';
import type { TodoError } from './errors';

export interface Storage {
  /**
   * Get item from storage
   */
  readonly get: <T>(key: string) => Effect.Effect<T | null, TodoError, never>;

  /**
   * Set item in storage
   */
  readonly set: <T>(
    key: string,
    value: T
  ) => Effect.Effect<void, TodoError, never>;

  /**
   * Remove item from storage
   */
  readonly remove: (key: string) => Effect.Effect<void, TodoError, never>;

  /**
   * Clear all items
   */
  readonly clear: () => Effect.Effect<void, TodoError, never>;
}

export const Storage = Context.GenericTag<Storage>('@services/Storage');
```

---

## 9.5 Layers - Implementations

### 9.5.1 TodoApiLive - Real Implementation

**src/layers/TodoApiLive.ts:**

```typescript
import { Effect, Layer } from 'effect';
import { TodoApi } from '@/services/TodoApi';
import type { Todo, CreateTodoRequest, UpdateTodoRequest } from '@/domain/Todo';
import { NetworkError, NotFoundError } from '@/services/errors';

const API_BASE = '/api/todos';

/**
 * Helper to handle HTTP responses
 */
function handleResponse<A>(
  promise: Promise<Response>
): Effect.Effect<A, NetworkError | NotFoundError, never> {
  return Effect.tryPromise({
    try: async () => {
      const response = await promise;

      // Handle 404
      if (response.status === 404) {
        const errorData = await response.json();
        throw new NotFoundError('Todo', errorData.id || 'unknown');
      }

      // Handle other errors
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }

      // Parse JSON
      const data = await response.json();

      // Convert date strings to Date objects
      return transformDates(data);
    },
    catch: (error) => {
      if (error instanceof NotFoundError) {
        return error;
      }
      return new NetworkError('API request failed', error);
    }
  });
}

/**
 * Transform date strings to Date objects
 */
function transformDates<T>(data: T): T {
  if (Array.isArray(data)) {
    return data.map(transformDates) as T;
  }

  if (data && typeof data === 'object') {
    const result: any = {};
    for (const [key, value] of Object.entries(data)) {
      if (key === 'createdAt' || key === 'updatedAt') {
        result[key] = value ? new Date(value as string) : undefined;
      } else {
        result[key] = transformDates(value);
      }
    }
    return result;
  }

  return data;
}

/**
 * Live implementation
 */
export const TodoApiLive = Layer.succeed(
  TodoApi,
  TodoApi.of({
    fetchAll: () =>
      handleResponse<Todo[]>(
        fetch(API_BASE, {
          method: 'GET',
          headers: { 'Content-Type': 'application/json' }
        })
      ),

    getById: (id: string) =>
      handleResponse<Todo>(
        fetch(`${API_BASE}/${id}`, {
          method: 'GET',
          headers: { 'Content-Type': 'application/json' }
        })
      ),

    create: (request: CreateTodoRequest) =>
      handleResponse<Todo>(
        fetch(API_BASE, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(request)
        })
      ),

    update: (id: string, request: UpdateTodoRequest) =>
      handleResponse<Todo>(
        fetch(`${API_BASE}/${id}`, {
          method: 'PUT',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(request)
        })
      ),

    toggle: (id: string) =>
      handleResponse<Todo>(
        fetch(`${API_BASE}/${id}/toggle`, {
          method: 'PATCH',
          headers: { 'Content-Type': 'application/json' }
        })
      ),

    delete: (id: string) =>
      Effect.tryPromise({
        try: async () => {
          const response = await fetch(`${API_BASE}/${id}`, {
            method: 'DELETE',
            headers: { 'Content-Type': 'application/json' }
          });

          if (response.status === 404) {
            throw new NotFoundError('Todo', id);
          }

          if (!response.ok) {
            throw new Error(`HTTP ${response.status}`);
          }
        },
        catch: (error) => {
          if (error instanceof NotFoundError) {
            return error;
          }
          return new NetworkError('Failed to delete todo', error);
        }
      })
  })
);
```

### 9.5.2 TodoApiMock - Test Implementation

**src/layers/TodoApiMock.ts:**

```typescript
import { Effect, Layer, Ref } from 'effect';
import { TodoApi } from '@/services/TodoApi';
import type { Todo, CreateTodoRequest, UpdateTodoRequest } from '@/domain/Todo';
import { NotFoundError } from '@/services/errors';

/**
 * In-memory mock data
 */
const createMockData = (): Todo[] => [
  {
    id: '1',
    title: 'Learn Effect-TS',
    completed: false,
    createdAt: new Date('2024-01-01')
  },
  {
    id: '2',
    title: 'Build Todo App',
    completed: false,
    createdAt: new Date('2024-01-02')
  },
  {
    id: '3',
    title: 'Write Tests',
    completed: true,
    createdAt: new Date('2024-01-03')
  }
];

/**
 * Mock implementation with in-memory state
 */
export const TodoApiMock = Layer.effect(
  TodoApi,
  Effect.gen(function* (_) {
    // Create mutable state
    const todosRef = yield* _(Ref.make(createMockData()));
    let nextId = 4;

    return TodoApi.of({
      fetchAll: () =>
        Effect.gen(function* (_) {
          const todos = yield* _(Ref.get(todosRef));
          // Simulate network delay
          yield* _(Effect.sleep('100 millis'));
          return todos;
        }),

      getById: (id: string) =>
        Effect.gen(function* (_) {
          const todos = yield* _(Ref.get(todosRef));
          const todo = todos.find(t => t.id === id);

          yield* _(Effect.sleep('50 millis'));

          if (!todo) {
            return yield* _(Effect.fail(new NotFoundError('Todo', id)));
          }

          return todo;
        }),

      create: (request: CreateTodoRequest) =>
        Effect.gen(function* (_) {
          const newTodo: Todo = {
            id: String(nextId++),
            title: request.title,
            completed: false,
            createdAt: new Date()
          };

          yield* _(Ref.update(todosRef, todos => [...todos, newTodo]));
          yield* _(Effect.sleep('100 millis'));

          return newTodo;
        }),

      update: (id: string, request: UpdateTodoRequest) =>
        Effect.gen(function* (_) {
          const todos = yield* _(Ref.get(todosRef));
          const todo = todos.find(t => t.id === id);

          if (!todo) {
            return yield* _(Effect.fail(new NotFoundError('Todo', id)));
          }

          const updated: Todo = {
            ...todo,
            ...request,
            updatedAt: new Date()
          };

          yield* _(
            Ref.update(todosRef, todos =>
              todos.map(t => (t.id === id ? updated : t))
            )
          );
          yield* _(Effect.sleep('100 millis'));

          return updated;
        }),

      toggle: (id: string) =>
        Effect.gen(function* (_) {
          const todos = yield* _(Ref.get(todosRef));
          const todo = todos.find(t => t.id === id);

          if (!todo) {
            return yield* _(Effect.fail(new NotFoundError('Todo', id)));
          }

          const updated: Todo = {
            ...todo,
            completed: !todo.completed,
            updatedAt: new Date()
          };

          yield* _(
            Ref.update(todosRef, todos =>
              todos.map(t => (t.id === id ? updated : t))
            )
          );
          yield* _(Effect.sleep('50 millis'));

          return updated;
        }),

      delete: (id: string) =>
        Effect.gen(function* (_) {
          const todos = yield* _(Ref.get(todosRef));
          const exists = todos.some(t => t.id === id);

          if (!exists) {
            return yield* _(Effect.fail(new NotFoundError('Todo', id)));
          }

          yield* _(Ref.update(todosRef, todos => todos.filter(t => t.id !== id)));
          yield* _(Effect.sleep('50 millis'));
        })
    });
  })
);
```

### 9.5.3 LoggerLive

**src/layers/LoggerLive.ts:**

```typescript
import { Effect, Layer } from 'effect';
import { Logger } from '@/services/Logger';

export const LoggerLive = Layer.succeed(
  Logger,
  Logger.of({
    debug: (message, data) =>
      Effect.sync(() => {
        console.debug(`[DEBUG] ${message}`, data ?? '');
      }),

    info: (message, data) =>
      Effect.sync(() => {
        console.info(`[INFO] ${message}`, data ?? '');
      }),

    warn: (message, data) =>
      Effect.sync(() => {
        console.warn(`[WARN] ${message}`, data ?? '');
      }),

    error: (message, error) =>
      Effect.sync(() => {
        console.error(`[ERROR] ${message}`, error ?? '');
      })
  })
);
```

### 9.5.4 StorageLive

**src/layers/StorageLive.ts:**

```typescript
import { Effect, Layer } from 'effect';
import { Storage } from '@/services/Storage';
import { NetworkError } from '@/services/errors';

export const StorageLive = Layer.succeed(
  Storage,
  Storage.of({
    get: <T>(key: string) =>
      Effect.try({
        try: () => {
          const item = localStorage.getItem(key);
          if (!item) return null;
          return JSON.parse(item) as T;
        },
        catch: (error) => new NetworkError('Failed to read from storage', error)
      }),

    set: <T>(key: string, value: T) =>
      Effect.try({
        try: () => {
          localStorage.setItem(key, JSON.stringify(value));
        },
        catch: (error) => new NetworkError('Failed to write to storage', error)
      }),

    remove: (key: string) =>
      Effect.try({
        try: () => {
          localStorage.removeItem(key);
        },
        catch: (error) => new NetworkError('Failed to remove from storage', error)
      }),

    clear: () =>
      Effect.try({
        try: () => {
          localStorage.clear();
        },
        catch: (error) => new NetworkError('Failed to clear storage', error)
      })
  })
);
```

### 9.5.5 Combine Layers

**src/layers/index.ts:**

```typescript
import { Layer } from 'effect';
import { TodoApiLive } from './TodoApiLive';
import { TodoApiMock } from './TodoApiMock';
import { LoggerLive } from './LoggerLive';
import { StorageLive } from './StorageLive';

/**
 * Production layer with real implementations
 */
export const AppLayerLive = Layer.mergeAll(
  TodoApiLive,
  LoggerLive,
  StorageLive
);

/**
 * Mock layer for testing/development
 */
export const AppLayerMock = Layer.mergeAll(
  TodoApiMock,
  LoggerLive,
  StorageLive
);

/**
 * Default layer (can switch based on env)
 */
export const AppLayer =
  import.meta.env.MODE === 'test' ? AppLayerMock : AppLayerLive;
```

---

## 9.6 Effects - Business Logic

### 9.6.1 Todo Effects

**src/effects/todos.ts:**

```typescript
import { Effect } from 'effect';
import { TodoApi } from '@/services/TodoApi';
import { Logger } from '@/services/Logger';
import { Storage } from '@/services/Storage';
import type { Todo, CreateTodoRequest, UpdateTodoRequest, TodoStats, TodoFilter } from '@/domain/Todo';
import type { TodoError } from '@/services/errors';
import { ValidationError } from '@/services/errors';

const CACHE_KEY = 'todos_cache';
const CACHE_DURATION_MS = 5 * 60 * 1000; // 5 minutes

/**
 * Fetch all todos with caching
 */
export const fetchAllTodos = Effect.gen(function* (_) {
  const api = yield* _(TodoApi);
  const logger = yield* _(Logger);
  const storage = yield* _(Storage);

  yield* _(logger.info('Fetching todos'));

  // Try cache first
  const cached = yield* _(
    Effect.orElseSucceed(
      storage.get<{ todos: Todo[]; timestamp: number }>(CACHE_KEY),
      () => null
    )
  );

  if (cached && Date.now() - cached.timestamp < CACHE_DURATION_MS) {
    yield* _(logger.debug('Using cached todos', { count: cached.todos.length }));
    return cached.todos;
  }

  // Fetch from API
  const todos = yield* _(api.fetchAll());

  yield* _(logger.info('Fetched todos from API', { count: todos.length }));

  // Update cache
  yield* _(
    Effect.orElseSucceed(
      storage.set(CACHE_KEY, { todos, timestamp: Date.now() }),
      () => undefined
    )
  );

  return todos;
});

/**
 * Get todo by ID
 */
export const getTodoById = (id: string) =>
  Effect.gen(function* (_) {
    const api = yield* _(TodoApi);
    const logger = yield* _(Logger);

    yield* _(logger.debug(`Getting todo ${id}`));

    const todo = yield* _(api.getById(id));

    yield* _(logger.debug(`Got todo ${id}`, todo));

    return todo;
  });

/**
 * Create todo with validation
 */
export const createTodo = (request: CreateTodoRequest) =>
  Effect.gen(function* (_) {
    const api = yield* _(TodoApi);
    const logger = yield* _(Logger);
    const storage = yield* _(Storage);

    // Validate
    const trimmed = request.title.trim();

    if (trimmed.length === 0) {
      return yield* _(
        Effect.fail(new ValidationError('title', 'Title cannot be empty'))
      );
    }

    if (trimmed.length > 200) {
      return yield* _(
        Effect.fail(
          new ValidationError('title', 'Title too long (max 200 characters)')
        )
      );
    }

    yield* _(logger.info(`Creating todo: ${trimmed}`));

    // Create
    const todo = yield* _(api.create({ title: trimmed }));

    yield* _(logger.info(`Created todo ${todo.id}`));

    // Invalidate cache
    yield* _(Effect.orElseSucceed(storage.remove(CACHE_KEY), () => undefined));

    return todo;
  });

/**
 * Update todo
 */
export const updateTodo = (id: string, request: UpdateTodoRequest) =>
  Effect.gen(function* (_) {
    const api = yield* _(TodoApi);
    const logger = yield* _(Logger);
    const storage = yield* _(Storage);

    // Validate title if provided
    if (request.title !== undefined) {
      const trimmed = request.title.trim();

      if (trimmed.length === 0) {
        return yield* _(
          Effect.fail(new ValidationError('title', 'Title cannot be empty'))
        );
      }

      if (trimmed.length > 200) {
        return yield* _(
          Effect.fail(
            new ValidationError('title', 'Title too long (max 200 characters)')
          )
        );
      }
    }

    yield* _(logger.info(`Updating todo ${id}`, request));

    const todo = yield* _(api.update(id, request));

    yield* _(logger.info(`Updated todo ${id}`));

    // Invalidate cache
    yield* _(Effect.orElseSucceed(storage.remove(CACHE_KEY), () => undefined));

    return todo;
  });

/**
 * Toggle todo completion
 */
export const toggleTodo = (id: string) =>
  Effect.gen(function* (_) {
    const api = yield* _(TodoApi);
    const logger = yield* _(Logger);
    const storage = yield* _(Storage);

    yield* _(logger.info(`Toggling todo ${id}`));

    const todo = yield* _(api.toggle(id));

    yield* _(logger.info(`Toggled todo ${id} to ${todo.completed}`));

    // Invalidate cache
    yield* _(Effect.orElseSucceed(storage.remove(CACHE_KEY), () => undefined));

    return todo;
  });

/**
 * Delete todo
 */
export const deleteTodo = (id: string) =>
  Effect.gen(function* (_) {
    const api = yield* _(TodoApi);
    const logger = yield* _(Logger);
    const storage = yield* _(Storage);

    yield* _(logger.info(`Deleting todo ${id}`));

    yield* _(api.delete(id));

    yield* _(logger.info(`Deleted todo ${id}`));

    // Invalidate cache
    yield* _(Effect.orElseSucceed(storage.remove(CACHE_KEY), () => undefined));
  });

/**
 * Filter todos
 */
export const filterTodos = (todos: Todo[], filter: TodoFilter): Todo[] => {
  switch (filter) {
    case 'active':
      return todos.filter(t => !t.completed);
    case 'completed':
      return todos.filter(t => t.completed);
    default:
      return todos;
  }
};

/**
 * Calculate statistics
 */
export const calculateStats = (todos: Todo[]): TodoStats => ({
  total: todos.length,
  active: todos.filter(t => !t.completed).length,
  completed: todos.filter(t => t.completed).length
});

/**
 * Get filtered todos with stats
 */
export const getTodosWithStats = (filter: TodoFilter) =>
  Effect.gen(function* (_) {
    const todos = yield* _(fetchAllTodos);

    const filtered = filterTodos(todos, filter);
    const stats = calculateStats(todos);

    return { todos: filtered, stats };
  });

/**
 * Clear completed todos
 */
export const clearCompleted = Effect.gen(function* (_) {
  const api = yield* _(TodoApi);
  const logger = yield* _(Logger);
  const storage = yield* _(Storage);

  yield* _(logger.info('Clearing completed todos'));

  // Get all todos
  const todos = yield* _(api.fetchAll());

  // Filter completed
  const completed = todos.filter(t => t.completed);

  yield* _(
    logger.info(`Found ${completed.length} completed todos to delete`)
  );

  // Delete all completed in parallel
  yield* _(
    Effect.all(
      completed.map(t => api.delete(t.id)),
      { concurrency: 5 } // Limit concurrent requests
    )
  );

  yield* _(logger.info('Cleared all completed todos'));

  // Invalidate cache
  yield* _(Effect.orElseSucceed(storage.remove(CACHE_KEY), () => undefined));
});
```

---

## 9.7 React Integration

### 9.7.1 useRunEffect Hook

**src/hooks/useRunEffect.ts:**

```typescript
import { useEffect, useState, useRef } from 'react';
import { Effect, Exit } from 'effect';

export interface UseEffectResult<A, E> {
  data: A | null;
  error: E | null;
  loading: boolean;
  refetch: () => void;
}

/**
 * Hook to run Effect in React
 */
export function useRunEffect<A, E>(
  createEffect: () => Effect.Effect<A, E, never>,
  deps: React.DependencyList = []
): UseEffectResult<A, E> {
  const [data, setData] = useState<A | null>(null);
  const [error, setError] = useState<E | null>(null);
  const [loading, setLoading] = useState(true);
  const [refetchCount, setRefetchCount] = useState(0);

  // Keep track of latest createEffect to avoid stale closures
  const effectRef = useRef(createEffect);
  effectRef.current = createEffect;

  useEffect(() => {
    let cancelled = false;

    setLoading(true);
    setError(null);

    const effect = effectRef.current();

    Effect.runPromiseExit(effect).then(exit => {
      if (cancelled) return;

      if (Exit.isSuccess(exit)) {
        setData(exit.value);
        setError(null);
      } else {
        setError(exit.cause.failureOrCause as E);
        setData(null);
      }

      setLoading(false);
    });

    return () => {
      cancelled = true;
    };
  }, [...deps, refetchCount]);

  const refetch = () => {
    setRefetchCount(c => c + 1);
  };

  return { data, error, loading, refetch };
}
```

### 9.7.2 useTodos Hook

**src/hooks/useTodos.ts:**

```typescript
import { useCallback } from 'react';
import { Effect } from 'effect';
import type { TodoFilter, CreateTodoRequest, UpdateTodoRequest } from '@/domain/Todo';
import type { TodoError } from '@/services/errors';
import {
  getTodosWithStats,
  createTodo,
  updateTodo,
  toggleTodo,
  deleteTodo,
  clearCompleted
} from '@/effects/todos';
import { AppLayer } from '@/layers';
import { useRunEffect } from './useRunEffect';

export function useTodos(filter: TodoFilter = 'all') {
  // Fetch todos with stats
  const { data, error, loading, refetch } = useRunEffect(
    () => getTodosWithStats(filter).pipe(Effect.provide(AppLayer)),
    [filter]
  );

  // Create todo
  const handleCreate = useCallback(
    async (request: CreateTodoRequest) => {
      const effect = createTodo(request).pipe(Effect.provide(AppLayer));

      try {
        await Effect.runPromise(effect);
        refetch();
      } catch (err) {
        throw err;
      }
    },
    [refetch]
  );

  // Update todo
  const handleUpdate = useCallback(
    async (id: string, request: UpdateTodoRequest) => {
      const effect = updateTodo(id, request).pipe(Effect.provide(AppLayer));

      try {
        await Effect.runPromise(effect);
        refetch();
      } catch (err) {
        throw err;
      }
    },
    [refetch]
  );

  // Toggle todo
  const handleToggle = useCallback(
    async (id: string) => {
      const effect = toggleTodo(id).pipe(Effect.provide(AppLayer));

      try {
        await Effect.runPromise(effect);
        refetch();
      } catch (err) {
        throw err;
      }
    },
    [refetch]
  );

  // Delete todo
  const handleDelete = useCallback(
    async (id: string) => {
      const effect = deleteTodo(id).pipe(Effect.provide(AppLayer));

      try {
        await Effect.runPromise(effect);
        refetch();
      } catch (err) {
        throw err;
      }
    },
    [refetch]
  );

  // Clear completed
  const handleClearCompleted = useCallback(async () => {
    const effect = clearCompleted.pipe(Effect.provide(AppLayer));

    try {
      await Effect.runPromise(effect);
      refetch();
    } catch (err) {
      throw err;
    }
  }, [refetch]);

  return {
    todos: data?.todos ?? [],
    stats: data?.stats ?? { total: 0, active: 0, completed: 0 },
    error: error as TodoError | null,
    loading,
    refetch,
    createTodo: handleCreate,
    updateTodo: handleUpdate,
    toggleTodo: handleToggle,
    deleteTodo: handleDelete,
    clearCompleted: handleClearCompleted
  };
}
```

---

## 9.8 Components

### 9.8.1 TodoList Component

**src/components/TodoList.tsx:**

```typescript
import React from 'react';
import type { Todo } from '@/domain/Todo';
import { TodoItem } from './TodoItem';

interface TodoListProps {
  todos: Todo[];
  onToggle: (id: string) => Promise<void>;
  onDelete: (id: string) => Promise<void>;
  onUpdate: (id: string, title: string) => Promise<void>;
}

export function TodoList({
  todos,
  onToggle,
  onDelete,
  onUpdate
}: TodoListProps) {
  if (todos.length === 0) {
    return (
      <div className="empty-state">
        <p>No todos yet. Add one above!</p>
      </div>
    );
  }

  return (
    <ul className="todo-list">
      {todos.map(todo => (
        <TodoItem
          key={todo.id}
          todo={todo}
          onToggle={onToggle}
          onDelete={onDelete}
          onUpdate={onUpdate}
        />
      ))}
    </ul>
  );
}
```

### 9.8.2 TodoItem Component

**src/components/TodoItem.tsx:**

```typescript
import React, { useState } from 'react';
import clsx from 'clsx';
import type { Todo } from '@/domain/Todo';

interface TodoItemProps {
  todo: Todo;
  onToggle: (id: string) => Promise<void>;
  onDelete: (id: string) => Promise<void>;
  onUpdate: (id: string, title: string) => Promise<void>;
}

export function TodoItem({ todo, onToggle, onDelete, onUpdate }: TodoItemProps) {
  const [editing, setEditing] = useState(false);
  const [editText, setEditText] = useState(todo.title);
  const [loading, setLoading] = useState(false);

  const handleToggle = async () => {
    setLoading(true);
    try {
      await onToggle(todo.id);
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = async () => {
    setLoading(true);
    try {
      await onDelete(todo.id);
    } finally {
      setLoading(false);
    }
  };

  const handleEdit = () => {
    setEditing(true);
    setEditText(todo.title);
  };

  const handleSave = async () => {
    if (editText.trim() === '') return;

    setLoading(true);
    try {
      await onUpdate(todo.id, editText.trim());
      setEditing(false);
    } finally {
      setLoading(false);
    }
  };

  const handleCancel = () => {
    setEditing(false);
    setEditText(todo.title);
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      handleSave();
    } else if (e.key === 'Escape') {
      handleCancel();
    }
  };

  return (
    <li className={clsx('todo-item', { completed: todo.completed, loading })}>
      <div className="todo-item-content">
        <input
          type="checkbox"
          checked={todo.completed}
          onChange={handleToggle}
          disabled={loading}
        />

        {editing ? (
          <input
            type="text"
            className="todo-item-edit"
            value={editText}
            onChange={e => setEditText(e.target.value)}
            onKeyDown={handleKeyDown}
            onBlur={handleSave}
            autoFocus
            disabled={loading}
          />
        ) : (
          <span
            className="todo-item-title"
            onDoubleClick={handleEdit}
          >
            {todo.title}
          </span>
        )}
      </div>

      <div className="todo-item-actions">
        {editing ? (
          <>
            <button onClick={handleSave} disabled={loading}>
              Save
            </button>
            <button onClick={handleCancel} disabled={loading}>
              Cancel
            </button>
          </>
        ) : (
          <>
            <button onClick={handleEdit} disabled={loading}>
              Edit
            </button>
            <button onClick={handleDelete} disabled={loading}>
              Delete
            </button>
          </>
        )}
      </div>
    </li>
  );
}
```

### 9.8.3 AddTodoForm Component

**src/components/AddTodoForm.tsx:**

```typescript
import React, { useState } from 'react';
import type { CreateTodoRequest } from '@/domain/Todo';
import type { TodoError } from '@/services/errors';

interface AddTodoFormProps {
  onAdd: (request: CreateTodoRequest) => Promise<void>;
}

export function AddTodoForm({ onAdd }: AddTodoFormProps) {
  const [title, setTitle] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (title.trim() === '') {
      setError('Title cannot be empty');
      return;
    }

    setLoading(true);
    setError(null);

    try {
      await onAdd({ title: title.trim() });
      setTitle('');
    } catch (err) {
      const todoError = err as TodoError;
      setError(todoError.message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <form className="add-todo-form" onSubmit={handleSubmit}>
      <input
        type="text"
        className="add-todo-input"
        placeholder="What needs to be done?"
        value={title}
        onChange={e => setTitle(e.target.value)}
        disabled={loading}
      />

      <button
        type="submit"
        className="add-todo-button"
        disabled={loading || title.trim() === ''}
      >
        {loading ? 'Adding...' : 'Add'}
      </button>

      {error && <div className="error-message">{error}</div>}
    </form>
  );
}
```

### 9.8.4 TodoFilters Component

**src/components/TodoFilters.tsx:**

```typescript
import React from 'react';
import clsx from 'clsx';
import type { TodoFilter, TodoStats } from '@/domain/Todo';

interface TodoFiltersProps {
  currentFilter: TodoFilter;
  stats: TodoStats;
  onFilterChange: (filter: TodoFilter) => void;
  onClearCompleted: () => Promise<void>;
}

export function TodoFilters({
  currentFilter,
  stats,
  onFilterChange,
  onClearCompleted
}: TodoFiltersProps) {
  const [clearing, setClearing] = React.useState(false);

  const handleClearCompleted = async () => {
    setClearing(true);
    try {
      await onClearCompleted();
    } finally {
      setClearing(false);
    }
  };

  return (
    <div className="todo-filters">
      <div className="filter-stats">
        <span className="stats-item">
          Total: <strong>{stats.total}</strong>
        </span>
        <span className="stats-item">
          Active: <strong>{stats.active}</strong>
        </span>
        <span className="stats-item">
          Completed: <strong>{stats.completed}</strong>
        </span>
      </div>

      <div className="filter-buttons">
        <button
          className={clsx('filter-button', { active: currentFilter === 'all' })}
          onClick={() => onFilterChange('all')}
        >
          All
        </button>

        <button
          className={clsx('filter-button', { active: currentFilter === 'active' })}
          onClick={() => onFilterChange('active')}
        >
          Active
        </button>

        <button
          className={clsx('filter-button', {
            active: currentFilter === 'completed'
          })}
          onClick={() => onFilterChange('completed')}
        >
          Completed
        </button>
      </div>

      {stats.completed > 0 && (
        <button
          className="clear-completed-button"
          onClick={handleClearCompleted}
          disabled={clearing}
        >
          {clearing ? 'Clearing...' : `Clear Completed (${stats.completed})`}
        </button>
      )}
    </div>
  );
}
```

### 9.8.5 App Component

**src/App.tsx:**

```typescript
import React, { useState } from 'react';
import type { TodoFilter } from '@/domain/Todo';
import { useTodos } from '@/hooks/useTodos';
import { AddTodoForm } from '@/components/AddTodoForm';
import { TodoList } from '@/components/TodoList';
import { TodoFilters } from '@/components/TodoFilters';
import './App.css';

function App() {
  const [filter, setFilter] = useState<TodoFilter>('all');

  const {
    todos,
    stats,
    loading,
    error,
    createTodo,
    updateTodo,
    toggleTodo,
    deleteTodo,
    clearCompleted
  } = useTodos(filter);

  const handleUpdate = async (id: string, title: string) => {
    await updateTodo(id, { title });
  };

  return (
    <div className="app">
      <header className="app-header">
        <h1>Todo App</h1>
        <p>Built with Effect-TS</p>
      </header>

      <main className="app-main">
        <AddTodoForm onAdd={createTodo} />

        <TodoFilters
          currentFilter={filter}
          stats={stats}
          onFilterChange={setFilter}
          onClearCompleted={clearCompleted}
        />

        {loading && <div className="loading">Loading todos...</div>}

        {error && (
          <div className="error">
            <strong>Error:</strong> {error.message}
          </div>
        )}

        {!loading && !error && (
          <TodoList
            todos={todos}
            onToggle={toggleTodo}
            onDelete={deleteTodo}
            onUpdate={handleUpdate}
          />
        )}
      </main>
    </div>
  );
}

export default App;
```

---

## 9.9 Testing

### 9.9.1 Testing Effects with Mock Layer

**src/effects/__tests__/todos.test.ts:**

```typescript
import { describe, it, expect } from 'vitest';
import { Effect } from 'effect';
import { AppLayerMock } from '@/layers';
import { fetchAllTodos, createTodo, toggleTodo } from '../todos';
import { ValidationError } from '@/services/errors';

describe('Todo Effects', () => {
  describe('fetchAllTodos', () => {
    it('should fetch todos successfully', async () => {
      const program = fetchAllTodos.pipe(Effect.provide(AppLayerMock));

      const todos = await Effect.runPromise(program);

      expect(todos).toHaveLength(3);
      expect(todos[0].title).toBe('Learn Effect-TS');
    });
  });

  describe('createTodo', () => {
    it('should create todo with valid title', async () => {
      const program = createTodo({ title: 'New Todo' }).pipe(
        Effect.provide(AppLayerMock)
      );

      const todo = await Effect.runPromise(program);

      expect(todo.title).toBe('New Todo');
      expect(todo.completed).toBe(false);
    });

    it('should reject empty title', async () => {
      const program = createTodo({ title: '' }).pipe(
        Effect.provide(AppLayerMock)
      );

      await expect(Effect.runPromise(program)).rejects.toMatchObject({
        _tag: 'ValidationError',
        field: 'title'
      });
    });

    it('should reject long title', async () => {
      const longTitle = 'x'.repeat(201);
      const program = createTodo({ title: longTitle }).pipe(
        Effect.provide(AppLayerMock)
      );

      await expect(Effect.runPromise(program)).rejects.toMatchObject({
        _tag: 'ValidationError',
        field: 'title'
      });
    });
  });

  describe('toggleTodo', () => {
    it('should toggle todo completion', async () => {
      const program = Effect.gen(function* (_) {
        // Get initial todo
        const todos = yield* _(fetchAllTodos);
        const todo = todos[0];
        expect(todo.completed).toBe(false);

        // Toggle it
        const toggled = yield* _(toggleTodo(todo.id));
        expect(toggled.completed).toBe(true);

        // Toggle back
        const toggledBack = yield* _(toggleTodo(todo.id));
        expect(toggledBack.completed).toBe(false);
      }).pipe(Effect.provide(AppLayerMock));

      await Effect.runPromise(program);
    });
  });
});
```

### 9.9.2 Testing React Components

**src/components/__tests__/TodoList.test.tsx:**

```typescript
import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { TodoList } from '../TodoList';
import type { Todo } from '@/domain/Todo';

describe('TodoList', () => {
  const mockTodos: Todo[] = [
    {
      id: '1',
      title: 'Test Todo 1',
      completed: false,
      createdAt: new Date()
    },
    {
      id: '2',
      title: 'Test Todo 2',
      completed: true,
      createdAt: new Date()
    }
  ];

  it('should render todos', () => {
    render(
      <TodoList
        todos={mockTodos}
        onToggle={vi.fn()}
        onDelete={vi.fn()}
        onUpdate={vi.fn()}
      />
    );

    expect(screen.getByText('Test Todo 1')).toBeInTheDocument();
    expect(screen.getByText('Test Todo 2')).toBeInTheDocument();
  });

  it('should show empty state', () => {
    render(
      <TodoList
        todos={[]}
        onToggle={vi.fn()}
        onDelete={vi.fn()}
        onUpdate={vi.fn()}
      />
    );

    expect(screen.getByText(/no todos yet/i)).toBeInTheDocument();
  });
});
```

---

## 9.10 Best Practices

### 9.10.1 Service Design

**DO:**
- ✅ แยก services ตาม responsibility (TodoApi, Logger, Storage)
- ✅ ใช้ discriminated unions สำหรับ errors
- ✅ Return type ชัดเจน `Effect<A, E, R>`

**DON'T:**
- ❌ รวม services ทั้งหมดเป็น service เดียว
- ❌ ใช้ generic `Error` type
- ❌ Throw exceptions ใน service implementations

### 9.10.2 Effect Composition

**DO:**
- ✅ ใช้ `Effect.gen` สำหรับ sequential operations
- ✅ ใช้ `Effect.all` สำหรับ parallel operations
- ✅ Handle errors ที่ level ที่เหมาะสม

**DON'T:**
- ❌ Nest `Effect.flatMap` ลึกเกินไป (ใช้ `Effect.gen` แทน)
- ❌ Run effects sequentially เมื่อสามารถทำ parallel ได้
- ❌ Catch errors เร็วเกินไป (ทำให้ lost context)

### 9.10.3 React Integration

**DO:**
- ✅ ใช้ custom hooks (`useRunEffect`, `useTodos`)
- ✅ Handle loading และ error states
- ✅ Memoize effects ด้วย `useMemo` หรือ `useCallback`

**DON'T:**
- ❌ Run effects ใน render function
- ❌ Create new effects ทุกครั้งที่ render
- ❌ Forget cleanup ใน `useEffect`

### 9.10.4 Performance

**DO:**
- ✅ Cache data เมื่อเหมาะสม (ดูใน `fetchAllTodos`)
- ✅ ใช้ `Effect.all` with `concurrency` limit
- ✅ Optimistic updates สำหรับ better UX

**DON'T:**
- ❌ Fetch ข้อมูลซ้ำโดยไม่จำเป็น
- ❌ Run unlimited concurrent requests
- ❌ Ignore loading states

---

## 9.11 สรุป

### สิ่งที่ได้เรียนรู้ในบทนี้

1. **Frontend Architecture with Effect-TS**
   - Services Layer - Interface definitions
   - Layers - Live และ Mock implementations
   - Effects - Business logic
   - Components - React integration

2. **Service Pattern**
   - Define interface with Context.Tag
   - Implement with Layer.succeed/Layer.effect
   - Provide with Effect.provide(AppLayer)
   - Test with mock layers

3. **Effect Composition**
   - Sequential with Effect.gen
   - Parallel with Effect.all
   - Error handling with type-safe errors
   - Caching and optimization

4. **React Integration**
   - Custom hooks for running effects
   - Loading and error states
   - Refetch mechanism
   - TypeScript type safety throughout

5. **Testing Strategy**
   - Unit test effects with mock layers
   - Integration test with real layers
   - Component testing with React Testing Library

### ข้อดีของ Architecture นี้

1. **Type Safety** - Compiler จับ bugs ก่อน runtime
2. **Testability** - Mock dependencies ได้ง่าย
3. **Maintainability** - โครงสร้างชัดเจน แยก concerns
4. **Composability** - ต่อ effects ได้สวยงาม
5. **Error Handling** - Errors เป็น values, จัดการได้เป็นระบบ

### บทถัดไป

ในบทที่ 10 เราจะเรียนรู้:
- **Advanced React Integration** - Context providers, custom hooks
- **State Management** - Global state with Effect
- **Real-time Updates** - WebSocket, Server-Sent Events
- **Form Validation** - Complex forms with Effect-TS

---

## แบบฝึกหัดท้ายบท

### ข้อ 1: Add UserApi Service

เพิ่ม `UserApi` service ที่มี methods:
- `getCurrentUser(): Effect<User, ApiError, never>`
- `updateProfile(updates: ProfileUpdate): Effect<User, ApiError, never>`

### ข้อ 2: Implement Caching Strategy

ปรับปรุง `fetchAllTodos` ให้มี:
- Cache TTL (Time-To-Live)
- Force refresh option
- Cache invalidation

### ข้อ 3: Add Optimistic Updates

เพิ่ม optimistic updates สำหรับ:
- Toggle todo (UI update ทันที, revert ถ้า error)
- Delete todo (hide ทันที, restore ถ้า error)

### ข้อ 4: Error Recovery

เพิ่ม retry logic สำหรับ API calls:
- Retry 3 times with exponential backoff
- Show retry counter in UI

### ข้อ5: Offline Support

เพิ่ม offline support:
- Queue operations เมื่อ offline
- Sync เมื่อกลับมา online
- Show offline indicator

---

**พร้อมที่จะสร้าง Production-Ready Frontend Architecture แล้วใช่ไหม?**
