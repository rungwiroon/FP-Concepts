# บทที่ 3: แนะนำ language-ext v5

> เรียนรู้ library สำหรับ Functional Programming ใน C# และ Has<M, RT, T>.ask Pattern

---

## เนื้อหาในบทนี้

- language-ext คืออะไร และทำไมต้องใช้
- Version 5 แตกต่างจากเวอร์ชันเก่าอย่างไร
- Has<M, RT, T>.ask Pattern - แนวคิดหลัก
- การติดตั้งและ Setup โปรเจค
- ตัวอย่างแรก - สร้าง Simple Effect
- โครงสร้าง Architecture
- Extension Operators (v5.0.0-beta-56+)
- แบบฝึกหัด

---

## 3.1 language-ext คืออะไร?

**language-ext** คือ functional programming library สำหรับ C# ที่นำเสนอ types และ patterns จาก functional programming มาให้ใช้งานใน .NET

### ความสามารถหลัก

**1. Functional Types**
```csharp
// Option<T> - จัดการค่าที่อาจไม่มี
Option<string> name = Some("John");
Option<string> empty = None;

// Either<L, R> - Error handling แบบ type-safe
Either<Error, User> result = Right(new User("John"));
Either<Error, User> error = Left(Error.New("User not found"));

// Validation<E, A> - รวบรวม errors หลายตัว
Validation<Error, User> validation =
    (ValidateName(name), ValidateEmail(email))
        .Apply((n, e) => new User(n, e))
        .As();
```

**2. Effect System**
```csharp
// Eff<RT, A> - Effect ที่ต้องการ runtime
Eff<MyRuntime, string> effect =
    from name in GetName()
    from greeting in CreateGreeting(name)
    select greeting;

// รัน effect
var result = await effect.RunAsync(runtime, EnvIO.New());
```

**3. Higher-Kinded Types**
```csharp
// K<M, A> - Generic over monad type
K<M, int> generic = M.Pure(42);

// ใช้ได้กับ monad ใดก็ได้
K<Option, int> opt = Some(42);
K<Either<Error>, int> either = Right(42);
K<Eff<RT>, int> eff = EffMaybe(42);
```

**4. LINQ Query Syntax**
```csharp
// เขียน monadic operations แบบ natural
from user in GetUser(userId)
from orders in GetOrders(user.Id)
from _ in LogInfo($"Found {orders.Count} orders")
select orders;
```

---

## 3.2 ทำไมต้องใช้ language-ext?

### C# Vanilla vs language-ext

**ปัญหาของ C# ธรรมดา:**

```csharp
// ❌ Null reference exceptions
public async Task<string> GetUserEmail(int userId)
{
    var user = await _db.Users.FindAsync(userId);
    return user.Email;  // 💥 NullReferenceException ถ้า user เป็น null!
}

// ❌ Exceptions ที่ไม่คาดคิด
public decimal Divide(decimal a, decimal b)
{
    return a / b;  // 💥 DivideByZeroException!
}

// ❌ Side effects ที่ซ่อนอยู่
public User GetUser(int id)
{
    Console.WriteLine($"Getting user {id}");  // Side effect!
    _metrics.Increment("get_user");           // Side effect!
    return _db.Users.Find(id);                // Side effect!
}
```

**แก้ด้วย language-ext:**

```csharp
// ✅ Type-safe null handling
public Eff<RT, string> GetUserEmail(int userId)
    where RT : Has<Eff<RT>, DatabaseIO>
{
    return
        from user in Database<Eff<RT>, RT>.get<User>(userId)
        from email in user.Email.ToEff($"User {userId} has no email")
        select email;
}

// ✅ Type-safe division
public Either<Error, decimal> Divide(decimal a, decimal b)
{
    if (b == 0)
        return Left(Error.New("Cannot divide by zero"));

    return Right(a / b);
}

// ✅ Explicit effects
public Eff<RT, User> GetUser<RT>(int id)
    where RT : Has<Eff<RT>, DatabaseIO>,
               Has<Eff<RT>, LoggerIO>,
               Has<Eff<RT>, MetricsIO>
{
    return
        from _ in Logger<Eff<RT>, RT>.logInfo($"Getting user {id}")
        from __ in Metrics<Eff<RT>, RT>.increment("get_user")
        from user in Database<Eff<RT>, RT>.get<User>(id)
        select user;
}
```

### ข้อดีหลัก

1. **Type Safety** - Compiler บังคับให้ handle errors
2. **Composability** - ต่อ effects เข้าด้วยกันได้ง่าย
3. **Testability** - Mock dependencies ได้ง่าย
4. **Explicitness** - Side effects ชัดเจน ไม่ซ่อน
5. **Refactorability** - เปลี่ยนโค้ดได้โดยไม่กลัวพัง

---

## 3.3 language-ext v5 แตกต่างอย่างไร?

### เปรียบเทียบ v4 vs v5

