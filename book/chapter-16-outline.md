# บทที่ 16: Pagination Pattern

> จัดการข้อมูลขนาดใหญ่อย่างมีประสิทธิภาพด้วย Paginated Queries

---

## 🎯 เป้าหมายของบท

หลังจากอ่านบทนี้แล้ว คุณจะสามารถ:
- เข้าใจปัญหาของการ load ข้อมูลทั้งหมดในครั้งเดียว
- ใช้ Pagination Pattern เพื่อจัดการ large datasets
- ผสม Pagination กับ Specification Pattern
- Implement ทั้ง offset-based และ cursor-based pagination
- เชื่อมต่อกับ Frontend (React/TypeScript)
- Optimize EF Core queries สำหรับ pagination

---

## 📚 สิ่งที่จะได้เรียนรู้

### 1. ปัญหาของ Large Datasets
- Performance issues เมื่อ load ข้อมูลหลายพันรายการ
- Memory consumption ที่สูง
- Network bandwidth wastage
- Poor user experience (slow loading)

### 2. Pagination Concepts
- Offset-based pagination (page number + size)
- Cursor-based pagination (for real-time data)
- Sorting และ filtering ร่วมกับ pagination
- Total count vs. approximate count

### 3. Integration กับ Specification Pattern
- ใช้ specification สำหรับ filtering
- Pagination parameters (page, pageSize, sortBy, sortOrder)
- PagedResult<T> model

### 4. Frontend Integration
- React hooks สำหรับ pagination
- Effect-TS integration
- Infinite scroll vs. page buttons
- Loading states และ error handling

---

## 📖 โครงสร้างเนื้อหา

### บทนำ: ปัญหาของ Large Datasets (5 นาที)
- Todo app ที่มี 10,000+ todos
- การ `GetAllTodos()` ทำให้:
  - API response ใช้เวลานาน
  - Browser ค้าง
  - Database overload

### ส่วนที่ 1: Pagination Fundamentals (10 นาที)

#### Offset-Based Pagination
```
Page 1: Skip(0).Take(10)  → Items 1-10
Page 2: Skip(10).Take(10) → Items 11-20
Page 3: Skip(20).Take(10) → Items 21-30
```

#### PagedResult Model
```csharp
public record PagedResult<T>
{
    public List<T> Items { get; init; }
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
    public bool HasPreviousPage { get; init; }
    public bool HasNextPage { get; init; }
}
```

### ส่วนที่ 2: Implementation (30 นาที)

#### 2.1 Sort Order Enum
```csharp
public enum SortOrder { Ascending, Descending }
```

#### 2.2 Update Repository Interface
```csharp
public interface ITodoRepository
{
    // เพิ่ม pagination method
    Task<PagedResult<Todo>> FindPagedAsync(
        Specification<Todo> spec,
        int pageNumber,
        int pageSize,
        Expression<Func<Todo, object>> sortBy,
        SortOrder sortOrder,
        CancellationToken ct
    );
}
```

#### 2.3 Test Implementation
```csharp
// In-memory pagination with LINQ
var query = _todos.AsQueryable()
    .Where(spec.ToExpression().Compile())
    .OrderBy(sortBy)
    .Skip((pageNumber - 1) * pageSize)
    .Take(pageSize);
```

#### 2.4 Live Implementation (EF Core)
```csharp
// Efficient database pagination
var query = _context.Todos
    .Where(spec.ToExpression())
    .OrderBy(sortBy)
    .Skip((pageNumber - 1) * pageSize)
    .Take(pageSize);

// Execute in single query
var (items, totalCount) = await (
    query.ToListAsync(ct),
    _context.Todos.Where(spec.ToExpression()).CountAsync(ct)
);
```

#### 2.5 Capability Module Updates
```csharp
public static partial class TodoRepoIO
{
    public static Eff<RT, PagedResult<Todo>> findTodosPaged<RT>(...)
        where RT : struct,
            HasTodoRepo<RT>,
            HasCancellationToken<RT>
    {
        return default(RT).TodoRepoEff
            .MapAsync(repo => repo.FindPagedAsync(...));
    }
}
```

### ส่วนที่ 3: API Controller Usage (15 นาที)

#### Query Parameters
```csharp
[HttpGet]
public async Task<ActionResult<PagedResult<TodoDto>>> GetTodos(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 10,
    [FromQuery] bool? isCompleted = null,
    [FromQuery] string sortBy = "createdAt",
    [FromQuery] string sortOrder = "desc"
)
```

#### Integration กับ Service Layer
```csharp
var spec = BuildSpecificationFromQuery(isCompleted);
var pagedResult = await TodoService
    .getTodosPaged(spec, page, pageSize, sortBy, sortOrder)
    .Run(runtime, ct);
```

### ส่วนที่ 4: Frontend Integration (20 นาที)

#### 4.1 Effect-TS Service
```typescript
export const getTodosPaged = (
  page: number,
  pageSize: number,
  filters?: TodoFilters
): Effect.Effect<PagedResult<Todo>, HttpError, never> => {
  const params = new URLSearchParams({
    page: page.toString(),
    pageSize: pageSize.toString(),
    ...filters
  })

  return HttpClient.get<PagedResult<Todo>>(
    `/api/todos?${params}`
  )
}
```

#### 4.2 React Hook
```typescript
const useTodosPaginated = (pageSize: number = 10) => {
  const [page, setPage] = useState(1)
  const [filters, setFilters] = useState<TodoFilters>({})

  const result = Effect.useRuntime(
    getTodosPaged(page, pageSize, filters)
  )

  return {
    todos: result.data?.items ?? [],
    totalPages: result.data?.totalPages ?? 0,
    currentPage: page,
    setPage,
    setFilters,
    isLoading: result.isLoading
  }
}
```

