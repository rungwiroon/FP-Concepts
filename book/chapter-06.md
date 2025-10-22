# บทที่ 6: Validation และ Error Handling

> Error handling แบบ Type-safe ไม่ใช้ Exceptions

---

## เนื้อหาในบทนี้

- ปัญหาของ Exceptions
- Option\<T\> - สำหรับค่าที่อาจไม่มี
- Either\<L, R\> - สำหรับ errors
- Validation\<Error, A\> - สำหรับ multiple errors
- Fin\<A\> - สำหรับ fallible effects
- Validation Patterns
- Error Handling Best Practices
- Real-world Examples
- แบบฝึกหัด

---

## 6.1 ปัญหาของ Exceptions

### 6.1.1 Exceptions ทำลาย Type Safety

**C# - ปัญหาของ Exceptions:**

```csharp
// ❌ Function signature โกหก - ไม่บอกว่าอาจ throw exception
public User GetUserById(int id)
{
    var user = _db.Users.Find(id);
    if (user == null)
        throw new NotFoundException($"User {id} not found");  // ← Hidden!
    return user;
}

// Caller ไม่รู้ว่าต้อง handle exception
var user = GetUserById(123);  // ← อาจ throw ได้ แต่ signature ไม่บอก
Console.WriteLine(user.Name); // ← อาจ crash!
```

**ปัญหา:**
1. ❌ **Type signature โกหก** - บอกว่า return `User` แต่จริงๆอาจ throw exception
2. ❌ **Compiler ไม่เช็ค** - ลืม catch exception ก็ compile ผ่าน
3. ❌ **Documentation ไม่น่าเชื่อถือ** - ต้องอ่าน source code หรือ docs (ถ้ามี)
4. ❌ **ทำลาย Referential Transparency** - เรียกครั้งเดียวอาจได้ผลต่างกับเรียกสองครั้ง

**TypeScript - ปัญหาเหมือนกัน:**

```typescript
// ❌ Type signature ไม่บอกว่าอาจ throw
function getUserById(id: number): User {
  const user = db.users.find(u => u.id === id);
  if (!user) {
    throw new Error(`User ${id} not found`);  // ← Hidden!
  }
  return user;
}

// Caller ไม่รู้ว่าต้อง try-catch
const user = getUserById(123);  // ← TypeScript ไม่เตือน!
console.log(user.name);        // ← อาจ crash runtime!
```

### 6.1.2 Exceptions ทำให้ Control Flow ซับซ้อน

```csharp
// ❌ Try-catch hell - ยากต่อการอ่าน
try
{
    var user = GetUserById(userId);
    try
    {
        var order = GetOrderById(orderId);
        try
        {
            var result = ProcessOrder(user, order);
            return result;
        }
        catch (ProcessingException ex)
        {
            _logger.LogError(ex, "Processing failed");
            return null;
        }
    }
    catch (NotFoundException ex)
    {
        _logger.LogError(ex, "Order not found");
        return null;
    }
}
catch (NotFoundException ex)
{
    _logger.LogError(ex, "User not found");
    return null;
}
```

**ปัญหา:**
1. ❌ **Nested try-catch** - อ่านยาก maintain ยาก
2. ❌ **Error handling กระจัดกระจาย** - ไม่รู้ว่า error ไหนมาจากไหน
3. ❌ **ไม่ compose ได้** - ต้อง copy-paste try-catch ทุกที่
4. ❌ **Performance overhead** - Stack unwinding ช้า

### 6.1.3 Exceptions ไม่ Composable

```csharp
// ❌ ไม่สามารถ chain operations ได้เรียบ
User user;
try { user = GetUserById(userId); }
catch { return null; }

Order order;
try { order = GetOrderById(orderId); }
catch { return null; }

Result result;
try { result = ProcessOrder(user, order); }
catch { return null; }

return result;
```

---

## 6.2 Option\<T\> - สำหรับค่าที่อาจไม่มี

### 6.2.1 Option\<T\> คืออะไร?

**Option\<T\>** เป็น type ที่แทน **"ค่าที่อาจมี หรืออาจไม่มี"** (optional value) แบบ type-safe

**ใน C# language-ext:**

```csharp
// Option<T> มี 2 cases:
// - Some(value) - มีค่า
// - None        - ไม่มีค่า

using LanguageExt;
using static LanguageExt.Prelude;

// สร้าง Option
Option<int> someValue = Some(42);        // มีค่า 42
Option<int> noValue = None;              // ไม่มีค่า
Option<int> nullable = Optional(null);   // null → None

// Pattern matching
var result = someValue.Match(
    Some: value => $"Got {value}",
    None: () => "No value"
);
// result = "Got 42"
```

**ใน TypeScript Effect-TS:**

```typescript
import { Option } from "effect";

// สร้าง Option
const someValue: Option.Option<number> = Option.some(42);  // มีค่า
const noValue: Option.Option<number> = Option.none();      // ไม่มีค่า
const fromNullable = Option.fromNullable(null);            // null → None

// Pattern matching
const result = Option.match(someValue, {
  onSome: (value) => `Got ${value}`,
  onNone: () => "No value"
});
// result = "Got 42"
```

### 6.2.2 เมื่อไหร่ควรใช้ Option\<T\>?

✅ **Use Option\<T\> เมื่อ:**

**1. Query ที่อาจไม่เจอข้อมูล**

```csharp
// C# language-ext
// ✅ ดี - Type บอกชัดเจนว่า "อาจไม่เจอ"
public Option<User> FindUserById(int id)
{
    var user = _db.Users.Find(id);
    return Optional(user);  // null → None, not null → Some(user)
}

// Caller ต้อง handle ทั้ง Some และ None
var userOpt = FindUserById(123);
var name = userOpt.Match(
    Some: user => user.Name,
    None: () => "Unknown"
);
```

```typescript
// TypeScript Effect-TS
// ✅ ดี - Type บอกว่า "อาจไม่เจอ"
function findUserById(id: number): Option.Option<User> {
  const user = db.users.find(u => u.id === id);
  return Option.fromNullable(user);  // undefined → None, found → Some(user)
}

// Caller ต้อง handle
const userOpt = findUserById(123);
const name = Option.match(userOpt, {
  onSome: (user) => user.name,
  onNone: () => "Unknown"
});
```

**2. Optional Configuration/Parameters**