| Feature | v4 | v5 |
|---------|----|----|
| **Higher-Kinded Types** | ไม่มี | ✅ `K<M, A>` |
| **Generic Monads** | แยกกัน | ✅ Unified `Monad<M>` |
| **Effect System** | `Aff<RT, A>` | ✅ `Eff<RT, A>` + `IO<A>` |
| **Capabilities** | Manual DI | ✅ `Has<M, RT, T>` pattern |
| **Performance** | ดี | ✅ ดีกว่า (zero-cost abstractions) |

### v5 ใช้ Higher-Kinded Types

**v4 - แยกกันทุก type:**
```csharp
// v4 - แต่ละ type มี method แยก
public static Option<B> Map<A, B>(this Option<A> ma, Func<A, B> f);
public static Either<L, B> Map<L, A, B>(this Either<L, A> ma, Func<A, B> f);
public static Try<B> Map<A, B>(this Try<A> ma, Func<A, B> f);
// ... ต้องเขียนซ้ำสำหรับทุก type!
```

**v5 - Generic เดียวทำงานได้กับทุก monad:**
```csharp
// v5 - Generic function เดียว ใช้ได้กับทุก monad!
public static K<M, B> Map<M, A, B>(this K<M, A> ma, Func<A, B> f)
    where M : Functor<M>
{
    return M.Map(ma, f);
}

// ใช้กับ Option
K<Option, int> opt = Some(42).Map(x => x * 2);

// ใช้กับ Either ก็ได้
K<Either<Error>, int> either = Right(42).Map(x => x * 2);

// ใช้กับ Eff ก็ได้
K<Eff<RT>, int> eff = EffMaybe(42).Map(x => x * 2);
```

### v5 ใช้ Has<M, RT, T> Pattern

**v4 - Dependency Injection แบบธรรมดา:**
```csharp
// v4 - ต้อง inject แยกกัน
public class TodoService
{
    private readonly IDatabase _db;
    private readonly ILogger _logger;
    private readonly ITimeProvider _time;

    public TodoService(IDatabase db, ILogger logger, ITimeProvider time)
    {
        _db = db;
        _logger = logger;
        _time = time;
    }

    public async Task<Todo> Create(string title)
    {
        _logger.LogInfo("Creating todo");
        var todo = new Todo { Title = title, CreatedAt = _time.Now() };
        await _db.Save(todo);
        return todo;
    }
}
```

**v5 - Capabilities แบบ functional:**
```csharp
// v5 - Capabilities ผ่าน type constraints
public static class TodoService<M, RT>
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, DatabaseIO>, Has<M, LoggerIO>, Has<M, TimeIO>
{
    public static K<M, Todo> Create(string title) =>
        from _ in Logger<M, RT>.logInfo("Creating todo")
        from now in Time<M, RT>.now()
        from todo in M.Pure(new Todo { Title = title, CreatedAt = now })
        from saved in Database<M, RT>.save(todo)
        select saved;
}
```

**ข้อดีของ Has pattern:**
- ✅ **Composable** - ต่อ capabilities ได้ง่าย
- ✅ **Type-safe** - Compiler เช็คว่ามี capability ครบไหม
- ✅ **Testable** - แทนที่ด้วย test implementation ได้ง่าย
- ✅ **Zero-cost** - Compile เป็น static dispatch (ไม่มี virtual call)

---

## 3.4 Has<M, RT, T>.ask Pattern

นี่คือหัวใจหลักของ language-ext v5! Pattern นี้ช่วยให้เราเขียน **capability-based architecture** แบบ functional

### แนวคิด

**Has<M, RT, T>** = "Runtime `RT` มี capability `T` สำหรับ monad `M`"

```csharp
where RT : Has<M, DatabaseIO>
// แปลว่า: RT ต้องมี capability DatabaseIO สำหรับ monad M
```

### 3 ส่วนหลัก

**1. Trait Interface (What) - กำหนดว่ามี operations อะไรบ้าง**

```csharp
// Infrastructure/Traits/DatabaseIO.cs
public interface DatabaseIO
{
    AppDbContext GetContext();
}
```

**2. Live Implementation (How) - ทำงานจริง**

```csharp
// Infrastructure/Live/LiveDatabaseIO.cs
public class LiveDatabaseIO : DatabaseIO
{
    private readonly IServiceProvider _services;

    public LiveDatabaseIO(IServiceProvider services)
    {
        _services = services;
    }

    public AppDbContext GetContext()
    {
        return _services.GetRequiredService<AppDbContext>();
    }
}
```

**3. Capability Module (API) - ใช้งาน capability**

```csharp
// Infrastructure/Capabilities/Database.cs
public static class Database<M, RT>
    where M : Monad<M>, MonadIO<M>
    where RT : Has<M, DatabaseIO>
{
    public static K<M, AppDbContext> getContext() =>
        from db in Has<M, RT, DatabaseIO>.ask  // ขอ capability
        from ctx in M.LiftIO(IO.lift(env => db.GetContext()))
        select ctx;
}
```

### Runtime ที่รวม Capabilities

