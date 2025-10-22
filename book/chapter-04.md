# บทที่ 4: Has<M, RT, T>.ask Pattern - เจาะลึก

> เรียนรู้ทุกมุมมองของ Has<M, RT, T>.ask Pattern - หัวใจของ language-ext v5

---

## เนื้อหาในบทนี้

- ทบทวน Has Pattern
- ทำไมต้อง 3 Type Parameters
- Higher-Kinded Types ใน C#
- Type Constraints แต่ละตัวทำอะไร
- การ Compose Capabilities หลายตัว
- Advanced Patterns
- Common Mistakes และวิธีแก้
- Best Practices
- แบบฝึกหัด

---

## 4.1 ทบทวน Has Pattern

ใน Chapter 3 เราได้เห็น Has pattern แล้ว มาทบทวนอีกครั้งก่อนเจาะลึก

### The Discovery - พบ Syntax ที่ใช้งานได้

หลังจากทดลอง **5 รูปแบบ** ในที่สุดก็พบ syntax ที่ใช้งานได้:

```csharp
// ✅ ใช้งานได้!
from capability in Has<M, RT, DatabaseIO>.ask  // 3 params, lowercase
```

```csharp
// ❌ Compile error CS8926
from capability in Has<M, DatabaseIO>.Ask  // 2 params, uppercase
```

### Syntax Rules

| Context | Syntax | Params | Case |
|---------|--------|--------|------|
| **Runtime Implementation** | `Has<Eff<RT>, T>.Ask` | 2 | Uppercase |
| **Business Logic Usage** | `Has<M, RT, T>.ask` | 3 | Lowercase |

**ตัวอย่าง:**

```csharp
// Runtime - Implement capability (2 params, uppercase)
public record AppRuntime : Has<Eff<AppRuntime>, DatabaseIO>
{
    static K<Eff<AppRuntime>, DatabaseIO> Has<Eff<AppRuntime>, DatabaseIO>.Ask =>
        liftEff((Func<AppRuntime, DatabaseIO>)(rt => new LiveDatabaseIO(rt)));
}

// Business Logic - Use capability (3 params, lowercase)
public static class TodoService<M, RT>
    where RT : Has<M, DatabaseIO>
{
    public static K<M, List<Todo>> List() =>
        from db in Has<M, RT, DatabaseIO>.ask  // ← 3 params!
        from todos in GetTodos(db)
        select todos;
}
```

---

## 4.2 ทำไมต้อง 3 Type Parameters?

นี่คือคำถามสำคัญ - **ทำไม business logic ต้องใช้ 3 parameters แต่ runtime ใช้ 2?**

### เหตุผลจาก C# Type System

#### Problem: Static Abstract Interface Members

C# 11 เพิ่ม **static abstract interface members** ซึ่งใช้ใน language-ext v5:

```csharp
public interface Has<M, T>
    where M : Monad<M>
{
    static abstract K<M, T> Ask { get; }  // Static abstract property
}
```

**ปัญหา:** Static members ไม่สามารถเข้าถึงโดยตรงผ่าน interface ได้!

```csharp
// ❌ ไม่ได้ - Cannot access static member directly
var capability = Has<M, DatabaseIO>.Ask;

// Compiler error: CS8926
// A static virtual or abstract interface member can be accessed only on a type parameter.
```

#### Solution: Type Parameter Witness

วิธีแก้คือใช้ **type parameter เป็น witness** ที่บอก compiler ว่า "มี type ที่ implement interface นี้อยู่":

```csharp
// ✅ ได้ - RT เป็น witness ที่ implement Has<M, T>
public static K<M, T> Ask<M, RT, T>()
    where M : Monad<M>
    where RT : Has<M, T>
{
    return Has<M, RT, T>.ask;  // RT ทำหน้าที่ witness
}
```

### RT = Runtime Witness

**RT** ทำหน้าที่ 2 อย่าง:

1. **Witness** - พิสูจน์ว่ามี runtime ที่ provide capability `T`
2. **Type Evidence** - ให้ compiler รู้ว่าต้อง dispatch ไปที่ implementation ไหน

```csharp
// RT เป็น witness ว่า "มี runtime ที่มี DatabaseIO"
where RT : Has<M, DatabaseIO>

// Compiler ใช้ RT เพื่อหา implementation
from db in Has<M, RT, DatabaseIO>.ask
//                ^^
//                └─ RT witness!
```

### เปรียบเทียบกับ Haskell Type Classes

pattern นี้คล้ายกับ Haskell type class dictionaries:

**Haskell:**
```haskell
-- Type class
class Has m rt t where
  ask :: m t

-- Usage (compiler passes dictionary automatically)
getDatabase :: (Monad m, Has m rt DatabaseIO) => m DatabaseIO
getDatabase = ask
```

