# บทที่ 7: Testing Functional Code

> Testing ง่ายขึ้นด้วย Pure Functions และ Dependency Injection

---

## เนื้อหาในบทนี้

- ทำไม Functional Code Test ง่ายกว่า
- Testing Pure Functions
- Testing Effects with Test Runtime
- Mocking Capabilities
- Property-Based Testing
- Testing Validation Logic
- Testing Error Cases
- Integration Testing
- Best Practices
- แบบฝึกหัด

---

## 7.1 ทำไม Functional Code Test ง่ายกว่า?

### 7.1.1 Pure Functions = Deterministic

**Pure functions** return ค่าเดียวกันเสมอสำหรับ input เดียวกัน → **Deterministic** → **Easy to test**

**❌ Imperative Code - ยาก:**

```csharp
// ❌ Hard to test - depends on external state
public class OrderService
{
    private readonly IDatabase _db;
    private readonly IEmailService _email;
    private readonly DateTime _currentTime = DateTime.UtcNow;  // ← Non-deterministic!

    public void ProcessOrder(int orderId)
    {
        var order = _db.GetOrder(orderId);

        if (DateTime.UtcNow - order.CreatedAt > TimeSpan.FromDays(7))  // ← Hard to test!
        {
            _email.Send(order.CustomerEmail, "Order expired");
            _db.UpdateOrderStatus(orderId, "Expired");
        }
    }
}

// Test?
[Test]
public void Should_Expire_Old_Orders()
{
    // ❌ ปัญหา:
    // 1. Mock database
    // 2. Mock email service
    // 3. DateTime.UtcNow ไม่ control ได้
    // 4. ต้อง setup state
    // 5. Verify side effects
}
```

**✅ Functional Code - ง่าย:**

```csharp
// C# language-ext - Pure functions
using LanguageExt;

// ✅ Easy to test - pure function
public static bool IsOrderExpired(DateTime orderCreatedAt, DateTime currentTime)
{
    return currentTime - orderCreatedAt > TimeSpan.FromDays(7);
}

// ✅ Easy to test - no side effects
public static OrderStatus DetermineOrderStatus(Order order, DateTime currentTime)
{
    return IsOrderExpired(order.CreatedAt, currentTime)
        ? OrderStatus.Expired
        : OrderStatus.Active;
}

// Test - ง่ายมาก!
[Test]
public void Should_Mark_Old_Orders_As_Expired()
{
    // Arrange
    var orderCreatedAt = new DateTime(2024, 1, 1);
    var currentTime = new DateTime(2024, 1, 10);  // 9 days later

    // Act
    var isExpired = IsOrderExpired(orderCreatedAt, currentTime);

    // Assert
    Assert.That(isExpired, Is.True);

    // ✅ ไม่ต้อง mock อะไรเลย!
    // ✅ Deterministic
    // ✅ Fast
}
```

### 7.1.2 Immutability = No Hidden State

**Immutable data** ไม่มี hidden state → **ไม่มี side effects** → **Easy to test**

**❌ Mutable - ยาก:**

```csharp
// ❌ Mutable state - hard to test
public class ShoppingCart
{
    private List<CartItem> _items = new();

    public void AddItem(CartItem item)
    {
        var existing = _items.FirstOrDefault(i => i.ProductId == item.ProductId);
        if (existing != null)
            existing.Quantity += item.Quantity;  // ← Mutation!
        else
            _items.Add(item);
    }

    public decimal GetTotal()
    {
        return _items.Sum(i => i.Price * i.Quantity);
    }
}

// Test - ต้อง track state
[Test]
public void Should_Calculate_Total()
{
    var cart = new ShoppingCart();
    cart.AddItem(new CartItem { ProductId = 1, Price = 100, Quantity = 2 });
    cart.AddItem(new CartItem { ProductId = 2, Price = 50, Quantity = 1 });
    // ← State เปลี่ยนไปแล้ว, ต้อง trust AddItem ทำงานถูก

    var total = cart.GetTotal();
    Assert.That(total, Is.EqualTo(250));
}
```

**✅ Immutable - ง่าย:**

```csharp
// C# - Immutable data
public record CartItem(int ProductId, decimal Price, int Quantity);

public static class ShoppingCart
{
    // ✅ Pure function - returns new list
    public static List<CartItem> AddItem(List<CartItem> cart, CartItem newItem)
    {
        var existing = cart.FirstOrDefault(i => i.ProductId == newItem.ProductId);

        return existing != null
            ? cart.Select(i => i.ProductId == newItem.ProductId
                ? i with { Quantity = i.Quantity + newItem.Quantity }
                : i).ToList()
            : cart.Append(newItem).ToList();
    }

    // ✅ Pure function
    public static decimal GetTotal(List<CartItem> cart)
    {
        return cart.Sum(i => i.Price * i.Quantity);
    }
}

// Test - ง่ายมาก!
[Test]
public void Should_Calculate_Total()
{
    // Arrange - สร้าง data ตรงๆ
    var cart = new List<CartItem>
    {
        new(1, 100, 2),
        new(2, 50, 1)
    };

    // Act
    var total = ShoppingCart.GetTotal(cart);

    // Assert
    Assert.That(total, Is.EqualTo(250));

    // ✅ ไม่ depend on state
    // ✅ ไม่ต้อง call AddItem ก่อน
}
```

