# บทที่ 10: React Integration

> Advanced React Integration Patterns กับ Effect-TS

---

## เนื้อหาในบทนี้

- Effect Context Provider Pattern
- Global State Management with Effect
- Advanced Custom Hooks
- Form Handling & Validation
- Error Boundaries for Effects
- Optimistic Updates Pattern
- Real-time Updates (WebSocket)
- React Suspense Integration
- Performance Optimization
- Testing React + Effect
- Best Practices

---

## 10.1 Effect Context Provider Pattern

### 10.1.1 ปัญหาของการ Provide Layer ซ้ำๆ

ในบทที่ 9 เราต้อง provide `AppLayer` ทุกครั้งที่ run effect:

```typescript
// ❌ ต้อง provide ทุกครั้ง - verbose
const { data, loading, error } = useRunEffect(
  () => fetchAllTodos.pipe(Effect.provide(AppLayer)),
  []
);

// ❌ ใน hooks ก็ต้อง provide
const handleCreate = async (request: CreateTodoRequest) => {
  const effect = createTodo(request).pipe(Effect.provide(AppLayer));
  await Effect.runPromise(effect);
};
```

**ปัญหา:**
1. ต้อง provide `AppLayer` ทุกครั้ง
2. ถ้าต้องการเปลี่ยน layer (เช่น เปลี่ยนจาก Live เป็น Mock) ต้องแก้หลายที่
3. Testing ยาก - ต้อง mock ทุก component

### 10.1.2 Solution: Effect Context Provider

**src/contexts/EffectContext.tsx:**

```typescript
import React, { createContext, useContext, useMemo } from 'react';
import type { Layer } from 'effect';
import { AppLayerLive, AppLayerMock } from '@/layers';

interface EffectContextValue {
  layer: Layer.Layer<any, never, never>;
}

const EffectContext = createContext<EffectContextValue | null>(null);

interface EffectProviderProps {
  children: React.ReactNode;
  /**
   * Override layer (useful for testing)
   */
  layer?: Layer.Layer<any, never, never>;
  /**
   * Use mock layer (development mode)
   */
  useMock?: boolean;
}

/**
 * Provider for Effect Layer
 */
export function EffectProvider({
  children,
  layer,
  useMock = false
}: EffectProviderProps) {
  const contextValue = useMemo(
    () => ({
      layer: layer ?? (useMock ? AppLayerMock : AppLayerLive)
    }),
    [layer, useMock]
  );

  return (
    <EffectContext.Provider value={contextValue}>
      {children}
    </EffectContext.Provider>
  );
}

/**
 * Hook to get current Effect Layer
 */
export function useEffectLayer() {
  const context = useContext(EffectContext);

  if (!context) {
    throw new Error('useEffectLayer must be used within EffectProvider');
  }

  return context.layer;
}
```

**การใช้งาน - App.tsx:**

```typescript
import React from 'react';
import { EffectProvider } from '@/contexts/EffectContext';
import { TodoApp } from '@/components/TodoApp';

function App() {
  return (
    <EffectProvider useMock={import.meta.env.DEV}>
      <TodoApp />
    </EffectProvider>
  );
}

export default App;
```

### 10.1.3 ปรับปรุง Hooks ให้ใช้ Context

**src/hooks/useRunEffect.ts (ปรับปรุง):**

```typescript
import { useEffect, useState, useRef } from 'react';
import { Effect, Exit } from 'effect';
import { useEffectLayer } from '@/contexts/EffectContext';

export interface UseEffectResult<A, E> {
  data: A | null;
  error: E | null;
  loading: boolean;
  refetch: () => void;
}

/**
 * Hook to run Effect in React with auto layer provision
 */
export function useRunEffect<A, E, R>(
  createEffect: () => Effect.Effect<A, E, R>,
  deps: React.DependencyList = []
): UseEffectResult<A, E> {
  const layer = useEffectLayer();
  const [data, setData] = useState<A | null>(null);
  const [error, setError] = useState<E | null>(null);
  const [loading, setLoading] = useState(true);
  const [refetchCount, setRefetchCount] = useState(0);

  const effectRef = useRef(createEffect);
  effectRef.current = createEffect;

  useEffect(() => {
    let cancelled = false;

    setLoading(true);
    setError(null);

    // Create effect and auto-provide layer from context
    const effect = effectRef.current().pipe(Effect.provide(layer));

    // ✅ Functional: Use Exit.match
    Effect.runPromiseExit(effect).then(exit => {
      if (cancelled) return;

      Exit.match(exit, {
        onFailure: (cause) => {
          setError(cause.failureOrCause as E);
          setData(null);
          setLoading(false);
        },
        onSuccess: (value) => {
          setData(value);
          setError(null);
          setLoading(false);
        }
      });
    });

    return () => {
      cancelled = true;
    };
  }, [...deps, refetchCount, layer]);

  const refetch = () => {
    setRefetchCount(c => c + 1);
  };

  return { data, error, loading, refetch };
}
```

**ตอนนี้ใช้งานง่ายขึ้น:**

```typescript
// ✅ ไม่ต้อง provide layer เอง!
const { data, loading, error } = useRunEffect(() => fetchAllTodos, []);

// ✅ Testing ง่าย - เปลี่ยน layer ที่ Provider
<EffectProvider layer={TestLayer}>
  <TodoList />
</EffectProvider>
```

---

## 10.2 Global State Management with Effect

### 10.2.1 ปัญหาของ Local State

```typescript
// ❌ ทุก component ต้อง fetch todos เอง
function TodoList() {
  const { todos } = useTodos();  // Fetch again
}

function TodoStats() {
  const { stats } = useTodos();  // Fetch again!
}

function TodoCount() {
  const { todos } = useTodos();  // Fetch again!!
}
```

