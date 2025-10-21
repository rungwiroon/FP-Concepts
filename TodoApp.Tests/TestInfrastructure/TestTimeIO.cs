using LanguageExt;
using TodoApp.Infrastructure.Traits;

namespace TodoApp.Tests.TestInfrastructure;

/// <summary>
/// Test implementation of TimeIO that allows controlling the current time for tests.
/// </summary>
public class TestTimeIO : TimeIO
{
    private DateTime _currentTime;

    public TestTimeIO(DateTime? initialTime = null)
    {
        _currentTime = initialTime ?? new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    }

    public DateTime GetUtcNow() => _currentTime;

    /// <summary>
    /// Set the current time for testing
    /// </summary>
    public void SetTime(DateTime time)
    {
        _currentTime = time;
    }

    /// <summary>
    /// Advance time by a given duration
    /// </summary>
    public void AdvanceTime(TimeSpan duration)
    {
        _currentTime = _currentTime.Add(duration);
    }
}