### 7.1.3 No Side Effects = No Mocking

**Pure functions** ไม่มี side effects → **ไม่ต้อง mock**

**❌ With Side Effects:**

```csharp
// ❌ Side effects everywhere
public class UserService
{
    private readonly IDatabase _db;
    private readonly ILogger _logger;
    private readonly IEmailService _email;

    public User CreateUser(string email, string password)
    {
        _logger.Log($"Creating user {email}");           // ← Side effect

        var hash = BCrypt.HashPassword(password);        // ← Side effect
        var user = new User { Email = email, PasswordHash = hash };

        _db.SaveUser(user);                              // ← Side effect
        _email.SendWelcomeEmail(user.Email);            // ← Side effect

        _logger.Log($"User {user.Id} created");         // ← Side effect

        return user;
    }
}

// Test - ต้อง mock ทุกอย่าง
[Test]
public void Should_Create_User()
{
    // Arrange - Mock hell!
    var mockDb = new Mock<IDatabase>();
    var mockLogger = new Mock<ILogger>();
    var mockEmail = new Mock<IEmailService>();

    mockDb.Setup(db => db.SaveUser(It.IsAny<User>()))
        .Callback<User>(u => u.Id = 123);

    var service = new UserService(mockDb.Object, mockLogger.Object, mockEmail.Object);

    // Act
    var user = service.CreateUser("test@example.com", "password");

    // Assert
    Assert.That(user.Email, Is.EqualTo("test@example.com"));
    mockDb.Verify(db => db.SaveUser(It.IsAny<User>()), Times.Once);
    mockEmail.Verify(e => e.SendWelcomeEmail("test@example.com"), Times.Once);
    mockLogger.Verify(l => l.Log(It.IsAny<string>()), Times.Exactly(2));
}
```

**✅ Functional - แยก Pure และ Effects:**

```csharp
// C# language-ext - Pure logic
public record User(int Id, string Email, string PasswordHash);

public static class UserLogic
{
    // ✅ Pure function - easy to test!
    public static User CreateUserData(string email, string passwordHash, int id)
    {
        return new User(id, email, passwordHash);
    }

    // ✅ Pure function
    public static string HashPassword(string password)
    {
        return BCrypt.HashPassword(password);
    }
}

// Test - ไม่ต้อง mock!
[Test]
public void Should_Create_User_With_Correct_Data()
{
    // Act
    var user = UserLogic.CreateUserData("test@example.com", "hash123", 1);

    // Assert
    Assert.That(user.Email, Is.EqualTo("test@example.com"));
    Assert.That(user.PasswordHash, Is.EqualTo("hash123"));
    Assert.That(user.Id, Is.EqualTo(1));

    // ✅ No mocking
    // ✅ Fast
    // ✅ Simple
}
```

---

## 7.2 Testing Pure Functions

### 7.2.1 Pure Functions คืออะไร?

**Pure function** มี 2 คุณสมบัติ:
1. **Deterministic** - input เดียวกัน → output เดียวกัน
2. **No side effects** - ไม่เปลี่ยน state, ไม่ทำ I/O

**ตัวอย่าง Pure Functions:**

```csharp
// C# - Pure functions
using LanguageExt;
using static LanguageExt.Prelude;

// ✅ Pure - deterministic, no side effects
public static int Add(int a, int b) => a + b;

// ✅ Pure - always same output for same input
public static Option<int> Divide(int a, int b) =>
    b == 0 ? None : Some(a / b);

// ✅ Pure - returns new list, doesn't modify input
public static List<int> Filter(List<int> numbers, Func<int, bool> predicate) =>
    numbers.Where(predicate).ToList();

// ✅ Pure - validates without side effects
public static Validation<Error, string> ValidateEmail(string email) =>
    string.IsNullOrWhiteSpace(email) || !email.Contains("@")
        ? Fail<Error, string>(Error.New("Invalid email"))
        : Success<Error, string>(email);
```

**ตัวอย่าง Impure Functions:**

```csharp
// ❌ Impure - uses DateTime.UtcNow (non-deterministic)
public static bool IsExpired(DateTime createdAt)
{
    return DateTime.UtcNow - createdAt > TimeSpan.FromDays(7);
}

// ❌ Impure - modifies input list
public static void AddToList(List<int> list, int value)
{
    list.Add(value);  // ← Side effect!
}

// ❌ Impure - logs to console
public static int AddWithLogging(int a, int b)
{
    var result = a + b;
    Console.WriteLine($"Result: {result}");  // ← Side effect!
    return result;
}
```

### 7.2.2 Testing Pure Functions - Examples

**Example 1: Testing Validation**

