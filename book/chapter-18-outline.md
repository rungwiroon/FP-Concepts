# บทที่ 18: Functional Programming with AI-Assisted Development

> เขียน FP ได้เร็วขึ้น 10 เท่าด้วย AI - จาก Boilerplate สู่ Business Logic

---

## 🎯 เป้าหมายของบท

หลังจากอ่านบทนี้แล้ว คุณจะสามารถ:
- เข้าใจว่าทำไม FP code ถึงเป็นมิตรกับ AI มากกว่า imperative
- ใช้ AI ลด boilerplate code (type constraints, monads, capabilities)
- เขียน prompts ที่มีประสิทธิภาพสำหรับ FP patterns
- Setup และใช้งาน Claude Code, GitHub Copilot กับ language-ext
- ทดสอบและ review AI-generated FP code
- Apply best practices จากโปรเจคจริง (TodoApp)

---

## 📚 สิ่งที่จะได้เรียนรู้

### 1. Why FP + AI = Perfect Match
- **Type signatures เป็น documentation** - AI อ่านเข้าใจได้ทันที
- **Pure functions = Predictable** - AI generate แม่นยำ
- **Pattern recognition** - FP patterns ซ้อนกันได้ AI จำง่าย
- **No hidden state** - AI ไม่ต้องเดา side effects

### 2. The Boilerplate Problem in FP ⭐
- **Generic constraints ยาว** - `where RT : struct, HasTodoRepo<RT>, HasLogger<RT>, ...`
- **Nested monadic types** - `Eff<RT, Either<Error, Option<Todo>>>`
- **Capability boilerplate** - interface, trait, runtime setup
- **Extension methods** - repetitive patterns
- **Pattern matching** - verbose switch expressions

### 3. AI Solutions for Boilerplate ⭐
- **Code generation** - AI เขียน boilerplate ให้ทั้งหมด
- **Snippet expansion** - แค่พิมพ์ signature AI เติมเต็ม
- **Refactoring** - แปลง imperative → FP อัตโนมัติ
- **Template completion** - AI รู้ pattern แล้วเติมเอง

### 4. Practical Prompting Strategies
- **Prompts for common patterns** (CRUD, validation, error handling)
- **Context building** - บอก AI ให้เข้าใจ architecture
- **Iterative refinement** - improve AI output
- **Anti-patterns to avoid**

### 5. Tools Integration
- **Claude Code** setup and workflows
- **GitHub Copilot** best practices
- **VS Code extensions** for FP
- **Custom snippets** and shortcuts

### 6. Testing AI-Generated Code
- **Property-based testing** - catch AI mistakes
- **Type-driven verification**
- **Manual review checklist**
- **Common AI errors** in FP

---

## 📖 โครงสร้างเนื้อหา

### บทนำ: FP Boilerplate Hell → AI Paradise (5 นาที)

**ปัญหาที่เจอจริง:**
```csharp
// ❌ เขียนมือ - ใช้เวลา 10 นาที สำหรับ function เดียว!
public static Eff<RT, Either<Error, Todo>> updateTodoTitle<RT>(
    int id,
    string newTitle)
    where RT : struct,
        HasTodoRepo<RT>,
        HasLogger<RT>,
        HasCancellationToken<RT>
{
    return
        from logger in default(RT).LoggerEff
        from _ in LoggerIO.logInfo<RT>($"Updating todo {id} title")
        from todoOpt in TodoRepo.getTodoById<RT>(id)
        from todo in todoOpt.ToEff(
            Error.New("TODO_NOT_FOUND", $"Todo {id} not found"))
        from validated in validateTitle(newTitle)
        from updated in pure(() => todo with { Title = validated })
        from __ in TodoRepo.updateTodo<RT>(updated)
        from ___ in UnitOfWork.saveChanges<RT>()
        from ____ in LoggerIO.logInfo<RT>($"Updated todo {id}")
        select updated.ToEither();
}

// 😱 Boilerplate:
// - 4 บรรทัด type constraints
// - 8 บรรทัด monadic binding
// - แค่ business logic จริงๆ: 2 บรรทัด!
```

**AI ช่วยแล้ว - 30 วินาที:**
```csharp
// ✅ พิมพ์แค่นี้:
// updateTodoTitle: (id, newTitle) -> validate -> update -> save

// ✅ AI generate ทั้งหมดให้ พร้อม:
// - Type constraints ครบ
// - Monadic composition ถูกต้อง
// - Error handling
// - Logging
```

---

### ส่วนที่ 1: Why FP Code is AI-Friendly (15 นาที)

#### 1.1 Type Signatures = Machine-Readable Specs

**Imperative (AI งง):**
```csharp
// AI ต้องอ่านทั้ง function เพื่อเข้าใจ
public async Task<object> GetTodo(int id)
{
    try
    {
        var todo = await _repo.FindAsync(id);
        if (todo == null)
            return new { error = "Not found", code = 404 };
        return todo;
    }
    catch (Exception ex)
    {
        return new { error = ex.Message, code = 500 };
    }
}
// AI: "return type คือ object... อาจเป็น Todo, error object, หรืออะไรก็ได้ 🤔"
```

**FP (AI เข้าใจทันที):**
```csharp
// AI แค่อ่าน signature รู้เลยว่าทำอะไร
public static Eff<RT, Either<Error, Todo>> getTodo<RT>(int id)
    where RT : struct, HasTodoRepo<RT>
{
    return from todo in TodoRepo.getTodoById<RT>(id)
           select todo.ToEither(Error.New("TODO_NOT_FOUND"));
}

// AI: "Input: int, Output: Either error or todo, Side effect: database read ✅"
```

#### 1.2 Pure Functions = No State Tracking

