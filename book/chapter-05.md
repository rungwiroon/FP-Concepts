# บทที่ 5: สร้าง Backend API ด้วย Capabilities

> ลงมือสร้าง RESTful API แบบเต็มรูปแบบด้วย language-ext v5

---

## เนื้อหาในบทนี้

- ภาพรวม Todo Application
- Project Setup
- Domain Layer - Todo Entity
- Infrastructure Layer - Capabilities ทั้งหมด
- Feature Layer - TodoService
- API Layer - ASP.NET Core Endpoints
- การรันและทดสอบ API
- Error Handling
- Best Practices
- แบบฝึกหัด

---

## 5.1 ภาพรวม Todo Application

เราจะสร้าง **RESTful API** สำหรับจัดการ Todo list โดยใช้ **Functional Programming** ทั้งระบบ

### Features

**CRUD Operations:**
- **GET /todos** - List all todos
- **GET /todos/{id}** - Get single todo
- **POST /todos** - Create new todo
- **PUT /todos/{id}** - Update todo
- **PATCH /todos/{id}/toggle** - Toggle completion
- **DELETE /todos/{id}** - Delete todo

### Technology Stack

**Backend:**
- ASP.NET Core 8.0 (Minimal APIs)
- language-ext v5.0.0-beta-54
- Entity Framework Core 9.0
- SQLite

### Architecture

```
TodoApp/
├── Domain/
│   └── Todo.cs                    # Domain entity
├── Data/
│   └── AppDbContext.cs            # EF Core context
├── Infrastructure/
│   ├── Traits/                    # Capability interfaces
│   │   ├── DatabaseIO.cs
│   │   ├── LoggerIO.cs
│   │   └── TimeIO.cs
│   ├── Live/                      # Production implementations
│   │   ├── LiveDatabaseIO.cs
│   │   ├── LiveLoggerIO.cs
│   │   └── LiveTimeIO.cs
│   ├── Capabilities/              # Capability modules
│   │   ├── Database.cs
│   │   ├── Logger.cs
│   │   └── Time.cs
│   ├── Extensions/                # Extension methods
│   │   ├── OptionExtensions.cs
│   │   └── ValidationExtensions.cs
│   └── AppRuntime.cs              # Runtime with capabilities
├── Features/
│   └── Todos/
│       ├── TodoService.cs         # Business logic
│       ├── TodoValidation.cs      # Validation rules
│       └── TodoDtos.cs            # DTOs
└── Program.cs                     # API endpoints & DI setup
```

### ภาพรวมการทำงาน

มาดูว่าเมื่อมี request เข้ามา แต่ละ layer ทำงานอย่างไร:

**Request Flow:**

```
1. Client ส่ง HTTP Request
   ↓
2. Program.cs (API Layer) - รับ request
   ↓
3. สร้าง AppRuntime (รวม capabilities ทั้งหมด)
   ↓
4. เรียก TodoService<M, RT> (Feature Layer)
   ↓
5. TodoService ใช้ Capabilities:
   - Logger<M, RT>.logInfo(...)      ← เรียก capability
   - Database<M, RT>.getTodoById(...) ← เรียก capability
   - Time<M, RT>.UtcNow               ← เรียก capability
   ↓
6. Capability Module ขอ trait จาก Runtime:
   - Has<M, RT, DatabaseIO>.ask      ← ได้ LiveDatabaseIO
   ↓
7. Live Implementation ทำงานจริง:
   - LiveDatabaseIO.GetTodoById(...)  ← Query database
   ↓
8. ผลลัพธ์กลับขึ้นมาเป็น K<M, Todo>
   ↓
9. .RunAsync() รัน effect
   ↓
10. ได้ Fin<Todo> (Success หรือ Fail)
   ↓
11. ToResult() แปลงเป็น IResult
   ↓
12. ส่ง HTTP Response กลับ Client
```

**ทำไมต้องแบ่ง layers แบบนี้?**

1. **Domain Layer** - Business entities (Todo)
   - ไม่รู้จัก database, HTTP, หรือ framework ใดๆ
   - Pure data structures
   - Immutable (ใช้ record)

2. **Infrastructure Layer** - Technical concerns
   - **Traits** - กำหนดว่ามี operations อะไรบ้าง (interface)
   - **Live** - Implementation จริงสำหรับ production
   - **Capabilities** - API สำหรับใช้งาน traits (generic over M, RT)
   - **Extensions** - Helper methods
   - **AppRuntime** - รวม capabilities ทั้งหมดเข้าด้วยกัน

3. **Feature Layer** - Business logic
   - TodoService - orchestrate capabilities เพื่อทำงาน business logic
   - TodoValidation - validation rules
   - TodoDtos - data transfer objects

4. **API Layer** - HTTP interface
   - Program.cs - รับ HTTP requests, เรียก services, ส่ง responses

**ข้อดีของการแบ่งแบบนี้:**

✅ **Testability** - แต่ละ layer test ได้อิสระ
- Domain → pure functions, ไม่ต้อง mock อะไร
- Infrastructure → สร้าง Test implementations (TestDatabaseIO)
- Feature → ใช้ TestRuntime แทน AppRuntime
- API → integration tests

✅ **Flexibility** - เปลี่ยน implementation ได้ง่าย
- ใช้ LiveDatabaseIO ใน production
- ใช้ TestDatabaseIO ใน tests
- ใช้ InMemoryDatabaseIO ใน development

✅ **Type Safety** - Compiler บังคับให้ระบุ capabilities
- TodoService ต้องการ DatabaseIO, LoggerIO, TimeIO
- Compiler เช็คว่า AppRuntime มี capabilities เหล่านี้
- ถ้าขาด จะ compile error

✅ **Maintainability** - โค้ดอ่านง่าย เข้าใจง่าย
- แต่ละ file มี responsibility ชัดเจน
- ไม่มี hidden dependencies
- LINQ syntax อ่านเหมือนภาษาธรรมดา

---

## 5.2 Project Setup

### 5.2.1 สร้าง Project

```bash
# สร้าง ASP.NET Core Web API project
dotnet new web -n TodoApp
cd TodoApp

# ติดตั้ง packages
dotnet add package LanguageExt.Core --version 5.0.0-beta-54
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 9.0.0
dotnet add package Microsoft.EntityFrameworkCore.Design --version 9.0.0
```

### 5.2.2 สร้าง Folder Structure

```bash
mkdir -p Domain
mkdir -p Data
mkdir -p Infrastructure/Traits
mkdir -p Infrastructure/Live
mkdir -p Infrastructure/Capabilities
mkdir -p Infrastructure/Extensions
mkdir -p Features/Todos
```

---

## 5.3 Domain Layer

### 5.3.1 Todo Entity

Domain entity ต้องเป็น **immutable** - ใช้ `record` ของ C#:

```csharp
// Domain/Todo.cs
using LanguageExt;

namespace TodoApp.Domain;

/// <summary>
/// Todo domain entity - immutable record.
/// All properties use 'init' for immutability.
/// </summary>
public record Todo
{
    public int Id { get; init; }
    public string Title { get; init; } = "";
    public string? Description { get; init; }
    public bool IsCompleted { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}
```

**Key Points:**
- ✅ `record` - Immutable by default
- ✅ `init` properties - Set เฉพาะตอน construction
- ✅ `with` expression - สร้าง copy พร้อมเปลี่ยนค่า
- ✅ ไม่มี setters - ป้องกัน mutation

**ตัวอย่างการใช้ `with`:**

```csharp
var todo = new Todo
{
    Id = 1,
    Title = "Learn FP",
    IsCompleted = false
};

// สร้าง copy ใหม่พร้อมเปลี่ยน IsCompleted
var completed = todo with { IsCompleted = true };

// todo เดิมไม่เปลี่ยน!
Console.WriteLine(todo.IsCompleted);       // false
Console.WriteLine(completed.IsCompleted);  // true
```

### 5.3.2 Database Context

```csharp
// Data/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using TodoApp.Domain;

namespace TodoApp.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Todo> Todos => Set<Todo>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Todo>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.IsCompleted).IsRequired();
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.CompletedAt);
        });
    }
}
```

---

## 5.4 Infrastructure Layer

Infrastructure Layer คือ **หัวใจของ capability-based architecture** - ที่นี่เราจะสร้าง capabilities ทั้งหมดที่ application ต้องการ

### ภาพรวม Infrastructure Layer

**มี 3 ส่วนหลักสำหรับแต่ละ capability:**

```
DatabaseIO Capability:
1. Trait (DatabaseIO.cs)          - Interface กำหนดว่ามี operations อะไร
   ↓
2. Live (LiveDatabaseIO.cs)       - Implementation จริงสำหรับ production
   ↓
3. Module (Database<M, RT>)       - API สำหรับใช้งาน capability
```

**ทำไมต้อง 3 ส่วน?**

1. **Trait** - "What" (อะไร)
   - กำหนดว่าต้องมี operations อะไรบ้าง
   - เป็น interface ธรรมดา
   - ไม่มี generic types (ใช้งานง่าย)

2. **Live** - "How" (ทำอย่างไร)
   - Implementation จริงสำหรับ production
   - เชื่อมต่อกับ external systems (database, logger, etc.)
   - มี side effects

3. **Module** - "API" (ใช้งานอย่างไร)
   - Generic over M (monad) และ RT (runtime)
   - ให้ API แบบ functional (return K<M, A>)
   - ห่อ side effects ด้วย M.LiftIO

**ตัวอย่าง flow การทำงาน:**

```csharp
// 1. Service เรียกใช้ Capability Module
from todo in Database<M, RT>.getTodoById(id)

// 2. Module ขอ trait จาก Runtime
from db in Has<M, RT, DatabaseIO>.ask  // ← ได้ LiveDatabaseIO

// 3. Module เรียก trait method
from result in M.LiftIO(IO.lift(_ => db.GetTodoById(id)))

// 4. Live Implementation query database จริง
var todo = dbContext.Todos.Find(id);
return Optional(todo);

// 5. ผลลัพธ์กลับขึ้นมาเป็น K<M, Option<Todo>>
```

### 5.4.1 DatabaseIO Capability

เริ่มจาก capability แรก - **DatabaseIO** สำหรับ database operations

**Trait Interface:**

```csharp
// Infrastructure/Traits/DatabaseIO.cs
using LanguageExt;
using TodoApp.Domain;

namespace TodoApp.Infrastructure.Traits;

/// <summary>
/// DatabaseIO capability - defines database operations.
/// Methods return Unit instead of void for better composition.
/// </summary>
public interface DatabaseIO
{
    // Query operations
    Option<Todo> GetTodoById(int id);
    List<Todo> GetAllTodos();

    // Command operations
    Todo AddTodo(Todo todo);
    Todo UpdateTodo(Todo todo);
    Unit DeleteTodo(Todo todo);
    Unit SaveChanges();
}
```

**Live Implementation:**

```csharp
// Infrastructure/Live/LiveDatabaseIO.cs
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using TodoApp.Data;
using TodoApp.Domain;
using TodoApp.Infrastructure.Traits;
using static LanguageExt.Prelude;

namespace TodoApp.Infrastructure.Live;

public class LiveDatabaseIO : DatabaseIO
{
    private readonly IServiceProvider _services;

    public LiveDatabaseIO(IServiceProvider services)
    {
        _services = services;
    }

    private AppDbContext GetContext() =>
        _services.GetRequiredService<AppDbContext>();

    public Option<Todo> GetTodoById(int id)
    {
        var ctx = GetContext();
        var todo = ctx.Todos.Find(id);
        return Optional(todo);
    }

    public List<Todo> GetAllTodos()
    {
        var ctx = GetContext();
        return ctx.Todos
            .OrderByDescending(t => t.CreatedAt)
            .ToList();
    }

    public Todo AddTodo(Todo todo)
    {
        var ctx = GetContext();
        ctx.Todos.Add(todo);
        return todo;
    }

    public Todo UpdateTodo(Todo todo)
    {
        var ctx = GetContext();
        ctx.Todos.Update(todo);
        return todo;
    }

    public Unit DeleteTodo(Todo todo)
    {
        var ctx = GetContext();
        ctx.Todos.Remove(todo);
        return Unit.Default;
    }

    public Unit SaveChanges()
    {
        var ctx = GetContext();
        ctx.SaveChanges();
        return Unit.Default;
    }
}
```

**Capability Module:**

```csharp
// Infrastructure/Capabilities/Database.cs
using LanguageExt;
using LanguageExt.Traits;
using TodoApp.Domain;
using TodoApp.Infrastructure.Traits;

namespace TodoApp.Infrastructure.Capabilities;

public static class Database<M, RT>
    where M : Monad<M>, MonadIO<M>
    where RT : Has<M, DatabaseIO>
{
    public static K<M, Option<Todo>> getTodoById(int id) =>
        from db in Has<M, RT, DatabaseIO>.ask
        from todo in M.LiftIO(IO.lift(_ => db.GetTodoById(id)))
        select todo;

    public static K<M, List<Todo>> getAllTodos() =>
        from db in Has<M, RT, DatabaseIO>.ask
        from todos in M.LiftIO(IO.lift(_ => db.GetAllTodos()))
        select todos;

    public static K<M, Todo> addTodo(Todo todo) =>
        from db in Has<M, RT, DatabaseIO>.ask
        from added in M.LiftIO(IO.lift(_ => db.AddTodo(todo)))
        from _ in M.LiftIO(IO.lift(_ => db.SaveChanges()))
        select added;

    public static K<M, Todo> updateTodo(Todo todo) =>
        from db in Has<M, RT, DatabaseIO>.ask
        from updated in M.LiftIO(IO.lift(_ => db.UpdateTodo(todo)))
        from _ in M.LiftIO(IO.lift(_ => db.SaveChanges()))
        select updated;

    public static K<M, Unit> deleteTodo(Todo todo) =>
        from db in Has<M, RT, DatabaseIO>.ask
        from _ in M.LiftIO(IO.lift(_ => db.DeleteTodo(todo)))
        from __ in M.LiftIO(IO.lift(_ => db.SaveChanges()))
        select Unit.Default;
}
```