**ปัญหา:**
1. Fetch ข้อมูลซ้ำหลายครั้ง
2. ข้อมูลไม่ sync ระหว่าง components
3. Loading states แยกกัน

### 10.2.2 Solution: Global Store with Effect

> **💡 Functional Pattern**: ในส่วนนี้เราใช้ `Effect.matchEffect` แทน `if-else` เพื่อให้โค้ดเป็น **declarative** และ **type-safe** มากขึ้น

**Pattern เปรียบเทียบ:**

```typescript
// ❌ Imperative style with if-else
const result = yield* _(Effect.either(someEffect));
if (result._tag === 'Right') {
  // handle success
} else {
  // handle error
}

// ✅ Functional style with Effect.matchEffect
yield* _(
  someEffect,
  Effect.matchEffect({
    onFailure: (error) => handleError(error),
    onSuccess: (value) => handleSuccess(value)
  })
);
```

**src/store/TodoStore.ts:**

```typescript
import { Effect, Ref, Stream, SubscriptionRef } from 'effect';
import type { Todo, TodoFilter, TodoStats } from '@/domain/Todo';
import type { TodoError } from '@/services/errors';
import * as TodoEffects from '@/effects/todos';

export interface TodoStoreState {
  todos: Todo[];
  filter: TodoFilter;
  loading: boolean;
  error: TodoError | null;
}

const initialState: TodoStoreState = {
  todos: [],
  filter: 'all',
  loading: false,
  error: null
};

/**
 * Create TodoStore with SubscriptionRef
 */
export const makeTodoStore = Effect.gen(function* (_) {
  // Create mutable state that can be subscribed to
  const stateRef = yield* _(SubscriptionRef.make(initialState));

  /**
   * Load todos
   */
  const load = Effect.gen(function* (_) {
    // Set loading
    yield* _(
      SubscriptionRef.update(stateRef, state => ({
        ...state,
        loading: true,
        error: null
      }))
    );

    // ✅ Functional: Use Effect.matchEffect
    yield* _(
      TodoEffects.fetchAllTodos,
      Effect.matchEffect({
        onFailure: (error) =>
          SubscriptionRef.update(stateRef, state => ({
            ...state,
            loading: false,
            error
          })),
        onSuccess: (todos) =>
          SubscriptionRef.update(stateRef, state => ({
            ...state,
            todos,
            loading: false,
            error: null
          }))
      })
    );
  });

  /**
   * Create todo
   */
  const create = (title: string) =>
    Effect.gen(function* (_) {
      const todo = yield* _(TodoEffects.createTodo({ title }));

      // Optimistic update
      yield* _(
        SubscriptionRef.update(stateRef, state => ({
          ...state,
          todos: [...state.todos, todo]
        }))
      );

      return todo;
    });

  /**
   * Toggle todo
   */
  const toggle = (id: string) =>
    Effect.gen(function* (_) {
      // Optimistic update
      yield* _(
        SubscriptionRef.update(stateRef, state => ({
          ...state,
          todos: state.todos.map(t =>
            t.id === id ? { ...t, completed: !t.completed } : t
          )
        }))
      );

      // ✅ Functional: Use Effect.matchEffect
      yield* _(
        TodoEffects.toggleTodo(id),
        Effect.matchEffect({
          onFailure: (error) =>
            // Revert optimistic update on error
            SubscriptionRef.update(stateRef, state => ({
              ...state,
              todos: state.todos.map(t =>
                t.id === id ? { ...t, completed: !t.completed } : t
              ),
              error
            })),
          onSuccess: () =>
            // Success - keep optimistic update
            Effect.succeed(undefined)
        })
      );
    });

  /**
   * Delete todo
   */
  const deleteTodo = (id: string) =>
    Effect.gen(function* (_) {
      // Save previous state for rollback
      const previousTodos = (yield* _(SubscriptionRef.get(stateRef))).todos;

      // Optimistic update
      yield* _(
        SubscriptionRef.update(stateRef, state => ({
          ...state,
          todos: state.todos.filter(t => t.id !== id)
        }))
      );

      // ✅ Functional: Use Effect.matchEffect
      yield* _(
        TodoEffects.deleteTodo(id),
        Effect.matchEffect({
          onFailure: (error) =>
            // Revert on error
            SubscriptionRef.update(stateRef, state => ({
              ...state,
              todos: previousTodos,
              error
            })),
          onSuccess: () =>
            // Success - keep optimistic update
            Effect.succeed(undefined)
        })
      );
    });

  /**
   * Set filter
   */
  const setFilter = (filter: TodoFilter) =>
    SubscriptionRef.update(stateRef, state => ({ ...state, filter }));

  /**
   * Get current state
   */
  const getState = SubscriptionRef.get(stateRef);

  /**
   * Subscribe to state changes
   */
  const subscribe = SubscriptionRef.changes(stateRef);

  return {
    load,
    create,
    toggle,
    delete: deleteTodo,
    setFilter,
    getState,
    subscribe
  };
});

export type TodoStore = Effect.Effect.Success<typeof makeTodoStore>;
```

**src/contexts/TodoStoreContext.tsx:**