**C# (language-ext):**
```csharp
// Interface (like type class)
public interface Has<M, T> where M : Monad<M>
{
    static abstract K<M, T> Ask { get; }
}

// Usage (we pass RT explicitly as witness)
public static K<M, DatabaseIO> GetDatabase<M, RT>()
    where M : Monad<M>
    where RT : Has<M, DatabaseIO>
{
    return Has<M, RT, DatabaseIO>.ask;  // RT = dictionary
}
```

**ความแตกต่าง:** Haskell ส่ง dictionary โดยอัตโนมัติ, C# เราต้องส่ง RT เอง

---

## 4.3 Higher-Kinded Types ใน C#

### K<M, A> คืออะไร?

**K<M, A>** = "Kind" - แทน type `M<A>` ที่ generic over monad `M`

```csharp
// ปกติ C# ไม่มี higher-kinded types
// ❌ ไม่สามารถเขียนแบบนี้ได้
public static M<B> Map<M, A, B>(M<A> ma, Func<A, B> f)
    where M : Monad  // ❌ M ต้องเป็น type ไม่ใช่ type constructor

// language-ext v5 ใช้ K<M, A> แทน
// ✅ ใช้งานได้!
public static K<M, B> Map<M, A, B>(K<M, A> ma, Func<A, B> f)
    where M : Functor<M>
    return M.Map(ma, f);
```

### K<M, A> ทำงานอย่างไร?

**K<M, A>** คือ interface ที่ wrap type `M<A>`:

```csharp
// Simplified version
public interface K<M, A>
{
    // Marker interface - no members
}

// Monad types implement K
public struct Option<A> : K<Option, A>
{
    // ...
}

public struct Either<L, A> : K<Either<L>, A>
{
    // ...
}

public struct Eff<RT, A> : K<Eff<RT>, A>
{
    // ...
}
```

### ตัวอย่างการใช้งาน

```csharp
// Function generic over any monad
public static K<M, B> Map<M, A, B>(K<M, A> ma, Func<A, B> f)
    where M : Functor<M>
{
    return M.Map(ma, f);
}

// ใช้กับ Option
K<Option, int> opt = Some(42);
K<Option, int> result = Map(opt, x => x * 2);  // Some(84)

// ใช้กับ Either
K<Either<Error>, int> either = Right<Error, int>(42);
K<Either<Error>, int> result = Map(either, x => x * 2);  // Right(84)

// ใช้กับ Eff
K<Eff<RT>, int> eff = Eff<RT, int>.Pure(42);
K<Eff<RT>, int> result = Map(eff, x => x * 2);  // Eff containing 84
```

### ทำไมต้องใช้ K<M, A>?

**เพื่อให้เขียน generic code ได้:**

```csharp
// ✅ Generic function - ใช้ได้กับทุก monad!
public static K<M, C> Combine<M, A, B, C>(
    K<M, A> ma,
    K<M, B> mb,
    Func<A, B, C> f)
    where M : Monad<M>
{
    return M.Bind(ma, a => M.Map(mb, b => f(a, b)));
}

// ใช้กับ Option
var opt = Combine(Some(1), Some(2), (a, b) => a + b);  // Some(3)

// ใช้กับ Either
var either = Combine(
    Right<Error, int>(1),
    Right<Error, int>(2),
    (a, b) => a + b);  // Right(3)

// ใช้กับ Eff
var eff = Combine(
    Eff<RT, int>.Pure(1),
    Eff<RT, int>.Pure(2),
    (a, b) => a + b);  // Eff containing 3
```

---

## 4.4 Type Constraints แต่ละตัวทำอะไร

เมื่อเขียน service มักเห็น constraints เหล่านี้:

```csharp
public static class TodoService<M, RT>
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, DatabaseIO>, Has<M, LoggerIO>, Has<M, TimeIO>
```

มาดูว่าแต่ละตัวทำอะไร:

### 4.4.1 Monad<M>

**Monad<M>** = Monad พื้นฐาน ให้ `Pure` และ `Bind` (flatMap)

```csharp
public interface Monad<M>
{
    // Pure - lift value เข้า monad
    static abstract K<M, A> Pure<A>(A value);

    // Bind - flatMap/chain operations
    static abstract K<M, B> Bind<A, B>(K<M, A> ma, Func<A, K<M, B>> f);

    // และ methods อื่นๆ...
}
```

**ใช้เมื่อไหร่:** ต้องใช้เสมอ! ไม่งั้น LINQ syntax ใช้ไม่ได้

**ตัวอย่าง:**

