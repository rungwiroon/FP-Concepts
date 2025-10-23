# บทที่ 12: Testing Frontend

> Comprehensive Testing Strategies สำหรับ React + Effect-TS Applications

---

## เนื้อหาในบทนี้

- Testing Philosophy
- Unit Testing Effects
- Testing React Components
- Testing Forms and Validation
- Testing Custom Hooks
- Testing with Mock Layers
- Integration Testing
- E2E Testing Strategies
- Test Coverage
- Testing Best Practices

---

## 12.1 Testing Philosophy

### 12.1.1 ทำไมต้อง Test?

Testing ช่วยให้เรา:

1. **Confidence** - มั่นใจว่าโค้ดทำงานถูกต้อง
2. **Refactoring Safety** - แก้ไขโค้ดโดยไม่กลัวพัง
3. **Documentation** - Tests เป็น documentation ที่ดี
4. **Bug Prevention** - จับ bugs ก่อนถึง production
5. **Faster Development** - ในระยะยาว tests ช่วยให้ dev เร็วขึ้น

**Testing Trade-offs:**
- ✅ Higher quality, fewer bugs
- ⚠️ Takes time to write tests
- ⚠️ Tests need maintenance

### 12.1.2 Testing Pyramid

```
        /\
       /E2E\        ← น้อยที่สุด (slow, expensive)
      /------\
     / Integ \     ← ปานกลาง (medium speed)
    /----------\
   /   Unit     \  ← มากที่สุด (fast, cheap)
  /--------------\
```

**Unit Tests (70%):**
- Test แต่ละฟังก์ชัน/component แยกกัน
- เร็ว, ง่าย, เยอะ
- Mock dependencies

**Integration Tests (20%):**
- Test หลาย components ทำงานร่วมกัน
- ใช้ real services บางส่วน
- ช้ากว่า unit tests

**E2E Tests (10%):**
- Test ทั้ง application จริงๆ
- User perspective
- ช้าที่สุด, แพงที่สุด

### 12.1.3 Testing Strategy สำหรับ Effect-TS

Effect-TS ทำให้ testing ง่ายขึ้นเพราะ:

1. **Pure Functions** - Effects เป็น descriptions, ไม่ run ทันที
2. **Dependency Injection** - Mock services ได้ง่าย
3. **Predictable** - Same input → Same output
4. **Composable** - Test แต่ละส่วนแยกกัน

**Pattern:**

```typescript
// 1. Define effect
const fetchUser = (id: string) => Effect.gen(function* (_) {
  const api = yield* _(UserApi);
  return yield* _(api.getUser(id));
});

// 2. Test with mock layer
const result = await Effect.runPromise(
  fetchUser('123').pipe(Effect.provide(MockUserApi))
);

// 3. Assert
expect(result).toEqual(mockUser);
```

---

## 12.2 Unit Testing Effects

### 12.2.1 Testing Pure Effects

Pure effects ไม่มี dependencies, test ได้ง่าย

**Example: Validation Logic**

```typescript
import { describe, it, expect } from 'vitest';
import { Effect } from 'effect';

// Effect to test
const validateAge = (age: number) =>
  age >= 18
    ? Effect.succeed(age)
    : Effect.fail(new Error('Must be at least 18 years old'));

describe('validateAge', () => {
  it('should succeed for valid age', async () => {
    const result = await Effect.runPromise(validateAge(25));
    expect(result).toBe(25);
  });

  it('should fail for invalid age', async () => {
    await expect(
      Effect.runPromise(validateAge(15))
    ).rejects.toThrow('Must be at least 18 years old');
  });

  it('should succeed for edge case (exactly 18)', async () => {
    const result = await Effect.runPromise(validateAge(18));
    expect(result).toBe(18);
  });
});
```

### 12.2.2 Testing Effects with Dependencies

Effects ที่มี dependencies ต้อง provide mock layers

**Setup:**

```typescript
// src/effects/todos.ts
import { Effect } from 'effect';
import { TodoApi } from '@/services/TodoApi';

export const fetchAllTodos = Effect.gen(function* (_) {
  const api = yield* _(TodoApi);
  return yield* _(api.fetchAll());
});
```

**Test:**

```typescript
// src/effects/__tests__/todos.test.ts
import { describe, it, expect } from 'vitest';
import { Effect, Layer } from 'effect';
import { TodoApi } from '@/services/TodoApi';
import { fetchAllTodos } from '../todos';

// Create mock layer
const mockTodos = [
  { id: '1', title: 'Test 1', completed: false, createdAt: new Date() },
  { id: '2', title: 'Test 2', completed: true, createdAt: new Date() }
];

const MockTodoApi = Layer.succeed(
  TodoApi,
  TodoApi.of({
    fetchAll: () => Effect.succeed(mockTodos),
    // ... other methods with default implementations
    getById: () => Effect.fail(new Error('Not implemented')),
    create: () => Effect.fail(new Error('Not implemented')),
    update: () => Effect.fail(new Error('Not implemented')),
    toggle: () => Effect.fail(new Error('Not implemented')),
    delete: () => Effect.fail(new Error('Not implemented'))
  })
);

describe('fetchAllTodos', () => {
  it('should fetch todos successfully', async () => {
    // Arrange: Provide mock layer
    const program = fetchAllTodos.pipe(Effect.provide(MockTodoApi));

    // Act: Run effect
    const result = await Effect.runPromise(program);

    // Assert
    expect(result).toEqual(mockTodos);
    expect(result).toHaveLength(2);
  });
});
```

