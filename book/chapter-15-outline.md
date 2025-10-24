# บทที่ 15: Specification Pattern

> แก้ปัญหา Repository Explosion ด้วย Composable Query Logic

---

## 🎯 เป้าหมายของบท

หลังจากอ่านบทนี้แล้ว คุณจะสามารถ:
- เข้าใจปัญหาของ Repository Explosion
- ใช้ Specification Pattern เพื่อสร้าง composable query logic
- ใช้ Expression Trees เพื่อทำงานกับทั้ง in-memory และ EF Core
- **เปรียบเทียบ OOP vs FP approaches สำหรับ specifications**
- **สร้าง pure specification builders แบบ functional**
- **ใช้ monadic composition (Map/Bind) กับ specifications**
- **ผสาน specifications กับ language-ext types (Option, Either, Validation)**
- เขียน Test และ Live implementations ที่ใช้ specification ร่วมกัน

---

## 📚 สิ่งที่จะได้เรียนรู้

### 1. ปัญหาของ Repository Explosion
- Repository ที่มี query methods มากเกินไป
- Combinatorial explosion (N × M combinations)
- ความยากในการ test และ maintain
- Code duplication ระหว่าง query methods

### 2. Specification Pattern
- แนวคิดพื้นฐานของ Specification Pattern
- Expression Trees ใน C#
- การ compose specifications ด้วย And, Or, Not
- Implicit operator สำหรับ convenience

### 3. Implementation ทีละขั้นตอน
- Step 1: สร้าง base `Specification<T>` class
- Step 2: สร้าง domain-specific specifications (TodosByUserSpec, CompletedTodoSpec, etc.)
- Step 3: อัพเดท repository interface เพิ่ม `FindAsync` method
- Step 4: Implement ใน Test repository (LINQ to Objects)
- Step 5: Implement ใน Live repository (EF Core)
- Step 6: อัพเดท capability modules
- Step 7: ใช้งานใน application services

### 4. Functional Programming Approach ⭐ NEW
- **OOP vs FP specifications** - เปรียบเทียบ 2 แนวทาง
- **Pure specification builders** - static methods แทน classes
- **Monadic composition** - Map, Bind operations
- **Specification as Functor/Monad** - category theory concepts

### 5. Integration กับ language-ext ⭐ NEW
- **Option integration** - queries ที่อาจไม่มีผลลัพธ์
- **Either integration** - error handling แบบ type-safe
- **Validation integration** - specifications เป็น validation rules
- **Pure functions** - specification builders ที่ไม่มี side effects

---

## 📖 โครงสร้างเนื้อหา

### บทนำ: ปัญหาที่เจอในโลกจริง (5 นาที)
- เริ่มจาก Todo app ที่มี basic queries
- เมื่อ requirements เพิ่มขึ้น → query methods ระเบิด
- ตัวอย่าง: GetCompletedTodosByUserCreatedAfter...() 😱

### ส่วนที่ 1: แนวคิด Specification Pattern (10 นาที)
- ประวัติและที่มาของ pattern
- ทำไม Expression Trees ถึงเหมาะกับ C#
- Architecture overview diagram

### ส่วนที่ 2: การ Implement (30 นาที)

#### 2.1 Base Specification Class
```csharp
public abstract class Specification<T>
{
    public abstract Expression<Func<T, bool>> ToExpression();
    public Specification<T> And(Specification<T> other) { ... }
    public Specification<T> Or(Specification<T> other) { ... }
    public Specification<T> Not() { ... }
}
```

#### 2.2 Domain-Specific Specifications
```csharp
public class CompletedTodoSpec : Specification<Todo>
public class TodosByUserSpec : Specification<Todo>
public class CreatedAfterSpec : Specification<Todo>
```

#### 2.3 Repository Interface
```csharp
public interface ITodoRepository
{
    // เดิม: GetAllTodosAsync, GetCompletedTodosAsync, ... (10+ methods)
    // ใหม่: FindAsync(Specification<Todo> spec, CancellationToken ct)
}
```

#### 2.4 Test Implementation
```csharp
// ใช้ .Where(spec.ToExpression().Compile()) กับ in-memory list
```

#### 2.5 Live Implementation
```csharp
// ใช้ .Where(spec.ToExpression()) กับ EF Core
```