**Comparison:**

| Aspect | Imperative | FP |
|--------|------------|-----|
| **State** | Hidden, mutable | Explicit in types |
| **Side effects** | Anywhere | Wrapped in Eff/IO |
| **Predictability** | ⭐⭐ | ⭐⭐⭐⭐⭐ |
| **AI accuracy** | 60-70% | 90-95% |
| **Testability** | Need mocks | Pure function tests |

#### 1.3 Composition Patterns

```csharp
// FP patterns มี structure ชัดเจน - AI จำได้
var spec = TodoSpecs.ByUser(userId)           // Pattern 1: Builder
    .And(TodoSpecs.IsCompleted())              // Pattern 2: Composition
    .Or(TodoSpecs.HighPriority());             // Pattern 3: Boolean algebra

var result = await TodoService
    .findTodos(spec)                           // Pattern 4: Service call
    .Map(todos => todos.Take(10))              // Pattern 5: Functor
    .MapFail(err => err.WithCode("QUERY_ERR")) // Pattern 6: Error handling
    .Run(runtime, ct);                         // Pattern 7: Effect execution

// AI เห็น patterns เหล่านี้ → generate code ใหม่ตาม patterns
```

---

### ส่วนที่ 2: AI-Powered Boilerplate Elimination ⭐ (30 นาที)

#### 2.1 Problem: FP Boilerplate Overhead

**Type Constraint Hell:**
```csharp
// 😱 ต้องพิมพ์ทุกครั้ง!
public static Eff<RT, Seq<Todo>> getAllTodos<RT>()
    where RT : struct,
        HasTodoRepo<RT>,
        HasLogger<RT>,
        HasCancellationToken<RT>

public static Eff<RT, Option<Todo>> getTodoById<RT>(int id)
    where RT : struct,
        HasTodoRepo<RT>,
        HasLogger<RT>,
        HasCancellationToken<RT>

public static Eff<RT, Todo> createTodo<RT>(TodoCreateDto dto)
    where RT : struct,
        HasTodoRepo<RT>,
        HasUnitOfWork<RT>,
        HasLogger<RT>,
        HasCancellationToken<RT>

// Same constraints, copy-paste 10+ times! 😭
```

**Nested Monad Hell:**
```csharp
// 😱 Types ซ้อนกันยาวมาก
Eff<RT, Either<Error, Option<Todo>>>
Eff<RT, Validation<Error, Seq<Todo>>>
Eff<RT, Either<Error, (Todo, List<Comment>)>>

// ทุกครั้งต้องคิดว่าต้อง:
// - unwrap อย่างไร
// - error handling ยังไง
// - compose ยังไง
```

**Capability Boilerplate:**
```csharp
// 😱 สร้าง capability ใหม่ = copy-paste 5 files!

// 1. Interface
public interface ICommentRepository { ... }

// 2. Trait
public interface HasCommentRepo<RT>
    where RT : struct, HasCommentRepo<RT>
{
    Eff<RT, ICommentRepository> CommentRepoEff { get; }
}

// 3. Capability static class
public static class CommentRepo
{
    public static Eff<RT, Comment> getById<RT>(int id)
        where RT : struct, HasCommentRepo<RT>, HasCancellationToken<RT>
    { ... }
}

// 4. Live implementation
public class LiveCommentRepository : ICommentRepository { ... }

// 5. Test implementation
public class TestCommentRepository : ICommentRepository { ... }

// 6. Update runtime
public record LiveRuntime(...) : HasCommentRepo<LiveRuntime> { ... }
```

---

#### 2.2 Solution: AI Code Generation ⭐

**Example 1: Generate Function with Constraints**

**Prompt:**
```
Generate a function to update todo status:
- Input: todo id (int), new status (bool)
- Return: Eff<RT, Either<Error, Todo>>
- Use TodoRepo and UnitOfWork
- Add logging
- Follow language-ext patterns from this codebase
```

**AI Output (30 วินาที):**
```csharp
public static Eff<RT, Either<Error, Todo>> updateTodoStatus<RT>(
    int id,
    bool isCompleted)
    where RT : struct,
        HasTodoRepo<RT>,
        HasUnitOfWork<RT>,
        HasLogger<RT>,
        HasCancellationToken<RT>
{
    return
        from _ in LoggerIO.logInfo<RT>($"Updating todo {id} status to {isCompleted}")
        from todoOpt in TodoRepo.getTodoById<RT>(id)
        from todo in todoOpt.ToEff(
            Error.New("TODO_NOT_FOUND", $"Todo {id} not found"))
        from updated in SuccessEff(todo with { IsCompleted = isCompleted })
        from __ in TodoRepo.updateTodo<RT>(updated)
        from ___ in UnitOfWork.saveChanges<RT>()
        from ____ in LoggerIO.logInfo<RT>(
            $"Todo {id} status updated successfully")
        select updated.ToEither<Error, Todo>();
}
```

**Analysis:**
- ✅ Type constraints ครบถ้วน
- ✅ Monadic composition ถูกต้อง
- ✅ Error handling with Either
- ✅ Logging before/after
- ✅ Immutable update with `with`
- ✅ SaveChanges pattern

---

**Example 2: Generate Entire Capability**

**Prompt:**
```
Create a new capability for CommentRepository with:
- Interface: ICommentRepository
- Methods: GetById, GetByTodoId, Create, Update, Delete
- Trait: HasCommentRepo<RT>
- Static class: CommentRepo with Eff wrappers
- Follow the same pattern as TodoRepo in this codebase
```