```csharp
// C# - Configuration ที่อาจไม่มี
public record AppConfig
{
    public Option<int> MaxRetries { get; init; }     // อาจไม่ตั้งค่า
    public Option<string> ApiKey { get; init; }      // อาจไม่มี
}

// ใช้ค่า default ถ้าไม่มี
var maxRetries = config.MaxRetries.IfNone(3);
var apiKey = config.ApiKey.IfNone("default-key");
```

```typescript
// TypeScript - Optional parameters
interface AppConfig {
  maxRetries?: number;
  apiKey?: string;
}

function loadConfig(config: AppConfig) {
  const maxRetries = Option.fromNullable(config.maxRetries);
  const apiKey = Option.fromNullable(config.apiKey);

  // Use defaults
  const retries = Option.getOrElse(maxRetries, () => 3);
  const key = Option.getOrElse(apiKey, () => "default-key");
}
```

**3. Nullable Properties**

```csharp
// C# - Domain entity
public record User
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public Option<string> Email { get; init; }       // อาจไม่มี email
    public Option<DateTime> LastLogin { get; init; } // อาจยังไม่เคย login
}

// Type-safe access
var emailText = user.Email.Match(
    Some: email => $"Email: {email}",
    None: () => "No email"
);
```

### 6.2.3 Option\<T\> Operations

**Map - Transform value ถ้ามี**

```csharp
// C# language-ext
Option<int> some = Some(42);
Option<int> none = None;

var doubled = some.Map(x => x * 2);  // Some(84)
var nothing = none.Map(x => x * 2);  // None (ไม่รัน function)

// Chain transformations
var result = Some(10)
    .Map(x => x * 2)      // Some(20)
    .Map(x => x + 5)      // Some(25)
    .Map(x => x.ToString()); // Some("25")
```

```typescript
// TypeScript Effect-TS
import { pipe } from "effect/Function";

const some = Option.some(42);
const none = Option.none<number>();

const doubled = pipe(some, Option.map(x => x * 2));  // Some(84)
const nothing = pipe(none, Option.map(x => x * 2));  // None

// Chain transformations
const result = pipe(
  Option.some(10),
  Option.map(x => x * 2),      // Some(20)
  Option.map(x => x + 5),      // Some(25)
  Option.map(x => x.toString()) // Some("25")
);
```

**Bind / FlatMap - Chain operations ที่ return Option**

```csharp
// C# - Chain database queries
public Option<User> FindUserById(int id) { ... }
public Option<Order> FindOrderByUserId(int userId) { ... }

// ❌ Map ไม่ได้ - จะได้ Option<Option<Order>>
var orderOpt = FindUserById(123)
    .Map(user => FindOrderByUserId(user.Id));  // ❌ Option<Option<Order>>

// ✅ Bind ได้ - flatten ให้อัตโนมัติ
var orderOpt = FindUserById(123)
    .Bind(user => FindOrderByUserId(user.Id)); // ✅ Option<Order>

// LINQ query syntax (ใช้ Bind ข้างใน)
var result =
    from user in FindUserById(123)
    from order in FindOrderByUserId(user.Id)
    select new { user, order };
```

```typescript
// TypeScript Effect-TS
function findUserById(id: number): Option.Option<User> { /* ... */ }
function findOrderByUserId(userId: number): Option.Option<Order> { /* ... */ }

// ❌ map ไม่ได้
const orderOpt = pipe(
  findUserById(123),
  Option.map(user => findOrderByUserId(user.id))  // ❌ Option<Option<Order>>
);

// ✅ flatMap ได้
const orderOpt = pipe(
  findUserById(123),
  Option.flatMap(user => findOrderByUserId(user.id))  // ✅ Option<Order>
);
```

**Filter - กรองค่า**

```csharp
// C# - เอาเฉพาะ even numbers
var some = Some(42);
var filtered = some.Filter(x => x % 2 == 0);  // Some(42) - ผ่าน filter

var odd = Some(41);
var removed = odd.Filter(x => x % 2 == 0);    // None - ไม่ผ่าน filter
```

```typescript
// TypeScript
const some = Option.some(42);
const filtered = pipe(some, Option.filter(x => x % 2 === 0));  // Some(42)

const odd = Option.some(41);
const removed = pipe(odd, Option.filter(x => x % 2 === 0));    // None
```

**IfNone / GetOrElse - ให้ค่า default**

```csharp
// C# language-ext
var some = Some(42);
var none = None;

var value1 = some.IfNone(0);  // 42
var value2 = none.IfNone(0);  // 0

// Lazy evaluation - ไม่รันถ้าไม่จำเป็น
var value3 = some.IfNone(() => ExpensiveComputation());  // ไม่รัน
var value4 = none.IfNone(() => ExpensiveComputation());  // รัน
```

```typescript
// TypeScript Effect-TS
const some = Option.some(42);
const none = Option.none<number>();

const value1 = Option.getOrElse(some, () => 0);  // 42
const value2 = Option.getOrElse(none, () => 0);  // 0

// Lazy - ไม่รันถ้าไม่จำเป็น
const value3 = Option.getOrElse(some, () => expensiveComputation());  // ไม่รัน
const value4 = Option.getOrElse(none, () => expensiveComputation());  // รัน
```

### 6.2.4 Real-world Example: User Lookup

```csharp
// C# language-ext - Complete example
using LanguageExt;
using static LanguageExt.Prelude;

public record User(int Id, string Name, Option<string> Email);

public class UserService
{
    private readonly List<User> _users = new()
    {
        new(1, "Alice", Some("alice@example.com")),
        new(2, "Bob", None),
        new(3, "Charlie", Some("charlie@example.com"))
    };

    // Find user by ID
    public Option<User> FindById(int id) =>
        Optional(_users.FirstOrDefault(u => u.Id == id));

    // Find user by email
    public Option<User> FindByEmail(string email) =>
        Optional(_users.FirstOrDefault(u =>
            u.Email.IsSome && u.Email == email));

    // Get user's email or default
    public string GetUserEmail(int id) =>
        FindById(id)
            .Bind(user => user.Email)       // Option<User> → Option<string>
            .IfNone("no-email@example.com");

    // Get display name with email
    public string GetDisplayName(int id) =>
        FindById(id).Match(
            Some: user => user.Email.Match(
                Some: email => $"{user.Name} <{email}>",
                None: () => user.Name
            ),
            None: () => "Unknown User"
        );
}

// Usage
var service = new UserService();

Console.WriteLine(service.GetDisplayName(1));  // "Alice <alice@example.com>"
Console.WriteLine(service.GetDisplayName(2));  // "Bob"
Console.WriteLine(service.GetDisplayName(99)); // "Unknown User"

Console.WriteLine(service.GetUserEmail(1));    // "alice@example.com"
Console.WriteLine(service.GetUserEmail(2));    // "no-email@example.com"
```