### ส่วนที่ 3: การใช้งานแบบ OOP (Traditional) (10 นาที)

#### ตัวอย่างที่ 1: Simple Query
```csharp
var completedTodos = await TodoRepo.findTodos(new CompletedTodoSpec(), ct);
```

#### ตัวอย่างที่ 2: Composed Query
```csharp
var spec = new CompletedTodoSpec()
    .And(new TodosByUserSpec(userId))
    .And(new CreatedAfterSpec(DateTime.Now.AddDays(-7)));

var recentCompletedByUser = await TodoRepo.findTodos(spec, ct);
```

**ปัญหาของแนวทาง OOP:**
- ต้องสร้าง class ใหม่ทุกครั้ง
- Constructor dependencies ทำให้ซับซ้อน
- ยากต่อการ reuse และ compose

---

### ส่วนที่ 4: FP Refactoring - Pure Specification Builders ⭐ (20 นาที)

#### 4.1 จาก Classes → Pure Functions

**❌ OOP Style (class-based):**
```csharp
public class CompletedTodoSpec : Specification<Todo>
{
    public override Expression<Func<Todo, bool>> ToExpression()
        => todo => todo.IsCompleted;
}

// ใช้งาน
var spec = new CompletedTodoSpec();
```

**✅ FP Style (pure builders):**
```csharp
public static class TodoSpecs
{
    // Pure specification builders - ไม่มี side effects
    public static Specification<Todo> IsCompleted() =>
        Spec.Create<Todo>(todo => todo.IsCompleted);

    public static Specification<Todo> ByUser(int userId) =>
        Spec.Create<Todo>(todo => todo.UserId == userId);

    public static Specification<Todo> CreatedAfter(DateTime date) =>
        Spec.Create<Todo>(todo => todo.CreatedAt > date);

    // Composed builders - function composition
    public static Specification<Todo> CompletedByUser(int userId) =>
        IsCompleted().And(ByUser(userId));

    public static Specification<Todo> RecentAndCompleted(int days) =>
        IsCompleted().And(CreatedAfter(DateTime.Now.AddDays(-days)));
}

// ใช้งาน - สั้นกระชับกว่า
var spec = TodoSpecs.IsCompleted();
var composed = TodoSpecs.CompletedByUser(userId);
```

**ข้อดีของ FP Builders:**
- ✅ Pure functions - predictable, testable
- ✅ No inheritance hierarchy
- ✅ Easy to compose and reuse
- ✅ Type inference ทำงานได้ดีกว่า
- ✅ สอดคล้องกับ language-ext style

#### 4.2 Generic Spec Helpers
```csharp
public static class Spec
{
    // Factory method สำหรับสร้าง spec
    public static Specification<T> Create<T>(Expression<Func<T, bool>> expr) =>
        new ExpressionSpec<T>(expr);

    // Combinator functions
    public static Specification<T> All<T>() =>
        Create<T>(_ => true);

    public static Specification<T> None<T>() =>
        Create<T>(_ => false);
}
```

---

### ส่วนที่ 5: Monadic Composition ⭐ (25 นาที)

#### 5.1 Specification as Functor

```csharp
// เพิ่ม Map method ใน Specification<T>
public abstract class Specification<T>
{
    // Existing methods...
    public abstract Expression<Func<T, bool>> ToExpression();
    public Specification<T> And(Specification<T> other) { ... }
    public Specification<T> Or(Specification<T> other) { ... }

    // ⭐ NEW: Functor Map
    public Specification<TResult> Map<TResult>(
        Func<T, TResult> selector)
    {
        // Transform specification to work on different type
        // Useful for projection queries
    }
}
```

**ตัวอย่างการใช้ Map:**
```csharp
// Query todos and project to DTO
var todoSpec = TodoSpecs.IsCompleted();

// Map spec to work with projections
var titleSpec = todoSpec.Map(todo => new TodoDto
{
    Id = todo.Id,
    Title = todo.Title
});
```

#### 5.2 Specification as Monad (Bind)

```csharp
public abstract class Specification<T>
{
    // ⭐ NEW: Monadic Bind
    public Specification<TResult> Bind<TResult>(
        Func<T, Specification<TResult>> binder)
    {
        // Chain specifications together
    }

    // ⭐ LINQ query syntax support
    public Specification<TResult> SelectMany<TResult>(
        Func<T, Specification<TResult>> binder) => Bind(binder);
}
```