```csharp
// C# language-ext - TodoValidation tests
using NUnit.Framework;
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

[TestFixture]
public class TodoValidationTests
{
    [Test]
    public void ValidateTitle_Should_Succeed_For_Valid_Title()
    {
        // Arrange
        var todo = new Todo { Title = "Learn FP", Description = "Study language-ext" };

        // Act
        var result = TodoValidation.ValidateTitle(todo);

        // Assert
        Assert.That(result.IsSuccess, Is.True);

        var value = result.Match(
            Succ: t => t.Title,
            Fail: _ => ""
        );
        Assert.That(value, Is.EqualTo("Learn FP"));
    }

    [Test]
    public void ValidateTitle_Should_Fail_For_Empty_Title()
    {
        // Arrange
        var todo = new Todo { Title = "", Description = "Test" };

        // Act
        var result = TodoValidation.ValidateTitle(todo);

        // Assert
        Assert.That(result.IsFail, Is.True);

        var error = result.Match(
            Succ: _ => "",
            Fail: errors => errors.Head.Message
        );
        Assert.That(error, Does.Contain("Title is required"));
    }

    [TestCase("A")]
    [TestCase("AB")]
    [TestCase("Valid Title")]
    [TestCase("A very long title that is exactly 200 characters long padding padding padding padding padding padding padding padding padding padding padding padding padding padding padding padding")]
    public void ValidateTitle_Should_Succeed_For_Valid_Lengths(string title)
    {
        // Arrange
        var todo = new Todo { Title = title };

        // Act
        var result = TodoValidation.ValidateTitle(todo);

        // Assert
        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public void ValidateTitle_Should_Fail_For_Title_Too_Long()
    {
        // Arrange
        var longTitle = new string('A', 201);  // 201 characters
        var todo = new Todo { Title = longTitle };

        // Act
        var result = TodoValidation.ValidateTitle(todo);

        // Assert
        Assert.That(result.IsFail, Is.True);
        var error = result.Match(
            Succ: _ => "",
            Fail: errors => errors.Head.Message
        );
        Assert.That(error, Does.Contain("less than 200"));
    }
}
```

**Example 2: Testing Option/Either Operations**

```csharp
[TestFixture]
public class OptionTests
{
    [Test]
    public void Some_Map_Should_Transform_Value()
    {
        // Arrange
        var some = Some(42);

        // Act
        var result = some.Map(x => x * 2);

        // Assert
        Assert.That(result.IsSome, Is.True);
        Assert.That(result.IfNone(0), Is.EqualTo(84));
    }

    [Test]
    public void None_Map_Should_Remain_None()
    {
        // Arrange
        Option<int> none = None;

        // Act
        var result = none.Map(x => x * 2);

        // Assert
        Assert.That(result.IsNone, Is.True);
    }

    [Test]
    public void Bind_Should_Chain_Options()
    {
        // Arrange
        Option<int> ParseInt(string s) =>
            int.TryParse(s, out var result) ? Some(result) : None;

        Option<int> Divide(int a, int b) =>
            b == 0 ? None : Some(a / b);

        // Act
        var result = ParseInt("10").Bind(a => Divide(a, 2));

        // Assert
        Assert.That(result.IsSome, Is.True);
        Assert.That(result.IfNone(0), Is.EqualTo(5));
    }

    [Test]
    public void Bind_Should_Short_Circuit_On_None()
    {
        // Arrange
        Option<int> ParseInt(string s) =>
            int.TryParse(s, out var result) ? Some(result) : None;

        Option<int> Divide(int a, int b) =>
            b == 0 ? None : Some(a / b);

        // Act - first operation fails
        var result = ParseInt("invalid").Bind(a => Divide(a, 2));

        // Assert
        Assert.That(result.IsNone, Is.True);
    }
}
```

**Example 3: Testing Validation with Multiple Errors**

```csharp
[TestFixture]
public class FormValidationTests
{
    [Test]
    public void Should_Succeed_For_Valid_Form()
    {
        // Arrange
        var form = new RegisterForm(
            Email: "test@example.com",
            Password: "Password123",
            ConfirmPassword: "Password123",
            Name: "John Doe",
            Age: 25
        );

        // Act
        var result = FormValidation.ValidateForm(form);

        // Assert
        Assert.That(result.IsSuccess, Is.True);

        var user = result.Match(
            Succ: u => u,
            Fail: _ => null
        );

        Assert.That(user, Is.Not.Null);
        Assert.That(user!.Email, Is.EqualTo("test@example.com"));
    }

    [Test]
    public void Should_Collect_All_Validation_Errors()
    {
        // Arrange - everything is invalid
        var form = new RegisterForm(
            Email: "invalid",           // ← Missing @
            Password: "short",          // ← Too short
            ConfirmPassword: "different", // ← Doesn't match
            Name: "A",                  // ← Too short
            Age: 15                     // ← Too young
        );

        // Act
        var result = FormValidation.ValidateForm(form);

        // Assert
        Assert.That(result.IsFail, Is.True);

        var errors = result.Match(
            Succ: _ => Seq<Error>(),
            Fail: e => e
        );

        // Should have multiple errors
        Assert.That(errors.Count, Is.GreaterThanOrEqualTo(5));

        var messages = errors.Map(e => e.Message).ToList();
        Assert.That(messages, Does.Contain("Email must contain @"));
        Assert.That(messages.Any(m => m.Contains("Password")), Is.True);
        Assert.That(messages, Does.Contain("Name must be at least 2 characters"));
        Assert.That(messages, Does.Contain("Must be at least 18 years old"));
    }
}
```