### 12.2.3 Testing Error Handling

Test ทั้ง success และ error cases

```typescript
import { describe, it, expect } from 'vitest';
import { Effect, Layer } from 'effect';
import { TodoApi } from '@/services/TodoApi';
import { NotFoundError } from '@/services/errors';
import { getTodoById } from '../todos';

describe('getTodoById', () => {
  it('should return todo when found', async () => {
    const mockTodo = {
      id: '1',
      title: 'Test',
      completed: false,
      createdAt: new Date()
    };

    const MockTodoApi = Layer.succeed(
      TodoApi,
      TodoApi.of({
        getById: (id: string) => Effect.succeed(mockTodo),
        // ... other methods
      })
    );

    const result = await Effect.runPromise(
      getTodoById('1').pipe(Effect.provide(MockTodoApi))
    );

    expect(result).toEqual(mockTodo);
  });

  it('should fail when todo not found', async () => {
    const MockTodoApi = Layer.succeed(
      TodoApi,
      TodoApi.of({
        getById: (id: string) => Effect.fail(new NotFoundError('Todo', id)),
        // ... other methods
      })
    );

    await expect(
      Effect.runPromise(
        getTodoById('999').pipe(Effect.provide(MockTodoApi))
      )
    ).rejects.toMatchObject({
      _tag: 'NotFoundError',
      id: '999'
    });
  });
});
```

### 12.2.4 Testing Effect Composition

Test effects ที่ประกอบจากหลาย effects

```typescript
import { describe, it, expect } from 'vitest';
import { Effect, Layer } from 'effect';
import { TodoApi } from '@/services/TodoApi';
import { Logger } from '@/services/Logger';
import { createTodo } from '../todos';

describe('createTodo', () => {
  it('should create todo with logging', async () => {
    // Track logged messages
    const logs: string[] = [];

    const MockTodoApi = Layer.succeed(
      TodoApi,
      TodoApi.of({
        create: (request) =>
          Effect.succeed({
            id: '123',
            title: request.title,
            completed: false,
            createdAt: new Date()
          }),
        // ... other methods
      })
    );

    const MockLogger = Layer.succeed(
      Logger,
      Logger.of({
        debug: (msg) => Effect.sync(() => logs.push(`DEBUG: ${msg}`)),
        info: (msg) => Effect.sync(() => logs.push(`INFO: ${msg}`)),
        warn: (msg) => Effect.sync(() => logs.push(`WARN: ${msg}`)),
        error: (msg) => Effect.sync(() => logs.push(`ERROR: ${msg}`))
      })
    );

    const AppLayer = Layer.mergeAll(MockTodoApi, MockLogger);

    const result = await Effect.runPromise(
      createTodo({ title: 'New Todo' }).pipe(Effect.provide(AppLayer))
    );

    expect(result.title).toBe('New Todo');
    expect(logs).toContain('INFO: Creating todo: New Todo');
    expect(logs).toContain('INFO: Created todo 123');
  });

  it('should validate title length', async () => {
    const MockLayer = Layer.succeed(TodoApi, TodoApi.of({}));

    // Empty title
    await expect(
      Effect.runPromise(
        createTodo({ title: '' }).pipe(Effect.provide(MockLayer))
      )
    ).rejects.toThrow('Title cannot be empty');

    // Too long title
    await expect(
      Effect.runPromise(
        createTodo({ title: 'x'.repeat(201) }).pipe(Effect.provide(MockLayer))
      )
    ).rejects.toThrow('Title too long');
  });
});
```

---

## 12.3 Testing React Components

### 12.3.1 Setup Testing Environment

**Install dependencies:**

```bash
npm install -D vitest @testing-library/react @testing-library/user-event @testing-library/jest-dom jsdom
```

**vitest.config.ts:**

```typescript
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import path from 'path';

export default defineConfig({
  plugins: [react()],
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts']
  },
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src')
    }
  }
});
```

**src/test/setup.ts:**

```typescript
import '@testing-library/jest-dom';
import { cleanup } from '@testing-library/react';
import { afterEach } from 'vitest';

// Cleanup after each test
afterEach(() => {
  cleanup();
});
```

### 12.3.2 Testing Simple Components