### 5.4.2 LoggerIO Capability

**ทำไมต้องทำ Logger เป็น Capability?**

หลายคนอาจสงสัยว่า "Logging ก็แค่ `Console.WriteLine` หรือ `_logger.LogInformation` ธรรมดา ทำไมต้องทำเป็น capability ด้วย?"

**คำตอบ: Testability + Observability + Flexibility**

✅ **1. Testability** - Test ได้ว่า service log อะไรบ้าง
```csharp
// ใน test สร้าง TestLoggerIO ที่เก็บ log messages
public class TestLoggerIO : LoggerIO
{
    public List<string> Logs { get; } = new();
    public Unit LogInfo(string message)
    {
        Logs.Add($"INFO: {message}");
        return Unit.Default;
    }
}

// Test ว่า service log ถูกต้อง
var logger = new TestLoggerIO();
var result = await TodoService<...>.Create(...);
Assert.Contains("Creating todo:", logger.Logs[0]);
```

✅ **2. Flexibility** - เปลี่ยน logging backend ได้ง่าย
- Production → `LiveLoggerIO` ใช้ `ILogger` ของ ASP.NET Core
- Development → `ConsoleLoggerIO` log ลง console
- Testing → `TestLoggerIO` เก็บ logs ใน memory
- Disable → `NoOpLoggerIO` ไม่ log อะไรเลย

✅ **3. Structured Logging** - Log พร้อม context
```csharp
// จาก:
_logger.LogInfo($"Created todo ID: {id}");

// เป็น:
Logger<M, RT>.logInfo("Created todo {TodoId}", id);

// ได้ structured log:
// { "message": "Created todo 123", "TodoId": 123, "timestamp": "..." }
```

✅ **4. Composability** - Log เป็นส่วนหนึ่งของ effect chain
```csharp
from _ in Logger<M, RT>.logInfo("Starting operation")
from result in DoSomething()
from __ in Logger<M, RT>.logInfo("Operation completed")
select result
```

**Trait Interface:**

```csharp
// Infrastructure/Traits/LoggerIO.cs
using LanguageExt;

namespace TodoApp.Infrastructure.Traits;

public interface LoggerIO
{
    Unit LogInfo(string message);
    Unit LogWarning(string message);
    Unit LogError(string message);
}
```

**Live Implementation:**

```csharp
// Infrastructure/Live/LiveLoggerIO.cs
using LanguageExt;
using Microsoft.Extensions.Logging;
using TodoApp.Infrastructure.Traits;

namespace TodoApp.Infrastructure.Live;

public class LiveLoggerIO : LoggerIO
{
    private readonly ILogger _logger;

    public LiveLoggerIO(ILogger logger)
    {
        _logger = logger;
    }

    public Unit LogInfo(string message)
    {
        _logger.LogInformation(message);
        return Unit.Default;
    }

    public Unit LogWarning(string message)
    {
        _logger.LogWarning(message);
        return Unit.Default;
    }

    public Unit LogError(string message)
    {
        _logger.LogError(message);
        return Unit.Default;
    }
}
```

**Capability Module:**

```csharp
// Infrastructure/Capabilities/Logger.cs
using LanguageExt;
using LanguageExt.Traits;
using TodoApp.Infrastructure.Traits;

namespace TodoApp.Infrastructure.Capabilities;

public static class Logger<M, RT>
    where M : Monad<M>
    where RT : Has<M, LoggerIO>
{
    public static K<M, Unit> logInfo(string message) =>
        from logger in Has<M, RT, LoggerIO>.ask
        select logger.LogInfo(message);

    public static K<M, Unit> logWarning(string message) =>
        from logger in Has<M, RT, LoggerIO>.ask
        select logger.LogWarning(message);

    public static K<M, Unit> logError(string message) =>
        from logger in Has<M, RT, LoggerIO>.ask
        select logger.LogError(message);
}
```

### 5.4.3 TimeIO Capability

**ทำไม DateTime.UtcNow ต้องเป็น Capability?**

`DateTime.UtcNow` ดูเหมือน innocent value ธรรมดา แต่จริงๆมันคือ **side effect ที่แอบแฝง** (hidden side effect)!

**ปัญหาของ DateTime.UtcNow:**

❌ **1. Non-deterministic** - เรียกทีละครั้งได้ค่าต่างกัน
```csharp
var time1 = DateTime.UtcNow;
Thread.Sleep(1000);
var time2 = DateTime.UtcNow;
// time1 != time2 → ไม่ใช่ pure function!
```

❌ **2. Untestable** - Test ยากมาก
```csharp
// ❌ Test นี้จะ fail บางครั้ง (flaky test)
public void Should_Set_CreatedAt_To_Now()
{
    var todo = CreateTodo("Test");
    var now = DateTime.UtcNow;
    Assert.Equal(now, todo.CreatedAt);  // ← อาจต่างกัน 1-2 ms
}
```

❌ **3. Time-dependent tests** - ต้อง mock time ยาก
```csharp
// ❌ Test business logic ที่ depend on time
// "Todo ต้อง complete ภายใน 24 ชั่วโมง"
// จะ test อย่างไร?
```

**✅ Solution: ทำ Time เป็น Capability**

```csharp
// Production: ใช้ LiveTimeIO (DateTime.UtcNow จริง)
var runtime = new AppRuntime(services);

// Testing: ใช้ TestTimeIO (เวลาที่เรากำหนด)
public class TestTimeIO : TimeIO
{
    private DateTime _fixedTime;
    public TestTimeIO(DateTime fixedTime) => _fixedTime = fixedTime;
    public DateTime UtcNow() => _fixedTime;
}

// Test ได้แบบ deterministic!
var testTime = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
var testRuntime = new TestRuntime(new TestTimeIO(testTime));

var result = await TodoService<...>.Create(...);
Assert.Equal(testTime, result.CreatedAt);  // ✅ Pass เสมอ
```

**✅ Benefits:**

1. **Deterministic Tests** - เวลาคงที่ใน tests
2. **Time Travel** - Test future/past scenarios ได้
3. **Pure Functions** - Business logic ไม่ depend on hidden effects
4. **Composable** - เป็นส่วนหนึ่งของ effect chain

**ตัวอย่างการใช้งาน:**