```csharp
// Infrastructure/AppRuntime.cs
using static LanguageExt.Prelude;

public record AppRuntime(IServiceProvider Services) :
    Has<Eff<AppRuntime>, DatabaseIO>,
    Has<Eff<AppRuntime>, LoggerIO>,
    Has<Eff<AppRuntime>, TimeIO>
{
    // Implement Has trait - บอกว่า capability นี้มาจากไหน
    static K<Eff<AppRuntime>, DatabaseIO> Has<Eff<AppRuntime>, DatabaseIO>.Ask =>
        liftEff((Func<AppRuntime, DatabaseIO>)(rt =>
            new LiveDatabaseIO(rt.Services)));

    static K<Eff<AppRuntime>, LoggerIO> Has<Eff<AppRuntime>, LoggerIO>.Ask =>
        liftEff((Func<AppRuntime, LoggerIO>)(rt =>
            new LiveLoggerIO(rt.Services.GetRequiredService<ILogger<AppRuntime>>())));

    static K<Eff<AppRuntime>, TimeIO> Has<Eff<AppRuntime>, TimeIO>.Ask =>
        liftEff((Func<AppRuntime, TimeIO>)(_ =>
            new LiveTimeIO()));
}
```

### Syntax Rules - สำคัญมาก!

| Context | Syntax | Type Params | Case |
|---------|--------|-------------|------|
| **Implementation** (Runtime) | `Has<Eff<RT>, T>.Ask` | 2 params | Uppercase `.Ask` |
| **Consumption** (Business Logic) | `Has<M, RT, T>.ask` | 3 params | Lowercase `.ask` |

**ตัวอย่าง:**

```csharp
// ✅ ถูกต้อง - ใน Runtime (2 params, uppercase)
static K<Eff<AppRuntime>, DatabaseIO> Has<Eff<AppRuntime>, DatabaseIO>.Ask =>
    liftEff((Func<AppRuntime, DatabaseIO>)(rt => new LiveDatabaseIO(rt.Services)));

// ✅ ถูกต้อง - ใน Business Logic (3 params, lowercase)
public static K<M, AppDbContext> getContext() =>
    from db in Has<M, RT, DatabaseIO>.ask
    select db.GetContext();

// ❌ ผิด - จะ compile error CS8926
from db in Has<M, DatabaseIO>.Ask  // 2 params ใน business logic ใช้ไม่ได้!
```

---

## 3.5 การติดตั้งและ Setup

### 3.5.1 สร้างโปรเจค

```bash
# สร้าง ASP.NET Core project
dotnet new web -n TodoApp
cd TodoApp

# ติดตั้ง packages
dotnet add package LanguageExt.Core --version 5.0.0-beta-54
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 9.0.0
dotnet add package Microsoft.EntityFrameworkCore.Design --version 9.0.0
```

### 3.5.2 โครงสร้างโปรเจค

```
TodoApp/
├── Domain/                      # Domain entities
│   └── Todo.cs
├── Data/                        # Database context
│   └── AppDbContext.cs
├── Infrastructure/
│   ├── Traits/                  # Capability interfaces
│   │   ├── DatabaseIO.cs
│   │   ├── LoggerIO.cs
│   │   └── TimeIO.cs
│   ├── Live/                    # Production implementations
│   │   ├── LiveDatabaseIO.cs
│   │   ├── LiveLoggerIO.cs
│   │   └── LiveTimeIO.cs
│   ├── Capabilities/            # Capability modules
│   │   ├── Database.cs
│   │   ├── Logger.cs
│   │   └── Time.cs
│   ├── Extensions/              # Helper extensions
│   │   ├── OptionExtensions.cs
│   │   └── ValidationExtensions.cs
│   └── AppRuntime.cs            # Runtime with all capabilities
├── Features/
│   └── Todos/                   # Feature modules
│       ├── TodoService.cs
│       ├── TodoValidation.cs
│       └── TodoDtos.cs
└── Program.cs                   # API endpoints
```

### 3.5.3 Imports ที่จำเป็น

```csharp
// ใน file ที่ใช้ language-ext
using LanguageExt;
using LanguageExt.Traits;
using LanguageExt.Effects;
using LanguageExt.Common;
using static LanguageExt.Prelude;  // สำหรับ helper functions

// K<M, A> syntax
using K = LanguageExt.K;
```

---

## 3.6 ตัวอย่างแรก - Hello World Effect

มาสร้าง effect แรกกันครับ - ระบบทักทายง่ายๆ

### 3.6.1 สร้าง Trait

```csharp
// Infrastructure/Traits/ConsoleIO.cs
namespace TodoApp.Infrastructure.Traits;

using LanguageExt;

public interface ConsoleIO
{
    Unit WriteLine(string message);  // Return Unit แทน void
    string ReadLine();
}
```

### 3.6.2 สร้าง Live Implementation