**Component:**

```typescript
// src/components/TodoItem.tsx
import React from 'react';
import type { Todo } from '@/domain/Todo';

interface TodoItemProps {
  todo: Todo;
  onToggle: (id: string) => void;
  onDelete: (id: string) => void;
}

export function TodoItem({ todo, onToggle, onDelete }: TodoItemProps) {
  return (
    <div className="todo-item" data-testid="todo-item">
      <input
        type="checkbox"
        checked={todo.completed}
        onChange={() => onToggle(todo.id)}
        aria-label="Toggle todo"
      />
      <span className={todo.completed ? 'completed' : ''}>
        {todo.title}
      </span>
      <button onClick={() => onDelete(todo.id)}>Delete</button>
    </div>
  );
}
```

**Test:**

```typescript
// src/components/__tests__/TodoItem.test.tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TodoItem } from '../TodoItem';

describe('TodoItem', () => {
  const mockTodo = {
    id: '1',
    title: 'Test Todo',
    completed: false,
    createdAt: new Date()
  };

  it('should render todo title', () => {
    render(
      <TodoItem
        todo={mockTodo}
        onToggle={vi.fn()}
        onDelete={vi.fn()}
      />
    );

    expect(screen.getByText('Test Todo')).toBeInTheDocument();
  });

  it('should call onToggle when checkbox clicked', async () => {
    const user = userEvent.setup();
    const onToggle = vi.fn();

    render(
      <TodoItem
        todo={mockTodo}
        onToggle={onToggle}
        onDelete={vi.fn()}
      />
    );

    const checkbox = screen.getByRole('checkbox');
    await user.click(checkbox);

    expect(onToggle).toHaveBeenCalledWith('1');
    expect(onToggle).toHaveBeenCalledTimes(1);
  });

  it('should call onDelete when delete button clicked', async () => {
    const user = userEvent.setup();
    const onDelete = vi.fn();

    render(
      <TodoItem
        todo={mockTodo}
        onToggle={vi.fn()}
        onDelete={onDelete}
      />
    );

    const deleteButton = screen.getByText('Delete');
    await user.click(deleteButton);

    expect(onDelete).toHaveBeenCalledWith('1');
  });

  it('should show completed style when todo is completed', () => {
    const completedTodo = { ...mockTodo, completed: true };

    render(
      <TodoItem
        todo={completedTodo}
        onToggle={vi.fn()}
        onDelete={vi.fn()}
      />
    );

    const title = screen.getByText('Test Todo');
    expect(title).toHaveClass('completed');
  });
});
```

### 12.3.3 Testing Components with Effects

Components ที่ใช้ Effect-TS ต้อง provide mock layers

**Component:**

```typescript
// src/components/TodoList.tsx
import React, { useEffect } from 'react';
import { Effect } from 'effect';
import { useTodoStore, useTodoStoreState } from '@/contexts/TodoStoreContext';
import { useEffectLayer } from '@/contexts/EffectContext';
import { TodoItem } from './TodoItem';

export function TodoList() {
  const store = useTodoStore();
  const layer = useEffectLayer();
  const { todos, loading, error } = useTodoStoreState();

  useEffect(() => {
    Effect.runPromise(store.load.pipe(Effect.provide(layer)));
  }, [store, layer]);

  if (loading) return <div>Loading...</div>;
  if (error) return <div>Error: {error.message}</div>;

  return (
    <div data-testid="todo-list">
      {todos.map(todo => (
        <TodoItem
          key={todo.id}
          todo={todo}
          onToggle={(id) => {
            Effect.runPromise(store.toggle(id).pipe(Effect.provide(layer)));
          }}
          onDelete={(id) => {
            Effect.runPromise(store.delete(id).pipe(Effect.provide(layer)));
          }}
        />
      ))}
    </div>
  );
}
```

**Test:**