#### 5.3 LINQ Query Syntax
```csharp
// ใช้ LINQ query syntax กับ specifications
var complexSpec =
    from todo in TodoSpecs.ByUser(userId)
    where todo.IsCompleted
    where todo.CreatedAt > DateTime.Now.AddDays(-7)
    select todo;
```

---

### ส่วนที่ 6: Integration กับ language-ext Types ⭐ (25 นาที)

#### 6.1 Specifications + Option<T>

```csharp
// Query ที่อาจไม่มีผลลัพธ์
public static Eff<RT, Option<Todo>> findFirst<RT>(
    Specification<Todo> spec)
    where RT : struct, HasTodoRepo<RT>, HasCancellationToken<RT>
{
    return from todos in TodoRepo.findTodos<RT>(spec)
           select todos.HeadOrNone();  // ✅ language-ext Option
}

// ใช้งาน
var result = await TodoService
    .findFirst(TodoSpecs.ByUser(userId))
    .Run(runtime, ct);

result.Match(
    Some: todo => $"Found: {todo.Title}",
    None: () => "No todo found"
);
```

#### 6.2 Specifications + Either<Error, T>

```csharp
// Query พร้อม error handling
public static Eff<RT, Either<Error, Todo>> findById<RT>(
    int id)
    where RT : struct, HasTodoRepo<RT>, HasCancellationToken<RT>
{
    return from todoOpt in findFirst<RT>(TodoSpecs.ById(id))
           select todoOpt.ToEither<Error>(
               Error.New("TODO_NOT_FOUND", $"Todo {id} not found")
           );
}

// ใช้งาน
var result = await TodoService.findById(123).Run(runtime, ct);

result.Match(
    Right: todo => $"Found: {todo.Title}",
    Left: error => $"Error: {error.Message}"
);
```

#### 6.3 Specifications as Validation Rules ⭐

```csharp
// ใช้ specification เป็น validation rules
public static class TodoValidations
{
    // Specification สำหรับ validation
    public static Specification<Todo> HasValidTitle() =>
        Spec.Create<Todo>(t => !string.IsNullOrWhiteSpace(t.Title));

    public static Specification<Todo> TitleNotTooLong() =>
        Spec.Create<Todo>(t => t.Title.Length <= 200);

    public static Specification<Todo> HasValidUser() =>
        Spec.Create<Todo>(t => t.UserId > 0);
}

// แปลง specification → Validation monad
public static Validation<Error, Todo> ValidateTodo(Todo todo)
{
    var validations = new[]
    {
        TodoValidations.HasValidTitle(),
        TodoValidations.TitleNotTooLong(),
        TodoValidations.HasValidUser()
    };

    var errors = validations
        .Where(spec => !spec.IsSatisfiedBy(todo))
        .Select(spec => Error.New("VALIDATION", spec.GetErrorMessage()))
        .ToSeq();

    return errors.IsEmpty
        ? Success<Error, Todo>(todo)
        : Fail<Error, Todo>(errors);
}

// ใช้งาน
var validation = ValidateTodo(newTodo);

validation.Match(
    Succ: todo => SaveTodo(todo),
    Fail: errors => ShowErrors(errors)
);
```

#### 6.4 Chaining Specifications with Eff

**❌ แบบเดิม (mutate variable):**
```csharp
// ⚠️ ไม่ใช่ FP - กำลัง mutate spec!
public static Eff<RT, Seq<Todo>> getFilteredTodos<RT>(
    int userId,
    bool completedOnly,
    int lastDays)
    where RT : struct, HasTodoRepo<RT>, HasCancellationToken<RT>
{
    var spec = TodoSpecs.ByUser(userId);  // mutable variable

    if (completedOnly)
        spec = spec.And(TodoSpecs.IsCompleted());  // ❌ mutation!

    if (lastDays > 0)
        spec = spec.And(TodoSpecs.CreatedAfter(
            DateTime.Now.AddDays(-lastDays)));  // ❌ mutation!

    return from todos in TodoRepo.findTodos<RT>(spec)
           select todos.ToSeq();
}
```

