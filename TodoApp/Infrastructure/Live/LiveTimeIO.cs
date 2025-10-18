using TodoApp.Infrastructure.Traits;

namespace TodoApp.Infrastructure.Live;

/// <summary>
/// Live implementation of TimeIO that returns actual system time
/// </summary>
public class LiveTimeIO : TimeIO
{
    public DateTime GetUtcNow() => DateTime.UtcNow;
}