```typescript
// TypeScript Effect-TS - Complete example
import { Option, pipe } from "effect";

interface User {
  id: number;
  name: string;
  email: Option.Option<string>;
}

class UserService {
  private users: User[] = [
    { id: 1, name: "Alice", email: Option.some("alice@example.com") },
    { id: 2, name: "Bob", email: Option.none() },
    { id: 3, name: "Charlie", email: Option.some("charlie@example.com") }
  ];

  findById(id: number): Option.Option<User> {
    const user = this.users.find(u => u.id === id);
    return Option.fromNullable(user);
  }

  findByEmail(email: string): Option.Option<User> {
    const user = this.users.find(u =>
      Option.isSome(u.email) && u.email.value === email
    );
    return Option.fromNullable(user);
  }

  getUserEmail(id: number): string {
    return pipe(
      this.findById(id),
      Option.flatMap(user => user.email),
      Option.getOrElse(() => "no-email@example.com")
    );
  }

  getDisplayName(id: number): string {
    return pipe(
      this.findById(id),
      Option.match({
        onSome: (user) => pipe(
          user.email,
          Option.match({
            onSome: (email) => `${user.name} <${email}>`,
            onNone: () => user.name
          })
        ),
        onNone: () => "Unknown User"
      })
    );
  }
}

// Usage
const service = new UserService();

console.log(service.getDisplayName(1));  // "Alice <alice@example.com>"
console.log(service.getDisplayName(2));  // "Bob"
console.log(service.getDisplayName(99)); // "Unknown User"

console.log(service.getUserEmail(1));    // "alice@example.com"
console.log(service.getUserEmail(2));    // "no-email@example.com"
```

---

## 6.3 Either\<L, R\> - สำหรับ Errors

### 6.3.1 Either\<L, R\> คืออะไร?

**Either\<L, R\>** แทน **"ค่าที่อาจสำเร็จ (Right) หรือล้มเหลว (Left)"**

**Convention:**
- **Left (L)** = Error case
- **Right (R)** = Success case
- "Right is right" = ความสำเร็จอยู่ทางขวา

**ใน C# language-ext:**

```csharp
using LanguageExt;
using static LanguageExt.Prelude;

// Either<L, R> มี 2 cases:
// - Left(error)  - ล้มเหลว
// - Right(value) - สำเร็จ

// สร้าง Either
Either<string, int> success = Right<string, int>(42);
Either<string, int> failure = Left<string, int>("Error!");

// Pattern matching
var result = success.Match(
    Left: error => $"Error: {error}",
    Right: value => $"Success: {value}"
);
// result = "Success: 42"
```

**ใน TypeScript Effect-TS:**

```typescript
import { Either } from "effect";

// สร้าง Either
const success: Either.Either<number, string> = Either.right(42);
const failure: Either.Either<number, string> = Either.left("Error!");

// Pattern matching
const result = Either.match(success, {
  onLeft: (error) => `Error: ${error}`,
  onRight: (value) => `Success: ${value}`
});
// result = "Success: 42"
```

**⚠️ สังเกต:** Effect-TS ใช้ `Either<R, L>` (Error type อยู่ตัวแรก) ต่างจาก language-ext!

### 6.3.2 เมื่อไหร่ควรใช้ Either\<L, R\>?

✅ **Use Either\<L, R\> เมื่อ:**

**1. Operation ที่อาจล้มเหลว พร้อมข้อมูล error**

```csharp
// C# - Parse integer with error message
public Either<string, int> ParseInt(string input)
{
    if (int.TryParse(input, out var result))
        return Right<string, int>(result);
    else
        return Left<string, int>($"'{input}' is not a valid integer");
}

// Usage - ต้อง handle ทั้ง success และ error
var result1 = ParseInt("42");    // Right(42)
var result2 = ParseInt("abc");   // Left("'abc' is not a valid integer")

var message = result2.Match(
    Left: error => $"Parse failed: {error}",
    Right: value => $"Parsed: {value}"
);
```

```typescript
// TypeScript Effect-TS
function parseIntSafe(input: string): Either.Either<number, string> {
  const num = parseInt(input, 10);
  if (isNaN(num)) {
    return Either.left(`'${input}' is not a valid integer`);
  }
  return Either.right(num);
}

// Usage
const result1 = parseIntSafe("42");    // Right(42)
const result2 = parseIntSafe("abc");   // Left("...")

const message = Either.match(result2, {
  onLeft: (error) => `Parse failed: ${error}`,
  onRight: (value) => `Parsed: ${value}`
});
```

**2. Business Logic ที่ต้องการ specific error types**

```csharp
// C# - Typed errors
public record ValidationError(string Field, string Message);
public record NotFoundError(string EntityType, int Id);
public record AuthorizationError(string Action, string Reason);

// Function with specific error type
public Either<NotFoundError, User> GetUser(int id)
{
    var user = _db.Users.Find(id);
    return user != null
        ? Right<NotFoundError, User>(user)
        : Left<NotFoundError, User>(new NotFoundError("User", id));
}

// Caller รู้ชัดว่า error type คืออะไร
var result = GetUser(123);
result.Match(
    Left: error => Console.WriteLine($"{error.EntityType} {error.Id} not found"),
    Right: user => Console.WriteLine($"Found: {user.Name}")
);
```

```typescript
// TypeScript - Typed errors
interface ValidationError {
  readonly _tag: "ValidationError";
  field: string;
  message: string;
}

interface NotFoundError {
  readonly _tag: "NotFoundError";
  entityType: string;
  id: number;
}

function getUser(id: number): Either.Either<User, NotFoundError> {
  const user = db.users.find(u => u.id === id);
  return user
    ? Either.right(user)
    : Either.left({ _tag: "NotFoundError", entityType: "User", id });
}
```

**3. Railway-Oriented Programming**

Either ช่วยให้เขียน "happy path" ได้เรียบ โดย error จะ "short-circuit" อัตโนมัติ:

```csharp
// C# - Chain operations, หยุดทันทีเมื่อเจอ error
public Either<string, User> RegisterUser(string email, string password)
{
    return ValidateEmail(email)           // Either<string, string>
        .Bind(e => ValidatePassword(password)
            .Map(_ => e))                 // Either<string, string>
        .Bind(e => CreateUser(e, password)); // Either<string, User>
}

// ถ้า ValidateEmail ล้มเหลว → return Left ทันที ไม่รัน ValidatePassword
// ถ้า ValidatePassword ล้มเหลว → return Left ทันที ไม่รัน CreateUser
// ถ้าทุกอย่างสำเร็จ → return Right(user)
```

### 6.3.3 Either\<L, R\> Operations

**Map - Transform success value**

```csharp
// C# - Map success value only
Either<string, int> success = Right<string, int>(42);
Either<string, int> failure = Left<string, int>("Error");

var doubled = success.Map(x => x * 2);  // Right(84)
var still = failure.Map(x => x * 2);    // Left("Error") - ไม่รัน function
```

**MapLeft - Transform error value**

```csharp
// C# - Map error value only
Either<string, int> failure = Left<string, int>("not found");

var enhanced = failure.MapLeft(err => $"Error: {err}");
// Left("Error: not found")
```

**Bind / FlatMap - Chain operations**

```csharp
// C# - Chain Either operations
public Either<string, int> ParseInt(string s) { ... }
public Either<string, int> Divide(int a, int b)
{
    if (b == 0)
        return Left<string, int>("Division by zero");
    return Right<string, int>(a / b);
}

// Chain them
var result = ParseInt("10")
    .Bind(a => ParseInt("2")
        .Bind(b => Divide(a, b)));

// "10" → Right(10) → "2" → Right(2) → 10/2 → Right(5)
// "10" → Right(10) → "abc" → Left("...") → ไม่รัน Divide
```

```typescript
// TypeScript Effect-TS
function parseIntSafe(s: string): Either.Either<number, string> { /* ... */ }
function divide(a: number, b: number): Either.Either<number, string> {
  if (b === 0) {
    return Either.left("Division by zero");
  }
  return Either.right(a / b);
}

// Chain them
const result = pipe(
  parseIntSafe("10"),
  Either.flatMap(a => pipe(
    parseIntSafe("2"),
    Either.flatMap(b => divide(a, b))
  ))
);
```

### 6.3.4 Real-world Example: User Registration

```csharp
// C# language-ext - Complete registration flow
using LanguageExt;
using static LanguageExt.Prelude;

public record User(string Email, string PasswordHash);

public class UserRegistration
{
    // Validation functions
    public static Either<string, string> ValidateEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Left<string, string>("Email is required");

        if (!email.Contains("@"))
            return Left<string, string>("Email must contain @");

        return Right<string, string>(email);
    }

    public static Either<string, string> ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return Left<string, string>("Password is required");

        if (password.Length < 8)
            return Left<string, string>("Password must be at least 8 characters");

        return Right<string, string>(password);
    }

    public static Either<string, User> CreateUser(string email, string password)
    {
        // Hash password (simplified)
        var hash = $"hashed_{password}";
        return Right<string, User>(new User(email, hash));
    }

    // Main registration flow - Railway-Oriented Programming
    public static Either<string, User> Register(string email, string password)
    {
        return ValidateEmail(email)
            .Bind(_ => ValidatePassword(password))
            .Bind(_ => CreateUser(email, password));
    }

    // Alternative: LINQ syntax
    public static Either<string, User> RegisterLinq(string email, string password)
    {
        return
            from e in ValidateEmail(email)
            from p in ValidatePassword(password)
            from user in CreateUser(e, p)
            select user;
    }
}

// Usage
var result1 = UserRegistration.Register("alice@example.com", "password123");
// Right(User("alice@example.com", "hashed_password123"))

var result2 = UserRegistration.Register("invalid", "short");
// Left("Email must contain @") - หยุดที่ error แรก

var result3 = UserRegistration.Register("bob@example.com", "short");
// Left("Password must be at least 8 characters")

// Handle result
result1.Match(
    Left: error => Console.WriteLine($"Registration failed: {error}"),
    Right: user => Console.WriteLine($"Registered: {user.Email}")
);
```

```typescript
// TypeScript Effect-TS - Complete registration flow
import { Either, pipe } from "effect";

interface User {
  email: string;
  passwordHash: string;
}

// Validation functions
function validateEmail(email: string): Either.Either<string, string> {
  if (!email) {
    return Either.left("Email is required");
  }
  if (!email.includes("@")) {
    return Either.left("Email must contain @");
  }
  return Either.right(email);
}

function validatePassword(password: string): Either.Either<string, string> {
  if (!password) {
    return Either.left("Password is required");
  }
  if (password.length < 8) {
    return Either.left("Password must be at least 8 characters");
  }
  return Either.right(password);
}

function createUser(email: string, password: string): Either.Either<User, string> {
  const hash = `hashed_${password}`;
  return Either.right({ email, passwordHash: hash });
}

// Main registration flow
function register(email: string, password: string): Either.Either<User, string> {
  return pipe(
    validateEmail(email),
    Either.flatMap(() => validatePassword(password)),
    Either.flatMap(() => createUser(email, password))
  );
}

// Usage
const result1 = register("alice@example.com", "password123");
// Right({ email: "alice@example.com", passwordHash: "hashed_password123" })

const result2 = register("invalid", "short");
// Left("Email must contain @")

const result3 = register("bob@example.com", "short");
// Left("Password must be at least 8 characters")

// Handle result
Either.match(result1, {
  onLeft: (error) => console.log(`Registration failed: ${error}`),
  onRight: (user) => console.log(`Registered: ${user.email}`)
});
```

---

## 6.4 Validation\<Error, A\> - สำหรับ Multiple Errors

### 6.4.1 ปัญหาของ Either - หยุดที่ Error แรก

Either ใช้ **Monadic composition** ซึ่งหมายถึง "หยุดทันทีเมื่อเจอ error แรก":

```csharp
// C# - Either หยุดที่ error แรก
var result = ValidateEmail("invalid")     // ← Left("Email must contain @")
    .Bind(_ => ValidatePassword("short")) // ← ไม่รัน! เพราะ Left แล้ว
    .Bind(_ => CreateUser(...));          // ← ไม่รัน!

// result = Left("Email must contain @")
// ผู้ใช้ได้ error แค่ 1 ข้อ ต้องแก้แล้ว submit ใหม่เพื่อเจอ error ถัดไป
```

**ปัญหา:**
- ❌ User experience แย่ - ต้อง submit form หลายรอบ
- ❌ ไม่เห็นภาพรวม errors ทั้งหมด
- ❌ Inefficient - ต้อง validate ซ้ำหลายครั้ง

