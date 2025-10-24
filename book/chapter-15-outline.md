# บทที่ 15: Specification Pattern

> แก้ปัญหา Repository Explosion ด้วย Composable Query Logic

---

## 🎯 เป้าหมายของบท

หลังจากอ่านบทนี้แล้ว คุณจะสามารถ:
- เข้าใจปัญหาของ Repository Explosion
- ใช้ Specification Pattern เพื่อสร้าง composable query logic
- ใช้ Expression Trees เพื่อทำงานกับทั้ง in-memory และ EF Core
- เขียน Test และ Live implementations ที่ใช้ specification ร่วมกัน
- สร้าง specification ที่ซับซ้อนด้วย And, Or, Not operations

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

### 4. Advanced Composition
- Combining multiple specifications
- Dynamic query building
- Reusable query logic across entities

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

### ส่วนที่ 3: การใช้งานจริง (15 นาที)

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

#### ตัวอย่างที่ 3: Dynamic Filters
```csharp
// Build spec based on user input
Specification<Todo> spec = new AllTodosSpec();
if (isCompletedOnly) spec = spec.And(new CompletedTodoSpec());
if (userId.HasValue) spec = spec.And(new TodosByUserSpec(userId.Value));
```

### ส่วนที่ 4: Testing Specifications (10 นาที)
- Unit test specifications in isolation
- Integration test with repository
- Verify EF Core query translation

### ส่วนที่ 5: ข้อดีของ Pattern นี้ (5 นาที)
- ✅ Eliminates repository explosion
- ✅ Reusable query logic
- ✅ Composable business logic
- ✅ Testable in isolation
- ✅ Works with both test and production
- ✅ Dynamic query building

---

## 💻 ตัวอย่างโค้ดที่จะใช้

### ไฟล์ที่จะสร้าง/แก้ไข
- `Domain/Specifications/Specification.cs` (new)
- `Domain/Specifications/TodoSpecs.cs` (new)
- `Infrastructure/Repositories/ITodoRepository.cs` (modified)
- `Infrastructure/Repositories/TestTodoRepository.cs` (modified)
- `Infrastructure/Repositories/LiveTodoRepository.cs` (modified)
- `Features/Todos/TodoService.cs` (modified)

### Code Statistics
- ลบ: ~10 query methods จาก repository
- เพิ่ม: 1 generic `FindAsync` method
- เพิ่ม: ~5 specification classes
- Total LOC: ~200 lines

---

## 🧪 แบบฝึกหัด

### ระดับง่าย: ทำความเข้าใจ
1. ทำไม repository ถึงเกิด method explosion?
2. Expression Tree คืออะไร? ต่างจาก Func อย่างไร?
3. `IsSatisfiedBy()` ใช้ทำอะไร?

### ระดับกลาง: ลองเขียน
1. สร้าง `IncompleteTodoSpec` specification
2. สร้าง `TodosByPrioritySpec` (สมมติมี priority field)
3. ใช้ And/Or เพื่อ compose query ที่ซับซ้อน

### ระดับยาก: Challenges
1. สร้าง `TextSearchSpec` ที่รองรับ partial text search
2. สร้าง specification สำหรับ entity อื่น (เช่น User, Project)
3. เพิ่ม sorting ใน specification (เช่น OrderByCreatedDate)

---

## 🔗 เชื่อมโยงกับบทอื่น

**ต้องอ่านก่อน:**
- บทที่ 4: Has<M, RT, T>.ask Pattern
- บทที่ 5: Backend API ด้วย Capabilities

**อ่านต่อ:**
- บทที่ 16: Pagination Pattern (ใช้ specification ร่วมกับ pagination)
- บทที่ 17: Transaction Handling (multi-entity operations)

---

## 📊 สถิติและเวลาอ่าน

- **ระดับความยาก:** ⭐⭐⭐ (กลาง)
- **เวลาอ่าน:** ~60 นาที
- **เวลาลงมือทำ:** ~90 นาที
- **จำนวนตัวอย่างโค้ด:** ~15 ตัวอย่าง
- **จำนวนหน้าโดยประมาณ:** ~12 หน้า

---

## 💡 Key Takeaways

หลังจากอ่านบทนี้ คุณจะได้:

1. **Pattern ที่แก้ปัญหาจริง** - Repository explosion เป็นปัญหาที่เจอบ่อย
2. **Composable design** - And/Or/Not ทำให้ query flexible
3. **Testable** - Specification test ได้แยกจาก repository
4. **Production-ready** - ใช้งานได้กับ EF Core โดยไม่ต้อง load ทั้ง table

---

## 📝 หมายเหตุสำหรับผู้เขียน

- ใช้ตัวอย่างจาก SCALING-PATTERNS.md sections:
  - "Specification Pattern for Repository Queries"
  - Implementation steps 1-7
  - Testing section
  - Benefits section

- เน้น before/after comparison
- แสดง EF Core query ที่ generate ออกมา (SQL)
- เปรียบเทียบกับ alternative approaches (Query Object, Repository per Query)

---

**Status:** 📋 Outline Ready → ⏳ Ready to Write