```csharp
// Service ใช้ Time capability
from now in Time<M, RT>.UtcNow
from todo in M.Pure(new Todo
{
    CreatedAt = now,  // ← ได้จาก capability, ไม่ใช่ DateTime.UtcNow
    ...
})
```

**Trait Interface:**

```csharp
// Infrastructure/Traits/TimeIO.cs
namespace TodoApp.Infrastructure.Traits;

public interface TimeIO
{
    DateTime UtcNow();
}
```

**Live Implementation:**

```csharp
// Infrastructure/Live/LiveTimeIO.cs
using TodoApp.Infrastructure.Traits;

namespace TodoApp.Infrastructure.Live;

public class LiveTimeIO : TimeIO
{
    public DateTime UtcNow() => DateTime.UtcNow;
}
```

**Capability Module:**

```csharp
// Infrastructure/Capabilities/Time.cs
using LanguageExt;
using LanguageExt.Traits;
using TodoApp.Infrastructure.Traits;

namespace TodoApp.Infrastructure.Capabilities;

public static class Time<M, RT>
    where M : Monad<M>
    where RT : Has<M, TimeIO>
{
    public static K<M, DateTime> UtcNow =>
        from time in Has<M, RT, TimeIO>.ask
        select time.UtcNow();
}
```

### 5.4.4 Extension Methods

**ทำไมต้องมี Extension Methods .To<M, A>()?**

ปัญหาที่พบบ่อยใน Functional Programming คือ **type mismatch** ระหว่าง:
- `Option<A>` vs `K<M, A>`
- `Validation<Error, A>` vs `K<M, A>`
- `Either<L, R>` vs `K<M, A>`

**ปัญหา: ไม่สามารถ compose กันได้โดยตรง**

```csharp
// ❌ Compile error!
public static K<M, Todo> Get(int id)
{
    return
        from todoOpt in Database<M, RT>.getTodoById(id)  // ← Return K<M, Option<Todo>>
        from todo in todoOpt  // ← ❌ Option<Todo> ไม่ใช่ K<M, Todo>
        select todo;
}
```

**❌ วิธีแก้แบบเก่า: ใช้ Match + Throw**

```csharp
from todoOpt in Database<M, RT>.getTodoById(id)
from todo in M.Pure(todoOpt.Match(
    Some: t => t,
    None: () => throw new Exception("Not found")  // ❌ Throw exception!
))
```

**Problems:**
1. ❌ ใช้ exceptions (ไม่ functional)
2. ❌ Error ไม่ type-safe
3. ❌ ทำลาย referential transparency
4. ❌ โค้ดอ่านยาก

**✅ Solution: Extension Method .To<M, A>()**

Extension method ที่แปลง `Option<A>` → `K<M, A>` แบบ **type-safe และ purely functional**!

**OptionExtensions:**

```csharp
// Infrastructure/Extensions/OptionExtensions.cs
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Traits;

namespace TodoApp.Infrastructure.Extensions;

public static class OptionExtensions
{
    public static K<M, A> To<M, A>(
        this Option<A> option,
        Func<Error> errorFactory)
        where M : Monad<M>, Fallible<M>
    {
        return option.Match(
            Some: value => M.Pure(value),
            None: () => M.Fail<A>(errorFactory())
        );
    }

    public static K<M, A> To<M, A>(
        this Option<A> option,
        string errorMessage)
        where M : Monad<M>, Fallible<M>
    {
        return option.To<M, A>(() => Error.New(errorMessage));
    }
}
```

**การใช้งาน OptionExtensions:**

```csharp
// ✅ สะอาด type-safe ไม่มี exceptions
public static K<M, Todo> Get(int id)
{
    return
        from todoOpt in Database<M, RT>.getTodoById(id)  // K<M, Option<Todo>>
        from todo in todoOpt.To<M, Todo>(() =>
            Error.New(404, $"Todo {id} not found"))      // ← แปลงเป็น K<M, Todo>
        select todo;
}

// เมื่อ todoOpt = Some(todo) → return M.Pure(todo)
// เมื่อ todoOpt = None        → return M.Fail(Error.New(...))
```

**Benefits:**

✅ **1. Type-safe** - Errors เป็น `Error` type ไม่ใช่ exceptions
✅ **2. Composable** - ใช้ใน LINQ query ได้เลย
✅ **3. Purely functional** - ไม่มี side effects
✅ **4. Explicit** - Error message ชัดเจน
✅ **5. Testable** - Test ได้ง่ายว่า return error อะไร

**ValidationExtensions:**

```csharp
// Infrastructure/Extensions/ValidationExtensions.cs
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Traits;

namespace TodoApp.Infrastructure.Extensions;

public static class ValidationExtensions
{
    public static K<M, A> To<M, A>(
        this Validation<Error, A> validation)
        where M : Monad<M>, Fallible<M>
    {
        return validation.Match(
            Succ: value => M.Pure(value),
            Fail: errors => M.Fail<A>(errors.Head)
        );
    }
}
```

**การใช้งาน ValidationExtensions:**

```csharp
// ✅ แปลง Validation → K<M, A> แบบ seamless
public static K<M, Todo> Create(string title, string? description)
{
    return
        from now in Time<M, RT>.UtcNow
        from newTodo in M.Pure(new Todo { Title = title, ... })
        from validated in TodoValidation.Validate(newTodo).To<M, Todo>()  // ← แปลง!
        from saved in Database<M, RT>.addTodo(validated)
        select saved;
}

// เมื่อ validation สำเร็จ → M.Pure(todo)
// เมื่อ validation ล้มเหลว → M.Fail(error)
```

**ทำไมต้องมีทั้ง OptionExtensions และ ValidationExtensions?**

แต่ละตัวจัดการ error cases ที่ต่างกัน:

- **OptionExtensions** - สำหรับ **"ไม่พบข้อมูล"** (404 Not Found)
  - `Option<Todo>` → `K<M, Todo>`
  - Use case: Query database, ไม่เจอ = None

- **ValidationExtensions** - สำหรับ **"ข้อมูลไม่ถูกต้อง"** (400 Bad Request)
  - `Validation<Error, Todo>` → `K<M, Todo>`
  - Use case: Validate input, ผิด = Fail(errors)

```csharp
// ตัวอย่างการใช้ร่วมกัน
public static K<M, Todo> Update(int id, string title, string? description)
{
    return
        // 1. เช็คว่ามี todo ไหม (OptionExtensions)
        from existing in Get(id)  // ← ใช้ .To<M, Todo>() สำหรับ Option

        // 2. สร้าง updated todo
        from updated in M.Pure(existing with { Title = title, ... })

        // 3. Validate (ValidationExtensions)
        from validated in TodoValidation.Validate(updated).To<M, Todo>()  // ← สำหรับ Validation

        // 4. Save
        from saved in Database<M, RT>.updateTodo(validated)
        select saved;
}
```

