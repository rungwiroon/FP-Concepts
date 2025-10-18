namespace TodoApp.Infrastructure.Traits;

/// <summary>
/// CancellationToken capability trait.
/// Provides access to the cancellation token for the current operation.
/// </summary>
public interface CancellationTokenIO
{
    CancellationToken GetCancellationToken();
}