```typescript
// src/components/__tests__/TodoList.test.tsx
import { describe, it, expect } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Effect, Layer } from 'effect';
import { EffectProvider } from '@/contexts/EffectContext';
import { TodoStoreProvider } from '@/contexts/TodoStoreContext';
import { TodoApi } from '@/services/TodoApi';
import { Logger } from '@/services/Logger';
import { TodoList } from '../TodoList';

describe('TodoList', () => {
  const mockTodos = [
    { id: '1', title: 'Todo 1', completed: false, createdAt: new Date() },
    { id: '2', title: 'Todo 2', completed: true, createdAt: new Date() }
  ];

  const createTestLayer = () => {
    const MockTodoApi = Layer.succeed(
      TodoApi,
      TodoApi.of({
        fetchAll: () => Effect.succeed(mockTodos),
        toggle: (id) =>
          Effect.succeed({
            ...mockTodos.find(t => t.id === id)!,
            completed: !mockTodos.find(t => t.id === id)!.completed
          }),
        delete: () => Effect.succeed(undefined),
        getById: () => Effect.fail(new Error('Not implemented')),
        create: () => Effect.fail(new Error('Not implemented')),
        update: () => Effect.fail(new Error('Not implemented'))
      })
    );

    const MockLogger = Layer.succeed(
      Logger,
      Logger.of({
        debug: () => Effect.succeed(undefined),
        info: () => Effect.succeed(undefined),
        warn: () => Effect.succeed(undefined),
        error: () => Effect.succeed(undefined)
      })
    );

    return Layer.mergeAll(MockTodoApi, MockLogger);
  };

  it('should render todos after loading', async () => {
    const TestLayer = createTestLayer();

    render(
      <EffectProvider layer={TestLayer}>
        <TodoStoreProvider>
          <TodoList />
        </TodoStoreProvider>
      </EffectProvider>
    );

    // Should show loading initially
    expect(screen.getByText('Loading...')).toBeInTheDocument();

    // Wait for todos to load
    await waitFor(() => {
      expect(screen.getByText('Todo 1')).toBeInTheDocument();
    });

    expect(screen.getByText('Todo 2')).toBeInTheDocument();
  });

  it('should toggle todo when checkbox clicked', async () => {
    const user = userEvent.setup();
    const TestLayer = createTestLayer();

    render(
      <EffectProvider layer={TestLayer}>
        <TodoStoreProvider>
          <TodoList />
        </TodoStoreProvider>
      </EffectProvider>
    );

    await waitFor(() => {
      expect(screen.getByText('Todo 1')).toBeInTheDocument();
    });

    const checkboxes = screen.getAllByRole('checkbox');
    await user.click(checkboxes[0]);

    // Verify toggle was called (implementation specific)
    // In real app, you might verify UI change or state update
  });

  it('should handle empty todo list', async () => {
    const EmptyLayer = Layer.succeed(
      TodoApi,
      TodoApi.of({
        fetchAll: () => Effect.succeed([]),
        // ... other methods
      })
    );

    render(
      <EffectProvider layer={EmptyLayer}>
        <TodoStoreProvider>
          <TodoList />
        </TodoStoreProvider>
      </EffectProvider>
    );

    await waitFor(() => {
      expect(screen.queryByText('Loading...')).not.toBeInTheDocument();
    });

    const todoList = screen.getByTestId('todo-list');
    expect(todoList.children).toHaveLength(0);
  });

  it('should show error message on failure', async () => {
    const ErrorLayer = Layer.succeed(
      TodoApi,
      TodoApi.of({
        fetchAll: () => Effect.fail(new Error('Network error')),
        // ... other methods
      })
    );

    render(
      <EffectProvider layer={ErrorLayer}>
        <TodoStoreProvider>
          <TodoList />
        </TodoStoreProvider>
      </EffectProvider>
    );

    await waitFor(() => {
      expect(screen.getByText(/Error: Network error/)).toBeInTheDocument();
    });
  });
});
```

---

## 12.4 Testing Forms and Validation

### 12.4.1 Testing Schema Validation

Test schema validation แยกจาก UI

```typescript
// src/schemas/__tests__/TodoSchema.test.ts
import { describe, it, expect } from 'vitest';
import { Schema } from '@effect/schema';
import { Effect, Either } from 'effect';
import { TodoFormSchema } from '../TodoSchema';

describe('TodoFormSchema', () => {
  const decode = Schema.decode(TodoFormSchema);

  it('should validate valid todo form', async () => {
    const validInput = {
      title: 'Valid Todo',
      description: 'A valid description',
      priority: 'medium' as const,
      dueDate: new Date(Date.now() + 24 * 60 * 60 * 1000),
      tags: ['work', 'important']
    };

    const result = await Effect.runPromise(
      Effect.either(decode(validInput))
    );

    expect(Either.isRight(result)).toBe(true);
  });

  it('should reject title too short', async () => {
    const invalidInput = {
      title: 'ab', // Too short
      priority: 'medium' as const,
      dueDate: new Date(Date.now() + 24 * 60 * 60 * 1000),
      tags: []
    };

    const result = await Effect.runPromise(
      Effect.either(decode(invalidInput))
    );

    expect(Either.isLeft(result)).toBe(true);
    if (Either.isLeft(result)) {
      const error = result.left;
      expect(error.message).toContain('at least 3 characters');
    }
  });

  it('should reject past due date', async () => {
    const invalidInput = {
      title: 'Valid Title',
      priority: 'medium' as const,
      dueDate: new Date(Date.now() - 24 * 60 * 60 * 1000), // Yesterday
      tags: []
    };

    const result = await Effect.runPromise(
      Effect.either(decode(invalidInput))
    );

    expect(Either.isLeft(result)).toBe(true);
  });

  it('should reject too many tags', async () => {
    const invalidInput = {
      title: 'Valid Title',
      priority: 'medium' as const,
      dueDate: new Date(Date.now() + 24 * 60 * 60 * 1000),
      tags: ['tag1', 'tag2', 'tag3', 'tag4', 'tag5', 'tag6'] // Too many
    };

    const result = await Effect.runPromise(
      Effect.either(decode(invalidInput))
    );

    expect(Either.isLeft(result)).toBe(true);
  });
});
```