**✅ FP Style 1: Conditional Expression (Immutable)**
```csharp
public static Eff<RT, Seq<Todo>> getFilteredTodos<RT>(
    int userId,
    bool completedOnly,
    int lastDays)
    where RT : struct, HasTodoRepo<RT>, HasCancellationToken<RT>
{
    // Pure expression - ไม่มี mutation
    var spec =
        (completedOnly
            ? TodoSpecs.ByUser(userId).And(TodoSpecs.IsCompleted())
            : TodoSpecs.ByUser(userId))
        |> (s => lastDays > 0
            ? s.And(TodoSpecs.CreatedAfter(DateTime.Now.AddDays(-lastDays)))
            : s);

    return from todos in TodoRepo.findTodos<RT>(spec)
           select todos.ToSeq();
}
```

**✅ FP Style 2: Fold Pattern (แนะนำ!)** ⭐
```csharp
public static Eff<RT, Seq<Todo>> getFilteredTodos<RT>(
    int userId,
    bool completedOnly,
    int lastDays)
    where RT : struct, HasTodoRepo<RT>, HasCancellationToken<RT>
{
    // Build list of filters to apply
    var filters = Seq(
        Some(TodoSpecs.ByUser(userId)),
        completedOnly ? Some(TodoSpecs.IsCompleted()) : None,
        lastDays > 0 ? Some(TodoSpecs.CreatedAfter(
            DateTime.Now.AddDays(-lastDays))) : None
    ).Somes();  // Keep only Some values

    // Fold all filters together - pure composition!
    var spec = filters.Reduce((acc, filter) => acc.And(filter));

    return from todos in TodoRepo.findTodos<RT>(spec)
           from validated in todos.Traverse(ValidateTodo)
           select validated.ToSeq();
}
```

**✅ FP Style 3: Pipe Operator** ⭐⭐
```csharp
public static Eff<RT, Seq<Todo>> getFilteredTodos<RT>(
    int userId,
    bool completedOnly,
    int lastDays)
    where RT : struct, HasTodoRepo<RT>, HasCancellationToken<RT>
{
    // Pure functional pipe - อ่านง่าย ไม่มี mutation
    var spec = TodoSpecs.ByUser(userId)
        .Apply(s => completedOnly ? s.And(TodoSpecs.IsCompleted()) : s)
        .Apply(s => lastDays > 0
            ? s.And(TodoSpecs.CreatedAfter(DateTime.Now.AddDays(-lastDays)))
            : s);

    return from todos in TodoRepo.findTodos<RT>(spec)
           from validated in todos.Traverse(ValidateTodo)
           select validated.ToSeq();
}

// Helper extension
public static class SpecificationExtensions
{
    public static T Apply<T>(this T value, Func<T, T> fn) => fn(value);
}
```

**✅ FP Style 4: Builder Function**
```csharp
public static Eff<RT, Seq<Todo>> getFilteredTodos<RT>(
    int userId,
    bool completedOnly,
    int lastDays)
    where RT : struct, HasTodoRepo<RT>, HasCancellationToken<RT>
{
    // Pure builder function
    Specification<Todo> BuildSpec()
    {
        var baseSpec = TodoSpecs.ByUser(userId);

        return (completedOnly, lastDays > 0) switch
        {
            (true, true) => baseSpec
                .And(TodoSpecs.IsCompleted())
                .And(TodoSpecs.CreatedAfter(DateTime.Now.AddDays(-lastDays))),

            (true, false) => baseSpec
                .And(TodoSpecs.IsCompleted()),

            (false, true) => baseSpec
                .And(TodoSpecs.CreatedAfter(DateTime.Now.AddDays(-lastDays))),

            (false, false) => baseSpec
        };
    }

    return from todos in TodoRepo.findTodos<RT>(BuildSpec())
           from validated in todos.Traverse(ValidateTodo)
           select validated.ToSeq();
}
```

**เปรียบเทียบ Styles:**

| Style | Readability | Purity | Scalability | แนะนำ |
|-------|-------------|--------|-------------|-------|
| Mutation | ⭐⭐⭐⭐ | ❌ | ⭐⭐ | ไม่แนะนำ |
| Conditional | ⭐⭐ | ✅ | ⭐⭐ | OK |
| Fold | ⭐⭐⭐⭐ | ✅ | ⭐⭐⭐⭐⭐ | ⭐ ดีที่สุด! |
| Pipe | ⭐⭐⭐⭐⭐ | ✅ | ⭐⭐⭐⭐ | ⭐ แนะนำ! |
| Pattern Match | ⭐⭐⭐ | ✅ | ⭐⭐ | OK |

