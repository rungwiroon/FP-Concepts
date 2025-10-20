using LanguageExt;
using TodoApp.Data;
using TodoApp.Infrastructure.Traits;

namespace TodoApp.Infrastructure.Live;

/// <summary>
/// Production implementation of DatabaseIO wrapping AppDbContext.
/// Simple wrapper - no IO or Eff wrapping needed!
/// </summary>
public class LiveDatabaseIO(AppDbContext context) : DatabaseIO
{
    public AppDbContext GetContext() => context;

    public async Task<Unit> SaveChangesAsync(CancellationToken cancellationToken)
    {
        await context.SaveChangesAsync(cancellationToken);

        return Unit.Default;
    }
        
}
