# บทที่ 1: ทำไมต้อง Functional Programming?

> "การเขียนโปรแกรมไม่ใช่แค่การทำให้โค้ดทำงาน แต่คือการทำให้โค้ดเข้าใจง่าย บำรุงรักษาง่าย และปรับเปลี่ยนได้ง่าย"

---

## 1.1 บทนำ

คุณเคยเจอปัญหาเหล่านี้ไหม?

- **Bug ที่หาไม่เจอ** - แก้ไขที่หนึ่ง แต่เกิด bug ที่อื่น
- **State ที่คาดเดาไม่ได้** - ไม่รู้ว่าค่าตัวแปรจะเป็นอะไรในแต่ละจุด
- **ทดสอบยาก** - ต้อง mock หลายอย่าง setup ซับซ้อน
- **โค้ดที่เปราะบาง** - เปลี่ยนนิดเดียว ระบบพัง
- **โค้ดที่อ่านยาก** - ต้องตามโฟลว์ว่าทำอะไรบ้าง

หากคุณพบปัญหาเหล่านี้บ่อยๆ **Functional Programming (FP)** อาจเป็นคำตอบที่คุณกำลังมองหา!

---

## 1.2 ปัญหาของ Imperative Programming

### 1.2.1 Mutable State - ภัยเงียบที่ซ่อนอยู่

ลองดูโค้ด Traditional Imperative แบบนี้:

**Backend (C# แบบ Imperative):**

```csharp
public async Task<Todo> GetTodo(int id)
{
    logger.LogInfo($"Getting todo {id}");

    var todo = await ctx.Todos.FindAsync(id);

    if (todo == null)
        throw new NotFoundException();

    logger.LogInfo($"Found todo {id}");

    return todo;
}
```

**ปัญหาที่เจอ:**

1. **Hidden Side Effects** - ฟังก์ชันมี side effects ที่มองไม่เห็น (logging, database access)
2. **Exception Handling** - ต้องจำว่าฟังก์ชันนี้อาจโยน exception
3. **Hard to Test** - ต้อง mock logger, DbContext, และ setup database
4. **Tight Coupling** - ผูกติดกับ infrastructure เฉพาะ (ILogger, DbContext)

**Frontend (TypeScript แบบ Imperative):**

```typescript
const [loading, setLoading] = useState(false);
const [error, setError] = useState(null);
const [data, setData] = useState(null);

const fetchTodos = async () => {
  setLoading(true);
  setError(null);

  try {
    const response = await fetch('/todos');
    const data = await response.json();
    setData(data);
  } catch (err) {
    setError(err);
  } finally {
    setLoading(false);
  }
};
```

**ปัญหาที่เจอ:**

1. **Manual State Management** - ต้องจัดการ state หลายตัว ด้วยมือ
2. **Easy to Forget** - ลืม `setLoading(false)` ใน catch block บ่อยๆ
3. **Inconsistent State** - อาจมีช่วงที่ `loading=false` แต่ `data=null` และ `error=null`
4. **Repeated Code** - ทุก API call ต้องเขียนซ้ำๆ

### 1.2.2 การเปรียบเทียบ: Imperative vs Functional

| ลักษณะ | Imperative | Functional |
|--------|-----------|-----------|
| **State** | Mutable - เปลี่ยนแปลงได้ตลอดเวลา | Immutable - ไม่เปลี่ยนแปลง |
| **Side Effects** | ซ่อนอยู่ในฟังก์ชัน | แยกออกมาชัดเจน (Effects) |
| **การเขียน** | อธิบายว่า "ทำอย่างไร" (How) | อธิบายว่า "ต้องการอะไร" (What) |
| **Error Handling** | Exceptions | Type-safe errors (Either, Fin) |
| **Testing** | ยาก - ต้อง mock หลายอย่าง | ง่าย - Pure functions |
| **Composability** | จำกัด | ยอดเยี่ยม - ต่อกันเหมือน LEGO |

---

## 1.3 ข้อดีของ Functional Programming

### 1.3.1 ตัวอย่าง: แบบ Functional

ตอนนี้มาดูโค้ดแบบ Functional เปรียบเทียบกัน:

**Backend (C# + language-ext v5):**

```csharp
public static K<M, Todo> Get<M, RT>(int id)
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, DatabaseIO>, Has<M, LoggerIO>
{
    return
        from _ in Logger<M, RT>.logInfo("Getting todo by ID: {Id}", id)
        from todo in Database<M, RT>.liftIO((ctx, ct) =>
            ctx.Todos.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id, ct)
                .Map(Optional))
            .Bind(opt => opt.To<M, Todo>(() => Error.New(404, $"Todo with id {id} not found")))
        from __ in Logger<M, RT>.logInfo("Found todo: {TodoTitle}", todo.Title)
        select todo;
}
```

**ข้อดี:**

- **Explicit Effects** - เห็นชัดเจนว่าต้องการ DatabaseIO และ LoggerIO
- **Type-Safe Errors** - คอมไพเลอร์บังคับจัดการ error
- **Composable** - สามารถต่อ operations ต่างๆ ได้อย่างอิสระ
- **Testable** - แทนที่ DatabaseIO, LoggerIO ด้วย test implementations ได้ง่าย
- **Pure Business Logic** - logic ชัดเจน ไม่ปะปนกับ infrastructure

**Frontend (TypeScript + Effect-TS):**

```typescript
export const listTodos = (): App<Todo[]> =>
  Effect.gen(function* (_) {
    const client = yield* _(App.httpClient());
    return yield* _(client.get<Todo[]>('/todos'));
  }).pipe(
    withLogging(
      'Fetching all todos',
      todos => `Fetched ${todos.length} todos`
    )
  );

// ใช้งานใน Component
const { state, refetch } = useAppQuery(env, listTodos(), []);
```

**ข้อดี:**

- **Automatic State Management** - RemoteData pattern จัดการ loading/error/success อัตโนมัติ
- **Reusable Effects** - `withLogging()` ใช้กับ operation ใดก็ได้
- **Type-Safe** - TypeScript รู้ว่า `state.data` คือ `Todo[]` เมื่อ success
- **Declarative** - อ่านง่าย เข้าใจได้ทันที
- **Composable** - สามารถเพิ่ม retry, cache, metrics ได้ง่าย

### 1.3.2 หลักการสำคัญของ Functional Programming

#### 1. **Pure Functions** - ฟังก์ชันบริสุทธิ์

```typescript
// ❌ Impure - มี side effects
let counter = 0;
function increment() {
  counter++;  // เปลี่ยน state ภายนอก
  return counter;
}

// ✅ Pure - ไม่มี side effects
function increment(counter: number): number {
  return counter + 1;  // return ค่าใหม่เสมอ
}
```

**คุณสมบัติของ Pure Function:**
- Input เดียวกัน → Output เดียวกันเสมอ
- ไม่มี side effects
- ไม่เปลี่ยน state ภายนอก

#### 2. **Immutability** - ไม่เปลี่ยนแปลงข้อมูล

```csharp
// ❌ Mutable
var todo = new Todo { Title = "Buy milk" };
todo.Title = "Buy eggs";  // เปลี่ยนค่าตรงๆ

// ✅ Immutable (C# Records)
var todo = new Todo { Title = "Buy milk" };
var updated = todo with { Title = "Buy eggs" };  // สร้าง object ใหม่
```

#### 3. **Composition** - การประกอบฟังก์ชัน

```csharp
// แทนที่จะเขียน:
var todo = GetTodo(id);
var validated = Validate(todo);
var saved = Save(validated);
var logged = Log(saved);

// เขียนแบบ Functional:
var result =
    from todo in GetTodo(id)
    from validated in Validate(todo)
    from saved in Save(validated)
    from _ in Log(saved)
    select saved;
```

#### 4. **Type Safety** - ความปลอดภัยจากระบบ Type

```typescript
// ❌ Runtime Error - ไม่รู้ว่า user อาจเป็น null
async function getUserName(userId: number): Promise<string> {
  const user = await db.users.findById(userId);
  return user.name;  // 💥 Runtime error ถ้า user เป็น null!
}

// ✅ Type-Safe with Option - คอมไพเลอร์บังคับให้ handle
async function getUserName(userId: number): Promise<Option<string>> {
  const user = await db.users.findById(userId);
  return Optional(user).map(u => u.name);
}

// ใช้งาน - คอมไพเลอร์บังคับให้จัดการทุก case
const nameOpt = await getUserName(1);
nameOpt.match({
  some: (name) => console.log(`Hello ${name}`),
  none: () => console.log("User not found")  // ต้องจัดการ case นี้ด้วย!
});
```

**ข้อดีของ Type Safety:**
- คอมไพเลอร์บังคับให้ handle ทุก case (ไม่มี null pointer exception)
- ไม่สามารถเข้าถึง `user.name` โดยตรงถ้า user อาจเป็น null
- Refactor ง่าย - ถ้าเปลี่ยน type คอมไพเลอร์จะบอกว่าต้องแก้ที่ไหนบ้าง

---

## 1.4 Application ที่เราจะสร้างในหนังสือเล่มนี้

### 1.4.1 ภาพรวม Todo Application

เราจะสร้าง **Full-Stack Todo Application** โดยใช้ Functional Programming ทั้งระบบ:

```
┌─────────────────────────────────────────────┐
│     React + TypeScript + Effect-TS          │
│              (Frontend)                      │
│                                             │
│  • Effect system with Context.Tag           │
│  • RemoteData pattern                       │
│  • Type-safe API client                     │
│  • Composable effects                       │
└──────────────┬──────────────────────────────┘
               │ HTTP (JSON)
               │
┌──────────────┴──────────────────────────────┐
│  ASP.NET Core + C# + language-ext v5        │
│               (Backend)                      │
│                                             │
│  • Has<M, RT, T>.ask pattern                │
│  • Trait-based capabilities                 │
│  • Type-safe error handling                 │
│  • Testable architecture                    │
└──────────────┬──────────────────────────────┘
               │
        ┌──────┴───────┐
        │    SQLite    │
        └──────────────┘
```

### 1.4.2 Features ที่จะสร้าง

**Functional Features:**

1. **CRUD Operations** - Create, Read, Update, Delete todos
2. **Toggle Completion** - เปลี่ยนสถานะ completed/incomplete
3. **Validation** - ตรวจสอบข้อมูลแบบ Applicative (รวม errors ทั้งหมด)
4. **Error Handling** - จัดการ errors แบบ type-safe
5. **Logging** - Structured logging ทุก operation
6. **Testing** - Unit tests แบบ trait-based (ไม่ต้องใช้ database จริง!)

**Architecture Patterns:**

- **Backend**: Has<M, RT, T>.ask pattern, Trait-based capabilities
- **Frontend**: Effect system, Context.Tag, RemoteData pattern
- **Testing**: Dictionary/Map-based test implementations
- **Full-Stack**: Parallel functional patterns ทั้ง backend และ frontend

### 1.4.3 Technology Stack

**Backend:**
- ASP.NET Core 8.0 - Web framework
- language-ext v5.0.0-beta-54 - Functional programming library
- Entity Framework Core 9.0 - ORM
- SQLite - Database

**Frontend:**
- React 18 - UI library
- TypeScript 5.2 - Type system
- Effect-TS 3.10 - Effect system
- Vite 5 - Build tool

**Testing:**
- Backend: NUnit + TestDatabaseIO (Dictionary-based)
- Frontend: Vitest + TestHttpClient (Map-based)

### 1.4.4 สิ่งที่คุณจะได้เรียนรู้

หลังจากอ่านหนังสือเล่มนี้จบ คุณจะสามารถ:

**Backend:**
- ใช้ Has<M, RT, T>.ask pattern สร้าง capability-based architecture
- สร้าง Trait interfaces และ implementations
- เขียน generic business logic ที่ไม่ผูกติดกับ infrastructure
- ทำ Applicative validation กับ error accumulation
- Testing แบบไม่ต้องใช้ database จริง

**Frontend:**
- ใช้ Effect-TS สร้าง effect system
- จัดการ dependency injection ด้วย Context.Tag
- ใช้ RemoteData pattern จัดการ loading states
- สร้าง type-safe API client
- Testing แบบไม่ต้องเรียก HTTP จริง

**Full-Stack:**
- เชื่อมต่อ backend และ frontend ด้วย functional patterns
- จัดการ errors แบบ end-to-end
- สร้าง composable effects ที่ reusable
- Architecture ที่ขยายง่าย (multi-entity)

---

## 1.5 เปรียบเทียบ: Before และ After

### Scenario: สร้าง Todo ใหม่

#### ❌ Before (Imperative)

**Backend:**
```csharp
public async Task<IActionResult> CreateTodo(CreateTodoRequest request)
{
    // Manual validation
    if (string.IsNullOrWhiteSpace(request.Title))
        return BadRequest("Title is required");

    if (request.Title.Length > 200)
        return BadRequest("Title too long");

    // Hidden side effects
    logger.LogInformation("Creating todo");

    var todo = new Todo
    {
        Title = request.Title,
        Description = request.Description,
        CreatedAt = DateTime.UtcNow
    };

    // Might throw exceptions
    await ctx.Todos.AddAsync(todo);
    await ctx.SaveChangesAsync();

    logger.LogInformation($"Created todo {todo.Id}");

    return Ok(todo);
}
```

**Frontend:**
```typescript
const [loading, setLoading] = useState(false);
const [error, setError] = useState<string | null>(null);
const [success, setSuccess] = useState(false);

const handleSubmit = async (e) => {
  e.preventDefault();

  // Manual validation
  if (!title) {
    setError("Title is required");
    return;
  }

  setLoading(true);
  setError(null);
  setSuccess(false);

  try {
    const response = await fetch('/todos', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ title, description })
    });

    if (!response.ok) {
      throw new Error('Failed to create todo');
    }

    const todo = await response.json();
    setSuccess(true);
    setTitle('');
    setDescription('');
  } catch (err) {
    setError(err.message);
  } finally {
    setLoading(false);
  }
};
```

**ปัญหา:**
- State management ทำด้วยมือ ซับซ้อน
- Validation กระจัดกระจาย
- Error handling ไม่ consistent
- Logging ทำเอง ทุกที่
- Testing ยาก ต้อง mock หลายอย่าง

#### ✅ After (Functional)

**Backend:**
```csharp
public static K<M, Todo> Create<M, RT>(string title, string description)
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, DatabaseIO>, Has<M, LoggerIO>, Has<M, TimeIO>
{
    return
        from validReq in TodoValidation.Validate(title, description).To<M, CreateTodoRequest>()
        from now in Time<M, RT>.now()
        from todo in M.Pure(new Todo
        {
            Title = validReq.Title,
            Description = validReq.Description,
            CreatedAt = now
        })
        from _ in Logger<M, RT>.logInfo("Creating todo: {Title}", todo.Title)
        from saved in Database<M, RT>.addTodo(todo)
        from __ in Logger<M, RT>.logInfo("Created todo {TodoId}", saved.Id)
        select saved;
}
```

**Frontend:**
```typescript
export const createTodo = (request: CreateTodoRequest): App<Todo> =>
  Effect.gen(function* (_) {
    const client = yield* _(App.httpClient());
    return yield* _(client.post<CreateTodoRequest, Todo>('/todos', request));
  }).pipe(
    withLogging(
      `Creating todo: ${request.title}`,
      todo => `Created todo with ID: ${todo.id}`
    )
  );

// ใช้ใน Component
const { state, execute } = useApp<Todo>(env);

const handleSubmit = async (e: FormEvent) => {
  e.preventDefault();

  const validation = validateTodo({ title, description });

  if (Either.isLeft(validation)) {
    setErrors(validation.left);
    return;
  }

  await execute(createTodo(validation.right));
};
```

**ข้อดี:**
- State management อัตโนมัติ (RemoteData pattern)
- Validation ชัดเจน แยกออกมา (Applicative)
- Error handling type-safe ทั้งหมด
- Logging เป็น composable effect
- Testing ง่าย - แทนที่ capabilities/services ได้

---

## 1.6 สรุป

Functional Programming ไม่ใช่แค่ "แนวคิดใหม่" แต่เป็น **แนวทางที่ช่วยแก้ปัญหาจริง** ในการพัฒนา software:

| ปัญหา | วิธีแก้แบบ FP |
|-------|---------------|
| **State ที่คาดเดาไม่ได้** | Immutability + Pure Functions |
| **Side Effects ที่ซ่อนอยู่** | Explicit Effects (Eff, Effect) |
| **Error Handling ไม่ชัดเจน** | Type-safe errors (Fin, Either) |
| **Testing ยาก** | Trait/Context-based testing |
| **โค้ดที่ซับซ้อน** | Composition + Declarative style |
| **Reusability ต่ำ** | Composable effects |

ในบทถัดไป เราจะเจาะลึกไปที่ **แนวคิดพื้นฐาน Functional Programming** ที่คุณจำเป็นต้องรู้ก่อนเริ่มเขียนโค้ด!

---

## แบบฝึกหัดท้ายบท

1. **คิดวิเคราะห์**: ดูโค้ดที่คุณเขียนล่าสุด มี Imperative patterns ที่ก่อปัญหาอะไรบ้าง?

2. **เปรียบเทียบ**: ลองเขียนฟังก์ชันบวกเลขสองแบบ:
   - แบบ Impure (มี side effects)
   - แบบ Pure (ไม่มี side effects)

3. **อภิปราย**: ทำไม Pure Functions ถึง test ง่ายกว่า Impure Functions?

4. **ลองคิด**: ถ้า state ทั้งหมดเป็น Immutable จะมีผลกับ performance อย่างไร? ข้อดี-ข้อเสียคืออะไร?

---

**หมายเหตุ:** ตัวอย่างโค้ดทั้งหมดในหนังสือนี้จะใช้:
- **C# + language-ext v5** สำหรับ Backend
- **TypeScript + Effect-TS** สำหรับ Frontend
- **SQLite** สำหรับ Database
- **React 18** สำหรับ UI

---

พร้อมไปบทถัดไปกันไหม? ในบทที่ 2 เราจะเจาะลึกแนวคิดพื้นฐาน Functional Programming ที่จำเป็นต้องรู้!