```typescript
import React, { createContext, useContext, useEffect, useState } from 'react';
import { Effect } from 'effect';
import { makeTodoStore, type TodoStore, type TodoStoreState } from '@/store/TodoStore';
import { useEffectLayer } from './EffectContext';

const TodoStoreContext = createContext<TodoStore | null>(null);

interface TodoStoreProviderProps {
  children: React.ReactNode;
}

/**
 * Provider for TodoStore
 */
export function TodoStoreProvider({ children }: TodoStoreProviderProps) {
  const layer = useEffectLayer();
  const [store, setStore] = useState<TodoStore | null>(null);

  useEffect(() => {
    // Create store
    const effect = makeTodoStore.pipe(Effect.provide(layer));

    Effect.runPromise(effect).then(setStore);
  }, [layer]);

  if (!store) {
    return <div>Initializing store...</div>;
  }

  return (
    <TodoStoreContext.Provider value={store}>
      {children}
    </TodoStoreContext.Provider>
  );
}

/**
 * Hook to get TodoStore
 */
export function useTodoStore() {
  const store = useContext(TodoStoreContext);

  if (!store) {
    throw new Error('useTodoStore must be used within TodoStoreProvider');
  }

  return store;
}

/**
 * Hook to subscribe to store state
 */
export function useTodoStoreState(): TodoStoreState {
  const store = useTodoStore();
  const layer = useEffectLayer();
  const [state, setState] = useState<TodoStoreState>({
    todos: [],
    filter: 'all',
    loading: false,
    error: null
  });

  useEffect(() => {
    let cancelled = false;

    // Get initial state
    Effect.runPromise(
      store.getState.pipe(Effect.provide(layer))
    ).then(initialState => {
      if (!cancelled) setState(initialState);
    });

    // Subscribe to changes
    const subscription = Effect.runPromise(
      Effect.gen(function* (_) {
        const stream = yield* _(store.subscribe);

        yield* _(
          stream,
          Stream.runForEach(newState =>
            Effect.sync(() => {
              if (!cancelled) setState(newState);
            })
          )
        );
      }).pipe(Effect.provide(layer))
    );

    return () => {
      cancelled = true;
    };
  }, [store, layer]);

  return state;
}
```

### 10.2.3 ใช้งาน Global Store

**src/App.tsx:**

```typescript
import React from 'react';
import { EffectProvider } from '@/contexts/EffectContext';
import { TodoStoreProvider } from '@/contexts/TodoStoreContext';
import { TodoApp } from '@/components/TodoApp';

function App() {
  return (
    <EffectProvider>
      <TodoStoreProvider>
        <TodoApp />
      </TodoStoreProvider>
    </EffectProvider>
  );
}

export default App;
```

**src/components/TodoList.tsx:**

```typescript
import React, { useEffect } from 'react';
import { Effect } from 'effect';
import { useTodoStore, useTodoStoreState } from '@/contexts/TodoStoreContext';
import { useEffectLayer } from '@/contexts/EffectContext';
import { TodoItem } from './TodoItem';

export function TodoList() {
  const store = useTodoStore();
  const layer = useEffectLayer();
  const { todos, filter, loading, error } = useTodoStoreState();

  // Load todos on mount
  useEffect(() => {
    Effect.runPromise(store.load.pipe(Effect.provide(layer)));
  }, [store, layer]);

  // Filter todos
  const filteredTodos = todos.filter(todo => {
    if (filter === 'active') return !todo.completed;
    if (filter === 'completed') return todo.completed;
    return true;
  });

  const handleToggle = (id: string) => {
    Effect.runPromise(store.toggle(id).pipe(Effect.provide(layer)));
  };

  const handleDelete = (id: string) => {
    Effect.runPromise(store.delete(id).pipe(Effect.provide(layer)));
  };

  if (loading) return <div>Loading...</div>;
  if (error) return <div>Error: {error.message}</div>;

  return (
    <ul className="todo-list">
      {filteredTodos.map(todo => (
        <TodoItem
          key={todo.id}
          todo={todo}
          onToggle={handleToggle}
          onDelete={handleDelete}
        />
      ))}
    </ul>
  );
}
```

**src/components/TodoStats.tsx:**

```typescript
import React from 'react';
import { useTodoStoreState } from '@/contexts/TodoStoreContext';

export function TodoStats() {
  const { todos } = useTodoStoreState();

  const total = todos.length;
  const active = todos.filter(t => !t.completed).length;
  const completed = todos.filter(t => t.completed).length;

  return (
    <div className="todo-stats">
      <span>Total: {total}</span>
      <span>Active: {active}</span>
      <span>Completed: {completed}</span>
    </div>
  );
}
```

**ข้อดี:**
- ✅ Fetch ครั้งเดียว ใช้ร่วมกันได้
- ✅ State sync ระหว่าง components
- ✅ Optimistic updates built-in
- ✅ Type-safe ทั้งระบบ

---

## 10.3 Form Handling & Validation

### 10.3.1 Form State with Effect

**src/hooks/useForm.ts:**