### 12.4.2 Testing Form Components

```typescript
// src/components/__tests__/CreateTodoForm.test.tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Effect, Layer } from 'effect';
import { EffectProvider } from '@/contexts/EffectContext';
import { TodoStoreProvider } from '@/contexts/TodoStoreContext';
import { TodoApi } from '@/services/TodoApi';
import { CreateTodoForm } from '../CreateTodoForm';

describe('CreateTodoForm', () => {
  const createMockLayer = (createFn = vi.fn()) => {
    return Layer.succeed(
      TodoApi,
      TodoApi.of({
        create: (request) => {
          createFn(request);
          return Effect.succeed({
            id: '123',
            title: request.title,
            completed: false,
            createdAt: new Date()
          });
        },
        // ... other methods
      })
    );
  };

  it('should render form fields', () => {
    const MockLayer = createMockLayer();

    render(
      <EffectProvider layer={MockLayer}>
        <TodoStoreProvider>
          <CreateTodoForm />
        </TodoStoreProvider>
      </EffectProvider>
    );

    expect(screen.getByLabelText(/title/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/description/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/priority/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/due date/i)).toBeInTheDocument();
  });

  it('should submit form with valid data', async () => {
    const user = userEvent.setup();
    const createFn = vi.fn();
    const MockLayer = createMockLayer(createFn);

    render(
      <EffectProvider layer={MockLayer}>
        <TodoStoreProvider>
          <CreateTodoForm />
        </TodoStoreProvider>
      </EffectProvider>
    );

    // Fill form
    await user.type(screen.getByLabelText(/title/i), 'New Todo');
    await user.selectOptions(screen.getByLabelText(/priority/i), 'high');

    // Submit
    await user.click(screen.getByText(/create todo/i));

    await waitFor(() => {
      expect(createFn).toHaveBeenCalledWith(
        expect.objectContaining({
          title: 'New Todo'
        })
      );
    });
  });

  it('should show validation error for empty title', async () => {
    const user = userEvent.setup();
    const MockLayer = createMockLayer();

    render(
      <EffectProvider layer={MockLayer}>
        <TodoStoreProvider>
          <CreateTodoForm />
        </TodoStoreProvider>
      </EffectProvider>
    );

    // Try to submit without filling title
    await user.click(screen.getByText(/create todo/i));

    await waitFor(() => {
      expect(screen.getByText(/title must be at least 3 characters/i))
        .toBeInTheDocument();
    });
  });

  it('should reset form after successful submit', async () => {
    const user = userEvent.setup();
    const MockLayer = createMockLayer();

    render(
      <EffectProvider layer={MockLayer}>
        <TodoStoreProvider>
          <CreateTodoForm />
        </TodoStoreProvider>
      </EffectProvider>
    );

    const titleInput = screen.getByLabelText(/title/i) as HTMLInputElement;

    // Fill and submit
    await user.type(titleInput, 'New Todo');
    await user.click(screen.getByText(/create todo/i));

    // Wait for reset
    await waitFor(() => {
      expect(titleInput.value).toBe('');
    });
  });
});
```

---

## 12.5 Testing Custom Hooks

### 12.5.1 Testing useRunEffect

```typescript
// src/hooks/__tests__/useRunEffect.test.ts
import { describe, it, expect } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { Effect, Layer } from 'effect';
import { EffectProvider } from '@/contexts/EffectContext';
import { useRunEffect } from '../useRunEffect';

describe('useRunEffect', () => {
  const wrapper = ({ children }: { children: React.ReactNode }) => (
    <EffectProvider>{children}</EffectProvider>
  );

  it('should run effect and return data', async () => {
    const { result } = renderHook(
      () => useRunEffect(() => Effect.succeed(42), []),
      { wrapper }
    );

    // Initially loading
    expect(result.current.loading).toBe(true);
    expect(result.current.data).toBe(null);

    // Wait for effect to complete
    await waitFor(() => {
      expect(result.current.loading).toBe(false);
    });

    expect(result.current.data).toBe(42);
    expect(result.current.error).toBe(null);
  });

  it('should handle effect errors', async () => {
    const { result } = renderHook(
      () =>
        useRunEffect(
          () => Effect.fail(new Error('Test error')),
          []
        ),
      { wrapper }
    );

    await waitFor(() => {
      expect(result.current.loading).toBe(false);
    });

    expect(result.current.data).toBe(null);
    expect(result.current.error).toMatchObject({
      message: 'Test error'
    });
  });

  it('should refetch when refetch is called', async () => {
    let callCount = 0;

    const { result } = renderHook(
      () =>
        useRunEffect(() => {
          callCount++;
          return Effect.succeed(callCount);
        }, []),
      { wrapper }
    );

    await waitFor(() => {
      expect(result.current.loading).toBe(false);
    });

    expect(result.current.data).toBe(1);

    // Trigger refetch
    result.current.refetch();

    await waitFor(() => {
      expect(result.current.data).toBe(2);
    });
  });

  it('should re-run effect when deps change', async () => {
    let value = 1;

    const { result, rerender } = renderHook(
      ({ deps }) => useRunEffect(() => Effect.succeed(value), deps),
      {
        wrapper,
        initialProps: { deps: [value] }
      }
    );

    await waitFor(() => {
      expect(result.current.data).toBe(1);
    });

    // Change deps
    value = 2;
    rerender({ deps: [value] });

    await waitFor(() => {
      expect(result.current.data).toBe(2);
    });
  });
});
```