**AI Output (1 นาที):**
```csharp
// ===== Infrastructure/Repositories/ICommentRepository.cs =====
public interface ICommentRepository
{
    Task<Option<Comment>> GetByIdAsync(int id, CancellationToken ct);
    Task<List<Comment>> GetByTodoIdAsync(int todoId, CancellationToken ct);
    void AddComment(Comment comment);
    void UpdateComment(Comment comment);
    void DeleteComment(Comment comment);
}

// ===== Infrastructure/Capabilities/HasCommentRepo.cs =====
public interface HasCommentRepo<RT>
    where RT : struct, HasCommentRepo<RT>
{
    Eff<RT, ICommentRepository> CommentRepoEff { get; }
}

// ===== Infrastructure/Capabilities/CommentRepositoryCapability.cs =====
public static class CommentRepo
{
    public static Eff<RT, Option<Comment>> getById<RT>(int id)
        where RT : struct,
            HasCommentRepo<RT>,
            HasCancellationToken<RT>
    {
        return from repo in default(RT).CommentRepoEff
               from ct in CancellationTokenIO.token<RT>()
               from comment in Eff(() => repo.GetByIdAsync(id, ct))
               select comment;
    }

    public static Eff<RT, List<Comment>> getByTodoId<RT>(int todoId)
        where RT : struct,
            HasCommentRepo<RT>,
            HasCancellationToken<RT>
    {
        return from repo in default(RT).CommentRepoEff
               from ct in CancellationTokenIO.token<RT>()
               from comments in Eff(() => repo.GetByTodoIdAsync(todoId, ct))
               select comments;
    }

    // ... other methods
}
```

**Time saved:**
- Manual: ~45 นาที
- With AI: ~2 นาที (including review)
- **Speedup: 22x** 🚀

---

**Example 3: Refactor Imperative → FP**

**Prompt:**
```
Refactor this imperative code to FP style using language-ext:
- Use Eff<RT, T> monad
- Add proper error handling with Either
- Make it pure and testable
- Add type constraints

[paste imperative code]
```

**Input (Imperative):**
```csharp
public async Task<Todo> CreateTodo(TodoCreateDto dto)
{
    if (string.IsNullOrEmpty(dto.Title))
        throw new ArgumentException("Title required");

    var todo = new Todo
    {
        Title = dto.Title,
        IsCompleted = false,
        CreatedAt = DateTime.Now
    };

    _context.Todos.Add(todo);
    await _context.SaveChangesAsync();

    _logger.LogInformation($"Created todo {todo.Id}");
    return todo;
}
```

**AI Output (FP):**
```csharp
public static Eff<RT, Either<Error, Todo>> createTodo<RT>(
    TodoCreateDto dto)
    where RT : struct,
        HasTodoRepo<RT>,
        HasUnitOfWork<RT>,
        HasLogger<RT>
{
    return from validated in validateTodoDto(dto)
           from todo in createTodoEntity(validated)
           from _ in TodoRepo.addTodo<RT>(todo)
           from __ in UnitOfWork.saveChanges<RT>()
           from ___ in LoggerIO.logInfo<RT>($"Created todo {todo.Id}")
           select todo.ToEither<Error, Todo>();
}

private static Either<Error, TodoCreateDto> validateTodoDto(TodoCreateDto dto) =>
    string.IsNullOrEmpty(dto.Title)
        ? Left<Error, TodoCreateDto>(Error.New("INVALID_TITLE", "Title required"))
        : Right<Error, TodoCreateDto>(dto);

private static Either<Error, Todo> createTodoEntity(TodoCreateDto dto) =>
    Right<Error, Todo>(new Todo
    {
        Title = dto.Title,
        IsCompleted = false,
        CreatedAt = DateTime.Now
    });
```

---

#### 2.3 AI Snippet Expansion

**Setup Custom Snippets for AI:**

```json
// .vscode/fp-snippets.json
{
  "eff-function": {
    "prefix": "eff",
    "description": "Eff function with common constraints",
    "body": [
      "public static Eff<RT, ${1:T}> ${2:functionName}<RT>(${3:params})",
      "    where RT : struct,",
      "        HasTodoRepo<RT>,",
      "        HasCancellationToken<RT>",
      "{",
      "    return ${4:// implementation}",
      "}"
    ]
  }
}
```

**Usage:**
1. Type `eff` + Tab
2. AI auto-completes based on context
3. Fill in specifics

---

#### 2.4 Pattern Templates

**Common FP Patterns - Let AI Handle Them:**

**Pattern 1: CRUD with Error Handling**
```csharp
// Template prompt:
// "Generate CRUD for Entity with Either<Error, T> pattern"

// AI generates all 5 functions:
// - create
// - getById
// - update
// - delete
// - getAll
```

**Pattern 2: Validation Pipeline**
```csharp
// Template prompt:
// "Generate validation pipeline with Validation<Error, T>"

// AI generates:
// - Multiple validation functions
// - Applicative composition
// - Error accumulation
```

**Pattern 3: Specification Composition**
```csharp
// Template prompt:
// "Generate specification builders for Entity filters"

// AI generates:
// - Pure builder functions
// - And/Or compositions
// - Common filter combinations
```

---

### ส่วนที่ 3: Practical Prompting Strategies (25 นาที)

#### 3.1 Anatomy of a Good FP Prompt

**Bad Prompt:**
```
Create a function to get todos
```

**Good Prompt:** ⭐
```
Create a function to get todos with these requirements:

**Context:**
- Using language-ext v5
- Follow Has<RT> pattern from existing TodoService
- Entity: Todo (int Id, string Title, bool IsCompleted)

**Function Spec:**
- Name: getCompletedTodosByUser
- Input: userId (int), days (int) - last N days
- Output: Eff<RT, Seq<Todo>>
- Constraints: HasTodoRepo<RT>, HasCancellationToken<RT>

**Logic:**
- Use specification pattern for filtering
- Filter by: userId, isCompleted=true, createdAt > now-days
- Use TodoSpecs.ByUser, TodoSpecs.IsCompleted, TodoSpecs.CreatedAfter
- Return empty Seq if no results (not error)

**Style:**
- LINQ query syntax for monadic composition
- Use language-ext Seq not List
- Follow existing code conventions
```