```typescript
import { useState, useCallback } from 'react';
import { Effect, Either } from 'effect';

export interface FieldError {
  field: string;
  message: string;
}

export interface FormState<T> {
  values: T;
  errors: Record<keyof T, string>;
  touched: Record<keyof T, boolean>;
  submitting: boolean;
}

export interface UseFormOptions<T> {
  initialValues: T;
  validate: (values: T) => Effect.Effect<T, FieldError[], never>;
  onSubmit: (values: T) => Effect.Effect<void, Error, never>;
}

export function useForm<T extends Record<string, any>>({
  initialValues,
  validate,
  onSubmit
}: UseFormOptions<T>) {
  const [state, setState] = useState<FormState<T>>({
    values: initialValues,
    errors: {} as Record<keyof T, string>,
    touched: {} as Record<keyof T, boolean>,
    submitting: false
  });

  const setValue = useCallback(
    <K extends keyof T>(field: K, value: T[K]) => {
      setState(prev => ({
        ...prev,
        values: { ...prev.values, [field]: value },
        touched: { ...prev.touched, [field]: true }
      }));
    },
    []
  );

  const setError = useCallback(
    (field: keyof T, message: string) => {
      setState(prev => ({
        ...prev,
        errors: { ...prev.errors, [field]: message }
      }));
    },
    []
  );

  const clearError = useCallback(
    (field: keyof T) => {
      setState(prev => {
        const errors = { ...prev.errors };
        delete errors[field];
        return { ...prev, errors };
      });
    },
    []
  );

  const handleSubmit = useCallback(
    async (e?: React.FormEvent) => {
      if (e) e.preventDefault();

      setState(prev => ({ ...prev, submitting: true, errors: {} }));

      try {
        // ✅ Functional: Use Effect.matchEffect for validation
        await Effect.runPromise(
          validate(state.values).pipe(
            Effect.matchEffect({
              onFailure: (fieldErrors) =>
                Effect.sync(() => {
                  // Validation failed
                  const errors = fieldErrors.reduce(
                    (acc, err) => ({ ...acc, [err.field]: err.message }),
                    {} as Record<keyof T, string>
                  );

                  setState(prev => ({
                    ...prev,
                    errors,
                    submitting: false
                  }));
                }),
              onSuccess: (validValues) =>
                // Submit with valid values
                onSubmit(validValues)
            })
          )
        );

        // Reset form on success
        setState({
          values: initialValues,
          errors: {} as Record<keyof T, string>,
          touched: {} as Record<keyof T, boolean>,
          submitting: false
        });
      } catch (error) {
        setState(prev => ({
          ...prev,
          submitting: false,
          errors: {
            ...prev.errors,
            _form: (error as Error).message
          } as any
        }));
      }
    },
    [state.values, validate, onSubmit, initialValues]
  );

  const reset = useCallback(() => {
    setState({
      values: initialValues,
      errors: {} as Record<keyof T, string>,
      touched: {} as Record<keyof T, boolean>,
      submitting: false
    });
  }, [initialValues]);

  return {
    values: state.values,
    errors: state.errors,
    touched: state.touched,
    submitting: state.submitting,
    setValue,
    setError,
    clearError,
    handleSubmit,
    reset
  };
}
```

### 10.3.2 Validation with Effect

**src/validation/todoValidation.ts:**

```typescript
import { Effect } from 'effect';
import type { FieldError } from '@/hooks/useForm';

export interface CreateTodoForm {
  title: string;
  description: string;
  priority: 'low' | 'medium' | 'high';
  dueDate: string;
}

/**
 * Validate todo form
 */
export const validateTodoForm = (
  values: CreateTodoForm
): Effect.Effect<CreateTodoForm, FieldError[], never> =>
  Effect.gen(function* (_) {
    const errors: FieldError[] = [];

    // Title validation
    if (values.title.trim() === '') {
      errors.push({ field: 'title', message: 'Title is required' });
    } else if (values.title.length < 3) {
      errors.push({ field: 'title', message: 'Title must be at least 3 characters' });
    } else if (values.title.length > 200) {
      errors.push({ field: 'title', message: 'Title must be less than 200 characters' });
    }

    // Description validation (optional)
    if (values.description.length > 1000) {
      errors.push({
        field: 'description',
        message: 'Description must be less than 1000 characters'
      });
    }

    // Due date validation
    if (values.dueDate) {
      const date = new Date(values.dueDate);
      const now = new Date();

      if (isNaN(date.getTime())) {
        errors.push({ field: 'dueDate', message: 'Invalid date' });
      } else if (date < now) {
        errors.push({ field: 'dueDate', message: 'Due date must be in the future' });
      }
    }

    // Return errors or valid values
    if (errors.length > 0) {
      return yield* _(Effect.fail(errors));
    }

    return values;
  });

/**
 * Async validation - check if title is unique
 */
export const validateUniqueTodoTitle = (
  title: string,
  existingTodos: string[]
): Effect.Effect<string, FieldError[], never> =>
  Effect.gen(function* (_) {
    // Simulate API call
    yield* _(Effect.sleep('100 millis'));

    if (existingTodos.includes(title.toLowerCase())) {
      return yield* _(
        Effect.fail([
          { field: 'title', message: 'A todo with this title already exists' }
        ])
      );
    }

    return title;
  });
```

### 10.3.3 Form Component

**src/components/CreateTodoForm.tsx:**