```csharp
// ✅ ต้องมี Monad<M>
public static K<M, int> Calculate<M>()
    where M : Monad<M>  // ← จำเป็น!
{
    return
        from a in M.Pure(10)     // ← ต้องการ Pure
        from b in M.Pure(20)     // ← ต้องการ Pure
        select a + b;             // ← LINQ ใช้ Bind internally
}
```

### 4.4.2 MonadIO<M>

**MonadIO<M>** = Monad ที่รัน IO effects ได้

```csharp
public interface MonadIO<M> where M : Monad<M>
{
    // LiftIO - lift IO effect เข้า monad M
    static abstract K<M, A> LiftIO<A>(IO<A> io);
}
```

**ใช้เมื่อไหร่:** ต้องทำ async operations (database, file I/O, HTTP calls)

**ตัวอย่าง:**

```csharp
// ✅ ต้องมี MonadIO<M>
public static K<M, List<Todo>> GetTodos<M, RT>()
    where M : Monad<M>, MonadIO<M>  // ← MonadIO จำเป็น!
    where RT : Has<M, DatabaseIO>
{
    return
        from db in Has<M, RT, DatabaseIO>.ask
        from ctx in M.LiftIO(IO.lift(env => db.GetContext()))  // ← ต้องการ LiftIO
        from todos in M.LiftIO(IO.liftAsync((env) =>          // ← ต้องการ LiftIO
            ctx.Todos.ToListAsync(env.Token)))
        select todos;
}
```

**โครงสร้าง IO effects:**

```csharp
// IO.lift - สำหรับ sync operations
IO<A> io = IO.lift(env => GetContext());

// IO.liftAsync - สำหรับ async operations
IO<A> io = IO.liftAsync(async (env) => await GetDataAsync(env.Token));

// M.LiftIO - lift IO เข้า monad M
K<M, A> result = M.LiftIO(io);
```

### 4.4.3 Fallible<M>

**Fallible<M>** = Monad ที่จัดการ errors ได้

```csharp
public interface Fallible<M> where M : Monad<M>
{
    // Fail - สร้าง failed state
    static abstract K<M, A> Fail<A>(Error error);

    // Catch - จัดการ errors
    static abstract K<M, A> Catch<A>(K<M, A> ma, Func<Error, K<M, A>> handler);
}
```

**ใช้เมื่อไหร่:** ต้องการ error handling (ส่วนใหญ่จะใช้)

**ตัวอย่าง:**

```csharp
// ✅ ต้องมี Fallible<M>
public static K<M, Todo> GetTodo<M, RT>(int id)
    where M : Monad<M>, MonadIO<M>, Fallible<M>  // ← Fallible จำเป็น!
    where RT : Has<M, DatabaseIO>
{
    return
        from db in Has<M, RT, DatabaseIO>.ask
        from todoOpt in Database<M, RT>.liftIO((ctx, ct) =>
            ctx.Todos.FindAsync(id, ct))
        from todo in todoOpt.Match(
            Some: t => M.Pure(t),
            None: () => M.Fail<Todo>(Error.New($"Todo {id} not found")))  // ← ต้องการ Fail
        select todo;
}
```

**Extension methods ที่ใช้ Fallible:**

```csharp
// OptionExtensions.To<M, A> - ใช้ Fallible
public static K<M, A> To<M, A>(this Option<A> option, Error error)
    where M : Monad<M>, Fallible<M>
{
    return option.Match(
        Some: value => M.Pure(value),
        None: () => M.Fail<A>(error)  // ← ต้องการ Fallible
    );
}

// ValidationExtensions.To<M, A> - ใช้ Fallible
public static K<M, A> To<M, A>(this Validation<Error, A> validation)
    where M : Monad<M>, Fallible<M>
{
    return validation.Match(
        Succ: value => M.Pure(value),
        Fail: errors => M.Fail<A>(errors.Head)  // ← ต้องการ Fallible
    );
}
```

### 4.4.4 Has<M, T> Constraints

**Has<M, T>** = Runtime ต้องมี capability T

```csharp
where RT : Has<M, DatabaseIO>  // RT ต้องมี DatabaseIO
```

**ใช้เมื่อไหร่:** ทุกครั้งที่ต้องการใช้ capability

**ตัวอย่าง:**

```csharp
public static K<M, Todo> Create<M, RT>(string title, string description)
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, DatabaseIO>,   // ← ต้องการ database
               Has<M, LoggerIO>,     // ← ต้องการ logging
               Has<M, TimeIO>        // ← ต้องการ time
{
    return
        from _ in Logger<M, RT>.logInfo("Creating todo")     // ← ใช้ LoggerIO
        from now in Time<M, RT>.now()                        // ← ใช้ TimeIO
        from todo in SaveTodo(title, description, now)
        from saved in Database<M, RT>.save(todo)             // ← ใช้ DatabaseIO
        select saved;
}
```