```csharp
// Infrastructure/Live/LiveConsoleIO.cs
namespace TodoApp.Infrastructure.Live;

using LanguageExt;
using TodoApp.Infrastructure.Traits;

public class LiveConsoleIO : ConsoleIO
{
    public Unit WriteLine(string message)
    {
        Console.WriteLine(message);
        return Unit.Default;  // Return Unit
    }

    public string ReadLine()
    {
        return Console.ReadLine() ?? "";
    }
}
```

### 3.6.3 สร้าง Capability Module

```csharp
// Infrastructure/Capabilities/ConsoleCapability.cs
namespace TodoApp.Infrastructure.Capabilities;

using LanguageExt;
using LanguageExt.Traits;
using TodoApp.Infrastructure.Traits;

public static class ConsoleCapability<M, RT>
    where M : Monad<M>
    where RT : Has<M, ConsoleIO>
{
    // ง่ายมาก! เพราะ WriteLine return Unit แล้ว
    public static K<M, Unit> writeLine(string message) =>
        from console in Has<M, RT, ConsoleIO>.ask
        select console.WriteLine(message);

    public static K<M, string> readLine() =>
        from console in Has<M, RT, ConsoleIO>.ask
        select console.ReadLine();
}
```

### 3.6.4 สร้าง Runtime

```csharp
// Infrastructure/HelloRuntime.cs
namespace TodoApp.Infrastructure;

using LanguageExt;
using LanguageExt.Effects;
using LanguageExt.Traits;
using TodoApp.Infrastructure.Traits;
using TodoApp.Infrastructure.Live;
using static LanguageExt.Prelude;

public record HelloRuntime : Has<Eff<HelloRuntime>, ConsoleIO>
{
    static K<Eff<HelloRuntime>, ConsoleIO> Has<Eff<HelloRuntime>, ConsoleIO>.Ask =>
        liftEff((Func<HelloRuntime, ConsoleIO>)(_ => new LiveConsoleIO()));
}
```

### 3.6.5 เขียน Business Logic

```csharp
// Features/Hello/GreetingService.cs
namespace TodoApp.Features.Hello;

using LanguageExt;
using LanguageExt.Traits;
using TodoApp.Infrastructure.Traits;
using TodoApp.Infrastructure.Capabilities;

public static class GreetingService<M, RT>
    where M : Monad<M>
    where RT : Has<M, ConsoleIO>
{
    public static K<M, Unit> Greet() =>
        from _ in ConsoleCapability<M, RT>.writeLine("What is your name?")
        from name in ConsoleCapability<M, RT>.readLine()
        from __ in ConsoleCapability<M, RT>.writeLine($"Hello, {name}!")
        select Unit.Default;
}
```

### 3.6.6 รันจาก Main

```csharp
// Program.cs
using LanguageExt;
using LanguageExt.Effects;
using TodoApp.Infrastructure;
using TodoApp.Features.Hello;

var runtime = new HelloRuntime();

var effect = GreetingService<Eff<HelloRuntime>, HelloRuntime>.Greet();

var result = await effect.RunAsync(runtime, EnvIO.New());

result.Match(
    Succ: _ => Console.WriteLine("Success!"),
    Fail: error => Console.WriteLine($"Error: {error}")
);
```

### ทดสอบรัน

```bash
$ dotnet run
What is your name?
John
Hello, John!
Success!
```

---

## 3.7 อธิบายแต่ละส่วน

### 3.7.1 Trait - Interface สำหรับ Capability

```csharp
public interface ConsoleIO
{
    void WriteLine(string message);
    string ReadLine();
}
```

**Trait** คือ interface ที่กำหนดว่า capability นี้ทำอะไรได้บ้าง (What)

**หลักการ:**
- ✅ ใช้ชื่อที่สื่อความหมาย เช่น `DatabaseIO`, `LoggerIO`, `ConsoleIO`
- ✅ Method ควรเป็น synchronous (async จะอยู่ใน capability module)
- ✅ ไม่ควรมี implementation logic
- ✅ Return type ควรเป็น primitive, domain types, หรือ **Unit** (แทน void)
- ✅ **สำคัญ!** ใช้ `Unit` แทน `void` เพื่อให้ compose ใน LINQ ได้ง่าย

### 3.7.2 Live Implementation - การทำงานจริง

```csharp
public class LiveConsoleIO : ConsoleIO
{
    public void WriteLine(string message)
    {
        Console.WriteLine(message);  // Side effect ที่แท้จริง
    }

    public string ReadLine()
    {
        return Console.ReadLine() ?? "";  // Side effect ที่แท้จริง
    }
}
```

**Live Implementation** คือ class ที่ implement trait และทำงานจริง (How)

**หลักการ:**
- ✅ ชื่อขึ้นต้นด้วย `Live` เช่น `LiveDatabaseIO`, `LiveLoggerIO`
- ✅ ทำ side effects ได้ เช่น เขียน console, query database
- ✅ รับ dependencies ผ่าน constructor (IServiceProvider, Config, etc.)
- ✅ ไม่ควรมี business logic

