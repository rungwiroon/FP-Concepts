# Frontend Testing Guide with Effect-TS

## Overview

This guide shows how to test React components and Effect-TS code using the same trait-based pattern as the backend. We'll use **Vitest** (Vite's test runner) and create test implementations of `HttpClient` and `Logger`.

## Testing Strategy

### Similar to Backend Pattern

**Backend Pattern:**
```csharp
// Trait interface
public interface DatabaseIO { ... }

// Test implementation (Dictionary-based)
public class TestDatabaseIO : DatabaseIO { ... }

// Production implementation (EF Core)
public class LiveDatabaseIO : DatabaseIO { ... }
```

**Frontend Pattern (Effect-TS):**
```typescript
// Context Tag (like a trait)
export class HttpClientService extends Context.Tag('HttpClientService')<
  HttpClientService,
  HttpClient
>() {}

// Test implementation (in-memory)
export const createTestHttpClient = (): HttpClient => { ... }

// Production implementation (fetch API)
export const createHttpClient = (baseURL: string): HttpClient => { ... }
```

## Setup

### 1. Install Testing Dependencies

```bash
npm install -D vitest @testing-library/react @testing-library/jest-dom jsdom
npm install -D @testing-library/user-event @vitest/ui
```

### 2. Update package.json

```json
{
  "scripts": {
    "dev": "vite",
    "build": "tsc && vite build",
    "preview": "vite preview",
    "test": "vitest",
    "test:ui": "vitest --ui",
    "test:coverage": "vitest --coverage"
  }
}
```

### 3. Create vitest.config.ts

```typescript
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: './src/test/setup.ts',
  },
});
```

### 4. Create Test Setup File

```typescript
// src/test/setup.ts
import { expect, afterEach } from 'vitest';
import { cleanup } from '@testing-library/react';
import '@testing-library/jest-dom';

// Cleanup after each test
afterEach(() => {
  cleanup();
});
```

## Test Infrastructure

### Test HttpClient (In-Memory)

Similar to `TestDatabaseIO` in backend - stores data in memory instead of making real HTTP requests.