#### 4.3 UI Components
```typescript
// Pagination Controls
<PaginationBar
  currentPage={currentPage}
  totalPages={totalPages}
  onPageChange={setPage}
/>

// Infinite Scroll Alternative
<InfiniteScroll
  hasMore={hasNextPage}
  loadMore={() => setPage(p => p + 1)}
/>
```

### ส่วนที่ 5: Advanced Topics (15 นาที)

#### 5.1 Cursor-Based Pagination
```csharp
// สำหรับ real-time data ที่เปลี่ยนบ่อย
public record CursorPagedResult<T>
{
    public List<T> Items { get; init; }
    public string? NextCursor { get; init; }
    public string? PreviousCursor { get; init; }
    public bool HasMore { get; init; }
}
```

#### 5.2 Performance Optimization
- Index strategies สำหรับ sorting columns
- Approximate count สำหรับ large tables
- Parallel count query
- Caching strategies

### ส่วนที่ 6: Testing Pagination (10 นาที)

#### Unit Tests
```csharp
[Test]
public async Task FindPaged_ReturnsCorrectPage()
{
    // Arrange: Create 25 todos
    // Act: Request page 2, size 10
    // Assert: Should return todos 11-20
}

[Test]
public async Task FindPaged_RespectsSpecification()
{
    // Arrange: 50 todos, 20 completed
    // Act: Get completed todos, page 1, size 10
    // Assert: Should return first 10 completed
}
```

#### Integration Tests
```csharp
[Test]
public async Task API_Pagination_WorksEndToEnd()
{
    // Test full flow from API to database
}
```

---

## 💻 ตัวอย่างโค้ดที่จะใช้

### Backend Files
- `Domain/Models/PagedResult.cs` (new)
- `Domain/Enums/SortOrder.cs` (new)
- `Infrastructure/Repositories/ITodoRepository.cs` (modified)
- `Infrastructure/Repositories/TestTodoRepository.cs` (modified)
- `Infrastructure/Repositories/LiveTodoRepository.cs` (modified)
- `Infrastructure/Capabilities/TodoRepositoryCapability.cs` (modified)
- `Features/Todos/TodoService.cs` (modified)
- `API/Controllers/TodosController.cs` (modified)

### Frontend Files
- `services/todoService.ts` (modified)
- `hooks/useTodosPaginated.ts` (new)
- `components/TodoList.tsx` (modified)
- `components/PaginationBar.tsx` (new)

### Code Statistics
- Backend LOC: ~150 lines
- Frontend LOC: ~100 lines
- Test LOC: ~80 lines

---

## 🧪 แบบฝึกหัด

### ระดับง่าย: ทำความเข้าใจ
1. Offset-based pagination ทำงานอย่างไร?
2. ทำไม pagination ถึงช่วย performance?
3. `TotalPages` คำนวณอย่างไร?

### ระดับกลาง: ลองเขียน
1. เพิ่ม sorting แบบ multi-column (sort by priority, then createdAt)
2. สร้าง `useTodosInfiniteScroll` hook สำหรับ infinite scrolling
3. เพิ่ม loading skeleton ขณะโหลดหน้าใหม่

### ระดับยาก: Challenges
1. Implement cursor-based pagination สำหรับ real-time feed
2. เพิ่ม caching layer เพื่อไม่ให้ re-fetch ทุกครั้งที่เปลี่ยนหน้า
3. Optimize total count query สำหรับ table ที่มีข้อมูลหลักล้านแถว
4. สร้าง virtual scrolling สำหรับแสดงผลข้อมูลหลักหมื่นรายการ

---

## 🔗 เชื่อมโยงกับบทอื่น

**ต้องอ่านก่อน:**
- บทที่ 15: Specification Pattern (ใช้ร่วมกับ pagination)

**อ่านต่อ:**
- บทที่ 17: Transaction Handling
- บทที่ 10: React Integration (สำหรับ frontend patterns)

---

## 📊 สถิติและเวลาอ่าน

- **ระดับความยาก:** ⭐⭐⭐ (กลาง)
- **เวลาอ่าน:** ~70 นาที
- **เวลาลงมือทำ:** ~120 นาที (รวม frontend)
- **จำนวนตัวอย่างโค้ด:** ~20 ตัวอย่าง
- **จำนวนหน้าโดยประมาณ:** ~14 หน้า

---

## 💡 Key Takeaways

หลังจากอ่านบทนี้ คุณจะได้:

1. **Performance** - จัดการ large datasets ได้อย่างมีประสิทธิภาพ
2. **UX** - ผู้ใช้ได้ผลลัพธ์เร็วขึ้น ไม่ต้องรอโหลดทั้งหมด
3. **Scalable** - รองรับข้อมูลที่เติบโตได้
4. **Full-Stack** - ทั้ง backend และ frontend ทำงานร่วมกันได้ดี

---

## 📝 หมายเหตุสำหรับผู้เขียน

- ใช้ตัวอย่างจาก SCALING-PATTERNS.md sections:
  - "Pagination Pattern for Repository Queries"
  - Frontend Integration
  - Performance Considerations
  - Advanced: Cursor-Based Pagination

- เน้น:
  - EF Core query optimization
  - Real-world performance metrics
  - UI/UX best practices
  - แสดง SQL ที่ generate ออกมา

- Diagrams ที่ควรมี:
  - Pagination flow diagram
  - Offset vs. Cursor comparison
  - Performance comparison charts

---

**Status:** 📋 Outline Ready → ⏳ Ready to Write
