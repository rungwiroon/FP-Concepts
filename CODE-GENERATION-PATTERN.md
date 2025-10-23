# Code Generation Pattern for Repository Infrastructure

## The Problem

With multiple entities, you need to write a LOT of repetitive code:

```
For each entity (Todo, User, Project, Task, etc.):
├── ITodoRepository interface         (5-10 methods)
├── TodoRepository<M, RT> capability   (5-10 wrapper methods)
├── TestTodoRepository                 (5-10 implementations + helpers)
└── LiveTodoRepository                 (5-10 EF Core implementations)

= 20-40 lines × 4 files × N entities = Hundreds of lines of boilerplate!
```

**Problems:**
- Tedious and error-prone
- Hard to maintain consistency
- Adding a new entity requires creating 4 files with same patterns
- Changing patterns requires updating all files

## The Solution: Source Generators

C# Source Generators can automatically generate all repository infrastructure at compile-time from your domain entities!

### Architecture Overview

```
┌────────────────────────────────────────────────────────────┐
│  Domain/Entities/Todo.cs                                   │
│  [GenerateRepository]  ← Just add this attribute!          │
│  public record Todo { ... }                                │
└────────────────────────────────────────────────────────────┘
                        │
                        ▼
┌────────────────────────────────────────────────────────────┐
│  Source Generator (Compile-Time)                           │
│  Analyzes attributes, generates code automatically         │
└────────────────────────────────────────────────────────────┘
                        │
        ┌───────────────┼───────────────┬──────────────┐
        ▼               ▼               ▼              ▼
   ITodoRepo    TodoRepository    TestTodoRepo    LiveTodoRepo
   (Generated)    (Generated)     (Generated)     (Generated)
```

## Implementation

### Step 1: Create Source Generator Project

```bash
dotnet new classlib -n TodoApp.SourceGenerators -f netstandard2.0
cd TodoApp.SourceGenerators

# Add required packages
dotnet add package Microsoft.CodeAnalysis.CSharp --version 4.5.0
dotnet add package Microsoft.CodeAnalysis.Analyzers --version 3.3.4
```

```xml
<!-- TodoApp.SourceGenerators/TodoApp.SourceGenerators.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <IsRoslynComponent>true</IsRoslynComponent>
    <EnforceExtendedAnalyzerRules>true</EnforceExtendedAnalyzerRules>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.5.0" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.3.4" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

### Step 2: Define Attributes

```csharp
// TodoApp/Attributes/GenerateRepositoryAttribute.cs
namespace TodoApp.Attributes;

/// <summary>
/// Marks a domain entity for automatic repository generation.
/// Source generator will create:
/// - IXxxRepository trait interface (with Specification and Pagination support)
/// - XxxRepository capability module (with find/count/findPaged methods)
/// - TestXxxRepository test implementation (in-memory with LINQ)
/// - LiveXxxRepository EF Core implementation (translates to SQL)
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class GenerateRepositoryAttribute : Attribute
{
    /// <summary>
    /// Name of the DbSet property in AppDbContext (defaults to entity name + "s")
    /// </summary>
    public string? DbSetName { get; set; }
}

/// <summary>
/// Marks a property as the primary key for repository operations.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class RepositoryKeyAttribute : Attribute
{
}
```

### Step 3: Annotate Domain Entities

```csharp
// Domain/Entities/Todo.cs
using TodoApp.Attributes;

[GenerateRepository(DbSetName = "Todos")]
public record Todo
{
    [RepositoryKey]
    public int Id { get; init; }

    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsCompleted { get; init; }
    public int UserId { get; init; }
    public DateTime CreatedAt { get; init; }
}

// Domain/Entities/User.cs
[GenerateRepository(DbSetName = "Users")]
public record User
{
    [RepositoryKey]
    public int Id { get; init; }

    public string Email { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

// Domain/Entities/Project.cs
[GenerateRepository(DbSetName = "Projects")]
public record Project
{
    [RepositoryKey]
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;
    public int OwnerId { get; init; }
    public DateTime CreatedAt { get; init; }
}
```

### Step 4: Source Generator Implementation

```csharp
// TodoApp.SourceGenerators/RepositoryGenerator.cs
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TodoApp.SourceGenerators;

[Generator]
public class RepositoryGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all classes with [GenerateRepository] attribute
        var entityDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsCandidateEntity(s),
                transform: static (ctx, _) => GetEntityInfo(ctx))
            .Where(static m => m is not null);