### สรุป Constraints

| Constraint | ให้อะไร | ใช้เมื่อไหร่ |
|------------|---------|--------------|
| `Monad<M>` | Pure, Bind, LINQ | ต้องใช้เสมอ |
| `MonadIO<M>` | LiftIO | ทำ async I/O operations |
| `Fallible<M>` | Fail, Catch | Error handling |
| `Has<M, T>` | Capability T | ต้องการ capability นั้นๆ |

**Template พื้นฐาน:**

```csharp
// Service ที่ใช้ capabilities + async + errors
public static class MyService<M, RT>
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, DatabaseIO>, Has<M, LoggerIO>
{
    // ...
}
```

---

## 4.5 การ Compose Capabilities หลายตัว

มาดูวิธีการใช้หลาย capabilities ร่วมกัน

### 4.5.1 Sequential Composition

ใช้ LINQ syntax เพื่อเรียงลำดับ effects:

```csharp
public static K<M, Todo> CreateTodo<M, RT>(
    string title,
    string description)
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, DatabaseIO>,
               Has<M, LoggerIO>,
               Has<M, TimeIO>,
               Has<M, MetricsIO>
{
    return
        // 1. Log start
        from _ in Logger<M, RT>.logInfo($"Creating todo: {title}")

        // 2. Get current time
        from now in Time<M, RT>.now()

        // 3. Validate input
        from validated in TodoValidation.Validate(title, description)
            .To<M, Todo>()

        // 4. Create todo entity
        from todo in M.Pure(validated with { CreatedAt = now })

        // 5. Save to database
        from saved in Database<M, RT>.save(todo)

        // 6. Increment metrics
        from __ in Metrics<M, RT>.increment("todos_created")

        // 7. Log success
        from ___ in Logger<M, RT>.logInfo($"Created todo ID: {saved.Id}")

        select saved;
}
```

**Key Points:**
- แต่ละ `from` clause คือ 1 effect
- รันตามลำดับ (sequential)
- ถ้าขั้นตอนไหนล้มเหลว จะหยุดทันที (short-circuit)
- Variables จากขั้นตอนก่อนหน้าใช้ได้ในขั้นตอนถัดไป

### 4.5.2 Parallel Composition

ใช้ `Apply` สำหรับรัน effects พร้อมกัน:

```csharp
public static K<M, (User, List<Todo>, Settings)> GetDashboard<M, RT>(int userId)
    where M : Applicative<M>, MonadIO<M>
    where RT : Has<M, DatabaseIO>
{
    var userEffect = GetUser(userId);
    var todosEffect = GetUserTodos(userId);
    var settingsEffect = GetUserSettings(userId);

    // รัน 3 effects พร้อมกัน!
    return (userEffect, todosEffect, settingsEffect)
        .Apply((user, todos, settings) => (user, todos, settings))
        .As();
}
```

**เมื่อไหร่ควรใช้ Parallel:**
- Effects ไม่ depend กัน
- ต้องการ performance ดีขึ้น
- Database queries ที่ query ได้แยกกัน

### 4.5.3 Conditional Composition

ใช้ pattern matching เพื่อ branch logic:

```csharp
public static K<M, Todo> UpdateTodo<M, RT>(
    int id,
    UpdateTodoRequest request)
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, DatabaseIO>, Has<M, LoggerIO>
{
    return
        from existing in GetTodo(id)

        // Conditional: ถ้า completed status เปลี่ยน
        from updated in existing.IsCompleted != request.IsCompleted
            ? ToggleCompletionStatus(existing, request)
            : UpdateFieldsOnly(existing, request)

        from saved in Database<M, RT>.save(updated)

        select saved;
}

private static K<M, Todo> ToggleCompletionStatus<M, RT>(
    Todo existing,
    UpdateTodoRequest request)
    where M : Monad<M>
    where RT : Has<M, TimeIO>
{
    return
        from now in Time<M, RT>.now()
        from updated in M.Pure(existing with
        {
            IsCompleted = request.IsCompleted,
            CompletedAt = request.IsCompleted ? now : null
        })
        select updated;
}

private static K<M, Todo> UpdateFieldsOnly<M, RT>(
    Todo existing,
    UpdateTodoRequest request)
    where M : Monad<M>
{
    return M.Pure(existing with
    {
        Title = request.Title,
        Description = request.Description
    });
}
```

### 4.5.4 Loop Composition

ใช้ `TraverseSerial` หรือ `TraverseParallel` สำหรับ loops:

```csharp
// Sequential - ทีละตัว
public static K<M, List<Todo>> CreateMultiple<M, RT>(
    List<string> titles)
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, DatabaseIO>, Has<M, LoggerIO>
{
    return titles
        .Map(title => CreateTodo(title, ""))
        .TraverseSerial(identity)  // รันทีละตัว
        .As();
}

// Parallel - พร้อมกัน
public static K<M, List<Todo>> GetMultiple<M, RT>(
    List<int> ids)
    where M : Applicative<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, DatabaseIO>
{
    return ids
        .Map(id => GetTodo(id))
        .TraverseParallel(identity)  // รันพร้อมกัน!
        .As();
}
```

**ความแตกต่าง:**
- `TraverseSerial` - รันทีละตัว, หยุดทันทีเมื่อ error
- `TraverseParallel` - รันพร้อมกัน, รวดเร็วกว่า แต่ใช้ memory มากกว่า

---

## 4.6 Advanced Patterns

### 4.6.1 Optional Capabilities

บางครั้งต้องการ capability ที่อาจมีหรือไม่มีก็ได้:

```csharp
// Optional metrics - มีก็ดี ไม่มีก็ไม่เป็นไร
public static K<M, Todo> CreateWithOptionalMetrics<M, RT>(
    string title,
    string description)
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, DatabaseIO>, Has<M, LoggerIO>
{
    return
        from _ in Logger<M, RT>.logInfo("Creating todo")
        from todo in CreateTodoCore(title, description)

        // Try to increment metrics, but don't fail if not available
        from __ in TryIncrementMetrics<M, RT>(todo)

        select todo;
}

private static K<M, Unit> TryIncrementMetrics<M, RT>(Todo todo)
    where M : Monad<M>, Fallible<M>
{
    // Check if RT has MetricsIO
    if (typeof(RT).GetInterfaces().Any(i =>
        i.IsGenericType &&
        i.GetGenericTypeDefinition() == typeof(Has<,>) &&
        i.GetGenericArguments()[1] == typeof(MetricsIO)))
    {
        return Metrics<M, RT>.increment("todos_created");
    }

    // No metrics available - just return success
    return M.Pure(Unit.Default);
}
```

**หมายเหตุ:** Pattern นี้ใช้ reflection ดังนั้นมี performance overhead

### 4.6.2 Scoped Capabilities

สร้าง capability ที่มีชีวิตเฉพาะ scope:

```csharp
public static K<M, A> WithTransaction<M, RT, A>(K<M, A> operation)
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, DatabaseIO>
{
    return
        from db in Has<M, RT, DatabaseIO>.ask
        from ctx in M.LiftIO(IO.lift(env => db.GetContext()))

        // Begin transaction
        from tx in M.LiftIO(IO.liftAsync(async (env) =>
        {
            var transaction = await ctx.Database.BeginTransactionAsync(env.Token);
            return transaction;
        }))

        // Run operation
        from result in operation
            .Catch(error =>
            {
                // Rollback on error
                tx.Rollback();
                return M.Fail<A>(error);
            })

        // Commit transaction
        from _ in M.LiftIO(IO.liftAsync(async (env) =>
        {
            await tx.CommitAsync(env.Token);
            return Unit.Default;
        }))

        select result;
}

// Usage
var result = WithTransaction<M, RT, Todo>(
    CreateTodo("Test", "Description")
);
```

### 4.6.3 Caching Capability

สร้าง capability wrapper ที่มี caching:

```csharp
public class CachedDatabaseIO : DatabaseIO
{
    private readonly DatabaseIO _inner;
    private readonly IMemoryCache _cache;

    public CachedDatabaseIO(DatabaseIO inner, IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public AppDbContext GetContext()
    {
        // Cache context? อาจไม่เหมาะกับ DbContext
        // แต่แสดงแนวคิดการ wrap
        return _inner.GetContext();
    }

    public async Task<Option<Todo>> GetTodoById(int id)
    {
        var cacheKey = $"todo_{id}";

        if (_cache.TryGetValue<Todo>(cacheKey, out var cached))
        {
            return Optional(cached);
        }

        var ctx = GetContext();
        var todo = await ctx.Todos.FindAsync(id);

        if (todo != null)
        {
            _cache.Set(cacheKey, todo, TimeSpan.FromMinutes(5));
        }

        return Optional(todo);
    }
}

// Runtime with caching
public record AppRuntime : Has<Eff<AppRuntime>, DatabaseIO>
{
    private readonly IMemoryCache _cache;

    public AppRuntime(IServiceProvider services, IMemoryCache cache)
    {
        Services = services;
        _cache = cache;
    }

    static K<Eff<AppRuntime>, DatabaseIO> Has<Eff<AppRuntime>, DatabaseIO>.Ask =>
        liftEff((Func<AppRuntime, DatabaseIO>)(rt =>
            new CachedDatabaseIO(
                new LiveDatabaseIO(rt.Services),
                rt._cache)));
}
```

