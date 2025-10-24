# บทที่ 17: Transaction Handling

> จัดการ Multi-Entity Operations ด้วย Unit of Work Pattern

---

## 🎯 เป้าหมายของบท

หลังจากอ่านบทนี้แล้ว คุณจะสามารถ:
- เข้าใจปัญหาของ data consistency ใน multi-entity operations
- ใช้ Unit of Work pattern เป็น trait
- จัดการ explicit transaction boundaries ใน service code
- ทำให้ transaction logic testable
- รู้ว่าเมื่อไหร่ควรใช้ transaction และเมื่อไหร่ไม่ควร

---

## 📚 สิ่งที่จะได้เรียนรู้

### 1. ปัญหาของ Data Consistency
- Data inconsistency เมื่อมี multiple entity updates
- Partial failures ที่ทำให้ data corrupt
- Race conditions ใน concurrent operations
- ปัญหาของ SaveChanges ใน repository แต่ละตัว

### 2. Unit of Work Pattern
- แนวคิดของ Unit of Work
- Transaction boundaries
- Atomic operations across multiple repositories
- Automatic rollback on failures

### 3. Implementation as Trait
- `IUnitOfWork` trait
- Integration กับ Has<M, RT, T> pattern
- Test และ Live implementations
- อัพเดท repositories และ runtimes

### 4. Transaction Patterns
- Single-entity operations (ไม่ต้องใช้ transaction)
- Multi-entity operations (ใช้ inTransaction)
- Conditional transactions
- Nested transactions

---

## 📖 โครงสร้างเนื้อหา

### บทนำ: ปัญหาของ Data Consistency (10 นาที)

#### Scenario: สร้าง Project พร้อม Tasks
```csharp
// ❌ ปัญหา: ถ้า CreateTask ล้มเหลว Project ยังถูกสร้าง!
await ProjectRepo.CreateProject(project, ct);
await ProjectRepo.SaveChangesAsync(ct);  // ✅ Saved

await TaskRepo.CreateTask(task1, ct);
await TaskRepo.SaveChangesAsync(ct);     // ❌ Failed!

// Result: Project สร้างแล้ว แต่ไม่มี Task → Data inconsistency!
```

#### สิ่งที่ต้องการ
- ทั้ง project และ tasks ต้อง **สำเร็จพร้อมกัน** หรือ **ล้มเหลวพร้อมกัน**
- Automatic rollback เมื่อเกิด error
- Testable transaction logic

### ส่วนที่ 1: Unit of Work Pattern (10 นาที)

#### แนวคิดพื้นฐาน
```
┌─────────────────────────────────────────┐
│  Service Layer                          │
│  ┌───────────────────────────────────┐  │
│  │ Begin Transaction                 │  │
│  │  ├─ ProjectRepo.Add()            │  │
│  │  ├─ TaskRepo.Add()               │  │
│  │  ├─ TaskRepo.Add()               │  │
│  │  └─ Commit (all or nothing)      │  │
│  └───────────────────────────────────┘  │
└─────────────────────────────────────────┘
```

#### Traditional Approach vs. Our Approach

**Traditional (tight coupling):**
```csharp
using var transaction = _dbContext.BeginTransaction();
try {
    // ... operations ...
    await transaction.CommitAsync();
} catch {
    await transaction.RollbackAsync();
}
```

**Our Approach (trait-based):**
```csharp
// ✅ Functional, testable, composable
await UnitOfWork.inTransaction(Eff(() =>
{
    from _ in ProjectRepo.createProject(project)
    from __ in TaskRepo.createTask(task1)
    from ___ in TaskRepo.createTask(task2)
    select unit
})).Run(runtime, ct);
```

### ส่วนที่ 2: Implementation (40 นาที)

#### 2.1 Define IUnitOfWork Trait
```csharp
// Infrastructure/Capabilities/IUnitOfWork.cs
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
    Task<T> InTransactionAsync<T>(
        Func<Task<T>> operation,
        CancellationToken ct
    );
}
```

#### 2.2 Update Repository Traits
```csharp
// เอา SaveChangesAsync ออกจาก repository methods
public interface ITodoRepository
{
    void AddTodo(Todo todo);          // ไม่ save เอง
    void UpdateTodo(Todo todo);       // ไม่ save เอง
    void DeleteTodo(Todo todo);       // ไม่ save เอง

    // Query methods ไม่เปลี่ยน
    Task<List<Todo>> GetAllTodosAsync(CancellationToken ct);
}
```