**Why Better:**
- ✅ Provides context (library, patterns)
- ✅ Clear inputs/outputs
- ✅ Specific type constraints
- ✅ Business logic detail
- ✅ Style preferences

---

#### 3.2 Prompt Templates by Task

**Task 1: Create New Service Function**
```
Create a ${function_name} function:

Context:
- Entity: ${entity_type}
- Service: ${service_name}
- Dependencies: ${capabilities}

Signature:
- Input: ${input_params}
- Output: Eff<RT, ${return_type}>
- Constraints: ${RT_constraints}

Logic:
${business_logic_description}

Error Handling:
- Use Either<Error, T> for: ${error_cases}
- Use Option<T> for: ${optional_cases}
- Error codes: ${error_codes}

Patterns:
- Use ${pattern_names}
- Follow ${existing_code_ref}
```

**Task 2: Add Validation**
```
Add validation to ${dto_name}:

Rules:
- ${field_name}: ${validation_rules}
- ${field_name}: ${validation_rules}

Output:
- Type: Validation<Error, ${dto_name}>
- Accumulate all errors
- Use Applicative composition
- Error format: Error.New(code, message)
```

**Task 3: Refactor to FP**
```
Refactor this code to FP style:

[paste code]

Requirements:
- Use Eff<RT, T> for side effects
- Replace exceptions with Either<Error, T>
- Make all functions pure
- Extract impure parts to capabilities
- Add proper type constraints
- Use LINQ query syntax
```

---

#### 3.3 Context Building for AI

**Strategy 1: Show Examples**
```
Here's an existing function in our codebase:

[paste well-written FP code]

Now create a similar function for Comments instead of Todos.
Follow the same patterns, naming conventions, and style.
```

**Strategy 2: Reference Architecture**
```
Our architecture uses:
- Has<RT> pattern for dependency injection
- Eff<RT, T> for effects
- Either<Error, T> for errors
- Option<T> for nullable
- Specification pattern for queries
- Pure functions in static classes

Create a function that follows these patterns...
```

**Strategy 3: Incremental Refinement**
```
# Round 1:
"Create basic CRUD functions for Comments"

[AI generates basic version]

# Round 2:
"Add validation using Validation<Error, T> monad"

[AI adds validation]

# Round 3:
"Add logging before/after each operation"

[AI adds logging]

# Round 4:
"Wrap everything in Either for error handling"

[AI adds Either]
```

---

### ส่วนที่ 4: Tool Integration (20 นาที)

#### 4.1 Claude Code Setup & Workflows

**Best Practices:**

**Workflow 1: Generate New Feature**
```bash
# 1. Start conversation with context
"I'm working on TodoApp using language-ext v5 and Has pattern.
I need to add a Comments feature.

Current architecture:
- TodoService uses Eff<RT, T>
- TodoRepo uses specifications
- Testing with trait-based mocks

Show me the plan for adding Comments."

# 2. Claude shows plan
# 3. Approve and generate code step-by-step

# 4. Review and integrate
```

**Workflow 2: Refactor Existing Code**
```bash
# 1. Share file content
"Here's my current TodoService implementation:
[paste code]

I want to:
- Add validation using Validation monad
- Improve error handling with Either
- Add logging

Refactor step by step, explain each change."
```

**Workflow 3: Debug & Fix**
```bash
# 1. Share error
"Getting this compilation error:
[paste error]

In this code:
[paste code]

How do I fix it while maintaining FP style?"
```

---

#### 4.2 GitHub Copilot Best Practices

**Configuration:**
```json
// .vscode/settings.json
{
  "github.copilot.enable": {
    "*": true,
    "csharp": true
  },
  "github.copilot.advanced": {
    "inlineSuggestCount": 3,
    "listCount": 10
  }
}
```

**Copilot Patterns:**

**Pattern 1: Function Signature → Implementation**
```csharp
// ✅ Type signature first (Copilot learns from this)
public static Eff<RT, Either<Error, Todo>> updateTodoTitle<RT>(
    int id,
    string newTitle)
    where RT : struct,
        HasTodoRepo<RT>,
        HasUnitOfWork<RT>,
        HasCancellationToken<RT>
{
    // Press Enter - Copilot suggests full implementation!
}
```

**Pattern 2: Comment-Driven Development**
```csharp
// ✅ Write detailed comments, Copilot generates code
public static class TodoService
{
    // Get todo by id
    // Return Either with TODO_NOT_FOUND error if not found
    // Add logging before and after
    // Use TodoRepo and Logger capabilities
    // [Copilot generates full function]
}
```

**Pattern 3: Example-Based Learning**
```csharp
// ✅ Show 1-2 examples, Copilot generates rest
public static Eff<RT, Seq<Todo>> getAll<RT>()
    where RT : struct, HasTodoRepo<RT>, HasCancellationToken<RT>
{
    return TodoRepo.getAllTodos<RT>();
}

public static Eff<RT, Option<Todo>> getById<RT>(int id)
    where RT : struct, HasTodoRepo<RT>, HasCancellationToken<RT>
{
    return TodoRepo.getTodoById<RT>(id);
}

// Type next function signature, Copilot completes pattern:
public static Eff<RT, Todo> create<RT>(TodoCreateDto dto)
    // Copilot suggests full implementation!
```

