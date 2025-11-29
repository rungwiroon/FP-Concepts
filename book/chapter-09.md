# บทที่ 9: สร้าง Frontend Architecture

**📦 Validated with Effect-TS 3.18.4 + TypeScript 5.9.3**

> 💡 **สำคัญ**: บทนี้ใช้ Effect-TS 3.x API ซึ่งมีการเปลี่ยนแปลงจาก 2.x:
> - ✅ `Effect.gen(function* () {})` - ไม่ต้องส่ง `_` parameter
> - ✅ `yield* service` - เข้าถึง service โดยตรง ไม่ต้องใช้ `yield* _()`
> - ✅ `Effect.provide(effect, layer)` - data-first API
> - ✅ `Ref.make()` - สร้าง mutable state แบบ functional
>
> หากคุณใช้ Effect-TS 2.x โปรดอัปเดตเป็น 3.x ก่อนทำตามบทนี้

> สร้าง Frontend แบบ Type-Safe, Testable และ Maintainable ด้วย Effect-TS

---

## 📚 Prerequisites - สิ่งที่ต้องรู้ก่อน

บทนี้สมมติว่าคุณ**เข้าใจแนวคิดพื้นฐาน**จากบทที่ 8 แล้ว:

✅ **Required Knowledge:**
- Effect<A, E, R> type และความหมายของแต่ละตัว
- Effect.gen syntax (Effect 3.x - ไม่มี `_` parameter)
- Context.Tag และ Layer pattern สำหรับ dependency injection
- การ run effects ด้วย Effect.runPromise
- React integration basics (useEffect, useState)

❓ **ถ้ายังไม่แน่ใจ:**
→ กลับไปอ่าน **[บทที่ 8: Effect-TS Fundamentals for Frontend](./chapter-08.md)** ก่อน

---

## เนื้อหาในบทนี้

- ภาพรวม Frontend Architecture
- Project Setup with React + Vite + Effect-TS
- Services Layer - Service Interfaces
- Layers - Live & Mock Implementations
- Effects - Business Logic
- React Integration - Custom Hooks
- Immer สำหรับ Complex State Updates
- Advanced Patterns
- State Management with Effect
- Error Handling & Loading States
- Testing Strategy
- Best Practices

---

## 9.1 ภาพรวม Frontend Architecture

> 🎯 **สิ่งที่เราจะสร้างในบทนี้**
>
> ในบทที่ 8 เราได้สร้าง **Simple Todo App** ที่มี:
> - ✅ แสดงรายการ todos (fetchTodos)
> - ✅ เพิ่ม todo ใหม่ (createTodo)
>
> ในบทนี้ เราจะ**ขยายเป็น Production-Ready App** ที่มี:
>
> **🔧 Full CRUD Operations:**
> - ✅ Create (เพิ่ม todo)
> - ✅ Read (แสดงรายการ + อ่านรายการเดียว)
> - ✅ Update (แก้ไข todo - inline editing)
> - ✅ Delete (ลบ todo)
> - ✅ Toggle (เปลี่ยนสถานะ completed)
>
> **⚡ Advanced Features:**
> - ✅ Filters (All / Active / Completed)
> - ✅ Statistics (นับจำนวน active/completed)
> - ✅ Clear Completed (ลบที่เสร็จแล้วทั้งหมด)
> - ✅ Inline Editing (double-click เพื่อแก้ไข)
> - ✅ Keyboard Shortcuts (Enter/Escape)
>
> **🏗️ Production Architecture:**
> - ✅ Domain Layer (types และ interfaces)
> - ✅ Services Layer (TodoApi, Logger, Storage)
> - ✅ Effects Layer (business logic with caching)
> - ✅ Components Layer (presentation components)
> - ✅ Comprehensive Error Handling
> - ✅ Caching Strategies with TTL
> - ✅ Testing Strategy

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
// Domain entity (matches backend schema)
export interface Todo {
  readonly id: number;              // Backend uses number, not string
  readonly title: string;
  readonly description?: string;     // Optional description field
  readonly isCompleted: boolean;     // Backend uses isCompleted, not completed
  readonly createdAt: Date;
  readonly completedAt?: Date | null; // Backend uses completedAt, not updatedAt
}

// Create Todo Request
export interface CreateTodoRequest {
  readonly title: string;
}

// Update Todo Request
export interface UpdateTodoRequest {
  readonly title?: string;
  readonly description?: string;
  readonly isCompleted?: boolean;   // Use isCompleted to match backend
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
    id: 1,
    title: 'Learn Effect-TS',
    description: '',
    isCompleted: false,
    createdAt: new Date('2024-01-01'),
    completedAt: null
  },
  {
    id: 2,
    title: 'Build Todo App',
    description: '',
    isCompleted: false,
    createdAt: new Date('2024-01-02'),
    completedAt: null
  },
  {
    id: 3,
    title: 'Write Tests',
    description: '',
    isCompleted: true,
    createdAt: new Date('2024-01-03'),
    completedAt: new Date('2024-01-03')
  }
];

