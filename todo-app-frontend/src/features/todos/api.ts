import { Effect } from 'effect';
import * as App from '../../lib/AppMonad';
import { withLogging } from '../../lib/effects/logging';
import type { Todo, CreateTodoRequest, UpdateTodoRequest } from './types';

// Re-export types
export type { Todo, CreateTodoRequest, UpdateTodoRequest } from './types';

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
