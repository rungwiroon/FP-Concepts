using LanguageExt;
using LanguageExt.Traits;
using Microsoft.Extensions.Logging;
using TodoApp.Data;
using TodoApp.Infrastructure.Live;
using TodoApp.Infrastructure.Traits;
using static LanguageExt.Prelude;

namespace TodoApp.Infrastructure;

/// <summary>
/// Application runtime implementing multiple capability traits.
/// Uses the Has<M, Trait> pattern from language-ext v5 with Has<M, RT, T>.ask (3 params).
/// </summary>
public record AppRuntime(IServiceProvider Services) :
    Has<Eff<AppRuntime>, LoggerIO>,
    Has<Eff<AppRuntime>, DatabaseIO>,
    Has<Eff<AppRuntime>, CancellationTokenIO>,
    Has<Eff<AppRuntime>, TimeIO>
{
    /// <summary>
    /// Implements Has<Eff<AppRuntime>, LoggerIO> by lifting the live implementation into an effect.
    /// Uses liftEff to wrap the concrete implementation.
    /// </summary>
    static K<Eff<AppRuntime>, LoggerIO> Has<Eff<AppRuntime>, LoggerIO>.Ask =>
        liftEff((Func<AppRuntime, LoggerIO>)(rt =>
        {
            var logger = rt.Services.GetService(typeof(ILogger<AppRuntime>)) as ILogger<AppRuntime>
                ?? throw new InvalidOperationException("ILogger<AppRuntime> not registered in DI container");
            return new LiveLoggerIO(logger);
        }));

    /// <summary>
    /// Implements Has<Eff<AppRuntime>, DatabaseIO> by lifting the live implementation into an effect.
    /// Uses liftEff to wrap the concrete implementation.
    /// </summary>
    static K<Eff<AppRuntime>, DatabaseIO> Has<Eff<AppRuntime>, DatabaseIO>.Ask =>
        liftEff((Func<AppRuntime, DatabaseIO>)(rt =>
        {
            var context = rt.Services.GetService(typeof(AppDbContext)) as AppDbContext
                ?? throw new InvalidOperationException("AppDbContext not registered in DI container");
            var ct = rt.Services.GetService(typeof(CancellationToken)) is CancellationToken token
                ? token
                : CancellationToken.None;
            return new LiveDatabaseIO(context, ct);
        }));

    /// <summary>
    /// Implements Has<Eff<AppRuntime>, CancellationTokenIO> by lifting the live implementation into an effect.
    /// Uses liftEff to wrap the concrete implementation.
    /// </summary>
    static K<Eff<AppRuntime>, CancellationTokenIO> Has<Eff<AppRuntime>, CancellationTokenIO>.Ask =>
        liftEff((Func<AppRuntime, CancellationTokenIO>)(rt =>
        {
            var ct = rt.Services.GetService(typeof(CancellationToken)) is CancellationToken token
                ? token
                : CancellationToken.None;
            return new LiveCancellationTokenIO(ct);
        }));

    /// <summary>
    /// Implements Has<Eff<AppRuntime>, TimeIO> by lifting the live implementation into an effect.
    /// Uses liftEff to wrap the concrete implementation.
    /// </summary>
    static K<Eff<AppRuntime>, TimeIO> Has<Eff<AppRuntime>, TimeIO>.Ask =>
        liftEff((Func<AppRuntime, TimeIO>)(rt => new LiveTimeIO()));
}
