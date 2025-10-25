# บทที่ 19: Best Practices - Decision Guides & Migration

> คำตอบสำหรับคำถามที่ถามบ่อย + การนำ FP ไปใช้จริง

---

## 🎯 เป้าหมายของบท

หลังจากอ่านบทนี้แล้ว คุณจะสามารถ:
- **ตัดสินใจได้ชัดเจน** ว่าเมื่อไหร่ใช้ Option, Either, Validation
- **เลือก Record หรือ Class** สำหรับ EF entities ได้อย่างมั่นใจ
- **รู้ว่าเมื่อไหร่ควร (และไม่ควร) ใช้ Specifications**
- **Migrate legacy code → FP** แบบค่อยเป็นค่อยไป ไม่ต้อง rewrite ทั้งหมด
- **Onboard ทีม** เข้าสู่ FP อย่างมีระบบ
- **นำ FP ขึ้น Production** ได้จริง

---

## 📚 โครงสร้างเนื้อหา

**ส่วนที่ 1: Decision Guides** (40 min) ⭐⭐⭐
- Option vs Either vs Validation - เมื่อไหร่ใช้อะไร?
- Record vs Class for EF - trade-offs ที่ต้องรู้
- When to use Specifications - complexity threshold
- Pure Functions vs Eff - where to draw the line?
- Seq vs List - lazy vs eager
- Map vs Bind - functor vs monad

**ส่วนที่ 2: Migration Strategies** (40 min) ⭐⭐⭐
- Why NOT to rewrite everything
- Strangler Pattern - ค่อยๆ แทนที่
- 3-Phase Migration Plan
- Legacy Integration Patterns
- Team Buy-in Strategies
- Measuring Migration Success

**ส่วนที่ 3: Team & Production** (30 min) ⭐⭐
- 4-Week Onboarding Plan
- Code Review Checklist
- Production Monitoring
- Common Pitfalls & Solutions

**ส่วนที่ 4: Reference Checklists** (10 min)
- Quick Decision Trees
- Production Readiness Checklist
- Code Quality Checklist

---

## 📖 รายละเอียดแต่ละส่วน

---

# ส่วนที่ 1: Decision Guides ⭐⭐⭐ (40 นาที)

> คำถามที่ถามบ่อยสุด - ตอบให้ชัดเจนที่นี่!

---

## 1.1 Option vs Either vs Validation

### ❓ คำถาม: "ควรใช้อะไร?"

**Decision Tree:**

```
คำถาม: Operation นี้อาจ fail ไหม?
│
├─ ไม่ fail แต่อาจไม่มีค่า (null/not found)
│  └─ ใช้ Option<T>
│     ตัวอย่าง: getTodoById, findUser, searchProduct
│
├─ อาจ fail ด้วยเหตุผลชัดเจน 1 อย่าง
│  └─ ใช้ Either<Error, T>
│     ตัวอย่าง: createTodo, updateUser, deleteProduct
│
└─ อาจ fail ด้วยหลายเหตุผล (ต้องการ error ทั้งหมด)
   └─ ใช้ Validation<Error, T>
      ตัวอย่าง: validateDto, validateForm, businessRules
```

---

### 📊 Comparison Table

| Scenario | Type | Reason | Example |
|----------|------|--------|---------|
| **ค้นหา record ที่อาจไม่มี** | `Option<T>` | ไม่เจอไม่ใช่ error | `getTodoById(999)` |
| **Create ที่อาจ duplicate** | `Either<Error, T>` | Duplicate = error | `createUser(existing)` |
| **Validate form หลาย fields** | `Validation<Error, T>` | แสดง error ทุก field | `validateTodo(dto)` |
| **Parse ที่อาจ invalid** | `Either<Error, T>` | Invalid = error | `parseDate("invalid")` |
| **ดึง config ที่ optional** | `Option<T>` | ไม่มี = ใช้ default | `getOptionalConfig()` |
| **Login ที่ผิดรหัส** | `Either<Error, User>` | Wrong password = error | `login(wrong)` |

---

### 💻 Code Examples

#### ✅ Option - Not Finding is Normal

```csharp
// ค้นหา todo - ไม่เจอไม่ใช่ error
public static Eff<RT, Option<Todo>> getTodoById<RT>(int id)
    where RT : struct, HasTodoRepo<RT>
{
    return TodoRepo.getTodoById<RT>(id);  // Returns Option<Todo>
}

// ใช้งาน
var result = await TodoService.getTodoById(999).Run(runtime, ct);

result.Match(
    Some: todo => Console.WriteLine($"Found: {todo.Title}"),
    None: () => Console.WriteLine("Not found - that's OK!")  // ไม่ใช่ error
);
```

#### ✅ Either - Failure is Error

```csharp
// สร้าง todo - fail = error ชัดเจน
public static Eff<RT, Either<Error, Todo>> createTodo<RT>(TodoCreateDto dto)
    where RT : struct, HasTodoRepo<RT>, HasUnitOfWork<RT>
{
    return from validated in validateDto(dto).ToEff()
           from todo in createEntity(validated)
           from _ in TodoRepo.addTodo<RT>(todo)
           from __ in UnitOfWork.saveChanges<RT>()
           select todo.ToEither<Error, Todo>();
}

// ใช้งาน
var result = await TodoService.createTodo(dto).Run(runtime, ct);

result.Match(
    Right: todo => Console.WriteLine($"Created: {todo.Id}"),
    Left: error => Console.WriteLine($"Error: {error.Message}")  // เป็น error!
);
```

#### ✅ Validation - Accumulate All Errors

```csharp
// Validate DTO - ต้องการ error ทุก field
public static Validation<Error, TodoCreateDto> validateDto(TodoCreateDto dto)
{
    return (
        ValidateTitle(dto.Title),
        ValidateUserId(dto.UserId),
        ValidateDueDate(dto.DueDate)
    ).Apply((title, userId, dueDate) => dto);
}

private static Validation<Error, string> ValidateTitle(string title) =>
    string.IsNullOrWhiteSpace(title)
        ? Fail<Error, string>(Error.New("TITLE_REQUIRED", "Title is required"))
        : title.Length > 200
            ? Fail<Error, string>(Error.New("TITLE_TOO_LONG", "Title max 200 chars"))
            : Success<Error, string>(title);

// ใช้งาน
var result = validateDto(dto);

result.Match(
    Succ: validDto => CreateTodo(validDto),
    Fail: errors => ShowErrors(errors)  // แสดง errors ทั้งหมด!
);
```