---

## 7.3 Testing Effects with Test Runtime

### 7.3.1 ทำไมต้อง Test Runtime?

**Effects** ใน Functional Programming เป็น **descriptions** ของงานที่ต้องทำ ไม่ใช่การทำจริง

เมื่อ test เราต้อง:
1. **สร้าง test implementations** ของ capabilities
2. **สร้าง test runtime** ที่ใช้ test implementations เหล่านั้น
3. **Run effects** กับ test runtime
4. **Verify results**

**Architecture:**

```
Production:
TodoService<Eff<AppRuntime>, AppRuntime>
  → Uses AppRuntime
    → Uses LiveDatabaseIO, LiveLoggerIO, LiveTimeIO
      → Actual database, actual logs, actual time

Testing:
TodoService<Eff<TestRuntime>, TestRuntime>
  → Uses TestRuntime
    → Uses TestDatabaseIO, TestLoggerIO, TestTimeIO
      → In-memory data, captured logs, fixed time
```

### 7.3.2 สร้าง Test Implementations

**Test Database:**

```csharp
// C# - Test DatabaseIO implementation
using LanguageExt;
using TodoApp.Domain;
using TodoApp.Infrastructure.Traits;
using static LanguageExt.Prelude;

public class TestDatabaseIO : DatabaseIO
{
    private readonly Dictionary<int, Todo> _todos = new();
    private int _nextId = 1;

    public Option<Todo> GetTodoById(int id)
    {
        return _todos.TryGetValue(id, out var todo)
            ? Some(todo)
            : None;
    }

    public List<Todo> GetAllTodos()
    {
        return _todos.Values.OrderByDescending(t => t.CreatedAt).ToList();
    }

    public Todo AddTodo(Todo todo)
    {
        var withId = todo with { Id = _nextId++ };
        _todos[withId.Id] = withId;
        return withId;
    }

    public Todo UpdateTodo(Todo todo)
    {
        _todos[todo.Id] = todo;
        return todo;
    }

    public Unit DeleteTodo(Todo todo)
    {
        _todos.Remove(todo.Id);
        return Unit.Default;
    }

    public Unit SaveChanges()
    {
        // No-op for in-memory
        return Unit.Default;
    }

    // Helper for tests
    public void Seed(params Todo[] todos)
    {
        foreach (var todo in todos)
        {
            _todos[todo.Id] = todo;
            _nextId = Math.Max(_nextId, todo.Id + 1);
        }
    }

    public void Clear()
    {
        _todos.Clear();
        _nextId = 1;
    }
}
```

**Test Logger:**

```csharp
// C# - Test LoggerIO implementation
public class TestLoggerIO : LoggerIO
{
    public List<string> InfoLogs { get; } = new();
    public List<string> WarningLogs { get; } = new();
    public List<string> ErrorLogs { get; } = new();

    public Unit LogInfo(string message)
    {
        InfoLogs.Add(message);
        return Unit.Default;
    }

    public Unit LogWarning(string message)
    {
        WarningLogs.Add(message);
        return Unit.Default;
    }

    public Unit LogError(string message)
    {
        ErrorLogs.Add(message);
        return Unit.Default;
    }

    public void Clear()
    {
        InfoLogs.Clear();
        WarningLogs.Clear();
        ErrorLogs.Clear();
    }
}
```

**Test Time:**

```csharp
// C# - Test TimeIO implementation
public class TestTimeIO : TimeIO
{
    private DateTime _fixedTime;

    public TestTimeIO(DateTime? fixedTime = null)
    {
        _fixedTime = fixedTime ?? new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);
    }

    public DateTime UtcNow() => _fixedTime;

    // Helper to advance time in tests
    public void AdvanceTime(TimeSpan duration)
    {
        _fixedTime = _fixedTime.Add(duration);
    }

    public void SetTime(DateTime time)
    {
        _fixedTime = time;
    }
}
```

### 7.3.3 สร้าง Test Runtime

```csharp
// C# - Test Runtime
using LanguageExt;
using LanguageExt.Effects;
using LanguageExt.Traits;
using TodoApp.Infrastructure.Traits;
using static LanguageExt.Prelude;

public record TestRuntime(
    TestDatabaseIO Database,
    TestLoggerIO Logger,
    TestTimeIO Time) :
    Has<Eff<TestRuntime>, DatabaseIO>,
    Has<Eff<TestRuntime>, LoggerIO>,
    Has<Eff<TestRuntime>, TimeIO>
{
    // Default constructor for convenience
    public TestRuntime() : this(
        new TestDatabaseIO(),
        new TestLoggerIO(),
        new TestTimeIO())
    {
    }

    static K<Eff<TestRuntime>, DatabaseIO> Has<Eff<TestRuntime>, DatabaseIO>.Ask =>
        liftEff((Func<TestRuntime, DatabaseIO>)(rt => rt.Database));

    static K<Eff<TestRuntime>, LoggerIO> Has<Eff<TestRuntime>, LoggerIO>.Ask =>
        liftEff((Func<TestRuntime, LoggerIO>)(rt => rt.Logger));

    static K<Eff<TestRuntime>, TimeIO> Has<Eff<TestRuntime>, TimeIO>.Ask =>
        liftEff((Func<TestRuntime, TimeIO>)(rt => rt.Time));
}
```