### 3.7.3 Capability Module - API สำหรับใช้งาน

```csharp
public static class ConsoleCapability<M, RT>
    where M : Monad<M>
    where RT : Has<M, ConsoleIO>
{
    // ง่ายมาก! เพราะ WriteLine return Unit แล้ว
    public static K<M, Unit> writeLine(string message) =>
        from console in Has<M, RT, ConsoleIO>.ask  // ✅ 3 params, lowercase
        select console.WriteLine(message);  // ✅ ไม่ต้องห่อด้วย M.Pure
}
```

**Capability Module** คือ static class ที่ให้ API สำหรับใช้ capability

**หลักการ:**
- ✅ เป็น static class generic over `<M, RT>`
- ✅ Constrain `RT : Has<M, TraitType>`
- ✅ Methods return `K<M, A>` (monadic values)
- ✅ ใช้ `Has<M, RT, T>.ask` (3 params, lowercase) เพื่อขอ capability
- ✅ ถ้า trait method return Unit ไม่ต้องห่อด้วย `M.Pure` - แค่ `select` ได้เลย!
- ✅ ถ้า trait method ต้อง async ใช้ `M.LiftIO`

### 3.7.4 Runtime - รวม Capabilities

```csharp
public record HelloRuntime : Has<Eff<HelloRuntime>, ConsoleIO>
{
    static K<Eff<HelloRuntime>, ConsoleIO> Has<Eff<HelloRuntime>, ConsoleIO>.Ask =>
        liftEff((Func<HelloRuntime, ConsoleIO>)(_ => new LiveConsoleIO()));
}
```

**Runtime** คือ record ที่ implement `Has<Eff<RT>, T>` traits

**หลักการ:**
- ✅ เป็น `record` (immutable)
- ✅ Implement `Has<Eff<RT>, T>` สำหรับทุก capability ที่ต้องการ
- ✅ ใช้ `liftEff` เพื่อ lift function เป็น effect
- ✅ Cast เป็น `Func<RT, T>` explicitly
- ✅ สร้าง Live implementation ภายใน

### 3.7.5 Business Logic - LINQ Query Syntax

```csharp
public static K<M, Unit> Greet() =>
    from _ in ConsoleCapability<M, RT>.writeLine("What is your name?")
    from name in ConsoleCapability<M, RT>.readLine()
    from __ in ConsoleCapability<M, RT>.writeLine($"Hello, {name}!")
    select Unit.Default;
```

**Business Logic** ใช้ LINQ query syntax เพื่อเรียงลำดับ effects

**หลักการ:**
- ✅ ใช้ `from ... in ... select ...` syntax
- ✅ แต่ละบรรทัดคือ 1 effect
- ✅ Variable ที่ไม่ใช้ ตั้งชื่อ `_`, `__`, `___` (underscore)
- ✅ ไม่มี side effects โดยตรง - ทุกอย่างผ่าน capabilities
- ✅ Return type เป็น `K<M, A>`

### 3.7.6 Running Effects

```csharp
var runtime = new HelloRuntime();
var effect = GreetingService<Eff<HelloRuntime>, HelloRuntime>.Greet();
var result = await effect.RunAsync(runtime, EnvIO.New());
```

**การรัน Effect:**
1. สร้าง runtime instance
2. เรียก service method (ได้ `K<M, A>` effect)
3. เรียก `.RunAsync(runtime, env)` เพื่อรัน
4. ได้ `Fin<A>` result กลับมา

---

## 3.8 ตัวอย่างเพิ่มเติม - Todo Service

มาดูตัวอย่างที่สมจริงกว่า - Todo Service ที่ใช้หลาย capabilities

### Domain Entity

```csharp
// Domain/Todo.cs
namespace TodoApp.Domain;

public record Todo
{
    public int Id { get; init; }
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public bool IsCompleted { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}
```

### Database Capability

```csharp
// Infrastructure/Capabilities/Database.cs
namespace TodoApp.Infrastructure.Capabilities;

using LanguageExt;
using LanguageExt.Traits;
using TodoApp.Data;
using TodoApp.Infrastructure.Traits;

public static class Database<M, RT>
    where M : Monad<M>, MonadIO<M>
    where RT : Has<M, DatabaseIO>
{
    public static K<M, AppDbContext> getContext() =>
        from db in Has<M, RT, DatabaseIO>.ask
        from ctx in M.LiftIO(IO.lift(env => db.GetContext()))
        select ctx;

    // Helper สำหรับ database operations
    public static K<M, A> liftIO<A>(Func<AppDbContext, CancellationToken, Task<A>> f) =>
        from ctx in getContext()
        from result in M.LiftIO(IO.liftAsync((env) => f(ctx, env.Token)))
        select result;
}
```

### Logger Capability