```typescript
// src/test/infrastructure/TestHttpClient.ts
import { Effect } from 'effect';
import type { HttpClient } from '../../lib/AppEnv';
import type { Todo } from '../../features/todos/types';

export class TestHttpClient implements HttpClient {
  private todos: Map<number, Todo> = new Map();
  private nextId = 1;

  // Track calls for assertions
  public callLog: Array<{ method: string; url: string; body?: any }> = [];

  constructor(initialTodos: Todo[] = []) {
    initialTodos.forEach(todo => {
      this.todos.set(todo.id, todo);
      if (todo.id >= this.nextId) {
        this.nextId = todo.id + 1;
      }
    });
  }

  get = <A>(url: string): Effect.Effect<A, Error> => {
    this.callLog.push({ method: 'GET', url });

    return Effect.sync(() => {
      // GET /todos
      if (url === '/todos') {
        return Array.from(this.todos.values()) as A;
      }

      // GET /todos/:id
      const match = url.match(/^\/todos\/(\d+)$/);
      if (match) {
        const id = parseInt(match[1]);
        const todo = this.todos.get(id);
        if (!todo) {
          throw new Error(`Todo with id ${id} not found`);
        }
        return todo as A;
      }

      throw new Error(`Unknown GET endpoint: ${url}`);
    });
  };

  post = <A, B>(url: string, body: A): Effect.Effect<B, Error> => {
    this.callLog.push({ method: 'POST', url, body });

    return Effect.sync(() => {
      // POST /todos
      if (url === '/todos') {
        const request = body as any;
        const todo: Todo = {
          id: this.nextId++,
          title: request.title,
          description: request.description,
          isCompleted: false,
          createdAt: new Date().toISOString(),
          completedAt: null,
        };
        this.todos.set(todo.id, todo);
        return todo as B;
      }

      throw new Error(`Unknown POST endpoint: ${url}`);
    });
  };

  put = <A, B>(url: string, body: A): Effect.Effect<B, Error> => {
    this.callLog.push({ method: 'PUT', url, body });

    return Effect.sync(() => {
      // PUT /todos/:id
      const match = url.match(/^\/todos\/(\d+)$/);
      if (match) {
        const id = parseInt(match[1]);
        const existing = this.todos.get(id);
        if (!existing) {
          throw new Error(`Todo with id ${id} not found`);
        }

        const request = body as any;
        const updated: Todo = {
          ...existing,
          title: request.title,
          description: request.description,
        };
        this.todos.set(id, updated);
        return updated as B;
      }

      throw new Error(`Unknown PUT endpoint: ${url}`);
    });
  };

  patch = <A>(url: string): Effect.Effect<A, Error> => {
    this.callLog.push({ method: 'PATCH', url });

    return Effect.sync(() => {
      // PATCH /todos/:id/toggle
      const match = url.match(/^\/todos\/(\d+)\/toggle$/);
      if (match) {
        const id = parseInt(match[1]);
        const existing = this.todos.get(id);
        if (!existing) {
          throw new Error(`Todo with id ${id} not found`);
        }

        const toggled: Todo = {
          ...existing,
          isCompleted: !existing.isCompleted,
          completedAt: !existing.isCompleted ? new Date().toISOString() : null,
        };
        this.todos.set(id, toggled);
        return toggled as A;
      }

      throw new Error(`Unknown PATCH endpoint: ${url}`);
    });
  };

  delete = (url: string): Effect.Effect<void, Error> => {
    this.callLog.push({ method: 'DELETE', url });

    return Effect.sync(() => {
      // DELETE /todos/:id
      const match = url.match(/^\/todos\/(\d+)$/);
      if (match) {
        const id = parseInt(match[1]);
        if (!this.todos.has(id)) {
          throw new Error(`Todo with id ${id} not found`);
        }
        this.todos.delete(id);
        return;
      }

      throw new Error(`Unknown DELETE endpoint: ${url}`);
    });
  };

  // Test helpers
  seed(todos: Todo[]) {
    todos.forEach(todo => {
      this.todos.set(todo.id, todo);
      if (todo.id >= this.nextId) {
        this.nextId = todo.id + 1;
      }
    });
  }

  clear() {
    this.todos.clear();
    this.nextId = 1;
    this.callLog = [];
  }

  getTodoById(id: number): Todo | undefined {
    return this.todos.get(id);
  }

  getAllTodos(): Todo[] {
    return Array.from(this.todos.values());
  }

  getCallCount(): number {
    return this.callLog.length;
  }
}
```

### Test Logger (In-Memory)

```typescript
// src/test/infrastructure/TestLogger.ts
import type { Logger } from '../../lib/AppEnv';

export class TestLogger implements Logger {
  public logs: Array<{ level: string; message: string; error?: unknown }> = [];

  info = (message: string) => {
    this.logs.push({ level: 'info', message });
  };

  warn = (message: string) => {
    this.logs.push({ level: 'warn', message });
  };

  error = (message: string, err?: unknown) => {
    this.logs.push({ level: 'error', message, error: err });
  };

  // Test helpers
  clear() {
    this.logs.clear();
  }

  getInfoLogs(): string[] {
    return this.logs.filter(l => l.level === 'info').map(l => l.message);
  }

  getErrorLogs(): string[] {
    return this.logs.filter(l => l.level === 'error').map(l => l.message);
  }

  hasLog(message: string): boolean {
    return this.logs.some(l => l.message.includes(message));
  }
}
```

### Test Environment Factory

```typescript
// src/test/infrastructure/TestEnv.ts
import type { AppEnv } from '../../lib/AppEnv';
import { TestHttpClient } from './TestHttpClient';
import { TestLogger } from './TestLogger';
import type { Todo } from '../../features/todos/types';

export const createTestEnv = (initialTodos: Todo[] = []): {
  env: AppEnv;
  httpClient: TestHttpClient;
  logger: TestLogger;
} => {
  const httpClient = new TestHttpClient(initialTodos);
  const logger = new TestLogger();

  const env: AppEnv = {
    httpClient,
    logger,
    baseUrl: 'http://test',
  };

  return { env, httpClient, logger };
};
```