---

### 🎯 Golden Rules

**Rule 1:** ไม่เจอ ≠ Error → `Option<T>`
```csharp
✅ Option<User> getUserById(int id)
❌ Either<Error, User> getUserById(int id)  // ถ้าไม่เจอไม่ใช่ error
```

**Rule 2:** ต้องการ error message → `Either<Error, T>`
```csharp
✅ Either<Error, User> createUser(dto)
❌ Option<User> createUser(dto)  // ควรบอก error ว่าทำไม fail
```

**Rule 3:** หลาย validations → `Validation<Error, T>`
```csharp
✅ Validation<Error, Dto> validateDto(dto)
❌ Either<Error, Dto> validateDto(dto)  // จะได้แค่ error แรก
```

---

## 1.2 Record vs Class for EF Entities

### ❓ คำถาม: "ควรใช้ Record หรือ Class?"

**Decision Matrix:**

| Consideration | Record | Class | Winner |
|---------------|--------|-------|--------|
| **Immutability** | ✅ Default | ❌ Manual | Record |
| **EF Core Support** | ⚠️ v5+ only, limited | ✅ Full support | Class |
| **Change Tracking** | ❌ Difficult | ✅ Built-in | Class |
| **Value Equality** | ✅ Built-in | ❌ Manual | Record |
| **With Expressions** | ✅ Built-in | ❌ Manual | Record |
| **No-tracking Queries** | ✅ Perfect fit | ⚠️ Need AsNoTracking | Record |
| **Performance** | ✅ Slightly faster | ⚠️ Tracking overhead | Record |

---

### 💡 Recommended Approaches

#### Approach 1: Hybrid Class (Best for TodoApp) ⭐⭐⭐

```csharp
// ใช้ class สำหรับ EF + เพิ่ม immutable methods
public class Todo
{
    // Mutable properties สำหรับ EF
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public bool IsCompleted { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; }

    // ✅ เพิ่ม immutable-style methods
    public Todo WithTitle(string newTitle) =>
        new()
        {
            Id = Id,
            Title = newTitle,
            IsCompleted = IsCompleted,
            UserId = UserId,
            CreatedAt = CreatedAt
        };

    public Todo WithCompleted(bool completed) =>
        new()
        {
            Id = Id,
            Title = Title,
            IsCompleted = completed,
            UserId = UserId,
            CreatedAt = CreatedAt
        };
}

// ใช้งาน - ดูเหมือน immutable
var updatedTodo = existingTodo.WithTitle("New Title");

// แต่ EF ยัง track ได้ปกติ
context.Todos.Update(updatedTodo);
await context.SaveChangesAsync();
```

**ข้อดี:**
- ✅ EF Core support เต็มรูปแบบ
- ✅ Change tracking ทำงานปกติ
- ✅ ใช้ immutable style ได้
- ✅ Best of both worlds

**ข้อเสีย:**
- ⚠️ ต้องเขียน With methods เอง
- ⚠️ ยังมี public setters (อาจ mutate ได้)

---

#### Approach 2: Separate Domain/Persistence Models ⭐⭐

```csharp
// Domain Model - immutable record (ใช้ใน business logic)
public record TodoDomain(
    int Id,
    string Title,
    bool IsCompleted,
    int UserId,
    DateTime CreatedAt
);

// Persistence Model - mutable class (ใช้กับ EF เท่านั้น)
public class TodoEntity
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public bool IsCompleted { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; }

    // Mapping methods
    public TodoDomain ToDomain() =>
        new(Id, Title, IsCompleted, UserId, CreatedAt);

    public static TodoEntity FromDomain(TodoDomain todo) =>
        new()
        {
            Id = todo.Id,
            Title = todo.Title,
            IsCompleted = todo.IsCompleted,
            UserId = todo.UserId,
            CreatedAt = todo.CreatedAt
        };
}

// Repository mapping
public class LiveTodoRepository : ITodoRepository
{
    public async Task<Option<TodoDomain>> GetByIdAsync(int id, CancellationToken ct)
    {
        var entity = await _context.Todos.FindAsync(id, ct);
        return entity?.ToDomain();  // ✅ Return domain model
    }

    public void Update(TodoDomain todo)
    {
        var entity = TodoEntity.FromDomain(todo);  // ✅ Convert to entity
        _context.Todos.Update(entity);
    }
}
```

**ข้อดี:**
- ✅ Domain model 100% immutable
- ✅ Clear separation of concerns
- ✅ Business logic ไม่ยุ่งกับ EF
- ✅ Testable (mock domain models)

**ข้อเสีย:**
- ❌ Mapping overhead (performance)
- ❌ Duplicate code (2 models)
- ❌ ซับซ้อนกว่า

**เมื่อไหร่ควรใช้:**
- Large domain model
- Complex business logic
- Need 100% immutability
- Multiple persistence options

---

#### Approach 3: Init-Only Properties ⭐⭐

```csharp
// C# 9+ init-only properties
public class Todo
{
    public int Id { get; init; }
    public string Title { get; init; } = "";
    public bool IsCompleted { get; init; }
    public int UserId { get; init; }
    public DateTime CreatedAt { get; init; }

    // ✅ ใช้ with expression ได้!
    // var updated = todo with { Title = "New" };
}

// EF Core 5+ รองรับ
modelBuilder.Entity<Todo>(entity =>
{
    entity.Property(e => e.Id).ValueGeneratedOnAdd();
    // Init-only properties work!
});
```

**ข้อดี:**
- ✅ Immutable after construction
- ✅ With expressions ใช้ได้
- ✅ Works with EF Core 5+

**ข้อเสีย:**
- ⚠️ ต้อง EF Core 5+
- ⚠️ Change tracking ต้อง manual
- ⚠️ ไม่ได้ pure record

---

### 🎯 Recommendation for TodoApp

**Start with Approach 1 (Hybrid Class)**