### 5.4.5 AppRuntime

**AppRuntime คือศูนย์กลางของ Capabilities**

`AppRuntime` เป็น record ที่:
1. **เก็บ dependencies** (IServiceProvider)
2. **Implement Has<M, T>** สำหรับทุก capability
3. **Return Live implementations** เมื่อ service ขอ capability

**การทำงานของ AppRuntime:**

```csharp
// 1. Service ขอ DatabaseIO
from db in Has<Eff<AppRuntime>, AppRuntime, DatabaseIO>.ask

// 2. AppRuntime.Ask ถูกเรียก
static K<Eff<AppRuntime>, DatabaseIO> Has<Eff<AppRuntime>, DatabaseIO>.Ask =>
    liftEff((Func<AppRuntime, DatabaseIO>)(rt =>
        new LiveDatabaseIO(rt.Services)));  // ← สร้าง LiveDatabaseIO

// 3. Service ได้ DatabaseIO (จริงๆคือ LiveDatabaseIO)
// 4. Service เรียก db.GetTodoById(...)
// 5. LiveDatabaseIO query database จริง
```

**ทำไมต้องใช้ liftEff?**

`liftEff` = "ยก function ธรรมดาเป็น effect"

```csharp
// ฟังก์ชันธรรมดา: AppRuntime → DatabaseIO
(AppRuntime rt) => new LiveDatabaseIO(rt.Services)

// ยกขึ้นเป็น effect: K<Eff<AppRuntime>, DatabaseIO>
liftEff((Func<AppRuntime, DatabaseIO>)(rt => new LiveDatabaseIO(rt.Services)))
```

เมื่อ effect นี้ถูก run (`RunAsync`), มันจะ:
1. รับ `AppRuntime` เป็น parameter
2. เรียกฟังก์ชัน
3. Return `LiveDatabaseIO` instance

```csharp
// Infrastructure/AppRuntime.cs
using LanguageExt;
using LanguageExt.Effects;
using LanguageExt.Traits;
using Microsoft.Extensions.Logging;
using TodoApp.Infrastructure.Live;
using TodoApp.Infrastructure.Traits;
using static LanguageExt.Prelude;

namespace TodoApp.Infrastructure;

/// <summary>
/// Application runtime that provides all capabilities.
/// Implements Has<Eff<AppRuntime>, T> for each capability.
/// </summary>
public record AppRuntime(IServiceProvider Services) :
    Has<Eff<AppRuntime>, DatabaseIO>,
    Has<Eff<AppRuntime>, LoggerIO>,
    Has<Eff<AppRuntime>, TimeIO>
{
    static K<Eff<AppRuntime>, DatabaseIO> Has<Eff<AppRuntime>, DatabaseIO>.Ask =>
        liftEff((Func<AppRuntime, DatabaseIO>)(rt =>
            new LiveDatabaseIO(rt.Services)));

    static K<Eff<AppRuntime>, LoggerIO> Has<Eff<AppRuntime>, LoggerIO>.Ask =>
        liftEff((Func<AppRuntime, LoggerIO>)(rt =>
            new LiveLoggerIO(
                rt.Services.GetRequiredService<ILogger<AppRuntime>>())));

    static K<Eff<AppRuntime>, TimeIO> Has<Eff<AppRuntime>, TimeIO>.Ask =>
        liftEff((Func<AppRuntime, TimeIO>)(_ =>
            new LiveTimeIO()));
}
```

---

## 5.5 Feature Layer

**Feature Layer คือที่รวม Business Logic ทั้งหมด**

Feature Layer ประกอบด้วย 3 ส่วนหลัก:

**1. TodoService** - Business logic / Use cases
- Orchestrate capabilities เพื่อทำงานที่ต้องการ
- Generic over `M` (monad) และ `RT` (runtime)
- ใช้ LINQ query syntax เพื่อ compose effects
- ไม่รู้จัก implementation details (database, HTTP, etc.)

**2. TodoValidation** - Validation rules
- Domain-specific validation rules
- Use **Applicative validation** (รวบรวม errors ทั้งหมด)
- Return `Validation<Error, A>` แล้วใช้ `.To<M, A>()` แปลงเป็น effect

**3. TodoDtos** - Data Transfer Objects
- Request DTOs (CreateTodoRequest, UpdateTodoRequest)
- Response DTOs (TodoResponse)
- แยกจาก Domain entities (Todo)

**ทำไมต้องแยก?**

✅ **1. Single Responsibility**
- TodoService = business logic
- TodoValidation = validation rules
- TodoDtos = API contracts

✅ **2. Testability**
- Test validation rules แยกจาก service
- Test service โดยไม่ต้อง setup HTTP

✅ **3. Reusability**
- Validation rules ใช้ได้ทั้ง Create และ Update
- DTOs ใช้ได้กับหลาย endpoints

✅ **4. Maintainability**
- เปลี่ยน validation rules ที่เดียว
- DTO changes ไม่กระทบ domain

### 5.5.1 TodoValidation

```csharp
// Features/Todos/TodoValidation.cs
using LanguageExt;
using LanguageExt.Common;
using TodoApp.Domain;
using static LanguageExt.Prelude;

namespace TodoApp.Features.Todos;

public static class TodoValidation
{
    public static Validation<Error, Todo> Validate(Todo todo) =>
        (ValidateTitle(todo), ValidateDescription(todo))
            .Apply((_, _) => todo).As();

    private static Validation<Error, Todo> ValidateTitle(Todo todo) =>
        !string.IsNullOrWhiteSpace(todo.Title) && todo.Title.Length <= 200
            ? Success<Error, Todo>(todo)
            : Fail<Error, Todo>(Error.New("Title is required and must be less than 200 characters"));

    private static Validation<Error, Todo> ValidateDescription(Todo todo) =>
        todo.Description == null || todo.Description.Length <= 1000
            ? Success<Error, Todo>(todo)
            : Fail<Error, Todo>(Error.New("Description must be less than 1000 characters"));
}
```

**Applicative Validation - ทำไมใช้ `Apply`?**

```csharp
// ✅ Applicative - รวบรวม errors ทั้งหมด
(ValidateTitle(todo), ValidateDescription(todo))
    .Apply((_, _) => todo).As();

// ถ้า title และ description ผิดทั้งคู่
// จะได้ errors 2 ตัว: ["Title is required...", "Description must be..."]

// ❌ Monadic - หยุดที่ error แรก
from _ in ValidateTitle(todo)
from __ in ValidateDescription(todo)
select todo;

// ถ้า title ผิด จะได้แค่ error ของ title
// ไม่ได้เช็ค description เลย!
```

### 5.5.2 TodoDtos