### 7.3.4 Testing TodoService

```csharp
// C# - TodoService tests
using NUnit.Framework;
using LanguageExt;
using LanguageExt.Effects;
using TodoApp.Domain;
using TodoApp.Features.Todos;
using static LanguageExt.Prelude;

[TestFixture]
public class TodoServiceTests
{
    private TestRuntime _runtime = null!;

    [SetUp]
    public void Setup()
    {
        _runtime = new TestRuntime();
    }

    [Test]
    public async Task List_Should_Return_All_Todos()
    {
        // Arrange
        var todo1 = new Todo { Id = 1, Title = "Todo 1", CreatedAt = DateTime.UtcNow };
        var todo2 = new Todo { Id = 2, Title = "Todo 2", CreatedAt = DateTime.UtcNow };
        _runtime.Database.Seed(todo1, todo2);

        // Act
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>
            .List()
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        var todos = result.Match(
            Succ: ts => ts,
            Fail: _ => new List<Todo>()
        );

        Assert.That(todos.Count, Is.EqualTo(2));
        Assert.That(todos.Any(t => t.Title == "Todo 1"), Is.True);
        Assert.That(todos.Any(t => t.Title == "Todo 2"), Is.True);

        // Verify logging
        Assert.That(_runtime.Logger.InfoLogs.Count, Is.GreaterThan(0));
        Assert.That(_runtime.Logger.InfoLogs[0], Does.Contain("Listing all todos"));
    }

    [Test]
    public async Task Get_Should_Return_Todo_When_Exists()
    {
        // Arrange
        var todo = new Todo { Id = 1, Title = "Test Todo", CreatedAt = DateTime.UtcNow };
        _runtime.Database.Seed(todo);

        // Act
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>
            .Get(1)
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        Assert.That(result.IsSucc, Is.True);

        var foundTodo = result.Match(
            Succ: t => t,
            Fail: _ => null
        );

        Assert.That(foundTodo, Is.Not.Null);
        Assert.That(foundTodo!.Title, Is.EqualTo("Test Todo"));
    }

    [Test]
    public async Task Get_Should_Fail_When_Not_Found()
    {
        // Arrange - no todos in database

        // Act
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>
            .Get(999)
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        Assert.That(result.IsFail, Is.True);

        var error = result.Match(
            Succ: _ => "",
            Fail: e => e.Message
        );

        Assert.That(error, Does.Contain("not found"));
    }

    [Test]
    public async Task Create_Should_Add_Todo_With_Timestamp()
    {
        // Arrange
        var expectedTime = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        _runtime.Time.SetTime(expectedTime);

        // Act
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>
            .Create("New Todo", "Description")
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        Assert.That(result.IsSucc, Is.True);

        var todo = result.Match(
            Succ: t => t,
            Fail: _ => null
        );

        Assert.That(todo, Is.Not.Null);
        Assert.That(todo!.Title, Is.EqualTo("New Todo"));
        Assert.That(todo.Description, Is.EqualTo("Description"));
        Assert.That(todo.CreatedAt, Is.EqualTo(expectedTime));
        Assert.That(todo.IsCompleted, Is.False);

        // Verify it was saved to database
        var saved = _runtime.Database.GetTodoById(todo.Id);
        Assert.That(saved.IsSome, Is.True);
    }

    [Test]
    public async Task Create_Should_Fail_For_Invalid_Title()
    {
        // Act - empty title
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>
            .Create("", "Description")
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        Assert.That(result.IsFail, Is.True);

        var error = result.Match(
            Succ: _ => "",
            Fail: e => e.Message
        );

        Assert.That(error, Does.Contain("Title"));
    }

    [Test]
    public async Task Update_Should_Modify_Existing_Todo()
    {
        // Arrange
        var original = new Todo
        {
            Id = 1,
            Title = "Original",
            Description = "Old description",
            CreatedAt = DateTime.UtcNow
        };
        _runtime.Database.Seed(original);

        // Act
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>
            .Update(1, "Updated", "New description")
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        Assert.That(result.IsSucc, Is.True);

        var updated = result.Match(
            Succ: t => t,
            Fail: _ => null
        );

        Assert.That(updated, Is.Not.Null);
        Assert.That(updated!.Title, Is.EqualTo("Updated"));
        Assert.That(updated.Description, Is.EqualTo("New description"));
        Assert.That(updated.Id, Is.EqualTo(1));
        Assert.That(updated.CreatedAt, Is.EqualTo(original.CreatedAt));
    }

    [Test]
    public async Task ToggleComplete_Should_Set_CompletedAt()
    {
        // Arrange
        var todo = new Todo
        {
            Id = 1,
            Title = "Test",
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow
        };
        _runtime.Database.Seed(todo);

        var completedTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        _runtime.Time.SetTime(completedTime);

        // Act
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>
            .ToggleComplete(1)
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        Assert.That(result.IsSucc, Is.True);

        var toggled = result.Match(
            Succ: t => t,
            Fail: _ => null
        );

        Assert.That(toggled, Is.Not.Null);
        Assert.That(toggled!.IsCompleted, Is.True);
        Assert.That(toggled.CompletedAt, Is.EqualTo(completedTime));
    }

    [Test]
    public async Task ToggleComplete_Should_Clear_CompletedAt_When_Unmarking()
    {
        // Arrange
        var todo = new Todo
        {
            Id = 1,
            Title = "Test",
            IsCompleted = true,
            CompletedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        _runtime.Database.Seed(todo);

        // Act
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>
            .ToggleComplete(1)
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        var toggled = result.Match(Succ: t => t, Fail: _ => null);

        Assert.That(toggled!.IsCompleted, Is.False);
        Assert.That(toggled.CompletedAt, Is.Null);
    }

    [Test]
    public async Task Delete_Should_Remove_Todo()
    {
        // Arrange
        var todo = new Todo { Id = 1, Title = "Test", CreatedAt = DateTime.UtcNow };
        _runtime.Database.Seed(todo);

        // Act
        var result = await TodoService<Eff<TestRuntime>, TestRuntime>
            .Delete(1)
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        Assert.That(result.IsSucc, Is.True);

        // Verify it's gone from database
        var found = _runtime.Database.GetTodoById(1);
        Assert.That(found.IsNone, Is.True);
    }
}
```