        // Generate code for each entity
        context.RegisterSourceOutput(entityDeclarations, static (spc, entity) =>
        {
            if (entity is null) return;

            // Generate all 4 files
            GenerateTraitInterface(spc, entity);
            GenerateCapabilityModule(spc, entity);
            GenerateTestRepository(spc, entity);
            GenerateLiveRepository(spc, entity);
        });
    }

    private static bool IsCandidateEntity(SyntaxNode node)
    {
        return node is RecordDeclarationSyntax or ClassDeclarationSyntax;
    }

    private static EntityInfo? GetEntityInfo(GeneratorSyntaxContext context)
    {
        var typeDeclaration = (TypeDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration);

        if (symbol is null) return null;

        // Check for [GenerateRepository] attribute
        var attribute = symbol.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "GenerateRepositoryAttribute");

        if (attribute is null) return null;

        // Extract entity information
        var entityName = symbol.Name;
        var namespaceName = symbol.ContainingNamespace.ToDisplayString();
        var dbSetName = GetAttributeValue<string>(attribute, "DbSetName") ?? $"{entityName}s";

        // Find key property
        var keyProperty = symbol.GetMembers()
            .OfType<IPropertySymbol>()
            .FirstOrDefault(p => p.GetAttributes()
                .Any(a => a.AttributeClass?.Name == "RepositoryKeyAttribute"));

        if (keyProperty is null) return null;

        return new EntityInfo
        {
            EntityName = entityName,
            Namespace = namespaceName,
            DbSetName = dbSetName,
            KeyProperty = new PropertyInfo
            {
                Name = keyProperty.Name,
                Type = keyProperty.Type.ToDisplayString()
            }
        };
    }

    private static T? GetAttributeValue<T>(AttributeData? attribute, string name)
    {
        if (attribute is null) return default;

        var namedArg = attribute.NamedArguments
            .FirstOrDefault(kvp => kvp.Key == name);

        if (namedArg.Value.Value is T value)
            return value;

        return default;
    }

    // Generation methods follow...
    private static void GenerateTraitInterface(SourceProductionContext context, EntityInfo entity)
    {
        var source = $$"""
// <auto-generated/>
#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt;
using TodoApp.Domain.Specifications;
using TodoApp.Domain.Models;
using {{entity.Namespace}};

namespace TodoApp.Infrastructure.Traits;

/// <summary>
/// Repository trait for {{entity.EntityName}} entity.
/// Auto-generated from [GenerateRepository] attribute.
/// Supports Specification Pattern and Pagination Pattern from SCALING-PATTERNS.md.
/// </summary>
public interface I{{entity.EntityName}}Repository
{
    // Standard CRUD operations
    Task<List<{{entity.EntityName}}>> GetAll{{entity.EntityName}}sAsync(CancellationToken ct);
    Task<Option<{{entity.EntityName}}>> Get{{entity.EntityName}}ByIdAsync({{entity.KeyProperty.Type}} id, CancellationToken ct);

    // ✅ Specification Pattern - Generic query methods
    Task<List<{{entity.EntityName}}>> FindAsync(Specification<{{entity.EntityName}}> spec, CancellationToken ct);
    Task<int> CountAsync(Specification<{{entity.EntityName}}> spec, CancellationToken ct);

    // ✅ Pagination Pattern - Paginated queries with specifications
    Task<PagedResult<{{entity.EntityName}}>> FindPagedAsync(
        Specification<{{entity.EntityName}}> spec,
        int pageNumber,
        int pageSize,
        SortOrder<{{entity.EntityName}}>? sortOrder,
        CancellationToken ct);

    // Write operations (sync, modify context only - Unit of Work handles saving)
    void Add{{entity.EntityName}}({{entity.EntityName}} entity);
    void Update{{entity.EntityName}}({{entity.EntityName}} entity);
    void Delete{{entity.EntityName}}({{entity.EntityName}} entity);
}
""";

        context.AddSource($"I{entity.EntityName}Repository.g.cs", source);
    }

    private static void GenerateCapabilityModule(SourceProductionContext context, EntityInfo entity)
    {
        var source = $$"""
// <auto-generated/>
#nullable enable

using System;
using System.Collections.Generic;
using LanguageExt;
using LanguageExt.Traits;
using TodoApp.Domain.Specifications;
using TodoApp.Domain.Models;
using TodoApp.Infrastructure.Traits;
using {{entity.Namespace}};
using static LanguageExt.Prelude;

namespace TodoApp.Infrastructure.Capabilities;

/// <summary>
/// Capability module for {{entity.EntityName}} repository using Has pattern.
/// Auto-generated from [GenerateRepository] attribute.
/// Supports Specification and Pagination patterns from SCALING-PATTERNS.md.
/// </summary>
public static class {{entity.EntityName}}Repository<M, RT>
    where M : Monad<M>, MonadIO<M>
    where RT : Has<M, I{{entity.EntityName}}Repository>
{
    // Standard CRUD queries
    public static K<M, List<{{entity.EntityName}}>> getAll{{entity.EntityName}}s =>
        from repo in Has<M, RT, I{{entity.EntityName}}Repository>.ask
        from entities in M.LiftIO(IO.liftAsync(env =>
            repo.GetAll{{entity.EntityName}}sAsync(env.Token)))
        select entities;

    public static K<M, Option<{{entity.EntityName}}>> get{{entity.EntityName}}ById({{entity.KeyProperty.Type}} id) =>
        from repo in Has<M, RT, I{{entity.EntityName}}Repository>.ask
        from entity in M.LiftIO(IO.liftAsync(env =>
            repo.Get{{entity.EntityName}}ByIdAsync(id, env.Token)))
        select entity;

    // ✅ Specification Pattern - Composable queries
    public static K<M, List<{{entity.EntityName}}>> find(Specification<{{entity.EntityName}}> spec) =>
        from repo in Has<M, RT, I{{entity.EntityName}}Repository>.ask
        from entities in M.LiftIO(IO.liftAsync(env =>
            repo.FindAsync(spec, env.Token)))
        select entities;

    public static K<M, int> count(Specification<{{entity.EntityName}}> spec) =>
        from repo in Has<M, RT, I{{entity.EntityName}}Repository>.ask
        from count in M.LiftIO(IO.liftAsync(env =>
            repo.CountAsync(spec, env.Token)))
        select count;

    // ✅ Pagination Pattern - Efficient paginated queries
    public static K<M, PagedResult<{{entity.EntityName}}>> findPaged(
        Specification<{{entity.EntityName}}> spec,
        int pageNumber,
        int pageSize,
        SortOrder<{{entity.EntityName}}>? sortOrder = null) =>
        from repo in Has<M, RT, I{{entity.EntityName}}Repository>.ask
        from result in M.LiftIO(IO.liftAsync(env =>
            repo.FindPagedAsync(spec, pageNumber, pageSize, sortOrder, env.Token)))
        select result;

    // Write operations (modify context only - UnitOfWork handles saving)
    public static K<M, Unit> add{{entity.EntityName}}({{entity.EntityName}} entity) =>
        from repo in Has<M, RT, I{{entity.EntityName}}Repository>.ask
        from _ in M.LiftIO(() =>
        {
            repo.Add{{entity.EntityName}}(entity);
            return unit;
        })
        select unit;

    public static K<M, Unit> update{{entity.EntityName}}({{entity.EntityName}} entity) =>
        from repo in Has<M, RT, I{{entity.EntityName}}Repository>.ask
        from _ in M.LiftIO(() =>
        {
            repo.Update{{entity.EntityName}}(entity);
            return unit;
        })
        select unit;

    public static K<M, Unit> delete{{entity.EntityName}}({{entity.EntityName}} entity) =>
        from repo in Has<M, RT, I{{entity.EntityName}}Repository>.ask
        from _ in M.LiftIO(() =>
        {
            repo.Delete{{entity.EntityName}}(entity);
            return unit;
        })
        select unit;
}
""";

        context.AddSource($"{entity.EntityName}Repository.g.cs", source);
    }

    private static void GenerateTestRepository(SourceProductionContext context, EntityInfo entity)
    {
        var source = $$"""
// <auto-generated/>
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt;
using TodoApp.Domain.Specifications;
using TodoApp.Domain.Models;
using TodoApp.Infrastructure.Traits;
using {{entity.Namespace}};
using static LanguageExt.Prelude;

namespace TodoApp.Tests.TestInfrastructure;

/// <summary>
/// Test implementation of I{{entity.EntityName}}Repository using in-memory dictionary.
/// Auto-generated from [GenerateRepository] attribute.
/// Uses LINQ to Objects for specification filtering (pure in-memory).
/// </summary>
public class Test{{entity.EntityName}}Repository : I{{entity.EntityName}}Repository
{
    private readonly Dictionary<{{entity.KeyProperty.Type}}, {{entity.EntityName}}> _entities = new();
    private {{GetNextIdInitializer(entity.KeyProperty.Type)}}

    // Standard CRUD operations
    public Task<List<{{entity.EntityName}}>> GetAll{{entity.EntityName}}sAsync(CancellationToken ct)
    {
        return Task.FromResult(_entities.Values.ToList());
    }

    public Task<Option<{{entity.EntityName}}>> Get{{entity.EntityName}}ByIdAsync({{entity.KeyProperty.Type}} id, CancellationToken ct)
    {
        var result = _entities.TryGetValue(id, out var entity) ? Some(entity) : None;
        return Task.FromResult(result);
    }

    // ✅ Specification Pattern - LINQ to Objects
    public Task<List<{{entity.EntityName}}>> FindAsync(Specification<{{entity.EntityName}}> spec, CancellationToken ct)
    {
        var predicate = spec.ToExpression().Compile();
        var results = _entities.Values.Where(predicate).ToList();
        return Task.FromResult(results);
    }

    public Task<int> CountAsync(Specification<{{entity.EntityName}}> spec, CancellationToken ct)
    {
        var predicate = spec.ToExpression().Compile();
        var count = _entities.Values.Count(predicate);
        return Task.FromResult(count);
    }

    // ✅ Pagination Pattern - In-memory pagination with sorting
    public Task<PagedResult<{{entity.EntityName}}>> FindPagedAsync(
        Specification<{{entity.EntityName}}> spec,
        int pageNumber,
        int pageSize,
        SortOrder<{{entity.EntityName}}>? sortOrder,
        CancellationToken ct)
    {
        var predicate = spec.ToExpression().Compile();
        var query = _entities.Values.Where(predicate).AsQueryable();

        // Apply sorting if specified
        if (sortOrder != null)
        {
            query = sortOrder.Ascending
                ? query.OrderBy(sortOrder.OrderBy.Compile())
                : query.OrderByDescending(sortOrder.OrderBy.Compile());
        }

        var totalCount = query.Count();
        var items = query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var result = new PagedResult<{{entity.EntityName}}>(
            items,
            totalCount,
            pageNumber,
            pageSize);

        return Task.FromResult(result);
    }

    // Write operations (modify in-memory dictionary only)
    public void Add{{entity.EntityName}}({{entity.EntityName}} entity)
    {
        if ({{GetIsDefaultKeyCheck("entity." + entity.KeyProperty.Name, entity.KeyProperty.Type)}})
        {
            entity = entity with { {{entity.KeyProperty.Name}} = {{GetNextIdExpression(entity.KeyProperty.Type)}} };
        }
        _entities[entity.{{entity.KeyProperty.Name}}] = entity;
    }

    public void Update{{entity.EntityName}}({{entity.EntityName}} entity)
    {
        if (!_entities.ContainsKey(entity.{{entity.KeyProperty.Name}}))
            throw new InvalidOperationException($"{{entity.EntityName}} {entity.{{entity.KeyProperty.Name}}} not found");
        _entities[entity.{{entity.KeyProperty.Name}}] = entity;
    }

    public void Delete{{entity.EntityName}}({{entity.EntityName}} entity)
    {
        _entities.Remove(entity.{{entity.KeyProperty.Name}});
    }

    // Test helpers
    public void Seed(params {{entity.EntityName}}[] entities)
    {
        foreach (var entity in entities)
            _entities[entity.{{entity.KeyProperty.Name}}] = entity;

        {{GetUpdateNextIdAfterSeed(entity)}}
    }

    public void Clear()
    {
        _entities.Clear();
    }

    public int Count => _entities.Count;
}
""";

        context.AddSource($"Test{entity.EntityName}Repository.g.cs", source);
    }

    private static void GenerateLiveRepository(SourceProductionContext context, EntityInfo entity)
    {
        var source = $$"""
// <auto-generated/>
#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt;
using Microsoft.EntityFrameworkCore;
using TodoApp.Data;
using TodoApp.Domain.Specifications;
using TodoApp.Domain.Models;
using TodoApp.Infrastructure.Traits;
using {{entity.Namespace}};
using static LanguageExt.Prelude;

namespace TodoApp.Infrastructure.Live;

/// <summary>
/// Production implementation of I{{entity.EntityName}}Repository using EF Core.
/// Auto-generated from [GenerateRepository] attribute.
/// Translates specifications to SQL for efficient database queries.
/// </summary>
public class Live{{entity.EntityName}}Repository(AppDbContext context) : I{{entity.EntityName}}Repository
{
    // Standard CRUD operations
    public async Task<List<{{entity.EntityName}}>> GetAll{{entity.EntityName}}sAsync(CancellationToken ct)
    {
        return await context.{{entity.DbSetName}}
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<Option<{{entity.EntityName}}>> Get{{entity.EntityName}}ByIdAsync({{entity.KeyProperty.Type}} id, CancellationToken ct)
    {
        var entity = await context.{{entity.DbSetName}}
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.{{entity.KeyProperty.Name}} == id, ct);

        return Optional(entity);
    }

    // ✅ Specification Pattern - EF Core translates to SQL WHERE clause
    public async Task<List<{{entity.EntityName}}>> FindAsync(Specification<{{entity.EntityName}}> spec, CancellationToken ct)
    {
        return await context.{{entity.DbSetName}}
            .AsNoTracking()
            .Where(spec.ToExpression())  // Expression tree → SQL
            .ToListAsync(ct);
    }

    public async Task<int> CountAsync(Specification<{{entity.EntityName}}> spec, CancellationToken ct)
    {
        return await context.{{entity.DbSetName}}
            .Where(spec.ToExpression())
            .CountAsync(ct);
    }

    // ✅ Pagination Pattern - Efficient SQL with OFFSET/FETCH
    public async Task<PagedResult<{{entity.EntityName}}>> FindPagedAsync(
        Specification<{{entity.EntityName}}> spec,
        int pageNumber,
        int pageSize,
        SortOrder<{{entity.EntityName}}>? sortOrder,
        CancellationToken ct)
    {
        var query = context.{{entity.DbSetName}}
            .AsNoTracking()
            .Where(spec.ToExpression());

        // Apply sorting if specified
        if (sortOrder != null)
        {
            query = sortOrder.Ascending
                ? query.OrderBy(sortOrder.OrderBy)
                : query.OrderByDescending(sortOrder.OrderBy);
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<{{entity.EntityName}}>(
            items,
            totalCount,
            pageNumber,
            pageSize);
    }

    // Write operations (modify DbContext only - UnitOfWork saves)
    public void Add{{entity.EntityName}}({{entity.EntityName}} entity)
    {
        context.{{entity.DbSetName}}.Add(entity);
    }

    public void Update{{entity.EntityName}}({{entity.EntityName}} entity)
    {
        context.Entry(entity).State = EntityState.Modified;
    }

    public void Delete{{entity.EntityName}}({{entity.EntityName}} entity)
    {
        context.{{entity.DbSetName}}.Remove(entity);
    }
}
""";

        context.AddSource($"Live{entity.EntityName}Repository.g.cs", source);
    }

    // Helper methods for code generation

    private static string GetNextIdInitializer(string type)
    {
        return type switch
        {
            "int" => "int _nextId = 1;",
            "long" => "long _nextId = 1L;",
            "Guid" or "System.Guid" => "// Guid uses NewGuid()",
            _ => $"// TODO: Initialize {type} ID generator"
        };
    }

    private static string GetNextIdExpression(string type)
    {
        return type switch
        {
            "int" or "long" => "_nextId++",
            "Guid" or "System.Guid" => "Guid.NewGuid()",
            _ => "default!"
        };
    }

    private static string GetIsDefaultKeyCheck(string keyExpression, string type)
    {
        return type switch
        {
            "int" or "long" => $"{keyExpression} == 0",
            "Guid" or "System.Guid" => $"{keyExpression} == Guid.Empty",
            _ => $"{keyExpression} == default"
        };
    }

    private static string GetUpdateNextIdAfterSeed(EntityInfo entity)
    {
        return entity.KeyProperty.Type switch
        {
            "int" => $"_nextId = _entities.Count > 0 ? _entities.Keys.Max() + 1 : 1;",
            "long" => $"_nextId = _entities.Count > 0 ? _entities.Keys.Max() + 1L : 1L;",
            _ => ""
        };
    }

    private static string ToCamelCase(string str)
    {
        if (string.IsNullOrEmpty(str) || char.IsLower(str[0]))
            return str;
        return char.ToLower(str[0]) + str.Substring(1);
    }
}