```csharp
public class Todo
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public bool IsCompleted { get; set; }

    // Add immutable methods as needed
    public Todo WithTitle(string title) => new() { ... };
    public Todo ToggleCompleted() => new() { ... };
}
```

**Upgrade to Approach 2 if:**
- Domain becomes complex
- Need multiple persistence options
- Want 100% immutability guarantee

---

## 1.3 When to Use Specifications?

### ❓ คำถาม: "เมื่อไหร่ควรใช้ Specification Pattern?"

**Decision Flowchart:**

```
มี query methods กี่ตัว?
│
├─ 1-3 methods, simple
│  └─ ❌ ไม่ต้องใช้ Specification (over-engineering!)
│     → ใช้ direct repository methods
│
├─ 4-6 methods, เริ่มซับซ้อน
│  │
│  ├─ มี filter combinations?
│  │  ├─ ✅ ใช้ Specification
│  │  └─ ❌ Direct methods ยังพอ
│  │
│  └─ ต้องการ reuse query logic?
│     ├─ ✅ ใช้ Specification
│     └─ ❌ Direct methods OK
│
└─ 7+ methods, มี combinatorial explosion
   └─ ✅ ใช้ Specification (ช่วยได้มาก!)
```

---

### 📊 When to Use / Not Use

| Situation | Use Spec? | Why |
|-----------|-----------|-----|
| **GetById** | ❌ No | Too simple, over-engineering |
| **GetAll** | ❌ No | No filtering, direct method better |
| **GetCompleted** | ❌ No | Simple filter, direct method OK |
| **GetByUser** | ❌ No | Simple filter, direct method OK |
| **GetCompletedByUser** | ⚠️ Maybe | 2 filters, starting to be useful |
| **Dynamic filters (UI)** | ✅ Yes! | User picks filters, perfect use case |
| **GetCompletedByUserInLastDays** | ✅ Yes! | 3+ filters, composition needed |
| **Complex business rules** | ✅ Yes! | Reusable, testable logic |

---

### 💻 Examples

#### ❌ Over-Engineering (Don't Do This)

```csharp
// Too simple for Specification!
var spec = new TodoByIdSpec(id);
var todo = await repo.FindAsync(spec, ct);

// ❌ 10 lines of code:
// - TodoByIdSpec class (5 lines)
// - FindAsync implementation (3 lines)
// - Usage (2 lines)

// vs

// ✅ 1 line:
var todo = await repo.GetByIdAsync(id, ct);
```

#### ✅ Good Use Case

```csharp
// ✅ Dynamic filtering based on user input
public static Eff<RT, Seq<Todo>> searchTodos<RT>(TodoSearchDto search)
    where RT : struct, HasTodoRepo<RT>
{
    // Build spec based on user choices
    var specs = Seq<Specification<Todo>>();

    if (search.UserId.HasValue)
        specs = specs.Add(TodoSpecs.ByUser(search.UserId.Value));

    if (search.CompletedOnly)
        specs = specs.Add(TodoSpecs.IsCompleted());

    if (search.LastDays.HasValue)
        specs = specs.Add(TodoSpecs.CreatedAfter(
            DateTime.Now.AddDays(-search.LastDays.Value)));

    if (search.TextSearch != null)
        specs = specs.Add(TodoSpecs.TitleContains(search.TextSearch));

    // Combine all specs
    var finalSpec = specs.Reduce((a, b) => a.And(b));

    return TodoRepo.findTodos<RT>(finalSpec);
}
```

---

### 🎯 Golden Rule

**"3 Filter Rule"**

- 1-2 filters → Direct repository method
- 3+ filters OR dynamic combinations → Specification Pattern

**Examples:**

```csharp
// 1 filter → Direct method ✅
public Task<List<Todo>> GetCompletedTodosAsync(ct)

// 2 filters → Still direct method ✅
public Task<List<Todo>> GetCompletedByUserAsync(int userId, ct)

// 3+ filters → Specification time! ✅
TodoSpecs.ByUser(id).And(IsCompleted()).And(CreatedAfter(date))

// Dynamic filters → Specification! ✅
if (filter1) spec = spec.And(Spec1);
if (filter2) spec = spec.And(Spec2);
if (filter3) spec = spec.And(Spec3);
```

---

## 1.4 Pure Functions vs Eff Monad

### ❓ คำถาม: "ควรเป็น pure function หรือ Eff?"

**Decision Rule:**

```
Function นี้มี side effects ไหม?
│
├─ ไม่มี (deterministic, no IO)
│  └─ ✅ Pure Function
│     Example: validation, calculation, mapping
│
└─ มี (IO, database, API, random, DateTime.Now)
   └─ ✅ Eff Monad
      Example: CRUD, logging, external API calls
```

---

### 📊 Pure vs Eff

| Type | Pure Function | Eff Monad |
|------|---------------|-----------|
| **Input → Output** | ✅ Same input = same output | ⚠️ May differ |
| **Side Effects** | ❌ None | ✅ Wrapped |
| **Testable** | ✅ Super easy | ⚠️ Need runtime |
| **Composable** | ✅ Easy | ✅ Easy |
| **Performance** | ✅ Fast (can cache) | ⚠️ IO overhead |

---

### 💻 Examples

#### ✅ Pure Functions

```csharp
// ✅ Pure - same input = same output, no IO
public static Either<Error, Todo> validateTodo(TodoCreateDto dto)
{
    return string.IsNullOrEmpty(dto.Title)
        ? Left<Error, Todo>(Error.New("INVALID_TITLE"))
        : Right<Error, Todo>(CreateTodoFromDto(dto));
}

// ✅ Pure - pure transformation
private static Todo CreateTodoFromDto(TodoCreateDto dto) =>
    new Todo
    {
        Title = dto.Title,
        UserId = dto.UserId,
        IsCompleted = false,
        CreatedAt = DateTime.UtcNow  // ❌ WAIT - Not pure! See below
    };

// ✅ Truly pure - ให้ caller ส่ง timestamp เข้ามา
private static Todo CreateTodoFromDto(TodoCreateDto dto, DateTime createdAt) =>
    new Todo
    {
        Title = dto.Title,
        UserId = dto.UserId,
        IsCompleted = false,
        CreatedAt = createdAt  // ✅ Pure!
    };
```

#### ✅ Eff Monad - Has Side Effects

