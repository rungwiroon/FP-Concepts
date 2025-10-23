# บทที่ 9: Advanced Topics และ Production Best Practices

> "ความเร็วมาจากการทำสิ่งที่ถูกต้อง ไม่ใช่การทำให้เร็ว" - Rich Hickey

---

## 9.1 Performance Optimization

### 9.1.1 Lazy Evaluation

**แนวคิด:** ไม่คำนวณค่าจนกว่าจะต้องใช้จริง

**Backend (C# language-ext):**

```csharp
using LanguageExt;

// ❌ Eager - คำนวณทันที (แม้ไม่ใช้ result)
var numbers = Enumerable.Range(1, 1000000)
    .Select(x => x * 2)
    .Select(x => x + 1)
    .ToList(); // คำนวณทั้งหมด 1M รายการ!

// ✅ Lazy - คำนวณเมื่อใช้จริง
var lazyNumbers = Enumerable.Range(1, 1000000)
    .Select(x => x * 2)
    .Select(x => x + 1); // ยังไม่คำนวณ

var first10 = lazyNumbers.Take(10).ToList(); // คำนวณแค่ 10 ตัว!
```

**Seq<T> ใน language-ext:**

```csharp
using LanguageExt;
using static LanguageExt.Prelude;

// Seq<T> คือ immutable lazy sequence
var numbers = Seq.range(1, 1000000)
    .Map(x => x * 2)
    .Filter(x => x > 100)
    .Take(10); // Lazy - ยังไม่คำนวณ

// คำนวณเมื่อ enumerate
foreach (var n in numbers)
{
    Console.WriteLine(n); // คำนวณทีละตัวตามต้องการ
}
```

**ตัวอย่างการใช้งาน - Infinite Sequences:**

```csharp
// สร้าง infinite sequence ของ Fibonacci
Seq<int> Fibonacci()
{
    return Generate((0, 1), state =>
    {
        var (a, b) = state;
        return (a, (b, a + b));
    });
}

// ใช้แค่ 10 ตัวแรก - ไม่ infinite loop!
var first10Fib = Fibonacci().Take(10).ToList();
// [0, 1, 1, 2, 3, 5, 8, 13, 21, 34]
```

**Frontend (TypeScript Effect-TS):**

```typescript
import { Effect, Stream } from "effect";

// Stream = Lazy async sequence
const numbers = Stream.range(1, 1000000).pipe(
  Stream.map(x => x * 2),
  Stream.filter(x => x > 100),
  Stream.take(10)
);
// ยังไม่คำนวณ - เป็นแค่ description

// คำนวณเมื่อ run
const first10 = await Effect.runPromise(
  Stream.runCollect(numbers)
);
```

### 9.1.2 Memoization

**แนวคิด:** Cache ผลลัพธ์ของ pure function เพื่อไม่ต้องคำนวณซ้ำ

**Backend (C#):**

```csharp
using System.Collections.Concurrent;

// Memoization helper
public static class Memo
{
    public static Func<T, R> Memoize<T, R>(Func<T, R> func)
        where T : notnull
    {
        var cache = new ConcurrentDictionary<T, R>();
        return arg => cache.GetOrAdd(arg, func);
    }
}

// ตัวอย่าง: Fibonacci แบบช้า
int FibonacciSlow(int n) =>
    n <= 1 ? n : FibonacciSlow(n - 1) + FibonacciSlow(n - 2);

// Memoized version - เร็วมาก!
var fibonacci = Memo.Memoize<int, int>(n =>
    n <= 1 ? n : fibonacci(n - 1) + fibonacci(n - 2)
);

// ใช้งาน
var result1 = fibonacci(40); // คำนวณครั้งแรก (ช้า)
var result2 = fibonacci(40); // ดึงจาก cache (เร็วมาก!)
```

**ใช้กับ language-ext:**

```csharp
using LanguageExt;
using static LanguageExt.Prelude;

// Memoize IO operation
var getUser = memo<int, IO<User>>(userId =>
    IO.liftAsync(async () =>
    {
        Console.WriteLine($"Fetching user {userId}...");
        await Task.Delay(1000); // Simulate DB call
        return new User { Id = userId, Name = $"User {userId}" };
    })
);

// ใช้งาน
var user1 = getUser(1).Run(); // Fetch จาก DB
var user2 = getUser(1).Run(); // ดึงจาก cache!
var user3 = getUser(2).Run(); // Fetch จาก DB (userId ต่างกัน)
```

**Frontend (TypeScript):**

```typescript
import { Effect, Cache, Duration } from "effect";

// Cache with TTL
const userCache = Cache.make({
  capacity: 100,
  timeToLive: Duration.minutes(5),
  lookup: (userId: string) => fetchUser(userId)
});

// ใช้งาน
const getUser = (userId: string) =>
  Effect.gen(function* (_) {
    const cache = yield* _(userCache);
    const user = yield* _(cache.get(userId)); // Auto cache
    return user;
  });

// First call - fetch from API
const user1 = await Effect.runPromise(getUser("123"));

// Second call (within 5 min) - from cache
const user2 = await Effect.runPromise(getUser("123"));
```

**React Memoization:**

```typescript
import { useMemo, useCallback } from "react";

function TodoList({ todos }: { todos: Todo[] }) {
  // Memoize expensive computation
  const sortedTodos = useMemo(
    () => todos.sort((a, b) => a.title.localeCompare(b.title)),
    [todos] // Recompute only when todos change
  );

  // Memoize callback
  const handleToggle = useCallback(
    (id: string) => {
      console.log(`Toggle ${id}`);
    },
    [] // Never changes
  );

  return (
    <div>
      {sortedTodos.map(todo => (
        <TodoItem key={todo.id} todo={todo} onToggle={handleToggle} />
      ))}
    </div>
  );
}
```

### 9.1.3 Structural Sharing

**แนวคิด:** Immutable data structures แชร์ส่วนที่ไม่เปลี่ยน

**Backend (C#):**

```csharp
using System.Collections.Immutable;

var list1 = ImmutableList.Create(1, 2, 3, 4, 5);
var list2 = list1.Add(6); // แชร์ nodes [1,2,3,4,5]

// list1 และ list2 แชร์ memory ส่วนใหญ่
// เพิ่มแค่ node ใหม่สำหรับ 6

// Performance: O(log n) แทน O(n) ของ List<T>.Add
```

**Visualization:**

```
list1: [1] → [2] → [3] → [4] → [5]
                                  ↓
list2: [1] → [2] → [3] → [4] → [5] → [6]
       └────── แชร์ nodes เดียวกัน ──────┘
```

**Frontend (TypeScript with Immer):**

```typescript
import produce from "immer";

const state1 = {
  user: { id: 1, name: "John" },
  todos: [
    { id: 1, title: "Todo 1" },
    { id: 2, title: "Todo 2" }
  ]
};

// ✅ Structural sharing with Immer
const state2 = produce(state1, draft => {
  draft.todos[0].title = "Updated Todo 1";
});

// state2.user === state1.user (same reference)
// state2.todos !== state1.todos (different reference)
// state2.todos[1] === state1.todos[1] (unchanged todo)
```

### 9.1.4 Performance Benchmarking

**Backend (C# with BenchmarkDotNet):**

```csharp
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

[MemoryDiagnoser]
public class FibonacciBenchmark
{
    [Benchmark]
    public int RecursiveFibonacci() => Fib(20);

    [Benchmark]
    public int MemoizedFibonacci() => FibMemo(20);

    int Fib(int n) => n <= 1 ? n : Fib(n - 1) + Fib(n - 2);

    static Func<int, int> FibMemo = Memo.Memoize<int, int>(n =>
        n <= 1 ? n : FibMemo(n - 1) + FibMemo(n - 2)
    );
}

// Run benchmark
BenchmarkRunner.Run<FibonacciBenchmark>();

// Results:
// |           Method |      Mean | Allocated |
// |----------------- |----------:|----------:|
// | RecursiveFibonacci | 13.50 μs |     40 B |
// | MemoizedFibonacci  |  0.05 μs |    120 B |
```

**Frontend (Performance API):**

```typescript
// Measure effect execution time
const measureEffect = <A, E, R>(
  name: string,
  effect: Effect.Effect<A, E, R>
) =>
  Effect.gen(function* (_) {
    const start = performance.now();
    const result = yield* _(effect);
    const end = performance.now();
    console.log(`${name} took ${end - start}ms`);
    return result;
  });

// Usage
const result = await Effect.runPromise(
  measureEffect("fetchTodos", fetchAllTodos)
);
```

---

## 9.2 Concurrency และ Parallelism

### 9.2.1 ความแตกต่างระหว่าง Concurrency และ Parallelism

**Concurrency** = จัดการหลายงานพร้อมกัน (interleaving)
**Parallelism** = ทำหลายงานพร้อมกันจริงๆ (simultaneous)

```
Concurrency (single core):
Task A: --|-----|-----|
Task B:   --|-----|-----|
Task C:     --|-----|
Time: →→→→→→→→→→→→→→→

Parallelism (multi-core):
Core 1: Task A --------
Core 2: Task B --------
Core 3: Task C --------
Time: →→→→→→→→
```

### 9.2.2 Parallel Execution (Backend)

**C# Parallel LINQ (PLINQ):**

```csharp
using System.Linq;

var numbers = Enumerable.Range(1, 1000000);

// Sequential (1 core)
var results1 = numbers
    .Where(x => IsPrime(x))
    .ToList();

// Parallel (all cores)
var results2 = numbers
    .AsParallel()
    .Where(x => IsPrime(x))
    .ToList();

// Parallel with degree of parallelism
var results3 = numbers
    .AsParallel()
    .WithDegreeOfParallelism(4) // Use 4 cores
    .Where(x => IsPrime(x))
    .ToList();
```

**language-ext Parallel Operations:**

```csharp
using LanguageExt;
using static LanguageExt.Prelude;

// Parallel map
var orders = Seq.range(1, 100);

var processedOrders = orders
    .AsParallel()
    .Map(orderId => ProcessOrder(orderId))
    .ToSeq();

// Parallel traverse (run effects in parallel)
Seq<K<M, OrderResult>> effects = orders.Map(id => ProcessOrderEffect(id));

K<M, Seq<OrderResult>> results = effects.Traverse(identity);
// ทำงานแบบ sequential

K<M, Seq<OrderResult>> resultsParallel = effects.TraverseParallel(identity);
// ทำงานแบบ parallel!
```

**Task Parallel Library:**

```csharp
using System.Threading.Tasks;

// Run multiple async operations in parallel
var task1 = FetchUserAsync(1);
var task2 = FetchOrdersAsync(1);
var task3 = FetchProfileAsync(1);

// Wait for all (parallel)
await Task.WhenAll(task1, task2, task3);

// With language-ext
var effects = Seq(
    FetchUserIO(1),
    FetchOrdersIO(1),
    FetchProfileIO(1)
);

var allResults = effects
    .TraverseParallel(identity)
    .Run(); // Run all in parallel
```

### 9.2.3 Concurrent Execution (Frontend)

**Effect-TS Parallel Execution:**

```typescript
import { Effect } from "effect";

// Sequential
const sequential = Effect.gen(function* (_) {
  const user = yield* _(fetchUser(userId));
  const orders = yield* _(fetchOrders(userId));
  const profile = yield* _(fetchProfile(userId));
  return { user, orders, profile };
});

// Parallel (all at once)
const parallel = Effect.all(
  [
    fetchUser(userId),
    fetchOrders(userId),
    fetchProfile(userId)
  ],
  { concurrency: "unbounded" }
).pipe(
  Effect.map(([user, orders, profile]) => ({ user, orders, profile }))
);

// Parallel with limit
const parallelLimited = Effect.all(
  orders.map(o => processOrder(o)),
  { concurrency: 5 } // Max 5 concurrent requests
);
```

**Race - First to complete:**

```typescript
import { Effect } from "effect";

// Use whichever finishes first
const fastest = Effect.race(
  fetchFromCache(key),
  fetchFromApi(key)
);

// Timeout with race
const withTimeout = Effect.race(
  fetchData(),
  Effect.sleep("5 seconds").pipe(
    Effect.flatMap(() => Effect.fail(new TimeoutError()))
  )
);
```

### 9.2.4 Fiber-based Concurrency (Effect-TS)

**Fiber = Lightweight thread**

```typescript
import { Effect, Fiber } from "effect";

// Fork effect into background fiber
const program = Effect.gen(function* (_) {
  // Start background task
  const fiber = yield* _(Effect.fork(longRunningTask()));

  // Do other work
  yield* _(otherWork());

  // Wait for background task
  const result = yield* _(Fiber.join(fiber));

  return result;
});
```

**Fiber Interruption:**

```typescript
import { Effect, Fiber } from "effect";

const program = Effect.gen(function* (_) {
  const fiber = yield* _(Effect.fork(infiniteLoop()));

  // Do some work
  yield* _(Effect.sleep("5 seconds"));

  // Cancel background fiber
  yield* _(Fiber.interrupt(fiber));
});

// Cleanup with interruption
const infiniteLoop = () =>
  Effect.gen(function* (_) {
    try {
      while (true) {
        yield* _(doWork());
        yield* _(Effect.sleep("1 second"));
      }
    } finally {
      // Cleanup code runs on interruption
      yield* _(cleanup());
    }
  });
```

### 9.2.5 Race Conditions และ Thread Safety

**ปัญหา: Shared Mutable State**

```typescript
// ❌ Race condition!
let counter = 0;

async function increment() {
  const current = counter; // Read
  await someAsyncWork();
  counter = current + 1; // Write (อาจเขียนทับ!)
}

// Run in parallel - counter อาจไม่ถึง 10!
await Promise.all([...Array(10)].map(() => increment()));
```

**แก้ไข: Immutable Updates**

```typescript
// ✅ Functional - ไม่มี shared mutable state
const increment = (current: number): Effect.Effect<number, never, never> =>
  Effect.gen(function* (_) {
    yield* _(someAsyncWork());
    return current + 1;
  });

// Safe - แต่ละ effect มี state เป็นของตัวเอง
```

**Backend: Thread-safe với Concurrent Collections**

```csharp
using System.Collections.Concurrent;

// ✅ Thread-safe
var dict = new ConcurrentDictionary<string, User>();

Parallel.For(0, 1000, i =>
{
    dict.TryAdd($"user{i}", new User { Id = i });
});

// ❌ Not thread-safe!
var normalDict = new Dictionary<string, User>();

Parallel.For(0, 1000, i =>
{
    normalDict[i.ToString()] = new User { Id = i }; // Race condition!
});
```

---

## 9.3 Stream Processing

### 9.3.1 Streams vs Collections

**Collection** = ข้อมูลทั้งหมดใน memory
**Stream** = ข้อมูลไหลผ่านทีละชิ้น (lazy)

```typescript
// Collection - load ทั้งหมดเข้า memory
const todos: Todo[] = await fetchAllTodos();
const completed = todos.filter(t => t.completed);

// Stream - process ทีละ chunk
const todoStream = Stream.fromAsyncIterable(
  fetchTodosStream(),
  error => new FetchError(error)
);

const completedStream = todoStream.pipe(
  Stream.filter(t => t.completed)
);
```

### 9.3.2 Backend Streams (C#)

**IAsyncEnumerable:**

```csharp
// Stream large dataset
async IAsyncEnumerable<Order> StreamOrders()
{
    var offset = 0;
    const int batchSize = 100;

    while (true)
    {
        var batch = await db.Orders
            .Skip(offset)
            .Take(batchSize)
            .ToListAsync();

        if (batch.Count == 0) break;

        foreach (var order in batch)
        {
            yield return order;
        }

        offset += batchSize;
    }
}

// Consume stream
await foreach (var order in StreamOrders())
{
    await ProcessOrder(order);
    // Process ทีละตัว - ไม่ load ทั้งหมดเข้า memory!
}
```

**language-ext Streaming:**

```csharp
using LanguageExt;

// Stream with backpressure
StreamT<M, RT, A> stream = StreamT.range<M, RT>(1, 1000000)
    .Map(x => x * 2)
    .Filter(x => x > 100)
    .Take(10);

// Consume
await stream.RunAsync(rt, EnvIO.New());
```

### 9.3.3 Frontend Streams (Effect-TS)

**Basic Stream Operations:**

```typescript
import { Stream, Effect } from "effect";

// Create stream from array
const numbers = Stream.fromIterable([1, 2, 3, 4, 5]);

// Transform stream
const doubled = numbers.pipe(
  Stream.map(x => x * 2),
  Stream.filter(x => x > 5)
);

// Collect results
const result = await Effect.runPromise(
  Stream.runCollect(doubled)
);
// Chunk([6, 8, 10])
```

**Async Streams:**

```typescript
import { Stream, Effect } from "effect";

// Stream from async generator
const todoStream = Stream.fromAsyncIterable(
  async function* () {
    let page = 1;
    while (true) {
      const todos = await fetchTodosPage(page);
      if (todos.length === 0) break;

      for (const todo of todos) {
        yield todo;
      }

      page++;
    }
  },
  error => new FetchError(String(error))
);

// Process stream
const program = todoStream.pipe(
  Stream.filter(todo => !todo.completed),
  Stream.map(todo => processTodo(todo)),
  Stream.runDrain // Process all items
);
```

**Stream Chunking:**

```typescript
import { Stream } from "effect";

// Group into chunks
const chunked = Stream.range(1, 100).pipe(
  Stream.grouped(10) // Groups of 10
);

// Process chunks in parallel
const processed = chunked.pipe(
  Stream.mapEffect(chunk =>
    Effect.all(
      chunk.map(processBatch),
      { concurrency: 5 }
    )
  )
);
```

### 9.3.4 Backpressure

**ปัญหา:** Producer เร็วกว่า Consumer

```typescript
// ❌ Producer overwhelms consumer
async function* fastProducer() {
  while (true) {
    yield generateData(); // Very fast
  }
}

async function slowConsumer(data) {
  await processData(data); // Very slow
}
// Consumer ไม่ทัน → memory overflow!
```

**แก้ไข: Backpressure**

```typescript
import { Stream, Effect } from "effect";

// Stream with buffer
const stream = Stream.fromAsyncIterable(fastProducer()).pipe(
  Stream.buffer({ capacity: 100 }) // Buffer max 100 items
);

// Consumer pulls when ready
const program = stream.pipe(
  Stream.mapEffect(data => slowConsumer(data)),
  Stream.runDrain
);
```

---

## 9.4 Resource Management

### 9.4.1 RAII Pattern (Resource Acquisition Is Initialization)

**Backend (C# using/Dispose):**

```csharp
// ✅ Automatic cleanup with 'using'
using (var db = new AppDbContext())
{
    var users = await db.Users.ToListAsync();
    // db.Dispose() called automatically
}

// Multiple resources
using var connection = new SqlConnection(connString);
using var command = new SqlCommand(query, connection);
using var reader = await command.ExecuteReaderAsync();
// All disposed in reverse order
```

**language-ext Bracket:**

```csharp
using LanguageExt;

// bracket = acquire → use → release
var program = bracket(
    acquire: IO.lift(() => new FileStream("file.txt", FileMode.Open)),
    use: stream => IO.liftAsync(async () =>
    {
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }),
    release: stream => IO.lift(() => stream.Dispose())
);

// Guarantee cleanup even on exception!
var content = program.Run();
```

### 9.4.2 Effect-TS Resource Management

**Effect.acquireRelease:**

```typescript
import { Effect } from "effect";

// Safe resource management
const program = Effect.acquireRelease(
  // Acquire
  Effect.sync(() => {
    console.log("Opening file");
    return openFile("data.txt");
  }),
  // Release (always called)
  file => Effect.sync(() => {
    console.log("Closing file");
    file.close();
  })
).pipe(
  // Use
  Effect.flatMap(file =>
    Effect.sync(() => file.read())
  )
);

// File closed even if error occurs!
```

**Scoped Resources:**

```typescript
import { Effect, Scope } from "effect";

// Database connection pool
const makePool = Effect.acquireRelease(
  Effect.sync(() => {
    console.log("Creating connection pool");
    return createPool();
  }),
  pool => Effect.sync(() => {
    console.log("Closing pool");
    pool.close();
  })
);

// Use pool in scope
const query = Effect.scoped(
  Effect.gen(function* (_) {
    const pool = yield* _(makePool);
    const connection = yield* _(pool.acquire());
    const result = yield* _(executeQuery(connection, "SELECT * FROM users"));
    return result;
  })
);
// Pool closed when scope exits
```

### 9.4.3 Memory Leaks และการป้องกัน

**Common Memory Leaks:**

**1. Event Listeners:**

```typescript
// ❌ Memory leak - listener never removed
class Component {
  constructor() {
    window.addEventListener('resize', this.handleResize);
  }

  handleResize = () => {
    // ...
  };
}

// ✅ Cleanup properly
class Component {
  constructor() {
    window.addEventListener('resize', this.handleResize);
  }

  destroy() {
    window.removeEventListener('resize', this.handleResize);
  }

  handleResize = () => {
    // ...
  };
}
```

**React useEffect cleanup:**

```typescript
import { useEffect } from "react";

function TodoList() {
  useEffect(() => {
    const subscription = todoStream.subscribe(handleTodo);

    // Cleanup function
    return () => {
      subscription.unsubscribe();
    };
  }, []);

  return <div>...</div>;
}
```

**2. Circular References:**

```typescript
// ❌ Circular reference
class Parent {
  children: Child[] = [];
}

class Child {
  parent: Parent; // Holds reference to parent
}

// ✅ Use WeakMap/WeakSet
const parentChildMap = new WeakMap<Parent, Child[]>();
```

**Backend (C#):**

```csharp
// ✅ Dispose IDisposable resources
public class UserService : IDisposable
{
    private readonly HttpClient _httpClient = new();
    private readonly AppDbContext _db = new();

    public void Dispose()
    {
        _httpClient.Dispose();
        _db.Dispose();
    }
}

// Use with 'using'
using var service = new UserService();
```

---

## 9.5 Production Best Practices

### 9.5.1 Structured Logging

**Backend (C# with Serilog):**

```csharp
using Serilog;

// Configure structured logging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/app-.txt", rollingInterval: RollingInterval.Day)
    .Enrich.FromLogContext()
    .CreateLogger();

// Log with context
Log.Information("User {UserId} created order {OrderId}",
    userId, orderId);

// Log with properties
using (LogContext.PushProperty("CorrelationId", correlationId))
{
    Log.Information("Processing request");
    // All logs in this scope include CorrelationId
}
```

**Integrate with language-ext:**

```csharp
// Logger capability
public interface LoggerIO
{
    K<M, Unit> LogInfo<M>(string message, params object[] args)
        where M : Monad<M>;

    K<M, Unit> LogError<M>(Exception ex, string message, params object[] args)
        where M : Monad<M>;
}

// Live implementation
public class LiveLoggerIO : LoggerIO
{
    public K<M, Unit> LogInfo<M>(string message, params object[] args)
        where M : Monad<M> =>
        M.LiftIO(() =>
        {
            Log.Information(message, args);
            return unit;
        });

    public K<M, Unit> LogError<M>(Exception ex, string message, params object[] args)
        where M : Monad<M> =>
        M.LiftIO(() =>
        {
            Log.Error(ex, message, args);
            return unit;
        });
}
```

**Frontend (TypeScript):**

```typescript
import { Effect, Context } from "effect";

// Structured logger service
interface Logger {
  readonly info: (message: string, context?: Record<string, unknown>) =>
    Effect.Effect<void, never, never>;
  readonly error: (message: string, error?: unknown, context?: Record<string, unknown>) =>
    Effect.Effect<void, never, never>;
}

const Logger = Context.GenericTag<Logger>("Logger");

// Production implementation (sends to backend)
const LoggerLive = Layer.succeed(
  Logger,
  Logger.of({
    info: (message, context) =>
      Effect.sync(() => {
        fetch('/api/logs', {
          method: 'POST',
          body: JSON.stringify({
            level: 'info',
            message,
            context,
            timestamp: new Date().toISOString()
          })
        });
      }),

    error: (message, error, context) =>
      Effect.sync(() => {
        fetch('/api/logs', {
          method: 'POST',
          body: JSON.stringify({
            level: 'error',
            message,
            error: String(error),
            context,
            timestamp: new Date().toISOString()
          })
        });
      })
  })
);
```

### 9.5.2 Error Tracking

**Backend (Sentry Integration):**

```csharp
using Sentry;

// Configure Sentry
SentrySdk.Init(options =>
{
    options.Dsn = "https://your-dsn@sentry.io/project";
    options.TracesSampleRate = 1.0;
    options.Environment = "production";
});

// Capture exception with context
try
{
    await ProcessOrder(orderId);
}
catch (Exception ex)
{
    SentrySdk.CaptureException(ex, scope =>
    {
        scope.SetTag("order_id", orderId);
        scope.SetUser(new User { Id = userId.ToString() });
        scope.SetExtra("order_details", orderDetails);
    });
    throw;
}
```

**Frontend (Sentry for JavaScript):**

```typescript
import * as Sentry from "@sentry/react";

// Initialize
Sentry.init({
  dsn: "https://your-dsn@sentry.io/project",
  environment: "production",
  integrations: [new Sentry.BrowserTracing()],
  tracesSampleRate: 1.0
});

// Capture with Effect-TS
const program = Effect.gen(function* (_) {
  try {
    const result = yield* _(riskyOperation());
    return result;
  } catch (error) {
    Sentry.captureException(error, {
      tags: { component: "TodoList" },
      extra: { userId, action: "create" }
    });
    return yield* _(Effect.fail(error));
  }
});
```

### 9.5.3 Monitoring และ Metrics

**Backend (Application Insights):**

```csharp
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

public class OrderService
{
    private readonly TelemetryClient _telemetry;

    // Track custom metric
    public async Task ProcessOrder(Order order)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await DoWork(order);

            // Track success metric
            _telemetry.TrackMetric("OrderProcessingTime",
                stopwatch.Elapsed.TotalMilliseconds);
            _telemetry.TrackEvent("OrderProcessed", new Dictionary<string, string>
            {
                ["OrderId"] = order.Id.ToString(),
                ["UserId"] = order.UserId.ToString()
            });
        }
        catch (Exception ex)
        {
            // Track failure
            _telemetry.TrackException(ex);
            throw;
        }
    }
}
```

**Frontend (Custom Metrics):**

```typescript
// Performance monitoring
const trackEffect = <A, E, R>(
  name: string,
  effect: Effect.Effect<A, E, R>
): Effect.Effect<A, E, R> =>
  Effect.gen(function* (_) {
    const start = performance.now();

    try {
      const result = yield* _(effect);
      const duration = performance.now() - start;

      // Send metric to analytics
      yield* _(
        Effect.sync(() => {
          analytics.track('effect_success', {
            name,
            duration,
            timestamp: Date.now()
          });
        })
      );

      return result;
    } catch (error) {
      const duration = performance.now() - start;

      yield* _(
        Effect.sync(() => {
          analytics.track('effect_error', {
            name,
            duration,
            error: String(error),
            timestamp: Date.now()
          });
        })
      );

      throw error;
    }
  });

// Usage
const fetchTodos = trackEffect(
  "fetchAllTodos",
  Effect.gen(function* (_) {
    const api = yield* _(TodoApi);
    return yield* _(api.fetchTodos());
  })
);
```

### 9.5.4 Health Checks

**Backend (ASP.NET Core):**

```csharp
// Startup.cs
services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>()
    .AddUrlGroup(new Uri("https://api.external.com/health"), "External API");

app.MapHealthChecks("/health");

// Custom health check
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly AppDbContext _db;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                "SELECT 1",
                cancellationToken);

            return HealthCheckResult.Healthy("Database is healthy");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Database is unhealthy",
                ex);
        }
    }
}
```

**Frontend (Health Check Endpoint):**

```typescript
import { Effect } from "effect";

// Check all services
const healthCheck = Effect.gen(function* (_) {
  const checks = yield* _(
    Effect.all([
      checkApiHealth(),
      checkCacheHealth(),
      checkWebSocketHealth()
    ], { concurrency: "unbounded" })
  );

  const allHealthy = checks.every(c => c.healthy);

  return {
    status: allHealthy ? "healthy" : "unhealthy",
    checks,
    timestamp: new Date().toISOString()
  };
});

// Expose endpoint
app.get('/health', async (req, res) => {
  const result = await Effect.runPromise(healthCheck);
  res.status(result.status === "healthy" ? 200 : 503).json(result);
});
```

---

## 9.6 Common Patterns และ Best Practices

### 9.6.1 Retry Pattern

**Backend (C# Polly):**

```csharp
using Polly;

// Retry up to 3 times with exponential backoff
var policy = Policy
    .Handle<HttpRequestException>()
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
        onRetry: (exception, timeSpan, retryCount, context) =>
        {
            Log.Warning("Retry {RetryCount} after {Delay}ms due to {Exception}",
                retryCount, timeSpan.TotalMilliseconds, exception.Message);
        });

// Use policy
var result = await policy.ExecuteAsync(async () =>
{
    return await httpClient.GetAsync("https://api.example.com/data");
});
```

**Frontend (Effect-TS):**

```typescript
import { Effect, Schedule } from "effect";

// Retry with exponential backoff
const fetchWithRetry = fetchTodos().pipe(
  Effect.retry(
    Schedule.exponential("100 millis").pipe(
      Schedule.compose(Schedule.recurs(3))
    )
  ),
  Effect.tap(() =>
    Effect.sync(() => console.log("Retrying..."))
  )
);

// Custom retry logic
const retryOnSpecificError = fetchTodos().pipe(
  Effect.retry(
    Schedule.recurWhile((error) =>
      error instanceof NetworkError
    ).pipe(
      Schedule.compose(Schedule.exponential("1 second")),
      Schedule.compose(Schedule.recurs(5))
    )
  )
);
```

### 9.6.2 Circuit Breaker Pattern

**แนวคิด:** ป้องกันการเรียก service ที่ล้มเหลว

```
State Machine:
         [Closed] ──(failures > threshold)──> [Open]
             ↑                                   |
             |                                   | (timeout)
             |                                   ↓
             └────(success)────────── [Half-Open]
```

**Backend (Polly):**

```csharp
using Polly;
using Polly.CircuitBreaker;

// Circuit breaker: open after 3 failures, try again after 30s
var circuitBreaker = Policy
    .Handle<HttpRequestException>()
    .CircuitBreakerAsync(
        handledEventsAllowedBeforeBreaking: 3,
        durationOfBreak: TimeSpan.FromSeconds(30),
        onBreak: (exception, duration) =>
        {
            Log.Warning("Circuit breaker opened for {Duration}ms", duration.TotalMilliseconds);
        },
        onReset: () =>
        {
            Log.Information("Circuit breaker reset");
        });

// Use
try
{
    var result = await circuitBreaker.ExecuteAsync(async () =>
    {
        return await CallExternalApi();
    });
}
catch (BrokenCircuitException)
{
    // Circuit is open - fail fast
    return CachedFallbackData();
}
```

**Frontend (Custom Implementation):**

```typescript
class CircuitBreaker<A, E> {
  private state: "closed" | "open" | "half-open" = "closed";
  private failureCount = 0;
  private lastFailureTime?: number;

  constructor(
    private threshold: number,
    private timeout: number
  ) {}

  execute(effect: Effect.Effect<A, E, never>): Effect.Effect<A, E | CircuitOpenError, never> {
    if (this.state === "open") {
      const now = Date.now();
      if (this.lastFailureTime && now - this.lastFailureTime > this.timeout) {
        this.state = "half-open";
      } else {
        return Effect.fail(new CircuitOpenError());
      }
    }

    return effect.pipe(
      Effect.tapError(() =>
        Effect.sync(() => {
          this.failureCount++;
          this.lastFailureTime = Date.now();

          if (this.failureCount >= this.threshold) {
            this.state = "open";
            console.log("Circuit breaker opened");
          }
        })
      ),
      Effect.tap(() =>
        Effect.sync(() => {
          if (this.state === "half-open") {
            this.state = "closed";
            this.failureCount = 0;
            console.log("Circuit breaker closed");
          }
        })
      )
    );
  }
}
```

### 9.6.3 Bulkhead Pattern

**แนวคิด:** แยก resource pools เพื่อป้องกัน cascade failures

```typescript
import { Effect, Semaphore } from "effect";

// Limit concurrent API calls
const createBulkhead = (maxConcurrent: number) =>
  Effect.gen(function* (_) {
    const semaphore = yield* _(Semaphore.make(maxConcurrent));

    return {
      execute: <A, E, R>(effect: Effect.Effect<A, E, R>) =>
        semaphore.withPermit(effect)
    };
  });

// Usage
const apiBulkhead = await Effect.runPromise(createBulkhead(10));

// Only 10 API calls can run concurrently
const results = await Effect.runPromise(
  Effect.all(
    requests.map(req => apiBulkhead.execute(callApi(req))),
    { concurrency: "unbounded" }
  )
);
```

### 9.6.4 Saga Pattern (Distributed Transactions)

**แนวคิด:** แทน distributed transactions ด้วย compensating actions

```typescript
import { Effect } from "effect";

// Step with compensation
interface SagaStep<A, E> {
  action: Effect.Effect<A, E, never>;
  compensation: (result: A) => Effect.Effect<void, never, never>;
}

// Execute saga
const executeSaga = <A, E>(steps: SagaStep<any, E>[]) =>
  Effect.gen(function* (_) {
    const completed: Array<{ result: any; compensation: any }> = [];

    try {
      for (const step of steps) {
        const result = yield* _(step.action);
        completed.push({ result, compensation: step.compensation });
      }

      return "success" as const;
    } catch (error) {
      // Compensate in reverse order
      for (const { result, compensation } of completed.reverse()) {
        yield* _(compensation(result));
      }

      return yield* _(Effect.fail(error));
    }
  });

// Example: Order saga
const createOrderSaga = (order: Order) => [
  {
    action: reserveInventory(order.items),
    compensation: (reservation) => releaseInventory(reservation)
  },
  {
    action: chargePayment(order.payment),
    compensation: (charge) => refundPayment(charge)
  },
  {
    action: createShipment(order),
    compensation: (shipment) => cancelShipment(shipment)
  }
];

// Execute
const result = await Effect.runPromise(
  executeSaga(createOrderSaga(order))
);
```

---

## 9.7 Common Pitfalls และ Troubleshooting

### 9.7.1 Stack Overflow จาก Recursion

**ปัญหา:**

```csharp
// ❌ Stack overflow with large lists!
int Sum(List<int> numbers)
{
    if (numbers.Count == 0) return 0;
    return numbers[0] + Sum(numbers.Skip(1).ToList());
}

Sum(Enumerable.Range(1, 100000).ToList()); // 💥 StackOverflowException
```

**แก้ไข: Tail Recursion**

```csharp
// ✅ Tail recursive (optimized by compiler)
int SumTailRec(List<int> numbers, int accumulator = 0)
{
    if (numbers.Count == 0) return accumulator;
    return SumTailRec(numbers.Skip(1).ToList(), accumulator + numbers[0]);
}
```

**แก้ไข: Iteration**

```csharp
// ✅ Iterative - no stack overflow
int SumIterative(List<int> numbers)
{
    var sum = 0;
    foreach (var n in numbers)
    {
        sum += n;
    }
    return sum;
}
```

**language-ext Solution:**

```csharp
// ✅ Fold (internally optimized)
var sum = numbers.Fold(0, (acc, n) => acc + n);
```

### 9.7.2 Memory Leaks จาก Closures

**ปัญหา:**

```typescript
// ❌ Memory leak - closures hold references
function createHandler() {
  const largeData = new Array(1000000).fill("data");

  return () => {
    console.log(largeData[0]); // Holds entire largeData in memory!
  };
}

const handlers = [];
for (let i = 0; i < 1000; i++) {
  handlers.push(createHandler()); // 1000 * 1M = 💥
}
```

**แก้ไข:**

```typescript
// ✅ Extract only what you need
function createHandler() {
  const largeData = new Array(1000000).fill("data");
  const firstItem = largeData[0]; // Extract needed data

  return () => {
    console.log(firstItem); // Only holds single string
  };
}
```

### 9.7.3 Promise/Effect Not Running

**ปัญหา:**

```typescript
// ❌ Effect created but never run!
const fetchData = Effect.gen(function* (_) {
  const data = yield* _(api.fetchData());
  return data;
});
// Nothing happens! Effect is lazy
```

**แก้ไข:**

```typescript
// ✅ Run the effect
const data = await Effect.runPromise(fetchData);

// Or in React
useEffect(() => {
  Effect.runPromise(fetchData).then(setData);
}, []);
```

### 9.7.4 Unhandled Errors

**ปัญหา:**

```typescript
// ❌ Error silently swallowed
const program = Effect.gen(function* (_) {
  const result = yield* _(riskyOperation());
  return result;
});

Effect.runPromise(program); // Error lost if riskyOperation fails!
```

**แก้ไข:**

```typescript
// ✅ Handle errors explicitly
const program = Effect.gen(function* (_) {
  const result = yield* _(riskyOperation());
  return result;
}).pipe(
  Effect.catchAll(error => {
    console.error("Error occurred:", error);
    return Effect.succeed(defaultValue);
  })
);

// Or use runPromiseExit
Effect.runPromiseExit(program).then(exit => {
  if (exit._tag === "Failure") {
    console.error("Program failed:", exit.cause);
  }
});
```

### 9.7.5 Performance Issues จาก N+1 Queries

**ปัญหา:**

```typescript
// ❌ N+1 query problem
const users = await fetchUsers(); // 1 query

for (const user of users) {
  const orders = await fetchOrders(user.id); // N queries!
  user.orders = orders;
}
```

**แก้ไข: Batch Loading**

```typescript
// ✅ Single query with join
const usersWithOrders = await db.query(`
  SELECT u.*, o.*
  FROM users u
  LEFT JOIN orders o ON o.user_id = u.id
`);

// ✅ DataLoader pattern (Effect-TS)
const orderLoader = createDataLoader(
  (userIds: string[]) =>
    Effect.gen(function* (_) {
      const orders = yield* _(fetchOrdersByUserIds(userIds));
      return groupBy(orders, o => o.userId);
    })
);

// Batches and caches requests
const orders1 = await orderLoader.load(userId1);
const orders2 = await orderLoader.load(userId2);
```

---

## 9.8 สรุป

### สิ่งที่ได้เรียนรู้ในบทนี้

#### 9.8.1 Performance Optimization
- **Lazy Evaluation** - คำนวณเมื่อต้องใช้จริง (Seq, Stream)
- **Memoization** - Cache ผลลัพธ์ pure functions
- **Structural Sharing** - Immutable data แชร์ memory
- **Benchmarking** - วัดผล performance อย่างเป็นระบบ

#### 9.8.2 Concurrency และ Parallelism
- **Parallel Execution** - ทำงานพร้อมกันบน multiple cores
- **Fiber** - Lightweight threads (Effect-TS)
- **Race Conditions** - ป้องกันด้วย immutability
- **Thread Safety** - Concurrent collections (C#)

#### 9.8.3 Stream Processing
- **Streams** - ประมวลผลข้อมูลขนาดใหญ่แบบ lazy
- **Backpressure** - จัดการ producer/consumer speed mismatch
- **Chunking** - แบ่ง stream เป็น batches

#### 9.8.4 Resource Management
- **RAII Pattern** - Automatic cleanup (using/Dispose)
- **Bracket** - Safe acquire/release
- **Memory Leaks** - ป้องกันและแก้ไข

#### 9.8.5 Production
- **Structured Logging** - Log with context
- **Error Tracking** - Sentry integration
- **Monitoring** - Metrics and dashboards
- **Health Checks** - Service availability

#### 9.8.6 Patterns
- **Retry** - Exponential backoff
- **Circuit Breaker** - Fail fast
- **Bulkhead** - Resource isolation
- **Saga** - Distributed transactions

### เมื่อไหร่ควรใช้อะไร?

| Scenario | Pattern | Tool |
|----------|---------|------|
| Large datasets | Streams | Seq<T>, Stream |
| Expensive computations | Memoization | memo(), Cache |
| Multiple independent tasks | Parallel | AsParallel(), Effect.all |
| Flaky external APIs | Retry + Circuit Breaker | Polly, Schedule |
| Resource cleanup | Bracket | using, acquireRelease |
| Distributed transactions | Saga | Custom implementation |

### Production Checklist

- [ ] Structured logging configured
- [ ] Error tracking (Sentry) enabled
- [ ] Metrics and monitoring dashboards
- [ ] Health check endpoints
- [ ] Retry policies for external calls
- [ ] Circuit breakers for critical services
- [ ] Resource cleanup (Dispose/bracket)
- [ ] Performance benchmarks established
- [ ] Memory profiling done
- [ ] Load testing completed

### บทสรุปของหนังสือ

เราได้เรียนรู้ Functional Programming ตั้งแต่พื้นฐานจนถึง production:

1. **Chapters 1-2**: ทำไมต้อง FP และแนวคิดพื้นฐาน
2. **Chapters 3-7**: Backend with C# language-ext v5
3. **Chapter 8**: Frontend with TypeScript Effect-TS
4. **Chapter 9**: Advanced topics และ production practices

**สิ่งสำคัญที่สุด:**
- **Type Safety** - Compiler ช่วยจับ bugs
- **Composition** - สร้างโปรแกรมใหญ่จากชิ้นเล็ก
- **Immutability** - ข้อมูลไม่เปลี่ยน = ง่ายต่อการ reason
- **Pure Functions** - Testable และ predictable
- **Effect Management** - จัดการ side effects อย่างเป็นระบบ

### ขั้นตอนถัดไป

1. **Practice** - เริ่มใช้ในโปรเจกต์จริง
2. **Start Small** - เริ่มจาก modules ใหม่
3. **Incremental Adoption** - ค่อยๆ แทนที่ code เก่า
4. **Team Learning** - แชร์ความรู้กับทีม
5. **Community** - เข้าร่วม FP communities

### Resources

**C# language-ext:**
- Documentation: https://louthy.github.io/language-ext/
- GitHub: https://github.com/louthy/language-ext
- Discord: language-ext community

**TypeScript Effect-TS:**
- Documentation: https://effect.website/
- GitHub: https://github.com/Effect-TS/effect
- Discord: Effect-TS community

**Books:**
- "Domain Modeling Made Functional" by Scott Wlaschin
- "Functional Programming in C#" by Enrico Buonanno
- "Composing Software" by Eric Elliott

**Courses:**
- F# for Fun and Profit (fsharpforfunandprofit.com)
- Functional-Programming-in-CSharp (GitHub learning path)

---

## แบบฝึกหัดท้ายบท

### ข้อ 1: Performance

เขียนฟังก์ชัน Fibonacci ทั้งแบบ recursive และ memoized แล้ว benchmark เปรียบเทียบ

```csharp
// TODO: Implement
int FibRecursive(int n) { }
Func<int, int> FibMemoized = Memo.Memoize<int, int>(n => { });
```

### ข้อ 2: Parallelism

เขียนโปรแกรมที่ process orders 1000 รายการแบบ parallel และวัดเวลา

```csharp
// TODO: Implement
async Task<List<OrderResult>> ProcessOrdersParallel(List<Order> orders)
```

### ข้อ 3: Stream

สร้าง stream ที่อ่าน CSV file ขนาดใหญ่ทีละ chunk และ process

```typescript
// TODO: Implement
const processCsvStream: Effect.Effect<void, Error, never> = ?
```

### ข้อ 4: Resource Management

เขียน bracket pattern สำหรับจัดการ database connection

```csharp
// TODO: Implement
K<IO, T> WithDatabase<T>(Func<DbConnection, IO<T>> use)
```

### ข้อ 5: Circuit Breaker

Implement circuit breaker สำหรับ API call ใน TypeScript

```typescript
// TODO: Implement
class CircuitBreaker<A, E> {
  execute(effect: Effect.Effect<A, E, never>): Effect.Effect<A, E | CircuitOpenError, never>
}
```

---

**ขอบคุณที่อ่านหนังสือเล่มนี้!**

**Happy Functional Programming!** 🎉
