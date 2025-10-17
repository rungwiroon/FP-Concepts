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