---

## 7.4 Property-Based Testing

### 7.4.1 Property-Based Testing คืออะไร?

**Example-based testing:**
```csharp
// ✅ Test specific examples
Assert.That(Add(2, 3), Is.EqualTo(5));
Assert.That(Add(0, 0), Is.EqualTo(0));
Assert.That(Add(-1, 1), Is.EqualTo(0));
```

**Property-based testing:**
```csharp
// ✅ Test properties that should hold for ALL inputs
Property: ∀ a, b: Add(a, b) == Add(b, a)  // Commutative
Property: ∀ a: Add(a, 0) == a             // Identity
Property: ∀ a, b, c: Add(Add(a, b), c) == Add(a, Add(b, c))  // Associative
```

### 7.4.2 FsCheck - Property Testing for .NET

```csharp
// C# - Using FsCheck
using FsCheck;
using FsCheck.NUnit;

[TestFixture]
public class PropertyTests
{
    [Property]
    public Property Addition_Is_Commutative(int a, int b)
    {
        return (Add(a, b) == Add(b, a)).ToProperty();
    }

    [Property]
    public Property Addition_Has_Identity(int a)
    {
        return (Add(a, 0) == a).ToProperty();
    }

    [Property]
    public Property Addition_Is_Associative(int a, int b, int c)
    {
        var left = Add(Add(a, b), c);
        var right = Add(a, Add(b, c));
        return (left == right).ToProperty();
    }

    // FsCheck will generate 100 random test cases automatically!
}
```

### 7.4.3 Testing Option Properties

```csharp
[TestFixture]
public class OptionPropertyTests
{
    [Property]
    public Property Map_Identity(int value)
    {
        // Property: map id == id
        var some = Some(value);
        var mapped = some.Map(x => x);

        return (mapped == some).ToProperty();
    }

    [Property]
    public Property Map_Composition(int value)
    {
        // Property: map (f . g) == map f . map g
        Func<int, int> f = x => x * 2;
        Func<int, int> g = x => x + 1;

        var some = Some(value);

        var left = some.Map(x => f(g(x)));
        var right = some.Map(g).Map(f);

        return (left.IfNone(0) == right.IfNone(0)).ToProperty();
    }

    [Property]
    public Property None_Is_Zero(int value)
    {
        // Property: None remains None under any operation
        Option<int> none = None;

        var mapped = none.Map(x => x * value);
        var bound = none.Bind(x => Some(x * value));

        return (mapped.IsNone && bound.IsNone).ToProperty();
    }
}
```

### 7.4.4 Testing List Operations

```csharp
[TestFixture]
public class ListPropertyTests
{
    [Property]
    public Property Reverse_Twice_Is_Identity(int[] array)
    {
        // Property: reverse . reverse == id
        var list = array.ToList();
        var reversed = list.Reverse<int>().ToList();
        var reversedTwice = reversed.Reverse<int>().ToList();

        return list.SequenceEqual(reversedTwice).ToProperty();
    }

    [Property]
    public Property Map_Preserves_Length(int[] array)
    {
        // Property: length (map f xs) == length xs
        var list = array.ToList();
        var mapped = list.Select(x => x * 2).ToList();

        return (list.Count == mapped.Count).ToProperty();
    }

    [Property]
    public Property Filter_Reduces_Length(int[] array)
    {
        // Property: length (filter p xs) <= length xs
        var list = array.ToList();
        var filtered = list.Where(x => x > 0).ToList();

        return (filtered.Count <= list.Count).ToProperty();
    }
}
```