```typescript
import React from 'react';
import { Effect } from 'effect';
import { useForm } from '@/hooks/useForm';
import { validateTodoForm, type CreateTodoForm } from '@/validation/todoValidation';
import { useTodoStore } from '@/contexts/TodoStoreContext';
import { useEffectLayer } from '@/contexts/EffectContext';
import clsx from 'clsx';

export function CreateTodoForm() {
  const store = useTodoStore();
  const layer = useEffectLayer();

  const form = useForm<CreateTodoForm>({
    initialValues: {
      title: '',
      description: '',
      priority: 'medium',
      dueDate: ''
    },
    validate: validateTodoForm,
    onSubmit: (values) =>
      Effect.gen(function* (_) {
        yield* _(store.create(values.title));
      }).pipe(Effect.provide(layer))
  });

  return (
    <form onSubmit={form.handleSubmit} className="create-todo-form">
      <div className="form-group">
        <label htmlFor="title">Title *</label>
        <input
          id="title"
          type="text"
          value={form.values.title}
          onChange={e => form.setValue('title', e.target.value)}
          className={clsx('form-control', {
            'is-invalid': form.touched.title && form.errors.title
          })}
          disabled={form.submitting}
        />
        {form.touched.title && form.errors.title && (
          <div className="error-message">{form.errors.title}</div>
        )}
      </div>

      <div className="form-group">
        <label htmlFor="description">Description</label>
        <textarea
          id="description"
          value={form.values.description}
          onChange={e => form.setValue('description', e.target.value)}
          className={clsx('form-control', {
            'is-invalid': form.touched.description && form.errors.description
          })}
          rows={4}
          disabled={form.submitting}
        />
        {form.touched.description && form.errors.description && (
          <div className="error-message">{form.errors.description}</div>
        )}
      </div>

      <div className="form-group">
        <label htmlFor="priority">Priority</label>
        <select
          id="priority"
          value={form.values.priority}
          onChange={e =>
            form.setValue('priority', e.target.value as CreateTodoForm['priority'])
          }
          className="form-control"
          disabled={form.submitting}
        >
          <option value="low">Low</option>
          <option value="medium">Medium</option>
          <option value="high">High</option>
        </select>
      </div>

      <div className="form-group">
        <label htmlFor="dueDate">Due Date</label>
        <input
          id="dueDate"
          type="date"
          value={form.values.dueDate}
          onChange={e => form.setValue('dueDate', e.target.value)}
          className={clsx('form-control', {
            'is-invalid': form.touched.dueDate && form.errors.dueDate
          })}
          disabled={form.submitting}
        />
        {form.touched.dueDate && form.errors.dueDate && (
          <div className="error-message">{form.errors.dueDate}</div>
        )}
      </div>

      {form.errors._form && (
        <div className="alert alert-danger">{form.errors._form}</div>
      )}

      <div className="form-actions">
        <button
          type="submit"
          className="btn btn-primary"
          disabled={form.submitting}
        >
          {form.submitting ? 'Creating...' : 'Create Todo'}
        </button>

        <button
          type="button"
          className="btn btn-secondary"
          onClick={form.reset}
          disabled={form.submitting}
        >
          Reset
        </button>
      </div>
    </form>
  );
}
```

---

## 10.4 Error Boundaries for Effects

### 10.4.1 Effect Error Boundary

**src/components/EffectErrorBoundary.tsx:**

```typescript
import React, { Component, ErrorInfo, ReactNode } from 'react';
import type { TodoError } from '@/services/errors';

interface Props {
  children: ReactNode;
  fallback?: (error: Error, reset: () => void) => ReactNode;
  onError?: (error: Error, errorInfo: ErrorInfo) => void;
}

interface State {
  hasError: boolean;
  error: Error | null;
}

export class EffectErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error('EffectErrorBoundary caught error:', error, errorInfo);
    this.props.onError?.(error, errorInfo);
  }

  reset = () => {
    this.setState({ hasError: false, error: null });
  };

  render() {
    if (this.state.hasError && this.state.error) {
      if (this.props.fallback) {
        return this.props.fallback(this.state.error, this.reset);
      }

      return (
        <div className="error-boundary">
          <h2>Something went wrong</h2>
          <details>
            <summary>Error details</summary>
            <pre>{this.state.error.message}</pre>
          </details>
          <button onClick={this.reset}>Try again</button>
        </div>
      );
    }

    return this.props.children;
  }
}

/**
 * Custom fallback for Todo errors
 */
export function TodoErrorFallback(error: Error, reset: () => void) {
  const todoError = error as TodoError;

  return (
    <div className="error-boundary todo-error">
      <h2>Failed to load todos</h2>

      {todoError._tag === 'NetworkError' && (
        <p>
          Network error occurred. Please check your connection and try again.
        </p>
      )}

      {todoError._tag === 'NotFoundError' && (
        <p>
          The requested todo was not found. It may have been deleted.
        </p>
      )}

      {todoError._tag === 'UnauthorizedError' && (
        <p>
          You are not authorized to access this resource. Please log in.
        </p>
      )}

      <button onClick={reset} className="btn btn-primary">
        Retry
      </button>
    </div>
  );
}
```

**การใช้งาน:**

```typescript
import { EffectErrorBoundary, TodoErrorFallback } from './EffectErrorBoundary';

function App() {
  return (
    <EffectProvider>
      <EffectErrorBoundary fallback={TodoErrorFallback}>
        <TodoStoreProvider>
          <TodoApp />
        </TodoStoreProvider>
      </EffectErrorBoundary>
    </EffectProvider>
  );
}
```

---

## 10.5 Optimistic Updates Pattern

### 10.5.1 Simple Optimistic Update

```typescript
/**
 * Toggle todo with optimistic update
 */
const toggleTodoOptimistic = (id: string, currentCompleted: boolean) =>
  Effect.gen(function* (_) {
    const logger = yield* _(Logger);

    // 1. Update UI immediately
    yield* _(logger.info(`Optimistically toggling todo ${id}`));

    // Return immediately (user sees instant feedback)
    // API call happens in background
    yield* _(
      Effect.fork(
        Effect.gen(function* (_) {
          const api = yield* _(TodoApi);

          // ✅ Functional: Use Effect.matchEffect
          yield* _(
            api.toggle(id),
            Effect.matchEffect({
              onFailure: (error) =>
                Effect.gen(function* (_) {
                  yield* _(logger.error('Failed to toggle, reverting', error));
                  // Revert logic here
                }),
              onSuccess: () =>
                logger.info('Toggle confirmed by server')
            })
          );
        })
      )
    );
  });
```

### 10.5.2 Advanced Optimistic Update with Rollback

**src/hooks/useOptimisticUpdate.ts:**