**แนะนำใช้ Fold Pattern** เพราะ:
- ✅ Pure - ไม่มี mutation
- ✅ Scalable - เพิ่ม filter ได้ง่าย
- ✅ Composable - ใช้ language-ext Seq
- ✅ Declarative - บอกว่า "มี filters อะไรบ้าง" แล้ว compose
- ✅ Type-safe - compile-time safety

---

### ส่วนที่ 7: Testing Specifications (15 นาที)

#### 7.1 Unit Test Pure Builders
```csharp
[Test]
public void TodoSpecs_IsCompleted_FilterCorrectly()
{
    // Arrange
    var todos = new[]
    {
        new Todo { Id = 1, IsCompleted = true },
        new Todo { Id = 2, IsCompleted = false },
        new Todo { Id = 3, IsCompleted = true }
    };

    // Act - test pure function
    var spec = TodoSpecs.IsCompleted();
    var filtered = todos.Where(spec.IsSatisfiedBy);

    // Assert
    filtered.Should().HaveCount(2);
}
```

#### 7.2 Test Monadic Composition
```csharp
[Test]
public void Specification_Map_TransformsCorrectly()
{
    var spec = TodoSpecs.ByUser(1);
    var mapped = spec.Map(todo => todo.Title);

    // Test functor laws
    // 1. Identity: spec.Map(x => x) == spec
    // 2. Composition: spec.Map(f).Map(g) == spec.Map(x => g(f(x)))
}
```

#### 7.3 Test with Option/Either
```csharp
[Test]
public async Task FindFirst_NoMatch_ReturnsNone()
{
    // Arrange
    var spec = TodoSpecs.ByUser(999); // Non-existent user

    // Act
    var result = await TodoService
        .findFirst(spec)
        .Run(testRuntime, ct);

    // Assert
    result.IsNone.Should().BeTrue();
}
```

---

### ส่วนที่ 8: OOP vs FP Comparison (10 นาที)

#### Side-by-Side Comparison

| Aspect | OOP Approach | FP Approach |
|--------|--------------|-------------|
| **Definition** | Classes (inheritance) | Pure functions (composition) |
| **State** | Instance fields | Stateless |
| **Composition** | Method chaining | Function composition |
| **Reuse** | Inheritance | Higher-order functions |
| **Testing** | Mock objects | Pure function testing |
| **Type Safety** | Manual validation | Monadic types (Option/Either) |

#### เมื่อไหร่ใช้อะไร?

**ใช้ OOP Style:**
- Specifications ที่ซับซ้อนมาก มี internal state
- ต้อง polymorphism แบบ runtime
- Team คุ้นเคยกับ OOP มากกว่า

**ใช้ FP Style:** ⭐ แนะนำ
- ต้องการ pure, testable code
- Composition มีความสำคัญ
- ผสานกับ language-ext ecosystem
- ต้องการ type-safe error handling

---

## 💻 ตัวอย่างโค้ดที่จะใช้

### ไฟล์ที่จะสร้าง/แก้ไข

**Traditional Implementation (40%):**
- `Domain/Specifications/Specification.cs` (new) - base class
- `Domain/Specifications/TodoSpecs.cs` (new) - OOP style specs
- `Infrastructure/Repositories/ITodoRepository.cs` (modified)
- `Infrastructure/Repositories/TestTodoRepository.cs` (modified)
- `Infrastructure/Repositories/LiveTodoRepository.cs` (modified)

**FP Enhancements (60%):** ⭐
- `Domain/Specifications/Spec.cs` (new) - pure builder helpers
- `Domain/Specifications/SpecificationExtensions.cs` (new) - Map/Bind
- `Domain/Specifications/TodoSpecBuilders.cs` (new) - FP style builders
- `Domain/Validations/TodoSpecValidations.cs` (new) - validation integration
- `Features/Todos/TodoService.cs` (modified) - Option/Either usage
- `Features/Todos/TodoQueryService.cs` (new) - monadic queries