#### 2.3 Capability Module
```csharp
// Infrastructure/Capabilities/UnitOfWorkCapability.cs
public interface HasUnitOfWork<RT> where RT : struct, HasUnitOfWork<RT>
{
    Eff<RT, IUnitOfWork> UnitOfWorkEff { get; }
}

public static class UnitOfWork
{
    public static Eff<RT, Unit> saveChanges<RT>()
        where RT : struct, HasUnitOfWork<RT>, HasCancellationToken<RT>
    {
        return from uow in default(RT).UnitOfWorkEff
               from ct in CancellationTokenIO.token<RT>()
               from _ in EffMaybe(() => uow.SaveChangesAsync(ct))
               select unit;
    }

    public static Eff<RT, A> inTransaction<RT, A>(Eff<RT, A> operation)
        where RT : struct, HasUnitOfWork<RT>, HasCancellationToken<RT>
    {
        return from uow in default(RT).UnitOfWorkEff
               from ct in CancellationTokenIO.token<RT>()
               from result in EffMaybe(() =>
                   uow.InTransactionAsync(async () =>
                       await operation.Run(default(RT), ct), ct))
               select result;
    }
}
```

#### 2.4 Live Implementation
```csharp
// Infrastructure/Live/LiveUnitOfWork.cs
public class LiveUnitOfWork(TodoDbContext context) : IUnitOfWork
{
    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await context.SaveChangesAsync(ct);
    }

    public async Task<T> InTransactionAsync<T>(
        Func<Task<T>> operation,
        CancellationToken ct)
    {
        var strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await context.Database.BeginTransactionAsync(ct);

            try
            {
                var result = await operation();
                await transaction.CommitAsync(ct);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }
}
```

#### 2.5 Test Implementation
```csharp
// Infrastructure/Test/TestUnitOfWork.cs
public class TestUnitOfWork : IUnitOfWork
{
    private bool _inTransaction = false;

    public Task SaveChangesAsync(CancellationToken ct)
    {
        // Test: Just mark as saved
        return Task.CompletedTask;
    }

    public async Task<T> InTransactionAsync<T>(
        Func<Task<T>> operation,
        CancellationToken ct)
    {
        _inTransaction = true;
        try
        {
            var result = await operation();
            _inTransaction = false;
            return result;
        }
        catch
        {
            _inTransaction = false;
            throw; // Simulate rollback
        }
    }
}
```

#### 2.6 Update Runtime
```csharp
public record LiveRuntime(
    IConfiguration Config,
    ILogger<LiveRuntime> Logger,
    TodoDbContext DbContext
) : IAppRuntime,
    HasTodoRepo<LiveRuntime>,
    HasUnitOfWork<LiveRuntime>,  // ✅ เพิ่ม
    HasLogger<LiveRuntime>,
    HasCancellationToken<LiveRuntime>
{
    private readonly Lazy<LiveTodoRepository> _todoRepo =
        new(() => new LiveTodoRepository(DbContext));
    private readonly Lazy<LiveUnitOfWork> _unitOfWork =
        new(() => new LiveUnitOfWork(DbContext));  // ✅ เพิ่ม

    public Eff<LiveRuntime, ITodoRepository> TodoRepoEff =>
        SuccessEff(_todoRepo.Value);

    public Eff<LiveRuntime, IUnitOfWork> UnitOfWorkEff =>  // ✅ เพิ่ม
        SuccessEff(_unitOfWork.Value);
}
```

### ส่วนที่ 3: Usage Patterns (20 นาที)

#### Pattern 1: Simple Single-Entity Operation
```csharp
// ✅ ไม่ต้องใช้ transaction
public static Eff<RT, Todo> createTodo<RT>(TodoCreateDto dto)
    where RT : struct,
        HasTodoRepo<RT>,
        HasUnitOfWork<RT>,
        HasCancellationToken<RT>
{
    return from todo in CreateTodoEntity(dto)
           from _ in TodoRepo.addTodo<RT>(todo)
           from __ in UnitOfWork.saveChanges<RT>()  // ✅ Simple save
           select todo;
}
```

