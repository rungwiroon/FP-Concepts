# บทที่ 8: Frontend Development with Effect-TS

> "ทำให้โค้ด Frontend มี Type Safety และจัดการ Effects อย่างเป็นระบบ"

---

## 8.1 ทำไมต้องใช้ Effect-TS ใน Frontend?

### 8.1.1 ปัญหาของ Frontend Development แบบเดิม

**ปัญหาที่ 1: Async Operations ยากต่อการจัดการ**

```typescript
// ❌ Promise hell และ error handling ไม่ชัดเจน
async function loadUserDashboard(userId: string) {
  try {
    const user = await fetchUser(userId);
    try {
      const orders = await fetchOrders(user.id);
      try {
        const products = await Promise.all(
          orders.map(o => fetchProduct(o.productId))
        );
        return { user, orders, products };
      } catch (error) {
        console.error('Failed to load products', error);
        return null; // หรือจะ throw ต่อ?
      }
    } catch (error) {
      console.error('Failed to load orders', error);
      return null;
    }
  } catch (error) {
    console.error('Failed to load user', error);
    return null;
  }
}
```

**ปัญหาที่ 2: Dependency Injection ไม่มีมาตรฐาน**

```typescript
// ❌ Global dependencies หรือ prop drilling
// Global - ทดสอบยาก
const api = new ApiClient();
const logger = new Logger();

function TodoList() {
  const todos = api.fetchTodos(); // ใช้ global - mock ยาก!
}

// Prop drilling - verbose
function App() {
  const api = new ApiClient();
  return <TodoList api={api} />; // ต้องส่งผ่าน props ทุกชั้น
}
```

**ปัญหาที่ 3: Error Types ไม่ชัดเจน**

```typescript
// ❌ ไม่รู้ว่า function นี้อาจมี error อะไรบ้าง
async function fetchTodo(id: string): Promise<Todo> {
  // อาจโยน NetworkError, ValidationError, NotFoundError, ...
  // แต่ไม่เห็นจาก type signature!
}
```

### 8.1.2 Effect-TS แก้ปัญหาอย่างไร?

**แนวทาง 1: Type-safe Error Handling**

```typescript
import { Effect } from "effect";

// ✅ Error types ชัดเจนใน signature
function fetchTodo(id: string): Effect.Effect<Todo, TodoError, ApiService> {
  // Effect<Success, Error, Requirements>
  // - Success: Todo
  // - Error: TodoError (type-safe!)
  // - Requirements: ต้องการ ApiService
}
```

**แนวทาง 2: Dependency Injection แบบ Type-safe**

```typescript
import { Effect, Context } from "effect";

// ✅ Define service interface
interface ApiService {
  readonly fetchTodos: () => Effect.Effect<Todo[], ApiError, never>;
}

// Tag for DI
const ApiService = Context.GenericTag<ApiService>("ApiService");

// ใช้งาน - compiler บังคับให้ provide ApiService
const program = Effect.gen(function* (_) {
  const api = yield* _(ApiService);
  const todos = yield* _(api.fetchTodos());
  return todos;
});
```

**แนวทาง 3: Composable Effects**

```typescript
// ✅ Compose operations แบบ readable
const loadDashboard = (userId: string) =>
  Effect.gen(function* (_) {
    const user = yield* _(fetchUser(userId));
    const orders = yield* _(fetchOrders(user.id));
    const products = yield* _(
      Effect.all(orders.map(o => fetchProduct(o.productId)))
    );
    return { user, orders, products };
  });
// Error handling automatic! ถ้าขั้นตอนไหน fail จะหยุดทันที
```

### 8.1.3 เปรียบเทียบ Effect-TS กับ Backend language-ext

Effect-TS ใน Frontend คล้ายกับ language-ext ใน Backend:

