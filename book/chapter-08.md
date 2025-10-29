# บทที่ 8: Effect-TS Fundamentals for Frontend

> "เริ่มต้นกับ Effect-TS - พื้นฐานที่ทุกคนต้องรู้"

**📦 Validated with Effect-TS 3.18.4 + TypeScript 5.9.3**

> 💡 **สำคัญ**: บทนี้ใช้ Effect-TS 3.x API ซึ่งมีการเปลี่ยนแปลงจาก 2.x:
> - ✅ `Effect.gen(function* () {})` - ไม่ต้องส่ง `_` parameter
> - ✅ `yield* service` - ไม่ต้อง `yield* _()`
> - ✅ `Effect.provide(effect, layer)` - data-first API
>
> หาก documentation อื่นใช้ Effect 2.x syntax อาจดูแตกต่าง!

> 🎯 **เป้าหมายบทนี้**: เรียนรู้พื้นฐาน Effect-TS และสร้าง Simple Todo App
>
> 💡 **บทนี้เน้น**: Concepts และ Basic Patterns - สำหรับ Production Architecture อ่านบทที่ 9

---

## เนื้อหาในบทนี้

- 8.1 ทำไมต้องใช้ Effect-TS ใน Frontend?
- 8.2 Effect-TS Core Concepts
- 8.3 Option และ Either
- 8.4 Context.Tag - Dependency Injection Basics
- 8.5 Your First Effect App - Simple Todo (Fetch + Create Only)
- 8.6 Testing Basics
- 8.7 Core Principles
- 8.8 Effect-TS vs Promises
- 8.9 สรุปและบทถัดไป

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
const program = Effect.gen(function* () {
  const api = yield* ApiService;
  const todos = yield* api.fetchTodos();
  return todos;
});

// 💡 Effect 3.x: ใช้ yield* โดยตรง (ไม่ต้อง yield* ) อีกต่อไป)
```

**แนวทาง 3: Composable Effects**

```typescript
// ✅ Compose operations แบบ readable
const loadDashboard = (userId: string) =>
  Effect.gen(function* () {
    const user = yield* fetchUser(userId);
    const orders = yield* fetchOrders(user.id);
    const products = yield* Effect.all(
      orders.map(o => fetchProduct(o.productId))
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
  Effect.gen(function* () {
    const logger = yield* LoggerTag;
    yield* logger.log("Hello from Effect-TS!");
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

const program = Effect.gen(function* () {
  // ใช้ yield* เพื่อ "unwrap" Effect
  const user = yield* fetchUser(userId);
  const orders = yield* fetchOrders(user.id);
  const total = yield* calculateTotal(orders);

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

**คำอธิบาย `yield*`:**
- `yield*` = unwrap Effect<A, E, R> → ได้ค่า A (extract value from Effect)
- ถ้า Effect fail จะหยุดทันที (short-circuit)
- ไม่ต้องใช้ `try/catch` - error handling automatic!

**💡 Effect 3.x**: ใช้ `yield*` โดยตรง (ไม่มี `_()` helper อีกต่อไป)

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
import { Effect, Exit } from "effect";

const effect = Effect.fail(new Error("Oops"));

// ✅ Functional approach with Exit.match
Effect.runPromiseExit(effect).then(
  Exit.match({
    onFailure: (cause) => console.log("Failure:", cause),
    onSuccess: (value) => console.log("Success:", value)
  })
);

// 🔄 Imperative approach (ใช้ได้แต่ไม่ functional)
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

### 8.3.1 `Option<A>` - ค่าที่อาจไม่มี

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

// Service interface - Simple API for Chapter 8
interface TodoApi {
  readonly fetchTodos: () => Effect.Effect<Todo[], ApiError, never>;
  readonly createTodo: (title: string) => Effect.Effect<Todo, ApiError, never>;
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
const fetchAllTodos = Effect.gen(function* () {
  const api = yield* TodoApi;
  const todos = yield* api.fetchTodos();
  return todos;
});

// Type: Effect<Todo[], ApiError, TodoApi>
//               ^success  ^error    ^requirements
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
    })
  })
);
```

### 8.4.4 Providing Dependencies

**Provide ด้วย Layer:**

```typescript
// 💡 Effect 3.x: ใช้ Effect.provide(effect, layer) - data-first API
const program = Effect.provide(fetchAllTodos, TodoApiLive);

// ตอนนี้ program มี type: Effect<Todo[], ApiError>
// Requirements (TodoApi) หายไปแล้ว!

// 🔄 Effect 2.x (เก่า): effect.pipe(Effect.provide(layer))
// const program = fetchAllTodos.pipe(Effect.provide(TodoApiLive));
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
const program = Effect.provide(myComplexProgram, AppLayer);
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
  readonly _tag = "TodoNotFoundError" as const;
  readonly id: string;

  constructor(id: string) {
    this.id = id;
  }
}

export class ApiError {
  readonly _tag = "ApiError" as const;
  readonly message: string;
  readonly cause?: unknown;

  constructor(message: string, cause?: unknown) {
    this.message = message;
    this.cause = cause;
  }
}

// 💡 หมายเหตุ: ใน TypeScript 5.x + Vite
// ต้องใช้ explicit property declarations แทน parameter properties
// เพื่อให้ทำงานกับ verbatimModuleSyntax ได้

export type TodoError = TodoNotFoundError | ApiError;

// Service interface - Simple API for Chapter 8
export interface TodoApi {
  readonly fetchTodos: () => Effect.Effect<Todo[], ApiError, never>;
  readonly createTodo: (title: string) => Effect.Effect<Todo, ApiError, never>;
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

    createTodo: (title: string) =>
      handleResponse<Todo>(
        fetch(`${API_BASE}/todos`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ title, completed: false })
        })
      )
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
export const fetchAllTodos = Effect.gen(function* () {
  const api = yield* TodoApi;
  const logger = yield* Logger;

  yield* logger.info("Fetching all todos");

  const todos = yield* api.fetchTodos();

  yield* logger.info(`Fetched ${todos.length} todos`);

  return todos;
});