### 12.5.2 Testing useFormSchema

```typescript
// src/hooks/__tests__/useFormSchema.test.ts
import { describe, it, expect, vi } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';
import { Schema } from '@effect/schema';
import { Effect } from 'effect';
import { useFormSchema } from '../useFormSchema';

const TestSchema = Schema.struct({
  name: Schema.string.pipe(
    Schema.minLength(3, { message: () => 'Name too short' })
  ),
  age: Schema.number.pipe(
    Schema.greaterThan(0, { message: () => 'Age must be positive' })
  )
});

describe('useFormSchema', () => {
  it('should initialize with initial values', () => {
    const { result } = renderHook(() =>
      useFormSchema({
        schema: TestSchema,
        initialValues: { name: 'John', age: 25 },
        onSubmit: () => Effect.succeed(undefined)
      })
    );

    expect(result.current.values).toEqual({ name: 'John', age: 25 });
    expect(result.current.errors).toEqual({});
    expect(result.current.submitting).toBe(false);
  });

  it('should update field values', () => {
    const { result } = renderHook(() =>
      useFormSchema({
        schema: TestSchema,
        initialValues: { name: '', age: 0 },
        onSubmit: () => Effect.succeed(undefined)
      })
    );

    act(() => {
      result.current.setValue('name', 'Alice');
    });

    expect(result.current.values.name).toBe('Alice');
  });

  it('should validate on submit', async () => {
    const onSubmit = vi.fn(() => Effect.succeed(undefined));

    const { result } = renderHook(() =>
      useFormSchema({
        schema: TestSchema,
        initialValues: { name: 'ab', age: 25 }, // Invalid name
        onSubmit
      })
    );

    await act(async () => {
      await result.current.handleSubmit();
    });

    await waitFor(() => {
      expect(result.current.errors.name).toBe('Name too short');
    });

    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('should submit valid data', async () => {
    const onSubmit = vi.fn(() => Effect.succeed(undefined));

    const { result } = renderHook(() =>
      useFormSchema({
        schema: TestSchema,
        initialValues: { name: 'Alice', age: 25 },
        onSubmit
      })
    );

    await act(async () => {
      await result.current.handleSubmit();
    });

    await waitFor(() => {
      expect(onSubmit).toHaveBeenCalledWith({ name: 'Alice', age: 25 });
    });
  });

  it('should reset form', () => {
    const initialValues = { name: 'John', age: 25 };

    const { result } = renderHook(() =>
      useFormSchema({
        schema: TestSchema,
        initialValues,
        onSubmit: () => Effect.succeed(undefined)
      })
    );

    // Change values
    act(() => {
      result.current.setValue('name', 'Alice');
      result.current.setValue('age', 30);
    });

    expect(result.current.values).toEqual({ name: 'Alice', age: 30 });

    // Reset
    act(() => {
      result.current.reset();
    });

    expect(result.current.values).toEqual(initialValues);
    expect(result.current.errors).toEqual({});
  });
});
```

---

## 12.6 Integration Testing

### 12.6.1 Integration Test Concept

Integration tests ทดสอบหลาย components/services ทำงานร่วมกัน

**Scope:**
- Test user workflows
- Use real Effect implementations (บางส่วน)
- Mock external APIs
- Test state management

### 12.6.2 Todo App Integration Test