```typescript
import { useState, useCallback, useRef } from 'react';
import { Effect } from 'effect';

export interface OptimisticUpdateOptions<T, E> {
  /**
   * Optimistic update function
   */
  update: (current: T) => T;

  /**
   * API call effect
   */
  apiCall: Effect.Effect<T, E, never>;

  /**
   * Callback on success
   */
  onSuccess?: (result: T) => void;

  /**
   * Callback on error
   */
  onError?: (error: E) => void;
}

export function useOptimisticUpdate<T>(initialValue: T) {
  const [value, setValue] = useState<T>(initialValue);
  const [pending, setPending] = useState(false);
  const previousValueRef = useRef<T>(initialValue);

  const execute = useCallback(
    async <E>(options: OptimisticUpdateOptions<T, E>) => {
      // Save current value
      previousValueRef.current = value;

      // Apply optimistic update
      const optimisticValue = options.update(value);
      setValue(optimisticValue);
      setPending(true);

      try {
        // Execute API call
        const result = await Effect.runPromise(options.apiCall);

        // Update with server response
        setValue(result);
        options.onSuccess?.(result);
      } catch (error) {
        // Rollback on error
        setValue(previousValueRef.current);
        options.onError?.(error as E);
      } finally {
        setPending(false);
      }
    },
    [value]
  );

  const rollback = useCallback(() => {
    setValue(previousValueRef.current);
    setPending(false);
  }, []);

  return {
    value,
    pending,
    execute,
    rollback
  };
}
```

**ตัวอย่างการใช้งาน:**

```typescript
function TodoItem({ todo }: { todo: Todo }) {
  const layer = useEffectLayer();
  const optimistic = useOptimisticUpdate(todo);

  const handleToggle = () => {
    optimistic.execute({
      update: (current) => ({ ...current, completed: !current.completed }),
      apiCall: toggleTodo(todo.id).pipe(Effect.provide(layer)),
      onSuccess: (updated) => {
        console.log('Toggle confirmed:', updated);
      },
      onError: (error) => {
        console.error('Toggle failed:', error);
        // Show error toast
      }
    });
  };

  return (
    <div className={clsx('todo-item', { pending: optimistic.pending })}>
      <input
        type="checkbox"
        checked={optimistic.value.completed}
        onChange={handleToggle}
      />
      <span>{optimistic.value.title}</span>
    </div>
  );
}
```

---

## 10.6 Real-time Updates with WebSocket

### 10.6.1 WebSocket Service

**src/services/WebSocketService.ts:**

```typescript
import { Context, Effect, Queue, Stream } from 'effect';

export interface WebSocketMessage<T = unknown> {
  type: string;
  payload: T;
}

export interface WebSocketService {
  /**
   * Connect to WebSocket
   */
  readonly connect: (url: string) => Effect.Effect<void, Error, never>;

  /**
   * Send message
   */
  readonly send: <T>(message: WebSocketMessage<T>) => Effect.Effect<void, Error, never>;

  /**
   * Subscribe to messages
   */
  readonly messages: Stream.Stream<WebSocketMessage, Error, never>;

  /**
   * Disconnect
   */
  readonly disconnect: () => Effect.Effect<void, never, never>;
}

export const WebSocketService = Context.GenericTag<WebSocketService>(
  '@services/WebSocketService'
);
```

**src/layers/WebSocketServiceLive.ts:**

```typescript
import { Effect, Layer, Queue, Stream, Ref } from 'effect';
import { WebSocketService, type WebSocketMessage } from '@/services/WebSocketService';

export const WebSocketServiceLive = Layer.effect(
  WebSocketService,
  Effect.gen(function* (_) {
    const messageQueue = yield* _(Queue.unbounded<WebSocketMessage>());
    const wsRef = yield* _(Ref.make<WebSocket | null>(null));

    const connect = (url: string) =>
      Effect.gen(function* (_) {
        // Create WebSocket
        const ws = new WebSocket(url);

        // Handle messages
        ws.onmessage = (event) => {
          try {
            const message = JSON.parse(event.data) as WebSocketMessage;
            Effect.runPromise(Queue.offer(messageQueue, message));
          } catch (error) {
            console.error('Failed to parse WebSocket message:', error);
          }
        };

        // Handle errors
        ws.onerror = (error) => {
          console.error('WebSocket error:', error);
        };

        // Wait for connection
        yield* _(
          Effect.async<void, Error>((resume) => {
            ws.onopen = () => resume(Effect.succeed(undefined));
            ws.onerror = (error) =>
              resume(Effect.fail(new Error('WebSocket connection failed')));
          })
        );

        // Store WebSocket
        yield* _(Ref.set(wsRef, ws));
      });

    const send = <T>(message: WebSocketMessage<T>) =>
      Effect.gen(function* (_) {
        const ws = yield* _(Ref.get(wsRef));

        if (!ws || ws.readyState !== WebSocket.OPEN) {
          return yield* _(Effect.fail(new Error('WebSocket not connected')));
        }

        ws.send(JSON.stringify(message));
      });

    const messages = Stream.fromQueue(messageQueue);

    const disconnect = Effect.gen(function* (_) {
      const ws = yield* _(Ref.get(wsRef));

      if (ws) {
        ws.close();
        yield* _(Ref.set(wsRef, null));
      }
    });

    return WebSocketService.of({
      connect,
      send,
      messages,
      disconnect
    });
  })
);
```

### 10.6.2 Real-time Todo Updates

**src/hooks/useRealtimeTodos.ts:**