**สิ่งที่ต้องการ:** Validate **ทุกฟิลด์พร้อมกัน** แล้วรวบรวม **errors ทั้งหมด**

### 6.4.2 Validation\<Error, A\> - Applicative Validation

**Validation\<Error, A\>** คล้าย Either แต่ใช้ **Applicative composition** ซึ่งรวบรวม errors ทั้งหมด:

**ใน C# language-ext:**

```csharp
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

// Validation<FAIL, SUCCESS> มี 2 cases:
// - Success(value)  - สำเร็จ
// - Fail(errors)    - ล้มเหลว พร้อม Seq<Error>

// สร้าง Validation
Validation<Error, int> success = Success<Error, int>(42);
Validation<Error, int> failure = Fail<Error, int>(Error.New("Something wrong"));

// Pattern matching
var result = success.Match(
    Succ: value => $"Success: {value}",
    Fail: errors => $"Errors: {string.Join(", ", errors.Map(e => e.Message))}"
);
```

**ใน TypeScript - ไม่มี built-in Validation**

Effect-TS ไม่มี `Validation` type แยกต่างหาก แต่สามารถใช้ `Effect` กับ error accumulation ได้:

```typescript
// TypeScript - จะต้องใช้ libraries อื่น เช่น fp-ts
// หรือสร้าง custom validation logic
```

**หมายเหตุ:** ตัวอย่างต่อไปจะเน้น C# เพราะ language-ext มี Validation built-in

### 6.4.3 Applicative Validation Pattern

**Key Idea:** ใช้ `Apply` เพื่อรวม validations หลายตัว และรวบรวม errors:

```csharp
// C# - Validate multiple fields
public record CreateUserRequest(string Email, string Password, string Name);

public static Validation<Error, string> ValidateEmail(string email)
{
    if (string.IsNullOrWhiteSpace(email))
        return Fail<Error, string>(Error.New("Email is required"));
    if (!email.Contains("@"))
        return Fail<Error, string>(Error.New("Email must contain @"));
    return Success<Error, string>(email);
}

public static Validation<Error, string> ValidatePassword(string password)
{
    if (string.IsNullOrWhiteSpace(password))
        return Fail<Error, string>(Error.New("Password is required"));
    if (password.Length < 8)
        return Fail<Error, string>(Error.New("Password must be at least 8 characters"));
    return Success<Error, string>(password);
}

public static Validation<Error, string> ValidateName(string name)
{
    if (string.IsNullOrWhiteSpace(name))
        return Fail<Error, string>(Error.New("Name is required"));
    if (name.Length < 2)
        return Fail<Error, string>(Error.New("Name must be at least 2 characters"));
    return Success<Error, string>(name);
}

// ✅ Applicative - รวบรวม errors ทั้งหมด
public static Validation<Error, CreateUserRequest> ValidateRequest(
    string email, string password, string name)
{
    return (ValidateEmail(email), ValidatePassword(password), ValidateName(name))
        .Apply((e, p, n) => new CreateUserRequest(e, p, n))
        .As();
}

// Test cases
var result1 = ValidateRequest("alice@example.com", "password123", "Alice");
// Success(CreateUserRequest(...))

var result2 = ValidateRequest("invalid", "short", "A");
// Fail([
//   "Email must contain @",
//   "Password must be at least 8 characters",
//   "Name must be at least 2 characters"
// ])
// ← ได้ errors ทั้ง 3 ข้อพร้อมกัน!
```

**การทำงานของ Apply:**

```
ValidateEmail("invalid")     → Fail(["Email must contain @"])
ValidatePassword("short")    → Fail(["Password must be at least 8 characters"])
ValidateName("A")            → Fail(["Name must be at least 2 characters"])

Apply((e, p, n) => ...)      → Fail([...รวม errors ทั้ง 3...])
```

### 6.4.4 Real-world Example: Form Validation

```csharp
// C# language-ext - Complete form validation
using LanguageExt;
using LanguageExt.Common;
using static LanguageExt.Prelude;

public record RegisterForm(
    string Email,
    string Password,
    string ConfirmPassword,
    string Name,
    int Age
);

public record User(string Email, string PasswordHash, string Name, int Age);

public class FormValidation
{
    // Individual field validations
    public static Validation<Error, string> ValidateEmail(string email)
    {
        var errors = Seq<Error>();

        if (string.IsNullOrWhiteSpace(email))
            errors = errors.Add(Error.New("Email is required"));
        else if (!email.Contains("@"))
            errors = errors.Add(Error.New("Email must contain @"));
        else if (email.Length > 100)
            errors = errors.Add(Error.New("Email too long (max 100 characters)"));

        return errors.IsEmpty
            ? Success<Error, string>(email)
            : Fail<Error, string>(errors);
    }

    public static Validation<Error, string> ValidatePassword(string password)
    {
        var errors = Seq<Error>();

        if (string.IsNullOrWhiteSpace(password))
            errors = errors.Add(Error.New("Password is required"));
        else
        {
            if (password.Length < 8)
                errors = errors.Add(Error.New("Password must be at least 8 characters"));
            if (!password.Any(char.IsDigit))
                errors = errors.Add(Error.New("Password must contain a number"));
            if (!password.Any(char.IsUpper))
                errors = errors.Add(Error.New("Password must contain an uppercase letter"));
        }

        return errors.IsEmpty
            ? Success<Error, string>(password)
            : Fail<Error, string>(errors);
    }

    public static Validation<Error, string> ValidatePasswordMatch(
        string password, string confirmPassword)
    {
        return password == confirmPassword
            ? Success<Error, string>(password)
            : Fail<Error, string>(Error.New("Passwords do not match"));
    }

    public static Validation<Error, string> ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Fail<Error, string>(Error.New("Name is required"));
        if (name.Length < 2)
            return Fail<Error, string>(Error.New("Name must be at least 2 characters"));
        return Success<Error, string>(name);
    }

    public static Validation<Error, int> ValidateAge(int age)
    {
        if (age < 18)
            return Fail<Error, int>(Error.New("Must be at least 18 years old"));
        if (age > 120)
            return Fail<Error, int>(Error.New("Invalid age"));
        return Success<Error, int>(age);
    }

    // Combine all validations - Applicative composition
    public static Validation<Error, User> ValidateForm(RegisterForm form)
    {
        var emailVal = ValidateEmail(form.Email);
        var passwordVal = ValidatePassword(form.Password)
            .Bind(_ => ValidatePasswordMatch(form.Password, form.ConfirmPassword));
        var nameVal = ValidateName(form.Name);
        var ageVal = ValidateAge(form.Age);

        // Apply - รวบรวม errors ทั้งหมด
        return (emailVal, passwordVal, nameVal, ageVal)
            .Apply((email, password, name, age) =>
                new User(email, $"hashed_{password}", name, age))
            .As();
    }
}

// Usage - Valid form
var validForm = new RegisterForm(
    Email: "alice@example.com",
    Password: "Password123",
    ConfirmPassword: "Password123",
    Name: "Alice",
    Age: 25
);

var result1 = FormValidation.ValidateForm(validForm);
// Success(User(...))

// Usage - Invalid form
var invalidForm = new RegisterForm(
    Email: "invalid-email",
    Password: "short",
    ConfirmPassword: "different",
    Name: "A",
    Age: 15
);

var result2 = FormValidation.ValidateForm(invalidForm);
// Fail([
//   "Email must contain @",
//   "Password must be at least 8 characters",
//   "Password must contain a number",
//   "Password must contain an uppercase letter",
//   "Passwords do not match",
//   "Name must be at least 2 characters",
//   "Must be at least 18 years old"
// ])

// Display errors
result2.Match(
    Succ: user => Console.WriteLine($"Created user: {user.Email}"),
    Fail: errors =>
    {
        Console.WriteLine("Validation errors:");
        foreach (var error in errors)
            Console.WriteLine($"  - {error.Message}");
    }
);
```