### Code Statistics
- ลบ: ~10 query methods จาก repository
- เพิ่ม Traditional: ~200 lines (Specification base, OOP specs)
- เพิ่ม FP: ~300 lines (Pure builders, Map/Bind, integrations)
- Total LOC: ~500 lines

---

## 🧪 แบบฝึกหัด

### ระดับง่าย: ทำความเข้าใจ
1. ทำไม repository ถึงเกิด method explosion?
2. Expression Tree คืออะไร? ต่างจาก Func อย่างไร?
3. Pure function specification builder ต่างจาก class-based อย่างไร?
4. Functor และ Monad คืออะไร? เกี่ยวข้องกับ Specification ยังไง?

### ระดับกลาง: ลองเขียน
1. สร้าง pure builder `IncompleteTodoSpec()` แบบ FP style
2. เขียน specification ที่ใช้ Map เพื่อ project ไป TodoDto
3. ใช้ Option<T> กับ query ที่อาจไม่มีผลลัพธ์
4. สร้าง validation rules จาก specifications
5. Compose หลาย specs ด้วย And/Or แบบ monadic

### ระดับยาก: Challenges ⭐
1. Implement `Bind` method ให้ Specification เป็น monad
2. เขียน LINQ query syntax provider สำหรับ specifications
3. สร้าง `Traverse` method เพื่อ validate list of entities
4. Implement specification caching สำหรับ compiled expressions
5. สร้าง specification ที่ทำงานกับ async predicates
6. เขียน property-based tests สำหรับ functor/monad laws

---

## 🔗 เชื่อมโยงกับบทอื่น

**ต้องอ่านก่อน:**
- บทที่ 2: แนวคิดพื้นฐาน FP (Pure Functions, Monads)
- บทที่ 3: แนะนำ language-ext v5 (Option, Either, Validation)
- บทที่ 4: Has<M, RT, T>.ask Pattern
- บทที่ 5: Backend API ด้วย Capabilities
- บทที่ 6: Validation และ Error Handling

**อ่านต่อ:**
- บทที่ 16: Pagination Pattern (ใช้ specification ร่วมกับ pagination)
- บทที่ 17: Transaction Handling (multi-entity operations)

---

## 📊 สถิติและเวลาอ่าน

- **ระดับความยาก:** ⭐⭐⭐⭐ (ค่อนข้างยาก - มี FP concepts เยอะ)
- **เวลาอ่าน:** ~90 นาที (เพิ่มจาก 60 เพราะมี FP sections)
- **เวลาลงมือทำ:** ~150 นาที (รวม FP refactoring)
- **จำนวนตัวอย่างโค้ด:** ~25 ตัวอย่าง (เพิ่มจาก 15)
- **จำนวนหน้าโดยประมาณ:** ~18 หน้า (เพิ่มจาก 12)

---

## 💡 Key Takeaways

หลังจากอ่านบทนี้ คุณจะได้:

1. **Pattern ที่แก้ปัญหาจริง** - Repository explosion เป็นปัญหาที่เจอบ่อย
2. **OOP → FP Evolution** - เห็นวิธี refactor traditional pattern เป็น functional
3. **Pure Functional Design** - Specification builders ที่ไม่มี side effects
4. **Monadic Composition** - ใช้ Map/Bind เพื่อ compose specifications
5. **Type-Safe Queries** - ผสาน Option/Either สำหรับ error handling
6. **Validation as Specifications** - reuse query logic เป็น validation rules
7. **Testable & Composable** - Pure functions ที่ test ง่ายและ compose ได้

---

## 📝 หมายเหตุสำหรับผู้เขียน

- ใช้ตัวอย่างจาก SCALING-PATTERNS.md sections:
  - "Specification Pattern for Repository Queries"
  - Implementation steps 1-7
  - Testing section
  - Benefits section

- **เน้น OOP → FP progression** (40% traditional, 60% FP)
- แสดง before/after comparison ทุกส่วน
- เปรียบเทียบ class-based vs pure builders side-by-side
- แสดง EF Core query ที่ generate ออกมา (SQL)
- เปรียบเทียบกับ alternative approaches
- **เน้น language-ext integration** - Option, Either, Validation, Seq
- **Functor/Monad laws** - อธิบายและทดสอบ
- **Real-world use cases** - เมื่อไหร่ใช้ OOP vs FP

---

**Status:** 📋 Outline Ready → ⏳ Ready to Write
