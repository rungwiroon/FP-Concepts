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
/// - IXxxRepository trait interface
/// - XxxRepository capability module
/// - TestXxxRepository test implementation
/// - LiveXxxRepository EF Core implementation
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class GenerateRepositoryAttribute : Attribute
{
    /// <summary>
    /// Name of the DbSet property in AppDbContext (defaults to entity name + "s")
    /// </summary>
    public string? DbSetName { get; set; }

    /// <summary>
    /// Additional query methods to generate (e.g., "GetByEmail", "GetByUserId")
    /// </summary>
    public string[]? CustomQueries { get; set; }
}

/// <summary>
/// Marks a property as the primary key for repository operations.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class RepositoryKeyAttribute : Attribute
{
}

/// <summary>
/// Marks a property for custom query generation.
/// Generates GetByXxx methods in the repository.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class RepositoryQueryAttribute : Attribute
{
    public string? MethodName { get; set; }
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

    [RepositoryQuery(MethodName = "GetTodosByUser")]
    public int UserId { get; init; }

    public DateTime CreatedAt { get; init; }
}

// Domain/Entities/User.cs
[GenerateRepository(DbSetName = "Users")]
public record User
{
    [RepositoryKey]
    public int Id { get; init; }

    [RepositoryQuery(MethodName = "GetUserByEmail")]
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

    [RepositoryQuery(MethodName = "GetProjectsByUser")]
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

        // Find query properties
        var queryProperties = symbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.GetAttributes()
                .Any(a => a.AttributeClass?.Name == "RepositoryQueryAttribute"))
            .Select(p => new QueryPropertyInfo
            {
                PropertyName = p.Name,
                PropertyType = p.Type.ToDisplayString(),
                MethodName = GetQueryMethodName(p)
            })
            .ToImmutableArray();

        return new EntityInfo
        {
            EntityName = entityName,
            Namespace = namespaceName,
            DbSetName = dbSetName,
            KeyProperty = new PropertyInfo
            {
                Name = keyProperty.Name,
                Type = keyProperty.Type.ToDisplayString()
            },
            QueryProperties = queryProperties
        };
    }

    private static string GetQueryMethodName(IPropertySymbol property)
    {
        var attr = property.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "RepositoryQueryAttribute");

        var methodName = GetAttributeValue<string>(attr, "MethodName");
        return methodName ?? $"GetBy{property.Name}";
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
using {{entity.Namespace}};

namespace TodoApp.Infrastructure.Traits;

/// <summary>
/// Repository trait for {{entity.EntityName}} entity.
/// Auto-generated from [GenerateRepository] attribute.
/// </summary>
public interface I{{entity.EntityName}}Repository
{
    // Read operations (async, return values)
    Task<List<{{entity.EntityName}}>> GetAll{{entity.EntityName}}sAsync(CancellationToken ct);
    Task<Option<{{entity.EntityName}}>> Get{{entity.EntityName}}ByIdAsync({{entity.KeyProperty.Type}} id, CancellationToken ct);

{{GenerateCustomQuerySignatures(entity)}}