// Create todo with validation
export const createTodo = (title: string) =>
  Effect.gen(function* () {
    const api = yield* TodoApi;
    const logger = yield* Logger;

    // Validation
    const trimmed = title.trim();
    if (trimmed.length === 0) {
      return yield* Effect.fail(new Error("Title cannot be empty"));
    }
    if (trimmed.length > 200) {
      return yield* Effect.fail(new Error("Title too long (max 200 chars)"));
    }

    yield* logger.info(`Creating todo: ${trimmed}`);

    const todo = yield* api.createTodo(trimmed);

    yield* logger.info(`Created todo with id: ${todo.id}`);

    return todo;
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
import { fetchAllTodos } from "../effects/todos";
import { AppLayer } from "../layers";
import type { Todo } from "../services/TodoApi";

interface TodoListProps {
  onRefresh: number;
}

export function TodoList({ onRefresh }: TodoListProps) {
  const { data: todos, error, loading } = useRunEffect(
    Effect.provide(fetchAllTodos, AppLayer),
    [onRefresh]
  );

  if (loading) return <div>Loading...</div>;
  if (error) return <div>Error: {String(error)}</div>;
  if (!todos) return <div>No data</div>;

  return (
    <div className="todo-list">
      {todos.map(todo => (
        <div key={todo.id} className="todo-item">
          <span style={{ textDecoration: todo.completed ? 'line-through' : 'none' }}>
            {todo.title}
          </span>
          <span className="todo-status">
            {todo.completed ? '✓ Completed' : '○ Pending'}
          </span>
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
      Effect.provide(createTodo(title), AppLayer)
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

**App.tsx - จัดการ state และ refresh:**

```typescript
import { useState } from 'react';
import './App.css';
import { TodoList } from './components/TodoList';
import { AddTodoForm } from './components/AddTodoForm';

function App() {
  const [refreshKey, setRefreshKey] = useState(0);

  const handleUpdate = () => {
    setRefreshKey(prev => prev + 1); // Trigger re-fetch
  };

  return (
    <div className="App">
      <header className="App-header">
        <h1>Effect-TS Todo App</h1>
        <p className="subtitle">Simple Todo - Fetch & Create Only (Chapter 8)</p>
      </header>
      <main className="App-main">
        <AddTodoForm onAdded={handleUpdate} />
        <TodoList onRefresh={refreshKey} />
      </main>
    </div>
  );
}

export default App;
```

**การทำงาน:**
1. เมื่อ add todo → เรียก `handleUpdate()`
2. `refreshKey` เพิ่มขึ้น → `useRunEffect` re-run
3. Re-fetch ข้อมูลใหม่โดยไม่ reload หน้า

> 💡 **Note**: Update และ Delete functionality จะครอบคลุมใน บทที่ 9

---

> 💡 **Advanced Patterns** (Retry, Timeout, Caching, Parallel Execution, Resource Management) จะครอบคลุมใน บทที่ 9

---

## 8.6 Testing Basics

การทดสอบ Effect-TS ทำได้ง่ายด้วย Mock Layers:

### 8.6.1 Testing with Mock Layer

```typescript
import { describe, it, expect } from "vitest";
import { Effect, Layer } from "effect";
import { fetchAllTodos } from "./effects/todos";
import { TodoApi } from "./services/TodoApi";

// Mock layer - ให้ข้อมูลทดสอบแบบ static
const TodoApiMock = Layer.succeed(
  TodoApi,
  TodoApi.of({
    fetchTodos: () => Effect.succeed([
      { id: "1", title: "Test 1", completed: false, createdAt: new Date() },
      { id: "2", title: "Test 2", completed: true, createdAt: new Date() }
    ]),
    createTodo: (title) => Effect.succeed({
      id: "test-id",
      title,
      completed: false,
      createdAt: new Date()
    })
  })
);

describe("fetchAllTodos", () => {
  it("should fetch todos successfully", async () => {
    const program = Effect.provide(fetchAllTodos, TodoApiMock);
    const result = await Effect.runPromise(program);

    expect(result).toHaveLength(2);
    expect(result[0].title).toBe("Test 1");
  });
});
```

### 8.6.2 Testing Validation

```typescript
import { createTodo } from "./effects/todos";
import { LoggerMock } from "./layers/LoggerMock";

const TestLayer = Layer.mergeAll(TodoApiMock, LoggerMock);

describe("createTodo", () => {
  it("should reject empty title", async () => {
    const program = Effect.provide(createTodo(""), TestLayer);

    await expect(Effect.runPromise(program)).rejects.toThrow(
      "Title cannot be empty"
    );
  });

  it("should create todo with valid title", async () => {
    const program = Effect.provide(createTodo("New Todo"), TestLayer);
    const result = await Effect.runPromise(program);

    expect(result.title).toBe("New Todo");
    expect(result.completed).toBe(false);
  });
});
```

**Key Testing Principles:**
1. ใช้ Mock Layers แทน real implementations
2. Test แต่ละ effect อิสระจากกัน
3. Verify ทั้ง success และ error cases

> 💡 **Advanced Testing** (Integration tests, property-based testing, concurrent testing) ครอบคลุมใน บทที่ 9 หัวข้อที่ 9.9

---

## 8.7 Core Principles

หลักการสำคัญที่ต้องเข้าใจเมื่อใช้ Effect-TS:

### 8.7.1 Type Safety First

**Effect Type เป็นสัญญา (Contract)**

```typescript
// Type บอกทุกอย่างที่เป็นไปได้
function fetchTodo(id: string): Effect.Effect<Todo, TodoError, TodoApi> {
  // Success: Todo
  // Error: TodoError (ไม่มี error อื่น!)
  // Requirements: ต้องมี TodoApi
}
```

**ข้อดี:**
- Compiler บังคับให้ handle errors
- ไม่มี runtime surprises
- Refactor ได้ปลอดภัย

### 8.7.2 Pure Functions

**แยก logic ออกจาก effects**

```typescript
// ✅ Pure validation - testable, reusable
function validateTitle(title: string): Either.Either<string, ValidationError> {
  if (title.trim() === "") {
    return Either.left({ _tag: "EmptyField", field: "title" });
  }
  return Either.right(title.trim());
}

// Effect ใช้ pure function
const createTodo = (title: string) =>
  Effect.gen(function* () {
    const validTitle = yield* Effect.fromEither(validateTitle(title));
    const api = yield* TodoApi;
    return yield* api.createTodo(validTitle);
  });
```

### 8.7.3 Explicit Dependencies

**ทุก dependency ต้องชัดเจนใน type signature**

```typescript
// ✅ Good - เห็นว่าต้องการ TodoApi และ Logger
const program: Effect.Effect<Todo[], ApiError, TodoApi | Logger> =
  Effect.gen(function* () {
    const api = yield* TodoApi;
    const logger = yield* Logger;
    // ...
  });

// ❌ Bad - ใช้ global (hidden dependency)
const api = new ApiClient(); // Global!
const program = Effect.sync(() => api.fetchTodos());
```

**ข้อดีของ Explicit Dependencies:**
- Testing ง่าย - swap implementations
- Refactoring ปลอดภัย - compiler บอกว่าขาดอะไร
- ไม่มี hidden side effects

---

> 💡 **Comprehensive Best Practices** (Performance, Architecture Patterns, Error Handling Strategies) ครอบคลุมใน **บทที่ 9 หัวข้อที่ 9.10**

---

## 8.8 Effect-TS vs Promises

### 8.8.1 เปรียบเทียบ

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

### 8.8.2 Migration Strategy

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

## 8.9 สรุปและบทถัดไป

### 8.9.1 สิ่งที่ได้เรียนรู้ในบทนี้

เราได้เรียนรู้พื้นฐานของ Effect-TS ที่จำเป็นสำหรับการพัฒนา Frontend:

1. **Effect<A, E, R>** - Type signature ที่บอกทุกอย่าง
   - A = Success value
   - E = Error type (type-safe!)
   - R = Requirements (dependencies)

2. **Effect.gen** - Generator syntax สำหรับเขียนโค้ดแบบ imperative
   - คล้าย async/await แต่ type-safe
   - Error handling automatic

3. **Context.Tag** - Dependency Injection แบบ type-safe
   - Define services ด้วย interface
   - Provide implementations ด้วย Layer
   - Test ด้วย mock layers

4. **Option และ Either** - ทางเลือกแทน null/undefined และ exceptions
   - `Option<A>` - ค่าที่อาจไม่มี
   - `Either<E, A>` - Success หรือ Error

5. **Composition** - ต่อ effects แบบ composable
   - Effect.map, Effect.flatMap
   - Effect.gen (sequential)
   - Testing ง่ายด้วย Mock Layers

### 8.9.2 สิ่งที่ยังไม่ได้ครอบคลุม

บทนี้เน้นพื้นฐาน - หัวข้อต่อไปนี้จะครอบคลุมใน **บทที่ 9: Production Architecture**:

**Advanced Patterns:**
- Retry with Schedule (exponential backoff, custom policies)
- Timeout Handling and Cancellation
- Caching Strategies (memoization, request deduplication)
- Parallel Execution (Effect.all, racing, batching)
- Resource Management (acquire/release patterns)

**Production Features:**
- Update/Delete Operations (full CRUD)
- Real-time Updates (WebSocket, Server-Sent Events)
- Optimistic Updates และ State Management
- Error Boundaries และ Fallback Strategies
- Performance Monitoring และ Observability

**Architecture:**
- Layered Architecture (Domain, Application, Infrastructure)
- Effect-based State Management
- Advanced Testing (Integration, E2E, Property-based)
- Production Deployment Best Practices

### 8.9.3 บทถัดไป

**บทที่ 9** จะพาคุณจาก Fundamentals สู่ Production-ready application:

- เรียนรู้ Advanced Effect Patterns
- สร้าง Full-featured Todo App (CRUD + Real-time)
- Production Architecture และ Best Practices
- Performance Optimization Techniques
- Comprehensive Testing Strategies

**พร้อมที่จะก้าวไปสู่ Production Level แล้วหรือยัง?** อ่าน Chapter 9 เลย!

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
  Effect.gen(function* () {
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