```csharp
// ✅ Eff - มี IO (database, logging)
public static Eff<RT, Either<Error, Todo>> createTodo<RT>(TodoCreateDto dto)
    where RT : struct,
        HasTodoRepo<RT>,
        HasUnitOfWork<RT>,
        HasLogger<RT>
{
    return from _ in LoggerIO.logInfo<RT>("Creating todo")  // IO!
           from validated in validateTodo(dto).ToEff()      // Pure
           from todo in pure(() => CreateTodoFromDto(dto, DateTime.UtcNow))  // Pure + impure datetime
           from __ in TodoRepo.addTodo<RT>(todo)           // IO!
           from ___ in UnitOfWork.saveChanges<RT>()        // IO!
           select todo.ToEither<Error, Todo>();
}
```

---

### 🎯 Best Practice: Pure Sandwich

```
┌─────────────────────────┐
│  Eff (IO)              │  Read from DB
├─────────────────────────┤
│  Pure Functions        │  Business logic
│  - Validate            │  (testable!)
│  - Calculate           │
│  - Transform           │
├─────────────────────────┤
│  Eff (IO)              │  Write to DB
└─────────────────────────┘
```

**Example:**

```csharp
public static Eff<RT, Either<Error, Todo>> updateTodoStatus<RT>(
    int id,
    bool isCompleted)
{
    return from existingOpt in TodoRepo.getTodoById<RT>(id)  // IO
           from existing in existingOpt.ToEff(Error.New("NOT_FOUND"))

           // ✅ Pure business logic
           from validated in pure(() => ValidateStatusChange(existing, isCompleted))
           from updated in pure(() => existing with { IsCompleted = isCompleted })

           from _ in TodoRepo.updateTodo<RT>(updated)  // IO
           from __ in UnitOfWork.saveChanges<RT>()    // IO
           select updated.ToEither<Error, Todo>();
}

// ✅ Pure function - easy to test!
private static Either<Error, Unit> ValidateStatusChange(Todo todo, bool newStatus)
{
    if (todo.IsCompleted == newStatus)
        return Left<Error, Unit>(Error.New("NO_CHANGE", "Status already set"));

    if (todo.UserId == 0)
        return Left<Error, Unit>(Error.New("INVALID_USER", "Todo has no user"));

    return Right<Error, Unit>(unit);
}
```

---

## 1.5 Seq vs List - Lazy vs Eager

### ❓ คำถาม: "ควรใช้ Seq หรือ List?"

**Quick Answer:**

- **Default: Use `Seq<T>`** (lazy, language-ext style)
- **Only use `List<T>`** when you need materialization

---

### 📊 Comparison

| Aspect | Seq<T> | List<T> |
|--------|--------|---------|
| **Evaluation** | ✅ Lazy | ❌ Eager |
| **Performance** | ✅ Better for chains | ⚠️ Multiple enumerations |
| **Memory** | ✅ Lower | ⚠️ Higher |
| **language-ext** | ✅ Native | ⚠️ Convert needed |
| **LINQ** | ✅ All methods | ✅ All methods |

---

### 💻 Examples

```csharp
// ❌ Eager - loads everything, multiple passes
public static Eff<RT, List<string>> getTodoTitles<RT>()
{
    return TodoRepo.getAllTodos<RT>()
        .Map(todos => todos
            .Where(t => t.IsCompleted)  // Pass 1
            .ToList()                    // Materialize
            .Select(t => t.Title)        // Pass 2
            .ToList()                    // Materialize again
            .Take(10)                    // Pass 3
            .ToList());                  // Materialize again!
}

// ✅ Lazy - single pass, only evaluates what's needed
public static Eff<RT, Seq<string>> getTodoTitles<RT>()
{
    return TodoRepo.getAllTodos<RT>()
        .Map(todos => todos
            .Filter(t => t.IsCompleted)  // Not evaluated yet
            .Map(t => t.Title)            // Not evaluated yet
            .Take(10)                     // Not evaluated yet
            .ToSeq());                    // Evaluated once when needed!
}
```

---

### 🎯 When to Use List

```csharp
// ✅ Use List when you need:

// 1. Count without enumeration
var count = list.Count;  // O(1) vs Seq.Count O(n)

// 2. Index access
var first = list[0];  // O(1) vs Seq not supported

// 3. Multiple enumerations (already materialized)
foreach (var item in list) { }
foreach (var item in list) { }  // No re-query

// 4. EF Core requirement
context.Todos.ToList();  // Sometimes needed for EF

// Otherwise, use Seq!
```

---

## 1.6 Map vs Bind - Functor vs Monad

### ❓ คำถาม: "ใช้ Map หรือ Bind?"

**Simple Rule:**

- **Map:** Transform value (T → U)
- **Bind:** Chain effects (T → Eff<U>)

---

### 💻 Examples

```csharp
// ✅ Map - simple transformation
Eff<RT, int> todoCount =
    TodoRepo.getAllTodos<RT>()
        .Map(todos => todos.Count());  // Seq<Todo> → int

// ✅ Bind - chain effects
Eff<RT, Option<User>> todoOwner =
    TodoRepo.getTodoById<RT>(id)
        .Bind(todoOpt => todoOpt.Match(
            Some: t => UserRepo.getUserById<RT>(t.UserId),  // Eff<RT, Option<User>>
            None: () => SuccessEff<RT, Option<User>>(None)
        ));

// ❌ Wrong - can't use Map for effect
Eff<RT, Eff<RT, Option<User>>> wrong =  // ❌ Nested Eff!
    TodoRepo.getTodoById<RT>(id)
        .Map(todoOpt => UserRepo.getUserById<RT>(todoOpt.UserId));  // Returns Eff

// ✅ Correct - use Bind for effect
Eff<RT, Option<User>> correct =
    TodoRepo.getTodoById<RT>(id)
        .Bind(todoOpt => UserRepo.getUserById<RT>(todoOpt.UserId));
```

---

# ส่วนที่ 2: Migration Strategies ⭐⭐⭐ (40 นาที)

> ไม่ต้อง rewrite ทั้งหมด! - นำ FP ไปใช้แบบค่อยเป็นค่อยไป

---

## 2.1 Why NOT to Rewrite Everything

