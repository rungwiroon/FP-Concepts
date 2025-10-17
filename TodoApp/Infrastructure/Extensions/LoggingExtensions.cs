using LanguageExt;
using LanguageExt.Traits;
using static LanguageExt.Prelude;

namespace TodoApp.Infrastructure.Extensions;

public static class LoggingExtensions
{
    public static K<Db, Unit> LogInfo(string message) =>
        from logger in Db.Logger()
        from _ in Db.LiftIO(() =>
        {
            logger.LogInformation(message);
            return Task.FromResult(unit);
        })
        select unit;

    public static K<Db, Unit> LogError(string message) =>
        from logger in Db.Logger()
        from _ in Db.LiftIO(() =>
        {
            logger.LogError(message);
            return Task.FromResult(unit);
        })
        select unit;

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