// Data models
record EntityInfo
{
    public required string EntityName { get; init; }
    public required string Namespace { get; init; }
    public required string DbSetName { get; init; }
    public required PropertyInfo KeyProperty { get; init; }
}

record PropertyInfo
{
    public required string Name { get; init; }
    public required string Type { get; init; }
}
```

### Step 5: Reference Source Generator in Main Project

```xml
<!-- TodoApp/TodoApp.csproj -->
<Project Sdk="Microsoft.NET.Sdk.Web">
  <!-- ... existing properties ... -->

  <ItemGroup>
    <!-- Reference the source generator -->
    <ProjectReference Include="../TodoApp.SourceGenerators/TodoApp.SourceGenerators.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
  </ItemGroup>
</Project>
```

## Usage Examples

### Before (Manual - 100+ lines per entity)

```csharp
// ❌ Manual: Write ALL of this for EACH entity

// Infrastructure/Traits/ITodoRepository.cs (20 lines)
public interface ITodoRepository { ... }

// Infrastructure/Capabilities/TodoRepository.cs (40 lines)
public static class TodoRepository<M, RT> { ... }

// Tests/TestInfrastructure/TestTodoRepository.cs (30 lines)
public class TestTodoRepository : ITodoRepository { ... }

// Infrastructure/Live/LiveTodoRepository.cs (30 lines)
public class LiveTodoRepository : ITodoRepository { ... }