---

#### 4.3 VS Code Extensions for FP

**Recommended Extensions:**
1. **C# Dev Kit** - IntelliSense for language-ext
2. **GitHub Copilot** - AI code completion
3. **Copilot Chat** - Conversational coding
4. **Error Lens** - Inline error display
5. **Snippets** - Custom FP snippets

**Custom Snippets:**
```json
{
  "Eff Function": {
    "prefix": "effrt",
    "body": [
      "public static Eff<RT, ${1:T}> ${2:name}<RT>(${3:params})",
      "    where RT : struct,",
      "        Has${4:Capability}<RT>,",
      "        HasCancellationToken<RT>",
      "{",
      "    return from ${5:x} in ${6:capability}.${7:method}<RT>(${8:args})",
      "           select ${9:result};",
      "}"
    ]
  }
}
```

---

### ส่วนที่ 5: Testing AI-Generated Code (20 นาที)

#### 5.1 Verification Checklist

**Type Safety Check:**
```csharp
// ✅ Verify:
// 1. All generic constraints present
// 2. No 'var' for public return types
// 3. Option<T> for nullable, not null
// 4. Either<Error, T> for fallible operations
// 5. Validation<Error, T> for multiple errors

// ❌ AI mistake example:
public static async Task<Todo> GetTodo(int id) // Wrong! No Eff
{
    var todo = await _repo.GetAsync(id); // Wrong! var + async/await
    if (todo == null) throw new Exception(); // Wrong! Exception
    return todo;
}

// ✅ Corrected:
public static Eff<RT, Either<Error, Todo>> getTodo<RT>(int id)
    where RT : struct, HasTodoRepo<RT>
{
    return from todoOpt in TodoRepo.getTodoById<RT>(id)
           select todoOpt.ToEither(Error.New("NOT_FOUND"));
}
```

**Purity Check:**
```csharp
// ❌ AI sometimes generates impure code
public static Todo CreateTodo(string title)
{
    var todo = new Todo
    {
        Id = _counter++,  // ❌ Mutation!
        Title = title,
        CreatedAt = DateTime.Now  // ❌ Non-deterministic!
    };
    return todo;
}

// ✅ Pure version:
public static Either<Error, Todo> createTodo(string title, int id, DateTime createdAt)
{
    return string.IsNullOrEmpty(title)
        ? Left<Error, Todo>(Error.New("INVALID_TITLE"))
        : Right<Error, Todo>(new Todo(id, title, false, createdAt));
}
```

---

#### 5.2 Property-Based Testing

```csharp
// Test AI-generated pure functions with properties
[Property]
public Property TodoSpecs_Composition_Associative(
    Todo todo,
    Specification<Todo> spec1,
    Specification<Todo> spec2,
    Specification<Todo> spec3)
{
    // (a AND b) AND c = a AND (b AND c)
    var left = spec1.And(spec2).And(spec3);
    var right = spec1.And(spec2.And(spec3));

    return (left.IsSatisfiedBy(todo) == right.IsSatisfiedBy(todo))
        .ToProperty();
}

// Catch AI mistakes in composition logic!
```

---

#### 5.3 Common AI Errors in FP

**Error 1: Mixing Async/Await with Eff**
```csharp
// ❌ AI often does this:
public static Eff<RT, Todo> getTodo<RT>(int id)
{
    var todo = await TodoRepo.getTodoById<RT>(id); // Wrong!
    return todo;
}

// ✅ Correct:
public static Eff<RT, Option<Todo>> getTodo<RT>(int id)
    where RT : struct, HasTodoRepo<RT>
{
    return TodoRepo.getTodoById<RT>(id); // Return Eff directly
}
```

**Error 2: Forgetting Constraints**
```csharp
// ❌ AI misses constraints:
public static Eff<RT, Todo> updateTodo<RT>(Todo todo)
{
    return TodoRepo.updateTodo<RT>(todo); // Compile error!
}

// ✅ Add all needed constraints:
public static Eff<RT, Todo> updateTodo<RT>(Todo todo)
    where RT : struct,
        HasTodoRepo<RT>,  // ← AI forgot this
        HasCancellationToken<RT>  // ← and this
{
    return TodoRepo.updateTodo<RT>(todo);
}
```

**Error 3: Exception instead of Either**
```csharp
// ❌ AI uses exceptions:
public static Todo ValidateTodo(Todo todo)
{
    if (string.IsNullOrEmpty(todo.Title))
        throw new ArgumentException("Title required"); // Wrong!
    return todo;
}

// ✅ Use Either:
public static Either<Error, Todo> validateTodo(Todo todo)
{
    return string.IsNullOrEmpty(todo.Title)
        ? Left<Error, Todo>(Error.New("INVALID_TITLE", "Title required"))
        : Right<Error, Todo>(todo);
}
```

**Error 4: Using Non-Existent API** ⭐
```csharp
// ❌ AI invents APIs that don't exist in language-ext:
public static Eff<RT, Option<Todo>> getTodo<RT>(int id)
    where RT : struct, HasTodoRepo<RT>, HasCancellationToken<RT>
{
    return from repo in default(RT).TodoRepoEff
           from ct in CancellationTokenIO.token<RT>()
           from todo in EffMaybe(repo.GetByIdAsync(id, ct))  // ❌ EffMaybe doesn't exist!
           select todo;
}

// ✅ Correct: Use Eff with lambda
public static Eff<RT, Option<Todo>> getTodo<RT>(int id)
    where RT : struct, HasTodoRepo<RT>, HasCancellationToken<RT>
{
    return from repo in default(RT).TodoRepoEff
           from ct in CancellationTokenIO.token<RT>()
           from todo in Eff(() => repo.GetByIdAsync(id, ct))  // ✅ Correct!
           select todo;
}

// ✅ Or with explicit async:
from todo in Eff(async () => await repo.GetByIdAsync(id, ct))
```