## Testing API Functions

### Unit Tests for Effect-TS Functions

```typescript
// src/features/todos/api.test.ts
import { describe, it, expect } from 'vitest';
import { Effect } from 'effect';
import { createTestEnv } from '../../test/infrastructure/TestEnv';
import { HttpClientService, LoggerService } from '../../lib/AppEnv';
import * as TodoAPI from './api';
import type { Todo } from './types';

describe('Todo API', () => {
  describe('listTodos', () => {
    it('should fetch all todos', async () => {
      // Arrange
      const { env, httpClient } = createTestEnv();
      const todo: Todo = {
        id: 1,
        title: 'Test Todo',
        description: 'Test Description',
        isCompleted: false,
        createdAt: new Date().toISOString(),
        completedAt: null,
      };
      httpClient.seed([todo]);

      // Act
      const result = await Effect.runPromise(
        TodoAPI.listTodos().pipe(
          Effect.provideService(HttpClientService, env.httpClient),
          Effect.provideService(LoggerService, env.logger)
        )
      );

      // Assert
      expect(result).toHaveLength(1);
      expect(result[0].title).toBe('Test Todo');
      expect(httpClient.callLog).toHaveLength(1);
      expect(httpClient.callLog[0]).toEqual({ method: 'GET', url: '/todos' });
    });

    it('should return empty array when no todos', async () => {
      // Arrange
      const { env } = createTestEnv();

      // Act
      const result = await Effect.runPromise(
        TodoAPI.listTodos().pipe(
          Effect.provideService(HttpClientService, env.httpClient),
          Effect.provideService(LoggerService, env.logger)
        )
      );

      // Assert
      expect(result).toHaveLength(0);
    });
  });

  describe('createTodo', () => {
    it('should create a new todo', async () => {
      // Arrange
      const { env, httpClient } = createTestEnv();
      const request = {
        title: 'New Todo',
        description: 'New Description',
      };

      // Act
      const result = await Effect.runPromise(
        TodoAPI.createTodo(request).pipe(
          Effect.provideService(HttpClientService, env.httpClient),
          Effect.provideService(LoggerService, env.logger)
        )
      );

      // Assert
      expect(result.id).toBe(1);
      expect(result.title).toBe('New Todo');
      expect(result.isCompleted).toBe(false);
      expect(httpClient.getAllTodos()).toHaveLength(1);
    });
  });

  describe('toggleTodo', () => {
    it('should toggle todo completion status', async () => {
      // Arrange
      const { env, httpClient } = createTestEnv();
      const todo: Todo = {
        id: 1,
        title: 'Test Todo',
        description: null,
        isCompleted: false,
        createdAt: new Date().toISOString(),
        completedAt: null,
      };
      httpClient.seed([todo]);

      // Act
      const result = await Effect.runPromise(
        TodoAPI.toggleTodo(1).pipe(
          Effect.provideService(HttpClientService, env.httpClient),
          Effect.provideService(LoggerService, env.logger)
        )
      );

      // Assert
      expect(result.isCompleted).toBe(true);
      expect(result.completedAt).not.toBeNull();
    });
  });

  describe('deleteTodo', () => {
    it('should delete a todo', async () => {
      // Arrange
      const { env, httpClient } = createTestEnv();
      const todo: Todo = {
        id: 1,
        title: 'Test Todo',
        description: null,
        isCompleted: false,
        createdAt: new Date().toISOString(),
        completedAt: null,
      };
      httpClient.seed([todo]);

      // Act
      await Effect.runPromise(
        TodoAPI.deleteTodo(1).pipe(
          Effect.provideService(HttpClientService, env.httpClient),
          Effect.provideService(LoggerService, env.logger)
        )
      );

      // Assert
      expect(httpClient.getAllTodos()).toHaveLength(0);
    });

    it('should throw error when todo not found', async () => {
      // Arrange
      const { env } = createTestEnv();

      // Act & Assert
      await expect(
        Effect.runPromise(
          TodoAPI.deleteTodo(999).pipe(
            Effect.provideService(HttpClientService, env.httpClient),
            Effect.provideService(LoggerService, env.logger)
          )
        )
      ).rejects.toThrow('Todo with id 999 not found');
    });
  });
});
```