**Output:**
```
Validation errors:
  - Email must contain @
  - Password must be at least 8 characters
  - Password must contain a number
  - Password must contain an uppercase letter
  - Passwords do not match
  - Name must be at least 2 characters
  - Must be at least 18 years old
```

### 6.4.5 Validation vs Either - เมื่อไหร่ใช้อะไร?

| Aspect | Either\<L, R\> | Validation\<Error, A\> |
|--------|--------------|----------------------|
| **Error handling** | หยุดที่ error แรก | รวบรวม errors ทั้งหมด |
| **Composition** | Monadic (Bind) | Applicative (Apply) |
| **Use case** | Business logic, workflows | Form validation, input validation |
| **User experience** | ต้อง submit ซ้ำ | เห็น errors ทั้งหมดทีเดียว |
| **Performance** | หยุดเร็ว (fail-fast) | Validate ทุกอย่าง |

**✅ ใช้ Either เมื่อ:**
- Workflow ที่ step ต่อไป depend on step ก่อนหน้า
- ต้องการ fail-fast (หยุดทันทีเมื่อเจอ error)
- Error แต่ละตัวต้องการ error type ที่ต่างกัน

```csharp
// Either - Sequential workflow
from user in GetUser(userId)           // ถ้าไม่เจอ user หยุดเลย
from order in GetOrder(orderId)        // ไม่ต้องเสีย effort query order
from payment in ProcessPayment(order)  // ไม่ต้อง charge card
select result;
```

**✅ ใช้ Validation เมื่อ:**
- Validate form input ที่ฟิลด์ต่างๆ independent กัน
- ต้องการ UX ที่ดี (แสดง errors ทุกฟิลด์พร้อมกัน)
- Error แต่ละตัวเป็น type เดียวกัน (Error)

```csharp
// Validation - Parallel validation
(ValidateEmail(email), ValidatePassword(password), ValidateName(name))
    .Apply((e, p, n) => new User(e, p, n))
    .As();
// ถ้า 3 ฟิลด์ผิดทั้งหมด → ได้ errors 3 ข้อพร้อมกัน
```

---

## 6.5 Fin\<A\> - สำหรับ Fallible Effects

### 6.5.1 Fin\<A\> คืออะไร?

**Fin\<A\>** (Finish) คือ result type ที่ได้จากการรัน effect ใน language-ext v5:

```csharp
// Fin<A> มี 2 cases:
// - Succ(value) - สำเร็จ
// - Fail(error) - ล้มเหลว พร้อม Error

// รัน effect ได้ Fin<A>
var runtime = new AppRuntime(services);
Fin<Todo> result = await TodoService<Eff<AppRuntime>, AppRuntime>
    .Get(id)
    .RunAsync(runtime, EnvIO.New(ct));

// Pattern matching
var response = result.Match(
    Succ: todo => Results.Ok(todo),
    Fail: error => Results.Problem(error.Message)
);
```

### 6.5.2 Fin\<A\> vs Either\<L, R\>

| Aspect | Fin\<A\> | Either\<L, R\> |
|--------|---------|--------------|
| **Context** | Result ของ effect ที่รันแล้ว | Pure computation |
| **Error type** | เสมอเป็น `Error` | Generic type L |
| **When** | หลัง `.RunAsync()` | ก่อนรัน effect |
| **Purpose** | Runtime result | Compile-time safety |

**ตัวอย่าง:**

```csharp
// Either - ก่อนรัน effect (compile-time)
public static K<M, Todo> Create(string title, string? description)
{
    // return K<M, Todo> - description ของ effect
    // ยังไม่ได้รัน, ยังไม่รู้ว่าจะสำเร็จหรือไม่
}

// Fin - หลังรัน effect (runtime)
var result = await TodoService.Create("Test", null).RunAsync(...);
// result: Fin<Todo> - รันแล้ว รู้แล้วว่าสำเร็จหรือล้มเหลว
```

### 6.5.3 Working with Fin\<A\>

**Match - Handle both cases**

```csharp
// Pattern matching
var result = await effect.RunAsync(runtime, envIO);
var response = result.Match(
    Succ: value => HandleSuccess(value),
    Fail: error => HandleError(error)
);
```

**IfFail - Provide default value**

```csharp
// ให้ default value ถ้าล้มเหลว
Fin<int> result = await effect.RunAsync(runtime, envIO);
int value = result.IfFail(0);  // 0 ถ้า Fail
```

**Bind / Map - Transform success value**

```csharp
// Transform value ถ้าสำเร็จ
Fin<Todo> todoResult = await GetTodo(id).RunAsync(...);
Fin<string> titleResult = todoResult.Map(todo => todo.Title);
```

**Extracting Error Information:**

```csharp
// Fin<A> wraps Error
Fin<Todo> result = await effect.RunAsync(runtime, envIO);

result.Match(
    Succ: todo => Console.WriteLine($"Success: {todo.Title}"),
    Fail: error =>
    {
        Console.WriteLine($"Code: {error.Code}");       // HTTP status code
        Console.WriteLine($"Message: {error.Message}"); // Error message
        Console.WriteLine($"IsExceptional: {error.IsExceptional}"); // Exception?
    }
);
```