    // Write operations (sync, modify context only)
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
using TodoApp.Infrastructure.Traits;
using {{entity.Namespace}};
using static LanguageExt.Prelude;

namespace TodoApp.Infrastructure.Capabilities;

/// <summary>
/// Capability module for {{entity.EntityName}} repository using Has pattern.
/// Auto-generated from [GenerateRepository] attribute.
/// </summary>
public static class {{entity.EntityName}}Repository<M, RT>
    where M : Monad<M>, MonadIO<M>
    where RT : Has<M, I{{entity.EntityName}}Repository>
{
    /// <summary>
    /// Gets all {{entity.EntityName}} entities.
    /// </summary>
    public static K<M, List<{{entity.EntityName}}>> getAll{{entity.EntityName}}s =>
        from repo in Has<M, RT, I{{entity.EntityName}}Repository>.ask
        from entities in M.LiftIO(IO.liftAsync(env =>
            repo.GetAll{{entity.EntityName}}sAsync(env.Token)))
        select entities;

    /// <summary>
    /// Gets a {{entity.EntityName}} by its ID.
    /// </summary>
    public static K<M, Option<{{entity.EntityName}}>> get{{entity.EntityName}}ById({{entity.KeyProperty.Type}} id) =>
        from repo in Has<M, RT, I{{entity.EntityName}}Repository>.ask
        from entity in M.LiftIO(IO.liftAsync(env =>
            repo.Get{{entity.EntityName}}ByIdAsync(id, env.Token)))
        select entity;

{{GenerateCustomQueryMethods(entity)}}

    /// <summary>
    /// Adds a new {{entity.EntityName}} (does not save).
    /// </summary>
    public static K<M, Unit> add{{entity.EntityName}}({{entity.EntityName}} entity) =>
        from repo in Has<M, RT, I{{entity.EntityName}}Repository>.ask
        from _ in M.LiftIO(() =>
        {
            repo.Add{{entity.EntityName}}(entity);
            return unit;
        })
        select unit;

    /// <summary>
    /// Updates a {{entity.EntityName}} (does not save).
    /// </summary>
    public static K<M, Unit> update{{entity.EntityName}}({{entity.EntityName}} entity) =>
        from repo in Has<M, RT, I{{entity.EntityName}}Repository>.ask
        from _ in M.LiftIO(() =>
        {
            repo.Update{{entity.EntityName}}(entity);
            return unit;
        })
        select unit;

    /// <summary>
    /// Deletes a {{entity.EntityName}} (does not save).
    /// </summary>
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
using TodoApp.Infrastructure.Traits;
using {{entity.Namespace}};
using static LanguageExt.Prelude;

namespace TodoApp.Tests.TestInfrastructure;

/// <summary>
/// Test implementation of I{{entity.EntityName}}Repository using in-memory dictionary.
/// Auto-generated from [GenerateRepository] attribute.
/// </summary>
public class Test{{entity.EntityName}}Repository : I{{entity.EntityName}}Repository
{
    private readonly Dictionary<{{entity.KeyProperty.Type}}, {{entity.EntityName}}> _entities = new();
    private {{GetNextIdInitializer(entity.KeyProperty.Type)}}

    // Read operations
    public Task<List<{{entity.EntityName}}>> GetAll{{entity.EntityName}}sAsync(CancellationToken ct)
    {
        return Task.FromResult(_entities.Values.ToList());
    }

    public Task<Option<{{entity.EntityName}}>> Get{{entity.EntityName}}ByIdAsync({{entity.KeyProperty.Type}} id, CancellationToken ct)
    {
        var result = _entities.TryGetValue(id, out var entity) ? Some(entity) : None;
        return Task.FromResult(result);
    }

{{GenerateTestCustomQueries(entity)}}

    // Write operations
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
using TodoApp.Infrastructure.Traits;
using {{entity.Namespace}};
using static LanguageExt.Prelude;

namespace TodoApp.Infrastructure.Live;

/// <summary>
/// Production implementation of I{{entity.EntityName}}Repository using EF Core.
/// Auto-generated from [GenerateRepository] attribute.
/// </summary>
public class Live{{entity.EntityName}}Repository(AppDbContext context) : I{{entity.EntityName}}Repository
{
    // Read operations
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

{{GenerateLiveCustomQueries(entity)}}

    // Write operations
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
    private static string GenerateCustomQuerySignatures(EntityInfo entity)
    {
        var sb = new StringBuilder();
        foreach (var query in entity.QueryProperties)
        {
            sb.AppendLine($"    Task<List<{entity.EntityName}>> {query.MethodName}Async({query.PropertyType} {ToCamelCase(query.PropertyName)}, CancellationToken ct);");
        }
        return sb.ToString().TrimEnd();
    }

    private static string GenerateCustomQueryMethods(EntityInfo entity)
    {
        var sb = new StringBuilder();
        foreach (var query in entity.QueryProperties)
        {
            var paramName = ToCamelCase(query.PropertyName);
            sb.AppendLine($$"""
    /// <summary>
    /// Gets {{entity.EntityName}} entities by {{query.PropertyName}}.
    /// </summary>
    public static K<M, List<{{entity.EntityName}}>> {{ToCamelCase(query.MethodName)}}({{query.PropertyType}} {{paramName}}) =>
        from repo in Has<M, RT, I{{entity.EntityName}}Repository>.ask
        from entities in M.LiftIO(IO.liftAsync(env =>
            repo.{{query.MethodName}}Async({{paramName}}, env.Token)))
        select entities;

""");
        }
        return sb.ToString().TrimEnd();
    }

    private static string GenerateTestCustomQueries(EntityInfo entity)
    {
        var sb = new StringBuilder();
        foreach (var query in entity.QueryProperties)
        {
            var paramName = ToCamelCase(query.PropertyName);
            sb.AppendLine($$"""
    public Task<List<{{entity.EntityName}}>> {{query.MethodName}}Async({{query.PropertyType}} {{paramName}}, CancellationToken ct)
    {
        var result = _entities.Values
            .Where(e => {{GetEqualityCheck($"e.{query.PropertyName}", paramName, query.PropertyType)}})
            .ToList();
        return Task.FromResult(result);
    }

""");
        }
        return sb.ToString().TrimEnd();
    }

    private static string GenerateLiveCustomQueries(EntityInfo entity)
    {
        var sb = new StringBuilder();
        foreach (var query in entity.QueryProperties)
        {
            var paramName = ToCamelCase(query.PropertyName);
            sb.AppendLine($$"""
    public async Task<List<{{entity.EntityName}}>> {{query.MethodName}}Async({{query.PropertyType}} {{paramName}}, CancellationToken ct)
    {
        return await context.{{entity.DbSetName}}
            .AsNoTracking()
            .Where(e => e.{{query.PropertyName}} == {{paramName}})
            .ToListAsync(ct);
    }

""");
        }
        return sb.ToString().TrimEnd();
    }

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

    private static string GetEqualityCheck(string left, string right, string type)
    {
        return type switch
        {
            "string" => $"{left}.Equals({right}, StringComparison.OrdinalIgnoreCase)",
            _ => $"{left} == {right}"
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
    public required ImmutableArray<QueryPropertyInfo> QueryProperties { get; init; }
}

record PropertyInfo
{
    public required string Name { get; init; }
    public required string Type { get; init; }
}

record QueryPropertyInfo
{
    public required string PropertyName { get; init; }
    public required string PropertyType { get; init; }
    public required string MethodName { get; init; }
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

### After (Generated - 3 lines per entity!)

```csharp
// ✅ Automated: Just annotate your entity!

[GenerateRepository(DbSetName = "Todos")]
public record Todo
{
    [RepositoryKey]
    public int Id { get; init; }

    public string Title { get; init; } = string.Empty;

    [RepositoryQuery(MethodName = "GetTodosByUser")]
    public int UserId { get; init; }

    public DateTime CreatedAt { get; init; }
}

// That's it! All 4 files generated automatically at compile-time:
// ✅ ITodoRepository.g.cs
// ✅ TodoRepository.g.cs
// ✅ TestTodoRepository.g.cs
// ✅ LiveTodoRepository.g.cs
```

### Using Generated Code

```csharp
// Application/Todos/TodoService.cs
public static class TodoService<M, RT>
    where M : Monad<M>, MonadIO<M>, Fallible<M>
    where RT : Has<M, ITodoRepository>,    // ✅ Generated interface
               Has<M, IUnitOfWork>,
               Has<M, ILoggerIO>
{
    public static K<M, Todo> CreateTodo(string title) =>
        from todo in M.Pure(new Todo { Title = title })
        from _ in TodoRepository<M, RT>.addTodo(todo)    // ✅ Generated capability
        from __ in UnitOfWork<M, RT>.saveChanges
        select todo;

    // ✅ Custom query generated from [RepositoryQuery] attribute
    public static K<M, List<Todo>> GetUserTodos(int userId) =>
        TodoRepository<M, RT>.getTodosByUser(userId);
}

// Tests
[Test]
public async Task Test()
{
    var runtime = new TestRuntime();

    // ✅ Generated test repository with helpers
    runtime.TodoRepository.Seed(new Todo { Id = 1, Title = "Test" });

    var result = await TodoService<Eff<TestRuntime>, TestRuntime>
        .GetUserTodos(1)
        .RunAsync(runtime, EnvIO.New());

    Assert.AreEqual(1, runtime.TodoRepository.Count);  // ✅ Generated helper
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
// Step 1: Define entity
[GenerateRepository]
public record Comment
{
    [RepositoryKey]
    public int Id { get; init; }

    [RepositoryQuery]
    public int PostId { get; init; }

    [RepositoryQuery]
    public int UserId { get; init; }

    public string Text { get; init; } = string.Empty;
}

// Step 2 & 3: Update runtimes (just add one line each)
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

// Done! 100+ lines of code generated automatically
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

## Advanced: Custom Repository Methods

For entity-specific logic that doesn't fit the pattern, use partial classes:

```csharp
// Infrastructure/Traits/ITodoRepository.Custom.cs
public partial interface ITodoRepository
{
    // ✅ Custom method not generated
    Task<List<Todo>> GetOverdueTodosAsync(CancellationToken ct);
}

// Infrastructure/Live/LiveTodoRepository.Custom.cs
public partial class LiveTodoRepository
{
    // ✅ Custom implementation
    public async Task<List<Todo>> GetOverdueTodosAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return await context.Todos
            .Where(t => !t.IsCompleted && t.DueDate < now)
            .ToListAsync(ct);
    }
}
```

## Benefits

### 1. Massive Code Reduction
- **Before:** ~140 lines per entity
- **After:** ~10 lines per entity
- **Savings:** 93% less boilerplate!

### 2. Consistency
- All repositories follow exact same pattern
- No copy-paste errors
- Changes propagate automatically

### 3. Compile-Time Safety
- Generated at compile-time (not runtime)
- Full IntelliSense support
- Type-safe generated code

### 4. Easy Maintenance
- Change generator once, affects all entities
- Pattern improvements apply everywhere
- Refactoring made simple

### 5. Developer Productivity
- Add new entity in 5 minutes instead of 30
- Focus on business logic, not boilerplate
- Less context switching

## Summary

Source generators eliminate repository boilerplate:

✅ **3 lines** to generate what used to take 140 lines
✅ **Compile-time** code generation with full type safety
✅ **Automatic** trait, capability, test, and live implementations
✅ **Consistent** pattern across all entities
✅ **Extensible** with partial classes for custom logic
✅ **Productive** - add entities in minutes, not hours

**For a project with 10 entities:**
- **Manual:** ~1,400 lines of repetitive code
- **Generated:** ~100 lines of attributes + 1 generator
- **Time saved:** Hours → Minutes

This is the power of meta-programming in functional architecture!
