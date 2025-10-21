import { Effect } from 'effect';
import type { HttpClient } from '../../lib/AppEnv';
import type { Todo } from '../../features/todos/types';

/**
 * Test HTTP Client - In-memory implementation
 * Similar to TestDatabaseIO in backend - uses Map instead of real HTTP calls
 * Enables fast, isolated unit tests without network dependencies
 */
export class TestHttpClient implements HttpClient {
  private todos: Map<number, Todo> = new Map();
  private nextId = 1;

  // Track calls for assertions (useful for testing side effects)
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
        return Array.from(this.todos.values())
          .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()) as A;
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
          description: request.description ?? null,
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
          description: request.description ?? null,
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

  // ========== Test Helper Methods ==========
  // Similar to TestDatabaseIO in backend

  /**
   * Seed the in-memory database with test todos
   * Much simpler than setting up a real HTTP server!
   */
  seed(todos: Todo[]) {
    todos.forEach(todo => {
      this.todos.set(todo.id, todo);
      if (todo.id >= this.nextId) {
        this.nextId = todo.id + 1;
      }
    });
  }

  /**
   * Clear all todos and reset state
   */
  clear() {
    this.todos.clear();
    this.nextId = 1;
    this.callLog = [];
  }

  /**
   * Get a todo by ID (for test assertions)
   */
  getTodoById(id: number): Todo | undefined {
    return this.todos.get(id);
  }

  /**
   * Get all todos (for test assertions)
   */
  getAllTodos(): Todo[] {
    return Array.from(this.todos.values());
  }

  /**
   * Get the number of API calls made (for testing)
   */
  getCallCount(): number {
    return this.callLog.length;
  }

  /**
   * Get calls by method (for testing)
   */
  getCallsByMethod(method: string): Array<{ method: string; url: string; body?: any }> {
    return this.callLog.filter(call => call.method === method);
  }
}