```typescript
// src/__tests__/integration/TodoApp.test.tsx
import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { EffectProvider } from '@/contexts/EffectContext';
import { TodoStoreProvider } from '@/contexts/TodoStoreContext';
import { AppLayerMock } from '@/layers';
import { TodoApp } from '@/components/TodoApp';

describe('TodoApp Integration', () => {
  beforeEach(() => {
    // Reset any global state if needed
  });

  it('should complete full todo workflow', async () => {
    const user = userEvent.setup();

    render(
      <EffectProvider layer={AppLayerMock}>
        <TodoStoreProvider>
          <TodoApp />
        </TodoStoreProvider>
      </EffectProvider>
    );

    // 1. Wait for initial todos to load
    await waitFor(() => {
      expect(screen.getByText('Learn Effect-TS')).toBeInTheDocument();
    });

    // 2. Create new todo
    const input = screen.getByPlaceholderText(/what needs to be done/i);
    await user.type(input, 'Write tests');
    await user.click(screen.getByText(/add/i));

    // 3. Verify todo was added
    await waitFor(() => {
      expect(screen.getByText('Write tests')).toBeInTheDocument();
    });

    // 4. Toggle todo
    const checkboxes = screen.getAllByRole('checkbox');
    const newTodoCheckbox = checkboxes.find(
      (cb) => cb.parentElement?.textContent?.includes('Write tests')
    );
    await user.click(newTodoCheckbox!);

    // 5. Verify todo was toggled
    await waitFor(() => {
      expect(newTodoCheckbox).toBeChecked();
    });

    // 6. Filter completed todos
    await user.click(screen.getByText(/completed/i));

    await waitFor(() => {
      expect(screen.getByText('Write tests')).toBeInTheDocument();
      expect(screen.queryByText('Learn Effect-TS')).not.toBeInTheDocument();
    });

    // 7. Delete todo
    const deleteButtons = screen.getAllByText(/delete/i);
    await user.click(deleteButtons[0]);

    await waitFor(() => {
      expect(screen.queryByText('Write tests')).not.toBeInTheDocument();
    });
  });

  it('should handle errors gracefully', async () => {
    // Test with error-throwing layer
    // ...
  });
});
```

---

## 12.7 Test Utilities

### 12.7.1 Custom Render Function

สร้าง custom render ที่ wrap providers อัตโนมัติ

```typescript
// src/test/test-utils.tsx
import React from 'react';
import { render, RenderOptions } from '@testing-library/react';
import { Layer } from 'effect';
import { EffectProvider } from '@/contexts/EffectContext';
import { TodoStoreProvider } from '@/contexts/TodoStoreContext';
import { AppLayerMock } from '@/layers';

interface CustomRenderOptions extends Omit<RenderOptions, 'wrapper'> {
  layer?: Layer.Layer<any, never, never>;
}

export function renderWithProviders(
  ui: React.ReactElement,
  options?: CustomRenderOptions
) {
  const { layer = AppLayerMock, ...renderOptions } = options || {};

  function Wrapper({ children }: { children: React.ReactNode }) {
    return (
      <EffectProvider layer={layer}>
        <TodoStoreProvider>
          {children}
        </TodoStoreProvider>
      </EffectProvider>
    );
  }

  return render(ui, { wrapper: Wrapper, ...renderOptions });
}

// Re-export everything
export * from '@testing-library/react';
export { renderWithProviders as render };
```

**Usage:**

```typescript
import { render, screen } from '@/test/test-utils';
import { TodoList } from '@/components/TodoList';

it('should render todos', async () => {
  render(<TodoList />);

  await waitFor(() => {
    expect(screen.getByText('Test Todo')).toBeInTheDocument();
  });
});
```

### 12.7.2 Test Data Factories

```typescript
// src/test/factories.ts
import type { Todo } from '@/domain/Todo';

export const createMockTodo = (overrides?: Partial<Todo>): Todo => ({
  id: Math.random().toString(),
  title: 'Test Todo',
  completed: false,
  createdAt: new Date(),
  ...overrides
});

export const createMockTodos = (count: number): Todo[] =>
  Array.from({ length: count }, (_, i) =>
    createMockTodo({
      id: String(i + 1),
      title: `Todo ${i + 1}`,
      completed: i % 2 === 0
    })
  );
```

**Usage:**

```typescript
import { createMockTodo, createMockTodos } from '@/test/factories';

it('should render multiple todos', () => {
  const todos = createMockTodos(5);
  // Use todos in test
});
```

---

## 12.8 Best Practices

### 12.8.1 Test Organization

**DO: Group related tests**

```typescript
// ✅ Good: Organized by feature
describe('TodoList', () => {
  describe('rendering', () => {
    it('should render todos');
    it('should show loading state');
    it('should show empty state');
  });

  describe('interactions', () => {
    it('should toggle todo');
    it('should delete todo');
  });

  describe('error handling', () => {
    it('should show error message');
    it('should retry on error');
  });
});
```

### 12.8.2 Test Independence

**DO: Each test should be independent**

```typescript
// ✅ Good: Independent tests
describe('TodoStore', () => {
  it('should create todo', async () => {
    const store = await createStore();
    // Test create
  });

  it('should delete todo', async () => {
    const store = await createStore();
    // Test delete (separate instance)
  });
});
```

**DON'T: Tests depend on each other**