```csharp
// Infrastructure/Capabilities/Logger.cs
namespace TodoApp.Infrastructure.Capabilities;

using LanguageExt;
using LanguageExt.Traits;
using TodoApp.Infrastructure.Traits;

public static class Logger<M, RT>
    where M : Monad<M>
    where RT : Has<M, LoggerIO>
{
    // ง่ายมาก! ถ้า LogInfo return Unit
    public static K<M, Unit> logInfo(string message) =>
        from logger in Has<M, RT, LoggerIO>.ask
        select logger.LogInfo(message);
}
```

### Time Capability

```csharp
// Infrastructure/Capabilities/Time.cs
namespace TodoApp.Infrastructure.Capabilities;

using LanguageExt;
using LanguageExt.Traits;
using TodoApp.Infrastructure.Traits;

public static class Time<M, RT>
    where M : Monad<M>
    where RT : Has<M, TimeIO>
{
    public static K<M, DateTime> now() =>
        from time in Has<M, RT, TimeIO>.ask
        select time.UtcNow();
}
```

### Todo Service

```csharp
// Features/Todos/TodoService.cs
namespace TodoApp.Features.Todos;

using LanguageExt;
using LanguageExt.Traits;
using Microsoft.EntityFrameworkCore;
using TodoApp.Domain;
using TodoApp.Infrastructure.Traits;
using TodoApp.Infrastructure.Capabilities;
using static LanguageExt.Prelude;

public static class TodoService<M, RT>
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, DatabaseIO>, Has<M, LoggerIO>, Has<M, TimeIO>
{
    // List all todos
    public static K<M, List<Todo>> List() =>
        from _ in Logger<M, RT>.logInfo("Listing all todos")
        from todos in Database<M, RT>.liftIO((ctx, ct) =>
            ctx.Todos.OrderByDescending(t => t.CreatedAt).ToListAsync(ct))
        from __ in Logger<M, RT>.logInfo($"Found {todos.Count} todos")
        select todos;

    // Create new todo
    public static K<M, Todo> Create(string title, string description) =>
        from _ in Logger<M, RT>.logInfo($"Creating todo: {title}")
        from now in Time<M, RT>.now()
        from todo in M.Pure(new Todo
        {
            Title = title,
            Description = description,
            IsCompleted = false,
            CreatedAt = now
        })
        from __ in Database<M, RT>.liftIO((ctx, ct) =>
            ctx.Todos.AddAsync(todo, ct).AsTask())
        from saved in Database<M, RT>.liftIO((ctx, ct) =>
            ctx.SaveChangesAsync(ct))
        from ___ in Logger<M, RT>.logInfo($"Created todo with ID: {todo.Id}")
        select todo;
}
```

**สังเกตว่า:**
- ใช้ 3 capabilities: `DatabaseIO`, `LoggerIO`, `TimeIO`
- ไม่มี side effects โดยตรง - ทุกอย่างผ่าน capabilities
- Compiler บังคับให้ระบุ capabilities ที่ต้องการใน type constraints
- ทดสอบได้ง่าย - แค่แทนที่ capabilities ด้วย test implementations

---

## 3.9 ข้อดีของ Architecture นี้

### 3.9.1 Type Safety

```csharp
// ❌ Compile error ถ้าลืม capability
public static K<M, Todo> Create<M, RT>(string title)
    where M : Monad<M>
    where RT : Has<M, DatabaseIO>  // มีแค่ DatabaseIO
{
    return
        from _ in Logger<M, RT>.logInfo("Creating")  // ❌ Error! ไม่มี LoggerIO
        from todo in Database<M, RT>.save(new Todo())
        select todo;
}

// ✅ ถูกต้อง - ระบุ capabilities ครบ
public static K<M, Todo> Create<M, RT>(string title)
    where M : Monad<M>
    where RT : Has<M, DatabaseIO>, Has<M, LoggerIO>  // ✅ ครบแล้ว
{
    return
        from _ in Logger<M, RT>.logInfo("Creating")  // ✅ OK!
        from todo in Database<M, RT>.save(new Todo())
        select todo;
}
```

### 3.9.2 Composability

```csharp
// สามารถต่อ effects ได้ง่าย
public static K<M, Unit> CreateMultiple<M, RT>(List<string> titles)
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, DatabaseIO>, Has<M, LoggerIO>, Has<M, TimeIO>
{
    return
        from _ in Logger<M, RT>.logInfo($"Creating {titles.Count} todos")
        from todos in titles.Map(title => Create(title, "")).TraverseSerial(identity)
        from __ in Logger<M, RT>.logInfo($"Created {todos.Count} todos")
        select Unit.Default;
}
```

### 3.9.3 Testability

