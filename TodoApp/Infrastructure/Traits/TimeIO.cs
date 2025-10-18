namespace TodoApp.Infrastructure.Traits;

/// <summary>
/// Trait for time operations (pure functional approach to DateTime)
/// </summary>
public interface TimeIO
{
    DateTime GetUtcNow();
}