#### Pattern 2: Multi-Entity Operation with Transaction
```csharp
// ✅ ใช้ transaction
public static Eff<RT, (Project, List<Task>)> createProjectWithTasks<RT>(
    ProjectCreateDto projectDto,
    List<TaskCreateDto> taskDtos)
    where RT : struct,
        HasProjectRepo<RT>,
        HasTaskRepo<RT>,
        HasUnitOfWork<RT>,
        HasCancellationToken<RT>
{
    return UnitOfWork.inTransaction<RT, (Project, List<Task>)>(
        from project in CreateProjectEntity(projectDto)
        from _ in ProjectRepo.addProject<RT>(project)
        from tasks in CreateTaskEntities(taskDtos, project.Id)
        from __ in tasks.Traverse(task => TaskRepo.addTask<RT>(task))
        select (project, tasks)
    );  // ✅ Auto commit/rollback
}
```

#### Pattern 3: Conditional Transaction
```csharp
// ใช้ transaction เฉพาะเมื่อมี related operations
public static Eff<RT, Todo> updateTodo<RT>(
    int id,
    TodoUpdateDto dto,
    bool notifyUsers)
    where RT : struct, /* ... */
{
    var updateOp =
        from todo in TodoRepo.getTodoById<RT>(id)
        from updated in UpdateTodoEntity(todo, dto)
        from _ in TodoRepo.updateTodo<RT>(updated)
        select updated;

    return notifyUsers
        ? UnitOfWork.inTransaction<RT, Todo>(
            from todo in updateOp
            from _ in NotificationRepo.createNotifications<RT>(todo)
            select todo
          )
        : from todo in updateOp
          from _ in UnitOfWork.saveChanges<RT>()
          select todo;
}
```

### ส่วนที่ 4: Testing Transactions (15 นาที)

#### Test 1: Transaction Success
```csharp
[Test]
public async Task CreateProjectWithTasks_Success_CommitsAll()
{
    // Arrange
    var projectDto = new ProjectCreateDto("New Project");
    var taskDtos = new List<TaskCreateDto>
    {
        new("Task 1"),
        new("Task 2")
    };

    // Act
    var result = await ProjectService
        .createProjectWithTasks(projectDto, taskDtos)
        .Run(testRuntime, CancellationToken.None);

    // Assert
    result.IsSuccess.Should().BeTrue();
    var projects = await testRuntime.ProjectRepo.GetAllAsync(ct);
    projects.Should().HaveCount(1);
    var tasks = await testRuntime.TaskRepo.GetAllAsync(ct);
    tasks.Should().HaveCount(2);
}
```

#### Test 2: Transaction Rollback on Failure
```csharp
[Test]
public async Task CreateProjectWithTasks_Failure_RollsBackAll()
{
    // Arrange: Make second task creation fail
    var projectDto = new ProjectCreateDto("Project");
    var taskDtos = new List<TaskCreateDto>
    {
        new("Task 1"),
        new("InvalidTask")  // This will fail validation
    };

    // Act
    var result = await ProjectService
        .createProjectWithTasks(projectDto, taskDtos)
        .Run(testRuntime, CancellationToken.None);

    // Assert
    result.IsFail.Should().BeTrue();

    // ✅ Nothing should be saved (atomic operation)
    var projects = await testRuntime.ProjectRepo.GetAllAsync(ct);
    projects.Should().BeEmpty();
    var tasks = await testRuntime.TaskRepo.GetAllAsync(ct);
    tasks.Should().BeEmpty();
}
```

### ส่วนที่ 5: Decision Guide (10 นาที)

#### ✅ ใช้ Transaction (inTransaction) เมื่อ:
- **Multi-entity operations** - สร้าง/แก้ไข/ลบหลาย entity
- **Data consistency critical** - ต้อง all-or-nothing
- **Related operations** - operations ที่ต้องเกิดพร้อมกัน
- **Cascading updates** - แก้ไข entity หนึ่งแล้วต้อง update related entities

**Examples:**
```
✅ Create project with initial tasks
✅ Transfer money between accounts
✅ Complete order (update stock, create invoice, send notification)
✅ Delete user and all related data
```

#### ✅ ใช้ Simple SaveChanges เมื่อ:
- **Single entity** - แก้ไข entity เดียว
- **Independent operations** - operation ไม่กระทบ entity อื่น
- **Query operations** - read-only
- **Simple CRUD** - create/update/delete แบบธรรมดา

**Examples:**
```
✅ Create single todo
✅ Update todo status
✅ Delete single todo
✅ Get todos (no save needed)
```

### ส่วนที่ 6: Migration Path (10 นาที)

