using TodoApp.Infrastructure.Traits;

namespace TodoApp.Infrastructure.Live;

/// <summary>
/// Live implementation of CancellationTokenIO.
/// Provides access to a real cancellation token.
/// </summary>
public class LiveCancellationTokenIO : CancellationTokenIO
{
    private readonly CancellationToken _cancellationToken;

    public LiveCancellationTokenIO(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
    }

    public CancellationToken GetCancellationToken() => _cancellationToken;
}