---

## 4.7 Common Mistakes และวิธีแก้

### Mistake 1: ใช้ 2 Parameters แทน 3

```csharp
// ❌ ผิด - CS8926 error
from db in Has<M, DatabaseIO>.Ask

// ✅ ถูกต้อง
from db in Has<M, RT, DatabaseIO>.ask
```

**วิธีจำ:** Business logic ใช้ **3 params, lowercase**

### Mistake 2: ลืม MonadIO Constraint

```csharp
// ❌ ผิด - CS0311 error
public static K<M, List<Todo>> GetTodos<M, RT>()
    where M : Monad<M>  // ← ลืม MonadIO!
    where RT : Has<M, DatabaseIO>
{
    return
        from db in Has<M, RT, DatabaseIO>.ask
        from todos in M.LiftIO(IO.liftAsync(...))  // ❌ Error! ไม่มี LiftIO
        select todos;
}

// ✅ ถูกต้อง
public static K<M, List<Todo>> GetTodos<M, RT>()
    where M : Monad<M>, MonadIO<M>  // ← เพิ่ม MonadIO
    where RT : Has<M, DatabaseIO>
```

**วิธีจำ:** ถ้าใช้ `M.LiftIO` ต้องมี `MonadIO<M>`

### Mistake 3: ไม่ได้ Cast ใน Runtime

```csharp
// ❌ ผิด - InvalidCastException
static K<Eff<AppRuntime>, DatabaseIO> Has<Eff<AppRuntime>, DatabaseIO>.Ask =>
    liftEff(rt => new LiveDatabaseIO(rt.Services));  // ← ไม่ได้ cast!

// ✅ ถูกต้อง
static K<Eff<AppRuntime>, DatabaseIO> Has<Eff<AppRuntime>, DatabaseIO>.Ask =>
    liftEff((Func<AppRuntime, DatabaseIO>)(rt => new LiveDatabaseIO(rt.Services)));
    //      ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    //      Cast explicitly!
```

**วิธีจำ:** ใน Runtime implementation ต้อง cast เป็น `Func<RT, T>` เสมอ

### Mistake 4: ใช้ Task<A> โดยตรง

```csharp
// ❌ ผิด - CS1503 error
from todos in M.LiftIO(ctx.Todos.ToListAsync(ct))  // ← Task<List<Todo>>

// ✅ ถูกต้อง
from todos in M.LiftIO(IO.liftAsync((env) =>
    ctx.Todos.ToListAsync(env.Token)))  // ← IO<List<Todo>>
```

**วิธีจำ:** `M.LiftIO` รับ `IO<A>` ไม่ใช่ `Task<A>` - ต้อง wrap ด้วย `IO.liftAsync`

### Mistake 5: Validation ไม่แปลงเป็น K<M, A>

```csharp
// ❌ ผิด - CS0029 error
from validated in TodoValidation.Validate(title, description)  // ← Validation<Error, Todo>

// ✅ ถูกต้อง
from validated in TodoValidation.Validate(title, description)
    .To<M, Todo>()  // ← K<M, Todo>
```

**วิธีจำ:** ใช้ `.To<M, A>()` extension เพื่อแปลง `Validation` เป็น `K<M, A>`

### Mistake 6: Option ไม่ได้ Handle None Case

```csharp
// ❌ ผิด - Option<Todo> ไม่ได้แปลงเป็น K<M, Todo>
from todoOpt in Database<M, RT>.findById(id)  // ← Option<Todo>
from updated in M.Pure(todoOpt with { Title = newTitle })  // ❌ Error!

// ✅ ถูกต้อง
from todoOpt in Database<M, RT>.findById(id)
from todo in todoOpt.To<M, Todo>($"Todo {id} not found")  // ← K<M, Todo>
from updated in M.Pure(todo with { Title = newTitle })
```

**วิธีจำ:** ใช้ `.To<M, A>(errorMessage)` เพื่อแปลง `Option` เป็น `K<M, A>`

---

## 4.8 Best Practices

### 1. Minimize Capabilities

ขอเฉพาะ capabilities ที่จำเป็น:

```csharp
// ❌ Over-constrained
public static K<M, DateTime> GetCurrentTime<M, RT>()
    where M : Monad<M>, MonadIO<M>, Fallible<M>  // ← ไม่จำเป็น!
    where RT : Has<M, DatabaseIO>,               // ← ไม่จำเป็น!
               Has<M, LoggerIO>,                 // ← ไม่จำเป็น!
               Has<M, TimeIO>                    // ← อันนี้พอ!

// ✅ Minimal constraints
public static K<M, DateTime> GetCurrentTime<M, RT>()
    where M : Monad<M>           // ← พอแล้ว
    where RT : Has<M, TimeIO>    // ← เฉพาะที่ใช้
```

