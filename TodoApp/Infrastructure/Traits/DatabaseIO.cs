using LanguageExt;
using TodoApp.Data;

namespace TodoApp.Infrastructure.Traits;

/// <summary>
/// Database capability trait.
/// Simple interface with async methods - returns Task, not IO!
/// </summary>
public interface DatabaseIO
{
    AppDbContext GetContext();
    Task<Unit> SaveChangesAsync(CancellationToken cancellationToken);
}
