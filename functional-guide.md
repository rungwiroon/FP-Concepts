# Functional ASP.NET Core with Language-Ext version 5

A comprehensive guide to building ASP.NET Core applications using functional programming patterns with the language-ext library.

## Table of Contents

- [Overview](#overview)
- [Setup](#setup)
- [Core Architecture](#core-architecture)
- [The Db Monad](#the-db-monad)
- [Effect Composition](#effect-composition)
- [Optional Dependencies](#optional-dependencies)
- [Effect Extensions](#effect-extensions)
- [Testing Strategy](#testing-strategy)
- [Best Practices](#best-practices)
- [Common Patterns](#common-patterns)

---

## Overview

This architecture uses monad transformers to compose multiple effects in a type-safe, functional way:

- **Database operations** via Entity Framework Core
- **Logging** via ILogger
- **HTTP calls** via HttpClient (optional)
- **Cancellation** via CancellationToken
- **Validation** with error accumulation
- **Transactions** with automatic commit/rollback
- **Caching** with graceful fallback
- **Metrics** for observability

### Key Benefits

✅ **Single abstraction** for all effects  
✅ **Composable** - stack effects like LEGO blocks  
✅ **Type-safe** - compiler ensures correctness  
✅ **Testable** - easy to mock dependencies  
✅ **Declarative** - clear intent, less boilerplate  
✅ **Error handling** - automatic short-circuiting  
✅ **Cancellation** - propagates through all operations  

---

## Setup

### 1. Install NuGet Packages

```bash
dotnet add package LanguageExt.Core
dotnet add package Microsoft.EntityFrameworkCore.InMemoryDatabase
```

### 2. Project Structure

```
YourProject/
├── Program.cs              # API endpoints
├── Domain/
│   └── Product.cs          # Domain entities
├── Data/
│   └── AppDbContext.cs     # EF Core context
├── Infrastructure/
│   ├── DbMonad.cs          # Db monad implementation
│   ├── DbEnv.cs            # Environment record
│   └── Extensions/
│       ├── LoggingExtensions.cs
│       ├── HttpExtensions.cs
│       ├── TransactionExtensions.cs
│       └── CacheExtensions.cs
└── Features/
    └── Products/
        ├── ProductRepository.cs
        └── ProductValidation.cs
```

---

## Core Architecture

### The DbEnv Record

The environment holds all dependencies needed for operations:

```csharp
record DbEnv(
    AppDbContext DbContext,                      // Required
    CancellationToken CancellationToken,         // Required
    ILogger Logger,                              // Required
    IHttpClientFactory? HttpClientFactory = null // Optional
);
```

**Helper methods for creating DbEnv:**

```csharp
static class DbEnvExtensions
{
    // Create without optional dependencies
    public static DbEnv Create(
        AppDbContext ctx, 
        CancellationToken ct, 
        ILogger logger) =>
        new DbEnv(ctx, ct, logger, null);
    
    // Fluent addition of optional dependencies
    public static DbEnv WithHttp(
        this DbEnv env,
        IHttpClientFactory httpFactory) =>
        env with { HttpClientFactory = httpFactory };
}
```

### The Db Monad

```csharp
record Db<A>(ReaderT<DbEnv, IO, A> RunDb) : K<Db, A>
{
    public Db<B> Select<B>(Func<A, B> m) => 
        this.Kind().Select(m).As();
    
    public Db<C> SelectMany<B, C>(Func<A, K<Db, B>> bind, Func<A, B, C> project) =>
        this.Kind().SelectMany(bind, project).As();
}
```

**Monad implementation:**

```csharp
abstract partial class Db : Monad<Db>, Readable<Db, DbEnv>, Fallible<Db>
{
    public static K<Db, B> Map<A, B>(Func<A, B> f, K<Db, A> ma) =>
        new Db<B>(ma.As().RunDb.Map(f).As());

    public static K<Db, A> Pure<A>(A value) =>
        new Db<A>(ReaderT<DbEnv, IO, A>.Pure(value));

    public static K<Db, B> Bind<A, B>(K<Db, A> ma, Func<A, K<Db, B>> f) =>
        new Db<B>(ma.Run().Bind(x => f(x).Run()).As());

    public static K<Db, A> Fail<A>(Error error) =>
        new Db<A>(ReaderT.liftIO<DbEnv, IO, A>(IO<A>.Fail(error)));

    // Lifting operations
    public static K<Db, A> LiftIO<A>(Task<A> task) =>
        new Db<A>(ReaderT.liftIO<DbEnv, IO, A>(
            IO.liftAsync(async _ => await task.ConfigureAwait(false))));

    public static K<Db, A> Lift<A>(Validation<Error, A> validation) =>
        new Db<A>(ReaderT<DbEnv, IO, A>.Lift(
            validation.Match(
                Succ: IO<A>.Pure,
                Fail: IO<A>.Fail)));
}
```

### Accessing Dependencies

```csharp
abstract partial class Db
{
    public static Db<DbEnv> Ask() =>
        Readable.ask<Db, DbEnv>().As();

    public static Db<TContext> Ctx<TContext>() where TContext : DbContext =>
        Ask().Select(env => (env.DbContext as TContext)!);

    public static Db<CancellationToken> CancellationToken() =>
        Ask().Select(env => env.CancellationToken);

    public static Db<ILogger> Logger() =>
        Ask().Select(env => env.Logger);

    // Optional: Returns Option<HttpClient>
    public static Db<Option<HttpClient>> HttpClientOpt() =>
        Ask().Select(env => env.HttpClientFactory != null
            ? Some(env.HttpClientFactory.CreateClient())
            : Option<HttpClient>.None);

    // Required: Fails if not available
    public static Db<HttpClient> HttpClient() =>
        from opt in HttpClientOpt()
        from client in opt.ToFail("HttpClient not available")
        select client;

    public static Db<Unit> Save() =>
        from env in Ask()
        from _ in LiftIO(() => env.DbContext.SaveChangesAsync(env.CancellationToken))
        select unit;
}
```

---

## Effect Composition

### Basic Repository Pattern

```csharp
static class ProductRepository
{
    public static K<Db, List<Product>> List() =>
        from ctx in Db.Ctx<AppDbContext>()
        from ct in Db.CancellationToken()
        from products in Db.LiftIO(ctx.Products.ToListAsync(ct))
        select products;

    public static K<Db, Product> Get(int id) =>
        from ctx in Db.Ctx<AppDbContext>()
        from ct in Db.CancellationToken()
        from product in Db.LiftIO(ctx.Products.FindAsync(new object[] { id }, ct).AsTask())
        from _ in guard(notnull(product), DbError.NotFound)
        select product;

    public static K<Db, Unit> Add(Product product) =>
        from validated in Db.Lift(ProductValidation.Validate(product))
        from ctx in Db.Ctx<AppDbContext>()
        from ct in Db.CancellationToken()
        from _ in Db.LiftIO(ctx.AddAsync(validated, ct).AsTask())
        from __ in Db.Save()
        select unit;
}
```

### Validation with Error Accumulation

```csharp
static class ProductValidation
{
    public static Validation<Error, Product> Validate(Product p) =>
        (ValidateId(p), ValidateName(p), ValidatePrice(p))
            .Apply((_, _, _) => p);

    static Validation<Error, Product> ValidateId(Product p) =>
        p.Id > 0
            ? Success<Error, Product>(p)
            : Fail<Error, Product>(ValidationError.InvalidId);

    static Validation<Error, Product> ValidateName(Product p) =>
        !string.IsNullOrWhiteSpace(p.Name)
            ? Success<Error, Product>(p)
            : Fail<Error, Product>(ValidationError.InvalidName);

    static Validation<Error, Product> ValidatePrice(Product p) =>
        p.Price > 0
            ? Success<Error, Product>(p)
            : Fail<Error, Product>(ValidationError.InvalidPrice);
}
```

---

## Optional Dependencies

### When to Use Optional Dependencies

Make a dependency optional when:
- Not all endpoints need it
- You want cleaner test setups
- Performance matters (avoid overhead)

### Pattern for Optional Dependencies

**1. Define as nullable with default:**
```csharp
record DbEnv(
    AppDbContext DbContext,
    CancellationToken CancellationToken,
    ILogger Logger,
    IHttpClientFactory? HttpClientFactory = null  // Optional
);
```

**2. Provide two access methods:**
```csharp
// Returns Option - graceful if missing
public static Db<Option<HttpClient>> HttpClientOpt() =>
    Ask().Select(env => Optional(env.HttpClientFactory?.CreateClient()));

// Fails if missing - explicit requirement
public static Db<HttpClient> HttpClient() =>
    from opt in HttpClientOpt()
    from client in opt.ToFail("HttpClient not configured")
    select client;
```

**3. Use in operations:**
```csharp
// Graceful degradation
public static K<Db, EnrichedProduct> GetEnriched(int id) =>
    from product in Get(id)
    from clientOpt in Db.HttpClientOpt()
    from externalInfo in clientOpt.Match(
        Some: c => FetchExternalInfo(c, product),
        None: () => Db.Pure<ExternalInfo?>(null))
    select new EnrichedProduct(product, externalInfo);

// Explicit requirement
public static K<Db, Data> FetchFromApi(string url) =>
    from client in Db.HttpClient()  // Fails if not available
    from data in CallApi(client, url)
    select data;
```

**4. Declare in endpoint signatures:**
```csharp
// Without HTTP
app.MapGet("/products", async (
    AppDbContext ctx, 
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    var env = DbEnvExtensions.Create(ctx, ct, logger);
    // ...
});

// With HTTP
app.MapGet("/products/{id}/enrich", async (
    int id,
    AppDbContext ctx,
    ILogger<Program> logger,
    IHttpClientFactory httpFactory,  // Explicit dependency
    CancellationToken ct) =>
{
    var env = new DbEnv(ctx, ct, logger, httpFactory);
    // ...
});
```

---

## Effect Extensions

Effect extensions are the key to composability. They wrap operations with cross-cutting concerns.

### Logging Extension

```csharp
static class LoggingExtensions
{
    public static K<Db, Unit> LogInfo(string message) =>
        from logger in Db.Logger()
        from _ in Db.LiftIO(() =>
        {
            logger.LogInformation(message);
            return Task.FromResult(unit);
        })
        select unit;

    // Wrapper for automatic start/end logging
    public static K<Db, A> WithLogging<A>(
        this K<Db, A> operation,
        string startMessage,
        Func<A, string> successMessage)
    {
        return
            from _ in LogInfo(startMessage)
            from result in operation
            from __ in LogInfo(successMessage(result))
            select result;
    }
}

// Usage
ProductRepository.List()
    .WithLogging(
        "Fetching all products",
        products => $"Retrieved {products.Count} products");
```

### Transaction Extension

```csharp
static class TransactionExtensions
{
    public static K<Db, A> WithTransaction<A>(this K<Db, A> operation)
    {
        return
            from _ in LogInfo("Starting transaction")
            from ctx in Db.Ctx<AppDbContext>()
            from ct in Db.CancellationToken()
            from result in ExecuteInTransaction(operation, ctx, ct)
            select result;
    }

    private static K<Db, A> ExecuteInTransaction<A>(
        K<Db, A> operation,
        AppDbContext ctx,
        CancellationToken ct)
    {
        return Db.LiftIO(async () =>
        {
            await using var transaction = await ctx.Database.BeginTransactionAsync(ct);
            
            try
            {
                var env = await Db.Ask().Run(default!).RunAsync();
                var result = await operation.Run(env).RunAsync();
                
                return await result.Match(
                    Succ: async value =>
                    {
                        await transaction.CommitAsync(ct);
                        await LogInfo("✓ Transaction committed").Run(env).RunAsync();
                        return Fin<A>.Succ(value);
                    },
                    Fail: async error =>
                    {
                        await transaction.RollbackAsync(ct);
                        await LogError($"✗ Rolled back: {error.Message}").Run(env).RunAsync();
                        return Fin<A>.Fail(error);
                    }
                );
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                return Fin<A>.Fail(Error.New(ex));
            }
        }).Bind(identity);
    }
}

// Usage
ProductRepository.BulkUpdate(updates)
    .WithTransaction()  // Wraps entire operation
    .WithMetrics("BulkUpdate");
```

### Cache Extension

```csharp
static class CacheExtensions
{
    private static readonly Dictionary<string, object> _cache = new();

    public static K<Db, A> WithCache<A>(
        this K<Db, A> operation,
        string cacheKey,
        TimeSpan expiration)
    {
        return
            from _ in LogInfo($"Checking cache: {cacheKey}")
            from cached in TryGetFromCache<A>(cacheKey)
            from result in cached.Match(
                Some: value => 
                    from __ in LogInfo($"Cache hit: {cacheKey}")
                    select value,
                None: () =>
                    from value in operation
                    from __ in SetCache(cacheKey, value)
                    from ___ in LogInfo($"Cache miss: {cacheKey}")
                    select value)
            select result;
    }

    private static K<Db, Option<A>> TryGetFromCache<A>(string key) =>
        Db.LiftIO(() => Task.FromResult(
            _cache.TryGetValue(key, out var value) && value is A typed
                ? Some(typed)
                : Option<A>.None
        ));

    private static K<Db, Unit> SetCache<A>(string key, A value) =>
        Db.LiftIO(() =>
        {
            _cache[key] = value!;
            return Task.FromResult(unit);
        });
}

// Usage
ProductRepository.Get(id)
    .WithCache($"product:{id}", TimeSpan.FromMinutes(5))
    .WithMetrics($"GetProduct_{id}");
```

### Metrics Extension

```csharp
static class MetricsExtensions
{
    public static K<Db, A> WithMetrics<A>(
        this K<Db, A> operation,
        string operationName)
    {
        return
            from startTime in Db.LiftIO(() => Task.FromResult(DateTime.UtcNow))
            from result in operation
            from endTime in Db.LiftIO(() => Task.FromResult(DateTime.UtcNow))
            from _ in LogInfo($"Metrics - {operationName}: {(endTime - startTime).TotalMilliseconds}ms")
            select result;
    }
}
```

### HTTP Extension

```csharp
static class HttpExtensions
{
    public static K<Db, string> GetAsync(string url) =>
        from client in Db.HttpClient()
        from ct in Db.CancellationToken()
        from response in Db.LiftIO(client.GetAsync(url, ct))
        from _ in guard(response.IsSuccessStatusCode, 
            Error.New($"HTTP failed: {response.StatusCode}"))
        from content in Db.LiftIO(response.Content.ReadAsStringAsync(ct))
        select content;

    public static K<Db, TResult> GetJsonAsync<TResult>(string url) =>
        from content in GetAsync(url)
        from result in Db.LiftIO(() => Task.FromResult(
            System.Text.Json.JsonSerializer.Deserialize<TResult>(content)!))
        select result;
}
```

### Composing Effects

Effects compose naturally - order matters!

```csharp
// Order: Metrics → Cache → Transaction → Logging → Database
ProductRepository.BulkUpdate(updates)
    .WithTransaction()      // Innermost - wraps DB operations
    .WithCache("updates", TimeSpan.FromMinutes(1))
    .WithMetrics("BulkUpdate");  // Outermost - measures everything
```

---

## Testing Strategy

### Unit Testing with Mock Environment

```csharp
[Fact]
public async Task Get_ReturnsProduct_WhenExists()
{
    // Arrange
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(databaseName: "TestDb")
        .Options;
    
    var ctx = new AppDbContext(options);
    ctx.Products.Add(new Product { Id = 1, Name = "Test", Price = 99.99m });
    await ctx.SaveChangesAsync();
    
    var logger = new Mock<ILogger>().Object;
    var ct = CancellationToken.None;
    var env = DbEnvExtensions.Create(ctx, ct, logger);
    
    // Act
    var result = await ProductRepository.Get(1)
        .Run(env)
        .RunAsync();
    
    // Assert
    result.Match(
        Succ: product => Assert.Equal("Test", product.Name),
        Fail: error => Assert.Fail($"Expected success but got error: {error}")
    );
}
```

### Integration Testing

```csharp
[Fact]
public async Task BulkUpdate_IsAtomic_RollsBackOnError()
{
    // Arrange
    var env = CreateTestEnvironment();
    var updates = new List<UpdateProductRequest>
    {
        new("Valid Product", 99.99m),
        new("", -50m)  // Invalid - will cause rollback
    };
    
    // Act
    var result = await ProductRepository.BulkUpdatePrices(updates)
        .WithTransaction()
        .Run(env)
        .RunAsync();
    
    // Assert
    Assert.True(result.IsFail);
    
    // Verify rollback - no products should be updated
    var products = await ctx.Products.ToListAsync();
    Assert.All(products, p => Assert.NotEqual("Valid Product", p.Name));
}
```

---

## Best Practices

### 1. Keep Operations Pure

```csharp
// ❌ Bad - side effects hidden
public static K<Db, Product> Get(int id)
{
    Console.WriteLine($"Getting product {id}");  // Hidden side effect
    return from product in FetchProduct(id) select product;
}

// ✅ Good - side effects explicit
public static K<Db, Product> Get(int id) =>
    from _ in LogInfo($"Getting product {id}")
    from product in FetchProduct(id)
    select product;
```

### 2. Fail Fast with Clear Errors

```csharp
// ❌ Bad - generic error
from product in GetProduct(id)
from _ in guard(product.Price > 0, Error.New("Invalid"))
select product;

// ✅ Good - specific error with context
from product in GetProduct(id)
from _ in guard(product.Price > 0, 
    Error.New($"Product {id} has invalid price: {product.Price}"))
select product;
```

### 3. Use Validation for Multiple Errors

```csharp
// ❌ Bad - stops at first error
from _ in guard(product.Id > 0, IdError)
from __ in guard(!string.IsNullOrEmpty(product.Name), NameError)
from ___ in guard(product.Price > 0, PriceError)
select product;

// ✅ Good - collects all errors
public static Validation<Error, Product> Validate(Product p) =>
    (ValidateId(p), ValidateName(p), ValidatePrice(p))
        .Apply((_, _, _) => p);
```

### 4. Keep Effect Order Consistent

```csharp
// Transaction should be innermost
operation
    .WithTransaction()      // 1. Wraps DB operations
    .WithCache(key, ttl)    // 2. Caching layer
    .WithMetrics(name)      // 3. Measures everything
    .WithLogging(msg, fmt)  // 4. Logs the entire process
```

### 5. Use Optional Dependencies Wisely

```csharp
// ❌ Bad - always requires HttpClient
record DbEnv(AppDbContext DbContext, IHttpClientFactory HttpFactory);

// ✅ Good - optional when not always needed
record DbEnv(
    AppDbContext DbContext,
    IHttpClientFactory? HttpFactory = null
);
```

### 6. Document Effect Composition

```csharp
// Document the effect stack
// Metrics → Cache → Transaction → Logging → Database
public static K<Db, Product> UpdateProductWithEffects(int id, string name) =>
    UpdateProduct(id, name)
        .WithTransaction()      // Atomic DB operation
        .WithCache($"product:{id}", TimeSpan.FromMinutes(5))
        .WithMetrics($"UpdateProduct_{id}");
```

---

## Common Patterns

### Pattern 1: CRUD with Validation

```csharp
public static K<Db, Unit> Create(Product product) =>
    from validated in Db.Lift(Validate(product))
    from ctx in Db.Ctx<AppDbContext>()
    from ct in Db.CancellationToken()
    from _ in Db.LiftIO(ctx.AddAsync(validated, ct).AsTask())
    from __ in Db.Save()
    select unit;
```

### Pattern 2: Multi-Step Transaction

```csharp
public static K<Db, Unit> TransferInventory(int fromId, int toId, int qty) =>
    (from fromProduct in Get(fromId)
     from toProduct in Get(toId)
     from _ in ValidateTransfer(fromProduct, qty)
     from __ in DeductInventory(fromProduct, qty)
     from ___ in AddInventory(toProduct, qty)
     select unit)
    .WithTransaction();  // All or nothing
```

### Pattern 3: External API Integration

```csharp
public static K<Db, EnrichedProduct> GetEnriched(int id) =>
    from product in Get(id)
    from externalData in HttpExtensions.GetJsonAsync<ExternalData>($"https://api.example.com/products/{id}")
    from enriched in CombineData(product, externalData)
    select enriched;
```

### Pattern 4: Conditional Operations

```csharp
public static K<Db, Product> UpdateIfChanged(int id, UpdateRequest req) =>
    from existing in Get(id)
    from updated in existing.Name == req.Name && existing.Price == req.Price
        ? Db.Pure(existing)  // No change needed
        : ApplyUpdate(existing, req)
    select updated;
```

### Pattern 5: Batch Operations

```csharp
public static K<Db, List<Product>> CreateMany(List<Product> products) =>
    from validated in products.Traverse(p => Db.Lift(Validate(p)))
    from ctx in Db.Ctx<AppDbContext>()
    from ct in Db.CancellationToken()
    from _ in validated.Traverse(p => Db.LiftIO(ctx.AddAsync(p, ct).AsTask()))
    from __ in Db.Save()
    select validated;
```

### Pattern 6: Error Recovery

```csharp
public static K<Db, Product> GetWithFallback(int id) =>
    Get(id)
    | GetFromCache(id)
    | GetDefault();

// Using explicit catch
public static K<Db, Product> GetWithRetry(int id) =>
    Get(id)
        .Catch(err => err.Code == 500, _ => Get(id))  // Retry once on 500
        .Catch(err => GetFromCache(id));  // Then try cache
```

---

## API Endpoint Patterns

### Basic Endpoint

```csharp
app.MapGet("/products/{id}", async (
    int id,
    AppDbContext ctx,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    var env = DbEnvExtensions.Create(ctx, ct, logger);
    var result = await ProductRepository.Get(id)
        .Run(env)
        .RunAsync();
    
    return result.Match(
        Succ: product => Results.Ok(product),
        Fail: err => err.Code switch
        {
            404 => Results.NotFound(new { message = err.Message }),
            _ => Results.Problem(err.Message, statusCode: 500)
        }
    );
});
```

### With Result Helper

```csharp
static class ApiResultHelpers
{
    public static IResult ToResult<A>(Fin<A> result, Func<A, IResult> onSuccess)
    {
        return result.Match(
            Succ: onSuccess,
            Fail: err => err.Code switch
            {
                404 => Results.NotFound(new { message = err.Message }),
                400 => Results.BadRequest(new { message = err.Message }),
                499 => Results.StatusCode(499), // Client closed request
                _ => Results.Problem(err.Message, statusCode: 500)
            }
        );
    }
}

// Usage
app.MapGet("/products/{id}", async (int id, AppDbContext ctx, ILogger<Program> logger, CancellationToken ct) =>
{
    var env = DbEnvExtensions.Create(ctx, ct, logger);
    var result = await ProductRepository.Get(id).Run(env).RunAsync();
    return ApiResultHelpers.ToResult(result, product => Results.Ok(product));
});
```

---

## Extending the Pattern

### Adding Custom Effects

```csharp
// 1. Define the extension
static class RetryExtensions
{
    public static K<Db, A> WithRetry<A>(
        this K<Db, A> operation,
        int maxAttempts = 3)
    {
        return TryExecute(operation, 1, maxAttempts);
    }

    private static K<Db, A> TryExecute<A>(K<Db, A> op, int attempt, int max) =>
        attempt > max
            ? Db.Fail<A>(Error.New($"Failed after {max} attempts"))
            : op.Catch(err => 
                from _ in LogWarning($"Attempt {attempt} failed: {err.Message}")
                from result in TryExecute(op, attempt + 1, max)
                select result);
}

// 2. Use it
ProductRepository.Get(id)
    .WithRetry(maxAttempts: 3)
    .WithMetrics("GetProduct");
```

### Adding Authorization

```csharp
static class AuthorizationExtensions
{
    public static K<Db, A> RequirePermission<A>(
        this K<Db, A> operation,
        string permission)
    {
        return
            from user in GetCurrentUser()
            from _ in guard(user.HasPermission(permission),
                Error.New(401, "Unauthorized"))
            from result in operation
            select result;
    }

    private static K<Db, User> GetCurrentUser() =>
        Db.Ask().Select(env => env.CurrentUser);  // Add to DbEnv
}
```

---

## Troubleshooting

### Issue: "HttpClient not available"

**Cause:** Endpoint needs HTTP but HttpClientFactory not provided.

**Fix:** Add `IHttpClientFactory` parameter to endpoint:
```csharp
app.MapGet("/endpoint", async (
    AppDbContext ctx,
    ILogger<Program> logger,
    IHttpClientFactory httpFactory,  // Add this
    CancellationToken ct) => { ... });
```

### Issue: Transaction not rolling back

**Cause:** Operation not wrapped in `WithTransaction()`.

**Fix:**
```csharp
// Before
ProductRepository.BulkUpdate(updates).Run(env);

// After
ProductRepository.BulkUpdate(updates)
    .WithTransaction()  // Add this
    .Run(env);
```

### Issue: Cancellation not working

**Cause:** Not passing CancellationToken to async operations.

**Fix:**
```csharp
// Before
from products in Db.LiftIO(ctx.Products.ToListAsync())

// After
from ct in Db.CancellationToken()
from products in Db.LiftIO(ctx.Products.ToListAsync(ct))
```

---

## Resources

- [language-ext Documentation](https://github.com/louthy/language-ext)
- [Functional Programming in C#](https://github.com/louthy/language-ext/wiki)
- [ASP.NET Core Minimal APIs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis)

---

## License

MIT License - Feel free to use this pattern in your projects!