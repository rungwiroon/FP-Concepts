using TodoApp.Data;
using TodoApp.Infrastructure.Traits;

namespace TodoApp.Infrastructure.Live;

/// <summary>
/// Production implementation of DatabaseIO wrapping AppDbContext.
/// Simple wrapper - no IO or Eff wrapping needed!
/// </summary>
public class LiveDatabaseIO(AppDbContext context, CancellationToken cancellationToken) : DatabaseIO
{
    public Task<AppDbContext> GetContextAsync() =>
        Task.FromResult(context);

    public Task<CancellationToken> GetCancellationTokenAsync() =>
        Task.FromResult(cancellationToken);

    public Task SaveChangesAsync() =>
        context.SaveChangesAsync(cancellationToken);
}