```typescript
// ❌ Bad: Dependent tests
describe('TodoStore', () => {
  let store: TodoStore;

  it('should create todo', async () => {
    store = await createStore();
    // Create todo
  });

  it('should delete todo', async () => {
    // Depends on previous test!
    // Delete todo from previous test
  });
});
```

### 12.8.3 Meaningful Test Names

**DO: Descriptive test names**

```typescript
// ✅ Good: Clear what is being tested
it('should show error message when API call fails');
it('should disable submit button while form is submitting');
it('should validate email format and show error for invalid email');
```

**DON'T: Vague test names**

```typescript
// ❌ Bad: Unclear
it('works');
it('handles error');
it('test1');
```

### 12.8.4 AAA Pattern

**Arrange-Act-Assert pattern:**

```typescript
it('should create todo with valid data', async () => {
  // Arrange: Setup test data and mocks
  const user = userEvent.setup();
  const createFn = vi.fn();
  const MockLayer = createMockLayer(createFn);

  render(
    <EffectProvider layer={MockLayer}>
      <CreateTodoForm />
    </EffectProvider>
  );

  // Act: Perform action
  await user.type(screen.getByLabelText(/title/i), 'New Todo');
  await user.click(screen.getByText(/create/i));

  // Assert: Verify result
  await waitFor(() => {
    expect(createFn).toHaveBeenCalledWith(
      expect.objectContaining({ title: 'New Todo' })
    );
  });
});
```

---

## 12.9 Test Coverage

### 12.9.1 Measuring Coverage

```bash
# Run tests with coverage
npm run test -- --coverage
```

**vitest.config.ts:**

```typescript
export default defineConfig({
  test: {
    coverage: {
      provider: 'v8',
      reporter: ['text', 'json', 'html'],
      exclude: [
        'node_modules/',
        'src/test/',
        '**/*.test.ts',
        '**/*.test.tsx'
      ],
      statements: 80,
      branches: 80,
      functions: 80,
      lines: 80
    }
  }
});
```

### 12.9.2 What to Test

**High Priority (Must Test):**
- ✅ Business logic
- ✅ User interactions
- ✅ Error handling
- ✅ Edge cases
- ✅ Validation logic

**Medium Priority:**
- ⚠️ UI rendering
- ⚠️ State management
- ⚠️ Integration between components

**Low Priority (Optional):**
- 🔵 Simple getters/setters
- 🔵 Type-only code
- 🔵 Third-party library wrappers

---

## 12.10 สรุป

### สิ่งที่ได้เรียนรู้ในบทนี้

1. **Testing Philosophy**
   - Testing pyramid
   - Test independence
   - AAA pattern

2. **Unit Testing**
   - Testing pure effects
   - Testing with dependencies
   - Mock layers

3. **Component Testing**
   - Testing simple components
   - Testing with Effect-TS
   - User interactions

4. **Form Testing**
   - Schema validation tests
   - Form submission
   - Error handling

5. **Custom Hooks Testing**
   - Testing hooks with renderHook
   - Testing state changes
   - Testing side effects

6. **Integration Testing**
   - User workflows
   - Multiple components
   - State management

7. **Test Utilities**
   - Custom render functions
   - Test factories
   - Reusable mocks

### Best Practices Summary

1. **Write tests first** - TDD when possible
2. **Test behavior, not implementation** - Focus on what, not how
3. **Keep tests simple** - One assertion per test
4. **Use descriptive names** - Tests as documentation
5. **Mock external dependencies** - Fast, reliable tests
6. **Test edge cases** - Not just happy paths
7. **Maintain tests** - Keep them up to date

### ข้อดีของ Testing กับ Effect-TS

1. **Easy Mocking** - Layer system ทำให้ mock ง่าย
2. **Predictable** - Pure functions → deterministic tests
3. **Type-Safe** - Compiler helps catch errors
4. **Composable** - Test pieces independently

---

## แบบฝึกหัดท้ายบท

### ข้อ 1: Test Todo Creation

เขียน tests สำหรับ `createTodo` effect:
- Valid input
- Empty title
- Title too long
- Special characters

### ข้อ 2: Test Wizard Navigation

เขียน tests สำหรับ wizard component:
- Navigate next/previous
- Validate each step
- Cannot skip steps
- Submit final form

### ข้อ 3: Test File Upload

เขียน tests สำหรับ file upload:
- Valid file upload
- File too large
- Invalid file type
- Upload progress
- Cancel upload

### ข้อ 4: Test Optimistic Updates

เขียน tests สำหรับ optimistic updates:
- Update UI immediately
- Revert on error
- Keep update on success
- Show pending state

### ข้อ 5: Integration Test

เขียน integration test สำหรับ complete user journey:
1. Load app
2. Create 3 todos
3. Toggle 1 todo
4. Filter completed
5. Delete todo
6. Verify final state

---

**พร้อมที่จะเขียน Production-Grade Tests แล้วใช่ไหม?**