### 🚫 ปัญหาของ Big Bang Rewrite

```
Big Bang Rewrite = 💣
├─ High risk - ทำผิดทั้งระบบ
├─ No deliverables - ไม่มี value ระหว่างทาง
├─ Team friction - คนเก่า vs คนใหม่
├─ Business停 - feature development หยุด
└─ Likely to fail - สถิติแย่มาก

Stats:
- 70% of big rewrites fail
- Average time: 2-3x estimate
- Lost features: 30%+
- Team burnout: 90%
```

---

### ✅ Incremental Migration Instead

```
Incremental = 🚀
├─ Low risk - revert ได้ง่าย
├─ Continuous value - ส่ง feature ได้ตลอด
├─ Learn as you go - adjust strategy
├─ Team adoption - เรียนรู้ทีละน้อย
└─ Success rate: 80%+

Benefits:
- New features in FP (clean slate)
- Old code เปลี่ยนทีละชิ้น
- Production runs throughout
- ROI ทันที
```

---

## 2.2 The Strangler Pattern

### 📖 Concept

> "Gradually strangle the old system by growing the new one around it"

**Visualization:**

```
┌─────────────────────────────────────────┐
│  Old System (Month 0)                   │
│  ████████████████████████████████████  │
│  Imperative Code: 100%                  │
└─────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│  Old System (Month 3)                   │
│  ████████████████████████░░░░░░░░░░░░  │
│  Imperative: 75%    FP: 25%            │
└─────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│  Old System (Month 6)                   │
│  ████████████░░░░░░░░░░░░░░░░░░░░░░░░  │
│  Imperative: 50%    FP: 50%            │
└─────────────────────────────────────────┘

┌─────────────────────────────────────────┐
│  New System (Month 12)                  │
│  ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  │
│  FP: 100% 🎉                            │
└─────────────────────────────────────────┘
```

---

### 🎯 How It Works

**Step 1: Intercept**
```csharp
// ✅ Route new requests to new FP code
public async Task<IActionResult> CreateTodo(TodoDto dto)
{
    if (FeatureFlags.UseFPImplementation)
        return await CreateTodoFP(dto);  // ✅ New FP code
    else
        return await CreateTodoOld(dto);  // Old code
}
```

**Step 2: Grow**
```csharp
// ✅ Gradually increase FP coverage
- Week 1: CreateTodo → FP (5% traffic)
- Week 2: UpdateTodo → FP (10% traffic)
- Week 3: DeleteTodo → FP (25% traffic)
- Week 4: Increase to 50% traffic
- Week 8: 100% traffic
- Week 10: Remove old code
```

**Step 3: Remove**
```csharp
// ✅ Delete old code when fully migrated
public async Task<IActionResult> CreateTodo(TodoDto dto)
{
    return await CreateTodoFP(dto);  // Only FP remains!
}
```

---

## 2.3 Three-Phase Migration Plan

### Phase 1: New Features Only (Month 1-2)

**Rule: แตะของเก่าน้อยที่สุด**

```csharp
// ✅ Keep old imperative code
public class OldTodoService
{
    public async Task<Todo> CreateTodo(TodoDto dto)
    {
        // DON'T TOUCH!
        var todo = new Todo { Title = dto.Title };
        _context.Add(todo);
        await _context.SaveChangesAsync();
        return todo;
    }
}

// ✅ Write new features in FP
public static class NewCommentService  // New feature!
{
    public static Eff<RT, Either<Error, Comment>> createComment<RT>(
        CommentCreateDto dto)
        where RT : struct, HasCommentRepo<RT>, HasUnitOfWork<RT>
    {
        // Pure FP from day 1!
        return from validated in validateDto(dto).ToEff()
               from comment in createEntity(validated)
               from _ in CommentRepo.addComment<RT>(comment)
               from __ in UnitOfWork.saveChanges<RT>()
               select comment.ToEither<Error, Comment>();
    }
}
```

**Benefits:**
- ✅ Zero risk to existing features
- ✅ Team learns FP on clean slate
- ✅ Can compare old vs new approach

---

### Phase 2: Extract and Wrap (Month 3-4)

**Rule: Wrap old code in Eff, don't rewrite yet**

```csharp
// ✅ Create wrapper for old service
public static class LegacyTodoService
{
    private static readonly OldTodoService _oldService = new();

    // Wrap in Eff - allows composition with new code
    public static Eff<RT, Todo> createTodoLegacy<RT>(TodoDto dto)
        where RT : struct, HasCancellationToken<RT>
    {
        return from ct in CancellationTokenIO.token<RT>()
               from todo in Eff(async () => await _oldService.CreateTodo(dto))
               select todo;
    }

    // Now can use with other FP code!
    public static Eff<RT, (Todo, Comment)> createTodoWithComment<RT>(
        TodoDto todoDto,
        CommentCreateDto commentDto)
        where RT : struct,
            HasCommentRepo<RT>,
            HasUnitOfWork<RT>,
            HasCancellationToken<RT>
    {
        return from todo in createTodoLegacy<RT>(todoDto)  // ✅ Wrapped!
               from comment in NewCommentService.createComment<RT>(
                   commentDto with { TodoId = todo.Id })
               select (todo, comment);
    }
}
```

**Benefits:**
- ✅ Old and new code can work together
- ✅ Incremental composition
- ✅ No big rewrite needed

---

### Phase 3: Gradual Refactoring (Month 5+)

**Rule: One function at a time, high-value first**

**Priority Matrix:**

| Priority | Criteria | Examples |
|----------|----------|----------|
| **High** | Frequently changed + Complex logic | CRUD, Validation |
| **Medium** | Frequently changed, Simple logic | Queries, Getters |
| **Low** | Rarely changed | Config, Utilities |
| **Never** | Stable + Working + Low value | Old reports |

**Example Refactoring:**