/**
 * Mock implementation with in-memory state
 */
export const TodoApiMock = Layer.effect(
  TodoApi,
  Effect.gen(function* () {
    // Create mutable state
    const todosRef = yield* Ref.make(createMockData());
    let nextId = 4;

    return TodoApi.of({
      fetchAll: () =>
        Effect.gen(function* () {
          const todos = yield* Ref.get(todosRef);
          // Simulate network delay
          yield* Effect.sleep('100 millis');
          return todos;
        }),

      getById: (id: string) =>
        Effect.gen(function* () {
          const todos = yield* Ref.get(todosRef);
          const todo = todos.find(t => t.id === Number(id));

          yield* Effect.sleep('50 millis');

          if (!todo) {
            return yield* Effect.fail(new NotFoundError('Todo', id));
          }

          return todo;
        }),

      create: (request: CreateTodoRequest) =>
        Effect.gen(function* () {
          const newTodo: Todo = {
            id: nextId++,
            title: request.title,
            description: '',
            isCompleted: false,
            createdAt: new Date(),
            completedAt: null
          };

          yield* Ref.update(todosRef, todos => [...todos, newTodo]);
          yield* Effect.sleep('100 millis');

          return newTodo;
        }),

      update: (id: string, request: UpdateTodoRequest) =>
        Effect.gen(function* () {
          const todos = yield* Ref.get(todosRef);
          const numId = Number(id);
          const todo = todos.find(t => t.id === numId);

          if (!todo) {
            return yield* Effect.fail(new NotFoundError('Todo', id));
          }

          const updated: Todo = {
            ...todo,
            ...request,
            createdAt: todo.createdAt
          };

          yield* Ref.update(todosRef, todos =>
            todos.map(t => (t.id === numId ? updated : t))
          );
          yield* Effect.sleep('100 millis');

          return updated;
        }),

      toggle: (id: string) =>
        Effect.gen(function* () {
          const todos = yield* Ref.get(todosRef);
          const numId = Number(id);
          const todo = todos.find(t => t.id === numId);

          if (!todo) {
            return yield* Effect.fail(new NotFoundError('Todo', id));
          }

          const updated: Todo = {
            ...todo,
            isCompleted: !todo.isCompleted,
            completedAt: !todo.isCompleted ? new Date() : null
          };

          yield* Ref.update(todosRef, todos =>
            todos.map(t => (t.id === numId ? updated : t))
          );
          yield* Effect.sleep('50 millis');

          return updated;
        }),

      delete: (id: string) =>
        Effect.gen(function* () {
          const todos = yield* Ref.get(todosRef);
          const numId = Number(id);
          const exists = todos.some(t => t.id === numId);

          if (!exists) {
            return yield* Effect.fail(new NotFoundError('Todo', id));
          }

          yield* Ref.update(todosRef, todos => todos.filter(t => t.id !== numId));
          yield* Effect.sleep('50 millis');
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
export const fetchAllTodos = Effect.gen(function* () {
  const api = yield* TodoApi;
  const logger = yield* Logger;
  const storage = yield* Storage;

  yield* logger.info('Fetching todos');

  // Try cache first
  const cached = yield* Effect.orElseSucceed(
    storage.get<{ todos: Todo[]; timestamp: number }>(CACHE_KEY),
    () => null
  );

  if (cached && Date.now() - cached.timestamp < CACHE_DURATION_MS) {
    yield* logger.debug('Using cached todos', { count: cached.todos.length });
    return cached.todos;
  }

  // Fetch from API
  const todos = yield* api.fetchAll();

  yield* logger.info('Fetched todos from API', { count: todos.length });

  // Update cache
  yield* Effect.orElseSucceed(
    storage.set(CACHE_KEY, { todos, timestamp: Date.now() }),
    () => undefined
  );

  return todos;
});

/**
 * Get todo by ID
 */
export const getTodoById = (id: string) =>
  Effect.gen(function* () {
    const api = yield* TodoApi;
    const logger = yield* Logger;

    yield* logger.debug(`Getting todo ${id}`);

    const todo = yield* api.getById(id);

    yield* logger.debug(`Got todo ${id}`, todo);

    return todo;
  });

/**
 * Create todo with validation
 */
export const createTodo = (request: CreateTodoRequest) =>
  Effect.gen(function* () {
    const api = yield* TodoApi;
    const logger = yield* Logger;
    const storage = yield* Storage;

    // Validate
    const trimmed = request.title.trim();

    if (trimmed.length === 0) {
      return yield* Effect.fail(
        new ValidationError('title', 'Title cannot be empty')
      );
    }

    if (trimmed.length > 200) {
      return yield* Effect.fail(
        new ValidationError('title', 'Title too long (max 200 characters)')
      );
    }

    yield* logger.info(`Creating todo: ${trimmed}`);

    // Create
    const todo = yield* api.create({ title: trimmed });

    yield* logger.info(`Created todo ${todo.id}`);

    // Invalidate cache
    yield* Effect.orElseSucceed(storage.remove(CACHE_KEY), () => undefined);

    return todo;
  });

/**
 * Update todo
 */
export const updateTodo = (id: string, request: UpdateTodoRequest) =>
  Effect.gen(function* () {
    const api = yield* TodoApi;
    const logger = yield* Logger;
    const storage = yield* Storage;

    // Validate title if provided
    if (request.title !== undefined) {
      const trimmed = request.title.trim();

      if (trimmed.length === 0) {
        return yield* Effect.fail(
          new ValidationError('title', 'Title cannot be empty')
        );
      }

      if (trimmed.length > 200) {
        return yield* Effect.fail(
          new ValidationError('title', 'Title too long (max 200 characters)')
        );
      }
    }

    yield* logger.info(`Updating todo ${id}`, request);

    const todo = yield* api.update(id, request);

    yield* logger.info(`Updated todo ${id}`);

    // Invalidate cache
    yield* Effect.orElseSucceed(storage.remove(CACHE_KEY), () => undefined);

    return todo;
  });

/**
 * Toggle todo completion
 */
export const toggleTodo = (id: string) =>
  Effect.gen(function* () {
    const api = yield* TodoApi;
    const logger = yield* Logger;
    const storage = yield* Storage;

    yield* logger.info(`Toggling todo ${id}`);

    const todo = yield* api.toggle(id);

    yield* logger.info(`Toggled todo ${id} to ${todo.isCompleted}`);

    // Invalidate cache
    yield* Effect.orElseSucceed(storage.remove(CACHE_KEY), () => undefined);

    return todo;
  });

/**
 * Delete todo
 */
export const deleteTodo = (id: string) =>
  Effect.gen(function* () {
    const api = yield* TodoApi;
    const logger = yield* Logger;
    const storage = yield* Storage;

    yield* logger.info(`Deleting todo ${id}`);

    yield* api.delete(id);

    yield* logger.info(`Deleted todo ${id}`);

    // Invalidate cache
    yield* Effect.orElseSucceed(storage.remove(CACHE_KEY), () => undefined);
  });

/**
 * Filter todos
 */
export const filterTodos = (todos: Todo[], filter: TodoFilter): Todo[] => {
  switch (filter) {
    case 'active':
      return todos.filter(t => !t.isCompleted);
    case 'completed':
      return todos.filter(t => t.isCompleted);
    default:
      return todos;
  }
};

/**
 * Calculate statistics
 */
export const calculateStats = (todos: Todo[]): TodoStats => ({
  total: todos.length,
  active: todos.filter(t => !t.isCompleted).length,
  completed: todos.filter(t => t.isCompleted).length
});

/**
 * Get filtered todos with stats
 */
export const getTodosWithStats = (filter: TodoFilter) =>
  Effect.gen(function* () {
    const todos = yield* fetchAllTodos;

    const filtered = filterTodos(todos, filter);
    const stats = calculateStats(todos);

    return { todos: filtered, stats };
  });

/**
 * Clear completed todos
 */
export const clearCompleted = Effect.gen(function* () {
  const api = yield* TodoApi;
  const logger = yield* Logger;
  const storage = yield* Storage;

  yield* logger.info('Clearing completed todos');

  // Get all todos
  const todos = yield* api.fetchAll();

  // Filter completed
  const completed = todos.filter(t => t.isCompleted);

  yield* logger.info(`Found ${completed.length} completed todos to delete`);

  // Delete all completed in parallel
  yield* Effect.all(
    completed.map(t => api.delete(t.id)),
    { concurrency: 5 } // Limit concurrent requests
  );

  yield* logger.info('Cleared all completed todos');

  // Invalidate cache
  yield* Effect.orElseSucceed(storage.remove(CACHE_KEY), () => undefined);
});
```

---

## 9.7 Advanced Patterns

> 💡 **หมายเหตุ:** Patterns เหล่านี้ขยายจากแนวคิดพื้นฐานในบทที่ 8

ในบทที่ 8 เราได้เรียนรู้ Effect basics แล้ว ตอนนี้เรามาดู advanced patterns ที่ใช้ใน production กัน

### 9.7.1 Retry with Schedule

Effect-TS มี **Schedule API** ที่ powerful สำหรับการ retry:

```typescript
import { Effect, Schedule } from 'effect';

// Retry 3 times with exponential backoff
const fetchWithRetry = fetchAllTodos.pipe(
  Effect.retry(
    Schedule.exponential('100 millis').pipe(
      Schedule.compose(Schedule.recurs(3))
    )
  )
);

// Custom retry logic - retry only on network errors
const retryOnNetworkError = fetchAllTodos.pipe(
  Effect.retry({
    while: (error) => error._tag === 'NetworkError',
    times: 3,
    schedule: Schedule.exponential('200 millis')
  })
);

// Retry with timeout per attempt
const fetchWithRetryAndTimeout = fetchAllTodos.pipe(
  Effect.timeout('5 seconds'),
  Effect.retry(Schedule.recurs(3))
);
```

**Use Case จริง:**
```typescript
// Retry API call with increasing delays
export const fetchTodosWithRetry = fetchAllTodos.pipe(
  Effect.retry(
    Schedule.exponential('100 millis', 2).pipe( // 100ms, 200ms, 400ms...
      Schedule.compose(Schedule.recurs(3)),      // max 3 retries
      Schedule.jittered                          // add randomness
    )
  ),
  Effect.tapError((error) =>
    Effect.gen(function* () {
      const logger = yield* Logger;
      yield* logger.error('Failed after retries', error);
    })
  )
);
```

### 9.7.2 Timeout Handling

```typescript
import { Effect, Duration } from 'effect';

// Simple timeout
const fetchWithTimeout = fetchAllTodos.pipe(
  Effect.timeout(Duration.seconds(5))
);

// Timeout with fallback value
const fetchWithFallback = fetchAllTodos.pipe(
  Effect.timeout(Duration.seconds(5)),
  Effect.catchTag('TimeoutException', () =>
    Effect.succeed([]) // Return empty array on timeout
  )
);

// Timeout with cache fallback
const fetchWithCacheFallback = Effect.gen(function* () {
  const storage = yield* Storage;

  return yield* Effect.timeout(
    fetchAllTodos,
    Duration.seconds(3)
  ).pipe(
    // Fallback to cache on timeout
    Effect.catchAll(() =>
      Effect.gen(function* () {
        const cached = yield* storage.get<Todo[]>('todos_backup');
        return cached ?? [] as Todo[];
      })
    )
  );
});
```

### 9.7.3 Caching Strategies

เราใช้ caching ใน `fetchAllTodos` แล้ว (Section 9.6) แต่นี่คือ patterns เพิ่มเติม:

```typescript
// 1. Cache with TTL (Time-To-Live) - เรามีอยู่แล้ว
const CACHE_TTL = 5 * 60 * 1000; // 5 minutes

// 2. Conditional caching - cache เฉพาะ success (Functional Style)
const cachedFetch = Effect.gen(function* () {
  const api = yield* TodoApi;
  const storage = yield* Storage;
  const cached = yield* storage.get<Todo[]>('todos');

  const fetchAndCache = Effect.gen(function* () {
    const fresh = yield* api.fetchAll();
    yield* storage.set('todos', fresh);
    return fresh;
  });

  // Use cached if valid, otherwise fetch fresh
  return yield* (cached && isValid(cached)
    ? Effect.succeed(cached)
    : fetchAndCache
  );
});

// 3. Cache invalidation strategies
const invalidateCache = (key: string) =>
  Effect.gen(function* () {
    const storage = yield* Storage;
    yield* storage.remove(key);
  });

// 4. Cache warming (pre-populate cache)
const warmCache = Effect.gen(function* () {
  const storage = yield* Storage;
  const todos = yield* api.fetchAll();

  yield* storage.set('todos_cache', {
    data: todos,
    timestamp: Date.now()
  });

  return todos;
});

// 5. Stale-While-Revalidate pattern - Functional Style
const fetchWithSWR = Effect.gen(function* () {
  const api = yield* TodoApi;
  const storage = yield* Storage;
  const cached = yield* storage.get<{ data: Todo[]; timestamp: number }>('todos');

  const fetchAndCache = Effect.gen(function* () {
    const fresh = yield* api.fetchAll();
    yield* storage.set('todos', { data: fresh, timestamp: Date.now() });
    return fresh;
  });

  // Use Option to handle nullable cached value
  return yield* (cached
    ? Effect.succeed(cached).pipe(
        Effect.tap((cache) =>
          // Revalidate in background if stale
          Effect.when(
            fetchAndCache.pipe(Effect.fork),
            () => Date.now() - cache.timestamp > CACHE_TTL
          )
        ),
        Effect.map(cache => cache.data)
      )
    : fetchAndCache
  );
});
```

### 9.7.4 Parallel Execution with Concurrency Limits

```typescript
import { Effect } from 'effect';

// ❌ Bad - Parallel without limit (อาจ overwhelm server)
const deleteAllBad = (ids: string[]) =>
  Effect.all(ids.map(id => deleteTodo(id)));

// ✅ Good - Parallel with concurrency limit
const deleteAllSafe = (ids: string[]) =>
  Effect.all(
    ids.map(id => deleteTodo(id)),
    { concurrency: 5 } // Max 5 concurrent requests
  );

// Different concurrency strategies
const fetchMultiple = (ids: string[]) =>
  Effect.all(
    ids.map(id => getTodoById(id)),
    {
      concurrency: 10,
      mode: 'either' // Continue even if some fail
    }
  );

// Batch processing with concurrency
const processBatch = (todos: Todo[]) =>
  Effect.all(
    todos.map(todo => updateTodo(todo.id, { processed: true })),
    {
      concurrency: 3,
      discard: false // Keep all results
    }
  );
```

**Use Case จริง - Clear Completed:**
```typescript
// เรามีใน Section 9.6 แล้ว แต่นี่คือ explanation
export const clearCompleted = Effect.gen(function* () {
  const api = yield* TodoApi;
  const todos = yield* api.fetchAll();

  const completed = todos.filter(t => t.completed);

  // Delete all completed in parallel but limit to 5 concurrent
  yield* Effect.all(
    completed.map(t => api.delete(t.id)),
    { concurrency: 5 } // ⭐ Prevent overwhelming server
  );
});
```

### 9.7.5 Error Recovery Patterns

```typescript
// 1. Fallback to cache on error - Functional Style
const fetchWithCacheFallback = Effect.gen(function* () {
  const api = yield* TodoApi;
  const storage = yield* Storage;
  const logger = yield* Logger;

  return yield* api.fetchAll().pipe(
    // On error, try cache fallback
    Effect.catchAll(() =>
      Effect.gen(function* () {
        const cached = yield* storage.get<Todo[]>('todos_cache');

        return yield* (cached
          ? logger.warn('Using cached data due to API failure').pipe(
              Effect.map(() => cached)
            )
          : logger.error('No cache available, returning empty').pipe(
              Effect.map(() => [] as Todo[])
            )
        );
      })
    )
  );
});

// 2. Circuit Breaker Pattern - Fully Functional with Ref
const CIRCUIT_BREAK_THRESHOLD = 5;
const CIRCUIT_RESET_TIMEOUT = 60000; // 1 minute

const makeCircuitBreaker = <A, E, R>(
  effect: Effect.Effect<A, E, R>
) =>
  Effect.gen(function* () {
    const failureCount = yield* Ref.make(0);

    return Effect.gen(function* () {
      const count = yield* failureCount.get;

      // Check circuit state and conditionally execute
      return yield* Effect.if(
        count >= CIRCUIT_BREAK_THRESHOLD,
        {
          // Circuit is open - fail immediately
          onTrue: () => Effect.fail(
            new NetworkError('Circuit breaker open - too many failures')
          ),
          // Circuit is closed - try the effect
          onFalse: () => effect.pipe(
            Effect.tap(() => failureCount.set(0)), // Reset on success
            Effect.catchAll((error) =>
              Effect.gen(function* () {
                yield* failureCount.update(n => n + 1);

                // Schedule circuit reset
                yield* Effect.async<void>((resume) => {
                  setTimeout(() => {
                    Effect.runPromise(failureCount.set(0));
                    resume(Effect.void);
                  }, CIRCUIT_RESET_TIMEOUT);
                }).pipe(Effect.fork);

                return yield* Effect.fail(error);
              })
            )
          )
        }
      );
    });
  });

// Usage
const fetchWithCircuitBreaker = Effect.gen(function* () {
  const breaker = yield* makeCircuitBreaker(fetchAllTodos);
  return yield* breaker;
});

// 3. Graceful Degradation - Functional Style
const fetchWithDegradation = Effect.gen(function* () {
  const api = yield* TodoApi;
  const storage = yield* Storage;
  const logger = yield* Logger;

  // Try multiple fallback strategies using Effect.orElse
  return yield* api.fetchAll().pipe(
    // Fallback 1: Try cached data
    Effect.orElse(() =>
      Effect.gen(function* () {
        const cached = yield* storage.get<Todo[]>('todos');
        return cached ?? yield* Effect.fail('No cache');
      })
    ),
    // Fallback 2: Try partial data (active only)
    Effect.orElse(() =>
      api.fetchAll().pipe(
        Effect.map(todos => todos.filter(t => !t.isCompleted))
      )
    ),
    // Fallback 3: Return empty array with logging
    Effect.catchAll(() =>
      logger.error('All fallbacks failed, returning empty').pipe(
        Effect.map(() => [] as Todo[])
      )
    )
  );
});
```

### 9.7.6 Resource Management

```typescript
// Automatic cleanup with Effect.acquireRelease
const withDatabase = <A, E>(
  action: (db: Database) => Effect.Effect<A, E, never>
) =>
  Effect.acquireRelease(
    Effect.sync(() => {
      console.log('Opening database connection');
      return new Database();
    }),
    (db) =>
      Effect.sync(() => {
        console.log('Closing database connection');
        db.close();
      })
  ).pipe(Effect.flatMap(action));

// Usage
const fetchWithDb = withDatabase((db) =>
  Effect.gen(function* () {
    const todos = yield* Effect.tryPromise(() => db.query('SELECT * FROM todos'));
    return todos;
  })
);
```

---

## 9.8 React Integration

### 9.8.1 useRunEffect Hook

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

### 9.8.2 useTodos Hook

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

### 9.8.3 Immer สำหรับ Complex State Updates (Optional)

เมื่อ state มีความซับซ้อน การใช้ spread operators อาจทำให้โค้ดอ่านยาก **Immer** ช่วยให้เขียน immutable updates ด้วย syntax ที่ดูเหมือน mutation ปกติ

**ติดตั้ง:**

```bash
npm install immer use-immer
```

#### ปัญหาของ Spread Operators

```typescript
// ❌ Nested spread - อ่านยาก, error-prone
const handleUpdateNested = () => {
  setState(prev => ({
    ...prev,
    user: {
      ...prev.user,
      profile: {
        ...prev.user.profile,
        settings: {
          ...prev.user.profile.settings,
          theme: 'dark'
        }
      }
    }
  }));
};

// ❌ Array update - verbose
const handleToggle = (id: number) => {
  setTodos(prev =>
    prev.map(todo =>
      todo.id === id
        ? { ...todo, isCompleted: !todo.isCompleted }
        : todo
    )
  );
};
```

#### Immer ทำให้ง่ายขึ้น

**src/hooks/useTodosWithImmer.ts:**

```typescript
import { useCallback } from 'react';
import { useImmer } from 'use-immer';
import { Effect } from 'effect';
import type { Todo, TodoFilter, TodoStats } from '@/domain/Todo';
import type { TodoError } from '@/services/errors';
import * as todoEffects from '@/effects/todos';
import { AppLayer } from '@/layers';

interface TodoState {
  todos: Todo[];
  filter: TodoFilter;
  loading: boolean;
  error: TodoError | null;
  // For optimistic updates
  pendingUpdates: Set<number>;
}

const initialState: TodoState = {
  todos: [],
  filter: 'all',
  loading: true,
  error: null,
  pendingUpdates: new Set()
};

export function useTodosWithImmer() {
  const [state, updateState] = useImmer<TodoState>(initialState);

  // Fetch todos
  const fetchTodos = useCallback(async () => {
    updateState(draft => {
      draft.loading = true;
      draft.error = null;
    });

    try {
      const todos = await Effect.runPromise(
        todoEffects.fetchAllTodos.pipe(Effect.provide(AppLayer))
      );

      updateState(draft => {
        draft.todos = todos;
        draft.loading = false;
      });
    } catch (err) {
      updateState(draft => {
        draft.error = err as TodoError;
        draft.loading = false;
      });
    }
  }, [updateState]);

  // Toggle with optimistic update
  const handleToggle = useCallback(async (id: number) => {
    // 1. Optimistic update - UI responds immediately
    updateState(draft => {
      const todo = draft.todos.find(t => t.id === id);
      if (todo) {
        todo.isCompleted = !todo.isCompleted;
        todo.completedAt = todo.isCompleted ? new Date() : null;
      }
      draft.pendingUpdates.add(id);
    });

    try {
      // 2. Call API
      await Effect.runPromise(
        todoEffects.toggleTodo(String(id)).pipe(Effect.provide(AppLayer))
      );

      // 3. Clear pending
      updateState(draft => {
        draft.pendingUpdates.delete(id);
      });
    } catch (err) {
      // 4. Revert on error
      updateState(draft => {
        const todo = draft.todos.find(t => t.id === id);
        if (todo) {
          todo.isCompleted = !todo.isCompleted;
          todo.completedAt = todo.isCompleted ? new Date() : null;
        }
        draft.pendingUpdates.delete(id);
        draft.error = err as TodoError;
      });
    }
  }, [updateState]);

  // Update todo title (inline editing)
  const handleUpdate = useCallback(async (id: number, title: string) => {
    const originalTitle = state.todos.find(t => t.id === id)?.title;

    // Optimistic update
    updateState(draft => {
      const todo = draft.todos.find(t => t.id === id);
      if (todo) {
        todo.title = title;
      }
      draft.pendingUpdates.add(id);
    });

    try {
      await Effect.runPromise(
        todoEffects.updateTodo(String(id), { title }).pipe(Effect.provide(AppLayer))
      );

      updateState(draft => {
        draft.pendingUpdates.delete(id);
      });
    } catch (err) {
      // Revert
      updateState(draft => {
        const todo = draft.todos.find(t => t.id === id);
        if (todo && originalTitle) {
          todo.title = originalTitle;
        }
        draft.pendingUpdates.delete(id);
        draft.error = err as TodoError;
      });
    }
  }, [state.todos, updateState]);

  // Delete with optimistic update
  const handleDelete = useCallback(async (id: number) => {
    const deletedTodo = state.todos.find(t => t.id === id);
    const deletedIndex = state.todos.findIndex(t => t.id === id);

    // Optimistic - remove immediately
    updateState(draft => {
      draft.todos = draft.todos.filter(t => t.id !== id);
    });

    try {
      await Effect.runPromise(
        todoEffects.deleteTodo(String(id)).pipe(Effect.provide(AppLayer))
      );
    } catch (err) {
      // Revert - restore at original position
      if (deletedTodo) {
        updateState(draft => {
          draft.todos.splice(deletedIndex, 0, deletedTodo);
          draft.error = err as TodoError;
        });
      }
    }
  }, [state.todos, updateState]);

  // Filter change
  const setFilter = useCallback((filter: TodoFilter) => {
    updateState(draft => {
      draft.filter = filter;
    });
  }, [updateState]);

  // Computed values
  const filteredTodos = state.todos.filter(todo => {
    switch (state.filter) {
      case 'active': return !todo.isCompleted;
      case 'completed': return todo.isCompleted;
      default: return true;
    }
  });

  const stats: TodoStats = {
    total: state.todos.length,
    active: state.todos.filter(t => !t.isCompleted).length,
    completed: state.todos.filter(t => t.isCompleted).length
  };

  return {
    todos: filteredTodos,
    allTodos: state.todos,
    stats,
    filter: state.filter,
    loading: state.loading,
    error: state.error,
    pendingUpdates: state.pendingUpdates,
    fetchTodos,
    toggleTodo: handleToggle,
    updateTodo: handleUpdate,
    deleteTodo: handleDelete,
    setFilter
  };
}
```

#### เปรียบเทียบ: useState vs useImmer

| Aspect | useState + Spread | useImmer |
|--------|-------------------|----------|
| **Nested updates** | Verbose, error-prone | Clean, intuitive |
| **Array mutations** | ต้องใช้ map/filter | ใช้ push/splice ได้ |
| **Optimistic updates** | ซับซ้อน | ง่ายกว่า |
| **Bundle size** | 0KB | ~12KB |
| **Learning curve** | ต่ำ | ต่ำ (เหมือนเขียน mutable) |

#### เมื่อไหร่ควรใช้ Immer?

**✅ ใช้ Immer เมื่อ:**
- State มี nested objects ลึก 2+ levels
- ต้องการ optimistic updates
- มี array operations หลายจุด
- โค้ด spread operators ยาวเกิน 3-4 บรรทัด

**❌ ไม่จำเป็นต้องใช้เมื่อ:**
- State เป็น flat object หรือ primitive
- Update แค่ 1-2 fields
- ต้องการลด bundle size ให้น้อยที่สุด

#### Immer กับ Effect-TS

Immer และ Effect-TS ทำงานคนละหน้าที่และใช้ร่วมกันได้ดี:

```typescript
// Effect-TS: จัดการ async operations และ business logic
const toggleTodoEffect = (id: string) =>
  Effect.gen(function* () {
    const api = yield* TodoApi;
    const logger = yield* Logger;

    yield* logger.info(`Toggling todo ${id}`);
    return yield* api.toggle(id);
  });

// Immer: จัดการ React state updates
const handleToggle = async (id: number) => {
  // Immer สำหรับ optimistic update
  updateState(draft => {
    const todo = draft.todos.find(t => t.id === id);
    if (todo) todo.isCompleted = !todo.isCompleted;
  });

  // Effect-TS สำหรับ API call
  await Effect.runPromise(
    toggleTodoEffect(String(id)).pipe(Effect.provide(AppLayer))
  );
};
```

> 💡 **สรุป**: Immer เป็น optional tool ที่ช่วยให้โค้ด state management อ่านง่ายขึ้น โดยเฉพาะเมื่อมี nested updates หรือ optimistic updates ไม่จำเป็นต้องใช้ทุกโปรเจค แต่มีประโยชน์เมื่อ state ซับซ้อน

---

## 9.9 Components

### 9.9.1 TodoList Component

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

### 9.9.2 TodoItem Component

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
    <li className={clsx('todo-item', { completed: todo.isCompleted, loading })}>
      <div className="todo-item-content">
        <input
          type="checkbox"
          checked={todo.isCompleted}
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

### 9.9.3 AddTodoForm Component

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

### 9.9.4 TodoFilters Component

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

### 9.9.5 App Component

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

## 9.10 Testing

### 9.10.1 Testing Effects with Mock Layer

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
      expect(todo.isCompleted).toBe(false);
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
      const program = Effect.gen(function* () {
        // Get initial todo
        const todos = yield* fetchAllTodos;
        const todo = todos[0];
        expect(todo.isCompleted).toBe(false);

        // Toggle it
        const toggled = yield* toggleTodo(todo.id);
        expect(toggled.isCompleted).toBe(true);

        // Toggle back
        const toggledBack = yield* toggleTodo(todo.id);
        expect(toggledBack.isCompleted).toBe(false);
      }).pipe(Effect.provide(AppLayerMock));

      await Effect.runPromise(program);
    });
  });
});
```

### 9.10.2 Testing React Components

**src/components/__tests__/TodoList.test.tsx:**

```typescript
import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { TodoList } from '../TodoList';
import type { Todo } from '@/domain/Todo';

describe('TodoList', () => {
  const mockTodos: Todo[] = [
    {
      id: 1,
      title: 'Test Todo 1',
      description: '',
      isCompleted: false,
      createdAt: new Date(),
      completedAt: null
    },
    {
      id: 2,
      title: 'Test Todo 2',
      description: '',
      isCompleted: true,
      createdAt: new Date(),
      completedAt: new Date()
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

## 9.11 Best Practices - Production-Ready Code

> 💡 **หมายเหตุ:** ส่วนนี้รวม best practices จากบทที่ 8 และเพิ่มเติมสำหรับ production

### 9.11.1 Service Design

**DO:**
- ✅ แยก services ตาม responsibility (TodoApi, Logger, Storage)
- ✅ ใช้ discriminated unions สำหรับ errors
- ✅ Return type ชัดเจน `Effect<A, E, R>`

**DON'T:**
- ❌ รวม services ทั้งหมดเป็น service เดียว
- ❌ ใช้ generic `Error` type
- ❌ Throw exceptions ใน service implementations

### 9.11.2 Effect Composition

**DO:**
- ✅ ใช้ `Effect.gen` สำหรับ sequential operations
- ✅ ใช้ `Effect.all` สำหรับ parallel operations
- ✅ Handle errors ที่ level ที่เหมาะสม

**DON'T:**
- ❌ Nest `Effect.flatMap` ลึกเกินไป (ใช้ `Effect.gen` แทน)
- ❌ Run effects sequentially เมื่อสามารถทำ parallel ได้
- ❌ Catch errors เร็วเกินไป (ทำให้ lost context)

### 9.11.3 React Integration

**DO:**
- ✅ ใช้ custom hooks (`useRunEffect`, `useTodos`)
- ✅ Handle loading และ error states
- ✅ Memoize effects ด้วย `useMemo` หรือ `useCallback`

**DON'T:**
- ❌ Run effects ใน render function
- ❌ Create new effects ทุกครั้งที่ render
- ❌ Forget cleanup ใน `useEffect`

### 9.11.4 Performance

**DO:**
- ✅ Cache data เมื่อเหมาะสม (ดูใน `fetchAllTodos`)
- ✅ ใช้ `Effect.all` with `concurrency` limit
- ✅ Optimistic updates สำหรับ better UX

**DON'T:**
- ❌ Fetch ข้อมูลซ้ำโดยไม่จำเป็น
- ❌ Run unlimited concurrent requests
- ❌ Ignore loading states

---

## 9.12 สรุป

### Learning Journey: Chapter 8 → Chapter 9

ตารางเปรียบเทียบสิ่งที่เรียนรู้จากทั้งสองบท:

| Aspect | Chapter 8 (Fundamentals) | Chapter 9 (Production) |
|--------|--------------------------|------------------------|
| **Focus** | Concepts & Basic Patterns | Production Architecture |
| **CRUD Operations** | Read + Create (2 ops) | Full CRUD (6+ ops) |
| **Services** | 1 service (TodoApi) | 3 services (TodoApi, Logger, Storage) |
| **Error Types** | 1 type (ApiError) | 4 types (Network, NotFound, Validation, Unauthorized) |
| **State Management** | None | Ref.make for stateful mocks |
| **Caching** | None | TTL-based caching |
| **Advanced Patterns** | None | Retry, Timeout, Parallel, Circuit Breaker |
| **Components** | Simple (display only) | Feature-rich (inline edit, filters, stats) |
| **Testing** | Basic examples | Comprehensive suite |
| **Time to Complete** | 15-20 minutes | 2-3 hours |

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

---

### 🔙 Review Chapter 8

ถ้ายังสับสนเรื่อง Effect-TS basics:
→ กลับไปทบทวน **[บทที่ 8: Effect-TS Fundamentals](./chapter-08.md)**

### 🚀 Next Steps

ในบทถัดไป เราจะเรียนรู้:
- **บทที่ 10:** Validation และ Form Handling
- **บทที่ 11:** Real-time Features (WebSocket, SSE)
- **บทที่ 12:** Testing Strategies (E2E, Integration)

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