---

## 7.5 Integration Testing

### 7.5.1 Testing with Real Database

```csharp
// C# - Integration tests with real database
using NUnit.Framework;
using Microsoft.EntityFrameworkCore;
using LanguageExt.Effects;

[TestFixture]
public class TodoServiceIntegrationTests
{
    private AppDbContext _dbContext = null!;
    private AppRuntime _runtime = null!;

    [SetUp]
    public void Setup()
    {
        // Use in-memory database for integration tests
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new AppDbContext(options);
        _dbContext.Database.EnsureCreated();

        // Create service provider
        var services = new ServiceCollection()
            .AddSingleton(_dbContext)
            .AddLogging()
            .BuildServiceProvider();

        _runtime = new AppRuntime(services);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Test]
    public async Task Should_Create_And_Retrieve_Todo()
    {
        // Act - Create
        var createResult = await TodoService<Eff<AppRuntime>, AppRuntime>
            .Create("Integration Test", "Testing with real DB")
            .RunAsync(_runtime, EnvIO.New());

        Assert.That(createResult.IsSucc, Is.True);
        var created = createResult.Match(Succ: t => t, Fail: _ => null);

        // Act - Retrieve
        var getResult = await TodoService<Eff<AppRuntime>, AppRuntime>
            .Get(created!.Id)
            .RunAsync(_runtime, EnvIO.New());

        // Assert
        Assert.That(getResult.IsSucc, Is.True);
        var retrieved = getResult.Match(Succ: t => t, Fail: _ => null);

        Assert.That(retrieved!.Title, Is.EqualTo("Integration Test"));
        Assert.That(retrieved.Description, Is.EqualTo("Testing with real DB"));
    }

    [Test]
    public async Task Should_Update_Todo_In_Database()
    {
        // Arrange - Create todo
        var createResult = await TodoService<Eff<AppRuntime>, AppRuntime>
            .Create("Original", null)
            .RunAsync(_runtime, EnvIO.New());

        var todo = createResult.Match(Succ: t => t, Fail: _ => null)!;

        // Act - Update
        var updateResult = await TodoService<Eff<AppRuntime>, AppRuntime>
            .Update(todo.Id, "Updated", "New description")
            .RunAsync(_runtime, EnvIO.New());

        // Assert - Retrieve and verify
        var getResult = await TodoService<Eff<AppRuntime>, AppRuntime>
            .Get(todo.Id)
            .RunAsync(_runtime, EnvIO.New());

        var updated = getResult.Match(Succ: t => t, Fail: _ => null);
        Assert.That(updated!.Title, Is.EqualTo("Updated"));
        Assert.That(updated.Description, Is.EqualTo("New description"));
    }
}
```

---

## 7.6 Best Practices

### 7.6.1 Test Organization

**✅ DO:**

```
Tests/
├── Unit/
│   ├── Domain/
│   │   └── TodoValidationTests.cs
│   ├── Infrastructure/
│   │   └── CapabilityTests.cs
│   └── Features/
│       └── TodoServiceTests.cs
├── Integration/
│   └── TodoServiceIntegrationTests.cs
└── TestHelpers/
    ├── TestRuntime.cs
    ├── TestDatabaseIO.cs
    └── TestLoggerIO.cs
```

### 7.6.2 Naming Conventions

**✅ DO:**

```csharp
[Test]
public void Should_Return_None_When_Email_Not_Found()
{
    // Method name describes:
    // - What it should do: "Return None"
    // - Under what condition: "When email not found"
}

[Test]
public void ValidateEmail_Should_Fail_For_Missing_At_Sign()
{
    // Alternative format:
    // - What is being tested: "ValidateEmail"
    // - Expected behavior: "Should fail"
    // - Scenario: "For missing @ sign"
}
```

### 7.6.3 AAA Pattern

**Arrange - Act - Assert:**

```csharp
[Test]
public async Task Should_Create_Todo()
{
    // Arrange - Setup test data and dependencies
    var runtime = new TestRuntime();
    runtime.Time.SetTime(new DateTime(2024, 1, 15));

    // Act - Execute the code under test
    var result = await TodoService<Eff<TestRuntime>, TestRuntime>
        .Create("Test", "Description")
        .RunAsync(runtime, EnvIO.New());

    // Assert - Verify the results
    Assert.That(result.IsSucc, Is.True);
    var todo = result.Match(Succ: t => t, Fail: _ => null);
    Assert.That(todo!.Title, Is.EqualTo("Test"));
}
```

### 7.6.4 Test One Thing

**❌ DON'T:**