```csharp
// ❌ Old imperative code
public async Task<Todo> UpdateTodo(int id, string title)
{
    var todo = await _context.Todos.FindAsync(id);
    if (todo == null)
        throw new NotFoundException();

    todo.Title = title;  // Mutation!
    await _context.SaveChangesAsync();

    _logger.LogInformation($"Updated todo {id}");
    return todo;
}

// ✅ Refactored to FP (one function)
public static Eff<RT, Either<Error, Todo>> updateTodo<RT>(
    int id,
    string title)
    where RT : struct,
        HasTodoRepo<RT>,
        HasUnitOfWork<RT>,
        HasLogger<RT>,
        HasCancellationToken<RT>
{
    return from todoOpt in TodoRepo.getTodoById<RT>(id)
           from todo in todoOpt.ToEff(Error.New("NOT_FOUND"))
           from updated in pure(() => todo with { Title = title })  // Immutable!
           from _ in TodoRepo.updateTodo<RT>(updated)
           from __ in UnitOfWork.saveChanges<RT>()
           from ___ in LoggerIO.logInfo<RT>($"Updated todo {id}")
           select updated.ToEither<Error, Todo>();
}
```

**Refactoring Schedule:**

```
Week 1: createTodo (high priority)
Week 2: updateTodo (high priority)
Week 3: deleteTodo (high priority)
Week 4: getAllTodos (medium priority)
Week 5-8: Other CRUD operations
Week 9-12: Utilities and helpers
```

---

## 2.4 Legacy Integration Patterns

### Pattern 1: Adapter Wrapper

```csharp
// ✅ Wrap legacy service to implement new interface
public class LegacyTodoRepositoryAdapter : ITodoRepository
{
    private readonly OldTodoService _legacyService;

    public async Task<Option<Todo>> GetTodoByIdAsync(int id, CancellationToken ct)
    {
        try
        {
            var todo = await _legacyService.GetById(id);  // Old code
            return Option<Todo>.Some(todo);
        }
        catch (NotFoundException)
        {
            return Option<Todo>.None;  // ✅ Convert exception to Option
        }
    }
}

// Use adapter in capability
public static class TodoRepo
{
    public static Eff<RT, Option<Todo>> getTodoById<RT>(int id)
        where RT : struct, HasTodoRepo<RT>, HasCancellationToken<RT>
    {
        return from repo in default(RT).TodoRepoEff  // May be adapter!
               from ct in CancellationTokenIO.token<RT>()
               from todo in Eff(() => repo.GetTodoByIdAsync(id, ct))
               select todo;
    }
}
```

---

### Pattern 2: Facade for Multiple Legacy Services

```csharp
// ✅ Unified FP facade over multiple old services
public static class UnifiedTodoService
{
    public static Eff<RT, Either<Error, TodoAggregate>> getTodoAggregate<RT>(
        int id)
        where RT : struct, HasCancellationToken<RT>
    {
        return from todo in LegacyWrapper.getTodo<RT>(id)  // Old service 1
               from comments in LegacyWrapper.getComments<RT>(id)  // Old service 2
               from user in LegacyWrapper.getUser<RT>(todo.UserId)  // Old service 3
               select new TodoAggregate(todo, comments, user)
                   .ToEither<Error, TodoAggregate>();
    }

    // ✅ New FP interface, delegates to old implementations
    // Can gradually replace implementations one by one
}
```

---

## 2.5 Team Buy-in Strategies

### 🎯 Getting Team On Board

**Common Objections:**

| Objection | Response | Proof |
|-----------|----------|-------|
| "Too complicated" | Show simple examples first | TodoService.createTodo |
| "Not C# style" | language-ext is mature C# library | LINQ is FP! |
| "Performance?" | Show benchmarks | Often faster |
| "Learning curve steep" | Provide training plan | 4 weeks to productive |
| "We don't have time" | Start with new features only | No rewrite needed |

---

**Winning Strategy:**

**Week 1: Show, Don't Tell**
```csharp
// ✅ Demo real code comparison

// Old way - 30 lines, 5 bugs
public async Task<Todo> CreateTodo(TodoDto dto) { ... }

// New way - 10 lines, 0 bugs (type safe!)
public static Eff<RT, Either<Error, Todo>> createTodo<RT>(...) { ... }

// Team sees: Less code, more safety
```

**Week 2: Pilot Project**
```
✅ Pick small feature
✅ Volunteer team (interested devs)
✅ Pair programming sessions
✅ Deliver to production
✅ Measure: Bugs, time, feedback

Result: Proof that it works!
```

**Week 3: Share Success**
```
✅ Demo to whole team
✅ Show metrics (fewer bugs!)
✅ Gather feedback
✅ Address concerns
✅ Plan rollout
```

**Week 4: Rollout**
```
✅ Training sessions
✅ Documentation
✅ Code review guidelines
✅ All new features in FP
✅ Gradual migration plan
```

---

## 2.6 Measuring Migration Success

### 📊 Metrics to Track

**Code Quality:**
```
- Bugs in FP code: ___
- Bugs in old code: ___
- Code review time: FP vs Old
- Test coverage: FP vs Old
- Mutation count: ↓
```

**Team Velocity:**
```
- Time to implement feature: FP vs Old
- Time to fix bug: FP vs Old
- Onboarding time: New devs
- Refactoring safety: FP vs Old
```

**Production:**
```
- Production bugs: FP vs Old
- Performance: FP vs Old
- Error handling: Exception % ↓
- Type safety: Compile errors caught
```

---

### 🎯 Success Criteria

**After 3 months:**
- [ ] 25%+ new code in FP
- [ ] 50%+ team trained
- [ ] 0 production bugs in FP code
- [ ] Team velocity same or better
- [ ] Positive team feedback

**After 6 months:**
- [ ] 50%+ code in FP
- [ ] 100% team trained
- [ ] All new features in FP
- [ ] Legacy code migration started
- [ ] Proven production stability

**After 12 months:**
- [ ] 80%+ code in FP
- [ ] Legacy code mostly migrated
- [ ] Team prefers FP
- [ ] Measurable quality improvement
- [ ] Case study ready!

---

# ส่วนที่ 3: Team & Production ⭐⭐ (30 นาที)

> ทำให้ FP work ในทีมและ production

---

## 3.1 Four-Week Onboarding Plan

### Week 1: Fundamentals

**Goal: เข้าใจ FP basics**

**Monday:**
- อ่าน Chapter 1-2 (Why FP, Basics)
- ดู TodoApp codebase overview
- Setup dev environment

**Tuesday-Wednesday:**
- Pure functions practice
- Immutability exercises
- Pattern matching