```csharp
// Features/Todos/TodoDtos.cs
namespace TodoApp.Features.Todos;

public record CreateTodoRequest(string Title, string? Description);

public record UpdateTodoRequest(string Title, string? Description);

public record TodoResponse(
    int Id,
    string Title,
    string? Description,
    bool IsCompleted,
    DateTime CreatedAt,
    DateTime? CompletedAt
);
```

### 5.5.3 TodoService

**TodoService - Business Logic ที่เป็น Pure Functional**

`TodoService<M, RT>` คือ static class ที่รวม **use cases ทั้งหมด** ของ Todo feature:
- List all todos
- Get single todo
- Create new todo
- Update existing todo
- Toggle completion status
- Delete todo

**Key Design Principles:**

✅ **1. Generic over M and RT**
```csharp
public static class TodoService<M, RT>
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, DatabaseIO>, Has<M, LoggerIO>, Has<M, TimeIO>
```
- `M` = Monad type (เช่น `Eff<AppRuntime>`)
- `RT` = Runtime type ที่มี capabilities ที่ต้องการ
- Compiler บังคับให้ RT มีครบทุก capability

✅ **2. Pure Functions (ส่วนใหญ่)**
- ไม่มี mutable state
- ไม่ throw exceptions
- Return `K<M, A>` (effect description)
- Side effects อยู่ใน capabilities เท่านั้น

✅ **3. Declarative Style**
- ใช้ LINQ query syntax
- อ่านเหมือนภาษาธรรมดา
- Compose effects แบบ step-by-step

✅ **4. Error Handling แบบ Type-safe**
- ไม่ใช้ try-catch
- Errors เป็น `Error` type
- Use `.To<M, A>()` สำหรับ Option/Validation

**ตัวอย่างการทำงาน:**

```csharp
// Use case: Get todo by ID
public static K<M, Todo> Get(int id)
{
    // Step 1: Log ว่ากำลังทำอะไร
    from _ in Logger<M, RT>.logInfo($"Getting todo {id}")

    // Step 2: Query database
    from todoOpt in Database<M, RT>.getTodoById(id)  // K<M, Option<Todo>>

    // Step 3: Handle not found (Option → K<M, A>)
    from todo in todoOpt.To<M, Todo>(() =>
        Error.New(404, $"Todo {id} not found"))      // ← Type-safe error!

    // Step 4: Log success
    from __ in Logger<M, RT>.logInfo($"Found todo: {todo.Title}")

    // Step 5: Return result
    select todo;
}
```

**Benefits of This Approach:**

✅ **Testable** - ใช้ TestRuntime แทน AppRuntime
✅ **Composable** - Combine effects ได้ง่าย
✅ **Readable** - อ่านเหมือน step-by-step instructions
✅ **Type-safe** - Compiler เช็คว่ามี capabilities ครบ
✅ **Maintainable** - แก้ไขง่าย เพิ่ม features ง่าย

```csharp
// Features/Todos/TodoService.cs
using LanguageExt;
using LanguageExt.Common;
using LanguageExt.Traits;
using TodoApp.Domain;
using TodoApp.Infrastructure.Capabilities;
using TodoApp.Infrastructure.Extensions;
using TodoApp.Infrastructure.Traits;
using static LanguageExt.Prelude;

namespace TodoApp.Features.Todos;

/// <summary>
/// TodoService - all business logic using capabilities.
/// Generic over M (monad) and RT (runtime).
/// </summary>
public static class TodoService<M, RT>
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, DatabaseIO>, Has<M, LoggerIO>, Has<M, TimeIO>
{
    public static K<M, List<Todo>> List() =>
        from _ in Logger<M, RT>.logInfo("Listing all todos")
        from todos in Database<M, RT>.getAllTodos()
        from __ in Logger<M, RT>.logInfo($"Found {todos.Count} todos")
        select todos;

    public static K<M, Todo> Get(int id) =>
        from _ in Logger<M, RT>.logInfo($"Getting todo {id}")
        from todoOpt in Database<M, RT>.getTodoById(id)
        from todo in todoOpt.To<M, Todo>(() => Error.New(404, $"Todo {id} not found"))
        from __ in Logger<M, RT>.logInfo($"Found todo: {todo.Title}")
        select todo;

    public static K<M, Todo> Create(string title, string? description) =>
        from _ in Logger<M, RT>.logInfo($"Creating todo: {title}")
        from now in Time<M, RT>.UtcNow
        from newTodo in M.Pure(new Todo
        {
            Title = title,
            Description = description,
            IsCompleted = false,
            CreatedAt = now
        })
        from validated in TodoValidation.Validate(newTodo).To<M, Todo>()
        from saved in Database<M, RT>.addTodo(validated)
        from __ in Logger<M, RT>.logInfo($"Created todo ID: {saved.Id}")
        select saved;

    public static K<M, Todo> Update(int id, string title, string? description) =>
        from _ in Logger<M, RT>.logInfo($"Updating todo {id}")
        from existing in Get(id)
        from updated in M.Pure(existing with
        {
            Title = title,
            Description = description
        })
        from validated in TodoValidation.Validate(updated).To<M, Todo>()
        from saved in Database<M, RT>.updateTodo(validated)
        from __ in Logger<M, RT>.logInfo($"Updated todo {id}")
        select saved;

    public static K<M, Todo> ToggleComplete(int id) =>
        from _ in Logger<M, RT>.logInfo($"Toggling todo {id}")
        from existing in Get(id)
        from now in Time<M, RT>.UtcNow
        from toggled in M.Pure(existing with
        {
            IsCompleted = !existing.IsCompleted,
            CompletedAt = !existing.IsCompleted ? now : null
        })
        from updated in Database<M, RT>.updateTodo(toggled)
        from __ in Logger<M, RT>.logInfo(
            $"Todo {id} marked as {(updated.IsCompleted ? "completed" : "incomplete")}")
        select updated;

    public static K<M, Unit> Delete(int id) =>
        from _ in Logger<M, RT>.logInfo($"Deleting todo {id}")
        from existing in Get(id)
        from __ in Database<M, RT>.deleteTodo(existing)
        from ___ in Logger<M, RT>.logInfo($"Deleted todo {id}")
        select unit;
}
```

---

## 5.6 API Layer

**API Layer เชื่อมโลก HTTP กับโลก Functional**

API Layer (Program.cs) มีหน้าที่:

**1. รับ HTTP Requests**
- Parse request body (JSON → DTOs)
- Extract route parameters (id, query params)
- Get CancellationToken

**2. สร้าง AppRuntime**
- สร้าง runtime ที่มี capabilities ทั้งหมด
- ส่ง IServiceProvider เข้าไป (สำหรับ DbContext, Logger)

**3. เรียก TodoService**
- ส่ง parameters เข้าไปใน service method
- Get effect description กลับมา (K<M, A>)

**4. Run Effect**
- เรียก `.RunAsync(runtime, envIO)` เพื่อรัน effect
- ได้ `Fin<A>` กลับมา (Success หรือ Fail)