```csharp
[Test]
public async Task Should_Test_Everything()
{
    // Testing too many things at once
    var result = await Service.Create(...);
    Assert.That(result.IsSucc, Is.True);

    var list = await Service.List();
    Assert.That(list.Count, Is.EqualTo(1));

    var updated = await Service.Update(...);
    Assert.That(updated.Title, Is.EqualTo("Updated"));

    // ❌ If this fails, which part failed?
}
```

**✅ DO:**

```csharp
[Test]
public async Task Should_Create_Todo()
{
    var result = await Service.Create(...);
    Assert.That(result.IsSucc, Is.True);
}

[Test]
public async Task Should_List_Created_Todos()
{
    await Service.Create(...);
    var list = await Service.List();
    Assert.That(list.Count, Is.EqualTo(1));
}

[Test]
public async Task Should_Update_Todo_Title()
{
    var created = await Service.Create(...);
    var updated = await Service.Update(...);
    Assert.That(updated.Title, Is.EqualTo("Updated"));
}
```

### 7.6.5 Avoid Test Interdependence

**❌ DON'T:**

```csharp
private static Todo? _sharedTodo;

[Test]
public async Task Test1_Create()
{
    _sharedTodo = await Service.Create(...);  // ← Sets shared state
}

[Test]
public async Task Test2_Update()
{
    await Service.Update(_sharedTodo!.Id, ...);  // ← Depends on Test1!
}
```

**✅ DO:**

```csharp
[Test]
public async Task Should_Update_Todo()
{
    // Arrange - create todo in this test
    var created = await Service.Create(...);

    // Act
    var updated = await Service.Update(created.Id, ...);

    // Assert
    Assert.That(updated.Title, Is.EqualTo("Updated"));
}
```

---

## 7.7 สรุป

### สิ่งที่เรียนรู้ในบทนี้

1. **ทำไม FP Test ง่ายกว่า** - Pure functions, immutability, no side effects
2. **Testing Pure Functions** - Deterministic, fast, no mocking
3. **Test Runtime** - Test implementations of capabilities
4. **Mocking Capabilities** - TestDatabaseIO, TestLoggerIO, TestTimeIO
5. **Property-Based Testing** - FsCheck สำหรับ test properties
6. **Integration Testing** - Test กับ real database
7. **Best Practices** - Organization, naming, AAA pattern, independence

### Key Takeaways

✅ **Pure Functions = Easy Testing**
- Deterministic
- No mocking needed
- Fast execution

✅ **Separation of Pure and Effects**
- Test pure logic separately
- Mock only effects

✅ **Test Runtime Pattern**
- Create test implementations
- Inject via runtime
- Full control over behavior

✅ **Property-Based Testing**
- Test properties, not examples
- Automatic test case generation
- Higher confidence

---

## 7.8 แบบฝึกหัด

### แบบฝึกหัดที่ 1: Test Validation

สร้าง tests สำหรับ password validation:

```csharp
// TODO: Implement tests
[TestFixture]
public class PasswordValidationTests
{
    [Test]
    public void Should_Succeed_For_Valid_Password()
    {
        // Hints:
        // - Valid: >= 8 chars, has digit, has uppercase
        // - Test with "Password123"
    }

    [Test]
    public void Should_Fail_For_Short_Password()
    {
        // Test with "Pass1"
    }

    [Test]
    public void Should_Collect_Multiple_Errors()
    {
        // Test with "short" - multiple validation failures
    }
}
```

### แบบฝึกหัดที่ 2: Test Effects

สร้าง tests สำหรับ User service:

```csharp
// TODO: Implement UserService tests
[TestFixture]
public class UserServiceTests
{
    [Test]
    public async Task Should_Create_User_With_Hashed_Password()
    {
        // Hints:
        // 1. Create TestRuntime
        // 2. Call UserService.Create
        // 3. Verify password is hashed
        // 4. Verify user is saved to database
    }

    [Test]
    public async Task Should_Not_Create_Duplicate_Email()
    {
        // Hints:
        // 1. Seed existing user
        // 2. Try to create with same email
        // 3. Should fail
    }
}
```

### แบบฝึกหัดที่ 3: Property-Based Testing

สร้าง property tests:

```csharp
// TODO: Implement property tests
[TestFixture]
public class ListPropertyTests
{
    [Property]
    public Property Append_Then_Length_Increases(int[] array, int value)
    {
        // Property: length (append x xs) == length xs + 1
    }

    [Property]
    public Property Filter_Then_Map_Order(int[] array)
    {
        // Property: map f . filter p == filter p . map f
        // (when f doesn't affect predicate p)
    }
}
```

### แบบฝึกหัดที่ 4: Integration Test

สร้าง integration test สำหรับ complete workflow:

```csharp
// TODO: Implement workflow test
[Test]
public async Task Should_Complete_Todo_Workflow()
{
    // Workflow:
    // 1. Create todo
    // 2. Update title
    // 3. Toggle complete
    // 4. Verify final state
    // 5. Delete todo
    // 6. Verify deletion
}
```

---

**บทถัดไป:** [บทที่ 8: Frontend with Effect-TS](chapter-08.md)

ในบทถัดไป เราจะสร้าง Frontend ด้วย TypeScript + Effect-TS!