**Thursday:**
- Option<T> และ Either<T>
- Match expressions
- Hands-on exercises

**Friday:**
- Quiz & review
- Pair programming with mentor
- Setup for next week

**Deliverable:** Complete 10 pure function exercises

---

### Week 2: Monads & Effects

**Goal: เข้าใจ Eff และ monadic composition**

**Monday:**
- Eff<RT, T> monad
- Has pattern intro
- Read Chapter 4

**Tuesday-Wednesday:**
- LINQ query syntax
- Monadic binding practice
- TodoService examples

**Thursday:**
- Error handling (Either, Validation)
- Write first service function
- Code review with mentor

**Friday:**
- Validation monad exercises
- Team code review session
- Plan first real task

**Deliverable:** Write 3 service functions (with tests)

---

### Week 3: First Feature

**Goal: Implement จริง**

**Monday:**
- Pick small feature (with mentor)
- Design approach
- Write specifications

**Tuesday-Thursday:**
- Implementation
- Daily stand-up with mentor
- Code reviews

**Friday:**
- Complete feature
- Tests pass
- Deploy to staging
- Retrospective

**Deliverable:** Ship first FP feature to staging! 🚀

---

### Week 4: Independence

**Goal: Work independently**

**Monday:**
- Pick medium feature (less guidance)
- Plan and design alone
- Review with mentor

**Tuesday-Thursday:**
- Independent implementation
- Ask questions as needed
- Code review process

**Friday:**
- Feature complete
- Production deployment
- Graduation! 🎉

**Deliverable:** Ship feature to production independently

---

### 📚 Learning Resources

**Week 1-2:**
- This book (Chapters 1-4)
- language-ext docs
- TodoApp code reading

**Week 3-4:**
- This book (Chapters 5-17)
- Advanced patterns
- Real PRs in codebase

**Ongoing:**
- Team wiki
- Pair programming
- Code review feedback

---

## 3.2 Code Review Checklist

### ✅ FP Code Review Guide

**Print this out! Keep at desk!**

---

#### 1. Purity ✨

```
- [ ] Functions are pure (same input = same output)?
- [ ] No hidden mutations?
- [ ] No global state access?
- [ ] DateTime.Now wrapped in Eff?
- [ ] Random numbers wrapped in Eff?
```

**Example:**
```csharp
// ❌ Not pure - hidden mutation
public static Todo UpdateTitle(Todo todo, string title)
{
    todo.Title = title;  // ❌ Mutation!
    return todo;
}

// ✅ Pure
public static Todo UpdateTitle(Todo todo, string title) =>
    todo with { Title = title };  // ✅ New instance
```

---

#### 2. Type Safety 🔒

```
- [ ] Return types explicit (no var for public APIs)?
- [ ] Option<T> for nullable?
- [ ] Either<Error, T> for fallible operations?
- [ ] Validation<Error, T> for multiple errors?
- [ ] No nulls?
- [ ] No exceptions (except truly exceptional)?
```

**Example:**
```csharp
// ❌ Bad types
public static async Task<Todo> GetTodo(int id)  // May return null or throw
{
    var todo = await _repo.Find(id);
    if (todo == null) throw new NotFoundException();
    return todo;
}

// ✅ Good types
public static Eff<RT, Option<Todo>> getTodo<RT>(int id)
    where RT : struct, HasTodoRepo<RT>
{
    return TodoRepo.getTodoById<RT>(id);  // Returns Option
}
```

---

#### 3. Naming 📝

```
- [ ] Functions: camelCase?
- [ ] Classes/Interfaces: PascalCase?
- [ ] Descriptive names?
- [ ] Type parameters clear (<RT>, <T>)?
- [ ] Error codes: UPPER_SNAKE_CASE?
```

---

#### 4. Error Handling 🚨

```
- [ ] All errors handled?
- [ ] Error messages clear?
- [ ] Error codes consistent?
- [ ] No swallowed exceptions?
- [ ] Match expressions cover all cases?
```

**Example:**
```csharp
// ✅ Good error handling
result.Match(
    Right: todo => ProcessTodo(todo),
    Left: error => error.Code switch
    {
        "NOT_FOUND" => NotFound(error.Message),
        "VALIDATION_ERROR" => BadRequest(error.Message),
        _ => InternalServerError(error.Message)
    }
);
```

---

#### 5. Testing 🧪

```
- [ ] Unit tests for pure functions?
- [ ] Integration tests for Eff pipelines?
- [ ] Property-based tests for core logic?
- [ ] Edge cases covered?
- [ ] Test names descriptive?
```

---

#### 6. Performance ⚡

```
- [ ] Seq<T> for large collections (lazy)?
- [ ] No unnecessary allocations?
- [ ] Caching where appropriate?
- [ ] No N+1 queries?
```

---

## 3.3 Production Monitoring

### 📊 Logging Strategy

```csharp
// ✅ Structured logging with context
public static Eff<RT, Either<Error, Todo>> createTodo<RT>(
    TodoCreateDto dto)
    where RT : struct,
        HasTodoRepo<RT>,
        HasLogger<RT>,
        HasUnitOfWork<RT>
{
    return from _ in LoggerIO.logInfo<RT>(
               "Creating todo",
               new  // ✅ Structured data
               {
                   UserId = dto.UserId,
                   TitleLength = dto.Title.Length,
                   Timestamp = DateTime.UtcNow
               })
           from validated in validateDto(dto).ToEff()
           from todo in createEntity(validated)
           from __ in TodoRepo.addTodo<RT>(todo)
           from ___ in UnitOfWork.saveChanges<RT>()
           from ____ in LoggerIO.logInfo<RT>(
               "Todo created successfully",
               new { TodoId = todo.Id })
           select todo.ToEither<Error, Todo>();
}
```

---

### 🔍 Error Tracking

```csharp
// ✅ Integrate with Sentry/Application Insights
public static class ErrorTracking
{
    public static Eff<RT, Unit> trackError<RT>(Error error)
        where RT : struct, HasErrorTracker<RT>
    {
        return from tracker in default(RT).ErrorTrackerEff
               from _ in Eff(() =>
               {
                   tracker.CaptureException(new
                   {
                       ErrorCode = error.Code,
                       Message = error.Message,
                       StackTrace = error.Inner?.StackTrace,
                       Timestamp = DateTime.UtcNow
                   });
                   return unit;
               })
               select unit;
    }
}

// Use in service
var result = await TodoService.createTodo(dto)
    .MapFail(error =>
        ErrorTracking.trackError(error)  // ✅ Track all errors
            .Map(_ => error))
    .Run(runtime, ct);
```