// Total: ~120 lines × N entities = LOTS of boilerplate
```

### After (Generated - 2 lines per entity!)

```csharp
// ✅ Automated: Just annotate your entity!

[GenerateRepository(DbSetName = "Todos")]
public record Todo
{
    [RepositoryKey]
    public int Id { get; init; }

    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsCompleted { get; init; }
    public int UserId { get; init; }
    public DateTime CreatedAt { get; init; }
}

// That's it! All 4 files generated automatically at compile-time:
// ✅ ITodoRepository.g.cs (with FindAsync, CountAsync, FindPagedAsync)
// ✅ TodoRepository.g.cs (with find, count, findPaged)
// ✅ TestTodoRepository.g.cs (LINQ to Objects)
// ✅ LiveTodoRepository.g.cs (EF Core → SQL)
```

### Using Generated Code with Specifications

```csharp
// Domain/Specifications/TodoSpecifications.cs
public class TodosByUserSpec : Specification<Todo>
{
    private readonly int _userId;
    public TodosByUserSpec(int userId) => _userId = userId;

    public override Expression<Func<Todo, bool>> ToExpression() =>
        todo => todo.UserId == _userId;
}

public class CompletedTodoSpec : Specification<Todo>
{
    public override Expression<Func<Todo, bool>> ToExpression() =>
        todo => todo.IsCompleted;
}