```typescript
import { useEffect } from 'react';
import { Effect, Stream } from 'effect';
import { WebSocketService } from '@/services/WebSocketService';
import { useTodoStore } from '@/contexts/TodoStoreContext';
import { useEffectLayer } from '@/contexts/EffectContext';

interface TodoUpdateMessage {
  type: 'todo.created' | 'todo.updated' | 'todo.deleted';
  payload: {
    id: string;
    todo?: any;
  };
}

export function useRealtimeTodos() {
  const store = useTodoStore();
  const layer = useEffectLayer();

  useEffect(() => {
    const program = Effect.gen(function* (_) {
      const ws = yield* _(WebSocketService);

      // Connect
      yield* _(ws.connect('ws://localhost:5000/todos'));

      // Subscribe to messages
      yield* _(
        ws.messages,
        Stream.runForEach((message) =>
          Effect.gen(function* (_) {
            const todoMessage = message as TodoUpdateMessage;

            switch (todoMessage.type) {
              case 'todo.created':
                // Reload todos
                yield* _(store.load);
                break;

              case 'todo.updated':
                // Reload todos
                yield* _(store.load);
                break;

              case 'todo.deleted':
                // Reload todos
                yield* _(store.load);
                break;
            }
          })
        )
      );
    }).pipe(Effect.provide(layer));

    const subscription = Effect.runPromise(program);

    return () => {
      // Cleanup: disconnect
      Effect.runPromise(
        Effect.gen(function* (_) {
          const ws = yield* _(WebSocketService);
          yield* _(ws.disconnect());
        }).pipe(Effect.provide(layer))
      );
    };
  }, [store, layer]);
}
```

**การใช้งาน:**

```typescript
function TodoApp() {
  // Enable real-time updates
  useRealtimeTodos();

  return (
    <div>
      <TodoList />
      <TodoStats />
    </div>
  );
}
```

---

## 10.7 Performance Optimization

### 10.7.1 Memoize Effects

```typescript
import { useMemo } from 'react';
import { Effect } from 'effect';

function TodoList({ filter }: { filter: TodoFilter }) {
  // ❌ Bad: creates new effect every render
  const { data } = useRunEffect(
    () => getTodosWithStats(filter),
    [filter]
  );

  // ✅ Good: memoize effect
  const effect = useMemo(
    () => getTodosWithStats(filter),
    [filter]
  );

  const { data } = useRunEffect(() => effect, [effect]);
}
```

### 10.7.2 Debounce API Calls

**src/hooks/useDebounced.ts:**

```typescript
import { useState, useEffect } from 'react';

export function useDebounced<T>(value: T, delay: number): T {
  const [debouncedValue, setDebouncedValue] = useState(value);

  useEffect(() => {
    const handler = setTimeout(() => {
      setDebouncedValue(value);
    }, delay);

    return () => {
      clearTimeout(handler);
    };
  }, [value, delay]);

  return debouncedValue;
}
```

**การใช้งาน:**

```typescript
function SearchTodos() {
  const [query, setQuery] = useState('');
  const debouncedQuery = useDebounced(query, 300);

  const { data: results } = useRunEffect(
    () => searchTodos(debouncedQuery),
    [debouncedQuery]
  );

  return (
    <div>
      <input
        value={query}
        onChange={e => setQuery(e.target.value)}
        placeholder="Search todos..."
      />
      {results && <TodoList todos={results} />}
    </div>
  );
}
```

### 10.7.3 Parallel Requests

```typescript
import { Effect } from 'effect';

// ❌ Bad: Sequential
const loadDashboard = Effect.gen(function* (_) {
  const todos = yield* _(fetchTodos());
  const stats = yield* _(fetchStats());
  const profile = yield* _(fetchProfile());
  return { todos, stats, profile };
});

// ✅ Good: Parallel
const loadDashboard = Effect.gen(function* (_) {
  const [todos, stats, profile] = yield* _(
    Effect.all(
      [fetchTodos(), fetchStats(), fetchProfile()],
      { concurrency: 'unbounded' }
    )
  );
  return { todos, stats, profile };
});
```

### 10.7.4 React.memo for Components

```typescript
import React, { memo } from 'react';

// ❌ Re-renders every time parent re-renders
export function TodoItem({ todo, onToggle }: TodoItemProps) {
  return <div>...</div>;
}

// ✅ Only re-renders when props change
export const TodoItem = memo(function TodoItem({ todo, onToggle }: TodoItemProps) {
  return <div>...</div>;
}, (prev, next) => {
  // Custom comparison
  return prev.todo.id === next.todo.id &&
         prev.todo.completed === next.todo.completed &&
         prev.todo.title === next.todo.title;
});
```

---

## 10.8 Testing React + Effect

### 10.8.1 Testing Components with Mock Layer

```typescript
import { describe, it, expect } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Effect, Layer } from 'effect';
import { EffectProvider } from '@/contexts/EffectContext';
import { TodoStoreProvider } from '@/contexts/TodoStoreContext';
import { TodoList } from '@/components/TodoList';
import { TodoApi } from '@/services/TodoApi';

// Create test layer
const TestTodoApi = Layer.succeed(
  TodoApi,
  TodoApi.of({
    fetchAll: () => Effect.succeed([
      { id: '1', title: 'Test Todo', completed: false, createdAt: new Date() }
    ]),
    toggle: (id) => Effect.succeed({
      id,
      title: 'Test Todo',
      completed: true,
      createdAt: new Date()
    }),
    // ... other methods
  })
);

describe('TodoList', () => {
  it('should load and display todos', async () => {
    render(
      <EffectProvider layer={TestTodoApi}>
        <TodoStoreProvider>
          <TodoList />
        </TodoStoreProvider>
      </EffectProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('Test Todo')).toBeInTheDocument();
    });
  });

  it('should toggle todo', async () => {
    const user = userEvent.setup();

    render(
      <EffectProvider layer={TestTodoApi}>
        <TodoStoreProvider>
          <TodoList />
        </TodoStoreProvider>
      </EffectProvider>
    );

    // Wait for load
    await waitFor(() => {
      expect(screen.getByText('Test Todo')).toBeInTheDocument();
    });

    // Click checkbox
    const checkbox = screen.getByRole('checkbox');
    await user.click(checkbox);

    // Verify toggled
    await waitFor(() => {
      expect(checkbox).toBeChecked();
    });
  });
});
```

