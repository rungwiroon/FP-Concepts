using TodoApp.Data;

namespace TodoApp.Infrastructure;

public record DbEnv(
    AppDbContext DbContext,
    CancellationToken CancellationToken,
    ILogger Logger
);

public static class DbEnvExtensions
{
    public static DbEnv Create(
        AppDbContext ctx,
        CancellationToken ct,
        ILogger logger) =>
        new DbEnv(ctx, ct, logger);
}
