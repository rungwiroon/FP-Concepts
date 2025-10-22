# บทที่ 2: แนวคิดพื้นฐาน Functional Programming

> "ความเรียบง่ายคือความซับซ้อนสูงสุด" - Leonardo da Vinci

---

## 2.1 Pure Functions - หัวใจของ Functional Programming

### 2.1.1 คำนิยาม

**Pure Function** คือฟังก์ชันที่มีคุณสมบัติ 2 ข้อ:

1. **Deterministic** - Input เดียวกัน ให้ Output เดียวกันเสมอ
2. **No Side Effects** - ไม่เปลี่ยนแปลงสิ่งใดนอกฟังก์ชัน

### 2.1.2 ตัวอย่างเปรียบเทียบ

**ตัวอย่างที่ 1: การคำนวณราคา**

**Impure Function (C#):**
```csharp
// ❌ Impure - ผลลัพธ์ขึ้นกับ state ภายนอก
private decimal taxRate = 0.07m;

public decimal CalculatePrice(decimal price)
{
    return price * (1 + taxRate);  // ขึ้นกับ taxRate ที่เปลี่ยนได้
}

// ถ้า taxRate เปลี่ยน ผลลัพธ์ก็เปลี่ยนตาม!
taxRate = 0.10m;
var total = CalculatePrice(100);  // ผลลัพธ์ต่างจากเดิม
```

**Pure Function (C#):**
```csharp
// ✅ Pure - Input เดียวกัน Output เดียวกันเสมอ
public decimal CalculatePrice(decimal price, decimal taxRate)
{
    return price * (1 + taxRate);
}

// เรียกด้วย input เดียวกัน ได้ผลเดียวกันเสมอ
var total1 = CalculatePrice(100, 0.07m);  // 107
var total2 = CalculatePrice(100, 0.07m);  // 107 (เหมือนเดิม)
```

**ตัวอย่างที่ 2: การจัดการ Array**

**Impure Function (TypeScript):**
```typescript
// ❌ Impure - เปลี่ยน array เดิม (side effect)
function addItem(items: string[], item: string): string[] {
  items.push(item);  // แก้ไข array เดิม!
  return items;
}

const myItems = ['apple', 'banana'];
const result = addItem(myItems, 'orange');
console.log(myItems);  // ['apple', 'banana', 'orange'] - เปลี่ยนไปแล้ว!
```

**Pure Function (TypeScript):**
```typescript
// ✅ Pure - คืน array ใหม่ ไม่แก้เดิม
function addItem(items: string[], item: string): string[] {
  return [...items, item];  // สร้าง array ใหม่
}

const myItems = ['apple', 'banana'];
const result = addItem(myItems, 'orange');
console.log(myItems);  // ['apple', 'banana'] - ไม่เปลี่ยน!
console.log(result);   // ['apple', 'banana', 'orange'] - array ใหม่
```

### 2.1.3 ประโยชน์ของ Pure Functions

#### 1. **ทดสอบง่าย**

**Impure - ยากต่อการทดสอบ:**
```csharp
// ต้อง mock database, logger, datetime
public async Task<User> CreateUser(string email, string name)
{
    logger.Log($"Creating user: {email}");  // Side effect
    var user = new User
    {
        Email = email,
        Name = name,
        CreatedAt = DateTime.UtcNow  // ไม่ deterministic
    };
    await db.Users.AddAsync(user);  // Side effect
    return user;
}
```

**Pure - ง่ายต่อการทดสอบ:**
```csharp
// ไม่ต้อง mock อะไรเลย!
public User CreateUser(string email, string name, DateTime createdAt)
{
    return new User
    {
        Email = email,
        Name = name,
        CreatedAt = createdAt
    };
}

// Test
[Test]
public void CreateUser_WithValidInput_ReturnsUser()
{
    var user = CreateUser("john@example.com", "John",
        new DateTime(2024, 1, 1));

    Assert.Equal("john@example.com", user.Email);
    Assert.Equal("John", user.Name);
}
```

#### 2. **Memoization - Cache ผลลัพธ์ได้**

Pure functions สามารถ cache ผลลัพธ์ได้ เพราะ input เดียวกันให้ output เดียวกันเสมอ:

**ตัวอย่าง (TypeScript):**
```typescript
// Pure function - เหมาะกับ memoization
function fibonacci(n: number): number {
  if (n <= 1) return n;
  return fibonacci(n - 1) + fibonacci(n - 2);
}

// Memoized version - เร็วขึ้นมาก
const memoizedFib = memoize(fibonacci);
memoizedFib(40);  // คำนวณครั้งแรก ช้า
memoizedFib(40);  // ใช้ค่าจาก cache ทันที!
```

#### 3. **Parallel Execution - ทำงานพร้อมกันได้**

Pure functions ไม่มี side effects ดังนั้นสามารถรันพร้อมกันได้โดยไม่กลัว race condition:

**ตัวอย่าง (C#):**
```csharp
// Pure function - ปลอดภัยกับ parallel execution
public decimal CalculateDiscount(Order order, decimal discountRate)
{
    return order.Total * discountRate;
}

// สามารถประมวลผล orders หลายตัวพร้อมกันได้
var discounts = orders
    .AsParallel()  // ทำงานพร้อมกัน - ปลอดภัย!
    .Select(order => CalculateDiscount(order, 0.1m))
    .ToList();
```

#### 4. **Reasoning ง่าย - เข้าใจโค้ดได้ไว**

**Impure - ต้องตามว่า state เปลี่ยนยังไง:**
```typescript
let total = 0;

function addToTotal(amount: number) {
  total += amount;  // total เปลี่ยนเรื่อยๆ
  if (total > 1000) {
    total *= 0.9;   // ลด 10% ถ้าเกิน 1000
  }
}

addToTotal(500);  // total = 500
addToTotal(600);  // total = ? (ต้องคิด)
```

**Pure - เข้าใจทันที:**
```typescript
function calculateTotal(current: number, amount: number): number {
  const newTotal = current + amount;
  return newTotal > 1000 ? newTotal * 0.9 : newTotal;
}

const total1 = calculateTotal(0, 500);      // 500
const total2 = calculateTotal(total1, 600); // 990
// เห็นชัดเจนทันที ไม่ต้องตาม state
```

### 2.1.4 Side Effects ที่ควรหลีกเลี่ยง

Side effects ที่ทำให้ฟังก์ชันไม่ pure:

1. **เปลี่ยน state ภายนอก** - แก้ไขตัวแปร global, properties, arrays
2. **I/O Operations** - อ่าน/เขียนไฟล์, database, network
3. **เรียก impure functions** - Random, DateTime.Now, Console.WriteLine
4. **Throw exceptions** - เปลี่ยน control flow

**ตัวอย่าง Side Effects:**
```csharp
// ❌ Side Effects ต่างๆ
public class ImpureService
{
    private List<string> logs = new();

    public void ProcessOrder(Order order)
    {
        // Side effect 1: แก้ไข state
        logs.Add($"Processing: {order.Id}");

        // Side effect 2: I/O
        File.AppendAllText("log.txt", $"{DateTime.Now}: {order.Id}\n");

        // Side effect 3: Random
        if (Random.Shared.Next(10) > 5)
            order.Priority = "High";

        // Side effect 4: Exception
        if (order.Total < 0)
            throw new InvalidOperationException();
    }
}
```

### 2.1.5 จัดการ Side Effects ในโลกจริง

**คำถาม:** แล้ว application ที่ทำ I/O จริง จะเป็น pure ได้ไหม?

**คำตอบ:** แยก **pure business logic** ออกจาก **side effects**!

**แนวทาง 1: Dependency Injection**

```csharp
// Pure business logic
public User ValidateAndCreateUser(string email, string name, DateTime now)
{
    if (!email.Contains("@"))
        throw new ArgumentException("Invalid email");

    return new User { Email = email, Name = name, CreatedAt = now };
}

// Side effects อยู่ที่ outer layer
public async Task<User> CreateUserWithEffects(string email, string name)
{
    var user = ValidateAndCreateUser(email, name, DateTime.UtcNow);
    logger.Log($"Creating user: {email}");
    await db.Users.AddAsync(user);
    return user;
}
```

**แนวทาง 2: Effect System (จะเรียนในบทถัดไป)**

```csharp
// Business logic เป็น pure - แค่อธิบายว่าต้องการทำอะไร
public static K<M, User> CreateUser<M, RT>(string email, string name)
    where M : Monad<M>, MonadIO<M>
    where RT : Has<M, TimeIO>, Has<M, LoggerIO>, Has<M, DatabaseIO>
{
    return
        from now in Time<M, RT>.now()
        from user in M.Pure(new User { Email = email, Name = name, CreatedAt = now })
        from _ in Logger<M, RT>.logInfo("Creating user: {Email}", email)
        from saved in Database<M, RT>.addUser(user)
        select saved;
}
// ไม่มี side effect ตรงนี้ - แค่ "อธิบาย" ว่าจะทำอะไร
// Side effects เกิดตอน run effect ทีหลัง
```

---

## 2.2 Immutability - ข้อมูลที่ไม่เปลี่ยนแปลง

### 2.2.1 ทำไมต้อง Immutable?

**ปัญหาของ Mutable State:**

**ตัวอย่างที่ 1: Unexpected Changes (C#)**
```csharp
// ❌ Mutable - เกิดปัญหาได้ง่าย
public class Order
{
    public List<OrderItem> Items { get; set; } = new();
    public decimal Total { get; set; }
}

public void ProcessOrder(Order order)
{
    // ใครสักคนแก้ไข order โดยไม่รู้ตัว
    order.Items.Add(new OrderItem { Price = 100 });

    // Total ไม่ตรงกับ Items แล้ว!
    Console.WriteLine(order.Total);  // ค่าเก่า ยังไม่รวม item ใหม่
}
```

**ตัวอย่างที่ 2: Concurrent Modification (TypeScript)**
```typescript
// ❌ Mutable - race condition
let cart = { items: [], total: 0 };

async function addToCart(item: Item) {
  cart.items.push(item);  // Thread 1
  cart.total += item.price;  // Thread 2 อาจแก้พร้อมกัน!
}
```

### 2.2.2 Immutable Data Structures

**C# Records - Immutable by Design:**

```csharp
// ✅ Immutable record
public record Order(
    Guid Id,
    ImmutableList<OrderItem> Items,
    decimal Total,
    DateTime CreatedAt
);

// การเปลี่ยนแปลง = สร้างใหม่
var order1 = new Order(
    Guid.NewGuid(),
    ImmutableList.Create(item1, item2),
    200m,
    DateTime.UtcNow
);

// ใช้ 'with' เพื่อสร้าง copy พร้อมเปลี่ยนแปลง
var order2 = order1 with
{
    Items = order1.Items.Add(item3),
    Total = order1.Total + item3.Price
};

// order1 ไม่เปลี่ยน!
Console.WriteLine(order1.Items.Count);  // 2
Console.WriteLine(order2.Items.Count);  // 3
```

**TypeScript - Spread Operator:**

```typescript
// ✅ Immutable objects
interface Todo {
  readonly id: number;
  readonly title: string;
  readonly completed: boolean;
}

const todo1: Todo = {
  id: 1,
  title: 'Learn FP',
  completed: false
};

// สร้าง object ใหม่
const todo2: Todo = {
  ...todo1,
  completed: true
};

console.log(todo1.completed);  // false - ไม่เปลี่ยน!
console.log(todo2.completed);  // true
```

### 2.2.3 Working with Immutable Collections

**C# - ImmutableList:**

```csharp
using System.Collections.Immutable;

// สร้าง immutable list
var list1 = ImmutableList.Create(1, 2, 3);

// การเปลี่ยนแปลง = คืน list ใหม่
var list2 = list1.Add(4);                    // เพิ่มท้าย list
var list3 = list2.Remove(2);                 // ลบตัวเลข 2 ออก (by value)
var list4 = list2.RemoveAt(1);               // ลบ index ที่ 1 ออก (by index)

Console.WriteLine(list1.Count);  // 3 - ไม่เปลี่ยน
Console.WriteLine(list2.Count);  // 4 → [1, 2, 3, 4]
Console.WriteLine(list3.Count);  // 3 → [1, 3, 4]
Console.WriteLine(list4.Count);  // 3 → [1, 3, 4]
```

**TypeScript - Array Methods:**

```typescript
const numbers = [1, 2, 3];

// ✅ Immutable operations
const added = [...numbers, 4];           // [1, 2, 3, 4]
const removed = numbers.filter(n => n !== 2);  // [1, 3]
const mapped = numbers.map(n => n * 2);   // [2, 4, 6]

console.log(numbers);  // [1, 2, 3] - ไม่เปลี่ยน!

// ❌ Mutable operations - หลีกเลี่ยง
numbers.push(4);      // แก้ array เดิม
numbers.splice(1, 1); // แก้ array เดิม
```

### 2.2.4 Performance Considerations

**คำถาม:** Immutable ไม่ช้ากว่าเหรอ? ต้องสร้าง copy ทุกครั้ง

**คำตอบ:** ใช้ **Structural Sharing**!

**Structural Sharing (C#):**
```csharp
// ImmutableList ใช้ AVL tree (balanced tree)
// ไม่ได้ copy ทั้ง list แต่แชร์ส่วนที่ไม่เปลี่ยน
var list1 = ImmutableList.Create(1, 2, 3);
var list2 = list1.Add(4);  // แชร์ nodes เดิม + เพิ่ม path ใหม่

// Time complexity:
// Add(item): O(log n) - สร้าง path ใหม่ตามความสูงของ tree
// Get[index]: O(log n) - traverse tree ตาม index
// RemoveAt(index): O(log n) - รู้ตำแหน่งแล้ว ลบแล้วสร้าง tree ใหม่
// Remove(value): O(n) - ต้องค้นหาก่อนว่า value อยู่ที่ไหน + ลบ

// เทียบกับ List<T> ธรรมดา:
// Add: O(1) amortized (เร็วกว่า ImmutableList)
// Get[index]: O(1) (เร็วกว่า ImmutableList)
// RemoveAt: O(n) (ช้ากว่า ImmutableList เพราะต้อง shift elements)
// Remove(value): O(n) (ช้าพอๆ กัน)
```

**Structural Sharing แบบ Manual (TypeScript):**
```typescript
// Nested updates - ยาก!
const state = {
  user: {
    profile: {
      name: 'John',
      email: 'john@example.com'
    }
  }
};

// ❌ แก้ตรงๆ - mutable
state.user.profile.name = 'Jane';

// ✅ Immutable - ยาว!
const newState = {
  ...state,
  user: {
    ...state.user,
    profile: {
      ...state.user.profile,
      name: 'Jane'
    }
  }
};

// ใช้ library เช่น Immer ช่วย:
import produce from 'immer';

const newState2 = produce(state, draft => {
  draft.user.profile.name = 'Jane';  // เขียนเหมือน mutable แต่ได้ immutable!
});
```

---

## 2.3 Function Composition - ประกอบฟังก์ชัน

### 2.3.1 แนวคิด

**Function Composition** = เอาฟังก์ชันเล็กๆ มาต่อกันเป็นฟังก์ชันใหญ่

คล้ายกับการต่อท่อน้ำ:
```
Input → Function1 → Function2 → Function3 → Output
```

### 2.3.2 Compose vs Pipe

**Compose - ทำงานจากขวาไปซ้าย:**

**ตัวอย่าง (TypeScript):**
```typescript
// ฟังก์ชันพื้นฐาน
const trim = (s: string) => s.trim();
const toLowerCase = (s: string) => s.toLowerCase();
const exclaim = (s: string) => `${s}!`;

// Compose: ทำงาน exclaim(toLowerCase(trim(input)))
const compose = <A, B, C>(
  f: (b: B) => C,
  g: (a: A) => B
) => (a: A) => f(g(a));

const process = compose(
  compose(exclaim, toLowerCase),
  trim
);

process('  Hello World  ');  // "hello world!"
```

**Pipe - ทำงานจากซ้ายไปขวา (อ่านง่ายกว่า!):**

**ตัวอย่าง (TypeScript):**
```typescript
import { pipe } from 'fp-ts/function';

// Pipe: ทำงานตามลำดับที่เขียน
const result = pipe(
  '  Hello World  ',
  trim,
  toLowerCase,
  exclaim
);  // "hello world!"
```

### 2.3.3 LINQ Query Syntax (C#)

C# มี syntax พิเศษสำหรับ composition:

```csharp
// ตัวอย่าง: ดึง active users ที่มีอายุมากกว่า 18
// แบบ Method Chaining
var adults = users
    .Where(u => u.IsActive)
    .Where(u => u.Age >= 18)
    .Select(u => u.Name)
    .ToList();

// แบบ LINQ Query Syntax - อ่านง่ายกว่า!
var adults =
    from u in users
    where u.IsActive
    where u.Age >= 18
    select u.Name;
```

**กับ language-ext:**
```csharp
// Compose operations กับ Monads
var result =
    from user in GetUser(userId)
    from orders in GetOrders(user.Id)
    from validated in ValidateOrders(orders)
    from saved in SaveOrders(validated)
    select saved;

// ทุก step คืน K<M, A> และ compose กันได้ธรรมชาติ
```

### 2.3.4 Practical Example - Data Pipeline

**Scenario:** ประมวลผล order data

**แบบ Imperative (TypeScript):**
```typescript
function processOrders(orders: Order[]): ProcessedOrder[] {
  const result: ProcessedOrder[] = [];

  for (const order of orders) {
    if (order.status === 'pending') {
      const validated = validateOrder(order);
      if (validated.isValid) {
        const enriched = enrichWithCustomerData(validated.order);
        const calculated = calculateTotals(enriched);
        result.push(calculated);
      }
    }
  }

  return result;
}
```

**แบบ Functional Composition (TypeScript):**
```typescript
import { pipe } from 'fp-ts/function';
import * as A from 'fp-ts/Array';
import * as O from 'fp-ts/Option';

const processOrders = (orders: Order[]): ProcessedOrder[] =>
  pipe(
    orders,
    A.filter(order => order.status === 'pending'),
    A.filterMap(order => pipe(
      validateOrder(order),
      O.map(enrichWithCustomerData),
      O.map(calculateTotals)
    ))
  );
```

---

## 2.4 Algebraic Data Types

### 2.4.1 Option<T> - การจัดการค่าที่อาจไม่มี

**ปัญหาของ null:**

**C# - Null Reference Exception:**
```csharp
// ❌ อันตราย!
User user = await db.Users.FindAsync(userId);
string email = user.Email;  // 💥 NullReferenceException ถ้า user เป็น null
```

**TypeScript - undefined:**
```typescript
// ❌ อันตราย!
const user = users.find(u => u.id === userId);
const email = user.email;  // 💥 TypeError ถ้า user เป็น undefined
```

**แก้ไขด้วย Option<T>:**

**C# + language-ext:**
```csharp
using LanguageExt;
using static LanguageExt.Prelude;

// ✅ Type-safe
Option<User> user = await db.Users.FindAsync(userId).ToOption();

// ต้อง handle ทั้ง Some และ None
string email = user.Match(
    Some: u => u.Email,
    None: () => "no-reply@example.com"
);
```

**TypeScript + fp-ts:**
```typescript
import * as O from 'fp-ts/Option';
import { pipe } from 'fp-ts/function';

// ✅ Type-safe
const user: O.Option<User> = O.fromNullable(
  users.find(u => u.id === userId)
);

// ต้อง handle ทั้ง Some และ None
const email = pipe(
  user,
  O.match(
    () => 'no-reply@example.com',
    (u) => u.email
  )
);
```

**Operations with Option:**

```csharp
// Map: แปลงค่าภายใน Option
Option<int> length = userOpt.Map(u => u.Name.Length);

// Bind (FlatMap): Compose options
Option<Order> orderOpt = userOpt.Bind(u => GetLatestOrder(u.Id));

// Filter: กรองตามเงื่อนไข
Option<User> adult = userOpt.Filter(u => u.Age >= 18);
```

### 2.4.2 Either<L, R> - Error Handling

**Either** แทน exceptions ด้วย type-safe errors:

**C# + language-ext:**
```csharp
using LanguageExt;

// ✅ Type-safe error handling
public static Either<Error, User> CreateUser(string email, string name)
{
    if (string.IsNullOrEmpty(email))
        return Error.New("Email is required");

    if (!email.Contains("@"))
        return Error.New("Invalid email format");

    return new User { Email = email, Name = name };
}

// ใช้งาน
var result = CreateUser("john@example.com", "John");
result.Match(
    Right: user => Console.WriteLine($"Created: {user.Email}"),
    Left: error => Console.WriteLine($"Error: {error.Message}")
);
```

**TypeScript + fp-ts:**
```typescript
import * as E from 'fp-ts/Either';

// ✅ Type-safe error handling
function createUser(
  email: string,
  name: string
): E.Either<Error, User> {
  if (!email) {
    return E.left(new Error('Email is required'));
  }

  if (!email.includes('@')) {
    return E.left(new Error('Invalid email format'));
  }

  return E.right({ email, name });
}

// ใช้งาน
const result = createUser('john@example.com', 'John');
pipe(
  result,
  E.match(
    (error) => console.log(`Error: ${error.message}`),
    (user) => console.log(`Created: ${user.email}`)
  )
);
```

### 2.4.3 Validation<E, A> - Error Accumulation

**ปัญหาของ Either:** หยุดที่ error แรก

**Validation:** รวบรวม errors ทั้งหมด!

**C# + language-ext:**
```csharp
using LanguageExt;
using static LanguageExt.Prelude;

public record CreateUserRequest(string Email, string Name, int Age);

public static Validation<Error, string> ValidateEmail(string email) =>
    string.IsNullOrEmpty(email) ? Fail<Error, string>("Email is required")
  : !email.Contains("@") ? Fail<Error, string>("Invalid email format")
  : Success<Error, string>(email);

public static Validation<Error, string> ValidateName(string name) =>
    string.IsNullOrEmpty(name) ? Fail<Error, string>("Name is required")
  : name.Length < 2 ? Fail<Error, string>("Name too short")
  : Success<Error, string>(name);

public static Validation<Error, int> ValidateAge(int age) =>
    age < 0 ? Fail<Error, int>("Age cannot be negative")
  : age > 150 ? Fail<Error, int>("Age too high")
  : Success<Error, int>(age);

// Applicative validation - รวม errors ทั้งหมด!
public static Validation<Error, CreateUserRequest> ValidateUser(
    string email, string name, int age) =>
    (ValidateEmail(email), ValidateName(name), ValidateAge(age))
        .Apply((e, n, a) => new CreateUserRequest(e, n, a))
        .As();

// ใช้งาน
var result = ValidateUser("", "J", -5);
result.Match(
    Succ: user => Console.WriteLine("Valid!"),
    Fail: errors => errors.Iter(e => Console.WriteLine(e.Message))
    // Output:
    // Email is required
    // Name too short
    // Age cannot be negative
);
```

---

## 2.5 Monads - อธิบายแบบเข้าใจง่าย

### 2.5.1 Monad คืออะไร?

**คำจำกัดความทางคณิตศาสตร์:** (ข้ามไปก่อน ไม่สำคัญตอนนี้)

**คำอธิบายแบบเข้าใจง่าย:**

> Monad คือ "กล่อง" ที่ใส่ค่าไว้ และมีวิธีการ "ต่อกล่อง" ให้เป็นลูกโซ่

**ตัวอย่าง:**
- `Option<T>` = กล่องที่อาจมีค่า หรือ ไม่มีค่า
- `Either<E, A>` = กล่องที่มีค่า หรือ error
- `List<T>` = กล่องที่มีหลายค่า
- `Task<T>` = กล่องที่จะมีค่าในอนาคต

### 2.5.2 Monad Laws (สั้นๆ)

Monad ต้องมี:
1. **Return** (Pure) - ใส่ค่าเข้ากล่อง
2. **Bind** (FlatMap, >>=) - ต่อกล่องเข้าด้วยกัน

**ตัวอย่าง Option Monad:**
```csharp
// Return: ใส่ค่าเข้า Option
Option<int> x = Some(5);

// Bind: ต่อ operations
Option<int> result = x.Bind(val => Some(val * 2));
```

### 2.5.3 ทำไมต้องใช้ Monad?

**ปัญหา: Nested Error Handling**

**แบบไม่ใช้ Monad (C#):**
```csharp
public User GetUserWithOrders(int userId)
{
    User user = GetUser(userId);
    if (user == null)
        return null;

    List<Order> orders = GetOrders(user.Id);
    if (orders == null)
        return null;

    user.Orders = orders;

    Address address = GetAddress(user.Id);
    if (address == null)
        return null;

    user.Address = address;
    return user;
}
// Nested null checks ทุกที่!
```

**แบบใช้ Monad:**
```csharp
public Option<User> GetUserWithOrders(int userId) =>
    from user in GetUser(userId)
    from orders in GetOrders(user.Id)
    from address in GetAddress(user.Id)
    select user with { Orders = orders, Address = address };
// สั้น ชัดเจน ไม่มี nested ifs!
```

### 2.5.4 Monad ใน language-ext และ Effect-TS

**language-ext - K<M, A>:**
```csharp
// K<M, A> = Higher-Kinded Type representation
// M = Monad type (Option, Either, Eff, etc.)
// A = Value type

public static K<M, int> Double<M>(K<M, int> ma)
    where M : Monad<M>
    => ma.Map(x => x * 2);

// ใช้ได้กับทุก Monad!
Option<int> opt = Some(5);
K<Option, int> doubled = Double(opt);  // Some(10)
```

**Effect-TS - Effect<A, E, R>:**
```typescript
import { Effect } from 'effect';

// Effect<A, E, R>
// A = Success type
// E = Error type
// R = Requirements (dependencies)

const program = Effect.gen(function* (_) {
  const user = yield* _(getUser(userId));
  const orders = yield* _(getOrders(user.id));
  return { user, orders };
});
```

---

## 2.6 Error Handling แบบ Functional

### 2.6.1 ปัญหาของ Exceptions

**Exceptions มีปัญหา:**

1. **ไม่เห็นใน type signature**
```csharp
// ฟังก์ชันนี้อาจโยน exception อะไรบ้าง? ไม่รู้!
public User GetUser(int id)
{
    // อาจโยน NotFoundException, DatabaseException, ...
}
```

2. **ทำลาย control flow**
```csharp
var user = GetUser(1);
var order = GetOrder(user.Id);  // ถ้า GetUser โยน exception บรรทัดนี้ไม่ทำงาน
```

3. **ยากต่อการ compose**
```csharp
// ไม่สามารถ chain ได้ถ้ามี exception
var result = GetUser(1)
    .ProcessOrders()  // ถ้า GetUser throw จะไม่ถึงบรรทัดนี้
    .CalculateTotal();
```

### 2.6.2 Railway Oriented Programming

**แนวคิด:** แทน exceptions ด้วย Either - มี 2 ทาง "สำเร็จ" หรือ "ล้มเหลว"

```
Input ─→ Step1 ─→ Step2 ─→ Step3 ─→ Success
          │        │        │
          ↓        ↓        ↓
        Error    Error    Error
```

**ตัวอย่าง (C#):**
```csharp
public Either<Error, Order> ProcessOrder(int orderId) =>
    from order in GetOrder(orderId)           // อาจ Left(Error)
    from validated in ValidateOrder(order)    // อาจ Left(Error)
    from payment in ProcessPayment(validated) // อาจ Left(Error)
    from saved in SaveOrder(payment)          // อาจ Left(Error)
    select saved;                             // Right(Order)

// ถ้าขั้นตอนไหนล้มเหลว จะหยุดทันที และคืน Error
// ไม่ต้อง try-catch!
```

**ตัวอย่าง (TypeScript):**
```typescript
import * as E from 'fp-ts/Either';
import { pipe } from 'fp-ts/function';

const processOrder = (orderId: number): E.Either<Error, Order> =>
  pipe(
    getOrder(orderId),
    E.chain(validateOrder),
    E.chain(processPayment),
    E.chain(saveOrder)
  );
```

### 2.6.3 Error Types

**แทนที่ generic Error ด้วย specific types:**

**C#:**
```csharp
// ✅ Type-safe errors
public abstract record OrderError
{
    public record NotFound(int OrderId) : OrderError;
    public record InvalidAmount(decimal Amount) : OrderError;
    public record PaymentFailed(string Reason) : OrderError;
}

public Either<OrderError, Order> ProcessOrder(int orderId) =>
    from order in GetOrder(orderId)  // อาจคืน NotFound
    from validated in ValidateOrder(order)  // อาจคืน InvalidAmount
    from payment in ProcessPayment(validated)  // อาจคืน PaymentFailed
    select payment;

// ใช้งาน - handle ทุก case
result.Match(
    Right: order => Console.WriteLine($"Success: {order.Id}"),
    Left: error => error switch
    {
        OrderError.NotFound nf => Console.WriteLine($"Order {nf.OrderId} not found"),
        OrderError.InvalidAmount ia => Console.WriteLine($"Invalid amount: {ia.Amount}"),
        OrderError.PaymentFailed pf => Console.WriteLine($"Payment failed: {pf.Reason}"),
        _ => Console.WriteLine("Unknown error")
    }
);
```

---

## 2.7 สรุป

### แนวคิดพื้นฐาน Functional Programming ที่ต้องรู้:

| แนวคิด | คำอธิบาย | ประโยชน์ |
|--------|----------|----------|
| **Pure Functions** | ไม่มี side effects, deterministic | ทดสอบง่าย, reasoning ง่าย |
| **Immutability** | ข้อมูลไม่เปลี่ยนแปลง | หลีกเลี่ยง bugs, thread-safe |
| **Composition** | ต่อฟังก์ชันเล็กเป็นใหญ่ | Code reuse, modularity |
| **Option<T>** | แทน null | Type-safe, ไม่มี null exceptions |
| **Either<E, A>** | แทน exceptions | Error เป็น values, composable |
| **Validation<E, A>** | รวม errors | User-friendly error messages |
| **Monads** | Pattern สำหรับ composition | จัดการ effects ได้ดี |

### เตรียมพร้อมสำหรับบทถัดไป

ในบทที่ 3 เราจะเริ่มใช้ **language-ext v5** สร้าง Backend จริง โดยใช้:
- `K<M, A>` - Higher-Kinded Types
- `Has<M, RT, T>.ask` - Trait-based capabilities
- `Eff<RT>` - Effect monad
- `Validation` - Error accumulation

และในบทที่ 8 เราจะใช้ **Effect-TS** สร้าง Frontend ด้วย:
- `Effect<A, E, R>` - Effect system
- `Context.Tag` - Dependency injection
- `Effect.gen` - Generator syntax

---

## แบบฝึกหัดท้ายบท

### ข้อ 1: Pure Functions
```csharp
// ฟังก์ชันนี้ pure หรือไม่? ทำไม?
public int Calculate(int x)
{
    Console.WriteLine($"Calculating: {x}");
    return x * 2;
}
```

### ข้อ 2: Immutability
```typescript
// แก้ไขโค้ดนี้ให้เป็น immutable
function updateUserAge(user: User, newAge: number): User {
  user.age = newAge;
  return user;
}
```

### ข้อ 3: Option
```csharp
// เขียนฟังก์ชันที่คืน Option<User> แทน null
public User? FindUserByEmail(string email)
{
    return users.FirstOrDefault(u => u.Email == email);
}
```

### ข้อ 4: Either
```typescript
// เขียนฟังก์ชัน divide ที่คืน Either<Error, number>
function divide(a: number, b: number): number {
  if (b === 0) throw new Error("Division by zero");
  return a / b;
}
```

### ข้อ 5: Composition
```csharp
// ใช้ LINQ compose ฟังก์ชันเหล่านี้:
Option<User> GetUser(int id);
Option<Order> GetLatestOrder(int userId);
decimal CalculateTotal(Order order);

// เป้าหมาย: เขียนฟังก์ชันที่รับ userId คืน Option<decimal>
```

---

**พร้อมแล้วใช่ไหม?** ในบทถัดไปเราจะเริ่มเขียน Backend จริงด้วย **language-ext v5**!