**Common Non-Existent APIs AI Invents:**
- ❌ `EffMaybe` - doesn't exist, use `Eff(() => ...)`
- ❌ `EffAsync` - doesn't exist, use `Eff(() => ...)`
- ❌ `ToEff()` extension - doesn't exist, use `Eff(() => ...)`
- ❌ `LiftEff` (wrong casing) - correct is `liftEff`

**Why this happens:**
- AI mixes APIs from different FP libraries
- AI extrapolates patterns that "should" exist
- AI invents names that sound plausible

**How to catch:**
1. ✅ Check intellisense - if method doesn't exist, it's wrong
2. ✅ Refer to language-ext docs/source
3. ✅ Compile and test - these errors show immediately

---

### ส่วนที่ 6: Real-World Examples from TodoApp (30 นาที)

#### 6.1 Example: Generate Specification Builders

**Prompt:**
```
Generate pure specification builders for Todo entity:

Entity fields:
- Id: int
- Title: string
- IsCompleted: bool
- UserId: int
- CreatedAt: DateTime

Create builders for:
- ByUser(userId)
- IsCompleted() / IsIncomplete()
- CreatedAfter(date) / CreatedBefore(date)
- TitleContains(text)
- Composed: CompletedByUser, RecentAndIncomplete

Use static class TodoSpecs.
Return Specification<Todo>.
Use Spec.Create helper.
```

**AI Output:**
```csharp
public static class TodoSpecs
{
    // Basic filters
    public static Specification<Todo> ByUser(int userId) =>
        Spec.Create<Todo>(t => t.UserId == userId);

    public static Specification<Todo> IsCompleted() =>
        Spec.Create<Todo>(t => t.IsCompleted);

    public static Specification<Todo> IsIncomplete() =>
        Spec.Create<Todo>(t => !t.IsCompleted);

    public static Specification<Todo> CreatedAfter(DateTime date) =>
        Spec.Create<Todo>(t => t.CreatedAt > date);

    public static Specification<Todo> CreatedBefore(DateTime date) =>
        Spec.Create<Todo>(t => t.CreatedAt < date);

    public static Specification<Todo> TitleContains(string text) =>
        Spec.Create<Todo>(t =>
            t.Title.Contains(text, StringComparison.OrdinalIgnoreCase));

    // Composed filters
    public static Specification<Todo> CompletedByUser(int userId) =>
        IsCompleted().And(ByUser(userId));

    public static Specification<Todo> RecentAndIncomplete(int days) =>
        IsIncomplete().And(CreatedAfter(DateTime.Now.AddDays(-days)));

    public static Specification<Todo> RecentCompletedByUser(
        int userId,
        int days) =>
        CompletedByUser(userId).And(CreatedAfter(
            DateTime.Now.AddDays(-days)));
}
```

**Time:** 2 minutes (vs 20 minutes manual)

---

#### 6.2 Example: Generate Complete CRUD Service

**Prompt:**
```
Generate complete CRUD service for Comment entity:

Entity: Comment
- Id: int
- TodoId: int (foreign key)
- UserId: int
- Text: string
- CreatedAt: DateTime

Generate these functions in CommentService:
1. create(dto) -> Eff<RT, Either<Error, Comment>>
   - Validate: text not empty, userId > 0, todoId exists
   - Use Validation monad
   - Save with UnitOfWork

2. getById(id) -> Eff<RT, Option<Comment>>
   - Return None if not found

3. getByTodoId(todoId) -> Eff<RT, Seq<Comment>>
   - Return all comments for a todo
   - Order by CreatedAt desc

4. update(id, text) -> Eff<RT, Either<Error, Comment>>
   - Validate text not empty
   - Return error if not found
   - Save changes

5. delete(id) -> Eff<RT, Either<Error, Unit>>
   - Return error if not found
   - Hard delete

Constraints:
- Use HasCommentRepo<RT>
- Use HasTodoRepo<RT> for validation
- Use HasUnitOfWork<RT>
- Use HasLogger<RT>
- Follow TodoService patterns
```

**AI Output:** (abbreviated)
```csharp
public static class CommentService
{
    public static Eff<RT, Either<Error, Comment>> create<RT>(
        CommentCreateDto dto)
        where RT : struct,
            HasCommentRepo<RT>,
            HasTodoRepo<RT>,
            HasUnitOfWork<RT>,
            HasLogger<RT>
    {
        return from validated in validateCreateDto(dto).ToEff()
               from todoExists in verifyTodoExists<RT>(dto.TodoId)
               from comment in createCommentEntity(validated)
               from _ in CommentRepo.add<RT>(comment)
               from __ in UnitOfWork.saveChanges<RT>()
               from ___ in LoggerIO.logInfo<RT>(
                   $"Created comment {comment.Id}")
               select comment.ToEither<Error, Comment>();
    }

    private static Validation<Error, CommentCreateDto> validateCreateDto(
        CommentCreateDto dto) =>
        (ValidateText(dto.Text), ValidateUserId(dto.UserId))
            .Apply((text, userId) => dto);

    private static Validation<Error, string> ValidateText(string text) =>
        string.IsNullOrWhiteSpace(text)
            ? Fail<Error, string>(Error.New(
                "INVALID_TEXT", "Comment text cannot be empty"))
            : Success<Error, string>(text);

    // ... rest of implementation
}
```

**Statistics:**
- Functions generated: 5
- Lines of code: ~250
- Time with AI: ~5 minutes
- Time manual: ~60 minutes
- **Speedup: 12x**