| Concept | Backend (C# language-ext) | Frontend (TypeScript Effect-TS) |
|---------|---------------------------|----------------------------------|
| Effect Type | `Eff<RT, A>` | `Effect<A, E, R>` |
| Error Handling | `Fin<A>` | Built-in error channel `E` |
| Dependency Injection | `Has<M, RT, T>` | `Context.Tag<T>` |
| Composition | LINQ query syntax | `Effect.gen` syntax |
| Pure computation | `K<M, A>` | `Effect<A, never, never>` |

**ความแตกต่างหลัก:**
- language-ext: Higher-Kinded Types (HKT) ด้วย `K<M, A>`
- Effect-TS: Concrete type `Effect<A, E, R>` (ง่ายกว่า)

---

## 8.2 Effect-TS Core Concepts

### 8.2.1 Effect<A, E, R> - The Effect Type

**Effect<A, E, R>** คือ type หลักของ Effect-TS:

```typescript
Effect<Success, Error, Requirements>
```

**Parameter แต่ละตัว:**

1. **A (Success)** - ค่าที่ได้เมื่อสำเร็จ
2. **E (Error)** - Error type ที่อาจเกิดขึ้น
3. **R (Requirements)** - Dependencies ที่ต้องการ

**ตัวอย่าง:**

```typescript
import { Effect } from "effect";

// Effect ที่ไม่มี error และไม่ต้องการ dependencies
const pureEffect: Effect.Effect<number, never, never> = Effect.succeed(42);

// Effect ที่อาจมี error
const mayFail: Effect.Effect<string, Error, never> =
  Effect.fail(new Error("Something went wrong"));

// Effect ที่ต้องการ dependencies
interface Logger {
  readonly log: (msg: string) => Effect.Effect<void, never, never>;
}
const LoggerTag = Context.GenericTag<Logger>("Logger");

const needsLogger: Effect.Effect<void, never, Logger> =
  Effect.gen(function* (_) {
    const logger = yield* _(LoggerTag);
    yield* _(logger.log("Hello from Effect-TS!"));
  });
```

### 8.2.2 Creating Effects

**1. Effect.succeed - Pure value**

```typescript
const value = Effect.succeed(42);
// Effect<number, never, never>
```

**2. Effect.fail - Error**

```typescript
const error = Effect.fail(new Error("Oops"));
// Effect<never, Error, never>
```

**3. Effect.sync - Synchronous side effect**

```typescript
const now = Effect.sync(() => new Date());
// Effect<Date, never, never>

const randomNum = Effect.sync(() => Math.random());
// Effect<number, never, never>
```

**4. Effect.promise - Async operation**

```typescript
const fetchData = Effect.promise(() =>
  fetch('https://api.example.com/data').then(r => r.json())
);
// Effect<any, never, never>
```

**5. Effect.tryPromise - Async with error handling**

```typescript
const safeFetch = Effect.tryPromise({
  try: () => fetch('https://api.example.com/data').then(r => r.json()),
  catch: (error) => new FetchError(String(error))
});
// Effect<any, FetchError, never>
```

### 8.2.3 Effect.gen - Generator Syntax

`Effect.gen` ให้เราเขียนโค้ด Effect แบบ imperative style (คล้าย async/await):

**แบบ imperative (อ่านง่าย):**

```typescript
import { Effect } from "effect";

const program = Effect.gen(function* (_) {
  // ใช้ yield* _() เพื่อ "unwrap" Effect
  const user = yield* _(fetchUser(userId));
  const orders = yield* _(fetchOrders(user.id));
  const total = yield* _(calculateTotal(orders));

  return { user, orders, total };
});
```

**แบบ functional (verbose):**

```typescript
// เหมือนกันกับด้านบน แต่ยาวกว่า
const program = fetchUser(userId).pipe(
  Effect.flatMap(user =>
    fetchOrders(user.id).pipe(
      Effect.flatMap(orders =>
        calculateTotal(orders).pipe(
          Effect.map(total => ({ user, orders, total }))
        )
      )
    )
  )
);
```

**คำอธิบาย `yield* _()`:**
- `yield*` = unwrap Effect<A, E, R> → ได้ค่า A
- `_()` = helper function ที่ Effect.gen ส่งให้
- ถ้า Effect fail จะหยุดทันที (short-circuit)

### 8.2.4 Transforming Effects

**1. Effect.map - Transform success value**

```typescript
const doubled = Effect.succeed(21).pipe(
  Effect.map(x => x * 2)
);
// Effect<number, never, never> → 42
```

**2. Effect.flatMap - Chain effects**

```typescript
const program = Effect.succeed(1).pipe(
  Effect.flatMap(x => Effect.succeed(x + 1)),
  Effect.flatMap(x => Effect.succeed(x * 2))
);
// Effect<number, never, never> → 4
```

**3. Effect.catchAll - Handle errors**

```typescript
const safe = mayFailEffect.pipe(
  Effect.catchAll(error => Effect.succeed("default value"))
);
// Error channel กลายเป็น never
```

**4. Effect.tap - Side effect (คล้าย peek)**

```typescript
const logged = Effect.succeed(42).pipe(
  Effect.tap(value => Effect.sync(() => console.log(`Value: ${value}`)))
);
// ยังคืน Effect<number, never, never> แต่ log ก่อน
```

### 8.2.5 Running Effects

**1. Effect.runPromise - แปลงเป็น Promise**

```typescript
const effect = Effect.succeed(42);

// Run effect → Promise
const promise = Effect.runPromise(effect);
promise.then(value => console.log(value)); // 42
```

**2. Effect.runSync - Run synchronously**

```typescript
const effect = Effect.succeed(42);
const value = Effect.runSync(effect); // 42
// ⚠️ ใช้ได้เฉพาะ pure effects (ไม่มี async)
```

**3. Effect.runPromiseExit - รับทั้ง success และ error**

```typescript
const effect = Effect.fail(new Error("Oops"));

Effect.runPromiseExit(effect).then(exit => {
  if (exit._tag === "Success") {
    console.log("Success:", exit.value);
  } else {
    console.log("Failure:", exit.cause);
  }
});
```

---

## 8.3 Option และ Either ใน Effect-TS

### 8.3.1 Option<A> - ค่าที่อาจไม่มี

Effect-TS มี `Option` type สำหรับค่าที่อาจเป็น `Some(value)` หรือ `None`:

**Creating Options:**

```typescript
import { Option } from "effect";

// Some - มีค่า
const some: Option.Option<number> = Option.some(42);

// None - ไม่มีค่า
const none: Option.Option<number> = Option.none();

// fromNullable - แปลง null/undefined → Option
const maybeUser: Option.Option<User> = Option.fromNullable(
  users.find(u => u.id === userId)
);
```

**Pattern Matching:**

```typescript
import { Option, pipe } from "effect";

const result = pipe(
  maybeUser,
  Option.match({
    onNone: () => "User not found",
    onSome: (user) => `Hello, ${user.name}!`
  })
);
```

**Operations:**

```typescript
// Map - แปลงค่าภายใน
const length: Option.Option<number> = pipe(
  Option.some("hello"),
  Option.map(s => s.length)
); // Some(5)

// FlatMap - Chain options
const user: Option.Option<User> = Option.some({ id: 1, addressId: 10 });
const address: Option.Option<Address> = pipe(
  user,
  Option.flatMap(u => findAddress(u.addressId))
);

// Filter - กรองตามเงื่อนไข
const adult: Option.Option<User> = pipe(
  Option.some({ name: "John", age: 25 }),
  Option.filter(u => u.age >= 18)
); // Some(...)

// GetOrElse - ค่า default ถ้าเป็น None
const name: string = pipe(
  maybeUser,
  Option.map(u => u.name),
  Option.getOrElse(() => "Guest")
);
```

**Integration with Effect:**

```typescript
// แปลง Option → Effect
const userEffect: Effect.Effect<User, NoSuchElementException, never> = pipe(
  maybeUser,
  Effect.fromOption
);

// หรือกำหนด error เอง
const userEffect2: Effect.Effect<User, UserNotFoundError, never> = pipe(
  maybeUser,
  Effect.fromOption(() => new UserNotFoundError())
);
```

### 8.3.2 Either<E, A> - Success หรือ Error

`Either` แทน computation ที่อาจสำเร็จ (Right) หรือล้มเหลว (Left):

**Creating Either:**

```typescript
import { Either } from "effect";

// Right - สำเร็จ
const success: Either.Either<number, Error> = Either.right(42);

// Left - ล้มเหลว
const failure: Either.Either<number, Error> = Either.left(new Error("Oops"));
```

**Pattern Matching:**

```typescript
const result = pipe(
  validateEmail(input),
  Either.match({
    onLeft: (error) => `Error: ${error.message}`,
    onRight: (email) => `Valid email: ${email}`
  })
);
```

**Operations:**

```typescript
// Map - แปลง Right value
const doubled = pipe(
  Either.right(21),
  Either.map(x => x * 2)
); // Right(42)

// MapLeft - แปลง Left value (error)
const withContext = pipe(
  Either.left(new Error("Failed")),
  Either.mapLeft(e => new DetailedError(e, context))
);

// FlatMap - Chain Either
const result = pipe(
  parseNumber(input),
  Either.flatMap(n => divide(100, n))
);
```

**Integration with Effect:**

```typescript
// แปลง Either → Effect
const effect: Effect.Effect<number, Error, never> = pipe(
  Either.right(42),
  Effect.fromEither
);
```

**ตัวอย่างการใช้งานจริง: Form Validation**

```typescript
import { Either, pipe } from "effect";

type ValidationError =
  | { _tag: "EmptyField"; field: string }
  | { _tag: "InvalidFormat"; field: string; reason: string };

function validateEmail(email: string): Either.Either<string, ValidationError> {
  if (email.trim() === "") {
    return Either.left({ _tag: "EmptyField", field: "email" });
  }
  if (!email.includes("@")) {
    return Either.left({
      _tag: "InvalidFormat",
      field: "email",
      reason: "Missing @"
    });
  }
  return Either.right(email);
}

function validateAge(age: string): Either.Either<number, ValidationError> {
  if (age.trim() === "") {
    return Either.left({ _tag: "EmptyField", field: "age" });
  }
  const num = parseInt(age);
  if (isNaN(num) || num < 0) {
    return Either.left({
      _tag: "InvalidFormat",
      field: "age",
      reason: "Must be positive number"
    });
  }
  return Either.right(num);
}

// ใช้งาน
const emailResult = validateEmail("john@example.com");
const ageResult = validateAge("25");

// Combine results
function validateUser(email: string, age: string) {
  return pipe(
    validateEmail(email),
    Either.flatMap(validEmail =>
      pipe(
        validateAge(age),
        Either.map(validAge => ({ email: validEmail, age: validAge }))
      )
    )
  );
}
```

---

## 8.4 Context.Tag - Dependency Injection

### 8.4.1 ทำไมต้องใช้ Dependency Injection?

**ปัญหาแบบเดิม:**

```typescript
// ❌ Global dependency - ทดสอบยาก
class ApiClient {
  fetchTodos(): Promise<Todo[]> {
    return fetch('/api/todos').then(r => r.json());
  }
}

const api = new ApiClient(); // Global instance

function TodoList() {
  const [todos, setTodos] = useState<Todo[]>([]);

  useEffect(() => {
    api.fetchTodos().then(setTodos); // ใช้ global - mock ยาก!
  }, []);

  return <div>{todos.map(t => <TodoItem todo={t} />)}</div>;
}
```

**ปัญหา:**
1. ทดสอบยาก - ต้อง mock global `api`
2. ไม่สามารถใช้ implementation ต่างกันต่อ environment
3. Coupling สูง

### 8.4.2 Context.Tag - Service Definition

**Step 1: Define Service Interface**

```typescript
import { Context, Effect } from "effect";

// Service interface
interface TodoApi {
  readonly fetchTodos: () => Effect.Effect<Todo[], ApiError, never>;
  readonly createTodo: (title: string) => Effect.Effect<Todo, ApiError, never>;
  readonly updateTodo: (id: string, updates: Partial<Todo>) => Effect.Effect<Todo, ApiError, never>;
  readonly deleteTodo: (id: string) => Effect.Effect<void, ApiError, never>;
}

// Error types
class ApiError {
  readonly _tag = "ApiError";
  constructor(readonly message: string, readonly cause?: unknown) {}
}
```

**Step 2: Create Context Tag**

```typescript
// Tag สำหรับ DI
const TodoApi = Context.GenericTag<TodoApi>("TodoApi");
```

**Step 3: Use in Program**

```typescript
// ใช้งาน - compiler บังคับให้ provide TodoApi
const fetchAllTodos = Effect.gen(function* (_) {
  const api = yield* _(TodoApi);
  const todos = yield* _(api.fetchTodos());
  return todos;
});

// Type: Effect<Todo[], ApiError, TodoApi>
//                    ^success  ^error    ^requirements
```

### 8.4.3 Service Implementation

**Implementation 1: Real API**

```typescript
import { Effect, Layer } from "effect";

const TodoApiLive = Layer.succeed(
  TodoApi,
  TodoApi.of({
    fetchTodos: () =>
      Effect.tryPromise({
        try: () => fetch('/api/todos').then(r => r.json()),
        catch: (error) => new ApiError("Failed to fetch todos", error)
      }),

    createTodo: (title: string) =>
      Effect.tryPromise({
        try: () =>
          fetch('/api/todos', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ title })
          }).then(r => r.json()),
        catch: (error) => new ApiError("Failed to create todo", error)
      }),

    updateTodo: (id: string, updates: Partial<Todo>) =>
      Effect.tryPromise({
        try: () =>
          fetch(`/api/todos/${id}`, {
            method: 'PATCH',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(updates)
          }).then(r => r.json()),
        catch: (error) => new ApiError("Failed to update todo", error)
      }),

    deleteTodo: (id: string) =>
      Effect.tryPromise({
        try: () =>
          fetch(`/api/todos/${id}`, { method: 'DELETE' }).then(() => {}),
        catch: (error) => new ApiError("Failed to delete todo", error)
      })
  })
);
```

**Implementation 2: Mock for Testing**

```typescript
const TodoApiMock = Layer.succeed(
  TodoApi,
  TodoApi.of({
    fetchTodos: () => Effect.succeed([
      { id: '1', title: 'Test Todo 1', completed: false },
      { id: '2', title: 'Test Todo 2', completed: true }
    ]),

    createTodo: (title: string) => Effect.succeed({
      id: Math.random().toString(),
      title,
      completed: false
    }),

    updateTodo: (id: string, updates: Partial<Todo>) => Effect.succeed({
      id,
      title: updates.title ?? 'Updated',
      completed: updates.completed ?? false
    }),

    deleteTodo: (id: string) => Effect.succeed(undefined)
  })
);
```

### 8.4.4 Providing Dependencies

**Provide ด้วย Layer:**

```typescript
// Provide TodoApiLive implementation
const program = fetchAllTodos.pipe(
  Effect.provide(TodoApiLive)
);

// ตอนนี้ program มี type: Effect<Todo[], ApiError, never>
// Requirements (TodoApi) หายไปแล้ว!
```

**Multiple Dependencies:**

```typescript
import { Layer } from "effect";

// Define services
const Logger = Context.GenericTag<Logger>("Logger");
const Database = Context.GenericTag<Database>("Database");

// Create layers
const LoggerLive = Layer.succeed(Logger, createLogger());
const DatabaseLive = Layer.succeed(Database, createDatabase());

// Combine layers
const AppLayer = Layer.mergeAll(
  LoggerLive,
  DatabaseLive,
  TodoApiLive
);

// Provide all at once
const program = myComplexProgram.pipe(
  Effect.provide(AppLayer)
);
```

---

## 8.5 Practical Example - Frontend Todo App

### 8.5.1 Project Structure

```
src/
├── services/
│   ├── TodoApi.ts        # API service definition
│   ├── Logger.ts         # Logger service
│   └── index.ts          # Export all services
├── layers/
│   ├── TodoApiLive.ts    # Real implementation
│   ├── TodoApiMock.ts    # Mock implementation
│   ├── LoggerLive.ts
│   └── index.ts
├── effects/
│   ├── todos.ts          # Todo-related effects
│   └── index.ts
├── components/
│   ├── TodoList.tsx      # React components
│   ├── TodoItem.tsx
│   └── AddTodoForm.tsx
└── App.tsx
```

### 8.5.2 Service Definitions

**services/TodoApi.ts:**

```typescript
import { Context, Effect } from "effect";

// Domain types
export interface Todo {
  readonly id: string;
  readonly title: string;
  readonly completed: boolean;
  readonly createdAt: Date;
}

// Error types
export class TodoNotFoundError {
  readonly _tag = "TodoNotFoundError";
  constructor(readonly id: string) {}
}

export class ApiError {
  readonly _tag = "ApiError";
  constructor(readonly message: string, readonly cause?: unknown) {}
}

export type TodoError = TodoNotFoundError | ApiError;

// Service interface
export interface TodoApi {
  readonly fetchTodos: () => Effect.Effect<Todo[], ApiError, never>;
  readonly getTodo: (id: string) => Effect.Effect<Todo, TodoError, never>;
  readonly createTodo: (title: string) => Effect.Effect<Todo, ApiError, never>;
  readonly updateTodo: (
    id: string,
    updates: Partial<Omit<Todo, 'id' | 'createdAt'>>
  ) => Effect.Effect<Todo, TodoError, never>;
  readonly deleteTodo: (id: string) => Effect.Effect<void, TodoError, never>;
}

// Context Tag
export const TodoApi = Context.GenericTag<TodoApi>("@services/TodoApi");
```

**services/Logger.ts:**

```typescript
import { Context, Effect } from "effect";

export interface Logger {
  readonly debug: (message: string, data?: unknown) => Effect.Effect<void, never, never>;
  readonly info: (message: string, data?: unknown) => Effect.Effect<void, never, never>;
  readonly warn: (message: string, data?: unknown) => Effect.Effect<void, never, never>;
  readonly error: (message: string, error?: unknown) => Effect.Effect<void, never, never>;
}

export const Logger = Context.GenericTag<Logger>("@services/Logger");
```

### 8.5.3 Live Implementations

**layers/TodoApiLive.ts:**

```typescript
import { Effect, Layer } from "effect";
import { TodoApi, Todo, TodoNotFoundError, ApiError } from "../services/TodoApi";

const API_BASE = "/api";

function handleResponse<A>(promise: Promise<Response>): Effect.Effect<A, ApiError, never> {
  return Effect.tryPromise({
    try: async () => {
      const response = await promise;
      if (!response.ok) {
        throw new Error(`HTTP ${response.status}: ${response.statusText}`);
      }
      return response.json();
    },
    catch: (error) => new ApiError(
      "API request failed",
      error
    )
  });
}

export const TodoApiLive = Layer.succeed(
  TodoApi,
  TodoApi.of({
    fetchTodos: () =>
      handleResponse<Todo[]>(
        fetch(`${API_BASE}/todos`)
      ),

    getTodo: (id: string) =>
      handleResponse<Todo>(
        fetch(`${API_BASE}/todos/${id}`)
      ).pipe(
        Effect.flatMap(todo =>
          todo
            ? Effect.succeed(todo)
            : Effect.fail(new TodoNotFoundError(id))
        )
      ),

    createTodo: (title: string) =>
      handleResponse<Todo>(
        fetch(`${API_BASE}/todos`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ title, completed: false })
        })
      ),

    updateTodo: (id: string, updates) =>
      handleResponse<Todo>(
        fetch(`${API_BASE}/todos/${id}`, {
          method: "PATCH",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify(updates)
        })
      ).pipe(
        Effect.flatMap(todo =>
          todo
            ? Effect.succeed(todo)
            : Effect.fail(new TodoNotFoundError(id))
        )
      ),

    deleteTodo: (id: string) =>
      Effect.tryPromise({
        try: async () => {
          const response = await fetch(`${API_BASE}/todos/${id}`, {
            method: "DELETE"
          });
          if (response.status === 404) {
            throw new TodoNotFoundError(id);
          }
          if (!response.ok) {
            throw new Error(`HTTP ${response.status}`);
          }
        },
        catch: (error) => {
          if (error instanceof TodoNotFoundError) {
            return error;
          }
          return new ApiError("Failed to delete todo", error);
        }
      })
  })
);
```

**layers/LoggerLive.ts:**

```typescript
import { Effect, Layer } from "effect";
import { Logger } from "../services/Logger";

export const LoggerLive = Layer.succeed(
  Logger,
  Logger.of({
    debug: (message, data) =>
      Effect.sync(() => {
        console.debug(`[DEBUG] ${message}`, data);
      }),

    info: (message, data) =>
      Effect.sync(() => {
        console.info(`[INFO] ${message}`, data);
      }),

    warn: (message, data) =>
      Effect.sync(() => {
        console.warn(`[WARN] ${message}`, data);
      }),

    error: (message, error) =>
      Effect.sync(() => {
        console.error(`[ERROR] ${message}`, error);
      })
  })
);
```

### 8.5.4 Business Logic Effects

**effects/todos.ts:**

```typescript
import { Effect } from "effect";
import { TodoApi, Todo, TodoError } from "../services/TodoApi";
import { Logger } from "../services/Logger";

// Fetch all todos with logging
export const fetchAllTodos = Effect.gen(function* (_) {
  const api = yield* _(TodoApi);
  const logger = yield* _(Logger);

  yield* _(logger.info("Fetching all todos"));

  const todos = yield* _(api.fetchTodos());

  yield* _(logger.info(`Fetched ${todos.length} todos`));

  return todos;
});

// Create todo with validation
export const createTodo = (title: string) =>
  Effect.gen(function* (_) {
    const api = yield* _(TodoApi);
    const logger = yield* _(Logger);

    // Validation
    const trimmed = title.trim();
    if (trimmed.length === 0) {
      return yield* _(Effect.fail(new Error("Title cannot be empty")));
    }
    if (trimmed.length > 200) {
      return yield* _(Effect.fail(new Error("Title too long (max 200 chars)")));
    }

    yield* _(logger.info(`Creating todo: ${trimmed}`));

    const todo = yield* _(api.createTodo(trimmed));

    yield* _(logger.info(`Created todo with id: ${todo.id}`));

    return todo;
  });

// Toggle todo completion
export const toggleTodo = (id: string, currentCompleted: boolean) =>
  Effect.gen(function* (_) {
    const api = yield* _(TodoApi);
    const logger = yield* _(Logger);

    yield* _(logger.info(`Toggling todo ${id}: ${currentCompleted} → ${!currentCompleted}`));

    const updated = yield* _(
      api.updateTodo(id, { completed: !currentCompleted })
    );

    yield* _(logger.info(`Todo ${id} updated successfully`));

    return updated;
  });

// Delete todo
export const deleteTodo = (id: string) =>
  Effect.gen(function* (_) {
    const api = yield* _(TodoApi);
    const logger = yield* _(Logger);

    yield* _(logger.info(`Deleting todo ${id}`));

    yield* _(api.deleteTodo(id));

    yield* _(logger.info(`Todo ${id} deleted successfully`));
  });

// Get incomplete todos count
export const getIncompleteTodosCount = Effect.gen(function* (_) {
  const todos = yield* _(fetchAllTodos);
  return todos.filter(t => !t.completed).length;
});
```

### 8.5.5 React Integration

**hooks/useEffect.ts:**

```typescript
import { useEffect, useState } from "react";
import { Effect } from "effect";

export function useRunEffect<A, E>(
  effect: Effect.Effect<A, E, never>,
  deps: React.DependencyList = []
) {
  const [data, setData] = useState<A | null>(null);
  const [error, setError] = useState<E | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    setLoading(true);
    setError(null);

    Effect.runPromise(effect)
      .then(result => {
        setData(result);
        setLoading(false);
      })
      .catch(err => {
        setError(err);
        setLoading(false);
      });
  }, deps);

  return { data, error, loading };
}
```

**components/TodoList.tsx:**

```typescript
import React from "react";
import { Effect } from "effect";
import { useRunEffect } from "../hooks/useEffect";
import { fetchAllTodos, toggleTodo, deleteTodo } from "../effects/todos";
import { AppLayer } from "../layers";

export function TodoList() {
  const { data: todos, error, loading } = useRunEffect(
    fetchAllTodos.pipe(Effect.provide(AppLayer)),
    []
  );

  const handleToggle = (id: string, completed: boolean) => {
    Effect.runPromise(
      toggleTodo(id, completed).pipe(Effect.provide(AppLayer))
    ).then(() => {
      // Refresh list
      window.location.reload();
    });
  };

  const handleDelete = (id: string) => {
    Effect.runPromise(
      deleteTodo(id).pipe(Effect.provide(AppLayer))
    ).then(() => {
      // Refresh list
      window.location.reload();
    });
  };

  if (loading) return <div>Loading...</div>;
  if (error) return <div>Error: {String(error)}</div>;
  if (!todos) return <div>No data</div>;

  return (
    <div className="todo-list">
      {todos.map(todo => (
        <div key={todo.id} className="todo-item">
          <input
            type="checkbox"
            checked={todo.completed}
            onChange={() => handleToggle(todo.id, todo.completed)}
          />
          <span style={{ textDecoration: todo.completed ? 'line-through' : 'none' }}>
            {todo.title}
          </span>
          <button onClick={() => handleDelete(todo.id)}>Delete</button>
        </div>
      ))}
    </div>
  );
}
```

**components/AddTodoForm.tsx:**

```typescript
import React, { useState } from "react";
import { Effect } from "effect";
import { createTodo } from "../effects/todos";
import { AppLayer } from "../layers";

export function AddTodoForm({ onAdded }: { onAdded: () => void }) {
  const [title, setTitle] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    setError(null);

    Effect.runPromise(
      createTodo(title).pipe(Effect.provide(AppLayer))
    )
      .then(() => {
        setTitle("");
        setSubmitting(false);
        onAdded();
      })
      .catch(err => {
        setError(String(err));
        setSubmitting(false);
      });
  };

  return (
    <form onSubmit={handleSubmit}>
      <input
        type="text"
        value={title}
        onChange={e => setTitle(e.target.value)}
        placeholder="Enter todo title"
        disabled={submitting}
      />
      <button type="submit" disabled={submitting}>
        {submitting ? "Adding..." : "Add Todo"}
      </button>
      {error && <div className="error">{error}</div>}
    </form>
  );
}
```

**layers/index.ts:**

```typescript
import { Layer } from "effect";
import { TodoApiLive } from "./TodoApiLive";
import { LoggerLive } from "./LoggerLive";

export const AppLayer = Layer.mergeAll(
  TodoApiLive,
  LoggerLive
);
```

---

## 8.6 Advanced Patterns

### 8.6.1 Retry Logic

```typescript
import { Effect, Schedule } from "effect";

// Retry up to 3 times with exponential backoff
const fetchWithRetry = fetchAllTodos.pipe(
  Effect.retry(
    Schedule.exponential("100 millis").pipe(
      Schedule.compose(Schedule.recurs(3))
    )
  ),
  Effect.provide(AppLayer)
);
```

### 8.6.2 Timeout

```typescript
import { Effect, Duration } from "effect";

// Timeout after 5 seconds
const fetchWithTimeout = fetchAllTodos.pipe(
  Effect.timeout(Duration.seconds(5)),
  Effect.provide(AppLayer)
);
```

### 8.6.3 Caching

```typescript
import { Effect, Cache, Duration } from "effect";

// Create cache
const todoCache = Cache.make({
  capacity: 100,
  timeToLive: Duration.minutes(5),
  lookup: (key: string) => fetchAllTodos.pipe(Effect.provide(AppLayer))
});

// Use cache
Effect.gen(function* (_) {
  const cache = yield* _(todoCache);
  const todos = yield* _(cache.get("all-todos"));
  return todos;
});
```

### 8.6.4 Parallel Execution

```typescript
import { Effect } from "effect";

// Run effects in parallel
const loadDashboard = Effect.gen(function* (_) {
  const [todos, stats, profile] = yield* _(
    Effect.all([
      fetchAllTodos,
      fetchStats(),
      fetchProfile()
    ], { concurrency: "unbounded" })
  );

  return { todos, stats, profile };
});
```

---

## 8.7 Testing

### 8.7.1 Unit Testing with Mock Layer

```typescript
import { describe, it, expect } from "vitest";
import { Effect, Layer } from "effect";
import { fetchAllTodos } from "./effects/todos";
import { TodoApi } from "./services/TodoApi";

// Mock layer
const TodoApiMock = Layer.succeed(
  TodoApi,
  TodoApi.of({
    fetchTodos: () => Effect.succeed([
      { id: "1", title: "Test 1", completed: false, createdAt: new Date() },
      { id: "2", title: "Test 2", completed: true, createdAt: new Date() }
    ]),
    getTodo: (id) => Effect.fail(new Error("Not implemented")),
    createTodo: (title) => Effect.fail(new Error("Not implemented")),
    updateTodo: (id, updates) => Effect.fail(new Error("Not implemented")),
    deleteTodo: (id) => Effect.fail(new Error("Not implemented"))
  })
);

describe("fetchAllTodos", () => {
  it("should fetch todos successfully", async () => {
    const program = fetchAllTodos.pipe(
      Effect.provide(TodoApiMock)
    );

    const result = await Effect.runPromise(program);

    expect(result).toHaveLength(2);
    expect(result[0].title).toBe("Test 1");
  });
});
```

### 8.7.2 Testing Error Handling

```typescript
describe("createTodo", () => {
  it("should reject empty title", async () => {
    const program = createTodo("").pipe(
      Effect.provide(TodoApiMock)
    );

    await expect(Effect.runPromise(program)).rejects.toThrow(
      "Title cannot be empty"
    );
  });

  it("should reject long title", async () => {
    const longTitle = "x".repeat(201);
    const program = createTodo(longTitle).pipe(
      Effect.provide(TodoApiMock)
    );

    await expect(Effect.runPromise(program)).rejects.toThrow(
      "Title too long"
    );
  });
});
```

---

## 8.8 Best Practices

### 8.8.1 Service Design

1. **Interface Segregation** - แยก service ตาม responsibility

```typescript
// ✅ Good - แยกชัดเจน
interface TodoApi { /* ... */ }
interface UserApi { /* ... */ }
interface AuthApi { /* ... */ }

// ❌ Bad - รวมทุกอย่าง
interface Api {
  fetchTodos: () => Effect.Effect<Todo[], Error, never>;
  fetchUsers: () => Effect.Effect<User[], Error, never>;
  login: (credentials: Credentials) => Effect.Effect<Token, Error, never>;
  // ... too many responsibilities
}
```

2. **Error Types** - ใช้ discriminated unions

```typescript
// ✅ Good - แยก error types ชัดเจน
type TodoError =
  | { _tag: "NotFound"; id: string }
  | { _tag: "ValidationError"; field: string; reason: string }
  | { _tag: "ApiError"; message: string };

// ❌ Bad - generic Error
type TodoError = Error;
```

3. **Pure Effects** - แยก pure logic จาก side effects

```typescript
// ✅ Good - pure validation logic
function validateTitle(title: string): Either.Either<string, ValidationError> {
  // Pure function - no side effects
}

// Then use in Effect
const createTodo = (title: string) =>
  Effect.gen(function* (_) {
    const validTitle = yield* _(
      Effect.fromEither(validateTitle(title))
    );
    // ... rest of logic
  });
```

### 8.8.2 Performance

1. **Avoid unnecessary re-renders**

```typescript
// ✅ Good - memoize effects
const fetchTodos = useMemo(
  () => fetchAllTodos.pipe(Effect.provide(AppLayer)),
  []
);

// ❌ Bad - creates new effect every render
const fetchTodos = fetchAllTodos.pipe(Effect.provide(AppLayer));
```

2. **Use parallel execution**

```typescript
// ✅ Good - parallel
Effect.all([effect1, effect2, effect3], { concurrency: "unbounded" });

// ❌ Bad - sequential
Effect.gen(function* (_) {
  const r1 = yield* _(effect1);
  const r2 = yield* _(effect2);
  const r3 = yield* _(effect3);
  return [r1, r2, r3];
});
```

### 8.8.3 Error Handling

1. **Handle errors at the right level**

```typescript
// ✅ Good - handle errors where you can recover
const loadTodos = fetchAllTodos.pipe(
  Effect.catchAll(error => {
    // Can recover by showing cached data
    return loadCachedTodos();
  })
);

// ❌ Bad - catch too early
const badApi = {
  fetchTodos: () => fetchTodos().pipe(
    Effect.catchAll(() => Effect.succeed([])) // Lost error info!
  )
};
```

2. **Use specific error types**

```typescript
// ✅ Good - handle each error differently
pipe(
  fetchTodo(id),
  Effect.catchTag("NotFound", () => Effect.succeed(null)),
  Effect.catchTag("Unauthorized", () => Effect.fail(new RedirectToLogin())),
  Effect.catchTag("ApiError", error => Effect.fail(error))
);
```

---

## 8.9 Effect-TS vs Promises

### 8.9.1 เปรียบเทียบ

| Aspect | Promise | Effect |
|--------|---------|--------|
| Eager/Lazy | Eager (เริ่มทันที) | Lazy (เริ่มเมื่อ run) |
| Error handling | try/catch, .catch() | Type-safe error channel E |
| Composition | .then(), async/await | Effect.gen, pipe |
| Dependencies | Implicit (globals, closures) | Explicit (R parameter) |
| Retry | Manual | Built-in (Schedule) |
| Timeout | Manual (Promise.race) | Built-in (Effect.timeout) |
| Testing | ต้อง mock globals | Provide mock layers |
| Type safety | Limited | Full (A, E, R tracked) |

### 8.9.2 Migration Strategy

**Step 1: Wrap existing Promises**

```typescript
// Existing
async function fetchTodos(): Promise<Todo[]> {
  return fetch('/api/todos').then(r => r.json());
}

// Wrapped in Effect
const fetchTodosEffect = Effect.promise(() => fetchTodos());
```

**Step 2: Add error handling**

```typescript
const fetchTodosEffect = Effect.tryPromise({
  try: () => fetchTodos(),
  catch: (error) => new ApiError("Failed to fetch todos", error)
});
```

**Step 3: Extract into service**

```typescript
const TodoApiLive = Layer.succeed(
  TodoApi,
  TodoApi.of({
    fetchTodos: () => fetchTodosEffect
  })
);
```

---

## 8.10 สรุป

### สิ่งที่ได้เรียนรู้ในบทนี้

1. **Effect<A, E, R>** - Type-safe effects
   - A = Success value
   - E = Error type
   - R = Requirements (dependencies)

2. **Effect.gen** - Generator syntax for readable code
   - คล้าย async/await แต่ type-safe กว่า

3. **Context.Tag** - Dependency Injection
   - Define services ด้วย interface
   - Provide implementations ด้วย Layer
   - Test ด้วย mock layers

4. **Option และ Either** - Type-safe null handling และ error handling
   - Option<A> แทน null/undefined
   - Either<E, A> แทน exceptions

5. **Composition** - ต่อ effects ให้เป็นโปรแกรมใหญ่
   - Effect.map, Effect.flatMap
   - Effect.all (parallel)
   - Effect.gen (sequential)

### ข้อดีของ Effect-TS

1. **Type Safety** - Compiler ช่วยจับ bugs
2. **Testability** - Mock dependencies ได้ง่าย
3. **Composability** - ต่อ effects ได้สวยงาม
4. **Error Handling** - Errors เป็น values, ไม่ใช่ exceptions
5. **Built-in Utilities** - Retry, timeout, caching

### ข้อแลกเปลี่ยน (Trade-offs)

1. **Learning Curve** - ต้องเรียนรู้ concepts ใหม่
2. **Verbosity** - โค้ดอาจยาวกว่า async/await
3. **Bundle Size** - Effect-TS มีขนาดใหญ่กว่า utility libraries อื่น
4. **Ecosystem** - น้อยกว่า Promise-based libraries

### บทถัดไป

ในบทที่ 9 เราจะเรียนรู้:
- **Performance Optimization** - Lazy evaluation, memoization
- **Advanced Effect System** - Fiber, Concurrency, Resource management
- **Production Deployment** - Monitoring, error tracking, logging

---

## แบบฝึกหัดท้ายบท

### ข้อ 1: Basic Effect

เขียน Effect ที่ generate random number ระหว่าง 1-100

```typescript
// TODO: Implement
const randomNumber: Effect.Effect<number, never, never> = ?
```

### ข้อ 2: Error Handling

เขียนฟังก์ชันแบ่งเลข ที่คืน Effect และ handle division by zero

```typescript
function divide(a: number, b: number): Effect.Effect<number, DivisionError, never> {
  // TODO: Implement
}
```

### ข้อ 3: Service

สร้าง WeatherApi service ที่มี method `getCurrentWeather(city: string)`

```typescript
interface WeatherApi {
  // TODO: Define interface
}

const WeatherApi = Context.GenericTag<WeatherApi>("WeatherApi");

// TODO: Create live implementation
```

### ข้อ 4: Composition

เขียนโปรแกรมที่:
1. Fetch user
2. Fetch user's orders
3. Calculate total spending
4. Return summary

```typescript
const getUserSummary = (userId: string) =>
  Effect.gen(function* (_) {
    // TODO: Implement
  });
```

### ข้อ 5: React Integration

สร้าง custom hook `useEffect` ที่รับ Effect และ return `{ data, error, loading }`

```typescript
function useEffect<A, E>(
  effect: Effect.Effect<A, E, never>,
  deps: React.DependencyList
) {
  // TODO: Implement
}
```

---

**พร้อมที่จะเอา Effect-TS ไปใช้ใน Frontend project จริงแล้วใช่ไหม?**