**ทำไม?**
- ง่ายต่อการ test
- Reusable มากขึ้น
- Compiler เร็วขึ้น

### 2. Separate Pure และ Effectful Code

```csharp
// ✅ Pure function - ไม่มี effects
public static Todo CalculateUpdatedTodo(
    Todo existing,
    string newTitle,
    DateTime now)
{
    return existing with
    {
        Title = newTitle,
        UpdatedAt = now
    };
}

// ✅ Effectful function - ใช้ capabilities
public static K<M, Todo> UpdateTodo<M, RT>(int id, string newTitle)
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, DatabaseIO>, Has<M, TimeIO>
{
    return
        from existing in GetTodo(id)
        from now in Time<M, RT>.now()
        from updated in M.Pure(CalculateUpdatedTodo(existing, newTitle, now))  // ← Pure!
        from saved in Database<M, RT>.save(updated)
        select saved;
}
```

**ทำไม?**
- Test pure functions ง่ายกว่า (ไม่ต้อง mock)
- Reusable มากขึ้น
- Easier to reason about

### 3. Use Extension Methods for Conversions

```csharp
// ✅ สร้าง extension methods
public static class OptionExtensions
{
    public static K<M, A> To<M, A>(this Option<A> option, string errorMsg)
        where M : Monad<M>, Fallible<M>
    {
        return option.Match(
            Some: value => M.Pure(value),
            None: () => M.Fail<A>(Error.New(errorMsg))
        );
    }
}

// ✅ ใช้งาน - clean และ readable
from todo in todoOpt.To<M, Todo>("Todo not found")
```

### 4. Consistent Naming

```csharp
// ✅ Trait interfaces - suffix "IO"
public interface DatabaseIO { }
public interface LoggerIO { }
public interface TimeIO { }

// ✅ Live implementations - prefix "Live"
public class LiveDatabaseIO : DatabaseIO { }
public class LiveLoggerIO : LoggerIO { }
public class LiveTimeIO : TimeIO { }

// ✅ Capability modules - match trait name
public static class Database<M, RT> { }  // for DatabaseIO
public static class Logger<M, RT> { }    // for LoggerIO
public static class Time<M, RT> { }      // for TimeIO

// ✅ Service classes - suffix "Service"
public static class TodoService<M, RT> { }
public static class UserService<M, RT> { }
```

### 5. Document Required Capabilities

```csharp
/// <summary>
/// Creates a new todo with validation and metrics.
/// </summary>
/// <remarks>
/// Required capabilities:
/// - DatabaseIO: To save the todo
/// - LoggerIO: To log creation
/// - TimeIO: To set CreatedAt timestamp
/// - MetricsIO: To increment counter
/// </remarks>
public static K<M, Todo> Create<M, RT>(string title, string description)
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, DatabaseIO>,
               Has<M, LoggerIO>,
               Has<M, TimeIO>,
               Has<M, MetricsIO>
{
    // ...
}
```

---

## 4.9 Performance Considerations

### 4.9.1 Zero-Cost Abstractions

Has pattern คือ **zero-cost abstraction** - compile แล้วไม่มี overhead!

**At Runtime:**
```csharp
// Source code
from db in Has<M, RT, DatabaseIO>.ask
from ctx in M.LiftIO(IO.lift(env => db.GetContext()))
select ctx;

// Compiled to (simplified)
{
    var db = runtime.GetDatabaseIO();  // Static dispatch!
    var ctx = db.GetContext();
    return ctx;
}
```

**ไม่มี:**
- Virtual calls
- Interface vtable lookups
- Reflection
- Boxing/Unboxing

**ทำไม?** เพราะ compiler รู้ exact type ของ RT ตอน compile time!

### 4.9.2 Async Performance

การใช้ `IO.liftAsync` กับ `M.LiftIO` มี overhead น้อยมาก:

```csharp
// ✅ Efficient
from todos in M.LiftIO(IO.liftAsync((env) =>
    ctx.Todos.Where(t => t.UserId == userId).ToListAsync(env.Token)))

// Compiled to approximately
await ctx.Todos.Where(t => t.UserId == userId).ToListAsync(token);
```

**Tips:**
- ใช้ `IO.lift` สำหรับ sync operations
- ใช้ `IO.liftAsync` สำหรับ async operations
- อย่าใช้ `.Result` หรือ `.Wait()` - ใช้ async ตลอด