---

#### 6.3 Example: Refactor Validation to Applicative

**Before (Sequential):**
```csharp
public static Either<Error, TodoCreateDto> validateDto(TodoCreateDto dto)
{
    if (string.IsNullOrEmpty(dto.Title))
        return Left(Error.New("INVALID_TITLE"));

    if (dto.Title.Length > 200)
        return Left(Error.New("TITLE_TOO_LONG"));

    if (dto.UserId <= 0)
        return Left(Error.New("INVALID_USER"));

    return Right(dto);
}
// Problem: Only returns FIRST error ❌
```

**Prompt:**
```
Refactor this validation to use Validation<Error, T> monad
with applicative composition.

Requirements:
- Accumulate ALL errors (not just first)
- Use language-ext Validation
- Each field validated separately
- Combine with Apply/SelectMany
```

**AI Output (Applicative):**
```csharp
public static Validation<Error, TodoCreateDto> validateDto(TodoCreateDto dto)
{
    return (
        ValidateTitle(dto.Title),
        ValidateTitleLength(dto.Title),
        ValidateUserId(dto.UserId)
    ).Apply((title, _, userId) => dto);
}

private static Validation<Error, string> ValidateTitle(string title) =>
    string.IsNullOrWhiteSpace(title)
        ? Fail<Error, string>(Error.New(
            "INVALID_TITLE", "Title is required"))
        : Success<Error, string>(title);

private static Validation<Error, string> ValidateTitleLength(string title) =>
    title?.Length > 200
        ? Fail<Error, string>(Error.New(
            "TITLE_TOO_LONG", "Title max 200 characters"))
        : Success<Error, string>(title ?? "");

private static Validation<Error, int> ValidateUserId(int userId) =>
    userId <= 0
        ? Fail<Error, int>(Error.New(
            "INVALID_USER", "UserId must be positive"))
        : Success<Error, int>(userId);

// Usage:
var result = validateDto(dto);
result.Match(
    Succ: validDto => Create(validDto),
    Fail: errors => ShowErrors(errors) // ✅ Shows ALL errors!
);
```

---

#### 6.4 Example: Generate Test Cases

**Prompt:**
```
Generate comprehensive test cases for TodoService.createTodo:

Function signature:
public static Eff<RT, Either<Error, Todo>> createTodo<RT>(
    TodoCreateDto dto)

Test scenarios:
1. Valid input - should create todo
2. Empty title - should fail with INVALID_TITLE
3. Title too long - should fail with TITLE_TOO_LONG
4. Invalid userId - should fail with INVALID_USER
5. Database error - should fail gracefully
6. Multiple validation errors - should accumulate

Use NUnit, FluentAssertions, and TestRuntime.
```

**AI Output:**
```csharp
public class TodoServiceTests
{
    private TestRuntime _runtime;

    [SetUp]
    public void Setup()
    {
        _runtime = new TestRuntime();
    }

    [Test]
    public async Task CreateTodo_ValidInput_Success()
    {
        // Arrange
        var dto = new TodoCreateDto("Valid Title", 1);

        // Act
        var result = await TodoService
            .createTodo(dto)
            .Run(_runtime, CancellationToken.None);

        // Assert
        result.IsRight.Should().BeTrue();
        result.IfRight(todo =>
        {
            todo.Title.Should().Be("Valid Title");
            todo.UserId.Should().Be(1);
            todo.IsCompleted.Should().BeFalse();
        });
    }

    [Test]
    public async Task CreateTodo_EmptyTitle_FailsValidation()
    {
        // Arrange
        var dto = new TodoCreateDto("", 1);

        // Act
        var result = await TodoService
            .createTodo(dto)
            .Run(_runtime, CancellationToken.None);

        // Assert
        result.IsLeft.Should().BeTrue();
        result.IfLeft(errors =>
        {
            errors.Should().Contain(e => e.Code == "INVALID_TITLE");
        });
    }

    // ... more test cases
}
```

---

### ส่วนที่ 7: Advanced Tips & Tricks (15 นาที)

#### 7.1 Multi-Step Generation

**Strategy: Break complex tasks into steps**

```
Step 1: "Generate domain model for Comment entity"
→ AI creates: Comment record, DTO classes

Step 2: "Generate repository interface for Comment"
→ AI creates: ICommentRepository with CRUD methods

Step 3: "Generate capability trait and static class"
→ AI creates: HasCommentRepo<RT> and CommentRepo

Step 4: "Generate Live and Test implementations"
→ AI creates: LiveCommentRepository, TestCommentRepository

Step 5: "Update LiveRuntime to include Comment capability"
→ AI updates: LiveRuntime record

Step 6: "Generate CommentService with full CRUD"
→ AI creates: CommentService static class

Step 7: "Generate API controller for Comments"
→ AI creates: CommentsController
```

**Benefit:** Better quality, easier to review each step

---

#### 7.2 Context Files

**Create a context file for AI:**

```markdown
// .ai/context.md

# TodoApp Architecture

## Tech Stack
- C# 12
- language-ext v5.0.0-beta-54
- ASP.NET Core 8.0
- Entity Framework Core 9.0

## Patterns
- Has<M, RT, T>.ask pattern for dependency injection
- Eff<RT, T> monad for effects
- Either<Error, T> for error handling
- Option<T> for nullable values
- Specification pattern for queries
- Unit of Work for transactions

## Naming Conventions
- Functions: camelCase (createTodo, getTodoById)
- Classes: PascalCase
- Capabilities: PascalCase + Eff suffix
- Errors: UPPER_SNAKE_CASE codes

## File Structure
- Domain/ - entities, specs, validations
- Infrastructure/ - repos, capabilities, runtime
- Features/ - service classes
- API/ - controllers

## Example Function
[paste well-written example]
```