// Application/Todos/TodoService.cs
public static class TodoService<M, RT>
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, ITodoRepository>,    // ✅ Generated interface
               Has<M, IUnitOfWork>,
               Has<M, ILoggerIO>
{
    // ✅ Create todo with generated repository
    public static K<M, Todo> CreateTodo(string title, int userId) =>
        from todo in M.Pure(new Todo { Title = title, UserId = userId, CreatedAt = DateTime.UtcNow })
        from _ in TodoRepository<M, RT>.addTodo(todo)    // ✅ Generated capability
        from __ in UnitOfWork<M, RT>.saveChanges
        select todo;

    // ✅ Query using Specification Pattern (not custom methods!)
    public static K<M, List<Todo>> GetCompletedTodosByUser(int userId) =>
        TodoRepository<M, RT>.find(
            new CompletedTodoSpec()
                .And(new TodosByUserSpec(userId))
        );

    // ✅ Paginated query with generated findPaged
    public static K<M, PagedResult<Todo>> GetUserTodosPaged(
        int userId,
        int pageNumber,
        int pageSize) =>
        TodoRepository<M, RT>.findPaged(
            new TodosByUserSpec(userId),
            pageNumber,
            pageSize,
            new SortOrder<Todo>(t => t.CreatedAt, ascending: false)
        );
}

