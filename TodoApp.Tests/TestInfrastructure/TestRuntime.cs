using LanguageExt;
using LanguageExt.Traits;
using TodoApp.Infrastructure.Traits;
using static LanguageExt.Prelude;

namespace TodoApp.Tests.TestInfrastructure;

/// <summary>
/// Test runtime that provides test implementations of all traits.
/// No EF Core - pure in-memory implementations for true unit testing!
/// </summary>
public class TestRuntime :
    Has<Eff<TestRuntime>, DatabaseIO>,
    Has<Eff<TestRuntime>, LoggerIO>,
    Has<Eff<TestRuntime>, TimeIO>
{
    public TestDatabaseIO Database { get; }
    public TestLoggerIO Logger { get; }
    public TestTimeIO Time { get; }

    public TestRuntime(DateTime? currentTime = null)
    {
        Database = new TestDatabaseIO();
        Logger = new TestLoggerIO();
        Time = new TestTimeIO(currentTime);
    }

    // Implement Has<M, T>.ask for DatabaseIO
    static K<Eff<TestRuntime>, DatabaseIO> Has<Eff<TestRuntime>, DatabaseIO>.Ask =>
        liftEff((Func<TestRuntime, DatabaseIO>)(rt => rt.Database));

    // Implement Has<M, T>.ask for LoggerIO
    static K<Eff<TestRuntime>, LoggerIO> Has<Eff<TestRuntime>, LoggerIO>.Ask =>
        liftEff((Func<TestRuntime, LoggerIO>)(rt => rt.Logger));

    // Implement Has<M, T>.ask for TimeIO
    static K<Eff<TestRuntime>, TimeIO> Has<Eff<TestRuntime>, TimeIO>.Ask =>
        liftEff((Func<TestRuntime, TimeIO>)(rt => rt.Time));
}