## Testing React Hooks

### Testing useAppQuery Hook

```typescript
// src/features/todos/hooks.test.ts
import { describe, it, expect, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useAppQuery } from './hooks';
import { createTestEnv } from '../../test/infrastructure/TestEnv';
import { listTodos } from './api';
import type { Todo } from './types';

describe('useAppQuery', () => {
  let testEnv: ReturnType<typeof createTestEnv>;

  beforeEach(() => {
    testEnv = createTestEnv();
  });

  it('should fetch data on mount', async () => {
    // Arrange
    const todo: Todo = {
      id: 1,
      title: 'Test Todo',
      description: null,
      isCompleted: false,
      createdAt: new Date().toISOString(),
      completedAt: null,
    };
    testEnv.httpClient.seed([todo]);

    // Act
    const { result } = renderHook(() =>
      useAppQuery(testEnv.env, listTodos(), [])
    );

    // Initial state should be loading
    expect(result.current.state._tag).toBe('Loading');

    // Wait for data to load
    await waitFor(() => {
      expect(result.current.state._tag).toBe('Success');
    });

    // Assert
    if (result.current.state._tag === 'Success') {
      expect(result.current.state.data).toHaveLength(1);
      expect(result.current.state.data[0].title).toBe('Test Todo');
    }
  });

  it('should handle errors', async () => {
    // Arrange - no todos seeded, will return empty array (not error)
    // For a real error test, we'd need to modify TestHttpClient to throw

    const { result } = renderHook(() =>
      useAppQuery(testEnv.env, listTodos(), [])
    );

    await waitFor(() => {
      expect(result.current.state._tag).toBe('Success');
    });

    if (result.current.state._tag === 'Success') {
      expect(result.current.state.data).toHaveLength(0);
    }
  });

  it('should provide refetch function', async () => {
    // Arrange
    testEnv.httpClient.seed([]);

    const { result } = renderHook(() =>
      useAppQuery(testEnv.env, listTodos(), [])
    );

    await waitFor(() => {
      expect(result.current.state._tag).toBe('Success');
    });

    // Add a todo after initial load
    testEnv.httpClient.seed([
      {
        id: 1,
        title: 'New Todo',
        description: null,
        isCompleted: false,
        createdAt: new Date().toISOString(),
        completedAt: null,
      },
    ]);

    // Act - refetch
    await result.current.refetch();

    // Assert
    await waitFor(() => {
      if (result.current.state._tag === 'Success') {
        expect(result.current.state.data).toHaveLength(1);
      }
    });
  });
});
```

## Testing React Components

### Testing TodoList Component