```csharp
// Test implementation ที่ไม่มี side effects
public class TestDatabaseIO : DatabaseIO
{
    private readonly AppDbContext _context;

    public TestDatabaseIO()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
    }

    public AppDbContext GetContext() => _context;
}

public class TestLoggerIO : LoggerIO
{
    public List<string> Logs { get; } = new();

    public void LogInfo(string message) => Logs.Add($"INFO: {message}");
    public void LogError(string message) => Logs.Add($"ERROR: {message}");
}

// Test runtime
public record TestRuntime :
    Has<Eff<TestRuntime>, DatabaseIO>,
    Has<Eff<TestRuntime>, LoggerIO>,
    Has<Eff<TestRuntime>, TimeIO>
{
    public TestDatabaseIO Database { get; } = new();
    public TestLoggerIO Logger { get; } = new();
    public TestTimeIO Time { get; } = new();

    static K<Eff<TestRuntime>, DatabaseIO> Has<Eff<TestRuntime>, DatabaseIO>.Ask =>
        liftEff((Func<TestRuntime, DatabaseIO>)(rt => rt.Database));

    static K<Eff<TestRuntime>, LoggerIO> Has<Eff<TestRuntime>, LoggerIO>.Ask =>
        liftEff((Func<TestRuntime, LoggerIO>)(rt => rt.Logger));

    static K<Eff<TestRuntime>, TimeIO> Has<Eff<TestRuntime>, TimeIO>.Ask =>
        liftEff((Func<TestRuntime, TimeIO>)(rt => rt.Time));
}

// Test
[Fact]
public async Task Create_ShouldLogCorrectly()
{
    // Arrange
    var runtime = new TestRuntime();

    // Act
    var result = await TodoService<Eff<TestRuntime>, TestRuntime>
        .Create("Test", "Description")
        .RunAsync(runtime, EnvIO.New());

    // Assert
    Assert.True(result.IsSucc);
    Assert.Contains("Creating todo: Test", runtime.Logger.Logs);
}
```

---

## 3.10 Extension Operators (v5.0.0-beta-56+)

> ⚠️ **หมายเหตุ**: Feature นี้ต้องการ **.NET 10.0** และ **language-ext v5.0.0-beta-56** ขึ้นไป
> เป็น optional syntax - ยังคงใช้ LINQ query syntax ได้ตามปกติ

ตั้งแต่ v5.0.0-beta-56 language-ext เพิ่ม **Extension Operators** ที่ช่วยให้เขียนโค้ดสั้นลงโดยใช้ operators แทน method calls

### 3.10.1 Operators ที่เพิ่มเข้ามา

| Operator | ชื่อ | แทน | ตัวอย่าง |
|----------|------|-----|----------|
| `*` | Functor Map | `.Map()` | `(x => x + 1) * mx` |
| `>>` | Monad Bind/Sequence | `.Bind()` / `>>` | `mx >> my` |
| `\|` | Choice/Fallback | `.OrElse()` | `operation \| fallbackValue` |
| `+` (prefix) | Downcast | `.As()` | `+mx` |

### 3.10.2 ตัวอย่างการใช้งาน

**Functor Map (`*`)**

```csharp
// ❌ แบบเดิม (verbose)
var result = mx.Map(x => x + 1);

// ✅ ด้วย operator (concise)
var result = (x => x + 1) * mx;

// ตัวอย่างจริง
Option<int> opt = Some(10);
var doubled = ((int x) => x * 2) * opt;  // Some(20)
```

**Applicative (`*` หลายตัว)**

```csharp
// Apply function กับหลาย arguments
Option<int> mx = Some(1);
Option<int> my = Some(2);
Option<int> mz = Some(3);

// ❌ แบบเดิม
var result = (mx, my, mz).Apply((x, y, z) => x + y + z);

// ✅ ด้วย operators
var result = ((int x, int y, int z) => x + y + z) * mx * my * mz;
// result = Some(6)
```

**Monad Bind/Sequence (`>>`)**

```csharp
// Sequence - ทำ mx แล้วทำ my (ไม่สนใจผลลัพธ์ของ mx)
// ❌ แบบเดิม
var result = mx.Bind(_ => my);

// ✅ ด้วย operator
var result = mx >> my;

// Bind - ใช้ผลลัพธ์จาก mx ใน my
// ❌ แบบเดิม
var result = readLine.Bind(line => parseInt(line));

// ✅ ด้วย operator (ต้องระบุ type)
var result = readLine >> parseInt<IO>;
```

**Choice/Fallback (`|`)**

```csharp
// ❌ แบบเดิม
var result = operation.IfFail(fallbackValue);
var result = operation.IfFail(Error.New("Error message"));

// ✅ ด้วย operator
var result = operation | fallbackValue;
var result = operation | "Error message";

// ตัวอย่างจริง - Try with fallback
Option<int> maybe = None;
var withDefault = maybe | 0;  // 0

Either<Error, int> risky = Left(Error.New("failed"));
var safe = risky | 42;  // 42
```

**Downcast (`+` prefix)**

```csharp
// ❌ แบบเดิม - ต้องใส่ .As() และวงเล็บเยอะ
var result = (
    from x in GetValue()
    from y in Calculate(x)
    select x + y
).As();

// ✅ ด้วย operator - สะอาดกว่า
var result = +(
    from x in GetValue()
    from y in Calculate(x)
    select x + y
);
```

### 3.10.3 Types ที่รองรับ (v5.0.0-beta-57+)

Operators ถูก implement สำหรับ 15 core types:

- **Effect Types**: `IO<A>`, `Eff<A>`, `Eff<RT, A>`
- **Option/Either**: `Option<A>`, `Either<L, R>`, `OptionT<M, A>`, `EitherT<L, M, R>`
- **Validation**: `Validation<F, A>`, `ValidationT<F, M, A>`
- **Error Handling**: `Fin<A>`, `FinT<M, A>`, `Try<A>`, `TryT<M, A>`
- **Others**: `These<A, B>`, `ChronicleT<Ch, M, A>`

### 3.10.4 เปรียบเทียบ: LINQ vs Operators

```csharp
// ตัวอย่าง: Fetch user และ orders

// ✅ LINQ Query Syntax (recommended สำหรับ complex flows)
var result =
    from user in GetUser(userId)
    from orders in GetOrders(user.Id)
    from _ in LogInfo($"Found {orders.Count} orders")
    select orders;

// ✅ Operators (good for simple chains)
var result = GetUser(userId)
    >> (user => GetOrders(user.Id))
    >> (orders => LogInfo($"Found {orders.Count} orders") >> IO.Pure(orders));
```

### 3.10.5 เมื่อไหร่ควรใช้ Operators?

**✅ ใช้ Operators เมื่อ:**
- Chain operations สั้นๆ (2-3 steps)
- Map หรือ Apply ง่ายๆ
- ต้องการ fallback values
- คุ้นเคยกับ Haskell-style operators

**❌ ใช้ LINQ Query Syntax เมื่อ:**
- Logic ซับซ้อน (4+ steps)
- ต้องการ readability สูง
- ทำงานในทีมที่ไม่คุ้น FP operators
- ต้องรองรับ .NET versions ก่อน 10.0

> 💡 **คำแนะนำ**: เริ่มต้นด้วย LINQ query syntax ก่อน เพราะอ่านง่ายกว่า
> ค่อยๆ เปลี่ยนไปใช้ operators เมื่อคุ้นเคยและต้องการโค้ดที่กระชับขึ้น

---

## 3.11 สรุป

### สิ่งที่เรียนรู้ในบทนี้

1. **language-ext** คือ FP library สำหรับ C# ที่ให้ types และ patterns
2. **Version 5** ใช้ Higher-Kinded Types และ Has<M, RT, T> pattern
3. **Capability-based architecture** แยก What (Trait) / How (Live) / API (Capability)
4. **Has<M, RT, T>.ask** pattern ใช้สำหรับเรียก capabilities
5. **Runtime** รวม capabilities ทั้งหมด และกำหนด implementations
6. **Business logic** ใช้ LINQ syntax เพื่อ compose effects

### Syntax ที่ต้องจำ

```csharp
// Implementation (Runtime) - 2 params, uppercase
static K<Eff<RT>, T> Has<Eff<RT>, T>.Ask => ...

// Consumption (Business Logic) - 3 params, lowercase
from capability in Has<M, RT, T>.ask
```

### ข้อดีหลัก

- ✅ **Type-safe** - Compiler เช็คทุกอย่าง
- ✅ **Composable** - ต่อ effects ได้ง่าย
- ✅ **Testable** - Mock capabilities ได้ง่าย
- ✅ **Explicit** - Side effects ชัดเจน
- ✅ **Zero-cost** - No runtime overhead

---

## 3.12 แบบฝึกหัด

### แบบฝึกหัดที่ 1: สร้าง FileIO Capability

สร้าง capability สำหรับอ่าน/เขียนไฟล์

**ให้สร้าง:**
1. `FileIO` trait interface ที่มี methods:
   - `string ReadAllText(string path)`
   - `void WriteAllText(string path, string content)`
2. `LiveFileIO` implementation ที่ใช้ `System.IO.File`
3. `FileCapability<M, RT>` module
4. เพิ่ม `FileIO` ใน `AppRuntime`
5. เขียน service ที่ใช้ `FileIO` เพื่อบันทึก todo list ลงไฟล์

### แบบฝึกหัดที่ 2: สร้าง HttpClient Capability

สร้าง capability สำหรับเรียก external API

**ให้สร้าง:**
1. `HttpIO` trait interface
2. `LiveHttpIO` implementation
3. `HttpCapability<M, RT>` module
4. Service ที่เรียก external API เพื่อตรวจสอบ email

### แบบฝึกหัดที่ 3: Compose Multiple Capabilities

สร้าง service ที่ใช้ capabilities หลายตัว:
- `DatabaseIO` - บันทึก todo
- `LoggerIO` - log operation
- `TimeIO` - ตั้งเวลา
- `HttpIO` - notify external service
- `FileIO` - backup ลงไฟล์

---

**บทถัดไป:** [บทที่ 4: Has<M, RT, T>.ask Pattern](chapter-04.md)

ในบทถัดไป เราจะเจาะลึก Has<M, RT, T>.ask Pattern และเรียนรู้วิธีใช้ให้เต็มประสิทธิภาพ!