จาก codebase ที่มี repository save ของตัวเอง → Unit of Work pattern

**Step 1:** เพิ่ม IUnitOfWork trait และ implementations
**Step 2:** ลบ SaveChangesAsync จาก repository interfaces
**Step 3:** อัพเดท repository implementations
**Step 4:** อัพเดท runtimes เพิ่ม IUnitOfWork
**Step 5:** แก้ services ใช้ UnitOfWork.saveChanges
**Step 6:** เพิ่ม transactions ให้ multi-entity operations
**Step 7:** อัพเดท tests

---

## 💻 ตัวอย่างโค้ดที่จะใช้

### Files to Create/Modify
- `Infrastructure/Capabilities/IUnitOfWork.cs` (new)
- `Infrastructure/Capabilities/UnitOfWorkCapability.cs` (new)
- `Infrastructure/Live/LiveUnitOfWork.cs` (new)
- `Infrastructure/Test/TestUnitOfWork.cs` (new)
- `Infrastructure/AppRuntime.cs` (modified - add IUnitOfWork)
- `Infrastructure/Repositories/ITodoRepository.cs` (modified - remove SaveChanges)
- `Infrastructure/Repositories/*.cs` (modified - remove SaveChanges)
- `Features/Todos/TodoService.cs` (modified - use UnitOfWork)

### Code Statistics
- New LOC: ~150 lines
- Modified LOC: ~200 lines
- Test LOC: ~120 lines

---

## 🧪 แบบฝึกหัด

### ระดับง่าย: ทำความเข้าใจ
1. Unit of Work pattern คืออะไร?
2. ทำไมต้องแยก SaveChanges ออกจาก repository?
3. inTransaction ต่างจาก saveChanges อย่างไร?

### ระดับกลาง: ลองเขียน
1. สร้าง operation ที่ update todo และสร้าง notification
2. เขียน test ที่ verify transaction rollback
3. เพิ่ม logging เพื่อ track transaction lifecycle

### ระดับยาก: Challenges
1. Implement nested transaction support
2. เพิ่ม transaction timeout handling
3. สร้าง transaction isolation level configuration
4. Implement distributed transaction (2-phase commit)
5. เพิ่ม retry logic สำหรับ deadlock scenarios

---

## 🔗 เชื่อมโยงกับบทอื่น

**ต้องอ่านก่อน:**
- บทที่ 4: Has<M, RT, T>.ask Pattern
- บทที่ 15: Specification Pattern
- บทที่ 16: Pagination Pattern

**อ่านต่อ:**
- บทที่ 18: Best Practices (transaction best practices)
- บทที่ 19: Production Deployment (database considerations)

---

## 📊 สถิติและเวลาอ่าน

- **ระดับความยาก:** ⭐⭐⭐⭐ (ค่อนข้างยาก)
- **เวลาอ่าน:** ~80 นาที
- **เวลาลงมือทำ:** ~120 นาที
- **จำนวนตัวอย่างโค้ด:** ~18 ตัวอย่าง
- **จำนวนหน้าโดยประมาณ:** ~16 หน้า

---

## 💡 Key Takeaways

หลังจากอ่านบทนี้ คุณจะได้:

1. **Data Consistency** - รับประกัน atomic operations across entities
2. **Testability** - Transaction logic test ได้โดยไม่ต้องใช้ database จริง
3. **Explicit Boundaries** - Transaction boundaries ชัดเจนใน service code
4. **Automatic Rollback** - Failure handling อัตโนมัติ
5. **Performance** - Batch saves ลด database round-trips

---

## 📝 หมายเหตุสำหรับผู้เขียน

- ใช้ตัวอย่างจาก SCALING-PATTERNS.md sections:
  - "Transaction Handling Pattern for Multi-Entity Architecture"
  - Implementation steps
  - Usage patterns
  - Testing section
  - Decision Guide

- เน้น:
  - Before/after migration examples
  - Real-world scenarios (project + tasks, order processing)
  - Common pitfalls และวิธีแก้
  - EF Core execution strategy

- Diagrams ที่ควรมี:
  - Transaction flow diagram
  - Commit vs. Rollback scenarios
  - Multi-entity dependency graph

- เปรียบเทียบ approaches:
  - Traditional using statement
  - Repository-level transactions
  - Service-level transactions (our approach)

---

**Status:** 📋 Outline Ready → ⏳ Ready to Write