### 4.9.3 Memory Usage

**Stack-allocated Effects:**
```csharp
// Effects เป็น struct ไม่ใช่ class
public struct Eff<RT, A> : K<Eff<RT>, A>  // ← struct!
{
    // ...
}
```

**ดี:**
- ไม่มี heap allocation
- GC pressure น้อย
- Cache-friendly

**แต่:**
- Copying มี cost ถ้า struct ใหญ่
- ดังนั้น language-ext ออกแบบให้ effect structs เล็กมาก

---

## 4.10 สรุป

### สิ่งที่เรียนรู้ในบทนี้

1. **3 Parameters Syntax** - RT เป็น witness ที่พิสูจน์ว่ามี capability
2. **Higher-Kinded Types** - K<M, A> ช่วยให้เขียน generic code ได้
3. **Type Constraints** - แต่ละตัวมีหน้าที่ชัดเจน
4. **Composition Patterns** - Sequential, Parallel, Conditional, Loop
5. **Advanced Patterns** - Optional, Scoped, Caching capabilities
6. **Common Mistakes** - และวิธีแก้แต่ละอย่าง
7. **Best Practices** - Minimize constraints, separate pure/effectful
8. **Performance** - Zero-cost abstractions, efficient async

### Syntax Cheat Sheet

```csharp
// Runtime Implementation (2 params, uppercase)
public record AppRuntime : Has<Eff<AppRuntime>, DatabaseIO>
{
    static K<Eff<AppRuntime>, DatabaseIO> Has<Eff<AppRuntime>, DatabaseIO>.Ask =>
        liftEff((Func<AppRuntime, DatabaseIO>)(rt => new LiveDatabaseIO(rt)));
}

// Business Logic Usage (3 params, lowercase)
public static K<M, AppDbContext> GetContext<M, RT>()
    where M : Monad<M>, MonadIO<M>
    where RT : Has<M, DatabaseIO>
{
    return
        from db in Has<M, RT, DatabaseIO>.ask
        from ctx in M.LiftIO(IO.lift(env => db.GetContext()))
        select ctx;
}
```

### Constraints Template

```csharp
// Full-featured service template
public static class MyService<M, RT>
    where M : Monad<M>,      // ← LINQ support
              MonadIO<M>,    // ← Async I/O
              Fallible<M>    // ← Error handling
    where RT : Has<M, DatabaseIO>,  // ← Capabilities you need
               Has<M, LoggerIO>,
               Has<M, TimeIO>
{
    // Your methods here
}
```

---

## 4.11 แบบฝึกหัด

### แบบฝึกหัดที่ 1: Debug Compilation Error

โค้ดนี้ compile ไม่ผ่าน แก้ให้ถูกต้อง:

```csharp
public static K<M, User> GetUser<M, RT>(int userId)
    where M : Monad<M>
    where RT : Has<M, DatabaseIO>
{
    return
        from db in Has<M, DatabaseIO>.Ask
        from user in db.Users.FindAsync(userId)
        select user;
}
```

**คำถาม:**
1. Error ที่เจออะไรบ้าง?
2. แก้อย่างไร?

### แบบฝึกหัดที่ 2: Add Caching

เพิ่ม caching ให้กับ `GetUser` โดยใช้ `CacheIO` capability:

```csharp
public interface CacheIO
{
    Option<A> Get<A>(string key);
    Unit Set<A>(string key, A value, TimeSpan ttl);
}

// TODO: Implement caching in GetUser
```

### แบบฝึกหัดที่ 3: Batch Operations

เขียน function ที่ batch save todos:

```csharp
// TODO: Implement
public static K<M, List<Todo>> CreateBatch<M, RT>(
    List<CreateTodoRequest> requests)
    where M : ...
    where RT : ...
{
    // Hints:
    // 1. Validate ทั้งหมดก่อน (Validation)
    // 2. Save ทั้งหมดพร้อมกัน (Parallel)
    // 3. Log summary
}
```

### แบบฝึกหัดที่ 4: Implement Retry Logic

สร้าง `WithRetry` wrapper:

```csharp
// TODO: Implement
public static K<M, A> WithRetry<M, A>(
    K<M, A> operation,
    int maxRetries = 3,
    TimeSpan delay = default)
    where M : Monad<M>, Fallible<M>
{
    // Hints:
    // 1. Try operation
    // 2. If fail, wait and retry
    // 3. Max retries reached -> fail
}
```

---

**บทถัดไป:** [บทที่ 5: สร้าง Backend API ด้วย Capabilities](chapter-05.md)

ในบทถัดไป เราจะใช้ความรู้ที่ได้เรียนมาสร้าง Backend API แบบเต็มรูปแบบ!