**5. แปลง Fin<A> → HTTP Response**
- Success → 200 OK, 201 Created, 204 No Content
- Fail → 404 Not Found, 500 Internal Server Error

**Helper: ToResult<A>**

`ToResult` คือ helper function ที่แปลง `Fin<A>` → `IResult`:

```csharp
static IResult ToResult<A>(Fin<A> result, Func<A, IResult> onSuccess) =>
    result.Match(
        Succ: onSuccess,                    // ← Success: ให้ caller กำหนด response
        Fail: error =>                      // ← Fail: แปลงเป็น Problem Details
        {
            var code = error.Code == 404 ? 404 : 500;
            return Results.Problem(
                detail: error.Message,
                statusCode: code);
        }
    );
```

**ทำไมต้องมี ToResult?**

✅ **1. Reusability** - ใช้ได้กับทุก endpoint
✅ **2. Consistency** - Error response format เหมือนกันทุก endpoint
✅ **3. Type-safety** - Compile-time checking
✅ **4. Separation** - แยก error handling ออกจาก endpoint logic

**Endpoint Pattern:**

ทุก endpoint ทำ 4 ขั้นตอนเดียวกัน:

```csharp
app.MapGet("/todos/{id:int}", async (
    int id,                              // 1. รับ parameters
    IServiceProvider services,
    CancellationToken ct) =>
{
    var runtime = new AppRuntime(services);  // 2. สร้าง runtime

    var result = await TodoService<Eff<AppRuntime>, AppRuntime>
        .Get(id)                             // 3. เรียก service
        .RunAsync(runtime, EnvIO.New(ct));   // 4. รัน effect

    return ToResult(result, todo =>          // 5. แปลง → HTTP response
        Results.Ok(MapToResponse(todo)));
});
```

**Benefits:**

✅ **Predictable** - ทุก endpoint ทำแบบเดียวกัน
✅ **Testable** - แยก test API layer กับ service layer ได้
✅ **Type-safe** - Compiler เช็ค type ตลอด
✅ **Maintainable** - แก้ไข pattern ที่เดียว ใช้ได้ทุกที่

### 5.6.1 Program.cs - Setup

```csharp
// Program.cs
using LanguageExt;
using LanguageExt.Effects;
using Microsoft.EntityFrameworkCore;
using TodoApp.Data;
using TodoApp.Domain;
using TodoApp.Features.Todos;
using TodoApp.Infrastructure;
using static LanguageExt.Prelude;

var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=todos.db"));

var app = builder.Build();

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// Helper to convert Fin<A> to IResult
static IResult ToResult<A>(Fin<A> result, Func<A, IResult> onSuccess) =>
    result.Match(
        Succ: onSuccess,
        Fail: error =>
        {
            var code = error.Code == 404 ? 404 : 500;
            return Results.Problem(
                detail: error.Message,
                statusCode: code);
        }
    );

// Mapper
static TodoResponse MapToResponse(Todo todo) => new(
    todo.Id,
    todo.Title,
    todo.Description,
    todo.IsCompleted,
    todo.CreatedAt,
    todo.CompletedAt
);
```

### 5.6.2 API Endpoints

```csharp
// GET /todos - List all
app.MapGet("/todos", async (IServiceProvider services, CancellationToken ct) =>
{
    var runtime = new AppRuntime(services);
    var result = await TodoService<Eff<AppRuntime>, AppRuntime>
        .List()
        .RunAsync(runtime, EnvIO.New(ct));

    return ToResult(result, todos =>
        Results.Ok(todos.Select(MapToResponse)));
});

// GET /todos/{id} - Get single
app.MapGet("/todos/{id:int}", async (
    int id,
    IServiceProvider services,
    CancellationToken ct) =>
{
    var runtime = new AppRuntime(services);
    var result = await TodoService<Eff<AppRuntime>, AppRuntime>
        .Get(id)
        .RunAsync(runtime, EnvIO.New(ct));

    return ToResult(result, todo => Results.Ok(MapToResponse(todo)));
});

// POST /todos - Create
app.MapPost("/todos", async (
    CreateTodoRequest request,
    IServiceProvider services,
    CancellationToken ct) =>
{
    var runtime = new AppRuntime(services);
    var result = await TodoService<Eff<AppRuntime>, AppRuntime>
        .Create(request.Title, request.Description)
        .RunAsync(runtime, EnvIO.New(ct));

    return ToResult(result, todo =>
        Results.Created($"/todos/{todo.Id}", MapToResponse(todo)));
});

// PUT /todos/{id} - Update
app.MapPut("/todos/{id:int}", async (
    int id,
    UpdateTodoRequest request,
    IServiceProvider services,
    CancellationToken ct) =>
{
    var runtime = new AppRuntime(services);
    var result = await TodoService<Eff<AppRuntime>, AppRuntime>
        .Update(id, request.Title, request.Description)
        .RunAsync(runtime, EnvIO.New(ct));

    return ToResult(result, todo => Results.Ok(MapToResponse(todo)));
});

// PATCH /todos/{id}/toggle - Toggle completion
app.MapPatch("/todos/{id:int}/toggle", async (
    int id,
    IServiceProvider services,
    CancellationToken ct) =>
{
    var runtime = new AppRuntime(services);
    var result = await TodoService<Eff<AppRuntime>, AppRuntime>
        .ToggleComplete(id)
        .RunAsync(runtime, EnvIO.New(ct));

    return ToResult(result, todo => Results.Ok(MapToResponse(todo)));
});

// DELETE /todos/{id} - Delete
app.MapDelete("/todos/{id:int}", async (
    int id,
    IServiceProvider services,
    CancellationToken ct) =>
{
    var runtime = new AppRuntime(services);
    var result = await TodoService<Eff<AppRuntime>, AppRuntime>
        .Delete(id)
        .RunAsync(runtime, EnvIO.New(ct));

    return ToResult(result, _ => Results.NoContent());
});

app.Run();
```

---

## 5.7 การรันและทดสอบ

### 5.7.1 รัน Application

```bash
dotnet restore
dotnet build
dotnet run
```

Server จะรันที่ `http://localhost:5000`

### 5.7.2 ทดสอบด้วย curl

**Create Todo:**

```bash
curl -X POST http://localhost:5000/todos \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Learn Functional Programming",
    "description": "Study language-ext v5"
  }'
```

**Response:**
```json
{
  "id": 1,
  "title": "Learn Functional Programming",
  "description": "Study language-ext v5",
  "isCompleted": false,
  "createdAt": "2024-01-15T10:30:00Z",
  "completedAt": null
}
```

**List Todos:**

```bash
curl http://localhost:5000/todos
```

**Get Single Todo:**

```bash
curl http://localhost:5000/todos/1
```

**Update Todo:**

