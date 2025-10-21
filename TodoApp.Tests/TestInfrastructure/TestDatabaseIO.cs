using LanguageExt;
using Microsoft.EntityFrameworkCore;
using TodoApp.Data;
using TodoApp.Infrastructure.Traits;

namespace TodoApp.Tests.TestInfrastructure;

/// <summary>
/// Test implementation of DatabaseIO using in-memory database.
/// Creates a new in-memory database for each test instance.
/// </summary>
public class TestDatabaseIO : DatabaseIO, IDisposable
{
    private readonly AppDbContext _context;

    public TestDatabaseIO(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        _context = new AppDbContext(options);
    }

    public AppDbContext GetContext() => _context;

    public Task<Unit> SaveChangesAsync(CancellationToken cancellationToken) =>
        _context.SaveChangesAsync(cancellationToken)
            .Map(_ => Unit.Default);

    public void Dispose()
    {
        _context?.Dispose();
    }

    /// <summary>
    /// Get the DbContext for seeding test data
    /// </summary>
    public AppDbContext Context => _context;
}