---

### 💚 Health Checks

```csharp
// ✅ Health check endpoint
public static Eff<RT, HealthStatus> checkHealth<RT>()
    where RT : struct, HasTodoRepo<RT>, HasDatabase<RT>
{
    return from dbPing in Database.ping<RT>()
               .MapFail(_ => false)
               .IfFail(false)
           from repoPing in TodoRepo.healthCheck<RT>()
               .MapFail(_ => false)
               .IfFail(false)
           select new HealthStatus
           {
               Database = dbPing ? "Healthy" : "Unhealthy",
               Repository = repoPing ? "Healthy" : "Unhealthy",
               Timestamp = DateTime.UtcNow
           };
}
```

---

# ส่วนที่ 4: Quick Reference ⭐ (10 นาที)

> กลับมาดูได้เสมอ - Checklists & Decision Trees

---

## 4.1 Decision Tree: Which Type to Return?

```
START: What does this function return?
│
├─ ค่าที่อาจไม่มี (nullable)?
│  │
│  ├─ Not having it is normal?
│  │  └─ ✅ Option<T>
│  │     Example: getUserById(999) → None (OK)
│  │
│  └─ Not having it is an error?
│     └─ ✅ Either<Error, T>
│        Example: getRequiredConfig() → Error
│
├─ Operation ที่อาจ fail?
│  │
│  ├─ Want all errors?
│  │  └─ ✅ Validation<Error, T>
│  │     Example: validateForm(dto) → All field errors
│  │
│  └─ Single error is enough?
│     └─ ✅ Either<Error, T>
│        Example: createTodo(dto) → First error
│
└─ Always succeeds?
   └─ ✅ T (plain value) or Eff<RT, T>
      Example: getCurrentUser() → User (always logged in)
```

---

## 4.2 Production Readiness Checklist

```
## Code Quality
- [ ] All functions pure or wrapped in Eff
- [ ] No mutations
- [ ] Proper error types (Option/Either/Validation)
- [ ] Naming conventions followed
- [ ] XML documentation on public APIs

## Testing
- [ ] Unit tests for pure functions (>80% coverage)
- [ ] Integration tests for Eff pipelines
- [ ] Property-based tests for core logic
- [ ] All tests passing

## Performance
- [ ] Seq<T> for large collections
- [ ] No unnecessary allocations
- [ ] Benchmarks for critical paths
- [ ] No N+1 database queries

## Team
- [ ] Team trained on FP basics
- [ ] Code review process in place
- [ ] Documentation up to date
- [ ] Onboarding guide available

## Production
- [ ] Structured logging implemented
- [ ] Error tracking integrated
- [ ] Health check endpoints
- [ ] Monitoring dashboards
- [ ] Deployment process tested
```

---

## 4.3 Common Patterns Quick Reference

### Pattern: CRUD with Error Handling

```csharp
// Template:
public static Eff<RT, Either<Error, T>> createEntity<RT>(Dto dto)
    where RT : struct, HasRepo<RT>, HasUnitOfWork<RT>
{
    return from validated in validate(dto).ToEff()
           from entity in createEntity(validated)
           from _ in Repo.add<RT>(entity)
           from __ in UnitOfWork.saveChanges<RT>()
           select entity.ToEither<Error, T>();
}
```

### Pattern: Query with Option

```csharp
// Template:
public static Eff<RT, Option<T>> getById<RT>(int id)
    where RT : struct, HasRepo<RT>
{
    return Repo.getById<RT>(id);
}
```

### Pattern: Validation

```csharp
// Template:
public static Validation<Error, Dto> validate(Dto dto)
{
    return (
        ValidateField1(dto.Field1),
        ValidateField2(dto.Field2),
        ValidateField3(dto.Field3)
    ).Apply((f1, f2, f3) => dto);
}
```

---

## 📚 Resources

**Documentation:**
- This book!
- language-ext GitHub: https://github.com/louthy/language-ext
- TodoApp wiki

**Tools:**
- VS Code + C# Dev Kit
- ReSharper (FP patterns)
- BenchmarkDotNet (performance)

**Community:**
- language-ext Discussions
- F# Slack (FP concepts)
- Team Slack channel

---

## 🎯 Key Takeaways

หลังจากอ่านบทนี้ คุณจะได้:

1. **ตัดสินใจได้ชัดเจน** ⭐⭐⭐
   - Option vs Either vs Validation
   - Record vs Class
   - When to use Specifications

2. **Migrate ได้จริง** ⭐⭐⭐
   - Strangler pattern
   - 3-phase plan
   - No big rewrite!

3. **Team ready** ⭐⭐
   - 4-week onboarding
   - Code review checklist
   - Production monitoring

4. **Quick reference** ⭐
   - Decision trees
   - Checklists
   - Common patterns

---

## 🧪 แบบฝึกหัด

### ระดับง่าย
1. ใช้ Decision Tree ตัดสินใจ type สำหรับ 5 functions
2. Review TodoApp code ด้วย checklist
3. สร้าง migration plan สำหรับ feature หนึ่ง

### ระดับกลาง
1. Implement Strangler pattern สำหรับ legacy function
2. สร้าง 4-week plan สำหรับ team ของคุณ
3. Setup production monitoring สำหรับ FP code

### ระดับยาก
1. Migrate legacy feature → FP แบบ incremental
2. Measure และ report migration metrics
3. Present FP adoption plan to stakeholders

---

## 📊 สถิติ

- **ระดับความยาก:** ⭐⭐⭐ (กลาง - practical)
- **เวลาอ่าน:** ~100 นาที
- **เวลาลงมือทำ:** ~120 นาที
- **Decision guides:** 6 guides
- **Checklists:** 5 checklists
- **จำนวนหน้า:** ~18 หน้า

---

**Status:** 📋 Outline Ready → ⏳ Ready to Write

**Focus:** Decision-heavy, migration-focused, practical reference chapter