// Tests
[Test]
public async Task TestCompletedTodosByUser()
{
    var runtime = new TestRuntime();

    // ✅ Generated test repository with helpers
    runtime.TodoRepository.Seed(
        new Todo { Id = 1, Title = "Test 1", UserId = 1, IsCompleted = true },
        new Todo { Id = 2, Title = "Test 2", UserId = 1, IsCompleted = false },
        new Todo { Id = 3, Title = "Test 3", UserId = 2, IsCompleted = true }
    );

    // ✅ Generated find method works with specifications
    var result = await TodoService<Eff<TestRuntime>, TestRuntime>
        .GetCompletedTodosByUser(1)
        .RunAsync(runtime, EnvIO.New());

    Assert.AreEqual(1, result.Count);  // Only completed todos for user 1
    Assert.IsTrue(result[0].IsCompleted);
}

[Test]
public async Task TestPaginatedQuery()
{
    var runtime = new TestRuntime();

    // Seed 25 todos
    runtime.TodoRepository.Seed(
        Enumerable.Range(1, 25).Select(i =>
            new Todo { Id = i, Title = $"Todo {i}", UserId = 1 }
        ).ToArray()
    );

    // ✅ Generated findPaged method
    var result = await TodoService<Eff<TestRuntime>, TestRuntime>
        .GetUserTodosPaged(1, pageNumber: 2, pageSize: 10)
        .RunAsync(runtime, EnvIO.New());

    Assert.AreEqual(10, result.Items.Count);     // Page 2 has 10 items
    Assert.AreEqual(25, result.TotalCount);      // Total is 25
    Assert.AreEqual(3, result.TotalPages);       // 3 pages total
    Assert.IsTrue(result.HasPreviousPage);       // Page 2 has previous
    Assert.IsTrue(result.HasNextPage);           // Page 2 has next
}
```

## Adding a New Entity

### Before (Manual)
1. Create domain entity (10 lines)
2. Create ITodoRepository interface (20 lines)
3. Create TodoRepository capability (40 lines)
4. Create TestTodoRepository (30 lines)
5. Create LiveTodoRepository (30 lines)
6. Update AppRuntime (5 lines)
7. Update TestRuntime (5 lines)

**Total: ~140 lines, 30 minutes**

### After (Generated)
1. Create domain entity with attributes (10 lines)
2. Update AppRuntime (5 lines)
3. Update TestRuntime (5 lines)

**Total: ~20 lines, 5 minutes**

```csharp
// Step 1: Define entity with attributes
[GenerateRepository]
public record Comment
{
    [RepositoryKey]
    public int Id { get; init; }