### 10.8.2 Testing Hooks

```typescript
import { describe, it, expect } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { Effect, Layer } from 'effect';
import { EffectProvider } from '@/contexts/EffectContext';
import { useRunEffect } from '@/hooks/useRunEffect';

describe('useRunEffect', () => {
  it('should run effect and return data', async () => {
    const { result } = renderHook(
      () => useRunEffect(() => Effect.succeed(42), []),
      {
        wrapper: ({ children }) => (
          <EffectProvider>{children}</EffectProvider>
        )
      }
    );

    expect(result.current.loading).toBe(true);

    await waitFor(() => {
      expect(result.current.loading).toBe(false);
      expect(result.current.data).toBe(42);
      expect(result.current.error).toBe(null);
    });
  });

  it('should handle errors', async () => {
    const { result } = renderHook(
      () => useRunEffect(() => Effect.fail(new Error('Test error')), []),
      {
        wrapper: ({ children }) => (
          <EffectProvider>{children}</EffectProvider>
        )
      }
    );

    await waitFor(() => {
      expect(result.current.loading).toBe(false);
      expect(result.current.error).toEqual(new Error('Test error'));
      expect(result.current.data).toBe(null);
    });
  });
});
```

---

## 10.9 Best Practices

### 10.9.1 Context Organization

```
contexts/
├── EffectContext.tsx       # Layer provider
├── TodoStoreContext.tsx    # Global store
├── AuthContext.tsx         # Authentication
└── index.ts                # Export all
```

**DO:**
- ✅ แยก contexts ตาม responsibility
- ✅ ใช้ TypeScript generics สำหรับ type safety
- ✅ Provide meaningful error messages

**DON'T:**
- ❌ รวม contexts ทั้งหมดเป็นอันเดียว
- ❌ ใช้ `any` type
- ❌ Forget null checks

### 10.9.2 Hook Patterns

**DO:**
- ✅ ใช้ prefix `use` สำหรับ custom hooks
- ✅ Return object สำหรับ multiple values
- ✅ Memoize expensive computations

```typescript
// ✅ Good
function useTodos() {
  return {
    todos,
    loading,
    error,
    createTodo,
    updateTodo
  };
}

// ❌ Bad
function getTodos() {  // Missing 'use' prefix
  return [todos, loading, error];  // Array is harder to use
}
```

### 10.9.3 Effect Composition

**DO:**
- ✅ ใช้ `Effect.gen` สำหรับ sequential operations
- ✅ ใช้ `Effect.all` สำหรับ parallel operations
- ✅ Handle errors explicitly

**DON'T:**
- ❌ Nest too many `Effect.flatMap`
- ❌ Ignore error handling
- ❌ Mix async/await กับ Effect

### 10.9.4 Performance

**DO:**
- ✅ Memoize effects และ callbacks
- ✅ Use `React.memo` สำหรับ expensive components
- ✅ Debounce user inputs
- ✅ Batch API calls

**DON'T:**
- ❌ Create new effects ทุก render
- ❌ Fetch ข้อมูลซ้ำโดยไม่จำเป็น
- ❌ Run unlimited concurrent requests

---

## 10.10 สรุป

### สิ่งที่ได้เรียนรู้ในบทนี้

1. **Effect Context Provider**
   - Centralized layer management
   - Easy testing with mock layers
   - Type-safe dependency injection

2. **Global State Management**
   - Store pattern with SubscriptionRef
   - Reactive state updates
   - Optimistic updates built-in

3. **Form Handling**
   - Validation with Effect
   - Type-safe error handling
   - Async validation support

4. **Error Boundaries**
   - Catch and recover from errors
   - Custom fallback components
   - Error logging

5. **Real-time Updates**
   - WebSocket integration
   - Stream processing
   - Auto-sync with server

6. **Performance Optimization**
   - Memoization strategies
   - Parallel requests
   - Debouncing
   - React.memo

7. **Testing**
   - Component testing with mock layers
   - Hook testing
   - Integration testing

### ข้อดีของ Advanced Integration

1. **Developer Experience** - Less boilerplate, more productivity
2. **Type Safety** - Compiler catches bugs early
3. **Testability** - Easy to mock and test
4. **Maintainability** - Clear separation of concerns
5. **Performance** - Optimized by default

### บทถัดไป

ในบทที่ 11 เราจะเรียนรู้:
- **Validation และ Form Handling** - Complex forms with Effect-TS
- **Schema Validation** - Using Effect Schema
- **Multi-step Forms** - Wizard patterns
- **File Uploads** - Handling files with Effect

---

## แบบฝึกหัดท้ายบท

### ข้อ 1: Implement Undo/Redo

สร้าง undo/redo functionality สำหรับ todo operations:
- Track command history
- Implement undo/redo effects
- Add UI buttons

### ข้อ 2: Add Pagination

เพิ่ม pagination สำหรับ todo list:
- Page size configuration
- Next/Previous navigation
- Total pages indicator

### ข้อ 3: Implement Search

สร้าง search functionality:
- Debounced search input
- Filter by title/description
- Highlight matches

### ข้อ 4: Add Keyboard Shortcuts

เพิ่ม keyboard shortcuts:
- `Ctrl+N` - Create new todo
- `Ctrl+/` - Focus search
- `Esc` - Clear filters

### ข้อ 5: Offline Queue

Implement offline operation queue:
- Queue operations when offline
- Sync when back online
- Show sync status

---

**พร้อมที่จะสร้าง Production-Ready React Apps ด้วย Effect-TS แล้วใช่ไหม?**