---

## 6.6 Validation Patterns

### 6.6.1 Field-Level Validation

แยก validation แต่ละฟิลด์ออกเป็น functions:

```csharp
// C# - Field-level validators
public static class Validators
{
    // Email validation
    public static Validation<Error, string> Email(string email) =>
        string.IsNullOrWhiteSpace(email) ? Fail<Error, string>(Error.New("Email required")) :
        !email.Contains("@") ? Fail<Error, string>(Error.New("Invalid email")) :
        Success<Error, string>(email);

    // Required string
    public static Validation<Error, string> Required(string value, string fieldName) =>
        string.IsNullOrWhiteSpace(value)
            ? Fail<Error, string>(Error.New($"{fieldName} is required"))
            : Success<Error, string>(value);

    // Min length
    public static Validation<Error, string> MinLength(string value, int min, string fieldName) =>
        value.Length < min
            ? Fail<Error, string>(Error.New($"{fieldName} must be at least {min} characters"))
            : Success<Error, string>(value);

    // Max length
    public static Validation<Error, string> MaxLength(string value, int max, string fieldName) =>
        value.Length > max
            ? Fail<Error, string>(Error.New($"{fieldName} must not exceed {max} characters"))
            : Success<Error, string>(value);

    // Range
    public static Validation<Error, int> Range(int value, int min, int max, string fieldName) =>
        value < min || value > max
            ? Fail<Error, int>(Error.New($"{fieldName} must be between {min} and {max}"))
            : Success<Error, int>(value);
}
```

### 6.6.2 Combinator Validation

Combine หลาย validators เป็น validation pipeline:

```csharp
// C# - Combine validators
public static Validation<Error, string> ValidatePassword(string password)
{
    // Chain validations - ถ้าตัวใดตัวหนึ่งล้มเหลว จะรวบรวม errors
    var required = Validators.Required(password, "Password");
    var minLength = Validators.MinLength(password, 8, "Password");
    var hasDigit = password.Any(char.IsDigit)
        ? Success<Error, string>(password)
        : Fail<Error, string>(Error.New("Password must contain a digit"));
    var hasUpper = password.Any(char.IsUpper)
        ? Success<Error, string>(password)
        : Fail<Error, string>(Error.New("Password must contain uppercase"));

    // Apply - รวม validations
    return (required, minLength, hasDigit, hasUpper)
        .Apply((_, __, ___, ____) => password)
        .As();
}
```

### 6.6.3 Cross-Field Validation

Validate ข้อมูลที่ depend on หลายฟิลด์:

```csharp
// C# - Cross-field validation
public record DateRange(DateTime Start, DateTime End);

public static Validation<Error, DateRange> ValidateDateRange(DateTime start, DateTime end)
{
    var startVal = start != default
        ? Success<Error, DateTime>(start)
        : Fail<Error, DateTime>(Error.New("Start date is required"));

    var endVal = end != default
        ? Success<Error, DateTime>(end)
        : Fail<Error, DateTime>(Error.New("End date is required"));

    // Validate relationship
    var rangeVal = startVal.Bind(s => endVal.Bind(e =>
        s < e
            ? Success<Error, DateRange>(new DateRange(s, e))
            : Fail<Error, DateRange>(Error.New("End date must be after start date"))
    ));

    return rangeVal;
}
```

### 6.6.4 Domain-Driven Validation

สร้าง smart constructors ที่ validate ตอนสร้าง object:

```csharp
// C# - Smart constructor pattern
public record Email
{
    public string Value { get; }

    // Private constructor
    private Email(string value) => Value = value;

    // Smart constructor - static factory method
    public static Validation<Error, Email> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Fail<Error, Email>(Error.New("Email is required"));

        if (!value.Contains("@"))
            return Fail<Error, Email>(Error.New("Invalid email format"));

        if (value.Length > 100)
            return Fail<Error, Email>(Error.New("Email too long"));

        return Success<Error, Email>(new Email(value));
    }
}

// Usage
var emailResult = Email.Create("alice@example.com");
// Success(Email("alice@example.com"))

// ไม่สามารถสร้าง Email ผิดได้!
// var invalid = new Email("invalid"); // ← Compile error! Constructor is private
```

---

## 6.7 Error Handling Best Practices

### 6.7.1 เลือก Error Type ให้เหมาะสม

**Decision Tree:**

```
ค่าอาจไม่มี (optional value)?
  ├─ YES → Option<T>
  └─ NO → ดำเนินการต่อ

Operation อาจล้มเหลว?
  ├─ NO → T (plain value)
  └─ YES → ดำเนินการต่อ

ต้องการ error details?
  ├─ NO → Option<T>
  └─ YES → ดำเนินการต่อ

Validations independent กัน?
  ├─ YES → Validation<Error, T>
  └─ NO → Either<L, R>

กำลังรัน effect?
  ├─ YES → Fin<T>
  └─ NO → Either<L, R> หรือ Validation<Error, T>
```

### 6.7.2 Error Messages ที่ดี

**✅ DO:**
- เขียน error message ที่ชัดเจน เฉพาะเจาะจง
- บอกวิธีแก้ไข
- Include context (field name, expected value, etc.)

```csharp
// ✅ Good error messages
Error.New("Email is required");
Error.New("Password must be at least 8 characters (got 5)");
Error.New("Age must be between 18 and 120 (got 15)");
Error.New(404, $"User {userId} not found");
```

**❌ DON'T:**
- Error messages คลุมเครือ
- ไม่บอก context
- Technical jargon ที่ user ไม่เข้าใจ

```csharp
// ❌ Bad error messages
Error.New("Invalid input");              // ← อะไร invalid?
Error.New("Error");                      // ← ไม่มี context
Error.New("NullReferenceException");     // ← Technical
```

### 6.7.3 Error Codes

ใช้ error codes สำหรับ HTTP status codes:

```csharp
// C# - Error codes for HTTP
Error.New(400, "Validation failed");      // Bad Request
Error.New(404, "User not found");         // Not Found
Error.New(409, "Email already exists");   // Conflict
Error.New(500, "Database error");         // Internal Server Error

// ใช้ใน API layer
static IResult ToResult<A>(Fin<A> result, Func<A, IResult> onSuccess) =>
    result.Match(
        Succ: onSuccess,
        Fail: error =>
        {
            var statusCode = error.Code > 0 ? error.Code : 500;
            return Results.Problem(error.Message, statusCode: statusCode);
        }
    );
```