    public int PostId { get; init; }
    public int UserId { get; init; }
    public string Text { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

// Step 2: Create specifications for querying (instead of custom methods)
public class CommentsByPostSpec : Specification<Comment>
{
    private readonly int _postId;
    public CommentsByPostSpec(int postId) => _postId = postId;

    public override Expression<Func<Comment, bool>> ToExpression() =>
        comment => comment.PostId == _postId;
}

public class CommentsByUserSpec : Specification<Comment>
{
    private readonly int _userId;
    public CommentsByUserSpec(int userId) => _userId = userId;

    public override Expression<Func<Comment, bool>> ToExpression() =>
        comment => comment.UserId == _userId;
}

// Step 3 & 4: Update runtimes (just add one line each)
public class AppRuntime :
    Has<Eff<AppRuntime>, ICommentRepository>,  // ✅ Auto-generated
    // ... other traits
{
    public AppRuntime(AppDbContext db, ILogger logger)
    {
        CommentRepository = new LiveCommentRepository(db);  // ✅ Auto-generated
        // ... other repos
    }

    public ICommentRepository CommentRepository { get; }
    ICommentRepository Has<Eff<AppRuntime>, ICommentRepository>.Ask => CommentRepository;
}

public class TestRuntime :
    Has<Eff<TestRuntime>, ICommentRepository>,  // ✅ Auto-generated
    // ... other traits
{
    public TestRuntime()
    {
        CommentRepository = new TestCommentRepository();  // ✅ Auto-generated
        // ... other repos
    }

    public ICommentRepository CommentRepository { get; }
    ICommentRepository Has<Eff<TestRuntime>, ICommentRepository>.Ask => CommentRepository;
}

// Done! 150+ lines of code generated automatically
// Use it with specifications:
// var comments = await CommentRepository<M, RT>.find(new CommentsByPostSpec(postId));
// var pagedComments = await CommentRepository<M, RT>.findPaged(spec, pageNumber, pageSize);
```

## View Generated Code

Generated files appear in your IDE:

```
TodoApp/
├── Dependencies/
│   └── Analyzers/
│       └── TodoApp.SourceGenerators/
│           ├── ITodoRepository.g.cs          ← View generated code here
│           ├── TodoRepository.g.cs
│           ├── TestTodoRepository.g.cs
│           └── LiveTodoRepository.g.cs
```

You can also see them in:
```
obj/Debug/net8.0/generated/TodoApp.SourceGenerators/...
```

## Advanced: Custom Specifications for Complex Queries

For entity-specific complex query logic, create custom Specifications instead of repository methods:

```csharp
// ✅ RECOMMENDED: Use Specification Pattern
// Domain/Specifications/OverdueTodoSpec.cs
public class OverdueTodoSpec : Specification<Todo>
{
    private readonly DateTime _asOfDate;

    public OverdueTodoSpec(DateTime asOfDate)
    {
        _asOfDate = asOfDate;
    }

    public override Expression<Func<Todo, bool>> ToExpression()
    {
        return todo => !todo.IsCompleted && todo.DueDate < _asOfDate;
    }
}

// Usage in service (pure FP - inject time via TimeIO trait):
from now in Time<M, RT>.UtcNow
from spec in M.Pure(new OverdueTodoSpec(now))
from overdueTodos in TodoRepository<M, RT>.find(spec)  // ✅ Uses generated find method
select overdueTodos;

// ❌ AVOID: Custom repository methods break the pattern
// Only use partial classes if you REALLY need database-specific optimizations
// that can't be expressed as specifications (very rare!)
```

**Why prefer Specifications over custom repository methods?**

1. **Composable** - Can combine with other specifications using And/Or/Not
2. **Testable** - Works identically in test (LINQ) and production (SQL)
3. **Reusable** - One specification class can be used in many services
4. **Consistent** - Follows the pattern established in SCALING-PATTERNS.md
5. **Pure FP** - No hidden dependencies, inject time/random via traits

## Benefits

### 1. Massive Code Reduction
- **Before:** ~150 lines per entity (interface + capability + test impl + live impl)
- **After:** ~2 lines per entity (just annotations!)
- **Savings:** 99% less boilerplate!

### 2. Full SCALING-PATTERNS.md Alignment
- ✅ **Specification Pattern** - Generated `FindAsync`/`find` methods work with specifications
- ✅ **Pagination Pattern** - Generated `FindPagedAsync`/`findPaged` with sorting support
- ✅ **Transaction Pattern** - Write methods don't save (Unit of Work handles saving)
- ✅ **No custom queries** - Encourages composable specifications instead of repository explosion
- ✅ **Pure FP** - All dependencies explicit (time, random, etc. via traits)

### 3. Consistency Across All Entities
- All repositories follow identical pattern
- No copy-paste errors or drift
- Changes to generator propagate automatically
- Test and production implementations stay in sync

### 4. Compile-Time Safety
- Generated at compile-time (not runtime reflection)
- Full IntelliSense support in IDE
- Type-safe generated code
- Errors caught at compile-time, not runtime

### 5. Works Everywhere (Test & Production)
- **Test:** LINQ to Objects (in-memory)
- **Production:** EF Core (translates to SQL)
- Same specification code works in both!
- No mocking needed - real implementations in tests

### 6. Easy Maintenance & Evolution
- Change generator once, affects all entities
- Add new methods (e.g., bulk operations) to all repos at once
- Refactoring is simple and safe
- Pattern improvements apply everywhere instantly

### 7. Developer Productivity
- Add new entity in **5 minutes** instead of 30
- Focus on domain logic and specifications, not boilerplate
- Less context switching between files
- New developers onboard faster (consistent pattern)

## Summary

Source generators + Specification Pattern = Maximum leverage for multi-entity architectures:

✅ **2 lines** (annotations) generate 150+ lines per entity<br/>
✅ **Specification Pattern** - Composable, reusable query logic (from SCALING-PATTERNS.md)<br/>
✅ **Pagination Pattern** - Efficient large dataset handling (from SCALING-PATTERNS.md)<br/>
✅ **Transaction Pattern** - Unit of Work coordinates saves (from SCALING-PATTERNS.md)<br/>
✅ **Compile-time** code generation with full type safety<br/>
✅ **Automatic** trait, capability, test, and live implementations<br/>
✅ **Consistent** pattern across all entities<br/>
✅ **Pure FP** - No DateTime.UtcNow, inject dependencies via traits<br/>
✅ **Testable** - Same code works with LINQ (test) and SQL (production)<br/>

**For a project with 10 entities:**
- **Manual approach:** ~1,500 lines of repetitive repository code + 500 lines of specifications
- **Generated approach:** ~20 lines of annotations + 500 lines of specifications + 1 generator
- **Code saved:** ~1,480 lines (98.7% reduction in boilerplate!)
- **Time saved:** 5 hours → 30 minutes for initial setup

**Key insight:** Generate the **mechanical parts** (repository infrastructure), write the **logic parts** (specifications) by hand. Specifications are reusable domain knowledge worth writing. Repository boilerplate is not.

This is the power of combining meta-programming (source generators) with functional patterns (specifications, traits, monads) for scalable, maintainable architecture!