```bash
curl -X PUT http://localhost:5000/todos/1 \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Master Functional Programming",
    "description": "Deep dive into language-ext v5"
  }'
```

**Toggle Completion:**

```bash
curl -X PATCH http://localhost:5000/todos/1/toggle
```

**Delete Todo:**

```bash
curl -X DELETE http://localhost:5000/todos/1
```

---

## 5.8 Error Handling

### 5.8.1 Not Found (404)

```csharp
// Get non-existent todo
curl http://localhost:5000/todos/999
```

**Response:**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "An error occurred",
  "status": 404,
  "detail": "Todo 999 not found"
}
```

### 5.8.2 Validation Error (500)

```csharp
// Create todo with invalid title
curl -X POST http://localhost:5000/todos \
  -H "Content-Type: application/json" \
  -d '{"title": "", "description": "Test"}'
```

**Response:**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.1",
  "title": "An error occurred",
  "status": 500,
  "detail": "Title is required and must be less than 200 characters"
}
```

**การจัดการ Error:**

```csharp
from todoOpt in Database<M, RT>.getTodoById(id)
from todo in todoOpt.To<M, Todo>(() =>
    Error.New(404, $"Todo {id} not found"))  // ← Error with code
```

```csharp
static IResult ToResult<A>(Fin<A> result, Func<A, IResult> onSuccess) =>
    result.Match(
        Succ: onSuccess,
        Fail: error =>
        {
            var code = error.Code == 404 ? 404 : 500;  // ← เช็ค error code
            return Results.Problem(
                detail: error.Message,
                statusCode: code);
        }
    );
```

---

## 5.9 Best Practices

### 5.9.1 Separate Layers

**❌ ห้ามทำ:**
```csharp
// Service ใช้ DbContext โดยตรง
public static K<M, Todo> Get<M, RT>(int id)
{
    var db = _services.GetRequiredService<AppDbContext>();  // ❌ ห้าม!
    var todo = db.Todos.Find(id);
    return M.Pure(todo);
}
```

**✅ ทำแบบนี้:**
```csharp
// Service ใช้ผ่าน capability
public static K<M, Todo> Get<M, RT>(int id)
    where RT : Has<M, DatabaseIO>
{
    return
        from todoOpt in Database<M, RT>.getTodoById(id)
        from todo in todoOpt.To<M, Todo>("Not found")
        select todo;
}
```

### 5.9.2 Pure Business Logic

```csharp
// ✅ Pure function - แยกจาก effects
public static Todo ToggleTodo(Todo todo, DateTime now) =>
    todo with
    {
        IsCompleted = !todo.IsCompleted,
        CompletedAt = !todo.IsCompleted ? now : null
    };

// ✅ Effectful function - ใช้ pure function
public static K<M, Todo> ToggleComplete<M, RT>(int id)
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, DatabaseIO>, Has<M, TimeIO>
{
    return
        from existing in Get(id)
        from now in Time<M, RT>.UtcNow
        from toggled in M.Pure(ToggleTodo(existing, now))  // ← Pure!
        from updated in Database<M, RT>.updateTodo(toggled)
        select updated;
}
```

### 5.9.3 Validation

```csharp
// ✅ Applicative validation - รวบรวม errors ทั้งหมด
(ValidateTitle(todo), ValidateDescription(todo))
    .Apply((_, _) => todo).As();

// ❌ Monadic - หยุดที่ error แรก
from _ in ValidateTitle(todo)
from __ in ValidateDescription(todo)
select todo;
```

### 5.9.4 Logging

```csharp
// ✅ Log ทุกขั้นตอนสำคัญ
public static K<M, Todo> Create<M, RT>(...)
{
    return
        from _ in Logger<M, RT>.logInfo($"Creating todo: {title}")
        from validated in Validate(...)
        from saved in Save(validated)
        from __ in Logger<M, RT>.logInfo($"Created todo ID: {saved.Id}")
        select saved;
}
```

---

## 5.10 สรุป

### สิ่งที่เรียนรู้ในบทนี้

1. **Project Structure** - Three-layer architecture
2. **Domain Layer** - Immutable entities with records
3. **Infrastructure Layer** - Traits, Live implementations, Capabilities
4. **Feature Layer** - TodoService, TodoValidation, TodoDtos
5. **API Layer** - ASP.NET Core Minimal APIs
6. **Error Handling** - Type-safe with Fin<A>
7. **Testing** - curl commands
8. **Best Practices** - Separate layers, pure functions, validation

### Key Patterns

**1. Trait → Live → Capability → Service**
```
DatabaseIO (trait)
  ↓
LiveDatabaseIO (implementation)
  ↓
Database<M, RT> (capability module)
  ↓
TodoService<M, RT> (business logic)
```

**2. Error Handling**
```
Option<Todo> → .To<M, Todo>(error) → K<M, Todo>
Validation<Error, Todo> → .To<M, Todo>() → K<M, Todo>
```

**3. LINQ Query Syntax**
```csharp
from x in GetX()
from y in GetY(x)
from z in GetZ(x, y)
select Result(x, y, z)
```

---

## 5.11 แบบฝึกหัด

### แบบฝึกหัดที่ 1: เพิ่ม Search Feature

เพิ่ม endpoint `GET /todos/search?q={query}` ที่ค้นหา todos จาก title หรือ description:

**Hints:**
1. เพิ่ม method ใน `DatabaseIO`
2. เพิ่ม method ใน `Database<M, RT>`
3. เพิ่ม method ใน `TodoService<M, RT>`
4. เพิ่ม endpoint ใน `Program.cs`

### แบบฝึกหัดที่ 2: เพิ่ม Pagination

เพิ่ม pagination สำหรับ `GET /todos?page=1&pageSize=10`:

**Hints:**
1. สร้าง `PagedResult<T>` DTO
2. แก้ไข `DatabaseIO.GetAllTodos` ให้รับ page parameters
3. Calculate total pages
4. Return PagedResult

### แบบฝึกหัดที่ 3: เพิ่ม Filtering

เพิ่ม filtering สำหรับ `GET /todos?completed=true`:

**Hints:**
1. เพิ่ม `FilterOptions` DTO
2. แก้ไข query logic ใน `DatabaseIO`
3. Support multiple filters

### แบบฝึกหัดที่ 4: เพิ่ม Metrics Capability

สร้าง `MetricsIO` capability ที่นับจำนวน operations:

**Hints:**
1. สร้าง `MetricsIO` trait
2. สร้าง `LiveMetricsIO` (in-memory counter)
3. เพิ่มใน `AppRuntime`
4. เรียกใช้ใน `TodoService`
5. สร้าง endpoint `GET /metrics` แสดง statistics

---

**บทถัดไป:** [บทที่ 6: Validation และ Error Handling](chapter-06.md)

ในบทถัดไป เราจะเจาะลึก Validation patterns และ Error handling แบบต่างๆ!