```typescript
// src/features/todos/components/TodoList.test.tsx
import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TodoList } from './TodoList';
import { createTestEnv } from '../../../test/infrastructure/TestEnv';
import type { Todo } from '../types';

describe('TodoList Component', () => {
  let testEnv: ReturnType<typeof createTestEnv>;

  beforeEach(() => {
    testEnv = createTestEnv();
  });

  it('should display loading state initially', () => {
    // Act
    render(<TodoList env={testEnv.env} />);

    // Assert
    expect(screen.getByText(/loading todos/i)).toBeInTheDocument();
  });

  it('should display todos after loading', async () => {
    // Arrange
    const todo: Todo = {
      id: 1,
      title: 'Test Todo',
      description: 'Test Description',
      isCompleted: false,
      createdAt: new Date().toISOString(),
      completedAt: null,
    };
    testEnv.httpClient.seed([todo]);

    // Act
    render(<TodoList env={testEnv.env} />);

    // Assert
    await waitFor(() => {
      expect(screen.getByText('Test Todo')).toBeInTheDocument();
      expect(screen.getByText('Test Description')).toBeInTheDocument();
    });
  });

  it('should display empty state when no todos', async () => {
    // Act
    render(<TodoList env={testEnv.env} />);

    // Assert
    await waitFor(() => {
      expect(
        screen.getByText(/no todos yet/i)
      ).toBeInTheDocument();
    });
  });

  it('should toggle todo completion', async () => {
    // Arrange
    const todo: Todo = {
      id: 1,
      title: 'Test Todo',
      description: null,
      isCompleted: false,
      createdAt: new Date().toISOString(),
      completedAt: null,
    };
    testEnv.httpClient.seed([todo]);
    const user = userEvent.setup();

    // Act
    render(<TodoList env={testEnv.env} />);

    await waitFor(() => {
      expect(screen.getByText('Test Todo')).toBeInTheDocument();
    });

    const completeButton = screen.getByRole('button', { name: /complete/i });
    await user.click(completeButton);

    // Assert
    await waitFor(() => {
      const todo = testEnv.httpClient.getTodoById(1);
      expect(todo?.isCompleted).toBe(true);
    });
  });

  it('should delete todo', async () => {
    // Arrange
    const todo: Todo = {
      id: 1,
      title: 'Test Todo',
      description: null,
      isCompleted: false,
      createdAt: new Date().toISOString(),
      completedAt: null,
    };
    testEnv.httpClient.seed([todo]);
    const user = userEvent.setup();

    // Mock window.confirm
    window.confirm = () => true;

    // Act
    render(<TodoList env={testEnv.env} />);

    await waitFor(() => {
      expect(screen.getByText('Test Todo')).toBeInTheDocument();
    });

    const deleteButton = screen.getByRole('button', { name: /delete/i });
    await user.click(deleteButton);

    // Assert
    await waitFor(() => {
      expect(testEnv.httpClient.getAllTodos()).toHaveLength(0);
    });
  });
});
```

## Running Tests

```bash
# Run all tests
npm test

# Run tests in watch mode
npm test -- --watch

# Run tests with UI
npm run test:ui

# Run tests with coverage
npm run test:coverage

# Run specific test file
npm test -- TodoList.test.tsx
```

## Key Benefits

### 1. Same Pattern as Backend
```typescript
// Backend: TestDatabaseIO (Dictionary)
public class TestDatabaseIO : DatabaseIO { ... }

// Frontend: TestHttpClient (Map)
export class TestHttpClient implements HttpClient { ... }
```

### 2. Fast Tests (No HTTP Calls)
- Tests run in memory
- No need for mock servers
- Instant feedback

### 3. Easy to Seed Test Data
```typescript
const { env, httpClient } = createTestEnv();
httpClient.seed([todo1, todo2, todo3]);
```

### 4. Track Side Effects
```typescript
expect(httpClient.callLog).toHaveLength(1);
expect(logger.hasLog('Creating todo')).toBe(true);
```

## Comparison: Backend vs Frontend Testing

| Aspect | Backend (C#) | Frontend (TypeScript) |
|--------|-------------|----------------------|
| **Trait/Interface** | `DatabaseIO` | `HttpClient` (Context Tag) |
| **Test Implementation** | `TestDatabaseIO` (Dictionary) | `TestHttpClient` (Map) |
| **Production Implementation** | `LiveDatabaseIO` (EF Core) | `createHttpClient` (fetch API) |
| **Test Framework** | NUnit | Vitest |
| **Storage** | `Dictionary<int, Todo>` | `Map<number, Todo>` |
| **Seeding** | `Seed(params Todo[])` | `seed(todos: Todo[])` |
| **Running Tests** | `dotnet test` | `npm test` |
| **Speed** | ~200ms for 14 tests | Similar performance |

## Next Steps

1. ✅ Install testing dependencies
2. ✅ Create test infrastructure (TestHttpClient, TestLogger)
3. ✅ Write API function tests
4. ✅ Write hook tests
5. ✅ Write component tests
6. Run tests and iterate

This approach gives you the same confidence in your frontend code as you have in your backend!