### 6.7.4 Logging Errors

Log errors ที่จุดที่เกิด แต่ handle ที่ caller:

```csharp
// C# - Log และ propagate error
public static K<M, User> GetUser<M, RT>(int id)
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, DatabaseIO>, Has<M, LoggerIO>
{
    return
        from _ in Logger<M, RT>.logInfo($"Getting user {id}")
        from userOpt in Database<M, RT>.getUserById(id)
        from user in userOpt.To<M, User>(() =>
        {
            // Log error ก่อน return
            Logger<M, RT>.logWarning($"User {id} not found");
            return Error.New(404, $"User {id} not found");
        })
        from __ in Logger<M, RT>.logInfo($"Found user: {user.Name}")
        select user;
}
```

### 6.7.5 Don't Swallow Errors

**❌ อย่าทำ:**

```csharp
// ❌ Swallow error - ไม่มีใครรู้ว่าเกิดอะไรขึ้น
try
{
    var result = DoSomething();
    return result;
}
catch
{
    return null;  // ← Lost error information!
}
```

**✅ ทำแบบนี้:**

```csharp
// ✅ Propagate error - ให้ caller handle
public Either<Error, Result> DoSomething()
{
    try
    {
        var result = PerformOperation();
        return Right<Error, Result>(result);
    }
    catch (Exception ex)
    {
        // Log แล้ว return error
        _logger.LogError(ex, "Operation failed");
        return Left<Error, Result>(Error.New(ex));
    }
}
```

---

## 6.8 สรุป

### สิ่งที่เรียนรู้ในบทนี้

1. **ปัญหาของ Exceptions** - ทำลาย type safety, ซับซ้อน, ไม่ composable
2. **Option\<T\>** - สำหรับค่าที่อาจไม่มี (optional values)
3. **Either\<L, R\>** - สำหรับ operations ที่อาจล้มเหลว พร้อม error details
4. **Validation\<Error, A\>** - สำหรับรวบรวม errors หลายตัว (form validation)
5. **Fin\<A\>** - Result จากการรัน effects
6. **Validation Patterns** - Field-level, Combinator, Cross-field, Domain-driven
7. **Best Practices** - เลือก type ให้เหมาะสม, error messages ที่ดี, logging

### Error Type Decision Guide

```
Option<T>              → ไม่มี error details, แค่ "มี/ไม่มี"
Either<L, R>           → มี error details, หยุดที่ error แรก
Validation<Error, A>   → รวบรวม errors ทั้งหมด
Fin<A>                 → Result จาก effect ที่รันแล้ว
```

### Key Takeaways

✅ **Type Safety** - Error handling เป็นส่วนหนึ่งของ type signature
✅ **Composability** - Chain operations ได้แบบ declarative
✅ **Explicit** - Compiler บังคับให้ handle errors
✅ **No Exceptions** - ไม่ต้องใช้ try-catch
✅ **Better UX** - Validation แสดง errors ทั้งหมดพร้อมกัน

---

## 6.9 แบบฝึกหัด

### แบบฝึกหัดที่ 1: Parse Configuration

สร้าง function ที่ parse configuration file และ validate ค่าต่างๆ:

```csharp
public record AppConfig(
    string DatabaseUrl,
    int MaxConnections,
    int TimeoutSeconds,
    Option<string> ApiKey
);

// TODO: Implement
public static Validation<Error, AppConfig> ParseConfig(Dictionary<string, string> config)
{
    // Hints:
    // 1. Parse DatabaseUrl (required, must start with "postgresql://")
    // 2. Parse MaxConnections (required, must be 1-1000)
    // 3. Parse TimeoutSeconds (required, must be 1-300)
    // 4. Parse ApiKey (optional)
    // 5. Use Apply to combine validations
}
```

### แบบฝึกหัดที่ 2: User Profile Update

สร้าง validation สำหรับ update user profile:

```csharp
public record UpdateProfileRequest(
    Option<string> Name,      // optional
    Option<string> Email,     // optional, validate ถ้ามี
    Option<int> Age           // optional, validate ถ้ามี
);

// TODO: Implement
public static Validation<Error, UpdateProfileRequest> ValidateUpdate(
    UpdateProfileRequest request)
{
    // Hints:
    // 1. Name ถ้ามี → ต้อง >= 2 characters
    // 2. Email ถ้ามี → ต้องมี @
    // 3. Age ถ้ามี → ต้อง 18-120
    // 4. ต้อง update อย่างน้อย 1 field
}
```

### แบบฝึกหัดที่ 3: Order Processing

Chain operations ด้วย Either สำหรับ order processing workflow:

```csharp
public record Order(int Id, int UserId, decimal Amount);
public record User(int Id, decimal Balance);
public record Payment(int OrderId, decimal Amount, DateTime ProcessedAt);

// TODO: Implement
public static Either<string, Payment> ProcessOrder(int orderId, int userId)
{
    // Hints:
    // 1. GetOrder(orderId) → Either<string, Order>
    // 2. GetUser(userId) → Either<string, User>
    // 3. CheckBalance(user, order) → Either<string, Unit>
    // 4. CreatePayment(order) → Either<string, Payment>
    // 5. Chain ด้วย Bind หรือ LINQ
}
```

### แบบฝึกหัดที่ 4: Smart Constructor

สร้าง smart constructor สำหรับ domain types:

```csharp
// TODO: Implement PositiveInt
public record PositiveInt
{
    public int Value { get; }

    private PositiveInt(int value) => Value = value;

    public static Validation<Error, PositiveInt> Create(int value)
    {
        // Validate value > 0
    }
}

// TODO: Implement NonEmptyString
public record NonEmptyString
{
    public string Value { get; }

    private NonEmptyString(string value) => Value = value;

    public static Validation<Error, NonEmptyString> Create(string value)
    {
        // Validate not null/empty/whitespace
    }
}

// TODO: Use them
public record Product(
    PositiveInt Id,
    NonEmptyString Name,
    PositiveInt Price
);

public static Validation<Error, Product> CreateProduct(int id, string name, int price)
{
    // Use smart constructors
}
```

---

**บทถัดไป:** [บทที่ 7: Testing Functional Code](chapter-07.md)

ในบทถัดไป เราจะเรียนรู้วิธี test code ที่เขียนแบบ Functional Programming!