**Usage:** Reference in prompts:
```
"Following the patterns in .ai/context.md, create..."
```

---

#### 7.3 AI Code Review

**Prompt for Review:**
```
Review this FP code for:
1. Type safety issues
2. Purity violations
3. Missing error handling
4. Performance problems
5. Style inconsistencies with language-ext

[paste code]

Provide specific fixes for each issue.
```

---

### ส่วนที่ 8: Summary & Best Practices (10 นาที)

#### Key Takeaways

**1. FP + AI = 10x Productivity**
- Boilerplate elimination
- Pattern recognition
- Type-driven generation
- Refactoring automation

**2. Write Better Prompts**
- Provide context (library, patterns, architecture)
- Be specific (types, constraints, error handling)
- Show examples from codebase
- Iterate and refine

**3. Trust but Verify**
- AI makes mistakes (especially with advanced FP)
- Always review generated code
- Test thoroughly
- Check type safety, purity, error handling

**4. Build Your Toolkit**
- Claude Code for complex tasks
- Copilot for completions
- Custom snippets for patterns
- Context files for consistency

**5. Continuous Learning**
- AI learns from your feedback
- Improve prompts over time
- Build personal prompt library
- Share patterns with team

---

## 💻 Tools & Resources

### Tools
- **Claude Code** - https://claude.ai/code
- **GitHub Copilot** - https://copilot.github.com
- **VS Code Extensions:**
  - C# Dev Kit
  - Copilot Chat
  - Error Lens
  - Snippet Generator

### Prompt Libraries
- Common FP patterns
- CRUD generation
- Validation templates
- Testing scaffolds

### Context Templates
- Architecture overview
- Pattern guide
- Code conventions
- Example snippets

---

## 🧪 แบบฝึกหัด

### ระดับง่าย: ทดลองใช้ AI
1. Generate basic CRUD function with AI
2. Refactor imperative code to FP using AI
3. Create specification builders with AI prompt
4. Generate test cases for existing function

### ระดับกลาง: Optimize Workflow
1. Create custom snippets for FP patterns
2. Build context file for your project
3. Generate entire capability (interface → impl → tests)
4. Refactor validation to Validation monad with AI

### ระดับยาก: Advanced Integration
1. Multi-step feature generation (domain → service → API → tests)
2. Property-based test generation
3. Performance optimization with AI suggestions
4. Architecture refactoring (e.g., add new capability layer)
5. Generate documentation from code with AI

---

## 🔗 เชื่อมโยงกับบทอื่น

**ต้องอ่านก่อน:**
- บทที่ 2-3: FP Fundamentals, language-ext
- บทที่ 4: Has<M, RT, T> Pattern
- บทที่ 5-7: Backend Development
- บทที่ 15-17: Advanced Patterns

**Apply ทั้งหมดที่เรียนมา:**
- ใช้ AI เพื่อ implement patterns จากบทก่อนๆ
- Generate code ตาม architecture ที่ออกแบบไว้
- Accelerate development ด้วย AI

---

## 📊 สถิติและเวลาอ่าน

- **ระดับความยาก:** ⭐⭐⭐ (กลาง - ใช้งานได้ทันที)
- **เวลาอ่าน:** ~100 นาที
- **เวลาลงมือทำ:** ~60 นาที (hands-on with AI)
- **จำนวนตัวอย่างโค้ด:** ~30 ตัวอย่าง
- **Productivity gain:** 5-10x speedup ⚡
- **จำนวนหน้าโดยประมาณ:** ~20 หน้า

---

## 💡 Key Takeaways

หลังจากอ่านบทนี้ คุณจะได้:

1. **Understand FP+AI Synergy** - ทำไม FP ถึงเหมาะกับ AI generation
2. **Eliminate Boilerplate** - ลด type constraints, monad nesting ด้วย AI
3. **Master Prompting** - เขียน prompts ที่ได้ FP code คุณภาพสูง
4. **Tool Proficiency** - ใช้ Claude Code, Copilot ได้อย่างเชี่ยวชาญ
5. **Quality Assurance** - รู้จัก verify และ test AI-generated code
6. **Real-World Speed** - เขียน FP เร็วขึ้น 5-10 เท่า
7. **Production Ready** - Code ที่ AI generate พร้อมใช้งานจริง

---

## 📝 หมายเหตุสำหรับผู้เขียน

**Content Sources:**
- Real examples from TodoApp codebase
- Actual prompts that work with Claude/Copilot
- Measured productivity gains
- Common AI mistakes observed

**Emphasis:**
- **Practical over theoretical** - ใช้งานได้จริง
- **Show before/after** - เห็นความแตกต่างชัด
- **Specific prompts** - copy-paste ได้เลย
- **Real metrics** - time saved, LOC generated
- **Boilerplate focus** ⭐ - ปัญหาหลักของ FP คือ verbose

**Tone:**
- Exciting - AI เป็น game-changer!
- Practical - เน้นใช้งานจริง
- Balanced - AI ไม่ perfect, ต้อง review
- Encouraging - ใครๆ ก็ทำได้

**Diagrams:**
- Prompt → AI → Code → Review flow
- Productivity comparison (manual vs AI)
- Boilerplate reduction visualization
- Common AI errors flowchart

**Unique Selling Points:**
- ✅ First book to cover FP + AI coding
- ✅ Real boilerplate solutions
- ✅ Copy-paste prompts
- ✅ TodoApp examples throughout
- ✅ Measured productivity gains

---

**Status:** 📋 Outline Ready → ⏳ Ready to Write (UNIQUE CHAPTER! 🚀)
